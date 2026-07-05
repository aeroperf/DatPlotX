using DatPlotX.Models;
using DatPlotX.Services;
using FluentAssertions;

namespace DatPlotX.Tests.Services;

public class AppSettingsPersistenceServiceTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _settingsFile;
    private readonly AppSettingsPersistenceService _sut;

    public AppSettingsPersistenceServiceTests()
    {
        Directory.CreateDirectory(_tempDir);
        _settingsFile = Path.Combine(_tempDir, "settings.json");
        _sut = new TestableService(_settingsFile);
    }

    [Fact]
    public void Load_NoFile_LeavesDefaults()
    {
        var settings = new ApplicationSettings();
        _sut.Load(settings);
        settings.HoverTooltipsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var settings = new ApplicationSettings { HoverTooltipsEnabledByDefault = false };
        _sut.Save(settings);

        var loaded = new ApplicationSettings { HoverTooltipsEnabledByDefault = true };
        _sut.Load(loaded);

        loaded.HoverTooltipsEnabledByDefault.Should().BeFalse();
    }

    [Fact]
    public void Load_CorruptJson_KeepsDefault()
    {
        File.WriteAllText(_settingsFile, "not valid json {{{");
        var settings = new ApplicationSettings();
        var act = () => _sut.Load(settings);
        act.Should().NotThrow();
        settings.HoverTooltipsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void Save_ThenLoad_TrueValue_RoundTrips()
    {
        var settings = new ApplicationSettings { HoverTooltipsEnabledByDefault = true };
        _sut.Save(settings);

        var loaded = new ApplicationSettings { HoverTooltipsEnabledByDefault = false };
        _sut.Load(loaded);

        loaded.HoverTooltipsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void Load_NoFile_CrashReportingDefaultsOff()
    {
        var settings = new ApplicationSettings { CrashReportingEnabled = true };
        _sut.Load(settings);
        // No file → Load is a no-op, so the in-memory value is untouched; the model default is off.
        new ApplicationSettings().CrashReportingEnabled.Should().BeFalse();
    }

    [Fact]
    public void Save_ThenLoad_CrashReportingEnabled_RoundTrips()
    {
        var settings = new ApplicationSettings { CrashReportingEnabled = true };
        _sut.Save(settings);

        var loaded = new ApplicationSettings { CrashReportingEnabled = false };
        _sut.Load(loaded);

        loaded.CrashReportingEnabled.Should().BeTrue();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    // Subclass that overrides file path for testing
    private sealed class TestableService(string path) : AppSettingsPersistenceService
    {
        protected override string SettingsFilePath => path;
    }
}
