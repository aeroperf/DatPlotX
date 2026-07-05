namespace DatPlotX.Services;

/// <summary>
/// Abstracts the application lifetime so ViewModels can trigger shutdown
/// without a direct dependency on Avalonia's IClassicDesktopStyleApplicationLifetime.
/// </summary>
public interface IApplicationLifetimeService
{
    void Shutdown(int exitCode = 0);
}
