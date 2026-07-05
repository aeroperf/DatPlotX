using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using DatPlotX.Helpers;
using Microsoft.Extensions.Logging;

namespace DatPlotX.Services;

/// <summary>
/// Writes local-only crash dumps when an unhandled exception terminates (or nearly terminates) the
/// app. <b>Nothing is ever uploaded</b> — the dump is a file on the user's disk that they may
/// choose to attach to a GitHub issue. The opt-in <see cref="Models.ApplicationSettings"/>
/// <c>CrashReportingEnabled</c> flag only controls whether we surface a "we found a crash report"
/// prompt on the next launch; the dump itself is always written so the user has it for their own
/// debugging.
///
/// <para>Dumps contain the stack trace, exception type/message, app version, and OS only — never
/// imported rows, never the source file's contents. <b>Scrubbing is path-only:</b> <see cref="Scrub"/>
/// reduces any absolute filesystem path to its basename, so directory layouts and user/customer
/// names embedded in paths never survive. It does <b>not</b> parse the exception <i>message</i> — a
/// parse/format exception can still quote a row value or column name in its text. That text stays
/// strictly local (never uploaded), so this is acceptable for a local debugging artifact; do not
/// upgrade this claim to "no column names / no cell values" without also sanitizing message bodies.</para>
/// </summary>
public sealed partial class CrashReporter : ICrashReporter
{
    private const string DumpPrefix = "crash-";
    private const string DumpExtension = ".txt";
    private const int RetentionDays = 30;

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception ({Context}); crash dump written")]
    private static partial void LogCrashDump(ILogger logger, Exception exception, string context);

    private readonly ILogger<CrashReporter>? _logger;

    public CrashReporter(ILogger<CrashReporter>? logger = null)
    {
        _logger = logger;
        TryPruneOldDumps();
    }

    public string CrashDirectory => AppPaths.CrashDirectory;

    /// <inheritdoc/>
    public string? WriteCrashDump(Exception exception, string context, bool isTerminating)
    {
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            var path = Path.Combine(AppPaths.CrashDirectory, $"{DumpPrefix}{stamp}{DumpExtension}");

            var sb = new StringBuilder(1024);
            sb.AppendLine("DatPlotX crash report (local only — not sent anywhere)");
            sb.AppendLine(new string('=', 60));
            sb.Append("Timestamp:   ").AppendLine(DateTime.Now.ToString("u", CultureInfo.InvariantCulture));
            sb.Append("App version: ").AppendLine(AppVersion());
            sb.Append("OS:          ").AppendLine(RuntimeInformation.OSDescription);
            sb.Append("Arch:        ").AppendLine(RuntimeInformation.OSArchitecture.ToString());
            sb.Append("Terminating: ").AppendLine(isTerminating ? "yes" : "no");
            sb.Append("Context:     ").AppendLine(Scrub(context));
            sb.AppendLine();
            sb.AppendLine("Exception:");
            sb.AppendLine(Scrub(exception.ToString()));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            if (_logger is not null)
                LogCrashDump(_logger, exception, context);
            return path;
        }
        catch
        {
            // A crash reporter that throws is worse than no crash reporter.
            return null;
        }
    }

    /// <inheritdoc/>
    public string? FindLatestDump()
    {
        try
        {
            var dir = new DirectoryInfo(AppPaths.CrashDirectory);
            if (!dir.Exists)
                return null;

            FileInfo? latest = null;
            foreach (var file in dir.EnumerateFiles($"{DumpPrefix}*{DumpExtension}"))
            {
                if (latest is null || file.LastWriteTimeUtc > latest.LastWriteTimeUtc)
                    latest = file;
            }

            return latest?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private static string AppVersion()
        => typeof(CrashReporter).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>
    /// Best-effort removal of absolute filesystem paths so a dump never leaks a directory layout or
    /// user/customer name embedded in a path. Keeps the leaf filename for context.
    /// </summary>
    private static string Scrub(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Collapse anything that looks like an absolute path to "<path>/leaf".
        var pattern = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? @"[A-Za-z]:\\[^\s""']+"
            : @"/(?:[^/\s""']+/)+[^/\s""']*";

        return System.Text.RegularExpressions.Regex.Replace(text, pattern, m =>
        {
            var leaf = Path.GetFileName(m.Value);
            return string.IsNullOrEmpty(leaf) ? "<path>" : $"<path>/{leaf}";
        });
    }

    private void TryPruneOldDumps()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(AppPaths.CrashDirectory, $"{DumpPrefix}*{DumpExtension}"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // Best-effort.
        }
    }
}
