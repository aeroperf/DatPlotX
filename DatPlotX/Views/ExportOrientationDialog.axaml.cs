using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for selecting export image orientation
/// </summary>
public partial class ExportOrientationDialog : Window
{
    public bool IsLandscape { get; private set; }

    public ExportOrientationDialog()
    {
        InitializeComponent();
        IsLandscape = true; // Default to landscape
        this.EnableEscapeToClose(false);
    }

    private void Landscape_Click(object? sender, RoutedEventArgs e)
    {
        IsLandscape = true;
        Close(true);
    }

    private void Portrait_Click(object? sender, RoutedEventArgs e)
    {
        IsLandscape = false;
        Close(true);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }
}
