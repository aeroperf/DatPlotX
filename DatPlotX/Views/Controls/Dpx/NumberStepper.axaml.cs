using Avalonia;
using Avalonia.Controls;
using System.Globalization;

namespace DatPlotX.Views.Controls.Dpx;

public partial class NumberStepper : UserControl
{
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Value), 0d,
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Minimum), double.MinValue);

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Maximum), double.MaxValue);

    public static readonly StyledProperty<double> StepProperty =
        AvaloniaProperty.Register<NumberStepper, double>(nameof(Step), 1d);

    public static readonly StyledProperty<int> DecimalsProperty =
        AvaloniaProperty.Register<NumberStepper, int>(nameof(Decimals), 0);

    public static readonly StyledProperty<string?> SuffixProperty =
        AvaloniaProperty.Register<NumberStepper, string?>(nameof(Suffix));

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double Step { get => GetValue(StepProperty); set => SetValue(StepProperty, value); }
    public int Decimals { get => GetValue(DecimalsProperty); set => SetValue(DecimalsProperty, value); }
    public string? Suffix { get => GetValue(SuffixProperty); set => SetValue(SuffixProperty, value); }

    public NumberStepper()
    {
        InitializeComponent();
        MinusBtn.Click += (_, _) => Bump(-Step);
        PlusBtn.Click += (_, _) => Bump(+Step);
        Refresh();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty
            || change.Property == DecimalsProperty
            || change.Property == SuffixProperty)
        {
            Refresh();
        }
    }

    private void Bump(double delta)
    {
        var next = Value + delta;
        if (next < Minimum) next = Minimum;
        if (next > Maximum) next = Maximum;
        Value = next;
    }

    private void Refresh()
    {
        ValueText.Text = Value.ToString("F" + Decimals, CultureInfo.InvariantCulture);
        SuffixText.Text = Suffix ?? string.Empty;
        SuffixText.IsVisible = !string.IsNullOrEmpty(Suffix);
    }
}
