using DatPlotX.Models;
using DatPlotX.Services;
using FluentAssertions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Data;

namespace DatPlotX.Tests.Services;

public class DataExportServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var f in _tempFiles)
        {
            try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    private string Temp(string ext)
    {
        var p = Path.Combine(Path.GetTempPath(), $"datplot_export_test_{Guid.NewGuid():N}{ext}");
        _tempFiles.Add(p);
        return p;
    }

    [Fact]
    public void CalculateCurveStatistics_EmptyData_Throws()
    {
        var svc = new DataExportService();
        var act = () => svc.CalculateCurveStatistics([], "c");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateCurveStatistics_SingleValue_StdDevIsZero()
    {
        var svc = new DataExportService();
        var stats = svc.CalculateCurveStatistics([5.0], "c");
        stats.Count.Should().Be(1);
        stats.Min.Should().Be(5.0);
        stats.Max.Should().Be(5.0);
        stats.Mean.Should().Be(5.0);
        stats.StdDev.Should().Be(0);
    }

    [Fact]
    public void CalculateCurveStatistics_KnownSample_ReturnsExpectedStdDev()
    {
        var svc = new DataExportService();
        // [2, 4, 4, 4, 5, 5, 7, 9] has known sample std dev ~2.138089935
        double[] data = [2, 4, 4, 4, 5, 5, 7, 9];
        var stats = svc.CalculateCurveStatistics(data, "x");
        stats.Mean.Should().BeApproximately(5.0, 1e-9);
        stats.StdDev.Should().BeApproximately(2.138089935, 1e-6);
        stats.Sum.Should().Be(40);
        stats.Count.Should().Be(8);
    }

    [Fact]
    public async Task ExportDataTableToCsvAsync_WritesHeaderAndRows()
    {
        var svc = new DataExportService();
        var table = new DataTable();
        table.Columns.Add("a", typeof(double));
        table.Columns.Add("b", typeof(double));
        table.Rows.Add(1.0, 2.0);
        table.Rows.Add(3.0, 4.0);

        var path = Temp(".csv");
        await svc.ExportDataTableToCsvAsync(table, path);

        var lines = File.ReadAllLines(path);
        lines[0].Should().Be("a,b");
        lines[1].Should().Be("1,2");
        lines[2].Should().Be("3,4");
    }

    [Fact]
    public async Task ExportRowsToCsvAsync_WritesMatrix_AndQuotesSpecials()
    {
        var svc = new DataExportService();
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Curve", "Mean", "Note" },
            new[] { "Altitude", "1000", "ok" },
            new[] { "A, B", "5", "has \"quote\"" }, // comma + embedded quote ⇒ must be CSV-quoted
        };

        var path = Temp(".csv");
        await svc.ExportRowsToCsvAsync(rows, path);

        var lines = File.ReadAllLines(path);
        lines[0].Should().Be("Curve,Mean,Note");
        lines[1].Should().Be("Altitude,1000,ok");
        lines[2].Should().Be("\"A, B\",5,\"has \"\"quote\"\"\"");
    }

    [Fact]
    public async Task ExportRowsToCsvAsync_RaggedRows_WriteShortAndBlankRows()
    {
        var svc = new DataExportService();
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Curve", "Mean" },
            Array.Empty<string>(),              // blank separator row
            new[] { "Tolerance Bands" },        // section header, single cell
        };

        var path = Temp(".csv");
        await svc.ExportRowsToCsvAsync(rows, path);

        var lines = File.ReadAllLines(path);
        lines[0].Should().Be("Curve,Mean");
        lines[1].Should().BeEmpty();
        lines[2].Should().Be("Tolerance Bands");
    }

    [Fact]
    public async Task ExportIntersectionsToCsvAsync_WritesAllFields()
    {
        var svc = new DataExportService();
        var data = new List<IntersectionPointModel>
        {
            new()
            {
                EventLineId = Guid.NewGuid(),
                EventLineLabel = "liftoff",
                XPosition = 1.5,
                CurveName = "altitude",
                YValue = 1000.0,
                PaneIndex = 0,
                YAxis = YAxisType.Y1
            }
        };

        var path = Temp(".csv");
        await svc.ExportIntersectionsToCsvAsync(data, path);

        var content = File.ReadAllText(path);
        content.Should().Contain("Event Line")
            .And.Contain("liftoff")
            .And.Contain("altitude")
            .And.Contain("1000");
    }

    [Fact]
    public async Task ExportIntersectionsToTabAsync_UsesTabDelimiter()
    {
        var svc = new DataExportService();
        var data = new List<IntersectionPointModel>
        {
            new()
            {
                EventLineLabel = "evt",
                XPosition = 1.0,
                CurveName = "c",
                YValue = 2.0,
                PaneIndex = 0,
                YAxis = YAxisType.Y1
            }
        };

        var path = Temp(".tab");
        await svc.ExportIntersectionsToTabAsync(data, path);
        var content = File.ReadAllText(path);
        content.Should().Contain("\t");
    }

    [Fact]
    public async Task ExportComprehensiveReportAsync_ContainsAllSections()
    {
        var svc = new DataExportService();
        var intersections = new List<IntersectionPointModel>
        {
            new() { EventLineLabel = "e1", XPosition = 1.0, CurveName = "c", YValue = 2.0, PaneIndex = 0, YAxis = YAxisType.Y1 }
        };
        var table = new DataTable();
        table.Columns.Add("col", typeof(double));
        table.Rows.Add(1.0);

        var path = Temp(".txt");
        await svc.ExportComprehensiveReportAsync(intersections, table, path);

        var content = File.ReadAllText(path);
        content.Should().Contain("SUMMARY").And.Contain("INTERSECTION POINTS")
            .And.Contain("STATISTICS BY EVENT LINE").And.Contain("SOURCE DATA SUMMARY");
    }

    private static PlotModel BuildOxyModel()
    {
        var model = new PlotModel { Background = OxyColors.White };
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "X" });
        model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Y" });
        var series = new LineSeries();
        for (int i = 0; i < 10; i++) series.Points.Add(new DataPoint(i, i * 2));
        model.Series.Add(series);
        return model;
    }

    [Theory]
    [InlineData(".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(".jpg", new byte[] { 0xFF, 0xD8, 0xFF })]
    [InlineData(".jpeg", new byte[] { 0xFF, 0xD8, 0xFF })]
    public void ExportOxyPlotByExtension_RasterFormats_WriteValidHeader(string ext, byte[] expectedMagic)
    {
        var svc = new DataExportService();
        var model = BuildOxyModel();
        var path = Temp(ext);

        svc.ExportOxyPlotByExtension(model, path, 800, 600);

        File.Exists(path).Should().BeTrue();
        var actual = new byte[expectedMagic.Length];
        using (var fs = File.OpenRead(path))
        {
            fs.Read(actual, 0, actual.Length).Should().Be(expectedMagic.Length);
        }
        actual.Should().Equal(expectedMagic);
        new FileInfo(path).Length.Should().BeGreaterThan(expectedMagic.Length);
    }

    [Fact]
    public void ExportOxyPlotByExtension_Svg_WritesXmlSvgRoot()
    {
        var svc = new DataExportService();
        var model = BuildOxyModel();
        var path = Temp(".svg");

        svc.ExportOxyPlotByExtension(model, path, 800, 600);

        var content = File.ReadAllText(path);
        content.Should().Contain("<svg");
        new FileInfo(path).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ExportOxyPlotByExtension_NullPlot_Throws()
    {
        var svc = new DataExportService();
        var act = () => svc.ExportOxyPlotByExtension(null!, Temp(".png"), 100, 100);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportOxyPlotByExtension_UnsupportedExtension_Throws()
    {
        var svc = new DataExportService();
        var model = BuildOxyModel();
        var act = () => svc.ExportOxyPlotByExtension(model, Temp(".xyz"), 100, 100);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ExportStatisticsToCsvAsync_WritesStatistics()
    {
        var svc = new DataExportService();
        var stats = new List<CurveStatistics>
        {
            new() { CurveName = "v", Count = 3, Min = 0, Max = 2, Mean = 1, StdDev = 1, Sum = 3 }
        };

        var path = Temp(".csv");
        await svc.ExportStatisticsToCsvAsync(stats, path);

        var content = File.ReadAllText(path);
        content.Should().Contain("Curve Name").And.Contain("v");
    }
}
