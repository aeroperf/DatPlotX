using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for managing global event lines that span all panes.
/// Global event lines appear at the same X position across all plot panes,
/// with labels shown only on the bottom pane.
/// </summary>
public class GlobalEventLineService : IGlobalEventLineService
{
    private readonly List<EventLineModel> _globalEventLines = new();
    private int _labelCounter;

    /// <inheritdoc />
    public event Action? OnEventLinesChanged;

    /// <inheritdoc />
    public int Count => _globalEventLines.Count;

    /// <inheritdoc />
    public Guid AddGlobalEventLine(
        double xPosition,
        string label,
        ObservableCollection<PlotPaneViewModel> panes,
        string color = "#FFB900")
    {
        // Create the event line model
        var eventLine = new EventLineModel
        {
            Id = Guid.NewGuid(),
            Label = label,
            XPosition = xPosition,
            Color = color,
            IsGlobal = true,
            IsVisible = true,
            CreatedAt = DateTime.Now
        };

        _globalEventLines.Add(eventLine);

        // Add visual to all panes
        AddVisualToAllPanes(eventLine, panes);

        OnEventLinesChanged?.Invoke();
        return eventLine.Id;
    }

    /// <inheritdoc />
    public bool RemoveGlobalEventLine(
        Guid eventLineId,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        var eventLine = _globalEventLines.FirstOrDefault(e => e.Id == eventLineId);
        if (eventLine == null)
            return false;

        // Remove from all panes
        foreach (var pane in panes)
        {
            pane.RemoveGlobalEventLine(eventLineId);
        }

        _globalEventLines.Remove(eventLine);
        OnEventLinesChanged?.Invoke();
        return true;
    }

    /// <inheritdoc />
    public void MoveGlobalEventLine(
        Guid eventLineId,
        double newXPosition,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        var eventLine = _globalEventLines.FirstOrDefault(e => e.Id == eventLineId);
        if (eventLine == null)
            return;

        // Update model
        eventLine.XPosition = newXPosition;

        // Update all pane visuals
        foreach (var pane in panes)
        {
            pane.MoveGlobalEventLine(eventLineId, newXPosition);
        }

        OnEventLinesChanged?.Invoke();
    }

    /// <inheritdoc />
    public void ClearAllGlobalEventLines(ObservableCollection<PlotPaneViewModel> panes)
    {
        // Clear from all panes
        foreach (var pane in panes)
        {
            pane.ClearGlobalEventLines();
        }

        _globalEventLines.Clear();
        _labelCounter = 0;
        OnEventLinesChanged?.Invoke();
    }

    /// <inheritdoc />
    public IReadOnlyList<EventLineModel> GetGlobalEventLines() => _globalEventLines.AsReadOnly();

    /// <inheritdoc />
    public EventLineModel? GetEventLineById(Guid eventLineId)
    {
        return _globalEventLines.FirstOrDefault(e => e.Id == eventLineId);
    }

    /// <inheritdoc />
    public void UpdateLabelVisibility(ObservableCollection<PlotPaneViewModel> panes)
    {
        if (panes.Count == 0)
            return;

        // Show the label on every pane so the line is identifiable wherever the user is looking
        // in the stack (the bottom-pane-only convention was confusing because most analysis
        // happens in the top panes; the per-pane top-anchored label is small and unobtrusive).
        foreach (var eventLine in _globalEventLines)
        {
            foreach (var pane in panes)
            {
                pane.UpdateGlobalEventLineLabel(eventLine.Id, showLabel: true, eventLine.Label);
            }
        }
    }

    /// <inheritdoc />
    public void RestoreGlobalEventLines(
        IEnumerable<EventLineModel> eventLines,
        ObservableCollection<PlotPaneViewModel> panes)
    {
        // Clear existing
        _globalEventLines.Clear();

        // Restore each global event line
        foreach (var eventLine in eventLines.Where(e => e.IsGlobal))
        {
            _globalEventLines.Add(eventLine);
            AddVisualToAllPanes(eventLine, panes);
        }

        // Update label counter based on existing labels
        UpdateLabelCounter();

        OnEventLinesChanged?.Invoke();
    }

    /// <summary>
    /// Generate a default label for a new event line
    /// </summary>
    public string GenerateDefaultLabel()
    {
        _labelCounter++;
        return $"E{_labelCounter}";
    }

    /// <summary>
    /// Add event line visual to all panes
    /// </summary>
    private void AddVisualToAllPanes(EventLineModel eventLine, ObservableCollection<PlotPaneViewModel> panes)
    {
        foreach (var pane in panes)
        {
            pane.AddGlobalEventLineVisual(
                eventLine.Id,
                eventLine.XPosition,
                eventLine.Label,
                showLabel: true,
                eventLine.Color);
        }
    }

    /// <summary>
    /// Update the label counter based on existing event line labels
    /// </summary>
    private void UpdateLabelCounter()
    {
        int maxNumber = 0;

        foreach (var eventLine in _globalEventLines)
        {
            // Try to parse label like "E1", "E2", etc.
            if (eventLine.Label.StartsWith('E') &&
                int.TryParse(eventLine.Label.AsSpan(1), out int number))
            {
                maxNumber = Math.Max(maxNumber, number);
            }
        }

        _labelCounter = maxNumber;
    }
}
