namespace DatPlotX.Models;

/// <summary>
/// Represents a single plot pane in a multi-pane layout
/// </summary>
public class PlotPaneModel
{
    /// <summary>
    /// Unique identifier for this pane
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for this pane (e.g., "Pane 1")
    /// </summary>
    public string Name { get; set; } = "Pane 1";

    /// <summary>
    /// Index of this pane in the layout (0-based)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Height ratio relative to other panes (for vertical stacking)
    /// </summary>
    public double HeightRatio { get; set; } = 1.0;

    /// <summary>
    /// Plot title for this pane
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// X-axis label
    /// </summary>
    public string XAxisLabel { get; set; } = "Time (s)";

    /// <summary>
    /// Y-axis label
    /// </summary>
    public string YAxisLabel { get; set; } = "Value";

    /// <summary>
    /// Y2-axis label (right Y-axis)
    /// </summary>
    public string Y2AxisLabel { get; set; } = "Value 2";

    /// <summary>
    /// Whether to show Y2 axis
    /// </summary>
    public bool ShowY2Axis { get; set; }

    /// <summary>
    /// Whether to show grid lines
    /// </summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Whether to show legend. Maintained for back-compat with existing project files;
    /// <see cref="LegendPosition"/> takes precedence when not <see cref="Models.LegendPosition.InsideUpperRight"/>.
    /// </summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// Placement of the pane legend. Defaults to inside upper-right (legacy behavior).
    /// </summary>
    public LegendPosition LegendPosition { get; set; } = LegendPosition.InsideUpperRight;

    /// <summary>
    /// X-axis minimum (null for auto)
    /// </summary>
    public double? XAxisMin { get; set; }

    /// <summary>
    /// X-axis maximum (null for auto)
    /// </summary>
    public double? XAxisMax { get; set; }

    /// <summary>
    /// Y-axis minimum (null for auto)
    /// </summary>
    public double? YAxisMin { get; set; }

    /// <summary>
    /// Y-axis maximum (null for auto)
    /// </summary>
    public double? YAxisMax { get; set; }

    /// <summary>
    /// Y2-axis minimum (null for auto)
    /// </summary>
    public double? Y2AxisMin { get; set; }

    /// <summary>
    /// Y2-axis maximum (null for auto)
    /// </summary>
    public double? Y2AxisMax { get; set; }

    /// <summary>
    /// Whether this pane's X-axis is synchronized with other panes
    /// </summary>
    public bool XAxisSynchronized { get; set; } = true;

    /// <summary>
    /// Whether to show X-axis labels (typically only bottom pane shows them)
    /// </summary>
    public bool ShowXAxisLabels { get; set; } = true;

    /// <summary>
    /// Background color in hex format
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Whether this pane is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    // Grid formatting properties
    /// <summary>
    /// Whether to show major grid lines
    /// </summary>
    public bool ShowMajorGrid { get; set; } = true;

    /// <summary>
    /// Whether to show minor grid lines
    /// </summary>
    public bool ShowMinorGrid { get; set; }

    /// <summary>
    /// Grid line color in hex format
    /// </summary>
    public string GridColor { get; set; } = "#E0E0E0";

    /// <summary>
    /// Grid line width
    /// </summary>
    public double GridLineWidth { get; set; } = 1.0;

    /// <summary>
    /// Grid line style (Solid, Dash, Dot)
    /// </summary>
    public string GridLineStyle { get; set; } = "Solid";

    // Axis label formatting
    /// <summary>
    /// Axis label font size
    /// </summary>
    public double AxisLabelFontSize { get; set; } = 14;

    /// <summary>
    /// Whether axis labels are bold
    /// </summary>
    public bool AxisLabelBold { get; set; } = true;

    // Tick formatting
    /// <summary>
    /// Number format for tick labels (Standard, Scientific, Custom)
    /// </summary>
    public string TickNumberFormat { get; set; } = "Standard";

    /// <summary>
    /// Decimal places for X-axis tick labels (default 2; smart-decimals may override from the
    /// data range when the first curve is added). Changes to this property propagate to all panes.
    /// </summary>
    public int XAxisDecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Decimal places for Y1-axis (left) tick labels (default 2; smart-decimals may override).
    /// Per-pane setting, does not propagate.
    /// </summary>
    public int Y1AxisDecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Decimal places for Y2-axis (right) tick labels (default 2; smart-decimals may override).
    /// Per-pane setting, does not propagate.
    /// </summary>
    public int Y2AxisDecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Tick label font size
    /// </summary>
    public double TickLabelFontSize { get; set; } = 14;

    /// <summary>
    /// Data area background color in hex format
    /// </summary>
    public string DataBackgroundColor { get; set; } = "#FFFFFF";

    // Title formatting
    /// <summary>
    /// Plot title text
    /// </summary>
    public string TitleText { get; set; } = "Pane Title";

    /// <summary>
    /// Title font size
    /// </summary>
    public double TitleFontSize { get; set; } = 20;

    /// <summary>
    /// Title font style (Normal, Bold, Italic)
    /// </summary>
    public string TitleFontStyle { get; set; } = "Bold";

    /// <summary>
    /// Legend font size
    /// </summary>
    public double LegendFontSize { get; set; } = 14;
}
