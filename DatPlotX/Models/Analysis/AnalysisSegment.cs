namespace DatPlotX.Models.Analysis;

/// <summary>
/// A named X-range that statistics are computed over. Replaces the legacy "visible window
/// only" stats behavior with first-class, user-defined regions.
/// </summary>
/// <param name="Id">Stable identifier used by the analysis-service cache and the .DPX file.</param>
/// <param name="Name">User-visible label (e.g. "Climb", "Cruise").</param>
/// <param name="XMin">Lower X bound. For <see cref="AnalysisSegmentSource.EventLinePair"/>
/// segments this is recomputed from the referenced event line on each query.</param>
/// <param name="XMax">Upper X bound. Same caveat as <paramref name="XMin"/>.</param>
/// <param name="Source">How the segment was defined; controls how X-range is refreshed.</param>
/// <param name="StartEventId">For <see cref="AnalysisSegmentSource.EventLinePair"/> segments,
/// the event-line ID providing <c>XMin</c>.</param>
/// <param name="EndEventId">For <see cref="AnalysisSegmentSource.EventLinePair"/> segments,
/// the event-line ID providing <c>XMax</c>.</param>
public sealed record AnalysisSegment(
    Guid Id,
    string Name,
    double XMin,
    double XMax,
    AnalysisSegmentSource Source,
    Guid? StartEventId = null,
    Guid? EndEventId = null)
{
    public static AnalysisSegment VisibleWindow(double xMin, double xMax) =>
        new(Guid.Empty, "Visible window", xMin, xMax, AnalysisSegmentSource.VisibleWindow);

    public static AnalysisSegment FullData(double xMin, double xMax) =>
        new(Guid.NewGuid(), "Full data", xMin, xMax, AnalysisSegmentSource.FullData);
}
