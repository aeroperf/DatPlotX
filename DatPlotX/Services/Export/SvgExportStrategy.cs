using DatPlotX.Helpers;
using ScottPlot;
using SkiaSharp;

namespace DatPlotX.Services.Export;

/// <summary>
/// Strategy for exporting plots to SVG format
/// Note: For multi-plot export, this currently renders to PNG as true SVG multi-plot
/// would require more complex implementation
/// </summary>
public class SvgExportStrategy : IImageExportStrategy
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

        // For SVG multi-plot export, we render to bitmap and save as PNG
        // True SVG multi-plot export would require more complex implementation
        using var combinedBitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(combinedBitmap);
        canvas.Clear(SKColors.White);

        int yOffset = 0;
        foreach (var plot in plots)
        {
            if (plot != null)
            {
                byte[] imageBytes = plot.GetImageBytes(width, paneHeight, ScottPlot.ImageFormat.Png);
                using var skData = SKData.CreateCopy(imageBytes);
                using var skBitmap = SKBitmap.Decode(skData);
                using var skImage = SKImage.FromBitmap(skBitmap);
                canvas.DrawImage(skImage, 0, yOffset);
                yOffset += paneHeight;
            }
        }

        using var image = SKImage.FromBitmap(combinedBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(filePath);
        data.SaveTo(stream);
    }
}
