using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.Services.Analysis;
using DatPlotX.Services.Export;
using DatPlotX.Services.Logging;
using DatPlotX.Services.Units;
using DatPlotX.ViewModels;

namespace DatPlotX;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    [LoggerMessage(Level = LogLevel.Information, Message = "DatPlotX started ({Os})")]
    private static partial void LogStarted(ILogger logger, string os);

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => (ServiceProvider as IDisposable)?.Dispose();

            if (mainWindow.DataContext is MainWindowViewModel vm)
            {
                // Windows / CLI: a double-clicked .dpx is forwarded as a command-line argument.
                var startupArg = Helpers.StartupFileLocator.FindProjectArgument(desktop.Args);
                if (startupArg is not null && File.Exists(startupArg))
                    OpenProjectWhenReady(mainWindow, vm, startupArg);

                // macOS: a double-clicked file arrives as an activation event, not via argv.
                WireFileActivation(vm);
            }
        }

        base.OnFrameworkInitializationCompleted();

        var crashReporter = ServiceProvider.GetRequiredService<ICrashReporter>();
        var logger = ServiceProvider.GetService<ILogger<App>>();
        if (logger is not null)
            LogStarted(logger, System.Runtime.InteropServices.RuntimeInformation.OSDescription);

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                crashReporter.WriteCrashDump(ex, "AppDomain unhandled exception", args.IsTerminating);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            crashReporter.WriteCrashDump(args.Exception, "Unobserved task exception", isTerminating: false);
            args.SetObserved();
        };
    }

    /// <summary>
    /// Open a project once the main window has been shown — project restore relies on the pane
    /// controls having realized, which only happens after the window opens.
    /// </summary>
    private static void OpenProjectWhenReady(Window window, MainWindowViewModel vm, string path)
    {
        void Handler(object? sender, EventArgs e)
        {
            window.Opened -= Handler;
            Dispatcher.UIThread.Post(() => _ = vm.OpenProjectFromPathAsync(path));
        }

        window.Opened += Handler;
    }

    /// <summary>
    /// Subscribe to OS file-activation events (macOS delivers a double-clicked file this way rather
    /// than via the command line). No-op on platforms that don't expose an activatable lifetime.
    /// </summary>
    private void WireFileActivation(MainWindowViewModel vm)
    {
        if (this.TryGetFeature<IActivatableLifetime>() is not { } activatable)
            return;

        activatable.Activated += (_, e) =>
        {
            if (e is not FileActivatedEventArgs fileActivated)
                return;

            var path = fileActivated.Files
                .Select(f => f.TryGetLocalPath())
                .FirstOrDefault(p => !string.IsNullOrEmpty(p) &&
                    p!.EndsWith(Helpers.StartupFileLocator.ProjectExtension, StringComparison.OrdinalIgnoreCase));

            if (path is not null)
                Dispatcher.UIThread.Post(() => _ = vm.OpenProjectFromPathAsync(path));
        };
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Local-only disk logging (rolling daily, 50 MB cap, 7-day retention). No third-party
        // package, no network — the log file is the only sink. Reachable via Help → Open Log Folder.
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddProvider(new FileLoggerProvider(AppPaths.LogDirectory));
        });

        // Local-only crash dumps. Never uploaded; opt-in flag only gates a next-launch prompt.
        services.AddSingleton<ICrashReporter, CrashReporter>();

        services.AddSingleton<ApplicationSettings>();
        services.AddSingleton<IAppSettingsPersistenceService, AppSettingsPersistenceService>();
        services.AddSingleton<IApplicationLifetimeService, ApplicationLifetimeService>();

        services.AddSingleton<Services.Parsers.ICsvDataParser, Services.Parsers.CsvDataParser>();
        services.AddSingleton<Services.Parsers.IXPlaneDataParser, Services.Parsers.XPlaneDataParser>();
        services.AddSingleton<IDataImportService, DataImportService>();
        services.AddSingleton<IFilePreviewService, FilePreviewService>();
        services.AddSingleton<IDataExportService, DataExportService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileOperationsService, FileOperationsService>();
        services.AddSingleton<IGlobalEventLineService, GlobalEventLineService>();
        services.AddSingleton<ICalloutAnnotationService, CalloutAnnotationService>();
        services.AddSingleton<ITextAnnotationService, TextAnnotationService>();
        services.AddSingleton<IArrowAnnotationService, ArrowAnnotationService>();
        services.AddSingleton<IPaneCoordinationService, PaneCoordinationService>();
        services.AddSingleton<ICurveCoordinationService, CurveCoordinationService>();
        services.AddSingleton<IIntersectionCalculator, IntersectionCalculator>();
        services.AddSingleton<IProjectStateManager, ProjectStateManager>();
        services.AddSingleton<ProjectFileService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<IFileAssociationService, FileAssociationService>();
        services.AddSingleton<IExportStrategyFactory, ExportStrategyFactory>();
        services.AddSingleton<IGroupedDataIndexer, GroupedDataIndexer>();

        // Curve-analysis tools (replaces legacy PlotPaneStatisticsManager)
        services.AddSingleton<IUnitRegistry>(UnitRegistry.Default);
        services.AddSingleton<IMetricRegistry, MetricRegistry>();
        services.AddSingleton<IAnalysisService, AnalysisService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<AnalysisPanelViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }
}
