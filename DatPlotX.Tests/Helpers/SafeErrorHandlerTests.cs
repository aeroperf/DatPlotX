using DatPlotX.Helpers;
using FluentAssertions;

namespace DatPlotX.Tests.Helpers;

public class SafeErrorHandlerTests
{
    [Fact]
    public void GetUserFriendlyMessage_FileNotFound_HidesSysPath()
    {
        var ex = new FileNotFoundException("C:\\secret\\path\\file.csv");
        var msg = SafeErrorHandler.GetUserFriendlyMessage(ex, "loading");
        msg.Should().NotContain("secret").And.NotContain("C:\\");
        msg.Should().Contain("could not be found");
    }

    [Fact]
    public void GetUserFriendlyMessage_UnauthorizedAccess_ReturnsPermissionMessage()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessage(new UnauthorizedAccessException(), "saving");
        msg.Should().Contain("permission");
    }

    [Fact]
    public void GetUserFriendlyMessage_IOException_ContainsOperation()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessage(new IOException(), "reading the file");
        msg.Should().Contain("reading the file");
    }

    [Fact]
    public void GetUserFriendlyMessage_InvalidData_ReturnsFormatMessage()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessage(new InvalidDataException(), "parsing");
        msg.Should().Contain("invalid or corrupted");
    }

    [Fact]
    public void GetUserFriendlyMessage_SecurityException_ReturnsSafeMessage()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessage(new System.Security.SecurityException("internal detail"), "load");
        msg.Should().NotContain("internal detail");
        msg.Should().Contain("security");
    }

    [Fact]
    public void GetUserFriendlyMessage_OutOfMemory_ReturnsTooLargeMessage()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessage(new OutOfMemoryException(), "import");
        (msg.Contains("large") || msg.Contains("memory")).Should().BeTrue(
            $"Expected message to mention 'large' or 'memory' but was: {msg}");
    }

    [Fact]
    public void GetUserFriendlyMessage_UnknownException_ReturnsGenericMessage()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessage(new Exception("secret internal error"), "op");
        msg.Should().NotContain("secret internal error");
        msg.Should().Contain("unexpected error");
    }

    [Fact]
    public void GetUserFriendlyMessageWithContext_AppendsContext()
    {
        var msg = SafeErrorHandler.GetUserFriendlyMessageWithContext(
            new FileNotFoundException(), "loading", "file.csv");
        msg.Should().Contain("Context: file.csv");
    }

    [Fact]
    public void LogError_DoesNotThrow()
    {
        var act = () => SafeErrorHandler.LogError(new Exception("test"), "testing", "extra info");
        act.Should().NotThrow();
    }

    [Fact]
    public void LogError_NullAdditionalInfo_DoesNotThrow()
    {
        var act = () => SafeErrorHandler.LogError(new Exception("test"), "testing", null);
        act.Should().NotThrow();
    }
}
