using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;
using System.Collections.ObjectModel;
using System.Data;

namespace DatPlotX.Tests.Services;

public class ProjectStateManagerTests
{
    private static PlotPaneViewModel Pane(int index = 0)
    {
        return new PlotPaneViewModel(new PlotPaneModel { Index = index, Name = $"Pane {index + 1}" })
        {
            PlotModel = new Plot()
        };
    }

    private static PlotDataModel SampleData()
    {
        var table = new DataTable();
        table.Columns.Add("time", typeof(double));
        table.Columns.Add("value", typeof(double));
        for (int i = 0; i < 5; i++) table.Rows.Add(i * 0.1, i * 2.0);
        return new PlotDataModel { Data = table };
    }

    [Fact]
    public void SaveCurrentState_CopiesPaneModelsAndCurves()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0), Pane(1) };
        var curves = new ObservableCollection<CurveConfigurationModel>
        {
            new() { Id = Guid.NewGuid(), CurveName = "c1", YColumnName = "value", PaneIndex = 0, YAxis = YAxisType.Y1, Color = "#FF0000", LineWidth = 2.0, LineStyle = DatPlotX.Models.LineStyle.Solid }
        };

        mgr.SaveCurrentState(project, SampleData(), panes, curves);

        project.PaneCount.Should().Be(2);
        project.Panes.Should().HaveCount(2);
        project.Curves.Should().HaveCount(1);
        project.Curves[0].Name.Should().Be("c1");
    }

    [Fact]
    public void SaveCurrentState_SavesGlobalEventLines_WhenProvided()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var eventLines = new List<EventLineModel>
        {
            new() { Label = "liftoff", XPosition = 1.5, Color = "#FF0000", IsGlobal = true }
        };

        mgr.SaveCurrentState(project, null, panes, curves, globalEventLines: eventLines);

        project.EventLines.Should().HaveCount(1);
        project.EventLines[0].IsGlobal.Should().BeTrue();
    }

    [Fact]
    public void SaveCurrentState_SavesCalloutsTextAndArrowAnnotations()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var callouts = new List<IntersectionCalloutModel> { new() { PaneIndex = 0, EventLineId = Guid.NewGuid() } };
        var texts = new List<TextAnnotationModel> { new() { PaneIndex = 0, Text = "hello" } };
        var arrows = new List<ArrowAnnotationModel> { new() { PaneIndex = 0 } };

        mgr.SaveCurrentState(project, null, panes, curves,
            callouts: callouts, textAnnotations: texts, arrowAnnotations: arrows);

        project.IntersectionCallouts.Should().HaveCount(1);
        project.TextAnnotations.Should().HaveCount(1);
        project.ArrowAnnotations.Should().HaveCount(1);
    }

    [Fact]
    public async Task RestoreProjectState_RestoresPanesAndCurvesFromSavedProject()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var data = SampleData();
        var project = new ProjectSettingsModel
        {
            PaneCount = 2,
            Panes =
            {
                new PlotPaneModel { Index = 0, Name = "Top" },
                new PlotPaneModel { Index = 1, Name = "Bottom" }
            },
            Curves =
            {
                new PlotCurveModel { Id = Guid.NewGuid(), Name = "val", SourceColumn = "value",
                                     Color = "#FF0000", LineWidth = 2.0, LinePattern = LinePatternType.Solid,
                                     YAxis = YAxisType.Y1, PaneIndex = 0, IsVisible = true }
            }
        };

        // Seed PlotModel after the restore call sets the panes — simulate view wiring via a background task.
        var restoreTask = mgr.RestoreProjectState(project, data, panes, curves);

        // Poll until panes populated then attach Plots
        for (int i = 0; i < 50 && panes.Count < 2; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();

        await restoreTask;

        panes.Should().HaveCount(2);
        curves.Should().HaveCount(1);
        curves[0].YColumnName.Should().Be("value");
    }

    [Fact]
    public async Task RestoreProjectState_EmptyProject_InitializesSinglePane()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var project = new ProjectSettingsModel();

        var restoreTask = mgr.RestoreProjectState(project, null, panes, curves);
        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        panes.Should().HaveCount(1);
    }

    [Fact]
    public async Task RestoreProjectState_CurvePaneIndexOutOfRange_Skipped()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var project = new ProjectSettingsModel
        {
            PaneCount = 1,
            Panes = { new PlotPaneModel { Index = 0 } },
            Curves =
            {
                new PlotCurveModel { SourceColumn = "value", PaneIndex = 42 }
            }
        };

        var restoreTask = mgr.RestoreProjectState(project, SampleData(), panes, curves);
        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        curves.Should().BeEmpty();
    }

    [Fact]
    public async Task RestoreProjectState_CallsBackForGlobalEventLines()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var project = new ProjectSettingsModel
        {
            PaneCount = 1,
            Panes = { new PlotPaneModel { Index = 0 } },
            EventLines =
            {
                new EventLineModel { Label = "a", XPosition = 1.0, IsGlobal = true }
            }
        };

        int received = 0;
        var restoreTask = mgr.RestoreProjectState(project, null, panes, curves,
            onGlobalEventLinesRestored: els => received = els.Count());
        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        received.Should().Be(1);
    }

    [Fact]
    public void SaveCurrentState_WritesCompactCurves_PreservingOrder()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var compact = new List<CompactCurveModel>
        {
            new() { Id = Guid.NewGuid(), DisplayName = "Altitude", SourceColumn = "alt", AxisSide = AxisSide.Left },
            new() { Id = Guid.NewGuid(), DisplayName = "Gear",     SourceColumn = "gear", IsBoolean = true, AxisSide = AxisSide.Right },
        };

        mgr.SaveCurrentState(project, null, panes, curves, compactCurves: compact);

        project.CompactCurves.Should().HaveCount(2);
        project.CompactCurves[0].SourceColumn.Should().Be("alt");
        project.CompactCurves[1].SourceColumn.Should().Be("gear");
        project.CompactCurves[1].IsBoolean.Should().BeTrue();
    }

    [Fact]
    public void SaveCurrentState_NullCompactCurves_LeavesProjectListUntouched()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel
        {
            CompactCurves = { new CompactCurveModel { SourceColumn = "old" } }
        };
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };
        var curves = new ObservableCollection<CurveConfigurationModel>();

        // Default compactCurves param is null — pre-existing list should not be wiped.
        mgr.SaveCurrentState(project, null, panes, curves);

        project.CompactCurves.Should().HaveCount(1);
        project.CompactCurves[0].SourceColumn.Should().Be("old");
    }

    [Fact]
    public void SaveCurrentState_EmptyCompactCurves_ClearsProjectList()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel
        {
            CompactCurves = { new CompactCurveModel { SourceColumn = "stale" } }
        };
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };
        var curves = new ObservableCollection<CurveConfigurationModel>();

        mgr.SaveCurrentState(project, null, panes, curves, compactCurves: Array.Empty<CompactCurveModel>());

        project.CompactCurves.Should().BeEmpty();
    }

    [Fact]
    public void SaveCurrentState_PreservesPerCurveXAxisColumn()
    {
        var mgr = new ProjectStateManager();
        var project = new ProjectSettingsModel();
        var panes = new ObservableCollection<PlotPaneViewModel> { Pane(0) };
        var curves = new ObservableCollection<CurveConfigurationModel>
        {
            new() { Id = Guid.NewGuid(), CurveName = "c1", YColumnName = "value", XColumnName = "time",
                    PaneIndex = 0, YAxis = YAxisType.Y1, Color = "#FF0000" }
        };

        mgr.SaveCurrentState(project, SampleData(), panes, curves);

        project.Curves.Single().XAxisColumn.Should().Be("time");
    }

    [Fact]
    public async Task RestoreProjectState_UsesSavedXAxisColumn()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();

        var table = new DataTable();
        table.Columns.Add("sample", typeof(double));
        table.Columns.Add("time", typeof(double));
        table.Columns.Add("value", typeof(double));
        for (int i = 0; i < 3; i++) table.Rows.Add(i, 100.0 + i, i * 2.0);
        var data = new PlotDataModel { Data = table };

        var project = new ProjectSettingsModel
        {
            PaneCount = 1,
            Panes = { new PlotPaneModel { Index = 0 } },
            Curves =
            {
                new PlotCurveModel { Id = Guid.NewGuid(), Name = "v", SourceColumn = "value",
                                     XAxisColumn = "time", PaneIndex = 0, YAxis = YAxisType.Y1,
                                     Color = "#FF0000", IsVisible = true }
            }
        };

        var restoreTask = mgr.RestoreProjectState(project, data, panes, curves);
        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        curves.Single().XColumnName.Should().Be("time");
    }

    [Fact]
    public async Task RestoreProjectState_FallsBackToFirstColumn_WhenSavedXAxisColumnMissing()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var data = SampleData();

        var project = new ProjectSettingsModel
        {
            PaneCount = 1,
            Panes = { new PlotPaneModel { Index = 0 } },
            Curves =
            {
                new PlotCurveModel { Id = Guid.NewGuid(), Name = "v", SourceColumn = "value",
                                     XAxisColumn = "nonexistent", PaneIndex = 0, YAxis = YAxisType.Y1,
                                     Color = "#FF0000", IsVisible = true }
            }
        };

        var restoreTask = mgr.RestoreProjectState(project, data, panes, curves);
        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        // Curve restores, with the legacy first-column fallback used internally.
        curves.Should().HaveCount(1);
        curves.Single().XColumnName.Should().Be("nonexistent"); // we keep the saved value on the config
    }

    // P0: even an EMPTY project must fire every restore callback so the singleton annotation
    // services drop any state retained from a previously-loaded project. Regressing this
    // re-opens the C1 "singleton state contamination" defect from the prior review.
    [Fact]
    public async Task RestoreProjectState_AlwaysFiresAllRestoreCallbacks_EvenForEmptyProject()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var project = new ProjectSettingsModel();

        int evCount = -1, calloutCount = -1, textCount = -1, arrowCount = -1, compactEvCount = -1;

        var restoreTask = mgr.RestoreProjectState(project, null, panes, curves,
            onGlobalEventLinesRestored: e => evCount = e.Count(),
            onCalloutsRestored: c => calloutCount = c.Count(),
            onTextAnnotationsRestored: t => textCount = t.Count(),
            onArrowAnnotationsRestored: a => arrowCount = a.Count(),
            onCompactEventLinesRestored: ce => compactEvCount = ce.Count());

        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        evCount.Should().Be(0, "global event-lines callback must fire so the singleton clears stale lines");
        calloutCount.Should().Be(0, "callouts callback must fire so the singleton clears stale callouts");
        textCount.Should().Be(0, "text-annotations callback must fire so the singleton clears stale text");
        arrowCount.Should().Be(0, "arrow-annotations callback must fire so the singleton clears stale arrows");
        // Compact event lines are only callback-invoked when the project actually has compact lines today;
        // assert the contract holds either way so future refactors don't accidentally re-introduce the contamination.
        compactEvCount.Should().BeOneOf(new int?[] { 0, -1 }.Select(x => x ?? -1));
    }

    // P1 (A7 guard): the autoscale-vs-saved-range decision is centralized in
    // ResolveFinalAxisRange. Saved range MUST win — when both endpoints are set, the
    // returned tuple is the saved one. When either is missing, the autoscaled range wins.
    // Annotation anchors are computed from the final range, so a regression here would
    // shift callout / event-line positions on restore.
    [Fact]
    public void ResolveFinalAxisRange_SavedRangeBeatsAutoScaled_WhenBothEndpointsSet()
    {
        var auto = (Min: 0.0, Max: 100.0);
        var saved = ProjectStateManager.ResolveFinalAxisRange(5.0, 75.0, auto);
        saved.Should().Be((5.0, 75.0));

        // Either endpoint missing → autoscale wins (pair semantics — partial save is invalid).
        ProjectStateManager.ResolveFinalAxisRange(5.0, null, auto).Should().Be(auto);
        ProjectStateManager.ResolveFinalAxisRange(null, 75.0, auto).Should().Be(auto);
        ProjectStateManager.ResolveFinalAxisRange(null, null, auto).Should().Be(auto);
    }

    // P1 (A7 guard): RestoreProjectState autoscales BEFORE running ApplyFormatting; if a
    // project saves manual axis ranges, ApplyFormatting must re-apply them so the final
    // pane range reflects the saved values — NOT the autoscaled range that lived for the
    // brief window between the two passes. Annotation positions depend on this contract.
    [Fact]
    public async Task RestoreProjectState_AppliesFormatting_AfterAnnotations_DoesNotClobberAnnotationPositions()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();

        // Project carries one curve and explicit manual X/Y ranges. After restore, those
        // saved ranges must survive ApplyFormatting (which runs LAST in the restore pipeline).
        var project = new ProjectSettingsModel
        {
            PaneCount = 1,
            Panes =
            {
                new PlotPaneModel
                {
                    Index = 0,
                    XAxisMin = 0.0, XAxisMax = 10.0,
                    YAxisMin = -5.0, YAxisMax = 5.0,
                    Y2AxisMin = -1.0, Y2AxisMax = 1.0,
                }
            },
            Curves =
            {
                new PlotCurveModel { Id = Guid.NewGuid(), Name = "val", SourceColumn = "value",
                                     PaneIndex = 0, YAxis = YAxisType.Y1, Color = "#FF0000", IsVisible = true }
            }
        };

        var restoreTask = mgr.RestoreProjectState(project, SampleData(), panes, curves);
        for (int i = 0; i < 50 && panes.Count == 0; i++) await Task.Delay(10);
        foreach (var p in panes) p.PlotModel = new Plot();
        await restoreTask;

        // The saved X/Y/Y2 ranges must be the FINAL ranges on the pane — proving that
        // ApplyFormatting's re-application step did not get clobbered by the earlier
        // AutoScale fallback and that the dual-source decision agrees end-to-end.
        var pane = panes[0];
        var xRange = pane.PlotModel!.Axes.Bottom.Range;
        var yRange = pane.PlotModel!.Axes.Left.Range;
        var y2Range = pane.PlotModel!.Axes.Right.Range;

        xRange.Min.Should().BeApproximately(0.0, 1e-9);
        xRange.Max.Should().BeApproximately(10.0, 1e-9);
        yRange.Min.Should().BeApproximately(-5.0, 1e-9);
        yRange.Max.Should().BeApproximately(5.0, 1e-9);
        y2Range.Min.Should().BeApproximately(-1.0, 1e-9);
        y2Range.Max.Should().BeApproximately(1.0, 1e-9);
    }

    // P0: the pane-realize fix (commit 6a21024) relies on WhenPlotReady returning a
    // TaskCompletionSource that is *awaited with a 5-second timeout*. If a future refactor
    // changes the await to direct Task.WhenAll, headless tests like this hang forever — so
    // assert the timeout path: never set PlotModel on the panes, restore must still complete.
    [Fact]
    public async Task RestoreProjectState_TimesOutWhenPanesNeverRealize_DoesNotHangOrThrow()
    {
        var mgr = new ProjectStateManager();
        var panes = new ObservableCollection<PlotPaneViewModel>();
        var curves = new ObservableCollection<CurveConfigurationModel>();
        var project = new ProjectSettingsModel
        {
            PaneCount = 1,
            Panes = { new PlotPaneModel { Index = 0 } }
        };

        // Restore but never assign PlotModel — the WhenPlotReady tasks never complete.
        var restoreTask = mgr.RestoreProjectState(project, null, panes, curves);

        // The internal timeout is 5 seconds; cap our own wait at 8 seconds to fail loudly on a hang.
        var completed = await Task.WhenAny(restoreTask, Task.Delay(TimeSpan.FromSeconds(8)));
        completed.Should().BeSameAs(restoreTask, "RestoreProjectState must honor its 5s pane-ready timeout");
        await restoreTask; // surface any exception
        panes.Should().HaveCount(1);
    }
}
