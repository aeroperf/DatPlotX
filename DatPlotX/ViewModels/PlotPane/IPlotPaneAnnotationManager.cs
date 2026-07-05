using DatPlotX.Models;

namespace DatPlotX.ViewModels.PlotPane;

/// <summary>
/// Interface for plot pane annotation management.
/// Handles all annotation types: callouts, text, and arrows.
/// </summary>
public interface IPlotPaneAnnotationManager
{
    #region Callout Annotations

    /// <summary>
    /// Add a callout annotation at an intersection point
    /// </summary>
    void AddCalloutAnnotation(Guid calloutId, double intersectionX, double intersectionY,
        string labelText, double offsetX, double offsetY, YAxisType yAxisType = YAxisType.Y1);

    /// <summary>
    /// Update the position of a callout annotation (after user drag)
    /// </summary>
    void UpdateCalloutPosition(Guid calloutId, double intersectionX, double intersectionY,
        double newOffsetX, double newOffsetY);

    /// <summary>
    /// Update the value displayed in a callout
    /// </summary>
    void UpdateCalloutValue(Guid calloutId, double newValue, string format = "F3");

    /// <summary>
    /// Remove a callout annotation
    /// </summary>
    bool RemoveCalloutAnnotation(Guid calloutId);

    /// <summary>
    /// Clear all callout annotations from this pane
    /// </summary>
    void ClearCalloutAnnotations();

    /// <summary>
    /// Get all callout IDs in this pane
    /// </summary>
    IReadOnlyCollection<Guid> GetCalloutIds();

    /// <summary>
    /// Check if a callout exists in this pane
    /// </summary>
    bool HasCallout(Guid calloutId);

    /// <summary>
    /// Get the callout plottable for hit testing during drag
    /// </summary>
    ScottPlot.Plottables.Callout? GetCallout(Guid calloutId);

    /// <summary>
    /// Get all callouts in this pane (for hit testing)
    /// </summary>
    IReadOnlyCollection<ScottPlot.Plottables.Callout> GetAllCallouts();

    /// <summary>
    /// Find a callout ID by its plottable reference
    /// </summary>
    Guid? FindCalloutId(ScottPlot.Plottables.Callout callout);

    #endregion

    #region Text Annotations

    /// <summary>
    /// Add a text annotation to this pane
    /// </summary>
    void AddTextAnnotation(TextAnnotationModel model);

    /// <summary>
    /// Update a text annotation's appearance
    /// </summary>
    void UpdateTextAnnotation(TextAnnotationModel model);

    /// <summary>
    /// Update only the position of a text annotation (for drag operations)
    /// </summary>
    void UpdateTextAnnotationPosition(Guid annotationId, double x, double y);

    /// <summary>
    /// Remove a text annotation
    /// </summary>
    bool RemoveTextAnnotation(Guid annotationId);

    /// <summary>
    /// Clear all text annotations
    /// </summary>
    void ClearTextAnnotations();

    /// <summary>
    /// Get all text annotation IDs in this pane
    /// </summary>
    IReadOnlyCollection<Guid> GetTextAnnotationIds();

    /// <summary>
    /// Check if a text annotation exists
    /// </summary>
    bool HasTextAnnotation(Guid annotationId);

    /// <summary>
    /// Get a text annotation plottable for hit testing
    /// </summary>
    ScottPlot.Plottables.Text? GetTextAnnotation(Guid annotationId);

    /// <summary>
    /// Get all text annotations for hit testing
    /// </summary>
    IReadOnlyCollection<ScottPlot.Plottables.Text> GetAllTextAnnotations();

    /// <summary>
    /// Find a text annotation ID by its plottable reference
    /// </summary>
    Guid? FindTextAnnotationId(ScottPlot.Plottables.Text textPlottable);

    #endregion

    #region Arrow Annotations

    /// <summary>
    /// Add an arrow annotation to this pane
    /// </summary>
    void AddArrowAnnotation(ArrowAnnotationModel model);

    /// <summary>
    /// Update an arrow annotation's appearance
    /// </summary>
    void UpdateArrowAnnotation(ArrowAnnotationModel model);

    /// <summary>
    /// Remove an arrow annotation
    /// </summary>
    bool RemoveArrowAnnotation(Guid annotationId);

    /// <summary>
    /// Clear all arrow annotations
    /// </summary>
    void ClearArrowAnnotations();

    /// <summary>
    /// Get all arrow annotation IDs in this pane
    /// </summary>
    IReadOnlyCollection<Guid> GetArrowAnnotationIds();

    /// <summary>
    /// Check if an arrow annotation exists
    /// </summary>
    bool HasArrowAnnotation(Guid annotationId);

    /// <summary>
    /// Get an arrow annotation plottable for hit testing
    /// </summary>
    ScottPlot.Plottables.Arrow? GetArrowAnnotation(Guid annotationId);

    /// <summary>
    /// Get all arrow annotations for hit testing
    /// </summary>
    IReadOnlyCollection<ScottPlot.Plottables.Arrow> GetAllArrowAnnotations();

    /// <summary>
    /// Find an arrow annotation ID by its plottable reference
    /// </summary>
    Guid? FindArrowAnnotationId(ScottPlot.Plottables.Arrow arrowPlottable);

    #endregion
}
