using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DatPlotX.Helpers;
using DatPlotX.Models;
using DatPlotX.Models.Analysis;
using DatPlotX.Services;
using DatPlotX.Services.Analysis;
using DatPlotX.Services.Units;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;

namespace DatPlotX.ViewModels;

/// <summary>
/// Main application ViewModel coordinating multi-pane plotting, curves, event lines, and project management
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    #region Services

    private readonly IDataImportService _importService;
    private readonly IDialogService _dialogService;
    private readonly IIntersectionCalculator _intersectionCalculator;
    private readonly IProjectStateManager _stateManager;
    private readonly IGlobalEventLineService _globalEventLineService;
    private readonly ICalloutAnnotationService _calloutAnnotationService;
    private readonly ITextAnnotationService _textAnnotationService;
    private readonly IArrowAnnotationService _arrowAnnotationService;
    private readonly IPaneCoordinationService _paneCoordinationService;
    private readonly ICurveCoordinationService _curveCoordinationService;
    private readonly IFileOperationsService _fileOperationsService;
    private readonly IApplicationLifetimeService _lifetimeService;
    private readonly ApplicationSettings _applicationSettings;
    private readonly IAppSettingsPersistenceService _settingsPersistence;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IGroupedDataIndexer _groupedDataIndexer;
    private readonly IAnalysisService _analysisService;
    private readonly IUnitRegistry _unitRegistry;
    private readonly ICrashReporter _crashReporter;
    private readonly IFileAssociationService _fileAssociationService;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private string _windowTitle = "DatPlotX";

    [ObservableProperty]
    private string _statusText = "Ready. Import a data file to begin.";

    [ObservableProperty]
    private string _mousePositionText = "";

    /// <summary>
    /// Whether the dockable Analysis Results panel is shown. The panel is an ad-hoc
    /// inspection tool — its visibility is transient session state and is deliberately
    /// NOT persisted to the <c>.DPX</c> project file.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalysisPanelVisible))]
    private bool _showAnalysisPanel;

    /// <summary>True when the Analysis Results panel should be on screen (analysis-capable mode, project open, toggle on).</summary>
    public bool IsAnalysisPanelVisible => IsProjectActive && IsAnalysisAvailable && ShowAnalysisPanel;

    /// <summary>
    /// True when an analysis source is wired (Stacked or Compact mode). Gates the Analysis
    /// panel, the Analyze-Curves / Manage-Segments menu items, and the panel-toggle command —
    /// replaces the old <see cref="IsPanesMode"/> gate now that Compact also drives analysis.
    /// Set whenever a source is attached/detached via <c>SetSource</c>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalysisPanelVisible))]
    private bool _isAnalysisAvailable;

    partial void OnIsAnalysisAvailableChanged(bool value)
    {
        ManageAnalysisSegmentsCommand.NotifyCanExecuteChanged();
        ManageToleranceBandCommand.NotifyCanExecuteChanged();
        ManageMetricsCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Whether the inline corner overlay (top metrics drawn on each pane) is shown. Transient
    /// session state like the panel — not persisted. Default off.
    /// </summary>
    [ObservableProperty]
    private bool _showInlineMetrics;

    partial void OnShowInlineMetricsChanged(bool value)
    {
        _analysisService.ShowInlineOverlay = value;
        if (value)
            RefreshInlineOverlay();
        else
            _stackedAnalysisOverlay?.HideInlineLabels();
    }

    /// <summary>
    /// Toggle the inline corner overlay (command-driven, like the panel toggle).
    /// Stacked-only: the overlay draws in each pane corner; Compact ships analysis panel-only,
    /// so the command is disabled there (the View menu item stays visible but greyed out).
    /// </summary>
    [RelayCommand(CanExecute = nameof(IsPanesMode))]
    private void ToggleInlineMetrics() => ShowInlineMetrics = !ShowInlineMetrics;

    [ObservableProperty]
    private int _eventLineCount = 0;

    [ObservableProperty]
    private bool _hasData = false;

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    [ObservableProperty]
    private DataTable? _intersectionData;

    [ObservableProperty]
    private DataView? _sourceData;

    [ObservableProperty]
    private int _paneCount = 1;

    [ObservableProperty]
    private bool _xAxisSynchronized = true;

    [ObservableProperty]
    private string? _selectedXColumn;

    /// <summary>
    /// Triggered when X-axis column selection changes - replot all curves with new X parameter
    /// </summary>
    partial void OnSelectedXColumnChanged(string? value)
    {
        _currentXColumn = value;
        HasUnsavedChanges = true;
        NotifyCommandsCanExecuteChanged();
        if (IsCompactMode)
        {
            CompactPlot.SetData(_currentData, value);
        }
        else if (IsGroupedMode)
        {
            // Grouped mode owns its own X/Y selectors in the sidebar — the top X picker
            // is hidden in this mode (see MainWindow.axaml), so nothing to propagate here.
        }
        else
        {
            ReplotAllCurves();
        }
    }

    /// <summary>
    /// Plot surface style for the current project. Locked at project creation; changing it
    /// requires <see cref="ResetToNewProject(PlotMode)"/> + re-import.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompactMode))]
    [NotifyPropertyChangedFor(nameof(IsPanesMode))]
    [NotifyPropertyChangedFor(nameof(IsGroupedMode))]
    [NotifyPropertyChangedFor(nameof(IsAnalysisPanelVisible))]
    [NotifyCanExecuteChangedFor(nameof(ToggleInlineMetricsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfigureGroupedInputsCommand))]
    private PlotMode _plotMode = PlotMode.Panes;

    public bool IsCompactMode => PlotMode == PlotMode.Compact;
    public bool IsPanesMode => PlotMode == PlotMode.Panes;
    public bool IsGroupedMode => PlotMode == PlotMode.Grouped;

    /// <summary>ViewModel for the Compact Plot Surface; only meaningful when <see cref="IsCompactMode"/>.</summary>
    public CompactPlotViewModel CompactPlot { get; } = new();

    /// <summary>ViewModel for the Grouped Parameter Plot; only meaningful when <see cref="IsGroupedMode"/>.</summary>
    public GroupedPlotViewModel GroupedPlot { get; }

    [ObservableProperty]
    private bool _hoverTooltipsEnabled = true;

    /// <summary>
    /// True once the user has created a new project (with plot mode + data) or opened an existing project.
    /// Drives the startup welcome view: when false, the plot surface and Source Data pane are hidden.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnalysisPanelVisible))]
    private bool _isProjectActive;

    /// <summary>
    /// Persisted Source Data pane collapsed state. Saved/restored with the project.
    /// </summary>
    [ObservableProperty]
    private bool _bottomPaneCollapsed;

    partial void OnHoverTooltipsEnabledChanged(bool value)
    {
        HoverTooltipsEnabledChanged?.Invoke(value);
    }

    partial void OnHasUnsavedChangesChanged(bool value) => UpdateWindowTitle();

    #endregion

    #region Events

    /// <summary>Fired when hover tooltip enabled state changes — MainWindow propagates to all panes</summary>
    public event Action<bool>? HoverTooltipsEnabledChanged;

    /// <summary>Fired when the user requests an image export of the Grouped Parameter Plot —
    /// MainWindow handles it because the ScottPlot <c>Plot</c> instance lives in the view.</summary>
    public event EventHandler? GroupedPlotExportRequested;

    /// <summary>Fired when the user opts to view a saved crash report — MainWindow reveals the
    /// supplied folder in the OS file manager (a view concern).</summary>
    public event Action<string>? CrashFolderOpenRequested;

    #endregion

    #region Collections

    public ObservableCollection<string> AvailableXColumns { get; } = new();
    public ObservableCollection<PlotPaneViewModel> Panes { get; } = new();
    public ObservableCollection<CurveConfigurationModel> ActiveCurves { get; } = new();
    public ObservableCollection<string> RecentFiles { get; } = new();

    /// <summary>The dockable Analysis Results panel VM (resolved from DI).</summary>
    public AnalysisPanelViewModel AnalysisPanel { get; }

    #endregion

    #region Private State

    private ProjectSettingsModel? _currentProject;
    private PlotDataModel? _currentData;
    private string? _currentFilePath;
    private string? _currentXColumn;

    // Analysis (Stacked mode). The source + overlay host adapt the live panes to the
    // mode-agnostic IAnalysisService. Built lazily when Panes mode is (re)entered.
    private StackedAnalysisCurveSource? _stackedAnalysisSource;
    private StackedAnalysisOverlayHost? _stackedAnalysisOverlay;
    // Analysis (Compact mode). Built lazily when Compact mode is (re)entered. Compact has no
    // inline-overlay support yet — only the panel + the persistent segment band.
    private CompactAnalysisCurveSource? _compactAnalysisSource;
    private CompactAnalysisOverlayHost? _compactAnalysisOverlay;
    // The overlay host + curve source for whichever analysis-capable mode is currently live
    // (Stacked or Compact). Mode-agnostic code (segment band, point flash, pan/zoom + curve
    // notifications) routes through these so it doesn't care which mode built them.
    private IAnalysisOverlayHost? _activeAnalysisOverlay;
    private IAnalysisCurveSource? _activeAnalysisSource;
    // Cancels a superseded inline-overlay recompute (rapid pan/zoom queues several).
    private CancellationTokenSource? _inlineOverlayCts;

    /// <summary>
    /// Default color palette for curves - cycles through 16 distinct colors
    /// </summary>
    private readonly System.Collections.Immutable.ImmutableArray<string> _colorPalette = DefaultCurvePalette.Colors;

    #endregion

    #region Constructor

    public MainWindowViewModel(
        IDataImportService importService,
        IDialogService dialogService,
        IIntersectionCalculator intersectionCalculator,
        IProjectStateManager stateManager,
        IGlobalEventLineService globalEventLineService,
        ICalloutAnnotationService calloutAnnotationService,
        ITextAnnotationService textAnnotationService,
        IArrowAnnotationService arrowAnnotationService,
        IPaneCoordinationService paneCoordinationService,
        ICurveCoordinationService curveCoordinationService,
        IFileOperationsService fileOperationsService,
        IApplicationLifetimeService lifetimeService,
        ApplicationSettings applicationSettings,
        IAppSettingsPersistenceService settingsPersistence,
        IRecentFilesService recentFilesService,
        IGroupedDataIndexer groupedDataIndexer,
        IAnalysisService analysisService,
        IUnitRegistry unitRegistry,
        ICrashReporter crashReporter,
        IFileAssociationService fileAssociationService,
        AnalysisPanelViewModel analysisPanel)
    {
        _importService = importService;
        _dialogService = dialogService;
        _lifetimeService = lifetimeService;
        _intersectionCalculator = intersectionCalculator;
        _stateManager = stateManager;
        _globalEventLineService = globalEventLineService;
        _calloutAnnotationService = calloutAnnotationService;
        _textAnnotationService = textAnnotationService;
        _arrowAnnotationService = arrowAnnotationService;
        _paneCoordinationService = paneCoordinationService;
        _curveCoordinationService = curveCoordinationService;
        _fileOperationsService = fileOperationsService;
        _applicationSettings = applicationSettings;
        _settingsPersistence = settingsPersistence;
        _recentFilesService = recentFilesService;
        _groupedDataIndexer = groupedDataIndexer;
        _analysisService = analysisService;
        _unitRegistry = unitRegistry;
        _crashReporter = crashReporter;
        _fileAssociationService = fileAssociationService;
        AnalysisPanel = analysisPanel;
        AnalysisPanel.PointFlashRequested += OnAnalysisPointFlashRequested;
        AnalysisPanel.PlaceEventLineRequested += OnAnalysisPlaceEventLineRequested;
        AnalysisPanel.LineToggleRequested += OnAnalysisLineToggleRequested;
        AnalysisPanel.TableInvalidated += OnAnalysisTableInvalidated;
        AnalysisPanel.BandsInvalidated += OnAnalysisBandsInvalidated;
        AnalysisPanel.BandRemoveRequested += OnAnalysisBandRemoveRequested;
        AnalysisPanel.CsvExportRequested += OnAnalysisCsvExportRequested;

        // Compact surface refuses to draw more bands than it can fit (a wide import + "Select All"
        // would otherwise collapse the plot area and crash Avalonia's layout pass). Warn when clamped.
        CompactPlot.BandLimitExceeded += OnCompactBandLimitExceeded;

        // Clear / Manage compact-curve commands gate on CompactPlot.Curves.Count. The direct
        // mutators already re-notify, but project load (ReplaceCurves) and any future path
        // bypass them — subscribe here so CanExecute can never go stale.
        CompactPlot.Curves.CollectionChanged += OnCompactCurvesChangedForCommands;

        // Adding / removing / clearing curves all mutate ActiveCurves — use it as the
        // single "curves changed" signal that drives an analysis recompute. Stored in a
        // field so Dispose can detach it (the analysis service is a singleton).
        ActiveCurves.CollectionChanged += OnActiveCurvesChangedForAnalysis;

        GroupedPlot = new GroupedPlotViewModel(_groupedDataIndexer, _applicationSettings);

        // Load persisted settings
        _settingsPersistence.Load(_applicationSettings);
        _hoverTooltipsEnabled = _applicationSettings.HoverTooltipsEnabledByDefault;
        LoadRecentFiles();

        // Set up large file warning callback
        _importService.OnLargeFileWarning = OnLargeFileWarning;

        IntersectionData = new DataTable();
        IntersectionCalculator.InitializeIntersectionTable(IntersectionData);

        InitializeDefaultPane();
    }

    #endregion

    /// <summary>
    /// On startup, if crash-report notifications are enabled and a dump from a previous session
    /// exists, offer to open the (local-only) crash folder. Declining deletes the dump so the user
    /// isn't nagged on every launch; choosing to view it leaves the file in place to attach to a
    /// bug report. No-op when the opt-in setting is off. Never throws.
    /// </summary>
    public async Task CheckForPreviousCrashAsync()
    {
        try
        {
            if (!_applicationSettings.CrashReportingEnabled)
                return;

            var dumpPath = _crashReporter.FindLatestDump();
            if (dumpPath is null)
                return;

            var result = await _dialogService.ShowConfirmation(
                "DatPlotX closed unexpectedly during a previous session. A crash report was saved " +
                "on this computer (it was not sent anywhere).\n\n" +
                "Open the crash report folder so you can review or attach it to a bug report?",
                "Crash Report Found");

            if (result == DialogResult.Yes)
            {
                CrashFolderOpenRequested?.Invoke(_crashReporter.CrashDirectory);
            }
            else
            {
                try { System.IO.File.Delete(dumpPath); } catch { /* best-effort */ }
            }
        }
        catch
        {
            // A crash-report prompt must never itself break startup.
        }
    }

    private void InitializeDefaultPane()
    {
        var paneModel = new PlotPaneModel
        {
            Index = 0,
            Name = "Pane 1",
            XAxisLabel = "Time (s)",
            YAxisLabel = "Value",
            ShowGrid = true,
            ShowLegend = true,
            ShowXAxisLabels = true,
            // Default to 2 decimals; smart-decimals overrides from the data range when the
            // first curve is added (see ApplySmartDecimalDefaults).
            XAxisDecimalPlaces = 2,
            Y1AxisDecimalPlaces = 2,
            Y2AxisDecimalPlaces = 2
        };

        var paneViewModel = new PlotPaneViewModel(paneModel);
        Panes.Add(paneViewModel);
        PaneCount = Panes.Count;

        // Default new project is Panes mode — wire analysis over the live panes.
        EnsureStackedAnalysisSource();
    }

    /// <summary>
    /// Apply smart decimal place defaults to a pane based on its current axis ranges
    /// This should only be called when the FIRST curve is added to a pane
    /// </summary>
    /// <param name="paneViewModel">The pane to update</param>
    public void ApplySmartDecimalDefaults(PlotPaneViewModel paneViewModel) =>
        _curveCoordinationService.ApplySmartDecimalDefaults(paneViewModel);

    /// <summary>
    /// Apply smart decimal defaults to all panes after loading a project
    /// Only applies if panes have curves and are using default decimal values (0)
    /// </summary>
    private void ApplySmartDefaultsAfterLoad()
    {
        if (Panes.Count == 0)
            return;

        // Track if we found any pane needing smart defaults
        bool appliedDefaults = false;
        int? xAxisDecimals = null;

        foreach (var pane in Panes)
        {
            // Check if this pane has curves
            var curveCount = pane.GetAllCurveConfigs().Count;
            if (curveCount == 0 || pane.PlotModel == null)
                continue;

            // Check if pane is using all default values (0) - meaning user hasn't customized
            bool isUsingDefaults = pane.PaneModel.XAxisDecimalPlaces == 0 &&
                                   pane.PaneModel.Y1AxisDecimalPlaces == 0 &&
                                   pane.PaneModel.Y2AxisDecimalPlaces == 0;

            if (isUsingDefaults)
            {
                // Force a render first to ensure axis ranges are calculated
                pane.PlotModel.RenderInMemory(800, 400);

                // Apply smart defaults based on actual data ranges
                ApplySmartDecimalDefaults(pane);
                appliedDefaults = true;

                // Track the first X-axis decimals we calculate
                if (!xAxisDecimals.HasValue)
                {
                    xAxisDecimals = pane.PaneModel.XAxisDecimalPlaces;
                }
            }
        }

        // If we applied smart defaults, synchronize X-axis decimals across all panes
        if (appliedDefaults && xAxisDecimals.HasValue && Panes.Count > 1)
        {
            foreach (var pane in Panes)
            {
                pane.PaneModel.XAxisDecimalPlaces = xAxisDecimals.Value;
                pane.ApplyFormatting();
            }
        }
    }

    #region File Menu Commands

    [RelayCommand]
    private async Task NewProject()
    {
        if (HasUnsavedChanges)
        {
            var result = await _dialogService.ShowUnsavedChangesDialog();
            if (result == DialogResult.Cancel)
                return;
            if (result == DialogResult.Yes)
                await SaveProject();
        }

        var mode = await PromptForPlotMode();
        if (mode is null) return; // user cancelled at plot-mode picker

        // New flow: pick data file before the surface is shown. Cancel at the file
        // picker aborts the New Project operation entirely — no empty surface.
        try
        {
            StatusText = "Opening data file...";
            var imported = await _fileOperationsService.ImportDataFileAsync();
            switch (imported.Outcome)
            {
                case FileOperationOutcome.Cancelled:
                    StatusText = "New project cancelled.";
                    return;
                case FileOperationOutcome.Failed:
                    StatusText = "Import failed.";
                    return;
            }

            ResetToNewProject(mode.Value);
            LoadDataIntoCurrentProject(imported.Value!);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error importing file: {ex.Message}", "Import Error");
            StatusText = "Import failed.";
        }
    }

    /// <summary>
    /// Show the modal "Choose Plot Style" dialog and return the user's selection,
    /// or <c>null</c> if cancelled. Single source of truth for the picker — used by
    /// New Project, first-time CSV import, and project load when PlotMode is missing.
    /// </summary>
    private Task<PlotMode?> PromptForPlotMode() => _dialogService.ShowPlotModePickerAsync();

    [RelayCommand]
    private async Task NewCompactProject()
    {
        if (HasUnsavedChanges)
        {
            var result = await _dialogService.ShowUnsavedChangesDialog();
            if (result == DialogResult.Cancel)
                return;
            if (result == DialogResult.Yes)
                await SaveProject();
        }
        ResetToNewProject(PlotMode.Compact);
    }

    /// <summary>
    /// Reset application state to a new, empty project. Defaults to <see cref="PlotMode.Panes"/>.
    /// </summary>
    private void ResetToNewProject() => ResetToNewProject(PlotMode.Panes);

    /// <summary>
    /// Reset application state to a new, empty project with the given plot surface style.
    /// The chosen <paramref name="mode"/> is persisted on the project model and the UI swaps
    /// between the multi-pane ScottPlot surface and the Compact OxyPlot surface.
    /// </summary>
    public void ResetToNewProject(PlotMode mode)
    {
        _currentProject = new ProjectSettingsModel
        {
            ProjectName = "Untitled Project",
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now,
            PlotMode = mode,
        };
        _currentFilePath = null;
        ReplaceCurrentData(null);
        _currentXColumn = null;

        SelectedXColumn = null;
        AvailableXColumns.Clear();

        ClearProjectScopedServiceState();
        ClearEventLinesList();
        TearDownPaneSurface();

        CompactPlot.Clear();
        CompactPlot.SetData(null, null);

        GroupedPlot.ApplyConfig(new GroupedPlotConfig());
        GroupedPlot.SetData(null);
        if (mode == PlotMode.Grouped)
        {
            _currentProject.GroupedPlot = new GroupedPlotConfig();
        }

        IntersectionData?.Clear();
        SourceData = null;

        PlotMode = mode;

        // TearDownPaneSurface → InitializeDefaultPane already (re)wired the Stacked analysis
        // source. Re-point analysis at the mode that's actually live: Stacked over the panes,
        // Compact over the OxyPlot surface, neither for Grouped.
        TearDownStackedAnalysisSource();
        TearDownCompactAnalysisSource();
        if (mode == PlotMode.Panes)
            EnsureStackedAnalysisSource();
        else if (mode == PlotMode.Compact)
            EnsureCompactAnalysisSource();

        // Fresh project — segment auto-naming restarts at 1.
        _segmentCounter = 0;

        HasData = false;
        HasUnsavedChanges = false;
        IsProjectActive = false;
        BottomPaneCollapsed = false;
        NotifyCommandsCanExecuteChanged();
        UpdateWindowTitle();
        StatusText = mode switch
        {
            PlotMode.Compact => "New compact-surface project. Import data to begin.",
            PlotMode.Grouped => "New grouped-plot project. Import data to begin.",
            _ => "New project created. Import data to begin.",
        };
    }

    /// <summary>
    /// Clear all panes and active curves
    /// </summary>
    private void ClearAllPanesAndCurves()
    {
        foreach (var pane in Panes)
        {
            pane.Clear();
        }
        ActiveCurves.Clear();
    }

    /// <summary>
    /// Wipe the multi-pane ScottPlot surface back to a single empty default pane:
    /// clear curves, dispose pane VMs (unsubscribes their event handlers), drop the
    /// observable collection, then re-seed one pane. Shared by <see cref="ResetToNewProject(PlotMode)"/>
    /// and the Compact branch of <see cref="ApplyLoadedProject"/>.
    /// </summary>
    private void TearDownPaneSurface()
    {
        ClearAllPanesAndCurves();
        foreach (var pane in Panes) pane.Dispose();
        Panes.Clear();
        InitializeDefaultPane();
    }

    /// <summary>
    /// Reset the event-line count. The live event lines are owned by the global event-line
    /// service; this only zeroes the displayed count for a fresh/loaded project.
    /// </summary>
    private void ClearEventLinesList()
    {
        EventLineCount = 0;
    }

    /// <summary>
    /// Clear all project-scoped state held by singleton services so a new or freshly
    /// loaded project starts from a clean slate. Must be invoked before applying any
    /// new/loaded project, and during restore even when saved collections are empty.
    /// Order matters: call this while the old Panes still exist so visuals detach cleanly.
    /// </summary>
    private void ClearProjectScopedServiceState()
    {
        _globalEventLineService.ClearAllGlobalEventLines(Panes);
        _calloutAnnotationService.ClearAllCallouts(Panes);
        _textAnnotationService.ClearAllAnnotations(Panes);
        _arrowAnnotationService.ClearAllAnnotations(Panes);
        EventLineCount = 0;
    }

    /// <summary>
    /// Replace <see cref="_currentData"/> and dispose the previous instance, unless
    /// the previous instance is still referenced by <see cref="_currentProject"/>
    /// (in which case the project owns it). Also a no-op if previous == new.
    /// </summary>
    private void ReplaceCurrentData(PlotDataModel? newData)
    {
        var previous = _currentData;
        _currentData = newData;

        if (previous == null) return;
        if (ReferenceEquals(previous, newData)) return;
        if (ReferenceEquals(previous, _currentProject?.PlotData)) return;

        previous.Dispose();
    }

    /// <summary>
    /// Wire an already-loaded <see cref="PlotDataModel"/> into the current project,
    /// rebuild the source-data view, and update the X-axis selector. Caller is
    /// responsible for ensuring <c>_currentProject</c> exists (typically via
    /// <see cref="ResetToNewProject(PlotMode)"/>).
    /// </summary>
    private void LoadDataIntoCurrentProject(PlotDataModel data)
    {
        ReplaceCurrentData(data);
        _currentProject!.PlotData = _currentData;
        _currentProject.DataSourcePath = data.SourcePath;

        SourceData = _currentData!.Data.DefaultView;
        UpdateAvailableXColumns();

        if (IsCompactMode)
        {
            CompactPlot.SetData(_currentData, SelectedXColumn);
        }
        else if (IsGroupedMode)
        {
            // First import into a Grouped project (or one whose previous CSV had different columns)
            // triggers the inputs-picker dialog. Configured projects skip straight to rebind.
            _currentProject!.GroupedPlot ??= new GroupedPlotConfig();
            GroupedPlot.ApplyConfig(_currentProject.GroupedPlot);
            GroupedPlot.SetData(_currentData);
            if (_currentProject.GroupedPlot.Inputs.Count == 0)
            {
                // Defer the picker until after the first render so the welcome state has cleared.
                _ = PromptGroupedInputsAfterImportAsync();
            }
        }

        HasData = true;
        HasUnsavedChanges = true;
        IsProjectActive = true;
        UpdateWindowTitle();
        NotifyCommandsCanExecuteChanged();
        StatusText = $"Imported {_currentData.RowCount} rows, {_currentData.ColumnCount} columns from {_currentData.SourceName}";
    }

    private void NotifyCommandsCanExecuteChanged()
    {
        AddCurvesCommand.NotifyCanExecuteChanged();
        ManageCurvesCommand.NotifyCanExecuteChanged();
        ClearAllCurvesCommand.NotifyCanExecuteChanged();
        AddCompactCurvesCommand.NotifyCanExecuteChanged();
        ClearCompactCurvesCommand.NotifyCanExecuteChanged();
        FormatCompactPaneCommand.NotifyCanExecuteChanged();
        ManageCompactCurvesCommand.NotifyCanExecuteChanged();
        ConfigureGroupedInputsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        if (HasUnsavedChanges)
        {
            var result = await _dialogService.ShowUnsavedChangesDialog();
            if (result == DialogResult.Cancel)
                return;
            if (result == DialogResult.Yes)
                await SaveProject();
        }

        try
        {
            StatusText = "Loading project...";

            var loaded = await _fileOperationsService.LoadProjectFileAsync();

            switch (loaded.Outcome)
            {
                case FileOperationOutcome.Cancelled:
                    StatusText = "Load cancelled.";
                    return;
                case FileOperationOutcome.Failed:
                    StatusText = "Failed to open project.";
                    return;
            }

            var (project, path) = loaded.Value!;
            _currentFilePath = path;
            await ApplyLoadedProject(project);
            _recentFilesService.AddFile(path);
            LoadRecentFiles();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error opening project: {ex.Message}", "Open Error");
            StatusText = "Failed to open project.";
        }
    }

    [RelayCommand]
    private async Task OpenRecentFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!File.Exists(path))
        {
            _recentFilesService.RemoveFile(path);
            LoadRecentFiles();
            await _dialogService.ShowInformation(
                $"'{path}' no longer exists and has been removed from the recent files list.",
                "File Not Found");
            return;
        }

        await OpenProjectFromPathAsync(path);
    }

    /// <summary>
    /// Open a <c>.DPX</c> project from an explicit path. Honors the unsaved-changes prompt and
    /// updates the recent-files list. Shared by the recent-files command and by external launch
    /// (command-line argument / OS file-association double-click — see <see cref="App"/>).
    /// </summary>
    public async Task OpenProjectFromPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        if (!File.Exists(path))
        {
            await _dialogService.ShowInformation($"'{path}' could not be found.", "File Not Found");
            return;
        }

        if (HasUnsavedChanges)
        {
            var result = await _dialogService.ShowUnsavedChangesDialog();
            if (result == DialogResult.Cancel)
                return;
            if (result == DialogResult.Yes)
                await SaveProject();
        }

        try
        {
            StatusText = "Loading project...";
            var loaded = await _fileOperationsService.LoadProjectFromPathAsync(path);
            if (loaded.Outcome != FileOperationOutcome.Success)
            {
                StatusText = "Load failed.";
                return;
            }
            _currentFilePath = path;
            await ApplyLoadedProject(loaded.Value!);
            _recentFilesService.AddFile(path);
            LoadRecentFiles();
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error opening project: {ex.Message}", "Open Error");
            StatusText = "Failed to open project.";
        }
    }

    /// <summary>
    /// True when DatPlotX can register itself as the <c>.dpx</c> file handler from inside the app
    /// (Windows). On macOS the association is declared in the app bundle, so the menu item is hidden.
    /// </summary>
    public bool IsFileAssociationSupported => _fileAssociationService.IsSupported;

    /// <summary>
    /// Opt-in: make DatPlotX the default handler for <c>.dpx</c> files for the current user so a
    /// double-click opens them here. Windows-only; writes per-user registry keys (no admin needed).
    /// </summary>
    [RelayCommand]
    private async Task RegisterFileAssociation()
    {
        if (!_fileAssociationService.IsSupported)
            return;

        try
        {
            _fileAssociationService.Register();
            await _dialogService.ShowInformation(
                "DatPlotX is now the default application for .dpx project files. " +
                "Double-clicking a .dpx file will open it here.",
                "File Association");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError(
                $"Could not set DatPlotX as the default for .dpx files.\n\n{ex.Message}",
                "File Association");
        }
    }

    [RelayCommand]
    private void ClearRecentFiles()
    {
        _recentFilesService.Clear();
        LoadRecentFiles();
    }

    private void LoadRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var p in _recentFilesService.Load())
            RecentFiles.Add(p);
    }

    private async Task ApplyLoadedProject(ProjectSettingsModel project)
    {
        _currentProject = project;
        ReplaceCurrentData(_currentProject.PlotData);

        // Project files without a saved PlotMode (older format or hand-edited JSON) prompt
        // the user to pick one. Mark the project dirty so the choice is captured on save.
        bool legacyModeUpgrade = false;
        if (project.PlotMode is null)
        {
            var picked = await PromptForPlotMode();
            project.PlotMode = picked ?? Models.PlotMode.Panes;
            legacyModeUpgrade = true;
        }

        PlotMode = project.PlotMode.Value;

        if (_currentData != null)
        {
            SourceData = _currentData.Data.DefaultView;
            UpdateAvailableXColumns();

            // Honor saved project-level X column when it still exists in the data; otherwise
            // UpdateAvailableXColumns has already auto-selected the first numeric column.
            if (!string.IsNullOrEmpty(project.SelectedXColumn)
                && AvailableXColumns.Contains(project.SelectedXColumn))
            {
                SelectedXColumn = project.SelectedXColumn;
            }
        }

        ClearProjectScopedServiceState();

        // Mark the project active BEFORE restoring panes so the panes ItemsControl
        // (which is bound to IsProjectActive) realizes the new PlotPaneControls.
        // Without this, the Loaded event never fires during restore, PlotModel stays
        // null, and AddScatterCurve silently no-ops — leaving an empty plot.
        IsProjectActive = true;

        if (PlotMode == PlotMode.Compact)
        {
            // Order matters: TearDownPaneSurface → InitializeDefaultPane re-wires the Stacked
            // analysis source, so tear that down AFTER, not before, or it survives as a dangling
            // _stackedAnalysisSource. A later load of a Stacked project would then see it non-null,
            // skip EnsureStackedAnalysisSource (early-returns when set), never attach, and leave
            // IsAnalysisAvailable false — blanking the View menu + killing Ctrl+R.
            TearDownPaneSurface();
            TearDownStackedAnalysisSource();
            CompactPlot.ApplySettings(project.CompactPaneSettings ?? new CompactPaneSettings());
            CompactPlot.SetData(_currentData, SelectedXColumn);
            CompactPlot.ReplaceCurves(project.CompactCurves);
            CompactPlot.ReplaceEventLines(project.CompactEventLines);
            CompactPlot.ReplaceAnnotations(project.CompactTextAnnotations, project.CompactArrowAnnotations);

            // Wire analysis over the freshly populated Compact surface, then restore segments.
            EnsureCompactAnalysisSource();
            NotifyAnalysisCurvesChanged();
            _analysisService.RestoreSegments(project.AnalysisSegments, project.ActiveSegmentId);
            _segmentCounter = Math.Max(_segmentCounter, HighestSegmentSuffix(project.AnalysisSegments));
            RestoreEnabledMetrics(project);
        }
        else if (PlotMode == PlotMode.Grouped)
        {
            TearDownCompactAnalysisSource();
            // After TearDownPaneSurface (which re-wires Stacked analysis via InitializeDefaultPane),
            // not before — see the Compact branch comment.
            TearDownPaneSurface();
            TearDownStackedAnalysisSource();
            project.GroupedPlot ??= new GroupedPlotConfig();
            GroupedPlot.ApplyConfig(project.GroupedPlot);
            GroupedPlot.SetData(_currentData);
            GroupedPlot.RestoreAnnotations(project.GroupedTextAnnotations, project.GroupedArrowAnnotations);
        }
        else
        {
            // A prior Compact project may still own the analysis source — drop it before
            // wiring the Stacked one so we don't leak it or leave _activeAnalysis* dangling.
            TearDownCompactAnalysisSource();

            await _stateManager.RestoreProjectState(
                _currentProject,
                _currentData,
                Panes,
                ActiveCurves,
                RestoreGlobalEventLines,
                RestoreCallouts,
                RestoreTextAnnotations,
                RestoreArrowAnnotations);

            ApplySmartDefaultsAfterLoad();

            // Wire (or rewire) analysis over the freshly restored panes + curves.
            EnsureStackedAnalysisSource();
            NotifyAnalysisCurvesChanged();

            // Restore user-defined analysis segments + the active selection.
            _analysisService.RestoreSegments(project.AnalysisSegments, project.ActiveSegmentId);
            // Keep the auto-name counter ahead of any restored "Segment N" names so new drags
            // don't collide with loaded ones. Seed from the highest numeric suffix actually in
            // use, not the count — a project whose only segment is "Segment 5" must not let the
            // next drag walk back up to "Segment 5".
            _segmentCounter = Math.Max(_segmentCounter, HighestSegmentSuffix(project.AnalysisSegments));
            RestoreEnabledMetrics(project);
        }

        HasData = _currentData != null && _currentData.RowCount > 0;
        HasUnsavedChanges = legacyModeUpgrade; // ask user to save the upgraded PlotMode field
        BottomPaneCollapsed = project.BottomPaneCollapsed;
        UpdateWindowTitle();
        NotifyCommandsCanExecuteChanged();
        // Prefer the on-disk file name when the project carries the default "Untitled Project"
        // name (we never prompt for a project name today, so the field is rarely meaningful).
        string displayName = !string.IsNullOrEmpty(_currentFilePath)
            ? Path.GetFileNameWithoutExtension(_currentFilePath)
            : _currentProject.ProjectName;
        StatusText = legacyModeUpgrade
            ? $"Loaded project: {displayName} (legacy file — save to keep the chosen plot style)"
            : $"Loaded project: {displayName}";
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        await SaveProjectInternal(_currentFilePath);
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        await SaveProjectInternal(null); // Force save-as dialog
    }

    /// <summary>
    /// Save project to a specific file path (or show save-as dialog if path is null)
    /// </summary>
    private async Task SaveProjectInternal(string? filePath)
    {
        try
        {
            _currentProject ??= new ProjectSettingsModel
            {
                ProjectName = "Untitled Project",
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };

            _currentProject.PlotMode = PlotMode;
            _currentProject.CompactPaneSettings = CompactPlot.Settings;
            _currentProject.BottomPaneCollapsed = BottomPaneCollapsed;
            _currentProject.SelectedXColumn = SelectedXColumn;
            if (IsGroupedMode)
                _currentProject.GroupedPlot = GroupedPlot.BuildConfig();

            // Snapshot per-mode annotations (Compact + Grouped) — the IProjectStateManager
            // surface only knows about Stacked-mode annotations, so we round-trip these inline.
            _currentProject.CompactTextAnnotations = CompactPlot.TextAnnotations.ToList();
            _currentProject.CompactArrowAnnotations = CompactPlot.ArrowAnnotations.ToList();
            _currentProject.GroupedTextAnnotations = GroupedPlot.Annotations?.Texts.ToList() ?? new();
            _currentProject.GroupedArrowAnnotations = GroupedPlot.Annotations?.Arrows.ToList() ?? new();

            // Analysis segments (user-defined only; the visible-window segment is implicit).
            _currentProject.AnalysisSegments = _analysisService.Segments
                .Where(s => s.Source != AnalysisSegmentSource.VisibleWindow)
                .ToList();
            var activeSeg = _analysisService.ActiveSegment;
            _currentProject.ActiveSegmentId = activeSeg.Source == AnalysisSegmentSource.VisibleWindow
                ? null
                : activeSeg.Id;

            // Chosen metric columns (Manage Metrics dialog). Persisted so the panel reopens with
            // the same columns; empty list means "service default" on the next load.
            _currentProject.EnabledMetricIds = _analysisService.EnabledMetricIds.ToList();

            _stateManager.SaveCurrentState(
                _currentProject,
                _currentData,
                Panes,
                ActiveCurves,
                GetGlobalEventLines(),
                GetCalloutModels(),
                GetTextAnnotations(),
                GetArrowAnnotations(),
                CompactPlot.Curves.ToList(),
                CompactPlot.EventLines.ToList());

            StatusText = "Saving project...";

            var savedPath = await _fileOperationsService.SaveProjectAsync(_currentProject, filePath);

            if (savedPath == null)
            {
                StatusText = "Save cancelled.";
                return;
            }

            _currentFilePath = savedPath;
            HasUnsavedChanges = false;
            UpdateWindowTitle();
            _recentFilesService.AddFile(savedPath);
            LoadRecentFiles();

            StatusText = $"Project saved: {Path.GetFileName(savedPath)}";
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error saving project: {ex.Message}", "Save Error");
            StatusText = "Failed to save project.";
        }
    }

    [RelayCommand]
    private async Task ExportImage()
    {
        if (IsCompactMode)
        {
            if (CompactPlot.Curves.Count == 0)
            {
                await _dialogService.ShowInformation("No plot to export.", "Export");
                return;
            }

            var compactSuccess = await _fileOperationsService.ExportCompactPlotAsync(CompactPlot.PlotModel);
            StatusText = compactSuccess ? "Exported Compact Plot Surface to image file" : "Export cancelled.";
            return;
        }

        if (IsGroupedMode)
        {
            if (GroupedPlot.Series.Count == 0)
            {
                await _dialogService.ShowInformation("No plot to export.", "Export");
                return;
            }
            GroupedPlotExportRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (Panes.Count == 0 || Panes.All(p => p.PlotModel == null))
        {
            await _dialogService.ShowInformation("No plot to export.", "Export");
            return;
        }

        var plotModels = Panes
            .Where(p => p.PlotModel != null)
            .Select(p => p.PlotModel!)
            .ToList();

        if (plotModels.Count == 0)
        {
            await _dialogService.ShowInformation("No plots to export.", "Export");
            return;
        }

        var success = await _fileOperationsService.ExportPlotsAsync(plotModels);

        if (success)
        {
            StatusText = $"Exported {plotModels.Count} pane(s) to image file";
        }
        else
        {
            StatusText = "Export cancelled.";
        }
    }

    [RelayCommand]
    private async Task ExportIntersections()
    {
        if (IntersectionData == null || IntersectionData.Rows.Count == 0)
        {
            await _dialogService.ShowInformation("No intersection data to export.", "Export");
            return;
        }

        var success = await _fileOperationsService.ExportIntersectionsAsync(IntersectionData);

        if (success)
        {
            StatusText = "Intersection data exported successfully";
        }
        else
        {
            StatusText = "Export cancelled.";
        }
    }

    /// <summary>
    /// Set once the close has been confirmed (by either the File → Exit command or the
    /// window-close prompt) so the window's OnClosing handler doesn't prompt a second time
    /// when <see cref="IApplicationLifetimeService.Shutdown"/> tears the window down.
    /// </summary>
    public bool CloseConfirmed { get; private set; }

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (!await ConfirmCloseAsync())
            return;

        CloseConfirmed = true;
        _lifetimeService.Shutdown();
    }

    /// <summary>
    /// Runs the unsaved-changes prompt and reports whether the caller may proceed with
    /// closing the application. Returns <c>true</c> when there is nothing to save, the user
    /// discarded their changes, or the save completed; returns <c>false</c> when the user
    /// cancelled or the save did not succeed (in which case <see cref="HasUnsavedChanges"/>
    /// is still set). Shared by the File → Exit command and the window-close handler so
    /// closing via the title-bar / ⌘Q / Alt+F4 can no longer silently discard work.
    /// </summary>
    public async Task<bool> ConfirmCloseAsync()
    {
        if (!HasUnsavedChanges)
            return true;

        var result = await _dialogService.ShowUnsavedChangesDialog();
        if (result == DialogResult.Cancel)
            return false;

        if (result == DialogResult.Yes)
        {
            await SaveProject();
            // SaveProject clears HasUnsavedChanges only on success; if the save was cancelled
            // or failed, the flag is still set — don't proceed with the close and lose work.
            return !HasUnsavedChanges;
        }

        // result == No → discard changes and allow the close.
        return true;
    }

    #endregion

    #region Event Line Commands

    /// <summary>
    /// Add a vertical event line at the center of the current view (legacy single-pane support)
    /// </summary>
    [RelayCommand]
    private async Task AddEventLine()
    {
        if (!HasData || Panes.Count == 0 || Panes[0].PlotModel == null)
        {
            await _dialogService.ShowInformation("Please load data first.", "No Data");
            return;
        }

        // Use the first pane's X-axis to determine center position
        var xAxis = Panes[0].PlotModel!.Axes.Bottom;
        double centerX = (xAxis.Range.Min + xAxis.Range.Max) / 2;

        string label = GenerateEventLineLabel();
        AddGlobalEventLine(centerX, label);
    }

    /// <summary>
    /// Clear all event lines from all panes
    /// </summary>
    [RelayCommand]
    private void ClearEventLines()
    {
        ClearAllGlobalEventLines();
    }

    /// <summary>
    /// Add an event line at the given X position on the Compact Plot Surface. Called from
    /// the surface right-click menu — the X is in data coordinates. No-op if the X is outside
    /// the current axis range or the surface has no curves.
    /// </summary>
    public void AddCompactEventLineAt(double xPosition)
        => AddCompactEventLineAt(xPosition, label: null, color: "#FFB900");

    /// <summary>
    /// Add an event line at the given X position with caller-supplied label + color
    /// (used by the surface dialog so the user names the line up front, mirroring Stacked).
    /// </summary>
    public void AddCompactEventLineAt(double xPosition, string? label, string color)
    {
        if (!IsCompactMode) return;
        if (CompactPlot.Curves.Count == 0) return;
        CompactPlot.AddEventLine(xPosition, label, color);
        HasUnsavedChanges = true;
    }

    /// <summary>Remove a single Compact event line by id.</summary>
    public void RemoveCompactEventLine(Guid id)
    {
        if (!IsCompactMode) return;
        if (CompactPlot.RemoveEventLine(id))
        {
            HasUnsavedChanges = true;
            // An EventLinePair segment may have been anchored to this line — resync so its
            // range drops to a stale fallback instead of pointing at a deleted line.
            _analysisService.SyncEventLinePairRanges();
        }
    }

    /// <summary>Move a Compact event line (drag-to-reposition). Caller clamps to axis range.</summary>
    public void MoveCompactEventLine(Guid id, double newXPosition)
    {
        if (!IsCompactMode) return;
        CompactPlot.MoveEventLine(id, newXPosition);
        HasUnsavedChanges = true;
        // Keep any EventLinePair segment that uses this line tracking its live position.
        _analysisService.SyncEventLinePairRanges();
    }

    [RelayCommand]
    private void ClearCompactEventLines()
    {
        if (!IsCompactMode) return;
        if (CompactPlot.EventLines.Count == 0) return;
        CompactPlot.ClearEventLines();
        HasUnsavedChanges = true;
    }

    #endregion

    #region Pane Management Commands

    [RelayCommand]
    private void AddPane()
    {
        var paneViewModel = _paneCoordinationService.AddPane(Panes, XAxisSynchronized);
        PaneCount = Panes.Count;

        HasUnsavedChanges = true;
        StatusText = $"Added {paneViewModel.PaneModel.Name}";
    }

    [RelayCommand]
    private async Task RemovePane()
    {
        if (!_paneCoordinationService.RemovePane(Panes))
        {
            await _dialogService.ShowInformation("Cannot remove the last pane.", "Remove Pane");
            return;
        }

        PaneCount = Panes.Count;
        HasUnsavedChanges = true;
        StatusText = $"Removed pane. {PaneCount} pane(s) remaining.";
    }

    /// <summary>
    /// Re-index all panes after addition or removal
    /// </summary>
    private void ReindexPanes() => _paneCoordinationService.ReindexPanes(Panes);

    /// <summary>
    /// Update X-axis label visibility - only bottom pane shows labels.
    /// Also updates global event line label visibility.
    /// </summary>
    private void UpdatePaneXAxisLabels() => _paneCoordinationService.UpdatePaneXAxisLabels(Panes);

    #endregion

    #region Curve Management Commands

    [RelayCommand]
    private Task AddCurvesDispatch()
    {
        if (IsCompactMode)
            return AddCompactCurves();

        return _dialogService.ShowInformation(
            "In Stacked mode, right-click the desired pane and choose \"Add Curves...\" — that's the only way to target a specific pane.",
            "Right-click a pane");
    }

    [RelayCommand]
    private Task ManageCurvesDispatch()
    {
        if (IsCompactMode)
            return ManageCompactCurves();

        return _dialogService.ShowInformation(
            "In Stacked mode, right-click the desired pane and choose \"Manage Curve\" — that's the only way to target a specific pane.",
            "Right-click a pane");
    }

    [RelayCommand]
    private async Task ClearAllCurvesDispatch()
    {
        int count = IsCompactMode ? CompactPlot.Curves.Count : ActiveCurves.Count;
        if (count == 0) return;

        var result = await _dialogService.ShowConfirmation(
            $"Remove all {count} curve{(count == 1 ? "" : "s")}? This cannot be undone.",
            "Clear All Curves");
        if (result != DialogResult.Yes) return;

        if (IsCompactMode)
            ClearCompactCurves();
        else
            ClearAllCurves();
    }

    [RelayCommand]
    private async Task ClearEventLinesDispatch()
    {
        bool hasAny = IsCompactMode
            ? CompactPlot.EventLines.Count > 0
            : Panes.Any(p => p.GetEventLines().Count > 0);
        if (!hasAny) return;

        var result = await _dialogService.ShowConfirmation(
            "Remove all event lines? This cannot be undone.",
            "Clear All Event Lines");
        if (result != DialogResult.Yes) return;

        if (IsCompactMode)
            ClearCompactEventLines();
        else
            ClearEventLines();
    }

    [RelayCommand(CanExecute = nameof(CanAddCurves))]
    private async Task AddCurves()
    {
        if (_currentData == null)
            return;

        if (string.IsNullOrEmpty(SelectedXColumn))
        {
            await _dialogService.ShowInformation(
                "Please select an X-Axis parameter first (see upper right corner).",
                "No X-Axis Selected");
            return;
        }

        await AddCurvesToSpecificPane(0);
    }

    private bool CanAddCurves() => _currentData?.ColumnCount > 1 && !string.IsNullOrEmpty(_currentXColumn) && IsPanesMode;

    /// <summary>
    /// Open the Compact Plot Surface "Add Curves" dialog, configure each picked column with
    /// band/side/style, and append the result to the compact surface.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddCompactCurves))]
    private async Task AddCompactCurves()
    {
        if (_currentData == null) return;

        if (string.IsNullOrEmpty(SelectedXColumn))
        {
            await _dialogService.ShowInformation(
                "Please select an X-Axis parameter first (see upper right corner).",
                "No X-Axis Selected");
            return;
        }

        var added = await _dialogService.ShowAddCompactCurvesAsync(
            _currentData, SelectedXColumn!, CompactPlot.Curves.Count);

        if (added is { Count: > 0 })
        {
            CompactPlot.SetData(_currentData, SelectedXColumn);
            foreach (var curve in added)
                CompactPlot.AddCurve(curve);
            HasUnsavedChanges = true;
            HasData = true;
            StatusText = $"Added {added.Count} curve(s) to compact surface ({CompactPlot.Curves.Count} total)";
            ManageCompactCurvesCommand.NotifyCanExecuteChanged();
            ClearCompactCurvesCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanAddCompactCurves() => _currentData?.ColumnCount > 1
        && !string.IsNullOrEmpty(_currentXColumn)
        && IsCompactMode;

    [RelayCommand(CanExecute = nameof(CanClearCompactCurves))]
    private void ClearCompactCurves()
    {
        if (CompactPlot.Curves.Count == 0) return;
        CompactPlot.Clear();
        HasUnsavedChanges = true;
        StatusText = "Cleared all compact-surface curves";
        ManageCompactCurvesCommand.NotifyCanExecuteChanged();
        ClearCompactCurvesCommand.NotifyCanExecuteChanged();
    }

    private bool CanClearCompactCurves() => IsCompactMode && CompactPlot.Curves.Count > 0;

    /// <summary>
    /// Open the Format Pane dialog for the Compact Plot Surface; on OK, apply the new
    /// settings to <see cref="CompactPlot"/> (which triggers a rebuild) and mark dirty.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanFormatCompactPane))]
    private async Task FormatCompactPane()
    {
        var updated = await _dialogService.ShowFormatCompactPaneAsync(CompactPlot.Settings);
        if (updated is not null)
        {
            CompactPlot.ApplySettings(updated);
            HasUnsavedChanges = true;
            StatusText = "Compact pane settings updated";
        }
    }

    private bool CanFormatCompactPane() => IsCompactMode;

    /// <summary>
    /// Open the Manage Curve dialog for the Compact Plot Surface; on Apply mutate the
    /// selected <see cref="CompactCurveModel"/> in place and rebuild, on Delete remove it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageCompactCurves))]
    private async Task ManageCompactCurves()
    {
        if (CompactPlot.Curves.Count == 0) return;

        var result = await _dialogService.ShowManageCompactCurveAsync(CompactPlot.Curves);
        if (result is null) return;

        var curve = result.Curve;
        if (result.DeleteRequested)
        {
            CompactPlot.RemoveCurve(curve.Id);
            StatusText = $"Removed curve: {curve.DisplayName}";
        }
        else
        {
            CompactPlot.UpdateCurve(curve);
            StatusText = $"Updated curve: {curve.DisplayName}";
        }
        HasUnsavedChanges = true;
        // In-place edits (visibility, rename, unit) mutate a model without raising
        // Curves.CollectionChanged, so the analysis source won't notice — nudge it explicitly.
        NotifyAnalysisCurvesChanged();
        ManageCompactCurvesCommand.NotifyCanExecuteChanged();
        ClearCompactCurvesCommand.NotifyCanExecuteChanged();
    }

    private bool CanManageCompactCurves() => IsCompactMode && CompactPlot.Curves.Count > 0;

    /// <summary>
    /// Open the "Configure Grouped Inputs" wizard. Replaces <see cref="GroupedPlot"/>'s config
    /// with the user's selection and rebinds the loaded data so the sidebar dropdowns refresh.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfigureGroupedInputs))]
    private async Task ConfigureGroupedInputs()
    {
        if (_currentData is null) return;
        var existing = GroupedPlot.BuildConfig();
        var updated = await _dialogService.ShowGroupedInputsPickerAsync(_currentData, existing);
        if (updated is null) return;

        _currentProject!.GroupedPlot = updated;
        GroupedPlot.ApplyConfig(updated);
        GroupedPlot.SetData(_currentData);
        HasUnsavedChanges = true;
        StatusText = $"Configured {updated.Inputs.Count} input(s)";
    }

    private bool CanConfigureGroupedInputs() => IsGroupedMode && _currentData is not null;

    private async Task PromptGroupedInputsAfterImportAsync()
    {
        // Called fire-and-forget (no caller awaits), so swallow-and-log rather than let a
        // throw escape as an unobserved task fault.
        try
        {
            if (_currentData is null) return;
            var updated = await _dialogService.ShowGroupedInputsPickerAsync(_currentData, _currentProject?.GroupedPlot);
            if (updated is null) return;
            _currentProject!.GroupedPlot = updated;
            GroupedPlot.ApplyConfig(updated);
            GroupedPlot.SetData(_currentData);
            HasUnsavedChanges = true;
        }
        catch (Exception ex)
        {
            SafeErrorHandler.LogError(ex, "prompting for grouped inputs after import");
        }
    }

    [RelayCommand(CanExecute = nameof(HasActiveCurves))]
    private void RemoveCurve(CurveConfigurationModel curve)
    {
        if (curve == null)
            return;

        // Remove from the pane it belongs to
        if (curve.PaneIndex >= 0 && curve.PaneIndex < Panes.Count)
        {
            var pane = Panes[curve.PaneIndex];
            pane.RemoveCurve(curve.Id);
        }

        // Remove from active curves list
        ActiveCurves.Remove(curve);

        HasUnsavedChanges = true;
        StatusText = $"Removed curve: {curve.CurveName}";
    }

    private bool HasActiveCurves() => ActiveCurves.Count > 0;

    [RelayCommand]
    private void ClearAllCurves()
    {
        // Clear all panes
        foreach (var pane in Panes)
        {
            pane.Clear();
        }

        // Clear active curves
        ActiveCurves.Clear();

        HasUnsavedChanges = true;
        StatusText = "All curves cleared";
    }

    [RelayCommand(CanExecute = nameof(HasActiveCurves))]
    private async Task ManageCurves()
    {
        var resultVm = await _dialogService.ShowCurveManagerAsync(ActiveCurves);
        if (resultVm is not null)
        {
            // Get curves marked for removal
            var curvesToRemove = resultVm.GetCurvesToRemove();

            // Remove curves from panes and active curves list
            foreach (var curve in curvesToRemove)
            {
                if (curve.PaneIndex >= 0 && curve.PaneIndex < Panes.Count)
                {
                    var pane = Panes[curve.PaneIndex];
                    pane.RemoveCurve(curve.Id);
                }
                ActiveCurves.Remove(curve);
            }

            // Get all modified curves and update their display
            var modifiedCurves = resultVm.GetModifiedCurves();

            foreach (var modifiedCurve in modifiedCurves)
            {
                // Skip curves marked for removal
                if (curvesToRemove.Contains(modifiedCurve))
                    continue;

                // Find the curve in ActiveCurves and update it
                var activeCurve = ActiveCurves.FirstOrDefault(c => c.Id == modifiedCurve.Id);
                if (activeCurve != null && modifiedCurve.PaneIndex >= 0 && modifiedCurve.PaneIndex < Panes.Count)
                {
                    var pane = Panes[modifiedCurve.PaneIndex];

                    // Apply visibility from dialog (idempotent; no toggle).
                    pane.SetCurveVisibility(modifiedCurve.Id, modifiedCurve.IsVisible);

                    // Update format (color, line style, line width, markers)
                    pane.UpdateCurveFormat(modifiedCurve);
                }
            }

            // Format / visibility edits don't change ActiveCurves membership, so nudge the
            // analysis panel to recompute (e.g. a curve toggled visible/hidden).
            NotifyAnalysisCurvesChanged();

            HasUnsavedChanges = true;

            int removedCount = curvesToRemove.Count;
            if (removedCount > 0)
            {
                StatusText = $"Removed {removedCount} curve(s), applied changes";
            }
            else
            {
                StatusText = "Curve changes applied";
            }
        }
    }

    /// <summary>
    /// Helper method to plot a single curve to a specific pane
    /// </summary>
    private void PlotSingleCurveToPane(int targetPaneIndex, string parameterName, string yAxisType, string? unitOverride = null)
    {
        _curveCoordinationService.PlotSingleCurveToPane(
            targetPaneIndex, parameterName, yAxisType,
            _currentData!, SelectedXColumn!, Panes, ActiveCurves, _colorPalette, unitOverride);

        HasData = true;
        HasUnsavedChanges = true;
        StatusText = $"Added curve '{parameterName}' to pane {targetPaneIndex + 1}";

        // ScottPlot auto-fits each pane independently on first render. With multi-pane projects
        // that means the just-plotted pane gets the data range while the others stay on their
        // previous (often default 0..1) range — event lines and X ticks end up misaligned until
        // the user pans something. Force an immediate AutoScale on the just-plotted pane so its
        // X-axis Range reflects real data, then push that range out to every other pane.
        if (targetPaneIndex >= 0 && targetPaneIndex < Panes.Count)
        {
            Panes[targetPaneIndex].AutoScale();
            OnPaneXAxisChanged(Panes[targetPaneIndex]);
        }
    }

    /// <summary>
    /// Add curves to a specific pane via dialog (called from context menu or command).
    /// Uses a callback to plot each curve immediately when the user clicks "Plot Curve".
    /// </summary>
    public async Task AddCurvesToSpecificPane(int targetPaneIndex)
    {
        if (_currentData == null || targetPaneIndex < 0 || targetPaneIndex >= Panes.Count)
            return;

        if (string.IsNullOrEmpty(SelectedXColumn))
        {
            await _dialogService.ShowInformation(
                "Please select an X-Axis parameter first (see upper right corner).",
                "No X-Axis Selected");
            return;
        }

        await _dialogService.ShowAddCurvesAsync(
            _currentData.Data,
            SelectedXColumn!,
            targetPaneIndex,
            onCurvePlotted: request =>
            {
                PlotSingleCurveToPane(targetPaneIndex, request.ParameterName, request.YAxis, request.Unit);
            });
    }

    /// <summary>
    /// Remove a specific pane by index (called from pane context menu)
    /// </summary>
    public void RemoveSpecificPane(int paneIndex)
    {
        if (Panes.Count <= 1 || paneIndex < 0 || paneIndex >= Panes.Count)
            return;

        var paneToRemove = Panes[paneIndex];
        paneToRemove.Clear();
        paneToRemove.Dispose();
        Panes.RemoveAt(paneIndex);
        PaneCount = Panes.Count;

        ReindexPanes();
        UpdatePaneXAxisLabels();

        HasUnsavedChanges = true;
        StatusText = $"Removed pane {paneIndex + 1}. {PaneCount} pane(s) remaining.";
    }

    #endregion

    #region Public Methods (called from View)

    public void UpdateMousePosition(double x, double y)
    {
        MousePositionText = $"X: {x:F2}, Y: {y:F2}";
    }

    public void OnEventLineMoved()
    {
        HasUnsavedChanges = true;
        CalculateIntersections();
    }

    /// <summary>
    /// Notify the user when the source-data preview grid had to truncate to fit memory.
    /// Phrasing matches the legacy view-side wording — keep them in sync.
    /// </summary>
    public void ReportSourceGridPreviewTruncated(int displayedRows, int totalRows)
    {
        StatusText = $"Showing first {displayedRows:N0} of {totalRows:N0} rows in preview.";
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Calculate intersections between event lines and curves across all panes
    /// </summary>
    private void CalculateIntersections()
    {
        if (IntersectionData == null || Panes.Count == 0)
            return;

        _intersectionCalculator.CalculateAndPopulateIntersections(Panes, IntersectionData);
        OnPropertyChanged(nameof(IntersectionData));
    }

    /// <summary>
    /// Update window title to reflect current file and unsaved changes
    /// </summary>
    private void UpdateWindowTitle()
    {
        string? fileLabel = null;
        if (_currentFilePath != null)
            fileLabel = Path.GetFileName(_currentFilePath);
        else if (_currentProject is { ProjectName: var name } && name != "Untitled Project")
            fileLabel = name;

        var dirty = HasUnsavedChanges ? "* " : string.Empty;
        WindowTitle = fileLabel is null
            ? $"{dirty}DatPlotX"
            : $"{dirty}{fileLabel} - DatPlotX";
    }

    /// <summary>
    /// Update available X-axis columns from current data and auto-select first if none selected
    /// </summary>
    private void UpdateAvailableXColumns()
    {
        AvailableXColumns.Clear();

        if (_currentData == null)
        {
            SelectedXColumn = null;
            return;
        }

        var numericColumns = _currentData.ColumnNames
            .Where(col => _currentData.Data.Columns[col]!.DataType == typeof(double) ||
                         _currentData.Data.Columns[col]!.DataType == typeof(int))
            .ToList();

        foreach (var column in numericColumns)
        {
            AvailableXColumns.Add(column);
        }

        // Drop a stale selection that no longer exists in the new data.
        if (SelectedXColumn != null && !AvailableXColumns.Contains(SelectedXColumn))
        {
            SelectedXColumn = null;
        }

        // Auto-select first column if none selected
        if (SelectedXColumn == null && AvailableXColumns.Count > 0)
        {
            SelectedXColumn = AvailableXColumns[0];
        }
    }

    /// <summary>
    /// Replot all active curves with the current X-axis parameter
    /// Called when user changes X-axis column selection
    /// </summary>
    private void ReplotAllCurves()
    {
        if (_curveCoordinationService.ReplotAllCurves(_currentData!, SelectedXColumn!, Panes, ActiveCurves))
        {
            StatusText = $"Replotted {ActiveCurves.Count} curve(s) with X-axis: {SelectedXColumn}";
        }
    }

    #region Analysis (Stacked)

    /// <summary>
    /// Build (once) the Stacked-mode analysis source + overlay host over the live panes
    /// and hand the source to the analysis service. Called when Panes mode is entered.
    /// </summary>
    private void EnsureStackedAnalysisSource()
    {
        if (_stackedAnalysisSource is not null) return;

        _stackedAnalysisSource = new StackedAnalysisCurveSource(Panes, ResolveXAxisUnit);
        _stackedAnalysisOverlay = new StackedAnalysisOverlayHost(Panes);
        _analysisService.EventLineResolver = id => _globalEventLineService.GetEventLineById(id)?.XPosition;
        AttachAnalysisSource(_stackedAnalysisSource, _stackedAnalysisOverlay);
    }

    /// <summary>Tear down the Stacked analysis source (e.g. switching away from Panes mode).</summary>
    private void TearDownStackedAnalysisSource()
    {
        if (_stackedAnalysisSource is null) return;
        DetachAnalysisSource();
        _stackedAnalysisSource.Dispose();
        _stackedAnalysisSource = null;
        _stackedAnalysisOverlay = null;
    }

    /// <summary>
    /// Build (once) the Compact-mode analysis source + overlay host over the OxyPlot surface
    /// and hand the source to the analysis service. Called when Compact mode is entered.
    /// Compact event-line positions live on <see cref="CompactPlotViewModel.EventLines"/>, not
    /// the global service, so the resolver reads them there.
    /// </summary>
    private void EnsureCompactAnalysisSource()
    {
        if (_compactAnalysisSource is not null) return;

        _compactAnalysisSource = new CompactAnalysisCurveSource(CompactPlot, () => _currentData);
        _compactAnalysisOverlay = new CompactAnalysisOverlayHost(CompactPlot);
        _analysisService.EventLineResolver = id =>
            CompactPlot.EventLines.FirstOrDefault(e => e.Id == id)?.XPosition;
        AttachAnalysisSource(_compactAnalysisSource, _compactAnalysisOverlay);
    }

    /// <summary>Tear down the Compact analysis source (e.g. switching away from Compact mode).</summary>
    private void TearDownCompactAnalysisSource()
    {
        if (_compactAnalysisSource is null) return;
        DetachAnalysisSource();
        _compactAnalysisSource.Dispose();
        _compactAnalysisSource = null;
        _compactAnalysisOverlay = null;
    }

    /// <summary>
    /// Shared wiring for any mode's analysis adapters: hand the source to the service, subscribe
    /// results, stash the mode-agnostic <see cref="_activeAnalysisSource"/> / <see cref="_activeAnalysisOverlay"/>
    /// pointers, mark analysis available, and draw the active segment band.
    /// </summary>
    private void AttachAnalysisSource(IAnalysisCurveSource source, IAnalysisOverlayHost overlay)
    {
        _activeAnalysisSource = source;
        _activeAnalysisOverlay = overlay;
        _analysisService.SetSource(source);
        _analysisService.ResultsChanged += OnAnalysisResultsChanged;
        IsAnalysisAvailable = true;
        RefreshActiveSegmentBand();
    }

    /// <summary>Shared teardown counterpart to <see cref="AttachAnalysisSource"/>. Does not dispose
    /// the source — the per-mode TearDown owns that, since only it holds the concrete-typed field.</summary>
    private void DetachAnalysisSource()
    {
        _analysisService.ResultsChanged -= OnAnalysisResultsChanged;
        _analysisService.SetSource(null);
        _activeAnalysisOverlay?.ClearHighlights();
        _activeAnalysisOverlay = null;
        _activeAnalysisSource = null;
        IsAnalysisAvailable = false;
        _inlineOverlayCts?.Cancel();
        _inlineOverlayCts?.Dispose();
        _inlineOverlayCts = null;
    }

    private void OnAnalysisResultsChanged(object? sender, EventArgs e)
    {
        RefreshActiveSegmentBand();
        if (ShowInlineMetrics)
            RefreshInlineOverlay();
    }

    // Inline overlay shows at most this many metrics per curve (in enabled order).
    private const int InlineOverlayMaxMetrics = 3;

    /// <summary>
    /// Recompute the active segment and draw a per-pane corner label summarizing the top few
    /// enabled metrics for each visible curve. No-op when the overlay is off or no source.
    /// </summary>
    /// <summary>
    /// Fire-and-forget entry point for the inline overlay refresh. Wraps the async work so an
    /// exception (e.g. a compute failure after the VM is torn down) can't escape as an
    /// unobserved fault and crash the app — the old <c>async void</c> form had no such guard.
    /// </summary>
    private void RefreshInlineOverlay()
    {
        _ = RefreshInlineOverlayGuardedAsync();

        async Task RefreshInlineOverlayGuardedAsync()
        {
            try
            {
                await RefreshInlineOverlayAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                SafeErrorHandler.LogError(ex, "refreshing inline analysis overlay");
            }
        }
    }

    private async Task RefreshInlineOverlayAsync()
    {
        if (_disposed || _stackedAnalysisOverlay is null || !ShowInlineMetrics) return;

        // Supersede any in-flight overlay compute so rapid pan/zoom doesn't stack redraws.
        _inlineOverlayCts?.Cancel();
        _inlineOverlayCts?.Dispose();
        _inlineOverlayCts = new CancellationTokenSource();
        var ct = _inlineOverlayCts.Token;

        IReadOnlyList<StatisticResult> results;
        try
        {
            results = await _analysisService.ComputeActiveAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException) { return; }

        if (_disposed || _stackedAnalysisOverlay is null) return; // disposed mid-compute

        if (ct.IsCancellationRequested || !ShowInlineMetrics) return; // superseded / toggled off

        var registry = _analysisService.EnabledMetricIds;
        // curveId → "DisplayName  max=.. min=.." using the first N enabled metrics.
        var byCurve = results
            .GroupBy(r => r.CurveId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var paneText = new Dictionary<PlotPaneViewModel, string>();
        foreach (var pane in Panes)
        {
            var lines = new List<string>();
            foreach (var info in pane.GetPlottedCurves())
            {
                if (!info.Config.IsVisible) continue;
                var id = info.Config.Id.ToString();
                if (!byCurve.TryGetValue(id, out var rows)) continue;

                var name = string.IsNullOrEmpty(info.Config.CurveName)
                    ? info.Config.YColumnName : info.Config.CurveName;

                var parts = new List<string>();
                foreach (var metricId in registry.Take(InlineOverlayMaxMetrics))
                {
                    var r = rows.FirstOrDefault(x => x.MetricId == metricId);
                    if (r is null || double.IsNaN(r.Value)) continue;
                    parts.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                        $"{metricId}={r.Value:0.###}"));
                }
                if (parts.Count > 0)
                    lines.Add($"{name}: {string.Join("  ", parts)}");
            }
            if (lines.Count > 0)
                paneText[pane] = string.Join("\n", lines);
        }

        _stackedAnalysisOverlay.ShowInlineLabels(paneText);
    }

    /// <summary>
    /// Draw the persistent band for the active segment on every pane, or clear it when the
    /// active segment is the visible window (banding the whole view is just noise).
    /// </summary>
    private void RefreshActiveSegmentBand()
    {
        if (_activeAnalysisOverlay is null) return;

        var seg = _analysisService.ActiveSegment;
        var range = _analysisService.ActiveSegmentRange;

        // No band for the visible window (it's the whole view) or an unresolvable range.
        if (seg.Source == AnalysisSegmentSource.VisibleWindow
            || range is not { } r
            || !double.IsFinite(r.XMin) || !double.IsFinite(r.XMax) || r.XMax <= r.XMin)
        {
            _activeAnalysisOverlay.ClearHighlights();
            return;
        }

        _activeAnalysisOverlay.HighlightSegment(r.XMin, r.XMax, seg.Name);
    }

    /// <summary>Best-effort X-axis unit from the selected X column header (e.g. "Time [s]" → "s").</summary>
    private string? ResolveXAxisUnit()
    {
        if (string.IsNullOrEmpty(_currentXColumn)) return null;
        return UnitHeaderParser.Parse(_currentXColumn).Unit;
    }

    private void OnActiveCurvesChangedForAnalysis(object? sender, NotifyCollectionChangedEventArgs e)
        => NotifyAnalysisCurvesChanged();

    private void OnCompactCurvesChangedForCommands(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ClearCompactCurvesCommand.NotifyCanExecuteChanged();
        ManageCompactCurvesCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Notify the analysis service that the curve set changed (add / remove / clear / visibility).
    /// Routes to whichever mode's source is active (Stacked / Compact).</summary>
    public void NotifyAnalysisCurvesChanged() => _activeAnalysisSource?.NotifyCurvesChanged();

    // Pan/zoom raises the axis-changed event once per frame. Forwarding each tick straight to the
    // analysis service drives a full snapshot+slice+metric recompute (and panel/overlay redraw) per
    // frame, which stutters on large datasets. Coalesce: restart a short timer on every tick and
    // only fire the actual recompute once the axis has settled.
    private Avalonia.Threading.DispatcherTimer? _visibleRangeDebounce;

    /// <summary>Notify the analysis service that the visible X-window changed (pan / zoom),
    /// debounced so a continuous drag triggers a single recompute when it settles.</summary>
    public void NotifyAnalysisVisibleRangeChanged()
    {
        if (_activeAnalysisSource is null) return;

        if (_visibleRangeDebounce is null)
        {
            _visibleRangeDebounce = new Avalonia.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80),
            };
            // Named handler (not a lambda) so Dispose can -= it alongside Stop()/null (review D5).
            _visibleRangeDebounce.Tick += OnVisibleRangeDebounceTick;
        }

        // Restart the quiet-period countdown on every tick of the drag.
        _visibleRangeDebounce.Stop();
        _visibleRangeDebounce.Start();
    }

    private void OnVisibleRangeDebounceTick(object? sender, EventArgs e)
    {
        _visibleRangeDebounce!.Stop();
        _activeAnalysisSource?.NotifyVisibleRangeChanged();
    }

    private void OnAnalysisPointFlashRequested(object? sender, AnalysisPointFlashRequest e)
        => _activeAnalysisOverlay?.HighlightPoint(e.CurveId, e.X, e.Y, e.Label);

    /// <summary>
    /// User clicked the place-event-line button on a min/max cell. Prompt (Add Event Line dialog,
    /// pre-filled with X and the standard auto label) then drop a persisted event line at the
    /// metric point's X in whichever mode is active. Min/max points aren't saved in the .DPX, but
    /// event lines are — this pins the found extremum so it survives save/reload.
    /// </summary>
    private void OnAnalysisPlaceEventLineRequested(object? sender, AnalysisPlaceEventLineRequest e)
    {
        // Dialog is async; fire-and-forget on the UI thread (matches OnCompactBandLimitExceeded).
        PostToUiThreadGuarded("placing event line from analysis", async () =>
        {
            if (IsCompactMode)
            {
                if (CompactPlot.Curves.Count == 0) return;
                var suggested = CompactPlot.GenerateEventLineLabel();
                var accepted = await _dialogService.ShowAddEventLineAsync(e.X, suggested);
                if (accepted is not null)
                    AddCompactEventLineAt(e.X, accepted.LabelText, accepted.ColorHex);
            }
            else if (IsPanesMode)
            {
                if (Panes.Count == 0) return;
                var suggested = GenerateEventLineLabel();
                var accepted = await _dialogService.ShowAddEventLineAsync(e.X, suggested);
                if (accepted is not null)
                    AddGlobalEventLine(e.X, accepted.LabelText, accepted.ColorHex);
            }
        });
    }

    /// <summary>
    /// Post an async action to the UI thread as fire-and-forget, but wrap it so a throw can't
    /// escape as an unobserved task fault (which surfaces as a crash dump). For dialog-driven
    /// handlers that have no caller to await them.
    /// </summary>
    private static void PostToUiThreadGuarded(string operation, Func<Task> action)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                SafeErrorHandler.LogError(ex, operation);
            }
        });
    }

    // Suppress repeat warnings: Rebuild() fires on every Compact mutation, so warn once per distinct
    // (shown, total) clamp rather than on every pan/format/rebuild that re-trips the same limit.
    private (int Shown, int Total)? _lastBandLimitWarning;

    private void OnCompactBandLimitExceeded(object? sender, (int Shown, int Total) e)
    {
        if (_lastBandLimitWarning == e) return;
        _lastBandLimitWarning = e;

        // Fire-and-forget the modal warning on the UI thread; don't block the rebuild.
        PostToUiThreadGuarded("warning about compact band limit", () =>
            _dialogService.ShowWarning(
                $"The Compact plot shows at most {e.Shown} curves at once; you have {e.Total}. " +
                $"Showing the first {e.Shown}. Hide some curves (Manage Curves…) to choose which are drawn.",
                "Too Many Curves"));
    }

    // (curveId, metricId) pairs the user has toggled "show on plot". Session-only (not persisted).
    // Re-drawn on every results change so pan/zoom and segment switches keep them current.
    private readonly HashSet<(string CurveId, string MetricId)> _enabledStatLines = new();

    // Stat lines (slope / mean / min / max) draw in opaque black so they stand out from their own
    // curve, which is the same color. Bands keep the curve color (set by their own callers).
    // NOTE: must be a 6-digit #RRGGBB — ScottPlot's Color.FromHex reads 8-digit hex as #RRGGBBAA
    // (not #AARRGGBB), so an alpha-prefixed value like "#CC000000" parses to a transparent colour
    // and the line/label vanish. Keep it opaque 6-digit to stay correct in both ScottPlot and OxyPlot.
    private const string StatLineColorHex = "#000000";

    private static string StatLineId(string curveId, string metricId) => $"{curveId}:{metricId}";

    private void OnAnalysisLineToggleRequested(object? sender, AnalysisLineToggleRequest e)
    {
        if (_activeAnalysisOverlay is null) return;

        var key = (e.CurveId, e.MetricId);
        var lineId = StatLineId(e.CurveId, e.MetricId);

        if (e.Show)
        {
            _enabledStatLines.Add(key);
            var (xMin, xMax) = ActiveSegmentLineSpan();
            _activeAnalysisOverlay.DrawSegmentLine(e.CurveId, lineId, e.Line, xMin, xMax, StatLineColorHex, e.Label);
        }
        else
        {
            _enabledStatLines.Remove(key);
            _activeAnalysisOverlay.ClearSegmentLine(lineId);
        }
    }

    /// <summary>Re-draw every enabled stat line from the panel's freshly-computed cells. Runs after
    /// each recompute (pan/zoom, segment switch, curve edit), so the lines track the data. Drops
    /// toggles whose curve/metric/line no longer exists.</summary>
    private void OnAnalysisTableInvalidated(object? sender, EventArgs e)
    {
        if (_activeAnalysisOverlay is null || _enabledStatLines.Count == 0) return;

        var (xMin, xMax) = ActiveSegmentLineSpan();

        foreach (var key in _enabledStatLines.ToList())
        {
            var lineId = StatLineId(key.CurveId, key.MetricId);
            var row = AnalysisPanel.Rows.FirstOrDefault(r => string.Equals(r.CurveId, key.CurveId, StringComparison.Ordinal));
            if (row is null || !row.Cells.TryGetValue(key.MetricId, out var cell) || cell.Line is null || !cell.CanShowLine)
            {
                // Curve/metric gone or no longer line-drawable — drop the toggle and clear any stale line.
                _activeAnalysisOverlay.ClearSegmentLine(lineId);
                _enabledStatLines.Remove(key);
                continue;
            }

            var label = AnalysisPanelViewModel.BuildLineLabel(MetricDisplayName(key.MetricId), cell, row.Unit);
            _activeAnalysisOverlay.DrawSegmentLine(key.CurveId, lineId, cell.Line, xMin, xMax, StatLineColorHex, label);
        }
    }

    // Tolerance bands the user has drawn (curveId set). Re-drawn on every BandsInvalidated so
    // pan/zoom and segment/scope changes keep the band + limit lines current.
    private readonly HashSet<string> _bandedCurves = new(StringComparer.Ordinal);

    private static string BandLineId(string curveId, string part) => $"band:{curveId}:{part}";

    /// <summary>Re-draw every tolerance band's center + limit lines from the panel's freshly-computed
    /// band rows. Lines reuse the stat-line overlay path (three horizontal lines per band). Drops the
    /// drawn lines for any curve whose band disappeared.</summary>
    private void OnAnalysisBandsInvalidated(object? sender, EventArgs e)
    {
        if (_activeAnalysisOverlay is null) return;

        var live = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in AnalysisPanel.BandRows)
        {
            var r = b.Result;
            if (!double.IsFinite(r.Lower) || !double.IsFinite(r.Upper)) continue;
            if (!double.IsFinite(b.XMin) || !double.IsFinite(b.XMax) || b.XMax <= b.XMin) continue;

            live.Add(b.CurveId);
            DrawBandLine(b.CurveId, "center", r.Center, b.XMin, b.XMax, b.ColorHex, "center");
            DrawBandLine(b.CurveId, "upper", r.Upper, b.XMin, b.XMax, b.ColorHex, "upper");
            DrawBandLine(b.CurveId, "lower", r.Lower, b.XMin, b.XMax, b.ColorHex, "lower");
        }

        // Clear lines for any previously-banded curve no longer present.
        foreach (var gone in _bandedCurves.Where(c => !live.Contains(c)).ToList())
            ClearBandLines(gone);
        _bandedCurves.Clear();
        foreach (var c in live) _bandedCurves.Add(c);
    }

    private void DrawBandLine(string curveId, string part, double y, double xMin, double xMax, string colorHex, string label)
    {
        var line = Models.Analysis.MetricLine.Horizontal(y);
        _activeAnalysisOverlay!.DrawSegmentLine(curveId, BandLineId(curveId, part), line, xMin, xMax, colorHex, label);
    }

    private void ClearBandLines(string curveId)
    {
        _activeAnalysisOverlay?.ClearSegmentLine(BandLineId(curveId, "center"));
        _activeAnalysisOverlay?.ClearSegmentLine(BandLineId(curveId, "upper"));
        _activeAnalysisOverlay?.ClearSegmentLine(BandLineId(curveId, "lower"));
    }

    private void OnAnalysisBandRemoveRequested(object? sender, string curveId)
    {
        ClearBandLines(curveId);
        _bandedCurves.Remove(curveId);
    }

    private async void OnAnalysisCsvExportRequested(object? sender, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        try
        {
            await _fileOperationsService.ExportAnalysisResultsAsync(rows, "AnalysisResults");
        }
        catch (Exception ex)
        {
            await _dialogService.ShowError($"Error exporting results: {ex.Message}", "Export Error");
        }
    }

    private string MetricDisplayName(string metricId) =>
        AnalysisPanel.Columns.FirstOrDefault(c => string.Equals(c.MetricId, metricId, StringComparison.Ordinal))?.DisplayName ?? metricId;

    /// <summary>X span for a horizontal stat line. Uses the active segment's resolved range; falls
    /// back to the visible window when the segment is the visible-window pseudo-segment.</summary>
    private (double XMin, double XMax) ActiveSegmentLineSpan()
    {
        var range = _analysisService.ActiveSegmentRange;
        if (range is { } r && double.IsFinite(r.XMin) && double.IsFinite(r.XMax) && r.XMax > r.XMin)
            return (r.XMin, r.XMax);
        return (double.NaN, double.NaN);
    }

    /// <summary>
    /// Toggle the Analysis Results panel. Driven by a command (not a two-way IsChecked binding)
    /// because Avalonia's checkable MenuItem doesn't reliably write IsChecked back to the VM —
    /// same pattern as Show Hover Tooltips.
    /// </summary>
    [RelayCommand]
    private void ToggleAnalysisPanel() => ShowAnalysisPanel = !ShowAnalysisPanel;

    private int _segmentCounter;

    /// <summary>
    /// Next auto-name for a new analysis segment. When the user has cleared every segment
    /// (only the implicit "Visible window" entry is left), numbering restarts at 1 instead of
    /// climbing forever.
    /// </summary>
    private string NextSegmentName()
    {
        bool anyUserSegments = _analysisService.Segments
            .Any(s => s.Source != AnalysisSegmentSource.VisibleWindow);
        if (!anyUserSegments) _segmentCounter = 0;
        return $"Segment {++_segmentCounter}";
    }

    /// <summary>Highest N across any segment named "Segment N" (0 if none), used to seed the
    /// auto-name counter on load so freshly dragged segments never collide with restored ones.</summary>
    private static int HighestSegmentSuffix(IEnumerable<Models.Analysis.AnalysisSegment> segments)
    {
        int max = 0;
        foreach (var seg in segments)
        {
            var name = seg.Name;
            if (name is null || !name.StartsWith("Segment ", StringComparison.Ordinal)) continue;
            if (int.TryParse(name.AsSpan("Segment ".Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var n)
                && n > max)
                max = n;
        }
        return max;
    }

    /// <summary>Apply a loaded project's saved metric-column choice to the analysis service. A
    /// missing / empty list (older files, or projects that never opened the picker) leaves the
    /// service's default columns in place. Unknown ids are ignored by <c>SetEnabledMetrics</c>.</summary>
    private void RestoreEnabledMetrics(ProjectSettingsModel project)
    {
        if (project.EnabledMetricIds is { Count: > 0 } ids)
            _analysisService.SetEnabledMetrics(ids);
    }

    // First event line picked for an EventLinePair segment; the second pick completes it.
    private Guid? _pendingSegmentBoundaryEventLineId;

    /// <summary>
    /// Handle the "Use as Segment Boundary" event-line action. The first pick is remembered;
    /// the second pick (a different line) creates an <see cref="AnalysisSegmentSource.EventLinePair"/>
    /// segment between the two and makes it active. Picking the same line twice cancels.
    /// </summary>
    public void PickEventLineSegmentBoundary(Guid eventLineId)
    {
        if (_pendingSegmentBoundaryEventLineId is null)
        {
            _pendingSegmentBoundaryEventLineId = eventLineId;
            var line = _globalEventLineService.GetEventLineById(eventLineId);
            StatusText = $"Segment boundary 1 set ({line?.Label ?? "event line"}). Right-click a second event line to finish.";
            return;
        }

        var startId = _pendingSegmentBoundaryEventLineId.Value;
        _pendingSegmentBoundaryEventLineId = null;

        if (startId == eventLineId)
        {
            StatusText = "Segment boundary cancelled (same event line picked twice).";
            return;
        }

        var start = _globalEventLineService.GetEventLineById(startId);
        var end = _globalEventLineService.GetEventLineById(eventLineId);
        if (start is null || end is null)
        {
            StatusText = "Segment boundary failed: an event line no longer exists.";
            return;
        }

        double xMin = Math.Min(start.XPosition, end.XPosition);
        double xMax = Math.Max(start.XPosition, end.XPosition);

        var segment = new AnalysisSegment(
            Guid.NewGuid(),
            NextSegmentName(),
            xMin, xMax,
            AnalysisSegmentSource.EventLinePair,
            StartEventId: startId,
            EndEventId: eventLineId);

        _analysisService.DefineSegment(segment);
        _analysisService.SetActiveSegment(segment.Id);
        ShowAnalysisPanel = true;
        StatusText = $"Created '{segment.Name}' between two event lines.";
    }

    /// <summary>
    /// Define a new manual analysis segment over [<paramref name="xMin"/>, <paramref name="xMax"/>]
    /// (from a Shift+drag on a pane), make it the active segment, and show the panel so the
    /// user sees the result immediately.
    /// </summary>
    public void DefineAnalysisSegment(double xMin, double xMax)
    {
        if (!double.IsFinite(xMin) || !double.IsFinite(xMax) || xMax <= xMin)
            return;

        var segment = new AnalysisSegment(
            Guid.NewGuid(),
            NextSegmentName(),
            xMin, xMax,
            AnalysisSegmentSource.Manual);

        _analysisService.DefineSegment(segment);
        _analysisService.SetActiveSegment(segment.Id);
        ShowAnalysisPanel = true;
    }

    private bool CanManageAnalysisSegments() => IsAnalysisAvailable;

    /// <summary>
    /// Open the Manage Segments dialog and apply the result: delete marked segments, rename
    /// changed ones, and set the chosen active segment. Reconciled against the live service
    /// only on Apply, so Cancel is a no-op.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageAnalysisSegments))]
    private async Task ManageAnalysisSegments()
    {
        var result = await _dialogService.ShowManageSegmentsAsync(
            _analysisService.Segments, _analysisService.ActiveSegment.Id);
        if (result is null) return;

        // Deletions first so a renamed/active row can't collide with a removed one.
        foreach (var row in result.ToRemove)
            _analysisService.RemoveSegment(row.Id);

        // Renames: DefineSegment replaces the segment with the matching Id in place.
        foreach (var row in result.Renamed)
        {
            var existing = _analysisService.Segments.FirstOrDefault(s => s.Id == row.Id);
            if (existing is not null)
                _analysisService.DefineSegment(existing with { Name = row.Name });
        }

        if (result.ActiveId is { } activeId)
            _analysisService.SetActiveSegment(activeId);

        ShowAnalysisPanel = true;
    }

    /// <summary>
    /// Open the Manage Metrics dialog and apply the chosen column set + order to the analysis
    /// service. The new selection is persisted with the project, so applying it marks the
    /// project dirty. Cancel is a no-op.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageAnalysisSegments))]
    private async Task ManageMetrics()
    {
        var result = await _dialogService.ShowManageMetricsAsync(
            _analysisService.AllMetrics, _analysisService.EnabledMetricIds);
        if (result is null) return;

        var chosen = result.EnabledIds;
        if (chosen.Count == 0) return;   // dialog guards this, but never blank the panel

        _analysisService.SetEnabledMetrics(chosen);
        HasUnsavedChanges = true;
        ShowAnalysisPanel = true;
    }

    /// <summary>
    /// Open the Tolerance Band dialog for the current curves and attach (or replace) the band the
    /// user configures. The band metrics appear in the panel's "Tolerance Bands" section; the band
    /// lines are drawn on the active scope via <see cref="OnAnalysisTableInvalidated"/>.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanManageAnalysisSegments))]
    private async Task ManageToleranceBand()
    {
        var curves = _analysisService.ListCurves()
            .Where(c => c.IsVisible)
            .Select(c => new Views.ToleranceBandCurveChoice(c.CurveId, c.DisplayName, c.Unit))
            .ToList();
        if (curves.Count == 0)
        {
            await _dialogService.ShowInformation("Add a visible curve before defining a tolerance band.", "Tolerance Band");
            return;
        }

        // Pre-populate with the first curve's existing band if it has one (single-band-per-curve UX).
        var existing = curves
            .Select(c => _analysisService.GetToleranceBand(c.CurveId))
            .FirstOrDefault(b => b is not null);

        var band = await _dialogService.ShowToleranceBandAsync(curves, ResolveBandPreview, existing);
        if (band is null) return;

        _analysisService.SetToleranceBand(band);
        ShowAnalysisPanel = true;
    }

    /// <summary>
    /// Resolve a candidate band's concrete (center, lower, upper) for the dialog's live preview by
    /// deriving the center against the curve's real data over the band's scope. Returns null when
    /// the curve has no data (the dialog falls back to a user-nominal-only preview).
    /// </summary>
    private (double Center, double Lower, double Upper)? ResolveBandPreview(Models.Analysis.ToleranceBand band)
    {
        if (_activeAnalysisSource is not { } source) return null;
        var data = source.GetData(band.CurveId);
        if (data is null) return null;

        var (xMin, xMax) = band.Scope == Models.Analysis.BandScope.WholeCurve
            ? source.FullDataXRange
            : ActiveSegmentLineSpan();
        if (!double.IsFinite(xMin) || !double.IsFinite(xMax)) return null;

        var (start, end) = data.SliceIndices(xMin, xMax);
        if (start > end) return band.ResolveLimits(double.NaN);

        var (_, ys) = data.Slice(start, end);
        double derived = Services.Analysis.ToleranceBandEvaluator.DeriveCenter(band.CenterMode, ys);
        return band.ResolveLimits(derived);
    }

    #endregion

    /// <summary>
    /// Handle X-axis change from a pane and propagate to others when synchronization is enabled
    /// </summary>
    public void OnPaneXAxisChanged(PlotPaneViewModel sourcePane)
    {
        if (!XAxisSynchronized || Panes.Count <= 1)
            return;

        var xRange = sourcePane.GetXAxisRange();
        if (!xRange.HasValue)
            return;

        foreach (var pane in Panes.Where(p => p != sourcePane))
        {
            pane.SetXAxisRange(xRange.Value.Min, xRange.Value.Max);
        }
    }

    /// <summary>
    /// Callback invoked when a large file is about to be imported
    /// Shows a warning dialog and allows user to proceed or cancel
    /// </summary>
    private async Task<bool> OnLargeFileWarning(double fileSizeMB, string message)
    {
        var result = await _dialogService.ShowConfirmation(
            message,
            $"Large File Warning ({fileSizeMB:F1} MB)").ConfigureAwait(false);

        return result == DialogResult.Yes;
    }

    #endregion

    #region Global Event Line Methods

    /// <summary>
    /// Add a global event line that appears across all panes with intersection callouts
    /// </summary>
    /// <param name="xPosition">X-axis position for the event line</param>
    /// <param name="label">Display label for the event line</param>
    public void AddGlobalEventLine(double xPosition, string label, string color = "#FFB900")
    {
        // Add the global event line to all panes
        var eventLineId = _globalEventLineService.AddGlobalEventLine(xPosition, label, Panes, color);

        // Create callout annotations at all curve intersections
        _calloutAnnotationService.CreateCalloutsForEventLine(eventLineId, xPosition, Panes);

        // Update event line count
        EventLineCount = _globalEventLineService.Count;

        // Calculate intersections for data table
        CalculateIntersections();

        // Adding a VerticalLine + Text label plottable can cause one pane's ScottPlot layout
        // to recompute independently — without an explicit re-sync, the top pane sometimes
        // ends up with a slightly different X-range/layout than its siblings until the user
        // pans. Force a sync using pane 0 as the source. Same shape as the v0.12.1 first-
        // plot fix.
        if (Panes.Count > 1 && XAxisSynchronized)
            OnPaneXAxisChanged(Panes[0]);

        HasUnsavedChanges = true;
        StatusText = $"Added global event line '{label}' at X={xPosition:F3}";
    }

    /// <summary>
    /// Remove a global event line and its callouts from all panes
    /// </summary>
    /// <param name="eventLineId">ID of the event line to remove</param>
    public void RemoveGlobalEventLine(Guid eventLineId)
    {
        // Remove callouts first
        _calloutAnnotationService.RemoveCalloutsForEventLine(eventLineId, Panes);

        // Remove the event line
        var eventLine = _globalEventLineService.GetEventLineById(eventLineId);
        string label = eventLine?.Label ?? "Unknown";

        _globalEventLineService.RemoveGlobalEventLine(eventLineId, Panes);

        // Update event line count
        EventLineCount = _globalEventLineService.Count;

        // Recalculate intersections
        CalculateIntersections();

        HasUnsavedChanges = true;
        StatusText = $"Removed global event line '{label}'";
    }

    /// <summary>
    /// Handle when a global event line is moved/dragged to a new position
    /// </summary>
    /// <param name="eventLineId">ID of the event line that was moved</param>
    /// <param name="newXPosition">New X position</param>
    public void OnGlobalEventLineMoved(Guid eventLineId, double newXPosition)
    {
        // Move the event line on all panes
        _globalEventLineService.MoveGlobalEventLine(eventLineId, newXPosition, Panes);

        // Update callout positions and values
        _calloutAnnotationService.UpdateCalloutsForEventLine(eventLineId, newXPosition, Panes);

        // Recalculate intersections for data table
        CalculateIntersections();

        // EventLinePair segments track live line positions. Sync the stored range of EVERY such
        // segment (not just the active one) so a non-active segment's persisted [XMin, XMax]
        // doesn't drift from its boundary lines and get saved stale. This raises ResultsChanged
        // when anything moved, which redraws the active segment's band.
        _analysisService.SyncEventLinePairRanges();

        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Update a callout's offset after user drag
    /// </summary>
    /// <param name="calloutId">ID of the callout</param>
    /// <param name="offsetX">New X offset</param>
    /// <param name="offsetY">New Y offset</param>
    public void UpdateCalloutOffset(Guid calloutId, double offsetX, double offsetY)
    {
        _calloutAnnotationService.UpdateCalloutOffset(calloutId, offsetX, offsetY);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Re-clamp callout label positions on all panes to their current axis ranges.
    /// Called by MainWindow after a zoom/pan changes any pane's X-axis range so
    /// callouts stay in the visible viewport.
    /// </summary>
    public void ReclampCalloutsForAllPanes()
    {
        foreach (var pane in Panes)
            _calloutAnnotationService.ReclampCalloutsForViewportChange(pane);
    }

    /// <summary>
    /// Clear all global event lines and their callouts
    /// </summary>
    public void ClearAllGlobalEventLines()
    {
        _calloutAnnotationService.ClearAllCallouts(Panes);
        _globalEventLineService.ClearAllGlobalEventLines(Panes);

        EventLineCount = 0;
        CalculateIntersections();

        HasUnsavedChanges = true;
        StatusText = "Cleared all event lines";
    }

    /// <summary>
    /// Generate a default label for a new event line
    /// </summary>
    public string GenerateEventLineLabel() => _globalEventLineService.GenerateDefaultLabel();

    /// <summary>
    /// Get all global event lines
    /// </summary>
    public IReadOnlyList<EventLineModel> GetGlobalEventLines()
    {
        return _globalEventLineService.GetGlobalEventLines();
    }

    /// <summary>
    /// Get a global event line by ID
    /// </summary>
    public EventLineModel? GetGlobalEventLineById(Guid eventLineId)
    {
        return _globalEventLineService.GetEventLineById(eventLineId);
    }

    /// <summary>
    /// Get all callout models for persistence
    /// </summary>
    public IReadOnlyList<IntersectionCalloutModel> GetCalloutModels()
    {
        return _calloutAnnotationService.GetCalloutModels();
    }

    /// <summary>
    /// Restore global event lines from saved project
    /// </summary>
    public void RestoreGlobalEventLines(IEnumerable<EventLineModel> eventLines)
    {
        _globalEventLineService.RestoreGlobalEventLines(eventLines, Panes);
        EventLineCount = _globalEventLineService.Count;
    }

    /// <summary>
    /// Restore callouts from saved project
    /// </summary>
    public void RestoreCallouts(IEnumerable<IntersectionCalloutModel> callouts)
    {
        _calloutAnnotationService.RestoreCallouts(callouts, Panes);
    }

    #endregion

    /// <summary>
    /// Show the text-annotation dialog. Exposed on the VM so views (CompactPlotControl,
    /// GroupedPlotControl) can launch it without taking a direct dependency on IDialogService.
    /// </summary>
    public Task<TextAnnotationModel?> ShowTextAnnotationDialogAsync(TextAnnotationModel seed)
        => _dialogService.ShowTextAnnotationDialogAsync(seed);

    /// <summary>
    /// Show the arrow-annotation dialog. See <see cref="ShowTextAnnotationDialogAsync"/>.
    /// </summary>
    public Task<ArrowAnnotationModel?> ShowArrowAnnotationDialogAsync(ArrowAnnotationModel seed)
        => _dialogService.ShowArrowAnnotationDialogAsync(seed);

    #region Text Annotation Methods

    /// <summary>
    /// Add a text annotation to a pane
    /// </summary>
    public void AddTextAnnotation(TextAnnotationModel model)
    {
        _textAnnotationService.AddAnnotation(model, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Update a text annotation's position after drag
    /// </summary>
    public void UpdateTextAnnotationPosition(Guid annotationId, double newX, double newY)
    {
        _textAnnotationService.UpdatePosition(annotationId, newX, newY, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Update a text annotation's properties
    /// </summary>
    public void UpdateTextAnnotation(TextAnnotationModel model)
    {
        _textAnnotationService.UpdateAnnotation(model, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Remove a text annotation
    /// </summary>
    public void RemoveTextAnnotation(Guid annotationId)
    {
        _textAnnotationService.RemoveAnnotation(annotationId, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Get a specific text annotation by ID
    /// </summary>
    public TextAnnotationModel? GetTextAnnotation(Guid annotationId)
    {
        return _textAnnotationService.GetAnnotation(annotationId);
    }

    /// <summary>
    /// Get all text annotations for persistence
    /// </summary>
    public IReadOnlyList<TextAnnotationModel> GetTextAnnotations()
    {
        return _textAnnotationService.GetAllAnnotations();
    }

    /// <summary>
    /// Restore text annotations from saved project
    /// </summary>
    public void RestoreTextAnnotations(IEnumerable<TextAnnotationModel> annotations)
    {
        _textAnnotationService.RestoreAnnotations(annotations, Panes);
    }

    /// <summary>
    /// Clear all text annotations
    /// </summary>
    public void ClearAllTextAnnotations()
    {
        _textAnnotationService.ClearAllAnnotations(Panes);
        HasUnsavedChanges = true;
    }

    #endregion

    #region Arrow Annotation Methods

    /// <summary>
    /// Add an arrow annotation to a pane
    /// </summary>
    public void AddArrowAnnotation(ArrowAnnotationModel model)
    {
        _arrowAnnotationService.AddAnnotation(model, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Update an arrow annotation's position after drag
    /// </summary>
    public void UpdateArrowAnnotationPosition(Guid annotationId, double baseX, double baseY, double tipX, double tipY)
    {
        var model = _arrowAnnotationService.GetAnnotation(annotationId);
        if (model != null)
        {
            model.BaseX = baseX;
            model.BaseY = baseY;
            model.TipX = tipX;
            model.TipY = tipY;
            _arrowAnnotationService.UpdateAnnotation(model, Panes);
            HasUnsavedChanges = true;
        }
    }

    /// <summary>
    /// Update an arrow annotation's properties
    /// </summary>
    public void UpdateArrowAnnotation(ArrowAnnotationModel model)
    {
        _arrowAnnotationService.UpdateAnnotation(model, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Remove an arrow annotation
    /// </summary>
    public void RemoveArrowAnnotation(Guid annotationId)
    {
        _arrowAnnotationService.RemoveAnnotation(annotationId, Panes);
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Get all arrow annotations for persistence
    /// </summary>
    public IReadOnlyList<ArrowAnnotationModel> GetArrowAnnotations()
    {
        return _arrowAnnotationService.GetAllAnnotations();
    }

    /// <summary>
    /// Get a specific arrow annotation by ID
    /// </summary>
    public ArrowAnnotationModel? GetArrowAnnotation(Guid annotationId)
    {
        return _arrowAnnotationService.GetAnnotation(annotationId);
    }

    /// <summary>
    /// Restore arrow annotations from saved project
    /// </summary>
    public void RestoreArrowAnnotations(IEnumerable<ArrowAnnotationModel> annotations)
    {
        _arrowAnnotationService.RestoreAnnotations(annotations, Panes);
    }

    /// <summary>
    /// Clear all arrow annotations
    /// </summary>
    public void ClearAllArrowAnnotations()
    {
        _arrowAnnotationService.ClearAllAnnotations(Panes);
        HasUnsavedChanges = true;
    }

    #endregion

    #region Tools Commands

    [RelayCommand]
    private void ToggleHoverTooltips()
    {
        HoverTooltipsEnabled = !HoverTooltipsEnabled;
    }

    [RelayCommand]
    private async Task ShowSettings()
    {
        bool? saved = await _dialogService.ShowSettingsAsync(_applicationSettings);
        if (saved == true)
        {
            _settingsPersistence.Save(_applicationSettings);
            // Apply new default if session state should mirror the new default
            HoverTooltipsEnabled = _applicationSettings.HoverTooltipsEnabledByDefault;
        }
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    /// <summary>
    /// Release the data model and all pane VMs. Idempotent. After Dispose the VM
    /// is unusable; call only from the window-close path.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Analysis: IAnalysisService is a singleton that outlives this window VM, so every
        // back-reference into the pane graph / panel must be severed or the whole graph leaks.
        ActiveCurves.CollectionChanged -= OnActiveCurvesChangedForAnalysis;
        CompactPlot.Curves.CollectionChanged -= OnCompactCurvesChangedForCommands;
        AnalysisPanel.PointFlashRequested -= OnAnalysisPointFlashRequested;
        AnalysisPanel.PlaceEventLineRequested -= OnAnalysisPlaceEventLineRequested;
        AnalysisPanel.LineToggleRequested -= OnAnalysisLineToggleRequested;
        AnalysisPanel.TableInvalidated -= OnAnalysisTableInvalidated;
        AnalysisPanel.BandsInvalidated -= OnAnalysisBandsInvalidated;
        AnalysisPanel.BandRemoveRequested -= OnAnalysisBandRemoveRequested;
        AnalysisPanel.CsvExportRequested -= OnAnalysisCsvExportRequested;
        CompactPlot.BandLimitExceeded -= OnCompactBandLimitExceeded;
        AnalysisPanel.Dispose();
        if (_visibleRangeDebounce is not null)
        {
            _visibleRangeDebounce.Tick -= OnVisibleRangeDebounceTick;
            _visibleRangeDebounce.Stop();
            _visibleRangeDebounce = null;
        }
        TearDownStackedAnalysisSource();
        TearDownCompactAnalysisSource();

        ReplaceCurrentData(null);

        foreach (var pane in Panes) pane.Dispose();
        Panes.Clear();

        GC.SuppressFinalize(this);
    }

    #endregion
}
