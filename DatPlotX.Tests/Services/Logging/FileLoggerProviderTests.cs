using DatPlotX.Services.Logging;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace DatPlotX.Tests.Services.Logging;

public class FileLoggerProviderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dpx-log-" + Guid.NewGuid());

    public FileLoggerProviderTests() => Directory.CreateDirectory(_dir);

    [Fact]
    public void Log_WritesEntryToDailyFile()
    {
        using var provider = new FileLoggerProvider(_dir, LogLevel.Information);
        var logger = provider.CreateLogger("DatPlotX.Demo.Widget");

        logger.LogInformation("imported {Rows} rows", 1234);

        var files = Directory.GetFiles(_dir, "datplotx-*.log");
        files.Should().ContainSingle();

        var content = File.ReadAllText(files[0]);
        content.Should().Contain("imported 1234 rows");
        content.Should().Contain("[INF]");
        content.Should().Contain("Widget"); // category trimmed to leaf type
    }

    [Fact]
    public void Log_BelowMinimumLevel_IsDropped()
    {
        using var provider = new FileLoggerProvider(_dir, LogLevel.Warning);
        var logger = provider.CreateLogger("Test");

        logger.LogInformation("should be filtered");
        logger.LogWarning("should be kept");

        var content = string.Concat(Directory.GetFiles(_dir, "datplotx-*.log").Select(File.ReadAllText));
        content.Should().NotContain("should be filtered");
        content.Should().Contain("should be kept");
    }

    [Fact]
    public void Constructor_PrunesFilesOlderThanRetention()
    {
        var stale = Path.Combine(_dir, "datplotx-20000101.log");
        File.WriteAllText(stale, "old");
        File.SetLastWriteTime(stale, DateTime.Now.AddDays(-(FileLoggerProvider.RetentionDays + 1)));

        var fresh = Path.Combine(_dir, "datplotx-20991231.log");
        File.WriteAllText(fresh, "new");
        File.SetLastWriteTime(fresh, DateTime.Now);

        using var provider = new FileLoggerProvider(_dir);

        File.Exists(stale).Should().BeFalse("files past retention are pruned on startup");
        File.Exists(fresh).Should().BeTrue();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
