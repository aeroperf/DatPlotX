using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for coordinating pane operations across the application.
/// Handles pane creation, removal, reindexing, and label management.
/// </summary>
public interface IPaneCoordinationService
{
    /// <summary>
    /// Create and add a new pane to the collection
    /// </summary>
    /// <param name="panes">The panes collection</param>
    /// <param name="xAxisSynchronized">Whether X-axis should be synchronized</param>
    /// <returns>The newly created pane view model</returns>
    PlotPaneViewModel AddPane(ObservableCollection<PlotPaneViewModel> panes, bool xAxisSynchronized);

    /// <summary>
    /// Remove the last pane from the collection
    /// </summary>
    /// <param name="panes">The panes collection</param>
    /// <returns>True if a pane was removed, false if collection has only one pane</returns>
    bool RemovePane(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Re-index all panes after addition or removal
    /// </summary>
    /// <param name="panes">The panes collection</param>
    void ReindexPanes(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update X-axis label visibility - only bottom pane shows labels
    /// </summary>
    /// <param name="panes">The panes collection</param>
    void UpdatePaneXAxisLabels(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Synchronize X-axis ranges across all panes
    /// </summary>
    /// <param name="panes">The panes collection</param>
    /// <param name="enabled">Whether synchronization is enabled</param>
    void SynchronizeXAxes(ObservableCollection<PlotPaneViewModel> panes, bool enabled);
}
