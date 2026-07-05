using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

public partial class ImportOptionsDialog : Window
{
    public ImportOptionsDialog(ImportOptionsDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        this.EnableEscapeToClose(false);
    }

    public ImportOptionsDialog()
    {
        InitializeComponent();
    }

    public ImportOptionsDialogViewModel ViewModel => (ImportOptionsDialogViewModel)DataContext!;

    private void OkButton_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel.CanImport) Close(true);
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
