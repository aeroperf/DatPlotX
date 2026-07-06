using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the dockable Analysis Results panel. One row per (curve × active segment);
/// each enabled metric becomes a column. Re-fetches whenever <see cref="IAnalysisService.ResultsChanged"/>
/// fires.
/// </summary>
public partial class AnalysisPanelViewModel : ObservableObject, IDisposable
{
    private readonly IAnalysisService _analysis;
    private readonly IMetricRegistry _registry;
    private CancellationTokenSource? _pendingCts;
    private bool _disposed;

    // True while we're syncing SelectedSegment from the service (OnResultsChanged), so the
    // property setter doesn't bounce a redundant SetActiveSegment back into the service.
    private bool _syncingSelection;

    public AnalysisPanelViewModel(IAnalysisService analysis, IMetricRegistry registry)
    {
        _analysis = analysis;
        _registry = registry;
        _analysis.ResultsChanged += OnResultsChanged;

        // Build initial column + segment sets
        RebuildColumns();
        RebuildSegmentChoices();
    }

    /// <summary>Rows shown in the DataGrid — one per visible curve.</summary>
    public ObservableCollection<AnalysisRowViewModel> Rows { get; } = new();

    /// <summary>Metric columns to display, in order. Driven by <see cref="IAnalysisService.EnabledMetricIds"/>.</summary>
    public ObservableCollection<AnalysisColumnViewModel> Columns { get; } = new();

    /// <summary>One row per curve that has a tolerance band attached. Populated alongside the metric
    /// table on every results change; drives the "Tolerance Band" section below the grid.</summary>
    public ObservableCollection<ToleranceBandRowViewModel> BandRows { get; } = new();

    /// <summary>True when any curve has a tolerance band (drives the band section's visibility).</summary>
    public bool HasBands => BandRows.Count > 0;

    /// <summary>Raised once after <see cref="BandRows"/> is refreshed so the view rebuilds the section.</summary>
    public event EventHandler? BandsInvalidated;

    /// <summary>
    /// All segments offered in the panel's segment picker (including the implicit visible-window
    /// entry). Rebuilt from <see cref="IAnalysisService.Segments"/> on every results change.
    /// </summary>
    public ObservableCollection<AnalysisSegmentChoice> SegmentChoices { get; } = new();

    /// <summary>The picker's current selection. Setting it activates that segment in the service.</summary>
    [ObservableProperty]
    private AnalysisSegmentChoice? _selectedSegment;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnSelectedSegmentChanged(AnalysisSegmentChoice? value)
    {
        if (_syncingSelection || value is null) return;
        _analysis.SetActiveSegment(value.Id);
    }

    [RelayCommand]
    private void DeleteSegment(AnalysisSegmentChoice? choice)
    {
        if (choice is null || !choice.CanDelete) return;
        _analysis.RemoveSegment(choice.Id);
    }

    /// <summary>Triggered by the user clicking the target button on a PointOnCurve cell.</summary>
    public event EventHandler<AnalysisPointFlashRequest>? PointFlashRequested;

    /// <summary>Triggered when the user toggles a metric cell's "show on plot" control. The owning
    /// MainWindowViewModel draws or clears the line on the active segment.</summary>
    public event EventHandler<AnalysisLineToggleRequest>? LineToggleRequested;

    /// <summary>Triggered by the user clicking the place-event-line button on a PointOnCurve cell
    /// (min / max). The owning MainWindowViewModel prompts for a label and drops an event line at
    /// the point's X — event lines persist in the .DPX, unlike the metric point itself.</summary>
    public event EventHandler<AnalysisPlaceEventLineRequest>? PlaceEventLineRequested;

    [RelayCommand]
    private void CopyAsTsv() => CopyToClipboard(BuildTsv());

    [RelayCommand]
    private void CopyAsMarkdown() => CopyToClipboard(BuildMarkdown());

    /// <summary>Export the results table (metrics grid + tolerance-band section) to a CSV file. The
    /// host (MainWindowViewModel) owns the save picker, so the VM just hands it the row matrix.</summary>
    [RelayCommand]
    private void ExportCsv() => CsvExportRequested?.Invoke(this, BuildCsvMatrix());

    /// <summary>Raised on Export CSV with the header-first row matrix. The host shows a save picker
    /// and writes it via <see cref="Services.IFileOperationsService.ExportAnalysisResultsAsync"/>.</summary>
    public event EventHandler<IReadOnlyList<IReadOnlyList<string>>>? CsvExportRequested;

    /// <summary>
    /// Called by the view (via behavior or code-behind) when the user clicks a target
    /// button on a PointOnCurve cell.
    /// </summary>
    public void FlashPoint(AnalysisRowViewModel row, AnalysisColumnViewModel column)
    {
        if (!row.Cells.TryGetValue(column.MetricId, out var cell)) return;
        if (cell.AtX is not { } x || cell.AtY is not { } y) return;

        PointFlashRequested?.Invoke(this, new AnalysisPointFlashRequest(
            row.CurveId, x, y, $"{column.DisplayName} @ ({Fmt(x)}, {Fmt(y, row.Unit)})"));
    }

    /// <summary>
    /// Called by the view when the user clicks the place-event-line button on a PointOnCurve cell.
    /// Forwards the point's X (and a descriptive label) to the host, which prompts and places the line.
    /// </summary>
    public void PlaceEventLine(AnalysisRowViewModel row, AnalysisColumnViewModel column)
    {
        if (!row.Cells.TryGetValue(column.MetricId, out var cell)) return;
        if (cell.AtX is not { } x) return;

        PlaceEventLineRequested?.Invoke(this, new AnalysisPlaceEventLineRequest(
            x, $"{column.DisplayName} {row.DisplayName}"));
    }

    /// <summary>
    /// Called by the view when the user clicks a cell's ╱ control. Advances the cell's
    /// <see cref="AnalysisCellViewModel.LabelMode"/> through the cycle (Off → Line → Number →
    /// Label+Number → Off) and asks the owning VM to draw / clear the line with the matching label.
    /// </summary>
    public void CycleLine(AnalysisRowViewModel row, AnalysisColumnViewModel column)
    {
        if (!row.Cells.TryGetValue(column.MetricId, out var cell) || !cell.CanShowLine) return;

        cell.LabelMode = cell.NextLabelMode();
        LineToggleRequested?.Invoke(this, new AnalysisLineToggleRequest(
            row.CurveId, column.MetricId, cell.ShowOnPlot, cell.Line!, row.ColorHex,
            BuildLineLabel(column.DisplayName, cell, row.Unit)));
    }

    /// <summary>Build the on-plot line label for a stat line given the cell's current label mode.
    /// Empty for <see cref="StatLineLabelMode.Off"/>/<see cref="StatLineLabelMode.Line"/>, the bare
    /// value for <see cref="StatLineLabelMode.LineNumber"/>, and "Label value" for
    /// <see cref="StatLineLabelMode.LineLabelNumber"/>. Shared by the toggle and the recompute
    /// redraw paths so the two never disagree.</summary>
    public static string BuildLineLabel(string metricDisplayName, AnalysisCellViewModel cell, string? unit) =>
        cell.LabelMode switch
        {
            StatLineLabelMode.LineNumber => cell.NumberText(unit),
            StatLineLabelMode.LineLabelNumber => $"{metricDisplayName} {cell.NumberText(unit)}",
            _ => string.Empty,
        };

    private void OnResultsChanged(object? sender, EventArgs e)
    {
        // Cancel + dispose any in-flight compute, then kick off a new one.
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
        _pendingCts = new CancellationTokenSource();
        var ct = _pendingCts.Token;

        RebuildSegmentChoices();
        RebuildColumns();

        IsBusy = true;
        _ = RefreshAsync(ct);
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var results = await _analysis.ComputeActiveAsync(ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;
            ApplyResults(results);

            var bands = await _analysis.ComputeToleranceBandsAsync(ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;
            ApplyBands(bands);
        }
        catch (OperationCanceledException) { /* superseded */ }
        finally
        {
            if (!ct.IsCancellationRequested) IsBusy = false;
        }
    }

    private void ApplyBands(IReadOnlyList<ToleranceBandEvaluation> bands)
    {
        BandRows.Clear();
        foreach (var b in bands.OrderBy(b => b.DisplayName, StringComparer.OrdinalIgnoreCase))
            BandRows.Add(new ToleranceBandRowViewModel(b));
        OnPropertyChanged(nameof(HasBands));
        BandsInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Remove a curve's tolerance band (the ✕ on a band row). Clears it from the service
    /// (which raises ResultsChanged → recompute) and asks the host to wipe the drawn band.</summary>
    public void RemoveBand(ToleranceBandRowViewModel bandRow)
    {
        if (bandRow is null) return;
        BandRemoveRequested?.Invoke(this, bandRow.CurveId);
        _analysis.RemoveToleranceBand(bandRow.CurveId);
    }

    /// <summary>Raised when the user removes a band; the host clears the on-plot band lines.</summary>
    public event EventHandler<string>? BandRemoveRequested;

    private void ApplyResults(IReadOnlyList<StatisticResult> results)
    {
        var descriptors = _analysis.ListCurves()
            .ToDictionary(d => d.CurveId, StringComparer.Ordinal);

        // Build the next row set up front so we can decide whether the *structure* (which curves,
        // in what order) actually changed. A pan/zoom only shifts the visible window — same curves,
        // same columns, new numbers — and must NOT clear-and-refill Rows: each Clear()/Add() raises
        // a CollectionChanged that the view turns into a full manual-grid rebuild, so the old path
        // rebuilt the whole table N+1 times per pan frame and dropped selection/scroll each time.
        var next = new List<AnalysisRowViewModel>();
        foreach (var grp in results.GroupBy(r => r.CurveId))
        {
            var first = grp.First();
            descriptors.TryGetValue(grp.Key, out var desc);

            var row = new AnalysisRowViewModel(
                curveId: grp.Key,
                displayName: desc?.DisplayName ?? grp.Key,
                colorHex: desc?.ColorHex ?? "#888888",
                unit: desc?.Unit ?? first.Units);

            foreach (var r in grp)
                row.Cells[r.MetricId] = new AnalysisCellViewModel(r);

            next.Add(row);
        }

        bool sameStructure = next.Count == Rows.Count;
        if (sameStructure)
        {
            for (int i = 0; i < next.Count; i++)
            {
                if (!string.Equals(next[i].CurveId, Rows[i].CurveId, StringComparison.Ordinal))
                {
                    sameStructure = false;
                    break;
                }
            }
        }

        if (sameStructure)
        {
            // Value-only update (the common pan/zoom case): refresh each existing row's cells in
            // place, keeping the row instances, then signal exactly one redraw.
            for (int i = 0; i < next.Count; i++)
                Rows[i].UpdateFrom(next[i]);
        }
        else
        {
            // Structure changed (curve added/removed/reordered): rebuild the row set. Suppress the
            // intermediate CollectionChanged storm so the view rebuilds once, not once per row.
            _suppressRowEvents = true;
            try
            {
                Rows.Clear();
                foreach (var row in next) Rows.Add(row);
            }
            finally
            {
                _suppressRowEvents = false;
            }
        }

        // One redraw per ApplyResults regardless of path — collapses the old per-row rebuild storm.
        TableInvalidated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>True while <see cref="ApplyResults"/> is rebuilding <see cref="Rows"/> in bulk; the
    /// view ignores per-item collection events and rebuilds once on <see cref="TableInvalidated"/>.</summary>
    public bool SuppressRowEvents => _suppressRowEvents;
    private bool _suppressRowEvents;

    /// <summary>Raised exactly once after <see cref="Rows"/>/cells are updated for a results change.
    /// The view rebuilds the manual table on this instead of reacting to per-row collection events.</summary>
    public event EventHandler? TableInvalidated;

    private void RebuildColumns()
    {
        Columns.Clear();
        foreach (var id in _analysis.EnabledMetricIds)
        {
            var m = _registry.TryGet(id);
            if (m is null) continue;
            Columns.Add(new AnalysisColumnViewModel(m.Id, m.DisplayName, m.Kind));
        }
    }

    /// <summary>
    /// Reconcile <see cref="SegmentChoices"/> with the service and re-point <see cref="SelectedSegment"/>
    /// at the active segment. Guarded by <see cref="_syncingSelection"/> so the selection change
    /// doesn't echo a redundant SetActiveSegment back into the service.
    /// </summary>
    /// <remarks>
    /// Critically, this does NOT clear-and-refill when the segment set is unchanged. A plain
    /// switch between existing segments fires ResultsChanged too; clearing the collection there
    /// would yank the ComboBox's SelectedItem out from under it and leave the collapsed selection
    /// box blank. So we only mutate the collection when ids/labels actually differ, and otherwise
    /// just move the selection to the matching existing instance.
    /// </remarks>
    private void RebuildSegmentChoices()
    {
        _syncingSelection = true;
        try
        {
            var desired = _analysis.Segments
                .Select(seg => new AnalysisSegmentChoice(seg.Id, BuildSegmentLabel(seg), CanDelete: seg.Source != AnalysisSegmentSource.VisibleWindow))
                .ToList();

            bool setChanged = desired.Count != SegmentChoices.Count
                || !desired.SequenceEqual(SegmentChoices);
            if (setChanged)
            {
                SegmentChoices.Clear();
                foreach (var choice in desired)
                    SegmentChoices.Add(choice);
            }

            var activeId = _analysis.ActiveSegment.Id;
            var target = SegmentChoices.FirstOrDefault(c => c.Id == activeId) ?? SegmentChoices.FirstOrDefault();
            if (!ReferenceEquals(SelectedSegment, target))
                SelectedSegment = target;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    private static string BuildSegmentLabel(AnalysisSegment segment) =>
        segment.Source switch
        {
            AnalysisSegmentSource.VisibleWindow =>
                string.Create(CultureInfo.InvariantCulture, $"Visible window  [{segment.XMin:0.###}, {segment.XMax:0.###}]"),
            _ =>
                string.Create(CultureInfo.InvariantCulture, $"{segment.Name}  [{segment.XMin:0.###}, {segment.XMax:0.###}]"),
        };

    private static string Fmt(double v, string? unit = null)
    {
        var s = v.ToString("0.###", CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(unit) ? s : $"{s} {unit}";
    }

    private string BuildTsv()
    {
        var sb = new StringBuilder();
        sb.Append("Curve");
        foreach (var col in Columns) sb.Append('\t').Append(col.DisplayName);
        sb.Append('\n');
        foreach (var row in Rows)
        {
            sb.Append(row.DisplayName);
            foreach (var col in Columns)
            {
                sb.Append('\t');
                sb.Append(row.Cells.TryGetValue(col.MetricId, out var c) ? c.DisplayText(row.Unit) : "");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Build the header-first row matrix for CSV export: the metrics grid (Curve + one column per
    /// enabled metric), then — if any band exists — a blank separator row and the "Tolerance Bands"
    /// block (curve, scope, limits, and the four band metrics). Cells are raw display strings.
    /// </summary>
    private static readonly string[] BandSectionTitleRow = { "Tolerance Bands" };
    private static readonly string[] BandHeaderRow =
        { "Curve", "Scope", "Limits", "In-Band", "Crossings", "Exceedance", "Max Excursion" };

    private List<IReadOnlyList<string>> BuildCsvMatrix()
    {
        var matrix = new List<IReadOnlyList<string>>();

        var header = new List<string> { "Curve" };
        header.AddRange(Columns.Select(c => c.DisplayName));
        matrix.Add(header);

        foreach (var row in Rows)
        {
            var line = new List<string> { row.DisplayName };
            foreach (var col in Columns)
                line.Add(row.Cells.TryGetValue(col.MetricId, out var c) ? c.DisplayText(row.Unit) : "");
            matrix.Add(line);
        }

        if (BandRows.Count > 0)
        {
            matrix.Add(Array.Empty<string>());
            matrix.Add(BandSectionTitleRow);
            matrix.Add(BandHeaderRow);
            foreach (var b in BandRows)
                matrix.Add(new[]
                {
                    b.DisplayName, b.ScopeLabel, b.LimitsText, b.InBandText,
                    b.CrossingsText, b.ExceedanceText, b.MaxExcursionText,
                });
        }

        return matrix;
    }

    private string BuildMarkdown()
    {
        var sb = new StringBuilder();
        sb.Append("| Curve |");
        foreach (var col in Columns) sb.Append(' ').Append(col.DisplayName).Append(" |");
        sb.Append('\n');
        sb.Append("| --- |");
        foreach (var _ in Columns) sb.Append(" --- |");
        sb.Append('\n');
        foreach (var row in Rows)
        {
            sb.Append("| ").Append(row.DisplayName).Append(" |");
            foreach (var col in Columns)
            {
                sb.Append(' ');
                sb.Append(row.Cells.TryGetValue(col.MetricId, out var c) ? c.DisplayText(row.Unit) : "");
                sb.Append(" |");
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private void CopyToClipboard(string text)
    {
        // View-layer concern; left as a hook for the panel View to implement via
        // Avalonia's TopLevel.GetTopLevel(...).Clipboard. Tests don't exercise this path.
        ClipboardRequested?.Invoke(this, text);
    }

    /// <summary>Raised when the user invokes Copy. The View should marshal <paramref name="text"/> to the clipboard.</summary>
    public event EventHandler<string>? ClipboardRequested;

    public void Dispose()
    {
        // Idempotent: this VM is owned both by MainWindowViewModel (disposed in
        // MainWindow.OnClosed) and by the DI scope (disposed via desktop.Exit on Cmd-Q),
        // so Dispose runs twice. Cancelling an already-disposed CTS throws
        // ObjectDisposedException, which on the shutdown path is unhandled -> abort().
        if (_disposed) return;
        _disposed = true;

        _analysis.ResultsChanged -= OnResultsChanged;
        _pendingCts?.Cancel();
        _pendingCts?.Dispose();
    }
}

public sealed record AnalysisColumnViewModel(string MetricId, string DisplayName, MetricKind Kind);

/// <summary>
/// One row of the panel's "Tolerance Band" section: the resolved limits and the four band
/// metrics (% in-band, # limit crossings, total exceedance duration, max signed excursion) for
/// a curve that has a band attached.
/// </summary>
public sealed class ToleranceBandRowViewModel
{
    private readonly ToleranceBandEvaluation _eval;

    public ToleranceBandRowViewModel(ToleranceBandEvaluation eval)
    {
        _eval = eval;
    }

    public string CurveId => _eval.Band.CurveId;
    public string DisplayName => _eval.DisplayName;
    public string ColorHex => _eval.ColorHex;
    public string? Unit => _eval.Units;

    /// <summary>The resolved limits + scope range, for the host to draw the band lines on the plot.</summary>
    public ToleranceBandResult Result => _eval.Result;
    public double XMin => _eval.XMin;
    public double XMax => _eval.XMax;

    public string ScopeLabel => _eval.Band.Scope == BandScope.WholeCurve ? "whole curve" : "active segment";

    public string LimitsText
    {
        get
        {
            var r = _eval.Result;
            if (!double.IsFinite(r.Lower) || !double.IsFinite(r.Upper)) return "—";
            return $"{Num(r.Center)} [{Num(r.Lower)}, {Num(r.Upper)}]{UnitSuffix()}";
        }
    }

    public string InBandText =>
        double.IsNaN(_eval.Result.FractionInBand) ? "—"
        : (_eval.Result.FractionInBand * 100).ToString("0.#", CultureInfo.InvariantCulture) + " %";

    public string CrossingsText => _eval.Result.LimitCrossings.ToString(CultureInfo.InvariantCulture);

    public string ExceedanceText =>
        _eval.Result.SpanX <= 0 ? "—" : Num(_eval.Result.ExceedanceDuration);

    public string MaxExcursionText
    {
        get
        {
            var e = _eval.Result.MaxExcursion;
            if (!double.IsFinite(e)) return "—";
            var sign = e > 0 ? "+" : "";
            return sign + Num(e) + UnitSuffix();
        }
    }

    private string UnitSuffix() => string.IsNullOrEmpty(Unit) ? "" : " " + Unit;
    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>One entry in the Analysis panel's segment picker. <see cref="CanDelete"/> is false
/// for the implicit visible-window segment, which the user can't remove.</summary>
public sealed record AnalysisSegmentChoice(Guid Id, string Label, bool CanDelete);

public sealed record AnalysisPointFlashRequest(string CurveId, double X, double Y, string Label);

/// <summary>Request to place a persisted event line at a PointOnCurve metric's X. <paramref name="SuggestedLabel"/>
/// is a descriptive hint (e.g. "max Altitude") the host may offer in the Add Event Line dialog.</summary>
public sealed record AnalysisPlaceEventLineRequest(double X, string SuggestedLabel);

/// <summary>Request to draw (<paramref name="Show"/> = true) or clear a metric's statistic line on
/// the active segment for a curve. <paramref name="MetricId"/> + <paramref name="CurveId"/> key the
/// line for individual clearing.</summary>
public sealed record AnalysisLineToggleRequest(
    string CurveId, string MetricId, bool Show, MetricLine Line, string ColorHex, string Label);

public partial class AnalysisRowViewModel : ObservableObject
{
    public AnalysisRowViewModel(string curveId, string displayName, string colorHex, string? unit)
    {
        CurveId = curveId;
        DisplayName = displayName;
        ColorHex = colorHex;
        Unit = unit;
    }

    public string CurveId { get; }
    public string DisplayName { get => _displayName; private set => SetProperty(ref _displayName, value); }
    public string ColorHex { get; }
    public string? Unit { get => _unit; private set => SetProperty(ref _unit, value); }
    public Dictionary<string, AnalysisCellViewModel> Cells { get; } = new();

    private string _displayName = string.Empty;
    private string? _unit;

    /// <summary>Refresh this row's label + unit + cell values in place from a freshly-built row for
    /// the same curve. Used on the value-only update path so the row instance survives a recompute —
    /// covers pan/zoom (numbers change) AND a curve rename/unit edit (label + unit change), which
    /// would otherwise leave the row label and the cell unit-fallback showing stale text.</summary>
    public void UpdateFrom(AnalysisRowViewModel next)
    {
        DisplayName = next.DisplayName;
        Unit = next.Unit;
        UpdateCells(next.Cells);
    }

    /// <summary>Replace this row's cell values in place (same curve, refreshed metric numbers).
    /// Carries each metric's <see cref="AnalysisCellViewModel.LabelMode"/> forward so a pan/zoom
    /// doesn't visually reset the ╱ control (the line itself is re-drawn by the owning VM).</summary>
    public void UpdateCells(Dictionary<string, AnalysisCellViewModel> newCells)
    {
        foreach (var (k, v) in newCells)
        {
            if (Cells.TryGetValue(k, out var old) && old.ShowOnPlot && v.CanShowLine)
                v.LabelMode = old.LabelMode;
        }
        Cells.Clear();
        foreach (var (k, v) in newCells) Cells[k] = v;
    }
}

public partial class AnalysisCellViewModel : ObservableObject
{
    public AnalysisCellViewModel(StatisticResult result)
    {
        Value = result.Value;
        AtX = result.AtX;
        AtY = result.AtY;
        Units = result.Units;
        DerivedRateLabel = result.DerivedRateLabel;
        Line = result.Line;
        Extras = result.Extras;
    }

    /// <summary>Secondary scalars the metric exposed beyond <see cref="Value"/> (slope's R² /
    /// intercept). Null for metrics that have none. Surfaced via <see cref="Tooltip"/>.</summary>
    public IReadOnlyDictionary<string, double>? Extras { get; }

    public double Value { get; }
    public double? AtX { get; }
    public double? AtY { get; }

    /// <summary>This cell's own display unit, when it differs from the row's base unit (e.g. a
    /// slope's "ftMSL/s" vs the curve's "ftMSL"). Null = use the row unit passed to
    /// <see cref="DisplayText"/>.</summary>
    public string? Units { get; }
    public string? DerivedRateLabel { get; }

    /// <summary>Drawable geometry for this cell's metric, when it has one (slope / mean / min / max).</summary>
    public MetricLine? Line { get; }

    public bool HasPoint => AtX is not null;

    /// <summary>True when this cell's metric can be drawn on the plot (offers the "show on plot" toggle).</summary>
    public bool CanShowLine => Line is not null && !double.IsNaN(Value);

    /// <summary>How this metric's line (and its label) are drawn on the plot. Cycled by the panel's
    /// ╱ control; the actual draw/clear is performed by the owning MainWindowViewModel. The line is
    /// considered "shown" for any mode other than <see cref="StatLineLabelMode.Off"/>.</summary>
    [ObservableProperty]
    private StatLineLabelMode _labelMode = StatLineLabelMode.Off;

    /// <summary>True when the metric's line is currently drawn (any mode except Off). Drives the
    /// table rebuild's "is this toggle active" visual and the carry-forward across recomputes.</summary>
    public bool ShowOnPlot => LabelMode != StatLineLabelMode.Off;

    /// <summary>Advance to the next label mode in the cycle: Off → Line → Number → Label+Number → Off.</summary>
    public StatLineLabelMode NextLabelMode() => LabelMode switch
    {
        StatLineLabelMode.Off => StatLineLabelMode.Line,
        StatLineLabelMode.Line => StatLineLabelMode.LineNumber,
        StatLineLabelMode.LineNumber => StatLineLabelMode.LineLabelNumber,
        _ => StatLineLabelMode.Off,
    };

    // This cell's own Units (set by the service for every metric) is authoritative: it carries
    // the base unit for min/max/mean and the slope's "Y/X" rate unit — or null when the slope
    // deliberately dropped it (X-axis unit unknown). Null always means "render no unit". The
    // row-unit argument is retained only as a fallback if a result ever carries no Units at all.
    public string DisplayText(string? rowUnit)
    {
        if (double.IsNaN(Value)) return "—";
        var unit = Units ?? rowUnit;
        var num = FormatValue(Value);
        var withUnits = string.IsNullOrEmpty(unit) ? num : $"{num} {unit}";
        return DerivedRateLabel is null ? withUnits : $"{withUnits} ({DerivedRateLabel})";
    }

    /// <summary>
    /// Hover text for the cell, surfacing the metric's <see cref="Extras"/> (currently the
    /// slope's R² and intercept). Returns null when there are no extras, so the view leaves
    /// <c>ToolTip.Tip</c> unset and no empty tooltip pops up. The intercept is a Y value, so it
    /// carries the curve's base Y unit (<paramref name="rowUnit"/>) — not the slope's rate unit.
    /// </summary>
    public string? Tooltip(string? rowUnit)
    {
        if (Extras is null || Extras.Count == 0) return null;

        var parts = new List<string>(2);
        if (Extras.TryGetValue("r2", out var r2) && !double.IsNaN(r2))
            parts.Add($"R² {r2.ToString("0.####", CultureInfo.InvariantCulture)}");
        if (Extras.TryGetValue("intercept", out var b) && !double.IsNaN(b))
        {
            var num = FormatValue(b);
            parts.Add(string.IsNullOrEmpty(rowUnit) ? $"intercept {num}" : $"intercept {num} {rowUnit}");
        }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>Bare formatted numeric (no surrounding label), used in the on-plot line label.
    /// Prefers the engineer-friendly derived rate when present (e.g. a slope's ft/min).</summary>
    public string NumberText(string? rowUnit)
    {
        if (!string.IsNullOrEmpty(DerivedRateLabel)) return DerivedRateLabel;
        var unit = Units ?? rowUnit;
        var num = FormatValue(Value);
        return string.IsNullOrEmpty(unit) ? num : $"{num} {unit}";
    }

    /// <summary>
    /// Format a metric value for display. Uses 3 decimals for values of ordinary magnitude, but
    /// widens precision for small-magnitude numbers so a tiny-but-nonzero slope (e.g. -0.00012)
    /// doesn't collapse to "0" / "-0". Negative zero is normalized to "0".
    /// </summary>
    public static string FormatValue(double value)
    {
        if (double.IsNaN(value)) return "—";

        double abs = Math.Abs(value);
        // Pick decimals so the first ~3 significant figures survive for small values; cap at 6 so
        // we never print noise. Values ≥ 0.1 keep the familiar 3-decimal look.
        string format = abs == 0 || abs >= 0.1 ? "0.###"
            : abs >= 0.01 ? "0.####"
            : abs >= 0.001 ? "0.#####"
            : "0.######";

        var text = value.ToString(format, CultureInfo.InvariantCulture);
        // "-0", "-0.000" etc. → drop the sign.
        if (text.StartsWith('-') && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed == 0)
            text = text[1..];
        return text;
    }
}

/// <summary>
/// What a metric's on-plot line shows. Cycled per-curve, per-metric on the Analysis panel's
/// ╱ control. Not persisted to the .DPX — a live-session display preference, same as the old
/// boolean show-on-plot toggle it replaces.
/// </summary>
public enum StatLineLabelMode
{
    /// <summary>Line not drawn.</summary>
    Off,
    /// <summary>Line only, no text label.</summary>
    Line,
    /// <summary>Line + the bare numeric value (e.g. "-0.342").</summary>
    LineNumber,
    /// <summary>Line + metric label and value (e.g. "Slope -0.342").</summary>
    LineLabelNumber,
}
