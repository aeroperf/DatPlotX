namespace DatPlotX.Models;

/// <summary>
/// Payload passed from the stacked-mode "Add Curves" dialog to its caller each time the
/// user clicks "Plot Curve". Replaces the previous positional <c>Action&lt;string,string&gt;</c>
/// callback to make the call site self-documenting and to allow future fields (colour,
/// marker, etc.) without breaking signatures.
/// </summary>
/// <param name="ParameterName">The Y-axis parameter (column name) the user selected.</param>
/// <param name="YAxis">The Y-axis target — "Y1" (left) or "Y2" (right).</param>
/// <param name="Unit">The unit for this curve (auto-parsed from the header, user-editable). Null/blank = unknown.</param>
public sealed record AddCurveRequest(string ParameterName, string YAxis, string? Unit = null);
