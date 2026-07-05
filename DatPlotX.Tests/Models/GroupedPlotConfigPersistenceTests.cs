using DatPlotX.Models;
using DatPlotX.Services;
using FluentAssertions;

namespace DatPlotX.Tests.Models;

public class GroupedPlotConfigPersistenceTests
{
    [Fact]
    public void GroupedPlot_RoundTripsThroughProjectSerializer()
    {
        var project = new ProjectSettingsModel
        {
            ProjectName = "Test",
            PlotMode = PlotMode.Grouped,
            GroupedPlot = new GroupedPlotConfig
            {
                XAxisColumn = "Time",
                YAxisColumn = "LevelOff",
                ShowLegend = true,
                ShowMarkers = false,
                Title = "Custom Title",
                XAxisMin = -1,
                XAxisMax = 15,
                YAxisMin = 0,
                YAxisMax = 50000,
                Inputs = new()
                {
                    new() { ColumnName = "Weight",   DisplayLabel = "W",    UnitSuffix = " lbs", Format = "N0", SelectedValue = 190000 },
                    new() { ColumnName = "DISA",     DisplayLabel = "Temp", UnitSuffix = "°C",   Format = "F0", SelectedValue = null },
                    new() { ColumnName = "StartAlt" }, // defaults
                },
            },
        };

        var serializer = new ProjectSerializer();
        var json = serializer.SerializeToJson(project);
        var loaded = serializer.DeserializeFromJson(json);

        loaded.PlotMode.Should().Be(PlotMode.Grouped);
        loaded.GroupedPlot.Should().NotBeNull();
        loaded.GroupedPlot!.XAxisColumn.Should().Be("Time");
        loaded.GroupedPlot.YAxisColumn.Should().Be("LevelOff");
        loaded.GroupedPlot.ShowLegend.Should().BeTrue();
        loaded.GroupedPlot.ShowMarkers.Should().BeFalse();
        loaded.GroupedPlot.Title.Should().Be("Custom Title");
        loaded.GroupedPlot.XAxisMin.Should().Be(-1);
        loaded.GroupedPlot.YAxisMax.Should().Be(50000);
        loaded.GroupedPlot.Inputs.Should().HaveCount(3);

        loaded.GroupedPlot.Inputs[0].ColumnName.Should().Be("Weight");
        loaded.GroupedPlot.Inputs[0].DisplayLabel.Should().Be("W");
        loaded.GroupedPlot.Inputs[0].UnitSuffix.Should().Be(" lbs");
        loaded.GroupedPlot.Inputs[0].Format.Should().Be("N0");
        loaded.GroupedPlot.Inputs[0].SelectedValue.Should().Be(190000);

        loaded.GroupedPlot.Inputs[1].SelectedValue.Should().BeNull();
        loaded.GroupedPlot.Inputs[2].ColumnName.Should().Be("StartAlt");
        loaded.GroupedPlot.Inputs[2].SelectedValue.Should().BeNull();
    }

    [Fact]
    public void GroupedPlot_NullWhenNonGroupedProjectSerialized()
    {
        var project = new ProjectSettingsModel { PlotMode = PlotMode.Panes };
        var serializer = new ProjectSerializer();
        var loaded = serializer.DeserializeFromJson(serializer.SerializeToJson(project));
        loaded.GroupedPlot.Should().BeNull();
    }
}
