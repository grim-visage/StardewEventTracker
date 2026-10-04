using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace StardewEventTracker.Data
{
    /// <summary>A group's unseen events, split into what can still happen and what's locked.</summary>
    internal sealed class PendingEvents
    {
        /// <summary>Unseen, unlocked events that can still happen, sorted by status then hearts.</summary>
        public List<(EventInfo Event, EventEvaluation Eval)> Pending { get; } = new();

        /// <summary>Events that need more friendship, lowest requirement first.</summary>
        public List<(EventInfo Event, EventEvaluation Eval)> LockedEvents { get; } = new();

        public (EventInfo Event, EventEvaluation Eval)? NextLocked => this.LockedEvents.Count > 0 ? this.LockedEvents[0] : null;
        public int Unreachable { get; set; }

        public int Count(EventStatus status) => this.Pending.Count(p => p.Eval.Status == status);

        /// <summary>Statuses where the player only has to show up at the right time or on the right day.</summary>
        public static bool IsActionable(EventStatus status) =>
            status is EventStatus.AvailableNow or EventStatus.LaterToday or EventStatus.WrongDay or EventStatus.GreenRain or EventStatus.FestivalHere or EventStatus.MissedToday;

        /// <summary>
        /// The event to follow next: one the player can act on today if there is one, otherwise whichever comes
        /// earliest in the NPC's story among the not-yet and locked events (so a marriage event never jumps ahead
        /// of an unlocked 4-heart event).
        /// </summary>
        public (EventInfo Event, EventEvaluation Eval)? GetNext(Func<EventInfo, bool> skip)
        {
            var awake = this.Pending.Where(p => !skip(p.Event)).ToList();
            var actionable = awake.FirstOrDefault(p => IsActionable(p.Eval.Status));
            if (actionable.Event != null)
                return actionable;

            var candidates = awake.Where(p => p.Eval.Status == EventStatus.NotYet).ToList();
            if (this.NextLocked is { } locked)
                candidates.Add(locked);
            if (candidates.Count > 0)
                return candidates.OrderBy(p => p.Event.ProgressRank).ThenBy(p => p.Eval.Status).ThenBy(p => p.Eval.UnmetCount).First();

            return awake.Count > 0 ? awake[0] : null;
        }
    }

    /// <summary>Every event in the game's current data: heart events by NPC, story events by location.</summary>
    internal sealed class EventIndex
    {
        /// <summary>Owner of story events, which aren't tied to one NPC.</summary>
        public const string OtherKey = "";

        /// <summary>Location key for events that can start in any location.</summary>
        public const string AnywhereKey = "*";

        /// <summary>
        /// Farmhouse events the game copies into every location's event list (GameLocation.TryGetLocationEvents),
        /// so they can start anywhere. They're indexed once, as "any location".
        /// </summary>
        private static bool IsGlobalEvent(string key) => key.StartsWith("558291/") || key.StartsWith("558292/");

        private readonly IMonitor monitor;
        private readonly Dictionary<string, EventEvaluation> evaluations = new();

        /// <summary>Each group's pending events, until the next <see cref="Invalidate"/>; the HUD, map, reminders and menu all ask for them.</summary>
        private readonly Dictionary<string, PendingEvents> pending = new();

        /// <summary>Everything the menu's search looks at for each event, until the next <see cref="Invalidate"/>.</summary>
        private readonly Dictionary<string, string[]> searchText = new();

        private Dictionary<string, List<EventInfo>> byOwner = new();
        private Dictionary<string, List<EventInfo>> storyByLocation = new();
        private Dictionary<string, EventInfo> byId = new();
        private Dictionary<string, EventInfo> byKey = new();

        /// <summary>Events that require having seen a given event ID.</summary>
        private Dictionary<string, List<EventInfo>> unlockedBy = new();

        /// <summary>Where mail flags and conversation topics in event requirements come from.</summary>
        public FlagSources Flags { get; } = new();

        /// <summary>Increments whenever data or evaluations change, so UI can rebuild.</summary>
        public int Version { get; private set; }

        /// <summary>Heart events by the NPC's internal name.</summary>
        public IReadOnlyDictionary<string, List<EventInfo>> ByOwner => this.byOwner;

        /// <summary>Story events by internal location name.</summary>
        public IReadOnlyDictionary<string, List<EventInfo>> StoryByLocation => this.storyByLocation;

        public EventIndex(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        public void Clear()
        {
            NpcNameCache.Clear();
            this.byOwner = new();
            this.storyByLocation = new();
            this.byId = new();
            this.byKey = new();
            this.unlockedBy = new();
            this.Invalidate();
        }

        /// <summary>Re-reads event data for every known location.</summary>
        public void Rebuild()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            NpcNameCache.Clear();
            CalendarInfo.Clear();
            DoorAccess.EnsureScanned(this.monitor);
            this.Flags.Rebuild(this.monitor);
            var locationNames = new HashSet<string>(Game1.locationData.Keys);
            Utility.ForEachLocation(location =>
            {
                locationNames.Add(location.NameOrUniqueName);
                return true;
            });

            var seenAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // the same event (ID and script) listed under two locations, e.g. Pam's trailer before and after its upgrade
            var seenEvents = new HashSet<string>();
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

                bool isFarmHouseAsset = assetName.Equals("Data\\Events\\FarmHouse", StringComparison.OrdinalIgnoreCase);
                string displayName = GetLocationDisplayName(name);
                bool any = false;
                foreach ((string key, string script) in events)
                {
                    bool global = IsGlobalEvent(key);
                    if (global && !isFarmHouseAsset)
                        continue;

                    // branches of other events can set flags, so they're scanned too
                    if (!key.Contains('/') && !GameLocation.IsValidLocationEvent(key, script))
                    {
                        this.Flags.AddFork(name, key, script);
                        continue;
                    }

                    try
                    {
                        EventInfo? info = global
                            ? Parse(AnywhereKey, I18n.Get("location.anywhere"), key, script)
                            : Parse(name, displayName, key, script);
                        if (info != null && seenEvents.Add(info.Id + "\n" + script))
                        {
                            all.Add(info);
                            this.Flags.ScanScript(info, script);
                            any = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        this.monitor.Log($"Skipped event '{key}' in {name}: {ex.Message}", LogLevel.Trace);
                    }
                }
                if (any)
                    locationCount++;
            }

            this.byOwner = all
                .Where(e => e.IsHeartEvent)
                .GroupBy(e => e.Owner)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.BaseRank).ThenBy(e => e.LocationDisplayName).ToList());
            this.storyByLocation = all
                .Where(e => e.IsStory)
                .GroupBy(e => e.LocationName)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Id).ToList());
            this.byId = new();
            this.byKey = new();
            foreach (EventInfo info in all)
            {
                this.byId.TryAdd(info.Id, info);
                this.byKey.TryAdd(info.Key, info);
            }

            this.Flags.ResolveForks();
            this.ComputeProgressRanks(all);

            this.unlockedBy = new();
            foreach (EventInfo info in all)
            {
                foreach (Precondition c in info.Conditions.Where(c => c.Is("SawEvent") && !c.Negated))
                {
                    foreach (string id in c.Args.Distinct())
                    {
                        if (!this.unlockedBy.TryGetValue(id, out List<EventInfo>? list))
                            this.unlockedBy[id] = list = new List<EventInfo>();
                        // one entry per event, however many versions of it need this one
                        if (!list.Any(e => e.Key == info.Key))
                            list.Add(info);
                    }
                }
            }

            this.Invalidate();
            this.monitor.Log($"Indexed {all.Count} events across {locationCount} locations ({this.byOwner.Count} NPCs with heart events, {this.storyByLocation.Count} locations with story events) in {timer.ElapsedMilliseconds}ms.", LogLevel.Debug);
        }

        /// <summary>Drops cached evaluations; call when time, location, weather or friendship may have changed.</summary>
        public void Invalidate()
        {
            this.evaluations.Clear();
            this.pending.Clear();
            this.searchText.Clear();
            this.Version++;
        }

        public EventEvaluation Evaluate(EventInfo evt)
        {
            if (!this.evaluations.TryGetValue(evt.EntryKey, out EventEvaluation? eval))
                this.evaluations[evt.EntryKey] = eval = EventEvaluator.Evaluate(evt, this.Flags);
            return eval;
        }

        /// <summary>Events that need this event to have been seen first, i.e. what seeing it leads to.</summary>
        public IReadOnlyList<EventInfo> GetUnlocks(string id) =>
            this.unlockedBy.TryGetValue(id, out List<EventInfo>? list) ? list : Array.Empty<EventInfo>();

        public EventInfo? FindByKey(string key) => this.byKey.TryGetValue(key, out EventInfo? info) ? info : null;

        public EventInfo? FindById(string id) => this.byId.TryGetValue(id, out EventInfo? info) ? info : null;

        /// <summary>An NPC's heart events.</summary>
        public IReadOnlyList<EventInfo> GetEvents(string owner) =>
            this.byOwner.TryGetValue(owner, out List<EventInfo>? list) ? list : Array.Empty<EventInfo>();

        /// <summary>A location's story events.</summary>
        public IReadOnlyList<EventInfo> GetStoryEvents(string location) =>
            this.storyByLocation.TryGetValue(location, out List<EventInfo>? list) ? list : Array.Empty<EventInfo>();

        /// <summary>Finds an NPC with heart events by internal or display name, ignoring case.</summary>
        public string? FindOwner(string name)
        {
            return this.byOwner.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? this.byOwner.Keys.FirstOrDefault(k => GetNpcDisplayName(k).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Finds a location with story events by internal or display name, ignoring case.</summary>
        public string? FindStoryLocation(string name)
        {
            return this.storyByLocation.Keys.FirstOrDefault(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? this.storyByLocation.Keys.FirstOrDefault(k => this.GetLocationName(k).Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The display name of a location that has story events.</summary>
        public string GetLocationName(string location) =>
            this.GetStoryEvents(location).FirstOrDefault()?.LocationDisplayName ?? location;

        public PendingEvents GetPending(string owner) => this.CollectCached("hearts:" + owner, this.GetEvents(owner));

        public PendingEvents GetStoryPending(string location) => this.CollectCached("story:" + location, this.GetStoryEvents(location));

        /// <summary>The event's ID, title, location, NPCs and requirements as text, for the menu's search.</summary>
        public IReadOnlyList<string> GetSearchText(EventInfo evt)
        {
            if (!this.searchText.TryGetValue(evt.EntryKey, out string[]? text))
            {
                IEnumerable<string> parts = new[] { evt.Id, evt.Title, evt.LocationDisplayName, evt.LocationName }
                    .Concat(evt.Actors.Select(GetNpcDisplayName))
                    .Concat(evt.Conditions.Select(c => PreconditionFormatter.Describe(c, this, evt)));
                if (evt.IsHeartEvent)
                    parts = parts.Append(GetNpcDisplayName(evt.Owner));
                this.searchText[evt.EntryKey] = text = parts.ToArray();
            }
            return text;
        }

        private PendingEvents CollectCached(string key, IEnumerable<EventInfo> events)
        {
            if (!this.pending.TryGetValue(key, out PendingEvents? result))
                this.pending[key] = result = this.Collect(events);
            return result;
        }

        private PendingEvents Collect(IEnumerable<EventInfo> events)
        {
            var result = new PendingEvents();
            foreach ((EventInfo evt, EventEvaluation eval) in this.BestVersions(events))
            {
                switch (eval.Status)
                {
                    case EventStatus.Seen:
                        break;

                    case EventStatus.Locked:
                        result.LockedEvents.Add((evt, eval));
                        break;

                    case EventStatus.Unreachable:
                        result.Unreachable++;
                        break;

                    default:
                        result.Pending.Add((evt, eval));
                        break;
                }
            }

            // most actionable first, then by heart requirement, then whichever needs the fewest things done
            var pending = result.Pending
                .OrderBy(p => p.Eval.Status)
                .ThenBy(p => p.Event.ProgressRank)
                .ThenBy(p => p.Eval.UnmetCount)
                .ToList();
            var locked = result.LockedEvents
                .OrderBy(p => p.Event.ProgressRank)
                .ThenBy(p => p.Eval.UnmetCount)
                .ToList();
            result.Pending.Clear();
            result.Pending.AddRange(pending);
            result.LockedEvents.Clear();
            result.LockedEvents.AddRange(locked);
            return result;
        }

        /// <summary>
        /// One entry per event: mods sometimes list several versions of an event (same ID and location, different
        /// requirements, e.g. one for rain and one for sun), and whichever matches first plays. Shows the version
        /// closest to happening, then the one needing the fewest things done.
        /// </summary>
        private IEnumerable<(EventInfo Event, EventEvaluation Eval)> BestVersions(IEnumerable<EventInfo> events)
        {
            return events
                .GroupBy(e => e.Key)
                .Select(g => g
                    .Select(e => (Event: e, Eval: this.Evaluate(e)))
                    .OrderBy(p => p.Eval.Status)
                    .ThenBy(p => p.Eval.UnmetCount)
                    .First());
        }

        /// <summary>Each event once, dropping the extra versions some mods list (see <see cref="BestVersions"/>).</summary>
        public static IEnumerable<EventInfo> Distinct(IEnumerable<EventInfo> events) => events.DistinctBy(e => e.Key);

        /// <summary>A label for an event ID, e.g. "Abigail's 4-heart event at Mountain (#4)".</summary>
        public string DescribeEvent(string id)
        {
            EventInfo? info = this.FindById(id);
            if (info == null)
                return I18n.Get("describe.unknown", new { id });

            var tokens = new { name = GetNpcDisplayName(info.Owner), title = info.TitleInline, location = info.LocationDisplayName, id };
            string label = I18n.Get(info.IsStory ? "describe.story" : "describe.heart", tokens);
            return info.IsSpecial ? I18n.Get("describe.special", new { label }) : label;
        }

        /// <summary>Raises each event's rank to that of any later event it needs to have seen first (e.g. a marriage event).</summary>
        private void ComputeProgressRanks(List<EventInfo> all)
        {
            var visiting = new HashSet<string>();
            var done = new HashSet<string>();

            int Rank(EventInfo evt)
            {
                if (done.Contains(evt.EntryKey) || !visiting.Add(evt.EntryKey))
                    return evt.ProgressRank;

                int rank = evt.BaseRank;
                foreach (Precondition c in evt.Conditions.Where(c => c.Is("SawEvent") && !c.Negated))
                {
                    // 'seen any of' these: the earliest one is enough
                    var needed = c.Args.Select(this.FindById).Where(e => e != null).Select(e => Rank(e!)).ToList();
                    if (needed.Count > 0)
                        rank = Math.Max(rank, needed.Min());
                }

                visiting.Remove(evt.EntryKey);
                done.Add(evt.EntryKey);
                return evt.ProgressRank = rank;
            }

            foreach (EventInfo evt in all)
                Rank(evt);
        }

        /// <summary>A short label for an event ID, e.g. "Leah's 4-heart event", for compact text like the HUD.</summary>
        public string DescribeEventShort(string id)
        {
            EventInfo? info = this.FindById(id);
            if (info == null)
                return I18n.Get("describe.unknown", new { id });

            return info.IsStory
                ? I18n.Get("describe.story-short", new { title = info.TitleInline, location = EventNarrator.WithArticle(info.LocationDisplayName) })
                : I18n.Get("describe.heart-short", new { name = GetNpcDisplayName(info.Owner), title = info.TitleInline });
        }

        /// <summary>Display names by internal name; looking up an NPC searches every location, so this is cached.</summary>
        private static readonly Dictionary<string, string> NpcNameCache = new();

        public static string GetNpcDisplayName(string name)
        {
            if (NpcNameCache.TryGetValue(name, out string? cached))
                return cached;

            string result = name;
            try
            {
                NPC? npc = Game1.getCharacterFromName(name);
                if (!string.IsNullOrWhiteSpace(npc?.displayName))
                    result = npc.displayName;
                else if (Game1.characterData.TryGetValue(name, out var data) && !string.IsNullOrWhiteSpace(data.DisplayName))
                    result = TokenParser.ParseText(data.DisplayName);
            }
            catch (Exception)
            {
                // fall back to the internal name
            }
            return NpcNameCache[name] = result;
        }

        private static EventInfo? Parse(string locationName, string locationDisplayName, string key, string script)
        {
            // the game's own check: fork/branch scripts and other non-event entries fail it
            if (!GameLocation.IsValidLocationEvent(key, script))
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

            string[] actors = ParseActors(script);
            string? owner = FindRelationshipOwner(conditions);
            int doorPoints = 0;

            // behind a door that needs their friendship (Caroline's Sunroom): their heart event, with the door's hearts
            if (owner == null && DoorAccess.GetResidentFriendship(locationName) is { } resident && actors.Contains(resident.Npc))
                (owner, doorPoints) = resident;

            return new EventInfo(id, locationName, locationDisplayName, owner ?? OtherKey, owner != null, actors, conditions, key, doorPoints);
        }

        /// <summary>The NPC an event needs friendship, dating or marriage with, if any.</summary>
        private static string? FindRelationshipOwner(Precondition[] conditions)
        {
            foreach (Precondition condition in conditions.Where(c => !c.Negated))
            {
                if (condition.Is("Friendship"))
                {
                    foreach ((string npc, _) in EventInfo.FriendshipPairs(condition))
                        return npc;
                }
                else if ((condition.Is("Dating") || condition.Is("Spouse") || condition.Is("Roommate")) && condition.Args.Length > 0)
                    return condition.Args[0];
            }
            return null;
        }

        /// <summary>The NPCs in an event's script, in order.</summary>
        private static string[] ParseActors(string script)
        {
            // script format: music/viewport/actors/...; actors are 'name x y direction' groups
            string[] fields = script.Split('/');
            if (fields.Length <= 2)
                return Array.Empty<string>();

            string[] tokens = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var actors = new List<string>();
            for (int i = 0; i < tokens.Length; i += 4)
            {
                string actor = tokens[i];
                if (!actor.StartsWith("farmer", StringComparison.OrdinalIgnoreCase) && Game1.characterData.ContainsKey(actor) && !actors.Contains(actor))
                    actors.Add(actor);
            }
            return actors.ToArray();
        }

        internal static string GetLocationDisplayName(string name)
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
