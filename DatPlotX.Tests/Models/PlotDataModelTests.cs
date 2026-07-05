using DatPlotX.Models;
using FluentAssertions;
using System.Data;

namespace DatPlotX.Tests.Models;

public class PlotDataModelTests
{
    private static PlotDataModel MakeModel(params (string col, double[] values)[] columns)
    {
        var model = new PlotDataModel();
        foreach (var (col, _) in columns)
            model.Data.Columns.Add(col, typeof(double));

        int rowCount = columns.Max(c => c.values.Length);
        for (int i = 0; i < rowCount; i++)
        {
            var row = model.Data.NewRow();
            foreach (var (col, values) in columns)
                row[col] = i < values.Length ? values[i] : DBNull.Value;
            model.Data.Rows.Add(row);
        }
        return model;
    }

    [Fact]
    public void ColumnNames_ReflectsDataTableColumns()
    {
        var model = MakeModel(("time", [1.0, 2.0]), ("gFx", [3.0, 4.0]));
        model.ColumnNames.Should().BeEquivalentTo(["time", "gFx"]);
    }

    [Fact]
    public void RowCount_MatchesDataTableRows()
    {
        var model = MakeModel(("time", [1.0, 2.0, 3.0]));
        model.RowCount.Should().Be(3);
    }

    [Fact]
    public void ColumnCount_MatchesDataTableColumnCount()
    {
        var model = MakeModel(("time", [1.0]), ("gFx", [2.0]));
        model.ColumnCount.Should().Be(2);
    }

    [Fact]
    public void GetColumnData_ByName_ReturnsCorrectValues()
    {
        var model = MakeModel(("time", [0.0, 1.0, 2.0]));
        var data = model.GetColumnData("time");
        data.Should().Equal([0.0, 1.0, 2.0]);
    }

    [Fact]
    public void GetColumnData_UnknownColumn_Throws()
    {
        var model = MakeModel(("time", [1.0]));
        var act = () => model.GetColumnData("notexist");
        act.Should().Throw<ArgumentException>().WithMessage("*notexist*");
    }

    [Fact]
    public void GetColumnData_DBNullValues_ReturnNaN()
    {
        var model = new PlotDataModel();
        model.Data.Columns.Add("val", typeof(double));
        var row = model.Data.NewRow();
        row["val"] = DBNull.Value;
        model.Data.Rows.Add(row);

        var data = model.GetColumnData("val");
        data[0].Should().Be(double.NaN);
    }

    [Fact]
    public void GetColumnData_ByIndex_ReturnsCorrectValues()
    {
        var model = MakeModel(("time", [0.0, 1.0]), ("gFx", [10.0, 20.0]));
        var data = model.GetColumnData(1);
        data.Should().Equal([10.0, 20.0]);
    }

    [Fact]
    public void GetColumnData_NegativeIndex_Throws()
    {
        var model = MakeModel(("time", [1.0]));
        var act = () => model.GetColumnData(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetColumnData_IndexOutOfRange_Throws()
    {
        var model = MakeModel(("time", [1.0]));
        var act = () => model.GetColumnData(5);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetColumnData_StringColumn_NonConvertible_ReturnsNaN()
    {
        var model = new PlotDataModel();
        model.Data.Columns.Add("label", typeof(string));
        var row = model.Data.NewRow();
        row["label"] = "not_a_number";
        model.Data.Rows.Add(row);

        var data = model.GetColumnData("label");
        data[0].Should().Be(double.NaN);
    }

    [Fact]
    public void ImportedAt_SetOnConstruction()
    {
        var before = DateTime.Now;
        var model = new PlotDataModel();
        model.ImportedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void GetColumnData_IntColumn_ReturnsTypedConversion()
    {
        var model = new PlotDataModel();
        model.Data.Columns.Add("idx", typeof(int));
        model.Data.Rows.Add(1);
        model.Data.Rows.Add(2);
        model.Data.Rows.Add(3);
        model.GetColumnData("idx").Should().Equal([1.0, 2.0, 3.0]);
    }

    [Fact]
    public void GetColumnData_LongColumn_ReturnsTypedConversion()
    {
        var model = new PlotDataModel();
        model.Data.Columns.Add("big", typeof(long));
        model.Data.Rows.Add(10_000_000_000L);
        model.GetColumnData("big")[0].Should().Be(10_000_000_000d);
    }

    [Fact]
    public void GetColumnData_FloatColumn_ReturnsTypedConversion()
    {
        var model = new PlotDataModel();
        model.Data.Columns.Add("f", typeof(float));
        model.Data.Rows.Add(1.25f);
        model.GetColumnData("f")[0].Should().Be(1.25);
    }

    [Fact]
    public void GetColumnData_CachedPerColumn_ReturnsSameArrayOnRepeatCall()
    {
        var model = MakeModel(("v", [1.0, 2.0, 3.0]));
        var first = model.GetColumnData("v");
        var second = model.GetColumnData("v");
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetColumnData_CacheInvalidated_OnDataReassignment()
    {
        var model = MakeModel(("v", [1.0, 2.0]));
        _ = model.GetColumnData("v");
        var replacement = new DataTable();
        replacement.Columns.Add("v", typeof(double));
        replacement.Rows.Add(99.0);
        model.Data = replacement;
        model.GetColumnData("v").Should().Equal([99.0]);
    }

    [Fact]
    public void InvalidateColumnCache_ForcesRecompute()
    {
        var model = MakeModel(("v", [1.0, 2.0]));
        var first = model.GetColumnData("v");
        model.InvalidateColumnCache();
        var second = model.GetColumnData("v");
        second.Should().NotBeSameAs(first);
        second.Should().Equal(first);
    }
}
