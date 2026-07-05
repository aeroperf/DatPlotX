using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class DefaultCurvePaletteTests
{
    // P2: cheap guard against accidental palette truncation. Both Stacked Panes
    // and Compact Plot Surface index into Colors modulo length; if the count drops
    // below 16, runs of similarly-coloured curves become indistinguishable.
    [Fact]
    public void Colors_ReturnsAtLeast16DistinctHexValues()
    {
        var colors = DefaultCurvePalette.Colors;

        colors.Length.Should().BeGreaterThanOrEqualTo(16,
            "the palette must offer at least 16 hues so cycling produces visually-distinct curves");
        colors.Distinct().Count().Should().Be(colors.Length,
            "every palette entry must be unique — duplicates defeat the cycling contract");

        // Every entry must be a parseable hex string (`#RRGGBB`).
        foreach (var hex in colors)
        {
            hex.Should().MatchRegex("^#[0-9A-Fa-f]{6}$",
                $"palette entry '{hex}' must be a 7-character #RRGGBB hex string");
        }
    }
}
