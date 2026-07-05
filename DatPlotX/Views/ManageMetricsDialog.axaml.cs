using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.Services.Analysis;
using DatPlotX.ViewModels;
using System.ComponentModel;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for choosing which metrics appear as columns in the Analysis Results panel and in
/// what order. Returns its <see cref="ViewModel"/> (with the chosen <see cref="ManageMetricsDialogViewModel.EnabledIds"/>)
/// on Apply, or null on Cancel.
/// </summary>
public partial class ManageMetricsDialog : Window
{
    public ManageMetricsDialogViewModel ViewModel { get; }

    public ManageMetricsDialog(IReadOnlyList<IMetricDefinition> allMetrics, IReadOnlyList<string> enabledIds)
    {
        ViewModel = new ManageMetricsDialogViewModel(allMetrics, enabledIds);
        InitializeComponent();
        DataContext = ViewModel;
        this.EnableEscapeToClose(false);
        HookRowToggles();
    }

    public ManageMetricsDialog()
    {
        ViewModel = new ManageMetricsDialogViewModel();
        InitializeComponent();
        DataContext = ViewModel;
    }

    // Keep the Apply button's enabled state in sync as the user checks / unchecks metrics.
    private void HookRowToggles()
    {
        foreach (var row in ViewModel.Rows)
            row.PropertyChanged += OnRowPropertyChanged;
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MetricRowViewModel.IsEnabled))
            ViewModel.OnEnabledChanged();
    }

    private void Apply_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
