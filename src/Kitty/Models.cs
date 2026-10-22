using System.Text.Json.Serialization;

namespace Kitty;

public enum TrendRange { Daily, Weekly, Monthly }

public enum SourceMode
{
    /// <summary>Search API with scrape fallback (default).</summary>
    ApiFirst,
    /// <summary>Scrape the real trending pages; user explicitly opted in.</summary>
    ScrapePrimary
}

public sealed class RepoItem
{
    public string Owner { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string Language { get; init; } = "";
    public string LanguageColor { get; init; } = "";
    public int Stars { get; init; }
    public int Forks { get; init; }
    /// <summary>Stars gained in the selected period, when the source provides it (scraper only).</summary>
    public int? PeriodStars { get; init; }
    public string HtmlUrl => $"https://github.com/{Owner}/{Name}";
    public string CloneUrl => $"https://github.com/{Owner}/{Name}.git";
    public string FullName => $"{Owner}/{Name}";
}

public sealed class TrendResult
{
    public required IReadOnlyList<RepoItem> Repos { get; init; }
    /// <summary>"search-api" or "trending-scrape" — shown honestly in the status bar.</summary>
    public required string Source { get; init; }
    public string? Warning { get; init; }
}

public static class LanguageColors
{
    /// <summary>GitHub linguist colors for common languages; used when the source doesn't provide one.</summary>
    public static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JavaScript"] = "#f1e05a", ["TypeScript"] = "#3178c6", ["Python"] = "#3572A5",
        ["HTML"] = "#e34c26", ["CSS"] = "#563d7c", ["C"] = "#555555", ["C++"] = "#f34b7d",
        ["C#"] = "#178600", ["Go"] = "#00ADD8", ["Rust"] = "#dea584", ["Java"] = "#b07219",
        ["Kotlin"] = "#A97BFF", ["Swift"] = "#F05138", ["Ruby"] = "#701516", ["PHP"] = "#4F5D95",
        ["Scala"] = "#c22d40", ["Dart"] = "#00B4AB", ["Lua"] = "#000080", ["Elixir"] = "#6e4a7e",
        ["Haskell"] = "#5e5086", ["Clojure"] = "#db5855", ["Shell"] = "#89e051",
        ["PowerShell"] = "#012456", ["Perl"] = "#0298c3", ["R"] = "#198CE7", ["Zig"] = "#ec915c",
        ["Jupyter Notebook"] = "#DA5B0B", ["Vue"] = "#41b883", ["Svelte"] = "#ff3e00",
        ["Objective-C"] = "#438eff", ["MATLAB"] = "#e16737", ["Vim Script"] = "#199f4b",
        ["Vim script"] = "#199f4b", ["Nix"] = "#7e7eff", ["Groovy"] = "#4298b8", ["Assembly"] = "#6E4C13"
    };

    public static string For(string language, string provided) =>
        string.IsNullOrWhiteSpace(provided) && !string.IsNullOrEmpty(language) && Map.TryGetValue(language, out var c)
            ? c : provided;
}

public sealed class Settings
{
    public SourceMode SourceMode { get; set; } = SourceMode.ApiFirst;
    /// <summary>Access token: a pasted personal access token, or one issued by the device flow.</summary>
    public string? Token { get; set; }
    /// <summary>Set when sign-in went through the OAuth device flow (otherwise null).</summary>
    public string? OAuthClientId { get; set; }
    /// <summary>Device-flow refresh token; null for pasted tokens or apps with token expiry off.</summary>
    public string? RefreshToken { get; set; }
    /// <summary>When the access token stops working, if GitHub gave an expiry.</summary>
    public DateTimeOffset? TokenExpiresAtUtc { get; set; }
    /// <summary>The signed-in GitHub login, when the token could be verified.</summary>
    public string? Login { get; set; }
    public string LastLanguage { get; set; } = "Any language";
    public string LastLanguageSlug { get; set; } = "";
    /// <summary>"dusk" (dark) or "dawn" (light). Retained for back-compat.</summary>
    public string Theme { get; set; } = "dusk";
    /// <summary>Visual world id: riso | shelf | board | tape | programme | canon.</summary>
    public string World { get; set; } = "riso";
    [JsonPropertyName("cacheLanguageListJson")]
    public string? CachedLanguagesJson { get; set; }
}
