using DatPlotX.ViewModels;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;

namespace DatPlotX.Services.Analysis;

/// <summary>
/// <see cref="IAnalysisOverlayHost"/> for Compact mode. Uses OxyPlot's
/// <see cref="PointAnnotation"/> + <see cref="TextAnnotation"/> (point) and
/// <see cref="RectangleAnnotation"/> (segment band). Tagged with the
/// <see cref="MarkerTag"/> / <see cref="SegmentTag"/> prefixes so we never sweep user
/// annotations on <see cref="ClearHighlights"/>.
/// </summary>
public sealed class CompactAnalysisOverlayHost : IAnalysisOverlayHost
{
    private const string MarkerTag = "analysis_marker:";
    private const string SegmentTag = "analysis_segment:";
    private const string StatLineTag = "analysis_statline:";

    private readonly CompactPlotViewModel _vm;

    public CompactAnalysisOverlayHost(CompactPlotViewModel vm)
    {
        _vm = vm;
    }

    public void HighlightPoint(string curveId, double x, double y, string label)
    {
        ClearMarkers();
        if (!Guid.TryParse(curveId, out var guid)) return;

        var curve = _vm.Curves.FirstOrDefault(c => c.Id == guid);
        if (curve is null) return;

        // Each compact curve has its own Y axis. Walk the LineSeries in PlotModel to find the
        // axis associated with this curve's source column.
        // Match the curve's own series by tag. If none matches (e.g. a rename that hasn't rebuilt
        // yet), do NOT fall back to the first series — that would draw the marker against a
        // different band's Y axis, landing it at a visibly wrong height. Skip drawing instead.
        var ySeries = _vm.PlotModel.Series.OfType<LineSeries>()
            .FirstOrDefault(s => string.Equals((s.Tag as string), curve.SourceColumn, StringComparison.Ordinal));
        var yKey = ySeries?.YAxisKey;
        if (string.IsNullOrEmpty(yKey)) return;

        var marker = new PointAnnotation
        {
            X = x,
            Y = y,
            Shape = MarkerType.Circle,
            Size = 8,
            Fill = OxyColors.Transparent,
            Stroke = OxyColors.Red,
            StrokeThickness = 2,
            XAxisKey = CompactPlotViewModel.XAxisKey,
            YAxisKey = yKey,
            Tag = MarkerTag + "point",
            Layer = AnnotationLayer.AboveSeries,
        };
        _vm.PlotModel.Annotations.Add(marker);

        var text = new TextAnnotation
        {
            TextPosition = new DataPoint(x, y),
            Text = label,
            TextColor = OxyColors.Red,
            FontSize = 10,
            Background = OxyColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
            Stroke = OxyColors.Transparent,
            TextHorizontalAlignment = HorizontalAlignment.Left,
            TextVerticalAlignment = VerticalAlignment.Bottom,
            Offset = new ScreenVector(8, -8),
            XAxisKey = CompactPlotViewModel.XAxisKey,
            YAxisKey = yKey,
            Tag = MarkerTag + "label",
            Layer = AnnotationLayer.AboveSeries,
        };
        _vm.PlotModel.Annotations.Add(text);

        // Pan the X-axis to keep the flashed point in view if it fell outside the visible
        // window (mirrors StackedAnalysisOverlayHost) — otherwise the marker is drawn off-screen
        // and the click appears to do nothing.
        var xAxis = _vm.PlotModel.Axes
            .FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
        if (xAxis is not null)
        {
            double min = xAxis.ActualMinimum;
            double max = xAxis.ActualMaximum;
            if (!double.IsNaN(min) && !double.IsNaN(max) && (x < min || x > max))
            {
                double width = Math.Max(1e-9, max - min);
                xAxis.Zoom(x - width / 2, x + width / 2);
            }
        }

        _vm.PlotModel.InvalidatePlot(false);
    }

    public void HighlightSegment(double xMin, double xMax, string label)
    {
        ClearSegments();

        // Each Compact curve owns its own banded Y axis ("__compact_y_{i}") occupying a vertical
        // slice of the plot (StartPosition/EndPosition). A single RectangleAnnotation with infinite
        // Y only fills the one axis it binds to — so it appeared on the first curve's band only.
        // Draw one band per banded Y axis instead, keyed to that axis, so together they span the
        // whole plot height. Falls back to a single unkeyed band when no banded axes exist yet.
        var bandedYKeys = _vm.PlotModel.Axes
            .Where(a => a.Key is { } k && k.StartsWith("__compact_y_", StringComparison.Ordinal))
            .Select(a => a.Key)
            .ToList();

        if (bandedYKeys.Count == 0)
        {
            AddBand(xMin, xMax, yAxisKey: null);
        }
        else
        {
            foreach (var yKey in bandedYKeys)
                AddBand(xMin, xMax, yKey);
        }

        _vm.PlotModel.InvalidatePlot(false);
        _ = label;
    }

    private void AddBand(double xMin, double xMax, string? yAxisKey)
    {
        // One band per banded Y axis stacks vertically to span the plot; drop the stroke so the
        // abutting slices read as a single continuous column with no internal divider lines.
        var band = new RectangleAnnotation
        {
            MinimumX = xMin,
            MaximumX = xMax,
            MinimumY = double.NegativeInfinity,
            MaximumY = double.PositiveInfinity,
            Fill = OxyColor.FromArgb(0x33, 0xFF, 0xD4, 0x3B),
            Stroke = OxyColors.Transparent,
            StrokeThickness = 0,
            XAxisKey = CompactPlotViewModel.XAxisKey,
            YAxisKey = yAxisKey,
            Tag = SegmentTag + "band",
            Layer = AnnotationLayer.BelowAxes,
        };
        _vm.PlotModel.Annotations.Add(band);
    }

    public void DrawSegmentLine(string curveId, string lineId, Models.Analysis.MetricLine line,
        double segXMin, double segXMax, string colorHex, string label)
    {
        ClearSegmentLine(lineId);
        if (!Guid.TryParse(curveId, out var guid)) return;

        var curve = _vm.Curves.FirstOrDefault(c => c.Id == guid);
        if (curve is null) return;

        // Match the curve's own series by tag. If none matches (e.g. a rename that hasn't rebuilt
        // yet), do NOT fall back to the first series — that would draw the marker against a
        // different band's Y axis, landing it at a visibly wrong height. Skip drawing instead.
        var ySeries = _vm.PlotModel.Series.OfType<LineSeries>()
            .FirstOrDefault(s => string.Equals((s.Tag as string), curve.SourceColumn, StringComparison.Ordinal));
        var yKey = ySeries?.YAxisKey;
        if (string.IsNullOrEmpty(yKey)) return;

        double x0, y0, x1, y1;
        if (line.Shape == Models.Analysis.MetricLineShape.Horizontal)
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
        var statLine = new LineSeries
        {
            Color = color,
            StrokeThickness = 2,
            LineStyle = OxyPlot.LineStyle.Dash,
            XAxisKey = CompactPlotViewModel.XAxisKey,
            YAxisKey = yKey,
            Tag = StatLineTag + lineId,
            RenderInLegend = false,
        };
        statLine.Points.Add(new DataPoint(x0, y0));
        statLine.Points.Add(new DataPoint(x1, y1));
        _vm.PlotModel.Series.Add(statLine);

        if (!string.IsNullOrEmpty(label))
        {
            // Default: text starts at the line's right endpoint and nudges right + slightly up so it
            // sits clear of the line. But a full-curve line ends at the right plot edge, where a
            // right-extending label gets clipped — so there, flip it to the left of the endpoint.
            bool nearRightEdge = NearRightEdge(x1);

            // OxyPlot clips a TextAnnotation to its Y axis's band slice (TransposableAnnotation.
            // GetClippingRect). A Bottom-aligned label sits ABOVE the endpoint, so when the line ends
            // at the top of the band (e.g. a full-span slope line whose endpoint is the band max — the
            // red `_dist` case) the label is drawn above the band top and clipped away entirely. Detect
            // that and flip to Top alignment so the label drops below the endpoint, staying inside the
            // band. Stacked mode never hits this — its axis spans the whole plot height.
            bool nearTopEdge = NearBandTopEdge(yKey, y1);
            var text = new TextAnnotation
            {
                TextPosition = new DataPoint(x1, y1),
                Text = label,
                TextColor = color,
                FontSize = 10,
                Background = OxyColor.FromArgb(0xCC, 0xFF, 0xFF, 0xFF),
                Stroke = OxyColors.Transparent,
                TextHorizontalAlignment = nearRightEdge ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                TextVerticalAlignment = nearTopEdge ? VerticalAlignment.Top : VerticalAlignment.Bottom,
                Offset = new ScreenVector(nearRightEdge ? -6 : 6, nearTopEdge ? 4 : -4),
                XAxisKey = CompactPlotViewModel.XAxisKey,
                YAxisKey = yKey,
                Tag = StatLineTag + lineId,
                Layer = AnnotationLayer.AboveSeries,
            };
            _vm.PlotModel.Annotations.Add(text);
        }

        _vm.PlotModel.InvalidatePlot(false);
    }

    /// <summary>True when <paramref name="x"/> sits in the rightmost slice of the visible X range,
    /// where a right-extending label would be clipped by the plot edge. Falls back to "not near the
    /// edge" when the axis range is unknown.</summary>
    private bool NearRightEdge(double x)
    {
        var xAxis = _vm.PlotModel.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
        if (xAxis is null) return false;
        double min = xAxis.ActualMinimum, max = xAxis.ActualMaximum;
        double width = max - min;
        if (!(width > 0) || double.IsNaN(width)) return false;
        return x >= max - 0.15 * width;
    }

    /// <summary>True when <paramref name="y"/> sits in the top slice of the band owned by
    /// <paramref name="yKey"/>, where a Bottom-aligned label (drawn above the point) would be clipped
    /// by the band's top edge. Falls back to "not near the edge" when the axis/range is unknown.</summary>
    private bool NearBandTopEdge(string yKey, double y)
    {
        var yAxis = _vm.PlotModel.Axes.FirstOrDefault(a => a.Key == yKey);
        if (yAxis is null) return false;
        double min = yAxis.ActualMinimum, max = yAxis.ActualMaximum;
        double height = max - min;
        if (!(height > 0) || double.IsNaN(height)) return false;
        return y >= max - 0.15 * height;
    }

    public void ClearSegmentLine(string lineId)
    {
        var tag = StatLineTag + lineId;
        bool any = false;
        var doomedSeries = _vm.PlotModel.Series
            .Where(s => s.Tag is string t && string.Equals(t, tag, StringComparison.Ordinal)).ToList();
        foreach (var s in doomedSeries) { _vm.PlotModel.Series.Remove(s); any = true; }
        var doomedAnn = _vm.PlotModel.Annotations
            .Where(a => a.Tag is string t && string.Equals(t, tag, StringComparison.Ordinal)).ToList();
        foreach (var a in doomedAnn) { _vm.PlotModel.Annotations.Remove(a); any = true; }
        if (any) _vm.PlotModel.InvalidatePlot(false);
    }

    public void ClearSegmentLines()
    {
        var doomedSeries = _vm.PlotModel.Series
            .Where(s => s.Tag is string t && t.StartsWith(StatLineTag, StringComparison.Ordinal)).ToList();
        foreach (var s in doomedSeries) _vm.PlotModel.Series.Remove(s);
        RemoveByPrefix(StatLineTag); // annotations (labels)
        if (doomedSeries.Count > 0) _vm.PlotModel.InvalidatePlot(false);
    }

    private static OxyColor ParseColor(string? hex)
    {
        if (!string.IsNullOrEmpty(hex))
        {
            try { return OxyColor.Parse(hex); }
            catch { /* fall through */ }
        }
        return OxyColors.Red;
    }

    public void ClearHighlights()
    {
        ClearMarkers();
        ClearSegments();
        ClearSegmentLines();
    }

    private void ClearMarkers() => RemoveByPrefix(MarkerTag);
    private void ClearSegments() => RemoveByPrefix(SegmentTag);

    private void RemoveByPrefix(string prefix)
    {
        var doomed = _vm.PlotModel.Annotations
            .Where(a => a.Tag is string s && s.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();
        if (doomed.Count == 0) return;
        foreach (var a in doomed) _vm.PlotModel.Annotations.Remove(a);
        _vm.PlotModel.InvalidatePlot(false);
    }
}
