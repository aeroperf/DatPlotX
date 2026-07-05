using DatPlotX.Models;
using DatPlotX.Services.Parsers;

namespace DatPlotX.Services;

/// <summary>
/// Interface for data import operations, enabling testability and DIP compliance
/// </summary>
public interface IDataImportService
{
    /// <summary>
    /// Callback invoked when a large file is detected
    /// Caller can show a dialog and decide whether to proceed
    /// </summary>
    LargeFileWarningCallback? OnLargeFileWarning { get; set; }

    /// <summary>
    /// Import data from a CSV file with default settings
    /// </summary>
    Task<PlotDataModel> ImportCsvAsync(string filePath);

    /// <summary>
    /// Import data from a CSV file with custom options
    /// </summary>
    Task<PlotDataModel> ImportCsvAsync(string filePath, CsvImportOptions options);

    /// <summary>
    /// Import data from a tab-delimited file
    /// </summary>
    Task<PlotDataModel> ImportTabAsync(string filePath);

    /// <summary>
    /// Import data from a text file with custom delimiter
    /// </summary>
    Task<PlotDataModel> ImportTextAsync(string filePath, string delimiter);

    /// <summary>
    /// Detect the file format and import accordingly
    /// </summary>
    Task<PlotDataModel> ImportAutoDetectAsync(string filePath);

    /// <summary>
    /// Import data with specified options (supports X-Plane format)
    /// </summary>
    Task<PlotDataModel> ImportDataAsync(string filePath, ImportOptionsModel options, CancellationToken cancellationToken = default);
}
