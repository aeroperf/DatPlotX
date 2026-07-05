using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DatPlotX.Views.Controls.Dpx;

public sealed record ChipOption(string Tag, string Label);

public partial class ChipGroup : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<ChipOption>> OptionsProperty =
        AvaloniaProperty.Register<ChipGroup, IReadOnlyList<ChipOption>>(nameof(Options),
            new List<ChipOption>());

    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<ChipGroup, string?>(nameof(Value));

    public IReadOnlyList<ChipOption> Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private readonly List<ToggleButton> _buttons = new();
    private bool _suppress;

    public ChipGroup()
    {
        InitializeComponent();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OptionsProperty)
            Rebuild();
        else if (change.Property == ValueProperty)
            SyncSelection();
    }

    private void Rebuild()
    {
        foreach (var old in _buttons) old.Click -= OnChipClick;
        _buttons.Clear();

        var options = Options ?? Array.Empty<ChipOption>();
        var children = new List<ToggleButton>(options.Count);
        foreach (var opt in options)
        {
            var btn = new ToggleButton
            {
                Classes = { "gchip" },
                Content = opt.Label,
                Tag = opt.Tag,
            };
            btn.Click += OnChipClick;
            _buttons.Add(btn);
            children.Add(btn);
        }
        ItemsHost.ItemsSource = children;
        SyncSelection();
    }

    private void OnChipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_suppress) return;
        if (sender is ToggleButton tb && tb.Tag is string tag)
            Value = tag;
    }

    private void SyncSelection()
    {
        _suppress = true;
        try
        {
            foreach (var btn in _buttons)
            {
                bool on = btn.Tag is string t && t == Value;
                if (btn.IsChecked != on) btn.IsChecked = on;
            }
        }
        finally { _suppress = false; }
    }
}
