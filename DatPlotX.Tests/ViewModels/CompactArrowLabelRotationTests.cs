using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using OxyPlot.Annotations;
using OxyPlot.SkiaSharp;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

/// <summary>
/// Regression: on the Compact (OxyPlot) surface an arrow's text label must rotate to follow the
/// arrow when the arrowhead is dragged, the same as the Stacked (ScottPlot) surface. A stale
/// transform-validity guard (gated on ScreenMin/ScreenMax orientation, which is identical for every
/// OxyPlot axis) silently dropped the label into a no-rotation, data-space-offset fallback — so the
/// label stayed horizontal and slid sideways as the arrow rotated. The guard now probes Transform()
/// directly.
/// </summary>
public class CompactArrowLabelRotationTests
{
    private static PlotDataModel BuildData()
    {
        var t = new DataTable();
        t.Columns.Add("time", typeof(double));
        t.Columns.Add("alt", typeof(double));
        for (int i = 0; i < 50; i++) t.Rows.Add((double)i, 1000.0 + i * 5);
        return new PlotDataModel { Data = t };
    }

    private static void Render(CompactPlotViewModel vm)
    {
        var exporter = new PngExporter { Width = 800, Height = 600 };
        using var ms = new MemoryStream();
        exporter.Export(vm.PlotModel, ms);
    }

    private static TextAnnotation? Label(CompactPlotViewModel vm) =>
        vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .FirstOrDefault(a => (a.Tag as string)?.StartsWith("compact_arrowlbl:", StringComparison.Ordinal) == true);

    [Fact]
    public void DraggingArrowheadDown_RotatesLabelWithArrow()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { DisplayName = "Altitude", SourceColumn = "alt", Color = "#0000FF" });

        var id = vm.AddArrowAnnotation(new ArrowAnnotationModel
        {
            BaseX = 5,
            BaseY = 1100,
            TipX = 25,
            TipY = 1100,
            Label = "Stall Onset",
            CompactCurveAnchor = "alt",
        });

        // Render once so the PlotModel's axes carry valid pixel transforms (the next Rebuild reads them).
        Render(vm);
        vm.UpdateArrowAnnotationPosition(id, 5, 1100, 25, 1100); // rebuild against the rendered model
        var horizontal = Label(vm);
        horizontal.Should().NotBeNull();
        horizontal!.TextRotation.Should().BeApproximately(0, 1.0, "a horizontal arrow's label is upright");
        double horizontalX = horizontal.TextPosition.X;

        // Drag the arrowhead down (alt 1100 → 1010): the arrow now tilts; the label must tilt with it.
        Render(vm);
        vm.UpdateArrowAnnotationPosition(id, 5, 1100, 25, 1010);
        Render(vm);
        vm.UpdateArrowAnnotationPosition(id, 5, 1100, 25, 1010); // rebuild against the now-tilted render

        var rotated = Label(vm);
        rotated.Should().NotBeNull();
        Math.Abs(rotated!.TextRotation).Should().BeGreaterThan(10,
            "the label must rotate to follow the tilted arrow, not stay horizontal");
        // And it must NOT drift sideways toward the tip — the midpoint X barely moves.
        rotated.TextPosition.X.Should().BeApproximately(horizontalX, 3.0,
            "the Middle-anchored label tracks the arrow midpoint, it does not slide toward the tip");
    }

    [Fact]
    public void AboveAlignment_PutsLabelAboveArrow_BelowPutsItBelow()
    {
        const double arrowY = 1100;

        double LabelDataY(ArrowLabelAlignment align)
        {
            var vm = new CompactPlotViewModel();
            vm.SetData(BuildData(), "time");
            vm.AddCurve(new CompactCurveModel { DisplayName = "Altitude", SourceColumn = "alt", Color = "#0000FF" });
            var id = vm.AddArrowAnnotation(new ArrowAnnotationModel
            {
                BaseX = 5,
                BaseY = arrowY,
                TipX = 25,
                TipY = arrowY,
                Label = "Stall Onset",
                CompactCurveAnchor = "alt",
                LabelAlignment = align,
            });
            Render(vm);
            // Re-emit the label now that transforms exist (anchor pixel offset needs them).
            vm.UpdateArrowAnnotationPosition(id, 5, arrowY, 25, arrowY);
            return Label(vm)!.TextPosition.Y;
        }

        // Data-Y increases upward on the band, so "Above" the arrow = a LARGER data Y than the
        // arrow line, and "Below" = a smaller one. (Regression: these were flipped on Compact.)
        LabelDataY(ArrowLabelAlignment.Above).Should().BeGreaterThan(arrowY,
            "the Above label sits above the arrow line");
        LabelDataY(ArrowLabelAlignment.Below).Should().BeLessThan(arrowY,
            "the Below label sits below the arrow line");
    }
}
