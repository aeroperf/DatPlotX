using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Median (50th percentile) of finite Y samples. Robust to a single noise spike — when one
/// bad sample is dragging the mean, the median tells you what the rest of the data looks like.
/// </summary>
public sealed class MedianMetric : IMetricDefinition
{
    public string Id => "median";
    public string DisplayName => "Median";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
        => PercentileMetric.ComputePercentile(y, 50);
}
