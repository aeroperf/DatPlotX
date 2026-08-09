using System.Globalization;

namespace DatPlotX.Helpers;

/// <summary>
/// Shared formatting for the event-line X-value row rendered under an event line's name.
/// Both plot modes (Stacked / ScottPlot and Compact / OxyPlot) draw the same text so a project
/// looks identical whichever surface it is on.
/// </summary>
public static class EventLineLabelFormatter
{
    /// <summary>Decimal places clamp — matches the Format Pane stepper bounds.</summary>
    public const int MinDecimals = 0;
    public const int MaxDecimals = 6;
    public const int DefaultDecimals = 2;

    /// <summary>
    /// Format an event line's X position for the row beneath its label. Non-finite values return
    /// an empty string so the caller can suppress the row rather than draw "NaN".
    /// </summary>
    public static string Format(double xPosition, int decimals)
    {
        if (double.IsNaN(xPosition) || double.IsInfinity(xPosition)) return string.Empty;
        int d = Math.Clamp(decimals, MinDecimals, MaxDecimals);
        return xPosition.ToString("F" + d.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Format for rendering, honoring the pane's "show X value" toggle. Returns null when the
    /// row should not be drawn at all.
    /// </summary>
    public static string? FormatOrNull(double xPosition, bool show, int decimals)
    {
        if (!show) return null;
        var text = Format(xPosition, decimals);
        return string.IsNullOrEmpty(text) ? null : text;
    }
}
