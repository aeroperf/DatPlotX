using DatPlotX.Models.Analysis;
using System.Globalization;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Linear-interpolation percentile (R-7 / NumPy default convention). Registered once per
/// supported percentile (P5, P50, P95 in Phase 1). Tail metrics matter more than the mean
/// for fatigue / durability analysis.
/// </summary>
public sealed class PercentileMetric : IMetricDefinition
{
    private readonly int _percent;

    public PercentileMetric(int percent)
    {
        if (percent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), "Percent must be in [0, 100].");
        _percent = percent;
    }

    public string Id => "p" + _percent.ToString(CultureInfo.InvariantCulture);
    public string DisplayName => "P" + _percent.ToString(CultureInfo.InvariantCulture);
    public MetricCategory Category => MetricCategory.Distribution;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
        => ComputePercentile(y, _percent);

    /// <summary>
    /// Type-7 linear-interpolation percentile (matches <c>numpy.percentile</c> default).
    /// Copies finite samples to a heap array and sorts — O(n log n) but works for any input.
    /// </summary>
    internal static MetricResult ComputePercentile(ReadOnlySpan<double> y, double percent)
    {
        if (y.IsEmpty) return MetricResult.Empty;

        // Copy out finite values
        var buf = new double[y.Length];
        int n = 0;
        foreach (var v in y) { if (double.IsFinite(v)) buf[n++] = v; }
        if (n == 0) return MetricResult.Empty;

        Array.Sort(buf, 0, n);

        // Type-7: rank = (n-1) * p/100
        double rank = (n - 1) * percent / 100.0;
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return new MetricResult(buf[lo]);

        double frac = rank - lo;
        return new MetricResult(buf[lo] * (1 - frac) + buf[hi] * frac);
    }
}
