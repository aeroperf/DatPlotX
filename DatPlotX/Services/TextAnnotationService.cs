using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for managing text annotations on plot panes.
/// Text annotations display user-defined text at specific locations
/// and can be dragged, styled, and edited.
/// </summary>
public class TextAnnotationService : ITextAnnotationService
{
    private readonly List<TextAnnotationModel> _annotations = new();

    /// <inheritdoc />
    public event Action? OnAnnotationsChanged;

    /// <inheritdoc />
    public int Count => _annotations.Count;

    /// <inheritdoc />
    public Guid AddAnnotation(TextAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes)
    {
        // Ensure the model has a unique ID
        if (model.Id == Guid.Empty)
        {
            model.Id = Guid.NewGuid();
        }

        // Add to our collection
        _annotations.Add(model);

        // Find the target pane and add the visual
        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.AddTextAnnotation(model);

        OnAnnotationsChanged?.Invoke();
        return model.Id;
    }

    /// <inheritdoc />
    public void UpdateAnnotation(TextAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes)
    {
        var existingIndex = _annotations.FindIndex(a => a.Id == model.Id);
        if (existingIndex < 0)
            return;

        // Check if pane changed
        var oldModel = _annotations[existingIndex];
        bool paneChanged = oldModel.PaneIndex != model.PaneIndex;

        // Update our collection
        _annotations[existingIndex] = model;

        if (paneChanged)
        {
            // Remove from old pane
            var oldPane = panes.FirstOrDefault(p => p.PaneModel.Index == oldModel.PaneIndex);
            oldPane?.RemoveTextAnnotation(model.Id);

            // Add to new pane
            var newPane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
            newPane?.AddTextAnnotation(model);
        }
        else
        {
            // Update in same pane
            var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
            pane?.UpdateTextAnnotation(model);
        }

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UpdatePosition(Guid annotationId, double newX, double newY, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return;

        model.X = newX;
        model.Y = newY;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.UpdateTextAnnotationPosition(annotationId, newX, newY);

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public bool RemoveAnnotation(Guid annotationId, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return false;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.RemoveTextAnnotation(annotationId);

        _annotations.Remove(model);
        OnAnnotationsChanged?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public TextAnnotationModel? GetAnnotation(Guid annotationId)
    {
        return _annotations.FirstOrDefault(a => a.Id == annotationId);
    }

    /// <inheritdoc />
    public IReadOnlyList<TextAnnotationModel> GetAllAnnotations() => _annotations.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<TextAnnotationModel> GetAnnotationsForPane(int paneIndex)
    {
        return _annotations.Where(a => a.PaneIndex == paneIndex).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public void SetVisibility(Guid annotationId, bool isVisible, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return;

        model.IsVisible = isVisible;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        if (pane == null)
            return;

        if (isVisible)
        {
            pane.AddTextAnnotation(model);
        }
        else
        {
            pane.RemoveTextAnnotation(annotationId);
        }

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void RestoreAnnotations(IEnumerable<TextAnnotationModel> models, ObservableCollection<PlotPaneViewModel> panes)
    {
        // Clear existing
        ClearAllAnnotations(panes);

        foreach (var model in models)
        {
            _annotations.Add(model);

            if (model.IsVisible)
            {
                var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
                pane?.AddTextAnnotation(model);
            }
        }

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ClearAllAnnotations(ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var pane in panes)
        {
            pane.ClearTextAnnotations();
        }

        _annotations.Clear();
        OnAnnotationsChanged?.Invoke();
    }
}
