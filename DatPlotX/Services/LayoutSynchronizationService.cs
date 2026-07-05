using ScottPlot;
using DatPlotX.ViewModels;

namespace DatPlotX.Services;

/// <summary>
/// Service to synchronize plot layout across multiple panes to ensure x-axis alignment
/// </summary>
public class LayoutSynchronizationService
{
    /// <summary>
    /// Synchronize layout across all plot panes by applying uniform padding
    /// </summary>
    /// <param name="panes">Collection of plot pane view models</param>
    /// <param name="figureWidth">Width of the figure in pixels</param>
    /// <param name="figureHeight">Height of each figure in pixels</param>
    public void SynchronizeLayout(IEnumerable<PlotPaneViewModel> panes, double figureWidth, double figureHeight)
    {
        var panesList = panes.ToList();
        if (panesList.Count == 0)
            return;

        // First, let all plots auto-layout by forcing a render
        // This allows ScottPlot to calculate the required space for labels
        foreach (var pane in panesList)
        {
            if (pane.PlotModel != null)
            {
                // Render to calculate layout using the new API
                pane.PlotModel.RenderInMemory((int)figureWidth, (int)figureHeight);
            }
        }

        // Measure the padding that ScottPlot calculated for each pane.
        // Left/right must be uniform so data columns align across panes.
        // Top/bottom are per-pane: non-bottom panes have no X-axis labels, so they
        // only need a small bottom margin instead of the full label+tick space.
        float maxLeft = 0;
        float maxRight = 0;
        float maxTopWithTitle = 0;    // for panes that have a title
        float maxTopNoTitle = 0;      // for panes without a title
        float maxBottomXLabels = 0;   // for the bottom pane (shows X-axis labels)
        float maxBottomNoLabels = 0;  // for upper panes (no X-axis labels)

        foreach (var pane in panesList)
        {
            if (pane.PlotModel == null)
                continue;

            try
            {
                var dataRect = pane.PlotModel.RenderManager.LastRender.DataRect;
                var figRect = pane.PlotModel.RenderManager.LastRender.FigureRect;

                float left = dataRect.Left - figRect.Left;
                float top = dataRect.Top - figRect.Top;
                float right = figRect.Right - dataRect.Right;
                float bottom = figRect.Bottom - dataRect.Bottom;

                maxLeft = Math.Max(maxLeft, left);
                maxRight = Math.Max(maxRight, right);

                bool hasTitle = !string.IsNullOrWhiteSpace(pane.PaneModel.TitleText);
                if (hasTitle)
                    maxTopWithTitle = Math.Max(maxTopWithTitle, top);
                else
                    maxTopNoTitle = Math.Max(maxTopNoTitle, top);

                if (pane.PaneModel.ShowXAxisLabels)
                    maxBottomXLabels = Math.Max(maxBottomXLabels, bottom);
                else
                    maxBottomNoLabels = Math.Max(maxBottomNoLabels, bottom);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LayoutSynchronizationService] Layout info unavailable for pane: {ex.Message}");
                continue;
            }
        }

        maxLeft = Math.Max(maxLeft, 120);
        maxRight = Math.Max(maxRight, 100);
        // ScottPlot measurement already accounts for Title.MaximumSize and Bottom axis clamps
        // that we set in PlotPaneFormattingService. Trust the measured value with a small floor,
        // so we do not re-bloat panes whose axis/title have been tightened.
        maxTopWithTitle = Math.Max(maxTopWithTitle, 4);
        maxTopNoTitle = Math.Max(maxTopNoTitle, 2);
        maxBottomXLabels = Math.Max(maxBottomXLabels, 50);
        maxBottomNoLabels = Math.Max(maxBottomNoLabels, 2);

        foreach (var pane in panesList)
        {
            if (pane.PlotModel == null)
                continue;

            bool hasTitle = !string.IsNullOrWhiteSpace(pane.PaneModel.TitleText);
            float topPad = hasTitle ? maxTopWithTitle : maxTopNoTitle;
            float bottomPad = pane.PaneModel.ShowXAxisLabels ? maxBottomXLabels : maxBottomNoLabels;

            // PixelPadding constructor order is (left, right, bottom, top) — NOT CSS order.
            // Earlier this call passed args in (left, top, right, bottom) order, which silently
            // mapped topPad → right and bottomPad → top, leaving non-bottom panes with the full
            // tick-label bottom margin baked in and producing the visible inter-pane gap.
            pane.PlotModel.Layout.Fixed(new PixelPadding(maxLeft, maxRight, bottomPad, topPad));
        }
    }

    /// <summary>
    /// Reset all panes to automatic layout
    /// </summary>
    public void ResetToAutomaticLayout(IEnumerable<PlotPaneViewModel> panes)
    {
        // Note: ScottPlot 5.x doesn't have a direct "Automatic" layout method
        // To reset, you would need to set minimal padding or recreate the plots
        // This method is kept for potential future use
        foreach (var pane in panes)
        {
            if (pane.PlotModel != null)
            {
                // Reset to minimal automatic-like padding.
                // PixelPadding constructor order: (left, right, bottom, top).
                var minimalPadding = new PixelPadding(0, 0, 0, 0);
                pane.PlotModel.Layout.Fixed(minimalPadding);
            }
        }
    }
}
