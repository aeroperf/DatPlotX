using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DatPlotX.ViewModels;
using DatPlotX.Views.Controls;
using ScottPlot;
using ScottPlot.Plottables;
using System.ComponentModel;
using Cursor = Avalonia.Input.Cursor;

namespace DatPlotX.Views;

/// <summary>
/// Hosts the ScottPlot surface for the Grouped Parameter Plot. Listens for
/// <see cref="GroupedPlotViewModel.PlotVersion"/> bumps and rebuilds the scatters; mirrors the
/// hover-tooltip pattern lifted from EnrouteStudio's PlotView. Owns the right-click context menu
/// + annotation hit-test / drag handlers — the underlying annotation lifecycle is managed by
/// <see cref="GroupedPlotAnnotationManager"/>.
/// </summary>
public partial class GroupedPlotControl : UserControl
{
    private static readonly Color[] PaletteColors =
    {
        Color.FromHex("#1f77b4"), Color.FromHex("#ff7f0e"), Color.FromHex("#2ca02c"),
        Color.FromHex("#d62728"), Color.FromHex("#9467bd"), Color.FromHex("#8c564b"),
        Color.FromHex("#e377c2"), Color.FromHex("#7f7f7f"), Color.FromHex("#bcbd22"),
        Color.FromHex("#17becf"),
    };

    private const double HoverThresholdPixels = 20.0;
    private const double AnnotationHitTolerancePixels = 8.0;

    private GroupedPlotViewModel? _vm;
    private readonly List<Scatter> _scatters = new();
    private GroupedPlotAnnotationManager? _annotationManager;

    /// <summary>
    /// Mirrors Stacked/Compact — Tools → Show Hover Tooltips gates the nearest-point tooltip.
    /// When false the (O(total points)) hover hit-test is skipped entirely. See review G6.
    /// </summary>
    public bool HoverTooltipsEnabled { get; set; } = true;

    private ContextMenu? _contextMenu;
    private MenuItem? _addTextHereItem;
    private MenuItem? _addArrowHereItem;
    private MenuItem? _editTextItem;
    private MenuItem? _deleteTextItem;
    private MenuItem? _editArrowItem;
    private MenuItem? _deleteArrowItem;
    private MenuItem? _clearAnnotationsItem;
    private MenuItem? _resetViewItem;
    private MenuItem? _exportImageItem;
    private Separator? _annotationsSeparator;

    /// <summary>X/Y data coords of the last right-press, used by "Add … Here".</summary>
    private (double X, double Y)? _rightClickCoords;
    private Guid? _rightClickedTextId;
    private Guid? _rightClickedArrowId;

    // Last hover hit-test result. Right-press hit-test occasionally misses by a pixel even
    // when hover hit-test (which paints the Hand cursor) passed; falling back to this means
    // the Edit / Delete menu items still surface when the user right-clicks the annotation
    // they were just hovering over.
    private Guid? _hoveredTextId;
    private Guid? _hoveredArrowId;

    /// <summary>Annotation currently being dragged via left-press. Either text or arrow.</summary>
    private Guid? _draggingTextId;
    private Guid? _draggingArrowId;
    /// <summary>Arrow endpoint being dragged: 0=base, 1=tip, 2=whole body.</summary>
    private int _arrowDragMode;
    private Coordinates _arrowDragStartBase;
    private Coordinates _arrowDragStartTip;
    private Coordinates _dragStartMouseCoord;

    /// <summary>Raised when the user clicks "Configure Inputs…" in the sidebar.</summary>
    public event EventHandler? ConfigureInputsRequested;

    public event EventHandler? ExportImageRequested;

    /// <summary>Raised when the user picks "Add Text Annotation Here" — data coords of right-click.</summary>
    public event Action<double, double>? AddTextAnnotationRequested;
    /// <summary>Raised when the user picks "Add Arrow Annotation Here" — data coords of right-click.</summary>
    public event Action<double, double>? AddArrowAnnotationRequested;
    public event Action<Guid>? EditTextAnnotationRequested;
    public event Action<Guid>? DeleteTextAnnotationRequested;
    public event Action<Guid>? EditArrowAnnotationRequested;
    public event Action<Guid>? DeleteArrowAnnotationRequested;
    /// <summary>Raised after a successful text-annotation drag with the new data position.</summary>
    public event Action<Guid, double, double>? TextAnnotationDragCompleted;
    /// <summary>Raised after a successful arrow-annotation drag with the new base/tip positions.</summary>
    public event Action<Guid, double, double, double, double>? ArrowAnnotationDragCompleted;

    public GroupedPlotControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetached;
        PlotControl.PointerMoved += OnPointerMoved;
        PlotControl.PointerExited += (_, _) => HideTooltip();
        PlotControl.PointerPressed += OnPlotPointerPressed;
        PlotControl.PointerReleased += OnPlotPointerReleased;
        PlotControl.PointerCaptureLost += (_, _) => CancelDrag();
        // Ctrl held → cross cursor to signal rectangle-zoom is armed (matches Stacked mode).
        // Handle on the control so we catch the key regardless of inner focus.
        AddHandler(KeyDownEvent, OnKeyDownForCursor, RoutingStrategies.Tunnel);
        AddHandler(KeyUpEvent, OnKeyUpForCursor, RoutingStrategies.Tunnel);
        ConfigureInputsButton.Click += (_, _) =>
            ConfigureInputsRequested?.Invoke(this, EventArgs.Empty);

        // Disable ScottPlot's default single-click context menu so the custom one wins; attach
        // our menu to the AvaPlot itself (not the outer UserControl) so Open(avaPlot) lands
        // on a real anchor. ScottPlot's input processor captures pointer events and does not
        // bubble ContextRequestedEvent, so we open the menu manually in OnPlotPointerPressed.
        PlotControl.UserInputProcessor.RemoveAll<ScottPlot.Interactivity.UserActionResponses.SingleClickContextMenu>();
        // Right button is exclusively the context menu — remove ScottPlot's default right-drag zoom
        // so a right-press with incidental movement doesn't nudge the plot before the menu opens,
        // and a deliberate right-drag doesn't fight the menu (review G2). Mirrors Stacked mode,
        // which clears all responses; Grouped keeps left-pan/wheel-zoom so it only removes these.
        PlotControl.UserInputProcessor.RemoveAll<ScottPlot.Interactivity.UserActionResponses.MouseDragZoom>();
        // Remove ScottPlot's FPS benchmark overlay (review G3). Double-click → reset is handled
        // directly in OnPlotPointerPressed (ClickCount == 2) so it can't be starved by the
        // stateful drag responses' PrimaryResponse locking.
        PlotControl.UserInputProcessor.RemoveAll<ScottPlot.Interactivity.UserActionResponses.DoubleClickBenchmark>();
        // Ctrl+left-drag → rubber-band rectangle zoom (matches Stacked panes / Compact surface).
        // Plain left-drag stays pan: the primary trigger button is a never-pressed sentinel, and the
        // rectangle only starts on the SecondaryMouseButton (Left) + SecondaryKey (Ctrl) combo.
        // Clear the default Ctrl/Shift axis-lock keys so Ctrl isn't consumed as a horizontal lock
        // while it's already the activation key (X/Y-only zoom is served by scroll-over-axis).
        var rectZoom = new ScottPlot.Interactivity.UserActionResponses.MouseDragZoomRectangle(
            new ScottPlot.Interactivity.MouseButton("none"))
        {
            SecondaryMouseButton = ScottPlot.Interactivity.StandardMouseButtons.Left,
            SecondaryKey = ScottPlot.Interactivity.StandardKeys.Control,
            HorizontalLockKey = ScottPlot.Interactivity.StandardKeys.Unknown,
            VerticalLockKey = ScottPlot.Interactivity.StandardKeys.Unknown,
        };
        PlotControl.UserInputProcessor.UserActionResponses.Add(rectZoom);
        // ScottPlot's default left-drag pan fires on ANY left-drag (it ignores modifiers) and
        // returns a primary response, which would consume the Ctrl+left gesture before the
        // rectangle could grow. Replace it with a pan that steps aside while Ctrl is held.
        PlotControl.UserInputProcessor.RemoveAll<ScottPlot.Interactivity.UserActionResponses.MouseDragPan>();
        PlotControl.UserInputProcessor.UserActionResponses.Add(
            new CtrlSuppressedMouseDragPan(
                ScottPlot.Interactivity.StandardMouseButtons.Left,
                ScottPlot.Interactivity.StandardKeys.Control));
        PlotControl.Menu = null!;
        PlotControl.ContextMenu = BuildContextMenu();

        // Strategy for left-drag: keep ScottPlot's default input responses (left-pan, wheel-zoom)
        // intact so plot manipulation works for empty space. When OnPlotPointerPressed detects
        // an annotation hit, call PlotControl.UserInputProcessor.Disable() to suppress SP's
        // pan for the duration of the gesture; re-enable on release. Mirrors Stacked-mode
        // PlotPaneDragHandler.
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.DetachAnnotationManager();
        }
        _vm = DataContext as GroupedPlotViewModel;
        _annotationManager = null;
        if (_vm is not null)
        {
            _annotationManager = new GroupedPlotAnnotationManager(
                getPlot: () => PlotControl.Plot,
                triggerRefresh: () => PlotControl.Refresh());
            _vm.AttachAnnotationManager(_annotationManager);
            _vm.PropertyChanged += OnVmPropertyChanged;
            UpdatePlot();
        }
    }

    private void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.DetachAnnotationManager();
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(GroupedPlotViewModel.PlotVersion))
            UpdatePlot();
        else if (e.PropertyName == nameof(GroupedPlotViewModel.CosmeticVersion))
            ApplyCosmetics();
    }

    /// <summary>
    /// Apply marker / legend toggles in place without clearing the plot or autoscaling, so the
    /// user's zoom survives a cosmetic change (see review G1).
    /// </summary>
    private void ApplyCosmetics()
    {
        if (_vm is null) return;
        var plot = PlotControl.Plot;
        foreach (var scatter in _scatters)
            scatter.MarkerSize = _vm.ShowMarkers ? 5 : 0;
        // Match UpdatePlot: never show a legend with no series, or ScottPlot draws an empty legend
        // box when the user toggles the legend on before both X and Y are selected.
        if (_vm.ShowLegend && _scatters.Count > 0)
        {
            plot.ShowLegend(Alignment.UpperRight);
            plot.Legend.Layout = new DatPlotX.Helpers.TopHeadroomLegendLayout();
        }
        else
            plot.HideLegend();
        PlotControl.Refresh();
    }

    private void UpdatePlot()
    {
        if (_vm is null) return;
        var plot = PlotControl.Plot;
        // Capture the pre-clear viewport so a data rebuild that doesn't change axis meaning
        // (e.g. changing a locked input value to compare lines) keeps the user's zoom. See G1.
        bool hasRendered = plot.LastRender.Count > 0;
        var priorLimits = plot.Axes.GetLimits();
        plot.Clear();
        _scatters.Clear();

        if (_vm.Series.Count == 0)
        {
            plot.Title(string.Empty);
            plot.XLabel(_vm.XAxisLabel);
            plot.YLabel(_vm.YAxisLabel);
            plot.HideLegend();
            // Even with no series, re-add any standing annotations so they remain on screen
            // after the user changes Y to something else; the manager handles empty gracefully.
            _annotationManager?.Reapply();
            PlotControl.Refresh();
            HideTooltip();
            return;
        }

        for (int i = 0; i < _vm.Series.Count; i++)
        {
            var s = _vm.Series[i];
            var color = PaletteColors[i % PaletteColors.Length];
            var scatter = plot.Add.Scatter(s.X, s.Y);
            scatter.LegendText = s.Label;
            scatter.Color = color;
            scatter.LineWidth = 2;
            scatter.MarkerSize = _vm.ShowMarkers ? 5 : 0;
            _scatters.Add(scatter);
        }

        plot.Title(_vm.PlotTitle);
        plot.XLabel(_vm.XAxisLabel);
        plot.YLabel(_vm.YAxisLabel);
        if (_vm.ShowLegend)
        {
            plot.ShowLegend(Alignment.UpperRight);
            // Reserve a few px above the top item so ScottPlot doesn't clip its glyph ascenders,
            // without inflating inter-row spacing (see TopHeadroomLegendLayout).
            plot.Legend.Layout = new DatPlotX.Helpers.TopHeadroomLegendLayout();
        }
        else
            plot.HideLegend();

        // Autoscale only when the axis meaning changed (first render, fresh data, or X/Y column
        // change). Otherwise restore the pre-clear viewport so the user's zoom survives. See G1.
        if (!hasRendered || _vm.ConsumeAutoScaleRequest())
            plot.Axes.AutoScale();
        else
            plot.Axes.SetLimits(priorLimits);

        // plot.Clear wiped any previously-added annotation plottables — reapply from the manager.
        _annotationManager?.Reapply();

        PlotControl.Refresh();
        HideTooltip();
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var pos = e.GetPosition(PlotControl);

        // Drag in progress wins over hover tooltip.
        if (_draggingTextId is { } textId)
        {
            SetCursor(PlotCursors.Hand);
            var newCoord = PlotControl.Plot.GetCoordinates(new Pixel((float)pos.X, (float)pos.Y));
            _annotationManager?.UpdateTextPosition(textId, newCoord.X, newCoord.Y);
            HideTooltip();
            e.Handled = true;
            return;
        }

        if (_draggingArrowId is { } arrowId)
        {
            SetCursor(PlotCursors.Hand);
            var current = PlotControl.Plot.GetCoordinates(new Pixel((float)pos.X, (float)pos.Y));
            double dx = current.X - _dragStartMouseCoord.X;
            double dy = current.Y - _dragStartMouseCoord.Y;
            double baseX = _arrowDragMode == 1 ? _arrowDragStartBase.X : _arrowDragStartBase.X + dx;
            double baseY = _arrowDragMode == 1 ? _arrowDragStartBase.Y : _arrowDragStartBase.Y + dy;
            double tipX = _arrowDragMode == 0 ? _arrowDragStartTip.X : _arrowDragStartTip.X + dx;
            double tipY = _arrowDragMode == 0 ? _arrowDragStartTip.Y : _arrowDragStartTip.Y + dy;
            // Mode 0 = drag base only, mode 1 = drag tip only, mode 2 = drag whole arrow.
            if (_arrowDragMode == 0) { baseX = current.X; baseY = current.Y; }
            else if (_arrowDragMode == 1) { tipX = current.X; tipY = current.Y; }
            _annotationManager?.UpdateArrowPosition(arrowId, baseX, baseY, tipX, tipY);
            HideTooltip();
            e.Handled = true;
            return;
        }

        // Hover: show Hand when over a grabbable annotation part, restore arrow otherwise.
        _hoveredTextId = HitTestTextAt(pos);
        _hoveredArrowId = _hoveredTextId is null ? HitTestArrowAt(pos)?.Id : null;
        bool overAnnotation = _hoveredTextId.HasValue || _hoveredArrowId.HasValue;
        // While Ctrl is held the cross cursor signals rectangle-zoom; don't let hover override it.
        if (_ctrlHeld && !overAnnotation)
            SetCursor(PlotCursors.Cross);
        else
            SetCursor(overAnnotation ? PlotCursors.Hand : PlotCursors.Arrow);

        UpdateHoverTooltip(pos);
    }

    private void UpdateHoverTooltip(Point pixelPos)
    {
        if (!HoverTooltipsEnabled) { HideTooltip(); return; }
        if (_scatters.Count == 0 || _vm is null) { HideTooltip(); return; }

        var mousePixel = new Pixel((float)pixelPos.X, (float)pixelPos.Y);
        var mouseCoord = PlotControl.Plot.GetCoordinates(mousePixel);

        Scatter? nearest = null;
        double nearestDist = double.MaxValue;
        foreach (var s in _scatters)
        {
            var n = s.Data.GetNearest(mouseCoord, PlotControl.Plot.LastRender);
            if (!n.IsReal) continue;
            var p = PlotControl.Plot.GetPixel(n.Coordinates);
            var dx = p.X - mousePixel.X;
            var dy = p.Y - mousePixel.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d < nearestDist) { nearestDist = d; nearest = s; }
        }
        if (nearest is null || nearestDist > HoverThresholdPixels) { HideTooltip(); return; }

        var pt = nearest.Data.GetNearest(mouseCoord, PlotControl.Plot.LastRender);
        var label = string.IsNullOrEmpty(nearest.LegendText) ? string.Empty : nearest.LegendText + "\n";
        TooltipText.Text = $"{label}{_vm.XAxisLabel}: {pt.Coordinates.X:N3}\n{_vm.YAxisLabel}: {pt.Coordinates.Y:N3}";
        TooltipBorder.Margin = new Thickness(pixelPos.X + 14, pixelPos.Y - 10, 0, 0);
        TooltipBorder.IsVisible = true;
    }

    private void HideTooltip() => TooltipBorder.IsVisible = false;

    /// <summary>Assign a shared cursor only when it actually changes — avoids native-handle churn
    /// on every pointer move (review M1).</summary>
    private void SetCursor(Cursor cursor)
    {
        if (!ReferenceEquals(PlotControl.Cursor, cursor))
            PlotControl.Cursor = cursor;
    }

    private bool _ctrlHeld;

    private void OnKeyDownForCursor(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl && !_ctrlHeld)
        {
            _ctrlHeld = true;
            SetCursor(PlotCursors.Cross); // rectangle-zoom armed
        }
    }

    private void OnKeyUpForCursor(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            _ctrlHeld = false;
            SetCursor(PlotCursors.Arrow);
        }
    }

    private void OnPlotPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(PlotControl).Properties;
        var pos = e.GetPosition(PlotControl);

        if (props.IsLeftButtonPressed)
        {
            // Hit-test annotations: text first (more common), then arrow endpoints, then body.
            // On hit, disable ScottPlot's input processor so its left-pan response doesn't fight
            // our drag. Re-enabled on release / capture-lost.
            var textId = HitTestTextAt(pos);
            if (textId is { } tid)
            {
                _draggingTextId = tid;
                PlotControl.UserInputProcessor.Disable();
                e.Pointer.Capture(PlotControl);
                e.Handled = true;
                return;
            }
            var arrowHit = HitTestArrowAt(pos);
            if (arrowHit is { } ah)
            {
                _draggingArrowId = ah.Id;
                _arrowDragMode = ah.Mode;
                var model = _annotationManager?.GetArrow(ah.Id);
                if (model is null) { _draggingArrowId = null; return; }
                _arrowDragStartBase = new Coordinates(model.BaseX, model.BaseY);
                _arrowDragStartTip = new Coordinates(model.TipX, model.TipY);
                _dragStartMouseCoord = PlotControl.Plot.GetCoordinates(new Pixel((float)pos.X, (float)pos.Y));
                PlotControl.UserInputProcessor.Disable();
                e.Pointer.Capture(PlotControl);
                e.Handled = true;
                return;
            }
            return;
        }

        if (!props.IsRightButtonPressed) return;

        var coord = PlotControl.Plot.GetCoordinates(new Pixel((float)pos.X, (float)pos.Y));
        _rightClickCoords = (coord.X, coord.Y);
        _rightClickedTextId = HitTestTextAt(pos);
        _rightClickedArrowId = _rightClickedTextId is null ? HitTestArrowAt(pos)?.Id : null;
        // Fallback to the hover hit-test result — see _hoveredTextId field comment.
        if (_rightClickedTextId is null && _rightClickedArrowId is null)
        {
            _rightClickedTextId = _hoveredTextId;
            _rightClickedArrowId = _hoveredArrowId;
        }
        // Set item visibility NOW — a programmatic ContextMenu.Open() does not raise Opening.
        RefreshContextMenuItemState();
        // ScottPlot's input handler captures the press and never raises ContextRequestedEvent
        // (same problem as Stacked-mode PlotPaneControl). Open the menu manually on the next
        // dispatcher tick so the press finishes before Avalonia opens the popup.
        e.Handled = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (PlotControl.ContextMenu is { } menu) menu.Open(PlotControl);
        });
    }

    private void OnPlotPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingTextId is { } tid)
        {
            var model = _annotationManager?.GetText(tid);
            if (model is not null)
                TextAnnotationDragCompleted?.Invoke(tid, model.X, model.Y);
        }
        else if (_draggingArrowId is { } aid)
        {
            var model = _annotationManager?.GetArrow(aid);
            if (model is not null)
                ArrowAnnotationDragCompleted?.Invoke(aid, model.BaseX, model.BaseY, model.TipX, model.TipY);
        }
        CancelDrag();
        if (e.Pointer.Captured == PlotControl) e.Pointer.Capture(null);
    }

    private void CancelDrag()
    {
        if (_draggingTextId is not null || _draggingArrowId is not null)
        {
            // Re-enable SP input processor disabled during the drag in OnPlotPointerPressed.
            PlotControl.UserInputProcessor.Enable();
        }
        _draggingTextId = null;
        _draggingArrowId = null;
    }

    private Guid? HitTestTextAt(Point pos)
    {
        if (_annotationManager is null) return null;
        var plot = PlotControl.Plot;
        foreach (var (id, plottable) in _annotationManager.TextPlottables)
        {
            // Authoritative bounding box once rendered — the primary (and normally only) test.
            var rect = plottable.LabelLastRenderPixelRect;
            if (rect.HasArea)
            {
                if (rect.Contains((float)pos.X, (float)pos.Y))
                    return id;
                continue; // rendered rect is trustworthy; don't widen with the anchor heuristic
            }

            // Genuinely-unrendered case only (first frame before ScottPlot populates the rect):
            // fall back to a modest anchor box. No distance fallback — a wide "nearest text"
            // slop hijacked left-drag panning near any annotation (review G4).
            var p = plot.GetPixel(plottable.Location);
            int longestLine = 1;
            foreach (var line in (plottable.LabelText ?? string.Empty).Split('\n'))
                if (line.Length > longestLine) longestLine = line.Length;
            double hitW = plottable.LabelFontSize * longestLine * 0.7 + 16;
            int lineCount = Math.Max(1, (plottable.LabelText ?? string.Empty).Split('\n').Length);
            double hitH = plottable.LabelFontSize * 1.4 * lineCount + 12;
            if (pos.X >= p.X - hitW / 2 && pos.X <= p.X + hitW / 2 &&
                pos.Y >= p.Y - hitH / 2 && pos.Y <= p.Y + hitH / 2)
                return id;
        }
        return null;
    }

    private (Guid Id, int Mode)? HitTestArrowAt(Point pos)
    {
        if (_annotationManager is null) return null;
        var plot = PlotControl.Plot;
        const double endpointTolerance = 14.0;
        double bodyTolerance = AnnotationHitTolerancePixels;
        (Guid Id, int Mode)? best = null;
        double bestScore = double.MaxValue; // lower = better
        foreach (var (id, arrow) in _annotationManager.ArrowPlottables)
        {
            var baseP = plot.GetPixel(arrow.Base);
            var tipP = plot.GetPixel(arrow.Tip);
            double dBase = Math.Sqrt((baseP.X - pos.X) * (baseP.X - pos.X) + (baseP.Y - pos.Y) * (baseP.Y - pos.Y));
            double dTip = Math.Sqrt((tipP.X - pos.X) * (tipP.X - pos.X) + (tipP.Y - pos.Y) * (tipP.Y - pos.Y));
            // Endpoints win over the body so clicking a tip/base rotates (drags just that end)
            // instead of translating. An endpoint also lies ON the segment, so without this
            // priority the near-zero body distance would override and force a whole-arrow drag.
            if (dBase <= endpointTolerance && dBase < bestScore) { bestScore = dBase; best = (id, 0); }
            if (dTip <= endpointTolerance && dTip < bestScore) { bestScore = dTip; best = (id, 1); }
            // Body — distance from line segment, penalized so endpoints near it still win.
            double dBody = DistancePointToSegment(pos.X, pos.Y, baseP.X, baseP.Y, tipP.X, tipP.Y);
            double bodyScore = dBody + endpointTolerance;
            if (dBody <= bodyTolerance && bodyScore < bestScore) { bestScore = bodyScore; best = (id, 2); }
        }
        return best;
    }

    private static double DistancePointToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double lenSq = dx * dx + dy * dy;
        if (lenSq < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = ((px - x1) * dx + (py - y1) * dy) / lenSq;
        t = Math.Max(0, Math.Min(1, t));
        double sx = x1 + t * dx, sy = y1 + t * dy;
        return Math.Sqrt((px - sx) * (px - sx) + (py - sy) * (py - sy));
    }

    private ContextMenu BuildContextMenu()
    {
        _addTextHereItem = new MenuItem { Header = "Add _Text Annotation Here..." };
        _addArrowHereItem = new MenuItem { Header = "Add _Arrow Annotation Here..." };
        _editTextItem = new MenuItem { Header = "_Edit Text Annotation...", IsVisible = false };
        _deleteTextItem = new MenuItem { Header = "_Delete Text Annotation", IsVisible = false };
        _editArrowItem = new MenuItem { Header = "Edit A_rrow Annotation...", IsVisible = false };
        _deleteArrowItem = new MenuItem { Header = "De_lete Arrow Annotation", IsVisible = false };
        _clearAnnotationsItem = new MenuItem { Header = "_Clear All Annotations" };
        _resetViewItem = new MenuItem { Header = "_Set Scale to Default" };
        _exportImageItem = new MenuItem { Header = "_Export Image..." };
        _annotationsSeparator = new Separator();

        _addTextHereItem.Click += (_, _) =>
        {
            if (_rightClickCoords is { } c) AddTextAnnotationRequested?.Invoke(c.X, c.Y);
        };
        _addArrowHereItem.Click += (_, _) =>
        {
            if (_rightClickCoords is { } c) AddArrowAnnotationRequested?.Invoke(c.X, c.Y);
        };
        _editTextItem.Click += (_, _) =>
        {
            if (_rightClickedTextId is { } id) EditTextAnnotationRequested?.Invoke(id);
        };
        _deleteTextItem.Click += (_, _) =>
        {
            if (_rightClickedTextId is { } id) DeleteTextAnnotationRequested?.Invoke(id);
        };
        _editArrowItem.Click += (_, _) =>
        {
            if (_rightClickedArrowId is { } id) EditArrowAnnotationRequested?.Invoke(id);
        };
        _deleteArrowItem.Click += (_, _) =>
        {
            if (_rightClickedArrowId is { } id) DeleteArrowAnnotationRequested?.Invoke(id);
        };
        _clearAnnotationsItem.Click += (_, _) => _annotationManager?.ClearAll();
        _resetViewItem.Click += (_, _) =>
        {
            PlotControl.Plot.Axes.AutoScale();
            _annotationManager?.Reapply();
            PlotControl.Refresh();
        };
        _exportImageItem.Click += (_, _) => ExportImageRequested?.Invoke(this, EventArgs.Empty);

        var menu = new ContextMenu();
        menu.Items.Add(_addTextHereItem);
        menu.Items.Add(_addArrowHereItem);
        menu.Items.Add(_editTextItem);
        menu.Items.Add(_deleteTextItem);
        menu.Items.Add(_editArrowItem);
        menu.Items.Add(_deleteArrowItem);
        menu.Items.Add(_annotationsSeparator);
        menu.Items.Add(_clearAnnotationsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_resetViewItem);
        menu.Items.Add(_exportImageItem);
        menu.Opening += OnContextMenuOpening;
        _contextMenu = menu;
        return menu;
    }

    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (_annotationManager is null) { e.Cancel = true; return; }
        RefreshContextMenuItemState();
    }

    /// <summary>
    /// Set per-annotation item visibility + enabled state from the current right-click hit-test ids.
    /// Called from <see cref="OnContextMenuOpening"/> AND directly before the manual
    /// <c>ContextMenu.Open()</c> in the right-press handler — Avalonia's <c>Opening</c> event does
    /// NOT fire for a programmatically opened ContextMenu, so without the direct call the Edit /
    /// Delete Annotation items stay stuck at their <c>IsVisible = false</c> default.
    /// </summary>
    private void RefreshContextMenuItemState()
    {
        if (_annotationManager is null) return;

        // The press-time hit-test occasionally misses by a pixel even when hover hit the same
        // annotation. Trust the hover ids as a fallback.
        if (!_rightClickedTextId.HasValue && !_rightClickedArrowId.HasValue)
        {
            _rightClickedTextId = _hoveredTextId;
            _rightClickedArrowId = _hoveredArrowId;
        }

        bool overText = _rightClickedTextId.HasValue;
        bool overArrow = _rightClickedArrowId.HasValue;
        if (_editTextItem is not null) _editTextItem.IsVisible = overText;
        if (_deleteTextItem is not null) _deleteTextItem.IsVisible = overText;
        if (_editArrowItem is not null) _editArrowItem.IsVisible = overArrow;
        if (_deleteArrowItem is not null) _deleteArrowItem.IsVisible = overArrow;
        if (_clearAnnotationsItem is not null)
            _clearAnnotationsItem.IsEnabled = _annotationManager.Count > 0;
        // Add Text/Arrow Here is enabled only when a click position was captured.
        if (_addTextHereItem is not null) _addTextHereItem.IsEnabled = _rightClickCoords.HasValue;
        if (_addArrowHereItem is not null) _addArrowHereItem.IsEnabled = _rightClickCoords.HasValue;
        // Export only makes sense with something plotted.
        if (_exportImageItem is not null) _exportImageItem.IsEnabled = _vm is { Series.Count: > 0 };
    }

    /// <summary>Hand the live ScottPlot <see cref="Plot"/> to the caller. MainWindow uses this to
    /// route image export through <c>IFileOperationsService.ExportGroupedPlotAsync</c>.</summary>
    public Plot GetPlot() => PlotControl.Plot;
}
