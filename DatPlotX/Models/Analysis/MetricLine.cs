namespace DatPlotX.Models.Analysis;

/// <summary>
/// Geometry of a line a metric wants drawn on the plot over its segment.
/// </summary>
public enum MetricLineShape
{
    /// <summary>A sloped line between two endpoints (linear fit, endpoint-to-endpoint slope).</summary>
    Segment,

    /// <summary>A horizontal line spanning the segment at a single Y (mean, min, max).</summary>
    Horizontal,
}

/// <summary>
/// Drawable geometry attached to a <see cref="MetricResult"/> for
/// <see cref="MetricKind.LineOnPlot"/> metrics. The overlay host renders this across the
/// active analysis segment. Coordinates are in the curve's data space.
///
/// <para>For <see cref="MetricLineShape.Horizontal"/>, only <see cref="Y0"/> is meaningful
/// (the X span is the segment); <see cref="X0"/>/<see cref="X1"/>/<see cref="Y1"/> are ignored.
/// For <see cref="MetricLineShape.Segment"/>, all four endpoint coordinates are used.</para>
/// </summary>
/// <param name="Shape">Sloped segment vs horizontal line.</param>
/// <param name="X0">Start X (Segment shape).</param>
/// <param name="Y0">Start Y (Segment shape) / the Y level (Horizontal shape).</param>
/// <param name="X1">End X (Segment shape).</param>
/// <param name="Y1">End Y (Segment shape).</param>
public sealed record MetricLine(
    MetricLineShape Shape,
    double X0,
    double Y0,
    double X1 = double.NaN,
    double Y1 = double.NaN)
{
    /// <summary>A horizontal line at <paramref name="y"/> spanning the segment.</summary>
    public static MetricLine Horizontal(double y) =>
        new(MetricLineShape.Horizontal, double.NaN, y);

    /// <summary>A sloped line between two endpoints.</summary>
    public static MetricLine Between(double x0, double y0, double x1, double y1) =>
        new(MetricLineShape.Segment, x0, y0, x1, y1);
}
