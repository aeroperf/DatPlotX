using Avalonia.Input;

namespace DatPlotX.Views.Controls;

/// <summary>
/// Shared, reusable <see cref="Cursor"/> instances for plot surfaces. Each Avalonia
/// <see cref="Cursor"/> wraps a disposable native cursor handle, so allocating a fresh one at
/// pointer-move frequency churned native handles ~120 Hz and never disposed the replaced one
/// (review M1). These are process-lifetime singletons — assign them instead of newing cursors,
/// and skip the assignment when the target already holds the desired cursor.
/// </summary>
internal static class PlotCursors
{
    public static readonly Cursor Arrow = new(StandardCursorType.Arrow);
    public static readonly Cursor Hand = new(StandardCursorType.Hand);
    public static readonly Cursor Cross = new(StandardCursorType.Cross);
    public static readonly Cursor SizeWestEast = new(StandardCursorType.SizeWestEast);
    public static readonly Cursor SizeAll = new(StandardCursorType.SizeAll);
}
