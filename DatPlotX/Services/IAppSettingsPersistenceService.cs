using DatPlotX.Models;

namespace DatPlotX.Services;

public interface IAppSettingsPersistenceService
{
    void Load(ApplicationSettings settings);
    void Save(ApplicationSettings settings);
}
