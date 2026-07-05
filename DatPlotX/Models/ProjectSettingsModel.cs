namespace DatPlotX.Models;

/// <summary>
/// Represents all settings and data for a DatPlot project (.DPX file)
/// </summary>
public class ProjectSettingsModel
{
    /// <summary>
    /// Current <c>.DPX</c> schema version emitted by this build. Bump when a breaking change to
    /// the on-disk JSON shape is introduced, then add a migration step in the loader.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Schema version of the serialized <c>.DPX</c> file. Defaults to
    /// <see cref="CurrentSchemaVersion"/> for newly created projects. A value of <c>0</c> after
    /// deserialization means the field was absent (a pre-versioning file) — loaders treat that as
    /// the original v1 shape. Lets future breaking changes degrade gracefully instead of throwing.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// Project format version for compatibility
    /// </summary>
    public string Version { get; set; } = "2.0.0";

    /// <summary>
    /// Plot surface style. Locked at project creation. <c>null</c> means the field was missing
    /// from the deserialized JSON — the app prompts the user to pick a mode on load.
    /// </summary>
    public PlotMode? PlotMode { get; set; }

    /// <summary>
    /// Curves on the Compact Plot Surface. Order in this list = top-to-bottom band order.
    /// Empty when <see cref="PlotMode"/> is <see cref="PlotMode.Panes"/>.
    /// </summary>
    public List<CompactCurveModel> CompactCurves { get; set; } = new();

    /// <summary>
    /// Pane-level formatting for the Compact Plot Surface (gridlines, background, X-axis).
    /// Ignored in Panes mode.
    /// </summary>
    public CompactPaneSettings CompactPaneSettings { get; set; } = new();

    /// <summary>
    /// Event lines on the Compact Plot Surface. Independent of Panes-mode <see cref="EventLines"/>
    /// because the two surfaces have separate lifecycles and persistence requirements.
    /// </summary>
    public List<EventLineModel> CompactEventLines { get; set; } = new();

    /// <summary>
    /// Text annotations on the Compact Plot Surface. Each model's <see cref="TextAnnotationModel.CompactCurveAnchor"/>
    /// identifies the banded curve whose Y axis the annotation tracks; null = first visible curve.
    /// </summary>
    public List<TextAnnotationModel> CompactTextAnnotations { get; set; } = new();

    /// <summary>
    /// Arrow annotations on the Compact Plot Surface. Each model's <see cref="ArrowAnnotationModel.CompactCurveAnchor"/>
    /// identifies the banded curve whose Y axis the annotation tracks; null = first visible curve.
    /// </summary>
    public List<ArrowAnnotationModel> CompactArrowAnnotations { get; set; } = new();

    /// <summary>
    /// Grouped Parameter Plot configuration. Non-null only when <see cref="PlotMode"/> is
    /// <see cref="Models.PlotMode.Grouped"/>; otherwise ignored.
    /// </summary>
    public GroupedPlotConfig? GroupedPlot { get; set; }

    /// <summary>
    /// Text annotations on the Grouped Parameter Plot. Empty in other modes.
    /// </summary>
    public List<TextAnnotationModel> GroupedTextAnnotations { get; set; } = new();

    /// <summary>
    /// Arrow annotations on the Grouped Parameter Plot. Empty in other modes.
    /// </summary>
    public List<ArrowAnnotationModel> GroupedArrowAnnotations { get; set; } = new();

    /// <summary>
    /// Project name
    /// </summary>
    public string ProjectName { get; set; } = "Untitled Project";

    /// <summary>
    /// When the project was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// When the project was last modified
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.Now;

    /// <summary>
    /// Author/creator of the project
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// Project description/notes
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Plot title
    /// </summary>
    public string PlotTitle { get; set; } = "DatPlot";

    /// <summary>
    /// X-axis label
    /// </summary>
    public string XAxisLabel { get; set; } = "Time (s)";

    /// <summary>
    /// Y-axis label
    /// </summary>
    public string YAxisLabel { get; set; } = "Value";

    /// <summary>
    /// Number of panes in the plot
    /// </summary>
    public int PaneCount { get; set; } = 1;

    /// <summary>
    /// List of plot panes (for multi-pane layouts)
    /// </summary>
    public List<PlotPaneModel> Panes { get; set; } = new();

    /// <summary>
    /// Whether to show grid lines
    /// </summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>
    /// Whether to show legend
    /// </summary>
    public bool ShowLegend { get; set; } = true;

    /// <summary>
    /// Data source file path (relative or absolute)
    /// </summary>
    public string? DataSourcePath { get; set; }

    /// <summary>
    /// Imported data
    /// </summary>
    public PlotDataModel? PlotData { get; set; }

    /// <summary>
    /// List of curves plotted
    /// </summary>
    public List<PlotCurveModel> Curves { get; set; } = new();

    /// <summary>
    /// Project-level X-axis column selection shown in the X picker. Null on legacy projects;
    /// loaders fall through to <c>UpdateAvailableXColumns</c> auto-pick when absent or invalid.
    /// </summary>
    public string? SelectedXColumn { get; set; }

    /// <summary>
    /// List of event lines
    /// </summary>
    public List<EventLineModel> EventLines { get; set; } = new();

    /// <summary>
    /// List of intersection callout configurations for position persistence.
    /// Stores the offset positions for callout annotations at curve intersections.
    /// </summary>
    public List<IntersectionCalloutModel> IntersectionCallouts { get; set; } = new();

    /// <summary>
    /// List of text annotations placed on panes
    /// </summary>
    public List<TextAnnotationModel> TextAnnotations { get; set; } = new();

    /// <summary>
    /// List of arrow annotations placed on panes
    /// </summary>
    public List<ArrowAnnotationModel> ArrowAnnotations { get; set; } = new();

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
    /// Whether the Source Data bottom pane was collapsed when the project was last saved.
    /// Restored on project load so the user's preferred layout persists.
    /// </summary>
    public bool BottomPaneCollapsed { get; set; }

    /// <summary>
    /// User-defined analysis segments (Manual / EventLinePair). The implicit "Visible window"
    /// segment is regenerated at runtime and is never stored.
    /// </summary>
    public List<Analysis.AnalysisSegment> AnalysisSegments { get; set; } = new();

    /// <summary>Id of the active analysis segment, when it isn't the visible window.</summary>
    public Guid? ActiveSegmentId { get; set; }

    /// <summary>
    /// Metric columns shown in the Analysis Results panel, in display order (the user's choice in
    /// the Manage Metrics dialog). Empty in older project files and in any project that never
    /// touched the picker — the analysis service then keeps its built-in default column set.
    /// </summary>
    public List<string> EnabledMetricIds { get; set; } = new();
}
