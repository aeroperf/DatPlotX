using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Interface for managing callout annotations at curve intersections.
/// Callouts display Y values with arrows pointing to intersection points
/// and can be dragged to custom positions.
/// </summary>
public interface ICalloutAnnotationService
{
    /// <summary>
    /// Event fired when callouts are updated
    /// </summary>
    event Action? OnCalloutsChanged;

    /// <summary>
    /// Create callout annotations for all curve intersections of an event line
    /// </summary>
    /// <param name="eventLineId">ID of the event line</param>
    /// <param name="xPosition">X position of the event line</param>
    /// <param name="panes">Collection of panes to add callouts to</param>
    void CreateCalloutsForEventLine(
        Guid eventLineId,
        double xPosition,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update callout positions and values when an event line moves
    /// </summary>
    /// <param name="eventLineId">ID of the event line that moved</param>
    /// <param name="newXPosition">New X position</param>
    /// <param name="panes">Collection of panes</param>
    void UpdateCalloutsForEventLine(
        Guid eventLineId,
        double newXPosition,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Remove all callouts associated with an event line
    /// </summary>
    /// <param name="eventLineId">ID of the event line</param>
    /// <param name="panes">Collection of panes to remove from</param>
    void RemoveCalloutsForEventLine(
        Guid eventLineId,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update the position offset for a specific callout after user drag
    /// </summary>
    /// <param name="calloutId">ID of the callout</param>
    /// <param name="offsetX">New X offset from intersection (data coordinates)</param>
    /// <param name="offsetY">New Y offset from intersection (data coordinates)</param>
    void UpdateCalloutOffset(Guid calloutId, double offsetX, double offsetY);

    /// <summary>
    /// Get all callout models for persistence
    /// </summary>
    IReadOnlyList<IntersectionCalloutModel> GetCalloutModels();

    /// <summary>
    /// Get callout models for a specific event line
    /// </summary>
    /// <param name="eventLineId">ID of the event line</param>
    IReadOnlyList<IntersectionCalloutModel> GetCalloutsForEventLine(Guid eventLineId);

    /// <summary>
    /// Restore callouts from saved models (for project load)
    /// </summary>
    /// <param name="calloutModels">Saved callout models</param>
    /// <param name="panes">Collection of panes to add to</param>
    void RestoreCallouts(
        IEnumerable<IntersectionCalloutModel> calloutModels,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Clear all callouts from all panes
    /// </summary>
    /// <param name="panes">Collection of panes</param>
    void ClearAllCallouts(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Toggle visibility of a specific callout
    /// </summary>
    /// <param name="calloutId">ID of the callout</param>
    /// <param name="isVisible">Whether the callout should be visible</param>
    /// <param name="panes">Collection of panes</param>
    void SetCalloutVisibility(
        Guid calloutId,
        bool isVisible,
        ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get the count of callouts
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Re-clamp callout label positions for the given pane to its current axis ranges.
    /// Called after zoom/pan operations so callouts stay inside the visible viewport
    /// even when the user zooms in past the original label position. Stored offsets
    /// are preserved — only the rendered TextCoordinates are recomputed.
    /// </summary>
    void ReclampCalloutsForViewportChange(PlotPaneViewModel pane);
}
