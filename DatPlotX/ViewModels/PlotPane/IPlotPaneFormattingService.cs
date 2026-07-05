namespace DatPlotX.ViewModels.PlotPane;

/// <summary>
/// Interface for plot pane formatting operations (axes, grid, colors, fonts).
/// Manages visual formatting of the plot without handling data or curves.
/// </summary>
public interface IPlotPaneFormattingService
{
    /// <summary>
    /// Apply all formatting settings from the pane model to the plot.
    /// This includes axis labels, fonts, grid settings, tick formatting, and background colors.
    /// </summary>
    void ApplyFormatting();

    /// <summary>
    /// Set the X-axis range
    /// </summary>
    /// <param name="min">Minimum X value</param>
    /// <param name="max">Maximum X value</param>
    void SetXAxisRange(double min, double max);

    /// <summary>
    /// Set the Y-axis range
    /// </summary>
    /// <param name="min">Minimum Y value</param>
    /// <param name="max">Maximum Y value</param>
    void SetYAxisRange(double min, double max);

    /// <summary>
    /// Set the Y2-axis range
    /// </summary>
    /// <param name="min">Minimum Y2 value</param>
    /// <param name="max">Maximum Y2 value</param>
    void SetY2AxisRange(double min, double max);

    /// <summary>
    /// Get the current X-axis range
    /// </summary>
    /// <returns>Tuple of (Min, Max) or null if plot is not initialized</returns>
    (double Min, double Max)? GetXAxisRange();

    /// <summary>
    /// Get the current Y-axis range
    /// </summary>
    /// <returns>Tuple of (Min, Max) or null if plot is not initialized</returns>
    (double Min, double Max)? GetYAxisRange();

    /// <summary>
    /// Get the current Y2-axis range
    /// </summary>
    /// <returns>Tuple of (Min, Max) or null if plot is not initialized</returns>
    (double Min, double Max)? GetY2AxisRange();

    /// <summary>
    /// Show legend with formatting applied from pane model
    /// </summary>
    void ShowLegendWithFormatting();

    /// <summary>
    /// Format X-value according to current axis formatting settings
    /// </summary>
    /// <param name="xValue">The X value to format</param>
    /// <returns>Formatted string representation</returns>
    string FormatXValue(double xValue);
}
