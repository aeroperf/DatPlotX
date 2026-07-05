using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Data;

namespace DatPlotX.Services;

/// <summary>
/// Calculates intersection points between event lines and curves across all panes
/// </summary>
public class IntersectionCalculator : IIntersectionCalculator
{
    /// <summary>
    /// Calculate intersections across all panes and populate a DataTable
    /// </summary>
    /// <param name="panes">Collection of plot panes to analyze</param>
    /// <param name="intersectionTable">Target DataTable to populate with results</param>
    public void CalculateAndPopulateIntersections(
        IEnumerable<PlotPaneViewModel> panes,
        DataTable intersectionTable)
    {
        intersectionTable.Clear();

        // Collect intersections from all panes
        var allIntersections = new List<IntersectionPointModel>();

        foreach (var pane in panes)
        {
            var paneIntersections = pane.CalculateIntersections();
            allIntersections.AddRange(paneIntersections);
        }

        // Populate table sorted by event line, then pane
        foreach (var intersection in allIntersections
            .OrderBy(i => i.EventLineLabel)
            .ThenBy(i => i.PaneIndex))
        {
            var row = intersectionTable.NewRow();
            row["Event Line"] = intersection.EventLineLabel;
            row["X Position"] = intersection.XPosition;
            row["Pane"] = $"Pane {intersection.PaneIndex + 1}";
            row["Curve"] = intersection.CurveName;
            row["Y Value"] = intersection.YValue;
            intersectionTable.Rows.Add(row);
        }
    }

    /// <summary>
    /// Initialize the structure of an intersection DataTable
    /// </summary>
    /// <param name="table">DataTable to initialize</param>
    public static void InitializeIntersectionTable(DataTable table)
    {
        table.Columns.Add("Event Line", typeof(string));
        table.Columns.Add("X Position", typeof(double));
        table.Columns.Add("Pane", typeof(string));
        table.Columns.Add("Curve", typeof(string));
        table.Columns.Add("Y Value", typeof(double));
    }

    /// <summary>
    /// Interpolate Y value at a given X position using nearest-neighbor approach
    /// </summary>
    /// <param name="period">Sample period for the data</param>
    /// <param name="data">Source data array</param>
    /// <param name="xPosition">X position to interpolate at</param>
    /// <returns>Interpolated Y value</returns>
    public static double InterpolateYValue(double period, double[] data, double xPosition)
    {
        // Calculate index from X position using sample period
        int index = (int)(xPosition / period);

        // Clamp to valid range
        index = Math.Clamp(index, 0, data.Length - 1);

        // Use nearest-neighbor interpolation
        // For more accuracy, consider implementing linear interpolation
        return data[index];
    }
}
