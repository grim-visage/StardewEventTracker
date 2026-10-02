using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Characters;

namespace NpcEventTracker.Data
{
    /// <summary>Builds the player-facing, in-world wording for events: menu tags, HUD lines and reminders.</summary>
    internal static class EventNarrator
    {
        /// <summary>Multi-word place names that read naturally with "the".</summary>
        private static readonly HashSet<string> GenericPlaces = new(StringComparer.OrdinalIgnoreCase)
        {
            "Bus Stop", "Secret Woods", "Tide Pools", "Community Center", "Joja Mart", "Wizard Tower",
            "Mutant Bug Lair", "Witch Swamp", "Witch Hut", "Skull Cavern", "Quarry Mine", "Train Station"
        };

        /****
        ** Messages
        ****/
        /// <summary>"It's time to visit Robin at her home, the Carpenter's Shop."</summary>
        public static string AvailableNow(EventInfo evt)
        {
            if (evt.IsStory)
                return Pick(evt, $"Something seems to be happening at {Place(evt)}...", $"There's a stir over at {Place(evt)}. Maybe go take a look?");

            var w = new Words(evt);
            var options = new List<string>();
            if (w.IsHome)
            {
                options.Add($"It's time to visit {w.Name} at {w.Poss} home, {w.Place}.");
                options.Add($"{w.Name} seems to be home at {w.Place}. Why not stop by?");
            }
            else if (w.Rainy)
                options.Add($"On a rainy day like this, {w.Name} might be at {w.Place}.");
            else if (w.Evening)
                options.Add($"{w.Name} might be out at {w.Place} this evening.");
            else if (w.Morning)
                options.Add($"{w.Name} might be at {w.Place} this morning.");

            if (options.Count == 0)
            {
                options.Add($"It's time to visit {w.Name} at {w.Place}.");
                options.Add($"{w.Name} might be at {w.Place} right now.");
                options.Add($"Now might be a good time to find {w.Name} at {w.Place}.");
            }
            return Pick(evt, options.ToArray());
        }

        /// <summary>"Abigail should be at the Mountain around 9:00 am. Head out soon to catch her!"</summary>
        public static string Reminder(EventInfo evt, EventEvaluation eval, int minutesLeft)
        {
            string time = StartTime(evt, eval);
            if (evt.IsStory)
                return $"Something might happen at {Place(evt)} around {time}.";

            var w = new Words(evt);
            string where = w.IsHome ? $"home at {w.Place}" : $"at {w.Place}";
            if (minutesLeft >= 120)
                return Pick(evt,
                    $"{w.Name} should be {where} in a couple of hours, around {time}.",
                    $"Around {time}, {w.Name} might be {where}. There's still time to get ready.");
            if (minutesLeft >= 60)
                return Pick(evt,
                    $"{w.Name} should be {where} around {time}. Head out soon to catch {w.Obj}!",
                    $"In about an hour, {w.Name} might be {where}. Better start heading over.");
            return $"{w.Name} will be {where} soon, around {time}. Hurry!";
        }

        /// <summary>"Today looks like a good day to see Abigail at the Mountain (9:00 am-12:00 pm)."</summary>
        public static string MorningHeadsUp(EventInfo evt)
        {
            var w = new Words(evt);
            string window = evt.Window is { } tw ? $" ({PreconditionFormatter.Time(tw.Start)}-{PreconditionFormatter.Time(tw.End)})" : "";
            return w.Rainy
                ? $"It's raining. A perfect day to find {w.Name} at {w.Place}{window}."
                : $"Today looks like a good day to see {w.Name} at {w.Place}{window}.";
        }

        /// <summary>"Tomorrow looks rainy. A good day to find Abigail at the Mountain."</summary>
        public static string TomorrowHeadsUp(EventInfo evt, string weatherTomorrow)
        {
            bool mentionsWeather = evt.Conditions.Any(c => c.Is("Weather"));
            string sky = weatherTomorrow switch
            {
                "Rain" => "rainy",
                "Storm" => "stormy",
                "Snow" => "snowy",
                "Wind" => "windy",
                _ => "sunny"
            };
            string lead = mentionsWeather ? $"Tomorrow looks {sky}." : "Tomorrow should work out.";
            if (evt.IsStory)
                return $"{lead} Something might happen at {Place(evt)}.";

            var w = new Words(evt);
            return Pick(evt,
                $"{lead} A good day to find {w.Name} at {w.Place}.",
                $"{lead} {w.Name} might be at {w.Place}.");
        }

        /****
        ** Short forms
        ****/
        /// <summary>The bracketed status shown next to an event in the menu.</summary>
        public static string StatusTag(EventInfo evt, EventEvaluation eval, EventIndex index)
        {
            return eval.Status switch
            {
                EventStatus.AvailableNow => evt.Window is { } w ? $"Available now, until {PreconditionFormatter.Time(w.End)}" : "Available now",
                EventStatus.LaterToday => $"Later today, from {StartTime(evt, eval)} (in {PreconditionFormatter.FormatDuration(eval.MinutesUntilStart ?? 0)})",
                EventStatus.WrongDay => $"Wait for {WaitFor(evt, eval)}{TomorrowHint(eval, includeNo: true)}",
                EventStatus.GreenRain => $"No events during Green Rain{TomorrowHint(eval, includeNo: true)}",
                EventStatus.FestivalHere => $"Festival here today{TomorrowHint(eval, includeNo: true)}",
                EventStatus.MissedToday => $"Missed today's window{TomorrowHint(eval, includeNo: true)}",
                EventStatus.NotYet => $"Not yet: {eval.UnmetCount} requirement(s) to go",
                EventStatus.Special => "Special trigger: can't be started by visiting",
                EventStatus.Locked => $"Locked: needs {evt.RequiredHearts} hearts",
                EventStatus.Unreachable => "Can no longer happen",
                _ => "Seen"
            };
        }

        /// <summary>How urgent the HUD should make an event look.</summary>
        public enum HudTone { Go, Soon, Normal }

        /// <summary>The short second HUD line, e.g. "Head out soon - starts 9:00 am (in 45m)".</summary>
        /// <param name="reminderMinutes">The player's reminder intervals, which set the "Get ready" and "Head out soon" stages.</param>
        public static (string Text, HudTone Tone) HudLine(EventInfo evt, EventEvaluation eval, IEnumerable<int> reminderMinutes)
        {
            switch (eval.Status)
            {
                case EventStatus.AvailableNow:
                    string go = evt.IsStory ? "Something's happening!" : "Time to visit!";
                    return (evt.Window is { } w ? $"{go} Until {PreconditionFormatter.Time(w.End)}" : go, HudTone.Go);

                case EventStatus.LaterToday:
                    int minutes = eval.MinutesUntilStart ?? 0;
                    string when = $"starts {StartTime(evt, eval)} (in {PreconditionFormatter.FormatDuration(minutes)})";
                    var stages = reminderMinutes.Where(m => m > 0).Distinct().OrderBy(m => m).ToList();
                    if (stages.Count == 0)
                        stages = new List<int> { 60, 120 };
                    if (minutes <= stages[0])
                        return ($"Head out soon - {when}", HudTone.Soon);
                    if (minutes <= stages[^1])
                        return ($"Get ready - {when}", HudTone.Soon);
                    return ($"Later today - {when}", HudTone.Normal);

                case EventStatus.WrongDay:
                    return ($"Wait for {WaitFor(evt, eval)}{TomorrowHint(eval, includeNo: false)}", HudTone.Normal);
                case EventStatus.GreenRain:
                    return ($"Green Rain today{TomorrowHint(eval, includeNo: false)}", HudTone.Normal);
                case EventStatus.FestivalHere:
                    return ($"Festival here today{TomorrowHint(eval, includeNo: false)}", HudTone.Normal);
                case EventStatus.MissedToday:
                    return ($"Missed today{TomorrowHint(eval, includeNo: false)}", HudTone.Normal);
                case EventStatus.NotYet:
                    return ($"Not yet - {eval.UnmetCount} to go", HudTone.Normal);
                case EventStatus.Special:
                    return ("Special trigger", HudTone.Normal);
                default:
                    return (eval.Status.ToString(), HudTone.Normal);
            }
        }

        /// <summary>What day an event is waiting for, e.g. "a sunny day" or "a day other than Tue".</summary>
        public static string WaitFor(EventInfo evt, EventEvaluation eval)
        {
            var parts = new List<string>();
            for (int i = 0; i < evt.Conditions.Count; i++)
            {
                Precondition c = evt.Conditions[i];
                if (c.Category != ConditionCategory.Calendar || eval.States[i] != ConditionState.Unmet)
                    continue;

                string args = string.Join("/", c.Args.Select(Capitalize));
                parts.Add(c.Name.ToLowerInvariant() switch
                {
                    "weather" => $"a {string.Join(" or ", c.Args.Select(a => a.ToLowerInvariant()))} day",
                    "dayofweek" => c.Negated ? $"a day other than {args}" : $"a {args}",
                    "dayofmonth" => c.Negated ? $"a day other than the {args}" : $"day {string.Join(" or ", c.Args)}",
                    "season" => c.Negated ? $"a season other than {args}" : args,
                    "festivalday" => c.Negated ? "a day without a festival" : "a festival day",
                    "upcomingfestival" => c.Negated ? "a day without a festival coming up" : "a day before a festival",
                    _ => c.Raw
                });
            }
            return parts.Count > 0 ? string.Join(" and ", parts) : "another day";
        }

        /// <summary>A place name with "the" where it reads naturally: "the Mountain", but "Pierre's General Store" and "Ridge Falls".</summary>
        public static string WithArticle(string name)
        {
            if (name.Length == 0 || name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) || char.IsDigit(name[0]))
                return name;

            int possessive = name.IndexOf("'s", StringComparison.Ordinal);
            if (possessive > 0)
            {
                // "Harvey's Clinic" names a person; "Carpenter's Shop" doesn't
                string owner = name[..possessive];
                bool isPerson = Game1.characterData.ContainsKey(owner) || Game1.characterData.Keys.Any(k => EventIndex.GetNpcDisplayName(k).Equals(owner, StringComparison.OrdinalIgnoreCase));
                return isPerson ? name : "the " + name;
            }

            return GenericPlaces.Contains(name) || !name.Contains(' ') ? "the " + name : name;
        }

        /****
        ** Helpers
        ****/
        private static string Place(EventInfo evt) => WithArticle(evt.LocationDisplayName);

        private static string StartTime(EventInfo evt, EventEvaluation eval) =>
            eval.StartTime is { } start ? PreconditionFormatter.Time(start)
            : evt.Window is { } w ? PreconditionFormatter.Time(w.Start)
            : "later";

        /// <summary>" (tomorrow works!)" or " (not tomorrow either)", from the forecast.</summary>
        private static string TomorrowHint(EventEvaluation eval, bool includeNo) => eval.WorksTomorrow switch
        {
            true => " (tomorrow works!)",
            false when includeNo => " (not tomorrow either)",
            _ => ""
        };

        private static string Capitalize(string s) =>
            s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;

        /// <summary>Picks a template by a stable hash of the event key, so an event always reads the same way.</summary>
        private static string Pick(EventInfo evt, params string[] options)
        {
            uint hash = 2166136261;
            foreach (char ch in evt.Key)
                hash = (hash ^ ch) * 16777619;
            return options[hash % (uint)options.Length];
        }

        /// <summary>The names, pronouns and context for one heart event's messages.</summary>
        private readonly struct Words
        {
            public string Name { get; }
            public string Place { get; }
            public string Obj { get; }
            public string Poss { get; }
            public bool IsHome { get; }
            public bool Rainy { get; }
            public bool Morning { get; }
            public bool Evening { get; }

            public Words(EventInfo evt)
            {
                this.Name = EventIndex.GetNpcDisplayName(evt.Owner);
                this.Place = WithArticle(evt.LocationDisplayName);

                // pronouns come from the NPC's gender in the game data
                Game1.characterData.TryGetValue(evt.Owner, out CharacterData? data);
                (this.Obj, this.Poss) = data?.Gender switch
                {
                    Gender.Male => ("him", "his"),
                    Gender.Female => ("her", "her"),
                    _ => ("them", "their")
                };

                this.IsHome = data?.Home?.Any(h => h.Location == evt.LocationName) == true
                    || Game1.getCharacterFromName(evt.Owner)?.DefaultMap == evt.LocationName;
                this.Rainy = evt.Conditions.Any(c => c.Is("Weather") && !c.Negated && c.Args.Any(a => a.Equals("rainy", StringComparison.OrdinalIgnoreCase)));
                this.Evening = evt.Window is { } w1 && w1.Start >= 1700;
                this.Morning = evt.Window is { } w2 && w2.End <= 1200;
            }
        }
    }
}
