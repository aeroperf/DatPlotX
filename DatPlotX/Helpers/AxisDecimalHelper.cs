using System.Globalization;

namespace DatPlotX.Helpers;

/// <summary>
/// Picks how many decimal places an axis tick label needs so the values stay
/// legible without manual configuration. The choice is driven by the *resolution*
/// of the axis (its approximate tick step), not just the raw span — a 0..0.4 axis
/// needs 2 decimals while a 0..50000 axis needs none.
/// </summary>
public static class AxisDecimalHelper
{
    /// <summary>Upper bound on decimals so labels never explode in width.</summary>
    public const int MaxDecimals = 6;

    /// <summary>
    /// Upper-bound estimate of major ticks on a tall/wide axis. A generous count is
    /// deliberate: it makes the estimated step land on (or below) the dense step a real
    /// axis picks, so the chosen decimals can still tell adjacent ticks apart. Under-
    /// counting is the failure mode — it leaves too few decimals and labels collide.
    /// </summary>
    private const double TargetMajorTicks = 15.0;

    /// <summary>
    /// Decimal places for an axis spanning [<paramref name="min"/>, <paramref name="max"/>].
    /// Returns 0 for non-finite or zero-width ranges.
    /// </summary>
    public static int ForRange(double min, double max)
    {
        if (double.IsNaN(min) || double.IsNaN(max) ||
            double.IsInfinity(min) || double.IsInfinity(max))
            return 0;

        double range = Math.Abs(max - min);
        if (range <= 0d)
            return 0;

        // Snap to the "nice" tick step the axis will actually land on (1/2/5 x 10^n),
        // then count the decimals needed to render that step without two adjacent ticks
        // collapsing to the same label. Estimating from range/TargetTicks alone
        // under-counts: a 0.66..1.40 axis lands on a 0.05 step, which needs 2 decimals —
        // at 1 decimal you get "1.1, 1.1, 1.0, 1.0" duplicates (the LIS_TO gFz case).
        double step = NiceStep(range / TargetMajorTicks);
        return DecimalsForStep(step);
    }

    /// <summary>
    /// Decimal places needed so the given (already-generated) major-tick positions all
    /// render as distinct labels. This is exact — it works off the real tick values the
    /// axis produced rather than estimating a step from the range, so it can't be fooled
    /// by a tall axis packing in more ticks than a range-based guess assumes (the LIS_TO
    /// gFz case, where a 0.05 step rendered "1.1, 1.1, 1.0, 1.0" at 1 decimal).
    /// Falls back to <see cref="ForRange"/> when there are too few ticks to measure.
    /// </summary>
    public static int ForTicks(IReadOnlyList<double> tickPositions)
    {
        if (tickPositions == null || tickPositions.Count < 2)
            return 0;

        // Distinct, finite, sorted — adjacent duplicates in the input shouldn't drive precision.
        double[] ticks = tickPositions
            .Where(t => !double.IsNaN(t) && !double.IsInfinity(t))
            .Distinct()
            .OrderBy(t => t)
            .ToArray();
        if (ticks.Length < 2)
            return 0;

        // Find the fewest decimals at which every consecutive pair of ticks formats to a
        // different string. As soon as no two neighbours collide, that precision is enough.
        for (int d = 0; d <= MaxDecimals; d++)
        {
            bool allDistinct = true;
            for (int i = 1; i < ticks.Length; i++)
            {
                string prev = ticks[i - 1].ToString("F" + d, CultureInfo.InvariantCulture);
                string cur = ticks[i].ToString("F" + d, CultureInfo.InvariantCulture);
                if (prev == cur)
                {
                    allDistinct = false;
                    break;
                }
            }
            if (allDistinct)
                return d;
        }
        return MaxDecimals;
    }

    /// <summary>
    /// Round a raw step up to the nearest "nice" number: 1, 2, or 5 times a power of 10.
    /// This mirrors how clean axes (ScottPlot/OxyPlot) choose major-tick spacing.
    /// </summary>
    private static double NiceStep(double rawStep)
    {
        if (rawStep <= 0d)
            return 0d;

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        double normalized = rawStep / magnitude; // in [1, 10)

        double niceNormalized = normalized switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d,
        };
        return niceNormalized * magnitude;
    }

    /// <summary>
    /// Decimals required to render <paramref name="step"/> exactly — i.e. so consecutive
    /// multiples of the step produce distinct labels. e.g. 0.05 -> 2, 0.2 -> 1, 10 -> 0.
    /// </summary>
    private static int DecimalsForStep(double step)
    {
        if (step <= 0d)
            return 0;

        // Walk up decimal places until rounding the step to that precision is exact,
        // so the label can distinguish one step from the next.
        for (int d = 0; d < MaxDecimals; d++)
        {
            double rounded = Math.Round(step, d);
            if (Math.Abs(rounded - step) <= step * 1e-9)
                return d;
        }
        return MaxDecimals;
    }
}
