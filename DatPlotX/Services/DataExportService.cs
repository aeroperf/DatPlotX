using CsvHelper;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Services.Export;
using ScottPlot;
using System.Data;
using System.Globalization;
using System.Text;
using OxyPlotModel = OxyPlot.PlotModel;
using OxyPngExporter = OxyPlot.SkiaSharp.PngExporter;
using OxyJpegExporter = OxyPlot.SkiaSharp.JpegExporter;

namespace DatPlotX.Services;

/// <summary>
/// Service for exporting data and plots to various formats
/// Uses Strategy pattern for image exports (OCP compliance)
/// </summary>
public class DataExportService : IDataExportService
{
    private readonly IExportStrategyFactory _exportStrategyFactory;

    public DataExportService() : this(new ExportStrategyFactory())
    {
    }

    public DataExportService(IExportStrategyFactory exportStrategyFactory)
    {
        _exportStrategyFactory = exportStrategyFactory ?? throw new ArgumentNullException(nameof(exportStrategyFactory));
    }

    /// <summary>
    /// Export intersection points to CSV file
    /// </summary>
    public async Task ExportIntersectionsToCsvAsync(
        List<IntersectionPointModel> intersections,
        string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        await Task.Run(() =>
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write header
            csv.WriteField("Event Line");
            csv.WriteField("X Position");
            csv.WriteField("Curve Name");
            csv.WriteField("Y Value");
            csv.WriteField("Pane Index");
            csv.WriteField("Y Axis");
            csv.NextRecord();

            // Write data
            foreach (var intersection in intersections)
            {
                csv.WriteField(intersection.EventLineLabel);
                csv.WriteField(intersection.XPosition);
                csv.WriteField(intersection.CurveName);
                csv.WriteField(intersection.YValue);
                csv.WriteField(intersection.PaneIndex);
                csv.WriteField(intersection.YAxis.ToString());
                csv.NextRecord();
            }
        });
    }

    /// <summary>
    /// Export intersection points to tab-delimited file
    /// </summary>
    public async Task ExportIntersectionsToTabAsync(
        List<IntersectionPointModel> intersections,
        string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        await Task.Run(() =>
        {
            var lines = new List<string>
            {
                "Event Line\tX Position\tCurve Name\tY Value\tPane Index\tY Axis"
            };

            foreach (var intersection in intersections)
            {
                lines.Add($"{intersection.EventLineLabel}\t" +
                         $"{intersection.XPosition}\t" +
                         $"{intersection.CurveName}\t" +
                         $"{intersection.YValue}\t" +
                         $"{intersection.PaneIndex}\t" +
                         $"{intersection.YAxis}");
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        });
    }

    /// <summary>
    /// Export DataTable to CSV file
    /// </summary>
    public async Task ExportDataTableToCsvAsync(DataTable dataTable, string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        await Task.Run(() =>
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            // Write header
            foreach (DataColumn column in dataTable.Columns)
            {
                csv.WriteField(column.ColumnName);
            }
            csv.NextRecord();

            // Write rows
            foreach (DataRow row in dataTable.Rows)
            {
                foreach (var item in row.ItemArray)
                {
                    csv.WriteField(item);
                }
                csv.NextRecord();
            }
        });
    }

    public async Task ExportRowsToCsvAsync(IReadOnlyList<IReadOnlyList<string>> rows, string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        await Task.Run(() =>
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            foreach (var row in rows)
            {
                foreach (var field in row)
                    csv.WriteField(field);
                csv.NextRecord();
            }
        });
    }

    /// <summary>
    /// Export plot to PNG image (delegates to strategy)
    /// </summary>
    public void ExportPlotToPng(Plot plot, string filePath, int width = 1920, int height = 1080)
    {
        _exportStrategyFactory.GetStrategy(".png").ExportSinglePlot(plot, filePath, width, height);
    }

    /// <summary>
    /// Export plot to JPEG image (delegates to strategy)
    /// </summary>
    public void ExportPlotToJpeg(Plot plot, string filePath, int width = 1920, int height = 1080, int quality = 90)
    {
        _exportStrategyFactory.GetStrategy(".jpg").ExportSinglePlot(plot, filePath, width, height);
    }

    /// <summary>
    /// Export plot to BMP image (delegates to strategy)
    /// </summary>
    public void ExportPlotToBmp(Plot plot, string filePath, int width = 1920, int height = 1080)
    {
        _exportStrategyFactory.GetStrategy(".bmp").ExportSinglePlot(plot, filePath, width, height);
    }

    /// <summary>
    /// Export plot to SVG (vector graphics) (delegates to strategy)
    /// </summary>
    public void ExportPlotToSvg(Plot plot, string filePath, int width = 1920, int height = 1080)
    {
        _exportStrategyFactory.GetStrategy(".svg").ExportSinglePlot(plot, filePath, width, height);
    }

    /// <summary>
    /// Export plot to file using automatic format detection from file extension (OCP compliant)
    /// </summary>
    public void ExportPlotByExtension(Plot plot, string filePath, int width = 1920, int height = 1080)
    {
        var extension = Path.GetExtension(filePath);
        _exportStrategyFactory.GetStrategy(extension).ExportSinglePlot(plot, filePath, width, height);
    }

    /// <summary>
    /// Export multiple plots to file using automatic format detection from file extension (OCP compliant)
    /// </summary>
    public void ExportMultiplePlotsByExtension(List<Plot> plots, string filePath, int width, int height)
    {
        var extension = Path.GetExtension(filePath);
        _exportStrategyFactory.GetStrategy(extension).ExportMultiplePlots(plots, filePath, width, height);
    }

    /// <summary>
    /// Export multiple plots combined vertically into a single PNG (delegates to strategy)
    /// </summary>
    public void ExportMultiplePlotsToPng(List<Plot> plots, string filePath, int width, int height)
    {
        _exportStrategyFactory.GetStrategy(".png").ExportMultiplePlots(plots, filePath, width, height);
    }

    /// <summary>
    /// Export multiple plots combined vertically into a single JPEG (delegates to strategy)
    /// </summary>
    public void ExportMultiplePlotsToJpeg(List<Plot> plots, string filePath, int width, int height, int quality = 90)
    {
        _exportStrategyFactory.GetStrategy(".jpg").ExportMultiplePlots(plots, filePath, width, height);
    }

    /// <summary>
    /// Export multiple plots combined vertically into a single BMP (delegates to strategy)
    /// </summary>
    public void ExportMultiplePlotsToBmp(List<Plot> plots, string filePath, int width, int height)
    {
        _exportStrategyFactory.GetStrategy(".bmp").ExportMultiplePlots(plots, filePath, width, height);
    }

    /// <summary>
    /// Export multiple plots combined vertically into a single SVG (delegates to strategy)
    /// </summary>
    public void ExportMultiplePlotsToSvg(List<Plot> plots, string filePath, int width, int height)
    {
        _exportStrategyFactory.GetStrategy(".svg").ExportMultiplePlots(plots, filePath, width, height);
    }

    /// <summary>
    /// Export an OxyPlot PlotModel (Compact Plot Surface) to an image file. Format chosen
    /// by extension: PNG/JPEG go through OxyPlot.SkiaSharp; SVG uses OxyPlot.SvgExporter.
    /// The model's <see cref="OxyPlotModel.Background"/> is set by <c>CompactPlotViewModel.Rebuild</c>
    /// (defaults white when alpha is zero), so no swap is needed here.
    /// </summary>
    public void ExportOxyPlotByExtension(OxyPlotModel plot, string filePath, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(plot);

        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        switch (extension)
        {
            case ".png":
                {
                    var exporter = new OxyPngExporter { Width = width, Height = height };
                    using var stream = File.Create(filePath);
                    exporter.Export(plot, stream);
                    break;
                }
            case ".jpg":
            case ".jpeg":
                {
                    var exporter = new OxyJpegExporter { Width = width, Height = height };
                    using var stream = File.Create(filePath);
                    exporter.Export(plot, stream);
                    break;
                }
            case ".svg":
                {
                    var svg = OxyPlot.SvgExporter.ExportToString(plot, width, height, isDocument: true);
                    File.WriteAllText(filePath, svg);
                    break;
                }
            default:
                throw new NotSupportedException($"Unsupported image format: '{extension}'");
        }
    }

    /// <summary>
    /// Export comprehensive report (intersections + summary statistics)
    /// </summary>
    public async Task ExportComprehensiveReportAsync(
        List<IntersectionPointModel> intersections,
        DataTable? sourceData,
        string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        await Task.Run(() =>
        {
            var lines = new List<string>
            {
                "DatPlot Export Report",
                $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                new string('=', 80),
                ""
            };

            // Summary section
            lines.Add("SUMMARY");
            lines.Add(new string('-', 80));
            lines.Add($"Total Event Lines: {intersections.Select(i => i.EventLineId).Distinct().Count()}");
            lines.Add($"Total Curves: {intersections.Select(i => i.CurveName).Distinct().Count()}");
            lines.Add($"Total Intersections: {intersections.Count}");
            lines.Add("");

            // Intersection data
            lines.Add("INTERSECTION POINTS");
            lines.Add(new string('-', 80));
            lines.Add($"{"Event Line",-15} {"X Position",-15} {"Curve Name",-20} {"Y Value",-15} {"Y Axis",-10}");
            lines.Add(new string('-', 80));

            foreach (var intersection in intersections.OrderBy(i => i.EventLineLabel).ThenBy(i => i.CurveName))
            {
                lines.Add($"{intersection.EventLineLabel,-15} " +
                         $"{intersection.XPosition,-15:F4} " +
                         $"{intersection.CurveName,-20} " +
                         $"{intersection.YValue,-15:F4} " +
                         $"{intersection.YAxis,-10}");
            }

            lines.Add("");

            // Statistics by event line
            lines.Add("STATISTICS BY EVENT LINE");
            lines.Add(new string('-', 80));

            var eventLineGroups = intersections.GroupBy(i => i.EventLineLabel).OrderBy(g => g.Key);
            foreach (var group in eventLineGroups)
            {
                lines.Add($"\nEvent Line: {group.Key} (X = {group.First().XPosition:F4})");
                lines.Add($"  Min Y: {group.Min(i => i.YValue):F4}");
                lines.Add($"  Max Y: {group.Max(i => i.YValue):F4}");
                lines.Add($"  Mean Y: {group.Average(i => i.YValue):F4}");
            }

            // Source data summary (if available)
            if (sourceData != null)
            {
                lines.Add("");
                lines.Add("SOURCE DATA SUMMARY");
                lines.Add(new string('-', 80));
                lines.Add($"Rows: {sourceData.Rows.Count}");
                lines.Add($"Columns: {sourceData.Columns.Count}");
                lines.Add($"Column Names: {string.Join(", ", sourceData.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
            }

            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        });
    }

    /// <summary>
    /// Generate summary statistics for a curve
    /// </summary>
    public CurveStatistics CalculateCurveStatistics(double[] data, string curveName)
    {
        if (data.Length == 0)
            throw new ArgumentException("Data array cannot be empty", nameof(data));

        return new CurveStatistics
        {
            CurveName = curveName,
            Count = data.Length,
            Min = data.Min(),
            Max = data.Max(),
            Mean = data.Average(),
            StdDev = CalculateStandardDeviation(data),
            Sum = data.Sum()
        };
    }

    /// <summary>
    /// Export statistics for multiple curves to CSV
    /// </summary>
    public async Task ExportStatisticsToCsvAsync(
        List<CurveStatistics> statistics,
        string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        await Task.Run(() =>
        {
            using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csv.WriteField("Curve Name");
            csv.WriteField("Count");
            csv.WriteField("Min");
            csv.WriteField("Max");
            csv.WriteField("Mean");
            csv.WriteField("Std Dev");
            csv.WriteField("Sum");
            csv.NextRecord();

            foreach (var stat in statistics)
            {
                csv.WriteField(stat.CurveName);
                csv.WriteField(stat.Count);
                csv.WriteField(stat.Min);
                csv.WriteField(stat.Max);
                csv.WriteField(stat.Mean);
                csv.WriteField(stat.StdDev);
                csv.WriteField(stat.Sum);
                csv.NextRecord();
            }
        });
    }

    #region Private Helper Methods

    private static double CalculateStandardDeviation(double[] values)
    {
        if (values.Length <= 1) return 0;

        double mean = 0;
        for (int i = 0; i < values.Length; i++) mean += values[i];
        mean /= values.Length;

        double sumOfSquares = 0;
        for (int i = 0; i < values.Length; i++)
        {
            double delta = values[i] - mean;
            sumOfSquares += delta * delta;
        }
        return Math.Sqrt(sumOfSquares / (values.Length - 1));
    }

    #endregion
}

/// <summary>
/// Statistical information for a curve
/// </summary>
public class CurveStatistics
{
    public string CurveName { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double Sum { get; set; }
}
