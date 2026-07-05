using DatPlotX.Models.Analysis;
using DatPlotX.Services.Analysis;
using DatPlotX.Services.Units;
using DatPlotX.ViewModels;
using FluentAssertions;

namespace DatPlotX.Tests.ViewModels;

public class AnalysisPanelViewModelTests
{
    /// <summary>
    /// Spin up the real AnalysisService over a fake source so the panel VM exercises its full
    /// compute → ApplyResults → row/column build path. Returns once the first compute lands.
    /// </summary>
    private static async Task<AnalysisPanelViewModel> BuildPopulatedAsync()
    {
        var registry = new MetricRegistry();
        var source = new FakeSource();
        source.AddCurve("a", "Altitude", "ft", new double[] { 10, 20, 30, 40, 50 });
        source.AddCurve("b", "Speed", null, new double[] { 1, 2, 3, 4, 5 });
        source.SetVisibleRange(0, 4);

        var svc = new AnalysisService(registry, UnitRegistry.Default);
        svc.SetSource(source);

        var vm = new AnalysisPanelViewModel(svc, registry);
        // Enable metrics AFTER the VM subscribes so its ResultsChanged handler fires a compute.
        svc.SetEnabledMetrics(new[] { "max", "min" });
        await WaitForRowsAsync(vm, expected: 2);
        return vm;
    }

    private static async Task WaitForRowsAsync(AnalysisPanelViewModel vm, int expected)
    {
        for (int i = 0; i < 100 && vm.Rows.Count != expected; i++)
            await Task.Delay(10);
        vm.Rows.Should().HaveCount(expected, "the async compute should have populated the rows");
    }

    [Fact]
    public async Task BuildsRowsAndColumns_FromEnabledMetrics()
    {
        var vm = await BuildPopulatedAsync();

        vm.Columns.Select(c => c.MetricId).Should().Equal("max", "min");
        vm.Rows.Select(r => r.DisplayName).Should().BeEquivalentTo(new[] { "Altitude", "Speed" });
    }

    [Fact]
    public async Task CopyAsTsv_EmitsHeaderAndValues_WithUnits()
    {
        var vm = await BuildPopulatedAsync();
        string? tsv = null;
        vm.ClipboardRequested += (_, text) => tsv = text;

        vm.CopyAsTsvCommand.Execute(null);

        tsv.Should().NotBeNull();
        var lines = tsv!.TrimEnd().Split('\n');
        lines[0].Should().Be("Curve\tMax\tMin");
        // Altitude has a unit → values carry "ft"; Speed has none.
        tsv.Should().Contain("Altitude\t50 ft\t10 ft");
        tsv.Should().Contain("Speed\t5\t1");
    }

    [Fact]
    public async Task CopyAsMarkdown_EmitsTableWithSeparatorRow()
    {
        var vm = await BuildPopulatedAsync();
        string? md = null;
        vm.ClipboardRequested += (_, text) => md = text;

        vm.CopyAsMarkdownCommand.Execute(null);

        md.Should().NotBeNull();
        var lines = md!.TrimEnd().Split('\n');
        lines[0].Should().Be("| Curve | Max | Min |");
        lines[1].Should().Be("| --- | --- | --- |");
        md.Should().Contain("| Altitude | 50 ft | 10 ft |");
    }

    [Fact]
    public void NaNCell_RendersAsEmDash()
    {
        var result = new StatisticResult("a", Guid.Empty, "max", double.NaN, null, null, "ft", null);
        var cell = new AnalysisCellViewModel(result);

        cell.DisplayText("ft").Should().Be("—");
    }

    [Fact]
    public void Cell_WithDerivedRate_AppendsLabelInParens()
    {
        // The cell's own Units ("ft/s", the slope rate unit) is authoritative — the row-unit
        // argument is ignored when the cell carries one.
        var result = new StatisticResult("a", Guid.Empty, "slope", 1.0, null, null, "ft/s", "60 ft/min");
        var cell = new AnalysisCellViewModel(result);

        cell.DisplayText("ft").Should().Be("1 ft/s (60 ft/min)");
    }

    [Fact]
    public void Cell_Tooltip_ShowsR2AndInterceptWithRowUnit()
    {
        var extras = new Dictionary<string, double> { ["r2"] = 0.9876, ["intercept"] = 5840.2 };
        var result = new StatisticResult("a", Guid.Empty, "slope", 53.6, Units: "ft/s", Extras: extras);
        var cell = new AnalysisCellViewModel(result);

        // Intercept is a Y value, so it carries the row's base Y unit, not the slope rate unit.
        cell.Tooltip("ft").Should().Be("R² 0.9876 · intercept 5840.2 ft");
    }

    [Fact]
    public void Cell_Tooltip_NullWhenNoExtras()
    {
        var result = new StatisticResult("a", Guid.Empty, "max", 9.0, Units: "ft");
        var cell = new AnalysisCellViewModel(result);

        cell.Tooltip("ft").Should().BeNull();
    }

    [Fact]
    public void Cell_SlopeWithDroppedUnit_RendersBareNumber()
    {
        // X-axis unit unknown → service emits Units = "" (explicit "no unit"); the cell must NOT
        // fall back to the row's base unit.
        var result = new StatisticResult("a", Guid.Empty, "slope", 1.5, Units: "");
        var cell = new AnalysisCellViewModel(result);

        cell.DisplayText("ftMSL").Should().Be("1.5");
    }

    [Fact]
    public void LabelMode_Cycles_Off_Line_Number_LabelNumber_Off()
    {
        var cell = new AnalysisCellViewModel(
            new StatisticResult("a", Guid.Empty, "slope", -0.342, Line: MetricLine.Between(0, 0, 1, -0.342)));

        cell.LabelMode.Should().Be(StatLineLabelMode.Off);
        cell.ShowOnPlot.Should().BeFalse();

        cell.LabelMode = cell.NextLabelMode();
        cell.LabelMode.Should().Be(StatLineLabelMode.Line);
        cell.ShowOnPlot.Should().BeTrue();

        cell.LabelMode = cell.NextLabelMode();
        cell.LabelMode.Should().Be(StatLineLabelMode.LineNumber);

        cell.LabelMode = cell.NextLabelMode();
        cell.LabelMode.Should().Be(StatLineLabelMode.LineLabelNumber);

        cell.LabelMode = cell.NextLabelMode();
        cell.LabelMode.Should().Be(StatLineLabelMode.Off);
        cell.ShowOnPlot.Should().BeFalse();
    }

    [Fact]
    public void BuildLineLabel_EmptyForOffAndLine_ValueForNumber_LabelPlusValueForBoth()
    {
        var cell = new AnalysisCellViewModel(
            new StatisticResult("a", Guid.Empty, "slope", -0.342, Units: "deg",
                Line: MetricLine.Between(0, 0, 1, -0.342)));

        cell.LabelMode = StatLineLabelMode.Off;
        AnalysisPanelViewModel.BuildLineLabel("Slope", cell, "deg").Should().BeEmpty();

        cell.LabelMode = StatLineLabelMode.Line;
        AnalysisPanelViewModel.BuildLineLabel("Slope", cell, "deg").Should().BeEmpty();

        cell.LabelMode = StatLineLabelMode.LineNumber;
        AnalysisPanelViewModel.BuildLineLabel("Slope", cell, "deg").Should().Be("-0.342 deg");

        cell.LabelMode = StatLineLabelMode.LineLabelNumber;
        AnalysisPanelViewModel.BuildLineLabel("Slope", cell, "deg").Should().Be("Slope -0.342 deg");
    }

    [Theory]
    [InlineData(-0.0, "0")]   // negative zero normalizes to "0" (also covers positive zero)
    [InlineData(13.96, "13.96")]
    [InlineData(-9.219, "-9.219")]
    [InlineData(91.45, "91.45")]
    [InlineData(0.012, "0.012")]
    [InlineData(-0.0001234, "-0.000123")]   // small slope keeps significant digits, not "-0"
    [InlineData(-0.00004, "-0.00004")]
    [InlineData(0.0009, "0.0009")]
    public void FormatValue_KeepsSmallMagnitudePrecision_AndNormalizesNegativeZero(double value, string expected)
    {
        AnalysisCellViewModel.FormatValue(value).Should().Be(expected);
    }

    [Fact]
    public void BuildLineLabel_TinySlope_DoesNotShowNegativeZero()
    {
        var cell = new AnalysisCellViewModel(
            new StatisticResult("a", Guid.Empty, "slope", -0.0001234,
                Line: MetricLine.Between(0, 0.07, 622, -0.05)));

        cell.LabelMode = StatLineLabelMode.LineLabelNumber;
        AnalysisPanelViewModel.BuildLineLabel("Slope", cell, null).Should().Be("Slope -0.000123");
    }

    [Fact]
    public void BuildLineLabel_PrefersDerivedRateOverRawValue()
    {
        var cell = new AnalysisCellViewModel(
            new StatisticResult("a", Guid.Empty, "slope", -0.342, Units: "ft", DerivedRateLabel: "-20.5 ft/min",
                Line: MetricLine.Between(0, 0, 1, -0.342)));

        cell.LabelMode = StatLineLabelMode.LineLabelNumber;
        AnalysisPanelViewModel.BuildLineLabel("Slope", cell, "ft").Should().Be("Slope -20.5 ft/min");
    }

    // ----- Fake source (mirrors AnalysisServiceTests.FakeSource) -----
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

        public void NotifyCurvesChanged() => CurvesChanged?.Invoke(this, EventArgs.Empty);
        public void NotifyVisibleRangeChanged() => VisibleRangeChanged?.Invoke(this, EventArgs.Empty);

        public IReadOnlyList<AnalysisCurveDescriptor> ListCurves() => _descriptors;
        public AnalysisCurveData? GetData(string curveId) => _data.GetValueOrDefault(curveId);

        public void AddCurve(string id, string name, string? unit, double[] y, bool isVisible = true)
        {
            _descriptors.Add(new AnalysisCurveDescriptor(id, name, "#000000", unit, isVisible));
            _data[id] = new AnalysisCurveData(id, y, period: 1.0);
            FullDataXRange = (Math.Min(FullDataXRange.XMin, 0), Math.Max(FullDataXRange.XMax, y.Length - 1));
        }

        public void SetVisibleRange(double xMin, double xMax) => _visible = (xMin, xMax);
    }
}
