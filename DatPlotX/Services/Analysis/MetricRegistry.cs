using DatPlotX.Services.Analysis.Metrics;
using System.Collections.Frozen;

namespace DatPlotX.Services.Analysis;

/// <inheritdoc />
public sealed class MetricRegistry : IMetricRegistry
{
    private readonly FrozenDictionary<string, IMetricDefinition> _byId;

    public MetricRegistry()
    {
        var metrics = new IMetricDefinition[]
        {
            // Basic
            new MaxMetric(),
            new MinMetric(),
            new MeanMetric(),
            new MedianMetric(),
            new StdDevMetric(),
            new VarianceMetric(),
            new RmsMetric(),
            new FirstMetric(),
            new LastMetric(),
            new RangeMetric(),
            new PeakToPeakMetric(),

            // Distribution
            new PercentileMetric(5),
            new PercentileMetric(50),
            new PercentileMetric(95),

            // Temporal
            new SlopeMetric(),
            new IntegralMetric(),

            // Quality
            new CountMetric(),
            new NanCountMetric(),
        };

        All = metrics;
        _byId = metrics.ToFrozenDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<IMetricDefinition> All { get; }

    public IMetricDefinition Require(string metricId) =>
        _byId.TryGetValue(metricId, out var m)
            ? m
            : throw new KeyNotFoundException($"Metric '{metricId}' is not registered.");

    public IMetricDefinition? TryGet(string metricId) =>
        _byId.TryGetValue(metricId, out var m) ? m : null;
}
