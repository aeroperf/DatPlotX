using DatPlotX.Models;
using ScottPlot.Plottables;

namespace DatPlotX.ViewModels.PlotPane;

/// <summary>
/// Interface for plot pane curve management.
/// Handles adding, removing, updating, and querying curves.
/// </summary>
public interface IPlotPaneCurveManager
{
    /// <summary>
    /// Event raised when curve changes require axis label updates.
    /// Subscribers should call UpdateAxisLabelsFromCurves() in response.
    /// </summary>
    event Action? OnAxisLabelsNeedUpdate;

    /// <summary>
    /// Add a curve to this pane with full configuration support
    /// </summary>
    void AddCurve(double[] data, double samplePeriod, CurveConfigurationModel config);

    /// <summary>
    /// Add a curve to this pane (legacy method for backward compatibility)
    /// </summary>
    void AddCurve(double[] data, double samplePeriod, string curveName, string color, double lineWidth = 2.0, YAxisType yAxis = YAxisType.Y1);

    /// <summary>
    /// Add a scatter plot curve to this pane with full configuration support
    /// </summary>
    void AddScatterCurve(double[] xData, double[] yData, CurveConfigurationModel config);

    /// <summary>
    /// Add a scatter plot curve to this pane (legacy method)
    /// </summary>
    void AddScatterCurve(double[] xData, double[] yData, string curveName, string color, double lineWidth = 2.0, YAxisType yAxis = YAxisType.Y1);

    /// <summary>
    /// Remove a curve from this pane by ID
    /// </summary>
    bool RemoveCurve(Guid curveId);

    /// <summary>
    /// Remove all curve plottables from this pane, leaving event lines and annotations intact.
    /// </summary>
    void ClearCurves();

    /// <summary>
    /// Toggle curve visibility by ID
    /// </summary>
    bool ToggleCurveVisibility(Guid curveId);

    /// <summary>
    /// Set curve visibility to a specific value. Idempotent — calling with the existing
    /// value is a no-op. Returns true if the curve exists, false otherwise.
    /// </summary>
    bool SetCurveVisibility(Guid curveId, bool isVisible);

    /// <summary>
    /// Update curve format (color, line style, line width, markers, etc.)
    /// </summary>
    bool UpdateCurveFormat(CurveConfigurationModel updatedConfig);

    /// <summary>
    /// Get curve configuration by ID
    /// </summary>
    CurveConfigurationModel? GetCurveConfig(Guid curveId);

    /// <summary>
    /// Get all curve configurations for this pane
    /// </summary>
    IReadOnlyList<CurveConfigurationModel> GetAllCurveConfigs();

    /// <summary>
    /// Get the Y value for a specific curve at a given X coordinate
    /// </summary>
    double GetCurveYValueAtX(Guid curveId, double x);

    /// <summary>
    /// Find the closest curve to a given coordinate point
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="y1">Y coordinate in Y1 (left) axis scale</param>
    /// <param name="y2">Y coordinate in Y2 (right) axis scale</param>
    (Guid CurveId, YAxisType YAxis, double Distance)? GetClosestCurveAt(double x, double y1, double y2);

    /// <summary>
    /// Get Y values for all visible curves at a given X position
    /// </summary>
    /// <param name="xPosition">X position to query</param>
    /// <returns>List of tuples containing curve info and Y value</returns>
    List<(CurveConfigurationModel Config, double YValue)> GetCurveValuesAtX(double xPosition);

    /// <summary>
    /// Get all curves in this pane (legacy method)
    /// </summary>
    IReadOnlyList<(Signal Signal, double Period, double[] Data, string CurveName)> GetCurves();
}
