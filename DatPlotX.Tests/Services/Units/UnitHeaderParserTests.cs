using DatPlotX.Services.Units;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Units;

public class UnitHeaderParserTests
{
    [Theory]
    [InlineData("Altitude [ft]", "Altitude", "ft")]
    [InlineData("EGT (°C)", "EGT", "°C")]
    [InlineData("AoA, deg", "AoA", "deg")]
    [InlineData("Altitude_ft", "Altitude", "ft")]
    [InlineData("pressure-psi", "pressure", "psi")]
    [InlineData("airspeed [kt]", "airspeed", "kt")]
    [InlineData("EGT (degC)", "EGT", "°C")]    // normalize alias
    [InlineData("delta_p [Pa]", "delta_p", "Pa")]
    [InlineData("count", "count", null)]
    [InlineData("velocity_x", "velocity_x", null)]    // _x is not a known unit
    [InlineData("thrust-port", "thrust-port", null)]   // -port is not a known unit
    [InlineData("my [bracket] column [ft]", "my [bracket] column", "ft")]   // trailing group wins
    public void ParsesCommonHeaderConventions(string header, string expectedName, string? expectedUnit)
    {
        var info = UnitHeaderParser.Parse(header);
        info.DisplayName.Should().Be(expectedName);
        info.Unit.Should().Be(expectedUnit);
    }

    [Fact]
    public void EmptyHeader_ReturnsEmptyName()
    {
        UnitHeaderParser.Parse("").DisplayName.Should().BeEmpty();
        UnitHeaderParser.Parse("").Unit.Should().BeNull();
    }

    [Fact]
    public void WhitespaceHeader_ReturnsWhitespaceVerbatim_AndNullUnit()
    {
        var info = UnitHeaderParser.Parse("   ");
        info.Unit.Should().BeNull();
    }

    [Fact]
    public void UnknownUnitInBrackets_IsPreservedVerbatim()
    {
        // Brackets always win regardless of known-unit set — the user wrote them on purpose.
        var info = UnitHeaderParser.Parse("Channel [widgets]");
        info.DisplayName.Should().Be("Channel");
        info.Unit.Should().Be("widgets");
    }

    [Fact]
    public void EmptyBrackets_AreIgnored()
    {
        var info = UnitHeaderParser.Parse("Channel []");
        info.DisplayName.Should().Be("Channel []");
        info.Unit.Should().BeNull();
    }

    [Fact]
    public void Unicode_DeltaP_Parsed()
    {
        var info = UnitHeaderParser.Parse("Δp [Pa]");
        info.DisplayName.Should().Be("Δp");
        info.Unit.Should().Be("Pa");
    }
}
