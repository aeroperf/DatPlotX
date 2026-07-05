using DatPlotX.Models;
using ScottPlot;
using ScottPlot.Plottables;

namespace DatPlotX.ViewModels.PlotPane;

/// <summary>
/// Manager for curve lifecycle on plot panes.
/// Handles adding, removing, updating, and querying curves with both Signal and Scatter types.
/// </summary>
public class PlotPaneCurveManager : IPlotPaneCurveManager
{
    private readonly Func<Plot?> _getPlot;
    private readonly Func<PlotPaneModel> _getModel;
    private readonly Dictionary<Guid, PlottedCurveInfo> _curves; // Shared reference
    private readonly Action _triggerUpdate;
    private readonly Func<bool> _getShowLegend;
    private readonly Action _showLegendWithFormatting;

    /// <inheritdoc />
    public event Action? OnAxisLabelsNeedUpdate;

    /// <summary>
    /// Constructor with dependencies
    /// </summary>
    /// <param name="getPlot">Function to get current Plot reference (deferred access)</param>
    /// <param name="getModel">Function to get current PlotPaneModel reference</param>
    /// <param name="curves">Shared reference to curves dictionary</param>
    /// <param name="triggerUpdate">Callback to trigger plot update event</param>
    /// <param name="getShowLegend">Function to get ShowLegend property</param>
    /// <param name="showLegendWithFormatting">Callback to show legend with formatting</param>
    public PlotPaneCurveManager(
        Func<Plot?> getPlot,
        Func<PlotPaneModel> getModel,
        Dictionary<Guid, PlottedCurveInfo> curves,
        Action triggerUpdate,
        Func<bool> getShowLegend,
        Action showLegendWithFormatting)
    {
        _getPlot = getPlot;
        _getModel = getModel;
        _curves = curves;
        _triggerUpdate = triggerUpdate;
        _getShowLegend = getShowLegend;
        _showLegendWithFormatting = showLegendWithFormatting;
    }

    /// <inheritdoc />
    public void AddCurve(double[] data, double samplePeriod, CurveConfigurationModel config)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        var paneModel = _getModel();

        // Ensure PaneIndex is set correctly
        config.PaneIndex = paneModel.Index;

        var signal = plotModel.Add.Signal(data, samplePeriod);
        signal.Color = ScottPlot.Color.FromHex(config.Color);
        signal.LegendText = config.CurveName;
        signal.LineWidth = (float)config.LineWidth;

        // Apply line style
        ApplyLineStyle(signal, config.LineStyle);

        // Set visibility
        signal.IsVisible = config.IsVisible;

        // Set Y-axis for the curve
        if (config.YAxis == YAxisType.Y2)
        {
            signal.Axes.YAxis = plotModel.Axes.Right;
            paneModel.ShowY2Axis = true; // Automatically enable Y2 axis when a curve is added to it
        }
        else
        {
            signal.Axes.YAxis = plotModel.Axes.Left;
        }

        _curves[config.Id] = new PlottedCurveInfo(signal, samplePeriod, data, config);

        // Notify that axis labels need update
        OnAxisLabelsNeedUpdate?.Invoke();

        if (_getShowLegend())
        {
            _showLegendWithFormatting();
        }

        _triggerUpdate();
    }

    /// <inheritdoc />
    public void AddCurve(double[] data, double samplePeriod, string curveName, string color, double lineWidth = 2.0, YAxisType yAxis = YAxisType.Y1)
    {
        var config = new CurveConfigurationModel
        {
            CurveName = curveName,
            Color = color,
            LineWidth = lineWidth,
            YAxis = yAxis,
            LineStyle = Models.LineStyle.Solid,
            IsVisible = true
        };

        AddCurve(data, samplePeriod, config);
    }

    /// <inheritdoc />
    public void AddScatterCurve(double[] xData, double[] yData, CurveConfigurationModel config)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        var paneModel = _getModel();

        // Ensure PaneIndex is set correctly
        config.PaneIndex = paneModel.Index;

        var scatter = plotModel.Add.Scatter(xData, yData);
        scatter.Color = ScottPlot.Color.FromHex(config.Color);
        scatter.LegendText = config.CurveName;

        // Apply line settings
        if (config.ShowLine)
        {
            scatter.LineWidth = (float)config.LineWidth;
            ApplyLineStyle(scatter, config.LineStyle);
        }
        else
        {
            scatter.LineWidth = 0; // Hide line
        }

        // Apply marker settings
        if (config.ShowMarkers)
        {
            scatter.MarkerSize = (float)config.MarkerSize;
            scatter.MarkerShape = GetMarkerShape(config.MarkerStyle);
            scatter.MarkerFillColor = ScottPlot.Color.FromHex(config.MarkerColor);
            scatter.MarkerLineColor = ScottPlot.Color.FromHex(config.MarkerColor);
        }
        else
        {
            scatter.MarkerSize = 0; // Hide markers
        }

        // Set visibility
        scatter.IsVisible = config.IsVisible;

        // Set Y-axis for the curve
        if (config.YAxis == YAxisType.Y2)
        {
            scatter.Axes.YAxis = plotModel.Axes.Right;
            paneModel.ShowY2Axis = true; // Automatically enable Y2 axis when a curve is added to it
        }
        else
        {
            scatter.Axes.YAxis = plotModel.Axes.Left;
        }

        // Store curve with its configuration. Scatter has no sample period (store 0) but
        // carries its real X array so analysis can slice the correct X-window.
        _curves[config.Id] = new PlottedCurveInfo(scatter, 0, yData, config) { XData = xData };

        // Notify that axis labels need update
        OnAxisLabelsNeedUpdate?.Invoke();

        if (_getShowLegend())
        {
            _showLegendWithFormatting();
        }

        _triggerUpdate();
    }

    /// <inheritdoc />
    public void AddScatterCurve(double[] xData, double[] yData, string curveName, string color, double lineWidth = 2.0, YAxisType yAxis = YAxisType.Y1)
    {
        var config = new CurveConfigurationModel
        {
            CurveName = curveName,
            Color = color,
            LineWidth = lineWidth,
            YAxis = yAxis,
            LineStyle = Models.LineStyle.Solid,
            IsVisible = true
        };

        AddScatterCurve(xData, yData, config);
    }

    /// <inheritdoc />
    public bool RemoveCurve(Guid curveId)
    {
        var plotModel = _getPlot();
        if (plotModel == null || !_curves.TryGetValue(curveId, out var curveInfo))
            return false;

        var (plottable, _, _, _) = curveInfo;

        // Remove from plot
        if (plottable != null)
        {
            if (plottable is Signal signal)
            {
                plotModel.Remove(signal);
            }
            else if (plottable is ScottPlot.Plottables.Scatter scatter)
            {
                plotModel.Remove(scatter);
            }
        }

        _curves.Remove(curveId);

        // Notify that axis labels need update
        OnAxisLabelsNeedUpdate?.Invoke();

        // Update legend (preserve user's configured placement)
        if (_getShowLegend() && _curves.Count > 0)
        {
            _showLegendWithFormatting();
        }

        _triggerUpdate();
        return true;
    }

    /// <inheritdoc />
    public void ClearCurves()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
        {
            _curves.Clear();
            return;
        }

        // Remove only the curve plottables — event lines, event-line labels, callouts, and
        // text/arrow annotations are owned elsewhere and must survive a "Clear Pane" (which
        // promises to remove curves only). Using PlotModel.Clear() here detached those plottables
        // while their owning dictionaries kept dangling references (reviews M2 / C4).
        foreach (var curveInfo in _curves.Values)
        {
            var (plottable, _, _, _) = curveInfo;
            if (plottable is Signal signal)
                plotModel.Remove(signal);
            else if (plottable is ScottPlot.Plottables.Scatter scatter)
                plotModel.Remove(scatter);
        }
        _curves.Clear();

        OnAxisLabelsNeedUpdate?.Invoke();
        _triggerUpdate();
    }

    /// <inheritdoc />
    public bool ToggleCurveVisibility(Guid curveId)
    {
        if (!_curves.TryGetValue(curveId, out var curveInfo))
            return false;

        var (plottable, period, data, config) = curveInfo;
        config.IsVisible = !config.IsVisible;

        // Update visibility for both Signal and Scatter plottables
        if (plottable != null)
        {
            if (plottable is Signal signal)
            {
                signal.IsVisible = config.IsVisible;
            }
            else if (plottable is ScottPlot.Plottables.Scatter scatter)
            {
                scatter.IsVisible = config.IsVisible;
            }
        }

        _triggerUpdate();
        return true;
    }

    /// <inheritdoc />
    public bool SetCurveVisibility(Guid curveId, bool isVisible)
    {
        if (!_curves.TryGetValue(curveId, out var curveInfo))
            return false;

        var (plottable, _, _, config) = curveInfo;
        if (config.IsVisible == isVisible)
            return true;

        config.IsVisible = isVisible;
        if (plottable is Signal signal)
        {
            signal.IsVisible = isVisible;
        }
        else if (plottable is ScottPlot.Plottables.Scatter scatter)
        {
            scatter.IsVisible = isVisible;
        }

        _triggerUpdate();
        return true;
    }

    /// <inheritdoc />
    public bool UpdateCurveFormat(CurveConfigurationModel updatedConfig)
    {
        if (!_curves.TryGetValue(updatedConfig.Id, out var curveInfo))
            return false;

        var (plottable, period, data, config) = curveInfo;

        // Update config with new values
        config.CurveName = updatedConfig.CurveName;
        config.Unit = updatedConfig.Unit;
        config.Color = updatedConfig.Color;
        config.LineStyle = updatedConfig.LineStyle;
        config.LineWidth = updatedConfig.LineWidth;
        config.ShowLine = updatedConfig.ShowLine;
        config.ShowMarkers = updatedConfig.ShowMarkers;
        config.MarkerStyle = updatedConfig.MarkerStyle;
        config.MarkerSize = updatedConfig.MarkerSize;
        config.MarkerColor = updatedConfig.MarkerColor;

        // Handle both Signal and Scatter plottables
        if (plottable is Signal signal)
        {
            signal.LegendText = config.CurveName;
            signal.Color = ScottPlot.Color.FromHex(config.Color);
            signal.LineWidth = (float)config.LineWidth;
            ApplyLineStyle(signal, config.LineStyle);
            // Note: Signals don't support markers in ScottPlot
        }
        else if (plottable is ScottPlot.Plottables.Scatter scatter)
        {
            scatter.LegendText = config.CurveName;
            scatter.Color = ScottPlot.Color.FromHex(config.Color);

            // Apply line settings
            if (config.ShowLine)
            {
                scatter.LineWidth = (float)config.LineWidth;
                ApplyLineStyle(scatter, config.LineStyle);
            }
            else
            {
                scatter.LineWidth = 0;
            }

            // Apply marker settings
            if (config.ShowMarkers)
            {
                scatter.MarkerSize = (float)config.MarkerSize;
                scatter.MarkerShape = GetMarkerShape(config.MarkerStyle);
                scatter.MarkerFillColor = ScottPlot.Color.FromHex(config.MarkerColor);
                scatter.MarkerLineColor = ScottPlot.Color.FromHex(config.MarkerColor);
            }
            else
            {
                scatter.MarkerSize = 0;
            }
        }

        // A rename / unit edit changes legend text and the Y-axis label — rebuild both.
        if (_getShowLegend())
            _showLegendWithFormatting();
        OnAxisLabelsNeedUpdate?.Invoke();

        _triggerUpdate();
        return true;
    }

    /// <inheritdoc />
    public CurveConfigurationModel? GetCurveConfig(Guid curveId)
    {
        return _curves.TryGetValue(curveId, out var info) ? info.Config : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<CurveConfigurationModel> GetAllCurveConfigs()
    {
        return _curves.Values.Select(c => c.Config).ToList().AsReadOnly();
    }

    public IReadOnlyList<PlottedCurveInfo> GetPlottedCurves()
    {
        return _curves.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public double GetCurveYValueAtX(Guid curveId, double x)
    {
        if (!_curves.TryGetValue(curveId, out var curveInfo))
            return double.NaN;

        var (plottable, period, data, config) = curveInfo;

        if (data == null || data.Length == 0)
            return double.NaN;

        // For Signal plots with a known sample period
        if (period > 0)
        {
            int index = (int)(x / period);

            // Bounds checking
            if (index < 0 || index >= data.Length)
                return double.NaN;

            return data[index];
        }
        else if (plottable is ScottPlot.Plottables.Scatter scatter)
        {
            return InterpolateScatterYValue(scatter, x);
        }

        return double.NaN;
    }

    /// <inheritdoc />
    public (Guid CurveId, YAxisType YAxis, double Distance)? GetClosestCurveAt(double x, double y1, double y2)
    {
        var plotModel = _getPlot();
        if (plotModel == null || _curves.Count == 0)
            return null;

        Guid? closestCurveId = null;
        YAxisType closestYAxis = YAxisType.Y1;
        double minDistance = double.MaxValue;

        foreach (var kvp in _curves)
        {
            var curveId = kvp.Key;
            var (plottable, period, data, config) = kvp.Value;

            if (!config.IsVisible)
                continue;

            double curveY = GetCurveYValueAtX(curveId, x);

            if (double.IsNaN(curveY))
                continue;

            // Use the appropriate Y coordinate based on which axis this curve is on
            double mouseY = config.YAxis == YAxisType.Y1 ? y1 : y2;

            // Calculate distance from mouse cursor to curve point
            double distance = Math.Abs(mouseY - curveY);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestCurveId = curveId;
                closestYAxis = config.YAxis;
            }
        }

        if (closestCurveId.HasValue)
        {
            return (closestCurveId.Value, closestYAxis, minDistance);
        }

        return null;
    }

    /// <inheritdoc />
    public List<(CurveConfigurationModel Config, double YValue)> GetCurveValuesAtX(double xPosition)
    {
        var results = new List<(CurveConfigurationModel Config, double YValue)>(_curves.Count);

        foreach (var curveInfo in _curves.Values)
        {
            var config = curveInfo.Config;
            if (!config.IsVisible) continue;

            double yValue;
            if (curveInfo.Plottable is Signal && curveInfo.Period > 0)
            {
                yValue = InterpolateYValue(curveInfo.Period, curveInfo.Data, xPosition);
            }
            else if (curveInfo.Plottable is ScottPlot.Plottables.Scatter scatter)
            {
                yValue = InterpolateScatterYValue(scatter, xPosition);
            }
            else
            {
                continue;
            }

            if (!double.IsNaN(yValue))
                results.Add((config, yValue));
        }

        return results;
    }

    /// <inheritdoc />
    public IReadOnlyList<(Signal Signal, double Period, double[] Data, string CurveName)> GetCurves()
    {
        return _curves.Values
            .Where(c => c.Plottable is Signal)
            .Select(c => ((Signal)c.Plottable!, c.Period, c.Data, c.Config.CurveName))
            .ToList()
            .AsReadOnly();
    }

    #region Private Helper Methods

    /// <summary>
    /// Apply line style to a signal
    /// </summary>
    private void ApplyLineStyle(Signal signal, Models.LineStyle style)
    {
        switch (style)
        {
            case Models.LineStyle.Solid:
                signal.LinePattern = ScottPlot.LinePattern.Solid;
                break;
            case Models.LineStyle.Dash:
                signal.LinePattern = ScottPlot.LinePattern.Dashed;
                break;
            case Models.LineStyle.Dot:
                signal.LinePattern = ScottPlot.LinePattern.Dotted;
                break;
            case Models.LineStyle.DashDot:
                // ScottPlot doesn't have a built-in DashDot pattern, use Dashed as fallback
                signal.LinePattern = ScottPlot.LinePattern.Dashed;
                break;
        }
    }

    /// <summary>
    /// Apply line style to a scatter plot
    /// </summary>
    private void ApplyLineStyle(ScottPlot.Plottables.Scatter scatter, Models.LineStyle style)
    {
        switch (style)
        {
            case Models.LineStyle.Solid:
                scatter.LinePattern = ScottPlot.LinePattern.Solid;
                break;
            case Models.LineStyle.Dash:
                scatter.LinePattern = ScottPlot.LinePattern.Dashed;
                break;
            case Models.LineStyle.Dot:
                scatter.LinePattern = ScottPlot.LinePattern.Dotted;
                break;
            case Models.LineStyle.DashDot:
                // ScottPlot doesn't have a built-in DashDot pattern, use Dashed as fallback
                scatter.LinePattern = ScottPlot.LinePattern.Dashed;
                break;
        }
    }

    /// <summary>
    /// Map MarkerStyle enum to ScottPlot MarkerShape
    /// </summary>
    private ScottPlot.MarkerShape GetMarkerShape(Models.MarkerStyle style)
    {
        return style switch
        {
            Models.MarkerStyle.None => ScottPlot.MarkerShape.None,
            Models.MarkerStyle.Circle => ScottPlot.MarkerShape.FilledCircle,
            Models.MarkerStyle.Square => ScottPlot.MarkerShape.FilledSquare,
            Models.MarkerStyle.Triangle => ScottPlot.MarkerShape.FilledTriangleUp,
            Models.MarkerStyle.Diamond => ScottPlot.MarkerShape.FilledDiamond,
            Models.MarkerStyle.Cross => ScottPlot.MarkerShape.Cross,
            Models.MarkerStyle.Plus => ScottPlot.MarkerShape.OpenCircle, // ScottPlot 5.x doesn't have Plus, use OpenCircle
            _ => ScottPlot.MarkerShape.FilledCircle
        };
    }

    /// <summary>
    /// Interpolate Y value from data array at a given X position
    /// </summary>
    private double InterpolateYValue(double period, double[] data, double xPosition)
    {
        // Calculate the data index from X position using the stored sample period
        int index = (int)(xPosition / period);

        // Bounds checking
        if (index < 0)
            index = 0;
        if (index >= data.Length)
            index = data.Length - 1;

        // Simple nearest-neighbor interpolation
        return data[index];
    }

    /// <summary>
    /// Interpolate Y value from a Scatter plottable at a given X position
    /// </summary>
    private double InterpolateScatterYValue(ScottPlot.Plottables.Scatter scatter, double xPosition)
    {
        var coordinates = scatter.Data.GetScatterPoints();
        if (coordinates == null) return double.NaN;

        var points = coordinates as IReadOnlyList<ScottPlot.Coordinates> ?? coordinates.ToArray();
        int count = points.Count;
        if (count == 0) return double.NaN;

        if (xPosition <= points[0].X) return points[0].Y;
        if (xPosition >= points[count - 1].X) return points[count - 1].Y;

        // Binary search for the rightmost point with X <= xPosition (data assumed X-sorted)
        int lo = 0, hi = count - 1;
        while (lo < hi)
        {
            int mid = lo + (hi - lo + 1) / 2;
            if (points[mid].X <= xPosition) lo = mid;
            else hi = mid - 1;
        }

        var p1 = points[lo];
        if (p1.X == xPosition || lo + 1 >= count) return p1.Y;
        var p2 = points[lo + 1];
        if (p2.X == p1.X) return p1.Y;

        double t = (xPosition - p1.X) / (p2.X - p1.X);
        return p1.Y + t * (p2.Y - p1.Y);
    }

    #endregion
}
