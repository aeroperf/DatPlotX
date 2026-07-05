using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;
using DatPlotX.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the Grouped Parameter Plot — owns the sidebar inputs, X/Y pickers, and the
/// projected series list. The view subscribes to <see cref="PlotVersion"/> and re-renders the
/// ScottPlot surface on every bump (mirrors the EnrouteStudio pattern). Every config change
/// (input dropdown, X, Y, ShowLegend, ShowMarkers) calls <see cref="Rebuild"/>.
/// </summary>
public sealed partial class GroupedPlotViewModel : ObservableObject
{
    private readonly IGroupedDataIndexer _indexer;
    private readonly ApplicationSettings _settings;

    private PlotDataModel? _data;
    private GroupedPlotConfig _config = new();
    private bool _suspendRebuild;
    private bool _recomputingColumns;

    /// <summary>
    /// Set by <see cref="Rebuild"/> when the axis meaning changed (first data load, or X/Y column
    /// change) so the view autoscales on the next render. Consumed once via
    /// <see cref="ConsumeAutoScaleRequest"/>; otherwise the view preserves the current viewport so
    /// tweaking a locked input value keeps the user's zoom (the whole point of comparing lines at
    /// the same viewport). See review G1.
    /// </summary>
    private bool _autoScaleOnNextRender = true;

    [ObservableProperty]
    private bool _showLegend;

    [ObservableProperty]
    private bool _showMarkers = true;

    [ObservableProperty]
    private string? _selectedXColumn;

    [ObservableProperty]
    private string? _selectedYColumn;

    [ObservableProperty]
    private string _plotTitle = string.Empty;

    [ObservableProperty]
    private string _xAxisLabel = string.Empty;

    [ObservableProperty]
    private string _yAxisLabel = string.Empty;

    [ObservableProperty]
    private int _plotVersion;

    /// <summary>
    /// Bumped for cosmetic-only changes (marker/legend toggles) that the view applies in place
    /// without clearing the plot or autoscaling — preserves the user's zoom. See review G1.
    /// </summary>
    [ObservableProperty]
    private int _cosmeticVersion;

    [ObservableProperty]
    private bool _truncationWarningVisible;

    [ObservableProperty]
    private string? _truncationWarningText;

    [ObservableProperty]
    private bool _hasInputs;

    public ObservableCollection<GroupedInputParameterViewModel> Inputs { get; } = new();

    public ObservableCollection<string> AvailableXColumns { get; } = new();

    public ObservableCollection<string> AvailableYColumns { get; } = new();

    public ObservableCollection<GroupedPlotSeries> Series { get; } = new();

    public GroupedPlotConfig Config => _config;

    public bool HasData => _data is not null;

    /// <summary>
    /// Annotation manager wired up by the view via <see cref="AttachAnnotationManager"/> once the
    /// ScottPlot surface is available. Null before the view is constructed and on detach.
    /// </summary>
    public GroupedPlotAnnotationManager? Annotations { get; private set; }

    /// <summary>
    /// Annotations queued by <see cref="RestoreAnnotations"/> before the view (and therefore the
    /// manager) was attached. Applied once on <see cref="AttachAnnotationManager"/>.
    /// </summary>
    private (List<TextAnnotationModel> Texts, List<ArrowAnnotationModel> Arrows)? _pendingAnnotationRestore;

    public GroupedPlotViewModel(IGroupedDataIndexer indexer, ApplicationSettings settings)
    {
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void AttachAnnotationManager(GroupedPlotAnnotationManager manager)
    {
        Annotations = manager;
        if (_pendingAnnotationRestore is { } pending)
        {
            manager.Restore(pending.Texts, pending.Arrows);
            _pendingAnnotationRestore = null;
        }
    }

    public void DetachAnnotationManager() => Annotations = null;

    /// <summary>
    /// Replay persisted annotations into the live manager, or queue them if the view hasn't
    /// attached yet (project load fires before the view's DataContextChanged).
    /// </summary>
    public void RestoreAnnotations(
        IEnumerable<TextAnnotationModel> texts,
        IEnumerable<ArrowAnnotationModel> arrows)
    {
        var t = texts.Select(m => m.Clone()).ToList();
        var a = arrows.Select(m => m.Clone()).ToList();
        if (Annotations is not null)
            Annotations.Restore(t, a);
        else
            _pendingAnnotationRestore = (t, a);
    }

    /// <summary>Attach (or detach) the imported data set. Inputs / X / Y are recomputed.</summary>
    public void SetData(PlotDataModel? data)
    {
        _data = data;
        _autoScaleOnNextRender = true; // fresh data set → fit the view
        RebuildInputsFromConfig();
        RecomputeAvailableColumns();
        Rebuild();
    }

    /// <summary>
    /// Replace the persisted configuration (e.g. on project load or after the inputs-picker dialog
    /// returns). Inputs and X/Y selections are restored from the new config.
    /// </summary>
    public void ApplyConfig(GroupedPlotConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _config = config;
        _autoScaleOnNextRender = true; // project load / inputs reconfigured → fit the view
        _suspendRebuild = true;
        try
        {
            SelectedXColumn = config.XAxisColumn;
            SelectedYColumn = config.YAxisColumn;
            ShowLegend = config.ShowLegend;
            ShowMarkers = config.ShowMarkers;
            RebuildInputsFromConfig();
            RecomputeAvailableColumns();
        }
        finally { _suspendRebuild = false; }
        Rebuild();
    }

    /// <summary>Snapshot the current state back to the persisted config object.</summary>
    public GroupedPlotConfig BuildConfig()
    {
        _config.XAxisColumn = SelectedXColumn;
        _config.YAxisColumn = SelectedYColumn;
        _config.ShowLegend = ShowLegend;
        _config.ShowMarkers = ShowMarkers;
        // Inputs are kept in sync via their per-row models (SelectedValue is mutated directly).
        return _config;
    }

    /// <summary>Re-project the data and bump <see cref="PlotVersion"/> so the view redraws.</summary>
    public void Rebuild()
    {
        if (_suspendRebuild) return;
        Series.Clear();

        if (_data is null || string.IsNullOrEmpty(SelectedXColumn) || string.IsNullOrEmpty(SelectedYColumn))
        {
            TruncationWarningVisible = false;
            PlotTitle = string.Empty;
            XAxisLabel = SelectedXColumn ?? string.Empty;
            YAxisLabel = SelectedYColumn ?? string.Empty;
            PlotVersion++;
            return;
        }

        var projection = _indexer.Project(_data, BuildConfig());
        foreach (var s in projection.Series)
            Series.Add(s);

        TruncationWarningVisible = projection.Truncated;
        TruncationWarningText = projection.Truncated
            ? $"Showing first {_settings.GroupedPlotMaxLines} of {projection.TotalGroupCount} lines — narrow your selection."
            : null;

        PlotTitle = BuildTitle();
        XAxisLabel = SelectedXColumn ?? string.Empty;
        YAxisLabel = SelectedYColumn ?? string.Empty;
        PlotVersion++;
    }

    partial void OnSelectedXColumnChanged(string? value)
    {
        if (_recomputingColumns) return;
        // X changed → axis meaning changed → autoscale on the next render.
        _autoScaleOnNextRender = true;
        // Y must not equal X — clear Y if it does and refresh the Y picker.
        if (!string.IsNullOrEmpty(value) && value == SelectedYColumn)
            SelectedYColumn = null;
        // Guard against the double-rebuild: clearing Y above already re-enters via
        // OnSelectedYColumnChanged. Suspend so RecomputeAvailableColumns + the final Rebuild
        // run exactly once (see review P1).
        _suspendRebuild = true;
        try { RecomputeAvailableColumns(); }
        finally { _suspendRebuild = false; }
        Rebuild();
    }

    partial void OnSelectedYColumnChanged(string? value)
    {
        if (_recomputingColumns) return;
        // Y changed → axis meaning changed → autoscale on the next render.
        _autoScaleOnNextRender = true;
        Rebuild();
    }

    // Cosmetic toggles apply in place in the view — no rebuild, no autoscale, zoom preserved.
    partial void OnShowLegendChanged(bool value) { CosmeticVersion++; }

    partial void OnShowMarkersChanged(bool value) { CosmeticVersion++; }

    /// <summary>
    /// Returns (and clears) the pending autoscale request. The view calls this in its render path:
    /// when true (or on the first render) it autoscales; otherwise it preserves the viewport.
    /// </summary>
    public bool ConsumeAutoScaleRequest()
    {
        var v = _autoScaleOnNextRender;
        _autoScaleOnNextRender = false;
        return v;
    }

    private void RebuildInputsFromConfig()
    {
        DetachInputs();
        Inputs.Clear();
        if (_data is null)
        {
            HasInputs = false;
            return;
        }
        foreach (var input in _config.Inputs)
        {
            if (!_data.Data.Columns.Contains(input.ColumnName)) continue;
            var distinct = _indexer.GetDistinctValues(_data, input.ColumnName, out _);
            var vm = new GroupedInputParameterViewModel(input, distinct);
            vm.SelectionChanged += OnInputSelectionChanged;
            Inputs.Add(vm);
        }
        HasInputs = Inputs.Count > 0;
    }

    private void OnInputSelectionChanged(object? sender, EventArgs e) => Rebuild();

    private void DetachInputs()
    {
        foreach (var vm in Inputs)
            vm.SelectionChanged -= OnInputSelectionChanged;
    }

    private void RecomputeAvailableColumns()
    {
        if (_recomputingColumns) return;
        _recomputingColumns = true;
        try
        {
            // Update collections in place rather than Clear+Add — clearing nulls the bound
            // ComboBox.SelectedItem, which writes null back into SelectedX/YColumn and visually
            // blanks the dropdown even after we restore the value.
            if (_data is null)
            {
                SyncCollection(AvailableXColumns, Array.Empty<string>());
                SyncCollection(AvailableYColumns, Array.Empty<string>());
                return;
            }

            var inputColumnNames = new HashSet<string>(_config.Inputs.Select(i => i.ColumnName), StringComparer.Ordinal);
            var xTarget = new List<string>();
            var yTarget = new List<string>();
            foreach (var col in _data.ColumnNames)
            {
                if (inputColumnNames.Contains(col)) continue;
                xTarget.Add(col);
                if (!string.Equals(col, SelectedXColumn, StringComparison.Ordinal))
                    yTarget.Add(col);
            }

            SyncCollection(AvailableXColumns, xTarget);
            SyncCollection(AvailableYColumns, yTarget);

            // Drop selections that are no longer eligible.
            if (SelectedXColumn is not null && !AvailableXColumns.Contains(SelectedXColumn))
                SelectedXColumn = null;
            if (SelectedYColumn is not null && !AvailableYColumns.Contains(SelectedYColumn))
                SelectedYColumn = null;
        }
        finally
        {
            _recomputingColumns = false;
        }
    }

    private static void SyncCollection(ObservableCollection<string> target, IList<string> desired)
    {
        // Remove items not in desired (back to front to keep indices stable).
        for (int i = target.Count - 1; i >= 0; i--)
        {
            if (!desired.Contains(target[i]))
                target.RemoveAt(i);
        }
        // Insert missing items at the correct position.
        for (int i = 0; i < desired.Count; i++)
        {
            if (i >= target.Count)
                target.Add(desired[i]);
            else if (!string.Equals(target[i], desired[i], StringComparison.Ordinal))
                target.Insert(i, desired[i]);
        }
    }

    private string BuildTitle()
    {
        if (!string.IsNullOrWhiteSpace(_config.Title)) return _config.Title!;
        var locked = _config.Inputs
            .Where(i => i.SelectedValue is double)
            .Select(i =>
            {
                var label = string.IsNullOrEmpty(i.DisplayLabel) ? i.ColumnName : i.DisplayLabel;
                var v = i.SelectedValue!.Value;
                var formatted = string.IsNullOrEmpty(i.Format)
                    ? v.ToString(CultureInfo.InvariantCulture)
                    : v.ToString(i.Format, CultureInfo.InvariantCulture);
                return $"{label}={formatted}{i.UnitSuffix}";
            })
            .ToArray();
        return string.Join(" • ", locked);
    }
}
