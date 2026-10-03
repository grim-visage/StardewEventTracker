using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;

namespace StardewEventTracker.Data
{
    internal enum ConditionCategory
    {
        /// <summary>Hearts, earlier events, mail flags, items: something the player has to do.</summary>
        Progress,

        /// <summary>Weather, day of week/month, season: may be fine on another day.</summary>
        Calendar,

        /// <summary>The time-of-day window: the player only has to show up at the right time.</summary>
        Time
    }

    /// <summary>An event's time-of-day window, in the game's HHMM format (e.g. 600 to 1200).</summary>
    internal readonly record struct TimeWindow(int Start, int End)
    {
        /// <summary>Converts HHMM to minutes since midnight (e.g. 930 -> 570).</summary>
        public static int ToMinutes(int time) => time / 100 * 60 + time % 100;

        public int MinutesUntilStart(int now) => ToMinutes(this.Start) - ToMinutes(now);
        public int MinutesUntilEnd(int now) => ToMinutes(this.End) - ToMinutes(now);
    }

    /// <summary>One precondition from an event key, e.g. <c>f Abigail 1000</c>.</summary>
    internal sealed class Precondition
    {
        /// <summary>The precondition exactly as written in the event key, passed to the game for evaluation.</summary>
        public string Raw { get; }

        /// <summary>The canonical 1.6 name (e.g. <c>Friendship</c>), with legacy aliases resolved.</summary>
        public string Name { get; }

        /// <summary>Whether the condition is inverted (a <c>!</c> prefix, or a legacy inverted alias like <c>k</c>).</summary>
        public bool Negated { get; }

        public string[] Args { get; }

        private Precondition(string raw, string name, bool negated, string[] args)
        {
            this.Raw = raw;
            this.Name = name;
            this.Negated = negated;
            this.Args = args;
        }

        public bool Is(string name) => this.Name.Equals(name, StringComparison.OrdinalIgnoreCase);

        public ConditionCategory Category =>
            this.Is("Time") ? ConditionCategory.Time
            : this.Is("Weather") || this.Is("DayOfWeek") || this.Is("DayOfMonth") || this.Is("Season") || this.Is("FestivalDay") || this.Is("UpcomingFestival")
                ? ConditionCategory.Calendar
            : ConditionCategory.Progress;

        /// <summary>
        /// A condition mods use to stop the game from ever starting the event on location entry, because their own
        /// code plays it instead (e.g. Ridgeside's <c>n InexistentMailFlag</c>, or <c>GameStateQuery FALSE</c>).
        /// </summary>
        public bool IsNeverTrue =>
            !this.Negated
            && (
                ((this.Is("HostOrLocalMail") || this.Is("HostMail")) && this.Args.Any(a => NeverFlagPattern.IsMatch(a)))
                || (this.Is("GameStateQuery") && this.Args.Length == 1 && this.Args[0].Equals("FALSE", StringComparison.OrdinalIgnoreCase))
            );

        private static readonly Regex NeverFlagPattern = new("(inexist|nonexist|never|impossible)", RegexOptions.IgnoreCase);

        public static Precondition Parse(string raw)
        {
            string[] parts = raw.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string key = parts.Length > 0 ? parts[0] : raw;
            bool negated = false;
            if (key.StartsWith('!'))
            {
                negated = true;
                key = key[1..];
            }

            if (Aliases.TryGetValue(key, out var alias))
            {
                key = alias.Name;
                negated ^= alias.Inverted;
            }

            return new Precondition(raw, key, negated, parts.Skip(1).ToArray());
        }

        /// <summary>Legacy single-letter aliases (case-sensitive) and whether they invert the long-form condition.</summary>
        private static readonly Dictionary<string, (string Name, bool Inverted)> Aliases = new(StringComparer.Ordinal)
        {
            ["a"] = ("Tile", false),
            ["b"] = ("ReachedMineBottom", false),
            ["C"] = ("CommunityCenterOrWarehouseDone", false),
            ["D"] = ("Dating", false),
            ["d"] = ("DayOfWeek", true),
            ["e"] = ("SawEvent", false),
            ["k"] = ("SawEvent", true),
            ["F"] = ("FestivalDay", true),
            ["f"] = ("Friendship", false),
            ["G"] = ("GameStateQuery", false),
            ["g"] = ("Gender", false),
            ["H"] = ("IsHost", false),
            ["Hn"] = ("HostMail", false),
            ["Hl"] = ("HostMail", true),
            ["h"] = ("MissingPet", false),
            ["i"] = ("HasItem", false),
            ["J"] = ("JojaBundlesDone", false),
            ["j"] = ("DaysPlayed", false),
            ["L"] = ("InUpgradedHouse", false),
            ["l"] = ("HostOrLocalMail", true),
            ["m"] = ("EarnedMoney", false),
            ["N"] = ("GoldenWalnuts", false),
            ["n"] = ("HostOrLocalMail", false),
            ["O"] = ("Spouse", false),
            ["o"] = ("Spouse", true),
            ["p"] = ("NpcVisibleHere", false),
            ["q"] = ("ChoseDialogueAnswers", false),
            ["R"] = ("Roommate", false),
            ["Rf"] = ("Roommate", true),
            ["r"] = ("Random", false),
            ["S"] = ("SawSecretNote", false),
            ["s"] = ("Shipped", false),
            ["t"] = ("Time", false),
            ["U"] = ("UpcomingFestival", true),
            ["u"] = ("DayOfMonth", false),
            ["v"] = ("NpcVisible", false),
            ["w"] = ("Weather", false),
            ["x"] = ("SendMail", false),
            ["y"] = ("Year", false),
            ["z"] = ("Season", true)
        };
    }

    /// <summary>A triggerable event from <c>Data/Events/{location}</c>.</summary>
    internal sealed class EventInfo
    {
        public string Id { get; }
        public string LocationName { get; }
        public string LocationDisplayName { get; }

        /// <summary>Internal name of the NPC whose heart event this is, or <see cref="EventIndex.OtherKey"/> for story events.</summary>
        public string Owner { get; }

        /// <summary>Internal names of the NPCs who appear in the scene, in script order.</summary>
        public IReadOnlyList<string> Actors { get; }

        /// <summary>The event's time-of-day window, if it has one.</summary>
        public TimeWindow? Window { get; }

        public IReadOnlyList<Precondition> Conditions { get; }

        /// <summary>Friendship points with <see cref="Owner"/> the event requires, or 0.</summary>
        public int RequiredPoints { get; }

        /// <summary>The event can't fire on a normal location entry (it only sends mail, or a mod's code starts it).</summary>
        public bool IsSpecial { get; }

        /// <summary>The event requires friendship, dating or marriage with <see cref="Owner"/>.</summary>
        public bool IsHeartEvent { get; }

        /// <summary>An event with no relationship requirement; grouped by location rather than NPC.</summary>
        public bool IsStory => !this.IsHeartEvent;

        /// <summary>Unique across locations, since mods can reuse an ID in different locations.</summary>
        public string Key => $"{this.LocationName}|{this.Id}";

        public bool Seen => Game1.player.eventsSeen.Contains(this.Id);

        public int RequiredHearts => this.RequiredPoints / NPC.friendshipPointsPerHeartLevel;

        /// <summary>A short name like "4-heart event" or "Story event".</summary>
        public string Title => this.GetTitle("title");

        /// <summary>The title as it reads mid-sentence, e.g. "Abigail's 4-heart event".</summary>
        public string TitleInline => this.GetTitle("title-inline");

        /// <summary>The relationship the event needs with its owner ("dating", "spouse", "roommate"), if any.</summary>
        public string? Relationship { get; }

        /// <summary>
        /// How far along the NPC's story the event is, in friendship points: its heart requirement, raised for
        /// dating (8 hearts) or marriage (past 10 hearts), and for needing a later event first. Used for ordering.
        /// </summary>
        public int ProgressRank { get; internal set; }

        /// <summary>The rank from the event's own requirements, before earlier events are taken into account.</summary>
        internal int BaseRank { get; }

        private string GetTitle(string prefix)
        {
            if (this.Relationship != null && this.RequiredPoints < 2000)
                return I18n.Get($"{prefix}.{this.Relationship}");
            if (this.RequiredPoints > 0)
                return this.RequiredPoints % NPC.friendshipPointsPerHeartLevel == 0
                    ? I18n.Get($"{prefix}.hearts", new { hearts = this.RequiredHearts })
                    : I18n.Get($"{prefix}.points", new { points = this.RequiredPoints });
            return I18n.Get($"{prefix}.story");
        }

        public EventInfo(string id, string locationName, string locationDisplayName, string owner, bool isHeartEvent, IReadOnlyList<string> actors, IReadOnlyList<Precondition> conditions)
        {
            this.Id = id;
            this.LocationName = locationName;
            this.LocationDisplayName = locationDisplayName;
            this.Owner = owner;
            this.IsHeartEvent = isHeartEvent;
            this.Actors = actors;
            this.Conditions = conditions;

            Precondition? time = conditions.FirstOrDefault(c => c.Is("Time") && !c.Negated && c.Args.Length >= 2);
            if (time != null && int.TryParse(time.Args[0], out int start) && int.TryParse(time.Args[1], out int end))
                this.Window = new TimeWindow(start, end);
            this.IsSpecial = conditions.Any(c => c.Is("SendMail") || c.IsNeverTrue);
            this.RequiredPoints = conditions
                .Where(c => c.Is("Friendship") && !c.Negated)
                .SelectMany(c => FriendshipPairs(c))
                .Where(p => p.Npc == owner)
                .Select(p => p.Points)
                .DefaultIfEmpty(0)
                .Max();

            Precondition? relationship = conditions.FirstOrDefault(c => !c.Negated && c.Args.FirstOrDefault() == owner && (c.Is("Dating") || c.Is("Spouse") || c.Is("Roommate")));
            this.Relationship = relationship?.Name.ToLowerInvariant() switch
            {
                "dating" => "dating",
                "spouse" => "spouse",
                "roommate" => "roommate",
                _ => null
            };

            int perHeart = NPC.friendshipPointsPerHeartLevel;
            this.BaseRank = this.Relationship switch
            {
                "dating" => Math.Max(this.RequiredPoints, 8 * perHeart),
                "spouse" or "roommate" => Math.Max(this.RequiredPoints, 10 * perHeart + 1),
                _ => this.RequiredPoints
            };
            this.ProgressRank = this.BaseRank;
        }

        /// <summary>Reads the <c>&lt;npc&gt; &lt;points&gt;</c> pairs of a Friendship precondition.</summary>
        public static IEnumerable<(string Npc, int Points)> FriendshipPairs(Precondition condition)
        {
            for (int i = 0; i + 1 < condition.Args.Length; i += 2)
            {
                if (int.TryParse(condition.Args[i + 1], out int points))
                    yield return (condition.Args[i], points);
            }
        }
    }
}
