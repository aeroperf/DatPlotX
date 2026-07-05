using DatPlotX.Services.Analysis;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Analysis;

/// <summary>
/// Regression coverage for the source's curve-data shaping decision. The original bug treated
/// scatter curves (Period = 0) as periodic with Period = 1, so a visible window like [0, 360]
/// sliced array indices [0, 360] instead of the real time range — yielding wildly wrong
/// min/max/mean for any curve with more than ~360 samples.
/// </summary>
public class StackedAnalysisCurveSourceTests
{
    [Fact]
    public void BuildData_PeriodicCurve_UsesIndexTimesPeriod()
    {
        var y = new double[] { 0, 1, 2, 3, 4 };
        var data = StackedAnalysisCurveSource.BuildData("c", period: 2.0, y, xData: null);

        data.IsPeriodic.Should().BeTrue();
        data.XAt(0).Should().Be(0);
        data.XAt(4).Should().Be(8);
    }

    [Fact]
    public void BuildData_ScatterCurve_UsesRealXArray()
    {
        // Y sampled at real times 0,100,200,300 — NOT 0,1,2,3.
        var x = new double[] { 0, 100, 200, 300 };
        var y = new double[] { 10, 20, 30, 40 };

        var data = StackedAnalysisCurveSource.BuildData("c", period: 0, y, xData: x);

        data.IsPeriodic.Should().BeFalse();
        data.XAt(0).Should().Be(0);
        data.XAt(3).Should().Be(300);
    }

    [Fact]
    public void BuildData_ScatterCurve_VisibleWindowSlicesByRealX_NotIndex()
    {
        // 1000 samples spread over t = 0..999 seconds. A window of [500, 600] must select
        // the samples in that TIME range, not the first 100 array entries.
        int n = 1000;
        var x = new double[n];
        var y = new double[n];
        for (int i = 0; i < n; i++) { x[i] = i; y[i] = i * 10.0; }

        var data = StackedAnalysisCurveSource.BuildData("c", period: 0, y, xData: x);

        var (start, end) = data.SliceIndices(500, 600);
        var (_, ySlice) = data.Slice(start, end);

        // Pre-fix this returned indices [0..100] → max 1000; now it must be the t=500..600
        // band → max 6000.
        ySlice.Max().Should().Be(6000);
        ySlice.Min().Should().Be(5000);
    }

    [Fact]
    public void BuildData_ScatterWithMismatchedX_FallsBackToIndex()
    {
        var y = new double[] { 1, 2, 3 };
        var x = new double[] { 0, 1 }; // wrong length

        var data = StackedAnalysisCurveSource.BuildData("c", period: 0, y, xData: x);

        // Falls back to periodic index-as-X (period 1) rather than throwing.
        data.IsPeriodic.Should().BeTrue();
        data.XAt(2).Should().Be(2);
    }
}
