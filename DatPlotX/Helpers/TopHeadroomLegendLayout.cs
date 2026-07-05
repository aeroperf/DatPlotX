using ScottPlot;
using ScottPlot.LegendLayouts;

namespace DatPlotX.Helpers;

/// <summary>
/// A ScottPlot legend layout that adds a few pixels of headroom above the top item.
/// </summary>
/// <remarks>
/// ScottPlot's legend renderer clips items to <c>LegendRect.Contract(Padding).Expand(1px)</c>
/// (<c>Legend.RenderLayout</c>). The top item's text is centered in a row whose height comes from
/// <c>LabelStyle.Measure</c> (font line-spacing), which under-reserves the cap/ascender — so the first
/// label's glyph tops (e.g. "Gload" → the d/l ascenders) get shaved by the frame. <c>Legend.Padding</c>
/// can't fix it: contracting the clip also shifts the text down by the same offset, so the two move
/// together with no net gain.
///
/// Earlier attempts that prefixed a newline to the label inflated the row height — and because the
/// <see cref="Wrapping"/> engine sizes EVERY row to the tallest item, that blew out the spacing between
/// all rows (an airy legend). Instead we wrap the stock <see cref="Wrapping"/> layout and post-process:
/// shift every symbol/label rect down by a small fixed headroom and grow the legend box by the same
/// amount on the TOP edge only. The top item drops clear of the clip while inter-row spacing stays at
/// ScottPlot's natural single-line height, and the bottom edge keeps its normal padding.
/// </remarks>
public sealed class TopHeadroomLegendLayout : ILegendLayout
{
    /// <summary>Extra pixels reserved above the top item — enough for an ascender, small enough to be invisible.</summary>
    public const float Headroom = 4f;

    private readonly Wrapping _inner = new();

    public LegendLayout GetLayout(Legend legend, LegendItem[] items, PixelSize maxSize, Paint paint)
    {
        var layout = _inner.GetLayout(legend, items, maxSize, paint);

        // Push content down by Headroom; grow the box's bottom by Headroom so the top edge stays put
        // and the extra space lands above the first row (PixelRect: Top is the smaller Y, Bottom larger).
        static PixelRect Down(PixelRect r) =>
            new(r.Left, r.Right, r.Bottom + Headroom, r.Top + Headroom);

        var lr = layout.LegendRect;
        return new LegendLayout
        {
            LegendItems = layout.LegendItems,
            LegendRect = new PixelRect(lr.Left, lr.Right, lr.Bottom + Headroom, lr.Top),
            LabelRects = layout.LabelRects.Select(Down).ToArray(),
            SymbolRects = layout.SymbolRects.Select(Down).ToArray(),
        };
    }
}
