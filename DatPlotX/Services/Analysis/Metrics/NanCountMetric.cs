using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>Count of NaN samples in the segment. Anything > 0 means sensor dropouts.</summary>
public sealed class NanCountMetric : IMetricDefinition
{
    public string Id => "nancount";
    public string DisplayName => "NaN count";
    public MetricCategory Category => MetricCategory.Quality;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        int n = 0;
        foreach (var v in y) { if (double.IsNaN(v)) n++; }
        return new MetricResult(n);
    }
}
