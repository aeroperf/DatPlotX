using DatPlotX.Helpers;
using System.Text;

namespace DatPlotX.Services;

/// <summary>
/// Reads the first N raw lines of a text file for the import-options preview.
/// Delimiter-agnostic — returns lines verbatim so the user can pick header / unit /
/// data-start line numbers against what they see in the file.
/// </summary>
public class FilePreviewService : IFilePreviewService
{
    private const int DefaultMaxLines = 100;

    public async Task<IReadOnlyList<string>> ReadFirstLinesAsync(
        string filePath,
        int maxLines = DefaultMaxLines,
        CancellationToken ct = default)
    {
        if (maxLines <= 0) return Array.Empty<string>();

        // SECURITY: same path validation as the parser path so the dialog can't be tricked
        // into reading outside permitted locations.
        filePath = FilePathValidator.ValidatePathForLoad(filePath);

        var lines = new List<string>(Math.Min(maxLines, 128));
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (lines.Count < maxLines)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;
            lines.Add(line);
        }

        return lines;
    }
}

public interface IFilePreviewService
{
    Task<IReadOnlyList<string>> ReadFirstLinesAsync(string filePath, int maxLines = 100, CancellationToken ct = default);
}
