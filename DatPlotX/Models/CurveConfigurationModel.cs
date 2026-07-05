namespace DatPlotX.Models;

/// <summary>
/// Line style for curves
/// </summary>
public enum LineStyle
{
    Solid,
    Dash,
    Dot,
    DashDot
}

/// <summary>
/// Configuration for a curve to be added to a plot pane
/// </summary>
public class CurveConfigurationModel
{
    /// <summary>
    /// Unique identifier for this curve configuration
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Name of the column to plot on Y-axis
    /// </summary>
    public string YColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the curve (defaults to column name)
    /// </summary>
    public string CurveName { get; set; } = string.Empty;

    /// <summary>
    /// Optional unit string (e.g. "ft", "kt", "°C"). When set, the Analysis panel and
    /// inline overlay append the unit to numeric results, and <see cref="Services.Analysis.Metrics.SlopeMetric"/>
    /// asks <see cref="Services.Units.IUnitRegistry.PreferredDerivedRate"/> for an
    /// engineer-friendly derived rate (e.g. <c>ft/min</c> alongside the raw <c>ft/s</c>).
    /// Null = unknown; raw numbers are shown with no suffix.
    /// </summary>
    public string? Unit { get; set; }

    /// <summary>
    /// Name of the X-axis column (if different from default)
    /// </summary>
    public string? XColumnName { get; set; }

    /// <summary>
    /// Which pane to plot this curve on (0-based index)
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// Which Y-axis to plot on
    /// </summary>
    public YAxisType YAxis { get; set; } = YAxisType.Y1;

    /// <summary>
    /// Curve color in hex format
    /// </summary>
    public string Color { get; set; } = "#0078D4";

    /// <summary>
    /// Line width in pixels
    /// </summary>
    public double LineWidth { get; set; } = 2.0;

    /// <summary>
    /// Line style (solid, dash, dot, dash-dot)
    /// </summary>
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;

    /// <summary>
    /// Whether to show markers on data points
    /// </summary>
    public bool ShowMarkers { get; set; }

    /// <summary>
    /// Marker style for data points
    /// </summary>
    public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.Circle;

    /// <summary>
    /// Marker size in pixels
    /// </summary>
    public double MarkerSize { get; set; } = 5.0;

    /// <summary>
    /// Marker color in hex format (can be different from line color)
    /// </summary>
    public string MarkerColor { get; set; } = "#0078D4";

    /// <summary>
    /// Whether to show the connecting line between markers
    /// </summary>
    public bool ShowLine { get; set; } = true;

    /// <summary>
    /// Whether this curve is currently visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Whether this curve configuration is selected for plotting
    /// </summary>
    public bool IsSelected { get; set; }
}

/// <summary>
/// Item for the curve selection list in the AddCurvesDialog
/// </summary>
public partial class CurveSelectionItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    /// <summary>
    /// Column name from the data source
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _columnName = string.Empty;

    /// <summary>
    /// Whether this column is selected for plotting
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _isSelected = false;

    /// <summary>
    /// Target pane index (0-based)
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int _targetPane = 0;

    /// <summary>
    /// Target Y-axis
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private YAxisType _targetAxis = YAxisType.Y1;

    /// <summary>
    /// Curve color
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _color = "#0078D4";

    /// <summary>
    /// Line width in pixels
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private double _lineWidth = 2.0;

    /// <summary>
    /// Line style
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private LineStyle _lineStyle = LineStyle.Solid;

    /// <summary>
    /// Whether to show markers
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _showMarkers = false;

    /// <summary>
    /// Marker style
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private MarkerStyle _markerStyle = MarkerStyle.Circle;

    /// <summary>
    /// Marker size
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private double _markerSize = 5.0;

    /// <summary>
    /// Marker color
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _markerColor = "#0078D4";

    /// <summary>
    /// Whether to show connecting line
    /// </summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private bool _showLine = true;
}
