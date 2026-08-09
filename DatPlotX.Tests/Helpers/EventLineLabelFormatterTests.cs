using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class EventLineLabelFormatterTests
{
    [Theory]
    [InlineData(218.171, 2, "218.17")]
    [InlineData(218.171, 3, "218.171")]
    [InlineData(218.171, 0, "218")]
    [InlineData(-4.5, 1, "-4.5")]
    public void Format_UsesRequestedDecimals(double x, int decimals, string expected)
    {
        EventLineLabelFormatter.Format(x, decimals).Should().Be(expected);
    }

    [Fact]
    public void Format_IsCultureInvariant()
    {
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            // de-DE writes a comma decimal separator; the label must stay '.' so it matches the
            // axis ticks the app renders elsewhere.
            System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            EventLineLabelFormatter.Format(218.171, 2).Should().Be("218.17");
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void Format_ClampsDecimalsToSupportedRange(int decimals)
    {
        var text = EventLineLabelFormatter.Format(1.23456789, decimals);
        int expectedDecimals = decimals < 0 ? EventLineLabelFormatter.MinDecimals : EventLineLabelFormatter.MaxDecimals;
        int actualDecimals = text.Contains('.') ? text.Split('.')[1].Length : 0;
        actualDecimals.Should().Be(expectedDecimals);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Format_NonFinite_ReturnsEmpty(double x)
    {
        EventLineLabelFormatter.Format(x, 2).Should().BeEmpty();
    }

    [Fact]
    public void FormatOrNull_ToggleOff_ReturnsNull()
    {
        EventLineLabelFormatter.FormatOrNull(218.171, show: false, decimals: 2).Should().BeNull();
    }

    [Fact]
    public void FormatOrNull_NonFinite_ReturnsNull()
    {
        EventLineLabelFormatter.FormatOrNull(double.NaN, show: true, decimals: 2).Should().BeNull();
    }

    [Fact]
    public void FormatOrNull_ToggleOn_ReturnsFormattedValue()
    {
        EventLineLabelFormatter.FormatOrNull(218.171, show: true, decimals: 2).Should().Be("218.17");
    }
}
