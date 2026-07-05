namespace DatPlotX.Models;

/// <summary>
/// Persisted configuration for the Grouped Parameter Plot mode. One per project.
/// Stored on <see cref="ProjectSettingsModel.GroupedPlot"/>; null in non-Grouped projects.
/// </summary>
public class GroupedPlotConfig
{
    /// <summary>Columns the user has marked as "inputs". Up to 8 (see <see cref="ApplicationSettings.GroupedPlotMaxInputs"/>).</summary>
    public List<GroupedInputParameter> Inputs { get; set; } = new();

    /// <summary>Column name supplying X values. Must not be an input column.</summary>
    public string? XAxisColumn { get; set; }

    /// <summary>Column name supplying Y values. Must not be an input column and must differ from X.</summary>
    public string? YAxisColumn { get; set; }

    public bool ShowLegend { get; set; }

    public bool ShowMarkers { get; set; } = true;

    /// <summary>Optional plot title override. Null/empty = auto-generate from locked input values.</summary>
    public string? Title { get; set; }

    public double? XAxisMin { get; set; }
    public double? XAxisMax { get; set; }
    public double? YAxisMin { get; set; }
    public double? YAxisMax { get; set; }
}

/// <summary>
/// One input parameter in a Grouped Parameter Plot. Each input column gets a left-sidebar
/// dropdown of its distinct values plus an "All" sentinel; "All" expands that axis to one
/// line per distinct value (cartesian product across All-mode inputs).
/// </summary>
public class GroupedInputParameter
{
    /// <summary>CSV column name. Must exist in the imported data.</summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>User-facing label shown above the sidebar dropdown. Defaults to <see cref="ColumnName"/>.</summary>
    public string DisplayLabel { get; set; } = string.Empty;

    /// <summary>Optional unit suffix appended to displayed values (e.g. <c>" lbs"</c>, <c>" ft"</c>).</summary>
    public string? UnitSuffix { get; set; }

    /// <summary>Optional <c>double.ToString</c> format (e.g. <c>"N0"</c>, <c>"F1"</c>). Null = invariant default.</summary>
    public string? Format { get; set; }

    /// <summary>Currently selected discrete value, or null for "All" (expand on this axis).</summary>
    public double? SelectedValue { get; set; }
}
