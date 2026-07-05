using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Interface for project state management operations, enabling testability and DIP compliance
/// </summary>
public interface IProjectStateManager
{
    /// <summary>
    /// Save current application state to a project model
    /// </summary>
    /// <param name="project">Target project to save state into</param>
    /// <param name="currentData">Current plot data</param>
    /// <param name="panes">Collection of panes</param>
    /// <param name="activeCurves">Collection of active curves</param>
    /// <param name="globalEventLines">Global event lines to save (optional)</param>
    /// <param name="callouts">Intersection callouts to save (optional)</param>
    /// <param name="textAnnotations">Text annotations to save (optional)</param>
    /// <param name="arrowAnnotations">Arrow annotations to save (optional)</param>
    void SaveCurrentState(
        ProjectSettingsModel project,
        PlotDataModel? currentData,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves,
        IReadOnlyList<EventLineModel>? globalEventLines = null,
        IReadOnlyList<IntersectionCalloutModel>? callouts = null,
        IReadOnlyList<TextAnnotationModel>? textAnnotations = null,
        IReadOnlyList<ArrowAnnotationModel>? arrowAnnotations = null,
        IReadOnlyList<CompactCurveModel>? compactCurves = null,
        IReadOnlyList<EventLineModel>? compactEventLines = null);

    /// <summary>
    /// Restore application state from a project model
    /// </summary>
    /// <param name="project">Source project to restore from</param>
    /// <param name="currentData">Current plot data model</param>
    /// <param name="panes">Target panes collection to populate</param>
    /// <param name="activeCurves">Target active curves collection to populate</param>
    /// <param name="onGlobalEventLinesRestored">Callback to restore global event lines</param>
    /// <param name="onCalloutsRestored">Callback to restore callouts</param>
    /// <param name="onTextAnnotationsRestored">Callback to restore text annotations</param>
    /// <param name="onArrowAnnotationsRestored">Callback to restore arrow annotations</param>
    Task RestoreProjectState(
        ProjectSettingsModel project,
        PlotDataModel? currentData,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves,
        Action<IEnumerable<EventLineModel>>? onGlobalEventLinesRestored = null,
        Action<IEnumerable<IntersectionCalloutModel>>? onCalloutsRestored = null,
        Action<IEnumerable<TextAnnotationModel>>? onTextAnnotationsRestored = null,
        Action<IEnumerable<ArrowAnnotationModel>>? onArrowAnnotationsRestored = null,
        Action<IEnumerable<EventLineModel>>? onCompactEventLinesRestored = null);
}
