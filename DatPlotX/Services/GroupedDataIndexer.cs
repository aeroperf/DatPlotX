using DatPlotX.Models;
using System.Globalization;

namespace DatPlotX.Services;

/// <inheritdoc />
/// <remarks>
/// Float equality uses a relative tolerance: <c>|a-b| &lt;= ε · max(|a|,|b|,1)</c> with ε = 1e-9.
/// Grouping uses a long key built by quantizing the value to ~9 significant digits (see
/// <c>ToGroupKey</c>) so two values that compare equal under the relative tolerance hash to the
/// same bucket at any magnitude — including large-magnitude columns like epoch-ms timestamps.
/// <see cref="double.NaN"/> values are skipped (they can never match a concrete selection and
/// would pollute group keys).
/// </remarks>
public sealed class GroupedDataIndexer : IGroupedDataIndexer
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// Significant digits preserved when quantizing a value to its bucket key. ~9 matches the
    /// relative <see cref="Epsilon"/> used by <see cref="ValuesEqual"/>, so values that compare
    /// equal under the relative tolerance land in the same bucket at any magnitude (review C2).
    /// </summary>
    private const int SignificantDigits = 9;

    private readonly ApplicationSettings _settings;

    public GroupedDataIndexer(ApplicationSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public double[] GetDistinctValues(PlotDataModel data, string columnName, out bool capped)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(columnName);

        capped = false;
        var column = data.GetColumnData(columnName);
        if (column.Length == 0)
            return Array.Empty<double>();

        var set = new HashSet<long>();
        var values = new List<double>();
        var cap = _settings.GroupedPlotMaxDistinctValues;

        for (int i = 0; i < column.Length; i++)
        {
            var v = column[i];
            if (double.IsNaN(v)) continue;
            var key = ToGroupKey(v);
            if (set.Add(key))
            {
                values.Add(v);
                if (values.Count > cap)
                {
                    capped = true;
                    break;
                }
            }
        }

        values.Sort();
        return values.ToArray();
    }

    public GroupedPlotProjection Project(PlotDataModel data, GroupedPlotConfig config)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrEmpty(config.XAxisColumn) || string.IsNullOrEmpty(config.YAxisColumn))
            return new GroupedPlotProjection(Array.Empty<GroupedPlotSeries>(), Truncated: false, TotalGroupCount: 0);

        var availableColumns = new HashSet<string>(data.ColumnNames, StringComparer.Ordinal);
        if (!availableColumns.Contains(config.XAxisColumn) || !availableColumns.Contains(config.YAxisColumn))
            return new GroupedPlotProjection(Array.Empty<GroupedPlotSeries>(), Truncated: false, TotalGroupCount: 0);

        var lockedInputs = new List<(GroupedInputParameter Input, double[] Column, double Value)>();
        var allInputs = new List<(GroupedInputParameter Input, double[] Column)>();
        foreach (var input in config.Inputs)
        {
            if (!availableColumns.Contains(input.ColumnName)) continue;
            var col = data.GetColumnData(input.ColumnName);
            if (input.SelectedValue is double v)
                lockedInputs.Add((input, col, v));
            else
                allInputs.Add((input, col));
        }

        var xCol = data.GetColumnData(config.XAxisColumn);
        var yCol = data.GetColumnData(config.YAxisColumn);
        int rowCount = data.RowCount;

        var groups = new Dictionary<GroupKey, GroupAccumulator>();

        for (int i = 0; i < rowCount; i++)
        {
            bool matched = true;
            for (int li = 0; li < lockedInputs.Count; li++)
            {
                var (_, col, target) = lockedInputs[li];
                if (!ValuesEqual(col[i], target)) { matched = false; break; }
            }
            if (!matched) continue;

            var x = xCol[i];
            var y = yCol[i];
            if (double.IsNaN(x) || double.IsNaN(y)) continue;

            var keyParts = new long[allInputs.Count];
            bool keyOk = true;
            for (int ai = 0; ai < allInputs.Count; ai++)
            {
                var v = allInputs[ai].Column[i];
                if (double.IsNaN(v)) { keyOk = false; break; }
                keyParts[ai] = ToGroupKey(v);
            }
            if (!keyOk) continue;

            var key = new GroupKey(keyParts);
            if (!groups.TryGetValue(key, out var acc))
            {
                var labelValues = new double[allInputs.Count];
                for (int ai = 0; ai < allInputs.Count; ai++)
                    labelValues[ai] = allInputs[ai].Column[i];
                acc = new GroupAccumulator(labelValues);
                groups[key] = acc;
            }
            acc.X.Add(x);
            acc.Y.Add(y);
        }

        int totalGroups = groups.Count;
        if (totalGroups == 0)
            return new GroupedPlotProjection(Array.Empty<GroupedPlotSeries>(), Truncated: false, TotalGroupCount: 0);

        // Sort groups by their All-input values left-to-right so the legend order is deterministic.
        var ordered = groups
            .Select(kvp => (Key: kvp.Key, Values: kvp.Value))
            .OrderBy(g => g.Values, GroupValueComparer.Instance)
            .ToList();

        int cap = _settings.GroupedPlotMaxLines;
        bool truncated = totalGroups > cap;
        var take = truncated ? cap : totalGroups;

        var series = new List<GroupedPlotSeries>(take);
        for (int g = 0; g < take; g++)
        {
            var acc = ordered[g].Values;
            var pairs = new (double X, double Y)[acc.X.Count];
            for (int p = 0; p < pairs.Length; p++)
                pairs[p] = (acc.X[p], acc.Y[p]);
            Array.Sort(pairs, static (a, b) => a.X.CompareTo(b.X));

            var xs = new double[pairs.Length];
            var ys = new double[pairs.Length];
            for (int p = 0; p < pairs.Length; p++)
            {
                xs[p] = pairs[p].X;
                ys[p] = pairs[p].Y;
            }

            var label = BuildLabel(allInputs, acc.LabelValues);
            series.Add(new GroupedPlotSeries(label, xs, ys));
        }

        return new GroupedPlotProjection(series, truncated, totalGroups);
    }

    private static string BuildLabel(
        List<(GroupedInputParameter Input, double[] Column)> allInputs,
        double[] labelValues)
    {
        if (allInputs.Count == 0) return string.Empty;
        var parts = new string[allInputs.Count];
        for (int i = 0; i < allInputs.Count; i++)
        {
            var input = allInputs[i].Input;
            var label = string.IsNullOrEmpty(input.DisplayLabel) ? input.ColumnName : input.DisplayLabel;
            var v = labelValues[i];
            var formatted = string.IsNullOrEmpty(input.Format)
                ? v.ToString(CultureInfo.InvariantCulture)
                : v.ToString(input.Format, CultureInfo.InvariantCulture);
            parts[i] = $"{label}={formatted}{input.UnitSuffix}";
        }
        return string.Join(" • ", parts);
    }

    /// <summary>
    /// Bucket key that stays consistent with <see cref="ValuesEqual"/>'s <em>relative</em> tolerance
    /// and never overflows. The old <c>(long)Math.Round(v / 1e-9)</c> overflowed <see cref="long"/>
    /// for |v| &gt; ~9.2e9 (epoch-ms timestamps, serial numbers, Julian dates) — an unchecked
    /// double→long of an out-of-range value collapses every large value into one bucket, so the
    /// whole column grouped as a single line (review C2). Here we round to ~9 significant digits
    /// (matching ε = 1e-9 relative) and hash the rounded double's bit pattern.
    /// </summary>
    private static long ToGroupKey(double value)
    {
        if (value == 0.0 || double.IsNaN(value) || double.IsInfinity(value))
            return BitConverter.DoubleToInt64Bits(value);

        // Round to SignificantDigits sig-figs regardless of magnitude: scale so the value's
        // leading digit sits at the SignificantDigits place, round to an integer, and rescale.
        int exp = (int)Math.Floor(Math.Log10(Math.Abs(value)));
        double scale = Math.Pow(10, exp - (SignificantDigits - 1));
        double rounded = Math.Round(value / scale) * scale;
        return BitConverter.DoubleToInt64Bits(rounded);
    }

    private static bool ValuesEqual(double a, double b)
    {
        if (double.IsNaN(a) || double.IsNaN(b)) return false;
        var scale = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), 1.0);
        return Math.Abs(a - b) <= Epsilon * scale;
    }

    private sealed class GroupAccumulator
    {
        public double[] LabelValues { get; }
        public List<double> X { get; } = new();
        public List<double> Y { get; } = new();

        public GroupAccumulator(double[] labelValues)
        {
            LabelValues = labelValues;
        }
    }

    private readonly struct GroupKey : IEquatable<GroupKey>
    {
        private readonly long[] _parts;
        private readonly int _hash;

        public GroupKey(long[] parts)
        {
            _parts = parts;
            var hc = new HashCode();
            for (int i = 0; i < parts.Length; i++) hc.Add(parts[i]);
            _hash = hc.ToHashCode();
        }

        public bool Equals(GroupKey other)
        {
            if (_parts.Length != other._parts.Length) return false;
            for (int i = 0; i < _parts.Length; i++)
                if (_parts[i] != other._parts[i]) return false;
            return true;
        }

        public override bool Equals(object? obj) => obj is GroupKey g && Equals(g);
        public override int GetHashCode() => _hash;
    }

    private sealed class GroupValueComparer : IComparer<GroupAccumulator>
    {
        public static readonly GroupValueComparer Instance = new();
        public int Compare(GroupAccumulator? x, GroupAccumulator? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var min = Math.Min(x.LabelValues.Length, y.LabelValues.Length);
            for (int i = 0; i < min; i++)
            {
                var c = x.LabelValues[i].CompareTo(y.LabelValues[i]);
                if (c != 0) return c;
            }
            return x.LabelValues.Length.CompareTo(y.LabelValues.Length);
        }
    }
}
