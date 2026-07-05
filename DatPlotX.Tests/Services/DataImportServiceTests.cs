using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.Services.Parsers;
using FluentAssertions;
using System.Text;

namespace DatPlotX.Tests.Services;

public class DataImportServiceTests : IDisposable
{
    private readonly List<string> _temp = new();

    public void Dispose()
    {
        foreach (var f in _temp) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
    }

    private string WriteTemp(string contents, string ext = ".csv")
    {
        var path = Path.Combine(Path.GetTempPath(), $"datplot_import_test_{Guid.NewGuid():N}{ext}");
        File.WriteAllText(path, contents, Encoding.UTF8);
        _temp.Add(path);
        return path;
    }

    private static DataImportService Make() => new(new CsvDataParser(new ApplicationSettings()));

    [Fact]
    public void Ctor_NullCsvParser_Throws()
    {
        var act = () => new DataImportService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ImportCsvAsync_DefaultOptions_ParsesFile()
    {
        var path = WriteTemp("a,b\n1,2\n3,4\n");
        var result = await Make().ImportCsvAsync(path);
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ImportCsvAsync_ExplicitOptions_Parses()
    {
        var path = WriteTemp("a;b\n1;2\n");
        var result = await Make().ImportCsvAsync(path, new CsvImportOptions { Delimiter = ";" });
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task ImportTabAsync_ParsesTabDelimited()
    {
        var path = WriteTemp("a\tb\n1\t2\n", ext: ".tab");
        var result = await Make().ImportTabAsync(path);
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task ImportTextAsync_ExplicitDelimiter_Respected()
    {
        var path = WriteTemp("a|b\n1|2\n3|4\n", ext: ".txt");
        var result = await Make().ImportTextAsync(path, "|");
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task ImportAutoDetectAsync_CsvExtension_UsesCsvPath()
    {
        var path = WriteTemp("a,b\n1,2\n");
        var result = await Make().ImportAutoDetectAsync(path);
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task ImportAutoDetectAsync_TabExtension_UsesTabPath()
    {
        var path = WriteTemp("a\tb\n1\t2\n", ext: ".tab");
        var result = await Make().ImportAutoDetectAsync(path);
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task ImportDataAsync_XPlaneFormatOption_UsesXPlaneParser()
    {
        // X-Plane-style file to exercise the xplane branch
        var path = WriteTemp("|_time,sec|alt,ft|\n|0.0|1000.0|\n", ext: ".txt");
        var result = await Make().ImportDataAsync(path, new ImportOptionsModel { IsXPlaneFormat = true });
        result.Data.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task ImportDataAsync_StandardCsvOption_UsesCsvParser()
    {
        var path = WriteTemp("a,b\n1,2\n");
        var result = await Make().ImportDataAsync(path, new ImportOptionsModel { Delimiter = ",", CultureName = "en-US" });
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task OnLargeFileWarning_ForwardedToBothParsers()
    {
        var csv = new CsvDataParser(new ApplicationSettings());
        var xplane = new XPlaneDataParser(csv);
        var svc = new DataImportService(csv, xplane);
        Task<bool> handler(double mb, string msg) => Task.FromResult(true);
        svc.OnLargeFileWarning = handler;
        csv.OnLargeFileWarning.Should().Be((LargeFileWarningCallback)handler);
        xplane.OnLargeFileWarning.Should().Be((LargeFileWarningCallback)handler);
    }
}
