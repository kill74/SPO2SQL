using System;

namespace SPO2SQL.Logging;

public static class Logger
{
    private const string TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
    private enum LogLevel { ERROR = 1, WARNING = 2, DEBUG = 3 }

    public static int VerboseLevel { get; set; } = 0;

    public static void Log(int level, string message)
    {
        if (level < 1 || level > 3)
        {
            return;
        }

        if (VerboseLevel < level)
        {
            return;
        }

        string timestamp = DateTime.Now.ToString(TIMESTAMP_FORMAT);
        string levelName = GetLevelName(level);
        string formattedMessage = $"[{timestamp}] [{levelName}] {message}";

        Console.WriteLine(formattedMessage);
    }

    public static void LogError(string message, Exception ex = null)
    {
        Log(1, message);
        if (ex != null && VerboseLevel >= 3)
        {
            Log(3, $"Exception Details: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void LogWarning(string message)
    {
        Log(2, message);
    }

    public static void LogDebug(string message)
    {
        Log(3, message);
    }

    private static string GetLevelName(int level) => level switch
    {
        1 => "ERROR",
        2 => "WARN",
        3 => "DEBUG",
        _ => "INFO"
    };
}
