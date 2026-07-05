using DatPlotX.Services.Analysis;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Analysis;

public class AnalysisCurveDataTests
{
    // ---------- Periodic (Signal) ----------
    [Fact]
    public void Periodic_SliceIndices_O1_IndexMath()
    {
        // 10 samples at period 0.5 → X = 0, 0.5, 1.0, ..., 4.5
        var data = new AnalysisCurveData("a", new double[10], period: 0.5);

        var (s, e) = data.SliceIndices(xMin: 1.0, xMax: 2.5);
        // Sample at X=1.0 is index 2 (ceil(1.0/0.5)=2); X=2.5 is index 5 (floor(2.5/0.5)=5)
        s.Should().Be(2);
        e.Should().Be(5);
    }

    [Fact]
    public void Periodic_RangeBeyondLastSample_ClampedAtEnd()
    {
        var data = new AnalysisCurveData("a", new double[5], period: 1.0);  // X = 0..4
        var (_, e) = data.SliceIndices(0, 100);
        e.Should().Be(4);
    }

    [Fact]
    public void Periodic_NegativeStart_ClampedAtZero()
    {
        var data = new AnalysisCurveData("a", new double[5], period: 1.0);
        var (s, _) = data.SliceIndices(-10, 4);
        s.Should().Be(0);
    }

    [Fact]
    public void Periodic_EmptyRange_StartExceedsEnd()
    {
        var data = new AnalysisCurveData("a", new double[5], period: 1.0);
        var (s, e) = data.SliceIndices(10, 20);
        (s > e).Should().BeTrue();
    }

    [Fact]
    public void Periodic_Slice_BuildsX_FromIndex()
    {
        var y = new double[] { 10, 20, 30, 40, 50 };
        var data = new AnalysisCurveData("a", y, period: 2.0);   // X = 0, 2, 4, 6, 8

        var (x, ySlice) = data.Slice(1, 3);
        x.Should().Equal(2, 4, 6);
        ySlice.Should().Equal(20, 30, 40);
    }

    // ---------- Scatter ----------
    [Fact]
    public void Scatter_SliceIndices_BinarySearch()
    {
        var x = new double[] { 0, 1, 2, 4, 7, 9, 15 };
        var y = new double[] { 0, 0, 0, 0, 0, 0, 0 };
        var data = new AnalysisCurveData("a", x, y);

        var (s, e) = data.SliceIndices(2, 9);
        s.Should().Be(2);    // first index where x >= 2
        e.Should().Be(5);    // last index where x <= 9
    }

    [Fact]
    public void Scatter_RangeOutsideData_ReturnsEmptySlice()
    {
        var x = new double[] { 10, 20, 30 };
        var y = new double[] { 0, 0, 0 };
        var data = new AnalysisCurveData("a", x, y);

        var (s, e) = data.SliceIndices(100, 200);
        (s > e).Should().BeTrue();
    }

    [Fact]
    public void Scatter_Slice_CopiesXAndY()
    {
        var x = new double[] { 0, 5, 10, 15, 20 };
        var y = new double[] { 1, 2, 3, 4, 5 };
        var data = new AnalysisCurveData("a", x, y);

        var (xs, ys) = data.Slice(1, 3);
        xs.Should().Equal(5, 10, 15);
        ys.Should().Equal(2, 3, 4);
    }

    // ---------- Validation ----------
    [Fact]
    public void Periodic_RequiresPositivePeriod()
    {
        var act = () => new AnalysisCurveData("a", new double[5], period: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Scatter_RequiresEqualLengths()
    {
        var act = () => new AnalysisCurveData("a", new double[3], new double[5]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Empty_Length_ReturnsEmptySlice()
    {
        var data = new AnalysisCurveData("a", Array.Empty<double>(), period: 1);
        var (s, e) = data.SliceIndices(0, 10);
        (s > e).Should().BeTrue();
    }
}
