using Avalonia;
using Avalonia.Controls;
using DatPlotX.Models;

namespace DatPlotX.Views.Controls.Dpx;

public partial class AxisSideToggle : UserControl
{
    public static readonly StyledProperty<AxisSide> ValueProperty =
        AvaloniaProperty.Register<AxisSideToggle, AxisSide>(nameof(Value), AxisSide.Left,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public AxisSide Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private bool _suppress;

    public AxisSideToggle()
    {
        InitializeComponent();
        LeftBtn.Click += (_, _) => OnClick(AxisSide.Left);
        RightBtn.Click += (_, _) => OnClick(AxisSide.Right);
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            Sync();
    }


    private void OnClick(AxisSide side)
    {
        if (_suppress) return;
        Value = side;
    }

    private void Sync()
    {
        _suppress = true;
        try
        {
            if (LeftBtn.IsChecked != (Value == AxisSide.Left)) LeftBtn.IsChecked = Value == AxisSide.Left;
            if (RightBtn.IsChecked != (Value == AxisSide.Right)) RightBtn.IsChecked = Value == AxisSide.Right;
        }
        finally { _suppress = false; }
    }
}
