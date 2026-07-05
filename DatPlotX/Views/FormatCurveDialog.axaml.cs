using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DatPlotX.Views;

/// <summary>
/// Stacked-mode Manage Curve dialog: pick a curve on the active pane, edit its
/// stroke / marker settings, or delete it. Dpx-styled counterpart of
/// <see cref="ManageCompactCurveDialog"/>.
/// </summary>
public partial class FormatCurveDialog : Window
{
    private readonly List<CurveEntry> _entries = new();

    private string _curveColor = "#0078D4";
    private string _markerColor = "#0078D4";
    private bool _suppressBindings;

    public CurveConfigurationModel? SelectedCurve { get; private set; }
    public bool DeleteRequested { get; private set; }

    public FormatCurveDialog(ObservableCollection<CurveConfigurationModel> curves, int paneIndex)
    {
        InitializeComponent();

        var paneCurves = curves.Where(c => c.PaneIndex == paneIndex).ToList();
        if (paneCurves.Count == 0)
        {
            Close(false);
            return;
        }

        BuildEntries(paneCurves);
        WireEvents();

        CurveSelector.SelectedIndex = -1;
        SetEditingEnabled(false);
        this.EnableEscapeToClose(false);
    }

    public FormatCurveDialog()
    {
        InitializeComponent();
        BuildEntries(Array.Empty<CurveConfigurationModel>());
        WireEvents();
        SetEditingEnabled(false);
    }

    private void BuildEntries(IEnumerable<CurveConfigurationModel> curves)
    {
        _entries.Clear();
        foreach (var curve in curves)
            _entries.Add(new CurveEntry(curve));
        CurveSelector.ItemsSource = _entries;
    }

    private void WireEvents()
    {
        ColorSwatchPicker.PropertyChanged += (_, e) =>
        {
            if (e.Property == Controls.Dpx.ColorSwatches.ColorProperty) OnSwatchColor();
        };
        ColorSwatchPicker.CustomPickRequested += (_, _) => OpenCurveColorPicker();
        ColorPickerPill.PickRequested += (_, _) => OpenCurveColorPicker();

        WidthSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                WidthValue.Text = WidthSlider.Value.ToString("F1", CultureInfo.InvariantCulture) + " px";
        };

        ShowLineCheckBox.IsCheckedChanged += (_, _) => UpdateStrokeEnablement();

        ShowMarkersCheckBox.IsCheckedChanged += (_, _) => UpdateMarkerEnablement();

        MarkerSizeSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                MarkerSizeValue.Text = MarkerSizeSlider.Value.ToString("F0", CultureInfo.InvariantCulture) + " px";
        };

        MarkerSwatchPicker.PropertyChanged += (_, e) =>
        {
            if (e.Property == Controls.Dpx.ColorSwatches.ColorProperty) OnMarkerSwatchColor();
        };
        MarkerSwatchPicker.CustomPickRequested += (_, _) => OpenMarkerColorPicker();

        MatchCurveColor.IsCheckedChanged += (_, _) =>
        {
            if (_suppressBindings) return;
            if (MatchCurveColor.IsChecked == true)
            {
                _markerColor = _curveColor;
                MarkerSwatchPicker.Color = _markerColor;
            }
        };
    }

    private void CurveSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (CurveSelector.SelectedItem is CurveEntry entry)
        {
            // Enable controls FIRST so UpdateMarkerEnablement / UpdateStrokeEnablement
            // (called from UpdateUIFromCurve) see ShowMarkersCheckBox.IsEnabled == true.
            SetEditingEnabled(true);
            UpdateUIFromCurve(entry.Curve);

            int idx = _entries.IndexOf(entry);
            IndexTag.Text = $"{idx + 1} of {_entries.Count}";
        }
        else
        {
            SetEditingEnabled(false);
            IndexTag.Text = "—";
        }
    }

    private void UpdateUIFromCurve(CurveConfigurationModel curve)
    {
        _suppressBindings = true;
        try
        {
            _curveColor = curve.Color;
            ColorSwatchPicker.Color = _curveColor;
            ColorPickerPill.Color = _curveColor;

            NameTextBox.Text = string.IsNullOrEmpty(curve.CurveName) ? curve.YColumnName : curve.CurveName;
            UnitTextBox.Text = curve.Unit ?? string.Empty;

            LineStylePicker.Value = curve.LineStyle;

            WidthSlider.Value = Math.Clamp(curve.LineWidth, 0.5, 5);
            WidthValue.Text = WidthSlider.Value.ToString("F1", CultureInfo.InvariantCulture) + " px";

            ShowLineCheckBox.IsChecked = curve.ShowLine;

            ShowMarkersCheckBox.IsChecked = curve.ShowMarkers;
            MarkerShapePicker.Value = curve.MarkerStyle == MarkerStyle.None
                ? MarkerStyle.Circle : curve.MarkerStyle;

            MarkerSizeSlider.Value = Math.Clamp(curve.MarkerSize, 3, 20);
            MarkerSizeValue.Text = MarkerSizeSlider.Value.ToString("F0", CultureInfo.InvariantCulture) + " px";

            _markerColor = string.IsNullOrEmpty(curve.MarkerColor) ? curve.Color : curve.MarkerColor;
            MarkerSwatchPicker.Color = _markerColor;
            MatchCurveColor.IsChecked = string.Equals(_markerColor, _curveColor, StringComparison.OrdinalIgnoreCase);

            UpdateStrokeEnablement();
            UpdateMarkerEnablement();
        }
        finally { _suppressBindings = false; }
    }

    private void SetEditingEnabled(bool enabled)
    {
        ApplyButton.IsEnabled = enabled;
        DeleteButton.IsEnabled = enabled;
        ColorSwatchPicker.IsEnabled = enabled;
        ColorPickerPill.IsEnabled = enabled;
        NameTextBox.IsEnabled = enabled;
        UnitTextBox.IsEnabled = enabled;
        LineStylePicker.IsEnabled = enabled;
        WidthSlider.IsEnabled = enabled;
        ShowLineCheckBox.IsEnabled = enabled;
        ShowMarkersCheckBox.IsEnabled = enabled;
        if (!enabled)
        {
            UpdateStrokeEnablement();
            UpdateMarkerEnablement();
        }
    }

    private void UpdateStrokeEnablement()
    {
        // Width slider only matters when a line is drawn. Don't disable the
        // line-style picker — it's still part of how a curve looks even with
        // markers-only selected (matches existing app behavior).
        bool show = ShowLineCheckBox.IsChecked == true && ShowLineCheckBox.IsEnabled;
        WidthSlider.Opacity = show ? 1.0 : 0.5;
    }

    private void UpdateMarkerEnablement()
    {
        bool show = ShowMarkersCheckBox.IsChecked == true && ShowMarkersCheckBox.IsEnabled;
        MarkerShapeRow.IsEnabled = show;
        MarkerSizeRow.IsEnabled = show;
        MarkerColorRow.IsEnabled = show;
        MarkerShapeRow.Opacity = show ? 1.0 : 0.5;
        MarkerSizeRow.Opacity = show ? 1.0 : 0.5;
        MarkerColorRow.Opacity = show ? 1.0 : 0.5;
    }

    private void OnSwatchColor()
    {
        if (_suppressBindings) return;
        _curveColor = ColorSwatchPicker.Color;
        ColorPickerPill.Color = _curveColor;
        if (MatchCurveColor.IsChecked == true)
        {
            _markerColor = _curveColor;
            _suppressBindings = true;
            try { MarkerSwatchPicker.Color = _markerColor; }
            finally { _suppressBindings = false; }
        }
    }

    private void OnMarkerSwatchColor()
    {
        if (_suppressBindings) return;
        _markerColor = MarkerSwatchPicker.Color;
        if (!string.Equals(_markerColor, _curveColor, StringComparison.OrdinalIgnoreCase))
            MatchCurveColor.IsChecked = false;
    }

    private void OpenCurveColorPicker() => SafeInvokeAsync(async () =>
    {
        var dlg = new ColorPickerDialog(_curveColor);
        if (await dlg.ShowDialog<bool?>(this) == true)
        {
            _curveColor = dlg.SelectedColor;
            _suppressBindings = true;
            try
            {
                ColorSwatchPicker.Color = _curveColor;
                ColorPickerPill.Color = _curveColor;
                if (MatchCurveColor.IsChecked == true)
                {
                    _markerColor = _curveColor;
                    MarkerSwatchPicker.Color = _markerColor;
                }
            }
            finally { _suppressBindings = false; }
        }
    });

    private void OpenMarkerColorPicker() => SafeInvokeAsync(async () =>
    {
        var dlg = new ColorPickerDialog(_markerColor);
        if (await dlg.ShowDialog<bool?>(this) == true)
        {
            _markerColor = dlg.SelectedColor;
            _suppressBindings = true;
            try
            {
                MarkerSwatchPicker.Color = _markerColor;
                MatchCurveColor.IsChecked = string.Equals(_markerColor, _curveColor, StringComparison.OrdinalIgnoreCase);
            }
            finally { _suppressBindings = false; }
        }
    });

    private async void SafeInvokeAsync(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FormatCurveDialog] {ex}"); }
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (CurveSelector.SelectedItem is not CurveEntry entry)
        {
            Close(false);
            return;
        }

        var curve = entry.Curve;
        curve.Color = _curveColor;

        // Rename: trim; blank falls back to the source column name so the curve never goes unlabeled.
        var name = NameTextBox.Text?.Trim();
        curve.CurveName = string.IsNullOrEmpty(name) ? curve.YColumnName : name;

        // Normalize the unit: trim, and treat blank as "unknown" (null).
        var unit = UnitTextBox.Text?.Trim();
        curve.Unit = string.IsNullOrEmpty(unit) ? null : unit;

        curve.LineStyle = LineStylePicker.Value;
        curve.LineWidth = Math.Round(WidthSlider.Value * 2) / 2;
        curve.ShowLine = ShowLineCheckBox.IsChecked == true;

        curve.ShowMarkers = ShowMarkersCheckBox.IsChecked == true;
        curve.MarkerStyle = MarkerShapePicker.Value == MarkerStyle.None
            ? MarkerStyle.Circle : MarkerShapePicker.Value;
        curve.MarkerSize = Math.Round(MarkerSizeSlider.Value);

        curve.MarkerColor = MatchCurveColor.IsChecked == true
            ? _curveColor
            : _markerColor;

        SelectedCurve = curve;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (CurveSelector.SelectedItem is not CurveEntry entry) return;
        SelectedCurve = entry.Curve;
        DeleteRequested = true;
        Close(true);
    }

    /// <summary>Adapter exposing <see cref="CurveConfigurationModel"/> with a brush for the picker dropdown.</summary>
    internal sealed class CurveEntry
    {
        public CurveConfigurationModel Curve { get; }
        public string DisplayName => string.IsNullOrEmpty(Curve.CurveName) ? Curve.YColumnName : Curve.CurveName;
        public IBrush ColorBrush => Avalonia.Media.Color.TryParse(Curve.Color, out var c)
            ? new SolidColorBrush(c)
            : Brushes.Gray;

        public CurveEntry(CurveConfigurationModel curve) { Curve = curve; }
    }
}
