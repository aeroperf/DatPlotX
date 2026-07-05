namespace DatPlotX.Helpers;

/// <summary>
/// Validates file paths to prevent path traversal and injection attacks
/// </summary>
public static class FilePathValidator
{
    /// <summary>
    /// Validates and normalizes a file path to prevent path traversal attacks (CWE-22)
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <param name="mustExist">If true, throws if file doesn't exist</param>
    /// <returns>Normalized absolute file path</returns>
    /// <exception cref="ArgumentException">If path is empty or invalid</exception>
    /// <exception cref="System.Security.SecurityException">If path traversal is detected</exception>
    /// <exception cref="FileNotFoundException">If mustExist is true and file doesn't exist</exception>
    public static string ValidateAndNormalizePath(string filePath, bool mustExist = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty", nameof(filePath));

        // Get the full absolute path - this also validates the path format
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid file path format", nameof(filePath), ex);
        }

        // SECURITY: Reject traversal sequences used as path segments (CWE-22).
        // A literal ".." only matters when it separates path segments — we accept it
        // within filenames like "foo..bar.csv".
        if (ContainsTraversalSegment(filePath))
        {
            throw new System.Security.SecurityException("Path traversal detected: Relative path sequences (..) are not allowed");
        }

        // Home-directory expansion: only reject when `~` is the path root.
        if (filePath.StartsWith('~'))
        {
            throw new System.Security.SecurityException("Path traversal detected: Home directory expansion (~) is not allowed");
        }

        // Verify file existence if required
        if (mustExist && !File.Exists(normalizedPath))
        {
            throw new FileNotFoundException("File not found", normalizedPath);
        }

        return normalizedPath;
    }

    /// <summary>
    /// Validates a file path for saving operations
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <returns>Normalized absolute file path</returns>
    public static string ValidatePathForSave(string filePath)
    {
        var normalizedPath = ValidateAndNormalizePath(filePath, mustExist: false);

        // Ensure parent directory exists or can be created
        var directory = Path.GetDirectoryName(normalizedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Cannot create directory for file", ex);
            }
        }

        return normalizedPath;
    }

    /// <summary>
    /// Validates a file path for loading operations
    /// </summary>
    /// <param name="filePath">The file path to validate</param>
    /// <returns>Normalized absolute file path</returns>
    public static string ValidatePathForLoad(string filePath)
    {
        return ValidateAndNormalizePath(filePath, mustExist: true);
    }

    private static bool ContainsTraversalSegment(string path)
    {
        var span = path.AsSpan();
        for (int i = 0; i < span.Length - 1; i++)
        {
            if (span[i] != '.' || span[i + 1] != '.') continue;
            bool leftIsBoundary = i == 0 || span[i - 1] == '/' || span[i - 1] == '\\';
            bool rightIsBoundary = i + 2 == span.Length || span[i + 2] == '/' || span[i + 2] == '\\';
            if (leftIsBoundary && rightIsBoundary) return true;
        }
        return false;
    }
}
