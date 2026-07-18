using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DatPlotX.Views;

/// <summary>
/// Compact-surface analog of <see cref="FormatCurveDialog"/>: pick one curve from the surface,
/// edit its color / line style / marker style + color / visibility, or delete it. Mutates the
/// supplied <see cref="CompactCurveModel"/> in-place on Apply; <see cref="DeleteRequested"/>
/// signals the caller to remove the curve via <c>CompactPlotViewModel.RemoveCurve</c>.
/// </summary>
public partial class ManageCompactCurveDialog : Window
{
    private readonly ObservableCollection<CompactCurveModel> _curves;
    private readonly List<CurveEntry> _entries = new();

    private string _curveColor = "#0000FF";
    private string _markerColor = "#0000FF";
    private bool _suppressBindings;

    public CompactCurveModel? SelectedCurve { get; private set; }
    public bool DeleteRequested { get; private set; }

    public ManageCompactCurveDialog(ObservableCollection<CompactCurveModel> curves, Guid? preselectId = null)
    {
        InitializeComponent();
        _curves = curves;

        if (curves.Count == 0)
        {
            Close(false);
            return;
        }

        BuildEntries();
        WireEvents();

        if (preselectId.HasValue)
            CurveSelector.SelectedItem = _entries.FirstOrDefault(e => e.Curve.Id == preselectId.Value);
        else
            CurveSelector.SelectedIndex = -1;

        SetEditingEnabled(false);
        this.EnableEscapeToClose(false);
    }

    public ManageCompactCurveDialog()
    {
        InitializeComponent();
        _curves = new ObservableCollection<CompactCurveModel>();
        BuildEntries();
        WireEvents();
        SetEditingEnabled(false);
    }

    private void BuildEntries()
    {
        _entries.Clear();
        foreach (var curve in _curves)
            _entries.Add(new CurveEntry(curve));
        CurveSelector.ItemsSource = _entries;
    }

    private void WireEvents()
    {
        CurveSelector.SelectionChanged += CurveSelector_SelectionChanged;
        DeleteButton.Click += Delete_Click;
        CancelButton.Click += (_, _) => Close(false);
        ApplyButton.Click += Apply_Click;

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

        MarkerSizeSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
                MarkerSizeValue.Text = MarkerSizeSlider.Value.ToString("F0", CultureInfo.InvariantCulture) + " px";
        };

        ShowMarkersCheckBox.IsCheckedChanged += (_, _) => UpdateMarkerEnablement();

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
            // Enable controls FIRST so UpdateMarkerEnablement (called from UpdateUIFromCurve)
            // sees ShowMarkersCheckBox.IsEnabled == true.
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

    private void UpdateUIFromCurve(CompactCurveModel curve)
    {
        _suppressBindings = true;
        try
        {
            NameTextBox.Text = string.IsNullOrEmpty(curve.DisplayName) ? curve.SourceColumn : curve.DisplayName;
            UnitTextBox.Text = curve.Unit ?? string.Empty;

            _curveColor = curve.Color;
            ColorSwatchPicker.Color = _curveColor;
            ColorPickerPill.Color = _curveColor;

            LineStylePicker.Value = curve.LineStyle;

            WidthSlider.Value = Math.Clamp(curve.LineWidth, 0.5, 4);
            WidthValue.Text = WidthSlider.Value.ToString("F1", CultureInfo.InvariantCulture) + " px";

            VisibleCheckBox.IsChecked = curve.IsVisible;

            bool markersOn = curve.MarkerStyle != MarkerStyle.None;
            ShowMarkersCheckBox.IsChecked = markersOn;
            MarkerShapePicker.Value = markersOn ? curve.MarkerStyle : MarkerStyle.Circle;

            MarkerSizeSlider.Value = Math.Clamp(curve.MarkerSize, 3, 20);
            MarkerSizeValue.Text = MarkerSizeSlider.Value.ToString("F0", CultureInfo.InvariantCulture) + " px";

            _markerColor = curve.MarkerColor ?? curve.Color;
            MarkerSwatchPicker.Color = _markerColor;
            MatchCurveColor.IsChecked = curve.MarkerColor is null;

            YFontSizeStepper.Value = Math.Clamp(curve.YAxisLabelFontSize, 6, 48);
            YBoldCheckBox.IsChecked = curve.YAxisLabelBold;
            YDecimalsStepper.Value = Math.Clamp(curve.YAxisDecimalPlaces, 0, 6);

            UpdateMarkerEnablement();
        }
        finally { _suppressBindings = false; }
    }

    private void SetEditingEnabled(bool enabled)
    {
        ApplyButton.IsEnabled = enabled;
        DeleteButton.IsEnabled = enabled;
        NameTextBox.IsEnabled = enabled;
        UnitTextBox.IsEnabled = enabled;
        ColorSwatchPicker.IsEnabled = enabled;
        ColorPickerPill.IsEnabled = enabled;
        LineStylePicker.IsEnabled = enabled;
        WidthSlider.IsEnabled = enabled;
        VisibleCheckBox.IsEnabled = enabled;
        ShowMarkersCheckBox.IsEnabled = enabled;
        YFontSizeStepper.IsEnabled = enabled;
        YBoldCheckBox.IsEnabled = enabled;
        YDecimalsStepper.IsEnabled = enabled;
        if (!enabled)
            UpdateMarkerEnablement();
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
        if (!ColorsEqual(_markerColor, _curveColor))
            MatchCurveColor.IsChecked = false;
    }

    /// <summary>
    /// Compare two hex color strings by their parsed ARGB value, not by text. Otherwise visually
    /// identical colors in different spellings (e.g. "#FF0000" vs "#FFFF0000") compare unequal and
    /// a spurious explicit MarkerColor override is stored, so the marker stops tracking the curve.
    /// </summary>
    private static bool ColorsEqual(string? a, string? b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (Color.TryParse(a, out var ca) && Color.TryParse(b, out var cb)) return ca == cb;
        return false;
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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ManageCompactCurveDialog] {ex}"); }
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (CurveSelector.SelectedItem is not CurveEntry entry)
        {
            Close(false);
            return;
        }

        var curve = entry.Curve;

        // Rename: trim; blank falls back to the source column so the curve never goes unlabeled.
        var name = NameTextBox.Text?.Trim();
        curve.DisplayName = string.IsNullOrEmpty(name) ? curve.SourceColumn : name;

        // Unit: trim, treat blank as "none" (null) so the axis title drops the parens.
        var unit = UnitTextBox.Text?.Trim();
        curve.Unit = string.IsNullOrEmpty(unit) ? null : unit;

        curve.Color = _curveColor;
        curve.LineStyle = LineStylePicker.Value;
        curve.LineWidth = Math.Round(WidthSlider.Value * 2) / 2;
        curve.IsVisible = VisibleCheckBox.IsChecked == true;

        bool showMarkers = ShowMarkersCheckBox.IsChecked == true;
        curve.MarkerStyle = showMarkers
            ? (MarkerShapePicker.Value == MarkerStyle.None ? MarkerStyle.Circle : MarkerShapePicker.Value)
            : MarkerStyle.None;
        curve.MarkerSize = Math.Round(MarkerSizeSlider.Value);

        curve.MarkerColor = MatchCurveColor.IsChecked == true || ColorsEqual(_markerColor, _curveColor)
            ? null
            : _markerColor;

        curve.YAxisLabelFontSize = YFontSizeStepper.Value;
        curve.YAxisLabelBold = YBoldCheckBox.IsChecked == true;
        curve.YAxisDecimalPlaces = (int)Math.Round(YDecimalsStepper.Value);

        SelectedCurve = curve;
        Close(true);
    }

    private void Delete_Click(object? sender, RoutedEventArgs e)
    {
        if (CurveSelector.SelectedItem is not CurveEntry entry) return;
        SelectedCurve = entry.Curve;
        DeleteRequested = true;
        Close(true);
    }

    /// <summary>Adapter exposing <see cref="CompactCurveModel"/> with a brush for the picker dropdown.</summary>
    internal sealed class CurveEntry
    {
        public CompactCurveModel Curve { get; }
        public string DisplayName => Curve.DisplayName;
        public IBrush ColorBrush => Avalonia.Media.Color.TryParse(Curve.Color, out var c)
            ? new SolidColorBrush(c)
            : Brushes.Gray;

        public CurveEntry(CompactCurveModel curve) { Curve = curve; }
    }
}
