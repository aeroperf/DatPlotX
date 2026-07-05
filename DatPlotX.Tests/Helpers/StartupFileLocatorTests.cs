using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class StartupFileLocatorTests
{
    [Fact]
    public void FindProjectArgument_ReturnsNull_ForNullArgs()
        => StartupFileLocator.FindProjectArgument(null).Should().BeNull();

    [Fact]
    public void FindProjectArgument_ReturnsNull_WhenNoDpxArgument()
        => StartupFileLocator.FindProjectArgument(new[] { "--flag", "data.csv" }).Should().BeNull();

    [Fact]
    public void FindProjectArgument_ReturnsFirstDpxPath()
        => StartupFileLocator.FindProjectArgument(new[] { @"C:\projects\flight.dpx" })
            .Should().Be(@"C:\projects\flight.dpx");

    [Fact]
    public void FindProjectArgument_IsCaseInsensitive()
        => StartupFileLocator.FindProjectArgument(new[] { "FLIGHT.DPX" }).Should().Be("FLIGHT.DPX");

    [Fact]
    public void FindProjectArgument_ReturnsFirstMatch_WhenMultiple()
        => StartupFileLocator.FindProjectArgument(new[] { "a.dpx", "b.dpx" }).Should().Be("a.dpx");

    [Fact]
    public void FindProjectArgument_SkipsNonDpxBeforeMatch()
        => StartupFileLocator.FindProjectArgument(new[] { "--open", "notes.txt", "p.dpx" })
            .Should().Be("p.dpx");
}
