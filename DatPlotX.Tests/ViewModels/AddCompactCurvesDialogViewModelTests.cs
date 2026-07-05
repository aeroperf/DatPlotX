using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

public class AddCompactCurvesDialogViewModelTests
{
    private static DataTable BuildTable()
    {
        var t = new DataTable();
        t.Columns.Add("time", typeof(double));
        t.Columns.Add("alt", typeof(double));
        t.Columns.Add("ias", typeof(double));
        t.Columns.Add("gear", typeof(int)); // boolean-like 0/1
        t.Columns.Add("note", typeof(string)); // non-numeric, should be filtered
        for (int i = 0; i < 50; i++)
        {
            t.Rows.Add((double)i, 1000.0 + i, 200.0 + i, i % 2, $"row{i}");
        }
        return t;
    }

    [Fact]
    public void AvailableColumns_ExcludeXColumn()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns.Select(c => c.ColumnName).Should().NotContain("time");
    }

    [Fact]
    public void AvailableColumns_ExcludeStringColumns()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns.Select(c => c.ColumnName).Should().NotContain("note");
    }

    [Fact]
    public void AvailableColumns_IncludeNumericColumns()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns.Select(c => c.ColumnName)
            .Should().BeEquivalentTo(new[] { "alt", "ias", "gear" });
    }

    [Fact]
    public void HasAnySelection_FalseInitially()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.HasAnySelection.Should().BeFalse();
    }

    [Fact]
    public void HasAnySelection_FollowsCheckboxState()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns[0].IsSelected = true;
        vm.HasAnySelection.Should().BeTrue();

        vm.AvailableColumns[0].IsSelected = false;
        vm.HasAnySelection.Should().BeFalse();
    }

    [Fact]
    public void AdvanceToStage2_BuildsOneDraftPerSelection()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns[0].IsSelected = true; // alt
        vm.AvailableColumns[2].IsSelected = true; // gear

        vm.AdvanceToStage2();

        vm.Stage.Should().Be(2);
        vm.Drafts.Should().HaveCount(2);
        vm.Drafts[0].SourceColumn.Should().Be("alt");
        vm.Drafts[1].SourceColumn.Should().Be("gear");
    }

    [Fact]
    public void AdvanceToStage2_DetectsBooleanColumns()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns.Single(c => c.ColumnName == "gear").IsSelected = true;
        vm.AvailableColumns.Single(c => c.ColumnName == "alt").IsSelected = true;

        vm.AdvanceToStage2();

        vm.Drafts.Single(d => d.SourceColumn == "gear").IsBoolean.Should().BeTrue();
        vm.Drafts.Single(d => d.SourceColumn == "alt").IsBoolean.Should().BeFalse();
    }

    [Fact]
    public void AdvanceToStage2_AlternatesAxisSides_StartingFromExistingCount()
    {
        // existingCurveCount=0 → first draft Left, second Right.
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        foreach (var c in vm.AvailableColumns) c.IsSelected = true;

        vm.AdvanceToStage2();

        vm.Drafts[0].AxisSide.Should().Be(AxisSide.Left);
        vm.Drafts[1].AxisSide.Should().Be(AxisSide.Right);
        vm.Drafts[2].AxisSide.Should().Be(AxisSide.Left);
    }

    [Fact]
    public void AdvanceToStage2_ContinuesPalette_FromExistingCount()
    {
        var vmA = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vmA.AvailableColumns[0].IsSelected = true;
        vmA.AdvanceToStage2();
        var firstColor = vmA.Drafts[0].Color;

        var vmB = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 1);
        vmB.AvailableColumns[0].IsSelected = true;
        vmB.AdvanceToStage2();
        var secondColor = vmB.Drafts[0].Color;

        // Different palette index → different default color.
        secondColor.Should().NotBe(firstColor);
    }

    [Fact]
    public void AdvanceToStage2_AlsoAffectsAxisSide_FromExistingCount()
    {
        // existingCurveCount=1 → first new draft Right, second Left.
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 1);
        foreach (var c in vm.AvailableColumns) c.IsSelected = true;

        vm.AdvanceToStage2();

        vm.Drafts[0].AxisSide.Should().Be(AxisSide.Right);
        vm.Drafts[1].AxisSide.Should().Be(AxisSide.Left);
    }

    [Fact]
    public void BackToStage1_ResetsStage_KeepsDrafts()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns[0].IsSelected = true;
        vm.AdvanceToStage2();

        vm.BackToStage1();

        vm.Stage.Should().Be(1);
        vm.Drafts.Should().HaveCount(1); // not cleared on back
    }

    [Fact]
    public void BuildCurves_ReturnsOneCurvePerDraft_PreservingProperties()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns.Single(c => c.ColumnName == "alt").IsSelected = true;
        vm.AdvanceToStage2();

        vm.Drafts[0].DisplayName = "Altitude";
        vm.Drafts[0].Unit = "ft";
        vm.Drafts[0].LineWidth = 2.5;
        vm.Drafts[0].Color = "#123456";
        vm.Drafts[0].YMin = 0;
        vm.Drafts[0].YMax = 5000;
        vm.Drafts[0].AllowOverflow = false;

        var curves = vm.BuildCurves();

        curves.Should().HaveCount(1);
        var c = curves[0];
        c.DisplayName.Should().Be("Altitude");
        c.Unit.Should().Be("ft");
        c.LineWidth.Should().Be(2.5);
        c.Color.Should().Be("#123456");
        c.YMin.Should().Be(0);
        c.YMax.Should().Be(5000);
        c.AllowOverflow.Should().BeFalse();
        c.IsVisible.Should().BeTrue();
        c.SourceColumn.Should().Be("alt");
    }

    [Fact]
    public void BuildCurves_BlankDisplayName_FallsBackToSourceColumn()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns[0].IsSelected = true;
        vm.AdvanceToStage2();
        vm.Drafts[0].DisplayName = "   ";

        var curves = vm.BuildCurves();

        curves[0].DisplayName.Should().Be(vm.Drafts[0].SourceColumn);
    }

    [Fact]
    public void BuildCurves_BlankUnit_StoredAsNull_NotEmptyString()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns[0].IsSelected = true;
        vm.AdvanceToStage2();
        vm.Drafts[0].Unit = "  ";

        var curves = vm.BuildCurves();

        curves[0].Unit.Should().BeNull();
    }

    [Fact]
    public void BuildCurves_TrimsDisplayNameAndUnit()
    {
        var vm = new AddCompactCurvesDialogViewModel(BuildTable(), "time", 0);
        vm.AvailableColumns[0].IsSelected = true;
        vm.AdvanceToStage2();
        vm.Drafts[0].DisplayName = "  Altitude  ";
        vm.Drafts[0].Unit = "  ft  ";

        var curves = vm.BuildCurves();

        curves[0].DisplayName.Should().Be("Altitude");
        curves[0].Unit.Should().Be("ft");
    }

    [Fact]
    public void NullTable_ProducesEmptyAvailableColumns()
    {
        var vm = new AddCompactCurvesDialogViewModel((DataTable?)null, "time", 0);
        vm.AvailableColumns.Should().BeEmpty();
    }
}
