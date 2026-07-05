namespace DatPlotX.Models.Analysis;

/// <summary>
/// How a tolerance / threshold value is interpreted by a parameterized metric.
/// </summary>
public enum ToleranceMode
{
    /// <summary>Value is in the curve's own units (e.g. ±500 ft).</summary>
    Absolute,

    /// <summary>Value is a percentage of the band center (e.g. ±5 %).</summary>
    Percent,
}

/// <summary>
/// Optional, immutable parameter bag passed to <see cref="Services.Analysis.IMetricDefinition.Compute"/>.
/// The Phase-1 metrics ignore it (they are pure functions of <c>(x, y)</c>); Phase-2B
/// parameterized metrics (tolerance / exceedance) read named values from <see cref="Values"/>
/// and the <see cref="Tolerance"/> mode. Construction is allocation-free for the common
/// no-parameter case via <see cref="None"/>.
/// </summary>
/// <param name="Values">Named scalar inputs (e.g. <c>"center"</c>, <c>"tolerance"</c>). Empty when unused.</param>
/// <param name="Tolerance">How a tolerance value in <see cref="Values"/> is read.</param>
public sealed record MetricParameters(
    IReadOnlyDictionary<string, double> Values,
    ToleranceMode Tolerance = ToleranceMode.Absolute)
{
    private static readonly IReadOnlyDictionary<string, double> EmptyValues =
        new Dictionary<string, double>();

    /// <summary>Shared empty bag — the default for every Phase-1 metric.</summary>
    public static MetricParameters None { get; } = new(EmptyValues);

    /// <summary>Read a named value, or <paramref name="fallback"/> when absent.</summary>
    public double Get(string key, double fallback = double.NaN) =>
        Values.TryGetValue(key, out var v) ? v : fallback;
}
