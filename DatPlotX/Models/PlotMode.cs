namespace DatPlotX.Models;

/// <summary>
/// Plot surface style for a project. Chosen at project creation; locked for the life of the project.
/// To switch styles the user must start a new project and re-import the CSV.
/// </summary>
public enum PlotMode
{
    /// <summary>Multi-pane ScottPlot stripchart (default, legacy DatPlot.Modern parity).</summary>
    Panes = 0,

    /// <summary>Single-surface OxyPlot FDA-style stack with multiple banded Y axes (one per curve).</summary>
    Compact = 1,

    /// <summary>Single ScottPlot surface that plots one line per unique combination of selected
    /// input-parameter values. Intended for tabular/array-style data where each row is one
    /// experimental point (e.g. parametric performance arrays).</summary>
    Grouped = 2,
}
