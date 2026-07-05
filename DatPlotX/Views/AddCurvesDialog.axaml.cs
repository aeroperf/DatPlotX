using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for adding curves to a stacked plot pane one at a time.
/// Matches the WPF DatPlot.Modern pattern: pick parameter + Y-axis, click "Plot curve".
/// </summary>
public partial class AddCurvesDialog : Window
{
    private static readonly string[] Palette =
    {
        "#0000FF", "#FF00FF", "#00FF00", "#FF0000", "#000000",
        "#FFA500", "#00FFFF", "#800080", "#8B4513", "#FF69B4",
    };

    private readonly AddCurvesDialogViewModel _viewModel;
    private readonly Action<AddCurveRequest>? _onCurvePlotted;
    private readonly ObservableCollection<PlottedRowVm> _rows = new();

    public AddCurvesDialog(DataTable? sourceData, string xColumn, int targetPaneIndex, Action<AddCurveRequest>? onCurvePlotted = null)
    {
        InitializeComponent();
        _viewModel = new AddCurvesDialogViewModel(sourceData, xColumn, targetPaneIndex);
        DataContext = _viewModel;
        _onCurvePlotted = onCurvePlotted;

        PlottedList.ItemsSource = _rows;
        PlotCurveButton.Click += PlotCurve_Click;
        CloseButton.Click += (_, _) => Close(true);
        UpdatePlottedHeader();
        this.EnableEscapeToClose(true);
    }

    public AddCurvesDialog() : this(null, string.Empty, 0) { }

    public AddCurvesDialogViewModel ViewModel => _viewModel;

    private void PlotCurve_Click(object? sender, RoutedEventArgs e) => SafeInvokeAsync(async () =>
    {
        if (string.IsNullOrEmpty(_viewModel.SelectedParameter))
        {
            await ShowMessage("Please select a parameter to plot.", "No Parameter Selected");
            return;
        }
        if (_viewModel.SelectedYAxis == null)
        {
            await ShowMessage("Please select a Y-axis.", "No Axis Selected");
            return;
        }

        var unit = string.IsNullOrWhiteSpace(_viewModel.UnitText) ? null : _viewModel.UnitText.Trim();
        _onCurvePlotted?.Invoke(new AddCurveRequest(_viewModel.SelectedParameter, _viewModel.SelectedYAxis.AxisType, unit));
        _viewModel.TrackPlottedCurve();

        var color = Palette[_rows.Count % Palette.Length];
        var axisLabel = _viewModel.SelectedYAxis.AxisType == "Y2" ? "Right" : "Left";
        if (Color.TryParse(color, out var c))
        {
            _rows.Add(new PlottedRowVm
            {
                ParameterName = _viewModel.SelectedParameter,
                AxisLabel = axisLabel,
                Color = new SolidColorBrush(c),
            });
        }
        UpdatePlottedHeader();
    });

    private void UpdatePlottedHeader()
        => PlottedHeading.Text = $"ALREADY ON PANE · {_rows.Count}";

    private async void SafeInvokeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AddCurvesDialog] {ex}"); }
    }

    private async Task ShowMessage(string message, string title)
    {
        var dlg = new Window
        {
            Title = title,
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Classes = { "dpx" },
        };
        var stack = new StackPanel { Margin = new Avalonia.Thickness(20) };
        stack.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Avalonia.Thickness(0, 0, 0, 15) });
        var ok = new Button { Content = "OK", Width = 80, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        ok.Classes.Add("dpx");
        stack.Children.Add(ok);
        dlg.Content = stack;
        ok.Click += (_, _) => dlg.Close();
        await dlg.ShowDialog(this);
    }

    internal sealed class PlottedRowVm
    {
        public string ParameterName { get; set; } = string.Empty;
        public string AxisLabel { get; set; } = string.Empty;
        public IBrush Color { get; set; } = Brushes.Black;
    }
}
