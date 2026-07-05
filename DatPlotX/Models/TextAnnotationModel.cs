namespace DatPlotX.Models;

/// <summary>
/// Represents a text annotation that can be placed on a plot pane.
/// Text annotations display user-defined text at a specific location
/// and can be dragged, resized, and styled.
/// </summary>
public class TextAnnotationModel
{
    /// <summary>
    /// Unique identifier for this text annotation
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Index of the pane this annotation belongs to
    /// </summary>
    public int PaneIndex { get; set; }

    /// <summary>
    /// The text content to display
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// X coordinate in data units
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate in data units
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Font size in points
    /// </summary>
    public double FontSize { get; set; } = 14;

    /// <summary>
    /// Font color in hex format (e.g., "#000000")
    /// </summary>
    public string FontColor { get; set; } = "#000000";

    /// <summary>
    /// Whether to use bold font
    /// </summary>
    public bool IsBold { get; set; }

    /// <summary>
    /// Whether to use italic font
    /// </summary>
    public bool IsItalic { get; set; }

    /// <summary>
    /// Background color in hex format (e.g., "#FFFFFF")
    /// Set to empty string for transparent background
    /// </summary>
    public string BackgroundColor { get; set; } = "#FFFFFF";

    /// <summary>
    /// Background opacity (0.0 to 1.0)
    /// </summary>
    public double BackgroundOpacity { get; set; } = 0.9;

    /// <summary>
    /// Border color in hex format
    /// </summary>
    public string BorderColor { get; set; } = "#999999";

    /// <summary>
    /// Border width in pixels (0 for no border)
    /// </summary>
    public double BorderWidth { get; set; } = 1;

    /// <summary>
    /// Text alignment within the annotation box
    /// </summary>
    public TextAnnotationAlignment Alignment { get; set; } = TextAnnotationAlignment.MiddleCenter;

    /// <summary>
    /// Horizontal alignment of the text *content* inside the box. Independent of
    /// <see cref="Alignment"/> (which anchors the box on the (X, Y) point). Only the text
    /// flows left / center / right when the label has multiple lines or a wider box.
    /// </summary>
    public TextHorizontalAlignment TextAlignment { get; set; } = TextHorizontalAlignment.Left;

    /// <summary>
    /// Rotation angle in degrees (0-360)
    /// </summary>
    public double Rotation { get; set; }

    /// <summary>
    /// Whether the annotation is visible
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// Which Y-axis this annotation is associated with (Stacked mode only)
    /// </summary>
    public YAxisType YAxis { get; set; } = YAxisType.Y1;

    /// <summary>
    /// Compact-mode only: source column of the curve whose banded Y axis this annotation tracks.
    /// <see cref="Y"/> is interpreted in that band's data coordinates. Null = first visible curve
    /// (degrades gracefully when the anchored curve is removed). Ignored outside Compact mode.
    /// </summary>
    public string? CompactCurveAnchor { get; set; }

    /// <summary>
    /// Creates a deep copy of this model
    /// </summary>
    public TextAnnotationModel Clone()
    {
        return new TextAnnotationModel
        {
            Id = this.Id,
            PaneIndex = this.PaneIndex,
            Text = this.Text,
            X = this.X,
            Y = this.Y,
            FontSize = this.FontSize,
            FontColor = this.FontColor,
            IsBold = this.IsBold,
            IsItalic = this.IsItalic,
            BackgroundColor = this.BackgroundColor,
            BackgroundOpacity = this.BackgroundOpacity,
            BorderColor = this.BorderColor,
            BorderWidth = this.BorderWidth,
            Alignment = this.Alignment,
            TextAlignment = this.TextAlignment,
            Rotation = this.Rotation,
            IsVisible = this.IsVisible,
            YAxis = this.YAxis,
            CompactCurveAnchor = this.CompactCurveAnchor
        };
    }
}

/// <summary>
/// Text alignment options for text annotations
/// </summary>
public enum TextAnnotationAlignment
{
    UpperLeft,
    UpperCenter,
    UpperRight,
    MiddleLeft,
    MiddleCenter,
    MiddleRight,
    LowerLeft,
    LowerCenter,
    LowerRight
}

/// <summary>
/// Horizontal alignment of multi-line text content within a text annotation's box.
/// </summary>
public enum TextHorizontalAlignment
{
    Left,
    Center,
    Right,
}
