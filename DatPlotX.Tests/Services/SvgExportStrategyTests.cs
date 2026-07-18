using DatPlotX.Services.Export;
using FluentAssertions;
using ScottPlot;

namespace DatPlotX.Tests.Services;

public class SvgExportStrategyTests
{
    private static string Temp(string ext)
        => Path.Combine(Path.GetTempPath(), $"dpx_svg_{Guid.NewGuid():N}{ext}");

    private static Plot MakePlot(double offset)
    {
        var plot = new Plot();
        plot.Add.Scatter(new double[] { 0, 1, 2, 3 }, new double[] { offset, offset + 1, offset + 2, offset + 3 });
        return plot;
    }

    // PNG files start with this 8-byte signature. The bug (#8) wrote PNG bytes into the .svg file;
    // this guards against a regression to that.
    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Fact]
    public void ExportMultiplePlots_WritesRealSvg_NotPng()
    {
        var strategy = new SvgExportStrategy();
        var path = Temp(".svg");
        try
        {
            strategy.ExportMultiplePlots(new List<Plot> { MakePlot(0), MakePlot(10) }, path, 800, 600);

            var bytes = File.ReadAllBytes(path);
            bytes.Take(PngMagic.Length).Should().NotEqual(PngMagic, "the .svg must not contain PNG bytes");

            var content = File.ReadAllText(path);
            content.Should().Contain("<svg");
            content.Should().Contain("</svg>");
            // Two panes → the outer document plus one nested <svg> per pane.
            System.Text.RegularExpressions.Regex.Count(content, "<svg").Should().BeGreaterThanOrEqualTo(3);

            // Must be well-formed XML.
            var act = () => System.Xml.Linq.XDocument.Parse(content);
            act.Should().NotThrow();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExportMultiplePlots_EmptyList_Throws()
    {
        var strategy = new SvgExportStrategy();
        var act = () => strategy.ExportMultiplePlots(new List<Plot>(), Temp(".svg"), 100, 100);
        act.Should().Throw<ArgumentException>();
    }
}
