using Avalonia.Platform.Storage;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Views;
using ScottPlot;
using System.Data;
using OxyPlotModel = OxyPlot.PlotModel;

namespace DatPlotX.Services;

/// <summary>
/// Service for handling file operations using Avalonia IStorageProvider
/// </summary>
public class FileOperationsService : IFileOperationsService
{
    private static readonly string[] DataFilePatterns = { "*.csv", "*.tab", "*.txt", "*.tsv" };
    private static readonly string[] CsvPatterns = { "*.csv" };
    private static readonly string[] TabPatterns = { "*.tab", "*.tsv" };
    private static readonly string[] TxtPatterns = { "*.txt" };
    private static readonly string[] AllFilesPatterns = { "*" };
    private static readonly string[] DpxPatterns = { "*.dpx" };
    private static readonly string[] PngPatterns = { "*.png" };
    private static readonly string[] JpegPatterns = { "*.jpg", "*.jpeg" };
    private static readonly string[] BmpPatterns = { "*.bmp" };
    private static readonly string[] SvgPatterns = { "*.svg" };
    private static readonly string[] TabExportPatterns = { "*.tab" };

    private readonly IDataImportService _importService;
    private readonly IDataExportService _exportService;
    private readonly ProjectFileService _projectService;
    private readonly IDialogService _dialogService;

    public FileOperationsService(
        IDataImportService importService,
        IDataExportService exportService,
        ProjectFileService projectService,
        IDialogService dialogService)
    {
        _importService = importService;
        _exportService = exportService;
        _projectService = projectService;
        _dialogService = dialogService;
    }

    private static IStorageProvider? GetStorageProvider()
    {
        var window = AppWindowHelper.GetMainWindow();
        return window?.StorageProvider;
    }

    public async Task<FileOperationResult<PlotDataModel>> ImportDataFileAsync()
    {
        try
        {
            var storageProvider = GetStorageProvider();
            if (storageProvider == null)
                return FileOperationResult.Failed<PlotDataModel>("Storage provider unavailable");

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Data File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Data Files") { Patterns = DataFilePatterns },
                    new FilePickerFileType("CSV Files") { Patterns = CsvPatterns },
                    new FilePickerFileType("Tab Files") { Patterns = TabPatterns },
                    new FilePickerFileType("Text Files") { Patterns = TxtPatterns },
                    new FilePickerFileType("All Files") { Patterns = AllFilesPatterns }
                }
            });

            if (files.Count == 0) return FileOperationResult.Cancelled<PlotDataModel>();
            var filePath = files[0].Path.LocalPath;

            var options = await _dialogService.ShowImportOptionsAsync(filePath);
            if (options is null) return FileOperationResult.Cancelled<PlotDataModel>();

            var data = await _importService.ImportDataAsync(filePath, options);
            return FileOperationResult.Success(data);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error opening file: {ex.Message}", "Import Error");
            return FileOperationResult.Failed<PlotDataModel>(ex.Message);
        }
    }

    public async Task<FileOperationResult<(ProjectSettingsModel Project, string FilePath)>> LoadProjectFileAsync()
    {
        try
        {
            var storageProvider = GetStorageProvider();
            if (storageProvider == null)
                return FileOperationResult.Failed<(ProjectSettingsModel, string)>("Storage provider unavailable");

            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open Project",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DatPlot Project") { Patterns = DpxPatterns },
                    new FilePickerFileType("All Files") { Patterns = AllFilesPatterns }
                }
            });

            if (files.Count == 0)
                return FileOperationResult.Cancelled<(ProjectSettingsModel, string)>();

            var path = files[0].Path.LocalPath;
            var project = await _projectService.LoadProjectAsync(path);
            return FileOperationResult.Success<(ProjectSettingsModel, string)>((project, path));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error opening project: {ex.Message}", "Open Error");
            return FileOperationResult.Failed<(ProjectSettingsModel, string)>(ex.Message);
        }
    }

    public async Task<FileOperationResult<ProjectSettingsModel>> LoadProjectFromPathAsync(string filePath)
    {
        try
        {
            var project = await _projectService.LoadProjectAsync(filePath);
            return FileOperationResult.Success(project);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error opening project: {ex.Message}", "Open Error");
            return FileOperationResult.Failed<ProjectSettingsModel>(ex.Message);
        }
    }

    public async Task<FileOperationResult<string>> SaveProjectAsync(ProjectSettingsModel project, string? currentFilePath)
    {
        try
        {
            string? filePath = currentFilePath;

            if (filePath == null)
            {
                var storageProvider = GetStorageProvider();
                if (storageProvider == null) return FileOperationResult.Cancelled<string>();

                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Project As",
                    DefaultExtension = "dpx",
                    SuggestedFileName = FileDialogHelper.GetDefaultFileNameWithoutExtension("DatPlot_Project"),
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("DatPlot Project") { Patterns = DpxPatterns }
                    }
                });

                if (file == null) return FileOperationResult.Cancelled<string>();
                filePath = file.Path.LocalPath;
            }

            await _projectService.SaveProjectAsync(project, filePath);
            return FileOperationResult.Success(filePath);
        }
        catch (Exception ex)
        {
            // The dialog surfaces the error; return Failed (not Cancelled) so the caller doesn't
            // report a failed save as "cancelled" and leave the user thinking nothing went wrong.
            await _dialogService.ShowError($"Error saving project: {ex.Message}", "Save Error");
            return FileOperationResult.Failed<string>(ex.Message);
        }
    }

    public async Task<bool> ExportPlotsAsync(List<Plot> plotModels)
    {
        try
        {
            if (plotModels.Count == 0)
            {
                await _dialogService.ShowInformation("No plots to export.", "Export");
                return false;
            }

            var parent = AppWindowHelper.GetMainWindow();
            if (parent == null) return false;

            var orientationDialog = new ExportOrientationDialog();
            var dialogResult = await orientationDialog.ShowDialog<bool?>(parent);
            if (dialogResult != true) return false;

            var (width, height) = orientationDialog.IsLandscape ? (1920, 1080) : (1080, 1920);

            var storageProvider = GetStorageProvider();
            if (storageProvider == null) return false;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Plot as Image",
                DefaultExtension = "png",
                SuggestedFileName = FileDialogHelper.GetDefaultFileName("DatPlot", "png"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = PngPatterns },
                    new FilePickerFileType("JPEG Image") { Patterns = JpegPatterns },
                    new FilePickerFileType("BMP Image") { Patterns = BmpPatterns },
                    new FilePickerFileType("SVG Image") { Patterns = SvgPatterns }
                }
            });

            if (file == null) return false;

            ExportPlotsByFormat(plotModels, file.Path.LocalPath, width, height);
            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error exporting image: {ex.Message}", "Export Error");
            return false;
        }
    }

    public async Task<bool> ExportCompactPlotAsync(OxyPlotModel plotModel)
    {
        try
        {
            if (plotModel is null)
            {
                await _dialogService.ShowInformation("No plot to export.", "Export");
                return false;
            }

            var parent = AppWindowHelper.GetMainWindow();
            if (parent == null) return false;

            var orientationDialog = new ExportOrientationDialog();
            var dialogResult = await orientationDialog.ShowDialog<bool?>(parent);
            if (dialogResult != true) return false;

            var (width, height) = orientationDialog.IsLandscape ? (1920, 1080) : (1080, 1920);

            var storageProvider = GetStorageProvider();
            if (storageProvider == null) return false;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Plot as Image",
                DefaultExtension = "png",
                SuggestedFileName = FileDialogHelper.GetDefaultFileName("DatPlot", "png"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = PngPatterns },
                    new FilePickerFileType("JPEG Image") { Patterns = JpegPatterns },
                    new FilePickerFileType("SVG Image") { Patterns = SvgPatterns }
                }
            });

            if (file == null) return false;

            _exportService.ExportOxyPlotByExtension(plotModel, file.Path.LocalPath, width, height);
            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error exporting image: {ex.Message}", "Export Error");
            return false;
        }
    }

    public async Task<bool> ExportGroupedPlotAsync(Plot plot)
    {
        try
        {
            if (plot is null)
            {
                await _dialogService.ShowInformation("No plot to export.", "Export");
                return false;
            }

            var parent = AppWindowHelper.GetMainWindow();
            if (parent == null) return false;

            var orientationDialog = new ExportOrientationDialog();
            var dialogResult = await orientationDialog.ShowDialog<bool?>(parent);
            if (dialogResult != true) return false;

            var (width, height) = orientationDialog.IsLandscape ? (1920, 1080) : (1080, 1920);

            var storageProvider = GetStorageProvider();
            if (storageProvider == null) return false;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Plot as Image",
                DefaultExtension = "png",
                SuggestedFileName = FileDialogHelper.GetDefaultFileName("DatPlot", "png"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image")  { Patterns = PngPatterns },
                    new FilePickerFileType("JPEG Image") { Patterns = JpegPatterns },
                    new FilePickerFileType("BMP Image")  { Patterns = BmpPatterns },
                }
            });

            if (file == null) return false;

            plot.Save(file.Path.LocalPath, width, height);
            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error exporting image: {ex.Message}", "Export Error");
            return false;
        }
    }

    public async Task<bool> ExportIntersectionsAsync(DataTable intersectionData)
    {
        try
        {
            if (intersectionData.Rows.Count == 0)
            {
                await _dialogService.ShowInformation("No intersection data to export.", "Export");
                return false;
            }

            var storageProvider = GetStorageProvider();
            if (storageProvider == null) return false;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Intersection Data",
                DefaultExtension = "csv",
                SuggestedFileName = FileDialogHelper.GetDefaultFileName("Intersections", "csv"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files") { Patterns = CsvPatterns },
                    new FilePickerFileType("Tab-delimited Files") { Patterns = TabExportPatterns }
                }
            });

            if (file == null) return false;

            await _exportService.ExportDataTableToCsvAsync(intersectionData, file.Path.LocalPath);
            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error exporting data: {ex.Message}", "Export Error");
            return false;
        }
    }

    public async Task<bool> ExportAnalysisResultsAsync(
        IReadOnlyList<IReadOnlyList<string>> rows, string suggestedName)
    {
        try
        {
            if (rows.Count == 0)
            {
                await _dialogService.ShowInformation("No analysis results to export.", "Export");
                return false;
            }

            var storageProvider = GetStorageProvider();
            if (storageProvider == null) return false;

            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Analysis Results",
                DefaultExtension = "csv",
                SuggestedFileName = FileDialogHelper.GetDefaultFileName(suggestedName, "csv"),
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files") { Patterns = CsvPatterns }
                }
            });

            if (file == null) return false;

            await _exportService.ExportRowsToCsvAsync(rows, file.Path.LocalPath);
            return true;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error exporting results: {ex.Message}", "Export Error");
            return false;
        }
    }

    private void ExportPlotsByFormat(List<Plot> plotModels, string fileName, int width, int height)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        switch (extension)
        {
            case ".png":
                _exportService.ExportMultiplePlotsToPng(plotModels, fileName, width, height);
                break;
            case ".jpg":
            case ".jpeg":
                _exportService.ExportMultiplePlotsToJpeg(plotModels, fileName, width, height);
                break;
            case ".bmp":
                _exportService.ExportMultiplePlotsToBmp(plotModels, fileName, width, height);
                break;
            case ".svg":
                _exportService.ExportMultiplePlotsToSvg(plotModels, fileName, width, height);
                break;
        }
    }
}
