using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using System.Collections.ObjectModel;

namespace DatPlotX.Tests.ViewModels;

public class CurveManagerDialogViewModelTests
{
    private static CurveConfigurationModel MakeCurve(string name = "gFx", int paneIndex = 0) => new()
    {
        CurveName = name,
        YColumnName = name,
        PaneIndex = paneIndex,
        Color = "#0078D4",
        LineWidth = 2.0,
        IsVisible = true,
        YAxis = YAxisType.Y1
    };

    private static ObservableCollection<CurveConfigurationModel> MakeCurves(params CurveConfigurationModel[] curves)
        => new(curves);

    [Fact]
    public void Constructor_EmptyCollection_HasNoCurves()
    {
        var vm = new CurveManagerDialogViewModel(MakeCurves());
        vm.Curves.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_PopulatesCurves()
    {
        var vm = new CurveManagerDialogViewModel(MakeCurves(MakeCurve("gFx"), MakeCurve("gFy")));
        vm.Curves.Should().HaveCount(2);
    }

    [Fact]
    public void Curves_HaveCorrectNames()
    {
        var vm = new CurveManagerDialogViewModel(MakeCurves(MakeCurve("gFx"), MakeCurve("gFy")));
        vm.Curves.Select(c => c.CurveName).Should().BeEquivalentTo(["gFx", "gFy"]);
    }

    [Fact]
    public void GetModifiedCurves_ReturnsCurrentVisibility_ForAllCurves()
    {
        var visible = MakeCurve("a"); visible.IsVisible = true;
        var hidden = MakeCurve("b"); hidden.IsVisible = false;
        var vm = new CurveManagerDialogViewModel(MakeCurves(visible, hidden));

        var result = vm.GetModifiedCurves();
        result.Single(c => c.CurveName == "a").IsVisible.Should().BeTrue();
        result.Single(c => c.CurveName == "b").IsVisible.Should().BeFalse();
    }

    [Fact]
    public void GetModifiedCurves_AfterTogglingItemIsVisibleAndApplying_ReflectsChange()
    {
        var curve = MakeCurve("a"); curve.IsVisible = true;
        var vm = new CurveManagerDialogViewModel(MakeCurves(curve));
        vm.Curves[0].IsVisible = false;
        vm.ApplyChanges();
        vm.GetModifiedCurves().Single().IsVisible.Should().BeFalse();
    }

    [Fact]
    public void GetCurvesToRemove_NoneMarked_ReturnsEmpty()
    {
        var vm = new CurveManagerDialogViewModel(MakeCurves(MakeCurve("gFx")));
        vm.GetCurvesToRemove().Should().BeEmpty();
    }

    [Fact]
    public void GetCurvesToRemove_OneMarked_ReturnsThatCurve()
    {
        var vm = new CurveManagerDialogViewModel(MakeCurves(MakeCurve("gFx"), MakeCurve("gFy")));
        vm.Curves[0].IsMarkedForRemoval = true;
        var toRemove = vm.GetCurvesToRemove();
        toRemove.Should().HaveCount(1);
        toRemove[0].CurveName.Should().Be("gFx");
    }

    [Fact]
    public void GetCurvesToRemove_AllMarked_ReturnsAll()
    {
        var vm = new CurveManagerDialogViewModel(MakeCurves(MakeCurve("gFx"), MakeCurve("gFy")));
        foreach (var c in vm.Curves) c.IsMarkedForRemoval = true;
        vm.GetCurvesToRemove().Should().HaveCount(2);
    }

    // --- CurveItemViewModel ---

    [Fact]
    public void CurveItemViewModel_PaneDisplay_FormattedCorrectly()
    {
        var curve = MakeCurve(paneIndex: 2);
        var item = new CurveItemViewModel(curve);
        item.PaneDisplay.Should().Be("Pane 3");
    }

    [Fact]
    public void CurveItemViewModel_YAxisDisplay_Y1_IsLeft()
    {
        var curve = MakeCurve();
        curve.YAxis = YAxisType.Y1;
        var item = new CurveItemViewModel(curve);
        item.YAxisDisplay.Should().Contain("Y1");
    }

    [Fact]
    public void CurveItemViewModel_YAxisDisplay_Y2_IsRight()
    {
        var curve = MakeCurve();
        curve.YAxis = YAxisType.Y2;
        var item = new CurveItemViewModel(curve);
        item.YAxisDisplay.Should().Contain("Y2");
    }

    [Fact]
    public void CurveItemViewModel_Edits_DoNotTouchConfig_UntilApplyChanges()
    {
        // Cancel semantics: editing a row must not mutate the live config. The setters hold the
        // edit on the row view model only; a dialog that closes without ApplyChanges (Cancel)
        // therefore leaves the config — and the plot / .DPX — untouched.
        var curve = MakeCurve();
        curve.IsVisible = true;
        curve.Color = "#111111";
        curve.LineWidth = 1.0;

        var item = new CurveItemViewModel(curve);
        item.IsVisible = false;
        item.Color = "#FF0000";
        item.LineWidth = 4.0;

        curve.IsVisible.Should().BeTrue();
        curve.Color.Should().Be("#111111");
        curve.LineWidth.Should().Be(1.0);
    }

    [Fact]
    public void CurveItemViewModel_ApplyChanges_PropagatesEditsBackToConfig()
    {
        var curve = MakeCurve();
        curve.IsVisible = true;
        var item = new CurveItemViewModel(curve);
        item.IsVisible = false;
        item.Color = "#FF0000";
        item.LineWidth = 4.0;

        item.ApplyChanges();

        curve.IsVisible.Should().BeFalse();
        curve.Color.Should().Be("#FF0000");
        curve.LineWidth.Should().Be(4.0);
    }

    [Fact]
    public void CurveItemViewModel_MarkForRemovalCommand_TogglesFlag()
    {
        var curve = MakeCurve();
        var item = new CurveItemViewModel(curve);
        item.IsMarkedForRemoval.Should().BeFalse();
        item.MarkForRemovalCommand.Execute(null);
        item.IsMarkedForRemoval.Should().BeTrue();
        item.MarkForRemovalCommand.Execute(null);
        item.IsMarkedForRemoval.Should().BeFalse();
    }
}
