using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatPlotX.Services.Analysis;
using System.Collections.ObjectModel;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the Manage Metrics dialog. Lets the user choose which of the registry's metrics
/// appear as columns in the Analysis Results panel, and in what order. Works on an editable
/// snapshot; the host applies <see cref="EnabledIds"/> to <c>IAnalysisService.SetEnabledMetrics</c>
/// only on Apply, so Cancel is a no-op.
///
/// <para>Rows are presented in display order: enabled metrics first (in their current column
/// order), then the remaining metrics grouped by registry order. Each row carries an
/// <see cref="MetricRowViewModel.IsEnabled"/> checkbox; enabled rows can be moved up/down to set
/// column order.</para>
/// </summary>
public partial class ManageMetricsDialogViewModel : ObservableObject
{
    public ManageMetricsDialogViewModel(
        IReadOnlyList<IMetricDefinition> allMetrics, IReadOnlyList<string> enabledIds)
    {
        // Enabled metrics first, in their persisted column order…
        foreach (var id in enabledIds)
        {
            var def = allMetrics.FirstOrDefault(m => string.Equals(m.Id, id, System.StringComparison.OrdinalIgnoreCase));
            if (def is not null)
                Rows.Add(new MetricRowViewModel(def) { IsEnabled = true });
        }
        // …then everything not already listed, in registry order.
        foreach (var def in allMetrics)
        {
            if (Rows.Any(r => string.Equals(r.Id, def.Id, System.StringComparison.OrdinalIgnoreCase)))
                continue;
            Rows.Add(new MetricRowViewModel(def) { IsEnabled = false });
        }

        SelectedRow = Rows.FirstOrDefault();
    }

    private static readonly string[] DefaultEnabledIds = { "max", "min", "mean", "stddev", "slope" };

    /// <summary>Design-time / fallback ctor.</summary>
    public ManageMetricsDialogViewModel()
        : this(new MetricRegistry().All, DefaultEnabledIds)
    {
    }

    public ObservableCollection<MetricRowViewModel> Rows { get; } = new();

    [ObservableProperty]
    private MetricRowViewModel? _selectedRow;

    /// <summary>Enabled metric IDs in row (column) order — the value the host applies on Apply.
    /// At least one is always kept so the panel never goes empty.</summary>
    public IReadOnlyList<string> EnabledIds =>
        Rows.Where(r => r.IsEnabled).Select(r => r.Id).ToList();

    /// <summary>True when at least one metric is enabled (Apply is otherwise pointless / would
    /// blank the panel). Bound to the Apply button's enabled state.</summary>
    public bool HasAnyEnabled => Rows.Any(r => r.IsEnabled);

    partial void OnSelectedRowChanged(MetricRowViewModel? value) => NotifyMoveState();

    private void NotifyMoveState()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Re-evaluate Apply availability after a checkbox toggle (called by the view).</summary>
    public void OnEnabledChanged() => OnPropertyChanged(nameof(HasAnyEnabled));

    private bool CanMoveUp() => SelectedRow is { } r && Rows.IndexOf(r) > 0;
    private bool CanMoveDown() => SelectedRow is { } r && Rows.IndexOf(r) < Rows.Count - 1;

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    private void Move(int delta)
    {
        if (SelectedRow is not { } r) return;
        int i = Rows.IndexOf(r);
        int j = i + delta;
        if (j < 0 || j >= Rows.Count) return;
        Rows.Move(i, j);
        SelectedRow = r;   // keep selection on the moved row
        NotifyMoveState();
    }
}

/// <summary>One metric row in the Manage Metrics dialog.</summary>
public partial class MetricRowViewModel : ObservableObject
{
    public MetricRowViewModel(IMetricDefinition def)
    {
        Id = def.Id;
        DisplayName = def.DisplayName;
        Category = def.Category.ToString();
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Category { get; }

    [ObservableProperty]
    private bool _isEnabled;
}
