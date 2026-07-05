using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Interface for managing text annotations on plot panes.
/// Text annotations display user-defined text at specific locations
/// and can be dragged, styled, and edited.
/// </summary>
public interface ITextAnnotationService
{
    /// <summary>
    /// Event fired when text annotations are updated
    /// </summary>
    event Action? OnAnnotationsChanged;

    /// <summary>
    /// Add a new text annotation to a pane
    /// </summary>
    /// <param name="model">Text annotation model with configuration</param>
    /// <param name="panes">Collection of panes</param>
    /// <returns>The created annotation's ID</returns>
    Guid AddAnnotation(TextAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update an existing text annotation
    /// </summary>
    /// <param name="model">Updated model</param>
    /// <param name="panes">Collection of panes</param>
    void UpdateAnnotation(TextAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Update the position of a text annotation (after user drag)
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <param name="newX">New X position in data coordinates</param>
    /// <param name="newY">New Y position in data coordinates</param>
    /// <param name="panes">Collection of panes</param>
    void UpdatePosition(Guid annotationId, double newX, double newY, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Remove a text annotation
    /// </summary>
    /// <param name="annotationId">ID of the annotation to remove</param>
    /// <param name="panes">Collection of panes</param>
    /// <returns>True if removed, false if not found</returns>
    bool RemoveAnnotation(Guid annotationId, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get a text annotation by ID
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <returns>The annotation model or null if not found</returns>
    TextAnnotationModel? GetAnnotation(Guid annotationId);

    /// <summary>
    /// Get all text annotation models
    /// </summary>
    IReadOnlyList<TextAnnotationModel> GetAllAnnotations();

    /// <summary>
    /// Get all text annotations for a specific pane
    /// </summary>
    /// <param name="paneIndex">Index of the pane</param>
    IReadOnlyList<TextAnnotationModel> GetAnnotationsForPane(int paneIndex);

    /// <summary>
    /// Set visibility of a text annotation
    /// </summary>
    /// <param name="annotationId">ID of the annotation</param>
    /// <param name="isVisible">Whether the annotation should be visible</param>
    /// <param name="panes">Collection of panes</param>
    void SetVisibility(Guid annotationId, bool isVisible, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Restore text annotations from saved models (for project load)
    /// </summary>
    /// <param name="models">Saved annotation models</param>
    /// <param name="panes">Collection of panes</param>
    void RestoreAnnotations(IEnumerable<TextAnnotationModel> models, ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Clear all text annotations
    /// </summary>
    /// <param name="panes">Collection of panes</param>
    void ClearAllAnnotations(ObservableCollection<PlotPaneViewModel> panes);

    /// <summary>
    /// Get the count of text annotations
    /// </summary>
    int Count { get; }
}
