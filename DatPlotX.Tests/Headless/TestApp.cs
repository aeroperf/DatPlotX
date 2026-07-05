namespace DatPlotX.Tests.Headless;

/// <summary>
/// Avalonia <see cref="Avalonia.Application"/> used by headless UI tests. It derives from the real
/// <see cref="DatPlotX.App"/> so the full style stack (Fluent, DataGrid, OxyPlot, DpxStyles/Tokens)
/// loads exactly as it does at runtime — without that, styled controls and the DataGrid render blank.
///
/// <para>
/// The real <c>App.OnFrameworkInitializationCompleted</c> only builds the DI container and shows the
/// main window when the lifetime is an <c>IClassicDesktopStyleApplicationLifetime</c>. Under the
/// headless test session that lifetime is absent, so the base implementation is a no-op past
/// <c>base.OnFrameworkInitializationCompleted()</c> and there is nothing to override here. Tests
/// construct the view under test directly and assign a view model as its <c>DataContext</c>.
/// </para>
/// </summary>
public sealed class TestApp : App
{
}
