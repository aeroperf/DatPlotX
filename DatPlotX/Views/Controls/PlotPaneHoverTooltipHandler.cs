using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using ScottPlot.Avalonia;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Handles hover tooltip display for a plot pane — shows curve name and coordinates
/// of the nearest plotted point within a pixel threshold as the mouse moves over the plot.
/// </summary>
public sealed class PlotPaneHoverTooltipHandler
{
    private const double HoverThresholdPixels = 20.0;

    private readonly AvaPlot _avaPlot;
    private readonly PlotPaneViewModel _viewModel;
    private readonly Border _tooltipBorder;
    private readonly TextBlock _tooltipText;

    public bool IsEnabled { get; set; } = true;

    public PlotPaneHoverTooltipHandler(
        AvaPlot avaPlot,
        PlotPaneViewModel viewModel,
        Border tooltipBorder,
        TextBlock tooltipText)
    {
        _avaPlot = avaPlot;
        _viewModel = viewModel;
        _tooltipBorder = tooltipBorder;
        _tooltipText = tooltipText;
    }

    public void OnPointerMoved(PointerEventArgs e, bool isCtrlPressed)
    {
        if (!IsEnabled || isCtrlPressed)
        {
            HideTooltip();
            return;
        }

        if (_viewModel.PlotModel is null)
        {
            HideTooltip();
            return;
        }

        var pixelPos = e.GetPosition(_avaPlot);
        var scale = _avaPlot.DisplayScale;
        var mousePixel = new ScottPlot.Pixel((float)(pixelPos.X * scale), (float)(pixelPos.Y * scale));

        // Mouse X is axis-independent (shared bottom axis); read it off the Y1 projection.
        var mouseX = _avaPlot.Plot.GetCoordinates(mousePixel, _avaPlot.Plot.Axes.Bottom, _avaPlot.Plot.Axes.Left).X;

        // Pick the nearest curve in PIXEL space. Selecting by data-unit distance (the old
        // GetClosestCurveAt) mixed Y1-scale and Y2-scale values, so on a dual-Y pane the curve on
        // the smaller-range axis always won regardless of what was actually under the cursor. Here
        // every candidate is projected to pixels through its own Y axis before comparing.
        var bottom = _avaPlot.Plot.Axes.Bottom;
        var leftAxis = _avaPlot.Plot.Axes.Left;
        var rightAxis = _avaPlot.Plot.Axes.Right;

        CurveConfigurationModel? bestConfig = null;
        double bestYValue = double.NaN;
        double bestPixelDist = double.MaxValue;

        foreach (var (config, yValue) in _viewModel.GetCurveValuesAtX(mouseX))
        {
            if (double.IsNaN(yValue)) continue;

            var yAxisPlot = config.YAxis == YAxisType.Y2 ? rightAxis : leftAxis;
            var curvePixel = _avaPlot.Plot.GetPixel(new ScottPlot.Coordinates(mouseX, yValue), bottom, yAxisPlot);
            var dx = curvePixel.X - mousePixel.X;
            var dy = curvePixel.Y - mousePixel.Y;
            var pixelDist = Math.Sqrt(dx * dx + dy * dy);

            if (pixelDist < bestPixelDist)
            {
                bestPixelDist = pixelDist;
                bestConfig = config;
                bestYValue = yValue;
            }
        }

        if (bestConfig is null || bestPixelDist > HoverThresholdPixels)
        {
            HideTooltip();
            return;
        }

        var xLabel = _viewModel.PaneModel.XAxisLabel ?? "X";

        _tooltipText.Text = $"{xLabel}: {mouseX:N3}\n{bestConfig.CurveName}: {bestYValue:N3}";

        // Position tooltip near cursor with offset so it doesn't obscure the point
        var tipX = pixelPos.X + 14;
        var tipY = pixelPos.Y - 10;
        _tooltipBorder.Margin = new Thickness(tipX, tipY, 0, 0);
        _tooltipBorder.IsVisible = true;
    }

    public void OnPointerExited() => HideTooltip();

    public void HideTooltip() => _tooltipBorder.IsVisible = false;
}
