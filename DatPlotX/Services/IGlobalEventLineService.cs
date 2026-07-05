using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Interface for managing global event lines that span all panes.
/// Global event lines appear at the same X position across all plot panes,
/// with labels shown only on the bottom pane.
/// </summary>
public interface IGlobalEventLineService
{
    /// <summary>
    /// Event fired when global event lines are added, removed, or moved
    /// </summary>
    event Action? OnEventLinesChanged;

    /// <summary>
    /// Add a global event line at the specified X position across all panes
    /// </summary>
    /// <param name="xPosition">X-axis position for the event line</param>
    /// <param name="label">Display label for the event line</param>
    /// <param name="panes">Collection of panes to add the event line to</param>
    /// <param name="color">Optional color in hex format (default: "#FFB900")</param>
    /// <returns>The ID of the newly created event line</returns>
    Guid AddGlobalEventLine(
        double xPosition,
        string label,
        ObservableCollection<PlotPaneViewModel> panes,
        string color = "#FFB900");

    /// <summary>
    /// Remove a global event line by ID from all panes
    /// </summary>
    /// <param name="eventLineId">ID of the event line to remove</param>
    /// <param name="panes">Collection of panes to remove from</param>
    /// <returns>True if the event line was found and removed</returns>
    bool RemoveGlobalEventLine(
        Guid eventLineId,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Move a global event line to a new X position across all panes
    /// </summary>
    /// <param name="eventLineId">ID of the event line to move</param>
    /// <param name="newXPosition">New X-axis position</param>
    /// <param name="panes">Collection of panes to update</param>
    void MoveGlobalEventLine(
        Guid eventLineId,
        double newXPosition,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Clear all global event lines from all panes
    /// </summary>
    /// <param name="panes">Collection of panes to clear</param>
    void ClearAllGlobalEventLines(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get all global event line models
    /// </summary>
    IReadOnlyList<EventLineModel> GetGlobalEventLines();

    /// <summary>
    /// Get a specific event line model by ID
    /// </summary>
    /// <param name="eventLineId">ID of the event line</param>
    /// <returns>The event line model, or null if not found</returns>
    EventLineModel? GetEventLineById(Guid eventLineId);

    /// <summary>
    /// Update which pane shows the event line labels (should be the bottom pane)
    /// </summary>
    /// <param name="panes">Collection of panes</param>
    void UpdateLabelVisibility(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Restore global event lines from saved models (for project load)
    /// </summary>
    /// <param name="eventLines">Saved event line models</param>
    /// <param name="panes">Collection of panes to add to</param>
    void RestoreGlobalEventLines(
        IEnumerable<EventLineModel> eventLines,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get the count of global event lines
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Generate the next default label (e.g. "E1", "E2"). Idempotent counter — calling
    /// twice returns sequential labels.
    /// </summary>
    string GenerateDefaultLabel();
}
