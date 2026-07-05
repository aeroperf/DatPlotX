using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using DatPlotX.Models;

namespace DatPlotX.Views;

/// <summary>
/// Modal "Choose Plot Style" dialog shown when starting a new project. The choice is locked
/// for the life of the project; switching requires a new project and a fresh CSV import.
/// </summary>
public partial class PlotModePickerDialog : Window
{
    public PlotMode SelectedMode { get; private set; } = PlotMode.Panes;

    public PlotModePickerDialog()
    {
        InitializeComponent();

        var panes = this.FindControl<Border>("PanesCard")!;
        var compact = this.FindControl<Border>("CompactCard")!;
        var grouped = this.FindControl<Border>("GroupedCard")!;
        var ok = this.FindControl<Button>("OkButton")!;
        var cancel = this.FindControl<Button>("CancelButton")!;

        panes.PointerPressed += (_, _) => Select(PlotMode.Panes);
        compact.PointerPressed += (_, _) => Select(PlotMode.Compact);
        grouped.PointerPressed += (_, _) => Select(PlotMode.Grouped);

        panes.DoubleTapped += (_, e) => { Select(PlotMode.Panes); e.Handled = true; Close(true); };
        compact.DoubleTapped += (_, e) => { Select(PlotMode.Compact); e.Handled = true; Close(true); };
        grouped.DoubleTapped += (_, e) => { Select(PlotMode.Grouped); e.Handled = true; Close(true); };

        ok.Click += (_, _) => Close(true);
        cancel.Click += (_, _) => Close(false);

        this.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(false); e.Handled = true; }
            else if (e.Key == Key.Enter) { Close(true); e.Handled = true; }
        };

        Select(PlotMode.Panes);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Select(PlotMode mode)
    {
        SelectedMode = mode;
        var panes = this.FindControl<Border>("PanesCard")!;
        var compact = this.FindControl<Border>("CompactCard")!;
        var grouped = this.FindControl<Border>("GroupedCard")!;
        SetSelected(panes, mode == PlotMode.Panes);
        SetSelected(compact, mode == PlotMode.Compact);
        SetSelected(grouped, mode == PlotMode.Grouped);
    }

    private static void SetSelected(Border card, bool selected)
    {
        if (selected && !card.Classes.Contains("selected"))
            card.Classes.Add("selected");
        else if (!selected && card.Classes.Contains("selected"))
            card.Classes.Remove("selected");
    }
}
