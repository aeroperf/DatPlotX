namespace DatPlotX.Models;

/// <summary>
/// One curve on the Compact Plot Surface. Each curve owns its own banded Y axis on the
/// shared OxyPlot plot area; band placement is computed from the order in
/// <see cref="ProjectSettingsModel.CompactCurves"/>.
/// </summary>
public class CompactCurveModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>User-facing label (legend + axis title). Defaults to <see cref="SourceColumn"/>.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>CSV column name supplying Y values.</summary>
    public string SourceColumn { get; set; } = string.Empty;

    /// <summary>Optional unit shown after the display name (e.g. "deg", "kt").</summary>
    public string? Unit { get; set; }

    /// <summary>Left or right edge of the plot area for this curve's Y axis.</summary>
    public AxisSide AxisSide { get; set; } = AxisSide.Left;

    /// <summary>Hex color (#RRGGBB or #AARRGGBB) for line, axis title, and tick labels.
    /// Also drives marker color when <see cref="MarkerColor"/> is null.</summary>
    public string Color { get; set; } = "#0000FF";

    public LineStyle LineStyle { get; set; } = LineStyle.Solid;

    public double LineWidth { get; set; } = 1.5;

    public MarkerStyle MarkerStyle { get; set; } = MarkerStyle.None;

    public double MarkerSize { get; set; } = 4.0;

    /// <summary>Optional marker color override. When null, markers use <see cref="Color"/>.</summary>
    public string? MarkerColor { get; set; }

    /// <summary>True if the column carries only 0/1 or true/false; band collapses to ~1 grid row.</summary>
    public bool IsBoolean { get; set; }

    /// <summary>Y axis minimum. Null = auto-fit on first render.</summary>
    public double? YMin { get; set; }

    /// <summary>Y axis maximum. Null = auto-fit on first render.</summary>
    public double? YMax { get; set; }

    /// <summary>True = data may bleed into adjacent bands (NTSB Roll-Angle behavior). Default true.</summary>
    public bool AllowOverflow { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    /// <summary>Y-axis title + tick label font size for this curve's banded axis.</summary>
    public double YAxisLabelFontSize { get; set; } = 14;

    /// <summary>True = Y-axis title and tick labels rendered bold.</summary>
    public bool YAxisLabelBold { get; set; }

    /// <summary>Decimal places shown on Y-axis tick labels. 0 = integer-style, e.g. <c>0</c>.</summary>
    public int YAxisDecimalPlaces { get; set; }
}

/// <summary>Plot edge a Compact-mode Y axis lives on.</summary>
public enum AxisSide
{
    Left = 0,
    Right = 1,
}
