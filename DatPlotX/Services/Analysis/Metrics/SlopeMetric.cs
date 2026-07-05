using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis.Metrics;

/// <summary>
/// Linear-regression slope over the segment. The scalar <see cref="MetricResult.Value"/> is the
/// fit slope; <see cref="MetricResult.Line"/> carries the drawable regression (trend) line across
/// the segment's X range, which the panel offers as a "show on plot" toggle (e.g. a rate-of-climb
/// trend over a climb segment). <see cref="MetricResult.Extras"/> carries <c>"intercept"</c> and
/// <c>"r2"</c> for the panel. The analysis service tacks on an engineer-friendly derived rate
/// (e.g. <c>ft/min</c>) via <see cref="Units.IUnitRegistry.PreferredDerivedRate"/> when source
/// units are known.
/// </summary>
public sealed class SlopeMetric : IMetricDefinition
{
    public string Id => "slope";
    public string DisplayName => "Slope";
    public MetricCategory Category => MetricCategory.Temporal;
    public MetricKind Kind => MetricKind.LineOnPlot;

    public MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters)
    {
        // Two-pass: compute means first, then accumulate centered cross-products.
        // More stable than the textbook single-pass sums when X has a large offset.
        double sumX = 0, sumY = 0;
        double minX = double.PositiveInfinity, maxX = double.NegativeInfinity;
        int n = 0;
        for (int i = 0; i < y.Length; i++)
        {
            if (double.IsNaN(y[i]) || double.IsNaN(x[i])) continue;
            sumX += x[i]; sumY += y[i]; n++;
            if (x[i] < minX) minX = x[i];
            if (x[i] > maxX) maxX = x[i];
        }
        if (n < 2) return MetricResult.Empty;

        double meanX = sumX / n;
        double meanY = sumY / n;

        double sxx = 0, sxy = 0, syy = 0;
        for (int i = 0; i < y.Length; i++)
        {
            if (double.IsNaN(y[i]) || double.IsNaN(x[i])) continue;
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;
            sxx += dx * dx;
            sxy += dx * dy;
            syy += dy * dy;
        }

        if (sxx == 0) return MetricResult.Empty;       // vertical / single unique X

        double slope = sxy / sxx;
        double intercept = meanY - slope * meanX;
        // A flat (or near-flat) line has no Y variance to explain, so the fit is
        // trivially perfect. Use a relative threshold on syy: summing a constant Y
        // accumulates rounding error that leaves syy tiny-but-nonzero, which would
        // otherwise yield a garbage r2 ≈ 0 instead of 1.
        double syyTolerance = 1e-20 + 1e-12 * meanY * meanY * n;
        double r2 = syy <= syyTolerance ? 1.0 : (sxy * sxy) / (sxx * syy);

        // Endpoints of the fit line across the segment's actual X extent.
        double y0 = slope * minX + intercept;
        double y1 = slope * maxX + intercept;

        return new MetricResult(
            slope,
            Extras: new Dictionary<string, double>
            {
                ["intercept"] = intercept,
                ["r2"] = r2,
            },
            Line: MetricLine.Between(minX, y0, maxX, y1));
    }
}
