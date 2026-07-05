using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Views;

/// <summary>
/// Manages layout synchronization and height calculations for plot panes in the main window.
/// Ensures equal distribution of pane heights and maintains x-axis alignment across all panes.
/// </summary>
public class MainWindowLayoutManager
{
    private readonly LayoutSynchronizationService _layoutService;
    private bool _isSynchronizingLayout;

    public MainWindowLayoutManager()
    {
        _layoutService = new LayoutSynchronizationService();
    }

    /// <summary>
    /// Update pane layout to distribute heights equally across all panes
    /// </summary>
    /// <param name="panes">Collection of pane view models</param>
    /// <param name="panesContainer">ItemsControl containing the pane controls</param>
    public void UpdatePaneLayout(
        ObservableCollection<PlotPaneViewModel> panes,
        ItemsControl panesContainer)
    {
        if (panes.Count == 0)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            var availableHeight = panesContainer.Bounds.Height;
            if (availableHeight > 0)
            {
                var paneHeight = availableHeight / panes.Count;
                paneHeight = Math.Max(paneHeight, 150);

                // Find PlotPaneControl instances via visual tree
                var paneControls = panesContainer.GetVisualDescendants()
                    .OfType<PlotPaneControl>();

                foreach (var paneControl in paneControls)
                {
                    paneControl.Height = paneHeight;
                }

                // Synchronize layout to ensure x-axis alignment
                SynchronizePlotLayout(panes, panesContainer);
            }
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Synchronize plot layout across all panes to ensure x-axis alignment
    /// </summary>
    /// <param name="panes">Collection of pane view models</param>
    /// <param name="panesContainer">ItemsControl containing the pane controls</param>
    public void SynchronizePlotLayout(
        ObservableCollection<PlotPaneViewModel> panes,
        ItemsControl panesContainer)
    {
        if (_isSynchronizingLayout || panes.Count == 0)
            return;

        _isSynchronizingLayout = true;

        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    double figureWidth = panesContainer.Bounds.Width;
                    double figureHeight = 0;

                    // Find a typical pane height from the visual tree
                    var firstPaneControl = panesContainer.GetVisualDescendants()
                        .OfType<PlotPaneControl>()
                        .FirstOrDefault();

                    if (firstPaneControl != null)
                    {
                        figureHeight = firstPaneControl.Height;
                    }

                    if (figureWidth > 0 && figureHeight > 0)
                    {
                        // Apply synchronized layout
                        _layoutService.SynchronizeLayout(panes, figureWidth, figureHeight);

                        // Refresh all pane controls
                        var paneControls = panesContainer.GetVisualDescendants()
                            .OfType<PlotPaneControl>();

                        foreach (var paneControl in paneControls)
                        {
                            paneControl.GetAvaPlot()?.Refresh();
                        }
                    }
                }
                finally
                {
                    _isSynchronizingLayout = false;
                }
            }, DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            _isSynchronizingLayout = false;
            System.Diagnostics.Debug.WriteLine($"[LayoutManager] SynchronizePlotLayout failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Enforce constraints on bottom pane splitter: bottom pane max 90%, top pane min 10%
    /// </summary>
    /// <param name="topPaneRow">The top row definition</param>
    /// <param name="bottomPaneRow">The bottom row definition</param>
    public void EnforceBottomPaneSplitterConstraints(RowDefinition topPaneRow, RowDefinition bottomPaneRow)
    {
        if (bottomPaneRow.Height.IsStar && topPaneRow.Height.IsStar)
        {
            double topValue = topPaneRow.Height.Value;
            double bottomValue = bottomPaneRow.Height.Value;
            double total = topValue + bottomValue;

            if (bottomValue / total > 0.90)
            {
                bottomPaneRow.Height = new GridLength(90, GridUnitType.Star);
                topPaneRow.Height = new GridLength(10, GridUnitType.Star);
            }
        }
    }
}
