using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for coordinating curve operations across panes.
/// Handles curve plotting, replott, and formatting coordination.
/// </summary>
public interface ICurveCoordinationService
{
    /// <summary>
    /// Plot a single curve to a specific pane
    /// </summary>
    void PlotSingleCurveToPane(
        int targetPaneIndex,
        string parameterName,
        string yAxisType,
        PlotDataModel currentData,
        string selectedXColumn,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves,
        System.Collections.Generic.IReadOnlyList<string> colorPalette,
        string? unitOverride = null);

    /// <summary>
    /// Replot all curves with the current X-axis selection
    /// </summary>
    bool ReplotAllCurves(
        PlotDataModel currentData,
        string selectedXColumn,
        ObservableCollection<PlotPaneViewModel> panes,
        ObservableCollection<CurveConfigurationModel> activeCurves);

    /// <summary>
    /// Apply smart decimal defaults to a pane based on axis ranges
    /// </summary>
    void ApplySmartDecimalDefaults(PlotPaneViewModel paneViewModel);

    /// <summary>
    /// Recompute only the Y2-axis decimal places — for the first curve added to Y2 after the
    /// initial smart-default pass (when Y2 had no data and was left at 0 decimals).
    /// </summary>
    void ApplyY2SmartDecimalDefaults(PlotPaneViewModel paneViewModel);

    /// <summary>
    /// Apply smart decimal defaults to a pane and synchronize X-axis decimals across all panes
    /// </summary>
    void ApplySmartDecimalDefaultsWithSync(
        PlotPaneViewModel targetPane,
        ObservableCollection<PlotPaneViewModel> allPanes);
}
