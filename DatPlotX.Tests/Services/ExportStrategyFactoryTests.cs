using DatPlotX.Services.Export;
using FluentAssertions;
using Moq;

namespace DatPlotX.Tests.Services;

public class ExportStrategyFactoryTests
{
    private readonly ExportStrategyFactory _factory = new();

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".bmp")]
    [InlineData(".svg")]
    public void GetStrategy_KnownExtension_ReturnsStrategy(string ext)
    {
        var strategy = _factory.GetStrategy(ext);
        strategy.Should().NotBeNull();
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpg")]
    [InlineData("svg")]
    public void GetStrategy_ExtensionWithoutDot_NormalizesAutomatically(string ext)
    {
        var act = () => _factory.GetStrategy(ext);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(".PNG")]
    [InlineData(".JPG")]
    [InlineData(".SVG")]
    public void GetStrategy_UppercaseExtension_CaseInsensitive(string ext)
    {
        var strategy = _factory.GetStrategy(ext);
        strategy.Should().NotBeNull();
    }

    [Fact]
    public void GetStrategy_UnknownExtension_ThrowsNotSupported()
    {
        var act = () => _factory.GetStrategy(".xyz");
        act.Should().Throw<NotSupportedException>().WithMessage("*not supported*");
    }

    [Fact]
    public void GetStrategy_EmptyExtension_Throws()
    {
        var act = () => _factory.GetStrategy("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetStrategy_NullExtension_Throws()
    {
        var act = () => _factory.GetStrategy(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(".png", true)]
    [InlineData(".jpg", true)]
    [InlineData(".svg", true)]
    [InlineData(".xyz", false)]
    [InlineData("", false)]
    public void IsFormatSupported_VariousExtensions(string ext, bool expected)
    {
        _factory.IsFormatSupported(ext).Should().Be(expected);
    }

    [Fact]
    public void GetAllStrategies_ReturnsDistinctStrategies()
    {
        var strategies = _factory.GetAllStrategies().ToList();
        strategies.Should().NotBeEmpty();
        strategies.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetCombinedFilter_ReturnsNonEmpty()
    {
        _factory.GetCombinedFilter().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void RegisterStrategy_NewExtension_CanBeRetrieved()
    {
        var mock = new Mock<IImageExportStrategy>();
        mock.Setup(s => s.FilterDescription).Returns("Test|*.tst");
        _factory.RegisterStrategy(".tst", mock.Object);
        _factory.GetStrategy(".tst").Should().BeSameAs(mock.Object);
    }

    [Fact]
    public void RegisterStrategy_NullStrategy_Throws()
    {
        var act = () => _factory.RegisterStrategy(".tst", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterStrategy_EmptyExtension_Throws()
    {
        var mock = new Mock<IImageExportStrategy>();
        var act = () => _factory.RegisterStrategy("", mock.Object);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegisterStrategy_OverridesExistingExtension()
    {
        var mock = new Mock<IImageExportStrategy>();
        mock.Setup(s => s.FilterDescription).Returns("Custom PNG|*.png");
        _factory.RegisterStrategy(".png", mock.Object);
        _factory.GetStrategy(".png").Should().BeSameAs(mock.Object);
    }
}
