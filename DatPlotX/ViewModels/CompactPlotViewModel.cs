using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Views.Compact;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Globalization;
using OxyLineStyle = OxyPlot.LineStyle;
using DpLineStyle = DatPlotX.Models.LineStyle;

namespace DatPlotX.ViewModels;

/// <summary>
/// ViewModel for the Compact Plot Surface — owns one OxyPlot <see cref="PlotModel"/> with a single
/// shared X axis and one banded Y axis per <see cref="CompactCurveModel"/>. Bands are auto-sized
/// (boolean curves get half-weight) and stacked top-to-bottom in <see cref="Curves"/> order.
/// </summary>
public sealed partial class CompactPlotViewModel : ObservableObject
{
    /// <summary>
    /// OxyPlot axis key for the shared X axis on the Compact surface. Public so view-side
    /// hit testing (<see cref="DatPlotX.Views.CompactPlotControl"/>) can look the axis up
    /// without re-typing the literal.
    /// </summary>
    public const string XAxisKey = "__compact_x";
    private const double BoolBandWeight = 0.5;
    private const double AnalogBandWeight = 1.0;

    /// <summary>
    /// Maximum number of visible banded curves the Compact surface will render. Each curve owns a
    /// full-width banded Y axis stacked vertically; past this count the per-band pixel height (and
    /// the axis tiers' label space) collapse the OxyPlot plot area to a degenerate / NaN size, which
    /// throws "Invalid size returned for Measure" in Avalonia's layout pass. Importing a wide source
    /// (e.g. X-Plane, ~70 columns) and "Select All" hits this. Beyond the cap we render the first N
    /// and raise <see cref="BandLimitExceeded"/> so the UI can warn. Mirrors the Grouped-plot line cap.
    /// </summary>
    public const int MaxVisibleBands = 24;

    /// <summary>Raised after a <see cref="Rebuild"/> that had to clamp the visible-curve count to
    /// <see cref="MaxVisibleBands"/>. <c>(shown, total)</c> — the UI surfaces a non-fatal notice.</summary>
    public event EventHandler<(int Shown, int Total)>? BandLimitExceeded;

    [ObservableProperty]
    private PlotModel _plotModel = new();

    [ObservableProperty]
    private string _xAxisLabel = "Time (s)";

    [ObservableProperty]
    private string? _xAxisColumn;

    public ObservableCollection<CompactCurveModel> Curves { get; } = new();

    /// <summary>
    /// Event lines drawn on top of all curves. Independent of Stacked-mode event lines —
    /// the two surfaces don't share state. Mutators auto-rebuild.
    /// </summary>
    public ObservableCollection<EventLineModel> EventLines { get; } = new();

    /// <summary>
    /// Text annotations on the Compact surface. Each model carries an optional
    /// <see cref="TextAnnotationModel.CompactCurveAnchor"/> identifying which banded curve's Y axis
    /// it tracks; null = anchor to the first visible curve. Mutators auto-rebuild.
    /// </summary>
    public ObservableCollection<TextAnnotationModel> TextAnnotations { get; } = new();

    /// <summary>
    /// Arrow annotations on the Compact surface. Same anchor semantics as
    /// <see cref="TextAnnotations"/>. Mutators auto-rebuild.
    /// </summary>
    public ObservableCollection<ArrowAnnotationModel> ArrowAnnotations { get; } = new();

    /// <summary>Annotation tags so view-side hit testing can map screen pixels back to the model id.</summary>
    private const string TextAnnotationTagPrefix = "compact_text:";
    private const string ArrowAnnotationTagPrefix = "compact_arrow:";
    private const string ArrowLabelTagPrefix = "compact_arrowlbl:";

    public static string BuildTextAnnotationTag(Guid id) => TextAnnotationTagPrefix + id.ToString("N");
    public static string BuildArrowAnnotationTag(Guid id) => ArrowAnnotationTagPrefix + id.ToString("N");
    public static string BuildArrowLabelTag(Guid id) => ArrowLabelTagPrefix + id.ToString("N");

    public static Guid? TryParseTextAnnotationTag(object? tag)
        => TryParseTag(tag, TextAnnotationTagPrefix);
    public static Guid? TryParseArrowAnnotationTag(object? tag)
        => TryParseTag(tag, ArrowAnnotationTagPrefix);

    private static Guid? TryParseTag(object? tag, string prefix)
    {
        if (tag is not string s) return null;
        if (!s.StartsWith(prefix, StringComparison.Ordinal)) return null;
        return Guid.TryParseExact(s.AsSpan(prefix.Length), "N", out var id) ? id : null;
    }

    /// <summary>Live pane-level formatting; <see cref="ApplySettings"/> swaps it and rebuilds.</summary>
    public CompactPaneSettings Settings { get; private set; } = new();

    private PlotDataModel? _data;
    private int _eventLabelCounter;

    /// <summary>Annotation tags so view-side hit testing can map screen pixels back to the model id.</summary>
    private const string EventLineTagPrefix = "compact_eventline:";

    public static string BuildEventLineTag(Guid id) => EventLineTagPrefix + id.ToString("N");

    public static Guid? TryParseEventLineTag(object? tag)
    {
        if (tag is not string s) return null;
        if (!s.StartsWith(EventLineTagPrefix, StringComparison.Ordinal)) return null;
        return Guid.TryParseExact(s.AsSpan(EventLineTagPrefix.Length), "N", out var id) ? id : null;
    }

    /// <summary>Replace the pane settings and rebuild. Always copies to the live instance.</summary>
    public void ApplySettings(CompactPaneSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Settings = settings;
        Rebuild();
    }

    /// <summary>
    /// Replace the underlying data and X-axis selection, then rebuild the plot model.
    /// </summary>
    public void SetData(PlotDataModel? data, string? xColumn)
    {
        _data = data;
        XAxisColumn = xColumn;
        XAxisLabel = string.IsNullOrEmpty(xColumn) ? "Time (s)" : xColumn;
        Rebuild();
    }

    /// <summary>
    /// Add a new curve at the bottom of the band stack and rebuild.
    /// </summary>
    public void AddCurve(CompactCurveModel curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        Curves.Add(curve);
        Rebuild();
    }

    /// <summary>
    /// Replace the curves list (used during project load) and rebuild.
    /// </summary>
    public void ReplaceCurves(IEnumerable<CompactCurveModel> curves)
    {
        Curves.Clear();
        foreach (var c in curves) Curves.Add(c);
        Rebuild();
    }

    public void RemoveCurve(Guid id)
    {
        var existing = Curves.FirstOrDefault(c => c.Id == id);
        if (existing is null) return;
        Curves.Remove(existing);
        Rebuild();
    }

    /// <summary>
    /// Apply in-place edits to a curve already in <see cref="Curves"/> and rebuild the plot.
    /// Caller mutates the <see cref="CompactCurveModel"/> directly (Color, LineStyle, MarkerStyle, etc.);
    /// this entry point exists so external editors don't have to call private <see cref="Rebuild"/>.
    /// No-op if the curve isn't in the collection.
    /// </summary>
    public void UpdateCurve(CompactCurveModel curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (!Curves.Contains(curve)) return;
        Rebuild();
    }

    public void Clear()
    {
        Curves.Clear();
        Rebuild();
    }

    // ── Text / Arrow annotations ─────────────────────────────────────────────

    public Guid AddTextAnnotation(TextAnnotationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
        var existing = TextAnnotations.FirstOrDefault(t => t.Id == model.Id);
        if (existing is not null) TextAnnotations.Remove(existing);
        TextAnnotations.Add(model);
        Rebuild();
        return model.Id;
    }

    public void UpdateTextAnnotation(TextAnnotationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var idx = -1;
        for (int i = 0; i < TextAnnotations.Count; i++)
            if (TextAnnotations[i].Id == model.Id) { idx = i; break; }
        if (idx < 0) return;
        TextAnnotations[idx] = model;
        Rebuild();
    }

    public void UpdateTextAnnotationPosition(Guid id, double x, double y)
    {
        var existing = TextAnnotations.FirstOrDefault(t => t.Id == id);
        if (existing is null) return;
        existing.X = x;
        existing.Y = y;
        // Soft-update — find the OxyPlot annotation by tag and move it in place to avoid the
        // full Rebuild cost during a drag (same pattern as MoveEventLine).
        foreach (var ann in PlotModel.Annotations)
        {
            if (ann is OxyPlot.Annotations.TextAnnotation t &&
                TryParseTextAnnotationTag(t.Tag) == id)
            {
                t.TextPosition = new DataPoint(x, y);
                PlotModel.InvalidatePlot(false);
                return;
            }
        }
    }

    public bool RemoveTextAnnotation(Guid id)
    {
        var existing = TextAnnotations.FirstOrDefault(t => t.Id == id);
        if (existing is null) return false;
        TextAnnotations.Remove(existing);
        Rebuild();
        return true;
    }

    public TextAnnotationModel? GetTextAnnotation(Guid id)
        => TextAnnotations.FirstOrDefault(t => t.Id == id);

    public Guid AddArrowAnnotation(ArrowAnnotationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
        var existing = ArrowAnnotations.FirstOrDefault(a => a.Id == model.Id);
        if (existing is not null) ArrowAnnotations.Remove(existing);
        ArrowAnnotations.Add(model);
        Rebuild();
        return model.Id;
    }

    public void UpdateArrowAnnotation(ArrowAnnotationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var idx = -1;
        for (int i = 0; i < ArrowAnnotations.Count; i++)
            if (ArrowAnnotations[i].Id == model.Id) { idx = i; break; }
        if (idx < 0) return;
        ArrowAnnotations[idx] = model;
        Rebuild();
    }

    public void UpdateArrowAnnotationPosition(Guid id, double baseX, double baseY, double tipX, double tipY)
    {
        var existing = ArrowAnnotations.FirstOrDefault(a => a.Id == id);
        if (existing is null) return;
        existing.BaseX = baseX; existing.BaseY = baseY;
        existing.TipX = tipX; existing.TipY = tipY;
        // Full Rebuild — arrow with label re-emits multiple annotations whose geometry depends
        // on the pixel angle. Drag perf is acceptable because there are few arrows per project.
        Rebuild();
    }

    public bool RemoveArrowAnnotation(Guid id)
    {
        var existing = ArrowAnnotations.FirstOrDefault(a => a.Id == id);
        if (existing is null) return false;
        ArrowAnnotations.Remove(existing);
        Rebuild();
        return true;
    }

    public ArrowAnnotationModel? GetArrowAnnotation(Guid id)
        => ArrowAnnotations.FirstOrDefault(a => a.Id == id);

    public void ReplaceAnnotations(
        IEnumerable<TextAnnotationModel> texts,
        IEnumerable<ArrowAnnotationModel> arrows)
    {
        TextAnnotations.Clear();
        foreach (var t in texts) TextAnnotations.Add(t.Clone());
        ArrowAnnotations.Clear();
        foreach (var a in arrows) ArrowAnnotations.Add(a.Clone());
        Rebuild();
    }

    public void ClearAllAnnotations()
    {
        if (TextAnnotations.Count == 0 && ArrowAnnotations.Count == 0) return;
        TextAnnotations.Clear();
        ArrowAnnotations.Clear();
        Rebuild();
    }

    /// <summary>
    /// Add an event line. <paramref name="label"/> may be null — caller can use
    /// <see cref="GenerateEventLineLabel"/> for the default "E1" / "E2" sequence.
    /// </summary>
    public Guid AddEventLine(double xPosition, string? label = null, string color = "#FFB900")
    {
        var ev = new EventLineModel
        {
            XPosition = xPosition,
            Label = string.IsNullOrEmpty(label) ? GenerateEventLineLabel() : label,
            Color = color,
            IsVisible = true,
            IsGlobal = true,
            ShowLabel = true,
        };
        EventLines.Add(ev);
        Rebuild();
        return ev.Id;
    }

    public bool RemoveEventLine(Guid id)
    {
        var existing = EventLines.FirstOrDefault(e => e.Id == id);
        if (existing is null) return false;
        EventLines.Remove(existing);
        Rebuild();
        return true;
    }

    /// <summary>
    /// Move an event line by id. No-op if the id isn't present. Caller is responsible for
    /// snapping/clamping to the current X-axis range.
    /// Hot path during a drag (~60Hz on macOS trackpad) — instead of calling
    /// <see cref="Rebuild"/> we mutate the matching annotation in place and request a soft
    /// invalidate. <see cref="Rebuild"/> reallocates every axis + series + datapoint per call,
    /// which on a multi-curve / 100k-point project is ~tens of MB per pointer-move.
    /// </summary>
    public void MoveEventLine(Guid id, double newXPosition)
    {
        var existing = EventLines.FirstOrDefault(e => e.Id == id);
        if (existing is null) return;
        existing.XPosition = newXPosition;

        var annotation = PlotModel.Annotations
            .OfType<CompactEventLineAnnotation>()
            .FirstOrDefault(a => TryParseEventLineTag(a.Tag) == id);
        if (annotation is null)
        {
            // Annotation not in the current model (e.g. event line added since the last full
            // rebuild was bypassed) — fall back to a full rebuild so the surface stays in sync.
            Rebuild();
            return;
        }

        annotation.X = newXPosition;
        UpdateCalloutsForEventLine(id, newXPosition);
        PlotModel.InvalidatePlot(false);
    }

    /// <summary>
    /// Sync every callout belonging to <paramref name="eventLineId"/> to a new event-line X:
    /// re-interpolate Y for each curve, update X/Y/Text in place. Skips when the source data
    /// or X column is unavailable (matches the same gate <see cref="Rebuild"/> uses).
    /// </summary>
    private void UpdateCalloutsForEventLine(Guid eventLineId, double newXPosition)
    {
        if (_data is null || string.IsNullOrEmpty(XAxisColumn)) return;

        double[] xData;
        // X column unreadable — GetColumnData logs the cause; nothing to reposition.
        try { xData = _data.GetColumnData(XAxisColumn!); }
        catch { return; }

        foreach (var ann in PlotModel.Annotations)
        {
            if (ann is not CompactCalloutAnnotation callout) continue;
            var parsed = TryParseCalloutTag(callout.Tag);
            if (parsed is not { } pair || pair.EventLineId != eventLineId) continue;

            var curve = Curves.FirstOrDefault(c => c.SourceColumn == pair.CurveColumn);
            if (curve is null) continue;

            double[] yData;
            // Curve column unreadable — GetColumnData logs the cause; skip this callout.
            try { yData = _data.GetColumnData(curve.SourceColumn); }
            catch { continue; }

            // Track the event line's X even when the interpolated Y is NaN (a gap / out-of-range X),
            // so the callout stays attached to the line instead of being stranded at its old X.
            // Only refresh Y and the value text when we have a finite reading.
            callout.X = newXPosition;

            double yVal = InterpolateY(xData, yData, newXPosition);
            if (double.IsNaN(yVal)) continue;

            callout.Y = yVal;
            callout.Text = FormatCalloutValue(yVal, curve.IsBoolean);
        }
    }

    /// <summary>Callout-value format: 4 decimals for analog (matches Stacked-mode callouts),
    /// rounded integer for boolean curves so 0/1 reads cleanly.</summary>
    private static string FormatCalloutValue(double value, bool isBoolean)
        => isBoolean
            ? ((int)System.Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("F4", CultureInfo.InvariantCulture);

    /// <summary>
    /// Update the per-curve pixel offset for an event-line callout. Hot path during a drag —
    /// mutates the matching annotation in place and triggers a soft invalidate, matching
    /// <see cref="MoveEventLine"/>'s pattern (a full <see cref="Rebuild"/> per pointer-move is
    /// many MB of allocations on multi-curve / large-data projects).
    /// </summary>
    public void SetCalloutOffset(Guid eventLineId, string curveColumn, double dxPixels, double dyPixels)
    {
        var ev = EventLines.FirstOrDefault(e => e.Id == eventLineId);
        if (ev is null) return;

        if (!ev.CompactCalloutOffsets.TryGetValue(curveColumn, out var existing))
        {
            existing = new CalloutOffset();
            ev.CompactCalloutOffsets[curveColumn] = existing;
        }
        existing.Dx = dxPixels;
        existing.Dy = dyPixels;

        string tag = BuildCalloutTag(eventLineId, curveColumn);
        var callout = PlotModel.Annotations
            .OfType<CompactCalloutAnnotation>()
            .FirstOrDefault(a => (a.Tag as string) == tag);
        if (callout is not null)
        {
            callout.OffsetXPixels = dxPixels;
            callout.OffsetYPixels = dyPixels;
            PlotModel.InvalidatePlot(false);
        }
        else
        {
            Rebuild();
        }
    }

    public void ClearEventLines()
    {
        if (EventLines.Count == 0) return;
        EventLines.Clear();
        _eventLabelCounter = 0;
        Rebuild();
    }

    /// <summary>
    /// Replace the event-line list (used during project load) and rebuild. Resets the label
    /// counter to one past the highest existing "E&lt;n&gt;" so subsequent
    /// <see cref="GenerateEventLineLabel"/> calls don't collide.
    /// </summary>
    public void ReplaceEventLines(IEnumerable<EventLineModel> eventLines)
    {
        EventLines.Clear();
        foreach (var e in eventLines) EventLines.Add(e);
        UpdateEventLabelCounter();
        Rebuild();
    }

    public string GenerateEventLineLabel()
    {
        _eventLabelCounter++;
        return $"E{_eventLabelCounter}";
    }

    private void UpdateEventLabelCounter()
    {
        int max = 0;
        foreach (var e in EventLines)
        {
            if (e.Label.Length > 1 && e.Label[0] == 'E' &&
                int.TryParse(e.Label.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            {
                if (n > max) max = n;
            }
        }
        _eventLabelCounter = max;
    }

    /// <summary>
    /// Rebuild the OxyPlot <see cref="PlotModel"/> from <see cref="Curves"/>.
    /// Invoked by every mutator (<see cref="SetData"/>, <see cref="AddCurve"/>,
    /// <see cref="RemoveCurve"/>, <see cref="ReplaceCurves"/>, <see cref="Clear"/>),
    /// so external callers should not need to call it directly.
    /// </summary>
    private void Rebuild()
    {
        var bg = ParseColor(Settings.BackgroundColor);
        if (bg.A == 0) bg = OxyColors.White;

        var model = new PlotModel
        {
            PlotAreaBorderThickness = new OxyThickness(1),
            Background = bg,
            IsLegendVisible = false,
        };

        string xTitle = !string.IsNullOrWhiteSpace(Settings.XAxisLabelOverride)
            ? Settings.XAxisLabelOverride!
            : XAxisLabel;

        double xWeight = Settings.XAxisLabelBold ? FontWeights.Bold : FontWeights.Normal;
        var xAxis = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Title = xTitle,
            MajorGridlineStyle = Settings.ShowMajorGridlines ? OxyLineStyle.Solid : OxyLineStyle.None,
            MajorGridlineColor = OxyColor.FromArgb(60, 0, 0, 0),
            MinorGridlineStyle = Settings.ShowMinorGridlines ? MapMinorStyle(Settings.MinorGridlineStyle) : OxyLineStyle.None,
            MinorGridlineColor = OxyColor.FromArgb(25, 0, 0, 0),
            StringFormat = BuildDecimalFormat(Settings.XAxisDecimalPlaces),
            Key = XAxisKey,
            IsZoomEnabled = true,
            IsPanEnabled = true,
            TitleFontSize = Settings.XAxisLabelFontSize,
            FontSize = Settings.XAxisLabelFontSize,
            TitleFontWeight = xWeight,
            FontWeight = xWeight,
        };
        if (!Settings.XAxisAutoScale)
        {
            if (Settings.XAxisMin.HasValue) xAxis.Minimum = Settings.XAxisMin.Value;
            if (Settings.XAxisMax.HasValue) xAxis.Maximum = Settings.XAxisMax.Value;
        }
        model.Axes.Add(xAxis);

        var visible = Curves.Where(c => c.IsVisible).ToList();

        // Cap the number of stacked bands. Too many banded Y axes collapse the OxyPlot plot area to
        // a degenerate size and crash Avalonia's measure pass. Render the first N; the surplus stay
        // in Curves (still listed in Manage Curves) but aren't drawn until the user hides some.
        int totalVisible = visible.Count;
        if (totalVisible > MaxVisibleBands)
        {
            visible = visible.Take(MaxVisibleBands).ToList();
            BandLimitExceeded?.Invoke(this, (MaxVisibleBands, totalVisible));
        }

        if (visible.Count == 0 || _data is null || string.IsNullOrEmpty(XAxisColumn))
        {
            // No bands to draw (data imported but no curves added yet, or no X column). OxyPlot
            // cannot compute a plot area from an X axis alone — a model with axes but no Y axis
            // measures to a degenerate/NaN size and crashes Avalonia's layout pass once the
            // PlotView is realized (repro: Stacked project → New Compact project → import). Give
            // it a neutral hidden Y axis so the empty surface has a well-defined plot area.
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Key = "__compact_y_empty",
                Minimum = 0,
                Maximum = 1,
                IsAxisVisible = false,
            });
            PlotModel = model;
            return;
        }

        double[] xData;
        // X column missing/unreadable (e.g. renamed after load) — GetColumnData logs the
        // cause; render the empty model rather than throwing out of a rebuild.
        try { xData = _data.GetColumnData(XAxisColumn!); }
        catch { PlotModel = model; return; }

        // 0 = "not customized" — derive X-axis decimals from the data span now that we
        // have xData (the axis was added above before the column was loaded).
        if (Settings.XAxisDecimalPlaces <= 0)
        {
            (double xLo, double xHi) = ColumnRange(xData);
            xAxis.StringFormat = BuildDecimalFormat(AxisDecimalHelper.ForRange(xLo, xHi));
        }

        double totalWeight = visible.Sum(c => c.IsBoolean ? BoolBandWeight : AnalogBandWeight);
        double cursor = 1.0;

        // Track per-curve Y-axis key + Y data so we can drop callout annotations at the
        // event-line intersections in a second pass below.
        var curveAxisInfo = new List<(CompactCurveModel Curve, string YKey, double[] Ys)>(visible.Count);

        for (int i = 0; i < visible.Count; i++)
        {
            var curve = visible[i];
            double weight = curve.IsBoolean ? BoolBandWeight : AnalogBandWeight;
            double bandHeight = weight / totalWeight;
            double end = cursor;
            double start = cursor - bandHeight;
            cursor = start;

            var axisPosition = curve.AxisSide == AxisSide.Left ? AxisPosition.Left : AxisPosition.Right;
            var color = ParseColor(curve.Color);
            string yKey = $"__compact_y_{i}";

            double[] yData;
            // Curve's column unreadable — GetColumnData logs the cause; skip this band
            // rather than aborting the whole rebuild.
            try { yData = _data.GetColumnData(curve.SourceColumn); }
            catch { continue; }

            (double yLo, double yHi) = ResolveYRange(curve, yData);

            double yWeight = curve.YAxisLabelBold ? FontWeights.Bold : FontWeights.Normal;
            var yAxis = new LinearAxis
            {
                Position = axisPosition,
                StartPosition = start,
                EndPosition = end,
                Minimum = yLo,
                Maximum = yHi,
                Title = BuildAxisTitle(curve),
                TitleColor = color,
                TextColor = color,
                TicklineColor = color,
                AxislineColor = color,
                AxislineStyle = OxyLineStyle.Solid,
                MajorGridlineStyle = Settings.ShowMajorGridlines ? OxyLineStyle.Solid : OxyLineStyle.None,
                MajorGridlineColor = OxyColor.FromArgb(40, 0, 0, 0),
                MinorGridlineStyle = Settings.ShowMinorGridlines ? MapMinorStyle(Settings.MinorGridlineStyle) : OxyLineStyle.None,
                MinorGridlineColor = OxyColor.FromArgb(20, 0, 0, 0),
                Key = yKey,
                PositionTier = 0,
                IsZoomEnabled = true,
                IsPanEnabled = true,
                TitleFontSize = curve.YAxisLabelFontSize,
                FontSize = curve.YAxisLabelFontSize,
                TitleFontWeight = yWeight,
                FontWeight = yWeight,
                // 0 = "not customized" — auto-pick decimals from the band's data range so a
                // tight analog band (e.g. 0..0.35) doesn't collapse to integer-only ticks.
                StringFormat = BuildDecimalFormat(
                    curve.YAxisDecimalPlaces > 0
                        ? curve.YAxisDecimalPlaces
                        : AxisDecimalHelper.ForRange(yLo, yHi)),
            };

            if (curve.IsBoolean)
            {
                yAxis.MajorStep = 1;
                yAxis.MinorStep = 1;
                yAxis.StringFormat = "0";
            }

            // R5: when overflow is allowed, drop axis-level clipping so values outside [Min, Max]
            // still render (Roll Angle bleeding into adjacent bands per NTSB convention).
            if (curve.AllowOverflow)
            {
                yAxis.IsAxisVisible = true;
                // OxyPlot clips to the plot area, not the axis range — overflow happens by default.
                // Nothing extra needed; the conditional is here for future tuning.
            }

            model.Axes.Add(yAxis);

            var series = BuildSeries(curve, color, yKey, xData, yData);
            model.Series.Add(series);

            curveAxisInfo.Add((curve, yKey, yData));
        }

        AddEventLineAnnotations(model, xData, curveAxisInfo);
        AddTextAnnotations(model, curveAxisInfo);
        AddArrowAnnotations(model, curveAxisInfo);

        PlotModel = model;
    }

    /// <summary>
    /// Resolve the OxyPlot Y-axis key for a given anchor curve column. Returns the key of the
    /// first visible curve when <paramref name="anchorColumn"/> is null or no longer present
    /// (e.g. the user removed the anchored curve).
    /// </summary>
    private static string? ResolveAnchorYKey(
        string? anchorColumn,
        IReadOnlyList<(CompactCurveModel Curve, string YKey, double[] Ys)> curveAxisInfo)
    {
        if (curveAxisInfo.Count == 0) return null;
        if (!string.IsNullOrEmpty(anchorColumn))
        {
            for (int i = 0; i < curveAxisInfo.Count; i++)
                if (curveAxisInfo[i].Curve.SourceColumn == anchorColumn)
                    return curveAxisInfo[i].YKey;
        }
        return curveAxisInfo[0].YKey;
    }

    /// <summary>
    /// Project each <see cref="TextAnnotations"/> item into an OxyPlot <c>TextAnnotation</c>
    /// attached to the anchor curve's banded Y axis. X is in shared X data coords;
    /// <see cref="TextAnnotationModel.Y"/> is interpreted in the anchored band's data coords.
    /// </summary>
    private void AddTextAnnotations(
        PlotModel model,
        IReadOnlyList<(CompactCurveModel Curve, string YKey, double[] Ys)> curveAxisInfo)
    {
        foreach (var ta in TextAnnotations)
        {
            if (!ta.IsVisible) continue;
            var yKey = ResolveAnchorYKey(ta.CompactCurveAnchor, curveAxisInfo);
            if (yKey is null) continue;

            var ann = new OxyPlot.Annotations.TextAnnotation
            {
                Text = ta.Text,
                TextPosition = new DataPoint(ta.X, ta.Y),
                TextColor = ParseColor(ta.FontColor),
                FontSize = ta.FontSize,
                FontWeight = ta.IsBold ? FontWeights.Bold : FontWeights.Normal,
                TextRotation = ta.Rotation,
                XAxisKey = XAxisKey,
                YAxisKey = yKey,
                Layer = AnnotationLayer.AboveSeries,
                Tag = BuildTextAnnotationTag(ta.Id),
                Background = WithAlphaSafe(ParseColor(ta.BackgroundColor), ta.BackgroundOpacity),
                Stroke = ParseColor(ta.BorderColor),
                StrokeThickness = ta.BorderWidth,
                TextHorizontalAlignment = MapHAlign(ta.Alignment),
                TextVerticalAlignment = MapVAlign(ta.Alignment),
                // Don't clip to the anchor band — labels often sit just outside the narrow band.
                ClipByXAxis = false,
                ClipByYAxis = false,
            };
            model.Annotations.Add(ann);
        }
    }

    /// <summary>
    /// Project each <see cref="ArrowAnnotations"/> item into an OxyPlot <c>ArrowAnnotation</c>
    /// (plus an optional label text annotation) attached to the anchor curve's banded Y axis.
    /// </summary>
    private void AddArrowAnnotations(
        PlotModel model,
        IReadOnlyList<(CompactCurveModel Curve, string YKey, double[] Ys)> curveAxisInfo)
    {
        foreach (var aa in ArrowAnnotations)
        {
            if (!aa.IsVisible) continue;
            var yKey = ResolveAnchorYKey(aa.CompactCurveAnchor, curveAxisInfo);
            if (yKey is null) continue;

            bool showTip = aa.ArrowEnds == ArrowEnds.End || aa.ArrowEnds == ArrowEnds.Both;
            bool showBase = aa.ArrowEnds == ArrowEnds.Start || aa.ArrowEnds == ArrowEnds.Both;

            var arrow = new OxyPlot.Annotations.ArrowAnnotation
            {
                StartPoint = new DataPoint(aa.BaseX, aa.BaseY),
                EndPoint = new DataPoint(aa.TipX, aa.TipY),
                Color = ParseColor(aa.Color),
                StrokeThickness = aa.LineWidth,
                HeadLength = showTip && aa.ArrowheadStyle != ArrowheadStyle.None ? aa.ArrowheadLength / 4.0 : 0,
                HeadWidth = showTip && aa.ArrowheadStyle != ArrowheadStyle.None ? aa.ArrowheadWidth / 4.0 : 0,
                XAxisKey = XAxisKey,
                YAxisKey = yKey,
                Layer = AnnotationLayer.AboveSeries,
                Tag = BuildArrowAnnotationTag(aa.Id),
            };
            model.Annotations.Add(arrow);

            if (showBase && aa.ArrowheadStyle != ArrowheadStyle.None)
            {
                // OxyPlot ArrowAnnotation always draws a shaft; with StrokeThickness = 0 the
                // arrowhead is also suppressed in some renders. Use the model's line width so
                // the reverse-direction head actually shows up; the two shafts overlap exactly
                // and the user sees a single line with arrowheads at both ends.
                var reverse = new OxyPlot.Annotations.ArrowAnnotation
                {
                    StartPoint = new DataPoint(aa.TipX, aa.TipY),
                    EndPoint = new DataPoint(aa.BaseX, aa.BaseY),
                    Color = ParseColor(aa.Color),
                    StrokeThickness = aa.LineWidth,
                    HeadLength = aa.ArrowheadLength / 4.0,
                    HeadWidth = aa.ArrowheadWidth / 4.0,
                    XAxisKey = XAxisKey,
                    YAxisKey = yKey,
                    Layer = AnnotationLayer.AboveSeries,
                    Tag = BuildArrowAnnotationTag(aa.Id),
                };
                model.Annotations.Add(reverse);
            }

            if (!string.IsNullOrEmpty(aa.Label))
            {
                var label = BuildArrowLabel(aa, yKey);
                if (label is not null) model.Annotations.Add(label);
            }
        }
    }

    /// <summary>
    /// Build a label TextAnnotation positioned and rotated to match the arrow per the model's
    /// LabelPosition / LabelAlignment / LabelRotateWithArrow fields. Returns null when label text
    /// is empty. Uses pixel-space offsets via the X axis transform so the visual distance from the
    /// arrow is constant under zoom and aspect changes.
    /// </summary>
    private OxyPlot.Annotations.TextAnnotation? BuildArrowLabel(ArrowAnnotationModel aa, string yKey)
    {
        if (string.IsNullOrEmpty(aa.Label)) return null;

        // Anchor point on the arrow in data coords.
        double anchorX = aa.LabelPosition switch
        {
            ArrowLabelPosition.Base => aa.BaseX,
            ArrowLabelPosition.Tip => aa.TipX,
            _ => (aa.BaseX + aa.TipX) / 2,
        };
        double anchorY = aa.LabelPosition switch
        {
            ArrowLabelPosition.Base => aa.BaseY,
            ArrowLabelPosition.Tip => aa.TipY,
            _ => (aa.BaseY + aa.TipY) / 2,
        };

        // Resolve axes from the PREVIOUS PlotModel (already rendered) so we have working pixel
        // transforms. The model under construction hasn't been laid out yet, so its Transform()
        // returns NaN. After the first render the soft-update path (via a Rebuild triggered by
        // any subsequent mutator or viewport change) recomputes with correct screen geometry.
        var xAxis = PlotModel?.Axes.FirstOrDefault(a => a.Key == XAxisKey);
        var yAxis = PlotModel?.Axes.FirstOrDefault(a => a.Key == yKey);
        // Validity = the axes actually produce finite, non-degenerate pixel transforms. The model
        // under construction hasn't been laid out (Transform() returns NaN), so we read the PREVIOUS
        // rendered model's axes. Do NOT gate on ScreenMin/ScreenMax orientation — for OxyPlot those
        // are just the shared plot-area rectangle corners (ScreenMax.Y > ScreenMin.Y for *every*
        // axis, X and Y alike, even the banded Y slices), so the old "Y is inverted" check was
        // always false here and silently dropped us into the no-rotation fallback. Probe Transform()
        // directly instead.
        bool haveTransforms = false;
        if (xAxis is not null && yAxis is not null)
        {
            double xp0 = xAxis.Transform(aa.BaseX), xp1 = xAxis.Transform(aa.TipX);
            double yp0 = yAxis.Transform(aa.BaseY), yp1 = yAxis.Transform(aa.TipY);
            haveTransforms = IsFinite(xp0) && IsFinite(xp1) && IsFinite(yp0) && IsFinite(yp1)
                && (Math.Abs(xp1 - xp0) > 1e-6 || Math.Abs(yp1 - yp0) > 1e-6);
        }

        double rotationDeg = 0;
        double labelX = anchorX, labelY = anchorY;

        if (haveTransforms)
        {
            double bxPx = xAxis!.Transform(aa.BaseX), byPx = yAxis!.Transform(aa.BaseY);
            double txPx = xAxis.Transform(aa.TipX), tyPx = yAxis.Transform(aa.TipY);
            double dxPx = txPx - bxPx;
            double dyPx = tyPx - byPx;
            double angle = Math.Atan2(dyPx, dxPx);
            double angleDeg = angle * 180 / Math.PI;
            double perp = angle + Math.PI / 2;
            const double offset = 15.0;

            double axPx = xAxis.Transform(anchorX), ayPx = yAxis.Transform(anchorY);
            double lxPx, lyPx;
            switch (aa.LabelAlignment)
            {
                // Screen Y increases downward (both OxyPlot Transform and ScottPlot GetPixel), so
                // "Above" must move toward a SMALLER pixel Y (subtract) and "Below" toward a larger
                // one (add) — matching the Stacked/ScottPlot path in PlotPaneAnnotationManager. These
                // were previously swapped, flipping Above/Below on the Compact surface.
                case ArrowLabelAlignment.Below:
                    lxPx = axPx + Math.Cos(perp) * offset;
                    lyPx = ayPx + Math.Sin(perp) * offset;
                    break;
                case ArrowLabelAlignment.InlineAtBase:
                    lxPx = bxPx - Math.Cos(angle) * offset * 2;
                    lyPx = byPx - Math.Sin(angle) * offset * 2;
                    break;
                case ArrowLabelAlignment.InlineAtTip:
                    lxPx = txPx + Math.Cos(angle) * offset * 2;
                    lyPx = tyPx + Math.Sin(angle) * offset * 2;
                    break;
                default: // Above
                    lxPx = axPx - Math.Cos(perp) * offset;
                    lyPx = ayPx - Math.Sin(perp) * offset;
                    break;
            }
            labelX = xAxis.InverseTransform(lxPx);
            labelY = yAxis.InverseTransform(lyPx);

            if (aa.LabelRotateWithArrow)
            {
                rotationDeg = angleDeg;
                if (rotationDeg > 90 || rotationDeg < -90) rotationDeg += 180;
            }
        }
        else
        {
            // No transforms yet — first-render fallback: place label at the data-space anchor
            // with a small data-coord nudge so it doesn't sit exactly on the arrow line.
            // Uses arrow vector to pick a perpendicular direction in data space (rough, but
            // visible until the next Rebuild snaps to pixel-accurate placement).
            double ddx = aa.TipX - aa.BaseX;
            double ddy = aa.TipY - aa.BaseY;
            double len = Math.Sqrt(ddx * ddx + ddy * ddy);
            if (len > 0)
            {
                double nx = -ddy / len, ny = ddx / len;
                double bump = len * 0.1;
                if (aa.LabelAlignment == ArrowLabelAlignment.Below) { nx = -nx; ny = -ny; }
                labelX = anchorX + nx * bump;
                labelY = anchorY + ny * bump;
            }
        }

        return new OxyPlot.Annotations.TextAnnotation
        {
            Text = aa.Label,
            TextPosition = new DataPoint(labelX, labelY),
            TextColor = ParseColor(aa.LabelFontColor),
            FontSize = aa.LabelFontSize,
            TextRotation = rotationDeg,
            Background = WithAlphaSafe(OxyColors.White, 0.8),
            Stroke = OxyColors.Transparent,
            StrokeThickness = 0,
            TextHorizontalAlignment = HorizontalAlignment.Center,
            TextVerticalAlignment = VerticalAlignment.Middle,
            XAxisKey = XAxisKey,
            YAxisKey = yKey,
            Layer = AnnotationLayer.AboveSeries,
            // Allow the label to render outside the anchor band — perpendicular offsets often
            // push it past the band's narrow Y range, and OxyPlot's default is to clip.
            ClipByXAxis = false,
            ClipByYAxis = false,
            Tag = BuildArrowLabelTag(aa.Id),
        };
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    private static OxyColor WithAlphaSafe(OxyColor c, double opacity)
        => OxyColor.FromArgb((byte)(Math.Clamp(opacity, 0, 1) * 255), c.R, c.G, c.B);

    private static HorizontalAlignment MapHAlign(TextAnnotationAlignment a) => a switch
    {
        TextAnnotationAlignment.UpperLeft or TextAnnotationAlignment.MiddleLeft or TextAnnotationAlignment.LowerLeft => HorizontalAlignment.Left,
        TextAnnotationAlignment.UpperRight or TextAnnotationAlignment.MiddleRight or TextAnnotationAlignment.LowerRight => HorizontalAlignment.Right,
        _ => HorizontalAlignment.Center,
    };

    private static VerticalAlignment MapVAlign(TextAnnotationAlignment a) => a switch
    {
        TextAnnotationAlignment.UpperLeft or TextAnnotationAlignment.UpperCenter or TextAnnotationAlignment.UpperRight => VerticalAlignment.Top,
        TextAnnotationAlignment.LowerLeft or TextAnnotationAlignment.LowerCenter or TextAnnotationAlignment.LowerRight => VerticalAlignment.Bottom,
        _ => VerticalAlignment.Middle,
    };

    /// <summary>
    /// Append a <see cref="CompactEventLineAnnotation"/> per visible event line. Each line spans
    /// the full plot area (top-to-bottom) in screen space — required because the Compact surface
    /// has one banded Y axis per curve and stock <see cref="LineAnnotation"/>s clip to the first
    /// one. Tagged so view-side hit testing can map back to the model id.
    /// </summary>
    private void AddEventLineAnnotations(
        PlotModel model,
        double[] xData,
        IReadOnlyList<(CompactCurveModel Curve, string YKey, double[] Ys)> curveAxisInfo)
    {
        if (EventLines.Count == 0) return;

        foreach (var ev in EventLines)
        {
            if (!ev.IsVisible) continue;

            var color = ParseColor(ev.Color);
            string tag = BuildEventLineTag(ev.Id);

            var line = new CompactEventLineAnnotation
            {
                X = ev.XPosition,
                Color = color,
                StrokeThickness = ev.LineWidth,
                LineStyle = MapEventLinePattern(ev.LinePattern),
                Label = ev.ShowLabel ? ev.Label : null,
                XAxisKey = XAxisKey,
                Layer = AnnotationLayer.AboveSeries,
                Tag = tag,
            };
            model.Annotations.Add(line);

            AddCalloutsForEventLine(model, ev, xData, curveAxisInfo);
        }
    }

    /// <summary>
    /// Per visible curve, interpolate the Y value at <paramref name="ev"/>.XPosition and add a
    /// <see cref="CompactCalloutAnnotation"/> that points from the intersection to a small label
    /// box. Default pixel offset staggers callouts down-right so they don't pile on top of each
    /// other; the user can drag any box, which writes back to <see cref="EventLineModel.CompactCalloutOffsets"/>.
    /// </summary>
    private void AddCalloutsForEventLine(
        PlotModel model,
        EventLineModel ev,
        double[] xData,
        IReadOnlyList<(CompactCurveModel Curve, string YKey, double[] Ys)> curveAxisInfo)
    {
        if (xData.Length == 0) return;

        for (int i = 0; i < curveAxisInfo.Count; i++)
        {
            var (curve, yKey, ys) = curveAxisInfo[i];
            double yVal = InterpolateY(xData, ys, ev.XPosition);
            if (double.IsNaN(yVal)) continue;

            (double dx, double dy) = ev.CompactCalloutOffsets.TryGetValue(curve.SourceColumn, out var saved)
                ? (saved.Dx, saved.Dy)
                : DefaultCalloutOffset(i);

            string text = FormatCalloutValue(yVal, curve.IsBoolean);

            var callout = new CompactCalloutAnnotation
            {
                X = ev.XPosition,
                Y = yVal,
                OffsetXPixels = dx,
                OffsetYPixels = dy,
                Text = text,
                Color = OxyColors.Black,
                BorderColor = ParseColor(curve.Color),
                XAxisKey = XAxisKey,
                YAxisKey = yKey,
                Layer = AnnotationLayer.AboveSeries,
                Tag = BuildCalloutTag(ev.Id, curve.SourceColumn),
            };
            model.Annotations.Add(callout);
        }
    }

    /// <summary>
    /// Linear interpolation of <paramref name="ys"/> at <paramref name="targetX"/>. Returns
    /// <c>NaN</c> when <paramref name="targetX"/> is outside the X range or arrays are empty.
    /// Assumes <paramref name="xs"/> is monotonically increasing (matches CSV import contract).
    /// </summary>
    private static double InterpolateY(double[] xs, double[] ys, double targetX)
    {
        int n = System.Math.Min(xs.Length, ys.Length);
        if (n == 0) return double.NaN;
        if (targetX <= xs[0]) return targetX == xs[0] ? ys[0] : double.NaN;
        if (targetX >= xs[n - 1]) return targetX == xs[n - 1] ? ys[n - 1] : double.NaN;

        int lo = 0, hi = n - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (xs[mid] <= targetX) lo = mid; else hi = mid;
        }
        double x0 = xs[lo], x1 = xs[hi];
        if (x1 == x0) return ys[lo];
        double t = (targetX - x0) / (x1 - x0);
        return ys[lo] + t * (ys[hi] - ys[lo]);
    }

    private static (double Dx, double Dy) DefaultCalloutOffset(int curveIndex)
        => (40 + (curveIndex % 3) * 8, -28 - (curveIndex % 3) * 6);

    private const string CalloutTagPrefix = "compact_callout:";
    public static string BuildCalloutTag(Guid eventLineId, string curveColumn)
        => $"{CalloutTagPrefix}{eventLineId:N}|{curveColumn}";

    public static (Guid EventLineId, string CurveColumn)? TryParseCalloutTag(object? tag)
    {
        if (tag is not string s) return null;
        if (!s.StartsWith(CalloutTagPrefix, StringComparison.Ordinal)) return null;
        var rest = s.AsSpan(CalloutTagPrefix.Length);
        int sep = rest.IndexOf('|');
        if (sep < 0) return null;
        if (!Guid.TryParseExact(rest.Slice(0, sep), "N", out var id)) return null;
        return (id, rest.Slice(sep + 1).ToString());
    }

    private static OxyLineStyle MapEventLinePattern(LinePatternType p) => p switch
    {
        LinePatternType.Dashed => OxyLineStyle.Dash,
        LinePatternType.Dotted => OxyLineStyle.Dot,
        LinePatternType.DashDot => OxyLineStyle.DashDot,
        _ => OxyLineStyle.Solid,
    };

    private static LineSeries BuildSeries(CompactCurveModel curve, OxyColor color, string yKey, double[] xs, double[] ys)
    {
        int n = Math.Min(xs.Length, ys.Length);
        var markerColor = string.IsNullOrWhiteSpace(curve.MarkerColor) ? color : ParseColor(curve.MarkerColor!);
        var series = new LineSeries
        {
            Title = curve.DisplayName,
            Color = color,
            StrokeThickness = curve.LineWidth,
            LineStyle = MapLineStyle(curve.LineStyle),
            MarkerType = MapMarker(curve.MarkerStyle),
            MarkerSize = curve.MarkerSize,
            MarkerStroke = markerColor,
            MarkerFill = markerColor,
            // Analysis point-flash (CompactAnalysisOverlayHost.HighlightPoint) locates this
            // series — and thus the curve's banded Y axis — by matching Tag to SourceColumn.
            Tag = curve.SourceColumn,
            XAxisKey = XAxisKey,
            YAxisKey = yKey,
            // Hover tracker label: "Display Name\nX-axis: 12.345\nUnit: 67.890"
            // {0}=Title, {1}=X-axis title, {2}=X value, {3}=Y-axis title, {4}=Y value
            TrackerFormatString = "{0}\n{1}: {2:N3}\n{3}: {4:N3}",
            CanTrackerInterpolatePoints = true,
        };

        for (int j = 0; j < n; j++)
        {
            double x = xs[j];
            double y = ys[j];
            if (double.IsNaN(x) || double.IsNaN(y)) continue;
            series.Points.Add(new DataPoint(x, y));
        }
        return series;
    }

    private static (double, double) ResolveYRange(CompactCurveModel curve, double[] ys)
    {
        if (curve.IsBoolean)
            return (curve.YMin ?? 0d, curve.YMax ?? 1d);

        if (curve.YMin.HasValue && curve.YMax.HasValue)
            return (curve.YMin.Value, curve.YMax.Value);

        (double min, double max) = ColumnRange(ys);
        if (double.IsInfinity(min) || double.IsInfinity(max))
            return (0d, 1d);
        if (min == max)
        {
            double pad = Math.Abs(min) > 0 ? Math.Abs(min) * 0.1 : 1d;
            return (min - pad, max + pad);
        }
        double margin = (max - min) * 0.05;
        return (curve.YMin ?? min - margin, curve.YMax ?? max + margin);
    }

    /// <summary>Finite min/max of a column, skipping NaN. Returns (+inf, -inf) when all-NaN/empty.</summary>
    private static (double Min, double Max) ColumnRange(double[] values)
    {
        double min = double.PositiveInfinity, max = double.NegativeInfinity;
        for (int i = 0; i < values.Length; i++)
        {
            double v = values[i];
            if (double.IsNaN(v)) continue;
            if (v < min) min = v;
            if (v > max) max = v;
        }
        return (min, max);
    }

    private static string BuildAxisTitle(CompactCurveModel curve)
        => string.IsNullOrEmpty(curve.Unit) ? curve.DisplayName : $"{curve.DisplayName} ({curve.Unit})";

    private static OxyColor ParseColor(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return OxyColors.Black;
        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];

        try
        {
            return s.Length switch
            {
                6 => OxyColor.FromArgb(255,
                    byte.Parse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
                8 => OxyColor.FromArgb(
                    byte.Parse(s.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(s.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)),
                _ => OxyColors.Black,
            };
        }
        catch
        {
            return OxyColors.Black;
        }
    }

    private static OxyLineStyle MapLineStyle(DpLineStyle s) => s switch
    {
        DpLineStyle.Solid => OxyLineStyle.Solid,
        DpLineStyle.Dash => OxyLineStyle.Dash,
        DpLineStyle.Dot => OxyLineStyle.Dot,
        DpLineStyle.DashDot => OxyLineStyle.DashDot,
        _ => OxyLineStyle.Solid,
    };

    private static OxyLineStyle MapMinorStyle(CompactGridLineStyle s) => s switch
    {
        CompactGridLineStyle.Dash => OxyLineStyle.Dash,
        CompactGridLineStyle.Dot => OxyLineStyle.Dot,
        CompactGridLineStyle.DashDot => OxyLineStyle.DashDot,
        _ => OxyLineStyle.Dash,
    };

    private static string? BuildDecimalFormat(int decimals)
    {
        if (decimals <= 0) return "0";
        // Fixed decimals ('0' not '#') so adjacent ticks align — 0.20 and 0.25 both
        // render with two places rather than collapsing to "0.2" / "0.25".
        return "0." + new string('0', decimals);
    }

    private static MarkerType MapMarker(MarkerStyle m) => m switch
    {
        MarkerStyle.None => MarkerType.None,
        MarkerStyle.Circle => MarkerType.Circle,
        MarkerStyle.Square => MarkerType.Square,
        MarkerStyle.Triangle => MarkerType.Triangle,
        MarkerStyle.Diamond => MarkerType.Diamond,
        MarkerStyle.Cross => MarkerType.Cross,
        MarkerStyle.Plus => MarkerType.Plus,
        _ => MarkerType.None,
    };
}
