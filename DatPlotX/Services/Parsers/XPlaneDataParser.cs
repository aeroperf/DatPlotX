using CsvHelper;
using DatPlotX.Helpers;
using DatPlotX.Models;
using System.Globalization;
using System.Text;

namespace DatPlotX.Services.Parsers;

/// <summary>
/// Parser for X-Plane data format
/// Handles X-Plane-specific format with pipe delimiters and comma/semicolon parameter-unit separators
/// </summary>
public class XPlaneDataParser : IXPlaneDataParser
{
    private readonly ICsvDataParser _csvParser;
    private readonly ApplicationSettings _settings;

    public XPlaneDataParser(ICsvDataParser csvParser) : this(csvParser, new ApplicationSettings()) { }

    public XPlaneDataParser(ICsvDataParser csvParser, ApplicationSettings settings)
    {
        _csvParser = csvParser ?? throw new ArgumentNullException(nameof(csvParser));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Callback for large file warnings (forwarded to CSV parser)
    /// </summary>
    public LargeFileWarningCallback? OnLargeFileWarning
    {
        get => _csvParser.OnLargeFileWarning;
        set => _csvParser.OnLargeFileWarning = value;
    }

    /// <summary>
    /// Parse X-Plane format file
    /// Converts X-Plane format to standard CSV, then parses
    /// </summary>
    public async Task<PlotDataModel> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        string? tempFilePath = null;
        try
        {
            tempFilePath = await PreProcessXPlaneFileAsync(filePath, cancellationToken);

            // The pre-processed temp file is laid out as: line 1 = parameter names,
            // line 2 = units, line 3+ = data. The CSV parser's unit-line path sanitizes the
            // name and unit independently and recombines them as "name (unit)" — so the parens
            // survive (the column-name sanitizer would otherwise strip them) and the shared
            // UnitHeaderParser recognizes the unit downstream.
            var csvImportOptions = new CsvImportOptions
            {
                Delimiter = ",",
                Culture = new CultureInfo("en-US"),
                HeaderLine = 1,
                UnitLine = 2,
                DataStartLine = 3
            };

            return await _csvParser.ParseAsync(tempFilePath, csvImportOptions, cancellationToken);
        }
        finally
        {
            // SECURITY: Ensure temp file cleanup even on errors (CWE-459)
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Log but don't throw - cleanup failure shouldn't break the app
                    System.Diagnostics.Debug.WriteLine($"Warning: Failed to delete temporary file: {tempFilePath}");
                }
            }
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Pre-process X-Plane data file format into standard CSV
    /// X-Plane format uses pipe (|) delimiters and comma/semicolon (,;) to separate parameter names from units
    /// Formats column headers as "parameter (unit)" with the X-Plane alignment-padding underscores
    /// stripped, so the shared <see cref="Units.UnitHeaderParser"/> picks the unit up downstream.
    /// </summary>
    private async Task<string> PreProcessXPlaneFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForLoad(filePath);

        // SECURITY: Check file size before reading any content (CWE-400).
        await ValidateSourceFileSizeAsync(filePath).ConfigureAwait(false);

        // First pass: collect header lines only (a handful of rows in X-Plane format).
        var headerLines = new List<string>();
        {
            using var headerReader = new StreamReader(filePath);
            string? line;
            bool headerFound = false;
            long headerLinesRead = 0;
            while ((line = await headerReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                // Bounded in a well-formed file (headers are a handful of rows), but a malformed
                // file with no data-start marker would otherwise scan to EOF unobservably.
                if ((++headerLinesRead & 0x3FFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                if (line.Contains("_time") || (headerFound && line.Trim().StartsWith("windo", StringComparison.Ordinal)))
                {
                    headerLines.Add(line);
                    headerFound = true;
                }
                else if (headerFound && !string.IsNullOrWhiteSpace(line))
                {
                    break; // headers exhausted, data begins
                }
            }
        }

        // Split each header token into a (parameter, unit) pair, stripping the X-Plane
        // alignment-padding underscores. Parameter names and units are written to separate
        // rows so the CSV parser's unit-line path can recombine them into "name (unit)".
        var parameterNames = new List<string>();
        var units = new List<string>();
        foreach (var headerLine in headerLines)
        {
            var columns = headerLine.Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.Trim())
                .Where(h => !string.IsNullOrEmpty(h));

            foreach (var column in columns)
            {
                var (name, unit) = SplitXPlaneHeader(column);
                parameterNames.Add(name);
                units.Add(unit);
            }
        }

        // X-Plane can emit two columns with an identical name AND unit (e.g. the deice block
        // carries "deice,__AOA" twice). Disambiguate the *parameter* names here — keyed on the
        // (name, unit) pair so genuinely-different-unit columns like "Vtrue (ktas)" / "Vtrue
        // (ktgs)" are left untouched — so the disambiguation suffix lands on the name instead of
        // after the unit parens, where it would hide the unit from UnitHeaderParser.
        DisambiguateParameterNames(parameterNames, units);

        // SECURITY: Create temp file with unique GUID name to prevent collisions (CWE-377)
        var tempFilePath = Path.Combine(
            Path.GetTempPath(),
            $"datplot_xplane_{Guid.NewGuid():N}.csv");

        try
        {
            int headerCount = headerLines.Count;
            // Use CsvWriter so headers / values containing , " \r \n get correctly escaped.
            using var writer = new StreamWriter(tempFilePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Row 1: parameter names. (Duplicate-name disambiguation is handled by the CSV
            // parser once names and units are recombined.)
            foreach (var name in parameterNames) csv.WriteField(name);
            await csv.NextRecordAsync().ConfigureAwait(false);

            // Row 2: units (blank where a parameter has none).
            foreach (var unit in units) csv.WriteField(unit);
            await csv.NextRecordAsync().ConfigureAwait(false);

            // Second pass: stream data lines, group every headerCount lines into one CSV row.
            using var sourceReader = new StreamReader(filePath);
            string? src;
            bool headerSkipped = false;
            var rowBuffer = new List<string>();
            int rowsInBuffer = 0;
            long linesRead = 0;

            while ((src = await sourceReader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                // Poll cancellation periodically so a cancel during the full-file rewrite of a large
                // X-Plane export actually stops the work (matches CsvDataParser's row-loop cadence).
                if ((++linesRead & 0x3FFF) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                if (!headerSkipped)
                {
                    // Skip past the header block in the second pass.
                    if (src.Contains("_time") || src.Trim().StartsWith("windo", StringComparison.Ordinal))
                        continue;
                    if (string.IsNullOrWhiteSpace(src)) continue;
                    headerSkipped = true;
                }
                if (string.IsNullOrWhiteSpace(src)) continue;

                // Manual span-split on '|', trimming each field, keeping non-empty ones. Replaces
                // the prior Split+Select(Trim)+Where(...) — that allocated an array plus two LINQ
                // iterators and a trimmed substring per field, once per source line (millions on a
                // large export). Here the only allocation is the substring we actually keep (review C2).
                var line = src.AsSpan();
                while (!line.IsEmpty)
                {
                    int bar = line.IndexOf('|');
                    ReadOnlySpan<char> field = bar < 0 ? line : line[..bar];
                    field = field.Trim();
                    if (!field.IsEmpty)
                        rowBuffer.Add(field.ToString());
                    if (bar < 0) break;
                    line = line[(bar + 1)..];
                }
                rowsInBuffer++;

                if (headerCount <= 0 || rowsInBuffer == headerCount)
                {
                    if (rowBuffer.Count > 0)
                    {
                        foreach (var v in rowBuffer) csv.WriteField(v);
                        await csv.NextRecordAsync().ConfigureAwait(false);
                    }
                    rowBuffer.Clear();
                    rowsInBuffer = 0;
                }
            }

            if (rowBuffer.Count > 0)
            {
                foreach (var v in rowBuffer) csv.WriteField(v);
                await csv.NextRecordAsync().ConfigureAwait(false);
            }

            return tempFilePath;
        }
        catch
        {
            // SECURITY: Clean up temp file on error (CWE-459)
            try
            {
                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch
            {
                // Ignore cleanup errors during exception handling
            }
            throw;
        }
    }

    /// <summary>
    /// Splits a raw X-Plane header token such as <c>_real,_time</c> or <c>thrst,_1,lb</c> into a
    /// clean (parameter, unit) pair. X-Plane right/left-pads every field to a fixed width with
    /// underscores, so those are stripped from both the parameter name and the unit. The unit is the
    /// final comma/semicolon-separated token; everything before it is the parameter name (any
    /// interior separators flattened to spaces, e.g. <c>thrst,_1,lb</c> → (<c>thrst 1</c>, <c>lb</c>)).
    /// Returns an empty unit when no separator is present.
    /// </summary>
    private static (string Name, string Unit) SplitXPlaneHeader(string column)
    {
        // Pick the separator the same way as the rest of the format: semicolon wins when present.
        char? separator = column.Contains(';') ? ';' : (column.Contains(',') ? ',' : null);

        if (separator is null)
            return (StripPadding(column), string.Empty);

        int lastSep = column.LastIndexOf(separator.Value);
        var name = StripPadding(column.Substring(0, lastSep).Replace(';', ' ').Replace(',', ' '));
        var unit = StripPadding(column.Substring(lastSep + 1));
        return (name, unit);
    }

    /// <summary>
    /// Suffixes duplicate parameter names in place so that each (name, unit) pair is unique.
    /// The suffix is a space + counter (e.g. <c>deice</c> → <c>deice 2</c>) so it survives the
    /// column-name sanitizer and keeps the unit cleanly separated. Columns sharing a name but
    /// differing in unit are left untouched.
    /// </summary>
    private static void DisambiguateParameterNames(List<string> names, List<string> units)
    {
        // taken / counts are keyed on "name<sep>unit" — NUL can't appear in a parsed header,
        // so it unambiguously separates the name and unit halves of the key.
        const string sep = "\0";
        var taken = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < names.Count; i++)
        {
            var unit = units[i];
            var key = names[i] + sep + unit;
            if (taken.Add(key))
            {
                counts[key] = 1;
                continue;
            }

            int next = counts[key] + 1;
            string candidateName, candidateKey;
            do
            {
                candidateName = $"{names[i]} {next}";
                candidateKey = candidateName + sep + unit;
                next++;
            } while (!taken.Add(candidateKey));

            counts[key] = next - 1;
            counts[candidateKey] = 1;
            names[i] = candidateName;
        }
    }

    /// <summary>
    /// Removes X-Plane alignment-padding underscores and collapses any resulting double spaces.
    /// </summary>
    private static string StripPadding(string token)
    {
        // Single pass: drop padding underscores and collapse runs of whitespace to one space,
        // trimming leading/trailing. Replaces the prior O(n²) `while (Contains("  ")) Replace(...)`
        // (a fresh string + full rescan per pass); header-scoped so bounded, but no reason to be
        // quadratic (review C4).
        var sb = new StringBuilder(token.Length);
        bool pendingSpace = false;
        foreach (var c in token)
        {
            if (c == '_') continue;
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0; // suppress leading; coalesce interior runs
                continue;
            }
            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private async Task ValidateSourceFileSizeAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSizeMB = fileInfo.Length / 1024.0 / 1024.0;

        if (_settings.ShowLargeFileWarnings
            && fileInfo.Length > _settings.LargeFileWarningThresholdBytes
            && OnLargeFileWarning != null)
        {
            var message = $"This X-Plane file is {fileSizeMB:F1} MB. Preprocessing will read it line-by-line into a temporary CSV. Proceed?";
            bool proceed = await OnLargeFileWarning(fileSizeMB, message).ConfigureAwait(false);
            if (!proceed)
                throw new OperationCanceledException("User cancelled X-Plane import");
        }

        if (fileInfo.Length > _settings.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File size ({fileSizeMB:F1} MB) exceeds maximum allowed " +
                $"({_settings.MaxFileSizeBytes / 1024 / 1024} MB).");
        }
    }

    #endregion
}

/// <summary>
/// Interface for X-Plane data parser
/// </summary>
public interface IXPlaneDataParser
{
    /// <summary>
    /// Callback for large file warnings
    /// </summary>
    LargeFileWarningCallback? OnLargeFileWarning { get; set; }

    /// <summary>
    /// Parse X-Plane format file
    /// </summary>
    Task<PlotDataModel> ParseAsync(string filePath, CancellationToken cancellationToken = default);
}
