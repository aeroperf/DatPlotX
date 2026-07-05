namespace DatPlotX.Models;

/// <summary>
/// Represents an event line (vertical reference line) on a plot
/// </summary>
public class EventLineModel
{
    /// <summary>
    /// Unique identifier for this event line
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name/label for this event line (e.g., "E1", "E2")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// X-axis position of the event line
    /// </summary>
    public double XPosition { get; set; }

    /// <summary>
    /// Color of the event line in hex format (e.g., "#FFB900")
    /// </summary>
    public string Color { get; set; } = "#FFB900";

    /// <summary>
    /// Line width in pixels
    /// </summary>
    public double LineWidth { get; set; } = 2.0;

    /// <summary>
    /// Line pattern (Solid, Dashed, Dotted)
    /// </summary>
    public LinePatternType LinePattern { get; set; } = LinePatternType.Solid;

    /// <summary>
    /// Whether this event line is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Which pane this event line belongs to (0-based index)
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// User notes/description for this event line
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When this event line was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Whether this is a global event line that spans all panes.
    /// When true, the event line appears on all panes at the same X position.
    /// When false, the event line only appears on the pane specified by PaneIndex (legacy behavior).
    /// </summary>
    public bool IsGlobal { get; set; } = true;

    /// <summary>
    /// Whether to show the label text on this event line instance.
    /// For global event lines, this is typically only true for the bottom pane.
    /// </summary>
    public bool ShowLabel { get; set; } = true;

    /// <summary>
    /// Compact Plot Surface only: per-curve drag offsets (in pixels) for the callout box that
    /// shows the Y-value at this event line's intersection with each curve. Key = curve
    /// <c>SourceColumn</c>. Missing entries get default staggered offsets at render time.
    /// Persisted in the .DPX file so manual repositioning survives project reload.
    /// </summary>
    public Dictionary<string, CalloutOffset> CompactCalloutOffsets { get; set; } = new();
}

/// <summary>Pixel offset (right, down) from the intersection point to the callout box anchor.</summary>
public class CalloutOffset
{
    public double Dx { get; set; }
    public double Dy { get; set; }
}

/// <summary>
/// Line pattern types
/// </summary>
public enum LinePatternType
{
    Solid,
    Dashed,
    Dotted,
    DashDot
}
