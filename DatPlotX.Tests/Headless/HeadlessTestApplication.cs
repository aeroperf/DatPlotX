using Avalonia;
using Avalonia.Headless;
using DatPlotX.Tests.Headless;

[assembly: AvaloniaTestApplication(typeof(HeadlessTestApplication))]

namespace DatPlotX.Tests.Headless;

/// <summary>
/// Bootstraps the Avalonia app builder for the headless test session. The
/// <see cref="AvaloniaTestApplicationAttribute"/> above points the <c>[AvaloniaFact]</c> /
/// <c>[AvaloniaTheory]</c> runner at this <see cref="BuildAvaloniaApp"/> factory; every UI test
/// then runs on a single shared headless dispatcher with no display, GPU, or windowing system.
/// </summary>
public static class HeadlessTestApplication
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
