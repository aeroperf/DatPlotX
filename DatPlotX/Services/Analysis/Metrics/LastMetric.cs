using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>Last finite Y in the segment, with its X location. Boundary value for deltas.</summary>
public sealed class LastMetric : IMetricDefinition
{
    public string Id => "last";
    public string DisplayName => "Last";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.PointOnCurve;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        for (int i = y.Length - 1; i >= 0; i--)
            if (!double.IsNaN(y[i])) return new MetricResult(y[i], x[i], y[i]);
        return MetricResult.Empty;
    }
}
