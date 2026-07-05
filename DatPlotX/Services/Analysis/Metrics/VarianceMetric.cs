using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>Population variance of finite Y samples. Two-pass for numerical stability.</summary>
public sealed class VarianceMetric : IMetricDefinition
{
    public string Id => "variance";
    public string DisplayName => "Variance";
    public MetricCategory Category => MetricCategory.Basic;
    public MetricKind Kind => MetricKind.Scalar;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        var v = ComputeVariance(y);
        return double.IsNaN(v) ? MetricResult.Empty : new MetricResult(v);
    }

    /// <summary>Population variance (divides by N, not N-1). NaN for empty input.</summary>
    internal static double ComputeVariance(ReadOnlySpan<double> y)
    {
        double sum = 0;
        int n = 0;
        foreach (var v in y) { if (!double.IsNaN(v)) { sum += v; n++; } }
        if (n == 0) return double.NaN;

        double mean = sum / n;
        double sq = 0;
        foreach (var v in y)
        {
            if (double.IsNaN(v)) continue;
            double d = v - mean;
            sq += d * d;
        }
        return sq / n;
    }
}
