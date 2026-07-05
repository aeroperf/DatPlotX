using DatPlotX.Helpers;
using ScottPlot;
using SkiaSharp;

namespace DatPlotX.Services.Export;

/// <summary>
/// Strategy for exporting plots to JPEG format
/// </summary>
public class JpegExportStrategy : IImageExportStrategy
{
    private readonly int _quality;

    public JpegExportStrategy(int quality = 90)
    {
        _quality = quality;
    }

    public string FileExtension => ".jpg";
    public string FilterDescription => "JPEG Files|*.jpg;*.jpeg";

    public void ExportSinglePlot(Plot plot, string filePath, int width, int height)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);
        plot.SaveJpeg(filePath, width, height, _quality);
    }

    public void ExportMultiplePlots(List<Plot> plots, string filePath, int width, int height)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        if (plots == null || plots.Count == 0)
            throw new ArgumentException("No plots to export");

        int paneHeight = height / plots.Count;

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
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, _quality);
        using var stream = File.Create(filePath);
        data.SaveTo(stream);
    }
}
