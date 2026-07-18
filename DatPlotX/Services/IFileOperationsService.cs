using DatPlotX.Models;
using ScottPlot;
using OxyPlotModel = OxyPlot.PlotModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for handling file operations including data import, project load/save, and export operations
/// </summary>
public interface IFileOperationsService
{
    /// <summary>
    /// Import data file with user dialog selection and import options.
    /// </summary>
    /// <returns>A result that distinguishes Success / Cancelled / Failed.</returns>
    Task<FileOperationResult<PlotDataModel>> ImportDataFileAsync();

    /// <summary>
    /// Load project file with user dialog selection.
    /// </summary>
    /// <returns>A result that distinguishes Success / Cancelled / Failed.</returns>
    Task<FileOperationResult<(ProjectSettingsModel Project, string FilePath)>> LoadProjectFileAsync();

    /// <summary>
    /// Load project from a known file path (no dialog).
    /// </summary>
    /// <param name="filePath">Absolute path to the .DPX project file</param>
    /// <returns>A result that distinguishes Success / Failed (no Cancelled path).</returns>
    Task<FileOperationResult<ProjectSettingsModel>> LoadProjectFromPathAsync(string filePath);

    /// <summary>
    /// Save project to file with optional save-as dialog
    /// </summary>
    /// <param name="project">Project to save</param>
    /// <param name="currentFilePath">Current file path (null to force save-as dialog)</param>
    /// <returns>File path where project was saved, or null if user cancelled</returns>
    Task<FileOperationResult<string>> SaveProjectAsync(ProjectSettingsModel project, string? currentFilePath);

    /// <summary>
    /// Export plots as images with user dialog for format/location selection
    /// </summary>
    /// <param name="plotModels">Plot models to export</param>
    /// <returns>True if export succeeded, false if user cancelled</returns>
    Task<bool> ExportPlotsAsync(List<Plot> plotModels);

    /// <summary>
    /// Export the Compact Plot Surface (OxyPlot) to an image with user dialog for format/location.
    /// </summary>
    /// <param name="plotModel">OxyPlot model to export</param>
    /// <returns>True if export succeeded, false if user cancelled</returns>
    Task<bool> ExportCompactPlotAsync(OxyPlotModel plotModel);

    /// <summary>
    /// Export intersection data to CSV with user dialog for location selection
    /// </summary>
    /// <param name="intersectionData">Intersection data table to export</param>
    /// <returns>True if export succeeded, false if user cancelled</returns>
    Task<bool> ExportIntersectionsAsync(System.Data.DataTable intersectionData);

    /// <summary>
    /// Export the Analysis Results table (already rendered to display strings) to a CSV via a save
    /// picker. <paramref name="rows"/> is header-first; <paramref name="suggestedName"/> seeds the
    /// file name. Returns true on success, false on cancel / empty.
    /// </summary>
    Task<bool> ExportAnalysisResultsAsync(
        IReadOnlyList<IReadOnlyList<string>> rows, string suggestedName);

    /// <summary>
    /// Export the Grouped Parameter Plot ScottPlot surface to a PNG/JPG/BMP via the user's
    /// chosen path. ScottPlot's <c>Plot.Save</c> dispatches by extension.
    /// </summary>
    Task<bool> ExportGroupedPlotAsync(Plot plot);
}
