using DatPlotX.Helpers;
using System.Data;
using System.Globalization;

namespace DatPlotX.Models;

/// <summary>
/// Represents imported data ready for plotting
/// </summary>
public class PlotDataModel : IDisposable
{
    private DataTable _data = new();
    private Dictionary<string, double[]>? _columnCache;
    private bool _disposed;

    public string SourceName { get; set; } = string.Empty;

    public string? SourcePath { get; set; }

    public DataTable Data
    {
        get => _data;
        set
        {
            if (!ReferenceEquals(_data, value))
            {
                _data?.Dispose();
            }
            _data = value ?? new DataTable();
            _columnCache = null;
        }
    }

    public List<string> ColumnNames => Data.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();

    public int RowCount => Data.Rows.Count;

    public int ColumnCount => Data.Columns.Count;

    public DateTime ImportedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Get data for a specific column as a double[]. Result is cached by column name.
    /// </summary>
    /// <remarks>
    /// THREAD-AFFINITY: the lazy <see cref="_columnCache"/> init and its <c>Dictionary</c> mutation
    /// are not synchronized. This is safe <b>only</b> because every caller (including the analysis
    /// snapshot in <c>AnalysisService.ComputeAsync</c>) runs on the UI thread — the snapshot is taken
    /// on the calling/UI thread precisely so the off-thread compute never touches this cache. If a
    /// future path needs column data off the UI thread, either take the value on the UI thread first
    /// or add a lock here; a concurrent reader/writer can corrupt the <c>Dictionary</c> (review D2).
    /// </remarks>
    public double[] GetColumnData(string columnName)
    {
        if (!Data.Columns.Contains(columnName))
            throw new ArgumentException($"Column '{columnName}' not found in data");

        _columnCache ??= new Dictionary<string, double[]>();
        if (_columnCache.TryGetValue(columnName, out var cached))
            return cached;

        var column = Data.Columns[columnName]!;
        var rows = Data.Rows;
        int count = rows.Count;
        var result = new double[count];
        int columnIndex = column.Ordinal;
        var columnType = column.DataType;

        if (columnType == typeof(double))
        {
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                result[i] = row.IsNull(columnIndex) ? double.NaN : (double)row[columnIndex];
            }
        }
        else if (columnType == typeof(int))
        {
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                result[i] = row.IsNull(columnIndex) ? double.NaN : (int)row[columnIndex];
            }
        }
        else if (columnType == typeof(long))
        {
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                result[i] = row.IsNull(columnIndex) ? double.NaN : (long)row[columnIndex];
            }
        }
        else if (columnType == typeof(float))
        {
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                result[i] = row.IsNull(columnIndex) ? double.NaN : (float)row[columnIndex];
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                if (row.IsNull(columnIndex))
                {
                    result[i] = double.NaN;
                    continue;
                }
                try
                {
                    result[i] = Convert.ToDouble(row[columnIndex], CultureInfo.InvariantCulture);
                }
                catch (Exception ex)
                {
                    // Security baseline: never log column names / row data; index + type only.
                    SafeErrorHandler.LogError(ex, "converting value to double",
                        $"Column index: {columnIndex}, Row index: {i}, Type: {columnType.Name}");
                    result[i] = double.NaN;
                }
            }
        }

        _columnCache[columnName] = result;
        return result;
    }

    public double[] GetColumnData(int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= Data.Columns.Count)
            throw new ArgumentOutOfRangeException(nameof(columnIndex));

        return GetColumnData(Data.Columns[columnIndex].ColumnName);
    }

    /// <summary>
    /// Invalidates the cached column arrays. Call after mutating <see cref="Data"/> rows in place.
    /// </summary>
    public void InvalidateColumnCache() => _columnCache = null;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;
        if (disposing)
        {
            _data?.Dispose();
            _columnCache = null;
        }
        _disposed = true;
    }
}
