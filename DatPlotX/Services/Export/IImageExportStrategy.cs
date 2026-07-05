using ScottPlot;

namespace DatPlotX.Services.Export;

/// <summary>
/// Strategy interface for image export formats (OCP compliance)
/// Each format implements this interface to allow extensibility without modifying existing code
/// </summary>
public interface IImageExportStrategy
{
    /// <summary>
    /// The file extension for this export format (e.g., ".png")
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// The file filter description for save dialogs (e.g., "PNG Files|*.png")
    /// </summary>
    string FilterDescription { get; }

    /// <summary>
    /// Export a single plot to a file
    /// </summary>
    /// <param name="plot">The plot to export</param>
    /// <param name="filePath">The destination file path</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    void ExportSinglePlot(Plot plot, string filePath, int width, int height);

    /// <summary>
    /// Export multiple plots combined vertically into a single image
    /// </summary>
    /// <param name="plots">The plots to export</param>
    /// <param name="filePath">The destination file path</param>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Total image height in pixels</param>
    void ExportMultiplePlots(List<Plot> plots, string filePath, int width, int height);
}
