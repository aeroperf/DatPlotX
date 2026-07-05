using System.Collections.ObjectModel;
using DatPlotX.Models;
using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;
using ScottPlot.Plottables;

namespace DatPlotX.Tests.Services.Analysis;

/// <summary>
/// Verifies the Stacked overlay host actually adds a drawable line (and label) to the owning
/// pane's plot for both Y1 and Y2 curves — the regression that made slope/mean/min/max lines
/// fail to appear in Stacked mode while working in Compact.
/// </summary>
public class StackedAnalysisOverlayHostTests
{
    private static PlotPaneViewModel MakePane() =>
        new(new PlotPaneModel { Index = 0 }) { PlotModel = new Plot() };

    private static CurveConfigurationModel AddCurve(PlotPaneViewModel pane, YAxisType yAxis)
    {
        var cfg = new CurveConfigurationModel
        {
            CurveName = "c",
            YColumnName = "c",
            PaneIndex = 0,
            YAxis = yAxis,
            Color = "#FF0000",
            IsVisible = true,
        };
        pane.AddScatterCurve([0.0, 1.0, 2.0], [1.0, 2.0, 3.0], cfg);
        return cfg;
    }

    private static int LineCount(Plot p) => p.GetPlottables().OfType<LinePlot>().Count();
    private static int TextCount(Plot p) => p.GetPlottables().OfType<Text>().Count();

    [Theory]
    [InlineData(YAxisType.Y1)]
    [InlineData(YAxisType.Y2)]
    public void DrawSegmentLine_AddsLineAndLabel_ForBothAxes(YAxisType yAxis)
    {
        var pane = MakePane();
        var cfg = AddCurve(pane, yAxis);
        var panes = new ObservableCollection<PlotPaneViewModel> { pane };
        var host = new StackedAnalysisOverlayHost(panes);

        int linesBefore = LineCount(pane.PlotModel!);

        var line = MetricLine.Between(0, 1, 2, 3);
        host.DrawSegmentLine(cfg.Id.ToString(), "lid", line, double.NaN, double.NaN, "#CC000000", "Slope -0.342");

        LineCount(pane.PlotModel!).Should().Be(linesBefore + 1, "the overlay must add one stat line");
        TextCount(pane.PlotModel!).Should().BeGreaterThan(0, "a non-empty label must be drawn");
    }

    [Fact]
    public void DrawSegmentLine_BlackHex_RendersOpaque_NotTransparent()
    {
        // Regression: ScottPlot's Color.FromHex reads 8-digit hex as #RRGGBBAA, so "#CC000000"
        // parsed to a TRANSPARENT colour (alpha 0) and the line vanished. A 6-digit "#000000"
        // must yield an opaque black line.
        var pane = MakePane();
        var cfg = AddCurve(pane, YAxisType.Y2);
        var host = new StackedAnalysisOverlayHost(new ObservableCollection<PlotPaneViewModel> { pane });

        host.DrawSegmentLine(cfg.Id.ToString(), "lid", MetricLine.Between(0, 1, 2, 3),
            double.NaN, double.NaN, "#000000", "Slope 0.012");

        var line = pane.PlotModel!.GetPlottables().OfType<LinePlot>().Last();
        line.LineColor.Alpha.Should().Be(255, "the stat line must be fully opaque, not transparent");
        line.LineColor.Red.Should().Be(0);
        line.LineColor.Green.Should().Be(0);
        line.LineColor.Blue.Should().Be(0);
    }

    [Fact]
    public void DrawSegmentLine_EmptyLabel_DrawsLineButNoText()
    {
        var pane = MakePane();
        var cfg = AddCurve(pane, YAxisType.Y2);
        var host = new StackedAnalysisOverlayHost(new ObservableCollection<PlotPaneViewModel> { pane });

        int linesBefore = LineCount(pane.PlotModel!);
        int textBefore = TextCount(pane.PlotModel!);

        host.DrawSegmentLine(cfg.Id.ToString(), "lid", MetricLine.Between(0, 1, 2, 3),
            double.NaN, double.NaN, "#CC000000", "");

        LineCount(pane.PlotModel!).Should().Be(linesBefore + 1);
        TextCount(pane.PlotModel!).Should().Be(textBefore, "Line-only mode draws no label");
    }

    [Fact]
    public void DrawSegmentLine_HorizontalLine_NeedsSegmentSpan()
    {
        // Mean/Min/Max lines are Horizontal: they require a finite segment X span. With NaN span
        // (the whole-curve / no-active-segment case) they currently draw nothing — documents the
        // bug so the fix has a guardrail.
        var pane = MakePane();
        var cfg = AddCurve(pane, YAxisType.Y1);
        var host = new StackedAnalysisOverlayHost(new ObservableCollection<PlotPaneViewModel> { pane });

        int linesBefore = LineCount(pane.PlotModel!);
        host.DrawSegmentLine(cfg.Id.ToString(), "lid", MetricLine.Horizontal(2.0),
            double.NaN, double.NaN, "#CC000000", "Mean 2");

        LineCount(pane.PlotModel!).Should().Be(linesBefore,
            "a horizontal line with no finite segment span cannot be drawn");
    }
}
