using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Analysis;

/// <summary>
/// Covers <see cref="ToleranceBandEvaluator"/>: center derivation, absolute vs percent limits,
/// in-band fraction (X-weighted, interpolated), crossing count, exceedance duration, and max
/// signed excursion. X is unit-spaced unless a test needs otherwise.
/// </summary>
public class ToleranceBandEvaluatorTests
{
    private static ToleranceBand Band(
        BandCenterMode mode = BandCenterMode.UserNominal,
        double nominal = 0,
        double tol = 1,
        ToleranceMode unit = ToleranceMode.Absolute) =>
        new("c", mode, nominal, tol, unit, BandScope.ActiveSegment);

    [Fact]
    public void Empty_ReturnsEmpty()
    {
        var r = ToleranceBandEvaluator.Evaluate(Band(), ReadOnlySpan<double>.Empty, ReadOnlySpan<double>.Empty);
        r.Should().Be(ToleranceBandResult.Empty);
    }

    [Fact]
    public void DeriveCenter_MeanAndMedian_IgnoreNaN()
    {
        double[] y = { 1, 2, double.NaN, 9 };
        ToleranceBandEvaluator.DeriveCenter(BandCenterMode.Mean, y).Should().Be(4); // (1+2+9)/3
        ToleranceBandEvaluator.DeriveCenter(BandCenterMode.Median, y).Should().Be(2);
    }

    [Fact]
    public void AbsoluteLimits_CenteredOnNominal()
    {
        double[] x = { 0, 1, 2 };
        double[] y = { 10, 10, 10 };
        var r = ToleranceBandEvaluator.Evaluate(Band(nominal: 10, tol: 2), x, y);
        r.Center.Should().Be(10);
        r.Lower.Should().Be(8);
        r.Upper.Should().Be(12);
        r.FractionInBand.Should().Be(1);
        r.LimitCrossings.Should().Be(0);
        r.ExceedanceDuration.Should().Be(0);
        r.MaxExcursion.Should().Be(0);
    }

    [Fact]
    public void PercentTolerance_IsPercentOfAbsCenter()
    {
        double[] x = { 0, 1 };
        double[] y = { 100, 100 };
        var r = ToleranceBandEvaluator.Evaluate(
            Band(nominal: 100, tol: 5, unit: ToleranceMode.Percent), x, y);
        r.Lower.Should().Be(95);
        r.Upper.Should().Be(105);
    }

    [Fact]
    public void DerivedMeanCenter_RecentersOnSlice()
    {
        double[] x = { 0, 1, 2 };
        double[] y = { 4, 6, 5 }; // mean 5
        var r = ToleranceBandEvaluator.Evaluate(Band(BandCenterMode.Mean, tol: 2), x, y);
        r.Center.Should().Be(5);
        r.Lower.Should().Be(3);
        r.Upper.Should().Be(7);
    }

    [Fact]
    public void MaxExcursion_IsSigned_BeyondNearestLimit()
    {
        double[] x = { 0, 1, 2, 3 };
        double[] y = { 10, 15, 10, 4 }; // band [8,12]: +3 above upper, -4 below lower
        var r = ToleranceBandEvaluator.Evaluate(Band(nominal: 10, tol: 2), x, y);
        r.MaxExcursion.Should().Be(-4); // |−4| > |+3|
    }

    [Fact]
    public void Crossings_CountInOutTransitions()
    {
        // in, out, out, in -> 2 crossings (in->out at idx1, out->in at idx3)
        double[] x = { 0, 1, 2, 3 };
        double[] y = { 10, 20, 20, 10 };
        var r = ToleranceBandEvaluator.Evaluate(Band(nominal: 10, tol: 2), x, y);
        r.LimitCrossings.Should().Be(2);
    }

    [Fact]
    public void ExceedanceDuration_Interpolated_HalfInterval()
    {
        // Single interval x:0->2, y:10 (in) -> 14 (out), band upper=12. Crosses at y=12 => t=0.5,
        // i.e. x=1. Out for x∈[1,2] => exceedance length 1.0; in-band fraction = 1/2.
        double[] x = { 0, 2 };
        double[] y = { 10, 14 };
        var r = ToleranceBandEvaluator.Evaluate(Band(nominal: 10, tol: 2), x, y);
        r.SpanX.Should().Be(2);
        r.ExceedanceDuration.Should().BeApproximately(1.0, 1e-9);
        r.FractionInBand.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void FullyOutside_FractionZero()
    {
        double[] x = { 0, 1, 2 };
        double[] y = { 100, 101, 102 };
        var r = ToleranceBandEvaluator.Evaluate(Band(nominal: 0, tol: 1), x, y);
        r.FractionInBand.Should().Be(0);
        r.ExceedanceDuration.Should().Be(2); // whole span
    }

    [Fact]
    public void NaNGap_SkipsIntervalAndDoesNotFabricateCrossing()
    {
        // in, NaN, in: the NaN breaks both intervals; no crossing, no exceedance, span only counts
        // intervals with finite endpoints (none here).
        double[] x = { 0, 1, 2 };
        double[] y = { 10, double.NaN, 10 };
        var r = ToleranceBandEvaluator.Evaluate(Band(nominal: 10, tol: 2), x, y);
        r.LimitCrossings.Should().Be(0);
        r.SpanX.Should().Be(0);
    }
}
