using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

/// <summary>
/// Tabbed dialog for formatting a stacked-mode plot pane: title, axis labels,
/// per-axis range + tick format, grid, background, legend &amp; statistics.
/// State lives in <see cref="FormatPaneDialogViewModel"/>; this code-behind only wires
/// the color-picker pop-ups and OK / Cancel.
/// </summary>
public partial class FormatPaneDialog : Window
{
    private readonly FormatPaneDialogViewModel _viewModel = new();

    public PlotPaneModel PaneModel { get; private set; }

    public FormatPaneDialog(PlotPaneModel paneModel)
    {
        InitializeComponent();
        PaneModel = paneModel;
        _viewModel.LoadFrom(paneModel);
        DataContext = _viewModel;
        WireColorPickers();
        this.EnableEscapeToClose(false);
    }

    public FormatPaneDialog()
    {
        InitializeComponent();
        PaneModel = new PlotPaneModel();
        _viewModel.LoadFrom(PaneModel);
        DataContext = _viewModel;
        WireColorPickers();
    }

    private void WireColorPickers()
    {
        GridColorPill.PickRequested += (_, _) =>
            OpenColorPicker(_viewModel.GridColor, c => _viewModel.GridColor = c);
        PlotBgColorPill.PickRequested += (_, _) =>
            OpenColorPicker(_viewModel.BackgroundColor, c => _viewModel.BackgroundColor = c);
        DataBgColorPill.PickRequested += (_, _) =>
            OpenColorPicker(_viewModel.DataBackgroundColor, c => _viewModel.DataBackgroundColor = c);
    }

    private void OpenColorPicker(string current, Action<string> onPicked) =>
        SafeInvokeAsync(async () =>
        {
            var dlg = new ColorPickerDialog(current);
            if (await dlg.ShowDialog<bool?>(this) == true)
                onPicked(dlg.SelectedColor);
        });

    private async void SafeInvokeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[FormatPaneDialog] {ex}"); }
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        _viewModel.ApplyTo(PaneModel);
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
