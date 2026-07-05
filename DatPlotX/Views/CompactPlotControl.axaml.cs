using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using DatPlotX.Views.Controls;
using OxyPlot;
using OxyPlot.Avalonia;
using OxyAxis = OxyPlot.Axes.Axis;
using System.Diagnostics;
using System.Windows.Input;

namespace DatPlotX.Views;

/// <summary>
/// Hosts the OxyPlot <see cref="PlotView"/> for the Compact Plot Surface and binds it to
/// <see cref="CompactPlotViewModel.PlotModel"/>. View-side responsibilities only — model
/// rebuilds and curve list are owned by the ViewModel.
/// </summary>
public partial class CompactPlotControl : UserControl
{
    private CompactPlotViewModel? _viewModel;
    private PlotView? _plotView;

    private MenuItem? _addCurvesMenuItem;
    private MenuItem? _manageCurveMenuItem;
    private MenuItem? _clearAllCurvesMenuItem;
    private MenuItem? _resetViewMenuItem;
    private MenuItem? _formatPaneMenuItem;
    private MenuItem? _exportImageMenuItem;
    private MenuItem? _addEventLineHereMenuItem;
    private MenuItem? _deleteEventLineMenuItem;
    private MenuItem? _useAsBoundaryMenuItem;
    private MenuItem? _clearEventLinesMenuItem;
    private MenuItem? _addTextHereMenuItem;
    private MenuItem? _addArrowHereMenuItem;
    private MenuItem? _editTextMenuItem;
    private MenuItem? _deleteTextMenuItem;
    private MenuItem? _editArrowMenuItem;
    private MenuItem? _deleteArrowMenuItem;
    private MenuItem? _clearAnnotationsMenuItem;
    private Separator? _eventLineSeparator;
    private Separator? _annotationSeparator;

    private const double AnnotationHitTolerancePixels = 8.0;

    /// <summary>The text annotation under the right-click, if any.</summary>
    private Guid? _rightClickedTextId;
    /// <summary>The arrow annotation under the right-click, if any.</summary>
    private Guid? _rightClickedArrowId;
    /// <summary>The right-click Y data coord (anchor-band-local). Used by Add Annotation Here.</summary>
    private double? _rightClickYData;
    /// <summary>Anchor curve column captured at right-click time — what the new annotation tracks.</summary>
    private string? _rightClickAnchorCurve;

    /// <summary>Text annotation currently being dragged.</summary>
    private Guid? _draggingTextId;
    /// <summary>Arrow annotation currently being dragged + which endpoint (0=base, 1=tip, 2=body).</summary>
    private (Guid Id, int Mode)? _draggingArrow;
    private (double BaseX, double BaseY, double TipX, double TipY) _arrowDragStart;
    private (double X, double Y) _arrowDragStartMouse;

    private PlotController? _controller;
    private CompactPlotHoverTooltipHandler? _hoverTooltipHandler;
    private bool _hoverTooltipsEnabled = true;

    /// <summary>X-axis data coordinate of the most recent right-button press, used by
    /// "Add Event Line Here". Null if the press wasn't on the plot area.</summary>
    private double? _rightClickXData;

    /// <summary>Event line id under the most recent right-click, if any. Drives the
    /// visibility of "Delete Event Line".</summary>
    private Guid? _rightClickedEventLineId;

    /// <summary>Event line id currently being dragged with left-button held.</summary>
    private Guid? _draggingEventLineId;
    private const double EventLineHitTolerancePixels = 6.0;

    /// <summary>X-data coordinate where a Shift+left-drag analysis-segment gesture began. Non-null
    /// only while the drag is in progress; the release emits <see cref="SegmentDefined"/>.</summary>
    private double? _segmentDragStartX;
    /// <summary>Screen-space X (PlotView coords) where the segment drag began — anchors the live
    /// preview rectangle so we don't have to round-trip data→pixel as the axis pans.</summary>
    private double _segmentDragStartPx;
    /// <summary>Live preview rubber band shown while Shift+dragging, before the committed band lands.</summary>
    private Avalonia.Controls.Shapes.Rectangle? _segmentDragVisual;
    /// <summary>Minimum screen-pixel drag distance before a Shift+drag commits a segment (matches Stacked).</summary>
    private const double SegmentDragMinPixels = 3.0;

    /// <summary>Raised when the user completes a Shift+left-drag over the plot, defining an
    /// analysis segment between the two X coordinates. Mirrors <c>PlotPaneControl.SegmentDefined</c>.</summary>
    public event Action<double, double>? SegmentDefined;

    /// <summary>Raised when the user picks "Use as Segment Boundary" on an event line's right-click
    /// menu. Two picks complete an event-line-pair segment. Mirrors the Stacked-pane event.</summary>
    public event Action<Guid>? UseEventLineAsSegmentBoundaryRequested;

    /// <summary>Raised (debounced by the consumer) when the shared X axis pans / zooms, so the
    /// visible-window analysis segment can track the live view range.</summary>
    public event Action? VisibleRangeChanged;

    /// <summary>The X axis we're currently subscribed to for <see cref="VisibleRangeChanged"/>.
    /// Re-subscribed on every <see cref="ApplyModel"/> because <c>Rebuild()</c> recreates axes.</summary>
    private OxyAxis? _subscribedXAxis;

    /// <summary>Callout currently being dragged (event-line id + curve source-column). When
    /// non-null, pointer-move updates the callout's pixel offset on the VM rather than panning.</summary>
    private (Guid EventLineId, string CurveColumn)? _draggingCallout;
    /// <summary>Pointer position (in PlotView coords) at drag start — drag deltas are applied to
    /// the saved offset, so jitter on the first frame doesn't snap the box to the cursor.</summary>
    private Avalonia.Point _calloutDragStartPos;
    /// <summary>Offset on the callout at drag start, so move deltas accumulate cleanly.</summary>
    private (double Dx, double Dy) _calloutDragStartOffset;

    /// <summary>
    /// When true (default), pointer-move on the plot updates a custom hover-tooltip overlay
    /// (Border + TextBlock) that snaps to the nearest visible <see cref="OxyPlot.Series.LineSeries"/>
    /// point. Replaces OxyPlot's built-in <c>HoverSnapTrack</c> path which rebuilt and re-added
    /// the tracker control on every mouse-move while holding the model SyncRoot — that layout
    /// storm caused a macOS UI lockup that only cleared on alt-tab.
    /// </summary>
    public bool HoverTooltipsEnabled
    {
        get => _hoverTooltipsEnabled;
        set
        {
            if (_hoverTooltipsEnabled == value) return;
            _hoverTooltipsEnabled = value;
            if (_hoverTooltipHandler is not null)
            {
                _hoverTooltipHandler.IsEnabled = value;
                if (!value) _hoverTooltipHandler.HideTooltip();
            }
        }
    }

    public CompactPlotControl()
    {
        InitializeComponent();
        Focusable = true;
        _plotView = this.FindControl<PlotView>("PlotView");
        Debug.Assert(_plotView is not null, "PlotView named 'PlotView' missing from CompactPlotControl.axaml");
        if (_plotView is not null)
        {
            _controller = BuildController();
            _plotView.Controller = _controller;
            // OxyPlot.Avalonia's built-in wheel handler drops fractional macOS trackpad
            // deltas — subscribe with handledEventsToo so we still receive the event after
            // PlotView.OnPointerWheelChanged sets e.Handled = true, then synthesize an
            // OxyMouseWheelEventArgs ourselves with a delta scaled to wheel notches.
            _plotView.AddHandler(
                InputElement.PointerWheelChangedEvent,
                OnPointerWheelChanged,
                RoutingStrategies.Bubble | RoutingStrategies.Tunnel,
                handledEventsToo: true);
            // Attach the ContextMenu to the outer UserControl, NOT the PlotView. OxyPlot's
            // PlotBase.OnPointerReleased manually toggles its own ContextMenu.IsVisible on
            // right-button release — bypassing IsOpen — which leaves the popup grab in a
            // stuck state on macOS (all mouse interactions on the surface freeze; alt-tab
            // partially recovers two-finger zoom and pan but right-click stays dead).
            // Avalonia's ContextRequestedEvent is raised by Control.OnPointerReleased and
            // bubbles up to the UserControl, where its attached ContextMenu opens cleanly
            // with no PlotBase interference and no manual menu.Open call.
            ContextMenu = BuildSurfaceContextMenu();

            // Suppress right-button press on the PlotView so PlotBase.OnPointerPressed
            // doesn't call e.Pointer.Capture(this). PlotBase early-exits when e.Handled,
            // so capture is never taken; Control.OnPointerReleased still raises
            // ContextRequestedEvent because it reads e.InitialPressMouseButton, not Handled.
            _plotView.AddHandler(
                InputElement.PointerPressedEvent,
                OnPlotViewPointerPressed,
                RoutingStrategies.Tunnel,
                handledEventsToo: true);

            // Hover tooltip — subscribe with handledEventsToo so we still see moves after
            // OxyPlot's PlotBase marks them handled. Bubble strategy: we want the event to
            // have been processed by the manipulators (pan/zoom) first.
            _plotView.AddHandler(
                InputElement.PointerMovedEvent,
                OnPlotViewPointerMoved,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            _plotView.AddHandler(
                InputElement.PointerExitedEvent,
                OnPlotViewPointerExited,
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        // Drag of an event line is tracked on the outer UserControl so it survives the cursor
        // leaving the PlotView mid-drag; capture is taken in OnPlotViewPointerPressed.
        PointerMoved += OnUserControlPointerMoved;
        PointerReleased += OnUserControlPointerReleased;
        PointerCaptureLost += OnUserControlPointerCaptureLost;
    }

    private void OnUserControlPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_plotView is null) return;

        // Live preview of the Shift+drag analysis segment (screen-space rubber band).
        if (_segmentDragStartX is not null)
        {
            DrawSegmentPreview(e.GetPosition(_plotView).X);
            e.Handled = true;
            return;
        }

        if (_draggingCallout is { } cb)
        {
            var pos = e.GetPosition(_plotView);
            double dx = _calloutDragStartOffset.Dx + (pos.X - _calloutDragStartPos.X);
            double dy = _calloutDragStartOffset.Dy + (pos.Y - _calloutDragStartPos.Y);
            _viewModel?.SetCalloutOffset(cb.EventLineId, cb.CurveColumn, dx, dy);
            var mainVm = GetMainWindowViewModel();
            if (mainVm is not null) mainVm.HasUnsavedChanges = true;
            e.Handled = true;
            return;
        }

        if (_draggingTextId is { } textId && _viewModel is not null)
        {
            var pos = e.GetPosition(_plotView);
            var existing = _viewModel.GetTextAnnotation(textId);
            var xy = TryGetXYDataAt(_plotView, pos, existing?.CompactCurveAnchor);
            if (xy is null) return;
            _viewModel.UpdateTextAnnotationPosition(textId, xy.Value.X, xy.Value.Y);
            e.Handled = true;
            return;
        }

        if (_draggingArrow is { } arrow && _viewModel is not null)
        {
            var pos = e.GetPosition(_plotView);
            var existing = _viewModel.GetArrowAnnotation(arrow.Id);
            var xy = TryGetXYDataAt(_plotView, pos, existing?.CompactCurveAnchor);
            if (xy is null) return;
            double dx = xy.Value.X - _arrowDragStartMouse.X;
            double dy = xy.Value.Y - _arrowDragStartMouse.Y;
            double bx = _arrowDragStart.BaseX, by = _arrowDragStart.BaseY;
            double tx = _arrowDragStart.TipX, ty = _arrowDragStart.TipY;
            switch (arrow.Mode)
            {
                case 0: bx = xy.Value.X; by = xy.Value.Y; break;
                case 1: tx = xy.Value.X; ty = xy.Value.Y; break;
                case 2: bx += dx; by += dy; tx += dx; ty += dy; break;
            }
            _viewModel.UpdateArrowAnnotationPosition(arrow.Id, bx, by, tx, ty);
            e.Handled = true;
            return;
        }

        if (_draggingEventLineId is null) return;
        {
            var pos = e.GetPosition(_plotView);
            var x = TryGetXDataAt(_plotView, pos);
            if (x is null) return;
            var mainVm = GetMainWindowViewModel();
            mainVm?.MoveCompactEventLine(_draggingEventLineId.Value, x.Value);
            e.Handled = true;
        }
    }

    private void OnUserControlPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // Complete a Shift+left-drag analysis segment, if one was in progress.
        if (_segmentDragStartX is { } startX && _plotView is not null)
        {
            var releasePos = e.GetPosition(_plotView);
            var endX = TryGetXDataAt(_plotView, releasePos);
            _segmentDragStartX = null;
            ClearSegmentPreview();
            e.Pointer.Capture(null);
            e.Handled = true;
            // Require a real drag (mirror Stacked's PlotPaneControl): compare in screen pixels,
            // not data coords, so the threshold is zoom-independent and a Shift+click with jitter
            // doesn't spawn a near-zero-width segment.
            if (endX is not null && Math.Abs(releasePos.X - _segmentDragStartPx) >= SegmentDragMinPixels)
                SegmentDefined?.Invoke(Math.Min(startX, endX.Value), Math.Max(startX, endX.Value));
            return;
        }

        bool wasDragging = _draggingEventLineId is not null || _draggingCallout is not null
            || _draggingTextId is not null || _draggingArrow is not null;
        if (!wasDragging) return;
        if (_draggingTextId is not null || _draggingArrow is not null)
        {
            var mainVm = GetMainWindowViewModel();
            if (mainVm is not null) mainVm.HasUnsavedChanges = true;
        }
        _draggingEventLineId = null;
        _draggingCallout = null;
        _draggingTextId = null;
        _draggingArrow = null;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnUserControlPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _segmentDragStartX = null;
        ClearSegmentPreview();
        _draggingEventLineId = null;
        _draggingCallout = null;
        _draggingTextId = null;
        _draggingArrow = null;
    }

    /// <summary>
    /// Build the right-click context menu in code (rather than XAML) so the named MenuItem
    /// fields are populated even though OxyPlot's <see cref="PlotView"/> exposes
    /// <c>ContextMenu</c> via a setter that the Avalonia name generator does not traverse.
    /// </summary>
    private ContextMenu BuildSurfaceContextMenu()
    {
        _addCurvesMenuItem = new MenuItem { Header = "_Add Curves..." };
        _manageCurveMenuItem = new MenuItem { Header = "_Manage Curve..." };
        _clearAllCurvesMenuItem = new MenuItem { Header = "_Clear All Curves" };
        _resetViewMenuItem = new MenuItem { Header = "_Reset View" };
        ToolTip.SetTip(_resetViewMenuItem, "Auto-fit all axes to the data");
        _formatPaneMenuItem = new MenuItem { Header = "_Format Pane..." };
        _exportImageMenuItem = new MenuItem { Header = "_Export Image..." };
        _addEventLineHereMenuItem = new MenuItem { Header = "Add _Event Line Here" };
        _deleteEventLineMenuItem = new MenuItem { Header = "_Delete Event Line", IsVisible = false };
        _useAsBoundaryMenuItem = new MenuItem { Header = "_Use as Segment Boundary", IsVisible = false };
        ToolTip.SetTip(_useAsBoundaryMenuItem, "Pick two event lines to define an analysis segment between them");
        _clearEventLinesMenuItem = new MenuItem { Header = "Clear All E_vent Lines" };
        _eventLineSeparator = new Separator();

        _addCurvesMenuItem.Click += (_, _) => InvokeMainCommand(vm => vm.AddCompactCurvesCommand);
        _manageCurveMenuItem.Click += (_, _) => InvokeMainCommand(vm => vm.ManageCompactCurvesCommand);
        _clearAllCurvesMenuItem.Click += (_, _) => InvokeMainCommand(vm => vm.ClearAllCurvesDispatchCommand);
        _resetViewMenuItem.Click += (_, _) => ResetView();
        _formatPaneMenuItem.Click += (_, _) => InvokeMainCommand(vm => vm.FormatCompactPaneCommand);
        _exportImageMenuItem.Click += (_, _) => InvokeMainCommand(vm => vm.ExportImageCommand);
        _addEventLineHereMenuItem.Click += (_, _) => OnAddEventLineHereClicked();
        _deleteEventLineMenuItem.Click += (_, _) => OnDeleteEventLineClicked();
        _useAsBoundaryMenuItem.Click += (_, _) =>
        {
            if (_rightClickedEventLineId is { } id)
                UseEventLineAsSegmentBoundaryRequested?.Invoke(id);
        };
        _clearEventLinesMenuItem.Click += (_, _) => InvokeMainCommand(vm => vm.ClearEventLinesDispatchCommand);

        _addTextHereMenuItem = new MenuItem { Header = "Add _Text Annotation Here..." };
        _addArrowHereMenuItem = new MenuItem { Header = "Add _Arrow Annotation Here..." };
        _editTextMenuItem = new MenuItem { Header = "Edit Te_xt Annotation...", IsVisible = false };
        _deleteTextMenuItem = new MenuItem { Header = "Delete Te_xt Annotation", IsVisible = false };
        _editArrowMenuItem = new MenuItem { Header = "Edit A_rrow Annotation...", IsVisible = false };
        _deleteArrowMenuItem = new MenuItem { Header = "Delete A_rrow Annotation", IsVisible = false };
        _clearAnnotationsMenuItem = new MenuItem { Header = "Clear All Anno_tations" };
        _annotationSeparator = new Separator();

        _addTextHereMenuItem.Click += (_, _) => OnAddCompactTextAnnotationClicked();
        _addArrowHereMenuItem.Click += (_, _) => OnAddCompactArrowAnnotationClicked();
        _editTextMenuItem.Click += (_, _) => OnEditCompactTextAnnotationClicked();
        _deleteTextMenuItem.Click += (_, _) => OnDeleteCompactTextAnnotationClicked();
        _editArrowMenuItem.Click += (_, _) => OnEditCompactArrowAnnotationClicked();
        _deleteArrowMenuItem.Click += (_, _) => OnDeleteCompactArrowAnnotationClicked();
        _clearAnnotationsMenuItem.Click += (_, _) =>
        {
            _viewModel?.ClearAllAnnotations();
            var mainVm = GetMainWindowViewModel();
            if (mainVm is not null) mainVm.HasUnsavedChanges = true;
        };

        var menu = new ContextMenu();
        menu.Items.Add(_addCurvesMenuItem);
        menu.Items.Add(_manageCurveMenuItem);
        menu.Items.Add(_clearAllCurvesMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_addEventLineHereMenuItem);
        menu.Items.Add(_deleteEventLineMenuItem);
        menu.Items.Add(_useAsBoundaryMenuItem);
        menu.Items.Add(_clearEventLinesMenuItem);
        menu.Items.Add(_eventLineSeparator);
        menu.Items.Add(_addTextHereMenuItem);
        menu.Items.Add(_addArrowHereMenuItem);
        menu.Items.Add(_editTextMenuItem);
        menu.Items.Add(_deleteTextMenuItem);
        menu.Items.Add(_editArrowMenuItem);
        menu.Items.Add(_deleteArrowMenuItem);
        menu.Items.Add(_clearAnnotationsMenuItem);
        menu.Items.Add(_annotationSeparator);
        menu.Items.Add(_resetViewMenuItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(_formatPaneMenuItem);
        menu.Items.Add(_exportImageMenuItem);
        menu.Opening += OnContextMenuOpening;
        return menu;
    }

    private void OnAddCompactTextAnnotationClicked()
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null || _viewModel is null || _rightClickXData is null || _rightClickYData is null) return;
        double x = _rightClickXData.Value;
        double y = _rightClickYData.Value;
        string? anchor = _rightClickAnchorCurve;
        SafeInvokeAsync(async () =>
        {
            var seed = new TextAnnotationModel { X = x, Y = y, Text = "Annotation", CompactCurveAnchor = anchor };
            var result = await mainVm.ShowTextAnnotationDialogAsync(seed);
            if (result is null) return;
            result.X = x; result.Y = y; result.CompactCurveAnchor = anchor;
            _viewModel.AddTextAnnotation(result);
            mainVm.HasUnsavedChanges = true;
            mainVm.StatusText = $"Added text annotation: {result.Text}";
        });
    }

    private void OnAddCompactArrowAnnotationClicked()
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null || _viewModel is null || _rightClickXData is null || _rightClickYData is null) return;
        double x = _rightClickXData.Value;
        double y = _rightClickYData.Value;
        string? anchor = _rightClickAnchorCurve;

        double xSpan = 0, ySpan = 0;
        if (_viewModel.PlotModel is { } model)
        {
            var xAxis = model.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
            if (xAxis is not null) xSpan = (xAxis.ActualMaximum - xAxis.ActualMinimum) * 0.1;
            var yAxis = anchor is null
                ? model.Axes.FirstOrDefault(a => a.Position is OxyPlot.Axes.AxisPosition.Left or OxyPlot.Axes.AxisPosition.Right)
                : model.Axes.FirstOrDefault(a => a.Key == FindAnchorYAxisKey(anchor));
            if (yAxis is not null) ySpan = (yAxis.ActualMaximum - yAxis.ActualMinimum) * 0.1;
        }
        SafeInvokeAsync(async () =>
        {
            var seed = new ArrowAnnotationModel
            {
                BaseX = x - xSpan,
                BaseY = y + ySpan,
                TipX = x,
                TipY = y,
                CompactCurveAnchor = anchor,
            };
            var result = await mainVm.ShowArrowAnnotationDialogAsync(seed);
            if (result is null) return;
            result.BaseX = x - xSpan; result.BaseY = y + ySpan; result.TipX = x; result.TipY = y;
            result.CompactCurveAnchor = anchor;
            _viewModel.AddArrowAnnotation(result);
            mainVm.HasUnsavedChanges = true;
            mainVm.StatusText = "Added arrow annotation";
        });
    }

    private void OnEditCompactTextAnnotationClicked()
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null || _viewModel is null || _rightClickedTextId is null) return;
        Guid id = _rightClickedTextId.Value;
        SafeInvokeAsync(async () =>
        {
            var model = _viewModel.GetTextAnnotation(id);
            if (model is null) return;
            var result = await mainVm.ShowTextAnnotationDialogAsync(model);
            if (result is null) return;
            _viewModel.UpdateTextAnnotation(result);
            mainVm.HasUnsavedChanges = true;
            mainVm.StatusText = "Text annotation updated";
        });
    }

    private void OnDeleteCompactTextAnnotationClicked()
    {
        if (_viewModel is null || _rightClickedTextId is null) return;
        if (_viewModel.RemoveTextAnnotation(_rightClickedTextId.Value))
        {
            var mainVm = GetMainWindowViewModel();
            if (mainVm is not null) { mainVm.HasUnsavedChanges = true; mainVm.StatusText = "Text annotation deleted"; }
        }
    }

    private void OnEditCompactArrowAnnotationClicked()
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null || _viewModel is null || _rightClickedArrowId is null) return;
        Guid id = _rightClickedArrowId.Value;
        SafeInvokeAsync(async () =>
        {
            var model = _viewModel.GetArrowAnnotation(id);
            if (model is null) return;
            var result = await mainVm.ShowArrowAnnotationDialogAsync(model);
            if (result is null) return;
            _viewModel.UpdateArrowAnnotation(result);
            mainVm.HasUnsavedChanges = true;
            mainVm.StatusText = "Arrow annotation updated";
        });
    }

    private void OnDeleteCompactArrowAnnotationClicked()
    {
        if (_viewModel is null || _rightClickedArrowId is null) return;
        if (_viewModel.RemoveArrowAnnotation(_rightClickedArrowId.Value))
        {
            var mainVm = GetMainWindowViewModel();
            if (mainVm is not null) { mainVm.HasUnsavedChanges = true; mainVm.StatusText = "Arrow annotation deleted"; }
        }
    }

    /// <summary>Look up the OxyPlot Y axis key matching a curve source column on the live model.</summary>
    private string? FindAnchorYAxisKey(string? anchorColumn)
    {
        if (_viewModel?.PlotModel is null || anchorColumn is null) return null;
        // The VM names Y axes "__compact_y_{i}" in visible-curve order — match by walking the
        // model's series (each LineSeries has its YAxisKey set) and finding the one whose source
        // column matches the anchor. Match by title since SourceColumn isn't on the series.
        var curveIdx = -1;
        for (int i = 0, vi = 0; i < _viewModel.Curves.Count; i++)
        {
            if (!_viewModel.Curves[i].IsVisible) continue;
            if (_viewModel.Curves[i].SourceColumn == anchorColumn) { curveIdx = vi; break; }
            vi++;
        }
        return curveIdx < 0 ? null : $"__compact_y_{curveIdx}";
    }

    /// <summary>Project pixel pos to data coords using the anchor's banded Y axis.</summary>
    private (double X, double Y)? TryGetXYDataAt(PlotView view, Avalonia.Point pos, string? anchorColumn)
    {
        var model = view.ActualModel;
        if (model is null) return null;
        var xAxis = model.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
        if (xAxis is null) return null;
        var area = model.PlotArea;
        if (pos.X < area.Left || pos.X > area.Right || pos.Y < area.Top || pos.Y > area.Bottom)
            return null;

        var yKey = FindAnchorYAxisKey(anchorColumn);
        var yAxis = yKey is null
            ? model.Axes.FirstOrDefault(a => a.Position is OxyPlot.Axes.AxisPosition.Left or OxyPlot.Axes.AxisPosition.Right)
            : model.Axes.FirstOrDefault(a => a.Key == yKey);
        if (yAxis is null) return null;
        return (xAxis.InverseTransform(pos.X), yAxis.InverseTransform(pos.Y));
    }

    /// <summary>Pick the topmost band (smallest StartPosition value at the given pixel-Y) so the
    /// user-clicked location maps to an obvious anchor curve.</summary>
    private string? PickAnchorCurveAtPixel(PlotView view, Avalonia.Point pos)
    {
        if (_viewModel is null) return null;
        var model = view.ActualModel;
        if (model is null) return null;
        var area = model.PlotArea;
        // OxyPlot StartPosition/EndPosition are fractions of the plot area, 0 = bottom 1 = top.
        // Convert pos.Y to a band fraction.
        double fracY = 1.0 - (pos.Y - area.Top) / area.Height;
        // Walk visible curves in same order as Rebuild — find the band whose [start,end] contains fracY.
        var visible = _viewModel.Curves.Where(c => c.IsVisible).ToList();
        for (int i = 0; i < visible.Count; i++)
        {
            string key = $"__compact_y_{i}";
            var axis = model.Axes.FirstOrDefault(a => a.Key == key);
            if (axis is null) continue;
            if (fracY >= axis.StartPosition && fracY <= axis.EndPosition)
                return visible[i].SourceColumn;
        }
        return visible.Count > 0 ? visible[0].SourceColumn : null;
    }

    private void OnAddEventLineHereClicked()
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null || _rightClickXData is null) return;
        double x = _rightClickXData.Value;
        // Mirror stacked-mode UX: prompt for label + color before adding so the user owns the
        // naming (Compact previously auto-named E1/E2 with no dialog — parity issue).
        SafeInvokeAsync(async () =>
        {
            var suggested = mainVm.CompactPlot.GenerateEventLineLabel();
            var dlg = new AddEventLineDialog(x, suggested);
            var owner = this.GetLogicalAncestors().OfType<Window>().FirstOrDefault();
            bool? ok = owner is not null
                ? await dlg.ShowDialog<bool?>(owner)
                : await dlg.ShowDialog<bool?>(dlg);
            if (ok == true && !string.IsNullOrWhiteSpace(dlg.LabelText))
                mainVm.AddCompactEventLineAt(x, dlg.LabelText, dlg.ColorHex);
        });
    }

    private async void SafeInvokeAsync(Func<System.Threading.Tasks.Task> action)
    {
        try { await action(); }
        catch (Exception ex) { Debug.WriteLine($"[CompactPlotControl] {ex}"); }
    }

    private void OnDeleteEventLineClicked()
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null || _rightClickedEventLineId is null) return;
        mainVm.RemoveCompactEventLine(_rightClickedEventLineId.Value);
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null) return;

        // Sync IsEnabled with current command state so disabled items render greyed-out.
        if (_addCurvesMenuItem is not null)
            _addCurvesMenuItem.IsEnabled = mainVm.AddCompactCurvesCommand.CanExecute(null);
        if (_manageCurveMenuItem is not null)
            _manageCurveMenuItem.IsEnabled = mainVm.ManageCompactCurvesCommand.CanExecute(null);
        if (_clearAllCurvesMenuItem is not null)
            _clearAllCurvesMenuItem.IsEnabled = mainVm.ClearCompactCurvesCommand.CanExecute(null);
        if (_formatPaneMenuItem is not null)
            _formatPaneMenuItem.IsEnabled = mainVm.FormatCompactPaneCommand.CanExecute(null);
        if (_exportImageMenuItem is not null)
            _exportImageMenuItem.IsEnabled = mainVm.ExportImageCommand.CanExecute(null);
        if (_resetViewMenuItem is not null)
            _resetViewMenuItem.IsEnabled = _viewModel?.PlotModel is not null;

        // Event-line items: enable Add when there are curves and a captured X; show Delete only
        // when the press was on an existing line.
        bool hasCurves = _viewModel?.Curves.Count > 0;
        if (_addEventLineHereMenuItem is not null)
            _addEventLineHereMenuItem.IsEnabled = hasCurves && _rightClickXData.HasValue;
        if (_clearEventLinesMenuItem is not null)
            _clearEventLinesMenuItem.IsEnabled = mainVm.ClearCompactEventLinesCommand.CanExecute(null);
        if (_deleteEventLineMenuItem is not null)
            _deleteEventLineMenuItem.IsVisible = _rightClickedEventLineId.HasValue;
        // "Use as Segment Boundary" only when analysis is available and a line is under the cursor.
        if (_useAsBoundaryMenuItem is not null)
            _useAsBoundaryMenuItem.IsVisible = _rightClickedEventLineId.HasValue && mainVm.IsAnalysisAvailable;

        // Annotation items: Add when a click position was captured; Edit/Delete only when the
        // press hit an existing annotation.
        bool canAddAnnotation = hasCurves && _rightClickXData.HasValue && _rightClickYData.HasValue;
        if (_addTextHereMenuItem is not null) _addTextHereMenuItem.IsEnabled = canAddAnnotation;
        if (_addArrowHereMenuItem is not null) _addArrowHereMenuItem.IsEnabled = canAddAnnotation;
        if (_editTextMenuItem is not null) _editTextMenuItem.IsVisible = _rightClickedTextId.HasValue;
        if (_deleteTextMenuItem is not null) _deleteTextMenuItem.IsVisible = _rightClickedTextId.HasValue;
        if (_editArrowMenuItem is not null) _editArrowMenuItem.IsVisible = _rightClickedArrowId.HasValue;
        if (_deleteArrowMenuItem is not null) _deleteArrowMenuItem.IsVisible = _rightClickedArrowId.HasValue;
        if (_clearAnnotationsMenuItem is not null)
            _clearAnnotationsMenuItem.IsEnabled = _viewModel is not null
                && (_viewModel.TextAnnotations.Count > 0 || _viewModel.ArrowAnnotations.Count > 0);
    }

    private void InvokeMainCommand(System.Func<MainWindowViewModel, ICommand> selector)
    {
        var mainVm = GetMainWindowViewModel();
        if (mainVm is null) return;
        var cmd = selector(mainVm);
        if (cmd.CanExecute(null)) cmd.Execute(null);
    }

    private MainWindowViewModel? GetMainWindowViewModel()
    {
        var window = this.GetLogicalAncestors().OfType<Window>().FirstOrDefault();
        return window?.DataContext as MainWindowViewModel;
    }

    private void ResetView()
    {
        if (_plotView is null || _viewModel?.PlotModel is null) return;
        _viewModel.PlotModel.ResetAllAxes();
        _plotView.InvalidatePlot(false);
    }

    private void OnPlotViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not PlotView view) return;
        var props = e.GetCurrentPoint(view).Properties;

        if (props.IsLeftButtonPressed)
        {
            var leftPos = e.GetPosition(view);

            // Shift+left-drag = define an analysis segment (mirrors Stacked panes). Takes
            // priority over pan/annotation drags so it never collides with the Ctrl path
            // (rubber-band zoom). The release emits SegmentDefined; suppress pan meanwhile.
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                var sx = TryGetXDataAt(view, leftPos);
                if (sx is not null)
                {
                    _segmentDragStartX = sx;
                    _segmentDragStartPx = leftPos.X;
                    e.Pointer.Capture(this);
                    e.Handled = true;
                }
                return;
            }

            // Priority: callout box (small target) > text annotation > arrow endpoint > event line.
            var calloutHit = HitTestCalloutAtPoint(view, leftPos);
            if (calloutHit is { } cb)
            {
                _draggingCallout = cb;
                _calloutDragStartPos = leftPos;
                _calloutDragStartOffset = GetCalloutOffset(cb.EventLineId, cb.CurveColumn);
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            var textHit = HitTestTextAnnotationAt(view, leftPos);
            if (textHit is { } tid)
            {
                _draggingTextId = tid;
                e.Pointer.Capture(this);
                e.Handled = true;
                return;
            }

            var arrowHit = HitTestArrowAnnotationAt(view, leftPos);
            if (arrowHit is { } ah && _viewModel is not null)
            {
                var model = _viewModel.GetArrowAnnotation(ah.Id);
                if (model is not null)
                {
                    _draggingArrow = ah;
                    _arrowDragStart = (model.BaseX, model.BaseY, model.TipX, model.TipY);
                    var xy = TryGetXYDataAt(view, leftPos, model.CompactCurveAnchor);
                    _arrowDragStartMouse = xy ?? (model.BaseX, model.BaseY);
                    e.Pointer.Capture(this);
                    e.Handled = true;
                    return;
                }
            }

            var hit = HitTestEventLineAtPoint(view, leftPos);
            if (hit.HasValue)
            {
                _draggingEventLineId = hit.Value;
                e.Pointer.Capture(this);
                e.Handled = true; // suppress pan
            }
            return;
        }

        if (!props.IsRightButtonPressed) return;

        // Right-press: record the X-data coordinate so "Add Event Line Here" knows where to drop,
        // and run a hit test so "Delete Event Line" only appears when a line is under the cursor.
        var rightPos = e.GetPosition(view);
        _rightClickXData = TryGetXDataAt(view, rightPos);
        _rightClickedEventLineId = HitTestEventLineAtPoint(view, rightPos);
        _rightClickAnchorCurve = PickAnchorCurveAtPixel(view, rightPos);
        var anchorXY = TryGetXYDataAt(view, rightPos, _rightClickAnchorCurve);
        _rightClickYData = anchorXY?.Y;
        _rightClickedTextId = HitTestTextAnnotationAt(view, rightPos);
        _rightClickedArrowId = _rightClickedTextId is null
            ? HitTestArrowAnnotationAt(view, rightPos)?.Id
            : null;

        // Mark right-press handled so PlotBase.OnPointerPressed early-exits and skips
        // e.Pointer.Capture(this). The ContextMenu is attached to the outer UserControl;
        // Control.OnPointerReleased raises ContextRequestedEvent (it reads
        // InitialPressMouseButton, not Handled), which bubbles up and auto-opens it.
        e.Handled = true;
    }

    /// <summary>
    /// Walk <see cref="CompactPlotViewModel.EventLines"/>, project each line's X to screen space
    /// via the shared X axis, and return the id of the line within
    /// <see cref="EventLineHitTolerancePixels"/> of the cursor (closest wins). Returns null when
    /// no line is in range or the model is empty.
    /// </summary>
    private Guid? HitTestEventLineAtPoint(PlotView view, Avalonia.Point pos)
    {
        if (_viewModel is null || _viewModel.EventLines.Count == 0) return null;
        var model = view.ActualModel;
        if (model is null) return null;

        var xAxis = model.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey) as OxyAxis;
        if (xAxis is null) return null;

        Guid? closestId = null;
        double closestDx = EventLineHitTolerancePixels;
        foreach (var ev in _viewModel.EventLines)
        {
            if (!ev.IsVisible) continue;
            double sx = xAxis.Transform(ev.XPosition);
            double dx = Math.Abs(sx - pos.X);
            if (dx <= closestDx)
            {
                closestDx = dx;
                closestId = ev.Id;
            }
        }
        return closestId;
    }

    /// <summary>
    /// Walk every <see cref="DatPlotX.Views.Compact.CompactCalloutAnnotation"/> in the model and
    /// return the (event-line id, curve column) of the one whose label box contains the cursor.
    /// Returns null when no callout is hit.
    /// </summary>
    private (Guid EventLineId, string CurveColumn)? HitTestCalloutAtPoint(PlotView view, Avalonia.Point pos)
    {
        var model = view.ActualModel;
        if (model is null) return null;

        foreach (var annotation in model.Annotations)
        {
            if (annotation is not DatPlotX.Views.Compact.CompactCalloutAnnotation callout) continue;
            if (callout.LastLabelBounds is not { } box) continue;
            if (!box.Contains(pos.X, pos.Y)) continue;
            var parsed = CompactPlotViewModel.TryParseCalloutTag(callout.Tag);
            if (parsed is { } p) return p;
        }
        return null;
    }

    private (double Dx, double Dy) GetCalloutOffset(Guid eventLineId, string curveColumn)
    {
        if (_viewModel is null) return (0, 0);
        var ev = _viewModel.EventLines.FirstOrDefault(e => e.Id == eventLineId);
        if (ev is null) return (0, 0);
        return ev.CompactCalloutOffsets.TryGetValue(curveColumn, out var off)
            ? (off.Dx, off.Dy)
            : (0, 0);
    }

    /// <summary>Hit-test text annotations: returns the id of the closest text whose TextPosition
    /// pixel-projects within <see cref="AnnotationHitTolerancePixels"/> (x4 for forgiveness — text
    /// has no width API).</summary>
    private Guid? HitTestTextAnnotationAt(PlotView view, Avalonia.Point pos)
    {
        var model = view.ActualModel;
        if (model is null) return null;
        Guid? best = null;
        double bestDist = AnnotationHitTolerancePixels * 4;
        var xAxis = model.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
        if (xAxis is null) return null;
        foreach (var ann in model.Annotations)
        {
            if (ann is not OxyPlot.Annotations.TextAnnotation t) continue;
            var id = CompactPlotViewModel.TryParseTextAnnotationTag(t.Tag);
            if (id is null) continue;
            var yAxis = model.Axes.FirstOrDefault(a => a.Key == t.YAxisKey);
            if (yAxis is null) continue;
            double sx = xAxis.Transform(t.TextPosition.X);
            double sy = yAxis.Transform(t.TextPosition.Y);
            double d = Math.Sqrt((sx - pos.X) * (sx - pos.X) + (sy - pos.Y) * (sy - pos.Y));
            if (d < bestDist) { bestDist = d; best = id; }
        }
        return best;
    }

    /// <summary>Hit-test arrow annotations: returns (id, mode) where mode is 0=base, 1=tip, 2=body.
    /// Endpoints have a wider tolerance than the body so the user can grab them reliably even when
    /// the line passes close to the cursor.</summary>
    private (Guid Id, int Mode)? HitTestArrowAnnotationAt(PlotView view, Avalonia.Point pos)
    {
        var model = view.ActualModel;
        if (model is null) return null;
        var xAxis = model.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
        if (xAxis is null) return null;
        const double endpointTolerance = 14.0;
        const double bodyTolerance = AnnotationHitTolerancePixels;
        (Guid Id, int Mode)? best = null;
        double bestScore = double.MaxValue; // lower = better
        // Walk every arrow plottable in the model (forward + reverse share the same tag id).
        foreach (var ann in model.Annotations)
        {
            if (ann is not OxyPlot.Annotations.ArrowAnnotation a) continue;
            var id = CompactPlotViewModel.TryParseArrowAnnotationTag(a.Tag);
            if (id is null) continue;
            var yAxis = model.Axes.FirstOrDefault(ax => ax.Key == a.YAxisKey);
            if (yAxis is null) continue;
            double bx = xAxis.Transform(a.StartPoint.X), by = yAxis.Transform(a.StartPoint.Y);
            double tx = xAxis.Transform(a.EndPoint.X), ty = yAxis.Transform(a.EndPoint.Y);
            double dBase = Math.Sqrt((bx - pos.X) * (bx - pos.X) + (by - pos.Y) * (by - pos.Y));
            double dTip = Math.Sqrt((tx - pos.X) * (tx - pos.X) + (ty - pos.Y) * (ty - pos.Y));
            // Endpoints win when within their tolerance. Score them above the body so a click near
            // an endpoint always picks the endpoint (not body drag).
            if (dBase <= endpointTolerance && dBase < bestScore)
            { bestScore = dBase; best = (id.Value, 0); }
            if (dTip <= endpointTolerance && dTip < bestScore)
            { bestScore = dTip; best = (id.Value, 1); }
            double dBody = DistancePointToSegment(pos.X, pos.Y, bx, by, tx, ty);
            // Penalize body hits so endpoints near body don't get overruled.
            double bodyScore = dBody + endpointTolerance;
            if (dBody <= bodyTolerance && bodyScore < bestScore)
            { bestScore = bodyScore; best = (id.Value, 2); }
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

    /// <summary>Project the cursor screen X back to a data X via the compact X axis.</summary>
    private static double? TryGetXDataAt(PlotView view, Avalonia.Point pos)
    {
        var model = view.ActualModel;
        if (model is null) return null;
        var xAxis = model.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey) as OxyAxis;
        if (xAxis is null) return null;
        // Reject clicks outside the plot area (the legend / axis gutter shouldn't drop a line).
        var area = model.PlotArea;
        if (pos.X < area.Left || pos.X > area.Right || pos.Y < area.Top || pos.Y > area.Bottom)
            return null;
        return xAxis.InverseTransform(pos.X);
    }

    private void OnPlotViewPointerMoved(object? sender, PointerEventArgs e)
        => _hoverTooltipHandler?.OnPointerMoved(e);

    private void OnPlotViewPointerExited(object? sender, PointerEventArgs e)
        => _hoverTooltipHandler?.OnPointerExited();

    private double _wheelAccumulator;

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var view = _plotView;
        if (view?.ActualController is null || view.ActualModel is null) return;

        var pos = e.GetPosition(view);
        var screenPoint = new ScreenPoint(pos.X, pos.Y);

        double rawDelta = e.Delta.Y != 0 ? e.Delta.Y : e.Delta.X;
        if (rawDelta == 0) return;

        // Accumulate fractional trackpad deltas so they eventually cross the ±120 threshold.
        _wheelAccumulator += rawDelta * 120;
        int delta = (int)_wheelAccumulator;
        if (delta == 0) return;
        _wheelAccumulator -= delta;

        var modifiers = OxyModifierKeys.None;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= OxyModifierKeys.Control;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= OxyModifierKeys.Shift;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= OxyModifierKeys.Alt;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Meta)) modifiers |= OxyModifierKeys.Windows;

        var args = new OxyMouseWheelEventArgs
        {
            Position = screenPoint,
            Delta = delta,
            ModifierKeys = modifiers,
        };

        view.ActualController.HandleMouseWheel(view, args);
        e.Handled = true;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as CompactPlotViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyModel();
            EnsureHoverHandler();
        }
    }

    private void EnsureHoverHandler()
    {
        if (_hoverTooltipHandler is not null || _viewModel is null) return;

        var border = this.FindControl<Border>("hoverTooltipBorder");
        var text = this.FindControl<TextBlock>("hoverTooltipText");
        if (_plotView is null || border is null || text is null) return;

        _hoverTooltipHandler = new CompactPlotHoverTooltipHandler(_plotView, _viewModel, border, text)
        {
            IsEnabled = _hoverTooltipsEnabled,
        };
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        // Mirror every subscription from the constructor — without this the inner PlotView
        // keeps a strong reference to this control's handlers, leaking the entire VM graph
        // when the surface is recreated (mode switch, project reload).
        if (_plotView is not null)
        {
            _plotView.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged);
            _plotView.RemoveHandler(InputElement.PointerPressedEvent, OnPlotViewPointerPressed);
            _plotView.RemoveHandler(InputElement.PointerMovedEvent, OnPlotViewPointerMoved);
            _plotView.RemoveHandler(InputElement.PointerExitedEvent, OnPlotViewPointerExited);
            _plotView = null;
        }

        PointerMoved -= OnUserControlPointerMoved;
        PointerReleased -= OnUserControlPointerReleased;
        PointerCaptureLost -= OnUserControlPointerCaptureLost;
        DataContextChanged -= OnDataContextChanged;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;

        if (_subscribedXAxis is not null)
        {
#pragma warning disable CS0618 // Axis.AxisChanged: v4.0 forward-deprecation (#111); no replacement in OxyPlot 2.x
            _subscribedXAxis.AxisChanged -= OnXAxisChanged;
#pragma warning restore CS0618
            _subscribedXAxis = null;
        }

        if (ContextMenu is { } menu)
        {
            menu.Opening -= OnContextMenuOpening;
            ContextMenu = null;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
        _hoverTooltipHandler?.HideTooltip();
        _hoverTooltipHandler = null;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompactPlotViewModel.PlotModel))
            ApplyModel();
    }

    private void ApplyModel()
    {
        if (_plotView is null || _viewModel is null) return;
        _plotView.Model = _viewModel.PlotModel;
        _plotView.InvalidatePlot(true);
        SubscribeXAxisChanged();
    }

    /// <summary>(Re)subscribe to the live X axis's <c>AxisChanged</c> event so pan/zoom drives
    /// <see cref="VisibleRangeChanged"/>. The axis instance is recreated by the VM's <c>Rebuild()</c>,
    /// so detach the prior one first.</summary>
    private void SubscribeXAxisChanged()
    {
#pragma warning disable CS0618 // Axis.AxisChanged: v4.0 forward-deprecation (#111); no replacement in OxyPlot 2.x
        if (_subscribedXAxis is not null)
        {
            _subscribedXAxis.AxisChanged -= OnXAxisChanged;
            _subscribedXAxis = null;
        }
        var xAxis = _viewModel?.PlotModel?.Axes.FirstOrDefault(a => a.Key == CompactPlotViewModel.XAxisKey);
        if (xAxis is null) return;
        _subscribedXAxis = xAxis;
        _subscribedXAxis.AxisChanged += OnXAxisChanged;
#pragma warning restore CS0618
    }

    private void OnXAxisChanged(object? sender, OxyPlot.Axes.AxisChangedEventArgs e)
        => VisibleRangeChanged?.Invoke();

    /// <summary>Draw / update the full-height rubber band between the drag start pixel and the
    /// current cursor pixel, clamped to the plot area. Screen-space so it tracks the cursor
    /// instantly without a data→pixel round-trip. Replaced by the committed band on release.</summary>
    private void DrawSegmentPreview(double currentPx)
    {
        var overlay = this.FindControl<Avalonia.Controls.Canvas>("segmentOverlay");
        if (overlay is null || _plotView is null) return;
        var area = _plotView.ActualModel?.PlotArea;
        if (area is null) return;

        double x1 = Math.Clamp(_segmentDragStartPx, area.Value.Left, area.Value.Right);
        double x2 = Math.Clamp(currentPx, area.Value.Left, area.Value.Right);
        double left = Math.Min(x1, x2);
        double width = Math.Abs(x2 - x1);

        if (_segmentDragVisual is null)
        {
            _segmentDragVisual = new Avalonia.Controls.Shapes.Rectangle
            {
                Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x33, 0xFF, 0xD4, 0x3B)),
                Stroke = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(0x99, 0xFF, 0xD4, 0x3B)),
                StrokeThickness = 1,
                IsHitTestVisible = false,
            };
            overlay.Children.Add(_segmentDragVisual);
        }

        _segmentDragVisual.Width = width;
        _segmentDragVisual.Height = area.Value.Height;
        Avalonia.Controls.Canvas.SetLeft(_segmentDragVisual, left);
        Avalonia.Controls.Canvas.SetTop(_segmentDragVisual, area.Value.Top);
    }

    private void ClearSegmentPreview()
    {
        if (_segmentDragVisual is null) return;
        var overlay = this.FindControl<Avalonia.Controls.Canvas>("segmentOverlay");
        overlay?.Children.Remove(_segmentDragVisual);
        _segmentDragVisual = null;
    }

    /// <summary>
    /// Custom <see cref="IPlotController"/> with bindings that mirror the Stacked-Panes
    /// (ScottPlot) shortcuts so muscle memory carries between modes:
    /// • Left-drag = pan
    /// • Ctrl+Left-drag = rubber-band zoom rectangle (matches Stacked behavior)
    /// • Mouse wheel / two-finger trackpad scroll = zoom at cursor
    /// • Cmd/Ctrl + wheel = fine zoom
    /// • Middle button or double-click = reset
    /// Per-axis zoom is automatic in OxyPlot: pointing at an axis routes wheel/drag to that axis only.
    ///
    /// Hover tracker is intentionally NOT bound here — the custom Avalonia overlay handler
    /// (<see cref="CompactPlotHoverTooltipHandler"/>) replaces <c>HoverSnapTrack</c> to
    /// avoid OxyPlot's per-mouse-move Canvas child swap that caused UI lockups.
    /// </summary>
    private static PlotController BuildController()
    {
        var c = new PlotController();
        c.UnbindAll();

        c.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
        c.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Control, PlotCommands.ZoomRectangle);
        c.BindMouseDown(OxyMouseButton.Middle, PlotCommands.ResetAt);
        c.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.None, 2, PlotCommands.ResetAt);

        c.BindMouseWheel(PlotCommands.ZoomWheel);
        c.BindMouseWheel(OxyModifierKeys.Control, PlotCommands.ZoomWheelFine);
        // macOS Cmd reports as OxyModifierKeys.Windows in OxyPlot; bind to fine-zoom
        // so Mac users with a trackpad get a precise zoom step.
        c.BindMouseWheel(OxyModifierKeys.Windows, PlotCommands.ZoomWheelFine);

        c.BindKeyDown(OxyKey.Home, PlotCommands.Reset);

        return c;
    }
}
