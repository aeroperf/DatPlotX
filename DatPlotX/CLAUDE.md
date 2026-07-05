# DatPlotX — Avalonia Data Plotting Application

## Project Overview
DatPlotX is a cross-platform data visualization application built with Avalonia UI 11.x, .NET 10, and **three interchangeable plot rendering modes**:

- **Stacked Panes** (default, ScottPlot 5.x) — multi-pane synchronized stripchart with shared X-axis. Supports event lines, intersection callouts, statistics, text/arrow annotations.
- **Compact Plot Surface** (OxyPlot 2.x) — single plot area with one banded Y axis per curve, alternating left/right edges. Modeled on FDA / FDM (NTSB-style) flight data exhibits. Supports event lines + curve×event-line callouts, text/arrow annotations (anchored to a curve's banded Y axis via `CompactCurveAnchor`), image export (PNG / JPEG / SVG), and curve analysis & statistics (Analysis Results panel + segments, shipped 0.14.0 — panel only, no inline corner overlay; see the "Compact-mode analysis (Phase 2A)" section below).
- **Grouped Parameter Plot** (ScottPlot 5.x) — single plot surface that draws one line per unique combination of selected input-parameter values. For tabular/array-style data where each row is one experimental point (parametric performance arrays, lookup tables, etc.).

The plot mode is **chosen at project creation and locked** for the life of the project — switching modes requires `New Project` + re-import.

## Architecture

### Pattern: MVVM with DI
- **CommunityToolkit.Mvvm** source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **Microsoft.Extensions.DependencyInjection** configured in `App.axaml.cs`
- ViewModels never reference Views directly; events and services bridge the gap

### Key Components

| Layer | Key Files |
|-------|-----------|
| Entry | `Program.cs`, `App.axaml.cs` (DI container) |
| Main VM | `ViewModels/MainWindowViewModel.cs` — central coordinator (~1900 lines) |
| Pane VM | `ViewModels/PlotPaneViewModel.cs` — per-pane state |
| Pane Managers | `ViewModels/PlotPane/PlotPaneCurveManager.cs`, `PlotPaneFormattingService.cs`, `PlotPaneAnnotationManager.cs` |
| Analysis | `Services/Analysis/` (metrics, registry, `AnalysisService`, mode sources + overlay hosts), `ViewModels/AnalysisPanelViewModel.cs`, `Views/AnalysisPanel.axaml` — replaces the removed legacy `PlotPaneStatisticsManager` |
| Services | `Services/CurveCoordinationService.cs` — curve plotting orchestration |
| Parsers | `Services/Parsers/CsvDataParser.cs` — CSV/TSV parsing via CsvHelper |
| Data Model | `Models/PlotDataModel.cs` — wraps `System.Data.DataTable` |
| Main View | `Views/MainWindow.axaml` + `.cs`, `MainWindowEventCoordinator.cs`, `MainWindowLayoutManager.cs` |
| Plot Control | `Views/PlotPaneControl.axaml` + `.cs` — hosts ScottPlot `AvaPlot` |

### Data Flow
1. `FileOperationsService.ImportDataFileAsync()` → file picker + import options dialog
2. `DataImportService.ImportDataAsync()` → `CsvDataParser.ParseAsync()` → `PlotDataModel`
3. `MainWindowViewModel.OpenDataFile()` sets `_currentData`, `SourceData`, and updates the source-data grid — **no auto-plot**; the user chooses curves via the Add Curves dialog (see "Add Curves Dialog")
4. Curves added via `CurveCoordinationService.PlotSingleCurveToPane()` → `PlotPaneViewModel.AddScatterCurve()`
5. ScottPlot `AvaPlot` renders the plot; refresh triggered via `OnPlotUpdated` event

### Multi-Pane Architecture
- `Panes` is an `ObservableCollection<PlotPaneViewModel>` — each has its own `ScottPlot.Plot` instance
- Always read the plot from `Panes[i].PlotModel`; there is no top-level `MainWindowViewModel.PlotModel` (the legacy property was removed)
- X-axis synchronization propagated via `MainWindowEventCoordinator.SynchronizeXAxis()`
- `PlotPaneControl.SetViewModel()` wires `avaPlot.Plot` → `paneViewModel.PlotModel`

### DataGrid (Source Data Panel)
- **Critical**: Avalonia's `DataGrid` requires its own theme stylesheet — `App.axaml` must include `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>`. Without it, the DataGrid renders nothing (no headers, no rows) despite being visible in the layout tree.
- Avalonia DataGrid does NOT support `DataRowView`, `ExpandoObject`, or custom indexer binding (unlike WPF). The `IPropertyAccessorPlugin` system is also not invoked for DataGrid cell resolution.
- Solution: `RebuildDataGridColumns()` in `MainWindow.axaml.cs` wraps each `DataRow` in a `DataRowWrapper` (pre-converts all values to `string[]`), then uses `DataGridTemplateColumn` with `FuncDataTemplate` that reads values directly from the array — bypassing Avalonia's binding system entirely.
- No XAML `ItemsSource` binding — everything is set in code-behind; dispatched to UI thread via `Dispatcher.UIThread`

### Add Curves Dialog
- Follows WPF DatPlot.Modern pattern: one curve at a time via callback
- User selects a Y parameter + Y-axis (Y1 left / Y2 right), clicks "Plot Curve" to immediately plot
- Dialog stays open for adding multiple curves, then user clicks "Close"
- Data import does NOT auto-plot — user controls which curves to add

### Compact Plot Surface
- `Models/PlotMode` enum (`Panes` = 0, `Compact` = 1) lives on `ProjectSettingsModel.PlotMode` (nullable). Null when the field is missing from the deserialized JSON.
- `MainWindowViewModel.PlotMode` mirrors the project field; `IsCompactMode` / `IsPanesMode` properties drive UI gating in [`MainWindow.axaml`](Views/MainWindow.axaml). Adding a new menu item that should appear in only one mode? Use `IsVisible="{Binding IsPanesMode}"` or `IsCompactMode`.
- `CompactPlotViewModel` owns one OxyPlot `PlotModel` and maintains an `ObservableCollection<CompactCurveModel>`. Mutators (`SetData`, `AddCurve`, `RemoveCurve`, `ReplaceCurves`, `UpdateCurve`, `Clear`) auto-rebuild the plot; do **not** call `Rebuild` directly (private). External editors mutate a `CompactCurveModel` in place then call `UpdateCurve(curve)` to trigger a rebuild.
- `CompactCurveModel.MarkerColor` is a nullable hex string. When null, markers use `Color`; when set, line and marker colors decouple. Persisted in `.DPX` JSON.
- Boolean curves (auto-detected via `Helpers/BooleanColumnDetector` — supports 0/1, true/false, on/off, yes/no, with a fast-path overload that takes a pre-computed `double[]`) get a smaller band (`BoolBandWeight = 0.5` vs `AnalogBandWeight = 1.0`).
- `Views/CompactPlotControl` hosts an OxyPlot `PlotView` and wires a custom `PlotController`: left-drag = pan, Ctrl+left-drag = rubber-band zoom, wheel = zoom, Cmd/Ctrl+wheel = fine zoom, double-click / middle button / Home key = reset, hover = `HoverSnapTrack` tracker (toggled by `HoverTooltipsEnabled`). Right-click is intercepted on the tunnel pass and the surface ContextMenu is opened via `Dispatcher.UIThread.Post` to avoid OxyPlot's pointer-capture freeze. The right-click ContextMenu is built in code (not XAML) — Avalonia's `x:Name` source generator does not traverse `<oxy:PlotView.ContextMenu>` nested items. Toggling hover tooltips off swaps in a fresh `PlotController` so any `HoverSnapTrack`-installed manipulator loses its host.
- `Views/AddCompactCurvesDialog` is a two-stage dialog (column picker → per-curve config). Construct with `PlotDataModel` (production) so the cached column arrays are reused for boolean detection; the `DataTable?` overload exists for designer / unit-test use.
- `Views/ManageCompactCurveDialog` is the post-creation curve editor (color, line style, marker style, marker color, visibility, delete). Pattern matches Stacked-mode `FormatCurveDialog`. Do not declare a manual `InitializeComponent` in code-behind — that hides the source generator's version and leaves named fields null (NRE on first use).
- `App.axaml` includes `<StyleInclude Source="avares://OxyPlot.Avalonia/Themes/Default.axaml"/>` — without it, `PlotView` renders blank.

### Plot mode lifecycle
- New project: `MainWindowViewModel.PromptForPlotMode()` shows the modal picker; canceling aborts the operation.
- First-time CSV import (no project yet): same prompt fires.
- Loading a project file with no `PlotMode`: prompt fires, project is marked dirty so the user can save the upgrade. We don't silently rewrite their file.
- Saving: `MainWindowViewModel.SaveProjectInternal` writes `PlotMode` to the project model and passes `CompactPlot.Curves` into `IProjectStateManager.SaveCurrentState(..., compactCurves: ...)`.
- Loading a `.DPX` project with `PlotMode` set: skip the prompt, switch UI mode based on `project.PlotMode`, take the Compact branch in `ApplyLoadedProject` which calls `TearDownPaneSurface()` + `CompactPlot.ReplaceCurves(project.CompactCurves)`.

### Grouped Parameter Plot
- `Models/PlotMode.Grouped = 2`; `MainWindowViewModel.IsGroupedMode` gates UI. Locked at project creation via `PlotModePickerDialog`'s third card.
- `Models/GroupedPlotConfig` is the persisted config (inputs, X/Y columns, ShowLegend, ShowMarkers); lives on `ProjectSettingsModel.GroupedPlot` (nullable; non-null only in Grouped projects).
- `Services/GroupedDataIndexer` does the heavy lifting: `GetDistinctValues` (epsilon-deduplicated, capped at `ApplicationSettings.GroupedPlotMaxDistinctValues = 5000`) and `Project` (cartesian product across "All" inputs, line cap = `ApplicationSettings.GroupedPlotMaxLines = 48`, sorted by X per series).
- `ViewModels/GroupedPlotViewModel` follows the **EnrouteStudio `PlotVersion++` pattern**: every dropdown change → `Rebuild()` → `PlotVersion++` → view's `OnVmPropertyChanged` redraws. Do **not** mutate `Series` from outside — `Rebuild` clears and refills it. `ApplyConfig` / `BuildConfig` bridge the persisted `GroupedPlotConfig`.
- `ViewModels/GroupedInputParameterViewModel` is the per-input sidebar row. Distinct values are formatted with `{Format}{UnitSuffix}` for display; `AllSentinel = "All"` is the bottom dropdown choice and maps to `SelectedValue = null`.
- `Views/GroupedPlotControl` is a `260,Auto,*` grid (sidebar / splitter / ScottPlot). Hover tooltip pattern lifted verbatim from EnrouteStudio's PlotView (`HoverThresholdPixels = 20`). Raises `ConfigureInputsRequested` for the sidebar button; MainWindow wires it to `ConfigureGroupedInputsCommand`. `GetPlot()` hands the live `ScottPlot.Plot` to `MainWindow.ExportGroupedPlotAsync` for image export through `IFileOperationsService.ExportGroupedPlotAsync`. Right-click context menu exposes Add Text/Arrow Annotation, Edit/Delete (when hit), Clear All Annotations, Reset View; annotation lifecycle owned by `GroupedPlotAnnotationManager`.
- Annotations on Grouped use `GroupedPlotAnnotationManager` (DI'd by the view via `_vm.AttachAnnotationManager`). Manager re-applies plottables on every `UpdatePlot` (the view's `plot.Clear()` wipes them). Persisted in `.DPX` under `GroupedTextAnnotations` / `GroupedArrowAnnotations`; restore through `GroupedPlotViewModel.RestoreAnnotations` queues until the manager attaches.
- `Views/GroupedInputsPickerDialog` is the wizard. Opens automatically on first import into a Grouped project (`MainWindowViewModel.PromptGroupedInputsAfterImportAsync`) and re-openable via `Tools → Configure Inputs…`.
- Top-bar X-Axis Parameter picker is hidden in Grouped mode (sidebar owns X/Y selection). Source Data bottom pane is preserved, collapsible, and persists state via the existing `BottomPaneCollapsed` field.

### Compact-mode export
- Image export wired through `MainWindowViewModel.ExportImageCommand` → `IFileOperationsService.ExportCompactPlotAsync` → `IDataExportService.ExportOxyPlotByExtension`. Formats: PNG / JPEG (OxyPlot.SkiaSharp) and SVG (OxyPlot.SvgExporter). No BMP, no PDF.
- Tests live in [`DatPlotX.Tests/Services/DataExportServiceTests.cs`](../DatPlotX.Tests/Services/DataExportServiceTests.cs) (`ExportOxyPlotByExtension_*`) — header-magic + null-input + unsupported-extension coverage.

### Compact-mode annotations
- Text and arrow annotations live on `CompactPlotViewModel.TextAnnotations` / `ArrowAnnotations`. Each model carries `CompactCurveAnchor` (a curve `SourceColumn`) — the annotation's Y is interpreted in that band's data coords. Anchor falls back to the first visible curve when null or stale.
- Mutators (`AddTextAnnotation`, `UpdateTextAnnotation`, `RemoveTextAnnotation`, `AddArrowAnnotation`, `UpdateArrowAnnotation`, `RemoveArrowAnnotation`, `ReplaceAnnotations`, `ClearAllAnnotations`) auto-rebuild; soft-update path exists for text drag (`UpdateTextAnnotationPosition` mutates the OxyPlot annotation in place to avoid the full rebuild cost).
- Annotation OxyPlot tags use prefixes `compact_text:` / `compact_arrow:` / `compact_arrowlbl:` — view-side hit testing (`CompactPlotControl`) parses these to map screen pixels back to the model id.
- Persisted in `.DPX` under `CompactTextAnnotations` / `CompactArrowAnnotations` on `ProjectSettingsModel`. Restored via `CompactPlot.ReplaceAnnotations(...)`.

### Compact-mode analysis (Phase 2A)
- Curve analysis is wired in Compact mode (panel only — no inline corner overlay; that stays Stacked-only for now). `MainWindowViewModel.EnsureCompactAnalysisSource()` / `TearDownCompactAnalysisSource()` build `CompactAnalysisCurveSource` + `CompactAnalysisOverlayHost` over `CompactPlot` and route through the mode-agnostic `_activeAnalysisSource` / `_activeAnalysisOverlay` pair (shared with Stacked via `AttachAnalysisSource` / `DetachAnalysisSource`).
- The Analysis panel + `Analyze Curves…` / `Manage Segments…` menu items now gate on `IsAnalysisAvailable` (Stacked **or** Compact), not `IsPanesMode`. The `Inline Metrics Overlay` item stays gated on `IsPanesMode`.
- Compact gestures (`Views/CompactPlotControl`): Shift+left-drag → `SegmentDefined` (analysis segment); event-line right-click → "Use as Segment Boundary" → `UseEventLineAsSegmentBoundaryRequested`; X-axis `AxisChanged` → `VisibleRangeChanged` (debounced in the VM). `MainWindow.axaml.cs` wires all three. Compact event-line moves/removes call `IAnalysisService.SyncEventLinePairRanges()`.
- `EventLineResolver` in Compact reads `CompactPlot.EventLines` (Compact lines do **not** live in `_globalEventLineService`).
- Segments persist in `.DPX` for any mode (no schema change).
- Grouped-mode analysis remains **unimplemented** (deliberately deferred — statistics are low-value for parametric grouped data). The plan lives in [`../Docs/Curve-Analysis-Phase2-Plus-Plan.md`](../Docs/Curve-Analysis-Phase2-Plus-Plan.md).
- No curve-specific right-click menu yet (the surface-level menu covers Add / Manage / Clear / Reset / Format / Export + annotations). User can already select-and-edit any curve via Manage Curve…; a hit-tested per-curve menu would be additive.

### Observability (logging + crash dumps)
- **Per-OS dirs:** `Helpers/AppPaths` resolves the data root (`%LOCALAPPDATA%\DatPlotX` / `~/Library/Application Support/DatPlotX` / `$XDG_DATA_HOME` or `~/.local/share/DatPlotX`) and exposes `LogDirectory` + `CrashDirectory`.
- **Logging:** `Microsoft.Extensions.Logging` wired in `App.axaml.cs` via a hand-rolled, zero-dependency `Services/Logging/FileLoggerProvider` (rolling daily, 50 MB cap → numbered shards, 7-day retention). Log **events + errors, never row data / column names / file contents**; paths are basename-only. Reachable via **Help → Open Log Folder** (`MainWindow.OpenFolderInFileManager`). It's the only sink — nothing is uploaded.
- **Crash dumps:** `Services/CrashReporter` (DI'd as `ICrashReporter`) writes a scrubbed local-only dump on `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException`. Dumps carry stack/version/OS only and `Scrub()` strips absolute paths. **Never uploaded.** `ApplicationSettings.CrashReportingEnabled` (OFF default, in Settings → Privacy) only governs the next-launch prompt (`MainWindowViewModel.CheckForPreviousCrashAsync` → `CrashFolderOpenRequested` event → view opens the folder); declining deletes the dump so the user isn't nagged.

### File association & external file open
- Double-clicking a `.dpx` opens it in DatPlotX on Windows and macOS. `App.OnFrameworkInitializationCompleted` reads `desktop.Args` (Windows forwards the path via the file association; also covers CLI launch) and subscribes to `IActivatableLifetime.Activated` → `FileActivatedEventArgs` (macOS delivers a double-clicked file as an activation event, not via argv). Both route to `MainWindowViewModel.OpenProjectFromPathAsync(path)` and are deferred to the window's `Opened` event (project restore needs realized pane controls).
- Arg parsing is isolated in the pure, testable `Helpers/StartupFileLocator` (`FindProjectArgument` — first `.dpx` arg, case-insensitive, no existence check).
- macOS association is declared in the bundle's `Info.plist` (`CFBundleDocumentTypes`, extension `dpx`) emitted by [`scripts/build-macos-app.sh`](../scripts/build-macos-app.sh) — registered by Launch Services, so no in-app step.
- Windows association is **opt-in**: `Services/FileAssociationService` (`IFileAssociationService`) writes per-user `HKCU\Software\Classes` keys (`.dpx` → `DatPlotX.Project` ProgId → `shell\open\command`) only when the user picks **Help → Set as Default for .dpx Files** (`RegisterFileAssociationCommand`, gated on `IsFileAssociationSupported`). No admin, no installer, nothing silent. macOS/Linux are no-ops. Registry types come from the .NET 10 framework — no NuGet package needed.

### .DPX schema versioning
- `ProjectSettingsModel.SchemaVersion` (default `CurrentSchemaVersion` = 1) is emitted in the JSON. `ProjectSerializer.DeserializeFromJson` normalizes a missing field (deserializes to 0) up to v1. Bump `CurrentSchemaVersion` + add a migration branch in the deserializer for any future breaking on-disk change.

## Build & Run
```bash
cd DatPlotX
dotnet build
dotnet run
```

Target framework: `net10.0`

## Key Conventions
- CSV comment lines start with `#` (CsvHelper `AllowComments = true`)
- Culture-aware number parsing: `Convert.ChangeType` must always use `options.Culture`
- `[RelayCommand(CanExecute = ...)]` requires manual `NotifyCanExecuteChanged()` calls when backing state changes
- Security: file paths validated via `FilePathValidator`, column names sanitized via `InputValidator`
- Max limits: 1GB file size, 10M rows, 5000 columns (configurable via `ApplicationSettings`)

## Testing Data
Unit tests generate CSV content inline via `WriteTempCsv()` / `WriteTemp()` helpers — no external fixture files required.
