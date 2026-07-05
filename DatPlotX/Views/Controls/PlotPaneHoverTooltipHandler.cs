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

        // Convert mouse pixel to plot coordinates (Y1 and Y2)
        var coordY1 = _avaPlot.Plot.GetCoordinates(mousePixel, _avaPlot.Plot.Axes.Bottom, _avaPlot.Plot.Axes.Left);
        var coordY2 = _avaPlot.Plot.GetCoordinates(mousePixel, _avaPlot.Plot.Axes.Bottom, _avaPlot.Plot.Axes.Right);

        // Ask the VM for the nearest curve at mouse X — covers both Signal and Scatter
        var closest = _viewModel.GetClosestCurveAt(coordY1.X, coordY1.Y, coordY2.Y);
        if (closest is null)
        {
            HideTooltip();
            return;
        }

        var (curveId, yAxis, yDistance) = closest.Value;

        // Convert y-distance from data coords to pixels for threshold check
        var config = _viewModel.GetCurveConfig(curveId);
        if (config is null || !config.IsVisible)
        {
            HideTooltip();
            return;
        }

        // Get the actual y value on that curve at the mouse's X
        double yValue = _viewModel.GetCurveYValueAtX(curveId, coordY1.X);
        if (double.IsNaN(yValue))
        {
            HideTooltip();
            return;
        }

        // Convert the curve point to pixels using the correct Y axis (Y1 or Y2)
        var yAxisPlot = yAxis == YAxisType.Y2
            ? _avaPlot.Plot.Axes.Right
            : _avaPlot.Plot.Axes.Left;
        var curvePixel = _avaPlot.Plot.GetPixel(
            new ScottPlot.Coordinates(coordY1.X, yValue),
            _avaPlot.Plot.Axes.Bottom,
            yAxisPlot);
        var dx = curvePixel.X - mousePixel.X;
        var dy = curvePixel.Y - mousePixel.Y;
        var pixelDist = Math.Sqrt(dx * dx + dy * dy);

        if (pixelDist > HoverThresholdPixels)
        {
            HideTooltip();
            return;
        }

        var xLabel = _viewModel.PaneModel.XAxisLabel ?? "X";

        _tooltipText.Text = $"{xLabel}: {coordY1.X:N3}\n{config.CurveName}: {yValue:N3}";

        // Position tooltip near cursor with offset so it doesn't obscure the point
        var tipX = pixelPos.X + 14;
        var tipY = pixelPos.Y - 10;
        _tooltipBorder.Margin = new Thickness(tipX, tipY, 0, 0);
        _tooltipBorder.IsVisible = true;
    }

    public void OnPointerExited() => HideTooltip();

    public void HideTooltip() => _tooltipBorder.IsVisible = false;
}
