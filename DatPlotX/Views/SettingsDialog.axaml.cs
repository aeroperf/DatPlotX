using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

public partial class SettingsDialog : Window
{
    public bool DialogAccepted { get; private set; }

    public SettingsDialog()
    {
        InitializeComponent();
        this.EnableEscapeToClose();
    }

    public SettingsDialog(SettingsDialogViewModel vm) : this()
    {
        DataContext = vm;
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
    {
        DialogAccepted = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
