using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DatPlotX.Views.Controls.Dpx;

public partial class ColorPill : UserControl
{
    public static readonly StyledProperty<string> ColorProperty =
        AvaloniaProperty.Register<ColorPill, string>(nameof(Color), "#0078D4",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public event EventHandler<RoutedEventArgs>? PickRequested;

    public ColorPill()
    {
        InitializeComponent();
        RootButton.Click += (_, e) => PickRequested?.Invoke(this, e);
        Apply(Color);
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ColorProperty)
            Apply(Color);
    }

    private void Apply(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        if (Avalonia.Media.Color.TryParse(hex, out var parsed))
        {
            ColorFill.Background = new SolidColorBrush(parsed);
            HexText.Text = hex.ToUpperInvariant();
        }
        else
        {
            HexText.Text = hex;
        }
    }
}
