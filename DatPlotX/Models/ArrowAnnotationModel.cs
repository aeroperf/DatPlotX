namespace DatPlotX.Models;

/// <summary>
/// Represents an arrow annotation that can be placed on a plot pane.
/// Arrows have a base (start) point and a tip (end) point, with the
/// arrowhead at the tip. They can be styled and dragged.
/// </summary>
public class ArrowAnnotationModel
{
    /// <summary>
    /// Unique identifier for this arrow annotation
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Index of the pane this annotation belongs to
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// X coordinate of the arrow base (start point) in data units
    /// </summary>
    public double BaseX { get; set; }

    /// <summary>
    /// Y coordinate of the arrow base (start point) in data units
    /// </summary>
    public double BaseY { get; set; }

    /// <summary>
    /// X coordinate of the arrow tip (end point with arrowhead) in data units
    /// </summary>
    public double TipX { get; set; }

    /// <summary>
    /// Y coordinate of the arrow tip (end point with arrowhead) in data units
    /// </summary>
    public double TipY { get; set; }

    /// <summary>
    /// Line color in hex format (e.g., "#000000")
    /// </summary>
    public string Color { get; set; } = "#333333";

    /// <summary>
    /// Line width in pixels
    /// </summary>
    public double LineWidth { get; set; } = 1;

    /// <summary>
    /// Width of the arrowhead in pixels
    /// </summary>
    public double ArrowheadWidth { get; set; } = 10;

    /// <summary>
    /// Length of the arrowhead in pixels
    /// </summary>
    public double ArrowheadLength { get; set; } = 15;

    /// <summary>
    /// Style of the arrowhead
    /// </summary>
    public ArrowheadStyle ArrowheadStyle { get; set; } = ArrowheadStyle.Filled;

    /// <summary>
    /// Which ends of the line have arrowheads
    /// </summary>
    public ArrowEnds ArrowEnds { get; set; } = ArrowEnds.End;

    /// <summary>
    /// Optional label text to display near the arrow
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Font size for the label in points
    /// </summary>
    public double LabelFontSize { get; set; } = 18;

    /// <summary>
    /// Font color for the label in hex format
    /// </summary>
    public string LabelFontColor { get; set; } = "#000000";

    /// <summary>
    /// Position of the label relative to the arrow (Base, Middle, Tip)
    /// </summary>
    public ArrowLabelPosition LabelPosition { get; set; } = ArrowLabelPosition.Middle;

    /// <summary>
    /// Alignment of the label relative to the arrow line
    /// </summary>
    public ArrowLabelAlignment LabelAlignment { get; set; } = ArrowLabelAlignment.Above;

    /// <summary>
    /// Whether to rotate the label to align with the arrow direction
    /// </summary>
    public bool LabelRotateWithArrow { get; set; } = true;

    /// <summary>
    /// Whether the annotation is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Which Y-axis this annotation is associated with (Stacked mode only)
    /// </summary>
    public YAxisType YAxis { get; set; } = YAxisType.Y1;

    /// <summary>
    /// Compact-mode only: source column of the curve whose banded Y axis this arrow's
    /// <see cref="BaseY"/> and <see cref="TipY"/> live in. Null = first visible curve.
    /// Ignored outside Compact mode.
    /// </summary>
    public string? CompactCurveAnchor { get; set; }

    /// <summary>
    /// Creates a deep copy of this model
    /// </summary>
    public ArrowAnnotationModel Clone()
    {
        return new ArrowAnnotationModel
        {
            Id = this.Id,
            PaneIndex = this.PaneIndex,
            BaseX = this.BaseX,
            BaseY = this.BaseY,
            TipX = this.TipX,
            TipY = this.TipY,
            Color = this.Color,
            LineWidth = this.LineWidth,
            ArrowheadWidth = this.ArrowheadWidth,
            ArrowheadLength = this.ArrowheadLength,
            ArrowheadStyle = this.ArrowheadStyle,
            ArrowEnds = this.ArrowEnds,
            Label = this.Label,
            LabelFontSize = this.LabelFontSize,
            LabelFontColor = this.LabelFontColor,
            LabelPosition = this.LabelPosition,
            LabelAlignment = this.LabelAlignment,
            LabelRotateWithArrow = this.LabelRotateWithArrow,
            IsVisible = this.IsVisible,
            YAxis = this.YAxis,
            CompactCurveAnchor = this.CompactCurveAnchor
        };
    }
}

/// <summary>
/// Arrowhead style options
/// </summary>
public enum ArrowheadStyle
{
    /// <summary>
    /// Filled triangular arrowhead
    /// </summary>
    Filled,

    /// <summary>
    /// Open/outline triangular arrowhead
    /// </summary>
    Open,

    /// <summary>
    /// No arrowhead (just a line)
    /// </summary>
    None
}

/// <summary>
/// Label position relative to the arrow
/// </summary>
public enum ArrowLabelPosition
{
    /// <summary>
    /// Label at the base (start) of the arrow
    /// </summary>
    Base,

    /// <summary>
    /// Label at the middle of the arrow
    /// </summary>
    Middle,

    /// <summary>
    /// Label at the tip (end) of the arrow
    /// </summary>
    Tip
}

/// <summary>
/// Specifies which ends of the arrow have arrowheads
/// </summary>
public enum ArrowEnds
{
    /// <summary>
    /// No arrowheads (plain line)
    /// </summary>
    None,

    /// <summary>
    /// Arrowhead at the start/base only
    /// </summary>
    Start,

    /// <summary>
    /// Arrowhead at the end/tip only (default)
    /// </summary>
    End,

    /// <summary>
    /// Arrowheads at both ends
    /// </summary>
    Both
}

/// <summary>
/// Label alignment relative to the arrow line
/// </summary>
public enum ArrowLabelAlignment
{
    /// <summary>
    /// Label positioned above the arrow line
    /// </summary>
    Above,

    /// <summary>
    /// Label positioned below the arrow line
    /// </summary>
    Below,

    /// <summary>
    /// Label positioned inline with arrow, extending from base away from tip
    /// </summary>
    InlineAtBase,

    /// <summary>
    /// Label positioned inline with arrow, extending from tip away from base
    /// </summary>
    InlineAtTip
}
