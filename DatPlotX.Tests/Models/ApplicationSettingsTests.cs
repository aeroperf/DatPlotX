using DatPlotX.Models;
using FluentAssertions;

namespace DatPlotX.Tests.Models;

public class ApplicationSettingsTests
{
    [Fact]
    public void DefaultMaxFileSizeBytes_Is1GB()
    {
        new ApplicationSettings().MaxFileSizeBytes.Should().Be(1024L * 1024 * 1024);
    }

    [Fact]
    public void DefaultMaxRowCount_Is10Million()
    {
        new ApplicationSettings().MaxRowCount.Should().Be(10_000_000);
    }

    [Fact]
    public void DefaultMaxColumnCount_Is5000()
    {
        new ApplicationSettings().MaxColumnCount.Should().Be(5000);
    }

    [Fact]
    public void ResetToDefaults_RestoresAllValues()
    {
        var s = new ApplicationSettings { MaxFileSizeBytes = 1, MaxRowCount = 1 };
        s.ResetToDefaults();
        s.MaxFileSizeBytes.Should().Be(1024L * 1024 * 1024);
        s.MaxRowCount.Should().Be(10_000_000);
    }

    [Fact]
    public void Validate_FileSizeBelowMin_ClampsTo1MB()
    {
        var s = new ApplicationSettings { MaxFileSizeBytes = 100 };
        s.Validate();
        s.MaxFileSizeBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void Validate_RowCountBelowMin_ClampsTo1000()
    {
        var s = new ApplicationSettings { MaxRowCount = 10 };
        s.Validate();
        s.MaxRowCount.Should().Be(1000);
    }

    [Fact]
    public void Validate_ColumnCountBelowMin_ClampsTo10()
    {
        var s = new ApplicationSettings { MaxColumnCount = 1 };
        s.Validate();
        s.MaxColumnCount.Should().Be(10);
    }

    [Fact]
    public void Validate_WarningThresholdExceedsMax_ReducesThreshold()
    {
        var s = new ApplicationSettings
        {
            MaxFileSizeBytes = 1024 * 1024,
            LargeFileWarningThresholdBytes = 999 * 1024 * 1024
        };
        s.Validate();
        s.LargeFileWarningThresholdBytes.Should().BeLessOrEqualTo(s.MaxFileSizeBytes);
    }

    [Fact]
    public void GetLimitsDescription_ContainsKeyValues()
    {
        var desc = new ApplicationSettings().GetLimitsDescription();
        desc.Should().Contain("Max File Size").And.Contain("Max Rows").And.Contain("Max Columns");
    }
}
