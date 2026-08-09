using OxyPlot;
using OxyPlot.Annotations;

namespace DatPlotX.Views.Compact;

/// <summary>
/// Vertical event line that spans the full plot area regardless of how many banded Y axes
/// the Compact Plot Surface stacks. OxyPlot's <see cref="LineAnnotation"/> with
/// <see cref="LineAnnotationType.Vertical"/> clips to its bound Y axis range; on the Compact
/// surface every curve has its own Y axis so a stock LineAnnotation only renders inside the
/// first band. This annotation reads <see cref="PlotElement.XAxis"/> for the X transform but
/// draws from <see cref="PlotModel.PlotArea"/>.Top to .Bottom in screen space.
/// </summary>
public sealed class CompactEventLineAnnotation : Annotation
{
    public double X { get; set; }
    public OxyColor Color { get; set; } = OxyColors.Orange;
    public double StrokeThickness { get; set; } = 2.0;
    public LineStyle LineStyle { get; set; } = LineStyle.Solid;
    public string? Label { get; set; }
    /// <summary>
    /// Optional second row drawn beneath <see cref="Label"/> (the event line's X-axis value,
    /// already formatted to the pane's decimal setting). Null suppresses the row.
    /// </summary>
    public string? ValueLabel { get; set; }
    /// <summary>0 = bottom of plot area, 1 = top. Matches LineAnnotation.TextLinePosition convention.</summary>
    public double LabelLinePosition { get; set; } = 0.97;
    public double LabelPadding { get; set; } = 4.0;

    public override void Render(IRenderContext rc)
    {
        base.Render(rc);
        if (XAxis is null || PlotModel is null) return;

        double sx = XAxis.Transform(X);
        var area = PlotModel.PlotArea;
        if (sx < area.Left || sx > area.Right) return;

        rc.DrawLine(
            new[]
            {
                new ScreenPoint(sx, area.Top),
                new ScreenPoint(sx, area.Bottom),
            },
            Color,
            StrokeThickness,
            EdgeRenderingMode.Automatic,
            LineStyle.GetDashArray(),
            LineJoin.Miter);

        if (string.IsNullOrEmpty(Label) && string.IsNullOrEmpty(ValueLabel)) return;

        // Rows are left-aligned against the line (matching Stacked mode, which uses ScottPlot's
        // multiline left alignment) so both surfaces read identically.
        double labelY = area.Top + (area.Bottom - area.Top) * (1.0 - LabelLinePosition);
        double rowX = sx + LabelPadding;
        string? nameRow = string.IsNullOrEmpty(Label) ? null : Label;
        string? valueRow = string.IsNullOrEmpty(ValueLabel) ? null : ValueLabel;

        double rowY = labelY;
        if (nameRow is not null)
        {
            DrawRow(rc, rowX, rowY, nameRow);
            rowY += Measure(rc, nameRow).Height;
        }

        if (valueRow is not null)
            DrawRow(rc, rowX, rowY, valueRow);
    }

    private void DrawRow(IRenderContext rc, double x, double y, string text) =>
        rc.DrawText(
            new ScreenPoint(x, y),
            text,
            Color,
            fontFamily: PlotModel!.DefaultFont,
            fontSize: PlotModel.DefaultFontSize,
            fontWeight: FontWeights.Bold,
            rotation: 0,
            horizontalAlignment: HorizontalAlignment.Left,
            verticalAlignment: VerticalAlignment.Top);

    private OxySize Measure(IRenderContext rc, string text) =>
        rc.MeasureText(text, PlotModel!.DefaultFont, PlotModel.DefaultFontSize, FontWeights.Bold);

    /// <inheritdoc />
    public override OxyRect GetClippingRect()
    {
        // Render across the entire plot area, ignoring axis-bound clipping (which is what
        // confines stock LineAnnotation to the first Y band on the Compact surface).
        return PlotModel?.PlotArea ?? base.GetClippingRect();
    }
}
