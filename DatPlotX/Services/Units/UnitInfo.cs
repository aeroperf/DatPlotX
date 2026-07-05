namespace DatPlotX.Services.Units;

/// <summary>
/// Result of parsing a CSV column name. The display name has any unit suffix stripped;
/// <see cref="Unit"/> is null when no recognizable unit was found.
/// </summary>
public sealed record UnitInfo(string DisplayName, string? Unit);

/// <summary>
/// A unit-conversion target for the <see cref="SlopeMetric"/>'s "engineer-friendly derived rate"
/// (e.g. raw slope is <c>ft/s</c>; derived rate is <c>(60, "ft/min")</c>).
/// </summary>
/// <param name="Multiplier">Multiplied into the raw slope value to produce the derived rate.</param>
/// <param name="Label">Label rendered next to the derived value.</param>
public sealed record DerivedRate(double Multiplier, string Label);
