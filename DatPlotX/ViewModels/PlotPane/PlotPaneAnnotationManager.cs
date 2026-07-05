using DatPlotX.Models;
using ScottPlot;
using System.Globalization;

namespace DatPlotX.ViewModels.PlotPane;

/// <summary>
/// Manager for all annotation types in a plot pane: callouts, text, and arrows.
/// Owns the annotation dictionaries and manages their lifecycle.
/// </summary>
public class PlotPaneAnnotationManager : IPlotPaneAnnotationManager
{
    private readonly Func<Plot?> _getPlot;
    private readonly Func<PlotPaneModel> _getModel;
    private readonly Dictionary<Guid, ScottPlot.Plottables.Callout> _calloutAnnotations;
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _textAnnotations;
    private readonly Dictionary<Guid, ScottPlot.Plottables.Arrow> _arrowAnnotations;
    private readonly Dictionary<Guid, ScottPlot.Plottables.Arrow> _reverseArrowAnnotations;
    private readonly Dictionary<Guid, ScottPlot.Plottables.Text> _arrowLabels;
    private readonly Action _triggerUpdate;

    /// <summary>
    /// Constructor with dependencies
    /// </summary>
    /// <param name="getPlot">Function to get current Plot reference (deferred access)</param>
    /// <param name="getModel">Function to get current PlotPaneModel reference</param>
    /// <param name="calloutAnnotations">Shared reference to callout annotations dictionary</param>
    /// <param name="textAnnotations">Shared reference to text annotations dictionary</param>
    /// <param name="arrowAnnotations">Shared reference to arrow annotations dictionary</param>
    /// <param name="reverseArrowAnnotations">Shared reference to reverse arrow annotations dictionary</param>
    /// <param name="arrowLabels">Shared reference to arrow labels dictionary</param>
    /// <param name="triggerUpdate">Callback to trigger plot update event</param>
    public PlotPaneAnnotationManager(
        Func<Plot?> getPlot,
        Func<PlotPaneModel> getModel,
        Dictionary<Guid, ScottPlot.Plottables.Callout> calloutAnnotations,
        Dictionary<Guid, ScottPlot.Plottables.Text> textAnnotations,
        Dictionary<Guid, ScottPlot.Plottables.Arrow> arrowAnnotations,
        Dictionary<Guid, ScottPlot.Plottables.Arrow> reverseArrowAnnotations,
        Dictionary<Guid, ScottPlot.Plottables.Text> arrowLabels,
        Action triggerUpdate)
    {
        _getPlot = getPlot;
        _getModel = getModel;
        _calloutAnnotations = calloutAnnotations;
        _textAnnotations = textAnnotations;
        _arrowAnnotations = arrowAnnotations;
        _reverseArrowAnnotations = reverseArrowAnnotations;
        _arrowLabels = arrowLabels;
        _triggerUpdate = triggerUpdate;
    }

    #region Callout Annotations

    /// <inheritdoc />
    public void AddCalloutAnnotation(Guid calloutId, double intersectionX, double intersectionY,
        string labelText, double offsetX, double offsetY, YAxisType yAxisType = YAxisType.Y1)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        // Remove existing if present (for updates)
        if (_calloutAnnotations.ContainsKey(calloutId))
        {
            RemoveCalloutAnnotation(calloutId);
        }

        // Get axis ranges for bounds checking
        var xRange = plotModel.Axes.Bottom.Range;
        var yRange = yAxisType == YAxisType.Y2 ? plotModel.Axes.Right.Range : plotModel.Axes.Left.Range;

        // Calculate label position with offset
        double labelX = intersectionX - offsetX;  // Negative offset to place label to left of intersection
        double labelY = intersectionY - offsetY;  // Negative offset to place label below intersection

        // Constrain label position to stay within pane bounds
        // Add margins (5% of axis range) to keep labels away from edges
        double xMargin = xRange.Span * 0.05;
        double yMargin = yRange.Span * 0.05;

        labelX = Math.Max(xRange.Min + xMargin, Math.Min(xRange.Max - xMargin, labelX));
        labelY = Math.Max(yRange.Min + yMargin, Math.Min(yRange.Max - yMargin, labelY));

        // Create callout using ScottPlot's built-in Callout plottable
        // textLocation is where the text appears, tipLocation is where the arrow points
        var callout = plotModel.Add.Callout(
            text: labelText,
            textLocation: new Coordinates(labelX, labelY),
            tipLocation: new Coordinates(intersectionX, intersectionY));

        // Configure callout appearance
        callout.FontSize = 14;
        callout.TextColor = ScottPlot.Color.FromHex("#000000");
        callout.TextBackgroundColor = ScottPlot.Color.FromHex("#FFFFF0");
        callout.TextBorderColor = ScottPlot.Color.FromHex("#666666");
        callout.TextBorderWidth = 1;

        // Configure arrow appearance - ensure all arrow properties are set for visibility
        callout.ArrowLineWidth = 1f;
        callout.ArrowLineColor = ScottPlot.Color.FromHex("#333333");
        callout.ArrowFillColor = ScottPlot.Color.FromHex("#333333");
        callout.ArrowWidth = 2f;
        callout.ArrowheadLength = 20f;
        callout.ArrowheadWidth = 16f;
        callout.ArrowMinimumLength = 0f;  // Allow arrows of any length

        // Ensure the callout is visible
        callout.IsVisible = true;

        // Debug: Output to console
        System.Diagnostics.Debug.WriteLine($"[Callout] Created at ({intersectionX:F2}, {intersectionY:F2}) with label '{labelText}', text at ({labelX:F2}, {labelY:F2})");

        // Set the axis for the callout based on Y axis type
        if (yAxisType == YAxisType.Y2)
        {
            callout.Axes.YAxis = plotModel.Axes.Right;
        }
        else
        {
            callout.Axes.YAxis = plotModel.Axes.Left;
        }

        _calloutAnnotations[calloutId] = callout;

        // Bring callout to front so it renders on top of other plottables
        plotModel.MoveToFront(callout);

        _triggerUpdate();
    }

    /// <inheritdoc />
    public void UpdateCalloutPosition(Guid calloutId, double intersectionX, double intersectionY,
        double newOffsetX, double newOffsetY)
    {
        var plotModel = _getPlot();
        if (!_calloutAnnotations.TryGetValue(calloutId, out var callout) || plotModel == null)
            return;

        // Get axis ranges for bounds checking
        var xRange = plotModel.Axes.Bottom.Range;
        var yRange = callout.Axes.YAxis == plotModel.Axes.Right ? plotModel.Axes.Right.Range : plotModel.Axes.Left.Range;

        // Calculate new label position
        double labelX = intersectionX - newOffsetX;
        double labelY = intersectionY - newOffsetY;

        // Constrain label position to stay within pane bounds
        // Add margins (5% of axis range) to keep labels away from edges
        double xMargin = xRange.Span * 0.05;
        double yMargin = yRange.Span * 0.05;

        labelX = Math.Max(xRange.Min + xMargin, Math.Min(xRange.Max - xMargin, labelX));
        labelY = Math.Max(yRange.Min + yMargin, Math.Min(yRange.Max - yMargin, labelY));

        // Update callout positions - tip stays at intersection, text moves
        callout.TipCoordinates = new Coordinates(intersectionX, intersectionY);
        callout.TextCoordinates = new Coordinates(labelX, labelY);

        _triggerUpdate();
    }

    /// <inheritdoc />
    public void UpdateCalloutValue(Guid calloutId, double newValue, string format = "F3")
    {
        if (_calloutAnnotations.TryGetValue(calloutId, out var callout))
        {
            callout.Text = newValue.ToString(format, CultureInfo.InvariantCulture);
            _triggerUpdate();
        }
    }

    /// <inheritdoc />
    public bool RemoveCalloutAnnotation(Guid calloutId)
    {
        var plotModel = _getPlot();
        if (plotModel == null || !_calloutAnnotations.TryGetValue(calloutId, out var callout))
            return false;

        plotModel.Remove(callout);
        _calloutAnnotations.Remove(calloutId);
        _triggerUpdate();
        return true;
    }

    /// <inheritdoc />
    public void ClearCalloutAnnotations()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        foreach (var callout in _calloutAnnotations.Values)
        {
            plotModel.Remove(callout);
        }

        _calloutAnnotations.Clear();
        _triggerUpdate();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetCalloutIds() => _calloutAnnotations.Keys.ToList().AsReadOnly();

    /// <inheritdoc />
    public bool HasCallout(Guid calloutId) => _calloutAnnotations.ContainsKey(calloutId);

    /// <inheritdoc />
    public ScottPlot.Plottables.Callout? GetCallout(Guid calloutId)
    {
        return _calloutAnnotations.TryGetValue(calloutId, out var callout) ? callout : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ScottPlot.Plottables.Callout> GetAllCallouts() => _calloutAnnotations.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public Guid? FindCalloutId(ScottPlot.Plottables.Callout callout)
    {
        foreach (var kvp in _calloutAnnotations)
        {
            if (kvp.Value == callout)
                return kvp.Key;
        }
        return null;
    }

    #endregion

    #region Text Annotations

    /// <inheritdoc />
    public void AddTextAnnotation(TextAnnotationModel model)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        // Remove existing if present (for updates)
        if (_textAnnotations.ContainsKey(model.Id))
        {
            RemoveTextAnnotation(model.Id);
        }

        var text = plotModel.Add.Text(ApplyTextAlignment(model.Text, model.TextAlignment), model.X, model.Y);
        text.LabelFontSize = (float)model.FontSize;
        text.LabelFontColor = ScottPlot.Color.FromHex(model.FontColor);
        text.LabelBold = model.IsBold;
        text.LabelItalic = model.IsItalic;
        // Padding inside the background/border box — ScottPlot's default is 0, which leaves the
        // text touching the border. 4px on each side matches the look of Compact's OxyPlot
        // TextAnnotation (which has built-in breathing room).
        text.LabelPadding = 4;

        // Set background
        if (!string.IsNullOrEmpty(model.BackgroundColor))
        {
            var bgColor = ScottPlot.Color.FromHex(model.BackgroundColor);
            text.LabelBackgroundColor = bgColor.WithAlpha(model.BackgroundOpacity);
        }
        else
        {
            text.LabelBackgroundColor = ScottPlot.Colors.Transparent;
        }

        // Set border
        text.LabelBorderColor = ScottPlot.Color.FromHex(model.BorderColor);
        text.LabelBorderWidth = (float)model.BorderWidth;

        // Set alignment
        text.LabelAlignment = ConvertAlignment(model.Alignment);

        // Set rotation
        text.LabelRotation = (float)model.Rotation;

        // Set Y-axis
        if (model.YAxis == YAxisType.Y2)
        {
            text.Axes.YAxis = plotModel.Axes.Right;
        }
        else
        {
            text.Axes.YAxis = plotModel.Axes.Left;
        }

        text.IsVisible = model.IsVisible;

        _textAnnotations[model.Id] = text;
        _triggerUpdate();
    }

    /// <inheritdoc />
    public void UpdateTextAnnotation(TextAnnotationModel model)
    {
        var plotModel = _getPlot();
        if (!_textAnnotations.TryGetValue(model.Id, out var text) || plotModel == null)
            return;

        text.LabelText = ApplyTextAlignment(model.Text, model.TextAlignment);
        text.Location = new ScottPlot.Coordinates(model.X, model.Y);
        text.LabelFontSize = (float)model.FontSize;
        text.LabelFontColor = ScottPlot.Color.FromHex(model.FontColor);
        text.LabelBold = model.IsBold;
        text.LabelItalic = model.IsItalic;
        text.LabelPadding = 4; // match add-path padding so updates don't shrink the box

        // Set background
        if (!string.IsNullOrEmpty(model.BackgroundColor))
        {
            var bgColor = ScottPlot.Color.FromHex(model.BackgroundColor);
            text.LabelBackgroundColor = bgColor.WithAlpha(model.BackgroundOpacity);
        }
        else
        {
            text.LabelBackgroundColor = ScottPlot.Colors.Transparent;
        }

        // Set border
        text.LabelBorderColor = ScottPlot.Color.FromHex(model.BorderColor);
        text.LabelBorderWidth = (float)model.BorderWidth;

        // Set alignment
        text.LabelAlignment = ConvertAlignment(model.Alignment);

        // Set rotation
        text.LabelRotation = (float)model.Rotation;

        // Set Y-axis
        if (model.YAxis == YAxisType.Y2)
        {
            text.Axes.YAxis = plotModel.Axes.Right;
        }
        else
        {
            text.Axes.YAxis = plotModel.Axes.Left;
        }

        text.IsVisible = model.IsVisible;

        _triggerUpdate();
    }

    /// <inheritdoc />
    public void UpdateTextAnnotationPosition(Guid annotationId, double x, double y)
    {
        if (_textAnnotations.TryGetValue(annotationId, out var text))
        {
            text.Location = new ScottPlot.Coordinates(x, y);
            _triggerUpdate();
        }
    }

    /// <inheritdoc />
    public bool RemoveTextAnnotation(Guid annotationId)
    {
        var plotModel = _getPlot();
        if (plotModel == null || !_textAnnotations.TryGetValue(annotationId, out var text))
            return false;

        plotModel.Remove(text);
        _textAnnotations.Remove(annotationId);
        _triggerUpdate();
        return true;
    }

    /// <inheritdoc />
    public void ClearTextAnnotations()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        foreach (var text in _textAnnotations.Values)
        {
            plotModel.Remove(text);
        }

        _textAnnotations.Clear();
        _triggerUpdate();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetTextAnnotationIds() => _textAnnotations.Keys.ToList().AsReadOnly();

    /// <inheritdoc />
    public bool HasTextAnnotation(Guid annotationId) => _textAnnotations.ContainsKey(annotationId);

    /// <inheritdoc />
    public ScottPlot.Plottables.Text? GetTextAnnotation(Guid annotationId)
    {
        return _textAnnotations.TryGetValue(annotationId, out var text) ? text : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ScottPlot.Plottables.Text> GetAllTextAnnotations() => _textAnnotations.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public Guid? FindTextAnnotationId(ScottPlot.Plottables.Text textPlottable)
    {
        foreach (var kvp in _textAnnotations)
        {
            if (kvp.Value == textPlottable)
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// Convert TextAnnotationAlignment to ScottPlot Alignment
    /// </summary>
    private static ScottPlot.Alignment ConvertAlignment(TextAnnotationAlignment alignment)
    {
        return alignment switch
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
            _ => ScottPlot.Alignment.UpperLeft
        };
    }

    /// <summary>
    /// ScottPlot 5.1.58's Text plottable has no per-line text alignment — it always renders
    /// multi-line text left-aligned. To honor the user's Left/Center/Right choice, pad each
    /// line with spaces so the visual columns line up. Spaces in a proportional font won't be
    /// pixel-perfect but are close enough that the alignment intent reads clearly. Left
    /// alignment is a no-op (the raw text already left-aligns).
    /// </summary>
    internal static string ApplyTextAlignment(string text, TextHorizontalAlignment alignment)
    {
        if (alignment == TextHorizontalAlignment.Left || string.IsNullOrEmpty(text)) return text;
        var lines = text.Split('\n');
        if (lines.Length <= 1) return text;
        int max = 0;
        foreach (var l in lines) if (l.Length > max) max = l.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            int pad = max - lines[i].Length;
            if (pad <= 0) continue;
            lines[i] = alignment == TextHorizontalAlignment.Right
                ? new string(' ', pad) + lines[i]
                : new string(' ', pad / 2) + lines[i] + new string(' ', pad - pad / 2);
        }
        return string.Join('\n', lines);
    }

    #endregion

    #region Arrow Annotations

    /// <inheritdoc />
    public void AddArrowAnnotation(ArrowAnnotationModel model)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        // Remove existing if present (for updates)
        if (_arrowAnnotations.ContainsKey(model.Id))
        {
            RemoveArrowAnnotation(model.Id);
        }

        // Determine which ends should have arrowheads
        bool showTipArrow = model.ArrowEnds == ArrowEnds.End || model.ArrowEnds == ArrowEnds.Both;
        bool showBaseArrow = model.ArrowEnds == ArrowEnds.Start || model.ArrowEnds == ArrowEnds.Both;

        // Create main arrow (tip arrow) using ScottPlot's Arrow plottable
        var baseCoord = new ScottPlot.Coordinates(model.BaseX, model.BaseY);
        var tipCoord = new ScottPlot.Coordinates(model.TipX, model.TipY);
        var arrow = plotModel.Add.Arrow(baseCoord, tipCoord);

        arrow.ArrowLineColor = ScottPlot.Color.FromHex(model.Color);
        arrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
        // ArrowWidth controls the shaft thickness, ArrowLineWidth is just the outline
        arrow.ArrowWidth = (float)model.LineWidth;
        arrow.ArrowLineWidth = 0; // No outline for cleaner appearance

        // Configure tip arrowhead based on ArrowEnds setting
        if (showTipArrow && model.ArrowheadStyle != ArrowheadStyle.None)
        {
            arrow.ArrowheadLength = (float)model.ArrowheadLength;
            arrow.ArrowheadWidth = (float)model.ArrowheadWidth;

            // Set arrowhead style
            switch (model.ArrowheadStyle)
            {
                case ArrowheadStyle.Filled:
                    arrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
                    break;
                case ArrowheadStyle.Open:
                    arrow.ArrowFillColor = ScottPlot.Colors.Transparent;
                    break;
            }
        }
        else
        {
            // No arrowhead at tip
            arrow.ArrowheadLength = 0;
            arrow.ArrowheadWidth = 0;
        }

        // Set Y-axis
        if (model.YAxis == YAxisType.Y2)
        {
            arrow.Axes.YAxis = plotModel.Axes.Right;
        }
        else
        {
            arrow.Axes.YAxis = plotModel.Axes.Left;
        }

        arrow.IsVisible = model.IsVisible;

        _arrowAnnotations[model.Id] = arrow;

        // Create reverse arrow for base arrowhead if needed
        if (showBaseArrow && model.ArrowheadStyle != ArrowheadStyle.None)
        {
            // Create arrow pointing from tip to base (reverse direction)
            var reverseArrow = plotModel.Add.Arrow(tipCoord, baseCoord);

            reverseArrow.ArrowLineColor = ScottPlot.Color.FromHex(model.Color);
            reverseArrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
            // Set line width to 0 so we don't draw a duplicate line
            reverseArrow.ArrowLineWidth = 0;
            reverseArrow.ArrowheadLength = (float)model.ArrowheadLength;
            reverseArrow.ArrowheadWidth = (float)model.ArrowheadWidth;

            // Set arrowhead style
            switch (model.ArrowheadStyle)
            {
                case ArrowheadStyle.Filled:
                    reverseArrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
                    break;
                case ArrowheadStyle.Open:
                    reverseArrow.ArrowFillColor = ScottPlot.Colors.Transparent;
                    break;
            }

            // Set Y-axis
            if (model.YAxis == YAxisType.Y2)
            {
                reverseArrow.Axes.YAxis = plotModel.Axes.Right;
            }
            else
            {
                reverseArrow.Axes.YAxis = plotModel.Axes.Left;
            }

            reverseArrow.IsVisible = model.IsVisible;

            _reverseArrowAnnotations[model.Id] = reverseArrow;
        }

        // Add label if specified
        if (!string.IsNullOrEmpty(model.Label))
        {
            AddArrowLabel(model);
        }

        _triggerUpdate();
    }

    /// <summary>
    /// Add a label for an arrow annotation
    /// </summary>
    private void AddArrowLabel(ArrowAnnotationModel model)
    {
        var plotModel = _getPlot();
        if (plotModel == null || string.IsNullOrEmpty(model.Label))
            return;

        // Calculate VISUAL angle using pixel coordinates for accuracy
        // This properly accounts for axis scaling AND plot aspect ratio
        var baseCoord = new ScottPlot.Coordinates(model.BaseX, model.BaseY);
        var tipCoord = new ScottPlot.Coordinates(model.TipX, model.TipY);
        var basePixel = plotModel.GetPixel(baseCoord);
        var tipPixel = plotModel.GetPixel(tipCoord);
        double pixelDx = tipPixel.X - basePixel.X;
        double pixelDy = tipPixel.Y - basePixel.Y; // In screen coords, Y increases downward
        double visualAngle = Math.Atan2(pixelDy, pixelDx); // radians, in screen coordinate system
        double visualAngleDeg = visualAngle * 180 / Math.PI;

        // Calculate anchor position based on LabelPosition
        double anchorX, anchorY;
        switch (model.LabelPosition)
        {
            case ArrowLabelPosition.Base:
                anchorX = model.BaseX;
                anchorY = model.BaseY;
                break;
            case ArrowLabelPosition.Tip:
                anchorX = model.TipX;
                anchorY = model.TipY;
                break;
            case ArrowLabelPosition.Middle:
            default:
                anchorX = (model.BaseX + model.TipX) / 2;
                anchorY = (model.BaseY + model.TipY) / 2;
                break;
        }

        // Calculate offset in PIXEL space for consistent visual distance regardless of arrow angle
        // Then convert back to data coordinates
        var anchorCoord = new ScottPlot.Coordinates(anchorX, anchorY);
        var anchorPixel = plotModel.GetPixel(anchorCoord);

        // Fixed pixel offset distance (adjust this value to control label-to-arrow distance)
        const double pixelOffset = 15.0;

        // Perpendicular angle in screen coordinates
        double perpAngle = visualAngle + Math.PI / 2;

        // Calculate label position in pixels, then convert to data coordinates
        double labelX, labelY;
        ScottPlot.Pixel labelPixel;
        ScottPlot.Coordinates labelCoord;

        switch (model.LabelAlignment)
        {
            case ArrowLabelAlignment.Above:
                // "Above" = visually higher on screen = SMALLER pixel Y (screen Y grows downward).
                // perpAngle is computed in screen coords, so subtracting sin(perpAngle)*offset
                // pushes the label toward the top of the screen for any arrow orientation.
                labelPixel = new ScottPlot.Pixel(
                    anchorPixel.X - Math.Cos(perpAngle) * pixelOffset,
                    anchorPixel.Y - Math.Sin(perpAngle) * pixelOffset);
                labelCoord = plotModel.GetCoordinates(labelPixel);
                labelX = labelCoord.X;
                labelY = labelCoord.Y;
                break;
            case ArrowLabelAlignment.Below:
                // "Below" = visually lower on screen = LARGER pixel Y.
                labelPixel = new ScottPlot.Pixel(
                    anchorPixel.X + Math.Cos(perpAngle) * pixelOffset,
                    anchorPixel.Y + Math.Sin(perpAngle) * pixelOffset);
                labelCoord = plotModel.GetCoordinates(labelPixel);
                labelX = labelCoord.X;
                labelY = labelCoord.Y;
                break;
            case ArrowLabelAlignment.InlineAtBase:
                // Position at base, extending away from tip (use arrow direction, not perpendicular)
                labelPixel = new ScottPlot.Pixel(
                    basePixel.X - Math.Cos(visualAngle) * pixelOffset * 2,
                    basePixel.Y - Math.Sin(visualAngle) * pixelOffset * 2);
                labelCoord = plotModel.GetCoordinates(labelPixel);
                labelX = labelCoord.X;
                labelY = labelCoord.Y;
                break;
            case ArrowLabelAlignment.InlineAtTip:
                // Position at tip, extending away from base (use arrow direction)
                labelPixel = new ScottPlot.Pixel(
                    tipPixel.X + Math.Cos(visualAngle) * pixelOffset * 2,
                    tipPixel.Y + Math.Sin(visualAngle) * pixelOffset * 2);
                labelCoord = plotModel.GetCoordinates(labelPixel);
                labelX = labelCoord.X;
                labelY = labelCoord.Y;
                break;
            default:
                labelX = anchorX;
                labelY = anchorY;
                break;
        }

        var paneModel = _getModel();
        var label = plotModel.Add.Text(model.Label, labelX, labelY);
        label.LabelFontSize = (float)model.LabelFontSize;
        label.LabelFontColor = ScottPlot.Color.FromHex(model.LabelFontColor);
        label.LabelBackgroundColor = ScottPlot.Colors.White.WithAlpha(0.8);
        label.LabelAlignment = ScottPlot.Alignment.MiddleCenter;

        // Apply rotation to make label parallel with arrow (using visual angle)
        double rotation = visualAngleDeg;
        // Flip text if arrow points left to keep text readable
        if (visualAngleDeg > 90 || visualAngleDeg < -90)
        {
            rotation += 180;
        }
        label.LabelRotation = (float)rotation;

        // Set Y-axis to match arrow
        if (model.YAxis == YAxisType.Y2)
        {
            label.Axes.YAxis = plotModel.Axes.Right;
        }
        else
        {
            label.Axes.YAxis = plotModel.Axes.Left;
        }

        label.IsVisible = model.IsVisible;

        _arrowLabels[model.Id] = label;
    }

    /// <inheritdoc />
    public void UpdateArrowAnnotation(ArrowAnnotationModel model)
    {
        var plotModel = _getPlot();
        if (!_arrowAnnotations.TryGetValue(model.Id, out var arrow) || plotModel == null)
            return;

        // Determine which ends should have arrowheads
        bool showTipArrow = model.ArrowEnds == ArrowEnds.End || model.ArrowEnds == ArrowEnds.Both;
        bool showBaseArrow = model.ArrowEnds == ArrowEnds.Start || model.ArrowEnds == ArrowEnds.Both;

        var baseCoord = new ScottPlot.Coordinates(model.BaseX, model.BaseY);
        var tipCoord = new ScottPlot.Coordinates(model.TipX, model.TipY);

        arrow.Base = baseCoord;
        arrow.Tip = tipCoord;
        arrow.ArrowLineColor = ScottPlot.Color.FromHex(model.Color);
        arrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
        // ArrowWidth controls the shaft thickness, ArrowLineWidth is just the outline
        arrow.ArrowWidth = (float)model.LineWidth;
        arrow.ArrowLineWidth = 0; // No outline for cleaner appearance

        // Configure tip arrowhead based on ArrowEnds setting
        if (showTipArrow && model.ArrowheadStyle != ArrowheadStyle.None)
        {
            arrow.ArrowheadLength = (float)model.ArrowheadLength;
            arrow.ArrowheadWidth = (float)model.ArrowheadWidth;

            // Set arrowhead style
            switch (model.ArrowheadStyle)
            {
                case ArrowheadStyle.Filled:
                    arrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
                    break;
                case ArrowheadStyle.Open:
                    arrow.ArrowFillColor = ScottPlot.Colors.Transparent;
                    break;
            }
        }
        else
        {
            // No arrowhead at tip
            arrow.ArrowheadLength = 0;
            arrow.ArrowheadWidth = 0;
        }

        // Set Y-axis
        if (model.YAxis == YAxisType.Y2)
        {
            arrow.Axes.YAxis = plotModel.Axes.Right;
        }
        else
        {
            arrow.Axes.YAxis = plotModel.Axes.Left;
        }

        arrow.IsVisible = model.IsVisible;

        // Handle reverse arrow for base arrowhead
        if (_reverseArrowAnnotations.TryGetValue(model.Id, out var existingReverseArrow))
        {
            plotModel.Remove(existingReverseArrow);
            _reverseArrowAnnotations.Remove(model.Id);
        }

        if (showBaseArrow && model.ArrowheadStyle != ArrowheadStyle.None)
        {
            // Create arrow pointing from tip to base (reverse direction)
            var reverseArrow = plotModel.Add.Arrow(tipCoord, baseCoord);

            reverseArrow.ArrowLineColor = ScottPlot.Color.FromHex(model.Color);
            reverseArrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
            // Set line width to 0 so we don't draw a duplicate line
            reverseArrow.ArrowLineWidth = 0;
            reverseArrow.ArrowheadLength = (float)model.ArrowheadLength;
            reverseArrow.ArrowheadWidth = (float)model.ArrowheadWidth;

            // Set arrowhead style
            switch (model.ArrowheadStyle)
            {
                case ArrowheadStyle.Filled:
                    reverseArrow.ArrowFillColor = ScottPlot.Color.FromHex(model.Color);
                    break;
                case ArrowheadStyle.Open:
                    reverseArrow.ArrowFillColor = ScottPlot.Colors.Transparent;
                    break;
            }

            // Set Y-axis
            if (model.YAxis == YAxisType.Y2)
            {
                reverseArrow.Axes.YAxis = plotModel.Axes.Right;
            }
            else
            {
                reverseArrow.Axes.YAxis = plotModel.Axes.Left;
            }

            reverseArrow.IsVisible = model.IsVisible;

            _reverseArrowAnnotations[model.Id] = reverseArrow;
        }

        // Update label
        if (_arrowLabels.TryGetValue(model.Id, out var existingLabel))
        {
            plotModel.Remove(existingLabel);
            _arrowLabels.Remove(model.Id);
        }

        if (!string.IsNullOrEmpty(model.Label))
        {
            AddArrowLabel(model);
        }

        _triggerUpdate();
    }

    /// <inheritdoc />
    public bool RemoveArrowAnnotation(Guid annotationId)
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return false;

        bool removed = false;

        if (_arrowAnnotations.TryGetValue(annotationId, out var arrow))
        {
            plotModel.Remove(arrow);
            _arrowAnnotations.Remove(annotationId);
            removed = true;
        }

        // Also remove reverse arrow if exists
        if (_reverseArrowAnnotations.TryGetValue(annotationId, out var reverseArrow))
        {
            plotModel.Remove(reverseArrow);
            _reverseArrowAnnotations.Remove(annotationId);
        }

        if (_arrowLabels.TryGetValue(annotationId, out var label))
        {
            plotModel.Remove(label);
            _arrowLabels.Remove(annotationId);
        }

        if (removed)
            _triggerUpdate();

        return removed;
    }

    /// <inheritdoc />
    public void ClearArrowAnnotations()
    {
        var plotModel = _getPlot();
        if (plotModel == null)
            return;

        foreach (var arrow in _arrowAnnotations.Values)
        {
            plotModel.Remove(arrow);
        }

        foreach (var reverseArrow in _reverseArrowAnnotations.Values)
        {
            plotModel.Remove(reverseArrow);
        }

        foreach (var label in _arrowLabels.Values)
        {
            plotModel.Remove(label);
        }

        _arrowAnnotations.Clear();
        _reverseArrowAnnotations.Clear();
        _arrowLabels.Clear();
        _triggerUpdate();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<Guid> GetArrowAnnotationIds() => _arrowAnnotations.Keys.ToList().AsReadOnly();

    /// <inheritdoc />
    public bool HasArrowAnnotation(Guid annotationId) => _arrowAnnotations.ContainsKey(annotationId);

    /// <inheritdoc />
    public ScottPlot.Plottables.Arrow? GetArrowAnnotation(Guid annotationId)
    {
        return _arrowAnnotations.TryGetValue(annotationId, out var arrow) ? arrow : null;
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ScottPlot.Plottables.Arrow> GetAllArrowAnnotations() => _arrowAnnotations.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public Guid? FindArrowAnnotationId(ScottPlot.Plottables.Arrow arrowPlottable)
    {
        foreach (var kvp in _arrowAnnotations)
        {
            if (kvp.Value == arrowPlottable)
                return kvp.Key;
        }
        return null;
    }

    #endregion
}
