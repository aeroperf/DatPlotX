using DatPlotX.Models.Analysis;
using DatPlotX.Services.Units;
using System.Globalization;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// Default <see cref="IAnalysisService"/>. Registered as a singleton, so it outlives the transient
/// <c>MainWindowViewModel</c> that subscribes to <see cref="ResultsChanged"/>.
///
/// <para>THREAD-AFFINITY (review D3): every public mutator and <see cref="ResultsChanged"/> raise
/// runs synchronously on the UI thread. This is a hard invariant, not a convenience — the
/// <see cref="ResultsChanged"/> handler mutates <c>ObservableCollection</c>s in the analysis panel,
/// which throws if touched off-thread. The compute methods deliberately snapshot live curve data on
/// the calling (UI) thread <i>before</i> the <c>Task.Run</c> so the worker only ever sees immutable
/// arrays. Do not introduce an <c>await …ConfigureAwait(false)</c> before an <c>OnResultsChanged()</c>;
/// route pan/zoom notifications through <c>MainWindowViewModel.NotifyAnalysisVisibleRangeChanged</c>
/// (debounced, UI-thread) rather than raising from a background continuation.</para>
///
/// <para>LIFETIME (review D4): the cross-object notification surface is strong C# events (no
/// <c>WeakReferenceMessenger</c>). Because this singleton outlives the transient VM, correctness
/// rests entirely on <c>MainWindowViewModel.Dispose()</c> detaching via <c>MainWindow.OnClosed</c>
/// (plus DI-scope disposal on exit). A second top-level window, or any close path that bypasses
/// <c>OnClosed</c>, would leak the whole VM graph through <see cref="ResultsChanged"/>. Preserve the
/// single-window-close disposal path, or move to weak events, before adding a second window.</para>
/// </summary>
public sealed class AnalysisService : IAnalysisService, IDisposable
{
    private static readonly Guid VisibleWindowSegmentId = Guid.Empty;
    private static readonly string[] DefaultEnabledMetrics = { "max", "min", "mean", "stddev", "slope" };

    private readonly IMetricRegistry _registry;
    private readonly IUnitRegistry _units;
    private readonly List<AnalysisSegment> _segments = new();
    private readonly List<string> _enabledMetricIds = new();

    // One tolerance band per curve (session-only — not persisted to .DPX this pass).
    private readonly Dictionary<string, ToleranceBand> _bands = new(StringComparer.Ordinal);

    private IAnalysisCurveSource? _source;
    private Guid _activeSegmentId = VisibleWindowSegmentId;

    public AnalysisService(IMetricRegistry registry, IUnitRegistry units)
    {
        _registry = registry;
        _units = units;
        _enabledMetricIds.AddRange(DefaultEnabledMetrics);
        _segments.Add(AnalysisSegment.VisibleWindow(0, 0));
    }

    public IReadOnlyList<AnalysisSegment> Segments => _segments;

    public AnalysisSegment ActiveSegment =>
        _segments.FirstOrDefault(s => s.Id == _activeSegmentId)
        ?? _segments[0];

    public (double XMin, double XMax)? ActiveSegmentRange =>
        _source is null ? null : ResolveRange(ActiveSegment, _source);

    public IReadOnlyList<string> EnabledMetricIds => _enabledMetricIds;

    public IReadOnlyList<IMetricDefinition> AllMetrics => _registry.All;

    public bool ShowInlineOverlay { get; set; }

    public Func<Guid, double?>? EventLineResolver { get; set; }

    public event EventHandler? ResultsChanged;

    public void SetSource(IAnalysisCurveSource? source)
    {
        if (_source is not null)
        {
            _source.CurvesChanged -= OnSourceChanged;
            _source.VisibleRangeChanged -= OnVisibleRangeChanged;
        }
        _source = source;
        if (_source is not null)
        {
            _source.CurvesChanged += OnSourceChanged;
            _source.VisibleRangeChanged += OnVisibleRangeChanged;
            RefreshVisibleWindowSegment();
        }
        OnResultsChanged();
    }

    public IReadOnlyList<AnalysisCurveDescriptor> ListCurves()
        => _source?.ListCurves() ?? Array.Empty<AnalysisCurveDescriptor>();

    public void DefineSegment(AnalysisSegment segment)
    {
        // Visible-window segment is always slot 0 and never user-defined.
        if (segment.Source == AnalysisSegmentSource.VisibleWindow)
            throw new ArgumentException("VisibleWindow segment is managed internally.", nameof(segment));

        // Replace if same Id already exists; else append.
        var idx = _segments.FindIndex(s => s.Id == segment.Id);
        if (idx >= 0) _segments[idx] = segment;
        else _segments.Add(segment);

        OnResultsChanged();
    }

    public void RemoveSegment(Guid id)
    {
        if (id == VisibleWindowSegmentId) return;
        var idx = _segments.FindIndex(s => s.Id == id);
        if (idx < 0) return;
        _segments.RemoveAt(idx);
        if (_activeSegmentId == id) _activeSegmentId = VisibleWindowSegmentId;
        OnResultsChanged();
    }

    public void SetActiveSegment(Guid id)
    {
        if (_segments.All(s => s.Id != id)) return;
        if (_activeSegmentId == id) return;
        _activeSegmentId = id;
        OnResultsChanged();
    }

    public void RestoreSegments(IEnumerable<AnalysisSegment> segments, Guid? activeId)
    {
        // Drop everything except the implicit visible-window segment (always slot 0).
        _segments.RemoveAll(s => s.Source != AnalysisSegmentSource.VisibleWindow);

        foreach (var seg in segments)
        {
            if (seg.Source == AnalysisSegmentSource.VisibleWindow) continue; // implicit, never stored
            // Avoid id collisions with a previously loaded project's segments.
            if (_segments.Any(s => s.Id == seg.Id)) continue;
            _segments.Add(seg);
        }

        _activeSegmentId = activeId is { } id && _segments.Any(s => s.Id == id)
            ? id
            : VisibleWindowSegmentId;

        OnResultsChanged();
    }

    public void SetEnabledMetrics(IEnumerable<string> metricIds)
    {
        _enabledMetricIds.Clear();
        foreach (var id in metricIds)
        {
            if (_registry.TryGet(id) is not null && !_enabledMetricIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                _enabledMetricIds.Add(id);
        }
        OnResultsChanged();
    }

    public void SyncEventLinePairRanges()
    {
        if (EventLineResolver is not { } resolve) return;

        bool changed = false;
        for (int i = 0; i < _segments.Count; i++)
        {
            var seg = _segments[i];
            if (seg.Source != AnalysisSegmentSource.EventLinePair) continue;
            if (seg.StartEventId is not { } startId || seg.EndEventId is not { } endId) continue;
            if (resolve(startId) is not { } sx || resolve(endId) is not { } ex) continue;

            double xMin = Math.Min(sx, ex), xMax = Math.Max(sx, ex);
            if (xMin == seg.XMin && xMax == seg.XMax) continue;

            _segments[i] = seg with { XMin = xMin, XMax = xMax };
            changed = true;
        }

        if (changed) OnResultsChanged();
    }

    public Task<IReadOnlyList<StatisticResult>> ComputeActiveAsync(CancellationToken ct = default)
        => ComputeAsync(ActiveSegment.Id, ct);

    public Task<IReadOnlyList<StatisticResult>> ComputeAsync(Guid segmentId, CancellationToken ct = default)
    {
        var source = _source;
        if (source is null)
            return Task.FromResult<IReadOnlyList<StatisticResult>>(Array.Empty<StatisticResult>());

        var segment = _segments.FirstOrDefault(s => s.Id == segmentId);
        if (segment is null)
            return Task.FromResult<IReadOnlyList<StatisticResult>>(Array.Empty<StatisticResult>());

        // Resolve range (visible-window may have drifted since last refresh)
        var (xMin, xMax) = ResolveRange(segment, source);
        var metrics = _enabledMetricIds
            .Select(id => _registry.TryGet(id))
            .Where(m => m is not null)
            .Select(m => m!)
            .ToList();

        var xUnit = source.XUnit;

        // Snapshot every visible curve's data on the CALLING (UI) thread. The source walks
        // the live pane collection; doing that off-thread races a pane add/remove. After this
        // point the worker touches only immutable arrays.
        var work = new List<(AnalysisCurveDescriptor Curve, AnalysisCurveData Data)>();
        foreach (var curve in source.ListCurves())
        {
            if (!curve.IsVisible) continue;
            var data = source.GetData(curve.CurveId);
            if (data is not null) work.Add((curve, data));
        }

        return Task.Run(() =>
        {
            var results = new List<StatisticResult>(work.Count * metrics.Count);
            foreach (var (curve, data) in work)
            {
                ct.ThrowIfCancellationRequested();

                var (start, end) = data.SliceIndices(xMin, xMax);
                if (start > end) continue;
                var (xSlice, ySlice) = data.Slice(start, end);

                foreach (var metric in metrics)
                {
                    ct.ThrowIfCancellationRequested();
                    var mr = metric.Compute(xSlice, ySlice, MetricParameters.None);

                    // Slope is a rate: its unit is Y-per-X (e.g. "kt/s", "ftMSL/s"), not the
                    // bare Y unit. When the X-axis unit can't be inferred there's no honest
                    // denominator, so we drop the unit entirely rather than imply a fake one.
                    string? displayUnit = curve.Unit;
                    string? derivedLabel = null;
                    if (metric.Id == "slope")
                    {
                        // Denominator uses the normalized X unit ("time" → "s") so it reads
                        // "ftMSL/s" rather than the raw "ftMSL/time". The numerator keeps the
                        // curve's own unit verbatim (engineers expect "ftMSL", "kt", ...). When
                        // either unit is unknown there's no honest denominator, so we emit ""
                        // (explicitly "no unit") — distinct from null so the panel renders a bare
                        // number instead of falling back to the curve's base Y unit.
                        displayUnit = (string.IsNullOrWhiteSpace(curve.Unit) || string.IsNullOrWhiteSpace(xUnit))
                            ? string.Empty
                            : $"{curve.Unit}/{_units.Normalize(xUnit)}";

                        if (curve.Unit is { } yu && xUnit is { } xu)
                        {
                            var rate = _units.PreferredDerivedRate(yu, xu);
                            if (rate is not null && !double.IsNaN(mr.Value))
                            {
                                var converted = mr.Value * rate.Multiplier;
                                derivedLabel = string.Create(CultureInfo.InvariantCulture,
                                    $"{converted:0.###} {rate.Label}");
                            }
                        }
                    }

                    results.Add(new StatisticResult(
                        curve.CurveId,
                        segment.Id,
                        metric.Id,
                        mr.Value,
                        mr.AtX,
                        mr.AtY,
                        displayUnit,
                        derivedLabel,
                        mr.Line,
                        mr.Extras));
                }
            }
            return (IReadOnlyList<StatisticResult>)results;
        }, ct);
    }

    public ToleranceBand? GetToleranceBand(string curveId)
        => _bands.TryGetValue(curveId, out var b) ? b : null;

    public IReadOnlyList<ToleranceBand> ToleranceBands => _bands.Values.ToList();

    public void SetToleranceBand(ToleranceBand band)
    {
        ArgumentNullException.ThrowIfNull(band);
        _bands[band.CurveId] = band;
        OnResultsChanged();
    }

    public void RemoveToleranceBand(string curveId)
    {
        if (_bands.Remove(curveId)) OnResultsChanged();
    }

    public Task<IReadOnlyList<ToleranceBandEvaluation>> ComputeToleranceBandsAsync(CancellationToken ct = default)
    {
        var source = _source;
        if (source is null || _bands.Count == 0)
            return Task.FromResult<IReadOnlyList<ToleranceBandEvaluation>>(Array.Empty<ToleranceBandEvaluation>());

        // Snapshot each banded curve's data + scope range on the calling (UI) thread, mirroring
        // ComputeAsync — after this the worker touches only immutable arrays.
        var descriptors = source.ListCurves().ToDictionary(d => d.CurveId, StringComparer.Ordinal);
        var work = new List<(ToleranceBand Band, AnalysisCurveDescriptor? Desc, AnalysisCurveData Data, double XMin, double XMax)>();
        foreach (var band in _bands.Values)
        {
            var data = source.GetData(band.CurveId);
            if (data is null) continue;
            var (xMin, xMax) = band.Scope == BandScope.WholeCurve
                ? source.FullDataXRange
                : ResolveRange(ActiveSegment, source);
            descriptors.TryGetValue(band.CurveId, out var desc);
            work.Add((band, desc, data, xMin, xMax));
        }

        return Task.Run(() =>
        {
            var results = new List<ToleranceBandEvaluation>(work.Count);
            foreach (var (band, desc, data, xMin, xMax) in work)
            {
                ct.ThrowIfCancellationRequested();
                var (start, end) = data.SliceIndices(xMin, xMax);
                ToleranceBandResult eval;
                if (start > end)
                {
                    eval = ToleranceBandResult.Empty;
                }
                else
                {
                    var (xs, ys) = data.Slice(start, end);
                    eval = ToleranceBandEvaluator.Evaluate(band, xs, ys);
                }
                results.Add(new ToleranceBandEvaluation(
                    band,
                    desc?.DisplayName ?? band.CurveId,
                    desc?.ColorHex ?? "#888888",
                    desc?.Unit,
                    eval,
                    xMin,
                    xMax));
            }
            return (IReadOnlyList<ToleranceBandEvaluation>)results;
        }, ct);
    }

    private (double XMin, double XMax) ResolveRange(AnalysisSegment segment, IAnalysisCurveSource source)
    {
        switch (segment.Source)
        {
            case AnalysisSegmentSource.VisibleWindow:
                return source.VisibleXRange;
            case AnalysisSegmentSource.FullData:
                return source.FullDataXRange;
            case AnalysisSegmentSource.EventLinePair:
                // Track the live event-line positions so moving a boundary line updates the
                // segment. Fall back to the stored range if a line is missing or no resolver.
                if (EventLineResolver is { } resolve
                    && segment.StartEventId is { } startId
                    && segment.EndEventId is { } endId
                    && resolve(startId) is { } sx
                    && resolve(endId) is { } ex)
                {
                    return (Math.Min(sx, ex), Math.Max(sx, ex));
                }
                return (segment.XMin, segment.XMax);
            default:
                return (segment.XMin, segment.XMax);
        }
    }

    private void RefreshVisibleWindowSegment()
    {
        if (_source is null) return;
        var (xMin, xMax) = _source.VisibleXRange;
        _segments[0] = new AnalysisSegment(
            VisibleWindowSegmentId, "Visible window", xMin, xMax,
            AnalysisSegmentSource.VisibleWindow);
    }

    private void OnSourceChanged(object? sender, EventArgs e)
    {
        RefreshVisibleWindowSegment();
        OnResultsChanged();
    }

    private void OnVisibleRangeChanged(object? sender, EventArgs e)
    {
        RefreshVisibleWindowSegment();
        if (ActiveSegment.Source == AnalysisSegmentSource.VisibleWindow)
            OnResultsChanged();
    }

    private void OnResultsChanged()
    {
        ResultsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => SetSource(null);
}
