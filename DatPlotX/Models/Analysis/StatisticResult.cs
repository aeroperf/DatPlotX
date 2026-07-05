namespace DatPlotX.Models.Analysis;

/// <summary>
/// One row of the Analysis Results panel. Combines a <see cref="MetricResult"/> with the
/// (curve, segment, metric) keys identifying which cell it populates, plus the units
/// to display.
/// </summary>
/// <param name="CurveId">Curve identifier (Stacked / Grouped: <c>CurveConfigurationModel.Id.ToString()</c>;
/// Compact: <c>CompactCurveModel.Id.ToString()</c>).</param>
/// <param name="SegmentId">Owning <see cref="AnalysisSegment.Id"/>.</param>
/// <param name="MetricId">Metric registry ID ("min", "max", "slope", ...).</param>
/// <param name="Value">Primary value. NaN when the input range had no valid samples.</param>
/// <param name="AtX">X-coordinate of the point for <see cref="MetricKind.PointOnCurve"/> metrics.</param>
/// <param name="AtY">Y-coordinate of the point for <see cref="MetricKind.PointOnCurve"/> metrics.</param>
/// <param name="Units">Display units for <see cref="Value"/>. Null = unknown; the panel renders
/// the raw number with no suffix.</param>
/// <param name="DerivedRateLabel">For <see cref="Services.Analysis.Metrics.SlopeMetric"/>, the
/// engineer-friendly derived rate label (e.g. <c>"2 868 ft/min"</c>); null otherwise.</param>
/// <param name="Line">For <see cref="MetricKind.LineOnPlot"/> metrics, the geometry the overlay
/// host draws over the segment; null otherwise.</param>
/// <param name="Extras">Secondary scalar outputs a metric exposes beyond <see cref="Value"/>
/// (e.g. <see cref="Services.Analysis.Metrics.SlopeMetric"/>'s <c>"r2"</c> and <c>"intercept"</c>).
/// Null when the metric has none. Surfaced as a tooltip on the cell, not as its own column.</param>
public sealed record StatisticResult(
    string CurveId,
    Guid SegmentId,
    string MetricId,
    double Value,
    double? AtX = null,
    double? AtY = null,
    string? Units = null,
    string? DerivedRateLabel = null,
    MetricLine? Line = null,
    IReadOnlyDictionary<string, double>? Extras = null);
