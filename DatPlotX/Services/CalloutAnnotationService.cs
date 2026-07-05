using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DatPlotX.Services;

/// <summary>
/// Service for managing callout annotations at curve intersections.
/// Callouts display Y values with arrows pointing to intersection points
/// and can be dragged to custom positions.
/// </summary>
public class CalloutAnnotationService : ICalloutAnnotationService
{
    private readonly List<IntersectionCalloutModel> _callouts = new();

    // Default offset for new callouts (percentage of Y-axis range)
    private const double DefaultOffsetXPercent = 0.05;
    private const double DefaultOffsetYPercent = 0.10;

    /// <inheritdoc />
    public event Action? OnCalloutsChanged;

    /// <inheritdoc />
    public int Count => _callouts.Count;

    /// <inheritdoc />
    public void CreateCalloutsForEventLine(
        Guid eventLineId,
        double xPosition,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var pane in panes)
        {
            var curveValues = pane.GetCurveValuesAtX(xPosition);

            // Calculate default offsets based on Y-axis range
            var yRange = pane.GetYAxisRange();
            double defaultOffsetY = yRange.HasValue
                ? (yRange.Value.Max - yRange.Value.Min) * DefaultOffsetYPercent
                : 10.0;

            var xRange = pane.GetXAxisRange();
            double defaultOffsetX = xRange.HasValue
                ? (xRange.Value.Max - xRange.Value.Min) * DefaultOffsetXPercent
                : 10.0;

            int curveIndex = 0;
            foreach (var (config, yValue) in curveValues)
            {
                // Create callout model
                var callout = new IntersectionCalloutModel
                {
                    Id = Guid.NewGuid(),
                    EventLineId = eventLineId,
                    CurveName = config.CurveName,
                    PaneIndex = pane.PaneModel.Index,
                    XPosition = xPosition,
                    YValue = yValue,
                    // Stagger offsets slightly for multiple curves
                    OffsetX = defaultOffsetX * (1 + curveIndex * 0.5),
                    OffsetY = defaultOffsetY * (1 + curveIndex * 0.3),
                    IsVisible = true
                };

                _callouts.Add(callout);

                // Add visual to pane
                pane.AddCalloutAnnotation(
                    callout.Id,
                    xPosition,
                    yValue,
                    yValue.ToString("F4", CultureInfo.InvariantCulture),
                    callout.OffsetX,
                    callout.OffsetY,
                    config.YAxis);

                curveIndex++;
            }
        }

        OnCalloutsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UpdateCalloutsForEventLine(
        Guid eventLineId,
        double newXPosition,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        // Get all callouts for this event line
        var eventLineCallouts = _callouts.Where(c => c.EventLineId == eventLineId).ToList();

        foreach (var callout in eventLineCallouts)
        {
            // Find the pane for this callout
            var pane = panes.FirstOrDefault(p => p.PaneModel.Index == callout.PaneIndex);
            if (pane == null)
                continue;

            // Recalculate Y value at new X position
            var curveValues = pane.GetCurveValuesAtX(newXPosition);
            var curveValue = curveValues.FirstOrDefault(cv => cv.Config.CurveName == callout.CurveName);

            if (curveValue.Config != null)
            {
                // Update callout model
                callout.XPosition = newXPosition;
                callout.YValue = curveValue.YValue;

                // Update visual position
                pane.UpdateCalloutPosition(
                    callout.Id,
                    newXPosition,
                    curveValue.YValue,
                    callout.OffsetX,
                    callout.OffsetY);

                // Update displayed value
                pane.UpdateCalloutValue(callout.Id, curveValue.YValue, "F4");
            }
        }

        OnCalloutsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void RemoveCalloutsForEventLine(
        Guid eventLineId,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        // Get all callouts for this event line
        var eventLineCallouts = _callouts.Where(c => c.EventLineId == eventLineId).ToList();

        foreach (var callout in eventLineCallouts)
        {
            // Find the pane and remove visual
            var pane = panes.FirstOrDefault(p => p.PaneModel.Index == callout.PaneIndex);
            pane?.RemoveCalloutAnnotation(callout.Id);

            _callouts.Remove(callout);
        }

        OnCalloutsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void UpdateCalloutOffset(Guid calloutId, double offsetX, double offsetY)
    {
        var callout = _callouts.FirstOrDefault(c => c.Id == calloutId);
        if (callout != null)
        {
            callout.OffsetX = offsetX;
            callout.OffsetY = offsetY;
            OnCalloutsChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IntersectionCalloutModel> GetCalloutModels() => _callouts.AsReadOnly();

    /// <inheritdoc />
    public void ReclampCalloutsForViewportChange(PlotPaneViewModel pane)
    {
        foreach (var callout in _callouts)
        {
            if (callout.PaneIndex != pane.PaneModel.Index)
                continue;

            // UpdateCalloutPosition recomputes labelX/labelY from offsets and clamps to
            // current axis range with the same 5% margin used at creation time. We pass
            // the stored offsets unchanged so zooming back out restores the original
            // visual offset.
            pane.UpdateCalloutPosition(
                callout.Id,
                callout.XPosition,
                callout.YValue,
                callout.OffsetX,
                callout.OffsetY);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<IntersectionCalloutModel> GetCalloutsForEventLine(Guid eventLineId)
    {
        return _callouts.Where(c => c.EventLineId == eventLineId).ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public void RestoreCallouts(
        IEnumerable<IntersectionCalloutModel> calloutModels,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        // Clear existing callouts
        ClearAllCallouts(panes);

        foreach (var callout in calloutModels)
        {
            _callouts.Add(callout);

            // Find the pane and add visual
            var pane = panes.FirstOrDefault(p => p.PaneModel.Index == callout.PaneIndex);
            if (pane == null)
                continue;

            // Get the Y axis type for this curve
            var curveValues = pane.GetCurveValuesAtX(callout.XPosition);
            var curveValue = curveValues.FirstOrDefault(cv => cv.Config.CurveName == callout.CurveName);

            YAxisType yAxis = curveValue.Config?.YAxis ?? YAxisType.Y1;

            pane.AddCalloutAnnotation(
                callout.Id,
                callout.XPosition,
                callout.YValue,
                callout.YValue.ToString("F4", CultureInfo.InvariantCulture),
                callout.OffsetX,
                callout.OffsetY,
                yAxis);
        }

        OnCalloutsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ClearAllCallouts(ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var pane in panes)
        {
            pane.ClearCalloutAnnotations();
        }

        _callouts.Clear();
        OnCalloutsChanged?.Invoke();
    }

    /// <inheritdoc />
    public void SetCalloutVisibility(
        Guid calloutId,
        bool isVisible,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        var callout = _callouts.FirstOrDefault(c => c.Id == calloutId);
        if (callout == null)
            return;

        callout.IsVisible = isVisible;

        // Find the pane
        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == callout.PaneIndex);
        if (pane == null)
            return;

        if (isVisible)
        {
            // Get Y axis type
            var curveValues = pane.GetCurveValuesAtX(callout.XPosition);
            var curveValue = curveValues.FirstOrDefault(cv => cv.Config.CurveName == callout.CurveName);
            YAxisType yAxis = curveValue.Config?.YAxis ?? YAxisType.Y1;

            // Re-add the visual
            pane.AddCalloutAnnotation(
                callout.Id,
                callout.XPosition,
                callout.YValue,
                callout.YValue.ToString("F4", CultureInfo.InvariantCulture),
                callout.OffsetX,
                callout.OffsetY,
                yAxis);
        }
        else
        {
            // Remove the visual
            pane.RemoveCalloutAnnotation(callout.Id);
        }

        OnCalloutsChanged?.Invoke();
    }

    /// <summary>
    /// Get a callout by ID
    /// </summary>
    public IntersectionCalloutModel? GetCalloutById(Guid calloutId)
    {
        return _callouts.FirstOrDefault(c => c.Id == calloutId);
    }

    /// <summary>
    /// Update the visual for a callout after its position has been changed via drag
    /// </summary>
    public void UpdateCalloutVisualAfterDrag(
        Guid calloutId,
        double newOffsetX,
        double newOffsetY,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        var callout = _callouts.FirstOrDefault(c => c.Id == calloutId);
        if (callout == null)
            return;

        // Update model
        callout.OffsetX = newOffsetX;
        callout.OffsetY = newOffsetY;

        // Find the pane and update visual
        var pane = panes.FirstOrDefault(p => p.PaneModel.Index == callout.PaneIndex);
        pane?.UpdateCalloutPosition(
            calloutId,
            callout.XPosition,
            callout.YValue,
            newOffsetX,
            newOffsetY);

        OnCalloutsChanged?.Invoke();
    }
}
