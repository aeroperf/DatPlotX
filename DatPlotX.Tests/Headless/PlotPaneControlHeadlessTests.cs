using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DatPlotX.Models;
using DatPlotX.ViewModels;
using DatPlotX.Views;
using FluentAssertions;

namespace DatPlotX.Tests.Headless;

/// <summary>
/// Sample headless UI test for the Stacked-mode pane control. Unlike the pure ViewModel tests, this
/// realizes the real <see cref="PlotPaneControl"/> (hosting a ScottPlot <c>AvaPlot</c>) inside a
/// headless window, so it exercises XAML loading, the control template, and the view↔VM wiring done
/// in <see cref="PlotPaneControl.SetViewModel"/> — none of which a VM-only test can reach.
///
/// <para>
/// Use <c>[AvaloniaFact]</c> / <c>[AvaloniaTheory]</c> (not plain <c>[Fact]</c>) so the test body
/// runs on the headless Avalonia dispatcher. The setup is registered by the assembly-level
/// <c>AvaloniaTestApplication</c> attribute in <see cref="HeadlessTestApplication"/>.
/// </para>
/// </summary>
public class PlotPaneControlHeadlessTests
{
    private static PlotPaneViewModel MakeViewModel()
        => new(new PlotPaneModel { Index = 0, Name = "Pane 1" });

    [AvaloniaFact]
    public void SetViewModel_WiresPlotModelAndDataContext()
    {
        var vm = MakeViewModel();
        var control = new PlotPaneControl();

        // Realize the control in a window so its template (and the hosted AvaPlot) applies.
        var window = new Window { Content = control, Width = 800, Height = 300 };
        window.Show();

        control.SetViewModel(vm);

        // SetViewModel assigns DataContext and hands the live ScottPlot.Plot to the VM.
        control.DataContext.Should().BeSameAs(vm);
        vm.PlotModel.Should().NotBeNull("SetViewModel wires avaPlot.Plot into the view model");
    }

    [AvaloniaFact]
    public void Control_LaysOut_WithRealBounds()
    {
        var vm = MakeViewModel();
        var control = new PlotPaneControl();
        var window = new Window { Content = control, Width = 800, Height = 300 };
        window.Show();
        control.SetViewModel(vm);

        // Force a layout pass on the headless surface (no Skia backend is configured, so we drive
        // measure/arrange rather than capturing pixels). Non-zero bounds prove the real control
        // template — ScottPlot AvaPlot included — realized and laid out headlessly without throwing.
        window.UpdateLayout();

        control.Bounds.Width.Should().BeGreaterThan(0);
        control.Bounds.Height.Should().BeGreaterThan(0);
    }
}
