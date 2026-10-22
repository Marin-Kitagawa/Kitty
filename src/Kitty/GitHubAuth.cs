using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Kitty;

/// <summary>A pending device-flow authorization: what to show the user, and what to poll with.</summary>
public sealed record DeviceCodeInfo(string DeviceCode, string UserCode, string VerificationUri, int ExpiresInSeconds, int IntervalSeconds);

/// <summary>An OAuth access token plus, when the OAuth app expires tokens, its refresh pair.</summary>
public sealed record OAuthToken(string AccessToken, string? RefreshToken, int? ExpiresInSeconds, string? Scope)
{
    /// <summary>Null when the app issues non-expiring tokens (expiry disabled in the app settings).</summary>
    public DateTimeOffset? ExpiresAtUtc =>
        ExpiresInSeconds is int seconds ? DateTimeOffset.UtcNow.AddSeconds(seconds) : null;
}

/// <summary>A device-flow failure that is worth showing the user verbatim.</summary>
public sealed class GitHubAuthException(string message, string? error = null) : Exception(message)
{
    /// <summary>The OAuth `error` code, when GitHub supplied one.</summary>
    public string? Error { get; } = error;
}

/// <summary>
/// GitHub OAuth *device flow* (RFC 8628) as a public client: no client secret, no
/// loopback listener. The app shows a code, the user approves it on github.com, and
/// <see cref="PollForTokenAsync"/> collects the token. Enabling "Enable Device Flow"
/// on the OAuth app is a prerequisite.
/// </summary>
public static class GitHubAuth
{
    /// <summary>
    /// Endpoints are properties rather than constants so the flow can be exercised against a
    /// local stub (and pointed at another host if that is ever wanted).
    /// </summary>
    public static string DeviceCodeEndpoint { get; set; } = "https://github.com/login/device/code";
    public static string TokenEndpoint { get; set; } = "https://github.com/login/oauth/access_token";
    public static string UserEndpoint { get; set; } = "https://api.github.com/user";

    /// <summary>Environment override, so a client ID can be used without editing settings.json.</summary>
    public const string ClientIdEnvironmentVariable = "KITTY_GITHUB_CLIENT_ID";

    private const string DeviceGrantType = "urn:ietf:params:oauth:grant-type:device_code";

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Kitty-trending/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return c;
    }

    public static string? ClientIdFromEnvironment =>
        Environment.GetEnvironmentVariable(ClientIdEnvironmentVariable) is { Length: > 0 } value ? value.Trim() : null;

    /// <summary>Starts an authorization: returns the code to show and the URL to open.</summary>
    public static async Task<DeviceCodeInfo> RequestDeviceCodeAsync(string clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            throw new GitHubAuthException(
                "No OAuth client ID is configured. Register a GitHub OAuth app with \"Enable Device Flow\" on and paste its client ID.",
                "missing_client_id");

        using var resp = await PostFormAsync(DeviceCodeEndpoint,
            new Dictionary<string, string> { ["client_id"] = clientId }, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = TryParse(body, resp);
        var root = doc.RootElement;

        var deviceCode = GetString(root, "device_code");
        var userCode = GetString(root, "user_code");
        if (deviceCode is null || userCode is null)
        {
            var error = GetString(root, "error");
            throw new GitHubAuthException(DescribeError(error, resp, body, root), error);
        }

        return new DeviceCodeInfo(
            deviceCode,
            userCode,
            GetString(root, "verification_uri") ?? "https://github.com/login/device",
            GetInt(root, "expires_in") ?? 900,
            Math.Max(1, GetInt(root, "interval") ?? 5));
    }

    /// <summary>
    /// Polls until the user approves the code, the code expires, or the request is denied.
    /// Waits <paramref name="intervalSeconds"/> between attempts and backs off on `slow_down`,
    /// as GitHub requires.
    /// </summary>
    public static async Task<OAuthToken> PollForTokenAsync(
        string clientId,
        string deviceCode,
        int intervalSeconds,
        int expiresInSeconds,
        Action<int>? onIntervalChanged,
        CancellationToken ct)
    {
        var interval = Math.Max(1, intervalSeconds);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, expiresInSeconds));

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (DateTimeOffset.UtcNow >= deadline)
                throw new GitHubAuthException("The sign-in code expired before it was approved — start sign-in again.", "expired_token");

            await Task.Delay(TimeSpan.FromSeconds(interval), ct);

            using var resp = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = DeviceGrantType
            }, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = TryParse(body, resp);
            var root = doc.RootElement;

            if (GetString(root, "access_token") is { Length: > 0 } accessToken)
                return new OAuthToken(accessToken, GetString(root, "refresh_token"), GetInt(root, "expires_in"), GetString(root, "scope"));

            switch (GetString(root, "error"))
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval += 5;
                    onIntervalChanged?.Invoke(interval);
                    continue;
                case "access_denied":
                    throw new GitHubAuthException("Sign-in was denied on GitHub.", "access_denied");
                case "expired_token":
                    throw new GitHubAuthException("The sign-in code expired — start sign-in again.", "expired_token");
                case "device_flow_disabled":
                    throw new GitHubAuthException(
                        "Device flow is disabled for this OAuth app — turn on \"Enable Device Flow\" in its GitHub settings.",
                        "device_flow_disabled");
                case "incorrect_device_code":
                    throw new GitHubAuthException("GitHub rejected the sign-in code — start sign-in again.", "incorrect_device_code");
                case "incorrect_client_credentials":
                    throw new GitHubAuthException("The OAuth client ID was rejected by GitHub — check it and try again.", "incorrect_client_credentials");
                default:
                    var failure = GetString(root, "error");
                    throw new GitHubAuthException(DescribeError(failure, resp, body, root), failure);
            }
        }
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token. Device-flow refresh needs no client
    /// secret (rotating: the old pair stops working).
    /// </summary>
    public static async Task<OAuthToken> RefreshAsync(string clientId, string refreshToken, CancellationToken ct)
    {
        using var resp = await PostFormAsync(TokenEndpoint, new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = TryParse(body, resp);
        var root = doc.RootElement;

        if (GetString(root, "access_token") is { Length: > 0 } accessToken)
            return new OAuthToken(accessToken, GetString(root, "refresh_token") ?? refreshToken, GetInt(root, "expires_in"), GetString(root, "scope"));

        var error = GetString(root, "error");
        throw new GitHubAuthException(DescribeError(error, resp, body, root), error);
    }

    /// <summary>
    /// Reads the signed-in login. Returns null when the token is not accepted (401) or the
    /// request fails, so callers can tell "no answer" from "rejected".
    /// </summary>
    public static async Task<LoginProbe> ProbeLoginAsync(string accessToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, UserEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var resp = await Http.SendAsync(req, ct);

        if (resp.StatusCode is HttpStatusCode.Unauthorized)
            return new LoginProbe(null, true);

        if (!resp.IsSuccessStatusCode) return new LoginProbe(null, false);

        var body = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        return new LoginProbe(GetString(doc.RootElement, "login"), false);
    }

    /// <summary>Login when GitHub answered, and whether the token was rejected.</summary>
    public sealed record LoginProbe(string? Login, bool Rejected);

    private static async Task<HttpResponseMessage> PostFormAsync(
        string endpoint, Dictionary<string, string> fields, CancellationToken ct)
    {
        using var form = new FormUrlEncodedContent(fields);
        // Disposing the form is fine: PostAsync buffers the content before sending.
        return await Http.PostAsync(endpoint, form, ct);
    }

    private static JsonDocument TryParse(string body, HttpResponseMessage resp)
    {
        try
        {
            return JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new GitHubAuthException(
                $"GitHub returned an unexpected response ({(int)resp.StatusCode} {resp.ReasonPhrase}).");
        }
    }

    /// <summary>
    /// Turns an OAuth failure into something the user can act on. GitHub answers an unknown
    /// client ID with a bare 404, which is useless on its own.
    /// </summary>
    private static string DescribeError(string? error, HttpResponseMessage resp, string body, JsonElement root)
    {
        switch (error)
        {
            case "device_flow_disabled":
                return "Device flow is disabled for this OAuth app — turn on \"Enable Device Flow\" in its GitHub settings.";
            case "incorrect_client_credentials":
                return "GitHub rejected the OAuth client ID — check it against your OAuth app's settings.";
            case null or "Not Found" when resp.StatusCode is HttpStatusCode.NotFound:
                return "GitHub didn't recognise that OAuth client ID (404) — check the client ID in your OAuth app's settings.";
            default:
                return Failure(resp, body, root);
        }
    }

    private static string Failure(HttpResponseMessage resp, string body, JsonElement root)
    {
        var description = GetString(root, "error_description");
        var error = GetString(root, "error");
        if (description is { Length: > 0 }) return $"{description} ({error ?? "error"})";
        if (error is { Length: > 0 }) return $"GitHub refused the request ({error}).";
        return $"GitHub refused the request ({(int)resp.StatusCode} {resp.ReasonPhrase}).";
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        // Some OAuth fields come back as strings.
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)) return parsed;
        return null;
    }
}
