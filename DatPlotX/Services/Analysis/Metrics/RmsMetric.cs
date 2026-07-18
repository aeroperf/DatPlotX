using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Root mean square — true magnitude of an AC waveform, vibration severity, power-equivalent
/// measure. <c>sqrt(mean(y²))</c> over finite samples.
/// </summary>
public sealed class RmsMetric : IMetricDefinition
{
    public string Id => "rms";
    public string DisplayName => "RMS";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        double sumSq = 0;
        int n = 0;
        foreach (var v in y) { if (double.IsFinite(v)) { sumSq += v * v; n++; } }
        return n > 0 ? new MetricResult(Math.Sqrt(sumSq / n)) : MetricResult.Empty;
    }
}
