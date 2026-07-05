using DatPlotX.Models;

namespace DatPlotX.Services;

/// <summary>
/// Builds the discrete-value index for input columns and projects the rows of a
/// <see cref="PlotDataModel"/> into the line set drawn by the Grouped Parameter Plot.
/// </summary>
public interface IGroupedDataIndexer
{
    /// <summary>
    /// Returns the sorted, epsilon-deduplicated set of distinct values in the given column.
    /// Caps the result at <see cref="ApplicationSettings.GroupedPlotMaxDistinctValues"/> entries;
    /// if the cap is reached, <paramref name="capped"/> is set to true and the caller should
    /// reject the column as an input candidate (likely a continuous-value column).
    /// </summary>
    double[] GetDistinctValues(PlotDataModel data, string columnName, out bool capped);

    /// <summary>
    /// Project rows into one line per surviving group. Inputs with a concrete
    /// <see cref="GroupedInputParameter.SelectedValue"/> filter rows; inputs with null select
    /// (the "All" sentinel) become partition keys. Returns up to
    /// <see cref="ApplicationSettings.GroupedPlotMaxLines"/> series in label order; the
    /// projection's <see cref="GroupedPlotProjection.Truncated"/> flag is true if more groups
    /// existed than the cap allowed.
    /// </summary>
    GroupedPlotProjection Project(PlotDataModel data, GroupedPlotConfig config);
}
