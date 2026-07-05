using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using DatPlotX.Services.Units;
using FluentAssertions;

namespace DatPlotX.Tests.Services.Analysis;

public class AnalysisServiceTests
{
    private static AnalysisService NewService(out FakeSource source)
    {
        source = new FakeSource();
        var svc = new AnalysisService(new MetricRegistry(), UnitRegistry.Default);
        svc.SetSource(source);
        return svc;
    }

    [Fact]
    public async Task ComputeActive_ReturnsOneRow_PerCurvePerMetric()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", "ft", y: new double[] { 1, 2, 3, 4, 5 });
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "max", "min" });

        var results = await svc.ComputeActiveAsync();

        results.Should().HaveCount(2);    // 1 curve × 2 metrics
        results.Should().Contain(r => r.MetricId == "max" && r.Value == 5);
        results.Should().Contain(r => r.MetricId == "min" && r.Value == 1);
    }

    [Fact]
    public async Task VisibleWindow_NarrowsRange()
    {
        var svc = NewService(out var source);
        // Period = 1, so X[i] = i
        source.AddCurve("a", "A", null, y: new double[] { 10, 20, 30, 40, 50 });
        source.SetVisibleRange(1, 3);   // covers samples at X = 1, 2, 3
        source.RaiseVisibleRangeChanged();
        svc.SetEnabledMetrics(new[] { "max", "min" });

        var results = await svc.ComputeActiveAsync();

        results.First(r => r.MetricId == "max").Value.Should().Be(40);
        results.First(r => r.MetricId == "min").Value.Should().Be(20);
    }

    [Fact]
    public async Task Slope_WithKnownUnits_GetsDerivedRateLabel()
    {
        var svc = NewService(out var source);
        source.XUnit = "s";
        source.AddCurve("alt", "Altitude", "ft", y: new double[] { 0, 1, 2, 3, 4 });  // slope = 1 ft/s
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "slope" });

        var results = await svc.ComputeActiveAsync();
        var slope = results.Single();

        slope.Value.Should().BeApproximately(1, 1e-9);
        slope.Units.Should().Be("ft/s");                 // raw slope unit is Y-per-X
        slope.DerivedRateLabel.Should().NotBeNull();
        slope.DerivedRateLabel.Should().Contain("ft/min");
        slope.DerivedRateLabel.Should().Contain("60");   // 1 ft/s × 60 = 60 ft/min
    }

    [Fact]
    public async Task Slope_XPlaneAltitude_ftMSL_over_time_GetsRateOfClimb()
    {
        // Mirrors the real X-Plane case: Y unit "ftMSL", X unit "time" (seconds).
        var svc = NewService(out var source);
        source.XUnit = "time";
        source.AddCurve("alt", "p-alt", "ftMSL", y: new double[] { 0, 2, 4, 6, 8 }); // 2 ftMSL/s
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "slope" });

        var slope = (await svc.ComputeActiveAsync()).Single();

        slope.Units.Should().Be("ftMSL/s");              // denominator normalized "time"→"s"
        slope.DerivedRateLabel.Should().Contain("ft/min");
        slope.DerivedRateLabel.Should().Contain("120");  // 2 ftMSL/s × 60 = 120 ft/min
    }

    [Fact]
    public async Task Slope_Speed_kt_over_s_ShowsAccelerationUnit_NoDerivedRate()
    {
        // Speed slope is acceleration (kt/s). There's no canonical "nicer" rate for it,
        // so the unit is shown verbatim and no derived label is produced.
        var svc = NewService(out var source);
        source.XUnit = "s";
        source.AddCurve("v", "Vind", "kt", y: new double[] { 100, 101, 102, 103, 104 });
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "slope" });

        var slope = (await svc.ComputeActiveAsync()).Single();

        slope.Units.Should().Be("kt/s");
        slope.DerivedRateLabel.Should().BeNull();
    }

    [Fact]
    public async Task Slope_WithoutUnits_NoDerivedRate()
    {
        var svc = NewService(out var source);
        source.XUnit = null;
        source.AddCurve("alt", "Altitude", null, y: new double[] { 0, 1, 2, 3, 4 });
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "slope" });

        var slope = (await svc.ComputeActiveAsync()).Single();

        slope.DerivedRateLabel.Should().BeNull();
        slope.Units.Should().BeEmpty();   // X unit unknown → unit deliberately dropped (not the base "ft")
    }

    [Fact]
    public async Task ToleranceBand_StoredPerCurve_AndComputed()
    {
        var svc = NewService(out var source);
        // y = 8..12 over X = 0..4; band centered 10 ± 1 ⇒ [9,11]; samples 8,9,10,11,12.
        source.AddCurve("a", "A", "ft", y: new double[] { 8, 9, 10, 11, 12 });
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();

        svc.GetToleranceBand("a").Should().BeNull();
        svc.SetToleranceBand(new ToleranceBand("a", BandCenterMode.UserNominal, 10, 1, ToleranceMode.Absolute, BandScope.ActiveSegment));
        svc.GetToleranceBand("a").Should().NotBeNull();
        svc.ToleranceBands.Should().HaveCount(1);

        var evals = await svc.ComputeToleranceBandsAsync();
        evals.Should().HaveCount(1);
        var e = evals[0];
        e.Result.Lower.Should().Be(9);
        e.Result.Upper.Should().Be(11);
        e.Result.LimitCrossings.Should().Be(2); // leaves below at start, re-enters, leaves above at end
        e.DisplayName.Should().Be("A");
        e.Units.Should().Be("ft");
    }

    [Fact]
    public async Task ToleranceBand_DerivedMeanCenter_UsesScopeSlice()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", null, y: new double[] { 0, 10, 20 }); // mean 10
        source.SetVisibleRange(0, 2);
        source.RaiseCurvesChanged();

        svc.SetToleranceBand(new ToleranceBand("a", BandCenterMode.Mean, 0, 5, ToleranceMode.Absolute, BandScope.ActiveSegment));
        var e = (await svc.ComputeToleranceBandsAsync()).Single();
        e.Result.Center.Should().Be(10);
        e.Result.Lower.Should().Be(5);
        e.Result.Upper.Should().Be(15);
    }

    [Fact]
    public async Task RemoveToleranceBand_DropsIt()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", null, y: new double[] { 1, 2, 3 });
        source.SetVisibleRange(0, 2);
        source.RaiseCurvesChanged();

        svc.SetToleranceBand(new ToleranceBand("a", BandCenterMode.UserNominal, 2, 1, ToleranceMode.Absolute, BandScope.ActiveSegment));
        svc.RemoveToleranceBand("a");
        svc.GetToleranceBand("a").Should().BeNull();
        (await svc.ComputeToleranceBandsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task ToleranceBand_WholeCurveScope_UsesFullRange()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", null, y: new double[] { 100, 0, 0, 0, 100 });
        source.SetVisibleRange(1, 3);   // visible window misses the spikes at the ends
        source.RaiseVisibleRangeChanged();

        svc.SetToleranceBand(new ToleranceBand("a", BandCenterMode.UserNominal, 0, 1, ToleranceMode.Absolute, BandScope.WholeCurve));
        var e = (await svc.ComputeToleranceBandsAsync()).Single();
        // Whole-curve scope sees the 100 spikes ⇒ excursion of +100 beyond the upper limit (1).
        e.Result.MaxExcursion.Should().Be(99);
    }

    [Fact]
    public async Task DefineSegment_AppearsInList_AndComputable()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", null, y: new double[] { 10, 20, 30, 40, 50 });
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();

        var seg = new AnalysisSegment(Guid.NewGuid(), "Climb", 1, 3, AnalysisSegmentSource.Manual);
        svc.DefineSegment(seg);
        svc.SetActiveSegment(seg.Id);
        svc.SetEnabledMetrics(new[] { "max" });

        svc.Segments.Should().Contain(s => s.Id == seg.Id);
        svc.ActiveSegment.Id.Should().Be(seg.Id);

        var max = (await svc.ComputeActiveAsync()).Single();
        max.Value.Should().Be(40);
    }

    [Fact]
    public void RemoveSegment_RemovesAndResetsActive()
    {
        var svc = NewService(out _);
        var seg = new AnalysisSegment(Guid.NewGuid(), "x", 0, 1, AnalysisSegmentSource.Manual);
        svc.DefineSegment(seg);
        svc.SetActiveSegment(seg.Id);

        svc.RemoveSegment(seg.Id);

        svc.Segments.Should().NotContain(s => s.Id == seg.Id);
        svc.ActiveSegment.Source.Should().Be(AnalysisSegmentSource.VisibleWindow);
    }

    [Fact]
    public void RemoveSegment_VisibleWindowIsProtected()
    {
        var svc = NewService(out _);
        var visibleId = svc.Segments[0].Id;
        svc.RemoveSegment(visibleId);
        svc.Segments.Should().Contain(s => s.Id == visibleId);
    }

    [Fact]
    public void DefineSegment_VisibleWindowSource_Throws()
    {
        var svc = NewService(out _);
        var act = () => svc.DefineSegment(
            new AnalysisSegment(Guid.NewGuid(), "x", 0, 1, AnalysisSegmentSource.VisibleWindow));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RestoreSegments_ReplacesManualSegments_KeepsVisibleWindow_AndSetsActive()
    {
        var svc = NewService(out _);
        // A stale manual segment that must be cleared by restore.
        svc.DefineSegment(new AnalysisSegment(Guid.NewGuid(), "stale", 0, 1, AnalysisSegmentSource.Manual));

        var a = new AnalysisSegment(Guid.NewGuid(), "A", 1, 2, AnalysisSegmentSource.Manual);
        var b = new AnalysisSegment(Guid.NewGuid(), "B", 3, 4, AnalysisSegmentSource.Manual);
        // A visible-window entry in the input must be ignored (it's implicit).
        var vw = AnalysisSegment.VisibleWindow(0, 99);

        svc.RestoreSegments(new[] { a, b, vw }, b.Id);

        svc.Segments.Count(s => s.Source == AnalysisSegmentSource.VisibleWindow).Should().Be(1);
        svc.Segments.Should().Contain(s => s.Id == a.Id);
        svc.Segments.Should().Contain(s => s.Id == b.Id);
        svc.Segments.Should().NotContain(s => s.Name == "stale");
        svc.ActiveSegment.Id.Should().Be(b.Id);
    }

    [Fact]
    public void RestoreSegments_NullActive_FallsBackToVisibleWindow()
    {
        var svc = NewService(out _);
        var a = new AnalysisSegment(Guid.NewGuid(), "A", 1, 2, AnalysisSegmentSource.Manual);

        svc.RestoreSegments(new[] { a }, activeId: null);

        svc.ActiveSegment.Source.Should().Be(AnalysisSegmentSource.VisibleWindow);
    }

    [Fact]
    public void ResultsChanged_FiresOnEnableMetricsChange()
    {
        var svc = NewService(out _);
        int count = 0;
        svc.ResultsChanged += (_, _) => count++;
        svc.SetEnabledMetrics(new[] { "max" });
        count.Should().Be(1);
    }

    [Fact]
    public async Task HiddenCurves_Excluded()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", null, new double[] { 1, 2, 3 }, isVisible: true);
        source.AddCurve("b", "B", null, new double[] { 100, 200, 300 }, isVisible: false);
        source.SetVisibleRange(0, 2);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "max" });

        var results = await svc.ComputeActiveAsync();
        results.Should().HaveCount(1);
        results.Single().CurveId.Should().Be("a");
    }

    [Fact]
    public void EventLinePair_ActiveSegmentRange_ResolvesFromLiveEventLines()
    {
        var svc = NewService(out _);
        var start = Guid.NewGuid();
        var end = Guid.NewGuid();
        var positions = new Dictionary<Guid, double> { [start] = 30, [end] = 10 };
        svc.EventLineResolver = id => positions.TryGetValue(id, out var x) ? x : null;

        // Stored range is stale (0,0); resolver should win and sort the bounds.
        var seg = new AnalysisSegment(Guid.NewGuid(), "pair", 0, 0,
            AnalysisSegmentSource.EventLinePair, StartEventId: start, EndEventId: end);
        svc.DefineSegment(seg);
        svc.SetActiveSegment(seg.Id);

        svc.ActiveSegmentRange.Should().Be((10.0, 30.0));

        // Move a line — range tracks it.
        positions[end] = 50;
        svc.ActiveSegmentRange.Should().Be((30.0, 50.0));
    }

    [Fact]
    public void EventLinePair_FallsBackToStoredRange_WhenLineMissing()
    {
        var svc = NewService(out _);
        var start = Guid.NewGuid();
        var end = Guid.NewGuid();
        // Resolver knows start but not end → fall back to the stored range.
        svc.EventLineResolver = id => id == start ? 5.0 : (double?)null;

        var seg = new AnalysisSegment(Guid.NewGuid(), "pair", 2, 8,
            AnalysisSegmentSource.EventLinePair, StartEventId: start, EndEventId: end);
        svc.DefineSegment(seg);
        svc.SetActiveSegment(seg.Id);

        svc.ActiveSegmentRange.Should().Be((2.0, 8.0));
    }

    [Fact]
    public void SyncEventLinePairRanges_UpdatesStoredRange_ForAllPairSegments_NotJustActive()
    {
        var svc = NewService(out _);
        var aStart = Guid.NewGuid(); var aEnd = Guid.NewGuid();
        var bStart = Guid.NewGuid(); var bEnd = Guid.NewGuid();
        var positions = new Dictionary<Guid, double>
        {
            [aStart] = 0,
            [aEnd] = 10,
            [bStart] = 20,
            [bEnd] = 30,
        };
        svc.EventLineResolver = id => positions.TryGetValue(id, out var x) ? x : null;

        var segA = new AnalysisSegment(Guid.NewGuid(), "A", 0, 10,
            AnalysisSegmentSource.EventLinePair, StartEventId: aStart, EndEventId: aEnd);
        var segB = new AnalysisSegment(Guid.NewGuid(), "B", 20, 30,
            AnalysisSegmentSource.EventLinePair, StartEventId: bStart, EndEventId: bEnd);
        svc.DefineSegment(segA);
        svc.DefineSegment(segB);
        svc.SetActiveSegment(segA.Id);  // A is active, B is not

        // Move B's boundary while A stays active.
        positions[bEnd] = 99;
        bool raised = false;
        svc.ResultsChanged += (_, _) => raised = true;

        svc.SyncEventLinePairRanges();

        raised.Should().BeTrue();
        var storedB = svc.Segments.First(s => s.Id == segB.Id);
        storedB.XMin.Should().Be(20);
        storedB.XMax.Should().Be(99);   // stale stored range was refreshed even though B isn't active
    }

    [Fact]
    public void SyncEventLinePairRanges_NoChange_DoesNotRaiseResultsChanged()
    {
        var svc = NewService(out _);
        var start = Guid.NewGuid(); var end = Guid.NewGuid();
        var positions = new Dictionary<Guid, double> { [start] = 1, [end] = 5 };
        svc.EventLineResolver = id => positions.TryGetValue(id, out var x) ? x : null;
        var seg = new AnalysisSegment(Guid.NewGuid(), "p", 1, 5,
            AnalysisSegmentSource.EventLinePair, StartEventId: start, EndEventId: end);
        svc.DefineSegment(seg);

        bool raised = false;
        svc.ResultsChanged += (_, _) => raised = true;
        svc.SyncEventLinePairRanges();   // ranges already match → no-op

        raised.Should().BeFalse();
    }

    [Fact]
    public async Task Slope_Extras_FlowThroughToStatisticResult()
    {
        // R²/intercept are computed by SlopeMetric but were historically dropped when the service
        // built StatisticResult. Guard the passthrough so the panel tooltip has data to show.
        var svc = NewService(out var source);
        source.XUnit = "s";
        source.AddCurve("a", "A", "ft", y: new double[] { 5, 8, 11, 14, 17 }); // y = 3x + 5, perfect fit
        source.SetVisibleRange(0, 4);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "slope" });

        var slope = (await svc.ComputeActiveAsync()).Single();

        slope.Extras.Should().NotBeNull();
        slope.Extras!["r2"].Should().BeApproximately(1.0, 1e-9);
        slope.Extras!["intercept"].Should().BeApproximately(5, 1e-9);
    }

    [Fact]
    public async Task Max_HasNoExtras()
    {
        var svc = NewService(out var source);
        source.AddCurve("a", "A", "ft", y: new double[] { 1, 2, 3 });
        source.SetVisibleRange(0, 2);
        source.RaiseCurvesChanged();
        svc.SetEnabledMetrics(new[] { "max" });

        var max = (await svc.ComputeActiveAsync()).Single();

        max.Extras.Should().BeNull();
    }

    [Fact]
    public void SetEnabledMetrics_KeepsOrder_DropsUnknown_Dedupes()
    {
        var svc = NewService(out _);

        svc.SetEnabledMetrics(new[] { "slope", "bogus", "max", "slope" });

        svc.EnabledMetricIds.Should().Equal("slope", "max");   // order preserved, unknown + dup dropped
    }

    [Fact]
    public void AllMetrics_ExposesEighteenIncludingIntegralAndPercentiles()
    {
        var svc = NewService(out _);

        svc.AllMetrics.Should().HaveCount(18);
        svc.AllMetrics.Select(m => m.Id).Should().Contain(new[] { "integral", "median", "rms", "p95" });
    }

    // ----- Fake source -----
    private sealed class FakeSource : IAnalysisCurveSource
    {
        private readonly List<AnalysisCurveDescriptor> _descriptors = new();
        private readonly Dictionary<string, AnalysisCurveData> _data = new();
        private (double, double) _visible = (0, 0);

        public string? XUnit { get; set; }
        public (double XMin, double XMax) VisibleXRange => _visible;
        public (double XMin, double XMax) FullDataXRange { get; private set; } = (0, 0);

        public event EventHandler? CurvesChanged;
        public event EventHandler? VisibleRangeChanged;

        public IReadOnlyList<AnalysisCurveDescriptor> ListCurves() => _descriptors;
        public AnalysisCurveData? GetData(string curveId) => _data.GetValueOrDefault(curveId);

        public void AddCurve(string id, string name, string? unit, double[] y, bool isVisible = true)
        {
            _descriptors.Add(new AnalysisCurveDescriptor(id, name, "#000000", unit, isVisible));
            _data[id] = new AnalysisCurveData(id, y, period: 1.0);

            double fullMin = Math.Min(FullDataXRange.XMin, 0);
            double fullMax = Math.Max(FullDataXRange.XMax, y.Length - 1);
            FullDataXRange = (fullMin, fullMax);
        }

        public void SetVisibleRange(double xMin, double xMax) => _visible = (xMin, xMax);
        public void RaiseCurvesChanged() => CurvesChanged?.Invoke(this, EventArgs.Empty);
        public void RaiseVisibleRangeChanged() => VisibleRangeChanged?.Invoke(this, EventArgs.Empty);
        public void NotifyCurvesChanged() => RaiseCurvesChanged();
        public void NotifyVisibleRangeChanged() => RaiseVisibleRangeChanged();
    }
}
