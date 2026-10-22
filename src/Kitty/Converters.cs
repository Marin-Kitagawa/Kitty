using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Kitty;

/// <summary>Language label → linguist color brush; unknown languages get a transparent dot.</summary>
public sealed class LanguageDotConverter : IValueConverter
{
    public static readonly LanguageDotConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string label && LanguageColors.Map.TryGetValue(label, out var hex)
            ? new SolidColorBrush(Color.Parse(hex))
            : Brushes.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

public sealed class HasLanguageDotConverter : IValueConverter
{
    public static readonly HasLanguageDotConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string slug && slug.Length > 0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

public sealed class StringToColorConverter : IValueConverter
{
    public static readonly StringToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && hex.StartsWith('#') ? Color.Parse(hex) : Colors.Gray;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}

public sealed class UpperCaseConverter : IValueConverter
{
    public static readonly UpperCaseConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string s ? s.ToUpperInvariant() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => null;
}
