using DatPlotX.Services.Analysis;
using DatPlotX.ViewModels;
using FluentAssertions;

namespace DatPlotX.Tests.ViewModels;

public class ManageMetricsDialogViewModelTests
{
    private static readonly IReadOnlyList<IMetricDefinition> AllMetrics = new MetricRegistry().All;

    [Fact]
    public void Seeds_EnabledFirstInOrder_ThenRemainder()
    {
        var vm = new ManageMetricsDialogViewModel(AllMetrics, new[] { "slope", "max" });

        // First two rows are the enabled ones, in the given order, both checked.
        vm.Rows[0].Id.Should().Be("slope");
        vm.Rows[1].Id.Should().Be("max");
        vm.Rows[0].IsEnabled.Should().BeTrue();
        vm.Rows[1].IsEnabled.Should().BeTrue();

        // Every registry metric appears exactly once; the rest are unchecked.
        vm.Rows.Should().HaveCount(AllMetrics.Count);
        vm.Rows.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        vm.Rows.Skip(2).Should().OnlyContain(r => !r.IsEnabled);
    }

    [Fact]
    public void EnabledIds_ReflectChecksInRowOrder()
    {
        var vm = new ManageMetricsDialogViewModel(AllMetrics, new[] { "max", "min" });

        // Enable a third metric (currently unchecked) and disable one of the seeded ones.
        vm.Rows.First(r => r.Id == "integral").IsEnabled = true;
        vm.Rows.First(r => r.Id == "min").IsEnabled = false;

        // "max" stays first (row order unchanged); "integral" keeps its row position after the others.
        vm.EnabledIds.Should().Equal("max", "integral");
    }

    [Fact]
    public void MoveDown_ReordersEnabledColumns()
    {
        var vm = new ManageMetricsDialogViewModel(AllMetrics, new[] { "max", "min", "mean" });
        vm.SelectedRow = vm.Rows[0];   // "max"

        vm.MoveDownCommand.Execute(null);

        vm.EnabledIds.Take(3).Should().Equal("min", "max", "mean");
        vm.SelectedRow!.Id.Should().Be("max");   // selection follows the moved row
    }

    [Fact]
    public void HasAnyEnabled_FalseWhenAllUnchecked()
    {
        var vm = new ManageMetricsDialogViewModel(AllMetrics, new[] { "max" });
        vm.HasAnyEnabled.Should().BeTrue();

        vm.Rows.First(r => r.Id == "max").IsEnabled = false;
        vm.OnEnabledChanged();

        vm.HasAnyEnabled.Should().BeFalse();
        vm.EnabledIds.Should().BeEmpty();
    }

    [Fact]
    public void MoveUp_CannotPushPastTop()
    {
        var vm = new ManageMetricsDialogViewModel(AllMetrics, new[] { "max", "min" });
        vm.SelectedRow = vm.Rows[0];

        vm.MoveUpCommand.CanExecute(null).Should().BeFalse();
    }
}
