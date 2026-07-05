namespace DatPlotX.Models;

/// <summary>
/// Runtime-only descriptor for one line on the Grouped Parameter Plot. Produced by
/// <see cref="DatPlotX.Services.GroupedDataIndexer"/> and consumed by the view to render
/// ScottPlot scatters. Not persisted — the line set is recomputed on every config change.
/// </summary>
public sealed record GroupedPlotSeries(string Label, double[] X, double[] Y);

/// <summary>
/// Result of one indexing pass. <see cref="Truncated"/> is true when the configured input
/// combination produced more lines than <see cref="ApplicationSettings.GroupedPlotMaxLines"/>;
/// the view shows a warning bar in that case.
/// </summary>
public sealed record GroupedPlotProjection(IReadOnlyList<GroupedPlotSeries> Series, bool Truncated, int TotalGroupCount);
