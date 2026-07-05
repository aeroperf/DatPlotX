namespace DatPlotX.Models;

/// <summary>
/// Represents information about a curve that has been plotted to a pane.
/// This record replaces the complex tuple type for better readability and maintainability.
/// </summary>
/// <param name="Plottable">The ScottPlot plottable object (Signal or Scatter)</param>
/// <param name="Period">The sample period for the curve data (Signal plots only; 0 for scatter)</param>
/// <param name="Data">The Y-axis data array</param>
/// <param name="Config">The curve configuration model</param>
public record PlottedCurveInfo(
    object? Plottable,
    double Period,
    double[] Data,
    CurveConfigurationModel Config)
{
    /// <summary>
    /// The X-axis data array for scatter curves; <c>null</c> for periodic (Signal) curves,
    /// whose X is <c>i × Period</c>. Carried so analysis slices the correct X-window (see
    /// <c>StackedAnalysisCurveSource</c>). Kept off the positional parameter list so the
    /// record's 4-arg deconstruction stays intact for existing callers.
    /// </summary>
    public double[]? XData { get; init; }
}
