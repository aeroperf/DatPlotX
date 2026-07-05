using Avalonia;
using Avalonia.Controls;

namespace DatPlotX.Views.Controls.Dpx;

public partial class RangeInputs : UserControl
{
    public static readonly StyledProperty<string?> MinTextProperty =
        AvaloniaProperty.Register<RangeInputs, string?>(nameof(MinText),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> MaxTextProperty =
        AvaloniaProperty.Register<RangeInputs, string?>(nameof(MaxText),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? MinText { get => GetValue(MinTextProperty); set => SetValue(MinTextProperty, value); }
    public string? MaxText { get => GetValue(MaxTextProperty); set => SetValue(MaxTextProperty, value); }

    public TextBox Min => MinBox;
    public TextBox Max => MaxBox;

    public RangeInputs()
    {
        InitializeComponent();
        MinBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) MinText = MinBox.Text; };
        MaxBox.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) MaxText = MaxBox.Text; };
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == MinTextProperty && MinBox.Text != MinText)
            MinBox.Text = MinText;
        else if (change.Property == MaxTextProperty && MaxBox.Text != MaxText)
            MaxBox.Text = MaxText;
    }
}
