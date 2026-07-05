using DatPlotX.Models;
using ScottPlot;
using System.Data;
using OxyPlotModel = OxyPlot.PlotModel;

namespace DatPlotX.Services;

/// <summary>
/// Interface for data export operations, enabling testability and DIP compliance
/// </summary>
public interface IDataExportService
{
    /// <summary>
    /// Export intersection points to CSV file
    /// </summary>
    Task ExportIntersectionsToCsvAsync(List<IntersectionPointModel> intersections, string filePath);

    /// <summary>
    /// Export intersection points to tab-delimited file
    /// </summary>
    Task ExportIntersectionsToTabAsync(List<IntersectionPointModel> intersections, string filePath);

    /// <summary>
    /// Export DataTable to CSV file
    /// </summary>
    Task ExportDataTableToCsvAsync(DataTable dataTable, string filePath);

    /// <summary>
    /// Export a pre-formatted string matrix to a CSV file (one inner list per row; the first row
    /// is typically the header). Used by the Analysis panel, whose table is dynamic and already
    /// rendered to display strings. CsvHelper handles quoting/escaping per RFC 4180.
    /// </summary>
    Task ExportRowsToCsvAsync(IReadOnlyList<IReadOnlyList<string>> rows, string filePath);

    /// <summary>
    /// Export plot to PNG image
    /// </summary>
    void ExportPlotToPng(Plot plot, string filePath, int width = 1920, int height = 1080);

    /// <summary>
    /// Export plot to JPEG image
    /// </summary>
    void ExportPlotToJpeg(Plot plot, string filePath, int width = 1920, int height = 1080, int quality = 90);

    /// <summary>
    /// Export plot to BMP image
    /// </summary>
    void ExportPlotToBmp(Plot plot, string filePath, int width = 1920, int height = 1080);

    /// <summary>
    /// Export plot to SVG (vector graphics)
    /// </summary>
    void ExportPlotToSvg(Plot plot, string filePath, int width = 1920, int height = 1080);

    /// <summary>
    /// Export multiple plots combined vertically into a single PNG
    /// </summary>
    void ExportMultiplePlotsToPng(List<Plot> plots, string filePath, int width, int height);

    /// <summary>
    /// Export multiple plots combined vertically into a single JPEG
    /// </summary>
    void ExportMultiplePlotsToJpeg(List<Plot> plots, string filePath, int width, int height, int quality = 90);

    /// <summary>
    /// Export multiple plots combined vertically into a single BMP
    /// </summary>
    void ExportMultiplePlotsToBmp(List<Plot> plots, string filePath, int width, int height);

    /// <summary>
    /// Export multiple plots combined vertically into a single SVG
    /// </summary>
    void ExportMultiplePlotsToSvg(List<Plot> plots, string filePath, int width, int height);

    /// <summary>
    /// Export an OxyPlot PlotModel (Compact Plot Surface) to an image file.
    /// Format is selected from the file extension (.png/.jpg/.jpeg/.bmp/.svg).
    /// </summary>
    void ExportOxyPlotByExtension(OxyPlotModel plot, string filePath, int width, int height);

    /// <summary>
    /// Export comprehensive report (intersections + summary statistics)
    /// </summary>
    Task ExportComprehensiveReportAsync(List<IntersectionPointModel> intersections, DataTable? sourceData, string filePath);

    /// <summary>
    /// Generate summary statistics for a curve
    /// </summary>
    CurveStatistics CalculateCurveStatistics(double[] data, string curveName);

    /// <summary>
    /// Export statistics for multiple curves to CSV
    /// </summary>
    Task ExportStatisticsToCsvAsync(List<CurveStatistics> statistics, string filePath);
}
