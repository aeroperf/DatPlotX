namespace DatPlotX.Services.Analysis;

/// <summary>
/// Metadata about one curve from the perspective of the analysis service. Plot-mode-agnostic —
/// each mode VM produces these descriptors via its <see cref="IAnalysisCurveSource"/>
/// implementation.
/// </summary>
/// <param name="CurveId">Stable identifier — usually the source <c>Guid.ToString()</c> of the
/// curve config / compact-curve model / grouped series.</param>
/// <param name="DisplayName">Label shown in the Analysis panel's first column.</param>
/// <param name="ColorHex">Hex color used for the curve's row swatch (matches the plotted color).</param>
/// <param name="Unit">Optional unit; null when unknown. Drives result formatting + slope's derived rate.</param>
/// <param name="IsVisible">When false, the panel may dim the row (or skip it entirely
/// depending on user preference).</param>
public sealed record AnalysisCurveDescriptor(
    string CurveId,
    string DisplayName,
    string ColorHex,
    string? Unit,
    bool IsVisible);
