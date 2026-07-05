using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace DatPlotX;

/// <summary>Highlight tint for selected list rows: returns DpxAccentSoft for true, transparent otherwise.
/// Implements <see cref="IMultiValueConverter"/> so the XAML may pass multiple bindings if needed.</summary>
public sealed class BoolToBrushConverter : IMultiValueConverter, IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => values.Count > 0 && values[0] is bool b && b ? Highlight() : Brushes.Transparent;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? Highlight() : Brushes.Transparent;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static IBrush Highlight()
    {
        var app = Application.Current;
        if (app is null) return Brushes.Transparent;
        return app.Resources.TryGetResource("DpxAccentSoft", null, out var resource) && resource is IBrush b
            ? b
            : Brushes.Transparent;
    }
}
