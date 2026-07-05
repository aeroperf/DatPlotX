using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using DatPlotX.Models;

namespace DatPlotX.Views.Controls.Dpx;

public partial class MarkerChips : UserControl
{
    public static readonly StyledProperty<MarkerStyle> ValueProperty =
        AvaloniaProperty.Register<MarkerChips, MarkerStyle>(nameof(Value), MarkerStyle.Circle);

    public MarkerStyle Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private bool _suppress;

    public MarkerChips()
    {
        InitializeComponent();
        CircleChip.Click += (_, _) => OnChipClick(MarkerStyle.Circle);
        SquareChip.Click += (_, _) => OnChipClick(MarkerStyle.Square);
        TriangleChip.Click += (_, _) => OnChipClick(MarkerStyle.Triangle);
        DiamondChip.Click += (_, _) => OnChipClick(MarkerStyle.Diamond);
        CrossChip.Click += (_, _) => OnChipClick(MarkerStyle.Cross);
        PlusChip.Click += (_, _) => OnChipClick(MarkerStyle.Plus);
        Sync();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
            Sync();
    }


    private void OnChipClick(MarkerStyle style)
    {
        if (_suppress) return;
        Value = style;
    }

    private void Sync()
    {
        _suppress = true;
        try
        {
            Set(CircleChip, Value == MarkerStyle.Circle);
            Set(SquareChip, Value == MarkerStyle.Square);
            Set(TriangleChip, Value == MarkerStyle.Triangle);
            Set(DiamondChip, Value == MarkerStyle.Diamond);
            Set(CrossChip, Value == MarkerStyle.Cross);
            Set(PlusChip, Value == MarkerStyle.Plus);
        }
        finally { _suppress = false; }

        static void Set(ToggleButton tb, bool on) { if (tb.IsChecked != on) tb.IsChecked = on; }
    }
}
