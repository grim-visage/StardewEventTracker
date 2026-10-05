using System;
using System.Diagnostics;
using StardewModdingAPI;

namespace StardewEventTracker
{
    /// <summary>Logs the tracker's own work that takes long enough to cause a visible stutter.</summary>
    internal static class Perf
    {
        /// <summary>How long a single piece of work can take before it's logged (a frame is about 16ms).</summary>
        private const long ThresholdMs = 8;

        private static IMonitor? monitor;

        /// <summary>Time spent evaluating events since the last <see cref="FlushTick"/>, in stopwatch ticks.</summary>
        private static long evalTicks;

        private static int evalCount;

        public static void Init(IMonitor m) => monitor = m;

        public static void Run(string what, Action action)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            Report(what, start);
        }

        public static T Run<T>(string what, Func<T> func)
        {
            long start = Stopwatch.GetTimestamp();
            T result = func();
            Report(what, start);
            return result;
        }

        /// <summary>Adds one event evaluation to this tick's total.</summary>
        public static T Eval<T>(Func<T> func)
        {
            long start = Stopwatch.GetTimestamp();
            T result = func();
            evalTicks += Stopwatch.GetTimestamp() - start;
            evalCount++;
            return result;
        }

        /// <summary>Logs the last tick's evaluations if they added up to a stutter, then starts counting again.</summary>
        public static void FlushTick()
        {
            long ms = evalTicks * 1000 / Stopwatch.Frequency;
            if (ms >= ThresholdMs)
                monitor?.Log($"Slow: evaluated {evalCount} events in {ms}ms (location {StardewValley.Game1.currentLocation?.NameOrUniqueName}).");
            evalTicks = 0;
            evalCount = 0;
        }

        private static void Report(string what, long start)
        {
            long ms = (Stopwatch.GetTimestamp() - start) * 1000 / Stopwatch.Frequency;
            if (ms >= ThresholdMs)
                monitor?.Log($"Slow: {what} took {ms}ms.");
        }
    }
}
