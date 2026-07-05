using Avalonia.Data.Converters;
using Avalonia.Media;
using System.Globalization;

namespace DatPlotX;

/// <summary>Hex string ("#RRGGBB" / "#AARRGGBB") to <see cref="IBrush"/>.
/// Returns transparent on null/empty/parse failure so XAML rendering does not crash.</summary>
public sealed class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && Color.TryParse(hex, out var c))
            return new SolidColorBrush(c);
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
