using DatPlotX.Services.Units;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Units;

public class UnitRegistryTests
{
    private readonly UnitRegistry _r = UnitRegistry.Default;

    [Theory]
    [InlineData("ft")]
    [InlineData("FT")]
    [InlineData("feet")]
    [InlineData("kt")]
    [InlineData("knots")]
    [InlineData("psi")]
    [InlineData("°C")]
    [InlineData("degC")]
    [InlineData("RPM")]
    public void Recognizes_CommonAliases(string unit)
    {
        _r.IsKnown(unit).Should().BeTrue();
    }

    [Theory]
    [InlineData("widgets")]
    [InlineData("flux")]
    [InlineData("")]
    public void Rejects_Unknown(string unit)
    {
        _r.IsKnown(unit).Should().BeFalse();
    }

    [Theory]
    [InlineData("FT", "ft")]
    [InlineData("feet", "ft")]
    [InlineData("knots", "kt")]
    [InlineData("degC", "°C")]
    [InlineData("widgets", "widgets")]    // unknown -> verbatim
    public void Normalizes_ToCanonical(string input, string expected)
    {
        _r.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Slope_ft_per_s_Yields_ft_per_min()
    {
        var rate = _r.PreferredDerivedRate("ft", "s");
        rate.Should().NotBeNull();
        rate!.Label.Should().Be("ft/min");
        rate.Multiplier.Should().BeApproximately(60.0, 1e-9);
    }

    [Theory]
    [InlineData("ftMSL")]
    [InlineData("ftAGL")]
    [InlineData("ft-MSL")]
    public void Slope_altitudeFeetVariant_per_s_Yields_ft_per_min(string altitudeUnit)
    {
        // X-Plane altitude columns carry units like "ftMSL" / "ftagl" — still feet
        // dimensionally, so the rate-of-climb conversion must fire.
        var rate = _r.PreferredDerivedRate(altitudeUnit, "s");
        rate.Should().NotBeNull();
        rate!.Label.Should().Be("ft/min");
        rate.Multiplier.Should().BeApproximately(60.0, 1e-9);
    }

    [Fact]
    public void Slope_ft_per_time_Yields_ft_per_min()
    {
        // "time" is X-Plane's elapsed-time unit token (always seconds).
        var rate = _r.PreferredDerivedRate("ftMSL", "time");
        rate.Should().NotBeNull();
        rate!.Label.Should().Be("ft/min");
    }

    [Fact]
    public void Slope_m_per_s_Yields_knots()
    {
        var rate = _r.PreferredDerivedRate("m", "s");
        rate.Should().NotBeNull();
        rate!.Label.Should().Be("kt");
        rate.Multiplier.Should().BeApproximately(1.94384449, 1e-6);
    }

    [Fact]
    public void Slope_Pa_per_s_NoDerivedRate()
    {
        _r.PreferredDerivedRate("Pa", "s").Should().BeNull();
    }

    [Fact]
    public void Slope_NullOrEmpty_NoDerivedRate()
    {
        _r.PreferredDerivedRate("", "s").Should().BeNull();
        _r.PreferredDerivedRate("ft", "").Should().BeNull();
    }
}
