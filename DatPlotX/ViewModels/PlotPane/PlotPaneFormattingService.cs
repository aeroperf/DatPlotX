using DatPlotX.Models;
using ScottPlot;
using System.Globalization;

namespace DatPlotX.ViewModels.PlotPane;

/// <summary>
/// Service for applying visual formatting to plot panes.
/// Handles axes, grid, colors, fonts, and tick formatting.
/// </summary>
public class PlotPaneFormattingService : IPlotPaneFormattingService
{
    private readonly Func<Plot?> _getPlot;
    private readonly Func<PlotPaneModel> _getModel;
    private readonly Action _triggerUpdate;

    /// <summary>
    /// Constructor with dependencies
    /// </summary>
    /// <param name="getPlot">Function to get current Plot reference (deferred access)</param>
    /// <param name="getModel">Function to get current PlotPaneModel reference</param>
    /// <param name="triggerUpdate">Callback to trigger plot update event</param>
    public PlotPaneFormattingService(
        Func<Plot?> getPlot,
        Func<PlotPaneModel> getModel,
        Action triggerUpdate)
    {
        _getPlot = getPlot;
        _getModel = getModel;
        _triggerUpdate = triggerUpdate;
    }

    /// <inheritdoc />
    public void ApplyFormatting()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        var paneModel = _getModel();

        // Capture current axis ranges so we can restore them — formatting must never reset user zoom.
        // Only override if the user has explicitly set manual min/max in the model.
        var currentXMin = plotModel.Axes.Bottom.Range.Min;
        var currentXMax = plotModel.Axes.Bottom.Range.Max;
        var currentYMin = plotModel.Axes.Left.Range.Min;
        var currentYMax = plotModel.Axes.Left.Range.Max;
        var currentY2Min = plotModel.Axes.Right.Range.Min;
        var currentY2Max = plotModel.Axes.Right.Range.Max;

        // Apply axis labels — suppress X label on non-bottom panes
        plotModel.Axes.Bottom.Label.Text = paneModel.ShowXAxisLabels ? paneModel.XAxisLabel : "";
        plotModel.Axes.Left.Label.Text = paneModel.YAxisLabel;
        plotModel.Axes.Right.Label.Text = paneModel.Y2AxisLabel;

        // Apply label font settings
        plotModel.Axes.Bottom.Label.FontSize = (float)paneModel.AxisLabelFontSize;
        plotModel.Axes.Left.Label.FontSize = (float)paneModel.AxisLabelFontSize;
        plotModel.Axes.Right.Label.FontSize = (float)paneModel.AxisLabelFontSize;

        plotModel.Axes.Bottom.Label.Bold = paneModel.AxisLabelBold;
        plotModel.Axes.Left.Label.Bold = paneModel.AxisLabelBold;
        plotModel.Axes.Right.Label.Bold = paneModel.AxisLabelBold;

        // Apply title formatting. Set both Text and IsVisible explicitly so the title panel
        // renders consistently even after repeated ApplyFormatting calls or pane reindexing.
        bool hasTitle = !string.IsNullOrWhiteSpace(paneModel.TitleText);
        plotModel.Axes.Title.Label.Text = paneModel.TitleText ?? "";
        plotModel.Axes.Title.IsVisible = hasTitle;
        plotModel.Axes.Title.Label.IsVisible = hasTitle;
        plotModel.Axes.Title.Label.FontSize = (float)paneModel.TitleFontSize;
        plotModel.Axes.Title.Label.Bold = paneModel.TitleFontStyle == "Bold";
        plotModel.Axes.Title.Label.Italic = paneModel.TitleFontStyle == "Italic";
        // Clamp title panel height tight around the glyph to minimize whitespace above data.
        // With 24pt title, ~36px fits comfortably; 0 when no title.
        plotModel.Axes.Title.MinimumSize = 0;
        plotModel.Axes.Title.MaximumSize = hasTitle ? (float)(paneModel.TitleFontSize * 1.5) : 0;

        // Apply Y2 axis visibility
        plotModel.Axes.Right.IsVisible = paneModel.ShowY2Axis;

        // Apply explicit manual axis ranges if set; otherwise restore the captured ranges.
        // Never call AutoScale here — that would destroy user zoom state.
        if (paneModel.XAxisMin.HasValue && paneModel.XAxisMax.HasValue)
            plotModel.Axes.Bottom.Range.Set(paneModel.XAxisMin.Value, paneModel.XAxisMax.Value);
        else
            plotModel.Axes.Bottom.Range.Set(currentXMin, currentXMax);

        if (paneModel.YAxisMin.HasValue && paneModel.YAxisMax.HasValue)
            plotModel.Axes.Left.Range.Set(paneModel.YAxisMin.Value, paneModel.YAxisMax.Value);
        else
            plotModel.Axes.Left.Range.Set(currentYMin, currentYMax);

        if (paneModel.Y2AxisMin.HasValue && paneModel.Y2AxisMax.HasValue)
            plotModel.Axes.Right.Range.Set(paneModel.Y2AxisMin.Value, paneModel.Y2AxisMax.Value);
        else
            plotModel.Axes.Right.Range.Set(currentY2Min, currentY2Max);

        // Apply grid settings
        plotModel.Grid.MajorLineColor = ScottPlot.Color.FromHex(paneModel.GridColor);
        plotModel.Grid.MajorLineWidth = paneModel.ShowMajorGrid ? (float)paneModel.GridLineWidth : 0f;

        var gridLineStyle = paneModel.GridLineStyle switch
        {
            "Dashed" => ScottPlot.LinePattern.Dashed,
            "Dotted" => ScottPlot.LinePattern.Dotted,
            _ => ScottPlot.LinePattern.Solid
        };
        plotModel.Grid.MajorLinePattern = gridLineStyle;

        // Minor gridlines: ScottPlot draws them only when MinorLineWidth > 0. DefaultGrid has no
        // separate minor-pattern setter, so we control visibility purely via width.
        plotModel.Grid.MinorLineColor = ScottPlot.Color.FromHex(paneModel.GridColor).WithAlpha(0.5);
        plotModel.Grid.MinorLineWidth = paneModel.ShowMinorGrid ? Math.Max(0.5f, (float)paneModel.GridLineWidth * 0.5f) : 0f;

        // Keep the grid layer visible whenever either tier is on.
        plotModel.Grid.IsVisible = paneModel.ShowMajorGrid || paneModel.ShowMinorGrid;

        // Apply X-axis tick formatting. Always use NumericAutomatic so vertical gridlines render.
        // On non-bottom panes, hide tick labels / frame / axis label instead of hiding the axis.
        var xBottomGen = new ScottPlot.TickGenerators.NumericAutomatic();
        xBottomGen.LabelFormatter = (value) => paneModel.TickNumberFormat == "Scientific"
            ? value.ToString($"E{paneModel.XAxisDecimalPlaces}", CultureInfo.InvariantCulture)
            : value.ToString($"F{paneModel.XAxisDecimalPlaces}", CultureInfo.InvariantCulture);
        plotModel.Axes.Bottom.TickGenerator = xBottomGen;

        plotModel.Axes.Bottom.IsVisible = true;
        if (!paneModel.ShowXAxisLabels)
        {
            plotModel.Axes.Bottom.TickLabelStyle.IsVisible = false;
            plotModel.Axes.Bottom.Label.IsVisible = false;
            // Keep frame line visible so the pane stays fully boxed; only hide the labels/ticks.
            plotModel.Axes.Bottom.FrameLineStyle.IsVisible = true;
            plotModel.Axes.Bottom.MajorTickStyle.Length = 0;
            plotModel.Axes.Bottom.MinorTickStyle.Length = 0;
            plotModel.Axes.Bottom.MinimumSize = 0;
            plotModel.Axes.Bottom.MaximumSize = 0;
        }
        else
        {
            plotModel.Axes.Bottom.TickLabelStyle.IsVisible = true;
            plotModel.Axes.Bottom.Label.IsVisible = true;
            plotModel.Axes.Bottom.FrameLineStyle.IsVisible = true;
            plotModel.Axes.Bottom.MinimumSize = 0;
            plotModel.Axes.Bottom.MaximumSize = float.MaxValue;
        }

        if (paneModel.TickNumberFormat == "Scientific")
        {
            var leftGen = new ScottPlot.TickGenerators.NumericAutomatic();
            leftGen.LabelFormatter = (value) => value.ToString($"E{paneModel.Y1AxisDecimalPlaces}", CultureInfo.InvariantCulture);
            plotModel.Axes.Left.TickGenerator = leftGen;

            var rightGen = new ScottPlot.TickGenerators.NumericAutomatic();
            rightGen.LabelFormatter = (value) => value.ToString($"E{paneModel.Y2AxisDecimalPlaces}", CultureInfo.InvariantCulture);
            plotModel.Axes.Right.TickGenerator = rightGen;
        }
        else
        {
            var leftGen = new ScottPlot.TickGenerators.NumericAutomatic();
            leftGen.LabelFormatter = (value) => value.ToString($"F{paneModel.Y1AxisDecimalPlaces}", CultureInfo.InvariantCulture);
            plotModel.Axes.Left.TickGenerator = leftGen;

            var rightGen = new ScottPlot.TickGenerators.NumericAutomatic();
            rightGen.LabelFormatter = (value) => value.ToString($"F{paneModel.Y2AxisDecimalPlaces}", CultureInfo.InvariantCulture);
            plotModel.Axes.Right.TickGenerator = rightGen;
        }

        // Apply tick label font size
        plotModel.Axes.Bottom.TickLabelStyle.FontSize = (float)paneModel.TickLabelFontSize;
        plotModel.Axes.Left.TickLabelStyle.FontSize = (float)paneModel.TickLabelFontSize;
        plotModel.Axes.Right.TickLabelStyle.FontSize = (float)paneModel.TickLabelFontSize;

        // Apply background colors
        plotModel.FigureBackground.Color = ScottPlot.Color.FromHex(paneModel.BackgroundColor);
        plotModel.DataBackground.Color = ScottPlot.Color.FromHex(paneModel.DataBackgroundColor);

        // Apply legend placement (inside corners / outside edges / hidden) and re-push font
        // size in case the legend was rebuilt by ShowLegend(Edge).
        if (paneModel.ShowLegend)
            ApplyLegendPosition(plotModel, paneModel);
        else
            plotModel.HideLegend();

        _triggerUpdate();
    }

    /// <inheritdoc />
    public void SetXAxisRange(double min, double max)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        plotModel.Axes.Bottom.Range.Set(min, max);
        _triggerUpdate();
    }

    /// <inheritdoc />
    public void SetYAxisRange(double min, double max)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        plotModel.Axes.Left.Range.Set(min, max);
        _triggerUpdate();
    }

    /// <inheritdoc />
    public void SetY2AxisRange(double min, double max)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        plotModel.Axes.Right.Range.Set(min, max);
        _triggerUpdate();
    }

    /// <inheritdoc />
    public (double Min, double Max)? GetXAxisRange()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return null;

        var range = plotModel.Axes.Bottom.Range;
        return (range.Min, range.Max);
    }

    /// <inheritdoc />
    public (double Min, double Max)? GetYAxisRange()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return null;

        var range = plotModel.Axes.Left.Range;
        return (range.Min, range.Max);
    }

    /// <inheritdoc />
    public (double Min, double Max)? GetY2AxisRange()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return null;

        var range = plotModel.Axes.Right.Range;
        return (range.Min, range.Max);
    }

    /// <inheritdoc />
    public void ShowLegendWithFormatting()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        var paneModel = _getModel();
        ApplyLegendPosition(plotModel, paneModel);
    }

    /// <summary>
    /// Apply <see cref="PlotPaneModel.LegendPosition"/> to the ScottPlot legend.
    /// Inside-* values use <c>Plot.ShowLegend(Alignment)</c>; Outside-* values use
    /// <c>Plot.ShowLegend(Edge)</c> to mount a legend panel outside the data area.
    /// <see cref="LegendPosition.Hidden"/> calls <c>Plot.HideLegend()</c>.
    /// </summary>
    private static void ApplyLegendPosition(Plot plotModel, PlotPaneModel paneModel)
    {
        switch (paneModel.LegendPosition)
        {
            case LegendPosition.Hidden:
                plotModel.HideLegend();
                return;

            case LegendPosition.InsideUpperLeft:
                plotModel.ShowLegend(Alignment.UpperLeft);
                break;
            case LegendPosition.InsideLowerLeft:
                plotModel.ShowLegend(Alignment.LowerLeft);
                break;
            case LegendPosition.InsideLowerRight:
                plotModel.ShowLegend(Alignment.LowerRight);
                break;
            case LegendPosition.InsideUpperRight:
            default:
                plotModel.ShowLegend(Alignment.UpperRight);
                break;
        }

        plotModel.Legend.FontSize = (float)paneModel.LegendFontSize;

        // Reserve a few px above the top item so ScottPlot doesn't clip its glyph ascenders, without
        // inflating inter-row spacing (see TopHeadroomLegendLayout). Idempotent.
        plotModel.Legend.Layout = new Helpers.TopHeadroomLegendLayout();
    }

    /// <inheritdoc />
    public string FormatXValue(double xValue)
    {
        var paneModel = _getModel();
        return paneModel.TickNumberFormat == "Scientific"
            ? xValue.ToString($"E{paneModel.XAxisDecimalPlaces}", CultureInfo.InvariantCulture)
            : xValue.ToString($"F{paneModel.XAxisDecimalPlaces}", CultureInfo.InvariantCulture);
    }
}
