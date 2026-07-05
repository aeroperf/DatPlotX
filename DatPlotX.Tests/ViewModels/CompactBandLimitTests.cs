using System.Data;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;

namespace DatPlotX.Tests.ViewModels;

/// <summary>
/// Regression: importing a wide source (e.g. X-Plane, ~70 columns) and adding a band per column
/// used to stack enough banded Y axes to collapse the OxyPlot plot area to a degenerate size,
/// crashing Avalonia's layout pass ("Invalid size returned for Measure"). The Compact surface now
/// caps the number of rendered bands at <see cref="CompactPlotViewModel.MaxVisibleBands"/>.
/// </summary>
public class CompactBandLimitTests
{
    private static PlotDataModel WideData(int columns, int rows)
    {
        var t = new DataTable();
        for (int c = 0; c < columns; c++) t.Columns.Add($"col{c}", typeof(double));
        for (int r = 0; r < rows; r++)
        {
            var row = t.NewRow();
            for (int c = 0; c < columns; c++) row[c] = r + c;   // finite, distinct per band
            t.Rows.Add(row);
        }
        return new PlotDataModel { Data = t };
    }

    private static List<CompactCurveModel> CurvesForAllButX(DataTable t) =>
        Enumerable.Range(1, t.Columns.Count - 1)
            .Select(i => new CompactCurveModel
            {
                SourceColumn = t.Columns[i].ColumnName,
                DisplayName = t.Columns[i].ColumnName,
                Color = "#1f77b4",
                IsVisible = true,
            })
            .ToList();

    [Fact]
    public void ManyCurves_ClampsRenderedBands_AndRaisesWarning()
    {
        var data = WideData(columns: 72, rows: 200);   // mirrors the X-Plane snippet shape
        var vm = new CompactPlotViewModel();

        (int Shown, int Total)? warned = null;
        vm.BandLimitExceeded += (_, e) => warned = e;

        vm.SetData(data, data.Data!.Columns[0].ColumnName);
        vm.ReplaceCurves(CurvesForAllButX(data.Data!));

        // Banded Y axes are capped (1 shared X axis + at most MaxVisibleBands Y axes).
        int bandedYAxes = vm.PlotModel.Axes.Count(a => a.Key?.StartsWith("__compact_y_", StringComparison.Ordinal) == true);
        bandedYAxes.Should().Be(CompactPlotViewModel.MaxVisibleBands);

        warned.Should().NotBeNull();
        warned!.Value.Shown.Should().Be(CompactPlotViewModel.MaxVisibleBands);
        warned.Value.Total.Should().Be(71);

        // All curves remain in the model (only rendering is clamped, not the data).
        vm.Curves.Count.Should().Be(71);
    }

    [Fact]
    public void CurvesWithinLimit_RenderAll_NoWarning()
    {
        var data = WideData(columns: 6, rows: 50);   // 5 curves, under the cap
        var vm = new CompactPlotViewModel();

        bool warned = false;
        vm.BandLimitExceeded += (_, _) => warned = true;

        vm.SetData(data, data.Data!.Columns[0].ColumnName);
        vm.ReplaceCurves(CurvesForAllButX(data.Data!));

        vm.PlotModel.Axes.Count(a => a.Key?.StartsWith("__compact_y_", StringComparison.Ordinal) == true).Should().Be(5);
        warned.Should().BeFalse();
    }

    [Fact]
    public void DataSetButNoCurves_ModelHasYAxis_SoOxyPlotCanComputePlotArea()
    {
        // Repro: Stacked project -> New Compact project -> import. SetData runs with data + an X
        // column but zero curves. A model with only an X axis measures to a degenerate / NaN size
        // in OxyPlot once the PlotView is realized, crashing Avalonia's layout pass. The empty
        // surface must carry a Y axis so the plot area is well-defined.
        var data = WideData(columns: 6, rows: 50);
        var vm = new CompactPlotViewModel();

        vm.SetData(data, data.Data!.Columns[0].ColumnName);   // no ReplaceCurves -> no bands

        vm.PlotModel.Series.Should().BeEmpty();
        vm.PlotModel.Axes.Any(a => a.Position == OxyPlot.Axes.AxisPosition.Left
            || a.Position == OxyPlot.Axes.AxisPosition.Right)
            .Should().BeTrue("an empty Compact surface still needs a Y axis or OxyPlot can't size the plot area");
    }
}
