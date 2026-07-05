using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace DatPlotX.Services;

/// <summary>
/// Avalonia-backed implementation of <see cref="IApplicationLifetimeService"/>.
/// Delegates shutdown to <see cref="IClassicDesktopStyleApplicationLifetime"/> when available.
/// </summary>
public class ApplicationLifetimeService : IApplicationLifetimeService
{
    public void Shutdown(int exitCode = 0)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown(exitCode);
        }
    }
}
