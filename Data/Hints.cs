using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>What starts a conversation topic, phrased to follow "after", e.g. "the bus is repaired".</summary>
        public Dictionary<string, Hint> Topics { get; set; } = new();

        /// <summary>Event requirements other mods register with the game, by name, e.g. Ridgeside's "rsvRidingHorse".</summary>
        public Dictionary<string, Hint> Preconditions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
            file.Flags = WithoutNulls(file.Flags, StringComparer.Ordinal);
            file.Topics = WithoutNulls(file.Topics, StringComparer.Ordinal);
            file.Preconditions = WithoutNulls(file.Preconditions, StringComparer.OrdinalIgnoreCase);
            monitor.Log($"Loaded {file.Flags.Count} flag hints, {file.Topics.Count} topic hints and {file.Preconditions.Count} requirement hints.", LogLevel.Trace);
        }

        /// <summary>A section of the file without the null sections or entries a typo can leave.</summary>
        private static Dictionary<string, Hint> WithoutNulls(Dictionary<string, Hint>? hints, StringComparer comparer) =>
            new((hints ?? new Dictionary<string, Hint>()).Where(p => p.Value != null), comparer);

        public static Hint? ForTopic(string topic) =>
            file.Topics.TryGetValue(topic, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;

        public static Hint? ForPrecondition(string name) =>
            file.Preconditions.TryGetValue(name, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;

        public static Hint? ForFlag(string flag) =>
            file.Flags.TryGetValue(flag, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;
    }
}
