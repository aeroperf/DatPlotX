using DatPlotX.Models;
using DatPlotX.Services.Parsers;
using System.Globalization;

namespace DatPlotX.Services;

/// <summary>
/// Delegate for large file warnings - allows caller to decide whether to proceed.
/// Async to avoid blocking the UI thread on dialog interaction.
/// </summary>
public delegate Task<bool> LargeFileWarningCallback(double fileSizeMB, string message);

/// <summary>
/// Service for importing data from CSV, TXT, TAB, and X-Plane files
/// Acts as a facade delegating to specialized parsers
/// </summary>
public class DataImportService : IDataImportService
{
    private readonly ICsvDataParser _csvParser;
    private readonly IXPlaneDataParser _xPlaneParser;

    public DataImportService(ICsvDataParser csvParser, IXPlaneDataParser? xPlaneParser = null)
    {
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _xPlaneParser = xPlaneParser ?? new XPlaneDataParser(csvParser);
    }

    /// <summary>
    /// Callback invoked when a large file is detected
    /// </summary>
    public LargeFileWarningCallback? OnLargeFileWarning
    {
        get => _csvParser.OnLargeFileWarning;
        set
        {
            _csvParser.OnLargeFileWarning = value;
            _xPlaneParser.OnLargeFileWarning = value;
        }
    }

    /// <summary>
    /// Import data from a CSV file with default settings
    /// </summary>
    public async Task<PlotDataModel> ImportCsvAsync(string filePath)
    {
        return await ImportCsvAsync(filePath, new CsvImportOptions());
    }

    /// <summary>
    /// Import data from a CSV file with custom options
    /// </summary>
    public async Task<PlotDataModel> ImportCsvAsync(string filePath, CsvImportOptions options)
    {
        return await _csvParser.ParseAsync(filePath, options);
    }

    /// <summary>
    /// Import data from a tab-delimited file
    /// </summary>
    public async Task<PlotDataModel> ImportTabAsync(string filePath)
    {
        return await _csvParser.ParseTabAsync(filePath);
    }

    /// <summary>
    /// Import data from a text file with custom delimiter
    /// </summary>
    public async Task<PlotDataModel> ImportTextAsync(string filePath, string delimiter)
    {
        return await _csvParser.ParseTextAsync(filePath, delimiter);
    }

    /// <summary>
    /// Detect the file format and import accordingly
    /// </summary>
    public async Task<PlotDataModel> ImportAutoDetectAsync(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".csv" => await ImportCsvAsync(filePath),
            ".tab" => await ImportTabAsync(filePath),
            ".txt" => await _csvParser.ParseTextAsync(filePath, null), // null = auto-detect delimiter
            _ => await ImportCsvAsync(filePath) // Default to CSV
        };
    }

    public async Task<PlotDataModel> ImportDataAsync(string filePath, ImportOptionsModel options, CancellationToken cancellationToken = default)
    {
        if (options.IsXPlaneFormat)
        {
            return await _xPlaneParser.ParseAsync(filePath, cancellationToken);
        }
        else
        {
            var culture = ResolveCulture(options.CultureName);
            var csvImportOptions = new CsvImportOptions
            {
                Delimiter = options.Delimiter,
                Culture = culture,
                HeaderLine = options.HeaderLine,
                UnitLine = options.UnitLine,
                DataStartLine = options.DataStartLine,
            };
            return await _csvParser.ParseAsync(filePath, csvImportOptions, cancellationToken);
        }
    }

    private static CultureInfo ResolveCulture(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
            return CultureInfo.InvariantCulture;
        try
        {
            return CultureInfo.GetCultureInfo(cultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

}
