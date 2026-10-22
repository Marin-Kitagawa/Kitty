using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kitty;

public partial class RepoRowViewModel : ObservableObject
{
    public RepoItem Item { get; }
    public RepoRowViewModel(RepoItem item) => Item = item;

    public string FullName => Item.FullName;
    public string Description => string.IsNullOrEmpty(Item.Description) ? "No description provided." : Item.Description;
    public bool HasDescription => !string.IsNullOrEmpty(Item.Description);
    public string Language => Item.Language;
    public string LanguageColor => LanguageColors.For(Item.Language, Item.LanguageColor);
    public bool HasLanguage => !string.IsNullOrEmpty(Item.Language);
    public string StarsDisplay => FormatCount(Item.Stars);
    public string ForksDisplay => FormatCount(Item.Forks);
    public string HtmlUrl => Item.HtmlUrl;
    public string CloneUrl => Item.CloneUrl;

    public string RankDisplay => Rank is int r ? $"{r + 1:000}" : "···";
    [ObservableProperty] private int? _rank;

    [ObservableProperty] private string? _periodStarsDisplay;
    [ObservableProperty] private bool _hasPeriodStars;
    [ObservableProperty] private string _periodUnit = "today";

    public void SetPeriod(int? period, TrendRange range)
    {
        HasPeriodStars = period is int p && p > 0;
        PeriodStarsDisplay = HasPeriodStars ? $"+{FormatCount(period!.Value)}" : null;
        PeriodUnit = range switch
        {
            TrendRange.Daily => "today",
            TrendRange.Weekly => "this week",
            _ => "this month"
        };
    }

    partial void OnRankChanged(int? value) => OnPropertyChanged(nameof(RankDisplay));

    private static string FormatCount(int n) => n >= 10000 ? $"{n / 1000.0:0.#}k" : n.ToString("N0");
}

public partial class MainViewModel : ObservableObject
{
    private readonly GitHubService _github = new();
    private CancellationTokenSource? _cts;
    private DispatcherTimer? _feedbackTimer;

    public ObservableCollection<LanguageOption> LanguagesList { get; } = new();
    public ObservableCollection<LanguageOption> VisibleLanguages { get; } = new();
    public ObservableCollection<RepoRowViewModel> VisibleRepos { get; } = new();
    private List<RepoRowViewModel> _allRepos = [];

    [ObservableProperty] private LanguageOption _selectedLanguage;
    [ObservableProperty] private TrendRange _range = TrendRange.Daily;

    public string RangeText => Range switch
    {
        TrendRange.Daily => "today",
        TrendRange.Weekly => "this week",
        _ => "this month"
    };
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private string _railFilter = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _warningText;
    [ObservableProperty] private string _countText = "";

    /// <summary>Sentence-case form of CountText for the page subtitle.</summary>
    public string CountSentence => CountText.ToLowerInvariant();

    partial void OnCountTextChanged(string value) => OnPropertyChanged(nameof(CountSentence));
    [ObservableProperty] private string _countShort = "0";
    [ObservableProperty] private string _sourceBadge = "";
    [ObservableProperty] private string _statusRight = "starting…";
    [ObservableProperty] private string _tokenLabel = "anonymous";

    /// <summary>True when a GitHub token is actually stored and applied (not merely typed).</summary>
    [ObservableProperty] private bool _hasToken;
    [ObservableProperty] private string _authStatusText = "NOT SIGNED IN";
    [ObservableProperty] private string _authStatusDetail = "requests are anonymous";
    /// <summary>Set when GitHub answered 401 for the stored token.</summary>
    [ObservableProperty] private bool _tokenRejected;

    // Settings panel state
    [ObservableProperty] private bool _scrapePrimary;
    [ObservableProperty] private string _tokenInput = "";
    [ObservableProperty] private string _settingsHint = "";

    // Device-flow sign-in state
    [ObservableProperty] private bool _isSigningIn;
    [ObservableProperty] private string _deviceUserCode = "";
    [ObservableProperty] private string _signInUrl = "https://github.com/login/device";
    [ObservableProperty] private string _signInHint = "";
    [ObservableProperty] private string _clientIdInput = "";
    [ObservableProperty] private bool _showClientIdField;

    /// <summary>Raised when a fetch replaced the list — the view plays the drawer-load stagger.</summary>
    public event Action? ListArrived;

    public MainViewModel()
    {
        var settings = SettingsService.Instance.Current;
        _selectedLanguage = new LanguageOption(settings.LastLanguage, settings.LastLanguageSlug);
        _lastGoodLanguage = _selectedLanguage;
        _scrapePrimary = settings.SourceMode == SourceMode.ScrapePrimary;
        _tokenInput = settings.Token ?? "";
        _clientIdInput = settings.OAuthClientId ?? GitHubAuth.ClientIdFromEnvironment ?? "";
        _showClientIdField = _clientIdInput.Length == 0;

        LanguagesList.Add(GitHubLanguages.Any);
        foreach (var l in GitHubLanguages.Fallback) LanguagesList.Add(l);
        RebuildVisibleLanguages();
        _github.ApplyToken(settings.Token);
        UpdateTokenLabel();
        _ = LoadLanguagesAsync();
        _ = VerifyStoredTokenAsync();
        _ = RefreshAsync();
    }

    public Action<string>? ClipboardSetter { get; set; }

    private async Task LoadLanguagesAsync()
    {
        try
        {
            var live = await GitHubLanguages.FetchLiveAsync(default);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var keep = SelectedLanguage;
                LanguagesList.Clear();
                LanguagesList.Add(GitHubLanguages.Any);
                foreach (var l in live) LanguagesList.Add(l);
                RebuildVisibleLanguages();
                if (!LanguagesList.Contains(keep)) SelectedLanguage = GitHubLanguages.Any;
            });
        }
        catch { /* fallback list already in place */ }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsLoading = true;
        WarningText = null;
        StatusRight = "fetching…";
        try
        {
            // A device-flow token lasts hours only — renew it before spending a request on it.
            await EnsureFreshTokenAsync();
            var result = await _github.FetchTrendingAsync(SelectedLanguage, Range, ct);
            ct.ThrowIfCancellationRequested();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _allRepos = result.Repos.Select(r => new RepoRowViewModel(r)).ToList();
                ApplyFilter(announce: true);
                SourceBadge = result.Source == "search-api"
                    ? "SOURCE · SEARCH API · approximate ranking"
                    : "SOURCE · TRENDING PAGES · true ranking";
                WarningText = result.Warning;
                StatusRight = $"{DateTime.Now:H:mm} · {TokenLabel}";
                SettingsService.Instance.Current.LastLanguage =
                    SelectedLanguage.Slug.Length == 0 ? "Any language" : SelectedLanguage.Label;
                SettingsService.Instance.Current.LastLanguageSlug = SelectedLanguage.Slug;
                SettingsService.Instance.Save();
            });
        }
        catch (OperationCanceledException) { }
        catch (RateLimitException ex)
        {
            StatusRight = "rate limited";
            WarningText = ex.Message + " Add a token in settings, or switch the source to trending pages.";
        }
        catch (Exception ex)
        {
            StatusRight = "failed";
            WarningText = $"Couldn't reach GitHub ({ex.Message}). Check your connection or switch the source in settings.";
            try { System.IO.File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kitty", "crash.log"), DateTime.Now + " " + ex + "\n\n"); } catch { }
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void OpenRepo(RepoRowViewModel? row)
    {
        if (row is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(row.HtmlUrl) { UseShellExecute = true });
        }
        catch (Exception ex) { WarningText = $"Couldn't open the browser ({ex.Message})."; }
    }

    [RelayCommand]
    private void CopyCloneUrl(RepoRowViewModel? row)
    {
        if (row is null) return;
        ClipboardSetter?.Invoke(row.CloneUrl);
        StatusRight = $"clone URL copied — {row.FullName}";
        _feedbackTimer?.Stop();
        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _feedbackTimer.Tick += (_, _) => { StatusRight = $"{DateTime.Now:H:mm} · {TokenLabel}"; _feedbackTimer!.Stop(); };
        _feedbackTimer.Start();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var s = SettingsService.Instance.Current;
        s.SourceMode = ScrapePrimary ? SourceMode.ScrapePrimary : SourceMode.ApiFirst;
        if (ClientIdInput?.Trim() is { Length: > 0 } clientId) s.OAuthClientId = clientId;
        else if (ResolveClientId().Length == 0) s.OAuthClientId = null;

        var token = TokenInput?.Trim();
        if (!string.IsNullOrEmpty(token)
            && !token.StartsWith("ghp_", StringComparison.OrdinalIgnoreCase)
            && !token.StartsWith("github_pat_", StringComparison.OrdinalIgnoreCase))
        {
            SettingsHint = "That doesn't look like a GitHub token — it should start with ghp_ or github_pat_. For sign-in without a token, use \"Sign in with GitHub\" instead.";
            return;
        }

        // A pasted token replaces whatever credential was there, including OAuth metadata.
        s.Token = string.IsNullOrEmpty(token) ? null : token;
        s.RefreshToken = null;
        s.TokenExpiresAtUtc = null;
        s.Login = null;
        TokenRejected = false;
        _github.ApplyToken(s.Token);
        UpdateTokenLabel();
        SettingsService.Instance.Save();
        SettingsHint = s.Token is null
            ? "Saved — fetching anonymously (rate-limited to 10 searches a minute)."
            : "Saved — checking the token with GitHub…";
        _ = VerifyStoredTokenAsync();
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void SignOut()
    {
        _signInCts?.Cancel();
        TokenInput = "";
        var s = SettingsService.Instance.Current;
        s.Token = null;
        s.RefreshToken = null;
        s.TokenExpiresAtUtc = null;
        s.Login = null;
        TokenRejected = false;
        _github.ApplyToken(null);
        UpdateTokenLabel();
        SettingsService.Instance.Save();
        SignInHint = "";
        SettingsHint = "Signed out — fetching anonymously.";
    }

    // ── Sign in with GitHub (OAuth device flow: no token to paste, no client secret) ──

    [RelayCommand]
    private async Task SignInAsync()
    {
        var clientId = ResolveClientId();
        if (clientId.Length == 0)
        {
            ShowClientIdField = true;
            SignInHint = "Sign-in needs a GitHub OAuth app. Create one at GitHub → Settings → Developer settings → OAuth apps → New OAuth app, tick \"Enable Device Flow\", then paste its client ID here (or set " + GitHubAuth.ClientIdEnvironmentVariable + ").";
            return;
        }

        _signInCts?.Cancel();
        _signInCts = new CancellationTokenSource();
        var ct = _signInCts.Token;
        IsSigningIn = true;
        TokenRejected = false;
        SignInHint = "Contacting GitHub…";
        UpdateTokenLabel();

        try
        {
            var code = await GitHubAuth.RequestDeviceCodeAsync(clientId, ct);
            DeviceUserCode = code.UserCode;
            SignInUrl = code.VerificationUri;
            SignInHint = $"Waiting for approval — checking every {code.IntervalSeconds}s.";
            OpenUrl(code.VerificationUri);

            var token = await GitHubAuth.PollForTokenAsync(
                clientId, code.DeviceCode, code.IntervalSeconds, code.ExpiresInSeconds,
                interval => SignInHint = $"Still waiting for approval — checking every {interval}s.", ct);

            var s = SettingsService.Instance.Current;
            s.Token = token.AccessToken;
            s.RefreshToken = token.RefreshToken;
            s.TokenExpiresAtUtc = token.ExpiresAtUtc;
            s.Login = null;
            s.OAuthClientId = clientId;
            _github.ApplyToken(s.Token);
            SettingsService.Instance.Save();
            SignInHint = "";
            SettingsHint = "Signed in with GitHub.";
            UpdateTokenLabel();
            await VerifyStoredTokenAsync();
            _ = RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            SignInHint = "Sign-in cancelled.";
        }
        catch (GitHubAuthException ex)
        {
            SignInHint = ex.Message;
        }
        catch (Exception ex)
        {
            SignInHint = $"Sign-in failed ({ex.Message}).";
        }
        finally
        {
            IsSigningIn = false;
            DeviceUserCode = "";
            UpdateTokenLabel();
        }
    }

    [RelayCommand]
    private void CancelSignIn()
    {
        _signInCts?.Cancel();
        SignInHint = "Sign-in cancelled.";
    }

    [RelayCommand]
    private void OpenDevicePage() => OpenUrl(SignInUrl.Length > 0 ? SignInUrl : "https://github.com/login/device");

    [RelayCommand]
    private void CopyDeviceCode()
    {
        if (DeviceUserCode.Length == 0) return;
        ClipboardSetter?.Invoke(DeviceUserCode);
        SignInHint = "Code copied — paste it on the GitHub page.";
    }

    [RelayCommand]
    private void ShowClientId() => ShowClientIdField = true;

    private CancellationTokenSource? _signInCts;

    /// <summary>Client ID: what was just typed, else the saved one, else the environment override.</summary>
    private string ResolveClientId()
    {
        if (ClientIdInput?.Trim() is { Length: > 0 } typed) return typed;
        if (SettingsService.Instance.Current.OAuthClientId?.Trim() is { Length: > 0 } saved) return saved;
        return GitHubAuth.ClientIdFromEnvironment ?? "";
    }

    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { SignInHint = $"Couldn't open the browser ({ex.Message}) — open {url} manually."; }
    }

    /// <summary>Renews an expiring device-flow token; false when the credential is gone.</summary>
    private async Task<bool> EnsureFreshTokenAsync()
    {
        var s = SettingsService.Instance.Current;
        if (string.IsNullOrWhiteSpace(s.Token)) return false;
        if (s.RefreshToken is null || s.TokenExpiresAtUtc is not { } expiry) return true;
        if (expiry > DateTimeOffset.UtcNow.AddMinutes(2)) return true;
        if (ResolveClientId() is not { Length: > 0 } clientId) return true;

        try
        {
            var refreshed = await GitHubAuth.RefreshAsync(clientId, s.RefreshToken, CancellationToken.None);
            s.Token = refreshed.AccessToken;
            s.RefreshToken = refreshed.RefreshToken ?? s.RefreshToken;
            s.TokenExpiresAtUtc = refreshed.ExpiresAtUtc;
            _github.ApplyToken(s.Token);
            SettingsService.Instance.Save();
            TokenRejected = false;
            UpdateTokenLabel();
            return true;
        }
        catch (GitHubAuthException ex) when (ex.Error is "bad_refresh_token" or "expired_token" or "incorrect_client_credentials")
        {
            SignOut();
            SignInHint = "Your GitHub sign-in expired — sign in again.";
            return false;
        }
        catch (Exception)
        {
            // Network trouble: keep the credential and let the fetch report the failure.
            return true;
        }
    }

    /// <summary>Asks GitHub who the stored token belongs to; 401 means the token is dead.</summary>
    private async Task VerifyStoredTokenAsync()
    {
        var s = SettingsService.Instance.Current;
        if (string.IsNullOrWhiteSpace(s.Token)) { UpdateTokenLabel(); return; }
        await EnsureFreshTokenAsync();
        s = SettingsService.Instance.Current;
        if (string.IsNullOrWhiteSpace(s.Token)) { UpdateTokenLabel(); return; }

        try
        {
            var probe = await GitHubAuth.ProbeLoginAsync(s.Token, CancellationToken.None);
            if (probe.Rejected)
            {
                TokenRejected = true;
                s.Login = null;
            }
            else if (probe.Login is { Length: > 0 } login)
            {
                TokenRejected = false;
                s.Login = login;
            }
            SettingsService.Instance.Save();
            SettingsHint = TokenRejected
                ? "GitHub rejected that credential — sign in again or paste a valid token."
                : SettingsHint;
        }
        catch
        {
            // Offline or GitHub unreachable: say nothing about the login rather than guessing.
        }
        UpdateTokenLabel();
    }

    private void UpdateTokenLabel()
    {
        var s = SettingsService.Instance.Current;
        var stored = !string.IsNullOrWhiteSpace(s.Token);
        HasToken = stored;
        TokenLabel = stored
            ? (s.Login is { Length: > 0 } login ? $"@{login}" : "signed in")
            : "anonymous";

        if (IsSigningIn)
        {
            AuthStatusText = "SIGNING IN…";
            AuthStatusDetail = "Approve the code on GitHub — this panel updates itself once GitHub confirms.";
            return;
        }

        if (!stored)
        {
            TokenRejected = false;
            AuthStatusText = "NOT SIGNED IN";
            AuthStatusDetail = "Remaining anonymous is fine: requests are capped at 10 searches a minute and trending pages work without a token. Signing in raises the cap to 30.";
            return;
        }

        if (TokenRejected)
        {
            AuthStatusText = "TOKEN REJECTED";
            AuthStatusDetail = "GitHub refused this credential (401) — sign in again with GitHub, or paste a fresh personal access token.";
            return;
        }

        AuthStatusText = s.Login is { Length: > 0 } who ? $"SIGNED IN · @{who}" : "SIGNED IN";
        AuthStatusDetail = s.OAuthClientId is not null
            ? s.TokenExpiresAtUtc is { } expiry
                ? $"Signed in with GitHub — the token renews automatically (current one good until {expiry.ToLocalTime():HH:mm})."
                : "Signed in with GitHub — searches are authenticated, raising the cap to 30 a minute."
            : "Personal access token stored and applied — searches are authenticated, raising the cap to 30 a minute.";
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        // The rail writes null while its list rebuilds — hold the previous choice.
        var chosen = value ?? _lastGoodLanguage ?? GitHubLanguages.Any;
        if (!ReferenceEquals(chosen, value))
        {
            SelectedLanguage = chosen;
            return;
        }
        _lastGoodLanguage = value;
        _ = RefreshAsync();
    }

    private LanguageOption? _lastGoodLanguage = GitHubLanguages.Any;
    partial void OnRangeChanged(TrendRange value)
    {
        OnPropertyChanged(nameof(RangeText));
        _ = RefreshAsync();
    }
    partial void OnFilterChanged(string value) => ApplyFilter(announce: false);
    partial void OnRailFilterChanged(string value) => RebuildVisibleLanguages();

    private void RebuildVisibleLanguages()
    {
        var f = RailFilter?.Trim() ?? "";
        VisibleLanguages.Clear();
        foreach (var l in LanguagesList)
        {
            if (f.Length == 0 || l.Label.Contains(f, StringComparison.OrdinalIgnoreCase))
                VisibleLanguages.Add(l);
        }
        RailCountText = $"{VisibleLanguages.Count} LANGUAGES";
    }

    [ObservableProperty] private string _railCountText = "LANGUAGES";
    partial void OnScrapePrimaryChanged(bool value)
    {
        SettingsService.Instance.Current.SourceMode = value ? SourceMode.ScrapePrimary : SourceMode.ApiFirst;
        SettingsService.Instance.Save();
    }

    private void ApplyFilter(bool announce)
    {
        var f = Filter.Trim();
        IEnumerable<RepoRowViewModel> query = _allRepos;
        if (f.Length > 0)
        {
            query = query.Where(r =>
                r.FullName.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                r.Language.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        var ranked = query.ToList();
        VisibleRepos.Clear();
        var i = 0;
        foreach (var r in ranked)
        {
            r.Rank = i++;
            r.SetPeriod(r.Item.PeriodStars, Range);
            VisibleRepos.Add(r);
        }
        CountText = f.Length == 0 ? $"{ranked.Count} REPOSITORIES" : $"{ranked.Count} OF {_allRepos.Count} REPOSITORIES";
        CountShort = ranked.Count.ToString();
        if (announce) ListArrived?.Invoke();
    }
}
