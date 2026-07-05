using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class AxisDecimalHelperTests
{
    [Theory]
    // The reported bug: a magenta gFz band spanning ~ -0.05..0.35 collapsed to "0"
    // ticks because decimals defaulted to 0. It should now resolve to 2.
    [InlineData(-0.05, 0.35, 2)]
    [InlineData(0.0, 0.35, 2)]
    // Tight sub-unit ranges need decimals proportional to their resolution.
    [InlineData(0.0, 0.9, 1)]
    [InlineData(0.0, 0.04, 3)]
    // The LIS_TO gFz Y2 case: a ~0.66..1.40 span lands on a 0.05 tick step. At 1 decimal
    // adjacent ticks collapse to identical labels ("1.1, 1.1, 1.0, 1.0"); it needs 2.
    [InlineData(0.66, 1.40, 2)]
    // Whole-number-scale ranges read fine as integers.
    [InlineData(0.0, 9.0, 0)]
    [InlineData(0.0, 50.0, 0)]
    [InlineData(0.0, 50000.0, 0)]
    public void ForRange_PicksResolutionAppropriateDecimals(double min, double max, int expected)
    {
        AxisDecimalHelper.ForRange(min, max).Should().Be(expected);
    }

    [Fact]
    public void ForTicks_PicksFewestDecimalsThatKeepLabelsDistinct()
    {
        // 0.05-step ticks (the gFz case) collide at 1 decimal -> need 2.
        var ticks = new[] { 0.90, 0.95, 1.00, 1.05, 1.10, 1.15, 1.20 };
        AxisDecimalHelper.ForTicks(ticks).Should().Be(2);
    }

    [Fact]
    public void ForTicks_IntegerTicksNeedNoDecimals()
    {
        AxisDecimalHelper.ForTicks(new[] { 0.0, 10.0, 20.0, 30.0 }).Should().Be(0);
    }

    [Fact]
    public void ForTicks_TooFewTicksReturnsZero()
    {
        AxisDecimalHelper.ForTicks(new[] { 1.0 }).Should().Be(0);
    }

    [Fact]
    public void ForRange_IsOrderIndependent()
    {
        AxisDecimalHelper.ForRange(0.35, -0.05)
            .Should().Be(AxisDecimalHelper.ForRange(-0.05, 0.35));
    }

    [Theory]
    [InlineData(double.NaN, 1.0)]
    [InlineData(0.0, double.PositiveInfinity)]
    [InlineData(5.0, 5.0)] // zero-width
    public void ForRange_ReturnsZeroForDegenerateRanges(double min, double max)
    {
        AxisDecimalHelper.ForRange(min, max).Should().Be(0);
    }

    [Fact]
    public void ForRange_ClampsToMaxDecimals()
    {
        // Vanishingly small range would otherwise demand many decimals.
        AxisDecimalHelper.ForRange(0.0, 1e-9)
            .Should().BeLessThanOrEqualTo(AxisDecimalHelper.MaxDecimals);
    }
}
