using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Analysis;

public class MetricRegistryTests
{
    private readonly MetricRegistry _r = new();

    [Fact]
    public void Registers_AllPhase1_Metrics()
    {
        var ids = _r.All.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        ids.Should().Contain(new[]
        {
            "max", "min", "mean", "median", "stddev", "variance", "rms",
            "first", "last", "range", "peaktopeak",
            "p5", "p50", "p95",
            "slope", "integral",
            "count", "nancount",
        });
    }

    [Fact]
    public void Require_KnownId_ReturnsMetric()
    {
        var m = _r.Require("slope");
        m.Id.Should().Be("slope");
        m.Category.Should().Be(MetricCategory.Temporal);
        m.Kind.Should().Be(MetricKind.LineOnPlot);   // Slope carries the drawable trend line
    }

    [Fact]
    public void Require_UnknownId_Throws()
    {
        var act = () => _r.Require("nonexistent");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGet_UnknownId_ReturnsNull()
    {
        _r.TryGet("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Lookup_IsCaseInsensitive()
    {
        _r.Require("MAX").Should().NotBeNull();
        _r.Require("Slope").Should().NotBeNull();
    }

    [Fact]
    public void All_Has_StableCount()
    {
        _r.All.Count.Should().Be(18);   // Phase-1 metrics; Slope now carries the trend line (LinearFit folded in)
    }
}
