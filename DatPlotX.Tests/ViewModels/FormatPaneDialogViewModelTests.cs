using DatPlotX.Models;
using DatPlotX.ViewModels;
using FluentAssertions;

namespace DatPlotX.Tests.ViewModels;

public class FormatPaneDialogViewModelTests
{
    [Fact]
    public void Validate_InvertedX_Fails()
    {
        var vm = new FormatPaneDialogViewModel { XAutoScale = false, XMinText = "10", XMaxText = "1" };
        vm.Validate().Should().NotBeNull().And.Contain("X axis");
    }

    [Fact]
    public void Validate_InvertedY1_Fails()
    {
        var vm = new FormatPaneDialogViewModel { Y1AutoScale = false, Y1MinText = "5", Y1MaxText = "5" };
        vm.Validate().Should().NotBeNull().And.Contain("Y1");
    }

    [Fact]
    public void Validate_ProperRange_Passes()
    {
        var vm = new FormatPaneDialogViewModel { XAutoScale = false, XMinText = "0", XMaxText = "100" };
        vm.Validate().Should().BeNull();
    }

    [Fact]
    public void Validate_OneSidedBound_Passes()
    {
        // Only a min: the other end fills from the live range at apply time, so it is not "inverted".
        var vm = new FormatPaneDialogViewModel { XAutoScale = false, XMinText = "0", XMaxText = "" };
        vm.Validate().Should().BeNull();
    }

    [Fact]
    public void Validate_AutoScale_IgnoresRangeText()
    {
        var vm = new FormatPaneDialogViewModel { XAutoScale = true, XMinText = "10", XMaxText = "1" };
        vm.Validate().Should().BeNull();
    }

    [Fact]
    public void ApplyTo_OneSidedMin_SetsOnlyThatBound()
    {
        var model = new PlotPaneModel();
        var vm = new FormatPaneDialogViewModel { XAutoScale = false, XMinText = "5", XMaxText = "" };
        vm.ApplyTo(model);

        model.XAxisMin.Should().Be(5.0);
        model.XAxisMax.Should().BeNull();
    }

    [Fact]
    public void LoadFrom_ReadsEventLineLabelSettings()
    {
        var model = new PlotPaneModel { ShowEventLineXValue = false, EventLineXValueDecimals = 4 };
        var vm = new FormatPaneDialogViewModel();
        vm.LoadFrom(model);

        vm.ShowEventLineXValue.Should().BeFalse();
        vm.EventLineXValueDecimals.Should().Be("4");
    }

    [Fact]
    public void ApplyTo_WritesEventLineLabelSettings()
    {
        var model = new PlotPaneModel();
        var vm = new FormatPaneDialogViewModel { ShowEventLineXValue = false, EventLineXValueDecimals = "3" };
        vm.ApplyTo(model);

        model.ShowEventLineXValue.Should().BeFalse();
        model.EventLineXValueDecimals.Should().Be(3);
    }

    [Fact]
    public void EventLineLabelPreview_TracksDecimalsAndToggle()
    {
        var vm = new FormatPaneDialogViewModel { EventLineXValueDecimals = "3" };
        vm.EventLineLabelPreview.Should().Contain("218.171");

        vm.ShowEventLineXValue = false;
        vm.EventLineLabelPreview.Should().NotContain("218");
    }
}
