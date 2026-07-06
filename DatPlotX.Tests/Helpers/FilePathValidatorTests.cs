using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class FilePathValidatorTests
{
    [Fact]
    public void ValidateAndNormalizePath_EmptyString_Throws()
    {
        var act = () => FilePathValidator.ValidateAndNormalizePath("");
        act.Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public void ValidateAndNormalizePath_WhitespaceOnly_Throws()
    {
        var act = () => FilePathValidator.ValidateAndNormalizePath("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateAndNormalizePath_PathTraversal_ThrowsSecurityException()
    {
        var act = () => FilePathValidator.ValidateAndNormalizePath("/tmp/../etc/passwd");
        act.Should().Throw<System.Security.SecurityException>().WithMessage("*traversal*");
    }

    [Fact]
    public void ValidateAndNormalizePath_HomeExpansion_ThrowsSecurityException()
    {
        var act = () => FilePathValidator.ValidateAndNormalizePath("~/documents/file.csv");
        act.Should().Throw<System.Security.SecurityException>().WithMessage("*traversal*");
    }

    [Fact]
    public void ValidateAndNormalizePath_ValidPath_ReturnsAbsolutePath()
    {
        var result = FilePathValidator.ValidateAndNormalizePath("/tmp/testfile.csv");
        result.Should().Be(Path.GetFullPath("/tmp/testfile.csv"));
    }

    [Fact]
    public void ValidateAndNormalizePath_MustExist_FileNotFound_Throws()
    {
        var act = () => FilePathValidator.ValidateAndNormalizePath("/tmp/doesnotexist_datplot_test.csv", mustExist: true);
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ValidateAndNormalizePath_MustExist_FileExists_ReturnsPath()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = FilePathValidator.ValidateAndNormalizePath(tempFile, mustExist: true);
            result.Should().Be(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidatePathForLoad_FileNotFound_Throws()
    {
        var act = () => FilePathValidator.ValidatePathForLoad("/tmp/notexist_datplot.csv");
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ValidatePathForSave_ValidPath_CreatesMissingDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"datplot_test_{Guid.NewGuid()}");
        var filePath = Path.Combine(tempDir, "file.dpx");
        try
        {
            var result = FilePathValidator.ValidatePathForSave(filePath);
            result.Should().Be(filePath);
            Directory.Exists(tempDir).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ValidatePathForSave_TraversalPath_Throws()
    {
        var act = () => FilePathValidator.ValidatePathForSave("/tmp/../tmp/evil.dpx");
        act.Should().Throw<System.Security.SecurityException>();
    }
}
