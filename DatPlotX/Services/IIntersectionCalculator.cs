using DatPlotX.ViewModels;
using System.Data;

namespace DatPlotX.Services;

/// <summary>
/// Interface for intersection calculation operations, enabling testability and DIP compliance
/// </summary>
public interface IIntersectionCalculator
{
    /// <summary>
    /// Calculate intersections across all panes and populate a DataTable
    /// </summary>
    /// <param name="panes">Collection of plot panes to analyze</param>
    /// <param name="intersectionTable">Target DataTable to populate with results</param>
    void CalculateAndPopulateIntersections(
        IEnumerable<PlotPaneViewModel> panes,
        DataTable intersectionTable);
}
