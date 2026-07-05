using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DatPlotX.ViewModels;
using DatPlotX.Views.Controls;
using ScottPlot.Avalonia;

namespace DatPlotX.Views;

/// <summary>
/// Interaction logic for PlotPaneControl.axaml
/// </summary>
public partial class PlotPaneControl : UserControl
{
    private PlotPaneViewModel? _viewModel;
    private double _contextMenuXPosition;
    /// <summary>Y data coord of the most recent right-click (Y1 axis). Used by "Add Text/Arrow
    /// Annotation Here" so the new annotation drops exactly under the cursor.</summary>
    private double _contextMenuY1Position;
    private double _contextMenuY2Position;
    private Action? _onPlotUpdatedHandler;

    // Handlers for input, dragging, hit testing, and hover tooltips
    private PlotPaneInputHandler? _inputHandler;
    private PlotPaneDragHandler? _dragHandler;
    private PlotPaneHitTestService? _hitTestService;
    private PlotPaneHoverTooltipHandler? _hoverTooltipHandler;

    // Stashed when HoverTooltipsEnabled is set before _hoverTooltipHandler is constructed
    // (Loaded fires before SetViewModel in some Avalonia template-recycling orderings).
    // Applied inside SetViewModel right after the handler is created.
    private bool? _pendingHoverEnabled;

    // Arrow annotation context menu state
    private Guid? _rightClickedArrowId;

    // Text annotation context menu state
    private Guid? _rightClickedTextId;

    // Event line context menu state — id of the line under the most recent right-press,
    // surfaced via the "Delete Event Line" menu item.
    private Guid? _rightClickedEventLineId;

    // Event for X-axis changes
    public event Action<double, double>? XAxisChanged;

    // Event for mouse position updates with coordinates
    // Parameters: x, y1, y2, hasY2Curves
    public event Action<double, double, double, bool>? MousePositionChanged;

    // Events for context menu actions
    public event Action? AddCurvesToPaneRequested;
    public event Action? FormatCurveRequested;
    public event Action? ClearPaneRequested;
    public event Action? AddPaneRequested;
    public event Action? RemovePaneRequested;
    public event Action? FormatPaneRequested;
    public event Action? ExportImageRequested;
    public event Action<double>? AddEventLineAtPositionRequested;
    public event Action? ClearEventLinesRequested;
    public event Action<Guid>? DeleteEventLineRequested;

    // Right-clicked event line chosen as an analysis-segment boundary. Two picks → segment.
    public event Action<Guid>? UseEventLineAsSegmentBoundaryRequested;

    // Event for callout drag completion (calloutId, newOffsetX, newOffsetY)
    public event Action<Guid, double, double>? CalloutDragCompleted;

    // Analysis segment defined via Shift+drag (xMin, xMax in data coords). Host turns this
    // into an AnalysisService.DefineSegment call.
    public event Action<double, double>? SegmentDefined;

    // Shift+drag rubber-band state for defining an analysis segment.
    private bool _segmentDragActive;
    private Avalonia.Point _segmentDragStart;
    private Avalonia.Controls.Shapes.Rectangle? _segmentDragVisual;

    // Per-frame event line drag (eventLineId, newXPosition). Lets the host live-sync the line
    // across every other pane so the stack stays vertically aligned during the drag.
    public event Action<Guid, double>? EventLineDragMoved;

    // Event for event line drag completion (eventLineId, newXPosition)
    public event Action<Guid, double>? EventLineDragCompleted;

    // Event for text annotation drag completion (annotationId, newX, newY)
    public event Action<Guid, double, double>? TextAnnotationDragCompleted;

    // Event for arrow annotation drag completion (annotationId, baseX, baseY, tipX, tipY)
    public event Action<Guid, double, double, double, double>? ArrowAnnotationDragCompleted;

    // Events for context menu annotation actions
    public event Action<double, double>? AddTextAnnotationRequested;
    public event Action<double, double>? AddArrowAnnotationRequested;
    public event Action<Guid>? EditTextAnnotationRequested;
    public event Action<Guid>? DeleteTextAnnotationRequested;
    public event Action<Guid>? EditArrowAnnotationRequested;
    public event Action<Guid>? DeleteArrowAnnotationRequested;

    public PlotPaneControl()
    {
        InitializeComponent();

        // Enable keyboard focus for key events
        Focusable = true;

        // Wire up context menu item clicks (menu items live for the control's lifetime;
        // their Click lambdas are released with the control). avaPlot input events are
        // wired/unwired symmetrically in Loaded/Unloaded so a re-attached control re-wires
        // and a detached one does not leak through avaPlot's event lists.
        WireContextMenuEvents();

        // Wire up loaded event to notify main window
        this.Loaded += PlotPaneControl_Loaded;
        this.Unloaded += PlotPaneControl_Unloaded;
    }

    // Guards against double-subscribing avaPlot input events if Loaded fires more than once
    // without an intervening Unloaded.
    private bool _avaPlotEventsWired;

    private void WireAvaPlotInputEvents()
    {
        if (_avaPlotEventsWired) return;
        _avaPlotEventsWired = true;

        avaPlot.PointerWheelChanged += AvaPlot_PointerWheelChanged;
        avaPlot.PointerPressed += AvaPlot_PointerPressed;
        avaPlot.PointerReleased += AvaPlot_PointerReleased;
        avaPlot.PointerMoved += AvaPlot_PointerMoved;
        avaPlot.PointerExited += AvaPlot_PointerExited;
        avaPlot.KeyDown += AvaPlot_KeyDown;
        avaPlot.KeyUp += AvaPlot_KeyUp;

        if (avaPlot.ContextMenu != null)
        {
            avaPlot.ContextMenu.Opening += ContextMenu_Opening;
        }
    }

    private void UnwireAvaPlotInputEvents()
    {
        if (!_avaPlotEventsWired) return;
        _avaPlotEventsWired = false;

        avaPlot.PointerWheelChanged -= AvaPlot_PointerWheelChanged;
        avaPlot.PointerPressed -= AvaPlot_PointerPressed;
        avaPlot.PointerReleased -= AvaPlot_PointerReleased;
        avaPlot.PointerMoved -= AvaPlot_PointerMoved;
        avaPlot.PointerExited -= AvaPlot_PointerExited;
        avaPlot.KeyDown -= AvaPlot_KeyDown;
        avaPlot.KeyUp -= AvaPlot_KeyUp;

        if (avaPlot.ContextMenu != null)
        {
            avaPlot.ContextMenu.Opening -= ContextMenu_Opening;
        }
    }

    private void PlotPaneControl_Unloaded(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null && _onPlotUpdatedHandler != null)
        {
            _viewModel.OnPlotUpdated -= _onPlotUpdatedHandler;
            _onPlotUpdatedHandler = null;
        }

        UnwireAvaPlotInputEvents();
    }

    private void PlotPaneControl_Loaded(object? sender, RoutedEventArgs e)
    {
        WireAvaPlotInputEvents();

        // Walk up the visual tree to find MainWindow and notify it
        var mainWindow = this.GetVisualAncestors().OfType<MainWindow>().FirstOrDefault();
        mainWindow?.OnPlotPaneControlLoaded(this);
    }

    private void WireContextMenuEvents()
    {
        AddCurvesToPaneItem.Click += (s, e) => AddCurvesToPaneRequested?.Invoke();
        FormatCurveItem.Click += (s, e) => FormatCurveRequested?.Invoke();
        ClearPaneItem.Click += (s, e) => ClearPaneRequested?.Invoke();
        AddEventLineHereItem.Click += (s, e) => AddEventLineAtPositionRequested?.Invoke(_contextMenuXPosition);
        UseAsSegmentBoundaryItem.Click += (s, e) =>
        {
            if (_rightClickedEventLineId.HasValue)
                UseEventLineAsSegmentBoundaryRequested?.Invoke(_rightClickedEventLineId.Value);
        };
        DeleteEventLineItem.Click += (s, e) =>
        {
            if (_rightClickedEventLineId.HasValue)
                DeleteEventLineRequested?.Invoke(_rightClickedEventLineId.Value);
        };
        ClearEventLinesItem.Click += (s, e) => ClearEventLinesRequested?.Invoke();
        AddPaneItem.Click += (s, e) => AddPaneRequested?.Invoke();
        RemovePaneItem.Click += (s, e) => RemovePaneRequested?.Invoke();
        FormatPaneItem.Click += (s, e) => FormatPaneRequested?.Invoke();
        ExportImageItem.Click += (s, e) => ExportImageRequested?.Invoke();

        SetScaleToDefaultItem.Click += (s, e) =>
        {
            if (_viewModel?.PlotModel == null) return;
            _viewModel.PlotModel.Axes.AutoScale();
            avaPlot.Refresh();
            Dispatcher.UIThread.Post(() => NotifyXAxisChanged(), DispatcherPriority.Background);
        };

        // Drop the new annotation at the exact mouse position captured during right-press —
        // previously this used the midpoint of the Y axis range, which placed the annotation
        // far from the cursor whenever the user right-clicked anywhere off-center.
        AddTextAnnotationHereItem.Click += (s, e) =>
        {
            if (_viewModel?.PlotModel != null)
                AddTextAnnotationRequested?.Invoke(_contextMenuXPosition, _contextMenuY1Position);
        };

        AddArrowAnnotationHereItem.Click += (s, e) =>
        {
            if (_viewModel?.PlotModel != null)
                AddArrowAnnotationRequested?.Invoke(_contextMenuXPosition, _contextMenuY1Position);
        };

        EditTextMenuItem.Click += (s, e) =>
        {
            if (_rightClickedTextId.HasValue)
                EditTextAnnotationRequested?.Invoke(_rightClickedTextId.Value);
        };

        DeleteTextMenuItem.Click += (s, e) =>
        {
            if (_rightClickedTextId.HasValue)
                DeleteTextAnnotationRequested?.Invoke(_rightClickedTextId.Value);
        };

        EditArrowMenuItem.Click += (s, e) =>
        {
            if (_rightClickedArrowId.HasValue)
                EditArrowAnnotationRequested?.Invoke(_rightClickedArrowId.Value);
        };

        DeleteArrowMenuItem.Click += (s, e) =>
        {
            if (_rightClickedArrowId.HasValue)
                DeleteArrowAnnotationRequested?.Invoke(_rightClickedArrowId.Value);
        };

        // ContextMenu.Opening is wired in WireAvaPlotInputEvents (Loaded) so it unwires
        // symmetrically in Unloaded — see the leak fix there.
    }

    private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Three independent populations of _rightClickedTextId / _rightClickedArrowId fight
        // platform timing on macOS Avalonia + ScottPlot:
        //   (a) AvaPlot_PointerPressed sets them on the right-press itself (fresh coords).
        //   (b) The drag handler's hover hit-test keeps HoveredTextId / HoveredArrowId in
        //       sync on every pointer-move — if the Hand cursor is showing, hover hit.
        //   (c) Last-resort: re-run the hit-test here against _lastPointerPosition.
        // We OR them together so any one succeeding surfaces the Edit / Delete items. This
        // is necessary because Avalonia can open the ContextMenu via ContextRequestedEvent
        // independently of our manual Dispatcher.Post(menu.Open), and on certain frames the
        // press-time hit-test misses by a pixel even though hover saw the same annotation.
        if (!_rightClickedTextId.HasValue && !_rightClickedArrowId.HasValue && _dragHandler != null)
        {
            _rightClickedTextId = _dragHandler.HoveredTextId;
            _rightClickedArrowId = _dragHandler.HoveredArrowId;
        }
        if (!_rightClickedTextId.HasValue && !_rightClickedArrowId.HasValue && _hitTestService != null)
        {
            float mx = (float)(_lastPointerPosition.X * avaPlot.DisplayScale);
            float my = (float)(_lastPointerPosition.Y * avaPlot.DisplayScale);
            var (tid, _) = _hitTestService.GetTextAnnotationUnderMouse(mx, my);
            if (tid.HasValue) _rightClickedTextId = tid;
            else
            {
                var (aid, _, _) = _hitTestService.GetArrowAnnotationUnderMouse(mx, my);
                if (aid.HasValue) _rightClickedArrowId = aid;
            }
        }

        UpdateAnnotationMenuItemVisibility();
    }

    /// <summary>
    /// Show/hide the per-annotation and per-event-line context menu items based on what the most
    /// recent right-press hit-tested onto. Called from <see cref="ContextMenu_Opening"/> AND
    /// directly from the right-press handler before we open the menu — because Avalonia's
    /// <c>ContextMenu.Opening</c> event does NOT fire when the menu is opened programmatically via
    /// <c>ContextMenu.Open()</c> (which is how we open it, since ScottPlot captures the pointer and
    /// suppresses the automatic ContextRequested path). Relying on Opening alone left the Edit /
    /// Delete Annotation items stuck at their XAML <c>IsVisible="False"</c> default.
    /// </summary>
    private void UpdateAnnotationMenuItemVisibility()
    {
        EditTextMenuItem.IsVisible = _rightClickedTextId.HasValue;
        DeleteTextMenuItem.IsVisible = _rightClickedTextId.HasValue;
        EditArrowMenuItem.IsVisible = _rightClickedArrowId.HasValue;
        DeleteArrowMenuItem.IsVisible = _rightClickedArrowId.HasValue;
        DeleteEventLineItem.IsVisible = _rightClickedEventLineId.HasValue;
        UseAsSegmentBoundaryItem.IsVisible = _rightClickedEventLineId.HasValue;
    }

    /// <summary>Set by MainWindow.OnPlotPaneRightClick when a line is under the cursor.</summary>
    internal void SetRightClickedEventLine(Guid? id) => _rightClickedEventLineId = id;

    // Track last pointer position for context menu opening
    private Avalonia.Point _lastPointerPosition;

    /// <summary>
    /// Set the ViewModel for this pane control
    /// </summary>
    public void SetViewModel(PlotPaneViewModel viewModel)
    {
        // If a previous VM was wired to this control, detach its handler first.
        if (_viewModel != null && _onPlotUpdatedHandler != null)
        {
            _viewModel.OnPlotUpdated -= _onPlotUpdatedHandler;
            _onPlotUpdatedHandler = null;
        }

        _viewModel = viewModel;
        DataContext = _viewModel;

        // Wire up the Plot instance
        _viewModel.PlotModel = avaPlot.Plot;

        // Subscribe to plot update events — store delegate so we can unsubscribe.
        _onPlotUpdatedHandler = () => avaPlot.Refresh();
        _viewModel.OnPlotUpdated += _onPlotUpdatedHandler;

        // Disable ScottPlot's default right-click context menu handler
        avaPlot.UserInputProcessor.RemoveAll<ScottPlot.Interactivity.UserActionResponses.SingleClickContextMenu>();

        // Disable ScottPlot's built-in context menu so our XAML-defined one works
        avaPlot.Menu = null!;

        // Configure custom interactions
        avaPlot.UserInputProcessor.UserActionResponses.Clear();

        // Add middle-click pan
        var middlePan = new ScottPlot.Interactivity.UserActionResponses.MouseDragPan(
            ScottPlot.Interactivity.StandardMouseButtons.Middle);
        avaPlot.UserInputProcessor.UserActionResponses.Add(middlePan);

        // Initialize handlers
        _inputHandler = new PlotPaneInputHandler(avaPlot, _viewModel);
        _hitTestService = new PlotPaneHitTestService(avaPlot, _viewModel);
        _dragHandler = new PlotPaneDragHandler(avaPlot, _viewModel, _hitTestService, overlayCanvas);
        _hoverTooltipHandler = new PlotPaneHoverTooltipHandler(avaPlot, _viewModel, hoverTooltipBorder, hoverTooltipText);
        if (_pendingHoverEnabled is { } pending)
        {
            _hoverTooltipHandler.IsEnabled = pending;
            if (!pending) _hoverTooltipHandler.HideTooltip();
            _pendingHoverEnabled = null;
        }

        // Wire up input handler events
        _inputHandler.XAxisSyncRequested += () =>
        {
            Dispatcher.UIThread.Post(() => NotifyXAxisChanged(), DispatcherPriority.Background);
        };
        _inputHandler.MousePositionChanged += (x, y1, y2, hasY2) => MousePositionChanged?.Invoke(x, y1, y2, hasY2);

        // Wire up drag handler events
        _dragHandler.XAxisSyncRequested += () =>
        {
            Dispatcher.UIThread.Post(() => NotifyXAxisChanged(), DispatcherPriority.Background);
        };
        _dragHandler.CalloutDragCompleted += (id, x, y) => CalloutDragCompleted?.Invoke(id, x, y);
        _dragHandler.EventLineDragMoved += (id, x) => EventLineDragMoved?.Invoke(id, x);
        _dragHandler.EventLineDragCompleted += (id, x) => EventLineDragCompleted?.Invoke(id, x);
        _dragHandler.TextAnnotationDragCompleted += (id, x, y) => TextAnnotationDragCompleted?.Invoke(id, x, y);
        _dragHandler.ArrowAnnotationDragCompleted += (id, bx, by, tx, ty) => ArrowAnnotationDragCompleted?.Invoke(id, bx, by, tx, ty);

        // Initialize the plot
        _viewModel.InitializePlot();
    }

    /// <summary>
    /// Enable or disable hover tooltips for this pane
    /// </summary>
    public bool HoverTooltipsEnabled
    {
        get => _hoverTooltipHandler?.IsEnabled ?? _pendingHoverEnabled ?? true;
        set
        {
            if (_hoverTooltipHandler != null)
            {
                _hoverTooltipHandler.IsEnabled = value;
                if (!value) _hoverTooltipHandler.HideTooltip();
            }
            else
            {
                // Handler not constructed yet (Loaded fired before SetViewModel) — stash so
                // we apply once SetViewModel creates the handler.
                _pendingHoverEnabled = value;
            }
        }
    }

    /// <summary>
    /// Refresh the plot display
    /// </summary>
    public void RefreshPlot()
    {
        avaPlot.Refresh();
    }

    /// <summary>
    /// Notify that the X-axis range has changed (called externally)
    /// </summary>
    public void NotifyXAxisChanged()
    {
        if (_viewModel?.PlotModel == null)
            return;

        if (_viewModel.PaneModel.XAxisSynchronized)
        {
            var range = _viewModel.PlotModel.Axes.Bottom.Range;
            XAxisChanged?.Invoke(range.Min, range.Max);
        }
    }

    /// <summary>
    /// Get the underlying AvaPlot control
    /// </summary>
    public AvaPlot GetAvaPlot() => avaPlot;

    /// <summary>
    /// Get the ViewModel
    /// </summary>
    public PlotPaneViewModel? GetViewModel() => _viewModel;

    /// <summary>
    /// Handle pointer wheel events (zoom) to trigger X-axis synchronization
    /// </summary>
    private void AvaPlot_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        _inputHandler?.HandleMouseWheel(e);
    }

    /// <summary>
    /// Handle pointer released events
    /// </summary>
    private void AvaPlot_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_segmentDragActive)
        {
            _segmentDragActive = false;
            e.Pointer.Capture(null);
            ClearSegmentBand();

            var end = e.GetPosition(avaPlot);
            // Ignore an accidental click (no meaningful drag).
            if (_viewModel?.PlotModel != null && Math.Abs(end.X - _segmentDragStart.X) >= 3)
            {
                double x1 = PixelToDataX(_segmentDragStart.X);
                double x2 = PixelToDataX(end.X);
                SegmentDefined?.Invoke(Math.Min(x1, x2), Math.Max(x1, x2));
            }
            e.Handled = true;
            return;
        }

        if (_dragHandler != null && _inputHandler != null)
        {
            e.Handled = _dragHandler.HandleMouseUp(e, _inputHandler.IsCtrlPressed, _inputHandler.IsAltPressed);
        }
    }

    /// <summary>Convert a control-space X (unscaled) to a data X coordinate on the bottom axis.</summary>
    private double PixelToDataX(double controlX)
    {
        float px = (float)(controlX * avaPlot.DisplayScale);
        // Y is irrelevant for the X coordinate; reuse the start Y to build a valid pixel.
        var pixel = new ScottPlot.Pixel(px, (float)(_segmentDragStart.Y * avaPlot.DisplayScale));
        return avaPlot.Plot.GetCoordinates(pixel).X;
    }

    private void DrawSegmentBand(Avalonia.Point start, Avalonia.Point current)
    {
        double left = Math.Min(start.X, current.X);
        double width = Math.Abs(current.X - start.X);

        if (_segmentDragVisual is null)
        {
            _segmentDragVisual = new Avalonia.Controls.Shapes.Rectangle
            {
                Stroke = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x99, 0xFF, 0xD4, 0x3B)),
                StrokeThickness = 1,
                Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x33, 0xFF, 0xD4, 0x3B)),
            };
            overlayCanvas.Children.Add(_segmentDragVisual);
        }

        _segmentDragVisual.Width = width;
        _segmentDragVisual.Height = overlayCanvas.Bounds.Height;
        Avalonia.Controls.Canvas.SetLeft(_segmentDragVisual, left);
        Avalonia.Controls.Canvas.SetTop(_segmentDragVisual, 0);
    }

    private void ClearSegmentBand()
    {
        if (_segmentDragVisual is not null)
        {
            overlayCanvas.Children.Remove(_segmentDragVisual);
            _segmentDragVisual = null;
        }
    }

    /// <summary>
    /// Handle pointer pressed - check for right-click on event line and handle left-click drag start
    /// </summary>
    private void AvaPlot_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Capture mouse position for context menu
        if (_viewModel?.PlotModel != null)
        {
            var mousePos = e.GetPosition(avaPlot);
            _lastPointerPosition = mousePos;

            // Check for right-click
            if (e.GetCurrentPoint(avaPlot).Properties.IsRightButtonPressed)
            {
                ScottPlot.Pixel mousePixel = new((float)(mousePos.X * avaPlot.DisplayScale), (float)(mousePos.Y * avaPlot.DisplayScale));
                var plotCoordinates = avaPlot.Plot.GetCoordinates(mousePixel);
                _contextMenuXPosition = plotCoordinates.X;
                _contextMenuY1Position = plotCoordinates.Y;
                // Y2 coord uses the Right axis when present; fall back to Y1 otherwise.
                var y2Coords = avaPlot.Plot.GetCoordinates(mousePixel, avaPlot.Plot.Axes.Bottom, avaPlot.Plot.Axes.Right);
                _contextMenuY2Position = y2Coords.Y;

                // Hit-test annotations RIGHT HERE on the fresh press coords. ContextMenu_Opening
                // runs one dispatcher tick later and the cached pointer position is unreliable
                // (Avalonia can fire pointer-move between the press and the menu opening).
                _rightClickedTextId = null;
                _rightClickedArrowId = null;
                if (_hitTestService != null)
                {
                    var (textId, _) = _hitTestService.GetTextAnnotationUnderMouse(mousePixel.X, mousePixel.Y);
                    if (textId.HasValue)
                    {
                        _rightClickedTextId = textId;
                    }
                    else
                    {
                        var (arrowId, _, _) = _hitTestService.GetArrowAnnotationUnderMouse(mousePixel.X, mousePixel.Y);
                        if (arrowId.HasValue) _rightClickedArrowId = arrowId;
                    }
                }

                // Fallback: if the press-time hit-test missed but the hover hit-test was
                // showing the Hand cursor, the user clearly clicked on the annotation.
                // Trust the hovered id so the Edit / Delete menu items surface.
                if (!_rightClickedTextId.HasValue && !_rightClickedArrowId.HasValue && _dragHandler != null)
                {
                    _rightClickedTextId = _dragHandler.HoveredTextId;
                    _rightClickedArrowId = _dragHandler.HoveredArrowId;
                }

                // Notify MainWindow about right-click for event line deletion check (sets
                // _rightClickedEventLineId so the event-line items can show too).
                var mainWindow = this.GetVisualAncestors().OfType<MainWindow>().FirstOrDefault();
                mainWindow?.OnPlotPaneRightClick(this, e);

                // If the event was handled (e.g., event line deletion), don't show context menu
                if (!e.Handled)
                {
                    // Set the per-annotation / event-line item visibility NOW from the fresh
                    // press-time hit-test ids. ContextMenu.Opening does not fire on a programmatic
                    // ContextMenu.Open(), so this is the only reliable place to do it.
                    UpdateAnnotationMenuItemVisibility();

                    // Manually open our context menu since ScottPlot's OnPointerPressed
                    // captures the pointer, preventing Avalonia's automatic context menu
                    e.Handled = true;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (avaPlot.ContextMenu != null)
                        {
                            avaPlot.ContextMenu.Open(avaPlot);
                        }
                    });
                }
                return;
            }
        }

        // Shift+left-drag defines an analysis segment over an X-range. Intercept before the
        // drag handler so it doesn't pan. Read the modifier off the event (stateless/reliable).
        if (_viewModel?.PlotModel != null
            && e.GetCurrentPoint(avaPlot).Properties.IsLeftButtonPressed
            && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            _segmentDragActive = true;
            _segmentDragStart = e.GetPosition(avaPlot);
            e.Pointer.Capture(avaPlot);
            e.Handled = true;
            return;
        }

        if (_dragHandler != null && _inputHandler != null)
        {
            e.Handled = _dragHandler.HandleMouseDown(e, _inputHandler.IsCtrlPressed, _inputHandler.IsAltPressed);
        }
    }

    /// <summary>
    /// Handle pointer move
    /// </summary>
    private void AvaPlot_PointerMoved(object? sender, PointerEventArgs e)
    {
        _lastPointerPosition = e.GetPosition(avaPlot);

        // Live segment-define rubber-band (full-height vertical band over the X-range).
        if (_segmentDragActive)
        {
            DrawSegmentBand(_segmentDragStart, e.GetPosition(avaPlot));
            e.Handled = true;
            return;
        }

        // Delegate drag handling to drag handler
        if (_dragHandler != null && _inputHandler != null)
        {
            bool handled = _dragHandler.HandleMouseMove(e, _inputHandler.IsCtrlPressed, _inputHandler.IsAltPressed);
            if (handled)
            {
                _hoverTooltipHandler?.HideTooltip();
                return;
            }
        }

        // Handle crosshair coordinate display when CTRL is pressed
        if (_inputHandler != null && _inputHandler.IsCtrlPressed && _viewModel?.PlotModel != null)
        {
            var mousePos = e.GetPosition(avaPlot);
            float mouseX = (float)(mousePos.X * avaPlot.DisplayScale);
            float mouseY = (float)(mousePos.Y * avaPlot.DisplayScale);

            ScottPlot.Pixel pixel = new(mouseX, mouseY);

            var coordinatesY1 = avaPlot.Plot.GetCoordinates(pixel, avaPlot.Plot.Axes.Bottom, avaPlot.Plot.Axes.Left);
            var coordinatesY2 = avaPlot.Plot.GetCoordinates(pixel, avaPlot.Plot.Axes.Bottom, avaPlot.Plot.Axes.Right);

            bool hasY2Curves = _viewModel.PaneModel.ShowY2Axis;
            MousePositionChanged?.Invoke(coordinatesY1.X, coordinatesY1.Y, coordinatesY2.Y, hasY2Curves);
        }

        // Update hover tooltip (suppressed when CTRL held for crosshair mode)
        _hoverTooltipHandler?.OnPointerMoved(e, _inputHandler?.IsCtrlPressed ?? false);
    }

    private void AvaPlot_PointerExited(object? sender, PointerEventArgs e)
    {
        _hoverTooltipHandler?.OnPointerExited();
    }

    /// <summary>
    /// Handle key down events
    /// </summary>
    private void AvaPlot_KeyDown(object? sender, KeyEventArgs e)
    {
        _inputHandler?.HandleKeyDown(e);
    }

    /// <summary>
    /// Handle key up events
    /// </summary>
    private void AvaPlot_KeyUp(object? sender, KeyEventArgs e)
    {
        _inputHandler?.HandleKeyUp(e);
    }
}
