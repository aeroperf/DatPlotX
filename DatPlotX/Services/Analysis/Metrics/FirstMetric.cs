using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>First finite Y in the segment, with its X location. Boundary value for deltas.</summary>
public sealed class FirstMetric : IMetricDefinition
{
    public string Id => "first";
    public string DisplayName => "First";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.PointOnCurve;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        for (int i = 0; i < y.Length; i++)
            if (double.IsFinite(y[i])) return new MetricResult(y[i], x[i], y[i]);
        return MetricResult.Empty;
    }
}
