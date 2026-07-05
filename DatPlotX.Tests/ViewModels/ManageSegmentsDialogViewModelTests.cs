using DatPlotX.Models.Analysis;
using DatPlotX.ViewModels;
using FluentAssertions;

namespace DatPlotX.Tests.ViewModels;

public class ManageSegmentsDialogViewModelTests
{
    private static AnalysisSegment Manual(string name, double a, double b) =>
        new(Guid.NewGuid(), name, a, b, AnalysisSegmentSource.Manual);

    [Fact]
    public void SelectedRow_DefaultsToActiveSegment()
    {
        var visible = AnalysisSegment.VisibleWindow(0, 10);
        var climb = Manual("Climb", 1, 3);
        var vm = new ManageSegmentsDialogViewModel(new[] { visible, climb }, climb.Id);

        vm.SelectedRow!.Id.Should().Be(climb.Id);
        vm.ActiveId.Should().Be(climb.Id);
    }

    [Fact]
    public void VisibleWindowRow_CannotBeDeletedOrRenamed()
    {
        var visible = AnalysisSegment.VisibleWindow(0, 10);
        var vm = new ManageSegmentsDialogViewModel(new[] { visible }, visible.Id);

        var row = vm.Rows[0];
        row.CanDelete.Should().BeFalse();
        row.CanRename.Should().BeFalse();
        row.MarkForRemovalCommand.Execute(null);
        row.IsMarkedForRemoval.Should().BeFalse("visible-window mark is a no-op");
        vm.ToRemove.Should().BeEmpty();
    }

    [Fact]
    public void Rename_AndDelete_AreReported()
    {
        var visible = AnalysisSegment.VisibleWindow(0, 10);
        var a = Manual("A", 1, 2);
        var b = Manual("B", 3, 4);
        var vm = new ManageSegmentsDialogViewModel(new[] { visible, a, b }, a.Id);

        var rowA = vm.Rows.First(r => r.Id == a.Id);
        var rowB = vm.Rows.First(r => r.Id == b.Id);

        rowA.Name = "Climb";                 // rename
        rowB.MarkForRemovalCommand.Execute(null); // delete

        vm.Renamed.Select(r => r.Id).Should().ContainSingle().Which.Should().Be(a.Id);
        vm.ToRemove.Select(r => r.Id).Should().ContainSingle().Which.Should().Be(b.Id);
    }

    [Fact]
    public void ActiveId_NullWhenSelectedRowMarkedForRemoval()
    {
        var visible = AnalysisSegment.VisibleWindow(0, 10);
        var a = Manual("A", 1, 2);
        var vm = new ManageSegmentsDialogViewModel(new[] { visible, a }, a.Id);

        vm.SelectedRow = vm.Rows.First(r => r.Id == a.Id);
        vm.SelectedRow.MarkForRemovalCommand.Execute(null);

        vm.ActiveId.Should().BeNull("a row marked for deletion shouldn't also become active");
    }
}
