using Microsoft.Extensions.Logging;

namespace SPO2SQL.Logging;

/// <summary>
/// Bridge between the legacy static logging calls used across the core library and
/// the host's <see cref="ILogger"/>. The host must call <see cref="Configure"/> at
/// startup; until then messages are written to the console so failures are never
/// silently discarded.
/// </summary>
public static class Logger
{
    private static readonly object Sync = new();

    private static ILogger? _sink;
    private static int _verboseLevel = 2;

    /// <summary>
    /// Legacy verbosity gate kept for source compatibility. 1 = errors only,
    /// 2 = warnings and above, 3 = all messages.
    /// </summary>
    public static int VerboseLevel
    {
        get => Volatile.Read(ref _verboseLevel);
        set => Volatile.Write(ref _verboseLevel, value);
    }

    /// <summary>
    /// Connects the static facade to the application's logging pipeline.
    /// </summary>
    public static void Configure(ILogger sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        Volatile.Write(ref _sink, sink);
        VerboseLevel = sink.IsEnabled(LogLevel.Debug) ? 3 : 2;
    }

    internal static void Reset()
    {
        Volatile.Write(ref _sink, null);
        _verboseLevel = 2;
    }

    /// <summary>
    /// Logs a message using the legacy numeric levels: 1 = information,
    /// 2 = warning, 3 = debug.
    /// </summary>
    public static void Log(int level, string message)
    {
        switch (level)
        {
            case 1:
                Write(LogLevel.Information, 1, message, null);
                break;
            case 2:
                Write(LogLevel.Warning, 2, message, null);
                break;
            case 3:
                Write(LogLevel.Debug, 3, message, null);
                break;
        }
    }

    public static void LogInformation(string message) => Write(LogLevel.Information, 1, message, null);

    public static void LogWarning(string message) => Write(LogLevel.Warning, 2, message, null);

    public static void LogDebug(string message) => Write(LogLevel.Debug, 3, message, null);

    public static void LogError(string message, Exception? ex = null) => Write(LogLevel.Error, 1, message, ex);

    private static void Write(LogLevel severity, int legacyLevel, string message, Exception? exception)
    {
        if (legacyLevel > VerboseLevel)
        {
            return;
        }

        ILogger? sink = Volatile.Read(ref _sink);
        if (sink != null)
        {
            if (!sink.IsEnabled(severity))
            {
                return;
            }

            sink.Log(severity, new EventId(0, "Legacy"), message, exception, static (state, _) => state);
            return;
        }

        string levelName = severity switch
        {
            LogLevel.Error or LogLevel.Critical => "ERROR",
            LogLevel.Warning => "WARN",
            LogLevel.Debug or LogLevel.Trace => "DEBUG",
            _ => "INFO"
        };

        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{levelName}] {message}";
        lock (Sync)
        {
            Console.WriteLine(line);
            if (exception != null)
            {
                Console.WriteLine(exception);
            }
        }
    }
}
