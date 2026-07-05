using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;
using DatPlotX.Services;
using System.Collections.ObjectModel;

namespace DatPlotX.ViewModels;

/// <summary>
/// Backs the "Configure Grouped Inputs" wizard. Lists every column in the imported data with a
/// checkbox + optional display label / unit / format. The user marks up to
/// <see cref="ApplicationSettings.GroupedPlotMaxInputs"/> columns as inputs, then picks an X and
/// Y column. Columns whose distinct-value count exceeds
/// <see cref="ApplicationSettings.GroupedPlotMaxDistinctValues"/> are flagged as ineligible.
/// </summary>
public sealed partial class GroupedInputsPickerDialogViewModel : ObservableObject
{
    private readonly PlotDataModel _data;
    private readonly IGroupedDataIndexer _indexer;
    private readonly ApplicationSettings _settings;
    private bool _suspendValidation;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectionSummary))]
    private int _selectedCount;

    [ObservableProperty]
    private string? _selectedXColumn;

    [ObservableProperty]
    private string? _selectedYColumn;

    [ObservableProperty]
    private bool _isValid;

    [ObservableProperty]
    private string? _validationMessage;

    public ObservableCollection<GroupedInputCandidate> Candidates { get; } = new();

    public ObservableCollection<string> AvailableXColumns { get; } = new();

    public ObservableCollection<string> AvailableYColumns { get; } = new();

    public int MaxInputs => _settings.GroupedPlotMaxInputs;

    public string SelectionSummary => $"{SelectedCount} of {MaxInputs} selected";

    public GroupedInputsPickerDialogViewModel(
        PlotDataModel data,
        IGroupedDataIndexer indexer,
        ApplicationSettings settings,
        GroupedPlotConfig? existing = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _suspendValidation = true;
        BuildCandidates(existing);
        _suspendValidation = false;

        SelectedXColumn = existing?.XAxisColumn;
        SelectedYColumn = existing?.YAxisColumn;
        RecomputeAvailableColumns();
        Validate();
    }

    /// <summary>Build a <see cref="GroupedPlotConfig"/> from the current dialog state.</summary>
    public GroupedPlotConfig BuildConfig(GroupedPlotConfig? existing = null)
    {
        var cfg = existing ?? new GroupedPlotConfig();
        cfg.Inputs = Candidates
            .Where(c => c.IsSelected)
            .Select(c => new GroupedInputParameter
            {
                ColumnName = c.ColumnName,
                DisplayLabel = string.IsNullOrWhiteSpace(c.DisplayLabel) ? c.ColumnName : c.DisplayLabel.Trim(),
                UnitSuffix = string.IsNullOrWhiteSpace(c.UnitSuffix) ? null : c.UnitSuffix,
                Format = string.IsNullOrWhiteSpace(c.Format) ? null : c.Format.Trim(),
                SelectedValue = existing?.Inputs.FirstOrDefault(i => i.ColumnName == c.ColumnName)?.SelectedValue,
            })
            .ToList();
        cfg.XAxisColumn = SelectedXColumn;
        cfg.YAxisColumn = SelectedYColumn;
        return cfg;
    }

    partial void OnSelectedXColumnChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value) && value == SelectedYColumn)
            SelectedYColumn = null;
        RecomputeAvailableColumns();
        Validate();
    }

    partial void OnSelectedYColumnChanged(string? value) => Validate();

    private void BuildCandidates(GroupedPlotConfig? existing)
    {
        var preselected = existing?.Inputs.ToDictionary(i => i.ColumnName, StringComparer.Ordinal)
                          ?? new Dictionary<string, GroupedInputParameter>(StringComparer.Ordinal);

        foreach (var col in _data.ColumnNames)
        {
            var distinct = _indexer.GetDistinctValues(_data, col, out var capped);
            var candidate = new GroupedInputCandidate(this)
            {
                ColumnName = col,
                DistinctCount = distinct.Length,
                Ineligible = capped,
                IneligibleReason = capped ? $">{_settings.GroupedPlotMaxDistinctValues} distinct values" : null,
            };
            if (preselected.TryGetValue(col, out var prior))
            {
                candidate.DisplayLabel = prior.DisplayLabel;
                candidate.UnitSuffix = prior.UnitSuffix ?? string.Empty;
                candidate.Format = prior.Format ?? string.Empty;
                candidate.IsSelected = true;
            }
            else
            {
                candidate.DisplayLabel = col;
            }
            Candidates.Add(candidate);
        }
        SelectedCount = Candidates.Count(c => c.IsSelected);
    }

    internal void OnCandidateToggled(GroupedInputCandidate candidate, bool nowSelected)
    {
        if (nowSelected && SelectedCount >= MaxInputs)
        {
            // Reject the toggle — caller already set IsSelected; revert.
            candidate.RevertSelectionSilently(false);
            return;
        }
        if (nowSelected && candidate.Ineligible)
        {
            candidate.RevertSelectionSilently(false);
            return;
        }
        SelectedCount = Candidates.Count(c => c.IsSelected);
        RecomputeAvailableColumns();
        Validate();
    }

    private void RecomputeAvailableColumns()
    {
        var inputs = new HashSet<string>(
            Candidates.Where(c => c.IsSelected).Select(c => c.ColumnName),
            StringComparer.Ordinal);

        AvailableXColumns.Clear();
        AvailableYColumns.Clear();
        foreach (var col in _data.ColumnNames)
        {
            if (inputs.Contains(col)) continue;
            AvailableXColumns.Add(col);
            if (!string.Equals(col, SelectedXColumn, StringComparison.Ordinal))
                AvailableYColumns.Add(col);
        }

        // Drop stale selections that are now in the input set.
        if (SelectedXColumn is not null && inputs.Contains(SelectedXColumn)) SelectedXColumn = null;
        if (SelectedYColumn is not null && inputs.Contains(SelectedYColumn)) SelectedYColumn = null;
    }

    private void Validate()
    {
        if (_suspendValidation) return;
        if (SelectedCount == 0)
        {
            IsValid = false;
            ValidationMessage = "Pick at least one input column.";
            return;
        }
        if (string.IsNullOrEmpty(SelectedXColumn))
        {
            IsValid = false;
            ValidationMessage = "Pick an X-axis column.";
            return;
        }
        if (string.IsNullOrEmpty(SelectedYColumn))
        {
            IsValid = false;
            ValidationMessage = "Pick a Y-axis column.";
            return;
        }
        if (SelectedXColumn == SelectedYColumn)
        {
            IsValid = false;
            ValidationMessage = "X and Y must be different columns.";
            return;
        }
        IsValid = true;
        ValidationMessage = null;
    }
}

/// <summary>One row in the candidate list of the inputs-picker dialog.</summary>
public sealed partial class GroupedInputCandidate : ObservableObject
{
    private readonly GroupedInputsPickerDialogViewModel _parent;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private string _columnName = string.Empty;

    [ObservableProperty]
    private string _displayLabel = string.Empty;

    [ObservableProperty]
    private string _unitSuffix = string.Empty;

    [ObservableProperty]
    private string _format = string.Empty;

    [ObservableProperty]
    private int _distinctCount;

    [ObservableProperty]
    private bool _ineligible;

    [ObservableProperty]
    private string? _ineligibleReason;

    private bool _suppressNotify;

    internal GroupedInputCandidate(GroupedInputsPickerDialogViewModel parent)
    {
        _parent = parent;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (_suppressNotify) return;
        _parent.OnCandidateToggled(this, value);
    }

    internal void RevertSelectionSilently(bool value)
    {
        _suppressNotify = true;
        try { IsSelected = value; } finally { _suppressNotify = false; }
    }
}
