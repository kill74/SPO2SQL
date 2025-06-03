using System;

namespace Bring.SPODataQuality
{
    public static class Logger
    {
        public static int VerboseLevel { get; set; } = 0;

        public static void Log(int level, string message)
        {
            if (VerboseLevel >= level)
                Console.WriteLine(message);
        }
    }
}