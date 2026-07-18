using Microsoft.Extensions.Logging;

namespace DatPlotX.Helpers;

/// <summary>
/// Sanitizes error messages to prevent information disclosure (CWE-209)
/// </summary>
public static class SafeErrorHandler
{
    /// <summary>
    /// Optional sink set by the app at startup so <see cref="LogError"/> reaches the rolling file
    /// log (Help → Open Log Folder), not just the debugger. Left null in tests/designer, where
    /// the Debug fallback is fine. In a Release build Debug.WriteLine is compiled out, so without
    /// this the errors left no on-disk trace at all.
    /// </summary>
    public static ILogger? Logger { get; set; }

    /// <summary>
    /// Gets a user-friendly error message that doesn't expose internal system details
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="operation">The operation that was being performed</param>
    /// <returns>A sanitized error message safe to display to users</returns>
    public static string GetUserFriendlyMessage(Exception ex, string operation)
    {
        // SECURITY: Return sanitized messages to prevent information disclosure (CWE-209)
        // Internal details are not exposed to users
        return ex switch
        {
            FileNotFoundException => "The requested file could not be found.",
            DirectoryNotFoundException => "The requested directory could not be found.",
            UnauthorizedAccessException => "You do not have permission to access this file or directory.",
            IOException => $"An error occurred while {operation}. Please verify the file is not in use and try again.",
            InvalidDataException => "The file format is invalid or corrupted.",
            System.Security.SecurityException => "This operation is not allowed for security reasons.",
            ArgumentException => "Invalid input provided. Please check your input and try again.",
            NotSupportedException => $"The {operation} operation is not supported for this file type.",
            OutOfMemoryException => "The file is too large or the system is out of memory.",
            _ => $"An unexpected error occurred while {operation}. Please try again or contact support if the problem persists."
        };
    }

    /// <summary>
    /// Gets a user-friendly message with additional context
    /// </summary>
    /// <param name="ex">The exception that occurred</param>
    /// <param name="operation">The operation that was being performed</param>
    /// <param name="context">Additional context about what was being done</param>
    /// <returns>A sanitized error message with context</returns>
    public static string GetUserFriendlyMessageWithContext(Exception ex, string operation, string context)
    {
        var baseMessage = GetUserFriendlyMessage(ex, operation);
        return $"{baseMessage}\n\nContext: {context}";
    }

    /// <summary>
    /// Logs detailed error information for debugging (would integrate with logging framework)
    /// </summary>
    /// <param name="ex">The exception to log</param>
    /// <param name="operation">The operation that failed</param>
    /// <param name="additionalInfo">Additional information for debugging</param>
    public static void LogError(Exception ex, string operation, string? additionalInfo = null)
    {
        // SECURITY: Detailed errors are logged server-side only, never shown to users
        // In a production app, this would integrate with a logging framework
        var logMessage = $"[ERROR] Operation: {operation}\n" +
                        $"Exception Type: {ex.GetType().Name}\n" +
                        $"Message: {ex.Message}\n" +
                        $"Stack Trace: {ex.StackTrace}";

        if (!string.IsNullOrEmpty(additionalInfo))
        {
            logMessage += $"\nAdditional Info: {additionalInfo}";
        }

        // Prefer the app's rolling file logger so errors are recoverable from the log folder the
        // user is directed to for bug reports. Fall back to Debug when no logger is wired (tests,
        // designer). The security-baseline scrubbing rule still holds: callers pass basename-only
        // additionalInfo, never row data / column names / file contents.
        if (Logger is { } logger)
            // CA1848 (LoggerMessage delegates) is not worth it on this rare error path.
#pragma warning disable CA1848
            logger.LogError(ex, "Operation: {Operation}. {AdditionalInfo}", operation, additionalInfo ?? string.Empty);
#pragma warning restore CA1848
        else
            System.Diagnostics.Debug.WriteLine(logMessage);
    }
}
