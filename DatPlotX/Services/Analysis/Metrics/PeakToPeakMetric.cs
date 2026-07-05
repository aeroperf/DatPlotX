using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Max − Min. Mechanical clearance, signal swing, vibration displacement amplitude.
/// </summary>
public sealed class PeakToPeakMetric : IMetricDefinition
{
    public string Id => "peaktopeak";
    public string DisplayName => "Peak-to-peak";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters) => ComputeP2P(y);

    internal static MetricResult ComputeP2P(ReadOnlySpan<double> y)
    {
        double min = double.PositiveInfinity;
        double max = double.NegativeInfinity;
        bool any = false;
        foreach (var v in y)
        {
            if (double.IsNaN(v)) continue;
            if (v < min) min = v;
            if (v > max) max = v;
            any = true;
        }
        return any ? new MetricResult(max - min) : MetricResult.Empty;
    }
}
