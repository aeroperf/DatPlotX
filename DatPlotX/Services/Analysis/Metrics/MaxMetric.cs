using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Largest finite Y in the segment, with the (x, y) location. Skips NaN.
/// </summary>
public sealed class MaxMetric : IMetricDefinition
{
    public string Id => "max";
    public string DisplayName => "Max";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.PointOnCurve;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        if (y.IsEmpty) return MetricResult.Empty;

        double bestY = double.NegativeInfinity;
        double bestX = 0;
        bool any = false;

        for (int i = 0; i < y.Length; i++)
        {
            double v = y[i];
            if (!double.IsFinite(v)) continue;
            if (v > bestY) { bestY = v; bestX = x[i]; any = true; }
        }

        // Also expose a horizontal line at the max so the panel can offer a "show on plot"
        // toggle (envelope line). The point-flash still uses AtX/AtY.
        return any
            ? new MetricResult(bestY, bestX, bestY, Line: MetricLine.Horizontal(bestY))
            : MetricResult.Empty;
    }
}
