using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for managing arrow annotations on plot panes.
/// Arrow annotations display arrows between two points and can
/// be dragged, styled, and edited.
/// </summary>
public class ArrowAnnotationService : IArrowAnnotationService
{
    private readonly List<ArrowAnnotationModel> _annotations = new();

    /// <inheritdoc />
    public event Action? OnAnnotationsChanged;

    /// <inheritdoc />
    public int Count => _annotations.Count;

    /// <inheritdoc />
    public Guid AddAnnotation(ArrowAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes)
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
        pane?.AddArrowAnnotation(model);

        OnAnnotationsChanged?.Invoke();
        return model.Id;
    }

    /// <inheritdoc />
    public void UpdateAnnotation(ArrowAnnotationModel model, ObservableCollection<PlotPaneViewModel> panes)
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
            oldPane?.RemoveArrowAnnotation(model.Id);

            // Add to new pane
            var newPane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
            newPane?.AddArrowAnnotation(model);
        }
        else
        {
            // Update in same pane
            var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
            pane?.UpdateArrowAnnotation(model);
        }

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UpdateBasePosition(Guid annotationId, double newBaseX, double newBaseY, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return;

        model.BaseX = newBaseX;
        model.BaseY = newBaseY;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.UpdateArrowAnnotation(model);

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UpdateTipPosition(Guid annotationId, double newTipX, double newTipY, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return;

        model.TipX = newTipX;
        model.TipY = newTipY;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.UpdateArrowAnnotation(model);

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void MoveAnnotation(Guid annotationId, double deltaX, double deltaY, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return;

        model.BaseX += deltaX;
        model.BaseY += deltaY;
        model.TipX += deltaX;
        model.TipY += deltaY;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.UpdateArrowAnnotation(model);

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public bool RemoveAnnotation(Guid annotationId, ObservableCollection<PlotPaneViewModel> panes)
    {
        var model = _annotations.FirstOrDefault(a => a.Id == annotationId);
        if (model == null)
            return false;

        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
        pane?.RemoveArrowAnnotation(annotationId);

        _annotations.Remove(model);
        OnAnnotationsChanged?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public ArrowAnnotationModel? GetAnnotation(Guid annotationId)
    {
        return _annotations.FirstOrDefault(a => a.Id == annotationId);
    }

    /// <inheritdoc />
    public IReadOnlyList<ArrowAnnotationModel> GetAllAnnotations() => _annotations.AsReadOnly();

    /// <inheritdoc />
    public IReadOnlyList<ArrowAnnotationModel> GetAnnotationsForPane(int paneIndex)
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
            pane.AddArrowAnnotation(model);
        }
        else
        {
            pane.RemoveArrowAnnotation(annotationId);
        }

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void RestoreAnnotations(IEnumerable<ArrowAnnotationModel> models, ObservableCollection<PlotPaneViewModel> panes)
    {
        // Clear existing
        ClearAllAnnotations(panes);

        foreach (var model in models)
        {
            _annotations.Add(model);

            if (model.IsVisible)
            {
                var pane = panes.FirstOrDefault(p => p.PaneModel.Index == model.PaneIndex);
                pane?.AddArrowAnnotation(model);
            }
        }

        OnAnnotationsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ClearAllAnnotations(ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var pane in panes)
        {
            pane.ClearArrowAnnotations();
        }

        _annotations.Clear();
        OnAnnotationsChanged?.Invoke();
    }
}
