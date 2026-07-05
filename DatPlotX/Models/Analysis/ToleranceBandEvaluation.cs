using DatPlotX.Services.Analysis;

namespace DatPlotX.Models.Analysis;

/// <summary>
/// A computed tolerance band ready for the panel and overlay: the band config, its evaluation
/// metrics, the resolved X span it covers, and the curve's display units. Produced by
/// <see cref="IAnalysisService.ComputeToleranceBandsAsync"/>.
/// </summary>
/// <param name="Band">The source band config.</param>
/// <param name="DisplayName">Owning curve's display name.</param>
/// <param name="ColorHex">Owning curve's color (for the panel swatch / band stroke).</param>
/// <param name="Units">Curve display units, or null.</param>
/// <param name="Result">In-band fraction, crossings, exceedance duration, max excursion, and resolved limits.</param>
/// <param name="XMin">Left edge of the evaluated scope (segment or whole-curve), for drawing.</param>
/// <param name="XMax">Right edge of the evaluated scope.</param>
public sealed record ToleranceBandEvaluation(
    ToleranceBand Band,
    string DisplayName,
    string ColorHex,
    string? Units,
    ToleranceBandResult Result,
    double XMin,
    double XMax);
