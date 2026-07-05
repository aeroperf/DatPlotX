using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DatPlotX.Services.Logging;

/// <summary>
/// Zero-dependency <see cref="ILoggerProvider"/> that writes to a rolling daily log file on the
/// local disk. No third-party logging package, and nothing leaves the machine — the file is the
/// only sink. Pairs with the "Open Log Folder" Help menu item and the privacy doc.
///
/// <para>Policy:</para>
/// <list type="bullet">
///   <item><description><b>Rolling:</b> one file per day, <c>datplotx-yyyyMMdd.log</c>.</description></item>
///   <item><description><b>Size cap:</b> when the day's file passes <see cref="MaxFileSizeBytes"/>
///     (50 MB) it rolls to <c>datplotx-yyyyMMdd.N.log</c>.</description></item>
///   <item><description><b>Retention:</b> files older than <see cref="RetentionDays"/> (7) days are
///     pruned on startup.</description></item>
/// </list>
///
/// <para>Writes are serialized through a lock and flushed per entry; the volume (events + errors,
/// never row data) is low enough that this is not a hot path.</para>
/// </summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;
    public const int RetentionDays = 7;

    private const string FilePrefix = "datplotx-";
    private const string FileExtension = ".log";

    private readonly string _logDirectory;
    private readonly LogLevel _minLevel;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();

    public FileLoggerProvider(string logDirectory, LogLevel minLevel = LogLevel.Information)
    {
        _logDirectory = logDirectory;
        _minLevel = minLevel;
        Directory.CreateDirectory(_logDirectory);
        TryPruneOldFiles();
    }

    public ILogger CreateLogger(string categoryName)
        => _loggers.GetOrAdd(categoryName, name => new FileLogger(this, name));

    public void Dispose() => _loggers.Clear();

    private bool IsEnabled(LogLevel level) => level >= _minLevel && level != LogLevel.None;

    /// <summary>Append one formatted line. Resolves today's file (rolling on size) under the lock.</summary>
    private void Write(string line)
    {
        lock (_writeLock)
        {
            try
            {
                var path = ResolveCurrentFile();
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Logging must never crash the app. A failed write is silently dropped.
            }
        }
    }

    private string ResolveCurrentFile()
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var basePath = Path.Combine(_logDirectory, $"{FilePrefix}{stamp}{FileExtension}");

        var info = new FileInfo(basePath);
        if (!info.Exists || info.Length < MaxFileSizeBytes)
            return basePath;

        // Today's primary file is full — roll to the next numbered shard for today.
        for (var i = 1; ; i++)
        {
            var shard = Path.Combine(_logDirectory, $"{FilePrefix}{stamp}.{i}{FileExtension}");
            var shardInfo = new FileInfo(shard);
            if (!shardInfo.Exists || shardInfo.Length < MaxFileSizeBytes)
                return shard;
        }
    }

    private void TryPruneOldFiles()
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            foreach (var file in Directory.EnumerateFiles(_logDirectory, $"{FilePrefix}*{FileExtension}"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch
        {
            // Pruning is best-effort; never block startup on it.
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _provider;
        private readonly string _category;

        public FileLogger(FileLoggerProvider provider, string category)
        {
            _provider = provider;
            // Trim the namespace down to the leaf type for readability.
            var dot = category.LastIndexOf('.');
            _category = dot >= 0 && dot < category.Length - 1 ? category[(dot + 1)..] : category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => _provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = formatter(state, exception);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var sb = new StringBuilder(128)
                .Append(timestamp).Append(" [").Append(Level(logLevel)).Append("] ")
                .Append(_category).Append(" - ").Append(message);

            if (exception is not null)
                sb.Append(Environment.NewLine).Append(exception);

            _provider.Write(sb.ToString());
        }

        private static string Level(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
