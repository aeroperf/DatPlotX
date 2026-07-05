namespace DatPlotX.Services.Units;

/// <summary>
/// Catalog of recognized engineering units and the derived-rate conversions used by
/// the <see cref="Analysis.Metrics.SlopeMetric"/>. Pure / stateless; safe to share as a singleton.
/// </summary>
public interface IUnitRegistry
{
    /// <summary>True when <paramref name="unit"/> matches any known unit (case-insensitive).</summary>
    bool IsKnown(string unit);

    /// <summary>
    /// Canonical form for a known unit (e.g. <c>"FT"</c> → <c>"ft"</c>, <c>"deg C"</c> → <c>"°C"</c>).
    /// Returns the input unchanged when the unit is unknown.
    /// </summary>
    string Normalize(string unit);

    /// <summary>
    /// Engineer-friendly derived rate for the slope metric, given the curve's Y unit and
    /// the X-axis unit. Returns null when there's no canonical conversion (e.g. <c>Pa/s</c>).
    /// </summary>
    /// <example>
    /// <c>PreferredDerivedRate("ft", "s")</c> → <c>(60, "ft/min")</c>;
    /// <c>PreferredDerivedRate("m",  "s")</c> → <c>(1.94384, "kt")</c>.
    /// </example>
    DerivedRate? PreferredDerivedRate(string baseUnit, string xUnit);
}
