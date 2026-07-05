using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.Models.Analysis;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

/// <summary>
/// Dialog for renaming, deleting, and choosing the active analysis segment.
/// </summary>
public partial class ManageSegmentsDialog : Window
{
    public static readonly IValueConverter RemoveButtonConverter =
        new FuncValueConverter<bool, string>(isMarked => isMarked ? "Undo" : "Remove");

    public ManageSegmentsDialogViewModel ViewModel { get; }

    public ManageSegmentsDialog(IReadOnlyList<AnalysisSegment> segments, Guid activeId)
    {
        ViewModel = new ManageSegmentsDialogViewModel(segments, activeId);
        InitializeComponent();
        DataContext = ViewModel;
        this.EnableEscapeToClose(false);
    }

    public ManageSegmentsDialog()
    {
        ViewModel = new ManageSegmentsDialogViewModel();
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void Apply_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
}
