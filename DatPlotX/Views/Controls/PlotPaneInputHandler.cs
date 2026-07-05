using Avalonia.Input;
using ScottPlot.Avalonia;
using DatPlotX.ViewModels;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Handles keyboard and mouse wheel input for plot pane interactions.
/// Tracks modifier keys (Ctrl, Alt, X, Y) and handles axis-specific zoom operations.
/// </summary>
public class PlotPaneInputHandler
{
    private readonly AvaPlot _avaPlot;
    private readonly PlotPaneViewModel _viewModel;

    // Keyboard state
    private bool _isCtrlPressed;
    private bool _isXKeyPressed;
    private bool _isYKeyPressed;
    private bool _isAltPressed;

    /// <summary>
    /// Raised when CTRL key state changes
    /// </summary>
    public event Action<bool>? CtrlStateChanged;

    /// <summary>
    /// Raised when ALT key state changes
    /// </summary>
    public event Action<bool>? AltStateChanged;

    /// <summary>
    /// Raised when X-axis should be synchronized after zoom
    /// </summary>
    public event Action? XAxisSyncRequested;

    /// <summary>
    /// Raised when mouse position coordinates should be displayed
    /// </summary>
    public event Action<double, double, double, bool>? MousePositionChanged;

    public PlotPaneInputHandler(AvaPlot avaPlot, PlotPaneViewModel viewModel)
    {
        _avaPlot = avaPlot;
        _viewModel = viewModel;
    }

    /// <summary>
    /// Get whether CTRL is currently pressed
    /// </summary>
    public bool IsCtrlPressed => _isCtrlPressed;

    /// <summary>
    /// Get whether ALT is currently pressed
    /// </summary>
    public bool IsAltPressed => _isAltPressed;

    /// <summary>
    /// Get whether X key is currently pressed
    /// </summary>
    public bool IsXKeyPressed => _isXKeyPressed;

    /// <summary>
    /// Get whether Y key is currently pressed
    /// </summary>
    public bool IsYKeyPressed => _isYKeyPressed;

    /// <summary>
    /// Handle KeyDown for CTRL, ALT, X, Y keys and zoom shortcuts
    /// </summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        // Track ALT key
        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
        {
            _isAltPressed = true;
            AltStateChanged?.Invoke(true);
            e.Handled = true;
        }

        // Track CTRL
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _isCtrlPressed = true;
            CtrlStateChanged?.Invoke(true);
            _avaPlot.Cursor = PlotCursors.Cross;
        }

        // Track X key
        if (e.Key == Key.X)
            _isXKeyPressed = true;

        // Track Y key
        if (e.Key == Key.Y)
            _isYKeyPressed = true;

        // CTRL + Plus: Zoom In
        if ((e.Key == Key.Add || e.Key == Key.OemPlus) && _isCtrlPressed)
        {
            e.Handled = true;
            _viewModel.PlotModel?.Axes.ZoomIn(1.2);
            _avaPlot.Refresh();
            XAxisSyncRequested?.Invoke();
        }

        // CTRL + Minus: Zoom Out
        if ((e.Key == Key.Subtract || e.Key == Key.OemMinus) && _isCtrlPressed)
        {
            e.Handled = true;
            _viewModel.PlotModel?.Axes.ZoomOut(1.2);
            _avaPlot.Refresh();
            XAxisSyncRequested?.Invoke();
        }
    }

    /// <summary>
    /// Handle KeyUp for CTRL, ALT, X, Y keys
    /// </summary>
    public void HandleKeyUp(KeyEventArgs e)
    {
        // CTRL release
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl)
        {
            _isCtrlPressed = false;
            CtrlStateChanged?.Invoke(false);
            _avaPlot.Cursor = PlotCursors.Arrow;
            // Clear mouse position when CTRL is released
            MousePositionChanged?.Invoke(0, 0, 0, false);
        }

        // ALT release
        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
        {
            _isAltPressed = false;
            AltStateChanged?.Invoke(false);
        }

        // X key release
        if (e.Key == Key.X)
            _isXKeyPressed = false;

        // Y key release
        if (e.Key == Key.Y)
            _isYKeyPressed = false;
    }

    /// <summary>
    /// Handle mouse wheel events with axis-specific zoom support.
    /// Uses PointerWheelEventArgs.Delta.Y instead of WPF's MouseWheelEventArgs.Delta.
    /// </summary>
    public void HandleMouseWheel(PointerWheelEventArgs e)
    {
        if (_viewModel.PlotModel == null)
            return;

        // Always handle zoom manually to support axis-specific modes
        e.Handled = true;
        double zoomFraction = e.Delta.Y > 0 ? 0.9 : 1.1;

        if (_isXKeyPressed)
        {
            // X-axis only zoom
            var xRange = _viewModel.PlotModel.Axes.Bottom.Range;
            double center = (xRange.Min + xRange.Max) / 2;
            double span = (xRange.Max - xRange.Min) * zoomFraction;
            _viewModel.PlotModel.Axes.Bottom.Range.Set(center - span / 2, center + span / 2);
        }
        else if (_isYKeyPressed)
        {
            // Y-axes only zoom (both Y1 and Y2) - X-axis unchanged
            var yRange = _viewModel.PlotModel.Axes.Left.Range;
            double yCenter = (yRange.Min + yRange.Max) / 2;
            double ySpan = (yRange.Max - yRange.Min) * zoomFraction;
            _viewModel.PlotModel.Axes.Left.Range.Set(yCenter - ySpan / 2, yCenter + ySpan / 2);

            var y2Range = _viewModel.PlotModel.Axes.Right.Range;
            double y2Center = (y2Range.Min + y2Range.Max) / 2;
            double y2Span = (y2Range.Max - y2Range.Min) * zoomFraction;
            _viewModel.PlotModel.Axes.Right.Range.Set(y2Center - y2Span / 2, y2Center + y2Span / 2);
        }
        else
        {
            // Default: Zoom both X and Y axes
            _viewModel.PlotModel.Axes.ZoomIn(e.Delta.Y > 0 ? 1.1 : 0.9);
        }

        _avaPlot.Refresh();

        // Trigger X-axis synchronization (only if X-axis was affected)
        if (!_isYKeyPressed)
        {
            XAxisSyncRequested?.Invoke();
        }
    }
}
