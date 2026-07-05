using Avalonia.Controls;
using Avalonia.Input;
using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;

namespace DatPlotX.Views;

/// <summary>
/// Modal wizard for configuring the Grouped Parameter Plot inputs and initial X/Y selection.
/// Returns the new <see cref="GroupedPlotConfig"/> when applied; returns null on cancel.
/// </summary>
public partial class GroupedInputsPickerDialog : Window
{
    private GroupedInputsPickerDialogViewModel? _vm;
    private GroupedPlotConfig? _existingConfig;
    public GroupedPlotConfig? Result { get; private set; }

    public GroupedInputsPickerDialog()
    {
        InitializeComponent();
        CancelButton.Click += (_, _) => Close(null);
        ApplyButton.Click += (_, _) =>
        {
            if (_vm is null || !_vm.IsValid) return;
            Result = _vm.BuildConfig(_existingConfig);
            Close(Result);
        };
        this.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
        };
    }

    public void Initialize(PlotDataModel data, IGroupedDataIndexer indexer, ApplicationSettings settings, GroupedPlotConfig? existing)
    {
        _existingConfig = existing;
        _vm = new GroupedInputsPickerDialogViewModel(data, indexer, settings, existing);
        DataContext = _vm;
    }
}
