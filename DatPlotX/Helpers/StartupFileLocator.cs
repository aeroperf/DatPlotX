namespace DatPlotX.Helpers;

/// <summary>
/// Resolves a DatPlotX project file passed to the app at launch — via the command line
/// (Windows file-association double-click forwards the path as an argument) or any other
/// caller that hands us raw arguments. Pure string logic so it can be unit-tested without
/// touching the file system; the caller is responsible for the <c>File.Exists</c> check.
/// </summary>
public static class StartupFileLocator
{
    /// <summary>The project-file extension DatPlotX opens on double-click.</summary>
    public const string ProjectExtension = ".dpx";

    /// <summary>
    /// Returns the first argument that looks like a <c>.dpx</c> project path (case-insensitive),
    /// or <c>null</c> if none of the arguments do. Existence is intentionally not checked here.
    /// </summary>
    public static string? FindProjectArgument(IReadOnlyList<string>? args)
    {
        if (args is null)
            return null;

        foreach (var arg in args)
        {
            if (!string.IsNullOrWhiteSpace(arg) &&
                arg.EndsWith(ProjectExtension, StringComparison.OrdinalIgnoreCase))
            {
                return arg;
            }
        }

        return null;
    }
}
