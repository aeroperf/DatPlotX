using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using DatPlotX.Views;
using System.Data;

namespace DatPlotX;

/// <summary>
/// Main window with MVVM architecture and multi-pane support
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly IDialogService _dialogService;
    private readonly IFileOperationsService _fileOperationsService;
    private readonly MainWindowLayoutManager _layoutManager;
    private readonly MainWindowEventCoordinator _eventCoordinator;
    private readonly Dictionary<PlotPaneViewModel, PlotPaneControl> _paneControlCache = new();
    private readonly Dictionary<PlotPaneViewModel, IDisposable> _paneSubscriptions = new();
    private GridLength _savedBottomPaneHeight = new GridLength(15, GridUnitType.Star);

    /// <summary>
    /// Designer-only parameterless ctor for the Avalonia previewer / x:CompileBindings.
    /// Do not use at runtime — DI provides the parameterized constructor.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _viewModel = null!;
        _dialogService = null!;
        _fileOperationsService = null!;
        _layoutManager = new MainWindowLayoutManager();
        _eventCoordinator = new MainWindowEventCoordinator();
    }

    public MainWindow(MainWindowViewModel viewModel, IDialogService dialogService, IFileOperationsService fileOperationsService)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _dialogService = dialogService;
        _fileOperationsService = fileOperationsService;
        _layoutManager = new MainWindowLayoutManager();
        _eventCoordinator = new MainWindowEventCoordinator();
        DataContext = _viewModel;

        // Subscribe to pane collection changes
        _viewModel.Panes.CollectionChanged += Panes_CollectionChanged;

        // Subscribe to SourceData changes to rebuild DataGrid columns
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Subscribe to window loaded
        this.Loaded += MainWindow_Loaded;

        // Wire up Help menu clicks
        var aboutMenuItem = this.FindControl<MenuItem>("AboutMenuItem");
        if (aboutMenuItem != null)
        {
            aboutMenuItem.Click += About_Click;
        }

        var userGuideMenuItem = this.FindControl<MenuItem>("UserGuideMenuItem");
        if (userGuideMenuItem != null)
        {
            userGuideMenuItem.Click += UserGuide_Click;
        }

        var whatsNewMenuItem = this.FindControl<MenuItem>("WhatsNewMenuItem");
        if (whatsNewMenuItem != null)
        {
            whatsNewMenuItem.Click += WhatsNew_Click;
        }

        var openLogFolderMenuItem = this.FindControl<MenuItem>("OpenLogFolderMenuItem");
        if (openLogFolderMenuItem != null)
        {
            openLogFolderMenuItem.Click += OpenLogFolder_Click;
        }

        this.KeyDown += MainWindow_KeyDown;

        // Wire up toggle button click
        var toggleButton = this.FindControl<Button>("ToggleBottomPaneButton");
        if (toggleButton != null)
        {
            toggleButton.Click += ToggleBottomPane_Click;
        }

        // Propagate hover tooltip toggle to all pane controls + compact surface
        _viewModel.HoverTooltipsEnabledChanged += OnHoverTooltipsEnabledChanged;
        var compact = this.FindControl<CompactPlotControl>("compactSurface");
        if (compact is not null)
        {
            compact.HoverTooltipsEnabled = _viewModel.HoverTooltipsEnabled;
            // Analysis gestures on the Compact surface (mirror the Stacked-pane wiring):
            // Shift+drag defines a segment; "Use as Segment Boundary" completes an event-line
            // pair; pan/zoom retracks the visible-window segment (debounced in the VM).
            compact.SegmentDefined += OnCompactSegmentDefined;
            compact.UseEventLineAsSegmentBoundaryRequested += OnCompactUseAsBoundary;
            compact.VisibleRangeChanged += OnCompactVisibleRangeChanged;
        }

        // Wire grouped-plot sidebar "Configure Inputs…" button + export request.
        var grouped = this.FindControl<GroupedPlotControl>("groupedSurface");
        if (grouped is not null)
        {
            grouped.HoverTooltipsEnabled = _viewModel.HoverTooltipsEnabled;
            grouped.ConfigureInputsRequested += (_, _) =>
            {
                if (_viewModel.ConfigureGroupedInputsCommand.CanExecute(null))
                    _viewModel.ConfigureGroupedInputsCommand.Execute(null);
            };
            grouped.AddTextAnnotationRequested += (x, y) => HandleAddGroupedTextAnnotation(x, y);
            grouped.AddArrowAnnotationRequested += (x, y) => HandleAddGroupedArrowAnnotation(x, y);
            grouped.EditTextAnnotationRequested += id => HandleEditGroupedTextAnnotation(id);
            grouped.DeleteTextAnnotationRequested += id => HandleDeleteGroupedTextAnnotation(id);
            grouped.EditArrowAnnotationRequested += id => HandleEditGroupedArrowAnnotation(id);
            grouped.DeleteArrowAnnotationRequested += id => HandleDeleteGroupedArrowAnnotation(id);
            grouped.TextAnnotationDragCompleted += (_, _, _) => _viewModel.HasUnsavedChanges = true;
            grouped.ArrowAnnotationDragCompleted += (_, _, _, _, _) => _viewModel.HasUnsavedChanges = true;
            grouped.ExportImageRequested += (_, _) =>
            {
                if (_viewModel.ExportImageCommand.CanExecute(null))
                    _viewModel.ExportImageCommand.Execute(null);
            };
        }
        _viewModel.GroupedPlotExportRequested += OnGroupedPlotExportRequested;

        // Wire up Open Recent submenu (rebuild on changes)
        _viewModel.RecentFiles.CollectionChanged += OnRecentFilesChanged;
        RebuildOpenRecentMenu();
    }

    // Named handler (not an async lambda) so OnClosed can detach it — see review M4.
    private void OnGroupedPlotExportRequested(object? sender, EventArgs e)
        => SafeInvokeAsync(ExportGroupedPlotAsync);

    private async Task ExportGroupedPlotAsync()
    {
        var grouped = this.FindControl<GroupedPlotControl>("groupedSurface");
        if (grouped is null) return;
        var ok = await _fileOperationsService.ExportGroupedPlotAsync(grouped.GetPlot());
        _viewModel.StatusText = ok ? "Exported Grouped Plot to image file" : "Export cancelled.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Panes.CollectionChanged -= Panes_CollectionChanged;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.HoverTooltipsEnabledChanged -= OnHoverTooltipsEnabledChanged;
        var compact = this.FindControl<CompactPlotControl>("compactSurface");
        if (compact is not null)
        {
            compact.SegmentDefined -= OnCompactSegmentDefined;
            compact.UseEventLineAsSegmentBoundaryRequested -= OnCompactUseAsBoundary;
            compact.VisibleRangeChanged -= OnCompactVisibleRangeChanged;
        }
        _viewModel.GroupedPlotExportRequested -= OnGroupedPlotExportRequested;
        _viewModel.CrashFolderOpenRequested -= OpenFolderInFileManager;
        _viewModel.RecentFiles.CollectionChanged -= OnRecentFilesChanged;
        panesContainer.PropertyChanged -= PanesContainer_PropertyChanged;
        BottomPaneSplitter.DragDelta -= BottomPaneSplitter_DragDelta;
        this.KeyDown -= MainWindow_KeyDown;
        foreach (var sub in _paneSubscriptions.Values) sub.Dispose();
        _paneSubscriptions.Clear();
        _paneControlCache.Clear();
        _viewModel.Dispose();
        base.OnClosed(e);
    }

    private void OnRecentFilesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => RebuildOpenRecentMenu();

    private void RebuildOpenRecentMenu()
    {
        var menu = this.FindControl<MenuItem>("OpenRecentMenu");
        if (menu is null) return;

        menu.Items.Clear();

        if (_viewModel.RecentFiles.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(empty)", IsEnabled = false });
            return;
        }

        foreach (var path in _viewModel.RecentFiles)
        {
            var item = new MenuItem
            {
                Header = TruncatePathStart(path, 60),
                Command = _viewModel.OpenRecentFileCommand,
                CommandParameter = path,
            };
            ToolTip.SetTip(item, path);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem
        {
            Header = "_Clear Recent Files",
            Command = _viewModel.ClearRecentFilesCommand,
        });
    }

    private static string TruncatePathStart(string path, int maxLength)
    {
        if (path.Length <= maxLength) return path;
        var trimmed = path[(path.Length - maxLength + 1)..];
        var sep = trimmed.IndexOfAny(['/', '\\']);
        return sep >= 0 ? "…" + trimmed[sep..] : "…" + trimmed;
    }

    private void OnCompactSegmentDefined(double xMin, double xMax) => _viewModel.DefineAnalysisSegment(xMin, xMax);
    private void OnCompactUseAsBoundary(Guid eventLineId) => _viewModel.PickEventLineSegmentBoundary(eventLineId);
    private void OnCompactVisibleRangeChanged() => _viewModel.NotifyAnalysisVisibleRangeChanged();

    private void OnHoverTooltipsEnabledChanged(bool enabled)
    {
        foreach (var paneControl in _paneControlCache.Values)
            paneControl.HoverTooltipsEnabled = enabled;

        // Compact surface lives outside the pane cache; reach it directly.
        var compact = this.FindControl<CompactPlotControl>("compactSurface");
        if (compact is not null)
            compact.HoverTooltipsEnabled = enabled;

        // Grouped surface likewise lives outside the pane cache.
        var grouped = this.FindControl<GroupedPlotControl>("groupedSurface");
        if (grouped is not null)
            grouped.HoverTooltipsEnabled = enabled;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SourceData))
        {
            if (Dispatcher.UIThread.CheckAccess())
                RebuildDataGridColumns();
            else
                Dispatcher.UIThread.Post(RebuildDataGridColumns);
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.BottomPaneCollapsed))
        {
            if (Dispatcher.UIThread.CheckAccess())
                ApplyBottomPaneCollapsed(_viewModel.BottomPaneCollapsed);
            else
                Dispatcher.UIThread.Post(() => ApplyBottomPaneCollapsed(_viewModel.BottomPaneCollapsed));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ShowAnalysisPanel))
        {
            // The Analysis panel lives inside the bottom pane (right split). Turning it on
            // while the bottom pane is collapsed would show nothing — force the pane open so
            // the panel is actually visible. (Ctrl+R / View menu while the pane is already
            // open just toggles the analysis split, which the binding handles on its own.)
            if (Dispatcher.UIThread.CheckAccess())
                EnsureBottomPaneVisibleForAnalysis();
            else
                Dispatcher.UIThread.Post(EnsureBottomPaneVisibleForAnalysis);
        }
    }

    private void EnsureBottomPaneVisibleForAnalysis()
    {
        if (_viewModel.ShowAnalysisPanel && _viewModel.IsAnalysisAvailable && _viewModel.BottomPaneCollapsed)
            _viewModel.BottomPaneCollapsed = false;
    }

    /// <summary>
    /// Cap rows materialized into the source-data preview grid.
    /// Avalonia's DataGrid does not virtualize per-row string arrays efficiently, so
    /// loading millions of rows is both slow and memory-heavy.
    /// </summary>
    private const int SourceDataGridMaxRows = 10_000;
    private const int SourceDataGridMinRows = 100;
    private const int SourceDataGridColumnWideThreshold = 200;
    private const long SourceDataGridCellBudget = 2_000_000;

    /// <summary>
    /// For column-heavy files (&gt; 200 columns), shrink the row cap so we never
    /// materialize more than ~2M cells. Each cell becomes a string in
    /// <see cref="DataRowWrapper.Values"/>; this keeps preview memory bounded.
    /// </summary>
    private static int ComputeSourceGridRowCap(int columnCount)
    {
        if (columnCount <= SourceDataGridColumnWideThreshold || columnCount <= 0)
            return SourceDataGridMaxRows;

        long byBudget = SourceDataGridCellBudget / columnCount;
        if (byBudget < SourceDataGridMinRows) return SourceDataGridMinRows;
        if (byBudget > SourceDataGridMaxRows) return SourceDataGridMaxRows;
        return (int)byBudget;
    }

    private void RebuildDataGridColumns()
    {
        SourceDataGrid.AutoGenerateColumns = false;
        SourceDataGrid.Columns.Clear();
        SourceDataGrid.ItemsSource = null;

        var dataView = _viewModel.SourceData;
        if (dataView?.Table == null)
            return;

        var table = dataView.Table;

        int rowCap = ComputeSourceGridRowCap(table.Columns.Count);
        int displayedRows = Math.Min(table.Rows.Count, rowCap);
        var rows = new List<DataRowWrapper>(displayedRows);
        for (int i = 0; i < displayedRows; i++)
        {
            rows.Add(new DataRowWrapper(table.Rows[i], table.Columns.Count));
        }

        if (table.Rows.Count > rowCap)
        {
            _viewModel.ReportSourceGridPreviewTruncated(displayedRows, table.Rows.Count);
        }

        // Build template columns that read values directly from the wrapper.
        for (int i = 0; i < table.Columns.Count; i++)
        {
            int colIndex = i;
            SourceDataGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = table.Columns[i].ColumnName,
                IsReadOnly = true,
                CellTemplate = new FuncDataTemplate<DataRowWrapper>((row, _) =>
                    new TextBlock
                    {
                        Text = row?.Values[colIndex] ?? string.Empty,
                        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                        Margin = new Thickness(4, 0)
                    })
            });
        }

        SourceDataGrid.ItemsSource = rows;
    }

    /// <summary>
    /// Lightweight wrapper around a DataRow with pre-converted string values
    /// for use with Avalonia DataGrid's FuncDataTemplate (no binding needed).
    /// </summary>
    internal sealed class DataRowWrapper
    {
        public readonly string[] Values;

        public DataRowWrapper(DataRow row, int columnCount)
        {
            Values = new string[columnCount];
            for (int i = 0; i < columnCount; i++)
            {
                var value = row[i];
                Values[i] = value == DBNull.Value ? string.Empty : value?.ToString() ?? string.Empty;
            }
        }
    }

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        // Subscribe to container size changes via Bounds property
        panesContainer.PropertyChanged += PanesContainer_PropertyChanged;
        _layoutManager.UpdatePaneLayout(_viewModel.Panes, panesContainer);

        // Subscribe to GridSplitter drag events
        BottomPaneSplitter.DragDelta += BottomPaneSplitter_DragDelta;

        // Sync initial bottom-pane state with the VM (e.g., a project loaded with collapsed=true).
        ApplyBottomPaneCollapsed(_viewModel.BottomPaneCollapsed);

        // Offer to surface a saved crash report from a previous session (opt-in; local-only).
        _viewModel.CrashFolderOpenRequested += OpenFolderInFileManager;
        SafeInvokeAsync(_viewModel.CheckForPreviousCrashAsync);
    }

    private void BottomPaneSplitter_DragDelta(object? sender, Avalonia.Input.VectorEventArgs e)
    {
        _layoutManager.EnforceBottomPaneSplitterConstraints(MainContentGrid.RowDefinitions[0], MainContentGrid.RowDefinitions[2]);
    }

    private void PanesContainer_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            _layoutManager.UpdatePaneLayout(_viewModel.Panes, panesContainer);
        }
    }

    private void Panes_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        // Update pane heights for equal distribution
        _layoutManager.UpdatePaneLayout(_viewModel.Panes, panesContainer);
    }

    /// <summary>
    /// Wire up a PlotPaneControl when it loads.
    /// Called from PlotPaneControl via its Loaded event wiring.
    /// </summary>
    internal void OnPlotPaneControlLoaded(PlotPaneControl paneControl)
    {
        if (paneControl.DataContext is not PlotPaneViewModel paneViewModel) return;

        // Idempotency: if this VM already has subscriptions from a prior Loaded cycle, detach them.
        if (_paneSubscriptions.Remove(paneViewModel, out var existing))
            existing.Dispose();

        RegisterPaneControl(paneViewModel, paneControl);

        void OnUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            paneControl.Unloaded -= OnUnloaded;
            if (_paneSubscriptions.Remove(paneViewModel, out var sub)) sub.Dispose();
            UnregisterPaneControl(paneViewModel);
        }
        paneControl.Unloaded += OnUnloaded;

        var subscription = _eventCoordinator.WirePaneControl(
            paneControl,
            paneViewModel,
            onXAxisChanged: (pc, min, max) =>
            {
                _eventCoordinator.SynchronizeXAxis(pc, min, max, _viewModel.Panes, panesContainer, _viewModel.XAxisSynchronized);
                // Zoom/pan changes the visible window — re-clamp callouts so their labels
                // stay inside the viewport even when the user zooms in past their original
                // position. Stored offsets are preserved; only render coords change.
                _viewModel.ReclampCalloutsForAllPanes();
                // The visible-window analysis segment tracks the live X-axis range.
                _viewModel.NotifyAnalysisVisibleRangeChanged();
            },
            onMousePositionChanged: HandleMousePositionChanged,
            onPlotUpdated: () => _layoutManager.SynchronizePlotLayout(_viewModel.Panes, panesContainer),
            addCurvesToPaneHandler: HandleAddCurvesToPane,
            formatCurveHandler: HandleFormatCurve,
            formatPaneHandler: HandleFormatPane,
            clearPaneHandler: HandleClearPane,
            addPaneHandler: () => _viewModel.AddPaneCommand.Execute(null),
            removePaneHandler: HandleRemoveSpecificPane,
            addEventLineAtPositionHandler: HandleAddEventLineAtPosition,
            clearEventLinesHandler: HandleClearPaneEventLines,
            deleteEventLineHandler: HandleDeleteEventLine,
            calloutDragCompletedHandler: HandleCalloutDragCompleted,
            eventLineDragMovedHandler: HandleEventLineDragMoved,
            eventLineDragCompletedHandler: HandleEventLineDragCompleted,
            addTextAnnotationHandler: HandleAddTextAnnotation,
            textAnnotationDragCompletedHandler: HandleTextAnnotationDragCompleted,
            editTextAnnotationHandler: HandleEditTextAnnotation,
            deleteTextAnnotationHandler: HandleDeleteTextAnnotation,
            addArrowAnnotationHandler: HandleAddArrowAnnotation,
            arrowAnnotationDragCompletedHandler: HandleArrowAnnotationDragCompleted,
            editArrowAnnotationHandler: HandleEditArrowAnnotation,
            deleteArrowAnnotationHandler: HandleDeleteArrowAnnotation,
            exportImageHandler: () => _viewModel.ExportImageCommand.Execute(null));

        // Shift+drag on a pane defines an analysis segment; "Use as Segment Boundary" on an
        // event line completes an EventLinePair segment over two picks. These are bundled into
        // the same disposable as WirePaneControl so the idempotency guard above (which detaches a
        // prior cycle's subscriptions before re-subscribing) covers them too — Avalonia re-raises
        // Loaded on every re-attach, and a bare += here would otherwise stack stale handlers and
        // fire DefineAnalysisSegment / PickEventLineSegmentBoundary multiple times per gesture.
        void OnSegmentDefined(double xMin, double xMax) => _viewModel.DefineAnalysisSegment(xMin, xMax);
        void OnUseAsBoundary(Guid eventLineId) => _viewModel.PickEventLineSegmentBoundary(eventLineId);
        paneControl.SegmentDefined += OnSegmentDefined;
        paneControl.UseEventLineAsSegmentBoundaryRequested += OnUseAsBoundary;

        _paneSubscriptions[paneViewModel] = new DisposableAction(() =>
        {
            subscription.Dispose();
            paneControl.SegmentDefined -= OnSegmentDefined;
            paneControl.UseEventLineAsSegmentBoundaryRequested -= OnUseAsBoundary;
        });

        // Apply current hover tooltip state AFTER WirePaneControl → SetViewModel has constructed
        // the handler. Doing this earlier silently no-ops (handler is null) and leaves the new
        // pane stuck in the default-true state, which surfaces as hover-tooltips dead on that
        // pane until the user toggles them off/on.
        paneControl.HoverTooltipsEnabled = _viewModel.HoverTooltipsEnabled;
    }

    /// <summary>
    /// Right-click on a stacked pane: hit-test the global event lines and stash the matched id
    /// on the pane control so the surface context menu can show "Delete Event Line" for it.
    /// Returning without setting Handled lets the menu open through the normal path.
    /// </summary>
    internal void OnPlotPaneRightClick(PlotPaneControl paneControl, Avalonia.Input.PointerPressedEventArgs e)
    {
        var paneViewModel = paneControl.GetViewModel();
        if (paneViewModel?.PlotModel == null)
        {
            paneControl.SetRightClickedEventLine(null);
            return;
        }

        var avaPlot = paneControl.GetAvaPlot();
        var mousePos = e.GetPosition(avaPlot);

        ScottPlot.Pixel mousePixel = new((float)(mousePos.X * avaPlot.DisplayScale), (float)(mousePos.Y * avaPlot.DisplayScale));
        var mouseCoordinate = avaPlot.Plot.GetCoordinates(mousePixel);

        // Pixel-based hit tolerance keeps the click target the same size regardless of zoom.
        // Convert ~8 screen pixels (DisplayScale-corrected) to data units by sampling the X-axis
        // at the cursor and at cursor+8px and taking the delta. Matches the Compact surface's
        // 6px tolerance with a small bump for stacked-mode's 2px-wide VerticalLine + ScottPlot's
        // DisplayScale rounding hysteresis.
        const float toleranceScreenPixels = 8f;
        var edgePixel = new ScottPlot.Pixel(mousePixel.X + toleranceScreenPixels, mousePixel.Y);
        var edgeCoordinate = avaPlot.Plot.GetCoordinates(edgePixel);
        double tolerance = System.Math.Abs(edgeCoordinate.X - mouseCoordinate.X);
        if (tolerance <= 0 || double.IsNaN(tolerance))
        {
            // Fallback before first render: 2% of current data range.
            var xRange = paneViewModel.PlotModel.Axes.Bottom.Range;
            tolerance = (xRange.Max - xRange.Min) * 0.02;
        }

        // Prefer the global-event-line id so deletion can be routed through the global service
        // (removes the line + label from every pane). Fall back to no-op if the pane only has
        // a local event line — those go away with "Clear All Event Lines".
        paneControl.SetRightClickedEventLine(paneViewModel.FindEventLineAtX(mouseCoordinate.X, tolerance));
    }

    /// <summary>
    /// Safely invoke an async operation, showing an error dialog on unhandled exception.
    /// Use this to wrap async void event handlers so exceptions don't crash the app.
    /// </summary>
    private async void SafeInvokeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            DatPlotX.Helpers.SafeErrorHandler.LogError(ex, "async handler", "MainWindow.SafeInvokeAsync");
            try
            {
                await _dialogService.ShowError($"An unexpected error occurred: {ex.Message}", "Error");
            }
            catch (Exception dialogEx)
            {
                // A crash in the error dialog must never terminate the process.
                DatPlotX.Helpers.SafeErrorHandler.LogError(dialogEx, "showing error dialog", "MainWindow.SafeInvokeAsync");
            }
        }
    }

    /// <summary>
    /// Handle callout drag completion - update the callout model with new offset
    /// </summary>
    private void HandleCalloutDragCompleted(PlotPaneViewModel paneViewModel, Guid calloutId, double offsetX, double offsetY)
    {
        _viewModel.UpdateCalloutOffset(calloutId, offsetX, offsetY);
    }

    /// <summary>
    /// Per-frame drag of an event line on one pane — push the new X to every *other* pane so
    /// the lines stay vertically aligned across the stack. The originating pane already moved
    /// itself locally inside <see cref="DatPlotX.Views.Controls.PlotPaneDragHandler"/>.
    /// </summary>
    private void HandleEventLineDragMoved(PlotPaneViewModel sourcePane, Guid eventLineId, double newXPosition)
    {
        foreach (var pane in _viewModel.Panes)
        {
            if (pane == sourcePane) continue;
            pane.MoveGlobalEventLine(eventLineId, newXPosition);
        }
    }

    /// <summary>
    /// Handle event line drag completion - update the model and synchronize across panes
    /// </summary>
    private void HandleEventLineDragCompleted(PlotPaneViewModel paneViewModel, Guid eventLineId, double newXPosition)
    {
        _viewModel.OnGlobalEventLineMoved(eventLineId, newXPosition);
    }

    /// <summary>
    /// Handle adding a text annotation at a specific position
    /// </summary>
    private void HandleAddTextAnnotation(PlotPaneViewModel paneViewModel, double x, double y)
    {
        SafeInvokeAsync(async () =>
        {
            var seed = new TextAnnotationModel
            {
                PaneIndex = paneViewModel.PaneModel.Index,
                X = x,
                Y = y,
                Text = "Annotation"
            };

            var result = await _dialogService.ShowTextAnnotationDialogAsync(seed);
            if (result is not null)
            {
                result.X = x;
                result.Y = y;
                result.PaneIndex = paneViewModel.PaneModel.Index;
                _viewModel.AddTextAnnotation(result);
                _viewModel.StatusText = $"Added text annotation: {result.Text}";
            }
        });
    }

    /// <summary>
    /// Handle text annotation drag completion
    /// </summary>
    private void HandleTextAnnotationDragCompleted(PlotPaneViewModel paneViewModel, Guid annotationId, double newX, double newY)
    {
        _viewModel.UpdateTextAnnotationPosition(annotationId, newX, newY);
    }

    /// <summary>
    /// Handle adding an arrow annotation at a specific position
    /// </summary>
    private void HandleAddArrowAnnotation(PlotPaneViewModel paneViewModel, double x, double y)
    {
        SafeInvokeAsync(async () =>
        {
            double xRange = 0;
            double yRange = 0;
            if (paneViewModel.PlotModel != null)
            {
                var xAxisRange = paneViewModel.PlotModel.Axes.Bottom.Range;
                var yAxisRange = paneViewModel.PlotModel.Axes.Left.Range;
                xRange = xAxisRange.Span * 0.1;
                yRange = yAxisRange.Span * 0.1;
            }

            var seed = new ArrowAnnotationModel
            {
                PaneIndex = paneViewModel.PaneModel.Index,
                BaseX = x - xRange,
                BaseY = y + yRange,
                TipX = x,
                TipY = y
            };

            var result = await _dialogService.ShowArrowAnnotationDialogAsync(seed);
            if (result is not null)
            {
                result.BaseX = x - xRange;
                result.BaseY = y + yRange;
                result.TipX = x;
                result.TipY = y;
                result.PaneIndex = paneViewModel.PaneModel.Index;
                _viewModel.AddArrowAnnotation(result);
                _viewModel.StatusText = "Added arrow annotation";
            }
        });
    }

    /// <summary>
    /// Handle arrow annotation drag completion
    /// </summary>
    private void HandleArrowAnnotationDragCompleted(PlotPaneViewModel paneViewModel, Guid annotationId,
        double baseX, double baseY, double tipX, double tipY)
    {
        _viewModel.UpdateArrowAnnotationPosition(annotationId, baseX, baseY, tipX, tipY);
    }

    /// <summary>
    /// Handle editing an existing text annotation
    /// </summary>
    private void HandleEditTextAnnotation(Guid annotationId)
    {
        SafeInvokeAsync(async () =>
        {
            var model = _viewModel.GetTextAnnotation(annotationId);
            if (model == null)
                return;

            var result = await _dialogService.ShowTextAnnotationDialogAsync(model);
            if (result is not null)
            {
                _viewModel.UpdateTextAnnotation(result);
                _viewModel.StatusText = "Text annotation updated";
            }
        });
    }

    /// <summary>
    /// Handle deleting a text annotation
    /// </summary>
    private void HandleDeleteTextAnnotation(Guid annotationId)
    {
        _viewModel.RemoveTextAnnotation(annotationId);
        _viewModel.StatusText = "Text annotation deleted";
    }

    /// <summary>
    /// Handle editing an existing arrow annotation
    /// </summary>
    private void HandleEditArrowAnnotation(Guid annotationId)
    {
        SafeInvokeAsync(async () =>
        {
            var model = _viewModel.GetArrowAnnotation(annotationId);
            if (model == null)
                return;

            var result = await _dialogService.ShowArrowAnnotationDialogAsync(model);
            if (result is not null)
            {
                _viewModel.UpdateArrowAnnotation(result);
                _viewModel.StatusText = "Arrow annotation updated";
            }
        });
    }

    /// <summary>
    /// Handle deleting an arrow annotation
    /// </summary>
    private void HandleDeleteArrowAnnotation(Guid annotationId)
    {
        _viewModel.RemoveArrowAnnotation(annotationId);
        _viewModel.StatusText = "Arrow annotation deleted";
    }

    // ── Grouped-mode annotation handlers ────────────────────────────────────

    private GroupedPlotAnnotationManager? GroupedAnnotations =>
        _viewModel.GroupedPlot?.Annotations;

    private void HandleAddGroupedTextAnnotation(double x, double y)
    {
        SafeInvokeAsync(async () =>
        {
            var seed = new TextAnnotationModel { X = x, Y = y, Text = "Annotation" };
            var result = await _dialogService.ShowTextAnnotationDialogAsync(seed);
            if (result is null || GroupedAnnotations is null) return;
            result.X = x;
            result.Y = y;
            GroupedAnnotations.AddText(result);
            _viewModel.HasUnsavedChanges = true;
            _viewModel.StatusText = $"Added text annotation: {result.Text}";
        });
    }

    private void HandleAddGroupedArrowAnnotation(double x, double y)
    {
        SafeInvokeAsync(async () =>
        {
            var grouped = this.FindControl<GroupedPlotControl>("groupedSurface");
            double xRange = 0, yRange = 0;
            if (grouped is not null)
            {
                var plot = grouped.GetPlot();
                xRange = plot.Axes.Bottom.Range.Span * 0.1;
                yRange = plot.Axes.Left.Range.Span * 0.1;
            }
            var seed = new ArrowAnnotationModel
            {
                BaseX = x - xRange,
                BaseY = y + yRange,
                TipX = x,
                TipY = y,
            };
            var result = await _dialogService.ShowArrowAnnotationDialogAsync(seed);
            if (result is null || GroupedAnnotations is null) return;
            result.BaseX = x - xRange;
            result.BaseY = y + yRange;
            result.TipX = x;
            result.TipY = y;
            GroupedAnnotations.AddArrow(result);
            _viewModel.HasUnsavedChanges = true;
            _viewModel.StatusText = "Added arrow annotation";
        });
    }

    private void HandleEditGroupedTextAnnotation(Guid id)
    {
        SafeInvokeAsync(async () =>
        {
            var model = GroupedAnnotations?.GetText(id);
            if (model is null) return;
            var result = await _dialogService.ShowTextAnnotationDialogAsync(model);
            if (result is null) return;
            GroupedAnnotations?.UpdateText(result);
            _viewModel.HasUnsavedChanges = true;
            _viewModel.StatusText = "Text annotation updated";
        });
    }

    private void HandleDeleteGroupedTextAnnotation(Guid id)
    {
        if (GroupedAnnotations?.RemoveText(id) == true)
        {
            _viewModel.HasUnsavedChanges = true;
            _viewModel.StatusText = "Text annotation deleted";
        }
    }

    private void HandleEditGroupedArrowAnnotation(Guid id)
    {
        SafeInvokeAsync(async () =>
        {
            var model = GroupedAnnotations?.GetArrow(id);
            if (model is null) return;
            var result = await _dialogService.ShowArrowAnnotationDialogAsync(model);
            if (result is null) return;
            GroupedAnnotations?.UpdateArrow(result);
            _viewModel.HasUnsavedChanges = true;
            _viewModel.StatusText = "Arrow annotation updated";
        });
    }

    private void HandleDeleteGroupedArrowAnnotation(Guid id)
    {
        if (GroupedAnnotations?.RemoveArrow(id) == true)
        {
            _viewModel.HasUnsavedChanges = true;
            _viewModel.StatusText = "Arrow annotation deleted";
        }
    }

    #region Menu Event Handlers

    private void About_Click(object? sender, RoutedEventArgs e)
        => SafeInvokeAsync(_dialogService.ShowAboutAsync);

    private void UserGuide_Click(object? sender, RoutedEventArgs e) => OpenUserGuide();

    private void WhatsNew_Click(object? sender, RoutedEventArgs e)
        => SafeInvokeAsync(_dialogService.ShowWhatsNewAsync);

    private void OpenUserGuide()
        => SafeInvokeAsync(_dialogService.ShowUserGuideAsync);

    private void OpenLogFolder_Click(object? sender, RoutedEventArgs e)
        => OpenFolderInFileManager(Helpers.AppPaths.LogDirectory);

    /// <summary>
    /// Reveal a folder in the OS file manager (Explorer / Finder / xdg-open). Used by the
    /// "Open Log Folder" Help item so users can locate logs to attach to a bug report.
    /// </summary>
    private void OpenFolderInFileManager(string path)
    {
        try
        {
            System.IO.Directory.CreateDirectory(path);
            string fileName;
            string arguments;
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                fileName = "explorer.exe";
                arguments = $"\"{path}\"";
            }
            else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                fileName = "open";
                arguments = $"\"{path}\"";
            }
            else
            {
                fileName = "xdg-open";
                arguments = $"\"{path}\"";
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            SafeInvokeAsync(() => _dialogService.ShowError(
                $"Could not open the log folder.\n\nLocation:\n{path}\n\n{ex.Message}", "Open Log Folder"));
        }
    }

    private void MainWindow_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.F1)
        {
            if ((e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) == Avalonia.Input.KeyModifiers.Shift)
            {
                SafeInvokeAsync(_dialogService.ShowWhatsNewAsync);
            }
            else
            {
                OpenUserGuide();
            }
            e.Handled = true;
        }
    }

    private void ToggleBottomPane_Click(object? sender, RoutedEventArgs e)
    {
        // Flip VM state; ApplyBottomPaneCollapsed runs via PropertyChanged.
        _viewModel.BottomPaneCollapsed = !_viewModel.BottomPaneCollapsed;
        _viewModel.HasUnsavedChanges = true;
    }

    private void ApplyBottomPaneCollapsed(bool collapsed)
    {
        var bottomPaneRow = MainContentGrid.RowDefinitions[2];
        if (!collapsed)
        {
            bottomPaneRow.Height = _savedBottomPaneHeight;
            BottomPaneBorder.IsVisible = true;
            BottomPaneSplitter.IsEnabled = true;
            ToggleBottomPaneButton.Content = "\u25BC"; // down arrow
            ToolTip.SetTip(ToggleBottomPaneButton, "Click to hide Source Data");
        }
        else
        {
            // Collapse — save current height, then set to 0 pixels (not star)
            if (bottomPaneRow.Height.GridUnitType != GridUnitType.Pixel || bottomPaneRow.Height.Value != 0)
                _savedBottomPaneHeight = bottomPaneRow.Height;
            bottomPaneRow.Height = new GridLength(0, GridUnitType.Pixel);
            BottomPaneBorder.IsVisible = false;
            BottomPaneSplitter.IsEnabled = false;
            ToggleBottomPaneButton.Content = "\u25B2"; // up arrow
            ToolTip.SetTip(ToggleBottomPaneButton, "Click to show Source Data");
        }
    }

    /// <summary>
    /// Handle mouse position changes and update statusbar with coordinates
    /// </summary>
    private void HandleMousePositionChanged(double x, double y1, double y2, bool hasY2)
    {
        if (!hasY2 && x == 0 && y1 == 0)
        {
            _viewModel.MousePositionText = "";
        }
        else if (hasY2)
        {
            _viewModel.MousePositionText = $"X: {x:F2}, Y1: {y1:F2}, Y2: {y2:F2}";
        }
        else
        {
            _viewModel.MousePositionText = $"X: {x:F2}, Y1: {y1:F2}";
        }
    }

    #endregion

    #region Context Menu Handlers

    private void HandleAddCurvesToPane(int paneIndex)
    {
        SafeInvokeAsync(() => _viewModel.AddCurvesToSpecificPane(paneIndex));
    }

    private void HandleFormatCurve(int paneIndex)
    {
        SafeInvokeAsync(async () =>
        {
            if (_viewModel.ActiveCurves.Count == 0)
            {
                await _dialogService.ShowInformation(
                    "No curves to manage. Add curves to the plot first.",
                    "No Curves");
                return;
            }

            var result = await _dialogService.ShowFormatCurveAsync(_viewModel.ActiveCurves, paneIndex);
            if (result is not null)
            {
                var curve = result.Curve;

                if (curve.PaneIndex >= 0 && curve.PaneIndex < _viewModel.Panes.Count)
                {
                    var pane = _viewModel.Panes[curve.PaneIndex];

                    if (result.DeleteRequested)
                    {
                        bool success = pane.RemoveCurve(curve.Id);
                        if (success)
                        {
                            // ActiveCurves change drives the analysis recompute.
                            _viewModel.ActiveCurves.Remove(curve);
                            _viewModel.StatusText = $"Deleted curve: {curve.CurveName}";
                        }
                    }
                    else
                    {
                        bool success = pane.UpdateCurveFormat(curve);
                        if (success)
                        {
                            // Format/visibility edit doesn't touch ActiveCurves — nudge analysis.
                            _viewModel.NotifyAnalysisCurvesChanged();
                            _viewModel.StatusText = $"Updated format for curve: {curve.CurveName}";
                        }
                    }
                }
            }
        });
    }

    private void HandleClearPane(int paneIndex)
    {
        if (paneIndex < 0 || paneIndex >= _viewModel.Panes.Count) return;

        var pane = _viewModel.Panes[paneIndex];
        int count = pane.GetPlottedCurves().Count;
        if (count == 0) return;

        SafeInvokeAsync(async () =>
        {
            var result = await _dialogService.ShowConfirmation(
                $"Remove all {count} curve{(count == 1 ? "" : "s")} from pane {paneIndex + 1}? This cannot be undone.",
                "Clear Pane");
            if (result != DialogResult.Yes) return;

            pane.Clear();
            _viewModel.StatusText = $"Cleared pane {paneIndex + 1}";
        });
    }

    private void HandleFormatPane(int paneIndex)
    {
        if (paneIndex < 0 || paneIndex >= _viewModel.Panes.Count)
            return;

        SafeInvokeAsync(async () =>
        {
            var pane = _viewModel.Panes[paneIndex];
            int originalXAxisDecimals = pane.PaneModel.XAxisDecimalPlaces;

            var dialogResult = await _dialogService.ShowFormatPaneAsync(pane.PaneModel);
            if (dialogResult == true)
            {
                if (pane.PaneModel.XAxisDecimalPlaces != originalXAxisDecimals)
                    _eventCoordinator.SynchronizeXAxisDecimals(paneIndex, pane.PaneModel.XAxisDecimalPlaces, _viewModel.Panes);

                pane.ApplyFormatting();
                _layoutManager.SynchronizePlotLayout(_viewModel.Panes, panesContainer);
                _viewModel.StatusText = $"Applied formatting to pane {paneIndex + 1}";
            }
        });
    }

    private void HandleAddEventLineAtPosition(PlotPaneViewModel paneViewModel, double xPosition)
    {
        SafeInvokeAsync(async () =>
        {
            var defaultLabel = _viewModel.GenerateEventLineLabel();
            var accepted = await _dialogService.ShowAddEventLineAsync(xPosition, defaultLabel);
            if (accepted is not null)
            {
                _viewModel.AddGlobalEventLine(xPosition, accepted.LabelText, accepted.ColorHex);
                foreach (var pane in _viewModel.Panes)
                    RefreshPane(pane);
            }
        });
    }

    /// <summary>
    /// Delete a single global event line from every pane (and any associated callouts).
    /// Wired from the per-pane right-click menu's "Delete Event Line" item.
    /// </summary>
    private void HandleDeleteEventLine(Guid eventLineId)
    {
        _viewModel.RemoveGlobalEventLine(eventLineId);
        foreach (var pane in _viewModel.Panes)
            RefreshPane(pane);
        _viewModel.StatusText = "Deleted event line";
    }

    private void HandleClearPaneEventLines(PlotPaneViewModel paneViewModel)
    {
        SafeInvokeAsync(async () =>
        {
            var result = await _dialogService.ShowConfirmation(
                "Clear all event lines from ALL panes?\n(This will remove all global event lines and their callouts)",
                "Clear All Event Lines");

            if (result == DatPlotX.Services.DialogResult.Yes)
            {
                _viewModel.ClearAllGlobalEventLines();
                foreach (var pane in _viewModel.Panes)
                {
                    pane.ClearEventLines();
                    RefreshPane(pane);
                }
                _viewModel.StatusText = "Cleared all event lines";
            }
        });
    }

    internal void RegisterPaneControl(PlotPaneViewModel paneViewModel, PlotPaneControl paneControl)
    {
        _paneControlCache[paneViewModel] = paneControl;
    }

    internal void UnregisterPaneControl(PlotPaneViewModel paneViewModel)
    {
        _paneControlCache.Remove(paneViewModel);
    }

    private void RefreshPane(PlotPaneViewModel paneViewModel)
    {
        if (_paneControlCache.TryGetValue(paneViewModel, out var control))
            control.RefreshPlot();
    }

    private void HandleRemoveSpecificPane(int paneIndex)
    {
        SafeInvokeAsync(async () =>
        {
            if (_viewModel.Panes.Count <= 1)
            {
                await _dialogService.ShowInformation("Cannot remove the last pane.", "Remove Pane");
                return;
            }

            if (paneIndex >= 0 && paneIndex < _viewModel.Panes.Count)
            {
                var result = await _dialogService.ShowConfirmation(
                    $"Are you sure you want to remove Pane {paneIndex}?",
                    "Remove Pane");

                if (result == DatPlotX.Services.DialogResult.Yes)
                    _viewModel.RemoveSpecificPane(paneIndex);
            }
        });
    }

    #endregion

    /// <summary>Runs a detach action on dispose. Used to bundle several pane subscriptions
    /// into one entry in <see cref="_paneSubscriptions"/> so they tear down together.</summary>
    private sealed class DisposableAction(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
