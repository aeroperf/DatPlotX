using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Views.Controls.Dpx;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for configuring arrow annotations with color, width, arrowhead style, and label options.
/// </summary>
public partial class ArrowAnnotationDialog : Window
{
    public ArrowAnnotationModel Result { get; private set; }

    private string _arrowColor = "#333333";
    private string _labelColor = "#000000";

    public ArrowAnnotationDialog(ArrowAnnotationModel? existingModel = null)
    {
        InitializeComponent();

        BuildChipOptions();
        WireEvents();

        if (existingModel != null)
        {
            Result = existingModel.Clone();
            LoadFromModel(existingModel);
        }
        else
        {
            Result = new ArrowAnnotationModel
            {
                Color = "#333333",
                LineWidth = 1,
                ArrowheadWidth = 10,
                ArrowheadLength = 15,
                ArrowheadStyle = ArrowheadStyle.Filled,
                ArrowEnds = ArrowEnds.End,
                LabelFontSize = 18,
                LabelFontColor = "#000000",
                LabelPosition = ArrowLabelPosition.Middle,
                LabelAlignment = ArrowLabelAlignment.Above,
                LabelRotateWithArrow = true,
            };
            LoadFromModel(Result);
        }

        UpdatePreview();
    }

    public ArrowAnnotationDialog() : this(null) { }

    private void BuildChipOptions()
    {
        ArrowheadStyleChips.Options = new List<ChipOption>
        {
            new("None",   "None"),
            new("Open",   "Open"),
            new("Filled", "Filled"),
        };
        ArrowEndsChips.Options = new List<ChipOption>
        {
            new("Start", "Start"),
            new("End",   "End"),
            new("Both",  "Both"),
        };
        LabelPositionChips.Options = new List<ChipOption>
        {
            new("Base",   "Start"),
            new("Middle", "Middle"),
            new("Tip",    "End"),
        };
        LabelAlignChips.Options = new List<ChipOption>
        {
            new("Above",        "Above"),
            new("Below",        "Below"),
            new("InlineAtBase", "Inline @ start"),
            new("InlineAtTip",  "Inline @ end"),
        };
    }

    private void WireEvents()
    {
        OkButton.Click += OK_Click;
        CancelButton.Click += (_, _) => Close(false);
        this.EnableEscapeToClose(false);

        ArrowColorPill.PickRequested += (_, _) => PickArrowColor();
        LabelColorPill.PickRequested += (_, _) => PickLabelColor();

        LineWidthStepper.PropertyChanged += (_, e) => { if (e.Property == NumberStepper.ValueProperty) UpdatePreview(); };
        ArrowheadSizeStepper.PropertyChanged += (_, e) => { if (e.Property == NumberStepper.ValueProperty) UpdatePreview(); };
        LabelFontSizeStepper.PropertyChanged += (_, e) => { if (e.Property == NumberStepper.ValueProperty) UpdatePreview(); };
        ArrowheadStyleChips.PropertyChanged += (_, e) => { if (e.Property == ChipGroup.ValueProperty) UpdatePreview(); };
        ArrowEndsChips.PropertyChanged += (_, e) => { if (e.Property == ChipGroup.ValueProperty) UpdatePreview(); };
        LabelPositionChips.PropertyChanged += (_, e) => { if (e.Property == ChipGroup.ValueProperty) UpdatePreview(); };
        LabelAlignChips.PropertyChanged += (_, e) => { if (e.Property == ChipGroup.ValueProperty) UpdatePreview(); };
        LabelTextBox.TextChanged += (_, _) => UpdatePreview();
        LabelRotateCheckBox.IsCheckedChanged += (_, _) => UpdatePreview();
    }

    private void LoadFromModel(ArrowAnnotationModel m)
    {
        _arrowColor = m.Color;
        _labelColor = m.LabelFontColor;
        ArrowColorPill.Color = _arrowColor;
        LabelColorPill.Color = _labelColor;

        // Set font size BEFORE other steppers — early WireEvents-driven UpdatePreview reads
        // LabelFontSizeStepper.Value, and the default 0 makes Avalonia reject FontSize.
        LabelFontSizeStepper.Value = m.LabelFontSize;
        LineWidthStepper.Value = m.LineWidth;
        ArrowheadSizeStepper.Value = m.ArrowheadWidth;
        ArrowheadStyleChips.Value = m.ArrowheadStyle.ToString();
        ArrowEndsChips.Value = m.ArrowEnds.ToString();
        LabelTextBox.Text = m.Label ?? string.Empty;
        LabelPositionChips.Value = m.LabelPosition.ToString();
        LabelAlignChips.Value = m.LabelAlignment.ToString();
        LabelRotateCheckBox.IsChecked = m.LabelRotateWithArrow;
    }

    // Preview canvas constants — arrow lives at fixed pixel coords on the 100h canvas.
    private const double PvBaseX = 30, PvBaseY = 80, PvTipX = 370, PvTipY = 30;
    private const double PvLabelOffsetPx = 14;

    private void UpdatePreview()
    {
        if (PreviewArrowLine == null) return;
        if (Color.TryParse(_arrowColor, out var c))
        {
            var brush = new SolidColorBrush(c);
            PreviewArrowLine.Stroke = brush;
            PreviewArrowHead.Fill = brush;
            PreviewArrowHeadBase.Fill = brush;
        }
        PreviewArrowLine.StrokeThickness = LineWidthStepper.Value;

        // Arrowhead visibility: respect ArrowEnds + Style (None hides both).
        bool styleOff = string.Equals(ArrowheadStyleChips.Value, "None", StringComparison.OrdinalIgnoreCase);
        bool showTip = !styleOff && ArrowEndsChips.Value is "End" or "Both";
        bool showBase = !styleOff && ArrowEndsChips.Value is "Start" or "Both";
        PreviewArrowHead.IsVisible = showTip;
        PreviewArrowHeadBase.IsVisible = showBase;

        var label = LabelTextBox.Text ?? string.Empty;
        PreviewLabel.IsVisible = !string.IsNullOrEmpty(label);
        PreviewLabel.Text = label;
        // Avalonia rejects FontSize <= 0, which fires when other steppers nudge UpdatePreview before
        // LabelFontSizeStepper has been seeded.
        PreviewLabel.FontSize = Math.Max(1, LabelFontSizeStepper.Value);
        if (Color.TryParse(_labelColor, out var lc))
            PreviewLabel.Foreground = new SolidColorBrush(lc);

        if (PreviewLabel.IsVisible)
            PositionPreviewLabel();
    }

    /// <summary>
    /// Project the label onto the preview canvas using the same pixel-space geometry the runtime
    /// renderer uses (perpendicular offset for Above/Below, axial offset for InlineAt*).
    /// </summary>
    private void PositionPreviewLabel()
    {
        double dx = PvTipX - PvBaseX;
        double dy = PvTipY - PvBaseY;
        double angle = Math.Atan2(dy, dx);
        double angleDeg = angle * 180 / Math.PI;
        double perp = angle + Math.PI / 2;

        double anchorX, anchorY;
        switch (LabelPositionChips.Value)
        {
            case "Base": anchorX = PvBaseX; anchorY = PvBaseY; break;
            case "Tip": anchorX = PvTipX; anchorY = PvTipY; break;
            default: anchorX = (PvBaseX + PvTipX) / 2; anchorY = (PvBaseY + PvTipY) / 2; break;
        }

        // Avalonia Canvas Y increases DOWNWARDS — for the user "Above" means visually higher on
        // screen (smaller Canvas Y). The runtime renderers use the same screen-Y convention but
        // their convention is "+sin(perp) = Above" because their target coord systems treat
        // ±perpendicular as left/right of arrow direction; here we want a mirror-flipped sign
        // so the preview agrees with what the user sees in the plot.
        double labelX, labelY;
        switch (LabelAlignChips.Value)
        {
            case "Below":
                labelX = anchorX + Math.Cos(perp) * PvLabelOffsetPx;
                labelY = anchorY + Math.Sin(perp) * PvLabelOffsetPx;
                break;
            case "InlineAtBase":
                labelX = PvBaseX - Math.Cos(angle) * PvLabelOffsetPx * 2;
                labelY = PvBaseY - Math.Sin(angle) * PvLabelOffsetPx * 2;
                break;
            case "InlineAtTip":
                labelX = PvTipX + Math.Cos(angle) * PvLabelOffsetPx * 2;
                labelY = PvTipY + Math.Sin(angle) * PvLabelOffsetPx * 2;
                break;
            default: // Above — visually up = smaller Canvas Y
                labelX = anchorX - Math.Cos(perp) * PvLabelOffsetPx;
                labelY = anchorY - Math.Sin(perp) * PvLabelOffsetPx;
                break;
        }

        // Apply rotation if requested — flip when arrow points left so text stays readable.
        bool rotate = LabelRotateCheckBox.IsChecked == true;
        double rotDeg = 0;
        if (rotate)
        {
            rotDeg = angleDeg;
            if (rotDeg > 90 || rotDeg < -90) rotDeg += 180;
        }

        // Center the text on (labelX, labelY) — measure text after layout pass settles.
        PreviewLabel.RenderTransform = new RotateTransform(rotDeg);
        // Force layout so Bounds reflect the latest font size before positioning.
        PreviewLabel.Measure(new Avalonia.Size(double.PositiveInfinity, double.PositiveInfinity));
        double w = PreviewLabel.DesiredSize.Width;
        double h = PreviewLabel.DesiredSize.Height;
        Canvas.SetLeft(PreviewLabel, labelX - w / 2);
        Canvas.SetTop(PreviewLabel, labelY - h / 2);
        PreviewLabel.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
    }

    private void PickArrowColor() => SafeInvokeAsync(async () =>
    {
        var dlg = new ColorPickerDialog(_arrowColor);
        if (await dlg.ShowDialog<bool?>(this) == true)
        {
            _arrowColor = dlg.SelectedColor;
            ArrowColorPill.Color = _arrowColor;
            UpdatePreview();
        }
    });

    private void PickLabelColor() => SafeInvokeAsync(async () =>
    {
        var dlg = new ColorPickerDialog(_labelColor);
        if (await dlg.ShowDialog<bool?>(this) == true)
        {
            _labelColor = dlg.SelectedColor;
            LabelColorPill.Color = _labelColor;
            UpdatePreview();
        }
    });

    private async void SafeInvokeAsync(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ArrowAnnotationDialog] {ex}"); }
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        Result.Color = _arrowColor;
        Result.LineWidth = LineWidthStepper.Value;

        if (Enum.TryParse<ArrowheadStyle>(ArrowheadStyleChips.Value, out var hs)) Result.ArrowheadStyle = hs;
        Result.ArrowheadWidth = ArrowheadSizeStepper.Value;
        Result.ArrowheadLength = ArrowheadSizeStepper.Value * 1.5;
        if (Enum.TryParse<ArrowEnds>(ArrowEndsChips.Value, out var ae)) Result.ArrowEnds = ae;

        Result.Label = string.IsNullOrWhiteSpace(LabelTextBox.Text) ? null : LabelTextBox.Text;
        if (Enum.TryParse<ArrowLabelPosition>(LabelPositionChips.Value, out var lp)) Result.LabelPosition = lp;
        if (Enum.TryParse<ArrowLabelAlignment>(LabelAlignChips.Value, out var la)) Result.LabelAlignment = la;
        Result.LabelFontSize = LabelFontSizeStepper.Value;
        Result.LabelFontColor = _labelColor;
        Result.LabelRotateWithArrow = LabelRotateCheckBox.IsChecked == true;

        Close(true);
    }
}
