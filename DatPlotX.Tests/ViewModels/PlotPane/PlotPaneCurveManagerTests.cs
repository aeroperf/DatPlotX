using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;

namespace DatPlotX.Tests.ViewModels.PlotPane;

public class PlotPaneCurveManagerTests
{
    private static PlotPaneViewModel MakePane(int index = 0)
    {
        return new PlotPaneViewModel(new PlotPaneModel { Index = index }) { PlotModel = new Plot() };
    }

    private static CurveConfigurationModel MakeConfig(string name, YAxisType yAxis = YAxisType.Y1)
    {
        return new CurveConfigurationModel
        {
            CurveName = name,
            YColumnName = name,
            PaneIndex = 0,
            YAxis = yAxis,
            Color = "#FF0000",
            IsVisible = true
        };
    }

    [Fact]
    public void AddScatterCurve_UsesConfigId()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.GetCurveConfig(config.Id).Should().NotBeNull();
    }

    [Fact]
    public void RemoveCurve_KnownId_ReturnsTrue()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.RemoveCurve(config.Id).Should().BeTrue();
        pane.GetAllCurveConfigs().Should().BeEmpty();
    }

    [Fact]
    public void RemoveCurve_UnknownId_ReturnsFalse()
    {
        var pane = MakePane();
        pane.RemoveCurve(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void ToggleCurveVisibility_FlipsIsVisible()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.ToggleCurveVisibility(config.Id).Should().BeTrue();
        pane.GetCurveConfig(config.Id)!.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void SetCurveVisibility_NoChange_KeepsValue()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.SetCurveVisibility(config.Id, true).Should().BeTrue();
        pane.GetCurveConfig(config.Id)!.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void SetCurveVisibility_ChangesValue_UpdatesConfig()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.SetCurveVisibility(config.Id, false).Should().BeTrue();
        pane.GetCurveConfig(config.Id)!.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void SetCurveVisibility_Idempotent_AppliedTwiceStaysSet()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.SetCurveVisibility(config.Id, false);
        pane.SetCurveVisibility(config.Id, false);
        pane.GetCurveConfig(config.Id)!.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void SetCurveVisibility_UnknownId_ReturnsFalse()
    {
        var pane = MakePane();
        pane.SetCurveVisibility(Guid.NewGuid(), true).Should().BeFalse();
    }

    [Fact]
    public void UpdateCurveFormat_UpdatesColor()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);

        var updated = new CurveConfigurationModel
        {
            Id = config.Id,
            CurveName = "c",
            YColumnName = "c",
            PaneIndex = 0,
            YAxis = YAxisType.Y1,
            Color = "#00FF00",
            IsVisible = true
        };
        pane.UpdateCurveFormat(updated).Should().BeTrue();
        pane.GetCurveConfig(config.Id)!.Color.Should().Be("#00FF00");
    }

    [Fact]
    public void GetCurveYValueAtX_ReturnsInterpolatedValue()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0, 2.0], [0.0, 10.0, 20.0], config);
        // Point X=0.5 bracketed by (0,0) and (1,10) → should interpolate to 5.0
        pane.GetCurveYValueAtX(config.Id, 0.5).Should().BeApproximately(5.0, 1e-9);
    }

    [Fact]
    public void GetCurveValuesAtX_ReturnsOnlyVisibleCurves()
    {
        var pane = MakePane();
        var c1 = MakeConfig("c1");
        var c2 = MakeConfig("c2");
        pane.AddScatterCurve([0.0, 1.0, 2.0], [0.0, 10.0, 20.0], c1);
        pane.AddScatterCurve([0.0, 1.0, 2.0], [0.0, 20.0, 40.0], c2);
        pane.ToggleCurveVisibility(c2.Id); // hide c2

        var results = pane.GetCurveValuesAtX(1.0);
        results.Should().HaveCount(1);
        results[0].Config.CurveName.Should().Be("c1");
    }

    [Fact]
    public void GetAllCurveConfigs_ReturnsAllCurves()
    {
        var pane = MakePane();
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], MakeConfig("c1"));
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], MakeConfig("c2"));
        pane.GetAllCurveConfigs().Should().HaveCount(2);
    }

    [Fact]
    public void GetClosestCurveAt_ReturnsNull_WhenNoCurves()
    {
        var pane = MakePane();
        pane.GetClosestCurveAt(0.0, 0.0, 0.0).Should().BeNull();
    }

    [Fact]
    public void AddScatterCurve_Y2Axis_AssignsRightAxis()
    {
        var pane = MakePane();
        var config = MakeConfig("c", YAxisType.Y2);
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);
        pane.GetCurveConfig(config.Id)!.YAxis.Should().Be(YAxisType.Y2);
    }

    // P1: H1 fix uses Set semantics — the call must set, not flip. Calling SetCurveVisibility
    // with the same value twice must leave the curve in that state (config + plottable agree).
    [Fact]
    public void SetCurveVisibility_Idempotent_DoesNotFlipOnSecondCall()
    {
        var pane = MakePane();
        var config = MakeConfig("c");
        pane.AddScatterCurve([0.0, 1.0], [1.0, 2.0], config);

        pane.SetCurveVisibility(config.Id, false).Should().BeTrue();
        pane.SetCurveVisibility(config.Id, false).Should().BeTrue();

        pane.GetCurveConfig(config.Id)!.IsVisible.Should().BeFalse();

        // The underlying plottable's IsVisible must mirror the config — this guards against a
        // future "Toggle" refactor that would flip the plottable back to visible on the second call.
        // Pane internally constructs a Signal for double-period X data; GetCurves only returns those.
        // For scatter-axis paths the assertion via config is the canonical check (line above).
        // We still assert the Signal-path mirror when present.
        var signals = pane.GetCurves();
        if (signals.Count > 0)
            signals[0].Signal.IsVisible.Should().BeFalse();
    }
}
