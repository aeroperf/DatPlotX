using DatPlotX.Models.Analysis;
using DatPlotX.ViewModels;
using System.Collections.ObjectModel;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// <see cref="IAnalysisOverlayHost"/> for Stacked mode. Locates the pane owning the curve
/// id, drops a contrasting marker + label there, and tracks the plottables so they can be
/// wiped without disturbing user annotations.
/// </summary>
/// <remarks>
/// Two independent layers are tracked: the momentary point flash (clicking a metric's target)
/// and the persistent active-segment band. They must not clobber each other — flashing a point
/// keeps the segment band, and re-banding keeps nothing stale from a prior point.
/// </remarks>
public sealed class StackedAnalysisOverlayHost : IAnalysisOverlayHost
{
    private readonly ObservableCollection<PlotPaneViewModel> _panes;
    private readonly List<(PlotPaneViewModel Pane, object Plottable)> _pointMarkers = new();
    private readonly List<(PlotPaneViewModel Pane, object Plottable)> _segmentMarkers = new();
    private readonly List<(PlotPaneViewModel Pane, object Plottable)> _inlineLabels = new();

    // Statistic lines (slope / mean / min / max), keyed by lineId so each can be replaced or
    // cleared on its own as the user toggles a metric's "show on plot" or pans/switches segments.
    private readonly Dictionary<string, List<(PlotPaneViewModel Pane, object Plottable)>> _statLines =
        new(StringComparer.Ordinal);

    public StackedAnalysisOverlayHost(ObservableCollection<PlotPaneViewModel> panes)
    {
        _panes = panes;
    }

    /// <summary>
    /// Draw a fixed corner label on each pane summarizing the top metrics for that pane's
    /// curves (one line per curve). <paramref name="paneText"/> maps a pane to its block of
    /// text; panes not present get no label. Replaces any prior inline labels.
    /// </summary>
    public void ShowInlineLabels(IReadOnlyDictionary<PlotPaneViewModel, string> paneText)
    {
        ClearInlineLabels();
        foreach (var (pane, text) in paneText)
        {
            if (pane.PlotModel is null || string.IsNullOrEmpty(text)) continue;

            var ann = pane.PlotModel.Add.Annotation(text, ScottPlot.Alignment.UpperLeft);
            ann.LabelFontSize = 11;
            ann.LabelFontColor = ScottPlot.Color.FromARGB(0xFF1B1B1B);
            ann.LabelBackgroundColor = ScottPlot.Color.FromARGB(0xCCFFFFFF);
            ann.LabelBorderColor = ScottPlot.Color.FromARGB(0x66999999);
            ann.LabelBorderWidth = 1;
            ann.LabelPadding = 5;
            _inlineLabels.Add((pane, ann));
            pane.RequestPlotRefresh();
        }
    }

    private void ClearInlineLabels() => Remove(_inlineLabels);

    public void HighlightPoint(string curveId, double x, double y, string label)
    {
        ClearPointMarkers();
        if (!Guid.TryParse(curveId, out var guid)) return;

        foreach (var pane in _panes)
        {
            var owner = pane.GetPlottedCurves().FirstOrDefault(p => p.Config.Id == guid);
            if (owner is null || pane.PlotModel is null) continue;

            // The point's Y is in the curve's own axis coordinates. A curve plotted on Y2
            // (right) would otherwise be drawn against Y1 (left) — landing on the wrong
            // vertical spot, or off-screen when the two axes have different ranges.
            var yAxis = owner.Config.YAxis == Models.YAxisType.Y2
                ? pane.PlotModel.Axes.Right
                : pane.PlotModel.Axes.Left;

            var marker = pane.PlotModel.Add.Marker(x, y);
            marker.Axes.YAxis = yAxis;
            marker.MarkerStyle.Shape = ScottPlot.MarkerShape.OpenCircle;
            marker.MarkerStyle.Size = 18;
            marker.MarkerStyle.LineWidth = 2.5f;
            marker.MarkerStyle.LineColor = ScottPlot.Colors.Red;
            marker.MarkerStyle.FillColor = ScottPlot.Colors.Transparent;
            _pointMarkers.Add((pane, marker));

            var text = pane.PlotModel.Add.Text(label, x, y);
            text.Axes.YAxis = yAxis;
            text.LabelFontSize = 11;
            text.LabelFontColor = ScottPlot.Colors.Red;
            text.LabelBackgroundColor = ScottPlot.Color.FromARGB(0xCCFFFFFF);
            text.LabelPadding = 4;
            text.LabelAlignment = ScottPlot.Alignment.LowerLeft;
            text.OffsetY = -12;
            _pointMarkers.Add((pane, text));

            pane.RequestPlotRefresh();

            // Pan the pane's X-axis to keep the point in view if it fell outside.
            var range = pane.GetXAxisRange();
            if (range is { } r && (x < r.Min || x > r.Max))
            {
                double width = Math.Max(1e-9, r.Max - r.Min);
                pane.SetXAxisRange(x - width / 2, x + width / 2);
            }
            return;
        }
    }

    public void HighlightSegment(double xMin, double xMax, string label)
    {
        ClearSegmentMarkers();
        foreach (var pane in _panes)
        {
            if (pane.PlotModel is null) continue;
            var span = pane.PlotModel.Add.HorizontalSpan(xMin, xMax);
            span.FillStyle.Color = ScottPlot.Color.FromARGB(0x33FFD43B);
            span.LineStyle.Color = ScottPlot.Color.FromARGB(0x99FFD43B);
            span.LineStyle.Width = 1;
            _segmentMarkers.Add((pane, span));
            pane.RequestPlotRefresh();
        }
        _ = label; // not drawn on the span; visible label is on the panel's segment chip
    }

    public void DrawSegmentLine(string curveId, string lineId, MetricLine line,
        double segXMin, double segXMax, string colorHex, string label)
    {
        ClearSegmentLine(lineId);
        if (!Guid.TryParse(curveId, out var guid)) return;

        // Resolve the line's endpoints. Horizontal lines span the segment at a single Y; sloped
        // lines carry their own endpoints (already clipped to the segment X extent by the metric).
        double x0, y0, x1, y1;
        if (line.Shape == MetricLineShape.Horizontal)
        {
            if (!double.IsFinite(segXMin) || !double.IsFinite(segXMax) || segXMax <= segXMin) return;
            x0 = segXMin; x1 = segXMax; y0 = y1 = line.Y0;
        }
        else
        {
            x0 = line.X0; y0 = line.Y0; x1 = line.X1; y1 = line.Y1;
        }
        if (!double.IsFinite(x0) || !double.IsFinite(y0) || !double.IsFinite(x1) || !double.IsFinite(y1))
            return;

        var color = ParseColor(colorHex);
        var tracked = new List<(PlotPaneViewModel, object)>();

        foreach (var pane in _panes)
        {
            var owner = pane.GetPlottedCurves().FirstOrDefault(p => p.Config.Id == guid);
            if (owner is null || pane.PlotModel is null) continue;

            // Same Y2-awareness as HighlightPoint — a curve on the right axis must draw against it.
            var yAxis = owner.Config.YAxis == Models.YAxisType.Y2
                ? pane.PlotModel.Axes.Right
                : pane.PlotModel.Axes.Left;

            var seg = pane.PlotModel.Add.Line(x0, y0, x1, y1);
            seg.Axes.YAxis = yAxis;
            seg.LineColor = color;
            seg.LineWidth = 2;
            seg.LinePattern = ScottPlot.LinePattern.Dashed;
            seg.MarkerStyle.IsVisible = false;
            tracked.Add((pane, seg));

            if (!string.IsNullOrEmpty(label))
            {
                var text = pane.PlotModel.Add.Text(label, x1, y1);
                text.Axes.YAxis = yAxis;
                text.LabelFontSize = 10;
                text.LabelFontColor = color;
                text.LabelBackgroundColor = ScottPlot.Color.FromARGB(0xCCFFFFFF);
                text.LabelPadding = 3;
                // Default: anchor the label's lower-left at the line's right endpoint and nudge it
                // right + slightly up so it sits clear of the line. But when the endpoint is near the
                // right plot edge (full-curve lines end at the axis), a right-extending label gets
                // clipped off-screen — so flip it to the left of the endpoint instead.
                if (NearRightEdge(pane, x1))
                {
                    text.LabelAlignment = ScottPlot.Alignment.LowerRight;
                    text.OffsetX = -6;
                }
                else
                {
                    text.LabelAlignment = ScottPlot.Alignment.LowerLeft;
                    text.OffsetX = 6;
                }
                text.OffsetY = -4;
                tracked.Add((pane, text));
            }

            pane.RequestPlotRefresh();
            break; // a curve id lives in exactly one pane
        }

        if (tracked.Count > 0) _statLines[lineId] = tracked;
    }

    public void ClearSegmentLine(string lineId)
    {
        if (_statLines.TryGetValue(lineId, out var markers))
        {
            Remove(markers);
            _statLines.Remove(lineId);
        }
    }

    public void ClearSegmentLines()
    {
        foreach (var markers in _statLines.Values) Remove(markers);
        _statLines.Clear();
    }

    /// <summary>True when <paramref name="x"/> sits in the rightmost slice of the pane's visible
    /// X range, where a right-extending label would be clipped by the plot edge. Falls back to
    /// "not near the edge" when the range is unknown.</summary>
    private static bool NearRightEdge(PlotPaneViewModel pane, double x)
    {
        if (pane.GetXAxisRange() is not { } r) return false;
        double width = r.Max - r.Min;
        if (!(width > 0)) return false;
        return x >= r.Max - 0.15 * width;
    }

    private static ScottPlot.Color ParseColor(string? hex)
    {
        if (!string.IsNullOrEmpty(hex))
        {
            try { return ScottPlot.Color.FromHex(hex); }
            catch { /* fall through to default */ }
        }
        return ScottPlot.Colors.Red;
    }

    /// <summary>Remove every analysis overlay (point flash + segment band + stat lines + inline labels). Idempotent.</summary>
    public void ClearHighlights()
    {
        ClearPointMarkers();
        ClearSegmentMarkers();
        ClearSegmentLines();
        ClearInlineLabels();
    }

    /// <summary>Remove only the inline corner labels (e.g. when the overlay is toggled off).</summary>
    public void HideInlineLabels() => ClearInlineLabels();

    private void ClearPointMarkers() => Remove(_pointMarkers);
    private void ClearSegmentMarkers() => Remove(_segmentMarkers);

    private static void Remove(List<(PlotPaneViewModel Pane, object Plottable)> markers)
    {
        foreach (var (pane, plottable) in markers)
        {
            if (pane.PlotModel is null) continue;
            try { pane.PlotModel.Remove((ScottPlot.IPlottable)plottable); }
            catch { /* ignore */ }
            pane.RequestPlotRefresh();
        }
        markers.Clear();
    }
}
