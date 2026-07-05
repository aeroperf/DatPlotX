using DatPlotX.Models;
using DatPlotX.Services;
using DatPlotX.ViewModels;
using FluentAssertions;
using System.Collections.ObjectModel;

namespace DatPlotX.Tests.Services;

/// <summary>
/// Tests for TextAnnotationService pure in-memory logic.
/// Uses empty pane collections to avoid ScottPlot/Avalonia dependencies.
/// </summary>
public class TextAnnotationServiceTests
{
    private static ObservableCollection<PlotPaneViewModel> NoPanes() => [];

    private static TextAnnotationModel MakeText(int paneIndex = 0) => new()
    {
        PaneIndex = paneIndex,
        Text = "Hello",
        X = 1.0,
        Y = 2.0,
        IsVisible = true
    };

    [Fact]
    public void Count_InitiallyZero()
    {
        new TextAnnotationService().Count.Should().Be(0);
    }

    [Fact]
    public void AddAnnotation_IncrementsCount()
    {
        var svc = new TextAnnotationService();
        svc.AddAnnotation(MakeText(), NoPanes());
        svc.Count.Should().Be(1);
    }

    [Fact]
    public void AddAnnotation_AssignsGuidIfEmpty()
    {
        var svc = new TextAnnotationService();
        var model = MakeText();
        model.Id = Guid.Empty;
        var id = svc.AddAnnotation(model, NoPanes());
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void AddAnnotation_FiresOnAnnotationsChanged()
    {
        var svc = new TextAnnotationService();
        var fired = false;
        svc.OnAnnotationsChanged += () => fired = true;
        svc.AddAnnotation(MakeText(), NoPanes());
        fired.Should().BeTrue();
    }

    [Fact]
    public void GetAnnotation_ReturnsCorrectModel()
    {
        var svc = new TextAnnotationService();
        var model = MakeText();
        var id = svc.AddAnnotation(model, NoPanes());
        svc.GetAnnotation(id).Should().BeSameAs(model);
    }

    [Fact]
    public void GetAnnotation_UnknownId_ReturnsNull()
    {
        new TextAnnotationService().GetAnnotation(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void RemoveAnnotation_ExistingId_ReturnsTrue()
    {
        var svc = new TextAnnotationService();
        var id = svc.AddAnnotation(MakeText(), NoPanes());
        svc.RemoveAnnotation(id, NoPanes()).Should().BeTrue();
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void RemoveAnnotation_UnknownId_ReturnsFalse()
    {
        new TextAnnotationService().RemoveAnnotation(Guid.NewGuid(), NoPanes()).Should().BeFalse();
    }

    [Fact]
    public void UpdatePosition_UpdatesModelCoordinates()
    {
        var svc = new TextAnnotationService();
        var model = MakeText();
        var id = svc.AddAnnotation(model, NoPanes());
        svc.UpdatePosition(id, 99.0, 88.0, NoPanes());
        model.X.Should().Be(99.0);
        model.Y.Should().Be(88.0);
    }

    [Fact]
    public void UpdatePosition_UnknownId_DoesNotThrow()
    {
        var svc = new TextAnnotationService();
        var act = () => svc.UpdatePosition(Guid.NewGuid(), 1.0, 2.0, NoPanes());
        act.Should().NotThrow();
    }

    [Fact]
    public void GetAnnotationsForPane_FiltersCorrectly()
    {
        var svc = new TextAnnotationService();
        svc.AddAnnotation(MakeText(0), NoPanes());
        svc.AddAnnotation(MakeText(1), NoPanes());
        svc.AddAnnotation(MakeText(0), NoPanes());

        svc.GetAnnotationsForPane(0).Should().HaveCount(2);
        svc.GetAnnotationsForPane(1).Should().HaveCount(1);
        svc.GetAnnotationsForPane(9).Should().BeEmpty();
    }

    [Fact]
    public void GetAllAnnotations_ReturnsAll()
    {
        var svc = new TextAnnotationService();
        svc.AddAnnotation(MakeText(), NoPanes());
        svc.AddAnnotation(MakeText(), NoPanes());
        svc.GetAllAnnotations().Should().HaveCount(2);
    }

    [Fact]
    public void SetVisibility_UpdatesFlag()
    {
        var svc = new TextAnnotationService();
        var model = MakeText();
        model.IsVisible = true;
        var id = svc.AddAnnotation(model, NoPanes());
        svc.SetVisibility(id, false, NoPanes());
        model.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void ClearAllAnnotations_ResetsToZero()
    {
        var svc = new TextAnnotationService();
        svc.AddAnnotation(MakeText(), NoPanes());
        svc.AddAnnotation(MakeText(), NoPanes());
        svc.ClearAllAnnotations(NoPanes());
        svc.Count.Should().Be(0);
    }

    [Fact]
    public void RestoreAnnotations_ReplacesExisting()
    {
        var svc = new TextAnnotationService();
        svc.AddAnnotation(MakeText(), NoPanes());
        svc.RestoreAnnotations([MakeText(), MakeText()], NoPanes());
        svc.Count.Should().Be(2);
    }
}
