using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace DatPlotX.Views.Controls.Dpx;

public partial class ColorSwatches : UserControl
{
    private static readonly string[] DefaultPalette =
    {
        "#E91E63", "#F59E0B", "#10B981", "#06B6D4",
        "#3B82F6", "#7C3AED", "#EF4444",
    };

    public static readonly StyledProperty<string> ColorProperty =
        AvaloniaProperty.Register<ColorSwatches, string>(nameof(Color), DefaultPalette[0],
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IReadOnlyList<string>> PresetsProperty =
        AvaloniaProperty.Register<ColorSwatches, IReadOnlyList<string>>(nameof(Presets),
            DefaultPalette);

    public string Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public IReadOnlyList<string> Presets
    {
        get => GetValue(PresetsProperty);
        set => SetValue(PresetsProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? CustomPickRequested;

    private readonly List<Button> _swatchButtons = new();
    private Button? _customSwatchButton;

    public ColorSwatches()
    {
        InitializeComponent();
        Rebuild();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PresetsProperty)
            Rebuild();
        else if (change.Property == ColorProperty)
            UpdateSelection();
    }

    private void Rebuild()
    {
        foreach (var old in _swatchButtons) old.Click -= OnPresetClick;
        _swatchButtons.Clear();
        if (_customSwatchButton is not null)
        {
            _customSwatchButton.Click -= OnCustomClick;
            _customSwatchButton = null;
        }

        SwatchHost.Children.Clear();
        var presets = Presets ?? DefaultPalette;
        foreach (var hex in presets)
        {
            var btn = MakeSwatch(hex);
            _swatchButtons.Add(btn);
            SwatchHost.Children.Add(btn);
        }
        _customSwatchButton = MakeCustomSwatch();
        SwatchHost.Children.Add(_customSwatchButton);
        UpdateSelection();
    }

    private void OnPresetClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
            Color = hex;
    }

    private void OnCustomClick(object? sender, RoutedEventArgs e)
        => CustomPickRequested?.Invoke(this, e);

    private Button MakeSwatch(string hex)
    {
        IBrush fill = Avalonia.Media.Color.TryParse(hex, out var c) ? new SolidColorBrush(c) : Brushes.Gray;
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = fill,
            BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(0x22, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0),
        };
        var btn = new Button
        {
            Classes = { "swatch" },
            Content = border,
            Tag = hex,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            MinHeight = 22,
            Margin = new Thickness(2.5),
        };
        btn.Click += OnPresetClick;
        return btn;
    }

    private Button MakeCustomSwatch()
    {
        var grid = new Grid();
        var checker = new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = MakeChecker(),
            BorderBrush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(0x22, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        };
        grid.Children.Add(checker);
        var slash = new Avalonia.Controls.Shapes.Path
        {
            Stroke = new SolidColorBrush(Avalonia.Media.Color.Parse("#8E8E93")),
            StrokeThickness = 1.5,
            Data = Avalonia.Media.Geometry.Parse("M 0,1 L 1,0"),
            Stretch = Stretch.Fill,
            Margin = new Thickness(4),
        };
        grid.Children.Add(slash);
        var btn = new Button
        {
            Classes = { "swatch" },
            Content = grid,
            Margin = new Thickness(2.5),
            MinHeight = 22,
        };
        btn.Click += OnCustomClick;
        ToolTip.SetTip(btn, "Custom color…");
        return btn;
    }

    private static SolidColorBrush MakeChecker()
        => new(Avalonia.Media.Color.Parse("#E0E0E0"));

    private void UpdateSelection()
    {
        foreach (var btn in _swatchButtons)
        {
            bool selected = string.Equals(btn.Tag as string, Color, StringComparison.OrdinalIgnoreCase);
            if (btn.Content is Border b)
            {
                b.BorderBrush = selected
                    ? new SolidColorBrush(Avalonia.Media.Color.Parse("#18181B"))
                    : new SolidColorBrush(Avalonia.Media.Color.FromArgb(0x22, 0, 0, 0));
                b.BorderThickness = new Thickness(selected ? 2 : 1);
            }
        }
    }
}
