using DatPlotX.Services;
using FluentAssertions;

namespace DatPlotX.Tests.Services;

public class ProjectCompressorTests
{
    private readonly ProjectCompressor _compressor = new();

    [Fact]
    public async Task CompressAsync_EmptyJson_Throws()
    {
        var act = () => _compressor.CompressAsync("", Path.GetTempFileName());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CompressAsync_PathTraversal_Throws()
    {
        var act = () => _compressor.CompressAsync("{}", "/tmp/../etc/evil.dpx");
        await act.Should().ThrowAsync<System.Security.SecurityException>();
    }

    [Fact]
    public async Task RoundTrip_CompressDecompress_RestoresOriginalJson()
    {
        var original = """{"projectName":"Test","version":"1.0"}""";
        var filePath = Path.GetTempFileName();
        try
        {
            await _compressor.CompressAsync(original, filePath);
            var restored = await _compressor.DecompressAsync(filePath);
            restored.Should().Be(original);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task CompressAsync_WritesSmaller_ThanPlainText()
    {
        // GZip compression only wins on repetitive content
        var bigJson = string.Concat(Enumerable.Repeat("""{"key":"value"},""", 500));
        var filePath = Path.GetTempFileName();
        try
        {
            await _compressor.CompressAsync(bigJson, filePath);
            var compressedSize = new FileInfo(filePath).Length;
            compressedSize.Should().BeLessThan(bigJson.Length);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task DecompressAsync_FileNotFound_Throws()
    {
        var act = () => _compressor.DecompressAsync("/tmp/datplot_nonexistent_test_file.dpx");
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DecompressAsync_PathTraversal_Throws()
    {
        var act = () => _compressor.DecompressAsync("/tmp/../etc/passwd");
        await act.Should().ThrowAsync<System.Security.SecurityException>();
    }

    [Fact]
    public async Task DecompressAsync_NotGzipFile_ThrowsInvalidData()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(filePath, "this is plain text not gzip");
            var act = () => _compressor.DecompressAsync(filePath);
            await act.Should().ThrowAsync<Exception>();
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}
