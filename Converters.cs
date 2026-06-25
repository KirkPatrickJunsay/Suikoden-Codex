using System.Globalization;

namespace SuikodenCodex;

public class StringNotEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => !string.IsNullOrWhiteSpace(value as string);
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => !(value is true);
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => !(value is true);
}

public class ChevronConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value is true ? "▾" : "▸";
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class FavoriteGlyphConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => value is true ? "★" : "☆";
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class BoolToColorConverter : IValueConverter
{
    // ConverterParameter format: "TrueHex|FalseHex"
    public object Convert(object? value, Type t, object? p, CultureInfo c)
    {
        var parts = (p as string ?? "#4CAF50|#00000000").Split('|');
        var hex = value is true ? parts[0] : parts[1];
        return Color.FromArgb(hex);
    }
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class HexToColorConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        Color.FromArgb(value as string ?? "#888888");
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}

public class QuizStateToColorConverter : IValueConverter
{
    public object Convert(object? value, Type t, object? p, CultureInfo c) => (value as string) switch
    {
        "correct" => Color.FromArgb("#2E7D32"),
        "wrong" => Color.FromArgb("#C62828"),
        _ => Color.FromArgb("#00000000")
    };
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => throw new NotSupportedException();
}
