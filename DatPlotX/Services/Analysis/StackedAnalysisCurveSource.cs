using DatPlotX.Models;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// <see cref="IAnalysisCurveSource"/> backed by Stacked-mode panes. Aggregates curves across
/// all panes into a single flat view for the Analysis service. Visibility / range come from
/// the panes' shared X-axis.
/// </summary>
/// <remarks>
/// MainWindowViewModel constructs one of these whenever the project enters Stacked mode and
/// passes it to <see cref="IAnalysisService.SetSource"/>. The view-model raises
/// <see cref="IAnalysisCurveSource.CurvesChanged"/> when its <see cref="ObservableCollection{T}.CollectionChanged"/>
/// fires; <see cref="IAnalysisCurveSource.VisibleRangeChanged"/> piggybacks on the existing
/// X-axis-sync hook.
/// </remarks>
public sealed class StackedAnalysisCurveSource : IAnalysisCurveSource, IDisposable
{
    private readonly ObservableCollection<PlotPaneViewModel> _panes;
    private readonly Func<string?> _getXUnit;

    public StackedAnalysisCurveSource(ObservableCollection<PlotPaneViewModel> panes, Func<string?> getXUnit)
    {
        _panes = panes;
        _getXUnit = getXUnit;
        _panes.CollectionChanged += OnPanesCollectionChanged;
    }

    public string? XUnit => _getXUnit();

    public (double XMin, double XMax) VisibleXRange
    {
        get
        {
            var plot = _panes.FirstOrDefault()?.PlotModel;
            if (plot is null) return (0, 0);
            var range = plot.Axes.Bottom.Range;
            return (range.Min, range.Max);
        }
    }

    public (double XMin, double XMax) FullDataXRange
    {
        get
        {
            double min = double.PositiveInfinity, max = double.NegativeInfinity;
            bool any = false;
            foreach (var pane in _panes)
            {
                foreach (var info in pane.GetPlottedCurves())
                {
                    if (info.Data.Length == 0) continue;

                    double localMin, localMax;
                    if (info.Period > 0)
                    {
                        // Signal: X[i] = i × Period, anchored at 0.
                        localMin = 0;
                        localMax = (info.Data.Length - 1) * info.Period;
                    }
                    else if (info.XData is { } x && x.Length > 0)
                    {
                        // Scatter: real X array (assumed sorted ascending, as plotted). A NaN/Inf
                        // endpoint would poison the global min/max, so fall back to index range.
                        if (double.IsFinite(x[0]) && double.IsFinite(x[^1]))
                        {
                            localMin = x[0];
                            localMax = x[^1];
                        }
                        else
                        {
                            localMin = 0;
                            localMax = info.Data.Length - 1;
                        }
                    }
                    else
                    {
                        localMin = 0;
                        localMax = info.Data.Length - 1;
                    }

                    if (localMin < min) min = localMin;
                    if (localMax > max) max = localMax;
                    any = true;
                }
            }
            return any ? (min, max) : (0, 0);
        }
    }

    public event EventHandler? CurvesChanged;
    public event EventHandler? VisibleRangeChanged;

    /// <summary>
    /// Called by <see cref="MainWindowViewModel"/> when the Stacked plot redraws — proxies
    /// to <see cref="VisibleRangeChanged"/> so the Analysis service can recompute the
    /// visible-window segment.
    /// </summary>
    public void NotifyVisibleRangeChanged()
        => VisibleRangeChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Called by <see cref="MainWindowViewModel"/> when curves are added / removed on any pane.
    /// </summary>
    public void NotifyCurvesChanged()
        => CurvesChanged?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<AnalysisCurveDescriptor> ListCurves()
    {
        var list = new List<AnalysisCurveDescriptor>();
        foreach (var pane in _panes)
        {
            foreach (var info in pane.GetPlottedCurves())
            {
                var cfg = info.Config;
                list.Add(new AnalysisCurveDescriptor(
                    CurveId: cfg.Id.ToString(),
                    DisplayName: string.IsNullOrEmpty(cfg.CurveName) ? cfg.YColumnName : cfg.CurveName,
                    ColorHex: cfg.Color,
                    Unit: cfg.Unit,
                    IsVisible: cfg.IsVisible));
            }
        }
        return list;
    }

    public AnalysisCurveData? GetData(string curveId)
    {
        if (!Guid.TryParse(curveId, out var guid)) return null;
        foreach (var pane in _panes)
        {
            foreach (var info in pane.GetPlottedCurves())
            {
                if (info.Config.Id == guid)
                    return BuildData(curveId, info);
            }
        }
        return null;
    }

    private static AnalysisCurveData BuildData(string curveId, PlottedCurveInfo info)
        => BuildData(curveId, info.Period, info.Data, info.XData);

    /// <summary>
    /// Chooses the right <see cref="AnalysisCurveData"/> shape for a plotted curve. Public for
    /// unit testing — this is the decision that, when wrong, slices the wrong X-window.
    /// </summary>
    public static AnalysisCurveData BuildData(string curveId, double period, double[] yData, double[]? xData)
    {
        // Period > 0 → Signal plot, periodic data (X[i] = i × Period).
        if (period > 0)
            return new AnalysisCurveData(curveId, yData, period);

        // Scatter — use the real X array so the visible-window slice maps to actual X
        // values (e.g. seconds), not array indices. Falls back to index-as-X only if the
        // X array is somehow absent or length-mismatched.
        if (xData is { } x && x.Length == yData.Length && x.Length > 0)
            return new AnalysisCurveData(curveId, x, yData);

        return new AnalysisCurveData(curveId, yData, period: 1.0);
    }

    private void OnPanesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CurvesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _panes.CollectionChanged -= OnPanesCollectionChanged;
    }
}
