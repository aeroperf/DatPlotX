using DatPlotX.Services;
using FluentAssertions;
using System.Text;

namespace DatPlotX.Tests.Services;

public class FilePreviewServiceTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var p in _tempFiles)
        {
            try { if (File.Exists(p)) File.Delete(p); }
            catch { /* cleanup only */ }
        }
    }

    private string WriteTemp(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"datplot_preview_{Guid.NewGuid():N}.csv");
        File.WriteAllText(path, contents, Encoding.UTF8);
        _tempFiles.Add(path);
        return path;
    }

    [Fact]
    public async Task ReadFirstLinesAsync_ShortFile_ReturnsAllLines()
    {
        var path = WriteTemp("a\nb\nc\n");
        var result = await new FilePreviewService().ReadFirstLinesAsync(path, 100);

        result.Should().HaveCount(3);
        result[0].Should().Be("a");
        result[2].Should().Be("c");
    }

    [Fact]
    public async Task ReadFirstLinesAsync_CapsAtMaxLines()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 250; i++) sb.Append("line").Append(i).Append('\n');
        var path = WriteTemp(sb.ToString());

        var result = await new FilePreviewService().ReadFirstLinesAsync(path, 100);

        result.Should().HaveCount(100);
        result[0].Should().Be("line0");
        result[99].Should().Be("line99");
    }

    [Fact]
    public async Task ReadFirstLinesAsync_PreservesBlankAndCommentLinesVerbatim()
    {
        // Blank/comment lines must be preserved so user-facing line numbers in the
        // preview match the actual file lines used by the parser's selector.
        var path = WriteTemp("# header comment\n\nname,value\n1,2\n");
        var result = await new FilePreviewService().ReadFirstLinesAsync(path, 100);

        result.Should().HaveCount(4);
        result[0].Should().Be("# header comment");
        result[1].Should().Be("");
        result[2].Should().Be("name,value");
    }

    [Fact]
    public async Task ReadFirstLinesAsync_NonExistentFile_Throws()
    {
        var act = async () => await new FilePreviewService().ReadFirstLinesAsync("/tmp/_nope_xyz.csv");
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ReadFirstLinesAsync_ZeroMaxLines_ReturnsEmpty()
    {
        var path = WriteTemp("a\nb\n");
        var result = await new FilePreviewService().ReadFirstLinesAsync(path, 0);
        result.Should().BeEmpty();
    }

    // P1: the preview service documents "same path validation as parser path" — a path
    // containing a `..` traversal segment must be rejected before any IO happens. Without
    // this guard, a poisoned dialog input could read arbitrary files.
    [Fact]
    public async Task ReadFirstLinesAsync_PathOutsidePermittedLocations_Throws()
    {
        var malicious = "/tmp/../etc/passwd";
        var act = async () => await new FilePreviewService().ReadFirstLinesAsync(malicious);
        await act.Should().ThrowAsync<System.Security.SecurityException>();
    }
}
