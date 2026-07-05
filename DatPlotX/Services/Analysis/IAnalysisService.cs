using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// Owns the project's <see cref="AnalysisSegment"/> list, the enabled-metric set, and the
/// active <see cref="IAnalysisCurveSource"/>. Mode-agnostic. Singleton in DI.
/// </summary>
public interface IAnalysisService
{
    /// <summary>All segments defined in the project, including the implicit visible-window segment.</summary>
    IReadOnlyList<AnalysisSegment> Segments { get; }

    /// <summary>Currently selected segment. Drives the Analysis panel's primary view.</summary>
    AnalysisSegment ActiveSegment { get; }

    /// <summary>
    /// The active segment's effective [XMin, XMax] right now — resolved live for
    /// VisibleWindow / FullData / EventLinePair sources. Used to draw the on-plot band.
    /// Returns null when there is no source or the range can't be resolved.
    /// </summary>
    (double XMin, double XMax)? ActiveSegmentRange { get; }

    /// <summary>Metric IDs enabled for display, in display order. Persisted in <c>.DPX</c>.</summary>
    IReadOnlyList<string> EnabledMetricIds { get; }

    /// <summary>Every metric the registry knows about — the full set the metric picker offers.</summary>
    IReadOnlyList<IMetricDefinition> AllMetrics { get; }

    /// <summary>Whether to draw the inline corner overlay (top 3 metrics for visible curves).</summary>
    bool ShowInlineOverlay { get; set; }

    /// <summary>
    /// Resolves a live event-line X position by id (or null if the line is gone). Set by the
    /// host so <see cref="Models.Analysis.AnalysisSegmentSource.EventLinePair"/> segments track
    /// the current line positions on every recompute instead of using a stale stored range.
    /// </summary>
    Func<Guid, double?>? EventLineResolver { get; set; }

    /// <summary>
    /// Swaps the active source (called by MainWindowViewModel when the plot mode changes
    /// or curves are loaded). Resets the visible-window segment to the new source's range.
    /// </summary>
    void SetSource(IAnalysisCurveSource? source);

    void DefineSegment(AnalysisSegment segment);
    void RemoveSegment(Guid id);
    void SetActiveSegment(Guid id);

    /// <summary>
    /// Replace all user-defined segments with <paramref name="segments"/> (the implicit
    /// visible-window segment is preserved) and set the active segment. Used on project load.
    /// Any visible-window entries in the input are ignored.
    /// </summary>
    void RestoreSegments(IEnumerable<AnalysisSegment> segments, Guid? activeId);

    /// <summary>
    /// Snapshot of the curves the active source currently exposes. Used by the panel to
    /// render display names + color swatches alongside metric values. Empty when no source.
    /// </summary>
    IReadOnlyList<AnalysisCurveDescriptor> ListCurves();

    /// <summary>Enable / reorder the metrics shown in the panel.</summary>
    void SetEnabledMetrics(IEnumerable<string> metricIds);

    /// <summary>
    /// Re-resolve the stored [XMin, XMax] of every <see cref="Models.Analysis.AnalysisSegmentSource.EventLinePair"/>
    /// segment from the live <see cref="EventLineResolver"/>. Call when an event line moves so a
    /// non-active EventLinePair segment's persisted range doesn't drift from its boundary lines
    /// (the active one resolves live on each compute, but the stored value is what gets saved).
    /// Raises <see cref="ResultsChanged"/> only if a stored range actually changed.
    /// </summary>
    void SyncEventLinePairRanges();

    /// <summary>
    /// Compute every enabled metric for every visible curve over the given segment.
    /// Runs on a background thread; results are dispatched via the marshalling layer the
    /// caller installs (typically <c>Dispatcher.UIThread.Post</c>).
    /// </summary>
    Task<IReadOnlyList<StatisticResult>> ComputeAsync(
        Guid segmentId,
        CancellationToken ct = default);

    /// <summary>Convenience overload: compute over <see cref="ActiveSegment"/>.</summary>
    Task<IReadOnlyList<StatisticResult>> ComputeActiveAsync(CancellationToken ct = default);

    /// <summary>The tolerance band currently attached to <paramref name="curveId"/>, or null. One band
    /// per curve this pass.</summary>
    ToleranceBand? GetToleranceBand(string curveId);

    /// <summary>All curves that currently have a tolerance band.</summary>
    IReadOnlyList<ToleranceBand> ToleranceBands { get; }

    /// <summary>Attach (or replace) a tolerance band on its <see cref="ToleranceBand.CurveId"/>.
    /// Pass null via <see cref="RemoveToleranceBand"/> to clear. Raises <see cref="ResultsChanged"/>.</summary>
    void SetToleranceBand(ToleranceBand band);

    /// <summary>Remove the band on <paramref name="curveId"/> if present. Raises <see cref="ResultsChanged"/>
    /// when something was removed.</summary>
    void RemoveToleranceBand(string curveId);

    /// <summary>
    /// Evaluate every attached tolerance band against its curve over the band's scope (active
    /// segment or whole curve). Derived-center bands re-center on the evaluated slice. Background
    /// thread, snapshotting curve data on the caller's thread like <see cref="ComputeActiveAsync"/>.
    /// </summary>
    Task<IReadOnlyList<ToleranceBandEvaluation>> ComputeToleranceBandsAsync(CancellationToken ct = default);

    /// <summary>
    /// Raised when the result set changes for any reason (curves added/removed, segment
    /// edited, metrics toggled, source swapped). UI panels listen and trigger a recompute.
    /// </summary>
    event EventHandler? ResultsChanged;
}
