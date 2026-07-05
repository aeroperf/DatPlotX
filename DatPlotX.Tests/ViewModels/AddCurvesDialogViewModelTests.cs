using DatPlotX.ViewModels;
using FluentAssertions;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

public class AddCurvesDialogViewModelTests
{
    private static DataTable MakeTable(params (string name, Type type)[] columns)
    {
        var table = new DataTable();
        foreach (var (name, type) in columns)
            table.Columns.Add(name, type);
        return table;
    }

    [Fact]
    public void Constructor_NullData_EmptyParameters()
    {
        var vm = new AddCurvesDialogViewModel(null, "time", 0);
        vm.AvailableYParameters.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_ExcludesXColumn()
    {
        var table = MakeTable(("time", typeof(double)), ("gFx", typeof(double)), ("gFy", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);
        vm.AvailableYParameters.Should().NotContain("time");
        vm.AvailableYParameters.Should().Contain("gFx").And.Contain("gFy");
    }

    [Fact]
    public void Constructor_ExcludesStringColumns()
    {
        var table = MakeTable(("time", typeof(double)), ("label", typeof(string)), ("value", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);
        vm.AvailableYParameters.Should().NotContain("label");
    }

    [Fact]
    public void Constructor_IncludesIntColumns()
    {
        var table = MakeTable(("time", typeof(double)), ("count", typeof(int)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);
        vm.AvailableYParameters.Should().Contain("count");
    }

    [Fact]
    public void Constructor_DefaultsFirstParameter()
    {
        var table = MakeTable(("time", typeof(double)), ("gFx", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);
        vm.SelectedParameter.Should().Be("gFx");
    }

    [Fact]
    public void Constructor_AlwaysHasTwoYAxes()
    {
        var vm = new AddCurvesDialogViewModel(null, "time", 0);
        vm.AvailableYAxes.Should().HaveCount(2);
        vm.AvailableYAxes[0].AxisType.Should().Be("Y1");
        vm.AvailableYAxes[1].AxisType.Should().Be("Y2");
    }

    [Fact]
    public void Constructor_DefaultsToY1Axis()
    {
        var vm = new AddCurvesDialogViewModel(null, "time", 0);
        vm.SelectedYAxis!.AxisType.Should().Be("Y1");
    }

    [Fact]
    public void TargetPaneIndex_ReturnsConstructorValue()
    {
        var vm = new AddCurvesDialogViewModel(null, "time", 3);
        vm.TargetPaneIndex.Should().Be(3);
    }

    [Fact]
    public void TrackPlottedCurve_AddsToCurvesCollection()
    {
        var table = MakeTable(("time", typeof(double)), ("gFx", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);
        vm.SelectedParameter = "gFx";
        vm.SelectedYAxis = vm.AvailableYAxes[0];

        vm.TrackPlottedCurve();

        vm.PlottedCurves.Should().HaveCount(1);
        vm.PlottedCurves[0].ParameterName.Should().Be("gFx");
        vm.PlottedCurves[0].YAxisType.Should().Be("Y1");
    }

    [Fact]
    public void TrackPlottedCurve_NullParameter_DoesNotAdd()
    {
        var vm = new AddCurvesDialogViewModel(null, "time", 0);
        vm.SelectedParameter = null;
        vm.TrackPlottedCurve();
        vm.PlottedCurves.Should().BeEmpty();
    }

    [Fact]
    public void TrackPlottedCurve_NullYAxis_DoesNotAdd()
    {
        var table = MakeTable(("time", typeof(double)), ("gFx", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);
        vm.SelectedYAxis = null;
        vm.TrackPlottedCurve();
        vm.PlottedCurves.Should().BeEmpty();
    }

    [Fact]
    public void TrackPlottedCurve_MultipleTimes_AccumulatesAll()
    {
        var table = MakeTable(("time", typeof(double)), ("gFx", typeof(double)), ("gFy", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);

        vm.SelectedParameter = "gFx";
        vm.SelectedYAxis = vm.AvailableYAxes[0];
        vm.TrackPlottedCurve();

        vm.SelectedParameter = "gFy";
        vm.SelectedYAxis = vm.AvailableYAxes[1];
        vm.TrackPlottedCurve();

        vm.PlottedCurves.Should().HaveCount(2);
    }

    [Fact]
    public void SelectingParameter_AutoParsesUnitFromHeader()
    {
        var table = MakeTable(("time", typeof(double)), ("Altitude [ft]", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);

        vm.SelectedParameter = "Altitude [ft]";
        vm.UnitText.Should().Be("ft");
    }

    [Fact]
    public void SelectingParameter_WithNoUnitInHeader_ClearsUnit()
    {
        var table = MakeTable(("time", typeof(double)), ("Altitude [ft]", typeof(double)), ("plain", typeof(double)));
        var vm = new AddCurvesDialogViewModel(table, "time", 0);

        vm.SelectedParameter = "Altitude [ft]";
        vm.UnitText.Should().Be("ft");

        vm.SelectedParameter = "plain";
        vm.UnitText.Should().BeEmpty("a header with no recognizable unit clears the field");
    }
}
