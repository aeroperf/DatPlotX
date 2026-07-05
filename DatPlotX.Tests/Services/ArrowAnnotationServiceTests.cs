using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using System.Collections.ObjectModel;

namespace DatPlotX.Tests.Services;

/// <summary>
/// Tests for ArrowAnnotationService pure in-memory logic.
/// Uses empty pane collections to avoid ScottPlot/Avalonia dependencies.
/// </summary>
public class ArrowAnnotationServiceTests
{
    private static ObservableCollection<PlotPaneViewModel> NoPanes() => [];

    private static ArrowAnnotationModel MakeArrow(int paneIndex = 0) => new()
    {
        PaneIndex = paneIndex,
        BaseX = 1.0,
        BaseY = 2.0,
        TipX = 3.0,
        TipY = 4.0,
        Color = "#FF0000"
    };

    [Fact]
    public void Count_InitiallyZero()
    {
        new ArrowAnnotationService().Count.Should().Be(0);
    }

    [Fact]
    public void AddAnnotation_IncrementsCount()
    {
        var svc = new ArrowAnnotationService();
        svc.AddAnnotation(MakeArrow(), NoPanes());
        svc.Count.Should().Be(1);
    }

    [Fact]
    public void AddAnnotation_AssignsGuidIfEmpty()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow();
        model.Id = Guid.Empty;
        var id = svc.AddAnnotation(model, NoPanes());
        id.Should().NotBe(Guid.Empty);
        model.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AddAnnotation_PreservesExistingGuid()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow();
        var expected = Guid.NewGuid();
        model.Id = expected;
        var id = svc.AddAnnotation(model, NoPanes());
        id.Should().Be(expected);
    }

    [Fact]
    public void AddAnnotation_FiresOnAnnotationsChanged()
    {
        var svc = new ArrowAnnotationService();
        var fired = false;
        svc.OnAnnotationsChanged += () => fired = true;
        svc.AddAnnotation(MakeArrow(), NoPanes());
        fired.Should().BeTrue();
    }

    [Fact]
    public void GetAnnotation_ReturnsCorrectModel()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow();
        var id = svc.AddAnnotation(model, NoPanes());
        svc.GetAnnotation(id).Should().BeSameAs(model);
    }

    [Fact]
    public void GetAnnotation_UnknownId_ReturnsNull()
    {
        var svc = new ArrowAnnotationService();
        svc.GetAnnotation(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void RemoveAnnotation_ExistingId_ReturnsTrue_DecrementsCount()
    {
        var svc = new ArrowAnnotationService();
        var id = svc.AddAnnotation(MakeArrow(), NoPanes());
        svc.RemoveAnnotation(id, NoPanes()).Should().BeTrue();
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveAnnotation_UnknownId_ReturnsFalse()
    {
        var svc = new ArrowAnnotationService();
        svc.RemoveAnnotation(Guid.NewGuid(), NoPanes()).Should().BeFalse();
    }

    [Fact]
    public void UpdateBasePosition_UpdatesModel()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow();
        var id = svc.AddAnnotation(model, NoPanes());
        svc.UpdateBasePosition(id, 10.0, 20.0, NoPanes());
        model.BaseX.Should().Be(10.0);
        model.BaseY.Should().Be(20.0);
    }

    [Fact]
    public void UpdateBasePosition_UnknownId_DoesNotThrow()
    {
        var svc = new ArrowAnnotationService();
        var act = () => svc.UpdateBasePosition(Guid.NewGuid(), 1.0, 2.0, NoPanes());
        act.Should().NotThrow();
    }

    [Fact]
    public void UpdateTipPosition_UpdatesModel()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow();
        var id = svc.AddAnnotation(model, NoPanes());
        svc.UpdateTipPosition(id, 99.0, 88.0, NoPanes());
        model.TipX.Should().Be(99.0);
        model.TipY.Should().Be(88.0);
    }

    [Fact]
    public void MoveAnnotation_ShiftsAllCoordinates()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow(); // base(1,2) tip(3,4)
        var id = svc.AddAnnotation(model, NoPanes());
        svc.MoveAnnotation(id, 10.0, 5.0, NoPanes());
        model.BaseX.Should().Be(11.0);
        model.BaseY.Should().Be(7.0);
        model.TipX.Should().Be(13.0);
        model.TipY.Should().Be(9.0);
    }

    [Fact]
    public void GetAnnotationsForPane_FiltersCorrectly()
    {
        var svc = new ArrowAnnotationService();
        svc.AddAnnotation(MakeArrow(paneIndex: 0), NoPanes());
        svc.AddAnnotation(MakeArrow(paneIndex: 1), NoPanes());
        svc.AddAnnotation(MakeArrow(paneIndex: 0), NoPanes());

        svc.GetAnnotationsForPane(0).Should().HaveCount(2);
        svc.GetAnnotationsForPane(1).Should().HaveCount(1);
        svc.GetAnnotationsForPane(2).Should().BeEmpty();
    }

    [Fact]
    public void GetAllAnnotations_ReturnsAll()
    {
        var svc = new ArrowAnnotationService();
        svc.AddAnnotation(MakeArrow(), NoPanes());
        svc.AddAnnotation(MakeArrow(), NoPanes());
        svc.GetAllAnnotations().Should().HaveCount(2);
    }

    [Fact]
    public void SetVisibility_UpdatesModelFlag()
    {
        var svc = new ArrowAnnotationService();
        var model = MakeArrow();
        model.IsVisible = true;
        var id = svc.AddAnnotation(model, NoPanes());
        svc.SetVisibility(id, false, NoPanes());
        model.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ClearAllAnnotations_ResetsToZero()
    {
        var svc = new ArrowAnnotationService();
        svc.AddAnnotation(MakeArrow(), NoPanes());
        svc.AddAnnotation(MakeArrow(), NoPanes());
        svc.ClearAllAnnotations(NoPanes());
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void RestoreAnnotations_ReplacesExisting()
    {
        var svc = new ArrowAnnotationService();
        svc.AddAnnotation(MakeArrow(), NoPanes());

        var restored = new[] { MakeArrow(), MakeArrow(), MakeArrow() };
        svc.RestoreAnnotations(restored, NoPanes());

        svc.Count.Should().Be(3);
    }

    [Fact]
    public void UpdateAnnotation_UnknownId_DoesNotThrow()
    {
        var svc = new ArrowAnnotationService();
        var unknown = MakeArrow();
        unknown.Id = Guid.NewGuid();
        var act = () => svc.UpdateAnnotation(unknown, NoPanes());
        act.Should().NotThrow();
    }
}
