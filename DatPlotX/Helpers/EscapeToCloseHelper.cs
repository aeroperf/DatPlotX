using Avalonia.Controls;
using Avalonia.Input;

namespace DatPlotX.Helpers;

internal static class EscapeToCloseHelper
{
    public static void EnableEscapeToClose(this Window window, object? cancelResult = null)
    {
        EventHandler<KeyEventArgs>? handler = null;
        handler = (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                if (cancelResult is null)
                    window.Close();
                else
                    window.Close(cancelResult);
                e.Handled = true;
            }
        };
        window.KeyDown += handler;
        // Detach on close so re-used windows (or future long-lived hosts) don't accumulate
        // closures that capture the original cancelResult / window pair.
        window.Closed += (_, _) =>
        {
            if (handler is not null)
                window.KeyDown -= handler;
        };
    }
}
