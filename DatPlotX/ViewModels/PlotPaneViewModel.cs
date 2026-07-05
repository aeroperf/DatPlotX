using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;
using DatPlotX.ViewModels.PlotPane;
using ScottPlot;
using ScottPlot.Plottables;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for an individual plot pane
/// </summary>
public partial class PlotPaneViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private PlotPaneModel _paneModel;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _showLegend = true;

    [ObservableProperty]
    private bool _showGrid = true;

    private Plot? _plotModel;
    private TaskCompletionSource<Plot>? _plotReadyTcs;
    private readonly object _plotReadyLock = new();

    // Plot reference (set by view)
    public Plot? PlotModel
    {
        get => _plotModel;
        set
        {
            _plotModel = value;
            if (value != null)
            {
                lock (_plotReadyLock)
                {
                    _plotReadyTcs?.TrySetResult(value);
                }
            }
        }
    }

    /// <summary>
    /// Completes when <see cref="PlotModel"/> is non-null. Use this instead of arbitrary delays
    /// when work needs the plot surface to be materialized by the view.
    /// </summary>
    public Task<Plot> WhenPlotReady()
    {
        if (_plotModel != null) return Task.FromResult(_plotModel);
        lock (_plotReadyLock)
        {
            if (_plotModel != null) return Task.FromResult(_plotModel);
            _plotReadyTcs ??= new TaskCompletionSource<Plot>(TaskCreationOptions.RunContinuationsAsynchronously);
            return _plotReadyTcs.Task;
        }
    }

    // Track plotted curves using named record type for better readability
    private readonly Dictionary<Guid, PlottedCurveInfo> _curves = new();

    // Track event lines in this pane (per-pane legacy event lines)
    private readonly List<VerticalLine> _eventLines = new();

    // Track global event lines by their ID (for synchronization across panes)
    private readonly Dictionary<Guid, VerticalLine> _globalEventLines = new();

    // Track callout annotations by their ID (for intersection callouts)
    private readonly Dictionary<Guid, ScottPlot.Plottables.Callout> _calloutAnnotations = new();

    // Track event line label text annotations (separate from VerticalLine for rotation support)
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _globalEventLineLabels = new();

    // Track text annotations by their ID
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _textAnnotations = new();

    // Track arrow annotations by their ID (tip arrow)
    private readonly Dictionary<Guid, ScottPlot.Plottables.Arrow> _arrowAnnotations = new();
    // Track reverse arrows for double-ended arrows (base arrow)
    private readonly Dictionary<Guid, ScottPlot.Plottables.Arrow> _reverseArrowAnnotations = new();
    // Track optional labels for arrows
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _arrowLabels = new();

    // Formatting service (extracts visual formatting logic)
    private readonly PlotPaneFormattingService _formattingService;

    // Curve manager (extracts curve lifecycle management)
    private readonly PlotPaneCurveManager _curveManager;

    // Held so Dispose can detach the OnAxisLabelsNeedUpdate subscription (prevents pane leak).
    private Action? _axisLabelsHandler;

    // Annotation manager (extracts annotation management: callouts, text, arrows)
    private readonly PlotPaneAnnotationManager _annotationManager;

    public PlotPaneViewModel(PlotPaneModel paneModel)
    {
        _paneModel = paneModel;
        _title = paneModel.Title ?? $"Pane {paneModel.Index + 1}";
        _showLegend = paneModel.ShowLegend;
        _showGrid = paneModel.ShowGrid;

        // Initialize formatting service
        _formattingService = new PlotPaneFormattingService(
            () => PlotModel,
            () => PaneModel,
            TriggerPlotUpdate);

        // Initialize curve manager
        _curveManager = new PlotPaneCurveManager(
            () => PlotModel,
            () => PaneModel,
            _curves,
            TriggerPlotUpdate,
            () => ShowLegend,
            ShowLegendWithFormatting);

        // Subscribe to curve manager events. Keep the delegate in a field so Dispose can
        // detach it — otherwise the captured `this` keeps the pane alive (memory leak).
        _axisLabelsHandler = () => UpdateAxisLabelsFromCurves();
        _curveManager.OnAxisLabelsNeedUpdate += _axisLabelsHandler;

        // Initialize annotation manager
        _annotationManager = new PlotPaneAnnotationManager(
            () => PlotModel,
            () => PaneModel,
            _calloutAnnotations,
            _textAnnotations,
            _arrowAnnotations,
            _reverseArrowAnnotations,
            _arrowLabels,
            TriggerPlotUpdate);
    }

    private int _updateSuppressionDepth;
    private bool _updatePending;

    /// <summary>
    /// Trigger plot update event for view refresh
    /// </summary>
    private void TriggerPlotUpdate()
    {
        if (_updateSuppressionDepth > 0)
        {
            _updatePending = true;
            return;
        }
        OnPlotUpdated?.Invoke();
    }

    /// <summary>
    /// Suppress plot-update notifications until the returned scope is disposed.
    /// A single batched update is fired on scope exit if any updates were triggered.
    /// </summary>
    public IDisposable BeginUpdate() => new UpdateScope(this);

    private sealed class UpdateScope : IDisposable
    {
        private readonly PlotPaneViewModel _owner;
        private bool _disposed;
        public UpdateScope(PlotPaneViewModel owner)
        {
            _owner = owner;
            _owner._updateSuppressionDepth++;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _owner._updateSuppressionDepth--;
            if (_owner._updateSuppressionDepth == 0 && _owner._updatePending)
            {
                _owner._updatePending = false;
                _owner.OnPlotUpdated?.Invoke();
            }
        }
    }

    /// <summary>External hook for the analysis overlay host to request a redraw after
    /// adding or removing analysis-only plottables. Plain pass-through to <see cref="TriggerPlotUpdate"/>.</summary>
    public void RequestPlotRefresh() => TriggerPlotUpdate();

    /// <summary>
    /// Initialize the plot with default settings
    /// </summary>
    public void InitializePlot()
    {
        if (PlotModel == null)
            return;

        // Apply all formatting from the model (this includes font sizes, colors, grid, etc.)
        ApplyFormatting();

        // Show/hide X-axis labels based on pane settings
        if (!PaneModel.ShowXAxisLabels)
        {
            PlotModel.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.EmptyTickGenerator();
        }

        // Show legend if enabled
        if (ShowLegend)
        {
            ShowLegendWithFormatting();
        }

        TriggerPlotUpdate();
    }

    /// <summary>
    /// Add a curve to this pane with full configuration support
    /// </summary>
    public void AddCurve(double[] data, double samplePeriod, CurveConfigurationModel config) =>
        _curveManager.AddCurve(data, samplePeriod, config);

    /// <summary>
    /// Add a curve to this pane (legacy method for backward compatibility)
    /// </summary>
    public void AddCurve(double[] data, double samplePeriod, string curveName, string color, double lineWidth = 2.0, YAxisType yAxis = YAxisType.Y1) =>
        _curveManager.AddCurve(data, samplePeriod, curveName, color, lineWidth, yAxis);

    /// <summary>
    /// Add a scatter plot curve to this pane with full configuration support
    /// </summary>
    public void AddScatterCurve(double[] xData, double[] yData, CurveConfigurationModel config) =>
        _curveManager.AddScatterCurve(xData, yData, config);

    /// <summary>
    /// Add a scatter plot curve to this pane (legacy method)
    /// </summary>
    public void AddScatterCurve(double[] xData, double[] yData, string curveName, string color, double lineWidth = 2.0, YAxisType yAxis = YAxisType.Y1) =>
        _curveManager.AddScatterCurve(xData, yData, curveName, color, lineWidth, yAxis);

    /// <summary>
    /// Add an event line to this pane
    /// </summary>
    public VerticalLine AddEventLine(double xPosition, string label, string color = "#FFB900")
    {
        if (PlotModel == null)
            throw new InvalidOperationException("PlotModel must be set before adding event lines");

        var eventLine = PlotModel.Add.VerticalLine(xPosition);
        eventLine.Color = ScottPlot.Color.FromHex(color);
        eventLine.LineWidth = 2;
        eventLine.Text = label;
        eventLine.LabelFontSize = 12;
        eventLine.LabelBackgroundColor = ScottPlot.Color.FromHex("#FFFEF5");
        eventLine.LabelFontColor = ScottPlot.Color.FromHex("#000000");
        eventLine.LabelAlignment = Alignment.UpperCenter;
        eventLine.IsDraggable = true;  // Enable ScottPlot's built-in drag functionality

        _eventLines.Add(eventLine);
        TriggerPlotUpdate();

        return eventLine;
    }

    /// <summary>
    /// Remove a curve from this pane by ID
    /// </summary>
    public bool RemoveCurve(Guid curveId) => _curveManager.RemoveCurve(curveId);

    /// <summary>
    /// Toggle curve visibility by ID
    /// </summary>
    public bool ToggleCurveVisibility(Guid curveId) => _curveManager.ToggleCurveVisibility(curveId);

    /// <summary>
    /// Set curve visibility to a specific value (idempotent).
    /// </summary>
    public bool SetCurveVisibility(Guid curveId, bool isVisible) => _curveManager.SetCurveVisibility(curveId, isVisible);

    /// <summary>
    /// Update curve format (color, line style, line width, markers, etc.)
    /// </summary>
    public bool UpdateCurveFormat(CurveConfigurationModel updatedConfig) => _curveManager.UpdateCurveFormat(updatedConfig);

    /// <summary>
    /// Get curve configuration by ID
    /// </summary>
    public CurveConfigurationModel? GetCurveConfig(Guid curveId) => _curveManager.GetCurveConfig(curveId);

    /// <summary>
    /// Get all curve configurations for this pane
    /// </summary>
    public IReadOnlyList<CurveConfigurationModel> GetAllCurveConfigs() => _curveManager.GetAllCurveConfigs();

    /// <summary>
    /// Get all plotted curve info (plottable + config) for hover detection
    /// </summary>
    public IReadOnlyList<Models.PlottedCurveInfo> GetPlottedCurves() => _curveManager.GetPlottedCurves();

    /// <summary>
    /// Remove an event line from this pane
    /// </summary>
    public void RemoveEventLine(VerticalLine eventLine)
    {
        if (PlotModel == null)
            return;

        PlotModel.Remove(eventLine);
        _eventLines.Remove(eventLine);
        TriggerPlotUpdate();
    }

    /// <summary>
    /// Clear all event lines from this pane
    /// </summary>
    public void ClearEventLines()
    {
        if (PlotModel == null)
            return;

        foreach (var line in _eventLines)
        {
            PlotModel.Remove(line);
        }

        _eventLines.Clear();
        TriggerPlotUpdate();
    }

    /// <summary>
    /// Clear all curves from this pane. Event lines and annotations are preserved — "Clear Pane"
    /// promises to remove curves only, and nuking the whole PlotModel left the global event-line /
    /// annotation services holding references to detached plottables (reviews M2 / C4).
    /// </summary>
    public void Clear()
    {
        if (PlotModel == null)
            return;

        // Remove the per-pane legacy event lines (owned here, not by the global service).
        foreach (var line in _eventLines)
            PlotModel.Remove(line);
        _eventLines.Clear();

        // Remove curve plottables only; leaves global event lines + annotations on the plot.
        _curveManager.ClearCurves();

        TriggerPlotUpdate();
    }

    /// <summary>
    /// Auto-scale the axes
    /// </summary>
    public void AutoScale()
    {
        PlotModel?.Axes.AutoScale();
        TriggerPlotUpdate();
    }

    /// <summary>
    /// Apply formatting settings from PaneModel to the plot
    /// </summary>
    public void ApplyFormatting()
    {
        _formattingService.ApplyFormatting();
    }

    /// <summary>
    /// Set the X-axis range
    /// </summary>
    public void SetXAxisRange(double min, double max) => _formattingService.SetXAxisRange(min, max);

    /// <summary>
    /// Set the Y-axis range
    /// </summary>
    public void SetYAxisRange(double min, double max) => _formattingService.SetYAxisRange(min, max);

    /// <summary>
    /// Get the current X-axis range
    /// </summary>
    public (double Min, double Max)? GetXAxisRange() => _formattingService.GetXAxisRange();

    /// <summary>
    /// Get the current Y-axis range
    /// </summary>
    public (double Min, double Max)? GetYAxisRange() => _formattingService.GetYAxisRange();

    /// <summary>
    /// Set the Y2-axis range
    /// </summary>
    public void SetY2AxisRange(double min, double max) => _formattingService.SetY2AxisRange(min, max);

    /// <summary>
    /// Get the current Y2-axis range
    /// </summary>
    public (double Min, double Max)? GetY2AxisRange() => _formattingService.GetY2AxisRange();

    /// <summary>
    /// Calculate intersection points between event lines and curves in this pane
    /// </summary>
    public List<IntersectionPointModel> CalculateIntersections()
    {
        var intersections = new List<IntersectionPointModel>();

        foreach (var eventLine in _eventLines)
        {
            double xPosition = eventLine.X;

            // Use curve manager to get Y values for all curves at this X position
            var curveValues = GetCurveValuesAtX(xPosition);

            foreach (var (config, yValue) in curveValues)
            {
                intersections.Add(new IntersectionPointModel
                {
                    EventLineLabel = eventLine.Text,
                    XPosition = xPosition,
                    CurveName = config.CurveName,
                    YValue = yValue,
                    PaneIndex = PaneModel.Index,
                    YAxis = config.YAxis
                });
            }
        }

        return intersections;
    }

    /// <summary>
    /// Get event line at a specific position (for drag detection)
    /// </summary>
    public VerticalLine? GetEventLineAtPosition(double x, double tolerance)
    {
        foreach (var line in _eventLines)
        {
            if (Math.Abs(line.X - x) < tolerance)
                return line;
        }

        return null;
    }

    /// <summary>
    /// Get all event lines in this pane
    /// </summary>
    public IReadOnlyList<VerticalLine> GetEventLines() => _eventLines.AsReadOnly();

    /// <summary>
    /// Get all curves in this pane (legacy method)
    /// </summary>
    public IReadOnlyList<(Signal Signal, double Period, double[] Data, string CurveName)> GetCurves() => _curveManager.GetCurves();

    /// <summary>
    /// Show legend with proper font size formatting
    /// </summary>
    private void ShowLegendWithFormatting() => _formattingService.ShowLegendWithFormatting();

    /// <summary>
    /// Format X-value according to current axis formatting settings
    /// </summary>
    private string FormatXValue(double xValue) => _formattingService.FormatXValue(xValue);

    /// <summary>
    /// <summary>
    /// Build the bottom-pane event-line label, anchored just inside the top of the data area
    /// next to the line — matches the Compact Plot Surface convention. Bold, no border, label
    /// color tracks the event-line color so multiple lines are visually distinguishable.
    /// </summary>
    private ScottPlot.Plottables.Text CreateEventLineLabel(double xPosition, string label, string colorHex)
    {
        var yRange = PlotModel!.Axes.Left.Range;
        // Anchor just inside the top edge of the data area; UpperLeft alignment + small +X pixel
        // offset puts the label immediately to the right of the line, baseline at the top tick.
        var textAnnotation = PlotModel.Add.Text(label, xPosition, yRange.Max);
        textAnnotation.LabelFontSize = 13f;
        textAnnotation.LabelFontColor = ScottPlot.Color.FromHex(colorHex);
        textAnnotation.LabelBold = true;
        textAnnotation.LabelBackgroundColor = ScottPlot.Colors.Transparent;
        textAnnotation.LabelBorderWidth = 0;
        textAnnotation.LabelPadding = 2f;
        textAnnotation.LabelRotation = 0f;
        textAnnotation.LabelAlignment = Alignment.UpperLeft;
        // Pull off the left edge so the label clears the line itself, and drop a few pixels
        // from the top tick row so the cap-height isn't clipped by the plot frame.
        textAnnotation.OffsetX = 4f;
        textAnnotation.OffsetY = 3f;
        return textAnnotation;
    }

    /// <summary>
    /// Find event line ID by mouse X coordinate (for hit testing)
    /// </summary>
    public Guid? FindEventLineAtX(double dataX, double tolerance)
    {
        foreach (var kvp in _globalEventLines)
        {
            double lineX = kvp.Value.X;
            if (Math.Abs(dataX - lineX) <= tolerance)
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Get event line label name (stored separately to avoid accessing obsolete API)
    /// </summary>
    private readonly Dictionary<Guid, string> _eventLineLabelNames = new();

    /// <summary>
    /// Get event line label text (for preserving during drag)
    /// </summary>
    public string? GetEventLineLabel(Guid eventLineId)
    {
        return _eventLineLabelNames.TryGetValue(eventLineId, out var labelName) ? labelName : null;
    }

    /// <summary>
    /// Update Y-axis and Y2-axis labels based on the parameter names of curves currently plotted
    /// </summary>
    private void UpdateAxisLabelsFromCurves()
    {
        if (PlotModel == null)
            return;

        // Collect parameter names for Y1 (left) and Y2 (right) axes, ordered, deduped.
        var y1Seen = new HashSet<string>(StringComparer.Ordinal);
        var y2Seen = new HashSet<string>(StringComparer.Ordinal);
        var y1ParameterNames = new List<string>();
        var y2ParameterNames = new List<string>();

        foreach (var (_, _, _, config) in _curves.Values)
        {
            var token = BuildAxisToken(config);
            if (string.IsNullOrEmpty(token)) continue;
            if (config.YAxis == YAxisType.Y1 && y1Seen.Add(token))
                y1ParameterNames.Add(token);
            else if (config.YAxis == YAxisType.Y2 && y2Seen.Add(token))
                y2ParameterNames.Add(token);
        }

        // Update Y1 (left) axis label
        if (y1ParameterNames.Count > 0)
        {
            PaneModel.YAxisLabel = string.Join(" | ", y1ParameterNames);
            PlotModel.Axes.Left.Label.Text = PaneModel.YAxisLabel;
        }
        else
        {
            PaneModel.YAxisLabel = "Value";
            PlotModel.Axes.Left.Label.Text = "Value";
        }

        // Update Y2 (right) axis label and visibility
        if (y2ParameterNames.Count > 0)
        {
            PaneModel.Y2AxisLabel = string.Join(" | ", y2ParameterNames);
            PlotModel.Axes.Right.Label.Text = PaneModel.Y2AxisLabel;
            PlotModel.Axes.Right.Label.FontSize = (float)PaneModel.AxisLabelFontSize;
            PlotModel.Axes.Right.Label.Bold = PaneModel.AxisLabelBold;
            PlotModel.Axes.Right.TickLabelStyle.FontSize = (float)PaneModel.TickLabelFontSize;
            PaneModel.ShowY2Axis = true;
            PlotModel.Axes.Right.IsVisible = true;
        }
        else
        {
            // No curves on Y2 axis, hide it
            PaneModel.ShowY2Axis = false;
            PlotModel.Axes.Right.IsVisible = false;
        }
    }

    /// <summary>
    /// Build one axis-label token for a curve: the user-facing display name (rename-aware) plus its
    /// unit. The default <see cref="CurveConfigurationModel.CurveName"/> already embeds the unit in
    /// parens (e.g. "Mach (ratio)"), so the unit is only appended when the name doesn't already carry
    /// it — a renamed curve ("GNormal" + unit "G") becomes "GNormal (G)" without double-printing.
    /// </summary>
    private static string BuildAxisToken(CurveConfigurationModel config)
    {
        var name = string.IsNullOrEmpty(config.CurveName) ? config.YColumnName : config.CurveName;
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var unit = config.Unit?.Trim();
        if (!string.IsNullOrEmpty(unit) && name.IndexOf($"({unit})", StringComparison.OrdinalIgnoreCase) < 0)
            name = $"{name} ({unit})";
        return name;
    }

    /// <summary>
    /// Get the Y value for a specific curve at a given X coordinate
    /// </summary>
    public double GetCurveYValueAtX(Guid curveId, double x) => _curveManager.GetCurveYValueAtX(curveId, x);

    /// <summary>
    /// Find the closest curve to a given coordinate point
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y1">Y coordinate in Y1 (left) axis scale</param>
    /// <param name="y2">Y coordinate in Y2 (right) axis scale</param>
    public (Guid CurveId, YAxisType YAxis, double Distance)? GetClosestCurveAt(double x, double y1, double y2) =>
        _curveManager.GetClosestCurveAt(x, y1, y2);

    // Event for plot updates
    public event Action? OnPlotUpdated;

    private bool _isDisposed;

    /// <summary>
    /// True once <see cref="Dispose"/> has run. Exposed for assertions/tests.
    /// </summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>
    /// Clear all event subscribers and drop the plot reference so the pane can be GC'd.
    /// Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        if (_axisLabelsHandler is not null)
        {
            _curveManager.OnAxisLabelsNeedUpdate -= _axisLabelsHandler;
            _axisLabelsHandler = null;
        }
        OnPlotUpdated = null;
        _plotModel = null;
        lock (_plotReadyLock)
        {
            _plotReadyTcs?.TrySetCanceled();
            _plotReadyTcs = null;
        }
    }

    #region Global Event Line Methods

    /// <summary>
    /// Add a global event line visual to this pane.
    /// Global event lines are synchronized across all panes and tracked by ID.
    /// </summary>
    /// <param name="eventLineId">Unique ID for the global event line</param>
    /// <param name="xPosition">X-axis position</param>
    /// <param name="label">Label text (shown only if showLabel is true)</param>
    /// <param name="showLabel">Whether to show the label (typically only true for bottom pane)</param>
    /// <param name="color">Line color in hex format</param>
    /// <returns>The created VerticalLine plottable</returns>
    public VerticalLine? AddGlobalEventLineVisual(Guid eventLineId, double xPosition, string label, bool showLabel, string color = "#FFB900")
    {
        if (PlotModel == null)
            return null;

        // Remove existing if present (for updates)
        if (_globalEventLines.ContainsKey(eventLineId))
        {
            RemoveGlobalEventLine(eventLineId);
        }

        // Create vertical line (no built-in label)
        var eventLine = PlotModel.Add.VerticalLine(xPosition);
        eventLine.Color = ScottPlot.Color.FromHex(color);
        eventLine.LineWidth = 2;
        eventLine.IsDraggable = false;  // We implement custom drag

        _globalEventLines[eventLineId] = eventLine;

        // Create separate text annotation for label (if showing)
        if (showLabel)
        {
            _globalEventLineLabels[eventLineId] = CreateEventLineLabel(xPosition, label, color);
            _eventLineLabelNames[eventLineId] = label;
        }

        TriggerPlotUpdate();
        return eventLine;
    }

    /// <summary>Resolve the event-line color back from the live <see cref="VerticalLine"/> plottable
    /// (canonical source — the user can recolor a line and we need labels to track).</summary>
    private string GetEventLineColorHex(Guid eventLineId)
    {
        if (_globalEventLines.TryGetValue(eventLineId, out var line))
            return line.Color.ToHex();
        return "#FFB900";
    }

    /// <summary>
    /// Move a global event line to a new X position
    /// </summary>
    /// <param name="eventLineId">ID of the event line to move</param>
    /// <param name="newXPosition">New X position</param>
    public void MoveGlobalEventLine(Guid eventLineId, double newXPosition)
    {
        // Update vertical line position
        if (_globalEventLines.TryGetValue(eventLineId, out var eventLine))
        {
            eventLine.X = newXPosition;
        }

        // Mutate the label in place — Remove + Add per drag frame caused per-pane Refresh()
        // calls + ScottPlot layout recomputation, which manifested as visible flicker across
        // the stack on every cursor move.
        if (_globalEventLineLabels.TryGetValue(eventLineId, out var label))
        {
            var yMax = PlotModel?.Axes.Left.Range.Max ?? label.Location.Y;
            label.Location = new Coordinates(newXPosition, yMax);
        }

        TriggerPlotUpdate();
    }

    /// <summary>
    /// Remove a global event line from this pane
    /// </summary>
    /// <param name="eventLineId">ID of the event line to remove</param>
    /// <returns>True if removed, false if not found</returns>
    public bool RemoveGlobalEventLine(Guid eventLineId)
    {
        if (PlotModel == null)
            return false;

        bool removed = false;

        // Remove vertical line
        if (_globalEventLines.TryGetValue(eventLineId, out var eventLine))
        {
            PlotModel.Remove(eventLine);
            _globalEventLines.Remove(eventLineId);
            removed = true;
        }

        // Remove label text annotation
        if (_globalEventLineLabels.TryGetValue(eventLineId, out var label))
        {
            PlotModel.Remove(label);
            _globalEventLineLabels.Remove(eventLineId);
            _eventLineLabelNames.Remove(eventLineId);
            removed = true;
        }

        if (removed)
            TriggerPlotUpdate();

        return removed;
    }

    /// <summary>
    /// Clear all global event lines from this pane
    /// </summary>
    public void ClearGlobalEventLines()
    {
        if (PlotModel == null)
            return;

        // Remove all vertical lines
        foreach (var eventLine in _globalEventLines.Values)
        {
            PlotModel.Remove(eventLine);
        }
        _globalEventLines.Clear();

        // Remove all label text annotations
        foreach (var label in _globalEventLineLabels.Values)
        {
            PlotModel.Remove(label);
        }
        _globalEventLineLabels.Clear();
        _eventLineLabelNames.Clear();

        TriggerPlotUpdate();
    }

    /// <summary>
    /// Get a global event line by ID
    /// </summary>
    public VerticalLine? GetGlobalEventLine(Guid eventLineId)
    {
        return _globalEventLines.TryGetValue(eventLineId, out var eventLine) ? eventLine : null;
    }

    /// <summary>
    /// Get all global event line IDs in this pane
    /// </summary>
    public IReadOnlyCollection<Guid> GetGlobalEventLineIds() => _globalEventLines.Keys.ToList().AsReadOnly();

    /// <summary>
    /// Update the label visibility for a global event line
    /// </summary>
    /// <param name="eventLineId">ID of the event line</param>
    /// <param name="showLabel">Whether to show the label</param>
    /// <param name="label">Label text (required if showLabel is true)</param>
    public void UpdateGlobalEventLineLabel(Guid eventLineId, bool showLabel, string label)
    {
        if (!_globalEventLines.TryGetValue(eventLineId, out var eventLine))
            return;

        double xPosition = eventLine.X;

        // Remove existing label if present
        if (_globalEventLineLabels.TryGetValue(eventLineId, out var existingLabel))
        {
            PlotModel?.Remove(existingLabel);
            _globalEventLineLabels.Remove(eventLineId);
            _eventLineLabelNames.Remove(eventLineId);
        }

        // Add new label if showing
        if (showLabel && PlotModel != null)
        {
            _globalEventLineLabels[eventLineId] = CreateEventLineLabel(xPosition, label, GetEventLineColorHex(eventLineId));
            _eventLineLabelNames[eventLineId] = label;
        }

        TriggerPlotUpdate();
    }

    #endregion

    #region Callout Annotation Methods

    /// <summary>
    /// Add a callout annotation at an intersection point
    /// </summary>
    public void AddCalloutAnnotation(Guid calloutId, double intersectionX, double intersectionY,
        string labelText, double offsetX, double offsetY, YAxisType yAxisType = YAxisType.Y1) =>
        _annotationManager.AddCalloutAnnotation(calloutId, intersectionX, intersectionY, labelText, offsetX, offsetY, yAxisType);

    /// <summary>
    /// Update the position of a callout annotation (after user drag)
    /// </summary>
    public void UpdateCalloutPosition(Guid calloutId, double intersectionX, double intersectionY,
        double newOffsetX, double newOffsetY) =>
        _annotationManager.UpdateCalloutPosition(calloutId, intersectionX, intersectionY, newOffsetX, newOffsetY);

    /// <summary>
    /// Update the value displayed in a callout
    /// </summary>
    public void UpdateCalloutValue(Guid calloutId, double newValue, string format = "F3") =>
        _annotationManager.UpdateCalloutValue(calloutId, newValue, format);

    /// <summary>
    /// Remove a callout annotation
    /// </summary>
    public bool RemoveCalloutAnnotation(Guid calloutId) => _annotationManager.RemoveCalloutAnnotation(calloutId);

    /// <summary>
    /// Clear all callout annotations from this pane
    /// </summary>
    public void ClearCalloutAnnotations() => _annotationManager.ClearCalloutAnnotations();

    /// <summary>
    /// Get all callout IDs in this pane
    /// </summary>
    public IReadOnlyCollection<Guid> GetCalloutIds() => _annotationManager.GetCalloutIds();

    /// <summary>
    /// Check if a callout exists in this pane
    /// </summary>
    public bool HasCallout(Guid calloutId) => _annotationManager.HasCallout(calloutId);

    /// <summary>
    /// Get the callout plottable for hit testing during drag
    /// </summary>
    public ScottPlot.Plottables.Callout? GetCallout(Guid calloutId) => _annotationManager.GetCallout(calloutId);

    /// <summary>
    /// Get all callouts in this pane (for hit testing)
    /// </summary>
    public IReadOnlyCollection<ScottPlot.Plottables.Callout> GetAllCallouts() => _annotationManager.GetAllCallouts();

    /// <summary>
    /// Find a callout ID by its plottable reference
    /// </summary>
    public Guid? FindCalloutId(ScottPlot.Plottables.Callout callout) => _annotationManager.FindCalloutId(callout);

    /// <summary>
    /// Calculate Y value at an X position for all visible curves in this pane.
    /// Used for creating intersection callouts.
    /// </summary>
    /// <param name="xPosition">X position to calculate intersections at</param>
    /// <returns>List of tuples containing curve info and Y value</returns>
    public List<(CurveConfigurationModel Config, double YValue)> GetCurveValuesAtX(double xPosition) =>
        _curveManager.GetCurveValuesAtX(xPosition);

    #endregion

    #region Text Annotation Methods

    /// <summary>
    /// Add a text annotation to this pane
    /// </summary>
    public void AddTextAnnotation(TextAnnotationModel model) => _annotationManager.AddTextAnnotation(model);

    /// <summary>
    /// Update a text annotation's appearance
    /// </summary>
    public void UpdateTextAnnotation(TextAnnotationModel model) => _annotationManager.UpdateTextAnnotation(model);

    /// <summary>
    /// Update only the position of a text annotation (for drag operations)
    /// </summary>
    public void UpdateTextAnnotationPosition(Guid annotationId, double x, double y) =>
        _annotationManager.UpdateTextAnnotationPosition(annotationId, x, y);

    /// <summary>
    /// Remove a text annotation
    /// </summary>
    public bool RemoveTextAnnotation(Guid annotationId) => _annotationManager.RemoveTextAnnotation(annotationId);

    /// <summary>
    /// Clear all text annotations
    /// </summary>
    public void ClearTextAnnotations() => _annotationManager.ClearTextAnnotations();

    /// <summary>
    /// Get all text annotation IDs in this pane
    /// </summary>
    public IReadOnlyCollection<Guid> GetTextAnnotationIds() => _annotationManager.GetTextAnnotationIds();

    /// <summary>
    /// Check if a text annotation exists
    /// </summary>
    public bool HasTextAnnotation(Guid annotationId) => _annotationManager.HasTextAnnotation(annotationId);

    /// <summary>
    /// Get a text annotation plottable for hit testing
    /// </summary>
    public ScottPlot.Plottables.Text? GetTextAnnotation(Guid annotationId) => _annotationManager.GetTextAnnotation(annotationId);

    /// <summary>
    /// Get all text annotations for hit testing
    /// </summary>
    public IReadOnlyCollection<ScottPlot.Plottables.Text> GetAllTextAnnotations() => _annotationManager.GetAllTextAnnotations();

    /// <summary>
    /// Find a text annotation ID by its plottable reference
    /// </summary>
    public Guid? FindTextAnnotationId(ScottPlot.Plottables.Text textPlottable) => _annotationManager.FindTextAnnotationId(textPlottable);

    #endregion

    #region Arrow Annotation Methods

    /// <summary>
    /// Add an arrow annotation to this pane
    /// </summary>
    public void AddArrowAnnotation(ArrowAnnotationModel model) => _annotationManager.AddArrowAnnotation(model);

    /// <summary>
    /// Update an arrow annotation's appearance
    /// </summary>
    public void UpdateArrowAnnotation(ArrowAnnotationModel model) => _annotationManager.UpdateArrowAnnotation(model);

    /// <summary>
    /// Remove an arrow annotation
    /// </summary>
    public bool RemoveArrowAnnotation(Guid annotationId) => _annotationManager.RemoveArrowAnnotation(annotationId);

    /// <summary>
    /// Clear all arrow annotations
    /// </summary>
    public void ClearArrowAnnotations() => _annotationManager.ClearArrowAnnotations();

    /// <summary>
    /// Get all arrow annotation IDs in this pane
    /// </summary>
    public IReadOnlyCollection<Guid> GetArrowAnnotationIds() => _annotationManager.GetArrowAnnotationIds();

    /// <summary>
    /// Check if an arrow annotation exists
    /// </summary>
    public bool HasArrowAnnotation(Guid annotationId) => _annotationManager.HasArrowAnnotation(annotationId);

    /// <summary>
    /// Get an arrow annotation plottable for hit testing
    /// </summary>
    public ScottPlot.Plottables.Arrow? GetArrowAnnotation(Guid annotationId) => _annotationManager.GetArrowAnnotation(annotationId);

    /// <summary>
    /// Get all arrow annotations for hit testing
    /// </summary>
    public IReadOnlyCollection<ScottPlot.Plottables.Arrow> GetAllArrowAnnotations() => _annotationManager.GetAllArrowAnnotations();

    /// <summary>
    /// Find an arrow annotation ID by its plottable reference
    /// </summary>
    public Guid? FindArrowAnnotationId(ScottPlot.Plottables.Arrow arrowPlottable) => _annotationManager.FindArrowAnnotationId(arrowPlottable);

    #endregion
}
