using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;
using System.Collections.ObjectModel;

namespace DatPlotX.Tests.Services;

public class PaneCoordinationServiceTests
{
    private static PlotPaneViewModel Pane(int index) =>
        new(new PlotPaneModel { Index = index, Name = $"Pane {index + 1}" }) { PlotModel = new Plot() };

    [Fact]
    public void RemovePane_DisposesLastPane()
    {
        var svc = new PaneCoordinationService(new GlobalEventLineService());
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0), Pane(1) };
        var last = panes[1];

        svc.RemovePane(panes).Should().BeTrue();

        last.IsDisposed.Should().BeTrue();
        panes.Should().HaveCount(1);
    }

    [Fact]
    public void RemovePane_AtMinimumCount_ReturnsFalse()
    {
        var svc = new PaneCoordinationService(new GlobalEventLineService());
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };

        svc.RemovePane(panes).Should().BeFalse();
        panes.Should().HaveCount(1);
        panes[0].IsDisposed.Should().BeFalse();
    }

    [Fact]
    public void RemovePane_ReindexesRemaining()
    {
        var svc = new PaneCoordinationService(new GlobalEventLineService());
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0), Pane(1), Pane(2) };

        svc.RemovePane(panes).Should().BeTrue();

        panes.Should().HaveCount(2);
        panes[0].PaneModel.Index.Should().Be(0);
        panes[1].PaneModel.Index.Should().Be(1);
    }
}
