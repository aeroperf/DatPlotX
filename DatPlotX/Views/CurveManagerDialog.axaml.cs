using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using DatPlotX.Views.Controls.Dpx;
using System.Collections.ObjectModel;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for managing curve visibility, color, line width, and style.
/// </summary>
public partial class CurveManagerDialog : Window
{
    public static readonly IValueConverter RemoveButtonConverter =
        new FuncValueConverter<bool, string>(isMarked => isMarked ? "Undo" : "Remove");

    public CurveManagerDialogViewModel ViewModel { get; }

    public CurveManagerDialog(ObservableCollection<CurveConfigurationModel> activeCurves)
    {
        ViewModel = new CurveManagerDialogViewModel(activeCurves);

        InitializeComponent();
        DataContext = ViewModel;
        this.EnableEscapeToClose(false);
    }

    public CurveManagerDialog()
    {
        ViewModel = new CurveManagerDialogViewModel(new ObservableCollection<CurveConfigurationModel>());

        InitializeComponent();
        DataContext = ViewModel;
    }

    private void Apply_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void ChangeColor_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not CurveItemViewModel curveItem) return;

        SafeInvokeAsync(async () =>
        {
            var dlg = new ColorPickerDialog(curveItem.Color);
            if (await dlg.ShowDialog<bool?>(this) == true)
            {
                curveItem.Color = dlg.SelectedColor;
                if (control is ColorPill pill)
                    pill.Color = dlg.SelectedColor;
            }
        });
    }

    private async void SafeInvokeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[CurveManagerDialog] {ex}"); }
    }
}
