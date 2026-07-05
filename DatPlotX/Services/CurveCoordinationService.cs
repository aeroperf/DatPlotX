using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for coordinating curve operations across panes.
/// Handles curve plotting, replotting, and decimal formatting coordination.
/// </summary>
public class CurveCoordinationService : ICurveCoordinationService
{
    /// <summary>
    /// Plot a single curve to a specific pane
    /// </summary>
    public void PlotSingleCurveToPane(
        int targetPaneIndex,
        string parameterName,
        string yAxisType,
        PlotDataModel currentData,
        string selectedXColumn,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves,
        System.Collections.Generic.IReadOnlyList<string> colorPalette,
        string? unitOverride = null)
    {
        if (currentData == null || targetPaneIndex < 0 || targetPaneIndex >= panes.Count)
            return;

        if (string.IsNullOrEmpty(selectedXColumn))
            return;

        // Get X and Y data
        var xData = currentData.GetColumnData(selectedXColumn);
        var yData = currentData.GetColumnData(parameterName);

        // Get target pane
        var targetPane = panes[targetPaneIndex];

        // Get current curve count to determine color
        int curveIndex = targetPane.GetAllCurveConfigs().Count;
        string curveColor = colorPalette[curveIndex % colorPalette.Count];

        // Create curve configuration. Auto-parse units from the column header so the
        // Analysis panel can render "Max: 12 500 ft" and the Slope metric can derive a
        // ft/min rate (see Services/Units/UnitHeaderParser). The display name stays the
        // raw column name — the unit is shown separately by the panel, not duplicated
        // in the legend.
        // Prefer an explicit unit from the dialog (the user may have edited the auto-parsed
        // value); fall back to parsing the column header.
        var unit = !string.IsNullOrWhiteSpace(unitOverride)
            ? unitOverride.Trim()
            : Services.Units.UnitHeaderParser.Parse(parameterName).Unit;

        var curveConfig = new CurveConfigurationModel
        {
            CurveName = parameterName,
            YColumnName = parameterName,
            XColumnName = selectedXColumn,
            Unit = unit,
            PaneIndex = targetPaneIndex,
            YAxis = Enum.TryParse<YAxisType>(yAxisType, ignoreCase: true, out var parsed) ? parsed : YAxisType.Y1,
            Color = curveColor,
            ShowMarkers = true,
            MarkerStyle = MarkerStyle.Circle,
            MarkerColor = curveColor,
            MarkerSize = 5.0
        };

        // Check if this is the first curve being added to this pane
        bool isFirstCurve = (curveIndex == 0);

        // Detect the first curve to land on the Y2 axis: the smart-default pass below only
        // runs on the first curve overall (typically a Y1 curve), when the Y2 axis doesn't
        // exist yet — so without this, a Y2 curve added later keeps Y2 decimals at 0 and its
        // ticks collapse to integers. See ApplyY2SmartDecimalDefaults.
        bool isFirstY2Curve = curveConfig.YAxis == YAxisType.Y2 &&
                              !targetPane.GetAllCurveConfigs().Any(c => c.YAxis == YAxisType.Y2);

        // Add curve to the target pane
        targetPane.AddScatterCurve(xData, yData, curveConfig);

        // Track the active curve
        activeCurves.Add(curveConfig);

        // Auto-scale the pane
        targetPane.AutoScale();

        // If this is the FIRST curve in the pane, apply smart defaults and synchronize
        if (isFirstCurve)
        {
            ApplySmartDecimalDefaultsWithSync(targetPane, panes);
        }
        else if (isFirstY2Curve)
        {
            // First Y1 curve already set X/Y1 decimals; only Y2 needs computing now.
            ApplyY2SmartDecimalDefaults(targetPane);
        }
    }

    /// <summary>
    /// Replot all curves with the current X-axis selection
    /// </summary>
    public bool ReplotAllCurves(
        PlotDataModel currentData,
        string selectedXColumn,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves)
    {
        if (currentData == null || string.IsNullOrEmpty(selectedXColumn) || activeCurves.Count == 0)
            return false;

        if (!TryGetColumnData(currentData, selectedXColumn, out var xData))
            return false;

        // Suppress per-curve update events — fire one batched update per pane at the end.
        var updateScopes = panes.Select(p => p.BeginUpdate()).ToList();
        try
        {
            foreach (var curve in activeCurves.ToList())
            {
                if (curve.PaneIndex < 0 || curve.PaneIndex >= panes.Count)
                    continue;

                if (string.IsNullOrEmpty(curve.YColumnName))
                    continue;

                if (!TryGetColumnData(currentData, curve.YColumnName, out var yData))
                    continue;

                var pane = panes[curve.PaneIndex];
                curve.XColumnName = selectedXColumn;
                pane.RemoveCurve(curve.Id);
                pane.AddScatterCurve(xData, yData, curve);
            }

            foreach (var pane in panes) pane.AutoScale();
        }
        finally
        {
            foreach (var scope in updateScopes) scope.Dispose();
        }

        return true;
    }

    /// <summary>
    /// Apply smart decimal defaults to a pane based on axis ranges
    /// </summary>
    public void ApplySmartDecimalDefaults(PlotPaneViewModel paneViewModel)
    {
        if (paneViewModel.PlotModel == null)
            return;

        // Render at a realistic pane size so the tick generator produces the same major
        // ticks the user will actually see. A 1x1 render packs almost no ticks in, which
        // hides the dense-step case (a tall axis on a 0.66..1.40 range lands on 0.05 ticks)
        // and led to colliding labels. Decimals are then derived from those real ticks.
        paneViewModel.PlotModel.RenderInMemory(SmartDecimalRenderWidth, SmartDecimalRenderHeight);

        // Get current axis ranges (after auto-scale from first curve)
        var xRange = paneViewModel.PlotModel.Axes.Bottom.Range;
        var y1Range = paneViewModel.PlotModel.Axes.Left.Range;
        var y2Range = paneViewModel.PlotModel.Axes.Right.Range;

        // Calculate smart defaults from the real generated ticks, falling back to the
        // range-based estimate when an axis hasn't produced enough ticks to measure.
        int xDecimals = DecimalsForAxis(paneViewModel.PlotModel.Axes.Bottom, xRange);
        int y1Decimals = DecimalsForAxis(paneViewModel.PlotModel.Axes.Left, y1Range);

        // Only calculate Y2 if it's visible/in use
        int y2Decimals = 0;
        if (paneViewModel.PaneModel.ShowY2Axis)
        {
            y2Decimals = DecimalsForAxis(paneViewModel.PlotModel.Axes.Right, y2Range);
        }

        // Apply the smart defaults
        paneViewModel.PaneModel.XAxisDecimalPlaces = xDecimals;
        paneViewModel.PaneModel.Y1AxisDecimalPlaces = y1Decimals;
        paneViewModel.PaneModel.Y2AxisDecimalPlaces = y2Decimals;

        // Reapply formatting to update the display
        paneViewModel.ApplyFormatting();
    }

    /// <summary>
    /// Recompute only the Y2-axis decimal places from its current range. Called when the
    /// first curve lands on Y2 after the initial (Y1) smart-default pass already ran — at
    /// which point Y2 had no data and was left at 0.
    /// </summary>
    public void ApplyY2SmartDecimalDefaults(PlotPaneViewModel paneViewModel)
    {
        if (paneViewModel.PlotModel == null || !paneViewModel.PaneModel.ShowY2Axis)
            return;

        paneViewModel.PlotModel.RenderInMemory(SmartDecimalRenderWidth, SmartDecimalRenderHeight);
        var y2Range = paneViewModel.PlotModel.Axes.Right.Range;
        paneViewModel.PaneModel.Y2AxisDecimalPlaces =
            DecimalsForAxis(paneViewModel.PlotModel.Axes.Right, y2Range);
        paneViewModel.ApplyFormatting();
    }

    /// <summary>
    /// Apply smart decimal defaults to a pane and synchronize X-axis decimals across all panes
    /// </summary>
    public void ApplySmartDecimalDefaultsWithSync(
        PlotPaneViewModel targetPane,
        ObservableCollection<PlotPaneViewModel> allPanes)
    {
        ApplySmartDecimalDefaults(targetPane);

        // If multiple panes exist, synchronize X-axis decimals to all panes
        if (allPanes.Count > 1)
        {
            int xAxisDecimals = targetPane.PaneModel.XAxisDecimalPlaces;
            foreach (var otherPane in allPanes)
            {
                if (otherPane.PaneModel.Index != targetPane.PaneModel.Index)
                {
                    otherPane.PaneModel.XAxisDecimalPlaces = xAxisDecimals;
                    otherPane.ApplyFormatting();
                }
            }
        }
    }

    /// <summary>
    /// Off-screen render size used to compute smart decimals. Roughly matches a real pane so
    /// the tick generator yields the same major ticks the user sees (tick density scales with
    /// pixels). Height drives Y-axis tick count — the dimension that exposed the collision bug.
    /// </summary>
    private const int SmartDecimalRenderWidth = 800;
    private const int SmartDecimalRenderHeight = 600;

    /// <summary>
    /// Decimal places for an axis: prefer the exact, collision-free count derived from the
    /// axis's real generated major ticks; fall back to a range-based estimate if the axis
    /// produced too few ticks to measure. See <see cref="AxisDecimalHelper"/>.
    /// </summary>
    private static int DecimalsForAxis(ScottPlot.IAxis axis, ScottPlot.CoordinateRangeMutable range)
    {
        var ticks = axis.TickGenerator?.Ticks;
        if (ticks != null && ticks.Length >= 2)
        {
            var positions = ticks.Where(t => t.IsMajor).Select(t => t.Position).ToList();
            if (positions.Count >= 2)
                return AxisDecimalHelper.ForTicks(positions);
        }
        return AxisDecimalHelper.ForRange(range.Min, range.Max);
    }

    /// <summary>
    /// Try to get column data safely, returning false on failure
    /// </summary>
    private static bool TryGetColumnData(PlotDataModel currentData, string columnName, out double[] data)
    {
        try
        {
            data = currentData.GetColumnData(columnName);
            return true;
        }
        catch (Exception ex)
        {
            // SECURITY: Log error details for debugging but don't expose to user (CWE-209).
            // Security baseline: never log column names / row data.
            SafeErrorHandler.LogError(ex, "getting column data");
            data = Array.Empty<double>();
            return false;
        }
    }
}
