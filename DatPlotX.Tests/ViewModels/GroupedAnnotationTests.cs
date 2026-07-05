using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;

namespace DatPlotX.Tests.ViewModels;

/// <summary>
/// Covers the Grouped Parameter Plot annotation manager — directly, without spinning up the
/// Avalonia ScottPlot control. The manager calls a delegate for plot access, so the tests pass
/// a fresh <see cref="Plot"/>.
/// </summary>
public class GroupedAnnotationTests
{
    private static (Plot plot, GroupedPlotAnnotationManager mgr) BuildManager()
    {
        var plot = new Plot();
        var mgr = new GroupedPlotAnnotationManager(getPlot: () => plot, triggerRefresh: () => { });
        return (plot, mgr);
    }

    [Fact]
    public void AddText_RecordsModelAndPlottable()
    {
        var (plot, mgr) = BuildManager();
        var id = mgr.AddText(new TextAnnotationModel { X = 1, Y = 2, Text = "hello" });

        mgr.Texts.Should().HaveCount(1);
        mgr.TextPlottables.Should().ContainKey(id);
        mgr.TextPlottables[id].LabelText.Should().Be("hello");
    }

    [Fact]
    public void RemoveText_DropsBoth()
    {
        var (plot, mgr) = BuildManager();
        var id = mgr.AddText(new TextAnnotationModel { X = 1, Y = 2, Text = "hello" });
        mgr.RemoveText(id).Should().BeTrue();
        mgr.Texts.Should().BeEmpty();
        mgr.TextPlottables.Should().BeEmpty();
    }

    [Fact]
    public void UpdateTextPosition_UpdatesModelAndPlottable()
    {
        var (plot, mgr) = BuildManager();
        var id = mgr.AddText(new TextAnnotationModel { X = 1, Y = 2, Text = "hello" });
        mgr.UpdateTextPosition(id, 10, 20);

        mgr.GetText(id)!.X.Should().Be(10);
        mgr.TextPlottables[id].Location.X.Should().Be(10);
        mgr.TextPlottables[id].Location.Y.Should().Be(20);
    }

    [Fact]
    public void AddArrow_EmitsArrowPlottable()
    {
        var (plot, mgr) = BuildManager();
        var id = mgr.AddArrow(new ArrowAnnotationModel
        {
            BaseX = 0,
            BaseY = 0,
            TipX = 5,
            TipY = 5,
            ArrowEnds = ArrowEnds.End,
            ArrowheadStyle = ArrowheadStyle.Filled,
        });
        mgr.Arrows.Should().HaveCount(1);
        mgr.ArrowPlottables.Should().ContainKey(id);
    }

    [Fact]
    public void ClearAll_RemovesEverything()
    {
        var (plot, mgr) = BuildManager();
        mgr.AddText(new TextAnnotationModel { X = 1, Y = 2, Text = "hello" });
        mgr.AddArrow(new ArrowAnnotationModel { BaseX = 0, BaseY = 0, TipX = 5, TipY = 5 });
        mgr.ClearAll();
        mgr.Count.Should().Be(0);
        mgr.TextPlottables.Should().BeEmpty();
        mgr.ArrowPlottables.Should().BeEmpty();
    }

    [Fact]
    public void Restore_RepopulatesFromModels()
    {
        var (plot, mgr) = BuildManager();
        mgr.Restore(
            new[] { new TextAnnotationModel { X = 1, Y = 2, Text = "A" }, new TextAnnotationModel { X = 3, Y = 4, Text = "B" } },
            new[] { new ArrowAnnotationModel { BaseX = 0, BaseY = 0, TipX = 1, TipY = 1 } });
        mgr.Texts.Should().HaveCount(2);
        mgr.Arrows.Should().HaveCount(1);
        mgr.TextPlottables.Should().HaveCount(2);
        mgr.ArrowPlottables.Should().HaveCount(1);
    }

    [Fact]
    public void Reapply_ReAddsPlottablesAfterPlotClear()
    {
        var (plot, mgr) = BuildManager();
        mgr.AddText(new TextAnnotationModel { X = 1, Y = 2, Text = "hello" });
        mgr.AddArrow(new ArrowAnnotationModel { BaseX = 0, BaseY = 0, TipX = 5, TipY = 5 });

        plot.Clear(); // simulate the Grouped view rebuilding the surface
        mgr.Reapply();

        mgr.TextPlottables.Should().HaveCount(1);
        mgr.ArrowPlottables.Should().HaveCount(1);
    }
}
