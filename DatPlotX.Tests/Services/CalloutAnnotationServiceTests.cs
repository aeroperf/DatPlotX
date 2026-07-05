using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using ScottPlot;
using System.Collections.ObjectModel;

namespace DatPlotX.Tests.Services;

public class CalloutAnnotationServiceTests
{
    private static ObservableCollection<PlotPaneViewModel> NoPanes() => new();

    private static PlotPaneViewModel PaneWithCurve(int index = 0, string curveName = "value")
    {
        var pane = new PlotPaneViewModel(new PlotPaneModel { Index = index })
        {
            PlotModel = new Plot()
        };
        var config = new CurveConfigurationModel
        {
            CurveName = curveName,
            YColumnName = curveName,
            PaneIndex = index,
            YAxis = YAxisType.Y1,
            Color = "#FF0000",
            IsVisible = true
        };
        pane.AddScatterCurve([0.0, 1.0, 2.0], [10.0, 20.0, 30.0], config);
        pane.AutoScale();
        return pane;
    }

    [Fact]
    public void CreateCalloutsForEventLine_EmptyPanes_AddsNoCallouts()
    {
        var service = new CalloutAnnotationService();
        service.CreateCalloutsForEventLine(Guid.NewGuid(), 1.0, NoPanes());
        service.Count.Should().Be(0);
    }

    [Fact]
    public void CreateCalloutsForEventLine_WithCurves_CreatesOnePerCurvePerPane()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        var eventId = Guid.NewGuid();

        service.CreateCalloutsForEventLine(eventId, 1.0, panes);

        service.Count.Should().Be(1);
        service.GetCalloutsForEventLine(eventId).Should().HaveCount(1);
    }

    [Fact]
    public void RemoveCalloutsForEventLine_RemovesOnlyMatchingEvent()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        service.CreateCalloutsForEventLine(id1, 1.0, panes);
        service.CreateCalloutsForEventLine(id2, 1.5, panes);

        service.RemoveCalloutsForEventLine(id1, panes);

        service.GetCalloutsForEventLine(id1).Should().BeEmpty();
        service.GetCalloutsForEventLine(id2).Should().HaveCount(1);
    }

    [Fact]
    public void UpdateCalloutOffset_UpdatesModelFields()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        var id = Guid.NewGuid();
        service.CreateCalloutsForEventLine(id, 1.0, panes);
        var callout = service.GetCalloutsForEventLine(id)[0];

        service.UpdateCalloutOffset(callout.Id, 42.0, -3.14);

        callout.OffsetX.Should().Be(42.0);
        callout.OffsetY.Should().Be(-3.14);
    }

    [Fact]
    public void UpdateCalloutOffset_UnknownId_DoesNothing()
    {
        var service = new CalloutAnnotationService();
        var act = () => service.UpdateCalloutOffset(Guid.NewGuid(), 1.0, 1.0);
        act.Should().NotThrow();
        service.Count.Should().Be(0);
    }

    [Fact]
    public void OnCalloutsChanged_FiresOnCreateUpdateRemove()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        int calls = 0;
        service.OnCalloutsChanged += () => calls++;

        var id = Guid.NewGuid();
        service.CreateCalloutsForEventLine(id, 1.0, panes);
        var callout = service.GetCalloutsForEventLine(id)[0];
        service.UpdateCalloutOffset(callout.Id, 0.1, 0.2);
        service.RemoveCalloutsForEventLine(id, panes);

        calls.Should().Be(3);
    }

    [Fact]
    public void ClearAllCallouts_RemovesEverything()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        service.CreateCalloutsForEventLine(Guid.NewGuid(), 0.5, panes);
        service.CreateCalloutsForEventLine(Guid.NewGuid(), 1.5, panes);

        service.ClearAllCallouts(panes);

        service.Count.Should().Be(0);
    }

    [Fact]
    public void RestoreCallouts_ReplacesExistingCallouts()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        service.CreateCalloutsForEventLine(Guid.NewGuid(), 1.0, panes);

        var restored = new List<IntersectionCalloutModel>
        {
            new() { Id = Guid.NewGuid(), PaneIndex = 0, CurveName = "value", XPosition = 0.5, YValue = 15.0, IsVisible = true }
        };
        service.RestoreCallouts(restored, panes);

        service.Count.Should().Be(1);
        service.GetCalloutModels()[0].XPosition.Should().Be(0.5);
    }

    [Fact]
    public void SetCalloutVisibility_TogglesIsVisibleFlag()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        var id = Guid.NewGuid();
        service.CreateCalloutsForEventLine(id, 1.0, panes);
        var callout = service.GetCalloutsForEventLine(id)[0];

        service.SetCalloutVisibility(callout.Id, false, panes);
        callout.IsVisible.Should().BeFalse();

        service.SetCalloutVisibility(callout.Id, true, panes);
        callout.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void UpdateCalloutsForEventLine_RecalculatesYAtNewX()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        var id = Guid.NewGuid();
        service.CreateCalloutsForEventLine(id, 0.0, panes);
        var first = service.GetCalloutsForEventLine(id)[0];
        double yAtZero = first.YValue;

        service.UpdateCalloutsForEventLine(id, 2.0, panes);
        first.XPosition.Should().Be(2.0);
        first.YValue.Should().NotBe(yAtZero);
    }

    [Fact]
    public void GetCalloutById_ReturnsMatchOrNull()
    {
        var service = new CalloutAnnotationService();
        var panes = new ObservableCollection<PlotPaneViewModel> { PaneWithCurve() };
        var id = Guid.NewGuid();
        service.CreateCalloutsForEventLine(id, 1.0, panes);
        var callout = service.GetCalloutsForEventLine(id)[0];

        service.GetCalloutById(callout.Id).Should().Be(callout);
        service.GetCalloutById(Guid.NewGuid()).Should().BeNull();
    }
}
