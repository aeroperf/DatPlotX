using DatPlotX.Models;
using FluentAssertions;
using System.Text.Json;

namespace DatPlotX.Tests.Models;

/// <summary>
/// Round-trips <see cref="ProjectSettingsModel"/> through the same JSON serializer the file IO
/// layer uses, asserting that the new per-mode annotation collections (Compact + Grouped) and the
/// <c>CompactCurveAnchor</c> field survive serialization.
/// </summary>
public class AnnotationPersistenceTests
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    [Fact]
    public void CompactAnnotations_RoundTripJson()
    {
        var project = new ProjectSettingsModel
        {
            PlotMode = PlotMode.Compact,
            CompactTextAnnotations =
            {
                new TextAnnotationModel { X = 10, Y = 1010, Text = "Climb", CompactCurveAnchor = "alt" },
            },
            CompactArrowAnnotations =
            {
                new ArrowAnnotationModel
                {
                    BaseX = 5, BaseY = 1005, TipX = 15, TipY = 1015,
                    Label = "Phase A", CompactCurveAnchor = "alt",
                },
            },
        };

        var json = JsonSerializer.Serialize(project, Json);
        var loaded = JsonSerializer.Deserialize<ProjectSettingsModel>(json, Json);

        loaded.Should().NotBeNull();
        loaded!.CompactTextAnnotations.Should().HaveCount(1);
        loaded.CompactTextAnnotations[0].Text.Should().Be("Climb");
        loaded.CompactTextAnnotations[0].CompactCurveAnchor.Should().Be("alt");
        loaded.CompactArrowAnnotations.Should().HaveCount(1);
        loaded.CompactArrowAnnotations[0].Label.Should().Be("Phase A");
        loaded.CompactArrowAnnotations[0].CompactCurveAnchor.Should().Be("alt");
    }

    [Fact]
    public void GroupedAnnotations_RoundTripJson()
    {
        var project = new ProjectSettingsModel
        {
            PlotMode = PlotMode.Grouped,
            GroupedTextAnnotations =
            {
                new TextAnnotationModel { X = 0.5, Y = 1.5, Text = "Peak" },
            },
            GroupedArrowAnnotations =
            {
                new ArrowAnnotationModel { BaseX = 0, BaseY = 0, TipX = 1, TipY = 1, Label = "Trend" },
            },
        };

        var json = JsonSerializer.Serialize(project, Json);
        var loaded = JsonSerializer.Deserialize<ProjectSettingsModel>(json, Json);

        loaded!.GroupedTextAnnotations.Should().HaveCount(1);
        loaded.GroupedTextAnnotations[0].Text.Should().Be("Peak");
        loaded.GroupedArrowAnnotations.Should().HaveCount(1);
        loaded.GroupedArrowAnnotations[0].Label.Should().Be("Trend");
    }

    [Fact]
    public void LegacyProjectJson_HasEmptyCompactAndGroupedAnnotationLists()
    {
        // Older .DPX files don't carry the new fields — System.Text.Json leaves the List<>
        // initialisers in place and yields empty collections, not nulls.
        const string legacy = """
            { "Version": "2.0.0", "ProjectName": "old" }
            """;
        var loaded = JsonSerializer.Deserialize<ProjectSettingsModel>(legacy, Json);

        loaded!.CompactTextAnnotations.Should().NotBeNull().And.BeEmpty();
        loaded.CompactArrowAnnotations.Should().NotBeNull().And.BeEmpty();
        loaded.GroupedTextAnnotations.Should().NotBeNull().And.BeEmpty();
        loaded.GroupedArrowAnnotations.Should().NotBeNull().And.BeEmpty();
    }
}
