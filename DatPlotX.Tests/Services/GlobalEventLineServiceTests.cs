using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using System.Collections.ObjectModel;

namespace DatPlotX.Tests.Services;

/// <summary>
/// Tests for GlobalEventLineService pure in-memory logic.
/// Uses empty pane collections to avoid ScottPlot/Avalonia dependencies.
/// </summary>
public class GlobalEventLineServiceTests
{
    private static ObservableCollection<PlotPaneViewModel> NoPanes() => [];

    [Fact]
    public void Count_InitiallyZero()
    {
        new GlobalEventLineService().Count.Should().Be(0);
    }

    [Fact]
    public void AddGlobalEventLine_IncrementsCount()
    {
        var svc = new GlobalEventLineService();
        svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        svc.Count.Should().Be(1);
    }

    [Fact]
    public void AddGlobalEventLine_ReturnsNonEmptyGuid()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AddGlobalEventLine_FiresOnEventLinesChanged()
    {
        var svc = new GlobalEventLineService();
        var fired = false;
        svc.OnEventLinesChanged += () => fired = true;
        svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        fired.Should().BeTrue();
    }

    [Fact]
    public void AddGlobalEventLine_StoresCorrectXPosition()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(3.14, "E1", NoPanes());
        svc.GetEventLineById(id)!.XPosition.Should().Be(3.14);
    }

    [Fact]
    public void AddGlobalEventLine_StoresCorrectLabel()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(0.0, "MyLabel", NoPanes());
        svc.GetEventLineById(id)!.Label.Should().Be("MyLabel");
    }

    [Fact]
    public void AddGlobalEventLine_DefaultColor_Applied()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(0.0, "E1", NoPanes());
        svc.GetEventLineById(id)!.Color.Should().Be("#FFB900");
    }

    [Fact]
    public void AddGlobalEventLine_CustomColor_Applied()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(0.0, "E1", NoPanes(), "#FF0000");
        svc.GetEventLineById(id)!.Color.Should().Be("#FF0000");
    }

    [Fact]
    public void RemoveGlobalEventLine_ExistingId_ReturnsTrue_DecrementsCount()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        svc.RemoveGlobalEventLine(id, NoPanes()).Should().BeTrue();
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveGlobalEventLine_UnknownId_ReturnsFalse()
    {
        var svc = new GlobalEventLineService();
        svc.RemoveGlobalEventLine(Guid.NewGuid(), NoPanes()).Should().BeFalse();
    }

    [Fact]
    public void RemoveGlobalEventLine_FiresOnEventLinesChanged()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        var fired = false;
        svc.OnEventLinesChanged += () => fired = true;
        svc.RemoveGlobalEventLine(id, NoPanes());
        fired.Should().BeTrue();
    }

    [Fact]
    public void MoveGlobalEventLine_UpdatesXPosition()
    {
        var svc = new GlobalEventLineService();
        var id = svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        svc.MoveGlobalEventLine(id, 5.5, NoPanes());
        svc.GetEventLineById(id)!.XPosition.Should().Be(5.5);
    }

    [Fact]
    public void MoveGlobalEventLine_UnknownId_DoesNotThrow()
    {
        var svc = new GlobalEventLineService();
        var act = () => svc.MoveGlobalEventLine(Guid.NewGuid(), 5.0, NoPanes());
        act.Should().NotThrow();
    }

    [Fact]
    public void ClearAllGlobalEventLines_ResetsCountToZero()
    {
        var svc = new GlobalEventLineService();
        svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        svc.AddGlobalEventLine(2.0, "E2", NoPanes());
        svc.ClearAllGlobalEventLines(NoPanes());
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void ClearAllGlobalEventLines_ReturnsEmptyList_Afterwards()
    {
        var svc = new GlobalEventLineService();
        svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        svc.ClearAllGlobalEventLines(NoPanes());
        svc.GetGlobalEventLines().Should().BeEmpty();
    }

    [Fact]
    public void GetGlobalEventLines_ReturnsAllAdded()
    {
        var svc = new GlobalEventLineService();
        svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        svc.AddGlobalEventLine(2.0, "E2", NoPanes());
        svc.GetGlobalEventLines().Should().HaveCount(2);
    }

    [Fact]
    public void GetEventLineById_NotFound_ReturnsNull()
    {
        var svc = new GlobalEventLineService();
        svc.GetEventLineById(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void GenerateDefaultLabel_IncrementsSequentially()
    {
        var svc = new GlobalEventLineService();
        svc.GenerateDefaultLabel().Should().Be("E1");
        svc.GenerateDefaultLabel().Should().Be("E2");
        svc.GenerateDefaultLabel().Should().Be("E3");
    }

    [Fact]
    public void RestoreGlobalEventLines_RestoresCorrectCount()
    {
        var svc = new GlobalEventLineService();
        var lines = new[]
        {
            new EventLineModel { IsGlobal = true, XPosition = 1.0, Label = "E1", Id = Guid.NewGuid() },
            new EventLineModel { IsGlobal = true, XPosition = 2.0, Label = "E2", Id = Guid.NewGuid() },
            new EventLineModel { IsGlobal = false, XPosition = 3.0, Label = "local", Id = Guid.NewGuid() }
        };

        svc.RestoreGlobalEventLines(lines, NoPanes());

        // Only global ones restored
        svc.Count.Should().Be(2);
    }

    [Fact]
    public void RestoreGlobalEventLines_UpdatesLabelCounter()
    {
        var svc = new GlobalEventLineService();
        var lines = new[]
        {
            new EventLineModel { IsGlobal = true, Label = "E5", Id = Guid.NewGuid() }
        };
        svc.RestoreGlobalEventLines(lines, NoPanes());
        // Next generated label should continue after E5
        svc.GenerateDefaultLabel().Should().Be("E6");
    }

    [Fact]
    public void UpdateLabelVisibility_EmptyPanes_DoesNotThrow()
    {
        var svc = new GlobalEventLineService();
        svc.AddGlobalEventLine(1.0, "E1", NoPanes());
        var act = () => svc.UpdateLabelVisibility(NoPanes());
        act.Should().NotThrow();
    }
}
