namespace DatPlotX.Models;

/// <summary>
/// Represents a curve plotted on a chart
/// </summary>
public class PlotCurveModel
{
    /// <summary>
    /// Unique identifier for this curve
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for this curve (shown in legend)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Source column name in the data
    /// </summary>
    public string SourceColumn { get; set; } = string.Empty;

    /// <summary>
    /// X-axis column name (if different from default)
    /// </summary>
    public string? XAxisColumn { get; set; }

    /// <summary>
    /// Color of the curve in hex format
    /// </summary>
    public string Color { get; set; } = "#0078D4";

    /// <summary>
    /// Marker color in hex format (separately customizable from line color).
    /// Null in legacy project files written before per-marker color was persisted —
    /// callers should fall back to <see cref="Color"/> when null.
    /// </summary>
    public string? MarkerColor { get; set; }

    /// <summary>
    /// Line width in pixels
    /// </summary>
    public double LineWidth { get; set; } = 2.0;

    /// <summary>
    /// Line pattern
    /// </summary>
    public LinePatternType LinePattern { get; set; } = LinePatternType.Solid;

    /// <summary>
    /// Which Y-axis this curve is plotted on
    /// </summary>
    public YAxisType YAxis { get; set; } = YAxisType.Y1;

    /// <summary>
    /// Which pane this curve belongs to (for multi-pane plots)
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// Whether this curve is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Whether to show markers on data points
    /// </summary>
    public bool ShowMarkers { get; set; }

    /// <summary>
    /// Marker style
    /// </summary>
    public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.Circle;

    /// <summary>
    /// Marker size in pixels
    /// </summary>
    public double MarkerSize { get; set; } = 5.0;
}

/// <summary>
/// Marker styles for data points
/// </summary>
public enum MarkerStyle
{
    None,
    Circle,
    Square,
    Triangle,
    Diamond,
    Cross,
    Plus
}
