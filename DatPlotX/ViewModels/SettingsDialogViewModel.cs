using CommunityToolkit.Mvvm.ComponentModel;
using DatPlotX.Models;

namespace DatPlotX.ViewModels;

public sealed partial class SettingsDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _hoverTooltipsEnabledByDefault;

    [ObservableProperty]
    private bool _crashReportingEnabled;

    public SettingsDialogViewModel(ApplicationSettings settings)
    {
        _hoverTooltipsEnabledByDefault = settings.HoverTooltipsEnabledByDefault;
        _crashReportingEnabled = settings.CrashReportingEnabled;
    }

    public void ApplyTo(ApplicationSettings settings)
    {
        settings.HoverTooltipsEnabledByDefault = HoverTooltipsEnabledByDefault;
        settings.CrashReportingEnabled = CrashReportingEnabled;
    }
}
