namespace DatPlotX.Models;

/// <summary>
/// Represents the user-selected options for importing a data file.
/// </summary>
public class ImportOptionsModel
{
    /// <summary>
    /// The character used to separate columns in the data file.
    /// </summary>
    public string Delimiter { get; set; } = ",";

    /// <summary>
    /// The culture name that defines the decimal format (e.g., "en-US" for '.', "de-DE" for ',').
    /// </summary>
    public string CultureName { get; set; } = "en-US";

    /// <summary>
    /// Indicates whether the file is in the X-Plane 12 data format.
    /// </summary>
    public bool IsXPlaneFormat { get; set; }

    /// <summary>
    /// 1-based line number containing column (parameter) names. 0 means no header
    /// (auto-generate Column1, Column2, ...).
    /// </summary>
    public int HeaderLine { get; set; } = 1;

    /// <summary>
    /// 1-based line number containing units. 0 means no unit row. When set, each
    /// unit cell is concatenated to its column name as "Header (Unit)".
    /// </summary>
    public int UnitLine { get; set; }

    /// <summary>
    /// 1-based line number where plot data starts. Lines above this (other than the
    /// header / unit lines) are skipped verbatim.
    /// </summary>
    public int DataStartLine { get; set; } = 2;
}
