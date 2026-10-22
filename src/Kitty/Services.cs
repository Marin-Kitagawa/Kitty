using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kitty;

public sealed class SettingsService
{
    public static SettingsService Instance { get; } = new();
    public Settings Current { get; private set; } = new();
    private readonly string _path;

    private SettingsService()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kitty");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
        if (File.Exists(_path))
        {
            try { Current = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_path)) ?? new(); }
            catch { Current = new(); }
        }
    }

    public void Save()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed partial class GitHubService
{
    private static readonly HttpClient Http = CreateClient();
    private readonly Settings _settings = SettingsService.Instance.Current;

    private static HttpClient CreateClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("Kitty-trending/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public void ApplyToken(string? token)
    {
        Http.DefaultRequestHeaders.Authorization =
            token is null ? null : new AuthenticationHeaderValue("Bearer", token);
    }

    // ---------- Search API (approximates trending) ----------

    public async Task<TrendResult> FetchViaSearchApiAsync(LanguageOption language, TrendRange range, CancellationToken ct)
    {
        var since = range switch
        {
            TrendRange.Daily => DateTime.UtcNow.AddDays(-1),
            TrendRange.Weekly => DateTime.UtcNow.AddDays(-7),
            _ => DateTime.UtcNow.AddDays(-30)
        };
        var q = language is null || language.Slug.Length == 0
            ? $"created:>={since:yyyy-MM-dd}"
            : $"language:{language.Label} created:>={since:yyyy-MM-dd}";
        var url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(q)}&sort=stars&order=desc&per_page=30";
        using var resp = await Http.GetAsync(url, ct);
        if ((int)resp.StatusCode == 403 || (int)resp.StatusCode == 429)
            throw new RateLimitException("API rate limit reached — sign in or switch source in settings.");
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var repos = doc.RootElement.GetProperty("items").EnumerateArray().Select(FromApiJson).ToList();
        return new TrendResult { Repos = repos, Source = "search-api" };
    }

    private static RepoItem FromApiJson(JsonElement e)
    {
        string GetStr(string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        return new RepoItem
        {
            Owner = GetStr("full_name").Split('/')[0],
            Name = GetStr("name"),
            Description = GetStr("description"),
            Language = GetStr("language"),
            Stars = e.TryGetProperty("stargazers_count", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : 0,
            Forks = e.TryGetProperty("forks_count", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0
        };
    }

    // ---------- Trending-page scraping (true ranking) ----------

    public async Task<TrendResult> FetchViaScrapeAsync(LanguageOption language, TrendRange range, CancellationToken ct)
    {
        var langSeg = language is null || language.Slug.Length == 0 ? "" : "/" + language.Slug;
        var url = $"https://github.com/trending{langSeg}?since={range switch { TrendRange.Daily => "daily", TrendRange.Weekly => "weekly", _ => "monthly" }}";
        using var resp = await Http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var html = await resp.Content.ReadAsStringAsync(ct);
        var repos = ParseTrendingHtml(html);
        if (repos.Count == 0)
            throw new HttpRequestException("Trending page parse failed (layout change?)");
        return new TrendResult { Repos = repos, Source = "trending-scrape" };
    }

    public static List<RepoItem> ParseTrendingHtml(string html)
    {
        var list = new List<RepoItem>();
        var repoBlocks = ArticleRx().Matches(html);
        foreach (Match block in repoBlocks)
        {
            var b = block.Value;
            var nm = NameRx().Match(b);
            if (!nm.Success) continue;
            var full = nm.Groups[1].Value.Trim().Replace(" / ", "/");
            var owner = full.Split('/')[0];
            var name = full.Contains('/') ? full.Split('/')[1] : full;
            var desc = DescRx().Match(b) is { Success: true } d ? System.Net.WebUtility.HtmlDecode(d.Groups[1].Value).Trim() : "";
            var lang = LangRx().Match(b) is { Success: true } l ? l.Groups[1].Value.Trim() : "";
            var color = ColorRx().Match(b) is { Success: true } c ? c.Groups[1].Value : "";
            var stars = StarsRx().Match(b) is { Success: true } s ? s.Groups[1].Value : "";
            var forks = ForksRx().Match(b) is { Success: true } f2 ? f2.Groups[1].Value : "";
            var period = PeriodRx().Match(b);
            list.Add(new RepoItem
            {
                Owner = owner, Name = name, Description = desc, Language = lang, LanguageColor = color,
                Stars = int.TryParse(stars.Replace(",", ""), out var st) ? st : 0,
                Forks = int.TryParse(forks.Replace(",", ""), out var fk) ? fk : 0,
                PeriodStars = period.Success && int.TryParse(period.Groups[1].Value.Replace(",", ""), out var ps) ? ps : null
            });
        }
        return list;
    }

    [GeneratedRegex(@"<article[^>]*>.*?</article>", RegexOptions.Singleline)]
    private static partial Regex ArticleRx();
    [GeneratedRegex(@"href=""/([\w.\-]+/[\w.\-]+)/stargazers""")]
    private static partial Regex NameRx();
    [GeneratedRegex(@"<p class=""col-9[^""]*"">\s*(.*?)\s*</p>", RegexOptions.Singleline)]
    private static partial Regex DescRx();
    [GeneratedRegex(@"itemprop=""programmingLanguage"">([^<]+)<")]
    private static partial Regex LangRx();
    [GeneratedRegex(@"repo-language-color"" style=""background-color: ?(#[0-9a-fA-F]+)")]
    private static partial Regex ColorRx();
    [GeneratedRegex(@"href=""/[^""]+/stargazers""[^>]*>.*?</svg>\s*([\d,]+)\s*</a>", RegexOptions.Singleline)]
    private static partial Regex StarsRx();
    [GeneratedRegex(@"href=""/[^""]+/forks""[^>]*>.*?</svg>\s*([\d,]+)\s*</a>", RegexOptions.Singleline)]
    private static partial Regex ForksRx();
    [GeneratedRegex(@"([\d,]+)\s+stars\s+(?:today|this week|this month)", RegexOptions.Singleline)]
    private static partial Regex PeriodRx();

    // ---------- Orchestrated fetch (mode + fallback) ----------

    public async Task<TrendResult> FetchTrendingAsync(LanguageOption language, TrendRange range, CancellationToken ct)
    {
        string? warning = null;
        if (_settings.SourceMode == SourceMode.ScrapePrimary)
        {
            try { return await FetchViaScrapeAsync(language, range, ct); }
            catch (Exception ex) { warning = $"Scrape failed ({ex.Message}) — fell back to Search API."; }
            var r = await FetchViaSearchApiAsync(language, range, ct);
            return new TrendResult { Repos = r.Repos, Source = r.Source, Warning = warning };
        }
        try { return await FetchViaSearchApiAsync(language, range, ct); }
        catch (Exception ex) { warning = $"Search API unavailable ({ex.Message}) — showing scraped trending pages instead."; }
        var r2 = await FetchViaScrapeAsync(language, range, ct);
        return new TrendResult { Repos = r2.Repos, Source = r2.Source, Warning = warning };
    }
}

public sealed class RateLimitException(string message) : Exception(message);

public sealed record LanguageOption(string Label, string Slug)
{
    public override string ToString() => Label;
}

public static partial class GitHubLanguages
{
    public static LanguageOption Any { get; } = new("Any language", "");

    /// <summary>Long-lived fallback list used until the live trending-page list loads.</summary>
    public static readonly LanguageOption[] Fallback =
    [
        new("C", "c"), new("C#", "c%23"), new("C++", "c%2B%2B"), new("Clojure", "clojure"),
        new("CSS", "css"), new("Dart", "dart"), new("Elixir", "elixir"), new("Go", "go"),
        new("Groovy", "groovy"), new("HTML", "html"), new("Java", "java"), new("JavaScript", "javascript"),
        new("Jupyter Notebook", "jupyter-notebook"), new("Kotlin", "kotlin"), new("Lua", "lua"),
        new("MATLAB", "matlab"), new("Objective-C", "objective-c"), new("Perl", "perl"), new("PHP", "php"),
        new("PowerShell", "powershell"), new("Python", "python"), new("R", "r"), new("Ruby", "ruby"),
        new("Rust", "rust"), new("Scala", "scala"), new("Shell", "shell"), new("Swift", "swift"),
        new("TypeScript", "typescript"), new("Vim script", "vim-script"), new("Zig", "zig")
    ];

    public static async Task<List<LanguageOption>> FetchLiveAsync(CancellationToken ct)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Kitty-trending/1.0");
        var html = await http.GetStringAsync("https://github.com/trending", ct);
        var langs = MenuRx().Matches(html)
            .Select(m => new LanguageOption(System.Net.WebUtility.HtmlDecode(m.Groups[2].Value.Trim()), m.Groups[1].Value))
            .ToList();
        langs.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
        return langs;
    }

    [GeneratedRegex(@"href=""/trending/([^?""]+)\?since=\w+""[^>]*><span data-menu-button-text[^>]*>\s*([^<]+?)\s*</span>")]
    private static partial Regex MenuRx();
}
