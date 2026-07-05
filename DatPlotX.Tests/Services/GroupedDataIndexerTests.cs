using DatPlotX.Models;
using DatPlotX.Services;
using FluentAssertions;
using System.Data;

namespace DatPlotX.Tests.Services;

public class GroupedDataIndexerTests
{
    private static PlotDataModel BuildGriddedData()
    {
        // 2 weights × 3 disas × 4 altitudes × 5 time points = 120 rows
        var t = new DataTable();
        t.Columns.Add("Weight", typeof(double));
        t.Columns.Add("DISA", typeof(double));
        t.Columns.Add("StartAlt", typeof(double));
        t.Columns.Add("Time", typeof(double));
        t.Columns.Add("LevelOff", typeof(double));

        double[] weights = { 190000, 200000 };
        double[] disas = { -10, 0, 10 };
        double[] alts = { 17000, 25000, 33000, 41000 };

        foreach (var w in weights)
            foreach (var d in disas)
                foreach (var a in alts)
                    for (int tIdx = 0; tIdx < 5; tIdx++)
                    {
                        var time = tIdx;
                        var level = a - tIdx * 1000;
                        t.Rows.Add(w, d, a, (double)time, level);
                    }
        return new PlotDataModel { Data = t };
    }

    private static GroupedDataIndexer BuildIndexer(int maxLines = 48, int maxDistinct = 5000)
    {
        var s = new ApplicationSettings { GroupedPlotMaxLines = maxLines, GroupedPlotMaxDistinctValues = maxDistinct };
        return new GroupedDataIndexer(s);
    }

    [Fact]
    public void GetDistinctValues_ReturnsSortedUniqueValues()
    {
        var data = BuildGriddedData();
        var sut = BuildIndexer();

        var distinct = sut.GetDistinctValues(data, "StartAlt", out var capped);

        capped.Should().BeFalse();
        distinct.Should().Equal(17000, 25000, 33000, 41000);
    }

    [Fact]
    public void GetDistinctValues_DedupsWithEpsilon()
    {
        var t = new DataTable();
        t.Columns.Add("x", typeof(double));
        t.Rows.Add(1.0);
        t.Rows.Add(1.0 + 1e-12);   // within epsilon
        t.Rows.Add(2.0);
        t.Rows.Add(double.NaN);
        var data = new PlotDataModel { Data = t };

        var distinct = BuildIndexer().GetDistinctValues(data, "x", out var capped);

        capped.Should().BeFalse();
        distinct.Should().HaveCount(2).And.Equal(1.0, 2.0);
    }

    [Fact]
    public void GetDistinctValues_SetsCappedFlagWhenExceedingLimit()
    {
        var t = new DataTable();
        t.Columns.Add("x", typeof(double));
        for (int i = 0; i < 50; i++) t.Rows.Add((double)i);
        var data = new PlotDataModel { Data = t };

        var distinct = BuildIndexer(maxLines: 48, maxDistinct: 10).GetDistinctValues(data, "x", out var capped);

        capped.Should().BeTrue();
    }

    [Fact]
    public void Project_AllLockedInputs_ReturnsSingleSeries()
    {
        var data = BuildGriddedData();
        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight",   SelectedValue = 190000 },
                new() { ColumnName = "DISA",     SelectedValue = 0 },
                new() { ColumnName = "StartAlt", SelectedValue = 33000 },
            },
        };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Truncated.Should().BeFalse();
        projection.Series.Should().HaveCount(1);
        projection.Series[0].X.Should().Equal(0, 1, 2, 3, 4);
        projection.Series[0].Y.Should().Equal(33000, 32000, 31000, 30000, 29000);
        projection.Series[0].Label.Should().BeEmpty();
    }

    [Fact]
    public void Project_OneAllInput_ExpandsToOneLinePerDistinctValue()
    {
        var data = BuildGriddedData();
        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight",   SelectedValue = 190000 },
                new() { ColumnName = "DISA",     SelectedValue = 0 },
                new() { ColumnName = "StartAlt", DisplayLabel = "StartAlt", Format = "N0", UnitSuffix = " ft" }, // All
            },
        };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Series.Should().HaveCount(4);
        projection.Series.Select(s => s.Label).Should().Equal(
            "StartAlt=17,000 ft",
            "StartAlt=25,000 ft",
            "StartAlt=33,000 ft",
            "StartAlt=41,000 ft");
    }

    [Fact]
    public void Project_TwoAllInputs_ProducesCartesianProduct()
    {
        var data = BuildGriddedData();
        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight",   SelectedValue = 190000 },
                new() { ColumnName = "DISA",     DisplayLabel = "DISA" },     // All (3)
                new() { ColumnName = "StartAlt", DisplayLabel = "Alt"  },     // All (4)
            },
        };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Series.Should().HaveCount(12);
        projection.Truncated.Should().BeFalse();
        projection.TotalGroupCount.Should().Be(12);
    }

    [Fact]
    public void Project_ExceedingCap_TruncatesAndFlags()
    {
        var data = BuildGriddedData();
        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "Time",
            YAxisColumn = "LevelOff",
            Inputs = new()
            {
                new() { ColumnName = "Weight",   DisplayLabel = "W" },  // All (2)
                new() { ColumnName = "DISA",     DisplayLabel = "D" },  // All (3)
                new() { ColumnName = "StartAlt", DisplayLabel = "A" },  // All (4)
            },
        };

        var projection = BuildIndexer(maxLines: 10).Project(data, cfg);

        projection.Series.Should().HaveCount(10);
        projection.Truncated.Should().BeTrue();
        projection.TotalGroupCount.Should().Be(24);
    }

    [Fact]
    public void Project_ReturnsEmpty_WhenXOrYColumnMissing()
    {
        var data = BuildGriddedData();
        var cfg = new GroupedPlotConfig { XAxisColumn = "NoSuch", YAxisColumn = "LevelOff" };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Series.Should().BeEmpty();
        projection.Truncated.Should().BeFalse();
    }

    [Fact]
    public void Project_SortsEachSeriesByX()
    {
        var t = new DataTable();
        t.Columns.Add("g", typeof(double));
        t.Columns.Add("x", typeof(double));
        t.Columns.Add("y", typeof(double));
        // intentionally unsorted x within group
        t.Rows.Add(1.0, 3.0, 30.0);
        t.Rows.Add(1.0, 1.0, 10.0);
        t.Rows.Add(1.0, 2.0, 20.0);
        var data = new PlotDataModel { Data = t };

        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "x",
            YAxisColumn = "y",
            Inputs = new() { new() { ColumnName = "g", SelectedValue = 1.0 } },
        };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Series[0].X.Should().Equal(1.0, 2.0, 3.0);
        projection.Series[0].Y.Should().Equal(10.0, 20.0, 30.0);
    }

    [Fact]
    public void Project_SkipsRowsWithNaNX_Y_OrInputValues()
    {
        var t = new DataTable();
        t.Columns.Add("g", typeof(double));
        t.Columns.Add("x", typeof(double));
        t.Columns.Add("y", typeof(double));
        t.Rows.Add(1.0, 1.0, 1.0);
        t.Rows.Add(1.0, double.NaN, 2.0);   // skip
        t.Rows.Add(1.0, 2.0, double.NaN);   // skip
        t.Rows.Add(double.NaN, 3.0, 3.0);   // skip (group key NaN)
        var data = new PlotDataModel { Data = t };

        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "x",
            YAxisColumn = "y",
            Inputs = new() { new() { ColumnName = "g" } },  // All
        };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Series.Should().HaveCount(1);
        projection.Series[0].X.Should().Equal(1.0);
        projection.Series[0].Y.Should().Equal(1.0);
    }

    // --- C2: group-key must stay distinct for large-magnitude columns (no long overflow). ---

    [Fact]
    public void GetDistinctValues_LargeMagnitudeValues_KeepsValuesDistinct()
    {
        // |v| ~ 1.7e12 overflowed the old (long)(v / 1e-9) key, collapsing every value into one
        // bucket regardless of magnitude. Values separated by far more than the relative tolerance
        // (~1.7e3 at this magnitude) must remain distinct. (Separations below the ~9-sig-digit
        // relative tolerance are intentionally merged — that is the ValuesEqual contract.)
        var t = new DataTable();
        t.Columns.Add("epoch", typeof(double));
        double[] stamps = { 1_700_000_000_000d, 1_700_001_000_000d, 1_700_002_000_000d };
        foreach (var s in stamps) t.Rows.Add(s);
        var data = new PlotDataModel { Data = t };

        var distinct = BuildIndexer().GetDistinctValues(data, "epoch", out var capped);

        capped.Should().BeFalse();
        distinct.Should().Equal(stamps);
    }

    [Fact]
    public void Project_LargeMagnitudeInput_DoesNotMergeAllRows()
    {
        // One "All" input of large-magnitude values must expand to one line per distinct value,
        // not a single merged series (the overflow symptom).
        var t = new DataTable();
        t.Columns.Add("serial", typeof(double)); // ~1e12, well past the old overflow threshold
        t.Columns.Add("x", typeof(double));
        t.Columns.Add("y", typeof(double));
        double[] serials = { 1_000_000_000_000d, 2_000_000_000_000d };
        foreach (var sn in serials)
            for (int i = 0; i < 3; i++)
                t.Rows.Add(sn, (double)i, sn + i);
        var data = new PlotDataModel { Data = t };

        var cfg = new GroupedPlotConfig
        {
            XAxisColumn = "x",
            YAxisColumn = "y",
            Inputs = new() { new() { ColumnName = "serial" } }, // All
        };

        var projection = BuildIndexer().Project(data, cfg);

        projection.Series.Should().HaveCount(2);
    }

    [Fact]
    public void GetDistinctValues_NearEqualLargeValues_DedupWithinRelativeTolerance()
    {
        // Two large values that differ only within the relative tolerance must share a bucket.
        var t = new DataTable();
        t.Columns.Add("x", typeof(double));
        t.Rows.Add(1_700_000_000_000d);
        t.Rows.Add(1_700_000_000_000d + 1e-3); // relative diff ~6e-16 « 1e-9
        t.Rows.Add(1_700_000_500_000d);        // clearly different
        var data = new PlotDataModel { Data = t };

        var distinct = BuildIndexer().GetDistinctValues(data, "x", out _);

        distinct.Should().HaveCount(2);
    }
}
