using System.Collections.Generic;
using StardewModdingAPI;

namespace StardewEventTracker.Data
{
    /// <summary>A hand-written hint for a story flag, with where the information came from.</summary>
    internal sealed class Hint
    {
        public string Text { get; set; } = "";
        public string? Source { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>The model for <c>assets/hints.json</c>.</summary>
    internal sealed class HintFile
    {
        public Dictionary<string, Hint> Flags { get; set; } = new();
    }

    /// <summary>
    /// Hints for story flags the game's data doesn't explain (set by a mod's code, quest rewards, or unfinished
    /// content), researched from mod wikis and handbooks.
    /// </summary>
    internal static class Hints
    {
        private static HintFile file = new();

        public static void Load(IModHelper helper, IMonitor monitor)
        {
            file = helper.Data.ReadJsonFile<HintFile>("assets/hints.json") ?? new HintFile();
            monitor.Log($"Loaded {file.Flags.Count} flag hints.", LogLevel.Trace);
        }

        public static Hint? ForFlag(string flag) =>
            file.Flags.TryGetValue(flag, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;
    }
}
