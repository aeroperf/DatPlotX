using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;
using System.Collections.ObjectModel;
using System.Globalization;

namespace DatPlotX.ViewModels;

/// <summary>
/// One dropdown choice for a Grouped Plot input: the raw value plus its formatted display string.
/// <see cref="Value"/> is <c>null</c> for the "All" sentinel. Keeping the raw double avoids ever
/// re-parsing the (possibly lossy) formatted text back into a value — see review C1.
/// </summary>
public sealed record GroupedInputOption(double? Value, string Display)
{
    public bool IsAll => Value is null;

    // ComboBox renders items via ToString when no DisplayMemberBinding is set.
    public override string ToString() => Display;
}

/// <summary>
/// Sidebar row for one Grouped Plot input parameter — label + dropdown of distinct values
/// (with an "All" sentinel at the bottom). Property changes propagate to the parent
/// <see cref="GroupedPlotViewModel"/> via the <see cref="SelectionChanged"/> event.
/// </summary>
public sealed partial class GroupedInputParameterViewModel : ObservableObject
{
    /// <summary>Display text shown in the dropdown for the "All" choice.</summary>
    public const string AllSentinel = "All";

    private readonly GroupedInputParameter _model;

    [ObservableProperty]
    private GroupedInputOption _selectedOption;

    public string ColumnName => _model.ColumnName;

    public string DisplayLabel => string.IsNullOrEmpty(_model.DisplayLabel) ? _model.ColumnName : _model.DisplayLabel;

    public string? UnitSuffix => _model.UnitSuffix;

    public string? Format => _model.Format;

    public ObservableCollection<GroupedInputOption> Options { get; } = new();

    /// <summary>Raised after <see cref="SelectedOption"/> changes so the parent VM can rebuild.</summary>
    public event EventHandler? SelectionChanged;

    public GroupedInputParameterViewModel(GroupedInputParameter model, IReadOnlyList<double> distinctValues)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(distinctValues);
        _model = model;
        SetDistinctValues(distinctValues);
        // Restore the previous selection by comparing raw doubles (no re-parse of display text).
        var allOption = Options[^1]; // "All" sentinel is always last (see SetDistinctValues)
        _selectedOption = allOption;
        if (_model.SelectedValue is double v)
        {
            var match = Options.FirstOrDefault(o => o.Value is double d && ValuesEqual(d, v));
            if (match is not null)
                _selectedOption = match;
            else
                _model.SelectedValue = null; // stale selection no longer present in the data
        }
    }

    public GroupedInputParameter Model => _model;

    /// <summary>
    /// Recompute the dropdown options from a fresh distinct-value set. Called when the user
    /// reconfigures inputs or imports new data.
    /// </summary>
    public void SetDistinctValues(IReadOnlyList<double> distinctValues)
    {
        Options.Clear();
        foreach (var v in distinctValues)
            Options.Add(new GroupedInputOption(v, FormatValue(v)));
        Options.Add(new GroupedInputOption(null, AllSentinel));
    }

    public string FormatValue(double value)
    {
        var body = string.IsNullOrEmpty(Format)
            ? value.ToString(CultureInfo.InvariantCulture)
            : value.ToString(Format, CultureInfo.InvariantCulture);
        return string.IsNullOrEmpty(UnitSuffix) ? body : body + UnitSuffix;
    }

    partial void OnSelectedOptionChanged(GroupedInputOption value)
    {
        // Bind the raw value straight through — no parsing of the formatted display string.
        _model.SelectedValue = value?.Value;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    // Match the indexer's relative tolerance so restore survives large-magnitude values.
    private static bool ValuesEqual(double a, double b)
    {
        var scale = Math.Max(Math.Max(Math.Abs(a), Math.Abs(b)), 1.0);
        return Math.Abs(a - b) <= 1e-9 * scale;
    }
}
