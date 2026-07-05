namespace DatPlotX.Models.Analysis;

/// <summary>
/// Shape of a metric's result. Drives how the Analysis panel renders the value cell
/// and whether it offers a "flash on plot" target button.
/// </summary>
public enum MetricKind
{
    /// <summary>Single numeric value (Mean, StdDev, RMS, Slope, ...).</summary>
    Scalar,

    /// <summary>Value plus the (x, y) sample location where it occurred (Min, Max, ...).</summary>
    PointOnCurve,

    /// <summary>
    /// Value plus a drawable <see cref="MetricResult.Line"/> rendered over the segment
    /// (LinearFit trend, mean level, ...). The panel offers a "show on plot" toggle.
    /// </summary>
    LineOnPlot,
}
