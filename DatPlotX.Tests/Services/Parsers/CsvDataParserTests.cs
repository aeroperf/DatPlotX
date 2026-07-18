using DatPlotX.Models;
using DatPlotX.Services.Parsers;
using FluentAssertions;
using System.Globalization;
using System.Text;

namespace DatPlotX.Tests.Services.Parsers;

public class CsvDataParserTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* cleanup only */ }
        }
    }

    private string WriteTempCsv(string contents, string suffix = ".csv")
    {
        var path = Path.Combine(Path.GetTempPath(), $"datplot_test_{Guid.NewGuid():N}{suffix}");
        File.WriteAllText(path, contents, Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task ParseAsync_BasicCsv_ReturnsAllRowsAndTypedColumns()
    {
        var path = WriteTempCsv("time,value\n0.0,1.5\n0.1,2.5\n0.2,3.5\n");
        var parser = new CsvDataParser();
        var result = await parser.ParseAsync(path, new CsvImportOptions());

        result.Data.Columns.Count.Should().Be(2);
        result.Data.Rows.Count.Should().Be(3);
        result.Data.Columns["time"]!.DataType.Should().Be(typeof(double));
        result.Data.Columns["value"]!.DataType.Should().Be(typeof(double));
        result.Data.Rows[2]["value"].Should().Be(3.5);
    }

    [Fact]
    public async Task ParseAsync_AllIntegerColumn_WidenedToDouble()
    {
        // We intentionally never infer int for plot data: an all-integer sample that turns
        // fractional later must not silently NaN out. All numerics widen to double (review C3).
        var path = WriteTempCsv("a,b\n1,2\n3,4\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Columns["a"]!.DataType.Should().Be(typeof(double));
        result.Data.Rows[0]["a"].Should().Be(1.0);
    }

    [Fact]
    public async Task ParseAsync_DetectionAndFill_UseSameNumberStyles_NoSilentDataLoss()
    {
        // Regression for review #6: column-type detection used NumberStyles.Any (accepts
        // parentheses-negatives, currency, etc.) while row-fill used Float|AllowThousands. A value
        // like "(1.5)" was detected as double but failed to fill and became DBNull → silent NaN.
        // Now both use the same styles, so such a column is classified as string and every value
        // is preserved verbatim (nothing silently dropped).
        var path = WriteTempCsv("v\n1.5\n(1.5)\n2.5\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns["v"]!.DataType.Should().Be(typeof(string));
        result.Data.Rows.Count.Should().Be(3);
        result.Data.Rows[1]["v"].Should().Be("(1.5)");
        // No cell silently nulled out.
        foreach (System.Data.DataRow row in result.Data.Rows)
            row["v"].Should().NotBe(DBNull.Value);
    }

    [Fact]
    public async Task ParseAsync_IntegerThenFractional_KeepsLaterFractionalValues()
    {
        // Regression for review C3: a column that is all-integer in the first (sampled) rows but
        // fractional later was typed int, so later "0.75" failed int.TryParse and became DBNull →
        // NaN in the plot. Build > 100 leading integer rows, then a fractional one.
        var sb = new StringBuilder("a\n");
        for (int i = 0; i < 150; i++) sb.Append("0\n");
        sb.Append("0.75\n");
        var path = WriteTempCsv(sb.ToString());

        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns["a"]!.DataType.Should().Be(typeof(double));
        result.Data.Rows[150]["a"].Should().Be(0.75);   // not DBNull / NaN
    }

    [Fact]
    public async Task ParseAsync_MixedTypes_FallbackToDouble()
    {
        var path = WriteTempCsv("a\n1\n2.5\n3\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Columns["a"]!.DataType.Should().Be(typeof(double));
    }

    [Fact]
    public async Task ParseAsync_StringData_DetectedAsString()
    {
        var path = WriteTempCsv("name\nalpha\nbeta\ngamma\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Columns["name"]!.DataType.Should().Be(typeof(string));
    }

    [Fact]
    public async Task ParseAsync_BlankCell_StoredAsDBNull()
    {
        var path = WriteTempCsv("a,b\n1,2\n3,\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Rows[1]["b"].Should().Be(DBNull.Value);
    }

    [Fact]
    public async Task ParseAsync_CommentLinesSkippedInDataBlock_WhenAllowCommentsTrue()
    {
        // Line selectors point past the leading comments; AllowComments still strips
        // any comment lines that appear within the data block itself.
        var path = WriteTempCsv("# Sensor: acc\n# Sample rate: 400 Hz\ntime,value\n0,1\n# inline\n1,2\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            AllowComments = true,
            HeaderLine = 3,
            DataStartLine = 4,
        });
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_TabDelimited_ViaOption()
    {
        var path = WriteTempCsv("a\tb\n1\t2\n3\t4\n", suffix: ".tsv");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions { Delimiter = "\t" });
        result.Data.Columns.Count.Should().Be(2);
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_RowLimitExceeded_Throws()
    {
        var sb = new StringBuilder("a\n");
        for (int i = 0; i < 50; i++) sb.Append(i).Append('\n');
        var path = WriteTempCsv(sb.ToString());

        var settings = new ApplicationSettings { MaxRowCount = 5, ShowLargeFileWarnings = false };
        var parser = new CsvDataParser(settings);
        var act = async () => await parser.ParseAsync(path, new CsvImportOptions());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Row count exceeds*");
    }

    [Fact]
    public async Task ParseAsync_ColumnLimitExceeded_Throws()
    {
        var header = string.Join(',', Enumerable.Range(0, 50).Select(i => $"c{i}"));
        var row = string.Join(',', Enumerable.Range(0, 50).Select(_ => "1"));
        var path = WriteTempCsv(header + "\n" + row + "\n");

        var settings = new ApplicationSettings { MaxColumnCount = 10, ShowLargeFileWarnings = false };
        var parser = new CsvDataParser(settings);
        var act = async () => await parser.ParseAsync(path, new CsvImportOptions());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Column count*");
    }

    [Fact]
    public async Task ParseAsync_LargeFileWithoutCallback_Throws()
    {
        var path = WriteTempCsv("a\n1\n");
        var settings = new ApplicationSettings
        {
            ShowLargeFileWarnings = true,
            LargeFileWarningThresholdBytes = 1
        };
        var parser = new CsvDataParser(settings);
        var act = async () => await parser.ParseAsync(path, new CsvImportOptions());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*user confirmation*");
    }

    [Fact]
    public async Task ParseAsync_LargeFileCallbackDenies_Cancels()
    {
        var path = WriteTempCsv("a\n1\n");
        var settings = new ApplicationSettings
        {
            ShowLargeFileWarnings = true,
            LargeFileWarningThresholdBytes = 1
        };
        var parser = new CsvDataParser(settings)
        {
            OnLargeFileWarning = (_, _) => Task.FromResult(false)
        };
        var act = async () => await parser.ParseAsync(path, new CsvImportOptions());
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ParseAsync_LargeFileCallbackAccepts_Parses()
    {
        var path = WriteTempCsv("a\n1\n2\n");
        var settings = new ApplicationSettings
        {
            ShowLargeFileWarnings = true,
            LargeFileWarningThresholdBytes = 1
        };
        var parser = new CsvDataParser(settings)
        {
            OnLargeFileWarning = (_, _) => Task.FromResult(true)
        };
        var result = await parser.ParseAsync(path, new CsvImportOptions());
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_MaxFileSizeExceeded_ThrowsHardLimit()
    {
        var sb = new StringBuilder("a\n");
        for (int i = 0; i < 100; i++) sb.AppendLine("1");
        var path = WriteTempCsv(sb.ToString());

        var settings = new ApplicationSettings
        {
            MaxFileSizeBytes = 10,
            LargeFileWarningThresholdBytes = 1_000_000,
            ShowLargeFileWarnings = false
        };
        var parser = new CsvDataParser(settings);
        var act = async () => await parser.ParseAsync(path, new CsvImportOptions());
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*exceeds maximum allowed size*");
    }

    [Fact]
    public async Task ParseAsync_CultureInvariant_DotAsDecimalSeparator()
    {
        var path = WriteTempCsv("a\n1.5\n2.5\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            Culture = CultureInfo.InvariantCulture
        });
        result.Data.Rows[0]["a"].Should().Be(1.5);
    }

    [Fact]
    public async Task ParseAsync_GermanCulture_CommaAsDecimalSeparator()
    {
        var path = WriteTempCsv("a;b\n1,5;2,5\n3,5;4,5\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            Delimiter = ";",
            Culture = CultureInfo.GetCultureInfo("de-DE")
        });
        result.Data.Rows[0]["a"].Should().Be(1.5);
        result.Data.Rows[1]["b"].Should().Be(4.5);
    }

    [Fact]
    public async Task ParseAsync_NoHeader_GeneratesColumnNames()
    {
        // Back-compat: HasHeader=false should still translate to "no header, data on line 1".
        var path = WriteTempCsv("1,2,3\n4,5,6\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions { HasHeader = false });
        result.Data.Columns.Count.Should().Be(3);
        result.Data.Columns[0].ColumnName.Should().Be("Column1");
        result.Data.Columns[2].ColumnName.Should().Be("Column3");
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_HeaderWithIllegalChars_Sanitized()
    {
        var path = WriteTempCsv("col<>%,b\n1,2\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Columns[0].ColumnName.Should().NotContain("<").And.NotContain(">").And.NotContain("%");
    }

    [Fact]
    public async Task ParseAsync_FileDoesNotExist_Throws()
    {
        var parser = new CsvDataParser();
        var act = async () => await parser.ParseAsync("/tmp/_not_a_real_file_xyz.csv", new CsvImportOptions());
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ParseAsync_EmptyFile_ReturnsEmptyTable()
    {
        var path = WriteTempCsv("");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Columns.Count.Should().Be(0);
        result.Data.Rows.Count.Should().Be(0);
    }

    [Fact]
    public async Task ParseTextAsync_AutoDetectsCommaDelimiter()
    {
        var path = WriteTempCsv("a,b,c\n1,2,3\n4,5,6\n", suffix: ".txt");
        var result = await new CsvDataParser().ParseTextAsync(path);
        result.Data.Columns.Count.Should().Be(3);
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseTextAsync_AutoDetectsTabDelimiter()
    {
        var path = WriteTempCsv("a\tb\n1\t2\n3\t4\n", suffix: ".txt");
        var result = await new CsvDataParser().ParseTextAsync(path);
        result.Data.Columns.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseTabAsync_WorksWithTabDelimiter()
    {
        var path = WriteTempCsv("a\tb\n1\t2\n3\t4\n", suffix: ".tab");
        var result = await new CsvDataParser().ParseTabAsync(path);
        result.Data.Columns.Count.Should().Be(2);
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_SettingsInjectedViaCtor_Applied()
    {
        var path = WriteTempCsv("a\n1\n");
        var settings = new ApplicationSettings { MaxRowCount = 100, ShowLargeFileWarnings = false };
        var parser = new CsvDataParser(settings);
        var result = await parser.ParseAsync(path, new CsvImportOptions());
        result.Data.Rows.Count.Should().Be(1);
    }

    [Fact]
    public async Task ParseAsync_CsvImportOptionsExplicitLimits_Respected()
    {
        var sb = new StringBuilder("a\n");
        for (int i = 0; i < 50; i++) sb.Append(i).Append('\n');
        var path = WriteTempCsv(sb.ToString());

        var parser = new CsvDataParser(new ApplicationSettings { ShowLargeFileWarnings = false });
        var act = async () => await parser.ParseAsync(path, new CsvImportOptions { MaxRowCount = 5 });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Row count exceeds*");
    }

    [Fact]
    public async Task ParseAsync_DuplicateHeaders_GetsUniqueColumnNames()
    {
        var path = WriteTempCsv("a,a,a\n1,2,3\n4,5,6\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Columns.Count.Should().Be(3);
        result.Data.Columns[0].ColumnName.Should().Be("a");
        result.Data.Columns[1].ColumnName.Should().Be("a_2");
        result.Data.Columns[2].ColumnName.Should().Be("a_3");
    }

    [Fact]
    public async Task ParseAsync_StreamsLargeFile_PreservesRowCountAndChecksum()
    {
        var sb = new StringBuilder("idx,val\n");
        int n = 5000;
        long checksum = 0;
        for (int i = 0; i < n; i++)
        {
            sb.Append(i).Append(',').Append(i * 2).Append('\n');
            checksum += i * 2;
        }
        var path = WriteTempCsv(sb.ToString());

        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());
        result.Data.Rows.Count.Should().Be(n);

        long sum = 0;
        foreach (System.Data.DataRow row in result.Data.Rows)
            sum += Convert.ToInt64(row["val"], CultureInfo.InvariantCulture);
        sum.Should().Be(checksum);
    }

    // ── Line-selector tests (HeaderLine / UnitLine / DataStartLine) ───────────

    [Fact]
    public async Task ParseAsync_HeaderOnLine3WithMetadataAbove_ReadsCorrectColumns()
    {
        // Lines 1-2 are arbitrary metadata; header on 3, data starts on 4.
        var path = WriteTempCsv("Vehicle: Tesla\nRecorded: 2024-01-01\ntime,speed\n0,30\n1,45\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 3,
            DataStartLine = 4,
        });

        result.Data.Columns.Count.Should().Be(2);
        result.Data.Columns[0].ColumnName.Should().Be("time");
        result.Data.Columns[1].ColumnName.Should().Be("speed");
        result.Data.Rows.Count.Should().Be(2);
        result.Data.Rows[1]["speed"].Should().Be(45);
    }

    [Fact]
    public async Task ParseAsync_UnitLineSet_ConcatenatedIntoColumnName()
    {
        // Header on 1, units on 2, data on 3.
        var path = WriteTempCsv("Altitude,Speed\nfeet,knots\n100,30\n200,45\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 1,
            UnitLine = 2,
            DataStartLine = 3,
        });

        result.Data.Columns[0].ColumnName.Should().Be("Altitude (feet)");
        result.Data.Columns[1].ColumnName.Should().Be("Speed (knots)");
        result.Data.Rows.Count.Should().Be(2);
        result.Data.Rows[0]["Altitude (feet)"].Should().Be(100);
    }

    [Fact]
    public async Task ParseAsync_UnitLineWithSomeBlankUnits_OnlyConcatsNonEmpty()
    {
        var path = WriteTempCsv("Altitude,Notes\nfeet,\n100,ok\n200,warn\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 1,
            UnitLine = 2,
            DataStartLine = 3,
        });

        result.Data.Columns[0].ColumnName.Should().Be("Altitude (feet)");
        result.Data.Columns[1].ColumnName.Should().Be("Notes");
    }

    [Fact]
    public async Task ParseAsync_HeaderUnitDataWithGapBetween_RespectsLineNumbers()
    {
        // Header 1, unit 2, lines 3-4 are junk, data starts on 5.
        var path = WriteTempCsv("a,b\nm,m/s\n--- begin ---\nignored line\n10,20\n11,21\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 1,
            UnitLine = 2,
            DataStartLine = 5,
        });

        result.Data.Columns[0].ColumnName.Should().Be("a (m)");
        result.Data.Columns[1].ColumnName.Should().Be("b (m/s)"); // unit text preserved verbatim
        result.Data.Rows.Count.Should().Be(2);
        result.Data.Rows[0]["a (m)"].Should().Be(10);
    }

    [Fact]
    public async Task ParseAsync_NoHeaderViaLineSelectors_GeneratesColumnNames()
    {
        var path = WriteTempCsv("1,2,3\n4,5,6\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 0,
            DataStartLine = 1,
        });

        result.Data.Columns.Count.Should().Be(3);
        result.Data.Columns[0].ColumnName.Should().Be("Column1");
        result.Data.Rows.Count.Should().Be(2);
    }

    [Fact]
    public async Task ParseAsync_DataStartBeforeHeader_Throws()
    {
        var path = WriteTempCsv("a,b\n1,2\n");
        var act = async () => await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 3,
            DataStartLine = 2,
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*greater than*");
    }

    [Fact]
    public async Task ParseAsync_HeaderEqualsUnit_Throws()
    {
        var path = WriteTempCsv("a,b\n1,2\n");
        var act = async () => await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 1,
            UnitLine = 1,
            DataStartLine = 2,
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*differ*");
    }

    [Fact]
    public async Task ParseAsync_DataStartLineZero_Throws()
    {
        var path = WriteTempCsv("a,b\n1,2\n");
        var act = async () => await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            DataStartLine = 0,
        });
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*DataStartLine*");
    }

    [Fact]
    public async Task ParseAsync_DefaultOptions_StillWorksLikeBefore()
    {
        // Regression: defaults (HeaderLine=1, UnitLine=0, DataStartLine=2) behave
        // identically to the legacy HasHeader=true path.
        var path = WriteTempCsv("time,value\n0,10\n1,20\n2,30\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns[0].ColumnName.Should().Be("time");
        result.Data.Rows.Count.Should().Be(3);
    }

    // P0: the row-count guard moved into FillRow when the parser switched to streaming.
    // The previous review report flagged off-by-one risk as P0. This test pins the boundary
    // at MaxRowCount + 1 — exactly that row must trigger the exception, and the message
    // must include the configured max so the user can act on it.
    [Fact]
    public async Task ParseAsync_RowCountGuard_ThrowsExactlyOnFirstOverLimitRow()
    {
        const int max = 5;
        var sb = new StringBuilder();
        sb.AppendLine("time,value");
        for (int i = 0; i < max + 1; i++)
            sb.AppendLine(CultureInfo.InvariantCulture, $"{i},{i * 10}");
        var path = WriteTempCsv(sb.ToString());

        var parser = new CsvDataParser();
        var options = new CsvImportOptions { MaxRowCount = max };

        var act = async () => await parser.ParseAsync(path, options);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{max}*", "the exception text must surface the configured max so users can adjust their settings");
    }

    // P1: hand-rolled SplitDelimitedLine claims to honor quoted CSV fields. A header line
    // with an embedded `,` inside quotes must collapse to one column, not split.
    [Fact]
    public async Task ParseAsync_HeaderLineWithEmbeddedComma_StaysOneColumn()
    {
        // Header: "col, with comma",b — two columns; the first quoted field contains a comma.
        // Data rows are simple numeric pairs to avoid the comma trap.
        var path = WriteTempCsv("\"col, with comma\",b\n1,2\n3,4\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns.Count.Should().Be(2);
        result.Data.Columns[0].ColumnName.Should().Be("col with comma");
        result.Data.Columns[1].ColumnName.Should().Be("b");
    }

    // P1: the unit row may contain `/` and parens — SanitizeUnit only strips control chars,
    // so the unit text must round-trip into the final column name verbatim.
    [Fact]
    public async Task ParseAsync_UnitLine_WithSlashAndParenChars_PreservedInColumnName()
    {
        var path = WriteTempCsv("a,b\ns,m/s (avg)\n1,2\n3,4\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions
        {
            HeaderLine = 1,
            UnitLine = 2,
            DataStartLine = 3,
        });

        result.Data.Columns.Count.Should().Be(2);
        // Header `b` plus unit `m/s (avg)` produces `b (m/s (avg))`.
        result.Data.Columns[1].ColumnName.Should().Be("b (m/s (avg))");
    }

    private string WriteTempBytes(string contents, Encoding encoding, string suffix = ".csv")
    {
        var path = Path.Combine(Path.GetTempPath(), $"datplot_test_{Guid.NewGuid():N}{suffix}");
        File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(contents)).ToArray());
        _tempFiles.Add(path);
        return path;
    }

    // A UTF-8 BOM on the header line must not corrupt the first column name (classic
    // "﻿time" instead of "time"). StreamReader strips the BOM by default; pin it so a
    // future encoding change can't silently reintroduce the corruption.
    [Fact]
    public async Task ParseAsync_Utf8Bom_FirstColumnNameNotCorrupted()
    {
        var path = WriteTempBytes("time,value\n0.0,1.5\n0.1,2.5\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns.Count.Should().Be(2);
        result.Data.Columns[0].ColumnName.Should().Be("time");
        result.Data.Columns[0].ColumnName.Should().NotStartWith("﻿");
        result.Data.Columns.Contains("time").Should().BeTrue();
    }

    // Windows CRLF line endings must parse identically to LF — no phantom rows and no trailing
    // '\r' contamination in the last column's values.
    [Fact]
    public async Task ParseAsync_CrlfLineEndings_NoTrailingCarriageReturnInValues()
    {
        var path = WriteTempCsv("a,b\r\n1,2\r\n3,4\r\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns.Count.Should().Be(2);
        result.Data.Rows.Count.Should().Be(2);
        // Last column would carry a trailing '\r' if CRLF weren't handled → non-numeric → wrong type.
        result.Data.Columns["b"]!.DataType.Should().Be(typeof(double));
        result.Data.Rows[1]["b"].Should().Be(4.0);
    }

    // A literal Infinity / -Infinity / NaN cell must parse to the matching double value, closing
    // the parse↔serialize symmetry (ProjectSerializer already round-trips these named literals).
    [Theory]
    [InlineData("Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    [InlineData("NaN", double.NaN)]
    public async Task ParseAsync_LiteralNonFiniteCell_ParsesToDoubleValue(string cell, double expected)
    {
        // Mix with a finite value so the column still types as double.
        var path = WriteTempCsv($"gain\n1.0\n{cell}\n2.0\n");
        var result = await new CsvDataParser().ParseAsync(path, new CsvImportOptions());

        result.Data.Columns["gain"]!.DataType.Should().Be(typeof(double));
        result.Data.Rows[1]["gain"].Should().Be(expected);
    }
}
