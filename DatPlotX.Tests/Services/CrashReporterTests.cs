using DatPlotX.Services;
using FluentAssertions;

namespace DatPlotX.Tests.Services;

public class CrashReporterTests
{
    [Fact]
    public void WriteCrashDump_WritesScrubbedFile_AndFindLatestReturnsIt()
    {
        var sut = new CrashReporter();
        Exception ex;
        try { throw new InvalidOperationException("boom"); }
        catch (Exception caught) { ex = caught; }

        var path = sut.WriteCrashDump(ex, "unit-test", isTerminating: false);

        try
        {
            path.Should().NotBeNull();
            File.Exists(path).Should().BeTrue();

            var content = File.ReadAllText(path!);
            content.Should().Contain("InvalidOperationException");
            content.Should().Contain("boom");
            content.Should().Contain("local only");

            var latest = sut.FindLatestDump();
            latest.Should().Be(path);
        }
        finally
        {
            if (path is not null) { try { File.Delete(path); } catch { } }
        }
    }

    [Fact]
    public void WriteCrashDump_ScrubsAbsolutePathsFromException()
    {
        var sut = new CrashReporter();
        // An exception message embedding an absolute path that must not survive verbatim.
        var secretPath = OperatingSystem.IsWindows()
            ? @"C:\data\sample\Customer\Secret\flight.csv"
            : "/data/sample/Customer/Secret/flight.csv";
        var ex = new InvalidDataException($"failed reading {secretPath}");

        var path = sut.WriteCrashDump(ex, "ctx", isTerminating: false);

        try
        {
            var content = File.ReadAllText(path!);
            content.Should().NotContain("Customer");
            content.Should().NotContain("Secret");
            // Leaf filename is preserved for context.
            content.Should().Contain("flight.csv");
        }
        finally
        {
            if (path is not null) { try { File.Delete(path); } catch { } }
        }
    }
}
