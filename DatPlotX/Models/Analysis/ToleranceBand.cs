namespace DatPlotX.Models.Analysis;

/// <summary>
/// Where a tolerance band's center line is derived from. Derived centers (the mean / median
/// modes) re-center live as the active segment changes; <see cref="UserNominal"/> freezes the
/// center at the value the user typed when the band was defined.
/// </summary>
public enum BandCenterMode
{
    /// <summary>Arithmetic mean of the curve over the evaluated scope.</summary>
    Mean,

    /// <summary>Median of the curve over the evaluated scope.</summary>
    Median,

    /// <summary>A fixed user-typed nominal value (the classic spec-limit entry).</summary>
    UserNominal,
}

/// <summary>
/// Which X span a tolerance band is evaluated (and drawn) over.
/// </summary>
public enum BandScope
{
    /// <summary>The analysis panel's currently active segment.</summary>
    ActiveSegment,

    /// <summary>The full data extent of the curve.</summary>
    WholeCurve,
}

/// <summary>
/// A user-defined horizontal tolerance / spec-limit band attached to one curve. Center is
/// resolved at evaluation time (so <see cref="BandCenterMode.Mean"/> / <see cref="BandCenterMode.Median"/>
/// re-center live when the segment is re-picked); the limits are then center ± tolerance, where
/// tolerance is read as absolute curve units or as a percent of the (absolute) center per
/// <see cref="ToleranceUnit"/>.
///
/// <para>Session-only — like the on-plot trend lines, a band is a transient overlay and is not
/// persisted to <c>.DPX</c> this pass.</para>
/// </summary>
/// <param name="CurveId">Owning curve identifier (mode-specific, same key space as <see cref="StatisticResult.CurveId"/>).</param>
/// <param name="CenterMode">How the center line is derived.</param>
/// <param name="NominalValue">The frozen center for <see cref="BandCenterMode.UserNominal"/>; ignored otherwise.</param>
/// <param name="Tolerance">Half-width of the band (always ≥ 0).</param>
/// <param name="ToleranceUnit">Whether <see cref="Tolerance"/> is absolute or a percent of the center.</param>
/// <param name="Scope">Which X span the band covers.</param>
public sealed record ToleranceBand(
    string CurveId,
    BandCenterMode CenterMode,
    double NominalValue,
    double Tolerance,
    ToleranceMode ToleranceUnit,
    BandScope Scope)
{
    /// <summary>
    /// Resolve the concrete (center, lower, upper) limits given an already-derived center value
    /// for the scope. <paramref name="derivedCenter"/> is ignored for <see cref="BandCenterMode.UserNominal"/>
    /// (the frozen <see cref="NominalValue"/> wins). Percent tolerance is taken against the
    /// absolute center, so a band around a negative center still has a positive half-width.
    /// Returns NaN limits when the inputs are not finite.
    /// </summary>
    public (double Center, double Lower, double Upper) ResolveLimits(double derivedCenter)
    {
        double center = CenterMode == BandCenterMode.UserNominal ? NominalValue : derivedCenter;
        if (!double.IsFinite(center)) return (double.NaN, double.NaN, double.NaN);

        double half = ToleranceUnit == ToleranceMode.Percent
            ? Math.Abs(center) * (Math.Abs(Tolerance) / 100.0)
            : Math.Abs(Tolerance);

        return (center, center - half, center + half);
    }
}
