using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using DatPlotX.Services.Analysis.Metrics;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Analysis;

/// <summary>
/// Test ergonomics: Phase-1 metrics are pure functions of (x, y) and ignore the parameter
/// bag, so these tests call a two-arg overload that forwards <see cref="MetricParameters.None"/>.
/// Parameterized (Phase-2B+) metrics are tested with the explicit three-arg signature.
/// </summary>
internal static class MetricTestExtensions
{
    public static MetricResult Compute(this IMetricDefinition metric,
        ReadOnlySpan<double> x, ReadOnlySpan<double> y) =>
        metric.Compute(x, y, MetricParameters.None);
}

/// <summary>
/// One file covering every Phase-1 metric. Each metric is a small pure function; the cases
/// shared between them are:
///   - empty input  → MetricResult.Empty (NaN)
///   - all-NaN input → MetricResult.Empty
///   - constant input → known result
///   - mixed valid + NaN → metric skips NaN
///   - point-on-curve metrics → AtX matches the sample that produced the value
/// </summary>
public class MetricsTests
{
    private static double[] X(int n)
    {
        var x = new double[n];
        for (int i = 0; i < n; i++) x[i] = i;
        return x;
    }

    [Fact]
    public void Max_FindsLargest_AndItsX()
    {
        var x = new double[] { 0, 1, 2, 3, 4 };
        var y = new double[] { 1, 3, 7, 2, 5 };
        var r = new MaxMetric().Compute(x, y);
        r.Value.Should().Be(7);
        r.AtX.Should().Be(2);
        r.AtY.Should().Be(7);
    }

    [Fact]
    public void Min_FindsSmallest_AndItsX()
    {
        var x = new double[] { 0, 1, 2, 3, 4 };
        var y = new double[] { 3, 1, 4, -2, 5 };
        var r = new MinMetric().Compute(x, y);
        r.Value.Should().Be(-2);
        r.AtX.Should().Be(3);
    }

    [Fact]
    public void Max_SkipsNaN()
    {
        var x = new double[] { 0, 1, 2 };
        var y = new double[] { double.NaN, 5, double.NaN };
        var r = new MaxMetric().Compute(x, y);
        r.Value.Should().Be(5);
        r.AtX.Should().Be(1);
    }

    [Fact]
    public void Mean_OfConstants_IsTheConstant()
    {
        var x = X(4); var y = new double[] { 7, 7, 7, 7 };
        new MeanMetric().Compute(x, y).Value.Should().Be(7);
    }

    [Fact]
    public void Mean_OfMixed_SkipsNaN()
    {
        var x = X(5); var y = new double[] { 2, double.NaN, 4, 6, double.NaN };
        new MeanMetric().Compute(x, y).Value.Should().Be(4);
    }

    [Fact]
    public void Median_OfFiveValues_IsMiddle()
    {
        var x = X(5); var y = new double[] { 1, 5, 3, 7, 2 };
        new MedianMetric().Compute(x, y).Value.Should().Be(3);
    }

    [Fact]
    public void Median_OfFourValues_InterpolatesMiddleTwo()
    {
        var x = X(4); var y = new double[] { 1, 2, 3, 4 };
        new MedianMetric().Compute(x, y).Value.Should().Be(2.5);
    }

    [Fact]
    public void StdDev_OfConstants_IsZero()
    {
        new StdDevMetric().Compute(X(4), new double[] { 5, 5, 5, 5 }).Value.Should().Be(0);
    }

    [Fact]
    public void StdDev_KnownCase()
    {
        // population variance of [2, 4, 4, 4, 5, 5, 7, 9] = 4 ; std = 2
        var y = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        new StdDevMetric().Compute(X(8), y).Value.Should().BeApproximately(2.0, 1e-9);
    }

    [Fact]
    public void Variance_KnownCase()
    {
        var y = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        new VarianceMetric().Compute(X(8), y).Value.Should().BeApproximately(4.0, 1e-9);
    }

    [Fact]
    public void Rms_OfConstants_IsTheConstant()
    {
        new RmsMetric().Compute(X(4), new double[] { 3, 3, 3, 3 }).Value.Should().BeApproximately(3, 1e-9);
    }

    [Fact]
    public void Rms_OfPlusMinusOne_IsOne()
    {
        new RmsMetric().Compute(X(4), new double[] { 1, -1, 1, -1 }).Value.Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void First_SkipsLeadingNaN()
    {
        var x = new double[] { 0, 1, 2, 3 };
        var y = new double[] { double.NaN, double.NaN, 5, 7 };
        var r = new FirstMetric().Compute(x, y);
        r.Value.Should().Be(5);
        r.AtX.Should().Be(2);
    }

    [Fact]
    public void Last_SkipsTrailingNaN()
    {
        var x = new double[] { 0, 1, 2, 3 };
        var y = new double[] { 1, 2, 3, double.NaN };
        var r = new LastMetric().Compute(x, y);
        r.Value.Should().Be(3);
        r.AtX.Should().Be(2);
    }

    [Fact]
    public void Range_And_PeakToPeak_SameNumber()
    {
        var x = X(5); var y = new double[] { 10, -3, 4, 7, 1 };
        new RangeMetric().Compute(x, y).Value.Should().Be(13);
        new PeakToPeakMetric().Compute(x, y).Value.Should().Be(13);
    }

    [Fact]
    public void P50_EqualsMedian()
    {
        var x = X(5); var y = new double[] { 1, 5, 3, 7, 2 };
        new PercentileMetric(50).Compute(x, y).Value.Should().Be(3);
    }

    [Fact]
    public void P5_And_P95_Bracket_Distribution()
    {
        // 100 evenly-spaced samples 1..100
        var y = new double[100];
        for (int i = 0; i < 100; i++) y[i] = i + 1;
        var x = X(100);
        new PercentileMetric(5).Compute(x, y).Value.Should().BeApproximately(5.95, 1e-9);
        new PercentileMetric(95).Compute(x, y).Value.Should().BeApproximately(95.05, 1e-9);
    }

    [Fact]
    public void Slope_OfPerfectLine_IsSlope_RSquaredIsOne()
    {
        // y = 3x + 5
        var x = X(20);
        var y = new double[20];
        for (int i = 0; i < 20; i++) y[i] = 3 * i + 5;

        var r = new SlopeMetric().Compute(x, y);
        r.Value.Should().BeApproximately(3, 1e-9);
        r.Extras!["intercept"].Should().BeApproximately(5, 1e-9);
        r.Extras!["r2"].Should().BeApproximately(1.0, 1e-9);
    }

    [Fact]
    public void Slope_OfFlatLine_IsZero()
    {
        var x = X(10);
        var y = new double[10]; for (int i = 0; i < 10; i++) y[i] = 4.2;
        var r = new SlopeMetric().Compute(x, y);
        r.Value.Should().Be(0);
        r.Extras!["r2"].Should().Be(1.0);   // perfect fit on a constant
    }

    [Fact]
    public void Slope_LessThanTwoPoints_IsEmpty()
    {
        var r = new SlopeMetric().Compute(new double[] { 1 }, new double[] { 2 });
        r.Value.Should().Be(double.NaN);
        r.Line.Should().BeNull();
    }

    [Fact]
    public void Integral_OfConstantTwo_OverFiveUnits_IsTen()
    {
        var x = new double[] { 0, 1, 2, 3, 4, 5 };
        var y = new double[] { 2, 2, 2, 2, 2, 2 };
        new IntegralMetric().Compute(x, y).Value.Should().BeApproximately(10, 1e-9);
    }

    [Fact]
    public void Integral_OfLinearY_TrapezoidalExact()
    {
        // y = x from x=0..10 ; integral = 50
        var x = new double[11]; for (int i = 0; i < 11; i++) x[i] = i;
        var y = new double[11]; for (int i = 0; i < 11; i++) y[i] = i;
        new IntegralMetric().Compute(x, y).Value.Should().BeApproximately(50, 1e-9);
    }

    [Fact]
    public void Count_And_NanCount_Sum_Equals_Length()
    {
        var x = X(5);
        var y = new double[] { 1, double.NaN, 2, double.NaN, 3 };
        new CountMetric().Compute(x, y).Value.Should().Be(3);
        new NanCountMetric().Compute(x, y).Value.Should().Be(2);
    }

    [Fact]
    public void Empty_Input_AllMetrics_ReturnNaN()
    {
        var x = ReadOnlySpan<double>.Empty;
        var y = ReadOnlySpan<double>.Empty;
        new MaxMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new MinMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new MeanMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new MedianMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new StdDevMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new RmsMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new SlopeMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new IntegralMetric().Compute(x, y).Value.Should().Be(double.NaN);
    }

    [Fact]
    public void AllNaN_Input_AllMetrics_ReturnNaN()
    {
        var x = X(3);
        var y = new double[] { double.NaN, double.NaN, double.NaN };
        new MaxMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new MinMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new MeanMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new RmsMetric().Compute(x, y).Value.Should().Be(double.NaN);
        new IntegralMetric().Compute(x, y).Value.Should().Be(double.NaN);
    }

    // ---- Phase 2B: line-on-plot geometry ----

    [Fact]
    public void Mean_ExposesHorizontalLineAtMean()
    {
        var x = X(4);
        var y = new double[] { 2, 4, 6, 8 };
        var r = new MeanMetric().Compute(x, y);
        r.Value.Should().Be(5);
        r.Line.Should().NotBeNull();
        r.Line!.Shape.Should().Be(MetricLineShape.Horizontal);
        r.Line.Y0.Should().Be(5);
    }

    [Fact]
    public void MinMax_ExposeHorizontalLinesAtExtreme_AndKeepPoint()
    {
        var x = X(4);
        var y = new double[] { 3, 9, 1, 7 };

        var max = new MaxMetric().Compute(x, y);
        max.Line!.Shape.Should().Be(MetricLineShape.Horizontal);
        max.Line.Y0.Should().Be(9);
        max.AtX.Should().Be(1);   // point-flash coords still present

        var min = new MinMetric().Compute(x, y);
        min.Line!.Y0.Should().Be(1);
        min.AtX.Should().Be(2);
    }

    [Fact]
    public void Slope_LineSpansSegmentX_OnExactLine()
    {
        // y = 2x + 1 over x = 0..4 → endpoints (0,1) and (4,9), slope 2, r2 = 1
        var x = X(5);
        var y = new double[] { 1, 3, 5, 7, 9 };
        var r = new SlopeMetric().Compute(x, y);

        r.Value.Should().BeApproximately(2, 1e-9);
        r.Extras!["r2"].Should().BeApproximately(1.0, 1e-9);
        r.Line.Should().NotBeNull();
        r.Line!.Shape.Should().Be(MetricLineShape.Segment);
        r.Line.X0.Should().Be(0);
        r.Line.Y0.Should().BeApproximately(1, 1e-9);
        r.Line.X1.Should().Be(4);
        r.Line.Y1.Should().BeApproximately(9, 1e-9);
    }

    // --- ±Infinity handling: metrics treat non-finite samples (sensor sentinels parsed from
    // "Infinity" in a CSV) like gaps, matching ToleranceBandEvaluator. Previously they filtered
    // only NaN, so a single ∞ poisoned Mean/RMS/StdDev/Max into ∞ or NaN.

    [Fact]
    public void Mean_SkipsInfinity()
    {
        var x = X(3);
        var y = new double[] { 100, 200, double.PositiveInfinity };
        new MeanMetric().Compute(x, y).Value.Should().BeApproximately(150, 1e-9);
    }

    [Fact]
    public void Max_SkipsInfinity()
    {
        var x = X(3);
        var y = new double[] { 1, 5, double.PositiveInfinity };
        new MaxMetric().Compute(x, y).Value.Should().Be(5);
    }

    [Fact]
    public void StdDev_SkipsInfinity()
    {
        var x = X(4);
        var y = new double[] { 2, 4, 6, double.NegativeInfinity };
        // Population stddev of {2,4,6} = sqrt(8/3).
        new StdDevMetric().Compute(x, y).Value.Should().BeApproximately(Math.Sqrt(8.0 / 3.0), 1e-9);
    }

    [Fact]
    public void Count_ExcludesInfinity()
    {
        var x = X(4);
        var y = new double[] { 1, 2, double.PositiveInfinity, double.NaN };
        new CountMetric().Compute(x, y).Value.Should().Be(2);
    }
}
