using DatPlotX.Models;
using DatPlotX.ViewModels;
using DatPlotX.Views.Compact;
using FluentAssertions;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

public class CompactPlotViewModelTests
{
    private static PlotDataModel BuildData()
    {
        var t = new DataTable();
        t.Columns.Add("time", typeof(double));
        t.Columns.Add("alt", typeof(double));
        t.Columns.Add("sw", typeof(int));
        for (int i = 0; i < 100; i++)
        {
            t.Rows.Add((double)i, 1000.0 + i, i % 2);
        }
        return new PlotDataModel { Data = t };
    }

    [Fact]
    public void Rebuild_NoCurves_ProducesEmptyModelWithXAndPlaceholderYAxis()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");

        vm.PlotModel.Series.Should().BeEmpty();
        // X axis + a hidden placeholder Y axis. The Y axis is required: a model with only an X
        // axis measures to a degenerate / NaN size in OxyPlot and crashes Avalonia's layout pass.
        vm.PlotModel.Axes.Should().HaveCount(2);
        vm.PlotModel.Axes.Should().Contain(a => a.Position == AxisPosition.Bottom);
        vm.PlotModel.Axes.Should().Contain(a => a.Position == AxisPosition.Left && !a.IsAxisVisible);
    }

    [Fact]
    public void AddCurve_AppendsBandedYAxisAndLineSeries()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");

        vm.AddCurve(new CompactCurveModel
        {
            DisplayName = "Altitude",
            SourceColumn = "alt",
            AxisSide = AxisSide.Left,
            Color = "#0000FF",
        });

        vm.PlotModel.Series.OfType<LineSeries>().Should().HaveCount(1);
        var yAxes = vm.PlotModel.Axes.OfType<LinearAxis>()
            .Where(a => a.Position == AxisPosition.Left).ToList();
        yAxes.Should().HaveCount(1);
        yAxes[0].StartPosition.Should().BeApproximately(0d, 1e-6);
        yAxes[0].EndPosition.Should().BeApproximately(1d, 1e-6);
    }

    [Fact]
    public void TwoCurves_BoolAndAnalog_BoolBandIsSmaller()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt", IsBoolean = false, AxisSide = AxisSide.Left });
        vm.AddCurve(new CompactCurveModel { SourceColumn = "sw", IsBoolean = true, AxisSide = AxisSide.Right });

        var leftAxis = vm.PlotModel.Axes.OfType<LinearAxis>().First(a => a.Position == AxisPosition.Left);
        var rightAxis = vm.PlotModel.Axes.OfType<LinearAxis>().First(a => a.Position == AxisPosition.Right);
        double leftHeight = leftAxis.EndPosition - leftAxis.StartPosition;
        double rightHeight = rightAxis.EndPosition - rightAxis.StartPosition;
        rightHeight.Should().BeLessThan(leftHeight);
    }

    [Fact]
    public void AlternatingTiers_LeftAndRight_BothPopulated()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt", AxisSide = AxisSide.Left });
        vm.AddCurve(new CompactCurveModel { SourceColumn = "sw", AxisSide = AxisSide.Right });

        vm.PlotModel.Axes.OfType<LinearAxis>().Count(a => a.Position == AxisPosition.Left).Should().Be(1);
        vm.PlotModel.Axes.OfType<LinearAxis>().Count(a => a.Position == AxisPosition.Right).Should().Be(1);
    }

    [Fact]
    public void RemoveCurve_RebuildsWithoutSeries()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        var c = new CompactCurveModel { SourceColumn = "alt" };
        vm.AddCurve(c);
        vm.RemoveCurve(c.Id);

        vm.PlotModel.Series.Should().BeEmpty();
    }

    [Fact]
    public void Clear_RemovesAllCurves()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });
        vm.AddCurve(new CompactCurveModel { SourceColumn = "sw" });

        vm.Clear();

        vm.Curves.Should().BeEmpty();
        vm.PlotModel.Series.Should().BeEmpty();
    }

    [Fact]
    public void ReplaceCurves_PreservesOrder_AndRebuildsSeries()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");

        var seed = new[]
        {
            new CompactCurveModel { Id = Guid.NewGuid(), SourceColumn = "alt", AxisSide = AxisSide.Left },
            new CompactCurveModel { Id = Guid.NewGuid(), SourceColumn = "sw", IsBoolean = true, AxisSide = AxisSide.Right },
        };

        vm.ReplaceCurves(seed);

        vm.Curves.Should().HaveCount(2);
        vm.Curves[0].SourceColumn.Should().Be("alt");
        vm.Curves[1].SourceColumn.Should().Be("sw");
        vm.PlotModel.Series.OfType<LineSeries>().Should().HaveCount(2);
    }

    [Fact]
    public void ReplaceCurves_ClearsExisting()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });

        vm.ReplaceCurves(Array.Empty<CompactCurveModel>());

        vm.Curves.Should().BeEmpty();
        vm.PlotModel.Series.Should().BeEmpty();
    }

    [Fact]
    public void SetData_NullData_LeavesEmptyModel()
    {
        var vm = new CompactPlotViewModel();
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });

        vm.SetData(null, null);

        // X axis + hidden placeholder Y axis still present; series wiped because there's no data.
        // (The Y axis guards against OxyPlot's degenerate-plot-area crash on an X-only model.)
        vm.PlotModel.Series.Should().BeEmpty();
        vm.PlotModel.Axes.Should().HaveCount(2);
        vm.XAxisColumn.Should().BeNull();
    }

    [Fact]
    public void Rebuild_DropsSeriesWhose_ColumnIsMissing_WithoutThrowing()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");

        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });
        vm.AddCurve(new CompactCurveModel { SourceColumn = "does_not_exist" });

        // The good curve still renders; the missing-column curve is silently skipped.
        vm.PlotModel.Series.OfType<LineSeries>().Should().HaveCount(1);
    }

    [Fact]
    public void NaNRows_AreSkipped_NotRendered()
    {
        var t = new DataTable();
        t.Columns.Add("time", typeof(double));
        t.Columns.Add("alt", typeof(double));
        t.Rows.Add(0d, double.NaN);
        t.Rows.Add(1d, 100d);
        t.Rows.Add(2d, double.NaN);
        t.Rows.Add(3d, 300d);

        var vm = new CompactPlotViewModel();
        vm.SetData(new PlotDataModel { Data = t }, "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });

        var series = vm.PlotModel.Series.OfType<LineSeries>().Single();
        series.Points.Should().HaveCount(2); // NaN rows dropped
    }

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("FF0000")]
    [InlineData("#80FF0000")]
    [InlineData("not-a-color")]
    [InlineData("")]
    public void Rebuild_TolerantOfColorStrings(string color)
    {
        // Malformed colors fall back to black; they should never throw.
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");

        var act = () => vm.AddCurve(new CompactCurveModel { SourceColumn = "alt", Color = color });
        act.Should().NotThrow();
    }

    [Fact]
    public void AddEventLine_AssignsLabelAndRendersFullHeightAnnotation()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });

        var id = vm.AddEventLine(50d);

        vm.EventLines.Should().HaveCount(1);
        vm.EventLines[0].Id.Should().Be(id);
        vm.EventLines[0].Label.Should().Be("E1");
        vm.EventLines[0].XPosition.Should().Be(50d);

        var annotations = vm.PlotModel.Annotations.OfType<CompactEventLineAnnotation>().ToList();
        annotations.Should().HaveCount(1);
        annotations[0].X.Should().Be(50d);
        annotations[0].Label.Should().Be("E1");
    }

    [Fact]
    public void AddEventLine_LabelCounter_Increments()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });

        vm.AddEventLine(10d);
        vm.AddEventLine(20d);
        vm.AddEventLine(30d);

        vm.EventLines.Select(e => e.Label).Should().Equal("E1", "E2", "E3");
    }

    [Fact]
    public void RemoveEventLine_DropsFromCollectionAndAnnotations()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });
        var id = vm.AddEventLine(50d);
        vm.AddEventLine(60d);

        vm.RemoveEventLine(id).Should().BeTrue();
        vm.EventLines.Should().HaveCount(1);
        vm.PlotModel.Annotations.OfType<CompactEventLineAnnotation>().Should().HaveCount(1);
    }

    [Fact]
    public void RemoveEventLine_UnknownId_ReturnsFalse()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");

        vm.RemoveEventLine(System.Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void MoveEventLine_UpdatesXPositionAndRebuildsAnnotation()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });
        var id = vm.AddEventLine(20d);

        vm.MoveEventLine(id, 75d);

        vm.EventLines[0].XPosition.Should().Be(75d);
        vm.PlotModel.Annotations.OfType<CompactEventLineAnnotation>().Single().X.Should().Be(75d);
    }

    [Fact]
    public void ClearEventLines_RemovesAllAndResetsLabelCounter()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });
        vm.AddEventLine(10d);
        vm.AddEventLine(20d);

        vm.ClearEventLines();
        vm.EventLines.Should().BeEmpty();
        vm.PlotModel.Annotations.OfType<CompactEventLineAnnotation>().Should().BeEmpty();

        // Counter should restart at E1 after clear.
        vm.AddEventLine(30d);
        vm.EventLines[0].Label.Should().Be("E1");
    }

    [Fact]
    public void ReplaceEventLines_RestoresLabelCounterFromExistingLabels()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { SourceColumn = "alt" });

        // Simulate project load with prior labels — counter must continue past the highest E#.
        vm.ReplaceEventLines(new[]
        {
            new EventLineModel { Label = "E1", XPosition = 5d },
            new EventLineModel { Label = "E5", XPosition = 25d },
            new EventLineModel { Label = "Custom", XPosition = 50d },
        });

        vm.EventLines.Should().HaveCount(3);
        vm.AddEventLine(60d);
        vm.EventLines.Last().Label.Should().Be("E6");
    }

    [Fact]
    public void EventLineTag_RoundTrip()
    {
        var id = System.Guid.NewGuid();
        var tag = CompactPlotViewModel.BuildEventLineTag(id);
        CompactPlotViewModel.TryParseEventLineTag(tag).Should().Be(id);
        CompactPlotViewModel.TryParseEventLineTag("not-a-tag").Should().BeNull();
        CompactPlotViewModel.TryParseEventLineTag(null).Should().BeNull();
    }
}
