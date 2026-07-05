namespace DatPlotX.Models.Analysis;

/// <summary>
/// How an <see cref="AnalysisSegment"/> was created. Determines whether its X-range is
/// stored verbatim (<see cref="Manual"/>), looked up from event lines on every recompute
/// (<see cref="EventLinePair"/>), or follows the plot view (<see cref="VisibleWindow"/>).
/// </summary>
public enum AnalysisSegmentSource
{
    /// <summary>User drag-selected the range. <c>XMin</c> / <c>XMax</c> are authoritative.</summary>
    Manual,

    /// <summary>
    /// Range is derived from two event lines. <c>StartEventId</c> / <c>EndEventId</c> point
    /// into the project's event-line list; X-range is recomputed when either moves.
    /// </summary>
    EventLinePair,

    /// <summary>
    /// Range follows the current plot's X-axis view. Recomputed on pan / zoom (debounced
    /// for large curves — see <see cref="Services.Analysis.IAnalysisService"/>).
    /// </summary>
    VisibleWindow,

    /// <summary>Range is the curve's full data extent. Cheap; no live tracking needed.</summary>
    FullData,
}
