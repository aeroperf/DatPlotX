using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.Tests.Services;

public class CurveCoordinationServiceTests
{
    private static readonly string[] Palette =
        ["#0000FF", "#FF00FF", "#00FF00", "#FF0000", "#000000"];

    private static PlotPaneViewModel Pane(int index = 0)
    {
        var pane = new PlotPaneViewModel(new PlotPaneModel { Index = index })
        {
            PlotModel = new Plot()
        };
        return pane;
    }

    private static PlotDataModel MakeData()
    {
        var table = new DataTable();
        table.Columns.Add("time", typeof(double));
        table.Columns.Add("gFx", typeof(double));
        table.Columns.Add("gFy", typeof(double));
        // gFz: tight sub-unit range (~0.66..1.40) — the LIS_TO Y2 case.
        table.Columns.Add("gFz", typeof(double));
        for (int i = 0; i < 10; i++)
            table.Rows.Add(i * 0.1, i * 1.0, i * -1.0, 0.66 + i * 0.082);
        return new PlotDataModel { Data = table };
    }

    [Fact]
    public void PlotSingleCurveToPane_AddsCurveAndTracksIt()
    {
        var service = new CurveCoordinationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane() };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);

        active.Should().HaveCount(1);
        active[0].YColumnName.Should().Be("gFx");
        active[0].YAxis.Should().Be(YAxisType.Y1);
        panes[0].GetAllCurveConfigs().Should().HaveCount(1);
    }

    [Fact]
    public void PlotSingleCurveToPane_UnknownPaneIndex_DoesNothing()
    {
        var service = new CurveCoordinationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane() };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(5, "gFx", "Y1", data, "time", panes, active, Palette);
        active.Should().BeEmpty();
    }

    [Fact]
    public void PlotSingleCurveToPane_InvalidYAxisString_DefaultsToY1()
    {
        var service = new CurveCoordinationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane() };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(0, "gFx", "NOT_AN_AXIS", data, "time", panes, active, Palette);
        active[0].YAxis.Should().Be(YAxisType.Y1);
    }

    [Fact]
    public void PlotSingleCurveToPane_ColorCyclesAcrossPalette()
    {
        var service = new CurveCoordinationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane() };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);
        service.PlotSingleCurveToPane(0, "gFy", "Y1", data, "time", panes, active, Palette);

        active[0].Color.Should().Be("#0000FF");
        active[1].Color.Should().Be("#FF00FF");
    }

    [Fact]
    public void ReplotAllCurves_NoData_ReturnsFalse()
    {
        var service = new CurveCoordinationService();
        var result = service.ReplotAllCurves(null!, "time",
            new ObservableCollection<PlotPaneViewModel>(),
            new ObservableCollection<CurveConfigurationModel>());
        result.Should().BeFalse();
    }

    [Fact]
    public void ReplotAllCurves_NoCurves_ReturnsFalse()
    {
        var service = new CurveCoordinationService();
        var result = service.ReplotAllCurves(MakeData(), "time",
            new ObservableCollection<PlotPaneViewModel>(),
            new ObservableCollection<CurveConfigurationModel>());
        result.Should().BeFalse();
    }

    [Fact]
    public void ReplotAllCurves_BatchesPlotUpdates_FiresOncePerPane()
    {
        var service = new CurveCoordinationService();
        var pane = Pane();
        int updates = 0;
        pane.OnPlotUpdated += () => updates++;

        var panes = new ObservableCollection<PlotPaneViewModel> { pane };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);
        service.PlotSingleCurveToPane(0, "gFy", "Y1", data, "time", panes, active, Palette);

        int beforeReplot = updates;
        var result = service.ReplotAllCurves(data, "time", panes, active);

        result.Should().BeTrue();
        int afterReplot = updates - beforeReplot;
        // Two curves would otherwise produce many updates (remove+add+autoscale each).
        // Batching should collapse to a single per-pane notification.
        afterReplot.Should().Be(1);
    }

    [Fact]
    public void ReplotAllCurves_SwappingXColumn_UsesNewValues()
    {
        var service = new CurveCoordinationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane() };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);
        // Replot with gFy as X column
        var ok = service.ReplotAllCurves(data, "gFy", panes, active);
        ok.Should().BeTrue();
        panes[0].GetAllCurveConfigs().Should().HaveCount(1);
    }

    [Fact]
    public void ApplySmartDecimalDefaults_SetsDecimalPlacesOnPaneModel()
    {
        var service = new CurveCoordinationService();
        var pane = Pane();
        var data = MakeData();

        var panes = new ObservableCollection<PlotPaneViewModel> { pane };
        var active = new ObservableCollection<CurveConfigurationModel>();
        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);

        // Smart decimals get applied during PlotSingleCurveToPane when this is the first curve.
        // Decimals now come from the axis's real generated ticks, chosen as the fewest places
        // that keep every adjacent tick label distinct. Assert that invariant directly rather
        // than a magic count (the chosen step depends on the rendered tick density).
        pane.PlotModel!.RenderInMemory(800, 600);
        AssertAdjacentTickLabelsDistinct(
            pane.PlotModel.Axes.Bottom, pane.PaneModel.XAxisDecimalPlaces);
        AssertAdjacentTickLabelsDistinct(
            pane.PlotModel.Axes.Left, pane.PaneModel.Y1AxisDecimalPlaces);
    }

    [Fact]
    public void PlotSingleCurveToPane_Y2CurveAfterY1_GetsSmartDecimals()
    {
        // Regression: smart-defaults only ran for the first (Y1) curve, when the Y2 axis had
        // no data — so a Y2 curve added afterwards kept 0 decimals and its ticks collapsed to
        // integers (the LIS_TO gFz screenshot). Now the first Y2 curve recomputes Y2 decimals.
        var service = new CurveCoordinationService();
        var pane = Pane();
        var data = MakeData();

        var panes = new ObservableCollection<PlotPaneViewModel> { pane };
        var active = new ObservableCollection<CurveConfigurationModel>();

        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);
        pane.PaneModel.Y2AxisDecimalPlaces.Should().Be(0); // no Y2 curve yet

        service.PlotSingleCurveToPane(0, "gFz", "Y2", data, "time", panes, active, Palette);

        // gFz spans ~0.66..1.40. The fix derives decimals from the real generated ticks so no
        // two adjacent Y2 labels collide — the screenshot showed "1.1, 1.1, 1.0, 1.0" at the
        // earlier 1-decimal guess (a 0.05 tick step needs 2).
        pane.PaneModel.ShowY2Axis.Should().BeTrue();
        pane.PaneModel.Y2AxisDecimalPlaces.Should().BeGreaterThan(0);

        // Render at a real size and confirm every adjacent major Y2 tick formats distinctly.
        pane.PlotModel!.RenderInMemory(800, 600);
        AssertAdjacentTickLabelsDistinct(
            pane.PlotModel.Axes.Right, pane.PaneModel.Y2AxisDecimalPlaces);
    }

    /// <summary>Fail if any two adjacent major-tick labels collide at the given precision.</summary>
    private static void AssertAdjacentTickLabelsDistinct(ScottPlot.IAxis axis, int decimals)
    {
        var labels = axis.TickGenerator!.Ticks
            .Where(t => t.IsMajor)
            .OrderBy(t => t.Position)
            .Select(t => t.Position.ToString("F" + decimals,
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        for (int i = 1; i < labels.Length; i++)
            labels[i].Should().NotBe(labels[i - 1], "adjacent tick labels must differ");
    }

    [Fact]
    public void ApplySmartDecimalDefaultsWithSync_PropagatesXDecimals()
    {
        var service = new CurveCoordinationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0), Pane(1) };
        var active = new ObservableCollection<CurveConfigurationModel>();
        var data = MakeData();

        service.PlotSingleCurveToPane(0, "gFx", "Y1", data, "time", panes, active, Palette);

        panes[0].PaneModel.XAxisDecimalPlaces.Should().Be(panes[1].PaneModel.XAxisDecimalPlaces);
    }
}
