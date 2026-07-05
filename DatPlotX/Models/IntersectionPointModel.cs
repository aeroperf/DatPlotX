namespace DatPlotX.Models;

/// <summary>
/// Represents an intersection point between an event line and a data curve
/// </summary>
public class IntersectionPointModel
{
    /// <summary>
    /// ID of the event line
    /// </summary>
    public Guid EventLineId { get; set; }

    /// <summary>
    /// Label of the event line (e.g., "E1")
    /// </summary>
    public string EventLineLabel { get; set; } = string.Empty;

    /// <summary>
    /// X position of the event line (and intersection)
    /// </summary>
    public double XPosition { get; set; }

    /// <summary>
    /// Name of the curve that intersects
    /// </summary>
    public string CurveName { get; set; } = string.Empty;

    /// <summary>
    /// Y value at the intersection
    /// </summary>
    public double YValue { get; set; }

    /// <summary>
    /// Index of the pane (for multi-pane plots)
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// Which Y-axis the curve is on (Y1 or Y2)
    /// </summary>
    public YAxisType YAxis { get; set; } = YAxisType.Y1;

    /// <summary>
    /// When this intersection was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Y-axis type for dual-axis support
/// </summary>
public enum YAxisType
{
    Y1,
    Y2
}
