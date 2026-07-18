using DatPlotX.Models;
using ScottPlot;

namespace DatPlotX.ViewModels;

/// <summary>
/// Owns text and arrow annotations on the Grouped Parameter Plot's single ScottPlot surface.
/// Mirrors the Stacked-mode <c>PlotPaneAnnotationManager</c> but trimmed for the single-Y-axis
/// Grouped surface (no Y2, no pane index). The Grouped surface clears and rebuilds plottables on
/// every <c>UpdatePlot</c>, so <see cref="Reapply"/> must be called after each redraw to re-add
/// the annotation plottables to the cleared plot.
/// </summary>
public sealed class GroupedPlotAnnotationManager
{
    private readonly Func<Plot?> _getPlot;
    private readonly Action _triggerRefresh;
    private readonly List<TextAnnotationModel> _texts = new();
    private readonly List<ArrowAnnotationModel> _arrows = new();
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _textPlottables = new();
    private readonly Dictionary<Guid, ScottPlot.Plottables.Arrow> _arrowPlottables = new();
    private readonly Dictionary<Guid, ScottPlot.Plottables.Arrow> _reverseArrowPlottables = new();
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _arrowLabelPlottables = new();

    public GroupedPlotAnnotationManager(Func<Plot?> getPlot, Action triggerRefresh)
    {
        _getPlot = getPlot ?? throw new ArgumentNullException(nameof(getPlot));
        _triggerRefresh = triggerRefresh ?? throw new ArgumentNullException(nameof(triggerRefresh));
    }

    public IReadOnlyList<TextAnnotationModel> Texts => _texts;
    public IReadOnlyList<ArrowAnnotationModel> Arrows => _arrows;
    public int Count => _texts.Count + _arrows.Count;

    public Guid AddText(TextAnnotationModel model)
    {
        if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
        _texts.RemoveAll(t => t.Id == model.Id);
        _texts.Add(model);
        ApplyText(model);
        _triggerRefresh();
        return model.Id;
    }

    public void UpdateText(TextAnnotationModel model)
    {
        var idx = _texts.FindIndex(t => t.Id == model.Id);
        if (idx < 0) return;
        _texts[idx] = model;
        RemovePlottableText(model.Id);
        ApplyText(model);
        _triggerRefresh();
    }

    public void UpdateTextPosition(Guid id, double x, double y)
    {
        var t = _texts.FirstOrDefault(t => t.Id == id);
        if (t is null) return;
        t.X = x;
        t.Y = y;
        if (_textPlottables.TryGetValue(id, out var p))
        {
            p.Location = new Coordinates(x, y);
            _triggerRefresh();
        }
    }

    public bool RemoveText(Guid id)
    {
        var t = _texts.FirstOrDefault(x => x.Id == id);
        if (t is null) return false;
        _texts.Remove(t);
        RemovePlottableText(id);
        _triggerRefresh();
        return true;
    }

    public TextAnnotationModel? GetText(Guid id) => _texts.FirstOrDefault(t => t.Id == id);

    public Guid AddArrow(ArrowAnnotationModel model)
    {
        if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
        _arrows.RemoveAll(a => a.Id == model.Id);
        _arrows.Add(model);
        ApplyArrow(model);
        _triggerRefresh();
        return model.Id;
    }

    public void UpdateArrow(ArrowAnnotationModel model)
    {
        var idx = _arrows.FindIndex(a => a.Id == model.Id);
        if (idx < 0) return;
        _arrows[idx] = model;
        RemovePlottableArrow(model.Id);
        ApplyArrow(model);
        _triggerRefresh();
    }

    public void UpdateArrowPosition(Guid id, double baseX, double baseY, double tipX, double tipY)
    {
        var a = _arrows.FirstOrDefault(x => x.Id == id);
        if (a is null) return;
        a.BaseX = baseX; a.BaseY = baseY; a.TipX = tipX; a.TipY = tipY;
        // Rebuild — arrow label position depends on geometry; cheaper to re-add than to patch.
        RemovePlottableArrow(id);
        ApplyArrow(a);
        _triggerRefresh();
    }

    public bool RemoveArrow(Guid id)
    {
        var a = _arrows.FirstOrDefault(x => x.Id == id);
        if (a is null) return false;
        _arrows.Remove(a);
        RemovePlottableArrow(id);
        _triggerRefresh();
        return true;
    }

    public ArrowAnnotationModel? GetArrow(Guid id) => _arrows.FirstOrDefault(a => a.Id == id);

    /// <summary>Plottable dictionary lookup — used by hit-test in the view.</summary>
    public IReadOnlyDictionary<Guid, ScottPlot.Plottables.Text> TextPlottables => _textPlottables;
    public IReadOnlyDictionary<Guid, ScottPlot.Plottables.Arrow> ArrowPlottables => _arrowPlottables;

    /// <summary>
    /// Replace the entire annotation set (project load). Re-applies plottables from scratch.
    /// </summary>
    public void Restore(IEnumerable<TextAnnotationModel> texts, IEnumerable<ArrowAnnotationModel> arrows)
    {
        ClearAll();
        foreach (var t in texts) { _texts.Add(t); ApplyText(t); }
        foreach (var a in arrows) { _arrows.Add(a); ApplyArrow(a); }
        _triggerRefresh();
    }

    public void ClearAll()
    {
        var plot = _getPlot();
        if (plot is not null)
        {
            foreach (var t in _textPlottables.Values) plot.Remove(t);
            foreach (var a in _arrowPlottables.Values) plot.Remove(a);
            foreach (var a in _reverseArrowPlottables.Values) plot.Remove(a);
            foreach (var l in _arrowLabelPlottables.Values) plot.Remove(l);
        }
        _texts.Clear();
        _arrows.Clear();
        _textPlottables.Clear();
        _arrowPlottables.Clear();
        _reverseArrowPlottables.Clear();
        _arrowLabelPlottables.Clear();
        _triggerRefresh();
    }

    /// <summary>
    /// Re-add all annotation plottables to the current plot. Safe to call whether or not the
    /// caller already did <c>plot.Clear()</c>: the normal <c>UpdatePlot</c> path clears the plot
    /// first, but the reset-view path only autoscales, so we must remove any still-attached
    /// plottables before re-adding. Without this, "Set Scale to Default" stacked a duplicate copy
    /// of every annotation on each click, and the prior copies — no longer tracked in the
    /// dictionaries — became orphaned and could not be deleted (review #7).
    /// </summary>
    public void Reapply()
    {
        var plot = _getPlot();
        if (plot is not null)
        {
            foreach (var t in _textPlottables.Values) plot.Remove(t);
            foreach (var a in _arrowPlottables.Values) plot.Remove(a);
            foreach (var a in _reverseArrowPlottables.Values) plot.Remove(a);
            foreach (var l in _arrowLabelPlottables.Values) plot.Remove(l);
        }
        _textPlottables.Clear();
        _arrowPlottables.Clear();
        _reverseArrowPlottables.Clear();
        _arrowLabelPlottables.Clear();
        foreach (var t in _texts) ApplyText(t);
        foreach (var a in _arrows) ApplyArrow(a);
    }

    private void ApplyText(TextAnnotationModel model)
    {
        var plot = _getPlot();
        if (plot is null) return;

        // Pad multi-line text so center/right alignment lines up visually — ScottPlot's Text
        // plottable has no per-line alignment in 5.1.58, see ViewModels/PlotPane/PlotPaneAnnotationManager.ApplyTextAlignment.
        var aligned = ViewModels.PlotPane.PlotPaneAnnotationManager.ApplyTextAlignment(model.Text, model.TextAlignment);
        var text = plot.Add.Text(aligned, model.X, model.Y);
        text.LabelFontSize = (float)model.FontSize;
        text.LabelFontColor = ScottPlot.Color.FromHex(model.FontColor);
        text.LabelBold = model.IsBold;
        text.LabelItalic = model.IsItalic;
        text.LabelPadding = 4; // breathing room inside the border, matches Compact's OxyPlot look

        if (!string.IsNullOrEmpty(model.BackgroundColor))
        {
            var bg = ScottPlot.Color.FromHex(model.BackgroundColor);
            text.LabelBackgroundColor = bg.WithAlpha(model.BackgroundOpacity);
        }
        else
        {
            text.LabelBackgroundColor = ScottPlot.Colors.Transparent;
        }
        text.LabelBorderColor = ScottPlot.Color.FromHex(model.BorderColor);
        text.LabelBorderWidth = (float)model.BorderWidth;
        text.LabelAlignment = ConvertAlignment(model.Alignment);
        text.LabelRotation = (float)model.Rotation;
        text.IsVisible = model.IsVisible;

        _textPlottables[model.Id] = text;
    }

    private void ApplyArrow(ArrowAnnotationModel model)
    {
        var plot = _getPlot();
        if (plot is null) return;

        bool showTip = model.ArrowEnds == ArrowEnds.End || model.ArrowEnds == ArrowEnds.Both;
        bool showBase = model.ArrowEnds == ArrowEnds.Start || model.ArrowEnds == ArrowEnds.Both;

        var baseCoord = new Coordinates(model.BaseX, model.BaseY);
        var tipCoord = new Coordinates(model.TipX, model.TipY);

        var arrow = plot.Add.Arrow(baseCoord, tipCoord);
        arrow.ArrowLineColor = ScottPlot.Color.FromHex(model.Color);
        arrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
        arrow.ArrowWidth = (float)model.LineWidth;
        arrow.ArrowLineWidth = 0;
        if (showTip && model.ArrowheadStyle != ArrowheadStyle.None)
        {
            arrow.ArrowheadLength = (float)model.ArrowheadLength;
            arrow.ArrowheadWidth = (float)model.ArrowheadWidth;
            arrow.ArrowFillColor = model.ArrowheadStyle == ArrowheadStyle.Open
                ? ScottPlot.Colors.Transparent
                : ScottPlot.Color.FromHex(model.Color);
        }
        else
        {
            arrow.ArrowheadLength = 0;
            arrow.ArrowheadWidth = 0;
        }
        arrow.IsVisible = model.IsVisible;
        _arrowPlottables[model.Id] = arrow;

        if (showBase && model.ArrowheadStyle != ArrowheadStyle.None)
        {
            var reverse = plot.Add.Arrow(tipCoord, baseCoord);
            reverse.ArrowLineColor = ScottPlot.Color.FromHex(model.Color);
            reverse.ArrowFillColor = model.ArrowheadStyle == ArrowheadStyle.Open
                ? ScottPlot.Colors.Transparent
                : ScottPlot.Color.FromHex(model.Color);
            reverse.ArrowLineWidth = 0;
            reverse.ArrowheadLength = (float)model.ArrowheadLength;
            reverse.ArrowheadWidth = (float)model.ArrowheadWidth;
            reverse.IsVisible = model.IsVisible;
            _reverseArrowPlottables[model.Id] = reverse;
        }

        if (!string.IsNullOrEmpty(model.Label))
            AddArrowLabel(model, plot);
    }

    private void AddArrowLabel(ArrowAnnotationModel model, Plot plot)
    {
        var basePixel = plot.GetPixel(new Coordinates(model.BaseX, model.BaseY));
        var tipPixel = plot.GetPixel(new Coordinates(model.TipX, model.TipY));
        double pixelDx = tipPixel.X - basePixel.X;
        double pixelDy = tipPixel.Y - basePixel.Y;
        double visualAngle = Math.Atan2(pixelDy, pixelDx);
        double visualAngleDeg = visualAngle * 180 / Math.PI;

        double anchorX, anchorY;
        switch (model.LabelPosition)
        {
            case ArrowLabelPosition.Base: anchorX = model.BaseX; anchorY = model.BaseY; break;
            case ArrowLabelPosition.Tip: anchorX = model.TipX; anchorY = model.TipY; break;
            default: anchorX = (model.BaseX + model.TipX) / 2; anchorY = (model.BaseY + model.TipY) / 2; break;
        }
        var anchorPixel = plot.GetPixel(new Coordinates(anchorX, anchorY));

        const double pixelOffset = 15.0;
        double perpAngle = visualAngle + Math.PI / 2;

        Pixel labelPixel;
        switch (model.LabelAlignment)
        {
            case ArrowLabelAlignment.Below:
                // Larger pixel Y = lower on screen.
                labelPixel = new Pixel(
                    (float)(anchorPixel.X + Math.Cos(perpAngle) * pixelOffset),
                    (float)(anchorPixel.Y + Math.Sin(perpAngle) * pixelOffset));
                break;
            case ArrowLabelAlignment.InlineAtBase:
                labelPixel = new Pixel(
                    (float)(basePixel.X - Math.Cos(visualAngle) * pixelOffset * 2),
                    (float)(basePixel.Y - Math.Sin(visualAngle) * pixelOffset * 2));
                break;
            case ArrowLabelAlignment.InlineAtTip:
                labelPixel = new Pixel(
                    (float)(tipPixel.X + Math.Cos(visualAngle) * pixelOffset * 2),
                    (float)(tipPixel.Y + Math.Sin(visualAngle) * pixelOffset * 2));
                break;
            default: // Above — smaller pixel Y = higher on screen.
                labelPixel = new Pixel(
                    (float)(anchorPixel.X - Math.Cos(perpAngle) * pixelOffset),
                    (float)(anchorPixel.Y - Math.Sin(perpAngle) * pixelOffset));
                break;
        }
        var labelCoord = plot.GetCoordinates(labelPixel);

        var label = plot.Add.Text(model.Label!, labelCoord.X, labelCoord.Y);
        label.LabelFontSize = (float)model.LabelFontSize;
        label.LabelFontColor = ScottPlot.Color.FromHex(model.LabelFontColor);
        label.LabelBackgroundColor = ScottPlot.Colors.White.WithAlpha(0.8);
        label.LabelAlignment = ScottPlot.Alignment.MiddleCenter;
        double rot = visualAngleDeg;
        if (visualAngleDeg > 90 || visualAngleDeg < -90) rot += 180;
        label.LabelRotation = (float)rot;
        label.IsVisible = model.IsVisible;
        _arrowLabelPlottables[model.Id] = label;
    }

    private void RemovePlottableText(Guid id)
    {
        var plot = _getPlot();
        if (plot is null) return;
        if (_textPlottables.TryGetValue(id, out var t))
        {
            plot.Remove(t);
            _textPlottables.Remove(id);
        }
    }

    private void RemovePlottableArrow(Guid id)
    {
        var plot = _getPlot();
        if (plot is null) return;
        if (_arrowPlottables.TryGetValue(id, out var a)) { plot.Remove(a); _arrowPlottables.Remove(id); }
        if (_reverseArrowPlottables.TryGetValue(id, out var r)) { plot.Remove(r); _reverseArrowPlottables.Remove(id); }
        if (_arrowLabelPlottables.TryGetValue(id, out var l)) { plot.Remove(l); _arrowLabelPlottables.Remove(id); }
    }

    private static ScottPlot.Alignment ConvertAlignment(TextAnnotationAlignment a) => a switch
    {
        TextAnnotationAlignment.UpperLeft => ScottPlot.Alignment.UpperLeft,
        TextAnnotationAlignment.UpperCenter => ScottPlot.Alignment.UpperCenter,
        TextAnnotationAlignment.UpperRight => ScottPlot.Alignment.UpperRight,
        TextAnnotationAlignment.MiddleLeft => ScottPlot.Alignment.MiddleLeft,
        TextAnnotationAlignment.MiddleCenter => ScottPlot.Alignment.MiddleCenter,
        TextAnnotationAlignment.MiddleRight => ScottPlot.Alignment.MiddleRight,
        TextAnnotationAlignment.LowerLeft => ScottPlot.Alignment.LowerLeft,
        TextAnnotationAlignment.LowerCenter => ScottPlot.Alignment.LowerCenter,
        TextAnnotationAlignment.LowerRight => ScottPlot.Alignment.LowerRight,
        _ => ScottPlot.Alignment.MiddleCenter,
    };
}
