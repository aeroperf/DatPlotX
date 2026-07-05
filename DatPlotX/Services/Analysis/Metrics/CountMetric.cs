using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>Count of finite (non-NaN) samples in the segment. Data-quality flag.</summary>
public sealed class CountMetric : IMetricDefinition
{
    public string Id => "count";
    public string DisplayName => "Count";
    public MetricCategory Category => MetricCategory.Quality;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        int n = 0;
        foreach (var v in y) { if (!double.IsNaN(v)) n++; }
        return new MetricResult(n);
    }
}
