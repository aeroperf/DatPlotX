using DatPlotX.Models;
using DatPlotX.Services;
using FluentAssertions;

namespace DatPlotX.Tests.Services;

public class ProjectFileServiceTests : IDisposable
{
    private readonly List<string> _temp = new();

    public void Dispose()
    {
        foreach (var f in _temp) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
    }

    private string Temp(string ext = ".dpx")
    {
        var p = Path.Combine(Path.GetTempPath(), $"datplot_projfile_{Guid.NewGuid():N}{ext}");
        _temp.Add(p);
        return p;
    }

    [Fact]
    public void Ctor_NullSerializer_Throws()
    {
        var act = () => new ProjectFileService(null!, new ProjectCompressor());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullCompressor_Throws()
    {
        var act = () => new ProjectFileService(new ProjectSerializer(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateNewProject_HasSensibleDefaults()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("my-project");

        project.ProjectName.Should().Be("my-project");
        project.PaneCount.Should().Be(1);
        project.ShowGrid.Should().BeTrue();
        project.ShowLegend.Should().BeTrue();
    }

    [Fact]
    public async Task Save_Then_Load_RoundTripPreservesProjectName()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("round-trip");
        project.Curves.Add(new PlotCurveModel { Name = "gFx", Color = "#FF0000" });

        var path = Temp();
        await svc.SaveProjectAsync(project, path);
        var restored = await svc.LoadProjectAsync(path);

        restored.ProjectName.Should().Be("round-trip");
        restored.Curves.Should().HaveCount(1);
        restored.Curves[0].Name.Should().Be("gFx");
    }

    [Fact]
    public async Task Save_Then_Load_PreservesCompactEventLines()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("compact-events");
        project.PlotMode = PlotMode.Compact;
        project.CompactEventLines.Add(new EventLineModel
        {
            Label = "E1",
            XPosition = 12.5d,
            Color = "#FFB900",
        });
        project.CompactEventLines.Add(new EventLineModel
        {
            Label = "E2",
            XPosition = 50d,
            Color = "#00FF00",
        });

        var path = Temp();
        await svc.SaveProjectAsync(project, path);
        var restored = await svc.LoadProjectAsync(path);

        restored.CompactEventLines.Should().HaveCount(2);
        restored.CompactEventLines[0].Label.Should().Be("E1");
        restored.CompactEventLines[0].XPosition.Should().Be(12.5d);
        restored.CompactEventLines[1].Color.Should().Be("#00FF00");
    }

    [Fact]
    public async Task SaveProjectAsJsonAsync_WritesPlainTextJson()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("json-only");
        var path = Temp(".json");

        await svc.SaveProjectAsJsonAsync(project, path);
        var content = await File.ReadAllTextAsync(path);
        content.Should().StartWith("{");
        content.Should().Contain("json-only");
    }

    [Fact]
    public async Task LoadProjectFromJsonAsync_RoundTrip()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("raw-json");
        var path = Temp(".json");

        await svc.SaveProjectAsJsonAsync(project, path);
        var restored = await svc.LoadProjectFromJsonAsync(path);
        restored.ProjectName.Should().Be("raw-json");
    }

    [Fact]
    public async Task LoadProjectAsync_NotGzip_ThrowsNotSupported()
    {
        var svc = new ProjectFileService();
        var path = Temp();
        await File.WriteAllTextAsync(path, "{ not gzip }");

        var act = async () => await svc.LoadProjectAsync(path);
        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task ValidateProjectFileAsync_MissingFile_ReturnsFalse()
    {
        var svc = new ProjectFileService();
        var (valid, format, err) = await svc.ValidateProjectFileAsync("/tmp/_no_project_here_xyz.dpx");
        valid.Should().BeFalse();
        format.Should().BeNull();
        err.Should().Be("File not found");
    }

    [Fact]
    public async Task ValidateProjectFileAsync_ValidModernFile_ReturnsTrue()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("valid");
        var path = Temp();
        await svc.SaveProjectAsync(project, path);

        var (valid, format, _) = await svc.ValidateProjectFileAsync(path);
        valid.Should().BeTrue();
        format.Should().Be("Modern (JSON+GZip)");
    }

    [Fact]
    public async Task LoadProjectFromJsonAsync_WrapsNonGzipLegacyIntoSupportedError()
    {
        // An uncompressed JSON file is loadable via LoadProjectFromJsonAsync,
        // but LoadProjectAsync (gzip path) will reject it with NotSupportedException.
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("x");
        var path = Temp(".json");
        await svc.SaveProjectAsJsonAsync(project, path);

        var roundTripped = await svc.LoadProjectFromJsonAsync(path);
        roundTripped.ProjectName.Should().Be("x");
    }

    [Fact]
    public async Task ExportProjectSummaryAsync_ContainsAllSections()
    {
        var svc = new ProjectFileService();
        var project = svc.CreateNewProject("summary");
        project.Curves.Add(new PlotCurveModel { Name = "c", Color = "#000000" });
        project.EventLines.Add(new EventLineModel { Label = "e", XPosition = 1.0, Color = "#000000" });

        var path = Temp(".txt");
        await svc.ExportProjectSummaryAsync(project, path);

        var content = await File.ReadAllTextAsync(path);
        content.Should().Contain("PROJECT INFORMATION").And.Contain("PLOT SETTINGS")
            .And.Contain("CURVES").And.Contain("EVENT LINES");
    }
}
