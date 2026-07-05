using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DatPlotX.Views.Controls.Dpx;

public partial class AnchorPicker : UserControl
{
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<AnchorPicker, string>(nameof(Value), "UpperLeft",
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static readonly string[] Cells =
    {
        "UpperLeft",  "UpperCenter",  "UpperRight",
        "MiddleLeft", "MiddleCenter", "MiddleRight",
        "LowerLeft",  "LowerCenter",  "LowerRight",
    };

    private readonly List<ToggleButton> _buttons = new();
    private bool _suppress;

    public AnchorPicker()
    {
        InitializeComponent();
        foreach (var anchor in Cells)
        {
            var btn = new ToggleButton
            {
                Classes = { "cell" },
                Tag = anchor,
                Margin = new Thickness(1),
            };
            btn.Click += (_, _) =>
            {
                if (_suppress) return;
                if (btn.Tag is string s) Value = s;
            };
            _buttons.Add(btn);
            CellHost.Children.Add(btn);
        }
        Sync();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) Sync();
    }

    private void Sync()
    {
        _suppress = true;
        try
        {
            foreach (var b in _buttons)
            {
                bool on = b.Tag is string s && s == Value;
                if (b.IsChecked != on) b.IsChecked = on;
            }
        }
        finally { _suppress = false; }
    }
}
