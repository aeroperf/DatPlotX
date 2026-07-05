using System.Collections.Immutable;

namespace DatPlotX.Helpers;

/// <summary>
/// Default cycling color palette used to seed new curves in both plot modes.
/// Both Stacked Panes (ScottPlot) and Compact Plot Surface (OxyPlot) draw from
/// this list when a curve is first created; users can recolor any curve afterwards.
/// </summary>
public static class DefaultCurvePalette
{
    /// <summary>
    /// 16 distinct hues that cycle modulo length. Picked for legibility on white
    /// and dark plot backgrounds. Immutable so callers cannot mutate the process-wide palette.
    /// </summary>
    public static ImmutableArray<string> Colors { get; } = ImmutableArray.Create(
        "#0000FF", "#FF00FF", "#32CD32", "#FF0000", "#000000",
        "#FFA500", "#1E90FF", "#800080", "#8B4513", "#FF69B4",
        "#000080", "#008080", "#808000", "#FF1493", "#4B0082", "#DC143C"
    );
}
