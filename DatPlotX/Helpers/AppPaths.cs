using System.Runtime.InteropServices;

namespace DatPlotX.Helpers;

/// <summary>
/// Resolves the per-OS directories DatPlotX writes to (logs, crash dumps). All paths live under a
/// single application data root so the user can find — and delete — everything in one place.
///
/// <para>Roots by platform:</para>
/// <list type="bullet">
///   <item><description>Windows: <c>%LOCALAPPDATA%\DatPlotX</c></description></item>
///   <item><description>macOS: <c>~/Library/Application Support/DatPlotX</c></description></item>
///   <item><description>Linux: <c>$XDG_DATA_HOME/DatPlotX</c> or <c>~/.local/share/DatPlotX</c></description></item>
/// </list>
/// </summary>
public static class AppPaths
{
    private const string AppFolderName = "DatPlotX";

    /// <summary>Root application-data directory for this user. Created on demand.</summary>
    public static string DataRoot
    {
        get
        {
            string baseDir;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                baseDir = Path.Combine(home, "Library", "Application Support");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                baseDir = !string.IsNullOrWhiteSpace(xdg)
                    ? xdg
                    : Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".local", "share");
            }
            else
            {
                // Windows (and any fallback): LocalApplicationData = %LOCALAPPDATA%.
                baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            return Path.Combine(baseDir, AppFolderName);
        }
    }

    /// <summary>Directory holding user config (settings, recent files). Created on demand.</summary>
    public static string ConfigDirectory => EnsureDir(DataRoot);

    /// <summary>Directory holding rolling daily log files. Created on demand.</summary>
    public static string LogDirectory => EnsureDir(Path.Combine(DataRoot, "logs"));

    /// <summary>Directory holding local-only crash dumps. Created on demand.</summary>
    public static string CrashDirectory => EnsureDir(Path.Combine(DataRoot, "crashes"));

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
