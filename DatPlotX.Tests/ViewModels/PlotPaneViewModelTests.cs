using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;

namespace DatPlotX.Tests.ViewModels;

public class PlotPaneViewModelTests
{
    private static PlotPaneViewModel MakePaneWithPlot(int index = 0)
    {
        var model = new PlotPaneModel { Index = index, Name = $"Pane {index + 1}" };
        var vm = new PlotPaneViewModel(model)
        {
            PlotModel = new Plot()
        };
        return vm;
    }

    [Fact]
    public void PlotModel_Setter_SignalsWhenPlotReady()
    {
        var pane = new PlotPaneViewModel(new PlotPaneModel());
        var readyTask = pane.WhenPlotReady();
        readyTask.IsCompleted.Should().BeFalse();

        pane.PlotModel = new Plot();

        readyTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void WhenPlotReady_AlreadySet_ReturnsCompletedTask()
    {
        var pane = MakePaneWithPlot();
        pane.WhenPlotReady().IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void BeginUpdate_Suppresses_BatchesSingleEvent()
    {
        var pane = MakePaneWithPlot();
        int updates = 0;
        pane.OnPlotUpdated += () => updates++;

        using (pane.BeginUpdate())
        {
            // Trigger several inner updates
            pane.AddEventLine(1.0, "a", "#000000");
            pane.AddEventLine(2.0, "b", "#000000");
            pane.AddEventLine(3.0, "c", "#000000");
        }

        updates.Should().Be(1);
    }

    [Fact]
    public void BeginUpdate_Nested_FiresOnlyAfterOutermostDispose()
    {
        var pane = MakePaneWithPlot();
        int updates = 0;
        pane.OnPlotUpdated += () => updates++;

        var outer = pane.BeginUpdate();
        var inner = pane.BeginUpdate();
        pane.AddEventLine(1.0, "a", "#000000");
        inner.Dispose();
        updates.Should().Be(0);
        outer.Dispose();
        updates.Should().Be(1);
    }

    [Fact]
    public void Dispose_RemovesSubscribers()
    {
        var pane = MakePaneWithPlot();
        int updates = 0;
        pane.OnPlotUpdated += () => updates++;

        pane.Dispose();
        // After dispose the event is cleared. Triggering any work should not reach subscribers.
        // Use AutoScale which is a no-op safe call that would otherwise trigger an update.
        pane.AutoScale();

        updates.Should().Be(0);
    }

    [Fact]
    public void Dispose_CancelsPendingWhenPlotReady()
    {
        var pane = new PlotPaneViewModel(new PlotPaneModel());
        var pending = pane.WhenPlotReady();
        pane.Dispose();
        pending.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public void AddScatterCurve_AddsToCurveCollection()
    {
        var pane = MakePaneWithPlot();
        var config = new CurveConfigurationModel
        {
            CurveName = "test",
            YColumnName = "test",
            Color = "#FF0000",
            YAxis = YAxisType.Y1
        };
        pane.AddScatterCurve([0.0, 1.0, 2.0], [1.0, 2.0, 3.0], config);
        pane.GetAllCurveConfigs().Should().HaveCount(1);
    }

    [Fact]
    public void Clear_RemovesAllCurvesAndResetsAxisLabels()
    {
        var pane = MakePaneWithPlot();
        var config = new CurveConfigurationModel { CurveName = "c", YColumnName = "c", Color = "#000000" };
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.Clear();
        pane.GetAllCurveConfigs().Should().BeEmpty();
    }

    [Fact]
    public void Clear_PreservesGlobalEventLinesAndAnnotations()
    {
        // Review M2 / C4: "Clear Pane" promises to remove curves only. It must NOT nuke the whole
        // PlotModel — that detached global event lines / annotations while their owning services
        // kept dangling references (ghost state on reload / cross-pane drag).
        var pane = MakePaneWithPlot();
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0],
            new CurveConfigurationModel { CurveName = "c", YColumnName = "c", Color = "#000000" });

        var eventLineId = Guid.NewGuid();
        pane.AddGlobalEventLineVisual(eventLineId, xPosition: 0.5, label: "EL", showLabel: true);
        var textModel = new TextAnnotationModel { X = 0.5, Y = 1.0, Text = "note" };
        pane.AddTextAnnotation(textModel);

        var plottablesBefore = pane.PlotModel!.GetPlottables().Count();

        pane.Clear();

        pane.GetAllCurveConfigs().Should().BeEmpty();
        pane.GetGlobalEventLineIds().Should().Contain(eventLineId);
        pane.GetTextAnnotationIds().Should().Contain(textModel.Id);
        // The event line + label + annotation plottables must still be on the plot.
        pane.PlotModel!.GetPlottables().Count().Should().BeLessThan(plottablesBefore)
            .And.BeGreaterThan(0);
    }
}
