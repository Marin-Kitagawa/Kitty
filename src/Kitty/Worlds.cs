using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;

namespace Kitty;

/// <summary>
/// The six palettes Kitty can wear. A world only supplies color; typography,
/// density, layout and motion are the same everywhere, so every world reads as
/// the same product in a different ink.
/// </summary>
public static class Worlds
{
    public sealed record World(string Id, string Title, string Uri, bool Dark);

    public static readonly World[] All =
    [
        new("riso", "Risograph Press",  "avares://Kitty/Themes/World.Riso.axaml",      false),
        new("shelf", "Character Shelf", "avares://Kitty/Themes/World.Shelf.axaml",     false),
        new("board", "Split-Flap Board","avares://Kitty/Themes/World.Board.axaml",     true),
        new("tape", "Cassette Insert",  "avares://Kitty/Themes/World.Tape.axaml",      false),
        new("programme", "Language Programme", "avares://Kitty/Themes/World.Programme.axaml", false),
        new("canon", "Category Standard","avares://Kitty/Themes/World.Canon.axaml",    false),
    ];

    public static World Resolve(string? id) =>
        Array.Find(All, w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static World Next(string? id)
    {
        var current = Resolve(id);
        var i = Array.FindIndex(All, w => w.Id == current.Id);
        return All[(i + 1) % All.Length];
    }

    private static ResourceInclude? _current;

    /// <summary>Swaps the active world dictionary and its light/dark variant.</summary>
    public static World Apply(Application app, string? id)
    {
        var w = Resolve(id);
        var include = new ResourceInclude(new Uri("avares://Kitty/")) { Source = new Uri(w.Uri) };
        if (_current is not null) app.Resources.MergedDictionaries.Remove(_current);
        app.Resources.MergedDictionaries.Add(include);
        _current = include;
        app.RequestedThemeVariant = w.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        return w;
    }
}
