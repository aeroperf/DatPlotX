using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Population standard deviation of finite Y samples. Two-pass for numerical stability —
/// the legacy single-pass formula is biased on long quasi-constant series.
/// </summary>
public sealed class StdDevMetric : IMetricDefinition
{
    public string Id => "stddev";
    public string DisplayName => "StdDev";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        var variance = VarianceMetric.ComputeVariance(y);
        if (double.IsNaN(variance)) return MetricResult.Empty;
        return new MetricResult(Math.Sqrt(variance));
    }
}
