using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.Services;

/// <summary>
/// Manages serialization and deserialization of project state
/// </summary>
public class ProjectStateManager : IProjectStateManager
{
    /// <summary>
    /// Save current application state to a project model
    /// </summary>
    /// <param name="project">Target project to save state into</param>
    /// <param name="currentData">Current plot data</param>
    /// <param name="panes">Collection of panes</param>
    /// <param name="activeCurves">Collection of active curves</param>
    /// <param name="globalEventLines">Global event lines to save (optional)</param>
    /// <param name="callouts">Intersection callouts to save (optional)</param>
    /// <param name="textAnnotations">Text annotations to save (optional)</param>
    /// <param name="arrowAnnotations">Arrow annotations to save (optional)</param>
    public void SaveCurrentState(
        ProjectSettingsModel project,
        PlotDataModel? currentData,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves,
        IReadOnlyList<EventLineModel>? globalEventLines = null,
        IReadOnlyList<IntersectionCalloutModel>? callouts = null,
        IReadOnlyList<TextAnnotationModel>? textAnnotations = null,
        IReadOnlyList<ArrowAnnotationModel>? arrowAnnotations = null,
        IReadOnlyList<CompactCurveModel>? compactCurves = null,
        IReadOnlyList<EventLineModel>? compactEventLines = null)
    {
        project.PlotData = currentData;
        project.PaneCount = panes.Count;

        if (compactCurves != null)
        {
            project.CompactCurves.Clear();
            foreach (var c in compactCurves)
                project.CompactCurves.Add(c);
        }

        if (compactEventLines != null)
        {
            project.CompactEventLines.Clear();
            foreach (var e in compactEventLines)
                project.CompactEventLines.Add(e);
        }

        // Save all pane configurations
        project.Panes.Clear();
        foreach (var pane in panes)
        {
            project.Panes.Add(pane.PaneModel);
        }

        // Save all curve configurations
        project.Curves.Clear();
        foreach (var curve in activeCurves)
        {
            var plotCurve = new PlotCurveModel
            {
                Id = curve.Id,
                Name = curve.CurveName,
                SourceColumn = curve.YColumnName ?? string.Empty,
                XAxisColumn = curve.XColumnName,
                Color = curve.Color,
                LineWidth = curve.LineWidth,
                LinePattern = MapLineStyleToLinePattern(curve.LineStyle),
                YAxis = curve.YAxis,
                PaneIndex = curve.PaneIndex,
                IsVisible = curve.IsVisible,
                ShowMarkers = curve.ShowMarkers,
                MarkerStyle = curve.MarkerStyle,
                MarkerSize = curve.MarkerSize,
                MarkerColor = curve.MarkerColor
            };
            project.Curves.Add(plotCurve);
        }

        // Save event lines
        project.EventLines.Clear();

        // Save global event lines if provided
        if (globalEventLines != null)
        {
            foreach (var eventLine in globalEventLines)
            {
                project.EventLines.Add(eventLine);
            }
        }
        else
        {
            // Fallback: save per-pane event lines (legacy behavior)
            foreach (var pane in panes)
            {
                var paneEventLines = pane.GetEventLines();
                foreach (var eventLine in paneEventLines)
                {
                    project.EventLines.Add(new EventLineModel
                    {
                        Label = eventLine.Text,
                        XPosition = eventLine.X,
                        Color = $"#{eventLine.Color.ToHex()}",
                        PaneIndex = pane.PaneModel.Index,
                        IsGlobal = false
                    });
                }
            }
        }

        // Save intersection callouts
        project.IntersectionCallouts.Clear();
        if (callouts != null)
        {
            foreach (var callout in callouts)
            {
                project.IntersectionCallouts.Add(callout);
            }
        }

        // Save text annotations
        project.TextAnnotations.Clear();
        if (textAnnotations != null)
        {
            foreach (var annotation in textAnnotations)
            {
                project.TextAnnotations.Add(annotation);
            }
        }

        // Save arrow annotations
        project.ArrowAnnotations.Clear();
        if (arrowAnnotations != null)
        {
            foreach (var annotation in arrowAnnotations)
            {
                project.ArrowAnnotations.Add(annotation);
            }
        }

        // Save axis ranges from first pane for legacy compatibility
        if (panes.Count > 0 && panes[0].PlotModel != null)
        {
            var plotModel = panes[0].PlotModel!;
            var xRange = plotModel.Axes.Bottom.Range;
            var yRange = plotModel.Axes.Left.Range;

            // An empty / never-scaled axis reports ±Infinity. Store null instead of a
            // non-finite range so the file stays clean and load falls back to auto-scale.
            project.XAxisMin = Finite(xRange.Min);
            project.XAxisMax = Finite(xRange.Max);
            project.YAxisMin = Finite(yRange.Min);
            project.YAxisMax = Finite(yRange.Max);
        }
    }

    /// <summary>Returns the value when finite, else null (drops ±Infinity / NaN axis ranges).</summary>
    private static double? Finite(double v) => double.IsFinite(v) ? v : null;

    /// <summary>
    /// Restore application state from a project model
    /// </summary>
    /// <param name="project">Source project to restore from</param>
    /// <param name="currentData">Current plot data model</param>
    /// <param name="panes">Target panes collection to populate</param>
    /// <param name="activeCurves">Target active curves collection to populate</param>
    /// <param name="onGlobalEventLinesRestored">Callback to restore global event lines</param>
    /// <param name="onCalloutsRestored">Callback to restore callouts</param>
    /// <param name="onTextAnnotationsRestored">Callback to restore text annotations</param>
    /// <param name="onArrowAnnotationsRestored">Callback to restore arrow annotations</param>
    public async Task RestoreProjectState(
        ProjectSettingsModel project,
        PlotDataModel? currentData,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves,
        Action<IEnumerable<EventLineModel>>? onGlobalEventLinesRestored = null,
        Action<IEnumerable<IntersectionCalloutModel>>? onCalloutsRestored = null,
        Action<IEnumerable<TextAnnotationModel>>? onTextAnnotationsRestored = null,
        Action<IEnumerable<ArrowAnnotationModel>>? onArrowAnnotationsRestored = null,
        Action<IEnumerable<EventLineModel>>? onCompactEventLinesRestored = null)
    {
        // Compact event lines live on a single OxyPlot surface that is independent of the
        // ScottPlot pane setup below — restore them up front so the surface refreshes
        // alongside curve restoration.
        if (project.CompactEventLines.Count > 0 && onCompactEventLinesRestored != null)
        {
            onCompactEventLinesRestored(project.CompactEventLines);
        }

        // Clear existing state
        foreach (var pane in panes)
        {
            pane.Clear();
            pane.Dispose();
        }
        panes.Clear();
        activeCurves.Clear();

        // Restore panes
        int paneCount = Math.Max(project.PaneCount, project.Panes.Count);
        if (paneCount == 0)
            paneCount = 1; // Ensure at least one pane

        for (int i = 0; i < paneCount; i++)
        {
            PlotPaneModel paneModel;
            if (i < project.Panes.Count)
            {
                paneModel = project.Panes[i];
            }
            else
            {
                // Create default pane if not in saved config
                paneModel = new PlotPaneModel
                {
                    Index = i,
                    Name = $"Pane {i + 1}",
                    XAxisLabel = project.XAxisLabel,
                    YAxisLabel = project.YAxisLabel,
                    ShowGrid = project.ShowGrid,
                    ShowLegend = project.ShowLegend,
                    // Use default decimal places (0) - smart defaults not applied during restore
                    XAxisDecimalPlaces = 0,
                    Y1AxisDecimalPlaces = 0,
                    Y2AxisDecimalPlaces = 0
                };
            }

            var paneViewModel = new PlotPaneViewModel(paneModel);
            panes.Add(paneViewModel);
        }

        // Wait for all pane PlotModels to be materialized by the view before touching them.
        // Timeout guards headless/test contexts where no view ever sets PlotModel.
        var readyTasks = panes.Select(p => p.WhenPlotReady()).ToArray();
        if (readyTasks.Length > 0)
        {
            var allReady = Task.WhenAll(readyTasks);
            // Must stay on UI thread — RestoreCurves mutates ScottPlot.Plot which is not thread-safe.
            await Task.WhenAny(allReady, Task.Delay(TimeSpan.FromSeconds(5)));
        }

        // Batch all restore mutations so OnPlotUpdated fires once per pane at the end
        // instead of after every Add.Scatter / AddEventLine. Without this, Avalonia's
        // render thread can iterate Plot.PlottableList (in ScottPlot's RegenerateTicks)
        // while this UI thread is still adding plottables, throwing
        // "Collection was modified" mid-render.
        var updateScopes = panes.Select(p => p.BeginUpdate()).ToList();
        try
        {
            // Restore curves if data is available
            if (currentData?.Data != null)
            {
                RestoreCurves(project, currentData, panes, activeCurves);
            }

            // Auto-scale axes to the restored curve data BEFORE event lines, callouts, and
            // annotations are placed — those compute label/anchor positions from the current
            // axis range, so they'd land outside the visible area if the plot still held the
            // default -10..10 range. Also runs before ApplyFormatting, which captures the
            // current range to preserve user zoom and would otherwise lock in the default.
            // The actual final-range selection (saved vs autoscaled) is centralised in
            // ResolveFinalAxisRange, then re-applied by pane.ApplyFormatting() below.
            foreach (var pane in panes)
            {
                if (pane.PlotModel == null) continue;
                var model = pane.PaneModel;
                bool needsAutoScale = !HasSavedRange(model.XAxisMin, model.XAxisMax)
                                   || !HasSavedRange(model.YAxisMin, model.YAxisMax)
                                   || !HasSavedRange(model.Y2AxisMin, model.Y2AxisMax);
                if (needsAutoScale)
                {
                    pane.PlotModel.Axes.AutoScale();
                }
            }

            // Restore event lines
            var globalEventLines = project.EventLines.Where(e => e.IsGlobal).ToList();
            var legacyEventLines = project.EventLines.Where(e => !e.IsGlobal).ToList();

            // Always invoke callbacks (even for empty collections) so the underlying
            // singleton services clear any stale state from the previous project.
            if (onGlobalEventLinesRestored != null)
            {
                onGlobalEventLinesRestored(globalEventLines);
            }
            else if (legacyEventLines.Count > 0)
            {
                // Restore legacy per-pane event lines directly
                RestoreEventLines(project, panes);
            }

            onCalloutsRestored?.Invoke(project.IntersectionCallouts);
            onTextAnnotationsRestored?.Invoke(project.TextAnnotations);
            onArrowAnnotationsRestored?.Invoke(project.ArrowAnnotations);

            // Apply formatting to all panes
            foreach (var pane in panes)
            {
                pane.ApplyFormatting();
            }
        }
        finally
        {
            foreach (var scope in updateScopes) scope.Dispose();
        }
    }

    /// <summary>
    /// Restore curves from project configuration
    /// </summary>
    private void RestoreCurves(
        ProjectSettingsModel project,
        PlotDataModel currentData,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves)
    {
        foreach (var savedCurve in project.Curves)
        {
            // Validate pane index
            if (savedCurve.PaneIndex < 0 || savedCurve.PaneIndex >= panes.Count)
                continue;

            var pane = panes[savedCurve.PaneIndex];

            // Validate column exists
            if (!currentData.Data.Columns.Contains(savedCurve.SourceColumn))
                continue;

            // Extract Y data via the typed, cached accessor.
            double[] yData;
            try
            {
                yData = currentData.GetColumnData(savedCurve.SourceColumn);
            }
            catch
            {
                continue;
            }

            double[] xData = GetXDataForCurve(currentData, savedCurve, yData.Length);

            // Create curve configuration
            var curveConfig = new CurveConfigurationModel
            {
                Id = savedCurve.Id,
                CurveName = savedCurve.Name,
                YColumnName = savedCurve.SourceColumn,
                XColumnName = savedCurve.XAxisColumn,
                Color = savedCurve.Color,
                LineWidth = savedCurve.LineWidth,
                LineStyle = MapLinePatternToLineStyle(savedCurve.LinePattern),
                YAxis = savedCurve.YAxis,
                PaneIndex = savedCurve.PaneIndex,
                IsVisible = savedCurve.IsVisible,
                ShowMarkers = savedCurve.ShowMarkers,
                MarkerStyle = savedCurve.MarkerStyle,
                MarkerSize = savedCurve.MarkerSize,
                MarkerColor = savedCurve.MarkerColor ?? savedCurve.Color
            };

            // Add curve to pane
            pane.AddScatterCurve(xData, yData, curveConfig);
            activeCurves.Add(curveConfig);
        }
    }

    /// <summary>
    /// Restore event lines from project configuration
    /// </summary>
    private void RestoreEventLines(
        ProjectSettingsModel project,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var eventLineModel in project.EventLines)
        {
            int paneIndex = eventLineModel.PaneIndex;
            if (paneIndex >= 0 && paneIndex < panes.Count)
            {
                var pane = panes[paneIndex];
                pane.AddEventLine(eventLineModel.XPosition, eventLineModel.Label, eventLineModel.Color);
            }
        }
    }

    /// <summary>
    /// Get X data for a restored curve, honoring its saved XAxisColumn when present.
    /// Falls back to the legacy first-column behavior when no saved column exists or
    /// the saved column is no longer in the data.
    /// </summary>
    private static double[] GetXDataForCurve(PlotDataModel currentData, PlotCurveModel curve, int length)
    {
        if (!string.IsNullOrEmpty(curve.XAxisColumn)
            && currentData.Data.Columns.Contains(curve.XAxisColumn))
        {
            try
            {
                return currentData.GetColumnData(curve.XAxisColumn);
            }
            catch
            {
                // Fall through to legacy first-column behavior.
            }
        }

        return GetXData(currentData, length);
    }

    /// <summary>
    /// Get X data from the first numeric column, or generate sequential values
    /// </summary>
    private static double[] GetXData(PlotDataModel currentData, int length)
    {
        var firstNumericCol = currentData.ColumnNames.FirstOrDefault();
        if (firstNumericCol != null && currentData.Data.Columns.Contains(firstNumericCol))
        {
            try
            {
                return currentData.GetColumnData(firstNumericCol);
            }
            catch
            {
                // Fall through to sequential fallback below.
            }
        }

        var xs = new double[length];
        for (int i = 0; i < length; i++) xs[i] = i;
        return xs;
    }

    /// <summary>
    /// Map LineStyle enum to LinePatternType
    /// </summary>
    private static LinePatternType MapLineStyleToLinePattern(Models.LineStyle lineStyle) => lineStyle switch
    {
        Models.LineStyle.Solid => LinePatternType.Solid,
        Models.LineStyle.Dash => LinePatternType.Dashed,
        Models.LineStyle.Dot => LinePatternType.Dotted,
        Models.LineStyle.DashDot => LinePatternType.DashDot,
        _ => LinePatternType.Solid
    };

    /// <summary>
    /// Map LinePatternType to LineStyle enum
    /// </summary>
    private static Models.LineStyle MapLinePatternToLineStyle(LinePatternType linePattern) => linePattern switch
    {
        LinePatternType.Solid => Models.LineStyle.Solid,
        LinePatternType.Dashed => Models.LineStyle.Dash,
        LinePatternType.Dotted => Models.LineStyle.Dot,
        LinePatternType.DashDot => Models.LineStyle.DashDot,
        _ => Models.LineStyle.Solid
    };

    // Saved range overrides autoscale; this is the single source of truth for restored axis ranges.
    /// <summary>
    /// Resolve the final axis (min, max) used after project restore: saved manual range wins,
    /// otherwise the autoscaled range from the currently restored data. Both paths in restore
    /// (the AutoScale fallback above and <c>PlotPaneViewModel.ApplyFormatting</c>) must agree
    /// on which range wins — this helper makes that decision explicit and testable.
    /// </summary>
    public static (double Min, double Max) ResolveFinalAxisRange(
        double? savedMin, double? savedMax, (double Min, double Max) autoScaled)
    {
        if (HasSavedRange(savedMin, savedMax))
            return (savedMin!.Value, savedMax!.Value);
        return autoScaled;
    }

    /// <summary>True when both endpoints are set; pair semantics — saved range needs both ends.</summary>
    public static bool HasSavedRange(double? min, double? max) => min.HasValue && max.HasValue;
}
