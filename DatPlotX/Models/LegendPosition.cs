namespace DatPlotX.Models;

/// <summary>
/// Placement of the per-pane legend in Stacked mode. Inside-* positions place the legend
/// inside the data area. <see cref="Hidden"/> removes the legend.
/// </summary>
public enum LegendPosition
{
    Hidden,
    InsideUpperLeft,
    InsideUpperRight,
    InsideLowerLeft,
    InsideLowerRight,
}
