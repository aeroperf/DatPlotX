using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;
using AvaloniaRectangle = Avalonia.Controls.Shapes.Rectangle;
using DatPlotX.ViewModels;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Handles all drag operations for plot pane interactions.
/// Manages callout dragging, event line dragging, text/arrow annotation dragging, zoom box, and panning.
/// Uses Avalonia pointer events instead of WPF mouse events.
/// </summary>
public class PlotPaneDragHandler
{
    private readonly AvaPlot _avaPlot;
    private readonly PlotPaneViewModel _viewModel;
    private readonly PlotPaneHitTestService _hitTestService;
    private readonly Canvas _overlayCanvas;

    // Zoom box and pan state
    private Point? _zoomBoxStart;
    private Point? _panStart;
    private Point? _leftClickPanStart;
    private AvaloniaRectangle? _zoomBoxVisual;

    // Callout dragging state
    private Callout? _calloutBeingDragged;
    private ScottPlot.PixelOffset _calloutDragOffset;

    // Event line dragging state
    private Guid? _eventLineBeingDragged;

    // Text annotation dragging state
    private Guid? _textAnnotationBeingDragged;
    private ScottPlot.PixelOffset _textAnnotationDragOffset;

    // Arrow annotation dragging state
    private Guid? _arrowAnnotationBeingDragged;
    private bool _isDraggingArrowTip;
    private ScottPlot.Coordinates _arrowDragStartBase;
    private ScottPlot.Coordinates _arrowDragStartTip;

    // Track if left button is pressed (Avalonia doesn't have MouseButtonState)
    private bool _isLeftButtonPressed;

    // Last hover hit-test result. Read by PlotPaneControl on right-click as a fallback —
    // the press-time hit-test can miss on certain frames (e.g. ScottPlot hasn't populated
    // LabelLastRenderPixelRect yet, or the press pixel lands one pixel outside the rect).
    // If the hand cursor is showing, hover hit-test succeeded; reuse that id so the Edit /
    // Delete menu items surface reliably.
    public Guid? HoveredTextId { get; private set; }
    public Guid? HoveredArrowId { get; private set; }

    /// <summary>
    /// Raised when X-axis should be synchronized after pan or zoom
    /// </summary>
    public event Action? XAxisSyncRequested;

    /// <summary>
    /// Raised when callout drag is completed
    /// </summary>
    public event Action<Guid, double, double>? CalloutDragCompleted;

    /// <summary>
    /// Raised on every pointer-move frame while an event line is being dragged. Lets the host
    /// propagate the new X to every other pane in the stack so the lines stay vertically
    /// aligned during the drag (the local-only <see cref="PlotPaneViewModel.MoveGlobalEventLine"/>
    /// call only touches the pane the drag started on).
    /// </summary>
    public event Action<Guid, double>? EventLineDragMoved;

    /// <summary>
    /// Raised when event line drag is completed
    /// </summary>
    public event Action<Guid, double>? EventLineDragCompleted;

    /// <summary>
    /// Raised when text annotation drag is completed
    /// </summary>
    public event Action<Guid, double, double>? TextAnnotationDragCompleted;

    /// <summary>
    /// Raised when arrow annotation drag is completed
    /// </summary>
    public event Action<Guid, double, double, double, double>? ArrowAnnotationDragCompleted;

    public PlotPaneDragHandler(
        AvaPlot avaPlot,
        PlotPaneViewModel viewModel,
        PlotPaneHitTestService hitTestService,
        Canvas overlayCanvas)
    {
        _avaPlot = avaPlot;
        _viewModel = viewModel;
        _hitTestService = hitTestService;
        _overlayCanvas = overlayCanvas;
    }

    /// <summary>
    /// Handle pointer pressed to initiate drag operations.
    /// Uses PointerPressedEventArgs instead of WPF MouseButtonEventArgs.
    /// </summary>
    public bool HandleMouseDown(PointerPressedEventArgs e, bool isCtrlPressed, bool isAltPressed)
    {
        var point = e.GetCurrentPoint(_avaPlot);
        if (!point.Properties.IsLeftButtonPressed)
            return false;

        _isLeftButtonPressed = true;
        var mousePos = e.GetPosition(_avaPlot);
        float mouseX = (float)(mousePos.X * _avaPlot.DisplayScale);
        float mouseY = (float)(mousePos.Y * _avaPlot.DisplayScale);

        // Priority -2: Check if clicking on a text annotation for dragging
        var (textAnnotationId, textAnnotation) = _hitTestService.GetTextAnnotationUnderMouse(mouseX, mouseY);
        if (textAnnotationId.HasValue && textAnnotation != null)
        {
            _textAnnotationBeingDragged = textAnnotationId.Value;

            var textPixel = _viewModel.PlotModel!.GetPixel(textAnnotation.Location);
            float dX = textPixel.X - mouseX;
            float dY = textPixel.Y - mouseY;
            _textAnnotationDragOffset = new ScottPlot.PixelOffset(dX, dY);

            _avaPlot.UserInputProcessor.Disable();
            _avaPlot.Cursor = PlotCursors.Hand;
            return true;
        }

        // Priority -1.5: Check if clicking on an arrow annotation for dragging
        var (arrowAnnotationId, arrowAnnotation, nearTip) = _hitTestService.GetArrowAnnotationUnderMouse(mouseX, mouseY);
        if (arrowAnnotationId.HasValue && arrowAnnotation != null)
        {
            _arrowAnnotationBeingDragged = arrowAnnotationId.Value;
            _isDraggingArrowTip = nearTip;
            _arrowDragStartBase = arrowAnnotation.Base;
            _arrowDragStartTip = arrowAnnotation.Tip;

            _avaPlot.UserInputProcessor.Disable();
            _avaPlot.Cursor = PlotCursors.Hand;
            return true;
        }

        // Priority -1: Check if clicking on an event line for dragging
        var eventLineId = _hitTestService.GetEventLineUnderMouse(mouseX, mouseY);
        if (eventLineId.HasValue)
        {
            _eventLineBeingDragged = eventLineId.Value;
            _avaPlot.UserInputProcessor.Disable();
            _avaPlot.Cursor = PlotCursors.SizeWestEast;
            return true;
        }

        // Priority 0: Check if clicking on a callout for dragging
        var calloutUnderMouse = _hitTestService.GetCalloutUnderMouse(mouseX, mouseY);
        if (calloutUnderMouse != null)
        {
            _calloutBeingDragged = calloutUnderMouse;

            float dX = calloutUnderMouse.TextPixel.X - mouseX;
            float dY = calloutUnderMouse.TextPixel.Y - mouseY;
            _calloutDragOffset = new ScottPlot.PixelOffset(dX, dY);

            _avaPlot.UserInputProcessor.Disable();
            _avaPlot.Cursor = PlotCursors.Hand;

            _viewModel.PlotModel?.MoveToFront(calloutUnderMouse);
            return true;
        }

        // Priority 1: CTRL+click for zoom box
        if (isCtrlPressed)
        {
            _zoomBoxStart = mousePos;
            return true;
        }

        // Priority 2: ALT+click for pan
        if (isAltPressed)
        {
            _panStart = mousePos;
            _avaPlot.Cursor = PlotCursors.SizeAll;
            return true;
        }

        // Priority 3: Left-click pan (no modifiers)
        _leftClickPanStart = mousePos;
        _avaPlot.Cursor = PlotCursors.SizeAll;
        return true;
    }

    /// <summary>
    /// Handle pointer move to update drag operations.
    /// Uses PointerEventArgs instead of WPF MouseEventArgs.
    /// </summary>
    public bool HandleMouseMove(PointerEventArgs e, bool isCtrlPressed, bool isAltPressed)
    {
        var mousePos = e.GetPosition(_avaPlot);
        float mouseX = (float)(mousePos.X * _avaPlot.DisplayScale);
        float mouseY = (float)(mousePos.Y * _avaPlot.DisplayScale);

        // Priority -3: Text annotation dragging in progress
        if (_textAnnotationBeingDragged.HasValue)
        {
            if (_viewModel.PlotModel == null)
                return false;

            ScottPlot.Pixel newPixel = new(mouseX + _textAnnotationDragOffset.X, mouseY + _textAnnotationDragOffset.Y);
            var newCoordinates = _avaPlot.Plot.GetCoordinates(newPixel);

            _viewModel.UpdateTextAnnotationPosition(_textAnnotationBeingDragged.Value, newCoordinates.X, newCoordinates.Y);
            _avaPlot.Refresh();
            return true;
        }

        // Priority -2: Arrow annotation dragging in progress
        if (_arrowAnnotationBeingDragged.HasValue)
        {
            if (_viewModel.PlotModel == null)
                return false;

            ScottPlot.Pixel mousePixel = new(mouseX, mouseY);
            var mouseCoords = _avaPlot.Plot.GetCoordinates(mousePixel);

            var arrow = _viewModel.GetArrowAnnotation(_arrowAnnotationBeingDragged.Value);
            if (arrow != null)
            {
                if (_isDraggingArrowTip)
                {
                    arrow.Tip = mouseCoords;
                }
                else
                {
                    var initialMidX = (_arrowDragStartBase.X + _arrowDragStartTip.X) / 2;
                    var initialMidY = (_arrowDragStartBase.Y + _arrowDragStartTip.Y) / 2;

                    arrow.Base = new ScottPlot.Coordinates(
                        _arrowDragStartBase.X + (mouseCoords.X - initialMidX),
                        _arrowDragStartBase.Y + (mouseCoords.Y - initialMidY));
                    arrow.Tip = new ScottPlot.Coordinates(
                        _arrowDragStartTip.X + (mouseCoords.X - initialMidX),
                        _arrowDragStartTip.Y + (mouseCoords.Y - initialMidY));
                }
            }
            _avaPlot.Refresh();
            return true;
        }

        // Priority -1: Event line dragging in progress
        if (_eventLineBeingDragged.HasValue)
        {
            if (_viewModel.PlotModel == null)
                return false;

            ScottPlot.Pixel mousePixel = new(mouseX, mouseY);
            var dataCoords = _avaPlot.Plot.GetCoordinates(mousePixel);

            _viewModel.MoveGlobalEventLine(_eventLineBeingDragged.Value, dataCoords.X);
            // Notify the host so the same X gets pushed to every other pane this frame —
            // without this, lines drift apart vertically during the drag.
            EventLineDragMoved?.Invoke(_eventLineBeingDragged.Value, dataCoords.X);
            _avaPlot.Refresh();
            return true;
        }

        // Priority 0: Callout dragging in progress
        if (_calloutBeingDragged != null)
        {
            ScottPlot.Pixel mousePixel = new(mouseX + _calloutDragOffset.X, mouseY + _calloutDragOffset.Y);
            var newCoordinates = _calloutBeingDragged.Axes.GetCoordinates(mousePixel);

            _calloutBeingDragged.TextCoordinates = newCoordinates;
            _avaPlot.Refresh();
            return true;
        }

        // Handle cursor updates for hover states
        if (!_isLeftButtonPressed && _calloutBeingDragged == null)
        {
            UpdateCursorForHover(mouseX, mouseY, isCtrlPressed, isAltPressed);
        }

        // Priority 1: Zoom box drawing (CTRL + Left drag)
        if (_zoomBoxStart.HasValue && _isLeftButtonPressed && isCtrlPressed)
        {
            DrawZoomBox(_zoomBoxStart.Value, mousePos);
            return true;
        }

        // Priority 2: Pan in progress (ALT + Left drag)
        if (_panStart.HasValue && _isLeftButtonPressed && isAltPressed)
        {
            PerformPan(_panStart.Value, mousePos);
            _panStart = mousePos;
            return true;
        }

        // Priority 3: Left-click pan (no modifiers)
        if (_leftClickPanStart.HasValue && _isLeftButtonPressed && !isCtrlPressed && !isAltPressed)
        {
            PerformPan(_leftClickPanStart.Value, mousePos);
            _leftClickPanStart = mousePos;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handle pointer released to complete drag operations.
    /// Uses PointerReleasedEventArgs instead of WPF MouseButtonEventArgs.
    /// </summary>
    public bool HandleMouseUp(PointerReleasedEventArgs e, bool isCtrlPressed, bool isAltPressed)
    {
        var point = e.GetCurrentPoint(_avaPlot);
        bool isLeftRelease = e.InitialPressMouseButton == MouseButton.Left;
        bool isMiddleRelease = e.InitialPressMouseButton == MouseButton.Middle;

        _isLeftButtonPressed = false;

        // Handle text annotation drag completion
        if (isLeftRelease && _textAnnotationBeingDragged.HasValue)
        {
            if (_viewModel.PlotModel != null)
            {
                float mouseX = (float)(e.GetPosition(_avaPlot).X * _avaPlot.DisplayScale);
                float mouseY = (float)(e.GetPosition(_avaPlot).Y * _avaPlot.DisplayScale);
                ScottPlot.Pixel finalPixel = new(mouseX + _textAnnotationDragOffset.X, mouseY + _textAnnotationDragOffset.Y);
                var finalCoords = _avaPlot.Plot.GetCoordinates(finalPixel);

                TextAnnotationDragCompleted?.Invoke(_textAnnotationBeingDragged.Value, finalCoords.X, finalCoords.Y);
            }

            _avaPlot.UserInputProcessor.Enable();
            _avaPlot.Cursor = PlotCursors.Arrow;
            _textAnnotationBeingDragged = null;
            _avaPlot.Refresh();
            return true;
        }

        // Handle arrow annotation drag completion
        if (isLeftRelease && _arrowAnnotationBeingDragged.HasValue)
        {
            if (_viewModel.PlotModel != null)
            {
                var arrow = _viewModel.GetArrowAnnotation(_arrowAnnotationBeingDragged.Value);
                if (arrow != null)
                {
                    ArrowAnnotationDragCompleted?.Invoke(
                        _arrowAnnotationBeingDragged.Value,
                        arrow.Base.X, arrow.Base.Y,
                        arrow.Tip.X, arrow.Tip.Y);
                }
            }

            _avaPlot.UserInputProcessor.Enable();
            _avaPlot.Cursor = PlotCursors.Arrow;
            _arrowAnnotationBeingDragged = null;
            _avaPlot.Refresh();
            return true;
        }

        // Handle callout drag completion
        if (isLeftRelease && _calloutBeingDragged != null)
        {
            var tipCoord = _calloutBeingDragged.TipCoordinates;
            var textCoord = _calloutBeingDragged.TextCoordinates;
            double offsetX = tipCoord.X - textCoord.X;
            double offsetY = tipCoord.Y - textCoord.Y;

            var calloutId = _viewModel.FindCalloutId(_calloutBeingDragged);
            if (calloutId.HasValue)
            {
                CalloutDragCompleted?.Invoke(calloutId.Value, offsetX, offsetY);
            }

            _avaPlot.UserInputProcessor.Enable();
            _avaPlot.Cursor = PlotCursors.Arrow;
            _calloutBeingDragged = null;
            _avaPlot.Refresh();
            return true;
        }

        // Handle event line drag completion
        if (isLeftRelease && _eventLineBeingDragged.HasValue)
        {
            if (_viewModel.PlotModel != null)
            {
                float mouseX = (float)(e.GetPosition(_avaPlot).X * _avaPlot.DisplayScale);
                float mouseY = (float)(e.GetPosition(_avaPlot).Y * _avaPlot.DisplayScale);
                ScottPlot.Pixel mousePixel = new(mouseX, mouseY);
                var finalCoords = _avaPlot.Plot.GetCoordinates(mousePixel);

                EventLineDragCompleted?.Invoke(_eventLineBeingDragged.Value, finalCoords.X);
            }

            _avaPlot.UserInputProcessor.Enable();
            _avaPlot.Cursor = PlotCursors.Arrow;
            _eventLineBeingDragged = null;
            _avaPlot.Refresh();
            return true;
        }

        // Handle zoom box completion
        if (isLeftRelease && _zoomBoxStart.HasValue && isCtrlPressed)
        {
            ApplyZoomBox(_zoomBoxStart.Value, e.GetPosition(_avaPlot));
            ClearZoomBox();
            _zoomBoxStart = null;
            _avaPlot.Cursor = isCtrlPressed ? PlotCursors.Cross : PlotCursors.Arrow;
            XAxisSyncRequested?.Invoke();
            return true;
        }

        // Handle ALT+pan completion
        if (isLeftRelease && _panStart.HasValue && isAltPressed)
        {
            _panStart = null;
            _avaPlot.Cursor = PlotCursors.Arrow;
            XAxisSyncRequested?.Invoke();
            return true;
        }

        // Handle left-click pan completion
        if (isLeftRelease && _leftClickPanStart.HasValue)
        {
            _leftClickPanStart = null;
            _avaPlot.Cursor = PlotCursors.Arrow;
            XAxisSyncRequested?.Invoke();
            return true;
        }

        // Handle middle-click pan synchronization
        if (isMiddleRelease)
        {
            XAxisSyncRequested?.Invoke();
            return false;
        }

        return false;
    }

    /// <summary>
    /// Update cursor based on what's under the mouse
    /// </summary>
    private void UpdateCursorForHover(float mouseX, float mouseY, bool isCtrlPressed, bool isAltPressed)
    {
        // Check text annotations first.
        // Use Hand on hover (and during drag) to telegraph that the element is grabbable —
        // macOS rendered SizeAll as a plus sign, which read more like "add" than "grab".
        var (textId, _) = _hitTestService.GetTextAnnotationUnderMouse(mouseX, mouseY);
        HoveredTextId = textId;
        if (textId.HasValue)
        {
            HoveredArrowId = null;
            _avaPlot.Cursor = PlotCursors.Hand;
            return;
        }

        // Check arrow annotations
        var (arrowId, _, nearTip) = _hitTestService.GetArrowAnnotationUnderMouse(mouseX, mouseY);
        HoveredArrowId = arrowId;
        if (arrowId.HasValue)
        {
            _avaPlot.Cursor = PlotCursors.Hand;
            return;
        }

        // Check event lines
        var eventLineIdHover = _hitTestService.GetEventLineUnderMouse(mouseX, mouseY);
        if (eventLineIdHover.HasValue)
        {
            _avaPlot.Cursor = PlotCursors.SizeWestEast;
            return;
        }

        // Check callouts
        var calloutUnderMouse = _hitTestService.GetCalloutUnderMouse(mouseX, mouseY);
        if (calloutUnderMouse != null)
        {
            _avaPlot.Cursor = PlotCursors.Hand;
            _viewModel.PlotModel?.MoveToFront(calloutUnderMouse);
            return;
        }

        if (!isCtrlPressed && !isAltPressed && _panStart == null && _leftClickPanStart == null)
        {
            _avaPlot.Cursor = PlotCursors.Arrow;
        }
    }

    #region Zoom Box Helper Methods

    private void DrawZoomBox(Point start, Point current)
    {
        if (_zoomBoxVisual != null)
        {
            _overlayCanvas.Children.Remove(_zoomBoxVisual);
        }

        _zoomBoxVisual = new AvaloniaRectangle
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255)),
            Width = Math.Abs(current.X - start.X),
            Height = Math.Abs(current.Y - start.Y)
        };

        Canvas.SetLeft(_zoomBoxVisual, Math.Min(start.X, current.X));
        Canvas.SetTop(_zoomBoxVisual, Math.Min(start.Y, current.Y));

        _overlayCanvas.Children.Add(_zoomBoxVisual);
    }

    private void ApplyZoomBox(Point start, Point end)
    {
        if (_viewModel.PlotModel == null)
            return;

        ScottPlot.Pixel startPixel = new(
            (float)(start.X * _avaPlot.DisplayScale),
            (float)(start.Y * _avaPlot.DisplayScale));
        ScottPlot.Pixel endPixel = new(
            (float)(end.X * _avaPlot.DisplayScale),
            (float)(end.Y * _avaPlot.DisplayScale));

        var startCoord = _avaPlot.Plot.GetCoordinates(startPixel);
        var endCoord = _avaPlot.Plot.GetCoordinates(endPixel);

        double xMin = Math.Min(startCoord.X, endCoord.X);
        double xMax = Math.Max(startCoord.X, endCoord.X);
        double yMin = Math.Min(startCoord.Y, endCoord.Y);
        double yMax = Math.Max(startCoord.Y, endCoord.Y);

        _viewModel.PlotModel.Axes.Bottom.Range.Set(xMin, xMax);
        _viewModel.PlotModel.Axes.Left.Range.Set(yMin, yMax);
        _avaPlot.Refresh();
    }

    private void ClearZoomBox()
    {
        if (_zoomBoxVisual != null)
        {
            _overlayCanvas.Children.Remove(_zoomBoxVisual);
            _zoomBoxVisual = null;
        }
    }

    #endregion

    #region Pan Helper Methods

    private void PerformPan(Point previousPosition, Point currentPosition)
    {
        if (_viewModel.PlotModel == null)
            return;

        ScottPlot.Pixel startPixel = new(
            (float)(previousPosition.X * _avaPlot.DisplayScale),
            (float)(previousPosition.Y * _avaPlot.DisplayScale));
        ScottPlot.Pixel endPixel = new(
            (float)(currentPosition.X * _avaPlot.DisplayScale),
            (float)(currentPosition.Y * _avaPlot.DisplayScale));

        var startCoord = _avaPlot.Plot.GetCoordinates(startPixel);
        var endCoord = _avaPlot.Plot.GetCoordinates(endPixel);

        double coordDx = endCoord.X - startCoord.X;
        double coordDy = endCoord.Y - startCoord.Y;

        var xRange = _viewModel.PlotModel.Axes.Bottom.Range;
        var yRange = _viewModel.PlotModel.Axes.Left.Range;

        _viewModel.PlotModel.Axes.Bottom.Range.Set(
            xRange.Min - coordDx, xRange.Max - coordDx);
        _viewModel.PlotModel.Axes.Left.Range.Set(
            yRange.Min - coordDy, yRange.Max - coordDy);

        _avaPlot.Refresh();
    }

    #endregion
}
