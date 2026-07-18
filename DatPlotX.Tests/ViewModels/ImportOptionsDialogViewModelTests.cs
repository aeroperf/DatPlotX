using DatPlotX.ViewModels;
using FluentAssertions;

namespace DatPlotX.Tests.ViewModels;

public class ImportOptionsDialogViewModelTests
{
    [Fact]
    public void DefaultDelimiter_IsComma()
    {
        new ImportOptionsDialogViewModel().SelectedDelimiter.Should().Be(",");
    }

    [Fact]
    public void DefaultDecimalFormat_IsPeriod()
    {
        new ImportOptionsDialogViewModel().SelectedDecimalFormat.Should().Be("Period (.)");
    }

    [Fact]
    public void IsDecimalFormatEnabled_DefaultDelimiter_IsTrue()
    {
        new ImportOptionsDialogViewModel().IsDecimalFormatEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsDecimalFormatEnabled_XPlaneDelimiter_IsFalse()
    {
        var vm = new ImportOptionsDialogViewModel { SelectedDelimiter = "X-Plane (.txt)" };
        vm.IsDecimalFormatEnabled.Should().BeFalse();
    }

    [Fact]
    public void Delimiters_ContainsExpectedOptions()
    {
        var vm = new ImportOptionsDialogViewModel();
        vm.Delimiters.Should().Contain(",").And.Contain(";").And.Contain("Tab").And.Contain("X-Plane (.txt)");
    }

    [Fact]
    public void DecimalFormats_ContainsBothOptions()
    {
        var vm = new ImportOptionsDialogViewModel();
        vm.DecimalFormats.Should().Contain("Period (.)").And.Contain("Comma (,)");
    }

    // --- Validation ---

    [Fact]
    public void CommaDelimiter_WithCommaDecimal_IsInvalid()
    {
        var vm = new ImportOptionsDialogViewModel
        {
            SelectedDelimiter = ",",
            SelectedDecimalFormat = "Comma (,)"
        };
        vm.CanImport.Should().BeFalse();
        vm.ValidationHint.Should().Contain("delimiter");
    }

    [Fact]
    public void CommaDelimiter_WithPeriodDecimal_IsValid()
    {
        var vm = new ImportOptionsDialogViewModel
        {
            SelectedDelimiter = ",",
            SelectedDecimalFormat = "Period (.)"
        };
        vm.CanImport.Should().BeTrue();
    }

    [Fact]
    public void SemicolonDelimiter_WithCommaDecimal_IsValid()
    {
        var vm = new ImportOptionsDialogViewModel
        {
            SelectedDelimiter = ";",
            SelectedDecimalFormat = "Comma (,)"
        };
        vm.CanImport.Should().BeTrue();
    }

    // --- GetImportOptions ---

    [Fact]
    public void GetImportOptions_CommaDelimiter_PeriodDecimal_EnUS()
    {
        var vm = new ImportOptionsDialogViewModel
        {
            SelectedDelimiter = ",",
            SelectedDecimalFormat = "Period (.)"
        };
        var opts = vm.GetImportOptions();
        opts.Delimiter.Should().Be(",");
        opts.CultureName.Should().Be("en-US");
        opts.IsXPlaneFormat.Should().BeFalse();
    }

    [Fact]
    public void GetImportOptions_SemicolonDelimiter_CommaDecimal_DeDE()
    {
        var vm = new ImportOptionsDialogViewModel
        {
            SelectedDelimiter = ";",
            SelectedDecimalFormat = "Comma (,)"
        };
        var opts = vm.GetImportOptions();
        opts.Delimiter.Should().Be(";");
        opts.CultureName.Should().Be("de-DE");
        opts.IsXPlaneFormat.Should().BeFalse();
    }

    [Fact]
    public void GetImportOptions_TabDelimiter_TranslatesToTabChar()
    {
        var vm = new ImportOptionsDialogViewModel { SelectedDelimiter = "Tab" };
        var opts = vm.GetImportOptions();
        opts.Delimiter.Should().Be("\t");
    }

    [Fact]
    public void GetImportOptions_XPlaneFormat_SetsAllFlags()
    {
        var vm = new ImportOptionsDialogViewModel { SelectedDelimiter = "X-Plane (.txt)" };
        var opts = vm.GetImportOptions();
        opts.IsXPlaneFormat.Should().BeTrue();
        opts.Delimiter.Should().Be("|");
        opts.CultureName.Should().Be("en-US");
    }

    [Fact]
    public void GetImportOptions_PipeDelimiter_PassedThrough()
    {
        var vm = new ImportOptionsDialogViewModel { SelectedDelimiter = "|" };
        var opts = vm.GetImportOptions();
        opts.Delimiter.Should().Be("|");
        opts.IsXPlaneFormat.Should().BeFalse();
    }

    // --- Line-selector validation (P1) ---

    // P1: dialog has its own copy of the line-selector validation rules; assert that
    // header == unit blocks the import and that the message explains why.
    [Fact]
    public void HeaderEqualsUnitLine_DisablesImport_AndExplainsWhy()
    {
        var vm = new ImportOptionsDialogViewModel
        {
            HeaderLine = 2,
            UnitLine = 2,
            DataStartLine = 3,
        };

        vm.CanImport.Should().BeFalse();
        vm.ValidationHint.Should().NotBeNull();
        vm.ValidationHint!.Should().Contain("differ");
    }

    // P1: changing DataStartLine must reclassify every preview row — Header below DataStart,
    // Unit below DataStart, DataStart itself, body rows, and Skipped rows above.
    [Fact]
    public void RetagPreviewLines_AfterDataStartLineChange_ClassifiesEachLine()
    {
        var vm = new ImportOptionsDialogViewModel();

        // Seed 5 preview lines exactly the way LoadPreviewAsync would.
        for (int i = 0; i < 5; i++)
            vm.PreviewLines.Add(new PreviewLineViewModel(i + 1, $"line {i + 1}"));

        // HeaderLine=1, UnitLine=2, DataStartLine=4 means:
        //   1 -> Header, 2 -> Unit, 3 -> Skipped, 4 -> DataStart, 5 -> DataBody.
        vm.HeaderLine = 1;
        vm.UnitLine = 2;
        vm.DataStartLine = 4;

        vm.PreviewLines[0].Kind.Should().Be(PreviewRowKind.Header);
        vm.PreviewLines[1].Kind.Should().Be(PreviewRowKind.Unit);
        vm.PreviewLines[2].Kind.Should().Be(PreviewRowKind.Skipped);
        vm.PreviewLines[3].Kind.Should().Be(PreviewRowKind.DataStart);
        vm.PreviewLines[4].Kind.Should().Be(PreviewRowKind.DataBody);
    }
}
