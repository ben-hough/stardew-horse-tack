using System.Collections.Generic;
using StardewModdingAPI;

namespace MrGlim.HorseTack.Framework
{
    /// <summary>Small logging wrapper with a verbose switch and log-once support.</summary>
    internal static class Log
    {
        private static IMonitor? Monitor;
        private static readonly HashSet<string> Once = new();
        public static bool Verbose;

        public static void Init(IMonitor monitor) => Monitor = monitor;

        public static void Trace(string msg) => Monitor?.Log(msg, Verbose ? LogLevel.Info : LogLevel.Trace);
        public static void Info(string msg) => Monitor?.Log(msg, LogLevel.Info);
        public static void Warn(string msg) => Monitor?.Log(msg, LogLevel.Warn);
        public static void Error(string msg) => Monitor?.Log(msg, LogLevel.Error);

        /// <summary>Log a warning only the first time this key is seen.</summary>
        public static void WarnOnce(string key, string msg)
        {
            if (Once.Add(key))
                Warn(msg);
        }

        public static void ResetOnce() => Once.Clear();
    }
}
