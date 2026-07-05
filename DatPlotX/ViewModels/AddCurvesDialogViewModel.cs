using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.ViewModels;

/// <summary>
/// Represents a Y-axis option for the Add Curves dialog
/// </summary>
public class YAxisOption
{
    public string Label { get; set; } = "";
    public string AxisType { get; set; } = "";  // "Y1" or "Y2"
}

/// <summary>
/// ViewModel for the Add Curves dialog.
/// Allows adding one curve at a time by selecting a parameter and Y-axis.
/// </summary>
public partial class AddCurvesDialogViewModel : ObservableObject
{
    private readonly DataTable? _sourceData;
    private readonly string _xColumn;
    private readonly int _targetPaneIndex;

    [ObservableProperty]
    private string? _selectedParameter;

    /// <summary>
    /// Unit for the selected parameter, auto-parsed from the column header and editable before
    /// plotting. Blank = unknown. Shown in the Analysis panel and used to derive rate units.
    /// </summary>
    [ObservableProperty]
    private string _unitText = string.Empty;

    [ObservableProperty]
    private YAxisOption? _selectedYAxis;

    // Auto-fill the unit field from the column header whenever the parameter changes (unless
    // the user has typed a custom unit for the current selection).
    partial void OnSelectedParameterChanged(string? value)
    {
        UnitText = string.IsNullOrEmpty(value)
            ? string.Empty
            : Services.Units.UnitHeaderParser.Parse(value).Unit ?? string.Empty;
    }

    [ObservableProperty]
    private string _filterText = string.Empty;

    private readonly List<string> _allYParameters = new();

    /// <summary>
    /// Available Y parameters (numeric columns excluding the X column), narrowed by <see cref="FilterText"/>.
    /// </summary>
    public ObservableCollection<string> AvailableYParameters { get; } = new();

    /// <summary>
    /// Available Y-axis options (Left Y1, Right Y2)
    /// </summary>
    public ObservableCollection<YAxisOption> AvailableYAxes { get; } = new();

    /// <summary>
    /// Curves that have been plotted during this dialog session
    /// </summary>
    public ObservableCollection<DialogPlottedCurve> PlottedCurves { get; } = new();

    public int TargetPaneIndex => _targetPaneIndex;

    public AddCurvesDialogViewModel(DataTable? sourceData, string xColumn, int targetPaneIndex)
    {
        _sourceData = sourceData;
        _xColumn = xColumn;
        _targetPaneIndex = targetPaneIndex;

        // Populate available Y parameters (numeric columns excluding X)
        if (_sourceData != null)
        {
            foreach (DataColumn column in _sourceData.Columns)
            {
                if ((column.DataType == typeof(double) || column.DataType == typeof(int)) &&
                    column.ColumnName != _xColumn)
                {
                    _allYParameters.Add(column.ColumnName);
                    AvailableYParameters.Add(column.ColumnName);
                }
            }
        }

        // Populate Y-axis options
        AvailableYAxes.Add(new YAxisOption { Label = "Left Y Axis (Y1)", AxisType = "Y1" });
        AvailableYAxes.Add(new YAxisOption { Label = "Right Y Axis (Y2)", AxisType = "Y2" });

        // Set defaults
        if (AvailableYParameters.Count > 0)
            SelectedParameter = AvailableYParameters[0];

        if (AvailableYAxes.Count > 0)
            SelectedYAxis = AvailableYAxes[0];
    }

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = (FilterText ?? string.Empty).Trim();
        var previousSelection = SelectedParameter;

        // Incremental sync: walk the source list and the visible list in lockstep, inserting
        // missing matches and removing stale ones. Avoids the Clear + N-Add storm that
        // re-templates the ComboBox dropdown on every keystroke for 5000-column files.
        int visibleIndex = 0;
        foreach (var name in _allYParameters)
        {
            bool match = query.Length == 0 || name.Contains(query, StringComparison.OrdinalIgnoreCase);
            if (!match) continue;

            if (visibleIndex < AvailableYParameters.Count)
            {
                if (!ReferenceEquals(AvailableYParameters[visibleIndex], name))
                {
                    int existing = AvailableYParameters.IndexOf(name);
                    if (existing > visibleIndex)
                        AvailableYParameters.Move(existing, visibleIndex);
                    else
                        AvailableYParameters.Insert(visibleIndex, name);
                }
            }
            else
            {
                AvailableYParameters.Add(name);
            }
            visibleIndex++;
        }

        while (AvailableYParameters.Count > visibleIndex)
            AvailableYParameters.RemoveAt(AvailableYParameters.Count - 1);

        if (previousSelection is not null && AvailableYParameters.Contains(previousSelection))
            SelectedParameter = previousSelection;
        else if (AvailableYParameters.Count > 0)
            SelectedParameter = AvailableYParameters[0];
        else
            SelectedParameter = null;
    }

    /// <summary>
    /// Track a plotted curve in the dialog's list
    /// </summary>
    public void TrackPlottedCurve()
    {
        if (string.IsNullOrEmpty(SelectedParameter) || SelectedYAxis == null)
            return;

        PlottedCurves.Add(new DialogPlottedCurve
        {
            ParameterName = SelectedParameter,
            YAxisType = SelectedYAxis.AxisType
        });
    }
}

/// <summary>
/// Tracks a curve that was plotted during the dialog session
/// </summary>
public class DialogPlottedCurve
{
    public string ParameterName { get; set; } = "";
    public string YAxisType { get; set; } = "";
}
