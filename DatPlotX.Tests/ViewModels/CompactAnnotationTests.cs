using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;
using OxyPlot.Annotations;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

/// <summary>
/// Verifies text + arrow annotation lifecycle on the Compact surface: mutators flow through to
/// <see cref="CompactPlotViewModel.PlotModel"/>, anchor columns route to the right banded Y axis,
/// and missing anchors degrade to the first visible curve.
/// </summary>
public class CompactAnnotationTests
{
    private static PlotDataModel BuildData()
    {
        var t = new DataTable();
        t.Columns.Add("time", typeof(double));
        t.Columns.Add("alt", typeof(double));
        t.Columns.Add("speed", typeof(double));
        for (int i = 0; i < 50; i++) t.Rows.Add((double)i, 1000.0 + i, 250.0 + i * 0.5);
        return new PlotDataModel { Data = t };
    }

    private static CompactPlotViewModel BuildVm()
    {
        var vm = new CompactPlotViewModel();
        vm.SetData(BuildData(), "time");
        vm.AddCurve(new CompactCurveModel { DisplayName = "Altitude", SourceColumn = "alt", Color = "#0000FF" });
        vm.AddCurve(new CompactCurveModel { DisplayName = "Speed", SourceColumn = "speed", Color = "#FF0000" });
        return vm;
    }

    [Fact]
    public void AddTextAnnotation_EmitsTaggedOxyTextAnnotation()
    {
        var vm = BuildVm();
        var id = vm.AddTextAnnotation(new TextAnnotationModel
        {
            X = 10,
            Y = 1010,
            Text = "First peak",
            CompactCurveAnchor = "alt",
        });

        vm.TextAnnotations.Should().HaveCount(1);
        var emitted = vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .FirstOrDefault(a => CompactPlotViewModel.TryParseTextAnnotationTag(a.Tag) == id);
        emitted.Should().NotBeNull();
        emitted!.YAxisKey.Should().Be("__compact_y_0", because: "alt is the first visible curve");
    }

    [Fact]
    public void AddTextAnnotation_AnchorToSecondCurve_PicksThatYAxis()
    {
        var vm = BuildVm();
        var id = vm.AddTextAnnotation(new TextAnnotationModel
        {
            X = 10,
            Y = 260,
            Text = "Vmax",
            CompactCurveAnchor = "speed",
        });

        var emitted = vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .First(a => CompactPlotViewModel.TryParseTextAnnotationTag(a.Tag) == id);
        emitted.YAxisKey.Should().Be("__compact_y_1");
    }

    [Fact]
    public void AddTextAnnotation_MissingAnchor_FallsBackToFirstVisibleCurve()
    {
        var vm = BuildVm();
        var id = vm.AddTextAnnotation(new TextAnnotationModel
        {
            X = 10,
            Y = 0,
            Text = "Floating",
            CompactCurveAnchor = "nonexistent",
        });

        var emitted = vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .First(a => CompactPlotViewModel.TryParseTextAnnotationTag(a.Tag) == id);
        emitted.YAxisKey.Should().Be("__compact_y_0");
    }

    [Fact]
    public void RemoveTextAnnotation_DropsFromModel()
    {
        var vm = BuildVm();
        var id = vm.AddTextAnnotation(new TextAnnotationModel { X = 5, Y = 1005, Text = "x" });
        vm.RemoveTextAnnotation(id).Should().BeTrue();
        vm.TextAnnotations.Should().BeEmpty();
        vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .Where(a => CompactPlotViewModel.TryParseTextAnnotationTag(a.Tag) == id)
            .Should().BeEmpty();
    }

    [Fact]
    public void UpdateTextAnnotationPosition_MovesAnnotationInPlace()
    {
        var vm = BuildVm();
        var id = vm.AddTextAnnotation(new TextAnnotationModel { X = 5, Y = 1005, Text = "x", CompactCurveAnchor = "alt" });
        vm.UpdateTextAnnotationPosition(id, 20, 1020);

        vm.GetTextAnnotation(id)!.X.Should().Be(20);
        vm.GetTextAnnotation(id)!.Y.Should().Be(1020);
        var emitted = vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .First(a => CompactPlotViewModel.TryParseTextAnnotationTag(a.Tag) == id);
        emitted.TextPosition.X.Should().Be(20);
        emitted.TextPosition.Y.Should().Be(1020);
    }

    [Fact]
    public void AddArrowAnnotation_TipOnly_EmitsSingleArrow()
    {
        var vm = BuildVm();
        var id = vm.AddArrowAnnotation(new ArrowAnnotationModel
        {
            BaseX = 5,
            BaseY = 1005,
            TipX = 15,
            TipY = 1015,
            ArrowEnds = ArrowEnds.End,
            CompactCurveAnchor = "alt",
        });
        var arrows = vm.PlotModel.Annotations.OfType<ArrowAnnotation>()
            .Where(a => CompactPlotViewModel.TryParseArrowAnnotationTag(a.Tag) == id).ToList();
        arrows.Should().HaveCount(1);
    }

    [Fact]
    public void AddArrowAnnotation_BothEnds_EmitsForwardPlusReverse()
    {
        var vm = BuildVm();
        var id = vm.AddArrowAnnotation(new ArrowAnnotationModel
        {
            BaseX = 5,
            BaseY = 1005,
            TipX = 15,
            TipY = 1015,
            ArrowEnds = ArrowEnds.Both,
            ArrowheadStyle = ArrowheadStyle.Filled,
            CompactCurveAnchor = "alt",
        });
        var arrows = vm.PlotModel.Annotations.OfType<ArrowAnnotation>()
            .Where(a => CompactPlotViewModel.TryParseArrowAnnotationTag(a.Tag) == id).ToList();
        arrows.Should().HaveCount(2, because: "forward arrow + reverse arrow for tip on both ends");
    }

    [Fact]
    public void AddArrowAnnotation_WithLabel_EmitsLabelAnnotation()
    {
        var vm = BuildVm();
        var id = vm.AddArrowAnnotation(new ArrowAnnotationModel
        {
            BaseX = 5,
            BaseY = 1005,
            TipX = 15,
            TipY = 1015,
            Label = "Climb start",
            CompactCurveAnchor = "alt",
        });
        // Label annotations are tagged with the ArrowLabel prefix.
        var labels = vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .Where(a => a.Tag is string s && s.StartsWith("compact_arrowlbl:", System.StringComparison.Ordinal))
            .ToList();
        labels.Should().HaveCount(1);
        labels[0].Text.Should().Be("Climb start");
    }

    [Fact]
    public void ClearAllAnnotations_EmptiesBothCollectionsAndModel()
    {
        var vm = BuildVm();
        vm.AddTextAnnotation(new TextAnnotationModel { X = 1, Y = 1001, Text = "t", CompactCurveAnchor = "alt" });
        vm.AddArrowAnnotation(new ArrowAnnotationModel { BaseX = 1, BaseY = 1001, TipX = 2, TipY = 1002, CompactCurveAnchor = "alt" });
        vm.ClearAllAnnotations();

        vm.TextAnnotations.Should().BeEmpty();
        vm.ArrowAnnotations.Should().BeEmpty();
        vm.PlotModel.Annotations.OfType<TextAnnotation>()
            .Where(a => CompactPlotViewModel.TryParseTextAnnotationTag(a.Tag) is not null).Should().BeEmpty();
        vm.PlotModel.Annotations.OfType<ArrowAnnotation>()
            .Where(a => CompactPlotViewModel.TryParseArrowAnnotationTag(a.Tag) is not null).Should().BeEmpty();
    }

    [Fact]
    public void ReplaceAnnotations_ClonesIntoCollectionsAndEmitsToModel()
    {
        var vm = BuildVm();
        var texts = new[]
        {
            new TextAnnotationModel { X = 5, Y = 1005, Text = "A", CompactCurveAnchor = "alt" },
            new TextAnnotationModel { X = 25, Y = 275, Text = "B", CompactCurveAnchor = "speed" },
        };
        var arrows = new[]
        {
            new ArrowAnnotationModel { BaseX = 1, BaseY = 1001, TipX = 4, TipY = 1004, CompactCurveAnchor = "alt" },
        };
        vm.ReplaceAnnotations(texts, arrows);

        vm.TextAnnotations.Should().HaveCount(2);
        vm.ArrowAnnotations.Should().HaveCount(1);
        // Clone — list-equality of references should not hold.
        vm.TextAnnotations[0].Should().NotBeSameAs(texts[0]);
    }
}
