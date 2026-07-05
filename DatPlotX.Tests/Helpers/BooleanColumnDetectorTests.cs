using DatPlotX.Helpers;
using FluentAssertions;
using System.Data;
using System.Globalization;

namespace DatPlotX.Tests.Helpers;

public class BooleanColumnDetectorTests
{
    [Fact]
    public void NativeBoolColumn_DetectedAsBoolean()
    {
        using var t = new DataTable();
        t.Columns.Add("flag", typeof(bool));
        t.Rows.Add(true);
        t.Rows.Add(false);

        BooleanColumnDetector.IsBooleanColumn(t, "flag").Should().BeTrue();
    }

    [Fact]
    public void NumericColumnWithOnlyZeroAndOne_DetectedAsBoolean()
    {
        using var t = new DataTable();
        t.Columns.Add("sw", typeof(int));
        t.Rows.Add(0);
        t.Rows.Add(1);
        t.Rows.Add(0);
        t.Rows.Add(1);

        BooleanColumnDetector.IsBooleanColumn(t, "sw").Should().BeTrue();
    }

    [Fact]
    public void NumericColumnWithOtherValues_RejectedAsBoolean()
    {
        using var t = new DataTable();
        t.Columns.Add("v", typeof(int));
        t.Rows.Add(0);
        t.Rows.Add(1);
        t.Rows.Add(2);

        BooleanColumnDetector.IsBooleanColumn(t, "v").Should().BeFalse();
    }

    [Fact]
    public void StringColumnWithTrueFalse_DetectedAsBoolean()
    {
        using var t = new DataTable();
        t.Columns.Add("flag", typeof(string));
        t.Rows.Add("True");
        t.Rows.Add("false");
        t.Rows.Add("TRUE");

        BooleanColumnDetector.IsBooleanColumn(t, "flag").Should().BeTrue();
    }

    [Fact]
    public void DoubleColumnWithFractionalValues_RejectedAsBoolean()
    {
        using var t = new DataTable();
        t.Columns.Add("alt", typeof(double));
        t.Rows.Add(0.0);
        t.Rows.Add(0.5);
        t.Rows.Add(1.0);

        BooleanColumnDetector.IsBooleanColumn(t, "alt").Should().BeFalse();
    }

    [Fact]
    public void EmptyTable_NotBoolean()
    {
        using var t = new DataTable();
        t.Columns.Add("v", typeof(int));

        BooleanColumnDetector.IsBooleanColumn(t, "v").Should().BeFalse();
    }

    [Fact]
    public void MissingColumn_NotBoolean()
    {
        using var t = new DataTable();
        BooleanColumnDetector.IsBooleanColumn(t, "nope").Should().BeFalse();
    }

    [Theory]
    [InlineData("yes", "no")]
    [InlineData("YES", "NO")]
    [InlineData("on", "off")]
    [InlineData("On", "Off")]
    public void StringColumnWithYesNoOnOff_DetectedAsBoolean(string truthy, string falsy)
    {
        using var t = new DataTable();
        t.Columns.Add("flag", typeof(string));
        t.Rows.Add(truthy);
        t.Rows.Add(falsy);
        t.Rows.Add(truthy);

        BooleanColumnDetector.IsBooleanColumn(t, "flag").Should().BeTrue();
    }

    [Fact]
    public void StringColumnWithWhitespaceMixedWithBoolean_StillBoolean()
    {
        // Whitespace-only strings are treated as null-equivalent by the string scanner —
        // they don't disqualify the column. If at least one real bool value is present,
        // the column counts as boolean.
        using var t = new DataTable();
        t.Columns.Add("flag", typeof(string));
        t.Rows.Add("   ");
        t.Rows.Add("");
        t.Rows.Add("true");

        BooleanColumnDetector.IsBooleanColumn(t, "flag").Should().BeTrue();
    }

    [Fact]
    public void AllDbNullRows_NotBoolean()
    {
        using var t = new DataTable();
        var col = t.Columns.Add("flag", typeof(int));
        col.AllowDBNull = true;
        t.Rows.Add(DBNull.Value);
        t.Rows.Add(DBNull.Value);

        BooleanColumnDetector.IsBooleanColumn(t, "flag").Should().BeFalse();
    }

    [Fact]
    public void Span_OnlyZeroAndOne_DetectedAsBoolean()
    {
        double[] values = { 0d, 1d, 0d, 1d };
        BooleanColumnDetector.IsBooleanColumn(values).Should().BeTrue();
    }

    [Fact]
    public void Span_NaNTreatedAsNull_StillBoolean()
    {
        double[] values = { 0d, double.NaN, 1d };
        BooleanColumnDetector.IsBooleanColumn(values).Should().BeTrue();
    }

    [Fact]
    public void Span_AllNaN_NotBoolean()
    {
        double[] values = { double.NaN, double.NaN };
        BooleanColumnDetector.IsBooleanColumn(values).Should().BeFalse();
    }

    [Fact]
    public void Span_Empty_NotBoolean()
    {
        BooleanColumnDetector.IsBooleanColumn(ReadOnlySpan<double>.Empty).Should().BeFalse();
    }

    [Fact]
    public void Span_FractionalValueRejected()
    {
        double[] values = { 0d, 0.5, 1d };
        BooleanColumnDetector.IsBooleanColumn(values).Should().BeFalse();
    }

    // Review D1: a de-DE string column storing comma-decimal "0,0"/"1,0" must classify as boolean
    // when the project culture is passed, and NOT under the InvariantCulture default (where "0,0"
    // parses as neither 0 nor 1). Guards the "always pass an explicit culture" rule.
    [Fact]
    public void StringColumn_CommaDecimalBool_DetectedUnderMatchingCulture()
    {
        using var t = new DataTable();
        t.Columns.Add("gear", typeof(string));
        t.Rows.Add("0,0");
        t.Rows.Add("1,0");
        t.Rows.Add("0,0");

        var deDE = CultureInfo.GetCultureInfo("de-DE");
        BooleanColumnDetector.IsBooleanColumn(t, "gear", deDE).Should().BeTrue();
    }

    [Fact]
    public void StringColumn_CommaDecimalBool_RejectedUnderInvariantDefault()
    {
        using var t = new DataTable();
        t.Columns.Add("gear", typeof(string));
        t.Rows.Add("0,0");
        t.Rows.Add("1,0");

        // Default (InvariantCulture): "0,0" is not a recognized boolean token.
        BooleanColumnDetector.IsBooleanColumn(t, "gear").Should().BeFalse();
    }
}
