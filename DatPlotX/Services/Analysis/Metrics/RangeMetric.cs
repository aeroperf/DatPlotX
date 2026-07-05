using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Max − Min over the segment. Alias for peak-to-peak when the signal is unipolar; same
/// number, different naming convention used in different engineering subcultures.
/// </summary>
public sealed class RangeMetric : IMetricDefinition
{
    public string Id => "range";
    public string DisplayName => "Range";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
        => PeakToPeakMetric.ComputeP2P(y);
}
