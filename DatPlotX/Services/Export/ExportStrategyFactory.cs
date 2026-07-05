namespace DatPlotX.Services.Export;

/// <summary>
/// Factory interface for creating export strategies
/// </summary>
public interface IExportStrategyFactory
{
    /// <summary>
    /// Get the appropriate export strategy for a file extension
    /// </summary>
    /// <param name="fileExtension">The file extension (e.g., ".png")</param>
    /// <returns>The appropriate export strategy</returns>
    /// <exception cref="NotSupportedException">If the format is not supported</exception>
    IImageExportStrategy GetStrategy(string fileExtension);

    /// <summary>
    /// Get all available export strategies
    /// </summary>
    IEnumerable<IImageExportStrategy> GetAllStrategies();

    /// <summary>
    /// Check if a format is supported
    /// </summary>
    bool IsFormatSupported(string fileExtension);

    /// <summary>
    /// Get combined file filter string for save dialogs
    /// </summary>
    string GetCombinedFilter();
}

/// <summary>
/// Factory for creating image export strategies based on file extension (OCP compliance)
/// New formats can be added by registering additional strategies
/// </summary>
public class ExportStrategyFactory : IExportStrategyFactory
{
    private readonly Dictionary<string, IImageExportStrategy> _strategies;

    public ExportStrategyFactory()
    {
        _strategies = new Dictionary<string, IImageExportStrategy>(StringComparer.OrdinalIgnoreCase)
        {
            { ".png", new PngExportStrategy() },
            { ".jpg", new JpegExportStrategy() },
            { ".jpeg", new JpegExportStrategy() },
            { ".bmp", new BmpExportStrategy() },
            { ".svg", new SvgExportStrategy() }
        };
    }

    public IImageExportStrategy GetStrategy(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
            throw new ArgumentException("File extension cannot be null or empty", nameof(fileExtension));

        // Ensure extension starts with dot
        if (!fileExtension.StartsWith('.'))
            fileExtension = "." + fileExtension;

        if (_strategies.TryGetValue(fileExtension, out var strategy))
            return strategy;

        throw new NotSupportedException($"Export format '{fileExtension}' is not supported. " +
            $"Supported formats: {string.Join(", ", _strategies.Keys)}");
    }

    public IEnumerable<IImageExportStrategy> GetAllStrategies()
    {
        return _strategies.Values.Distinct();
    }

    public bool IsFormatSupported(string fileExtension)
    {
        if (string.IsNullOrWhiteSpace(fileExtension))
            return false;

        if (!fileExtension.StartsWith('.'))
            fileExtension = "." + fileExtension;

        return _strategies.ContainsKey(fileExtension);
    }

    public string GetCombinedFilter()
    {
        var filters = GetAllStrategies()
            .Select(s => s.FilterDescription)
            .Distinct();
        return string.Join("|", filters);
    }

    /// <summary>
    /// Register a new export strategy (for extensibility)
    /// </summary>
    public void RegisterStrategy(string extension, IImageExportStrategy strategy)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Extension cannot be null or empty", nameof(extension));

        if (!extension.StartsWith('.'))
            extension = "." + extension;

        _strategies[extension] = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }
}
