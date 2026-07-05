namespace DatPlotX.Models;

/// <summary>
/// Application settings for resource limits and preferences
/// Configurable by users to balance performance and safety
/// </summary>
public class ApplicationSettings
{
    public const long DefaultMaxFileSizeBytes = 1024L * 1024 * 1024;
    public const int DefaultMaxRowCount = 10_000_000;
    public const int DefaultMaxColumnCount = 5000;
    public const long DefaultLargeFileWarningThresholdBytes = 100L * 1024 * 1024;
    public const int DefaultLargeRowCountWarningThreshold = 1_000_000;

    public const int DefaultGroupedPlotMaxLines = 48;
    public const int DefaultGroupedPlotMaxDistinctValues = 5000;
    public const int DefaultGroupedPlotMaxInputs = 8;

    public long MaxFileSizeBytes { get; set; } = DefaultMaxFileSizeBytes;

    public int MaxRowCount { get; set; } = DefaultMaxRowCount;

    public int MaxColumnCount { get; set; } = DefaultMaxColumnCount;

    public long LargeFileWarningThresholdBytes { get; set; } = DefaultLargeFileWarningThresholdBytes;

    public bool ShowLargeFileWarnings { get; set; } = true;

    public bool ShowLargeRowCountWarnings { get; set; } = true;

    public int LargeRowCountWarningThreshold { get; set; } = DefaultLargeRowCountWarningThreshold;

    public bool HoverTooltipsEnabledByDefault { get; set; } = true;

    /// <summary>
    /// Opt-in flag (OFF by default) controlling whether DatPlotX prompts the user about a crash
    /// dump it found from a previous session. Crash dumps are always written locally and are
    /// <b>never</b> uploaded; this flag only governs the next-launch "we found a crash report"
    /// prompt. Privacy-by-default: silent unless the user turns it on.
    /// </summary>
    public bool CrashReportingEnabled { get; set; }

    /// <summary>Maximum number of lines drawn on the Grouped Parameter Plot. Beyond this the
    /// indexer truncates and the view surfaces a "narrow your selection" warning.</summary>
    public int GroupedPlotMaxLines { get; set; } = DefaultGroupedPlotMaxLines;

    /// <summary>Cap on distinct values per input column. A column with more distinct values than
    /// this is rejected as an input candidate (likely a continuous-value column, not a discrete axis).</summary>
    public int GroupedPlotMaxDistinctValues { get; set; } = DefaultGroupedPlotMaxDistinctValues;

    /// <summary>Maximum inputs the user can configure on a Grouped Plot.</summary>
    public int GroupedPlotMaxInputs { get; set; } = DefaultGroupedPlotMaxInputs;

    public void ResetToDefaults()
    {
        MaxFileSizeBytes = DefaultMaxFileSizeBytes;
        MaxRowCount = DefaultMaxRowCount;
        MaxColumnCount = DefaultMaxColumnCount;
        LargeFileWarningThresholdBytes = DefaultLargeFileWarningThresholdBytes;
        ShowLargeFileWarnings = true;
        ShowLargeRowCountWarnings = true;
        LargeRowCountWarningThreshold = DefaultLargeRowCountWarningThreshold;
        HoverTooltipsEnabledByDefault = true;
        CrashReportingEnabled = false;
        GroupedPlotMaxLines = DefaultGroupedPlotMaxLines;
        GroupedPlotMaxDistinctValues = DefaultGroupedPlotMaxDistinctValues;
        GroupedPlotMaxInputs = DefaultGroupedPlotMaxInputs;
    }

    public void Validate()
    {
        if (MaxFileSizeBytes < 1024 * 1024)
            MaxFileSizeBytes = 1024 * 1024;

        if (MaxRowCount < 1000)
            MaxRowCount = 1000;

        if (MaxColumnCount < 10)
            MaxColumnCount = 10;

        if (LargeFileWarningThresholdBytes > MaxFileSizeBytes)
            LargeFileWarningThresholdBytes = MaxFileSizeBytes / 10;

        if (LargeRowCountWarningThreshold > MaxRowCount)
            LargeRowCountWarningThreshold = MaxRowCount / 10;

        if (GroupedPlotMaxLines < 1)
            GroupedPlotMaxLines = DefaultGroupedPlotMaxLines;
        if (GroupedPlotMaxDistinctValues < 2)
            GroupedPlotMaxDistinctValues = DefaultGroupedPlotMaxDistinctValues;
        if (GroupedPlotMaxInputs < 1)
            GroupedPlotMaxInputs = DefaultGroupedPlotMaxInputs;
    }

    public string GetLimitsDescription()
    {
        return $"Max File Size: {MaxFileSizeBytes / 1024 / 1024} MB\n" +
               $"Max Rows: {MaxRowCount:N0}\n" +
               $"Max Columns: {MaxColumnCount:N0}\n" +
               $"Large File Warning: {LargeFileWarningThresholdBytes / 1024 / 1024} MB";
    }
}
