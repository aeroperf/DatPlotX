namespace DatPlotX.Models;

/// <summary>
/// Per-project formatting for the Compact Plot Surface. Lives on
/// <see cref="ProjectSettingsModel.CompactPaneSettings"/> and is read by
/// <c>CompactPlotViewModel.Rebuild()</c> on every plot regeneration.
/// </summary>
public class CompactPaneSettings
{
    public bool ShowMajorGridlines { get; set; } = true;
    public bool ShowMinorGridlines { get; set; }
    public CompactGridLineStyle MinorGridlineStyle { get; set; } = CompactGridLineStyle.Dash;

    /// <summary>Plot background hex color (#RRGGBB or #AARRGGBB).</summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>Override for the X-axis title. Null/empty falls back to the column name.</summary>
    public string? XAxisLabelOverride { get; set; }

    public int XAxisDecimalPlaces { get; set; }
    public bool XAxisAutoScale { get; set; } = true;
    public double? XAxisMin { get; set; }
    public double? XAxisMax { get; set; }

    /// <summary>X-axis label font size (used for both axis title and tick labels).</summary>
    public double XAxisLabelFontSize { get; set; } = 14;

    /// <summary>True = X-axis label rendered bold.</summary>
    public bool XAxisLabelBold { get; set; }
}

public enum CompactGridLineStyle
{
    Dash = 0,
    Dot = 1,
    DashDot = 2,
}
