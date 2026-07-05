using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for coordinating pane operations across the application.
/// Handles pane creation, removal, reindexing, and label management.
/// </summary>
public class PaneCoordinationService : IPaneCoordinationService
{
    private readonly IGlobalEventLineService _globalEventLineService;

    public PaneCoordinationService(IGlobalEventLineService globalEventLineService)
    {
        _globalEventLineService = globalEventLineService;
    }

    /// <inheritdoc />
    public PlotPaneViewModel AddPane(ObservableCollection<PlotPaneViewModel> panes, bool xAxisSynchronized)
    {
        var paneIndex = panes.Count;

        // Copy X-axis decimal places from existing panes to maintain consistency
        int xAxisDecimals = 0;
        if (panes.Count > 0)
        {
            xAxisDecimals = panes[0].PaneModel.XAxisDecimalPlaces;
        }

        var paneModel = new PlotPaneModel
        {
            Index = paneIndex,
            Name = $"Pane {paneIndex + 1}",
            XAxisLabel = "Time (s)",
            YAxisLabel = "Value",
            ShowGrid = true,
            ShowLegend = true,
            ShowXAxisLabels = (paneIndex == panes.Count), // Only bottom pane shows X labels
            XAxisSynchronized = xAxisSynchronized,
            // Inherit X-axis decimals from existing panes, use default 0 for Y axes
            XAxisDecimalPlaces = xAxisDecimals,
            Y1AxisDecimalPlaces = 0,
            Y2AxisDecimalPlaces = 0
        };

        var paneViewModel = new PlotPaneViewModel(paneModel);
        panes.Add(paneViewModel);

        // Update existing panes to hide X-axis labels except for the bottom one
        UpdatePaneXAxisLabels(panes);

        return paneViewModel;
    }

    /// <inheritdoc />
    public bool RemovePane(ObservableCollection<PlotPaneViewModel> panes)
    {
        if (panes.Count <= 1)
        {
            return false;
        }

        var lastPane = panes[panes.Count - 1];
        lastPane.Clear();
        lastPane.Dispose();
        panes.RemoveAt(panes.Count - 1);

        ReindexPanes(panes);
        UpdatePaneXAxisLabels(panes);

        return true;
    }

    /// <inheritdoc />
    public void ReindexPanes(ObservableCollection<PlotPaneViewModel> panes)
    {
        for (int i = 0; i < panes.Count; i++)
        {
            panes[i].PaneModel.Index = i;
            panes[i].PaneModel.Name = $"Pane {i + 1}";
        }
    }

    /// <inheritdoc />
    public void UpdatePaneXAxisLabels(ObservableCollection<PlotPaneViewModel> panes)
    {
        for (int i = 0; i < panes.Count; i++)
        {
            panes[i].PaneModel.ShowXAxisLabels = (i == panes.Count - 1);
        }

        // Update global event line label visibility (label only on bottom pane)
        _globalEventLineService.UpdateLabelVisibility(panes);
    }

    /// <inheritdoc />
    public void SynchronizeXAxes(ObservableCollection<PlotPaneViewModel> panes, bool enabled)
    {
        if (!enabled || panes.Count == 0)
            return;

        // Get the X-axis range from the first pane
        var firstPaneRange = panes[0].GetXAxisRange();
        if (firstPaneRange == null)
            return;

        // Apply to all other panes
        for (int i = 1; i < panes.Count; i++)
        {
            panes[i].SetXAxisRange(firstPaneRange.Value.Min, firstPaneRange.Value.Max);
        }
    }
}
