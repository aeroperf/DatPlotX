using CsvHelper;
using CsvHelper.Configuration;
using DatPlotX.Helpers;
using DatPlotX.Models;
using System.Data;
using System.Globalization;
using System.Text;

namespace DatPlotX.Services.Parsers;

/// <summary>
/// Parser for CSV, TSV, and text files with delimiters
/// Handles CSV format detection, column type inference, and data import
/// </summary>
public class CsvDataParser : ICsvDataParser
{
    private readonly ApplicationSettings _settings;

    public CsvDataParser() : this(new ApplicationSettings()) { }

    public CsvDataParser(ApplicationSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public LargeFileWarningCallback? OnLargeFileWarning { get; set; }

    public async Task<PlotDataModel> ParseAsync(string filePath, CsvImportOptions options, CancellationToken cancellationToken = default)
    {
        options.ApplySettingsIfUnset(_settings);

        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForLoad(filePath);

        // SECURITY: Check file size before reading to prevent resource exhaustion (CWE-400)
        await ValidateFileSizeAsync(filePath, options).ConfigureAwait(false);

        var plotData = new PlotDataModel
        {
            SourceName = Path.GetFileName(filePath),
            SourcePath = filePath
        };

        await Task.Run(() =>
        {
            plotData.Data = ParseToDataTable(filePath, options, cancellationToken);
        }, cancellationToken).ConfigureAwait(false);

        return plotData;
    }

    private const int TypeSampleRowCap = 100;

    /// <summary>
    /// Two-pass streaming parse: a bounded sample (<see cref="TypeSampleRowCap"/> rows) drives
    /// column-type detection; the remainder of the file is streamed straight into the
    /// DataTable so peak raw-row retention stays bounded regardless of file size.
    /// </summary>
    /// <remarks>
    /// Honors <see cref="CsvImportOptions.HeaderLine"/>, <see cref="CsvImportOptions.UnitLine"/>,
    /// and <see cref="CsvImportOptions.DataStartLine"/> by manually advancing the underlying
    /// <see cref="StreamReader"/> past the prelude, then handing the remainder to
    /// <see cref="CsvReader"/> with <c>HasHeaderRecord = false</c> for data rows. Comment lines
    /// (starting with <c>#</c>) and blank lines count as physical lines for selector purposes
    /// so user-facing line numbers match what the preview shows.
    /// </remarks>
    private static DataTable ParseToDataTable(string filePath, CsvImportOptions options, CancellationToken cancellationToken = default)
    {
        ValidateLineSelectors(options);

        using var reader = new StreamReader(filePath, options.Encoding);

        string? headerLineText = null;
        string? unitLineText = null;
        int currentLine = 0;
        int dataStart = ResolveDataStartLine(options);

        // Phase 1: walk the file line-by-line until we reach the data-start line, capturing
        // header / unit lines along the way. Bounded by dataStart - 1, so does not stream the
        // whole file.
        while (currentLine < dataStart - 1)
        {
            var line = reader.ReadLine();
            if (line is null) break; // file shorter than dataStart -> empty data set
            currentLine++;

            if (currentLine == options.HeaderLine) headerLineText = line;
            if (options.UnitLine > 0 && currentLine == options.UnitLine) unitLineText = line;
        }

        var dataConfig = CreateConfiguration(options);
        using var csv = new CsvReader(reader, dataConfig);

        string[] columnNames = BuildColumnNames(headerLineText, unitLineText, options);

        if (columnNames.Length == 0)
        {
            // No columns determined yet — peek the first data row and synthesize names.
            if (!csv.Read())
                return new DataTable();
            int count = csv.Parser.Count;
            EnforceColumnCount(count, options);
            columnNames = new string[count];
            for (int i = 0; i < count; i++) columnNames[i] = $"Column{i + 1}";
            columnNames = InputValidator.MakeUniqueColumnNames(columnNames);

            int columnCount0 = columnNames.Length;
            var sample0 = new List<string[]> { ReadCurrentRow(csv, columnCount0) };
            return BuildAndFillTable(csv, sample0, columnNames, options, cancellationToken);
        }

        return BuildAndFillTable(csv, new List<string[]>(TypeSampleRowCap), columnNames, options, cancellationToken);
    }

    private static int ResolveDataStartLine(CsvImportOptions options)
    {
        // Defensive lower bound: must be at least 1, and must be after both header & unit lines.
        int minStart = 1;
        if (options.HeaderLine > 0) minStart = Math.Max(minStart, options.HeaderLine + 1);
        if (options.UnitLine > 0) minStart = Math.Max(minStart, options.UnitLine + 1);
        return Math.Max(options.DataStartLine, minStart);
    }

    private static void ValidateLineSelectors(CsvImportOptions options)
    {
        if (options.HeaderLine < 0)
            throw new InvalidOperationException("HeaderLine must be >= 0 (0 = no header).");
        if (options.UnitLine < 0)
            throw new InvalidOperationException("UnitLine must be >= 0 (0 = no unit line).");
        if (options.DataStartLine < 1)
            throw new InvalidOperationException("DataStartLine must be >= 1.");
        if (options.HeaderLine > 0 && options.HeaderLine == options.UnitLine)
            throw new InvalidOperationException("HeaderLine and UnitLine must differ when both are set.");
        if (options.HeaderLine > 0 && options.DataStartLine <= options.HeaderLine)
            throw new InvalidOperationException("DataStartLine must be greater than HeaderLine.");
        if (options.UnitLine > 0 && options.DataStartLine <= options.UnitLine)
            throw new InvalidOperationException("DataStartLine must be greater than UnitLine.");
    }

    private static string[] BuildColumnNames(string? headerLineText, string? unitLineText, CsvImportOptions options)
    {
        if (headerLineText is null && unitLineText is null)
            return Array.Empty<string>();

        var headerCells = headerLineText is null ? Array.Empty<string>() : SplitDelimitedLine(headerLineText, options.Delimiter);
        var unitCells = unitLineText is null ? Array.Empty<string>() : SplitDelimitedLine(unitLineText, options.Delimiter);

        int columnCount = Math.Max(headerCells.Length, unitCells.Length);
        EnforceColumnCount(columnCount, options);

        var names = new string[columnCount];
        for (int i = 0; i < columnCount; i++)
        {
            string rawHeader = i < headerCells.Length ? headerCells[i].Trim() : string.Empty;
            string headerSan = string.IsNullOrWhiteSpace(rawHeader)
                ? $"Column{i + 1}"
                : InputValidator.SanitizeColumnName(rawHeader);

            string rawUnit = i < unitCells.Length ? unitCells[i].Trim() : string.Empty;
            // Unit may contain characters the column-name sanitizer would strip (e.g. parens,
            // slashes). Strip control chars only so the user's unit text survives.
            string cleanedUnit = rawUnit.Length == 0 ? string.Empty : SanitizeUnit(rawUnit);

            names[i] = cleanedUnit.Length == 0
                ? headerSan
                : $"{headerSan} ({cleanedUnit})";
        }

        return InputValidator.MakeUniqueColumnNames(names);
    }

    private static string[] SplitDelimitedLine(string line, string delimiter)
    {
        // Lightweight split honoring CSV-style quoted fields ("a,b" stays one field). We don't
        // need full CsvHelper power here — header / unit lines aren't expected to contain
        // embedded newlines.
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        char delim = delimiter.Length > 0 ? delimiter[0] : ',';
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == delim)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    private static string SanitizeUnit(string unit)
    {
        var sb = new StringBuilder(unit.Length);
        foreach (var c in unit)
        {
            if (!char.IsControl(c)) sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    private static void EnforceColumnCount(int columnCount, CsvImportOptions options)
    {
        if (columnCount > options.MaxColumnCount)
            throw new InvalidOperationException(
                $"Column count ({columnCount}) exceeds maximum allowed ({options.MaxColumnCount})");
    }

    private static DataTable BuildAndFillTable(
        CsvReader csv,
        List<string[]> sample,
        string[] columnNames,
        CsvImportOptions options,
        CancellationToken cancellationToken = default)
    {
        int columnCount = columnNames.Length;
        int rowCount = 0;

        while (sample.Count < TypeSampleRowCap && csv.Read())
        {
            sample.Add(ReadCurrentRow(csv, columnCount));
        }

        var dataTable = new DataTable();
        for (int i = 0; i < columnCount; i++)
        {
            var type = DetectColumnType(sample, i, options.Culture);
            dataTable.Columns.Add(columnNames[i], type);
        }

        dataTable.BeginLoadData();
        try
        {
            // Throttle per-column conversion-error logging to one entry per column to avoid
            // 10M-row × N-column log floods when a column is mis-detected.
            var loggedConversionErrors = new bool[columnCount];

            foreach (var raw in sample)
                FillRow(dataTable, raw, columnCount, options.Culture, columnNames, ref rowCount, options.MaxRowCount, loggedConversionErrors);

            var streamBuffer = new string[columnCount];
            while (csv.Read())
            {
                // Check periodically (not per-row) so a cancelled import of a huge file stops
                // promptly without adding measurable overhead to the hot loop.
                if ((rowCount & 0x3FFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                int fieldCount = Math.Min(csv.Parser.Count, columnCount);
                for (int i = 0; i < fieldCount; i++) streamBuffer[i] = csv.GetField(i) ?? string.Empty;
                for (int i = fieldCount; i < columnCount; i++) streamBuffer[i] = string.Empty;
                FillRow(dataTable, streamBuffer, columnCount, options.Culture, columnNames, ref rowCount, options.MaxRowCount, loggedConversionErrors);
            }
        }
        finally
        {
            dataTable.EndLoadData();
        }

        return dataTable;
    }

    private static string[] ReadCurrentRow(CsvReader csv, int columnCount)
    {
        var row = new string[columnCount];
        int fieldCount = Math.Min(csv.Parser.Count, columnCount);
        for (int i = 0; i < fieldCount; i++) row[i] = csv.GetField(i) ?? string.Empty;
        for (int i = fieldCount; i < columnCount; i++) row[i] = string.Empty;
        return row;
    }

    private static void FillRow(
        DataTable dataTable,
        string[] raw,
        int columnCount,
        CultureInfo culture,
        string[] columnNames,
        ref int rowCount,
        int maxRowCount,
        bool[] loggedConversionErrors)
    {
        if (++rowCount > maxRowCount)
            throw new InvalidOperationException(
                $"Row count exceeds maximum allowed ({maxRowCount})");

        var dataRow = dataTable.NewRow();
        for (int i = 0; i < columnCount; i++)
        {
            var value = raw[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                dataRow[i] = DBNull.Value;
                continue;
            }

            // Fast TryParse path for common numeric/date/bool types — avoids Convert.ChangeType's
            // boxing + exception throw on bad data. Fall through to ChangeType for unusual types
            // so the slow path still serves anything we haven't special-cased.
            var targetType = dataTable.Columns[i].DataType;
            object? parsed = null;
            bool fastHandled = true;
            bool fastSucceeded = false;

            if (targetType == typeof(double))
            {
                if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var d))
                { parsed = d; fastSucceeded = true; }
            }
            else if (targetType == typeof(int))
            {
                if (int.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, culture, out var n))
                { parsed = n; fastSucceeded = true; }
            }
            else if (targetType == typeof(long))
            {
                if (long.TryParse(value, NumberStyles.Integer | NumberStyles.AllowThousands, culture, out var n))
                { parsed = n; fastSucceeded = true; }
            }
            else if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(value, culture, DateTimeStyles.None, out var dt))
                { parsed = dt; fastSucceeded = true; }
            }
            else if (targetType == typeof(bool))
            {
                if (bool.TryParse(value, out var b))
                { parsed = b; fastSucceeded = true; }
            }
            else
            {
                fastHandled = false;
            }

            if (fastHandled)
            {
                if (fastSucceeded)
                {
                    dataRow[i] = parsed!;
                }
                else
                {
                    if (!loggedConversionErrors[i])
                    {
                        loggedConversionErrors[i] = true;
                        // Security baseline: never log row data / column names / file contents.
                        // Identify the column by index and report the target type only.
                        SafeErrorHandler.LogError(
                            new FormatException($"Value is not a valid {targetType.Name}"),
                            "converting CSV value (suppressing further errors for this column)",
                            $"Column index: {i}, Target Type: {targetType.Name}");
                    }
                    dataRow[i] = DBNull.Value;
                }
                continue;
            }

            // Slow path: unusual types (e.g. decimal, custom IConvertible) — keep Convert.ChangeType.
            try
            {
                dataRow[i] = Convert.ChangeType(value, targetType, culture);
            }
            catch (Exception ex)
            {
                if (!loggedConversionErrors[i])
                {
                    loggedConversionErrors[i] = true;
                    // Security baseline: never log row data / column names / file contents.
                    SafeErrorHandler.LogError(ex, "converting CSV value (suppressing further errors for this column)",
                        $"Column index: {i}, Target Type: {targetType.Name}");
                }
                dataRow[i] = DBNull.Value;
            }
        }
        dataTable.Rows.Add(dataRow);
    }

    /// <summary>
    /// Detect delimiter and parse text file
    /// </summary>
    public async Task<PlotDataModel> ParseTextAsync(string filePath, string? delimiter = null)
    {
        // SECURITY: Validate/normalize before DetectDelimiterAsync opens the file — that path reads
        // the raw file ahead of ParseAsync's own validation, the sole gap in the "all paths through
        // FilePathValidator" invariant (CWE-22). ParseAsync re-validating the normalized path is safe.
        filePath = FilePathValidator.ValidatePathForLoad(filePath);

        if (delimiter == null)
        {
            delimiter = await DetectDelimiterAsync(filePath);
        }

        var options = new CsvImportOptions
        {
            Delimiter = delimiter,
            HasHeader = true
        };

        return await ParseAsync(filePath, options);
    }

    /// <summary>
    /// Parse tab-delimited file
    /// </summary>
    public async Task<PlotDataModel> ParseTabAsync(string filePath)
    {
        var options = new CsvImportOptions
        {
            Delimiter = "\t",
            HasHeader = true
        };

        return await ParseAsync(filePath, options);
    }

    #region Private Helper Methods

    private async Task ValidateFileSizeAsync(string filePath, CsvImportOptions options)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSizeMB = fileInfo.Length / 1024.0 / 1024.0;

        if (_settings.ShowLargeFileWarnings &&
            fileInfo.Length > _settings.LargeFileWarningThresholdBytes)
        {
            var warningMessage = $"This file is {fileSizeMB:F1} MB, which may take some time to import and use significant memory.\n\n" +
                               $"Estimated memory usage: typically ~{fileSizeMB:F0}-{fileSizeMB * 3:F0} MB depending on column count\n\n" +
                               "Do you want to proceed?";

            if (OnLargeFileWarning != null)
            {
                bool proceed = await OnLargeFileWarning(fileSizeMB, warningMessage).ConfigureAwait(false);
                if (!proceed)
                {
                    throw new OperationCanceledException("User cancelled import due to large file size");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Large file import requires user confirmation. " +
                    $"File size: {fileSizeMB:F1} MB exceeds warning threshold. " +
                    $"Please ensure OnLargeFileWarning callback is configured.");
            }
        }

        if (fileInfo.Length > options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File size ({fileSizeMB:F1} MB) exceeds maximum allowed size " +
                $"({options.MaxFileSizeBytes / 1024 / 1024} MB). " +
                $"You can increase this limit in application settings.");
        }
    }

    private static CsvConfiguration CreateConfiguration(CsvImportOptions options)
    {
        return new CsvConfiguration(options.Culture)
        {
            Delimiter = options.Delimiter,
            HasHeaderRecord = false, // Caller handles header lines manually via line selectors.
            MissingFieldFound = null, // Ignore missing fields
            BadDataFound = null, // Ignore bad data
            TrimOptions = TrimOptions.Trim,
            AllowComments = options.AllowComments,
            Comment = '#',
            IgnoreBlankLines = true
        };
    }

    private static Type DetectColumnType(List<string[]> rawRows, int columnIndex, CultureInfo culture)
    {
        int sampleSize = Math.Min(100, rawRows.Count);
        if (sampleSize == 0) return typeof(string);

        bool canBeDouble = true, canBeDateTime = true, canBeBool = true;
        int nonEmptyCount = 0;

        for (int i = 0; i < sampleSize; i++)
        {
            var value = rawRows[i][columnIndex];
            if (string.IsNullOrWhiteSpace(value)) continue;
            nonEmptyCount++;

            if (canBeDouble && !double.TryParse(value, NumberStyles.Any, culture, out _)) canBeDouble = false;
            if (canBeDateTime && !DateTime.TryParse(value, culture, DateTimeStyles.None, out _)) canBeDateTime = false;
            if (canBeBool && !bool.TryParse(value, out _)) canBeBool = false;

            if (!canBeDouble && !canBeDateTime && !canBeBool) return typeof(string);
        }

        if (nonEmptyCount == 0) return typeof(string);
        // Never infer int: a column that is all-integer in the first `sampleSize` rows but
        // fractional later (a channel that sits at 0 during startup) would silently NaN out its
        // later values under int.TryParse. Widen all numerics to double — GetColumnData converts
        // to double[] anyway, so there is no downstream benefit to int (review C3).
        if (canBeDouble) return typeof(double);
        if (canBeDateTime) return typeof(DateTime);
        if (canBeBool) return typeof(bool);
        return typeof(string);
    }

    private static async Task<string> DetectDelimiterAsync(string filePath)
    {
        string? firstLine;
        using (var reader = new StreamReader(filePath))
        {
            firstLine = await reader.ReadLineAsync().ConfigureAwait(false);
        }

        if (string.IsNullOrEmpty(firstLine))
            throw new InvalidDataException("File is empty");

        var delimiters = new[] { "\t", ",", ";", "|", " " };
        string? bestDelimiter = null;
        int maxColumns = 0;

        foreach (var delimiter in delimiters)
        {
            var columns = firstLine.Split(delimiter).Length;
            if (columns > maxColumns)
            {
                maxColumns = columns;
                bestDelimiter = delimiter;
            }
        }

        if (bestDelimiter == null || maxColumns < 2)
            throw new InvalidDataException("Could not detect delimiter in file");

        return bestDelimiter;
    }

    #endregion
}

/// <summary>
/// Interface for CSV data parser
/// </summary>
public interface ICsvDataParser
{
    /// <summary>
    /// Callback for large file warnings
    /// </summary>
    LargeFileWarningCallback? OnLargeFileWarning { get; set; }

    /// <summary>
    /// Parse CSV file with custom options
    /// </summary>
    Task<PlotDataModel> ParseAsync(string filePath, CsvImportOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse tab-delimited file
    /// </summary>
    Task<PlotDataModel> ParseTabAsync(string filePath);

    /// <summary>
    /// Detect delimiter and parse text file
    /// </summary>
    Task<PlotDataModel> ParseTextAsync(string filePath, string? delimiter = null);
}

/// <summary>
/// Options for CSV import
/// </summary>
public class CsvImportOptions
{
    /// <summary>
    /// Column delimiter (default: comma)
    /// </summary>
    public string Delimiter { get; set; } = ",";

    private bool _hasHeader = true;

    /// <summary>
    /// Convenience switch: when false, sets <see cref="HeaderLine"/> = 0 and
    /// <see cref="DataStartLine"/> = 1 (no header, data on line 1). When true,
    /// uses the explicit <see cref="HeaderLine"/> / <see cref="DataStartLine"/>
    /// values. Prefer setting those directly.
    /// </summary>
    public bool HasHeader
    {
        get => _hasHeader;
        set
        {
            _hasHeader = value;
            if (!value)
            {
                HeaderLine = 0;
                DataStartLine = 1;
            }
        }
    }

    /// <summary>
    /// Text encoding (default: UTF-8)
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    /// <summary>
    /// Whether to allow comments in the file (lines starting with #)
    /// </summary>
    public bool AllowComments { get; set; } = true;

    /// <summary>
    /// Culture for number parsing
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;

    /// <summary>
    /// 1-based line number containing column names. 0 means no header row;
    /// columns are auto-named Column1, Column2, ...
    /// </summary>
    public int HeaderLine { get; set; } = 1;

    /// <summary>
    /// 1-based line number containing units. 0 means no unit row. When > 0,
    /// each unit cell is concatenated to its column name as "Header (Unit)".
    /// </summary>
    public int UnitLine { get; set; }

    /// <summary>
    /// 1-based line number of the first data row. Must be greater than
    /// <see cref="HeaderLine"/> and <see cref="UnitLine"/>.
    /// </summary>
    public int DataStartLine { get; set; } = 2;

    /// <summary>
    /// Maximum file size in bytes. If 0, populated from parser settings at parse time.
    /// </summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Maximum number of rows. If 0, populated from parser settings at parse time.
    /// </summary>
    public int MaxRowCount { get; set; }

    /// <summary>
    /// Maximum number of columns. If 0, populated from parser settings at parse time.
    /// </summary>
    public int MaxColumnCount { get; set; }

    internal void ApplySettingsIfUnset(ApplicationSettings settings)
    {
        if (MaxFileSizeBytes <= 0) MaxFileSizeBytes = settings.MaxFileSizeBytes;
        if (MaxRowCount <= 0) MaxRowCount = settings.MaxRowCount;
        if (MaxColumnCount <= 0) MaxColumnCount = settings.MaxColumnCount;
    }
}
