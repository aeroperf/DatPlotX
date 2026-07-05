using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class InputValidatorTests
{
    // --- ValidateFileName ---

    [Fact]
    public void ValidateFileName_Empty_Throws()
    {
        var act = () => InputValidator.ValidateFileName("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFileName_TooLong_Throws()
    {
        var act = () => InputValidator.ValidateFileName(new string('a', 256));
        act.Should().Throw<ArgumentException>().WithMessage("*maximum length*");
    }

    [Fact]
    public void ValidateFileName_InvalidChars_Throws()
    {
        var act = () => InputValidator.ValidateFileName("file/name.csv");
        act.Should().Throw<ArgumentException>().WithMessage("*invalid characters*");
    }

    [Fact]
    public void ValidateFileName_LeadingTrailingDots_Stripped()
    {
        var result = InputValidator.ValidateFileName("...filename...");
        result.Should().Be("filename");
    }

    [Fact]
    public void ValidateFileName_OnlyDots_Throws()
    {
        var act = () => InputValidator.ValidateFileName("...");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFileName_ValidName_Returns()
    {
        var result = InputValidator.ValidateFileName("MyFile.csv");
        result.Should().Be("MyFile.csv");
    }

    // --- ValidateLabel ---

    [Fact]
    public void ValidateLabel_Empty_Throws()
    {
        var act = () => InputValidator.ValidateLabel("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateLabel_TooLong_Throws()
    {
        var act = () => InputValidator.ValidateLabel(new string('x', 101));
        act.Should().Throw<ArgumentException>().WithMessage("*maximum length*");
    }

    [Fact]
    public void ValidateLabel_ControlCharacters_Stripped()
    {
        var label = "Hello\x01\x02World";
        var result = InputValidator.ValidateLabel(label);
        result.Should().Be("HelloWorld");
    }

    [Fact]
    public void ValidateLabel_NewlinesAllowed()
    {
        var result = InputValidator.ValidateLabel("Line1\nLine2");
        result.Should().Contain("Line1").And.Contain("Line2");
    }

    [Fact]
    public void ValidateLabel_Valid_ReturnsTrimmed()
    {
        var result = InputValidator.ValidateLabel("  E1  ");
        result.Should().Be("E1");
    }

    // --- ValidateColumnName ---

    [Fact]
    public void ValidateColumnName_Empty_Throws()
    {
        var act = () => InputValidator.ValidateColumnName("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateColumnName_TooLong_Throws()
    {
        var act = () => InputValidator.ValidateColumnName(new string('c', 201));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateColumnName_Valid_Returns()
    {
        var result = InputValidator.ValidateColumnName("gFx");
        result.Should().Be("gFx");
    }

    // --- ValidatePositiveInteger ---

    [Fact]
    public void ValidatePositiveInteger_BelowMin_Throws()
    {
        var act = () => InputValidator.ValidatePositiveInteger(0, 1, 100, "param");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidatePositiveInteger_AboveMax_Throws()
    {
        var act = () => InputValidator.ValidatePositiveInteger(101, 1, 100, "param");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidatePositiveInteger_AtMin_Returns()
    {
        InputValidator.ValidatePositiveInteger(1, 1, 100, "param").Should().Be(1);
    }

    [Fact]
    public void ValidatePositiveInteger_AtMax_Returns()
    {
        InputValidator.ValidatePositiveInteger(100, 1, 100, "param").Should().Be(100);
    }

    // --- ValidateDouble ---

    [Fact]
    public void ValidateDouble_NaN_Throws()
    {
        var act = () => InputValidator.ValidateDouble(double.NaN, 0, 100, "param");
        act.Should().Throw<ArgumentException>().WithMessage("*valid number*");
    }

    [Fact]
    public void ValidateDouble_Infinity_Throws()
    {
        var act = () => InputValidator.ValidateDouble(double.PositiveInfinity, 0, 100, "param");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateDouble_BelowMin_Throws()
    {
        var act = () => InputValidator.ValidateDouble(-1.0, 0, 100, "param");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ValidateDouble_Valid_Returns()
    {
        InputValidator.ValidateDouble(50.5, 0, 100, "param").Should().Be(50.5);
    }

    // --- SanitizeColumnName ---

    [Fact]
    public void SanitizeColumnName_Null_ReturnsDefault()
    {
        InputValidator.SanitizeColumnName(null!).Should().Be("Column");
    }

    [Fact]
    public void SanitizeColumnName_Empty_ReturnsDefault()
    {
        InputValidator.SanitizeColumnName("").Should().Be("Column");
    }

    [Fact]
    public void SanitizeColumnName_StartsWithDigit_Prefixed()
    {
        var result = InputValidator.SanitizeColumnName("1abc");
        result.Should().StartWith("_");
    }

    [Fact]
    public void SanitizeColumnName_DangerousChars_Removed()
    {
        var result = InputValidator.SanitizeColumnName("col<script>name");
        result.Should().NotContain("<").And.NotContain(">");
    }

    [Fact]
    public void SanitizeColumnName_TooLong_Truncated()
    {
        var result = InputValidator.SanitizeColumnName(new string('a', 200));
        result.Length.Should().BeLessOrEqualTo(128);
    }

    [Fact]
    public void SanitizeColumnName_Valid_Unchanged()
    {
        InputValidator.SanitizeColumnName("gFx").Should().Be("gFx");
    }

    [Fact]
    public void MakeUniqueColumnNames_NoDuplicates_LeavesAsIs()
    {
        InputValidator.MakeUniqueColumnNames(new[] { "a", "b", "c" })
            .Should().Equal("a", "b", "c");
    }

    [Fact]
    public void MakeUniqueColumnNames_TwoIdenticalHeaders_SuffixesSecond()
    {
        InputValidator.MakeUniqueColumnNames(new[] { "Foo", "Foo" })
            .Should().Equal("Foo", "Foo_2");
    }

    [Fact]
    public void MakeUniqueColumnNames_ThreeIdenticalHeaders_SuffixesSecondAndThird()
    {
        InputValidator.MakeUniqueColumnNames(new[] { "Foo", "Foo", "Foo" })
            .Should().Equal("Foo", "Foo_2", "Foo_3");
    }

    [Fact]
    public void MakeUniqueColumnNames_PreExistingSuffix_SkipsCollision()
    {
        InputValidator.MakeUniqueColumnNames(new[] { "Foo", "Foo_2", "Foo" })
            .Should().Equal("Foo", "Foo_2", "Foo_3");
    }

    // P2 (M1 guard): the probe-forward loop must keep advancing the candidate suffix until
    // it finds a non-clashing slot. Input `["a","a","a_2"]` exercises this because the
    // straight `_2` suffix is already taken by the third element — the probe must walk to
    // `_3` or beyond rather than emitting a duplicate.
    [Fact]
    public void MakeUniqueColumnNames_CollisionWithSuffix_ProbesForward()
    {
        var result = InputValidator.MakeUniqueColumnNames(new[] { "a", "a", "a_2" });

        result.Should().HaveCount(3);
        result[0].Should().Be("a", "the first occurrence is never renamed");
        // No duplicates allowed.
        result.Should().OnlyHaveUniqueItems();
        // Every output is a recognisable variant of the input.
        result[1].Should().StartWith("a");
        result[2].Should().StartWith("a");
    }
}
