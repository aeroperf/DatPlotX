namespace DatPlotX.Services.Analysis;

/// <summary>
/// Plot-mode-specific overlay drawer. Implemented by each mode VM (Stacked / Compact /
/// Grouped). The Analysis panel asks the active host to flash a point or band when the
/// user clicks the target button on a result cell; the host translates that into native
/// ScottPlot or OxyPlot primitives.
/// </summary>
/// <remarks>
/// Highlights are visual-only — never persisted to <c>.DPX</c>. Implementations tag their
/// plottables so they can be wholesale cleared on <see cref="ClearHighlights"/> without
/// touching user-owned annotations.
/// </remarks>
public interface IAnalysisOverlayHost
{
    /// <summary>Draw / replace a single point marker on the named curve.</summary>
    void HighlightPoint(string curveId, double x, double y, string label);

    /// <summary>Draw / replace a translucent X-range band labeled with the segment name.</summary>
    void HighlightSegment(double xMin, double xMax, string label);

    /// <summary>
    /// Draw / replace a statistic line (linear-fit trend, mean / min / max level) on the named
    /// curve, clipped to the active segment's X span. <paramref name="lineId"/> uniquely keys the
    /// line (e.g. <c>"{curveId}:{metricId}"</c>) so it can be replaced or cleared individually.
    /// For <see cref="Models.Analysis.MetricLineShape.Horizontal"/> the line spans
    /// <paramref name="segXMin"/>..<paramref name="segXMax"/> at <see cref="Models.Analysis.MetricLine.Y0"/>;
    /// for <see cref="Models.Analysis.MetricLineShape.Segment"/> the line uses the metric's own endpoints.
    /// Drawn in the curve's own axis coordinates (Y2-aware), like <see cref="HighlightPoint"/>.
    /// </summary>
    void DrawSegmentLine(string curveId, string lineId, Models.Analysis.MetricLine line,
        double segXMin, double segXMax, string colorHex, string label);

    /// <summary>Remove a single statistic line by its <c>lineId</c>. Idempotent.</summary>
    void ClearSegmentLine(string lineId);

    /// <summary>Remove every statistic line (but keep point flash + segment band). Idempotent.</summary>
    void ClearSegmentLines();

    /// <summary>Remove all analysis highlights (markers + bands + statistic lines). Idempotent.</summary>
    void ClearHighlights();
}
