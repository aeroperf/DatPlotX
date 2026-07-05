using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.Services.Analysis;
using DatPlotX.Services.Units;
using DatPlotX.ViewModels;
using FluentAssertions;
using Moq;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.Tests.ViewModels;

/// <summary>
/// Workflow integration coverage for the four dialog migrations in M2: every dialog the VM
/// used to construct directly is now reached via <see cref="IDialogService"/>, so each
/// workflow can be exercised by mocking the service and asserting the VM's state mutations.
/// </summary>
public class MainWindowViewModelDialogTests
{
    private static PlotDataModel BuildSampleData()
    {
        var table = new DataTable();
        table.Columns.Add("time", typeof(double));
        table.Columns.Add("alt", typeof(double));
        table.Columns.Add("speed", typeof(double));
        for (int i = 0; i < 5; i++)
            table.Rows.Add((double)i, i * 10.0, i * 100.0);
        return new PlotDataModel { Data = table, SourceName = "sample" };
    }

    /// <summary>Builds a VM with loose mocks for every service. Tests then override
    /// the dialog service expectations they care about.</summary>
    private sealed class Harness
    {
        public Mock<IDataImportService> Import { get; } = new();
        public Mock<IDialogService> Dialog { get; } = new();
        public Mock<IIntersectionCalculator> Intersection { get; } = new();
        public Mock<IProjectStateManager> State { get; } = new();
        public Mock<IGlobalEventLineService> EventLines { get; } = new();
        public Mock<ICalloutAnnotationService> Callouts { get; } = new();
        public Mock<ITextAnnotationService> Text { get; } = new();
        public Mock<IArrowAnnotationService> Arrow { get; } = new();
        public Mock<IPaneCoordinationService> Pane { get; } = new();
        public Mock<ICurveCoordinationService> Curve { get; } = new();
        public Mock<IFileOperationsService> Files { get; } = new();
        public Mock<IApplicationLifetimeService> Lifetime { get; } = new();
        public Mock<IAppSettingsPersistenceService> SettingsPersistence { get; } = new();
        public Mock<IRecentFilesService> Recent { get; } = new();
        public ApplicationSettings Settings { get; } = new();
        public Mock<IGroupedDataIndexer> GroupedIndexer { get; } = new();
        public Mock<ICrashReporter> CrashReporter { get; } = new();
        public Mock<IFileAssociationService> FileAssociation { get; } = new();

        // Analysis stack is dependency-free and cheap, so use the real implementations.
        public IUnitRegistry Units { get; } = UnitRegistry.Default;
        public IMetricRegistry Metrics { get; } = new MetricRegistry();

        public MainWindowViewModel Build()
        {
            Recent.Setup(r => r.Load()).Returns(new List<string>());
            var analysis = new AnalysisService(Metrics, Units);
            var analysisPanel = new AnalysisPanelViewModel(analysis, Metrics);
            return new MainWindowViewModel(
                Import.Object, Dialog.Object, Intersection.Object, State.Object,
                EventLines.Object, Callouts.Object, Text.Object, Arrow.Object,
                Pane.Object, Curve.Object, Files.Object, Lifetime.Object,
                Settings, SettingsPersistence.Object, Recent.Object, GroupedIndexer.Object,
                analysis, Units, CrashReporter.Object, FileAssociation.Object, analysisPanel);
        }

        /// <summary>Drive the VM into "data loaded, Panes mode" through the public NewProject
        /// command. This is the only public path that sets <c>_currentData</c>; doing it via
        /// the command keeps the test honest about the surrounding wiring.</summary>
        public async Task<MainWindowViewModel> BuildWithData(PlotMode mode)
        {
            var data = BuildSampleData();
            Dialog.Setup(d => d.ShowPlotModePickerAsync()).ReturnsAsync(mode);
            Files.Setup(f => f.ImportDataFileAsync())
                .ReturnsAsync(FileOperationResult.Success(data));

            var vm = Build();
            await vm.NewProjectCommand.ExecuteAsync(null);
            vm.HasData.Should().BeTrue("NewProject mock pipeline should have loaded sample data");
            return vm;
        }
    }

    // -- ManageCurves (stacked-mode CurveManagerDialog) ------------------------------------

    [Fact]
    public async Task ManageCurves_DialogReturnsVm_AppliesRemovalsAndVisibilityChanges()
    {
        var h = new Harness();
        var vm = h.Build();

        // Seed two curves directly; ManageCurves works against ActiveCurves and does not need _currentData.
        var keep = new CurveConfigurationModel { CurveName = "alt", PaneIndex = 0, IsVisible = true };
        var drop = new CurveConfigurationModel { CurveName = "speed", PaneIndex = 0, IsVisible = true };
        vm.ActiveCurves.Add(keep);
        vm.ActiveCurves.Add(drop);

        // Dialog VM: mark drop for removal, toggle keep invisible.
        var dialogVm = new CurveManagerDialogViewModel(vm.ActiveCurves);
        var dropItem = dialogVm.Curves.First(c => c.Configuration.Id == drop.Id);
        dropItem.IsMarkedForRemoval = true;
        var keepItem = dialogVm.Curves.First(c => c.Configuration.Id == keep.Id);
        keepItem.IsVisible = false;

        h.Dialog.Setup(d => d.ShowCurveManagerAsync(It.IsAny<ObservableCollection<CurveConfigurationModel>>()))
            .ReturnsAsync(dialogVm);

        await vm.ManageCurvesCommand.ExecuteAsync(null);

        vm.ActiveCurves.Should().ContainSingle().Which.Id.Should().Be(keep.Id);
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task ManageCurves_DialogCancelled_LeavesActiveCurvesUntouched()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.ActiveCurves.Add(new CurveConfigurationModel { CurveName = "alt", PaneIndex = 0 });
        vm.HasUnsavedChanges = false;

        h.Dialog.Setup(d => d.ShowCurveManagerAsync(It.IsAny<ObservableCollection<CurveConfigurationModel>>()))
            .ReturnsAsync((CurveManagerDialogViewModel?)null);

        await vm.ManageCurvesCommand.ExecuteAsync(null);

        vm.ActiveCurves.Should().HaveCount(1);
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    // -- ManageCompactCurves --------------------------------------------------------------

    [Fact]
    public async Task ManageCompactCurves_DeleteResult_RemovesCurveFromSurface()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.PlotMode = PlotMode.Compact;

        var curve = new CompactCurveModel { DisplayName = "alt" };
        vm.CompactPlot.AddCurve(curve);

        h.Dialog.Setup(d => d.ShowManageCompactCurveAsync(It.IsAny<ObservableCollection<CompactCurveModel>>()))
            .ReturnsAsync(new ManageCompactCurveResult(curve, DeleteRequested: true));

        await vm.ManageCompactCurvesCommand.ExecuteAsync(null);

        vm.CompactPlot.Curves.Should().BeEmpty();
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task ManageCompactCurves_UpdateResult_KeepsCurveAndMarksDirty()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.PlotMode = PlotMode.Compact;

        var curve = new CompactCurveModel { DisplayName = "alt", Color = "#000000" };
        vm.CompactPlot.AddCurve(curve);

        // Simulate the dialog mutating the curve in place before returning.
        curve.Color = "#FF0000";
        h.Dialog.Setup(d => d.ShowManageCompactCurveAsync(It.IsAny<ObservableCollection<CompactCurveModel>>()))
            .ReturnsAsync(new ManageCompactCurveResult(curve, DeleteRequested: false));

        await vm.ManageCompactCurvesCommand.ExecuteAsync(null);

        vm.CompactPlot.Curves.Should().ContainSingle();
        vm.CompactPlot.Curves[0].Color.Should().Be("#FF0000");
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task ManageCompactCurves_CancelledDialog_NoStateChange()
    {
        var h = new Harness();
        var vm = h.Build();
        vm.PlotMode = PlotMode.Compact;
        var curve = new CompactCurveModel { DisplayName = "alt" };
        vm.CompactPlot.AddCurve(curve);
        vm.HasUnsavedChanges = false;

        h.Dialog.Setup(d => d.ShowManageCompactCurveAsync(It.IsAny<ObservableCollection<CompactCurveModel>>()))
            .ReturnsAsync((ManageCompactCurveResult?)null);

        await vm.ManageCompactCurvesCommand.ExecuteAsync(null);

        vm.CompactPlot.Curves.Should().ContainSingle();
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    // -- AddCompactCurves -----------------------------------------------------------------

    [Fact]
    public async Task AddCompactCurves_DialogReturnsCurves_AppendsToCompactSurface()
    {
        var h = new Harness();
        var vm = await h.BuildWithData(PlotMode.Compact);

        var newCurves = new List<CompactCurveModel>
        {
            new() { SourceColumn = "alt", DisplayName = "alt" },
            new() { SourceColumn = "speed", DisplayName = "speed" },
        };
        h.Dialog.Setup(d => d.ShowAddCompactCurvesAsync(
                It.IsAny<PlotDataModel>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(newCurves);

        await vm.AddCompactCurvesCommand.ExecuteAsync(null);

        vm.CompactPlot.Curves.Should().HaveCount(2);
        vm.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public async Task AddCompactCurves_DialogCancelled_NoChange()
    {
        var h = new Harness();
        var vm = await h.BuildWithData(PlotMode.Compact);
        int before = vm.CompactPlot.Curves.Count;
        vm.HasUnsavedChanges = false;

        h.Dialog.Setup(d => d.ShowAddCompactCurvesAsync(
                It.IsAny<PlotDataModel>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync((IReadOnlyList<CompactCurveModel>?)null);

        await vm.AddCompactCurvesCommand.ExecuteAsync(null);

        vm.CompactPlot.Curves.Should().HaveCount(before);
        vm.HasUnsavedChanges.Should().BeFalse();
    }

    // -- AddCurves (stacked-mode AddCurvesDialog with callback) ---------------------------

    [Fact]
    public async Task AddCurvesToSpecificPane_DialogInvokesCallbackTwice_PlotsBothCurves()
    {
        var h = new Harness();

        // Service callback appends to ActiveCurves so we can assert from the test.
        h.Curve.Setup(c => c.PlotSingleCurveToPane(
                It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<PlotDataModel>(), It.IsAny<string>(),
                It.IsAny<ObservableCollection<PlotPaneViewModel>>(),
                It.IsAny<ObservableCollection<CurveConfigurationModel>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>()))
            .Callback<int, string, string, PlotDataModel, string,
                     ObservableCollection<PlotPaneViewModel>,
                     ObservableCollection<CurveConfigurationModel>, IReadOnlyList<string>, string?>(
                (paneIdx, parameter, axis, _, _, _, active, _, unit) =>
                {
                    active.Add(new CurveConfigurationModel
                    {
                        CurveName = parameter,
                        PaneIndex = paneIdx,
                        Unit = unit,
                    });
                });

        var vm = await h.BuildWithData(PlotMode.Panes);

        // Dialog mock invokes the supplied callback twice — same shape as the real dialog.
        h.Dialog.Setup(d => d.ShowAddCurvesAsync(
                It.IsAny<DataTable>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<Action<AddCurveRequest>>()))
            .Returns<DataTable, string, int, Action<AddCurveRequest>>(
                (_, _, _, cb) =>
                {
                    cb(new AddCurveRequest("alt", "Y1"));
                    cb(new AddCurveRequest("speed", "Y2"));
                    return Task.CompletedTask;
                });

        int activeBefore = vm.ActiveCurves.Count;
        await vm.AddCurvesToSpecificPane(0);

        vm.ActiveCurves.Should().HaveCount(activeBefore + 2);
        vm.ActiveCurves.Last().CurveName.Should().Be("speed");
    }

    // -- ResetToNewProject ----------------------------------------------------------------

    // P0: H3 fix — ResetToNewProject must drop both the chosen X column and the list of
    // candidate X columns derived from the previously-loaded data. Re-using a stale X
    // column against the new (empty) project was the bug.
    [Fact]
    public async Task ResetToNewProject_ClearsSelectedXColumnAndAvailableColumns()
    {
        var h = new Harness();
        var vm = await h.BuildWithData(PlotMode.Panes);
        vm.SelectedXColumn = "time";
        vm.AvailableXColumns.Add("time");
        vm.AvailableXColumns.Add("alt");

        vm.ResetToNewProject(PlotMode.Panes);

        vm.SelectedXColumn.Should().BeNull("the new project has no data so no X column is valid");
        vm.AvailableXColumns.Should().BeEmpty("AvailableXColumns must reflect the (empty) new project");
        vm.HasData.Should().BeFalse();
        vm.IsProjectActive.Should().BeFalse();
    }

    // -- OpenProject failure paths ---------------------------------------------------------

    // P0: L1 fix — a Failed load must surface as "Failed to open project.", NOT
    // "Load cancelled.". Masking failures as cancels destroys user trust.
    [Fact]
    public async Task OpenProject_LoadFails_StatusReportsFailureNotCancellation()
    {
        var h = new Harness();
        h.Files.Setup(f => f.LoadProjectFileAsync())
            .ReturnsAsync(FileOperationResult.Failed<(ProjectSettingsModel Project, string FilePath)>("disk on fire"));

        var vm = h.Build();
        vm.HasUnsavedChanges = false; // avoid the unsaved-changes prompt path

        await vm.OpenProjectCommand.ExecuteAsync(null);

        vm.StatusText.Should().Be("Failed to open project.");
    }

    // P1: the New Project flow now opens the file picker BEFORE wiping state. A cancel at
    // that picker must leave the previously-loaded data and HasData intact.
    [Fact]
    public async Task NewProject_CancelAtFilePicker_PreservesPreviousData()
    {
        var h = new Harness();
        var vm = await h.BuildWithData(PlotMode.Panes);

        bool hadDataBefore = vm.HasData;
        hadDataBefore.Should().BeTrue();

        // Second NewProject run: user picks a mode but cancels at the file picker.
        h.Dialog.Setup(d => d.ShowPlotModePickerAsync()).ReturnsAsync(PlotMode.Panes);
        h.Files.Setup(f => f.ImportDataFileAsync())
            .ReturnsAsync(FileOperationResult.Cancelled<PlotDataModel>());
        // Avoid the unsaved-changes prompt path; we already set state via BuildWithData.
        vm.HasUnsavedChanges = false;

        await vm.NewProjectCommand.ExecuteAsync(null);

        vm.HasData.Should().BeTrue("cancel at the file picker must NOT wipe the previously loaded data");
        vm.StatusText.Should().Be("New project cancelled.");
    }

    // P2: Loading a project with no saved PlotMode (legacy file) must prompt the user and
    // then mark the project dirty so the user is asked to save the upgraded field.
    [Fact]
    public async Task ApplyLoadedProject_LegacyPlotModeMissing_SetsHasUnsavedChangesTrue()
    {
        var h = new Harness();

        // Legacy project: PlotMode left null. Bare-minimum shape so ApplyLoadedProject
        // doesn't blow up trying to access PlotData / panes.
        var legacy = new ProjectSettingsModel
        {
            ProjectName = "Legacy",
            PlotMode = null,
        };
        h.Files.Setup(f => f.LoadProjectFileAsync())
            .ReturnsAsync(FileOperationResult.Success((legacy, "/tmp/legacy.dpx")));

        // User picks Panes when prompted to upgrade.
        h.Dialog.Setup(d => d.ShowPlotModePickerAsync()).ReturnsAsync(PlotMode.Panes);

        var vm = h.Build();
        vm.HasUnsavedChanges = false; // skip unsaved-changes prompt

        await vm.OpenProjectCommand.ExecuteAsync(null);

        vm.HasUnsavedChanges.Should().BeTrue(
            "legacy projects without a saved PlotMode must be marked dirty so the user is prompted to save the upgrade");
        vm.PlotMode.Should().Be(PlotMode.Panes);
    }
}
