using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DatPlotX.ViewModels;
using System.Globalization;

namespace DatPlotX.Converters;

/// <summary>
/// Maps a <see cref="PreviewRowKind"/> to the row background brush used by the
/// import-options preview list. Pulls colors from the active dpx token resources
/// so light / dark variants follow the rest of the dialog.
/// </summary>
public class PreviewRowBackgroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PreviewRowKind kind) return Brushes.Transparent;
        var key = kind switch
        {
            PreviewRowKind.Header => "DpxAccentSoft",
            PreviewRowKind.Unit => "DpxRaised",
            PreviewRowKind.DataStart => "DpxSurface",
            _ => null,
        };
        if (key is null) return Brushes.Transparent;
        return ResolveBrush(key, Brushes.Transparent);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static IBrush ResolveBrush(string key, IBrush fallback)
    {
        var app = Application.Current;
        if (app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        return fallback;
    }
}

/// <summary>
/// Picks the foreground brush so skipped lines fade and data lines stay full strength.
/// </summary>
public class PreviewRowForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not PreviewRowKind kind) return Brushes.Transparent;
        var key = kind switch
        {
            PreviewRowKind.Skipped => "DpxTextDisabled",
            PreviewRowKind.Unit => "DpxText2",
            _ => "DpxText",
        };
        var app = Application.Current;
        if (app is not null && app.TryGetResource(key, app.ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        return Brushes.Black;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Returns a 1px top border for the data-start row so the user sees the
/// "data block begins here" divider, transparent otherwise.
/// </summary>
public class PreviewRowBorderThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is PreviewRowKind kind && kind == PreviewRowKind.DataStart)
            return new Thickness(0, 1, 0, 0);
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
