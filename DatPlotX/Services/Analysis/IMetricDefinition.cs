using DatPlotX.Models.Analysis;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// One statistic the engineer can compute over an <see cref="AnalysisSegment"/> of a curve.
/// Implementations are pure functions of <c>(x, y)</c> spans — no mutable state, no I/O,
/// safe to call from any thread. The registry holds one instance per metric ID, registered
/// at app startup.
/// </summary>
public interface IMetricDefinition
{
    /// <summary>Stable lowercase identifier persisted in <c>.DPX</c> (e.g. <c>"max"</c>, <c>"slope"</c>).</summary>
    string Id { get; }

    /// <summary>User-facing label shown in the metric picker and panel header (e.g. <c>"Max"</c>).</summary>
    string DisplayName { get; }

    /// <summary>Grouping for the picker dialog.</summary>
    MetricCategory Category { get; }

    /// <summary>Determines panel rendering and whether a "flash on plot" target button is offered.</summary>
    MetricKind Kind { get; }

    /// <summary>
    /// Compute the statistic. The spans share an index — <c>x[i]</c> corresponds to <c>y[i]</c>.
    /// Implementations must handle empty input by returning <see cref="MetricResult.Empty"/>
    /// (i.e. <c>Value = NaN</c>) and must skip NaN samples when accumulating.
    /// </summary>
    /// <param name="parameters">Optional named inputs (thresholds, tolerance). Phase-1 metrics
    /// ignore this; it defaults to <see cref="MetricParameters.None"/> at every call site.</param>
    MetricResult Compute(ReadOnlySpan<double> x, ReadOnlySpan<double> y, MetricParameters parameters);
}
