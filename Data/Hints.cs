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

    /// <summary>Whose heart event an event is, where the tracker's guess (the first NPC it needs friendship with) is wrong.</summary>
    internal sealed class OwnerHint
    {
        public string Npc { get; set; } = "";
        public string? Source { get; set; }
    }

    /// <summary>The model for <c>assets/hints.json</c>.</summary>
    internal sealed class HintFile
    {
        public Dictionary<string, Hint> Flags { get; set; } = new();

        /// <summary>What starts a conversation topic, phrased to follow "after", e.g. "the bus is repaired".</summary>
        public Dictionary<string, Hint> Topics { get; set; } = new();

        /// <summary>How to open up the way into an area a mod added, by location name, e.g. Stardew Valley Expanded's Highlands.</summary>
        public Dictionary<string, Hint> Locations { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Event requirements other mods register with the game, by name, e.g. Ridgeside's "rsvRidingHorse".</summary>
        public Dictionary<string, Hint> Preconditions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whose heart event an event is, by event ID, e.g. SVE's family dinner that needs 2 hearts with Maru but is Sebastian's.</summary>
        public Dictionary<string, OwnerHint> EventOwners { get; set; } = new();
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
            // Content Patcher packs write flags in any case ("jojaMember"), so match hints the same way
            file.Flags = WithoutNulls(file.Flags, StringComparer.OrdinalIgnoreCase);
            file.Topics = WithoutNulls(file.Topics, StringComparer.Ordinal);
            file.Preconditions = WithoutNulls(file.Preconditions, StringComparer.OrdinalIgnoreCase);
            file.Locations = WithoutNulls(file.Locations, StringComparer.OrdinalIgnoreCase);
            file.EventOwners = new((file.EventOwners ?? new Dictionary<string, OwnerHint>()).Where(p => !string.IsNullOrWhiteSpace(p.Value?.Npc)));
            monitor.Log($"Loaded {file.Flags.Count} flag hints, {file.Topics.Count} topic hints, {file.Preconditions.Count} requirement hints, {file.Locations.Count} location hints and {file.EventOwners.Count} event owners.", LogLevel.Trace);
        }

        /// <summary>A section of the file without the null sections or entries a typo can leave.</summary>
        private static Dictionary<string, Hint> WithoutNulls(Dictionary<string, Hint>? hints, StringComparer comparer) =>
            new((hints ?? new Dictionary<string, Hint>()).Where(p => p.Value != null), comparer);

        public static Hint? ForTopic(string topic) =>
            file.Topics.TryGetValue(topic, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;

        public static Hint? ForPrecondition(string name) =>
            file.Preconditions.TryGetValue(name, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;

        public static Hint? ForLocation(string location) =>
            file.Locations.TryGetValue(location, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;

        /// <summary>The NPC whose heart event this is, if the hints say so.</summary>
        public static string? OwnerOf(string eventId) =>
            file.EventOwners.TryGetValue(eventId, out OwnerHint? hint) ? hint.Npc : null;

        public static Hint? ForFlag(string flag) =>
            file.Flags.TryGetValue(flag, out Hint? hint) && !string.IsNullOrWhiteSpace(hint.Text) ? hint : null;
    }
}
