using System.Data;
using System.Globalization;

namespace DatPlotX.Helpers;

/// <summary>
/// Detects whether a CSV column carries boolean-style values (0/1, true/false, on/off).
/// Compact Plot Surface uses this to size the curve's Y band: ~1 grid row for booleans, ~3 for analog.
/// </summary>
public static class BooleanColumnDetector
{
    /// <summary>Maximum rows scanned to keep this O(N) for huge files but still reliable.</summary>
    private const int MaxRowsToScan = 2000;

    /// <summary>
    /// Returns true if the column appears to hold only boolean-like values.
    /// Native bool columns are always boolean. Numeric columns qualify only if every non-null
    /// value is 0 or 1. String columns qualify if every value parses to a known boolean token.
    /// </summary>
    /// <param name="culture">
    /// Culture used to parse decimal string tokens (e.g. a <c>de-DE</c> column storing
    /// <c>"0,0"</c>/<c>"1,0"</c>). Pass the project's <c>options.Culture</c> to honor the
    /// "always pass an explicit <c>IFormatProvider</c>" rule; defaults to
    /// <see cref="CultureInfo.InvariantCulture"/> (review D1).
    /// </param>
    public static bool IsBooleanColumn(DataTable table, string columnName, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (string.IsNullOrEmpty(columnName) || !table.Columns.Contains(columnName))
            return false;

        var column = table.Columns[columnName]!;

        if (column.DataType == typeof(bool))
            return true;

        int rowsToScan = Math.Min(table.Rows.Count, MaxRowsToScan);
        if (rowsToScan == 0)
            return false;

        culture ??= CultureInfo.InvariantCulture;
        bool sawAny = false;
        for (int i = 0; i < rowsToScan; i++)
        {
            var raw = table.Rows[i][column];
            if (raw == DBNull.Value || raw is null)
                continue;
            if (!IsBooleanValue(raw, culture))
                return false;
            sawAny = true;
        }
        return sawAny;
    }

    /// <summary>
    /// Fast-path overload for already-converted numeric column data (e.g. the cached array
    /// from <see cref="Models.PlotDataModel.GetColumnData(string)"/>). Skips boxing.
    /// NaN entries are treated as nulls. Returns false if every value is NaN.
    /// </summary>
    public static bool IsBooleanColumn(ReadOnlySpan<double> values)
    {
        int limit = Math.Min(values.Length, MaxRowsToScan);
        if (limit == 0) return false;

        bool sawAny = false;
        for (int i = 0; i < limit; i++)
        {
            double v = values[i];
            if (double.IsNaN(v)) continue;
            if (v != 0d && v != 1d) return false;
            sawAny = true;
        }
        return sawAny;
    }

    private static bool IsBooleanValue(object value, CultureInfo culture) => value switch
    {
        bool => true,
        sbyte sb => sb is 0 or 1,
        byte b => b is 0 or 1,
        short s => s is 0 or 1,
        ushort us => us is 0 or 1,
        int i => i is 0 or 1,
        uint ui => ui is 0u or 1u,
        long l => l is 0L or 1L,
        ulong ul => ul is 0UL or 1UL,
        float f => f is 0f or 1f,
        double d => d is 0d or 1d,
        decimal m => m == 0m || m == 1m,
        string str => IsBooleanString(str, culture),
        _ => false,
    };

    private static bool IsBooleanString(string value, CultureInfo culture)
    {
        var trimmed = value.Trim();
        if (trimmed.Length == 0) return true;
        if (trimmed is "0" or "1") return true;
        if (bool.TryParse(trimmed, out _)) return true;
        if (double.TryParse(trimmed, NumberStyles.Float, culture, out var d)
            && (d == 0d || d == 1d))
            return true;
        return string.Equals(trimmed, "yes", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "no", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "on", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "off", StringComparison.OrdinalIgnoreCase);
    }
}
