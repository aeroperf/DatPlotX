using System.Text.RegularExpressions;

namespace DatPlotX.Helpers;

/// <summary>
/// Validates user input to prevent injection and formatting issues (CWE-20)
/// </summary>
public static partial class InputValidator
{
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();
    private static readonly char[] InvalidPathChars = Path.GetInvalidPathChars();

    [GeneratedRegex(@"[^\w\s\-\.]", RegexOptions.CultureInvariant)]
    private static partial Regex ColumnNameInvalidCharsRegex();

    /// <summary>
    /// Validates a file name (not a full path) to ensure it doesn't contain invalid characters
    /// </summary>
    /// <param name="fileName">The file name to validate</param>
    /// <param name="maxLength">Maximum allowed length (default: 255)</param>
    /// <returns>Sanitized file name</returns>
    /// <exception cref="ArgumentException">If validation fails</exception>
    public static string ValidateFileName(string fileName, int maxLength = 255)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot be empty", nameof(fileName));

        // SECURITY: Check length to prevent buffer issues (CWE-20)
        if (fileName.Length > maxLength)
            throw new ArgumentException($"File name exceeds maximum length of {maxLength} characters", nameof(fileName));

        // SECURITY: Check for invalid file name characters (CWE-20)
        if (fileName.IndexOfAny(InvalidFileNameChars) >= 0)
            throw new ArgumentException("File name contains invalid characters", nameof(fileName));

        // Remove leading/trailing dots and spaces (Windows compatibility)
        fileName = fileName.Trim('.', ' ');

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name cannot consist only of dots and spaces", nameof(fileName));

        return fileName;
    }

    /// <summary>
    /// Validates a label or text string for display purposes
    /// </summary>
    /// <param name="label">The label to validate</param>
    /// <param name="maxLength">Maximum allowed length (default: 100)</param>
    /// <returns>Sanitized label</returns>
    /// <exception cref="ArgumentException">If validation fails</exception>
    public static string ValidateLabel(string label, int maxLength = 100)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot be empty", nameof(label));

        // SECURITY: Check length (CWE-20)
        if (label.Length > maxLength)
            throw new ArgumentException($"Label exceeds maximum length of {maxLength} characters", nameof(label));

        // SECURITY: Sanitize for display - remove control characters except newlines/tabs (CWE-20)
        label = new string(label.Where(c => !char.IsControl(c) || c == '\n' || c == '\t').ToArray());

        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label cannot consist only of control characters", nameof(label));

        return label.Trim();
    }

    /// <summary>
    /// Validates a column name or parameter name
    /// </summary>
    /// <param name="name">The name to validate</param>
    /// <param name="maxLength">Maximum allowed length (default: 200)</param>
    /// <returns>Sanitized name</returns>
    public static string ValidateColumnName(string name, int maxLength = 200)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be empty", nameof(name));

        // SECURITY: Check length (CWE-20)
        if (name.Length > maxLength)
            throw new ArgumentException($"Column name exceeds maximum length of {maxLength} characters", nameof(name));

        // Remove control characters
        name = new string(name.Where(c => !char.IsControl(c)).ToArray());

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot consist only of control characters", nameof(name));

        return name.Trim();
    }

    /// <summary>
    /// Validates a positive integer value within a range
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="min">Minimum allowed value (inclusive)</param>
    /// <param name="max">Maximum allowed value (inclusive)</param>
    /// <param name="paramName">Parameter name for error messages</param>
    /// <returns>The validated value</returns>
    public static int ValidatePositiveInteger(int value, int min, int max, string paramName)
    {
        if (value < min)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be at least {min}");

        if (value > max)
            throw new ArgumentOutOfRangeException(paramName, $"Value must not exceed {max}");

        return value;
    }

    /// <summary>
    /// Validates a positive double value within a range
    /// </summary>
    /// <param name="value">The value to validate</param>
    /// <param name="min">Minimum allowed value (inclusive)</param>
    /// <param name="max">Maximum allowed value (inclusive)</param>
    /// <param name="paramName">Parameter name for error messages</param>
    /// <returns>The validated value</returns>
    public static double ValidateDouble(double value, double min, double max, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException("Value must be a valid number", paramName);

        if (value < min)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be at least {min}");

        if (value > max)
            throw new ArgumentOutOfRangeException(paramName, $"Value must not exceed {max}");

        return value;
    }

    /// <summary>
    /// Sanitizes a column name from external sources (e.g., CSV headers) to prevent injection attacks
    /// SECURITY: Removes potentially dangerous characters and ensures safe usage in bindings and data contexts
    /// </summary>
    /// <param name="columnName">The column name to sanitize</param>
    /// <param name="maxLength">Maximum allowed length (default: 128)</param>
    /// <returns>A sanitized column name safe for use in data bindings</returns>
    public static string SanitizeColumnName(string columnName, int maxLength = 128)
    {
        // SECURITY: Return safe default for null/empty input
        if (string.IsNullOrWhiteSpace(columnName))
            return "Column";

        // SECURITY: Remove characters that could cause issues in WPF bindings or data contexts
        // Only allow word characters (letters, digits, underscore), spaces, hyphens, and dots
        var sanitized = ColumnNameInvalidCharsRegex().Replace(columnName, "");

        // SECURITY: Limit length to prevent buffer issues (CWE-20)
        if (sanitized.Length > maxLength)
            sanitized = sanitized.Substring(0, maxLength);

        // SECURITY: Ensure column name doesn't start with a digit (invalid for many contexts)
        if (!string.IsNullOrEmpty(sanitized) && char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;

        // Trim whitespace
        sanitized = sanitized.Trim();

        // Return safe default if sanitization resulted in empty string
        return string.IsNullOrWhiteSpace(sanitized) ? "Column" : sanitized;
    }

    /// <summary>
    /// Disambiguate already-sanitized names so they can be used as DataTable column names.
    /// Preserves the first occurrence; subsequent duplicates are suffixed with _2, _3, etc.
    /// Suffix collisions against pre-existing names are avoided by probing forward.
    /// </summary>
    public static string[] MakeUniqueColumnNames(string[] sanitized)
    {
        ArgumentNullException.ThrowIfNull(sanitized);

        var taken = new HashSet<string>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new string[sanitized.Length];

        for (int i = 0; i < sanitized.Length; i++)
        {
            var name = sanitized[i];
            if (taken.Add(name))
            {
                counts[name] = 1;
                result[i] = name;
                continue;
            }

            int next = counts[name] + 1;
            string candidate;
            do
            {
                candidate = $"{name}_{next}";
                next++;
            } while (!taken.Add(candidate));

            counts[name] = next - 1;
            counts[candidate] = 1;
            result[i] = candidate;
        }

        return result;
    }
}
