using DatPlotX.Helpers;
using ScottPlot;
using System.Text;
using System.Text.RegularExpressions;

namespace DatPlotX.Services.Export;

/// <summary>
/// Strategy for exporting plots to SVG format.
/// Multi-plot export composites each pane's vector SVG into one document (stacked vertically)
/// via nested &lt;svg&gt; elements, so the result is genuine, editable vector output.
/// </summary>
public partial class SvgExportStrategy : IImageExportStrategy
{
    public string FileExtension => ".svg";
    public string FilterDescription => "SVG Files|*.svg";

    public void ExportSinglePlot(Plot plot, string filePath, int width, int height)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);
        plot.SaveSvg(filePath, width, height);
    }

    public void ExportMultiplePlots(List<Plot> plots, string filePath, int width, int height)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        if (plots == null || plots.Count == 0)
            throw new ArgumentException("No plots to export");

        int paneHeight = height / plots.Count;

        // Composite each pane's SVG into one parent SVG document. Each pane is emitted as its own
        // vector SVG (ScottPlot's GetSvgXml) and embedded as a nested <svg> positioned at the
        // pane's y-offset — nested <svg> is native SVG and needs no parsing of the pane internals.
        // Previously this rasterized every pane to PNG and wrote the bytes into a .svg file, so the
        // "SVG" opened in no vector editor (review #8).
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" ")
          .Append($"viewBox=\"0 0 {width} {height}\">\n");
        sb.Append($"<rect width=\"{width}\" height=\"{height}\" fill=\"white\"/>\n");

        int yOffset = 0;
        foreach (var plot in plots)
        {
            if (plot != null)
            {
                string paneSvg = StripXmlProlog(plot.GetSvgXml(width, paneHeight));
                sb.Append($"<svg x=\"0\" y=\"{yOffset}\" width=\"{width}\" height=\"{paneHeight}\" ")
                  .Append($"viewBox=\"0 0 {width} {paneHeight}\" overflow=\"visible\">\n");
                sb.Append(paneSvg);
                sb.Append("\n</svg>\n");
                yOffset += paneHeight;
            }
        }

        sb.Append("</svg>\n");

        // false = truncate/overwrite; write the assembled UTF-8 SVG document.
        File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>
    /// Remove a leading <c>&lt;?xml …?&gt;</c> declaration (and any leading doctype) from a child
    /// SVG so it can be embedded as a nested element inside the parent document.
    /// </summary>
    private static string StripXmlProlog(string svg)
    {
        svg = svg.TrimStart();
        svg = XmlPrologRegex().Replace(svg, string.Empty, 1);
        svg = DoctypeRegex().Replace(svg, string.Empty, 1);
        return svg.TrimStart();
    }

    [GeneratedRegex(@"^\s*<\?xml[^>]*\?>", RegexOptions.IgnoreCase)]
    private static partial Regex XmlPrologRegex();

    [GeneratedRegex(@"^\s*<!DOCTYPE[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DoctypeRegex();
}
