using System.Text.Json;
using DatPlotX.Helpers;
using DatPlotX.Models;

namespace DatPlotX.Services;

public class AppSettingsPersistenceService : IAppSettingsPersistenceService
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    // Persist under the same per-OS application-data root as logs and crash dumps
    // (AppPaths.ConfigDirectory) so everything lives in one discoverable place.
    protected virtual string SettingsFilePath
        => Path.Combine(AppPaths.ConfigDirectory, "settings.json");

    public void Load(ApplicationSettings settings)
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return;
            var json = File.ReadAllText(SettingsFilePath);
            var dto = JsonSerializer.Deserialize<SettingsDto>(json);
            if (dto is null) return;
            settings.HoverTooltipsEnabledByDefault = dto.HoverTooltipsEnabledByDefault;
            settings.CrashReportingEnabled = dto.CrashReportingEnabled;
        }
        catch { /* corrupt or missing — keep defaults */ }
    }

    public void Save(ApplicationSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);
            var dto = new SettingsDto
            {
                HoverTooltipsEnabledByDefault = settings.HoverTooltipsEnabledByDefault,
                CrashReportingEnabled = settings.CrashReportingEnabled
            };
            var json = JsonSerializer.Serialize(dto, _writeOptions);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }

    private sealed class SettingsDto
    {
        public bool HoverTooltipsEnabledByDefault { get; set; } = true;
        public bool CrashReportingEnabled { get; set; }
    }
}
