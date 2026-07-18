using DatPlotX.Helpers;
using System.IO.Compression;

namespace DatPlotX.Services;

/// <summary>
/// Handles GZip compression and decompression for project files
/// Provides efficient file size reduction for project data
/// </summary>
public class ProjectCompressor : IProjectCompressor
{
    // SECURITY: cap on decompressed project JSON (CWE-409, GZip decompression bomb).
    // A .DPX holds project metadata (panes, curves, annotations), not bulk row data, so a
    // small gz that expands past this is hostile, not legitimate. 200 MB is far above any
    // real project yet stops an out-of-memory crash from a crafted file.
    private const long MaxDecompressedBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Compress JSON string and write to file with GZip
    /// </summary>
    public async Task CompressAsync(string json, string filePath)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON string cannot be null or empty", nameof(json));

        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForSave(filePath);

        // DURABILITY: write to a sibling temp file first, then atomically replace the target.
        // FileMode.Create would truncate an existing .DPX up front, so a crash / disk-full /
        // process kill mid-write would destroy the user's saved project. Writing the full
        // compressed payload to a temp file and only then moving it into place means the
        // original file is never in a half-written state.
        var tempPath = filePath + ".tmp";
        try
        {
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            await using (var gzipStream = new GZipStream(fileStream, CompressionLevel.Optimal))
            await using (var writer = new StreamWriter(gzipStream))
            {
                await writer.WriteAsync(json);
                await writer.FlushAsync();
                await gzipStream.FlushAsync();
                await fileStream.FlushAsync();
            }

            // Atomically move the completed temp file over the target. File.Move with
            // overwrite replaces in a single step where the OS supports it; if the target
            // does not exist yet it is a plain rename.
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            // Best-effort cleanup of the partial temp file so we never leave litter behind.
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* best-effort */ }

            // SECURITY: Log detailed error but throw sanitized message (CWE-209)
            SafeErrorHandler.LogError(ex, "compressing project file", $"File: {Path.GetFileName(filePath)}");
            var userMessage = SafeErrorHandler.GetUserFriendlyMessage(ex, "compressing the project");
            throw new InvalidOperationException(userMessage, ex);
        }
    }

    /// <summary>
    /// Decompress GZip file and return JSON string
    /// </summary>
    public async Task<string> DecompressAsync(string filePath)
    {
        // SECURITY: Validate and normalize file path to prevent path traversal (CWE-22)
        filePath = FilePathValidator.ValidatePathForLoad(filePath);

        try
        {
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzipStream);

            // SECURITY: read through a fixed cap instead of ReadToEndAsync so a crafted gz that
            // expands without bound can't exhaust memory (CWE-409).
            var sb = new System.Text.StringBuilder();
            var buffer = new char[81920];
            int read;
            while ((read = await reader.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                sb.Append(buffer, 0, read);
                if (sb.Length > MaxDecompressedBytes)
                    throw new InvalidDataException(
                        "Decompressed project exceeds the maximum allowed size; the file may be corrupt or malicious.");
            }

            return sb.ToString();
        }
        catch (InvalidDataException)
        {
            // Not a GZip file (or over the decompression cap) - rethrow to let caller handle
            throw;
        }
        catch (Exception ex)
        {
            // SECURITY: Log detailed error but throw sanitized message (CWE-209)
            SafeErrorHandler.LogError(ex, "decompressing project file", $"File: {Path.GetFileName(filePath)}");
            var userMessage = SafeErrorHandler.GetUserFriendlyMessage(ex, "decompressing the project");
            throw new InvalidOperationException(userMessage, ex);
        }
    }
}

/// <summary>
/// Interface for project compression
/// </summary>
public interface IProjectCompressor
{
    /// <summary>
    /// Compress JSON string and write to file with GZip
    /// </summary>
    Task CompressAsync(string json, string filePath);

    /// <summary>
    /// Decompress GZip file and return JSON string
    /// </summary>
    Task<string> DecompressAsync(string filePath);
}
