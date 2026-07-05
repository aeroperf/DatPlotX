using Avalonia.Controls;
using Avalonia.Interactivity;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.ComponentModel;

namespace DatPlotX.Views;

/// <summary>
/// Two-stage dialog for building a Compact Plot Surface curve list.
/// Stage 1 = column picker (flat list); Stage 2 = per-curve band/side/style config.
/// On OK returns the assembled <see cref="CompactCurveModel"/> list via <see cref="Result"/>.
/// </summary>
public partial class AddCompactCurvesDialog : Window
{
    private readonly AddCompactCurvesDialogViewModel _viewModel;

    /// <summary>Curves to add when the dialog is accepted; empty/null otherwise.</summary>
    public IReadOnlyList<CompactCurveModel>? Result { get; private set; }

    public AddCompactCurvesDialog(PlotDataModel? data, string xColumn, int existingCurveCount)
    {
        InitializeComponent();
        _viewModel = new AddCompactCurvesDialogViewModel(data, xColumn, existingCurveCount);
        DataContext = _viewModel;
        WireEvents();
        UpdateStage();
        UpdateSelectionTag();
    }

    public AddCompactCurvesDialog()
    {
        InitializeComponent();
        _viewModel = new AddCompactCurvesDialogViewModel((PlotDataModel?)null, string.Empty, 0);
        DataContext = _viewModel;
        WireEvents();
        UpdateStage();
        UpdateSelectionTag();
    }

    private void WireEvents()
    {
        NextButton.Click += OnNext;
        BackButton.Click += OnBack;
        CancelButton.Click += (_, _) => Close(false);
        this.EnableEscapeToClose(false);
        SelectAllButton.Click += (_, _) => SetAll(true);
        ClearAllButton.Click += (_, _) => SetAll(false);

        ColumnSearch.TextChangedEvent += (_, _) => ApplyFilter();

        Stage1Stepper.Labels = new[] { "Pick parameters", "Configure curves" };
        Stage2Stepper.Labels = new[] { "Pick parameters", "Configure curves" };

        // Re-evaluate selection tag whenever a column toggles.
        foreach (var pick in _viewModel.AvailableColumns)
            pick.PropertyChanged += OnColumnChanged;
        _viewModel.AvailableColumns.CollectionChanged += (_, _) =>
        {
            foreach (var pick in _viewModel.AvailableColumns)
            {
                pick.PropertyChanged -= OnColumnChanged;
                pick.PropertyChanged += OnColumnChanged;
            }
            UpdateSelectionTag();
        };

        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(_viewModel.Stage) or nameof(_viewModel.HasAnySelection))
                UpdateStage();
        };

        _viewModel.Drafts.CollectionChanged += (_, _) =>
        {
            if (_viewModel.Stage == 2) UpdateStage();
        };
    }

    private void OnColumnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompactColumnPick.IsSelected))
            UpdateSelectionTag();
    }

    private void SetAll(bool isSelected)
    {
        foreach (var pick in _viewModel.AvailableColumns)
            pick.IsSelected = isSelected;
    }

    private void ApplyFilter() => _viewModel.ApplyColumnFilter(ColumnSearch.Text);

    private void UpdateSelectionTag()
    {
        int total = _viewModel.AvailableColumns.Count;
        int selected = 0;
        foreach (var pick in _viewModel.AvailableColumns)
            if (pick.IsSelected) selected++;
        SelectionTag.Text = $"{selected} of {total}";
        FooterHint.Text = _viewModel.Stage == 1
            ? $"{selected} of {total} selected"
            : string.Empty;
        NextButton.IsEnabled = _viewModel.Stage == 1 ? selected > 0 : _viewModel.Drafts.Count > 0;
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.Stage == 1)
        {
            if (!_viewModel.HasAnySelection) return;
            _viewModel.AdvanceToStage2();
        }
        else
        {
            Result = _viewModel.BuildCurves();
            Close(true);
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.Stage == 2)
            _viewModel.BackToStage1();
    }

    private void UpdateStage()
    {
        if (_viewModel.Stage == 1)
        {
            Stage1Panel.IsVisible = true;
            Stage2Panel.IsVisible = false;
            BackButton.IsVisible = false;
            NextButton.Content = "Next →";
        }
        else
        {
            Stage1Panel.IsVisible = false;
            Stage2Panel.IsVisible = true;
            BackButton.IsVisible = true;
            NextButton.Content = "Add curves";
        }
        UpdateSelectionTag();
    }

    private async void ColorCell_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button btn || btn.Tag is not CompactCurveDraft draft) return;
            var dlg = new ColorPickerDialog(draft.Color);
            if (await dlg.ShowDialog<bool?>(this) == true)
                draft.Color = dlg.SelectedColor;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[AddCompactCurvesDialog] {ex}"); }
    }
}
