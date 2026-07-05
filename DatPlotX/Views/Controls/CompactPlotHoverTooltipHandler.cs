using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using OxyPlot;
using OxyPlot.Avalonia;
using DatPlotX.ViewModels;
using LineSeries = OxyPlot.Series.LineSeries;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Hover tooltip for the Compact Plot Surface. Mirrors the Stacked-mode pattern: a single
/// pre-built <see cref="Border"/> is repositioned and toggled visible — no Canvas child
/// add/remove on every mouse-move.
///
/// Replaces OxyPlot's built-in <c>HoverSnapTrack</c> + <c>ShowTracker</c> path, which
/// rebuilds and re-inserts the tracker control on every <c>PointerMoved</c> while holding
/// <c>ActualModel.SyncRoot</c>. That layout-storm was the cause of the macOS UI lockup
/// users could only break out of by alt-tabbing away from the app.
/// </summary>
public sealed class CompactPlotHoverTooltipHandler
{
    private const double HoverThresholdPixels = 20.0;

    private readonly PlotView _plotView;
    private readonly CompactPlotViewModel _viewModel;
    private readonly Border _tooltipBorder;
    private readonly TextBlock _tooltipText;

    public bool IsEnabled { get; set; } = true;

    public CompactPlotHoverTooltipHandler(
        PlotView plotView,
        CompactPlotViewModel viewModel,
        Border tooltipBorder,
        TextBlock tooltipText)
    {
        _plotView = plotView;
        _viewModel = viewModel;
        _tooltipBorder = tooltipBorder;
        _tooltipText = tooltipText;
    }

    public void OnPointerMoved(PointerEventArgs e)
    {
        if (!IsEnabled)
        {
            HideTooltip();
            return;
        }

        var model = _plotView.ActualModel;
        if (model is null)
        {
            HideTooltip();
            return;
        }

        var pos = e.GetPosition(_plotView);
        var screenPoint = new ScreenPoint(pos.X, pos.Y);

        if (!model.PlotArea.Contains(screenPoint.X, screenPoint.Y))
        {
            HideTooltip();
            return;
        }

        TrackerHitResult? best = null;
        double bestDistance = double.PositiveInfinity;

        foreach (var series in model.Series)
        {
            if (series is not LineSeries line || !line.IsVisible)
                continue;

            var hit = line.GetNearestPoint(screenPoint, interpolate: true)
                   ?? line.GetNearestPoint(screenPoint, interpolate: false);
            if (hit is null) continue;

            double dx = hit.Position.X - screenPoint.X;
            double dy = hit.Position.Y - screenPoint.Y;
            double dist = System.Math.Sqrt(dx * dx + dy * dy);
            if (dist < bestDistance)
            {
                bestDistance = dist;
                best = hit;
            }
        }

        if (best is null || bestDistance > HoverThresholdPixels)
        {
            HideTooltip();
            return;
        }

        var xAxisTitle = best.XAxis?.Title ?? "X";
        var yAxisTitle = best.YAxis?.Title ?? "Y";
        var seriesTitle = best.Series?.Title ?? string.Empty;

        _tooltipText.Text = $"{seriesTitle}\n{xAxisTitle}: {best.DataPoint.X:N3}\n{yAxisTitle}: {best.DataPoint.Y:N3}";

        var tipX = pos.X + 14;
        var tipY = pos.Y - 10;
        _tooltipBorder.Margin = new Thickness(tipX, tipY, 0, 0);
        _tooltipBorder.IsVisible = true;
    }

    public void OnPointerExited() => HideTooltip();

    public void HideTooltip() => _tooltipBorder.IsVisible = false;
}
