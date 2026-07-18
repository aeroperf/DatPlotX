using DatPlotX.Models;
using DatPlotX.ViewModels;
using OxyPlot.Axes;
using System.Collections.Specialized;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// <see cref="IAnalysisCurveSource"/> backed by the Compact Plot Surface. One curve per
/// <see cref="CompactCurveModel"/>; X data is the project's selected X column. The visible
/// X range comes from the OxyPlot shared X axis.
/// </summary>
public sealed class CompactAnalysisCurveSource : IAnalysisCurveSource, IDisposable
{
    private readonly CompactPlotViewModel _vm;
    private readonly Func<PlotDataModel?> _getData;

    public CompactAnalysisCurveSource(CompactPlotViewModel vm, Func<PlotDataModel?> getData)
    {
        _vm = vm;
        _getData = getData;
        _vm.Curves.CollectionChanged += OnCurvesCollectionChanged;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    public string? XUnit
    {
        get
        {
            var col = _vm.XAxisColumn;
            if (string.IsNullOrEmpty(col)) return null;
            return Units.UnitHeaderParser.Parse(col).Unit;
        }
    }

    public (double XMin, double XMax) VisibleXRange
    {
        get
        {
            var axis = FindXAxis();
            if (axis is null) return FullDataXRange;
            // OxyPlot ActualMinimum/Maximum read the live view range, but only once the model has
            // rendered + auto-scaled. Right after a Rebuild() (e.g. adding an event line) the axis
            // is freshly constructed with no manual range, so Actual* sit at their default 0 until
            // the next render pass — a degenerate [0,0] window that makes every metric compute on an
            // empty slice and blanks the Analysis panel until the user pans. Treat NaN or a
            // collapsed (min >= max) range as "not yet laid out" and fall back to the full data
            // range, matching how the panel behaves in Stacked mode.
            if (double.IsNaN(axis.ActualMinimum) || double.IsNaN(axis.ActualMaximum) ||
                axis.ActualMinimum >= axis.ActualMaximum)
                return FullDataXRange;
            return (axis.ActualMinimum, axis.ActualMaximum);
        }
    }

    public (double XMin, double XMax) FullDataXRange
    {
        get
        {
            var data = _getData();
            if (data is null || string.IsNullOrEmpty(_vm.XAxisColumn)) return (0, 0);
            try
            {
                var x = data.GetColumnData(_vm.XAxisColumn!);
                // Seed from the first finite sample, not x[0]: a leading NaN (sensor dropout at
                // file start) would otherwise poison min/max forever, since every NaN comparison
                // is false, and FullDataXRange feeds the VisibleXRange fallback + the slice bounds.
                bool any = false;
                double min = 0, max = 0;
                foreach (var v in x)
                {
                    if (double.IsNaN(v)) continue;
                    if (!any) { min = max = v; any = true; }
                    else { if (v < min) min = v; if (v > max) max = v; }
                }
                return any ? (min, max) : (0, 0);
            }
            catch { return (0, 0); }
        }
    }

    public event EventHandler? CurvesChanged;
    public event EventHandler? VisibleRangeChanged;

    /// <summary>Called by MainWindowViewModel when the Compact plot pan/zooms.</summary>
    public void NotifyVisibleRangeChanged()
        => VisibleRangeChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Force a curves-changed recompute. The <see cref="CompactPlotViewModel.Curves"/>
    /// collection-changed subscription already covers add / remove / clear, but in-place curve
    /// edits (visibility, rename) mutate a model without raising CollectionChanged — the VM calls
    /// this after such edits so the analysis panel refreshes.</summary>
    public void NotifyCurvesChanged()
        => CurvesChanged?.Invoke(this, EventArgs.Empty);

    public IReadOnlyList<AnalysisCurveDescriptor> ListCurves()
    {
        var list = new List<AnalysisCurveDescriptor>(_vm.Curves.Count);
        foreach (var c in _vm.Curves)
        {
            list.Add(new AnalysisCurveDescriptor(
                CurveId: c.Id.ToString(),
                DisplayName: string.IsNullOrEmpty(c.DisplayName) ? c.SourceColumn : c.DisplayName,
                ColorHex: c.Color,
                Unit: ExtractUnit(c),
                IsVisible: c.IsVisible));
        }
        return list;
    }

    public AnalysisCurveData? GetData(string curveId)
    {
        if (!Guid.TryParse(curveId, out var guid)) return null;
        var curve = _vm.Curves.FirstOrDefault(c => c.Id == guid);
        if (curve is null) return null;

        var data = _getData();
        if (data is null || string.IsNullOrEmpty(_vm.XAxisColumn)) return null;

        try
        {
            var xSrc = data.GetColumnData(_vm.XAxisColumn!);
            var ySrc = data.GetColumnData(curve.SourceColumn);
            int n = Math.Min(xSrc.Length, ySrc.Length);
            if (n == 0) return null;

            // AnalysisCurveData slices the visible window with a binary search and integrates
            // with a trapezoid rule — both require X ascending. Compact plots points in raw
            // file order (no sort, unlike Stacked's as-plotted scatter), and the X column is
            // any user-chosen column, so it may not be monotonic. When X is out of order we must
            // sort the (X, Y) pairs; when it's already ascending (the common time/index case) we
            // can hand the arrays straight through.
            bool needsSort = !IsAscending(xSrc, n);

            double[] x, y;
            if (n < xSrc.Length || n < ySrc.Length || needsSort)
            {
                // Copy before handing off. GetColumnData returns the *cached backing array by
                // reference*, so sorting (or truncating a view of) it in place would corrupt the
                // shared column data for the plot and every other reader. Always work on a copy
                // whenever we'd otherwise touch or re-length the source.
                x = new double[n]; Array.Copy(xSrc, x, n);
                y = new double[n]; Array.Copy(ySrc, y, n);
                if (needsSort) Array.Sort(x, y);
            }
            else
            {
                x = xSrc;
                y = ySrc;
            }

            return new AnalysisCurveData(curveId, x, y);
        }
        catch { return null; }
    }

    private static bool IsAscending(double[] x, int count)
    {
        for (int i = 1; i < count; i++)
        {
            // NaN comparisons are false, so a NaN X here returns false and forces a sort, which
            // corrals NaN keys to the front (Array.Sort orders NaN first) and leaves the finite
            // tail binary-searchable — metrics skip NaN samples regardless.
            if (!(x[i - 1] <= x[i])) return false;
        }
        return true;
    }

    private static string? ExtractUnit(CompactCurveModel c)
    {
        if (!string.IsNullOrEmpty(c.Unit)) return c.Unit;
        return Units.UnitHeaderParser.Parse(c.SourceColumn).Unit;
    }

    private LinearAxis? FindXAxis()
    {
        foreach (var ax in _vm.PlotModel.Axes)
            if (ax is LinearAxis la && la.Key == CompactPlotViewModel.XAxisKey)
                return la;
        return null;
    }

    private void OnCurvesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => CurvesChanged?.Invoke(this, EventArgs.Empty);

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompactPlotViewModel.XAxisColumn) ||
            e.PropertyName == nameof(CompactPlotViewModel.PlotModel))
        {
            CurvesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        _vm.Curves.CollectionChanged -= OnCurvesCollectionChanged;
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }
}
