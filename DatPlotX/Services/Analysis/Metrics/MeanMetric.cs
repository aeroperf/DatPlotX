using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>Arithmetic mean of finite Y samples in the segment.</summary>
public sealed class MeanMetric : IMetricDefinition
{
    public string Id => "mean";
    public string DisplayName => "Mean";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        double sum = 0;
        int n = 0;
        foreach (var v in y) { if (!double.IsNaN(v)) { sum += v; n++; } }
        if (n == 0) return MetricResult.Empty;
        double mean = sum / n;
        return new MetricResult(mean, Line: MetricLine.Horizontal(mean));
    }
}
