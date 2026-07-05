namespace DatPlotX.Models.Analysis;

/// <summary>
/// Grouping for the "Choose metrics" picker. Categories match the engineering disciplines
/// called out in <see cref="Docs/Curve-Analysis-Tools-Plan.md"/> §2.
/// </summary>
public enum MetricCategory
{
    Basic,
    Distribution,
    Temporal,
    Quality,
}
