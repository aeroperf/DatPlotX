using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Models.Analysis;
using System.Globalization;

namespace DatPlotX.Views;

/// <summary>One selectable curve in the Tolerance Band dialog.</summary>
public sealed record ToleranceBandCurveChoice(string CurveId, string DisplayName, string? Unit)
{
    public override string ToString() =>
        string.IsNullOrEmpty(Unit) ? DisplayName : $"{DisplayName} ({Unit})";
}

/// <summary>
/// Modal dialog that configures a <see cref="ToleranceBand"/> for a curve: center source
/// (mean / median / user nominal), tolerance (absolute or percent), and scope (active segment
/// vs whole curve). A live preview resolves the concrete limits as the user types, via the
/// <c>previewResolver</c> the host supplies (so derived centers reflect the real data).
/// Returns the built band on Apply; <c>null</c> on cancel.
/// </summary>
public partial class ToleranceBandDialog : Window
{
    private readonly Func<ToleranceBand, (double Center, double Lower, double Upper)?>? _previewResolver;

    public ToleranceBand? Result { get; private set; }

    public ToleranceBandDialog(
        IReadOnlyList<ToleranceBandCurveChoice> curves,
        Func<ToleranceBand, (double Center, double Lower, double Upper)?>? previewResolver = null,
        ToleranceBand? existing = null)
    {
        InitializeComponent();
        _previewResolver = previewResolver;

        foreach (var c in curves) CurveCombo.Items.Add(c);
        CurveCombo.SelectedIndex = 0;

        if (existing is not null)
        {
            var match = curves.FirstOrDefault(c => string.Equals(c.CurveId, existing.CurveId, StringComparison.Ordinal));
            if (match is not null) CurveCombo.SelectedItem = match;
            CenterMean.IsChecked = existing.CenterMode == BandCenterMode.Mean;
            CenterMedian.IsChecked = existing.CenterMode == BandCenterMode.Median;
            CenterNominal.IsChecked = existing.CenterMode == BandCenterMode.UserNominal;
            NominalBox.Text = existing.NominalValue.ToString("0.######", CultureInfo.InvariantCulture);
            ToleranceBox.Text = existing.Tolerance.ToString("0.######", CultureInfo.InvariantCulture);
            UnitToggle.IsChecked = existing.ToleranceUnit == ToleranceMode.Percent;
            ScopeSegment.IsChecked = existing.Scope == BandScope.ActiveSegment;
            ScopeWhole.IsChecked = existing.Scope == BandScope.WholeCurve;
        }

        UpdateUnitToggleLabel();
        UpdateNominalEnabled();
        UpdatePreview();

        // Re-preview / re-gate on any input change.
        CurveCombo.SelectionChanged += (_, _) => UpdatePreview();
        foreach (var rb in new[] { CenterMean, CenterMedian, CenterNominal })
            rb.IsCheckedChanged += (_, _) => { UpdateNominalEnabled(); UpdatePreview(); };
        foreach (var rb in new[] { ScopeSegment, ScopeWhole })
            rb.IsCheckedChanged += (_, _) => UpdatePreview();
        NominalBox.TextChanged += (_, _) => UpdatePreview();
        ToleranceBox.TextChanged += (_, _) => UpdatePreview();
        UnitToggle.IsCheckedChanged += (_, _) => { UpdateUnitToggleLabel(); UpdatePreview(); };

        OkButton.Click += OnOk;
        CancelButton.Click += (_, _) => Close();
    }

    public ToleranceBandDialog() : this(Array.Empty<ToleranceBandCurveChoice>()) { }

    private void UpdateUnitToggleLabel() =>
        UnitToggle.Content = UnitToggle.IsChecked == true ? "percent (%)" : "absolute";

    private void UpdateNominalEnabled() =>
        NominalRow.IsEnabled = CenterNominal.IsChecked == true;

    private BandCenterMode CenterMode =>
        CenterNominal.IsChecked == true ? BandCenterMode.UserNominal
        : CenterMedian.IsChecked == true ? BandCenterMode.Median
        : BandCenterMode.Mean;

    private ToleranceMode ToleranceUnit =>
        UnitToggle.IsChecked == true ? ToleranceMode.Percent : ToleranceMode.Absolute;

    private BandScope Scope =>
        ScopeWhole.IsChecked == true ? BandScope.WholeCurve : BandScope.ActiveSegment;

    /// <summary>Build a band from the current inputs, or null when required numbers don't parse.</summary>
    private ToleranceBand? BuildBand()
    {
        if (CurveCombo.SelectedItem is not ToleranceBandCurveChoice curve) return null;
        if (!TryParse(ToleranceBox.Text, out var tol) || tol < 0) return null;

        double nominal = 0;
        if (CenterMode == BandCenterMode.UserNominal && !TryParse(NominalBox.Text, out nominal))
            return null;

        return new ToleranceBand(curve.CurveId, CenterMode, nominal, tol, ToleranceUnit, Scope);
    }

    private void UpdatePreview()
    {
        var band = BuildBand();
        if (band is null)
        {
            PreviewText.Text = "—";
            OkButton.IsEnabled = false;
            return;
        }

        OkButton.IsEnabled = true;

        var unit = (CurveCombo.SelectedItem as ToleranceBandCurveChoice)?.Unit;
        string suffix = string.IsNullOrEmpty(unit) ? "" : " " + unit;

        var resolved = _previewResolver?.Invoke(band);
        if (resolved is { } r && double.IsFinite(r.Lower) && double.IsFinite(r.Upper))
        {
            PreviewText.Text =
                $"center {Num(r.Center)}{suffix}\nlimits [{Num(r.Lower)}, {Num(r.Upper)}]{suffix}";
        }
        else
        {
            // No resolver / no data yet: still show the user-typed nominal case directly.
            var (c, lo, hi) = band.ResolveLimits(double.NaN);
            PreviewText.Text = double.IsFinite(lo)
                ? $"center {Num(c)}{suffix}\nlimits [{Num(lo)}, {Num(hi)}]{suffix}"
                : "center derived from data on apply";
        }
    }

    private void OnOk(object? sender, RoutedEventArgs e)
    {
        var band = BuildBand();
        if (band is null) return;
        Result = band;
        Close();
    }

    private static bool TryParse(string? s, out double value) =>
        double.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static string Num(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
