using DatPlotX.Models;
using DatPlotX.Models.Analysis;
using DatPlotX.Services;
using FluentAssertions;
using System.Data;
using System.Globalization;
using System.Security;
using System.Text.Json;

namespace DatPlotX.Tests.Services;

public class ProjectSerializerTests
{
    private readonly ProjectSerializer _serializer = new();

    [Fact]
    public void SerializeToJson_NullProject_Throws()
    {
        var act = () => _serializer.SerializeToJson(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void SerializeToJson_EmptyProject_ProducesValidJson()
    {
        var project = new ProjectSettingsModel();
        var json = _serializer.SerializeToJson(project);
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("{");
    }

    [Fact]
    public void SerializeToJson_UpdatesLastModified()
    {
        var project = new ProjectSettingsModel { LastModified = DateTime.MinValue };
        var before = DateTime.Now;
        _serializer.SerializeToJson(project);
        project.LastModified.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void SerializeToJson_EmitsCurrentSchemaVersion()
    {
        var project = new ProjectSettingsModel();
        var json = _serializer.SerializeToJson(project);
        json.Should().Contain("\"schemaVersion\": 1");
    }

    [Fact]
    public void DeserializeFromJson_RoundTripsSchemaVersion()
    {
        var json = _serializer.SerializeToJson(new ProjectSettingsModel());
        var restored = _serializer.DeserializeFromJson(json);
        restored.SchemaVersion.Should().Be(ProjectSettingsModel.CurrentSchemaVersion);
    }

    [Fact]
    public void DeserializeFromJson_LegacyFileWithoutSchemaVersion_NormalizesToV1()
    {
        // A pre-versioning file has no schemaVersion field; it must load as v1, not 0.
        const string legacy = "{\"projectName\":\"Legacy\",\"plotTitle\":\"DatPlot\"}";
        var restored = _serializer.DeserializeFromJson(legacy);
        restored.SchemaVersion.Should().Be(1);
        restored.ProjectName.Should().Be("Legacy");
    }

    [Fact]
    public void DeserializeFromJson_EmptyString_Throws()
    {
        var act = () => _serializer.DeserializeFromJson("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeserializeFromJson_NullString_Throws()
    {
        var act = () => _serializer.DeserializeFromJson(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DeserializeFromJson_InvalidJson_Throws()
    {
        var act = () => _serializer.DeserializeFromJson("not json at all {{{{");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void RoundTrip_ProjectName_Preserved()
    {
        var project = new ProjectSettingsModel { ProjectName = "My Test Project" };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);
        restored.ProjectName.Should().Be("My Test Project");
    }

    [Fact]
    public void RoundTrip_DataSourcePath_Preserved()
    {
        var project = new ProjectSettingsModel { DataSourcePath = "/data/file.csv" };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);
        restored.DataSourcePath.Should().Be("/data/file.csv");
    }

    [Fact]
    public void RoundTrip_WithPanes_PreservesPaneCount()
    {
        var project = new ProjectSettingsModel
        {
            Panes = [new PlotPaneModel { Index = 0 }, new PlotPaneModel { Index = 1 }]
        };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);
        restored.Panes.Should().HaveCount(2);
    }

    [Fact]
    public void RoundTrip_WithCurves_PreservesCurveProperties()
    {
        var project = new ProjectSettingsModel
        {
            Curves = [new PlotCurveModel { Name = "gFx", Color = "#FF0000", LineWidth = 3.0 }]
        };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);

        restored.Curves.Should().HaveCount(1);
        restored.Curves[0].Name.Should().Be("gFx");
        restored.Curves[0].Color.Should().Be("#FF0000");
        restored.Curves[0].LineWidth.Should().Be(3.0);
    }

    [Fact]
    public void RoundTrip_PlotMode_Compact_Preserved()
    {
        var project = new ProjectSettingsModel { PlotMode = PlotMode.Compact };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);
        restored.PlotMode.Should().Be(PlotMode.Compact);
    }

    [Fact]
    public void RoundTrip_PlotMode_Panes_Preserved()
    {
        var project = new ProjectSettingsModel { PlotMode = PlotMode.Panes };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);
        restored.PlotMode.Should().Be(PlotMode.Panes);
    }

    [Fact]
    public void RoundTrip_PlotMode_Null_PreservedAsNull_LegacyFile()
    {
        var project = new ProjectSettingsModel { PlotMode = null };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);
        restored.PlotMode.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_AnalysisSegments_PreservesSegmentsAndActiveId()
    {
        var manual = new AnalysisSegment(
            Guid.NewGuid(), "Climb", 10.5, 42.25, AnalysisSegmentSource.Manual);
        var pair = new AnalysisSegment(
            Guid.NewGuid(), "Cruise", 0, 0, AnalysisSegmentSource.EventLinePair,
            StartEventId: Guid.NewGuid(), EndEventId: Guid.NewGuid());

        var project = new ProjectSettingsModel
        {
            AnalysisSegments = { manual, pair },
            ActiveSegmentId = manual.Id,
        };

        var restored = _serializer.DeserializeFromJson(_serializer.SerializeToJson(project));

        restored.AnalysisSegments.Should().HaveCount(2);
        var rManual = restored.AnalysisSegments[0];
        rManual.Id.Should().Be(manual.Id);
        rManual.Name.Should().Be("Climb");
        rManual.XMin.Should().Be(10.5);
        rManual.XMax.Should().Be(42.25);
        rManual.Source.Should().Be(AnalysisSegmentSource.Manual);

        var rPair = restored.AnalysisSegments[1];
        rPair.Source.Should().Be(AnalysisSegmentSource.EventLinePair);
        rPair.StartEventId.Should().Be(pair.StartEventId);
        rPair.EndEventId.Should().Be(pair.EndEventId);

        restored.ActiveSegmentId.Should().Be(manual.Id);
    }

    [Fact]
    public void RoundTrip_EnabledMetricIds_PreservesOrder()
    {
        var project = new ProjectSettingsModel
        {
            EnabledMetricIds = { "slope", "max", "integral" },
        };

        var restored = _serializer.DeserializeFromJson(_serializer.SerializeToJson(project));

        restored.EnabledMetricIds.Should().Equal("slope", "max", "integral");
    }

    [Fact]
    public void DeserializeFromJson_WithoutEnabledMetricIds_DefaultsToEmptyList()
    {
        // Older project files have no EnabledMetricIds field; it must deserialize to an empty list
        // (the load path then keeps the analysis service's default columns) — never null.
        var json = _serializer.SerializeToJson(new ProjectSettingsModel());
        var stripped = json.Replace("\"EnabledMetricIds\"", "\"_removedField\"");

        var restored = _serializer.DeserializeFromJson(stripped);

        restored.EnabledMetricIds.Should().NotBeNull();
        restored.EnabledMetricIds.Should().BeEmpty();
    }

    [Fact]
    public void DeserializeFromJson_WithoutPlotModeField_TreatsAsLegacyNull()
    {
        // Hand-craft a minimal JSON missing the PlotMode field — simulates a pre-0.6.0 project file.
        const string legacyJson = """
            {
                "Version": "2.0.0",
                "ProjectName": "Legacy"
            }
            """;
        var restored = _serializer.DeserializeFromJson(legacyJson);
        restored.PlotMode.Should().BeNull();
        restored.CompactCurves.Should().BeEmpty();
    }

    [Fact]
    public void RoundTrip_CompactCurves_PreservesOrderAndProperties()
    {
        var project = new ProjectSettingsModel
        {
            PlotMode = PlotMode.Compact,
            CompactCurves =
            {
                new CompactCurveModel
                {
                    DisplayName = "Altitude", SourceColumn = "alt", Unit = "ft",
                    AxisSide = AxisSide.Left, Color = "#0000FF", IsBoolean = false,
                    YMin = 0, YMax = 5000, AllowOverflow = false, IsVisible = true,
                    LineWidth = 2.0,
                },
                new CompactCurveModel
                {
                    DisplayName = "Gear",     SourceColumn = "gear",
                    AxisSide = AxisSide.Right, Color = "#FF0000", IsBoolean = true,
                    AllowOverflow = true, IsVisible = false,
                },
            }
        };
        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);

        restored.CompactCurves.Should().HaveCount(2);
        restored.CompactCurves[0].SourceColumn.Should().Be("alt");
        restored.CompactCurves[0].Unit.Should().Be("ft");
        restored.CompactCurves[0].YMin.Should().Be(0);
        restored.CompactCurves[0].YMax.Should().Be(5000);
        restored.CompactCurves[0].AllowOverflow.Should().BeFalse();
        restored.CompactCurves[1].SourceColumn.Should().Be("gear");
        restored.CompactCurves[1].IsBoolean.Should().BeTrue();
        restored.CompactCurves[1].AxisSide.Should().Be(AxisSide.Right);
        restored.CompactCurves[1].IsVisible.Should().BeFalse();
    }
}

public class DataTableJsonConverterTests
{
    private readonly ProjectSerializer _serializer = new();

    private ProjectSettingsModel ProjectWithTable(DataTable table)
    {
        var project = new ProjectSettingsModel();
        project.PlotData = new PlotDataModel { Data = table };
        return project;
    }

    [Fact]
    public void RoundTrip_DataTable_PreservesColumnsAndRows()
    {
        var table = new DataTable();
        table.Columns.Add("time", typeof(double));
        table.Columns.Add("value", typeof(double));
        table.Rows.Add(0.0, 1.5);
        table.Rows.Add(0.01, 2.5);

        var json = _serializer.SerializeToJson(ProjectWithTable(table));
        var restored = _serializer.DeserializeFromJson(json);

        var restoredTable = restored.PlotData!.Data;
        restoredTable.Columns.Count.Should().Be(2);
        restoredTable.Rows.Count.Should().Be(2);
        Convert.ToDouble(restoredTable.Rows[0]["time"], CultureInfo.InvariantCulture).Should().Be(0.0);
        Convert.ToDouble(restoredTable.Rows[1]["value"], CultureInfo.InvariantCulture).Should().Be(2.5);
    }

    [Fact]
    public void RoundTrip_DataTable_NullValues_HandledAsDBNull()
    {
        var table = new DataTable();
        table.Columns.Add("col", typeof(string));
        table.Rows.Add((object?)null);

        var json = _serializer.SerializeToJson(ProjectWithTable(table));
        var restored = _serializer.DeserializeFromJson(json);
        restored.PlotData!.Data.Rows[0]["col"].Should().Be(DBNull.Value);
    }

    [Theory]
    [InlineData(typeof(short), (short)42)]
    [InlineData(typeof(int), 42)]
    [InlineData(typeof(long), 42L)]
    [InlineData(typeof(uint), (uint)42)]
    [InlineData(typeof(ulong), (ulong)42)]
    [InlineData(typeof(byte), (byte)42)]
    [InlineData(typeof(float), 3.14f)]
    [InlineData(typeof(double), 3.14)]
    [InlineData(typeof(bool), true)]
    public void RoundTrip_DataTable_PreservesTypedValues(Type columnType, object value)
    {
        var table = new DataTable();
        table.Columns.Add("col", columnType);
        table.Rows.Add(value);

        var json = _serializer.SerializeToJson(ProjectWithTable(table));
        var restored = _serializer.DeserializeFromJson(json);

        var restoredValue = restored.PlotData!.Data.Rows[0]["col"];
        restoredValue.Should().BeOfType(columnType);
        restoredValue.Should().Be(value);
    }

    [Fact]
    public void RoundTrip_DataTable_PreservesDecimal()
    {
        var table = new DataTable();
        table.Columns.Add("col", typeof(decimal));
        table.Rows.Add(3.14m);

        var json = _serializer.SerializeToJson(ProjectWithTable(table));
        var restored = _serializer.DeserializeFromJson(json);

        var restoredValue = restored.PlotData!.Data.Rows[0]["col"];
        restoredValue.Should().BeOfType<decimal>();
        restoredValue.Should().Be(3.14m);
    }

    [Fact]
    public void RoundTrip_DataTable_PreservesDateTime()
    {
        var table = new DataTable();
        table.Columns.Add("col", typeof(DateTime));
        var dt = new DateTime(2024, 1, 15, 10, 30, 45, DateTimeKind.Utc);
        table.Rows.Add(dt);

        var json = _serializer.SerializeToJson(ProjectWithTable(table));
        var restored = _serializer.DeserializeFromJson(json);

        var restoredValue = restored.PlotData!.Data.Rows[0]["col"];
        restoredValue.Should().BeOfType<DateTime>();
        ((DateTime)restoredValue).Should().BeCloseTo(dt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void RoundTrip_DataTable_PreservesChar()
    {
        var table = new DataTable();
        table.Columns.Add("col", typeof(char));
        table.Rows.Add('X');

        var json = _serializer.SerializeToJson(ProjectWithTable(table));
        var restored = _serializer.DeserializeFromJson(json);

        var restoredValue = restored.PlotData!.Data.Rows[0]["col"];
        restoredValue.Should().BeOfType<char>();
        restoredValue.Should().Be('X');
    }

    [Fact]
    public void Deserialize_DisallowedType_ThrowsSecurityException()
    {
        var json = """
        {
          "plotData": {
            "sourceName": "",
            "data": {
              "columns": [{"name":"col","type":"System.Threading.Thread"}],
              "rows": []
            }
          }
        }
        """;

        Exception? caughtEx = null;
        try { _serializer.DeserializeFromJson(json); }
        catch (Exception ex) { caughtEx = ex; }

        caughtEx.Should().NotBeNull();
        bool hasSecurityEx = caughtEx is SecurityException
            || caughtEx?.InnerException is SecurityException
            || (caughtEx?.Message?.Contains("Unsafe") ?? false)
            || (caughtEx?.InnerException?.Message?.Contains("Unsafe") ?? false);
        hasSecurityEx.Should().BeTrue();
    }

    // SECURITY (review B2): a hand-crafted .DPX bypasses the CSV parser, so the DataTable
    // converter must enforce the same MaxColumnCount cap — otherwise a small file declaring
    // millions of columns drives excessive allocation on load. Match the CSV parser's rejection.
    [Fact]
    public void Deserialize_ColumnCountAboveCap_Throws()
    {
        var cols = string.Join(",", Enumerable.Range(0, ApplicationSettings.DefaultMaxColumnCount + 1)
            .Select(i => $"{{\"name\":\"c{i}\",\"type\":\"System.Double\"}}"));
        var json = $$"""
        {
          "plotData": {
            "sourceName": "",
            "data": { "columns": [{{cols}}], "rows": [] }
          }
        }
        """;

        var act = () => _serializer.DeserializeFromJson(json);
        act.Should().Throw<InvalidDataException>().WithMessage("*column count*");
    }

    // A column count exactly at the cap must still load — the guard is off-by-one-safe.
    [Fact]
    public void Deserialize_ColumnCountAtCap_Succeeds()
    {
        var cols = string.Join(",", Enumerable.Range(0, ApplicationSettings.DefaultMaxColumnCount)
            .Select(i => $"{{\"name\":\"c{i}\",\"type\":\"System.Double\"}}"));
        var json = $$"""
        {
          "plotData": {
            "sourceName": "",
            "data": { "columns": [{{cols}}], "rows": [] }
          }
        }
        """;

        var restored = _serializer.DeserializeFromJson(json);
        restored.PlotData!.Data.Columns.Count.Should().Be(ApplicationSettings.DefaultMaxColumnCount);
    }

    // Regression: a CSV can legitimately contain ±Infinity / NaN (e.g. a 'Gain' column with
    // '-∞'). Utf8JsonWriter.WriteNumberValue throws on those regardless of NumberHandling, so
    // the DataTable converter must emit/parse them as named-literal strings instead of failing
    // the whole project save.
    [Fact]
    public void SerializeToJson_DataTableWithNonFiniteDoubles_DoesNotThrow()
    {
        var project = ProjectWithGainColumn(double.NegativeInfinity, double.PositiveInfinity, double.NaN);
        var act = () => _serializer.SerializeToJson(project);
        act.Should().NotThrow();
    }

    [Fact]
    public void RoundTrip_DataTableWithNonFiniteDoubles_PreservesValues()
    {
        var project = ProjectWithGainColumn(double.NegativeInfinity, double.PositiveInfinity, double.NaN);

        var json = _serializer.SerializeToJson(project);
        var restored = _serializer.DeserializeFromJson(json);

        var rows = restored.PlotData!.Data.Rows;
        ((double)rows[0]["gain"]).Should().Be(double.NegativeInfinity);
        ((double)rows[1]["gain"]).Should().Be(double.PositiveInfinity);
        ((double)rows[2]["gain"]).Should().Be(double.NaN);
        // Finite values still survive normally.
        ((double)rows[3]["gain"]).Should().Be(1.5);
    }

    [Fact]
    public void RoundTrip_AnalysisSegments_PreservesAllFields()
    {
        var startId = Guid.NewGuid();
        var endId = Guid.NewGuid();
        var manual = new AnalysisSegment(Guid.NewGuid(), "Climb", 10.5, 42.0, AnalysisSegmentSource.Manual);
        var pair = new AnalysisSegment(
            Guid.NewGuid(), "Cruise", 0, 0, AnalysisSegmentSource.EventLinePair,
            StartEventId: startId, EndEventId: endId);

        var project = new ProjectSettingsModel
        {
            AnalysisSegments = [manual, pair],
            ActiveSegmentId = pair.Id,
        };

        var restored = _serializer.DeserializeFromJson(_serializer.SerializeToJson(project));

        restored.AnalysisSegments.Should().HaveCount(2);
        restored.ActiveSegmentId.Should().Be(pair.Id);

        var rm = restored.AnalysisSegments.Single(s => s.Id == manual.Id);
        rm.Name.Should().Be("Climb");
        rm.XMin.Should().Be(10.5);
        rm.XMax.Should().Be(42.0);
        rm.Source.Should().Be(AnalysisSegmentSource.Manual);

        var rp = restored.AnalysisSegments.Single(s => s.Id == pair.Id);
        rp.Source.Should().Be(AnalysisSegmentSource.EventLinePair);
        rp.StartEventId.Should().Be(startId);
        rp.EndEventId.Should().Be(endId);
    }

    [Fact]
    public void RoundTrip_NoAnalysisSegments_DefaultsToEmpty()
    {
        var restored = _serializer.DeserializeFromJson(_serializer.SerializeToJson(new ProjectSettingsModel()));
        restored.AnalysisSegments.Should().BeEmpty();
        restored.ActiveSegmentId.Should().BeNull();
    }

    private static ProjectSettingsModel ProjectWithGainColumn(params double[] firstValues)
    {
        var table = new DataTable();
        table.Columns.Add("gain", typeof(double));
        foreach (var v in firstValues) table.Rows.Add(v);
        table.Rows.Add(1.5);
        return new ProjectSettingsModel
        {
            PlotData = new PlotDataModel { Data = table, SourceName = "test" },
        };
    }
}
