namespace DatPlotX.Services;

/// <summary>
/// Distinguishes the three outcomes of a file-picker-driven operation so the UI can
/// show the right status message without conflating cancellation with failure.
/// </summary>
public enum FileOperationOutcome
{
    Success,
    Cancelled,
    Failed,
}

/// <summary>
/// Result of a file-picker-driven operation. The service shows the error dialog;
/// the caller only needs to switch on <see cref="Outcome"/> to update status text.
/// </summary>
public sealed record FileOperationResult<T>(FileOperationOutcome Outcome, T? Value, string? ErrorMessage);

public static class FileOperationResult
{
    public static FileOperationResult<T> Success<T>(T value) => new(FileOperationOutcome.Success, value, null);
    public static FileOperationResult<T> Cancelled<T>() => new(FileOperationOutcome.Cancelled, default, null);
    public static FileOperationResult<T> Failed<T>(string message) => new(FileOperationOutcome.Failed, default, message);
}
