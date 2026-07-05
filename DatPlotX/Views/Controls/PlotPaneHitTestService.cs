using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using DatPlotX.ViewModels;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Provides hit testing services for plot pane interactive elements.
/// Determines if mouse is over callouts, event lines, text annotations, or arrow annotations.
/// Uses AvaPlot instead of WpfPlot.
/// </summary>
public class PlotPaneHitTestService
{
    private readonly AvaPlot _avaPlot;
    private readonly PlotPaneViewModel _viewModel;

    public PlotPaneHitTestService(AvaPlot avaPlot, PlotPaneViewModel viewModel)
    {
        _avaPlot = avaPlot;
        _viewModel = viewModel;
    }

    /// <summary>
    /// Find callout under the mouse cursor using hit testing
    /// </summary>
    public Callout? GetCalloutUnderMouse(float x, float y)
    {
        var callouts = _viewModel.GetAllCallouts();
        foreach (var callout in callouts.Reverse())
        {
            if (callout.LastRenderRect.Contains(x, y))
                return callout;
        }

        return null;
    }

    /// <summary>
    /// Find event line under the mouse cursor using hit testing with tolerance
    /// </summary>
    public Guid? GetEventLineUnderMouse(float mouseX, float mouseY)
    {
        if (_viewModel.PlotModel == null)
            return null;

        ScottPlot.Pixel mousePixel = new(mouseX, mouseY);
        var dataCoords = _avaPlot.Plot.GetCoordinates(mousePixel);

        var xRange = _viewModel.PlotModel.Axes.Bottom.Range;
        double tolerance = xRange.Span * 0.01;

        return _viewModel.FindEventLineAtX(dataCoords.X, tolerance);
    }

    /// <summary>
    /// Find text annotation under the mouse cursor using hit testing.
    /// Prefers <c>LabelLastRenderPixelRect</c> when available (post-first-render) so the hit
    /// box exactly matches what the user sees; falls back to a heuristic box for the very
    /// first frame before the plottable has rendered.
    /// </summary>
    public (Guid? Id, ScottPlot.Plottables.Text? Text) GetTextAnnotationUnderMouse(float mouseX, float mouseY)
    {
        if (_viewModel.PlotModel == null)
            return (null, null);

        var textAnnotations = _viewModel.GetAllTextAnnotations();
        foreach (var text in textAnnotations.Reverse())
        {
            // Try the rendered rect first — it's the authoritative bounding box once the
            // plottable has been drawn at least once.
            var rect = text.LabelLastRenderPixelRect;
            if (rect.HasArea && rect.Contains(mouseX, mouseY))
            {
                var id = _viewModel.FindTextAnnotationId(text);
                return (id, text);
            }

            // Always also check a generous anchor-based box. Covers (a) the first frame
            // before any render has happened, and (b) ScottPlot frames where the rendered
            // rect didn't get populated for whatever reason — without this fallback,
            // right-click on a freshly-placed annotation misses and the Edit menu never
            // surfaces. Use the plottable's own axes — Y2-anchored annotations would land
            // at the wrong pixel if we used Plot.GetPixel's default (Y1) axes.
            var textPixel = _viewModel.PlotModel.GetPixel(text.Location, text.Axes.XAxis, text.Axes.YAxis);
            // Use the longest line for width (single-line counts as full length); add padding
            // so the box is forgiving of the .6 char-width estimate undershooting.
            int longestLine = 1;
            foreach (var line in (text.LabelText ?? string.Empty).Split('\n'))
                if (line.Length > longestLine) longestLine = line.Length;
            float hitWidth = Math.Max(text.LabelFontSize * longestLine * 0.7f + 16f, 60f);
            int lineCount = Math.Max(1, (text.LabelText ?? string.Empty).Split('\n').Length);
            float hitHeight = Math.Max(text.LabelFontSize * 1.4f * lineCount + 12f, 28f);
            if (mouseX >= textPixel.X - hitWidth / 2 && mouseX <= textPixel.X + hitWidth / 2 &&
                mouseY >= textPixel.Y - hitHeight / 2 && mouseY <= textPixel.Y + hitHeight / 2)
            {
                var id = _viewModel.FindTextAnnotationId(text);
                return (id, text);
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Find arrow annotation under the mouse cursor using hit testing.
    /// Returns the arrow ID and whether we're near the tip (for tip-only dragging).
    /// </summary>
    public (Guid? Id, ScottPlot.Plottables.Arrow? Arrow, bool NearTip) GetArrowAnnotationUnderMouse(float mouseX, float mouseY)
    {
        if (_viewModel.PlotModel == null)
            return (null, null, false);

        var arrows = _viewModel.GetAllArrowAnnotations();
        ScottPlot.Pixel mousePixel = new(mouseX, mouseY);

        foreach (var arrow in arrows.Reverse())
        {
            // Check if near the tip (arrowhead) - 15 pixel tolerance
            var tipPixel = _viewModel.PlotModel.GetPixel(arrow.Tip);
            double tipDistance = Math.Sqrt(Math.Pow(mouseX - tipPixel.X, 2) + Math.Pow(mouseY - tipPixel.Y, 2));
            if (tipDistance < 15)
            {
                var id = _viewModel.FindArrowAnnotationId(arrow);
                return (id, arrow, true);
            }

            // Check if near the base - 15 pixel tolerance
            var basePixel = _viewModel.PlotModel.GetPixel(arrow.Base);
            double baseDistance = Math.Sqrt(Math.Pow(mouseX - basePixel.X, 2) + Math.Pow(mouseY - basePixel.Y, 2));
            if (baseDistance < 15)
            {
                var id = _viewModel.FindArrowAnnotationId(arrow);
                return (id, arrow, false);
            }

            // Check if near the line segment (for whole arrow drag)
            if (IsPointNearLineSegment(mouseX, mouseY, basePixel.X, basePixel.Y, tipPixel.X, tipPixel.Y, 8))
            {
                var id = _viewModel.FindArrowAnnotationId(arrow);
                return (id, arrow, false);
            }
        }

        return (null, null, false);
    }

    /// <summary>
    /// Check if a point is near a line segment
    /// </summary>
    private bool IsPointNearLineSegment(float px, float py, float x1, float y1, float x2, float y2, float tolerance)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 0.001f)
        {
            float dist = (float)Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
            return dist <= tolerance;
        }

        float t = Math.Max(0, Math.Min(1, ((px - x1) * dx + (py - y1) * dy) / lengthSquared));

        float closestX = x1 + t * dx;
        float closestY = y1 + t * dy;

        float distance = (float)Math.Sqrt((px - closestX) * (px - closestX) + (py - closestY) * (py - closestY));
        return distance <= tolerance;
    }
}
