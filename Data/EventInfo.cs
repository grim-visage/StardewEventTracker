using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewValley;

namespace NpcEventTracker.Data
{
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

        /// <summary>Internal name of the NPC this event belongs to, or <see cref="EventIndex.OtherKey"/>.</summary>
        public string Owner { get; }

        public IReadOnlyList<Precondition> Conditions { get; }

        /// <summary>Friendship points with <see cref="Owner"/> the event requires, or 0.</summary>
        public int RequiredPoints { get; }

        /// <summary>The event can't fire on a normal location entry (it only sends mail, or a mod's code starts it).</summary>
        public bool IsSpecial { get; }

        /// <summary>The owner comes from a friendship/dating/spouse requirement, not just from appearing in the scene.</summary>
        public bool IsHeartEvent { get; }

        /// <summary>Unique across locations, since mods can reuse an ID in different locations.</summary>
        public string Key => $"{this.LocationName}|{this.Id}";

        public bool Seen => Game1.player.eventsSeen.Contains(this.Id);

        public int RequiredHearts => this.RequiredPoints / NPC.friendshipPointsPerHeartLevel;

        public string Title
        {
            get
            {
                if (this.RequiredPoints > 0)
                    return this.RequiredPoints % NPC.friendshipPointsPerHeartLevel == 0
                        ? $"{this.RequiredHearts}-heart event"
                        : $"{this.RequiredPoints}-point event";
                return "Story event";
            }
        }

        public EventInfo(string id, string locationName, string locationDisplayName, string owner, bool isHeartEvent, IReadOnlyList<Precondition> conditions)
        {
            this.Id = id;
            this.LocationName = locationName;
            this.LocationDisplayName = locationDisplayName;
            this.Owner = owner;
            this.IsHeartEvent = isHeartEvent;
            this.Conditions = conditions;
            this.IsSpecial = conditions.Any(c => c.Is("SendMail") || c.IsNeverTrue);
            this.RequiredPoints = conditions
                .Where(c => c.Is("Friendship") && !c.Negated)
                .SelectMany(c => FriendshipPairs(c))
                .Where(p => p.Npc == owner)
                .Select(p => p.Points)
                .DefaultIfEmpty(0)
                .Max();
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
