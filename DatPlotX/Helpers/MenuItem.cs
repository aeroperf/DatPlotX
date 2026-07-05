using System.Windows.Input;

namespace DatPlotX.Helpers;

/// <summary>
/// Helper class for creating menu items with commands
/// </summary>
public class MenuItem
{
    public string Header { get; set; } = string.Empty;
    public ICommand? Command { get; set; }
    public object? CommandParameter { get; set; }
    public string? InputGestureText { get; set; }
    public List<MenuItem>? SubItems { get; set; }
}
