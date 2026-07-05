using DatPlotX.Helpers;
using DatPlotX.Models;
using System.Data;

namespace DatPlotX.Services;

/// <summary>
/// Service for saving and loading DatPlot project files (.DPX)
/// Acts as a facade coordinating serialization and compression
/// </summary>
public class ProjectFileService
{
    private readonly IProjectSerializer _serializer;
    private readonly IProjectCompressor _compressor;

    public ProjectFileService() : this(new ProjectSerializer(), new ProjectCompressor())
    {
    }

    public ProjectFileService(IProjectSerializer serializer, IProjectCompressor compressor)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _compressor = compressor ?? throw new ArgumentNullException(nameof(compressor));
    }

    /// <summary>
    /// Save project to .DPX file (JSON + GZip compressed)
    /// </summary>
    public async Task SaveProjectAsync(ProjectSettingsModel project, string filePath)
    {
        // Serialize to JSON
        var json = _serializer.SerializeToJson(project);

        // Compress and save
        await _compressor.CompressAsync(json, filePath);
    }

    /// <summary>
    /// Load project from .DPX file (JSON + GZip compressed)
    /// </summary>
    public async Task<ProjectSettingsModel> LoadProjectAsync(string filePath)
    {
        try
        {
            // Decompress file
            var json = await _compressor.DecompressAsync(filePath);

            // Deserialize from JSON
            return _serializer.DeserializeFromJson(json);
        }
        catch (InvalidDataException)
        {
            throw new NotSupportedException(
                "File is not a valid DatPlotX project (expected GZip-compressed JSON).");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new NotSupportedException(
                "File is not a valid DatPlotX project (JSON could not be parsed).");
        }
        catch (Exception ex) when (ex is not NotSupportedException)
        {
            // SECURITY: Log detailed error but throw sanitized message (CWE-209)
            SafeErrorHandler.LogError(ex, "loading project", $"File: {Path.GetFileName(filePath)}");
            var userMessage = SafeErrorHandler.GetUserFriendlyMessage(ex, "loading the project");
            throw new InvalidOperationException(userMessage, ex);
        }
    }

    /// <summary>
    /// Save project as uncompressed JSON (for debugging or manual editing)
    /// </summary>
    public async Task SaveProjectAsJsonAsync(ProjectSettingsModel project, string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        var json = _serializer.SerializeToJson(project);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Load project from uncompressed JSON file
    /// </summary>
    public async Task<ProjectSettingsModel> LoadProjectFromJsonAsync(string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForLoad(filePath);

        var json = await File.ReadAllTextAsync(filePath);
        return _serializer.DeserializeFromJson(json);
    }

    /// <summary>
    /// Check if a file is a valid DatPlot project file
    /// </summary>
    public async Task<(bool IsValid, string? Format, string? ErrorMessage)> ValidateProjectFileAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return (false, null, "File not found");

            // Try to read as modern format
            try
            {
                await LoadProjectAsync(filePath);
                return (true, "Modern (JSON+GZip)", null);
            }
            catch (InvalidDataException)
            {
                // Not GZip compressed, might be uncompressed JSON
                try
                {
                    await LoadProjectFromJsonAsync(filePath);
                    return (true, "JSON (uncompressed)", null);
                }
                catch
                {
                    return (false, "Legacy or Unknown", "Unsupported format");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Create a new empty project with default settings
    /// </summary>
    public ProjectSettingsModel CreateNewProject(string projectName = "Untitled Project")
    {
        return new ProjectSettingsModel
        {
            ProjectName = projectName,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now,
            PlotTitle = "DatPlot",
            XAxisLabel = "Time (s)",
            YAxisLabel = "Value",
            PaneCount = 1,
            ShowGrid = true,
            ShowLegend = true
        };
    }

    /// <summary>
    /// Export project settings to a human-readable summary file
    /// </summary>
    public async Task ExportProjectSummaryAsync(ProjectSettingsModel project, string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        var lines = new List<string>
        {
            "DatPlot Project Summary",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            new string('=', 80),
            "",
            "PROJECT INFORMATION",
            new string('-', 80),
            $"Name: {project.ProjectName}",
            $"Created: {project.CreatedAt:yyyy-MM-dd HH:mm:ss}",
            $"Last Modified: {project.LastModified:yyyy-MM-dd HH:mm:ss}",
            $"Author: {project.Author ?? "N/A"}",
            $"Description: {project.Description ?? "N/A"}",
            "",
            "PLOT SETTINGS",
            new string('-', 80),
            $"Title: {project.PlotTitle}",
            $"X-Axis Label: {project.XAxisLabel}",
            $"Y-Axis Label: {project.YAxisLabel}",
            $"Panes: {project.PaneCount}",
            $"Show Grid: {project.ShowGrid}",
            $"Show Legend: {project.ShowLegend}",
            ""
        };

        if (project.PlotData != null)
        {
            lines.Add("DATA SOURCE");
            lines.Add(new string('-', 80));
            lines.Add($"Source: {project.PlotData.SourceName}");
            lines.Add($"Path: {project.PlotData.SourcePath ?? "N/A"}");
            lines.Add($"Rows: {project.PlotData.RowCount}");
            lines.Add($"Columns: {project.PlotData.ColumnCount}");
            lines.Add($"Column Names: {string.Join(", ", project.PlotData.ColumnNames)}");
            lines.Add("");
        }

        if (project.Curves.Count > 0)
        {
            lines.Add("CURVES");
            lines.Add(new string('-', 80));
            foreach (var curve in project.Curves)
            {
                lines.Add($"  - {curve.Name} ({curve.SourceColumn}) - Color: {curve.Color}, " +
                         $"Line Width: {curve.LineWidth}, Y-Axis: {curve.YAxis}");
            }
            lines.Add("");
        }

        if (project.EventLines.Count > 0)
        {
            lines.Add("EVENT LINES");
            lines.Add(new string('-', 80));
            foreach (var eventLine in project.EventLines.OrderBy(e => e.XPosition))
            {
                lines.Add($"  - {eventLine.Label}: X = {eventLine.XPosition:F4} - Color: {eventLine.Color}, " +
                         $"Line Width: {eventLine.LineWidth}");
                if (!string.IsNullOrWhiteSpace(eventLine.Description))
                    lines.Add($"    Description: {eventLine.Description}");
            }
        }

        await File.WriteAllLinesAsync(filePath, lines);
    }
}
