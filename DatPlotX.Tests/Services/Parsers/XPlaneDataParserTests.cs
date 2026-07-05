using DatPlotX.Services.Parsers;
using FluentAssertions;
using System.Text;

namespace DatPlotX.Tests.Services.Parsers;

public class XPlaneDataParserTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }

    private string WriteTempXPlane(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"datplot_xplane_test_{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, contents, Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    private static XPlaneDataParser Parser() => new(new CsvDataParser(new DatPlotX.Models.ApplicationSettings()));

    [Fact]
    public async Task ParseAsync_SimpleHeaderCommaSeparatedUnits_ParsesAllColumnsAndRows()
    {
        // X-Plane format: pipe-delimited rows, comma or semicolon as param-unit separator.
        var content = "|_time,sec|alt,ft|vspeed,fpm|\n|0.0|1000.0|500.0|\n|1.0|1050.0|550.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);

        result.Data.Columns.Count.Should().Be(3);
        result.Data.Rows.Count.Should().Be(2);
        // Padding underscores are stripped and the unit is emitted in "name (unit)" form.
        result.Data.Columns[0].ColumnName.Should().Be("time (sec)");
    }

    [Fact]
    public async Task ParseAsync_StripsPaddingUnderscoresAndEmitsParenUnits()
    {
        // Real X-Plane tokens are padded to a fixed width with underscores purely for alignment.
        var content = "|_real,_time|Vtrue,_ktas|____A,_ktas|p-alt,ftMSL|\n|0.0|1.0|2.0|3.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);

        result.Data.Columns[0].ColumnName.Should().Be("real (time)");
        result.Data.Columns[1].ColumnName.Should().Be("Vtrue (ktas)");
        result.Data.Columns[2].ColumnName.Should().Be("A (ktas)");
        result.Data.Columns[3].ColumnName.Should().Be("p-alt (ftMSL)"); // hyphen preserved
    }

    [Fact]
    public async Task ParseAsync_MultiFieldToken_TreatsLastTokenAsUnit()
    {
        // "thrst,_1,lb" → parameter "thrst 1", unit "lb".
        var content = "|_time,sec|thrst,_1,lb|FF__1,gal/h|\n|0.0|100.0|5.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);

        result.Data.Columns[1].ColumnName.Should().Be("thrst 1 (lb)");
        result.Data.Columns[2].ColumnName.Should().Be("FF1 (gal/h)");
    }

    [Fact]
    public async Task ParseAsync_DuplicateNameAndUnit_DisambiguatesWithoutHidingUnit()
    {
        // X-Plane emits two identical "deice,__AOA" columns in its deice block. The duplicate
        // must stay unit-parseable: the disambiguation suffix goes on the name, not after the unit.
        var content = "|_time,sec|deice,__AOA|deice,__AOA|\n|0.0|1.0|0.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);

        result.Data.Columns[1].ColumnName.Should().Be("deice (AOA)");
        result.Data.Columns[2].ColumnName.Should().Be("deice 2 (AOA)");

        // Both must round-trip their unit through the shared parser.
        DatPlotX.Services.Units.UnitHeaderParser.Parse(result.Data.Columns[1].ColumnName).Unit.Should().Be("AOA");
        DatPlotX.Services.Units.UnitHeaderParser.Parse(result.Data.Columns[2].ColumnName).Unit.Should().Be("AOA");
    }

    [Fact]
    public async Task ParseAsync_SameNameDifferentUnit_NotDisambiguated()
    {
        // "Vtrue (ktas)" and "Vtrue (ktgs)" share a name but differ in unit — both keep the bare
        // name; only true (name, unit) duplicates get a counter.
        var content = "|_time,sec|Vtrue,_ktas|Vtrue,_ktgs|\n|0.0|1.0|2.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);

        result.Data.Columns[1].ColumnName.Should().Be("Vtrue (ktas)");
        result.Data.Columns[2].ColumnName.Should().Be("Vtrue (ktgs)");
    }

    [Fact]
    public async Task ParseAsync_EmittedUnit_IsRecognizedByUnitHeaderParser()
    {
        // The whole point: the cleaned header must round-trip through the shared unit parser.
        var content = "|_real,_time|Vtrue,_ktas|\n|0.0|1.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);

        var parsed = DatPlotX.Services.Units.UnitHeaderParser.Parse(result.Data.Columns[1].ColumnName);
        parsed.DisplayName.Should().Be("Vtrue");
        parsed.Unit.Should().Be("ktas");
    }

    [Fact]
    public async Task ParseAsync_SemicolonSeparator_AlsoParses()
    {
        var content = "|_time;sec|alt;ft|\n|0.0|1000.0|\n|1.0|1050.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);
        result.Data.Columns.Count.Should().Be(2);
        result.Data.Columns[1].ColumnName.Should().Be("alt (ft)");
    }

    [Fact]
    public async Task ParseAsync_NoUnit_KeepsParameterNameOnly()
    {
        var content = "|_time,sec|paramX|\n|0.0|100.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);
        result.Data.Columns[1].ColumnName.Should().Contain("paramX").And.NotContain("-");
    }

    [Fact]
    public async Task ParseAsync_FilenameLookingLikeHeaderDoesNotFalseMatch()
    {
        // A column like "last_time_seen" must not be treated as the header-start line
        // now that we use StartsWith("_time") instead of Contains("_time").
        var content = "|_time,sec|last_time_seen,n|\n|0.0|5.0|\n";
        var path = WriteTempXPlane(content);
        var result = await Parser().ParseAsync(path);
        result.Data.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_LargeFileCallback_PropagatedToCsvParser()
    {
        var csv = new CsvDataParser(new DatPlotX.Models.ApplicationSettings());
        var xplane = new XPlaneDataParser(csv);
        bool called = false;
        xplane.OnLargeFileWarning = (_, _) =>
        {
            called = true;
            return Task.FromResult(true);
        };
        csv.OnLargeFileWarning.Should().NotBeNull();
        called.Should().BeFalse();
    }

    [Fact]
    public async Task ParseAsync_TempFileCleanedUpAfterParse()
    {
        var content = "|_time,sec|\n|0.0|\n";
        var path = WriteTempXPlane(content);

        int tempsBefore = Directory.GetFiles(Path.GetTempPath(), "datplot_xplane_*").Length;
        _ = await Parser().ParseAsync(path);
        int tempsAfter = Directory.GetFiles(Path.GetTempPath(), "datplot_xplane_*").Length;

        tempsAfter.Should().Be(tempsBefore);
    }

    [Fact]
    public async Task ParseAsync_NonExistentFile_Throws()
    {
        var act = async () => await Parser().ParseAsync("/tmp/_no_such_xplane_file.txt");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    // P1 (H5 guard): the X-Plane parser must reject oversized files via
    // ValidateSourceFileSizeAsync before reading any data. If a future re-ordering moves the
    // header pass above the size check, this test goes red and we avoid re-opening the
    // resource-exhaustion path.
    [Fact]
    public async Task ParseAsync_OversizedFile_RejectedBeforeReadingBody()
    {
        // Write a valid X-Plane file but configure MaxFileSizeBytes to a tiny limit so the
        // size guard trips before any header / body read.
        var content = "|_time,sec|alt,ft|\n|0.0|1000.0|\n|1.0|1050.0|\n";
        var path = WriteTempXPlane(content);

        var settings = new DatPlotX.Models.ApplicationSettings
        {
            MaxFileSizeBytes = 16,  // far smaller than the temp file we just wrote
            ShowLargeFileWarnings = false,
        };
        var csv = new CsvDataParser(settings);
        var xplane = new XPlaneDataParser(csv, settings);

        bool warningInvoked = false;
        xplane.OnLargeFileWarning = (_, _) => { warningInvoked = true; return Task.FromResult(true); };

        var act = async () => await xplane.ParseAsync(path);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed*");
        // The warning callback is only fired when the file exceeds the warning threshold,
        // not the hard cap — and certainly not after the body has been read.
        warningInvoked.Should().BeFalse();
    }

    // P1 (M6 guard): X-Plane cell VALUES containing `,` or `"` must round-trip via the
    // CsvWriter / CsvReader pair into the parsed table. The column-name sanitizer strips
    // quotes from headers (security control), but values pass through unmodified.
    [Fact]
    public async Task ParseAsync_HeaderOrValueContainsCommaAndQuote_RoundTripsViaCsvWriter()
    {
        // Use a string column (`name`) so values pass through verbatim. Embed `,` and `"`
        // in a value to exercise CsvWriter's escaping; before M6 the raw write path would
        // have produced a corrupt temp file.
        var content = "|_time,sec|name|\n|0.0|hello, \"world\"|\n|1.0|plain|\n";
        var path = WriteTempXPlane(content);

        var result = await Parser().ParseAsync(path);

        result.Data.Columns.Count.Should().Be(2);
        result.Data.Rows.Count.Should().Be(2);
        // The value with the embedded comma + quote must survive end-to-end.
        var roundTripped = result.Data.Rows[0][1].ToString();
        roundTripped.Should().Contain(",");
        roundTripped.Should().Contain("\"");
        result.Data.Rows[1][1].ToString().Should().Be("plain");
    }
}
