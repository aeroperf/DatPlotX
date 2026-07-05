namespace DatPlotX.Services;

/// <summary>
/// Writes local-only crash dumps. Implementations must never upload anything; the dump is a file on
/// the user's disk that they may optionally attach to a bug report.
/// </summary>
public interface ICrashReporter
{
    /// <summary>Directory where crash dumps are written.</summary>
    string CrashDirectory { get; }

    /// <summary>
    /// Write a scrubbed crash dump for the given exception. Returns the dump file path, or
    /// <c>null</c> if the dump could not be written. Never throws.
    /// </summary>
    string? WriteCrashDump(Exception exception, string context, bool isTerminating);

    /// <summary>
    /// Return the path of the most recent crash dump, or <c>null</c> if none exists. Used on the
    /// next launch to optionally prompt the user (when crash reporting is enabled).
    /// </summary>
    string? FindLatestDump();
}
