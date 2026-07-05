using OxyPlot;
using OxyPlot.Annotations;

namespace DatPlotX.Views.Compact;

/// <summary>
/// Callout box + leader line for an event-line/curve intersection on the Compact Plot Surface.
/// Anchored at the intersection in data coords (<see cref="X"/>, <see cref="Y"/>) on the curve's
/// banded Y axis, displaced by pixel offsets (<see cref="OffsetXPixels"/>, <see cref="OffsetYPixels"/>).
///
/// Why a custom annotation: OxyPlot's stock <c>ArrowAnnotation</c> + <c>TextAnnotation</c> pair
/// would need both pieces synced and each clipped to a single Y axis. The Compact surface stacks
/// one Y axis per curve, so a sibling-pair approach quickly breaks when the callout drifts past
/// its anchor band. This annotation reads the curve's Y axis for the intersection transform
/// only, then renders both leader and label across the whole <see cref="PlotModel.PlotArea"/>
/// in screen space — mirroring <see cref="CompactEventLineAnnotation"/>.
/// </summary>
public sealed class CompactCalloutAnnotation : Annotation
{
    /// <summary>Intersection X in data coords (event-line position).</summary>
    public double X { get; set; }
    /// <summary>Intersection Y in data coords on the curve's Y axis.</summary>
    public double Y { get; set; }
    /// <summary>Pixel offset from intersection to the label box anchor (right = positive).</summary>
    public double OffsetXPixels { get; set; } = 40;
    /// <summary>Pixel offset from intersection to the label box anchor (down = positive).</summary>
    public double OffsetYPixels { get; set; } = -30;
    public string Text { get; set; } = string.Empty;
    public OxyColor Color { get; set; } = OxyColors.Black;
    public OxyColor BackgroundColor { get; set; } = OxyColor.FromArgb(240, 255, 254, 240);
    public OxyColor BorderColor { get; set; } = OxyColors.Black;
    /// <summary>Leader-line color (and arrowhead fill). Defaults to black; the surface gives a
    /// hard pop against pale plot backgrounds and matches the Stacked-mode callout convention.</summary>
    public OxyColor LeaderColor { get; set; } = OxyColors.Black;
    public double Padding { get; set; } = 4.0;
    public new double FontSize { get; set; } = 11.0;
    /// <summary>Arrowhead size in pixels (along the leader direction).</summary>
    public double ArrowheadLength { get; set; } = 10.0;
    /// <summary>Arrowhead half-width in pixels (perpendicular to the leader).</summary>
    public double ArrowheadHalfWidth { get; set; } = 5.0;

    /// <summary>Bounds of the label box from the last render — used by view-side hit testing.</summary>
    internal OxyRect? LastLabelBounds { get; private set; }
    /// <summary>Screen anchor of the intersection from the last render — leader-line endpoint.</summary>
    internal ScreenPoint LastAnchor { get; private set; }

    public override void Render(IRenderContext rc)
    {
        base.Render(rc);
        if (XAxis is null || YAxis is null || PlotModel is null) return;
        if (string.IsNullOrEmpty(Text)) return;

        // Anchor at the intersection, then push the label by the per-callout pixel offset so
        // user drags translate cleanly regardless of zoom level.
        double ax = XAxis.Transform(X);
        double ay = YAxis.Transform(Y);
        var anchor = new ScreenPoint(ax, ay);
        LastAnchor = anchor;

        double lx = ax + OffsetXPixels;
        double ly = ay + OffsetYPixels;

        var textSize = rc.MeasureText(Text, PlotModel.DefaultFont, FontSize, FontWeights.Normal);
        double w = textSize.Width + 2 * Padding;
        double h = textSize.Height + 2 * Padding;
        // Box centered on (lx, ly) so dragging moves the centroid.
        var box = new OxyRect(lx - w / 2, ly - h / 2, w, h);
        LastLabelBounds = box;

        // Leader line from the box edge nearest the anchor → the intersection. Drawn from box
        // to anchor (not the reverse) so the arrowhead sits *on the anchor*, pointing at the
        // intersection (Stacked-mode convention).
        var leaderStart = ClosestPointOnRect(box, anchor);
        DrawArrow(rc, leaderStart, anchor);

        rc.DrawRectangle(box, BackgroundColor, BorderColor, 1.0, EdgeRenderingMode.Automatic);

        rc.DrawText(
            new ScreenPoint(lx, ly),
            Text,
            Color,
            fontFamily: PlotModel.DefaultFont,
            fontSize: FontSize,
            fontWeight: FontWeights.Normal,
            rotation: 0,
            horizontalAlignment: HorizontalAlignment.Center,
            verticalAlignment: VerticalAlignment.Middle);
    }

    /// <inheritdoc />
    public override OxyRect GetClippingRect()
        => PlotModel?.PlotArea ?? base.GetClippingRect();

    private static ScreenPoint ClosestPointOnRect(OxyRect rect, ScreenPoint p)
    {
        double cx = System.Math.Max(rect.Left, System.Math.Min(p.X, rect.Right));
        double cy = System.Math.Max(rect.Top, System.Math.Min(p.Y, rect.Bottom));
        return new ScreenPoint(cx, cy);
    }

    /// <summary>
    /// Render a single-line arrow from <paramref name="from"/> to <paramref name="to"/>: a stroked
    /// shaft followed by a filled triangle arrowhead pointing at <paramref name="to"/>. Inlined
    /// rather than relying on OxyPlot's <c>ArrowAnnotation</c> because that plottable wants
    /// data coordinates and rebuilds geometry per render — here both endpoints already live in
    /// screen space.
    /// </summary>
    private void DrawArrow(IRenderContext rc, ScreenPoint from, ScreenPoint to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double len = System.Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-3)
        {
            // Degenerate — anchor sits inside the label box. Skip; the rectangle border is
            // visual cue enough that the callout exists.
            return;
        }

        double headLen = System.Math.Min(ArrowheadLength, len);
        double ux = dx / len;
        double uy = dy / len;
        // Shaft stops at the arrowhead base so the head's filled triangle doesn't pile on a
        // stroked line tip (which would look fatter than the head).
        var shaftEnd = new ScreenPoint(to.X - ux * headLen, to.Y - uy * headLen);

        rc.DrawLine(
            new[] { from, shaftEnd },
            LeaderColor,
            1.2,
            EdgeRenderingMode.Automatic,
            LineStyle.Solid.GetDashArray(),
            LineJoin.Miter);

        // Triangle: tip at `to`, base centered at `shaftEnd`, half-width perpendicular to (ux,uy).
        double pxx = -uy * ArrowheadHalfWidth;
        double pxy = ux * ArrowheadHalfWidth;
        var head = new[]
        {
            to,
            new ScreenPoint(shaftEnd.X + pxx, shaftEnd.Y + pxy),
            new ScreenPoint(shaftEnd.X - pxx, shaftEnd.Y - pxy),
        };
        rc.DrawPolygon(
            head,
            LeaderColor,
            LeaderColor,
            0,
            EdgeRenderingMode.Automatic);
    }
}
