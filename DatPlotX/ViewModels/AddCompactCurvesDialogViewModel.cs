using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Helpers;
using DatPlotX.Models;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the two-stage Compact Plot "Add Curves" dialog.
/// Stage 1 = pick columns; Stage 2 = per-curve band/side/style config.
/// </summary>
public sealed partial class AddCompactCurvesDialogViewModel : ObservableObject
{
    private static readonly ImmutableArray<string> DefaultPalette = DefaultCurvePalette.Colors;

    private readonly DataTable? _sourceTable;
    private readonly PlotDataModel? _data;
    private readonly string _xColumn;
    private readonly int _existingCurveCount;

    public ObservableCollection<CompactColumnPick> AvailableColumns { get; } = new();
    public ObservableCollection<CompactCurveDraft> Drafts { get; } = new();

    [ObservableProperty]
    private int _stage = 1;

    [ObservableProperty]
    private bool _hasAnySelection;

    public AddCompactCurvesDialogViewModel(PlotDataModel? data, string xColumn, int existingCurveCount)
    {
        _data = data;
        _sourceTable = data?.Data;
        _xColumn = xColumn;
        _existingCurveCount = existingCurveCount;
        PopulateAvailableColumns();
    }

    /// <summary>Tests / designer ctor that accepts a raw <see cref="DataTable"/>; production
    /// uses the <see cref="PlotDataModel"/> overload so the cached column arrays can be reused.</summary>
    public AddCompactCurvesDialogViewModel(DataTable? table, string xColumn, int existingCurveCount)
    {
        _data = null;
        _sourceTable = table;
        _xColumn = xColumn;
        _existingCurveCount = existingCurveCount;
        PopulateAvailableColumns();
    }

    private void PopulateAvailableColumns()
    {
        if (_sourceTable is null) return;
        foreach (DataColumn col in _sourceTable.Columns)
        {
            if (string.Equals(col.ColumnName, _xColumn, StringComparison.Ordinal))
                continue;
            if (!IsNumericLike(col.DataType))
                continue;
            var pick = new CompactColumnPick { ColumnName = col.ColumnName };
            pick.PropertyChanged += (_, _) => RefreshHasSelection();
            AvailableColumns.Add(pick);
        }
    }

    private static bool IsNumericLike(Type t)
        => t == typeof(double) || t == typeof(float) || t == typeof(decimal)
           || t == typeof(int) || t == typeof(long) || t == typeof(short)
           || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort)
           || t == typeof(byte) || t == typeof(sbyte) || t == typeof(bool);

    private void RefreshHasSelection()
        => HasAnySelection = AvailableColumns.Any(c => c.IsSelected);

    /// <summary>Toggle <see cref="CompactColumnPick.IsVisible"/> on each column based on a name filter.</summary>
    public void ApplyColumnFilter(string? query)
    {
        var trimmed = (query ?? string.Empty).Trim();
        bool noFilter = trimmed.Length == 0;
        foreach (var pick in AvailableColumns)
        {
            pick.IsVisible = noFilter
                || pick.ColumnName.Contains(trimmed, StringComparison.OrdinalIgnoreCase);
        }
    }

    private bool DetectBoolean(string columnName)
    {
        if (_sourceTable is null) return false;

        // Fast path: numeric columns can reuse PlotDataModel's cached double[] (no boxing).
        // Native bool columns and string columns still need the DataTable scan.
        if (_data is not null && _sourceTable.Columns.Contains(columnName))
        {
            var col = _sourceTable.Columns[columnName]!;
            if (col.DataType == typeof(bool)) return true;
            if (col.DataType != typeof(string))
            {
                try
                {
                    var values = _data.GetColumnData(columnName);
                    return BooleanColumnDetector.IsBooleanColumn(values);
                }
                catch
                {
                    // fall through to DataTable scan
                }
            }
        }
        return BooleanColumnDetector.IsBooleanColumn(_sourceTable, columnName);
    }

    /// <summary>Build draft rows for stage 2 from currently checked columns.</summary>
    public void AdvanceToStage2()
    {
        Drafts.Clear();
        int globalIndex = _existingCurveCount;
        foreach (var pick in AvailableColumns.Where(c => c.IsSelected))
        {
            bool isBool = DetectBoolean(pick.ColumnName);
            var draft = new CompactCurveDraft
            {
                SourceColumn = pick.ColumnName,
                DisplayName = pick.ColumnName,
                Color = DefaultPalette[globalIndex % DefaultPalette.Length],
                AxisSide = (globalIndex % 2 == 0) ? AxisSide.Left : AxisSide.Right,
                IsBoolean = isBool,
                LineStyle = LineStyle.Solid,
                LineWidth = 1.5,
                AllowOverflow = true,
            };
            Drafts.Add(draft);
            globalIndex++;
        }
        Stage = 2;
    }

    public void BackToStage1() => Stage = 1;

    public IReadOnlyList<CompactCurveModel> BuildCurves()
    {
        var result = new List<CompactCurveModel>(Drafts.Count);
        foreach (var d in Drafts)
        {
            result.Add(new CompactCurveModel
            {
                DisplayName = string.IsNullOrWhiteSpace(d.DisplayName) ? d.SourceColumn : d.DisplayName.Trim(),
                SourceColumn = d.SourceColumn,
                Unit = string.IsNullOrWhiteSpace(d.Unit) ? null : d.Unit.Trim(),
                AxisSide = d.AxisSide,
                Color = d.Color,
                LineStyle = d.LineStyle,
                LineWidth = d.LineWidth,
                MarkerStyle = d.MarkerStyle,
                MarkerSize = d.MarkerSize,
                IsBoolean = d.IsBoolean,
                YMin = d.YMin,
                YMax = d.YMax,
                AllowOverflow = d.AllowOverflow,
                IsVisible = true,
            });
        }
        return result;
    }
}

public sealed partial class CompactColumnPick : ObservableObject
{
    [ObservableProperty]
    private string _columnName = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisible = true;
}

public sealed partial class CompactCurveDraft : ObservableObject
{
    [ObservableProperty]
    private string _sourceColumn = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _unit;

    [ObservableProperty]
    private AxisSide _axisSide = AxisSide.Left;

    [ObservableProperty]
    private string _color = "#0000FF";

    [ObservableProperty]
    private LineStyle _lineStyle = LineStyle.Solid;

    [ObservableProperty]
    private double _lineWidth = 1.5;

    [ObservableProperty]
    private MarkerStyle _markerStyle = MarkerStyle.None;

    [ObservableProperty]
    private double _markerSize = 4.0;

    [ObservableProperty]
    private bool _isBoolean;

    [ObservableProperty]
    private double? _yMin;

    [ObservableProperty]
    private double? _yMax;

    [ObservableProperty]
    private bool _allowOverflow = true;
}
