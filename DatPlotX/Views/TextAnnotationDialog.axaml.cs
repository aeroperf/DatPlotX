using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Views.Controls.Dpx;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for configuring text annotations: text, font, colors (with opacity), border, anchor, rotation.
/// </summary>
public partial class TextAnnotationDialog : Window
{
    public TextAnnotationModel Result { get; private set; }

    private string _fontColor = "#000000";
    private string _backgroundColor = "#FFFFFF";
    private string _borderColor = "#999999";

    public TextAnnotationDialog(TextAnnotationModel? existingModel = null)
    {
        InitializeComponent();

        BuildChips();
        WireEvents();

        if (existingModel != null)
        {
            Result = existingModel.Clone();
            LoadFromModel(existingModel);
        }
        else
        {
            Result = new TextAnnotationModel
            {
                Text = "Annotation",
                FontSize = 14,
                FontColor = "#000000",
                BackgroundColor = "#FFFFFF",
                BackgroundOpacity = 0.9,
                BorderColor = "#999999",
                BorderWidth = 1,
                Alignment = TextAnnotationAlignment.MiddleCenter,
                Rotation = 0,
            };
            TextContentBox.Text = "Annotation";
            FontSizeStepper.Value = 14;
            BorderWidthStepper.Value = 1;
            OpacitySlider.Value = 90;
            FontColorPill.Color = _fontColor;
            BackgroundColorPill.Color = _backgroundColor;
            BorderColorPill.Color = _borderColor;
            AlignChips.Value = "Left";
        }

        UpdatePreview();
    }

    public TextAnnotationDialog() : this(null) { }

    private void BuildChips()
    {
        StyleChips.Options = new List<ChipOption>
        {
            new("Bold", "B"),
            new("Italic", "I"),
        };
        AlignChips.Options = new List<ChipOption>
        {
            new("Left", "L"),
            new("Center", "C"),
            new("Right", "R"),
        };
    }

    private void WireEvents()
    {
        OkButton.Click += OK_Click;
        CancelButton.Click += (_, _) => Close(false);
        this.EnableEscapeToClose(false);

        FontColorPill.PickRequested += (_, _) => PickColor(_fontColor, c => { _fontColor = c; FontColorPill.Color = c; UpdatePreview(); });
        BackgroundColorPill.PickRequested += (_, _) => PickColor(_backgroundColor, c => { _backgroundColor = c; BackgroundColorPill.Color = c; UpdatePreview(); });
        BorderColorPill.PickRequested += (_, _) => PickColor(_borderColor, c => { _borderColor = c; BorderColorPill.Color = c; UpdatePreview(); });

        OpacitySlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                OpacityText.Text = $"{(int)OpacitySlider.Value}%";
                UpdatePreview();
            }
        };
        RotationSlider.PropertyChanged += (_, e) =>
        {
            if (e.Property == Slider.ValueProperty)
            {
                RotationText.Text = $"{(int)RotationSlider.Value}°";
                UpdatePreview();
            }
        };

        FontSizeStepper.PropertyChanged += (_, e) => { if (e.Property == NumberStepper.ValueProperty) UpdatePreview(); };
        BorderWidthStepper.PropertyChanged += (_, e) => { if (e.Property == NumberStepper.ValueProperty) UpdatePreview(); };
        StyleChips.PropertyChanged += (_, e) => { if (e.Property == ChipGroup.ValueProperty) UpdatePreview(); };
        AlignChips.PropertyChanged += (_, e) => { if (e.Property == ChipGroup.ValueProperty) UpdatePreview(); };
        TextContentBox.TextChanged += (_, _) => UpdatePreview();
    }

    private void LoadFromModel(TextAnnotationModel m)
    {
        TextContentBox.Text = m.Text;
        FontSizeStepper.Value = m.FontSize;
        BorderWidthStepper.Value = m.BorderWidth;
        OpacitySlider.Value = m.BackgroundOpacity * 100;
        RotationSlider.Value = m.Rotation;

        // No multi-toggle support; pick single
        StyleChips.Value = m.IsBold ? "Bold" : (m.IsItalic ? "Italic" : null);
        AlignChips.Value = m.TextAlignment switch
        {
            TextHorizontalAlignment.Center => "Center",
            TextHorizontalAlignment.Right => "Right",
            _ => "Left",
        };

        _fontColor = m.FontColor;
        _backgroundColor = m.BackgroundColor;
        _borderColor = m.BorderColor;
        FontColorPill.Color = _fontColor;
        BackgroundColorPill.Color = _backgroundColor;
        BorderColorPill.Color = _borderColor;

        OpacityText.Text = $"{(int)OpacitySlider.Value}%";
        RotationText.Text = $"{(int)RotationSlider.Value}°";
    }

    private void UpdatePreview()
    {
        if (PreviewText == null || PreviewBorder == null) return;

        PreviewText.Text = string.IsNullOrEmpty(TextContentBox.Text) ? "Sample" : TextContentBox.Text;
        // Avalonia rejects FontSize <= 0; UpdatePreview can fire from TextChanged before
        // FontSizeStepper has been seeded.
        PreviewText.FontSize = Math.Max(1, FontSizeStepper.Value);
        PreviewText.FontWeight = StyleChips.Value == "Bold" ? FontWeight.Bold : FontWeight.Normal;
        PreviewText.FontStyle = StyleChips.Value == "Italic" ? FontStyle.Italic : FontStyle.Normal;
        PreviewText.TextAlignment = AlignChips.Value switch
        {
            "Center" => Avalonia.Media.TextAlignment.Center,
            "Right" => Avalonia.Media.TextAlignment.Right,
            _ => Avalonia.Media.TextAlignment.Left,
        };

        if (Color.TryParse(_fontColor, out var fc))
            PreviewText.Foreground = new SolidColorBrush(fc);

        if (Color.TryParse(_backgroundColor, out var bg))
        {
            byte alpha = (byte)Math.Clamp(OpacitySlider.Value * 2.55, 0, 255);
            PreviewBorder.Background = new SolidColorBrush(Color.FromArgb(alpha, bg.R, bg.G, bg.B));
        }
        if (Color.TryParse(_borderColor, out var bc))
            PreviewBorder.BorderBrush = new SolidColorBrush(bc);
        PreviewBorder.BorderThickness = new Thickness(BorderWidthStepper.Value);

        PreviewBorder.RenderTransform = new RotateTransform(RotationSlider.Value);
        PreviewBorder.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
    }

    private void PickColor(string current, Action<string> apply) => SafeInvokeAsync(async () =>
    {
        var dlg = new ColorPickerDialog(current);
        if (await dlg.ShowDialog<bool?>(this) == true)
            apply(dlg.SelectedColor);
    });

    private async void SafeInvokeAsync(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TextAnnotationDialog] {ex}"); }
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TextContentBox.Text)) return;

        Result.Text = TextContentBox.Text;
        Result.FontSize = FontSizeStepper.Value;
        Result.IsBold = StyleChips.Value == "Bold";
        Result.IsItalic = StyleChips.Value == "Italic";
        Result.FontColor = _fontColor;
        Result.BackgroundColor = _backgroundColor;
        Result.BackgroundOpacity = OpacitySlider.Value / 100.0;
        Result.BorderColor = _borderColor;
        Result.BorderWidth = BorderWidthStepper.Value;

        // Box-anchor alignment is locked to MiddleCenter so drag positions stay coherent (the
        // (X, Y) is the visual center of the rendered box regardless of font/text length).
        // Text content alignment (left/center/right) is independent and user-chosen.
        Result.Alignment = TextAnnotationAlignment.MiddleCenter;
        Result.TextAlignment = AlignChips.Value switch
        {
            "Center" => TextHorizontalAlignment.Center,
            "Right" => TextHorizontalAlignment.Right,
            _ => TextHorizontalAlignment.Left,
        };
        Result.Rotation = RotationSlider.Value;

        Close(true);
    }
}
