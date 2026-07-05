using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Interface for managing arrow annotations on plot panes.
/// Arrow annotations display arrows between two points and can
/// be dragged, styled, and edited.
/// </summary>
public interface IArrowAnnotationService
{
    /// <summary>
    /// Event fired when arrow annotations are updated
    /// </summary>
    event Action? OnAnnotationsChanged;

    /// <summary>
    /// Add a new arrow annotation to a pane
    /// </summary>
    /// <param name="model">Arrow annotation model with configuration</param>
    /// <param name="panes">Collection of panes</param>
    /// <returns>The created annotation's ID</returns>
    Guid AddAnnotation(ArrowAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update an existing arrow annotation
    /// </summary>
    /// <param name="model">Updated model</param>
    /// <param name="panes">Collection of panes</param>
    void UpdateAnnotation(ArrowAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update the base position of an arrow annotation (after user drag of base point)
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <param name="newBaseX">New base X position</param>
    /// <param name="newBaseY">New base Y position</param>
    /// <param name="panes">Collection of panes</param>
    void UpdateBasePosition(Guid annotationId, double newBaseX, double newBaseY, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update the tip position of an arrow annotation (after user drag of tip point)
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <param name="newTipX">New tip X position</param>
    /// <param name="newTipY">New tip Y position</param>
    /// <param name="panes">Collection of panes</param>
    void UpdateTipPosition(Guid annotationId, double newTipX, double newTipY, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Move the entire arrow annotation by an offset (after user drag of whole arrow)
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <param name="deltaX">X offset to move</param>
    /// <param name="deltaY">Y offset to move</param>
    /// <param name="panes">Collection of panes</param>
    void MoveAnnotation(Guid annotationId, double deltaX, double deltaY, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Remove an arrow annotation
    /// </summary>
    /// <param name="annotationId">ID of the annotation to remove</param>
    /// <param name="panes">Collection of panes</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveAnnotation(Guid annotationId, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get an arrow annotation by ID
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <returns>The annotation model or null if not found</returns>
    ArrowAnnotationModel? GetAnnotation(Guid annotationId);

    /// <summary>
    /// Get all arrow annotation models
    /// </summary>
    IReadOnlyList<ArrowAnnotationModel> GetAllAnnotations();

    /// <summary>
    /// Get all arrow annotations for a specific pane
    /// </summary>
    /// <param name="paneIndex">Index of the pane</param>
    IReadOnlyList<ArrowAnnotationModel> GetAnnotationsForPane(int paneIndex);

    /// <summary>
    /// Set visibility of an arrow annotation
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <param name="isVisible">Whether the annotation should be visible</param>
    /// <param name="panes">Collection of panes</param>
    void SetVisibility(Guid annotationId, bool isVisible, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Restore arrow annotations from saved models (for project load)
    /// </summary>
    /// <param name="models">Saved annotation models</param>
    /// <param name="panes">Collection of panes</param>
    void RestoreAnnotations(IEnumerable<ArrowAnnotationModel> models, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Clear all arrow annotations
    /// </summary>
    /// <param name="panes">Collection of panes</param>
    void ClearAllAnnotations(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get the count of arrow annotations
    /// </summary>
    int Count { get; }
}
