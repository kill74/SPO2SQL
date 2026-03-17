using System;

namespace Bring.SPODataQuality
{
    /// <summary>
    /// Static logger for consistent, formatted console output with verbosity control.
    /// Supports levels 0-3: 0=silent, 1=errors, 2=warnings, 3=debug.
    /// </summary>
    public static class Logger
    {
        private const string TIMESTAMP_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        private enum LogLevel { ERROR = 1, WARNING = 2, DEBUG = 3 }

        /// <summary>
        /// Verbosity level (0-3). Messages logged at or below this level will display.
        /// </summary>
        public static int VerboseLevel { get; set; } = 0;

        /// <summary>
        /// Logs a message with the specified verbosity level.
        /// </summary>
        /// <param name="level">1=ERROR, 2=WARNING, 3=DEBUG. Only messages at or below VerboseLevel display.</param>
        /// <param name="message">The message to log.</param>
        public static void Log(int level, string message)
        {
            if (level < 1 || level > 3) return;
            if (VerboseLevel < level) return;

            string timestamp = DateTime.Now.ToString(TIMESTAMP_FORMAT);
            string levelName = GetLevelName(level);
            string formattedMessage = $"[{timestamp}] [{levelName}] {message}";
            
            Console.WriteLine(formattedMessage);
        }

        /// <summary>
        /// Logs an error message (level 1).
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            if (VerboseLevel >= 1)
            {
                Log(1, message);
                if (ex != null && VerboseLevel >= 3)
                {
                    Log(3, $"Exception Details: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Logs a warning message (level 2).
        /// </summary>
        public static void LogWarning(string message)
        {
            Log(2, message);
        }

        /// <summary>
        /// Logs a debug message (level 3).
        /// </summary>
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
}