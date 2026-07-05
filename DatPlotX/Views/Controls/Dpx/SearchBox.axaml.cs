using Avalonia;
using Avalonia.Controls;

namespace DatPlotX.Views.Controls.Dpx;

public partial class SearchBox : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(Watermark), "Search…");

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string? Watermark
    {
        get => GetValue(WatermarkProperty);
        set => SetValue(WatermarkProperty, value);
    }

    public event EventHandler<string?>? TextChangedEvent;

    public SearchBox()
    {
        InitializeComponent();
        InnerBox.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty)
            {
                var t = InnerBox.Text;
                Text = t;
                TextChangedEvent?.Invoke(this, t);
            }
        };
        ApplyText();
        ApplyWatermark();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty) ApplyText();
        else if (change.Property == WatermarkProperty) ApplyWatermark();
    }

    private void ApplyText()
    {
        if (InnerBox.Text != Text) InnerBox.Text = Text;
    }

    private void ApplyWatermark() => InnerBox.Watermark = Watermark;
}
