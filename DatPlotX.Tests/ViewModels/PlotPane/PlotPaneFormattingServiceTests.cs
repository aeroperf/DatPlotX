using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;

namespace DatPlotX.Tests.ViewModels.PlotPane;

public class PlotPaneFormattingServiceTests
{
    private static PlotPaneViewModel Pane()
    {
        return new PlotPaneViewModel(new PlotPaneModel { Index = 0 }) { PlotModel = new Plot() };
    }

    [Fact]
    public void SetXAxisRange_PersistsRange()
    {
        var pane = Pane();
        pane.SetXAxisRange(0.0, 10.0);
        var range = pane.GetXAxisRange();
        range.Should().NotBeNull();
        range!.Value.Min.Should().Be(0.0);
        range.Value.Max.Should().Be(10.0);
    }

    [Fact]
    public void SetYAxisRange_PersistsRange()
    {
        var pane = Pane();
        pane.SetYAxisRange(-5.0, 5.0);
        var range = pane.GetYAxisRange();
        range.Should().NotBeNull();
        range!.Value.Min.Should().Be(-5.0);
        range.Value.Max.Should().Be(5.0);
    }

    [Fact]
    public void SetY2AxisRange_PersistsRange()
    {
        var pane = Pane();
        pane.SetY2AxisRange(100.0, 200.0);
        var range = pane.GetY2AxisRange();
        range.Should().NotBeNull();
        range!.Value.Min.Should().Be(100.0);
        range.Value.Max.Should().Be(200.0);
    }

    [Fact]
    public void ApplyFormatting_DoesNotThrow()
    {
        var pane = Pane();
        var act = () => pane.ApplyFormatting();
        act.Should().NotThrow();
    }

    [Fact]
    public void ShowLegend_TogglesSuccessfully()
    {
        var pane = Pane();
        pane.ShowLegend = true;
        pane.ApplyFormatting();
        pane.ShowLegend = false;
        pane.ApplyFormatting();
    }
}
