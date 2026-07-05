using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Views.Controls.Dpx;
using System.Globalization;

namespace DatPlotX.Views;

/// <summary>
/// Modal pane-formatting dialog for the Compact Plot Surface. Tabs: Grid · X-Axis · Y-Axis · Background.
/// Returns an updated <see cref="CompactPaneSettings"/> via <see cref="Result"/> when OK is clicked.
/// </summary>
public partial class FormatCompactPaneDialog : Window
{
    public CompactPaneSettings? Result { get; private set; }

    private readonly CompactPaneSettings _working;
    private string _bgColor = "#FFFFFF";
    private readonly List<Border> _bgPresetButtons = new();

    private static readonly (string Id, string Color, string Label)[] BgPresets =
    {
        ("white",       "#FFFFFF", "White"),
        ("paper",       "#FAFAF7", "Paper"),
        ("gray",        "#F0F0F0", "Gray"),
        ("dark",        "#1A1A1D", "Dark"),
        ("transparent", "transparent", "None"),
    };

    public FormatCompactPaneDialog() : this(new CompactPaneSettings()) { }

    public FormatCompactPaneDialog(CompactPaneSettings current)
    {
        InitializeComponent();
        _working = Clone(current);

        BuildMinorStyleChips();
        BuildBackgroundPresets();
        WireEvents();
        LoadIntoUi();
        UpdateRangeRowEnablement();
        UpdateTickPreview();
    }

    private void BuildMinorStyleChips()
    {
        MinorStyleChips.Options = new List<ChipOption>
        {
            new("Dash", "Dash"),
            new("Dot", "Dot"),
            new("DashDot", "Dash·Dot"),
        };
    }

    private void BuildBackgroundPresets()
    {
        _bgPresetButtons.Clear();
        BgPresetHost.Children.Clear();
        foreach (var (id, color, label) in BgPresets)
        {
            var fill = color == "transparent"
                ? (IBrush)new SolidColorBrush(Color.Parse("#F0F0F0"))
                : new SolidColorBrush(Color.Parse(color));
            var stamp = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = fill,
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(2.5),
                Tag = color,
                Padding = new Thickness(4),
                MinHeight = 30,
            };
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 9.5,
                FontWeight = FontWeight.Medium,
                Foreground = id == "dark" ? Brushes.White : new SolidColorBrush(Color.Parse("#52525B")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            stamp.Child = lbl;
            stamp.PointerPressed += (_, _) => SetBackgroundColor(color);
            _bgPresetButtons.Add(stamp);
            BgPresetHost.Children.Add(stamp);
        }
    }

    private void WireEvents()
    {
        OkButton.Click += OK_Click;
        CancelButton.Click += (_, _) => { Result = null; Close(false); };
        this.EnableEscapeToClose(false);
        XAutoScaleCheckBox.IsCheckedChanged += (_, _) => UpdateRangeRowEnablement();
        DecimalsStepper.PropertyChanged += (_, e) =>
        {
            if (e.Property == NumberStepper.ValueProperty) UpdateTickPreview();
        };
        BgColorPill.PickRequested += (_, _) => SafeInvokeAsync(async () =>
        {
            var dlg = new ColorPickerDialog(string.Equals(_bgColor, "transparent", StringComparison.OrdinalIgnoreCase) ? "#FFFFFF" : _bgColor);
            if (await dlg.ShowDialog<bool?>(this) == true)
                SetBackgroundColor(dlg.SelectedColor);
        });
        ShowMinorGridCheckBox.IsCheckedChanged += (_, _) => UpdateMinorRowEnablement();
    }

    private void LoadIntoUi()
    {
        ShowMajorGridCheckBox.IsChecked = _working.ShowMajorGridlines;
        ShowMinorGridCheckBox.IsChecked = _working.ShowMinorGridlines;
        MinorStyleChips.Value = _working.MinorGridlineStyle.ToString();
        UpdateMinorRowEnablement();

        XLabelTextBox.Text = _working.XAxisLabelOverride ?? string.Empty;
        XLabelFontSizeStepper.Value = Math.Clamp(_working.XAxisLabelFontSize, 6, 48);
        XLabelBoldCheckBox.IsChecked = _working.XAxisLabelBold;
        DecimalsStepper.Value = Math.Clamp(_working.XAxisDecimalPlaces, 0, 6);
        XAutoScaleCheckBox.IsChecked = _working.XAxisAutoScale;
        RangeBox.Min.Text = _working.XAxisMin?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        RangeBox.Max.Text = _working.XAxisMax?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        SetBackgroundColor(_working.BackgroundColor);
    }

    private void UpdateRangeRowEnablement()
    {
        bool auto = XAutoScaleCheckBox.IsChecked == true;
        MinMaxRow.IsEnabled = !auto;
        MinMaxRow.Opacity = auto ? 0.5 : 1.0;
    }

    private void UpdateMinorRowEnablement()
    {
        bool show = ShowMinorGridCheckBox.IsChecked == true;
        MinorStyleRow.IsEnabled = show;
        MinorStyleRow.Opacity = show ? 1.0 : 0.5;
    }

    private void UpdateTickPreview()
    {
        TickPreviewHost.Children.Clear();
        var ticks = new[] { 0.0, 30.0, 60.0, 90.0, 120.0 };
        int decimals = (int)Math.Round(DecimalsStepper.Value);
        for (int i = 0; i < ticks.Length; i++)
        {
            var col = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 2,
            };
            col.Children.Add(new Rectangle { Width = 1, Height = 4, Fill = (IBrush)Application.Current!.FindResource("DpxText3")! });
            col.Children.Add(new TextBlock
            {
                Text = ticks[i].ToString("F" + decimals, CultureInfo.InvariantCulture),
                FontFamily = (FontFamily)Application.Current!.FindResource("DpxMonoFamily")!,
                FontSize = 10.5,
                Foreground = (IBrush)Application.Current!.FindResource("DpxText2")!,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            Grid.SetColumn(col, i);
            TickPreviewHost.Children.Add(col);
        }
    }

    private void SetBackgroundColor(string color)
    {
        _bgColor = color;
        if (string.Equals(color, "transparent", StringComparison.OrdinalIgnoreCase))
        {
            BgPreviewFrame.Background = new SolidColorBrush(Color.Parse("#FFFFFF"));
            BgColorPill.Color = "transparent";
        }
        else if (Color.TryParse(color, out var c))
        {
            BgPreviewFrame.Background = new SolidColorBrush(c);
            BgColorPill.Color = color;
        }

        foreach (var b in _bgPresetButtons)
        {
            bool selected = string.Equals(b.Tag as string, color, StringComparison.OrdinalIgnoreCase);
            b.BorderBrush = selected
                ? (IBrush)Application.Current!.FindResource("DpxText")!
                : new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0));
            b.BorderThickness = new Thickness(selected ? 2 : 1);
        }
    }

    private async void SafeInvokeAsync(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FormatCompactPaneDialog] {ex}"); }
    }

    private void OK_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _working.ShowMajorGridlines = ShowMajorGridCheckBox.IsChecked == true;
        _working.ShowMinorGridlines = ShowMinorGridCheckBox.IsChecked == true;
        if (Enum.TryParse<CompactGridLineStyle>(MinorStyleChips.Value, out var style))
            _working.MinorGridlineStyle = style;

        var label = XLabelTextBox.Text?.Trim();
        _working.XAxisLabelOverride = string.IsNullOrEmpty(label) ? null : label;
        _working.XAxisLabelFontSize = XLabelFontSizeStepper.Value;
        _working.XAxisLabelBold = XLabelBoldCheckBox.IsChecked == true;
        _working.XAxisDecimalPlaces = (int)Math.Round(DecimalsStepper.Value);
        _working.XAxisAutoScale = XAutoScaleCheckBox.IsChecked == true;
        _working.XAxisMin = TryParseDouble(RangeBox.Min.Text);
        _working.XAxisMax = TryParseDouble(RangeBox.Max.Text);

        _working.BackgroundColor = _bgColor;

        Result = _working;
        Close(true);
    }

    private static double? TryParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static CompactPaneSettings Clone(CompactPaneSettings s) => new()
    {
        ShowMajorGridlines = s.ShowMajorGridlines,
        ShowMinorGridlines = s.ShowMinorGridlines,
        MinorGridlineStyle = s.MinorGridlineStyle,
        BackgroundColor = s.BackgroundColor,
        XAxisLabelOverride = s.XAxisLabelOverride,
        XAxisDecimalPlaces = s.XAxisDecimalPlaces,
        XAxisAutoScale = s.XAxisAutoScale,
        XAxisMin = s.XAxisMin,
        XAxisMax = s.XAxisMax,
        XAxisLabelFontSize = s.XAxisLabelFontSize,
        XAxisLabelBold = s.XAxisLabelBold,
    };
}
