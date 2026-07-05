using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;
using System.Data;
using System.Globalization;

namespace DatPlotX.Tests.Services;

public class IntersectionCalculatorTests
{
    // --- InitializeIntersectionTable ---

    [Fact]
    public void InitializeIntersectionTable_AddsExpectedColumns()
    {
        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);

        table.Columns.Count.Should().Be(5);
        table.Columns["Event Line"].Should().NotBeNull();
        table.Columns["X Position"].Should().NotBeNull();
        table.Columns["Pane"].Should().NotBeNull();
        table.Columns["Curve"].Should().NotBeNull();
        table.Columns["Y Value"].Should().NotBeNull();
    }

    [Fact]
    public void InitializeIntersectionTable_CorrectColumnTypes()
    {
        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);

        table.Columns["Event Line"]!.DataType.Should().Be(typeof(string));
        table.Columns["X Position"]!.DataType.Should().Be(typeof(double));
        table.Columns["Pane"]!.DataType.Should().Be(typeof(string));
        table.Columns["Curve"]!.DataType.Should().Be(typeof(string));
        table.Columns["Y Value"]!.DataType.Should().Be(typeof(double));
    }

    // --- InterpolateYValue ---

    [Fact]
    public void InterpolateYValue_ExactIndex_ReturnsCorrectValue()
    {
        var data = new double[] { 10.0, 20.0, 30.0, 40.0, 50.0 };
        var result = IntersectionCalculator.InterpolateYValue(1.0, data, 2.0);
        result.Should().Be(30.0);
    }

    [Fact]
    public void InterpolateYValue_XBeforeStart_ClampsToFirst()
    {
        var data = new double[] { 5.0, 10.0, 15.0 };
        var result = IntersectionCalculator.InterpolateYValue(1.0, data, -5.0);
        result.Should().Be(5.0);
    }

    [Fact]
    public void InterpolateYValue_XBeyondEnd_ClampsToLast()
    {
        var data = new double[] { 5.0, 10.0, 15.0 };
        var result = IntersectionCalculator.InterpolateYValue(1.0, data, 1000.0);
        result.Should().Be(15.0);
    }

    [Fact]
    public void InterpolateYValue_ZeroXPosition_ReturnsFirstElement()
    {
        var data = new double[] { 42.0, 99.0 };
        var result = IntersectionCalculator.InterpolateYValue(0.01, data, 0.0);
        result.Should().Be(42.0);
    }

    [Fact]
    public void InterpolateYValue_SingleElement_AlwaysReturnsThatElement()
    {
        var data = new double[] { 7.5 };
        IntersectionCalculator.InterpolateYValue(1.0, data, 0.0).Should().Be(7.5);
        IntersectionCalculator.InterpolateYValue(1.0, data, 999.0).Should().Be(7.5);
    }

    [Fact]
    public void InterpolateYValue_SamplePeriod_CorrectIndexCalculation()
    {
        // period=0.01, data has 100 points. xPosition=0.5 → index 50
        var data = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();
        var result = IntersectionCalculator.InterpolateYValue(0.01, data, 0.5);
        result.Should().Be(50.0);
    }

    // --- CalculateAndPopulateIntersections ---

    [Fact]
    public void CalculateAndPopulateIntersections_EmptyPanes_EmptyTable()
    {
        var calc = new IntersectionCalculator();
        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);

        calc.CalculateAndPopulateIntersections([], table);

        table.Rows.Count.Should().Be(0);
    }

    [Fact]
    public void CalculateAndPopulateIntersections_ClearsTableFirst()
    {
        var calc = new IntersectionCalculator();
        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);
        var row = table.NewRow();
        row["Event Line"] = "old";
        row["X Position"] = 0.0;
        row["Pane"] = "Pane 1";
        row["Curve"] = "old";
        row["Y Value"] = 0.0;
        table.Rows.Add(row);

        calc.CalculateAndPopulateIntersections([], table);

        table.Rows.Count.Should().Be(0);
    }

    private static PlotPaneViewModel PaneWithCurveAndEventLine(int index, string curveName, double eventX)
    {
        var pane = new PlotPaneViewModel(new PlotPaneModel { Index = index })
        {
            PlotModel = new Plot()
        };
        pane.AddScatterCurve(
            [0.0, 1.0, 2.0, 3.0, 4.0],
            [10.0, 20.0, 30.0, 40.0, 50.0],
            new CurveConfigurationModel
            {
                CurveName = curveName,
                YColumnName = curveName,
                PaneIndex = index,
                YAxis = YAxisType.Y1,
                Color = "#FF0000",
                IsVisible = true
            });
        pane.AutoScale();
        pane.AddEventLine(eventX, $"evt@{eventX}", "#000000");
        return pane;
    }

    [Fact]
    public void CalculateAndPopulateIntersections_WithRealPanes_PopulatesRows()
    {
        var calc = new IntersectionCalculator();
        var panes = new[] { PaneWithCurveAndEventLine(0, "altitude", 2.0) };
        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);

        calc.CalculateAndPopulateIntersections(panes, table);

        table.Rows.Count.Should().Be(1);
        table.Rows[0]["Curve"].Should().Be("altitude");
        Convert.ToDouble(table.Rows[0]["X Position"], CultureInfo.InvariantCulture).Should().Be(2.0);
    }

    [Fact]
    public void CalculateAndPopulateIntersections_MultiplePanesMultipleEventLines_ProducesCartesianProduct()
    {
        var calc = new IntersectionCalculator();
        var pane0 = PaneWithCurveAndEventLine(0, "alt", 1.0);
        pane0.AddEventLine(3.0, "evt@3", "#000000");
        var pane1 = PaneWithCurveAndEventLine(1, "vspeed", 1.0);

        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);

        calc.CalculateAndPopulateIntersections([pane0, pane1], table);

        // pane0 has 2 event lines × 1 curve = 2 rows; pane1 has 1 × 1 = 1 row
        table.Rows.Count.Should().Be(3);
    }

    [Fact]
    public void CalculateAndPopulateIntersections_SortedByEventLineThenPane()
    {
        var calc = new IntersectionCalculator();
        // Pane 1 added before pane 0 in order to verify sort on PaneIndex
        var pane0 = PaneWithCurveAndEventLine(0, "a", 1.0);
        var pane1 = PaneWithCurveAndEventLine(1, "b", 1.0);

        var table = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(table);

        calc.CalculateAndPopulateIntersections([pane1, pane0], table);

        table.Rows.Count.Should().Be(2);
        // Sort is by event-line label first; both have identical auto labels, so pane index breaks ties.
        table.Rows[0]["Pane"].ToString().Should().Contain("1");
        table.Rows[1]["Pane"].ToString().Should().Contain("2");
    }
}
