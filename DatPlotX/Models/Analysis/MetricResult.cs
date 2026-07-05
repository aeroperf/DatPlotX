namespace DatPlotX.Models.Analysis;

/// <summary>
/// Output of <see cref="Services.Analysis.IMetricDefinition.Compute"/>. Carries the
/// scalar value plus optional point-on-curve coordinates and any side-channel extras
/// the metric wants to expose (e.g. <see cref="Services.Analysis.Metrics.SlopeMetric"/>
/// exposes <c>Intercept</c> and <c>RSquared</c>).
/// </summary>
/// <param name="Value">Primary scalar (NaN when input is empty / undefined).</param>
/// <param name="AtX">For <see cref="MetricKind.PointOnCurve"/> metrics, the X of the sample.</param>
/// <param name="AtY">For <see cref="MetricKind.PointOnCurve"/> metrics, the Y of the sample.</param>
/// <param name="Extras">Optional named scalars (e.g. slope's intercept, R²).</param>
/// <param name="Line">For <see cref="MetricKind.LineOnPlot"/> metrics, the geometry to draw
/// over the segment; null otherwise.</param>
public sealed record MetricResult(
    double Value,
    double? AtX = null,
    double? AtY = null,
    IReadOnlyDictionary<string, double>? Extras = null,
    MetricLine? Line = null)
{
    /// <summary>Convenience for empty / undefined inputs — used by every metric on short input.</summary>
    public static MetricResult Empty { get; } = new(double.NaN);
}
