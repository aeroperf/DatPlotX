using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace DatPlotX.Converters;

/// <summary>
/// Converts a boolean to a <see cref="GridLength"/>: <c>true</c> → the length given in the
/// converter parameter (default <c>2*</c>), <c>false</c> → <c>0</c>. Used to collapse a grid
/// column entirely when its content is hidden, so nothing bleeds past the collapsed edge.
/// </summary>
/// <remarks>
/// The parameter accepts any string <see cref="GridLength.Parse(string)"/> understands —
/// e.g. <c>"360"</c> (pixels), <c>"2*"</c> (star), <c>"Auto"</c>.
/// </remarks>
public sealed class BoolToGridLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool show = value is true;
        if (!show) return new GridLength(0);

        if (parameter is string s && !string.IsNullOrWhiteSpace(s))
            return GridLength.Parse(s);

        return new GridLength(2, GridUnitType.Star);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
