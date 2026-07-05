namespace DatPlotX.Services.Analysis;

/// <summary>
/// Holds every <see cref="IMetricDefinition"/> registered with the app. Singleton — populated
/// once in DI startup and then read-only.
/// </summary>
public interface IMetricRegistry
{
    /// <summary>All registered metrics, in stable registration order.</summary>
    IReadOnlyList<IMetricDefinition> All { get; }

    /// <summary>Lookup by <see cref="IMetricDefinition.Id"/>. Throws when the ID is unknown.</summary>
    IMetricDefinition Require(string metricId);

    /// <summary>Lookup by ID. Returns <c>null</c> when the ID is unknown.</summary>
    IMetricDefinition? TryGet(string metricId);
}
