using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

public class GroupedPlotViewModelTests
{
    private static PlotDataModel BuildData()
    {
        var t = new DataTable();
        t.Columns.Add("Weight", typeof(double));
        t.Columns.Add("DISA", typeof(double));
        t.Columns.Add("Time", typeof(double));
        t.Columns.Add("LevelOff", typeof(double));

        double[] weights = { 190000, 200000 };
        double[] disas = { -10, 0, 10 };
        foreach (var w in weights)
            foreach (var d in disas)
                for (int i = 0; i < 5; i++)
                    t.Rows.Add(w, d, (double)i, 30000.0 - i * 1000);
        return new PlotDataModel { Data = t };
    }

    private static GroupedPlotViewModel BuildVm(ApplicationSettings? settings = null)
    {
        settings ??= new ApplicationSettings();
        return new GroupedPlotViewModel(new GroupedDataIndexer(settings), settings);
    }

    [Fact]
    public void SetData_NoInputs_AvailableColumnsListsAll()
    {
        var vm = BuildVm();
        vm.SetData(BuildData());

        vm.AvailableXColumns.Should().Equal("Weight", "DISA", "Time", "LevelOff");
        // Y excludes the currently-selected X (which is null), so it equals X list.
        vm.AvailableYColumns.Should().Equal("Weight", "DISA", "Time", "LevelOff");
    }

    [Fact]
    public void ApplyConfig_WithInputs_ExcludesInputsFromAxisDropdowns()
    {
        var vm = BuildVm();
        vm.SetData(BuildData());
        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight", SelectedValue = 190000 },
                new() { ColumnName = "DISA",   SelectedValue = 0 },
            },
        };
        vm.ApplyConfig(cfg);

        vm.AvailableXColumns.Should().Equal("Time", "LevelOff");
        vm.AvailableYColumns.Should().Equal("LevelOff");
        vm.Inputs.Should().HaveCount(2);
    }

    [Fact]
    public void ChangingInputSelection_BumpsPlotVersionAndRebuildsSeries()
    {
        var vm = BuildVm();
        vm.SetData(BuildData());
        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight", SelectedValue = 190000 },
                new() { ColumnName = "DISA",   SelectedValue = 0 },
            },
        };
        vm.ApplyConfig(cfg);
        var initialVersion = vm.PlotVersion;
        vm.Series.Should().HaveCount(1);

        // Toggle DISA to "All" → should expand to 3 series.
        var disaInput = vm.Inputs.First(i => i.ColumnName == "DISA");
        disaInput.SelectedOption = disaInput.Options.First(o => o.IsAll);

        vm.PlotVersion.Should().BeGreaterThan(initialVersion);
        vm.Series.Should().HaveCount(3);
    }

    [Fact]
    public void SelectingXEqualToY_ClearsY()
    {
        var vm = BuildVm();
        vm.SetData(BuildData());
        vm.SelectedYColumn = "Time";
        vm.SelectedXColumn = "Time";

        vm.SelectedYColumn.Should().BeNull();
    }

    [Fact]
    public void TruncationFlag_SetWhenLineCountExceedsLimit()
    {
        var settings = new ApplicationSettings { GroupedPlotMaxLines = 2 };
        var vm = BuildVm(settings);
        vm.SetData(BuildData());
        vm.ApplyConfig(new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight" },  // All (2)
                new() { ColumnName = "DISA"   },  // All (3)  → 6 groups total
            },
        });

        vm.Series.Should().HaveCount(2);
        vm.TruncationWarningVisible.Should().BeTrue();
        vm.TruncationWarningText.Should().Contain("Showing first 2 of 6");
    }

    [Fact]
    public void Series_LabelUsesDisplayLabelAndFormat()
    {
        var vm = BuildVm();
        vm.SetData(BuildData());
        vm.ApplyConfig(new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight", SelectedValue = 190000 },
                new() { ColumnName = "DISA",   DisplayLabel = "Temp", Format = "F0", UnitSuffix = "°C" }, // All
            },
        });

        vm.Series.Select(s => s.Label).Should().Equal("Temp=-10°C", "Temp=0°C", "Temp=10°C");
    }

    // --- C1: selecting a lossily-formatted input value must still match its rows. ---

    private static PlotDataModel BuildFractionalData()
    {
        // Mach column has fractional distinct values that round when formatted with "0.0"
        // (0.75 → "0.8", 0.25 → "0.2"). Old code parsed the display string back, so 0.75 became
        // 0.8 and matched no row → blank plot.
        var t = new DataTable();
        t.Columns.Add("Mach", typeof(double));
        t.Columns.Add("Time", typeof(double));
        t.Columns.Add("Value", typeof(double));
        double[] machs = { 0.25, 0.75 };
        foreach (var m in machs)
            for (int i = 0; i < 3; i++)
                t.Rows.Add(m, (double)i, m * 100 + i);
        return new PlotDataModel { Data = t };
    }

    [Fact]
    public void SelectingLossilyFormattedValue_StillProducesSeries()
    {
        var vm = BuildVm();
        vm.SetData(BuildFractionalData());
        vm.ApplyConfig(new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "Value",
            Inputs = new()
            {
                new() { ColumnName = "Mach", Format = "0.0" }, // lossy: 0.75 → "0.8"
            },
        });

        var machInput = vm.Inputs.First(i => i.ColumnName == "Mach");
        // Pick the 0.75 option (displays "0.8" under the lossy format).
        var option = machInput.Options.First(o => o.Value is > 0.7 and < 0.8);
        machInput.SelectedOption = option;

        vm.Series.Should().HaveCount(1);              // not blank
        vm.Series[0].Y.Should().Equal(75.0, 76.0, 77.0);
    }

    [Fact]
    public void RestoringLossilyFormattedSelection_MatchesRawValue()
    {
        // Restore path: config carries the raw SelectedValue; the option match must find it even
        // though its display string is rounded.
        var vm = BuildVm();
        vm.SetData(BuildFractionalData());
        vm.ApplyConfig(new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "Value",
            Inputs = new()
            {
                new() { ColumnName = "Mach", Format = "0.0", SelectedValue = 0.75 },
            },
        });

        var machInput = vm.Inputs.First(i => i.ColumnName == "Mach");
        machInput.SelectedOption.Value.Should().Be(0.75);
        vm.Series.Should().HaveCount(1);
        vm.Series[0].Y.Should().Equal(75.0, 76.0, 77.0);
    }
}
