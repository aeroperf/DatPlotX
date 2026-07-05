using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// Outcome of evaluating a curve slice against a tolerance band. "Time" is X-weighted (the
/// X axis is usually time but need not be), measured in the curve's X units. All fields are
/// NaN / zero for an empty or degenerate slice.
/// </summary>
/// <param name="Center">Resolved center line Y.</param>
/// <param name="Lower">Resolved lower limit Y.</param>
/// <param name="Upper">Resolved upper limit Y.</param>
/// <param name="FractionInBand">Share of the evaluated X span the curve spent within [lower, upper], 0..1.</param>
/// <param name="LimitCrossings">Number of times the curve crossed either limit (transitions in↔out).</param>
/// <param name="ExceedanceDuration">Total X-length the curve spent outside the band.</param>
/// <param name="MaxExcursion">Largest signed distance beyond the nearest limit (positive = above upper,
/// negative = below lower); 0 when the curve never left the band.</param>
/// <param name="SpanX">Total X span covered by the evaluated samples (denominator of <see cref="FractionInBand"/>).</param>
public readonly record struct ToleranceBandResult(
    double Center,
    double Lower,
    double Upper,
    double FractionInBand,
    int LimitCrossings,
    double ExceedanceDuration,
    double MaxExcursion,
    double SpanX)
{
    public static readonly ToleranceBandResult Empty =
        new(double.NaN, double.NaN, double.NaN, double.NaN, 0, 0, double.NaN, 0);
}

/// <summary>
/// Pure evaluator for a <see cref="ToleranceBand"/> over a curve slice. Computes how the curve
/// behaves against the band: fraction of (X-weighted) time in-band, limit crossings, total
/// exceedance duration, and max signed excursion. Crossings and durations use linear
/// interpolation between samples so the limit boundary is honored mid-interval, not just at
/// sample points.
/// </summary>
public static class ToleranceBandEvaluator
{
    /// <summary>Derive the band's center value for the slice (mean / median; UserNominal is frozen
    /// and resolved by the band itself). Returns NaN when there are no finite samples.</summary>
    public static double DeriveCenter(BandCenterMode mode, ReadOnlySpan<double> y)
    {
        switch (mode)
        {
            case BandCenterMode.Mean:
                {
                    double sum = 0; int n = 0;
                    foreach (var v in y) { if (double.IsFinite(v)) { sum += v; n++; } }
                    return n == 0 ? double.NaN : sum / n;
                }
            case BandCenterMode.Median:
                {
                    var finite = new List<double>(y.Length);
                    foreach (var v in y) if (double.IsFinite(v)) finite.Add(v);
                    if (finite.Count == 0) return double.NaN;
                    finite.Sort();
                    int m = finite.Count;
                    return m % 2 == 1 ? finite[m / 2] : (finite[m / 2 - 1] + finite[m / 2]) / 2.0;
                }
            default:
                return double.NaN; // UserNominal resolved by ToleranceBand.ResolveLimits
        }
    }

    /// <summary>
    /// Evaluate <paramref name="band"/> against the parallel <paramref name="x"/> / <paramref name="y"/>
    /// slice (assumed X-ascending). The center for derived modes is computed from this same slice,
    /// so a re-picked segment re-centers the band.
    /// </summary>
    public static ToleranceBandResult Evaluate(ToleranceBand band, ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        int n = Math.Min(x.Length, y.Length);
        if (n == 0) return ToleranceBandResult.Empty;

        double derived = DeriveCenter(band.CenterMode, y);
        var (center, lower, upper) = band.ResolveLimits(derived);
        if (!double.IsFinite(lower) || !double.IsFinite(upper))
            return ToleranceBandResult.Empty with { Center = center, Lower = lower, Upper = upper };

        double spanX = 0, exceed = 0, maxExc = 0;
        int crossings = 0;

        for (int i = 0; i < n; i++)
        {
            double yi = y[i];
            if (double.IsFinite(yi))
            {
                double exc = Excursion(yi, lower, upper);
                if (Math.Abs(exc) > Math.Abs(maxExc)) maxExc = exc;
            }

            if (i == 0) continue;

            double x0 = x[i - 1], x1 = x[i];
            double y0 = y[i - 1], y1 = yi;
            double dx = x1 - x0;
            if (!(dx > 0) || !double.IsFinite(y0) || !double.IsFinite(y1))
            {
                // Gap or NaN endpoint: can't interpolate this interval's in/out split. Skip it for
                // the time integrals and don't fabricate a crossing across the gap.
                continue;
            }

            spanX += dx;

            // A crossing happens within this interval when its endpoints fall on opposite sides of
            // the band boundary (one inside, one outside). The segment is monotonic in y, so it can
            // straddle at most one limit, hence at most one crossing per interval.
            bool startInside = y0 >= lower && y0 <= upper;
            bool endInside = y1 >= lower && y1 <= upper;
            if (startInside != endInside) crossings++;

            exceed += IntervalExceedanceLength(x0, y0, x1, y1, lower, upper);
        }

        double inBand = spanX > 0 ? Math.Clamp((spanX - exceed) / spanX, 0, 1) : double.NaN;
        return new ToleranceBandResult(center, lower, upper, inBand, crossings, exceed, maxExc, spanX);
    }

    /// <summary>Signed distance of <paramref name="y"/> beyond the nearest limit; 0 inside the band.</summary>
    private static double Excursion(double y, double lower, double upper)
    {
        if (y > upper) return y - upper;
        if (y < lower) return y - lower; // negative
        return 0;
    }

    /// <summary>
    /// X-length of the [x0,x1] interval the linear segment (x0,y0)→(x1,y1) spends outside
    /// [lower, upper]. Solves for the crossing X where the segment meets each limit.
    /// </summary>
    private static double IntervalExceedanceLength(
        double x0, double y0, double x1, double y1, double lower, double upper)
    {
        double dx = x1 - x0, dy = y1 - y0;

        bool in0 = y0 >= lower && y0 <= upper;
        bool in1 = y1 >= lower && y1 <= upper;

        // Wholly inside.
        if (in0 && in1) return 0;

        // Flat (no slope): either fully in or fully out for the whole interval.
        if (dy == 0) return in0 ? 0 : dx;

        // Parametric crossings t∈[0,1] where y(t) hits each limit.
        double tUpper = (upper - y0) / dy;
        double tLower = (lower - y0) / dy;

        // Collect the in-band sub-interval [tIn0, tIn1] (the segment is monotonic in y, so the
        // in-band region — if any — is a single contiguous t-interval).
        double tLo = Math.Min(tUpper, tLower);
        double tHi = Math.Max(tUpper, tLower);
        double inStart = Math.Clamp(tLo, 0, 1);
        double inEnd = Math.Clamp(tHi, 0, 1);
        double insideFraction = Math.Max(0, inEnd - inStart);

        // But the band's t-window [tLo,tHi] is "inside" only if it actually overlaps where y is
        // within limits. For a monotonic segment, y is inside exactly between the two limit
        // crossings, which is [tLo, tHi]. Outside fraction is the complement clamped to [0,1].
        double outsideFraction = 1.0 - insideFraction;
        return Math.Clamp(outsideFraction, 0, 1) * dx;
    }
}
