using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

namespace DatPlotX.Views.Controls.Dpx;

public partial class WizardStepper : UserControl
{
    public static readonly StyledProperty<int> StepProperty =
        AvaloniaProperty.Register<WizardStepper, int>(nameof(Step), 1);

    public static readonly StyledProperty<IReadOnlyList<string>> LabelsProperty =
        AvaloniaProperty.Register<WizardStepper, IReadOnlyList<string>>(nameof(Labels),
            new List<string>());

    public int Step
    {
        get => GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    public IReadOnlyList<string> Labels
    {
        get => GetValue(LabelsProperty);
        set => SetValue(LabelsProperty, value);
    }

    public WizardStepper()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StepProperty || change.Property == LabelsProperty)
        {
            // Defer until attached so theme resources resolve (Application.Current may be null in
            // designer / unit-test scenarios, and resources aren't available before attach).
            if (this.Parent is not null)
                Rebuild();
        }
    }

    private IBrush ResolveBrush(string key)
    {
        if (this.TryFindResource(key, out var resource) && resource is IBrush b) return b;
        return Brushes.Transparent;
    }

    private void Rebuild()
    {
        Host.Children.Clear();
        var labels = Labels ?? new List<string>();
        for (int i = 0; i < labels.Count; i++)
        {
            int idx = i + 1;
            string state = idx < Step ? "done" : idx == Step ? "active" : "todo";

            var dot = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var dotText = new TextBlock
            {
                Text = idx.ToString(System.Globalization.CultureInfo.InvariantCulture),
                FontSize = 10.5,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            switch (state)
            {
                case "active":
                    dot.Background = ResolveBrush("DpxAccent");
                    dot.BorderBrush = ResolveBrush("DpxAccent");
                    dotText.Foreground = ResolveBrush("DpxAccentInk");
                    break;
                case "done":
                    dot.Background = ResolveBrush("DpxAccentSoft");
                    dot.BorderBrush = Brushes.Transparent;
                    dotText.Foreground = ResolveBrush("DpxAccent");
                    dotText.Text = "✓";
                    dotText.FontSize = 11;
                    break;
                default:
                    dot.Background = ResolveBrush("DpxSurface");
                    dot.BorderBrush = ResolveBrush("DpxBorderStrong");
                    dotText.Foreground = ResolveBrush("DpxText3");
                    break;
            }

            dot.Child = dotText;
            Host.Children.Add(dot);

            var label = new TextBlock
            {
                Text = labels[i],
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = state == "active" ? ResolveBrush("DpxText") : ResolveBrush("DpxText2"),
                FontWeight = state == "active" ? FontWeight.SemiBold : FontWeight.Medium,
            };
            Host.Children.Add(label);

            if (i < labels.Count - 1)
            {
                var bar = new Rectangle
                {
                    Width = 28,
                    Height = 2,
                    RadiusX = 1,
                    RadiusY = 1,
                    Fill = idx < Step ? ResolveBrush("DpxAccent") : ResolveBrush("DpxBorder"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Host.Children.Add(bar);
            }
        }
    }
}
