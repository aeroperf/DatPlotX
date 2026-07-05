using System.Text.Json;
using DatPlotX.Helpers;

namespace DatPlotX.Services;

public interface IRecentFilesService
{
    List<string> Load();
    void AddFile(string path);
    void RemoveFile(string path);
    void Clear();
}

public sealed class RecentFilesService : IRecentFilesService
{
    public const int MaxRecentFiles = 10;

    // Persist under the same per-OS application-data root as settings, logs, and crash
    // dumps (AppPaths.ConfigDirectory) so everything lives in one discoverable place.
    private static readonly string SettingsPath =
        Path.Combine(AppPaths.ConfigDirectory, "recent-files.json");

    public List<string> Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    public void AddFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > MaxRecentFiles)
            list = list.Take(MaxRecentFiles).ToList();
        Save(list);
    }

    public void RemoveFile(string path)
    {
        var list = Load();
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Save(list);
    }

    public void Clear() => Save([]);

    private static void Save(List<string> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}
