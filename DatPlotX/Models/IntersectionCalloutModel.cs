namespace DatPlotX.Models;

/// <summary>
/// Represents a callout annotation for an intersection point between
/// an event line and a curve, with draggable position offsets for persistence.
/// </summary>
public class IntersectionCalloutModel
{
    /// <summary>
    /// Unique identifier for this callout
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the parent global event line
    /// </summary>
    public Guid EventLineId { get; set; }

    /// <summary>
    /// Name of the curve this callout is attached to
    /// </summary>
    public string CurveName { get; set; } = string.Empty;

    /// <summary>
    /// Index of the pane where this callout resides (0-based)
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// X offset from intersection point (in data coordinates)
    /// Positive values move the label to the right of the intersection
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// Y offset from intersection point (in data coordinates)
    /// Positive values move the label above the intersection
    /// </summary>
    public double OffsetY { get; set; }

    /// <summary>
    /// Whether this callout is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// The Y value at the intersection (for display)
    /// </summary>
    public double YValue { get; set; }

    /// <summary>
    /// The X position of the intersection (event line X position)
    /// </summary>
    public double XPosition { get; set; }
}
