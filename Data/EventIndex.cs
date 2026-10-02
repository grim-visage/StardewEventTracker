using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace NpcEventTracker.Data
{
    /// <summary>An NPC's unseen events, split into what's actionable now and what's still locked.</summary>
    internal sealed class PendingEvents
    {
        /// <summary>Unseen heart events (or, for the "Other" group, any unseen event) that are unlocked.</summary>
        public List<(EventInfo Event, EventEvaluation Eval)> Pending { get; } = new();

        /// <summary>Unlocked events the NPC only appears in, without a friendship requirement for them.</summary>
        public List<(EventInfo Event, EventEvaluation Eval)> OtherScenes { get; } = new();
        public (EventInfo Event, EventEvaluation Eval)? NextLocked { get; set; }
        public int Unreachable { get; set; }
        public int Locked { get; set; }
    }

    /// <summary>Every event in the game's current data, grouped by the NPC it belongs to.</summary>
    internal sealed class EventIndex
    {
        /// <summary>Owner key for events not tied to any NPC.</summary>
        public const string OtherKey = "";

        private readonly IMonitor monitor;
        private readonly Dictionary<string, EventEvaluation> evaluations = new();
        private Dictionary<string, List<EventInfo>> byOwner = new();
        private Dictionary<string, EventInfo> byId = new();

        /// <summary>Increments whenever data or evaluations change, so UI can rebuild.</summary>
        public int Version { get; private set; }

        public IReadOnlyDictionary<string, List<EventInfo>> ByOwner => this.byOwner;

        public EventIndex(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public void Clear()
        {
            this.byOwner = new();
            this.byId = new();
            this.Invalidate();
        }

        /// <summary>Re-reads event data for every known location.</summary>
        public void Rebuild()
        {
            var locationNames = new HashSet<string>(Game1.locationData.Keys);
            Utility.ForEachLocation(location =>
            {
                locationNames.Add(location.NameOrUniqueName);
                return true;
            });

            var seenAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var all = new List<EventInfo>();
            int locationCount = 0;

            foreach (string name in locationNames)
            {
                Dictionary<string, string>? events;
                string assetName;
                try
                {
                    GameLocation? location = Game1.getLocationFromName(name);
                    if (location != null)
                    {
                        if (!location.TryGetLocationEvents(out assetName, out events))
                            continue;
                    }
                    else
                    {
                        assetName = "Data\\Events\\" + name;
                        if (!Game1.content.DoesAssetExist<Dictionary<string, string>>(assetName))
                            continue;
                        events = Game1.content.Load<Dictionary<string, string>>(assetName);
                    }
                }
                catch (Exception ex)
                {
                    this.monitor.Log($"Couldn't read events for location '{name}': {ex.Message}", LogLevel.Trace);
                    continue;
                }

                if (events == null || !seenAssets.Add(assetName))
                    continue;

                locationCount++;
                string displayName = GetLocationDisplayName(name);
                foreach ((string key, string script) in events)
                {
                    try
                    {
                        EventInfo? info = Parse(name, displayName, key, script);
                        if (info != null)
                            all.Add(info);
                    }
                    catch (Exception ex)
                    {
                        this.monitor.Log($"Skipped event '{key}' in {name}: {ex.Message}", LogLevel.Trace);
                    }
                }
            }

            this.byOwner = all
                .GroupBy(e => e.Owner)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.RequiredPoints).ThenBy(e => e.LocationDisplayName).ToList());
            this.byId = new();
            foreach (EventInfo info in all)
                this.byId.TryAdd(info.Id, info);

            this.Invalidate();
            this.monitor.Log($"Indexed {all.Count} events across {locationCount} locations ({this.byOwner.Count} groups).", LogLevel.Debug);
        }

        /// <summary>Drops cached evaluations; call when time, location, weather or friendship may have changed.</summary>
        public void Invalidate()
        {
            this.evaluations.Clear();
            this.Version++;
        }

        public EventEvaluation Evaluate(EventInfo evt)
        {
            if (!this.evaluations.TryGetValue(evt.Key, out EventEvaluation? eval))
                this.evaluations[evt.Key] = eval = EventEvaluator.Evaluate(evt);
            return eval;
        }

        public EventInfo? FindById(string id) => this.byId.TryGetValue(id, out EventInfo? info) ? info : null;

        public IReadOnlyList<EventInfo> GetEvents(string owner) =>
            this.byOwner.TryGetValue(owner, out List<EventInfo>? list) ? list : Array.Empty<EventInfo>();

        /// <summary>Finds an owner key by internal or display name, ignoring case.</summary>
        public string? FindOwner(string name)
        {
            return this.byOwner.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? this.byOwner.Keys.FirstOrDefault(k => GetNpcDisplayName(k).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public PendingEvents GetPending(string owner)
        {
            var result = new PendingEvents();
            foreach (EventInfo evt in this.GetEvents(owner))
            {
                EventEvaluation eval = this.Evaluate(evt);
                switch (eval.Status)
                {
                    case EventStatus.Ready:
                    case EventStatus.Pending:
                    case EventStatus.Special:
                        if (evt.IsHeartEvent || owner == OtherKey)
                            result.Pending.Add((evt, eval));
                        else
                            result.OtherScenes.Add((evt, eval));
                        break;

                    case EventStatus.Locked:
                        result.Locked++;
                        if (result.NextLocked == null || evt.RequiredPoints < result.NextLocked.Value.Event.RequiredPoints)
                            result.NextLocked = (evt, eval);
                        break;

                    case EventStatus.Unreachable:
                        result.Unreachable++;
                        break;
                }
            }

            // ready first, then by heart requirement
            static int Compare((EventInfo Event, EventEvaluation Eval) a, (EventInfo Event, EventEvaluation Eval) b)
            {
                int byStatus = a.Eval.Status.CompareTo(b.Eval.Status);
                return byStatus != 0 ? byStatus : a.Event.RequiredPoints.CompareTo(b.Event.RequiredPoints);
            }
            result.Pending.Sort(Compare);
            result.OtherScenes.Sort(Compare);
            return result;
        }

        /// <summary>A label for an event ID, e.g. "Abigail's 4-heart event at Mountain (#4)".</summary>
        public string DescribeEvent(string id)
        {
            EventInfo? info = this.FindById(id);
            if (info == null)
                return $"event #{id}";

            string label = info.Owner == OtherKey
                ? $"{info.Title.ToLowerInvariant()} at {info.LocationDisplayName} (#{id})"
                : $"{GetNpcDisplayName(info.Owner)}'s {info.Title.ToLowerInvariant()} at {info.LocationDisplayName} (#{id})";
            return info.IsSpecial ? label + ", which the mod's code starts" : label;
        }

        public static string GetNpcDisplayName(string name)
        {
            if (name == OtherKey)
                return "Other events";

            try
            {
                NPC? npc = Game1.getCharacterFromName(name);
                if (!string.IsNullOrWhiteSpace(npc?.displayName))
                    return npc.displayName;
                if (Game1.characterData.TryGetValue(name, out var data) && !string.IsNullOrWhiteSpace(data.DisplayName))
                    return TokenParser.ParseText(data.DisplayName);
            }
            catch (Exception)
            {
                // fall back to the internal name
            }
            return name;
        }

        private static EventInfo? Parse(string locationName, string locationDisplayName, string key, string script)
        {
            // keys without preconditions are fork/branch scripts, not triggerable events
            if (!key.Contains('/'))
                return null;

            string[] parts = Event.SplitPreconditions(key);
            string id = parts[0].Trim();
            if (id.Length == 0 || id == "-1")
                return null;

            Precondition[] conditions = parts
                .Skip(1)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .Select(Precondition.Parse)
                .ToArray();

            (string owner, bool isHeartEvent) = FindOwner(conditions, script);
            return new EventInfo(id, locationName, locationDisplayName, owner, isHeartEvent, conditions);
        }

        /// <summary>Picks the NPC an event belongs to: friendship requirement, then dating/spouse, then first actor in the script.</summary>
        private static (string Owner, bool FromConditions) FindOwner(Precondition[] conditions, string script)
        {
            foreach (Precondition condition in conditions.Where(c => !c.Negated))
            {
                if (condition.Is("Friendship"))
                {
                    foreach ((string npc, _) in EventInfo.FriendshipPairs(condition))
                        return (npc, true);
                }
                else if ((condition.Is("Dating") || condition.Is("Spouse") || condition.Is("Roommate")) && condition.Args.Length > 0)
                    return (condition.Args[0], true);
            }

            // script format: music/viewport/actors/...; actors are 'name x y direction' groups
            string[] fields = script.Split('/');
            if (fields.Length > 2)
            {
                string[] actorTokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < actorTokens.Length; i += 4)
                {
                    string actor = actorTokens[i];
                    if (!actor.StartsWith("farmer", StringComparison.OrdinalIgnoreCase) && Game1.characterData.ContainsKey(actor))
                        return (actor, false);
                }
            }

            return (OtherKey, false);
        }

        private static string GetLocationDisplayName(string name)
        {
            try
            {
                string? displayName = Game1.getLocationFromName(name)?.DisplayName;
                if (string.IsNullOrWhiteSpace(displayName) && Game1.locationData.TryGetValue(name, out var data) && !string.IsNullOrWhiteSpace(data.DisplayName))
                    displayName = TokenParser.ParseText(data.DisplayName);
                // mods sometimes point at a missing translation, which renders as "(no translation:...)"
                if (!string.IsNullOrWhiteSpace(displayName) && displayName != name && !displayName.Contains("no translation") && !displayName.StartsWith('['))
                    return displayName;
            }
            catch (Exception)
            {
                // fall back to a prettified internal name
            }

            // "Custom_RSV_RidgesideVillage" -> "Ridgeside Village"
            string pretty = Regex.Replace(name, @"^(Custom_)?([A-Z]{2,4}_)?", "");
            pretty = Regex.Replace(pretty.Replace('_', ' '), "(?<=[a-z])(?=[A-Z])", " ");
            return pretty.Length > 0 ? pretty : name;
        }
    }
}
