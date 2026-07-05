namespace DatPlotX.Services.Analysis;

/// <summary>
/// Mode-specific adapter that hands curve data to the <see cref="IAnalysisService"/>.
/// Each plot mode VM (Stacked, Compact, Grouped) implements this so the service stays
/// agnostic of ScottPlot vs OxyPlot vs grouped-series semantics.
/// </summary>
public interface IAnalysisCurveSource
{
    /// <summary>All curves currently plotted in the active mode (visible + hidden).</summary>
    IReadOnlyList<AnalysisCurveDescriptor> ListCurves();

    /// <summary>Returns the Y data and X-axis representation for <paramref name="curveId"/>.
    /// Returns null when the ID is unknown (curve was removed between query and fetch).</summary>
    AnalysisCurveData? GetData(string curveId);

    /// <summary>X-axis unit for the project (e.g. <c>"s"</c>). Drives the slope metric's derived rate.</summary>
    string? XUnit { get; }

    /// <summary>X-axis extent of the current plot view. Used by <see cref="Models.Analysis.AnalysisSegmentSource.VisibleWindow"/>.</summary>
    (double XMin, double XMax) VisibleXRange { get; }

    /// <summary>X-axis extent across all curves' data. Used by <see cref="Models.Analysis.AnalysisSegmentSource.FullData"/>.</summary>
    (double XMin, double XMax) FullDataXRange { get; }

    /// <summary>
    /// Raised when curves change (added / removed / data reloaded). The service responds
    /// by clearing its cache and re-emitting <see cref="IAnalysisService.ResultsChanged"/>.
    /// </summary>
    event EventHandler? CurvesChanged;

    /// <summary>Raised when the X-axis view changes (pan/zoom). Drives visible-window segment recompute.</summary>
    event EventHandler? VisibleRangeChanged;

    /// <summary>Force a curves-changed recompute. Used for in-place curve edits (visibility, rename,
    /// unit) that mutate a model without raising the underlying collection-changed event.</summary>
    void NotifyCurvesChanged();

    /// <summary>Force a visible-range recompute. Called (debounced) after the X axis pans / zooms.</summary>
    void NotifyVisibleRangeChanged();
}
