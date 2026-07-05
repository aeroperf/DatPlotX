using ScottPlot;
using ScottPlot.Interactivity;
using ScottPlot.Interactivity.UserActionResponses;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Left-drag pan that steps aside while a suppress key (Ctrl) is held, so Ctrl+left-drag is free
/// to draw a zoom rectangle instead. ScottPlot's built-in <see cref="MouseDragPan"/> fires on any
/// left-drag regardless of modifiers and returns a primary response, which would otherwise consume
/// the Ctrl+left gesture before <see cref="MouseDragZoomRectangle"/> could grow the box.
/// </summary>
internal sealed class CtrlSuppressedMouseDragPan : IUserActionResponse
{
    private readonly MouseDragPan _inner;
    private readonly Key _suppressKey;

    public CtrlSuppressedMouseDragPan(MouseButton button, Key suppressKey)
    {
        _inner = new MouseDragPan(button);
        _suppressKey = suppressKey;
    }

    public ResponseInfo Execute(IPlotControl plotControl, IUserAction userAction, KeyboardState keys)
    {
        // While the suppress key is down, do nothing and hold no state — let the rectangle-zoom
        // response own the gesture. ResetState clears any half-started pan from before the key
        // went down so we don't leave the inner pan mid-drag.
        if (keys.IsPressed(_suppressKey))
        {
            _inner.ResetState(plotControl);
            return ResponseInfo.NoActionRequired;
        }
        return _inner.Execute(plotControl, userAction, keys);
    }

    public void ResetState(IPlotControl plotControl) => _inner.ResetState(plotControl);
}
