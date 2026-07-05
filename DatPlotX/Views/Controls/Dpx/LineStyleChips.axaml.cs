using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DatPlotX.Models;

namespace DatPlotX.Views.Controls.Dpx;

public partial class LineStyleChips : UserControl
{
    public static readonly StyledProperty<LineStyle> ValueProperty =
        AvaloniaProperty.Register<LineStyleChips, LineStyle>(nameof(Value), LineStyle.Solid);

    public LineStyle Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private bool _suppress;

    public LineStyleChips()
    {
        InitializeComponent();
        SolidChip.Click += (_, _) => OnChipClick(LineStyle.Solid);
        DashChip.Click += (_, _) => OnChipClick(LineStyle.Dash);
        DotChip.Click += (_, _) => OnChipClick(LineStyle.Dot);
        DashDotChip.Click += (_, _) => OnChipClick(LineStyle.DashDot);
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            Sync();
    }


    private void OnChipClick(LineStyle style)
    {
        if (_suppress) return;
        Value = style;
    }

    private void Sync()
    {
        _suppress = true;
        try
        {
            Set(SolidChip, Value == LineStyle.Solid);
            Set(DashChip, Value == LineStyle.Dash);
            Set(DotChip, Value == LineStyle.Dot);
            Set(DashDotChip, Value == LineStyle.DashDot);
        }
        finally { _suppress = false; }

        static void Set(ToggleButton tb, bool on) { if (tb.IsChecked != on) tb.IsChecked = on; }
    }
}
