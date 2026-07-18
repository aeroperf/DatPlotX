using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Trapezoidal integral ∫ y dx over the segment. Fuel burned from flow rate, distance from
/// velocity, work from force × velocity. Skips intervals where either endpoint is NaN.
/// </summary>
public sealed class IntegralMetric : IMetricDefinition
{
    public string Id => "integral";
    public string DisplayName => "Integral";
    public MetricCategory Category => MetricCategory.Temporal;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        if (y.Length < 2) return MetricResult.Empty;

        double sum = 0;
        bool any = false;
        for (int i = 1; i < y.Length; i++)
        {
            double y0 = y[i - 1], y1 = y[i];
            double x0 = x[i - 1], x1 = x[i];
            if (!double.IsFinite(y0) || !double.IsFinite(y1) || !double.IsFinite(x0) || !double.IsFinite(x1))
                continue;
            sum += 0.5 * (y0 + y1) * (x1 - x0);
            any = true;
        }
        return any ? new MetricResult(sum) : MetricResult.Empty;
    }
}
