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
                return Pick(evt, "msg.available.story", 2, new { place = Place(evt) });

            var w = new Words(evt);
            return w.IsHome ? Pick(evt, "msg.available.home", 2, w.Tokens())
                : w.Rainy ? Pick(evt, "msg.available.rainy", 1, w.Tokens())
                : w.Evening ? Pick(evt, "msg.available.evening", 1, w.Tokens())
                : w.Morning ? Pick(evt, "msg.available.morning", 1, w.Tokens())
                : Pick(evt, "msg.available.any", 3, w.Tokens());
        }

        /// <summary>"Abigail should be at the Mountain around 9:00 am. Head out soon to catch her!"</summary>
        public static string Reminder(EventInfo evt, EventEvaluation eval, int minutesLeft)
        {
            string time = StartTime(evt, eval);
            if (evt.IsStory)
                return I18n.Get("msg.reminder.story", new { place = Place(evt), time });

            var w = new Words(evt);
            object tokens = w.Tokens(time: time, where: I18n.Get(w.IsHome ? "msg.where.home" : "msg.where.at", new { place = w.Place }));
            if (minutesLeft >= 120)
                return Pick(evt, "msg.reminder.hours", 2, tokens);
            if (minutesLeft >= 60)
                return Pick(evt, "msg.reminder.hour", 2, tokens);
            return I18n.Get("msg.reminder.soon", tokens);
        }

        /// <summary>"Time to head out: Abigail should be at the Mountain around 9:00 am, about 40m away."</summary>
        public static string LeaveNow(EventInfo evt, EventEvaluation eval, int travelMinutes)
        {
            string time = StartTime(evt, eval);
            string away = PreconditionFormatter.FormatDuration(travelMinutes);
            if (evt.IsStory)
                return I18n.Get("msg.leave.story", new { place = Place(evt), time, away });

            var w = new Words(evt);
            return Pick(evt, "msg.leave", 2, w.Tokens(time: time, away: away));
        }

        /// <summary>"Today looks like a good day to see Abigail at the Mountain (9:00 am-12:00 pm)."</summary>
        public static string MorningHeadsUp(EventInfo evt)
        {
            string window = evt.Window is { } tw
                ? I18n.Get("msg.morning.window", new { start = PreconditionFormatter.Time(tw.Start), end = PreconditionFormatter.Time(tw.End) })
                : "";
            if (evt.IsStory)
                return I18n.Get("msg.morning.story", new { place = Place(evt), window });

            var w = new Words(evt);
            return I18n.Get(w.Rainy ? "msg.morning.rainy" : "msg.morning", w.Tokens(window: window));
        }

        /// <summary>"Tomorrow looks rainy. A good day to find Abigail at the Mountain."</summary>
        public static string TomorrowHeadsUp(EventInfo evt, string weatherTomorrow)
        {
            string lead = evt.Conditions.Any(c => c.Is("Weather"))
                ? I18n.Get("msg.tomorrow.weather", new { weather = PreconditionFormatter.WeatherName(weatherTomorrow).ToLower() })
                : I18n.Get("msg.tomorrow.works");
            if (evt.IsStory)
                return I18n.Get("msg.tomorrow.story", new { lead, place = Place(evt) });

            var w = new Words(evt);
            return Pick(evt, "msg.tomorrow", 2, w.Tokens(lead: lead));
        }

        /****
        ** Short forms
        ****/
        /// <summary>The bracketed status shown next to an event in the menu.</summary>
        public static string StatusTag(EventInfo evt, EventEvaluation eval, EventIndex index)
        {
            string tomorrow = TomorrowHint(eval, includeNo: true);
            return eval.Status switch
            {
                EventStatus.AvailableNow => evt.Window is { } w
                    ? I18n.Get("status.available-until", new { end = PreconditionFormatter.Time(w.End) })
                    : I18n.Get("status.available"),
                EventStatus.LaterToday => I18n.Get("status.later-today", new { start = StartTime(evt, eval), duration = PreconditionFormatter.FormatDuration(eval.MinutesUntilStart ?? 0) }),
                EventStatus.WrongDay => I18n.Get("status.wait-for", new { day = WaitFor(evt, eval) }) + tomorrow,
                EventStatus.GreenRain => I18n.Get("status.green-rain") + tomorrow,
                EventStatus.FestivalHere => I18n.Get("status.festival") + tomorrow,
                EventStatus.MissedToday => I18n.Get("status.missed") + tomorrow,
                EventStatus.NotYet => I18n.Get("status.not-yet", new { count = eval.UnmetCount }),
                EventStatus.Special => I18n.Get("status.special"),
                EventStatus.Locked => I18n.Get("status.locked", new { hearts = evt.RequiredHearts }),
                EventStatus.Unreachable => I18n.Get("status.unreachable"),
                _ => I18n.Get("status.seen")
            };
        }

        /// <summary>How urgent the HUD should make an event look.</summary>
        public enum HudTone { Go, Soon, Normal }

        /// <summary>The short second HUD line, e.g. "Head out soon - starts 9:00 am (in 45m)".</summary>
        /// <param name="reminderMinutes">The player's reminder intervals, which set the "Get ready" and "Head out soon" stages.</param>
        /// <param name="travelMinutes">Walking time to the event's location, if known.</param>
        /// <param name="travelBuffer">Slack added to the walk before it's time to leave.</param>
        public static (string Text, HudTone Tone) HudLine(EventInfo evt, EventEvaluation eval, IEnumerable<int> reminderMinutes, int? travelMinutes = null, int travelBuffer = 0)
        {
            string away = travelMinutes > 0 ? I18n.Get("hud.away", new { duration = PreconditionFormatter.FormatDuration(travelMinutes.Value) }) : "";
            string tomorrow = TomorrowHint(eval, includeNo: false);
            switch (eval.Status)
            {
                case EventStatus.AvailableNow:
                    string go = I18n.Get(evt.IsStory ? "hud.go.story" : "hud.go");
                    string until = evt.Window is { } w ? I18n.Get("hud.until", new { go, end = PreconditionFormatter.Time(w.End) }) : go;
                    return (until + away, HudTone.Go);

                case EventStatus.LaterToday:
                    int minutes = eval.MinutesUntilStart ?? 0;
                    string start = StartTime(evt, eval);
                    if (travelMinutes > 0 && minutes <= travelMinutes + travelBuffer)
                        return (I18n.Get("hud.leave-now", new { duration = PreconditionFormatter.FormatDuration(travelMinutes.Value), start }), HudTone.Soon);

                    string when = I18n.Get("hud.starts", new { start, duration = PreconditionFormatter.FormatDuration(minutes) }) + away;
                    var stages = reminderMinutes.Where(m => m > 0).Distinct().OrderBy(m => m).ToList();
                    if (stages.Count == 0)
                        stages = new List<int> { 60, 120 };
                    if (minutes <= stages[0])
                        return (I18n.Get("hud.head-out", new { when }), HudTone.Soon);
                    if (minutes <= stages[^1])
                        return (I18n.Get("hud.get-ready", new { when }), HudTone.Soon);
                    return (I18n.Get("hud.later-today", new { when }), HudTone.Normal);

                case EventStatus.WrongDay:
                    return (I18n.Get("status.wait-for", new { day = WaitFor(evt, eval) }) + tomorrow, HudTone.Normal);
                case EventStatus.GreenRain:
                    return (I18n.Get("hud.green-rain") + tomorrow, HudTone.Normal);
                case EventStatus.FestivalHere:
                    return (I18n.Get("status.festival") + tomorrow, HudTone.Normal);
                case EventStatus.MissedToday:
                    return (I18n.Get("hud.missed") + tomorrow, HudTone.Normal);
                case EventStatus.NotYet:
                    return (I18n.Get("hud.not-yet", new { count = eval.UnmetCount }), HudTone.Normal);
                case EventStatus.Special:
                    return (I18n.Get("hud.special"), HudTone.Normal);
                default:
                    return (StatusTag(evt, eval, null!), HudTone.Normal);
            }
        }

        /// <summary>What day an event is waiting for, e.g. "a sunny day" or "a day other than Tue".</summary>
        public static string WaitFor(EventInfo evt, EventEvaluation eval)
        {
            var parts = new List<string>();
            string or = I18n.Get("join.or");
            for (int i = 0; i < evt.Conditions.Count; i++)
            {
                Precondition c = evt.Conditions[i];
                if (c.Category != ConditionCategory.Calendar || eval.States[i] != ConditionState.Unmet)
                    continue;

                string neg = c.Negated ? ".not" : "";
                parts.Add(c.Name.ToLowerInvariant() switch
                {
                    "weather" => I18n.Get("wait.weather", new { weather = string.Join(or, c.Args.Select(a => PreconditionFormatter.WeatherName(a).ToLower())) }),
                    "dayofweek" => I18n.Get("wait.day-of-week" + neg, new { days = string.Join("/", c.Args.Select(PreconditionFormatter.DayName)) }),
                    "dayofmonth" => I18n.Get("wait.day-of-month" + neg, new { days = string.Join(or, c.Args) }),
                    "season" => I18n.Get("wait.season" + neg, new { seasons = string.Join("/", c.Args.Select(PreconditionFormatter.SeasonName)) }),
                    "festivalday" => I18n.Get("wait.festival-day" + neg),
                    "upcomingfestival" => I18n.Get("wait.upcoming-festival" + neg),
                    _ => c.Raw
                });
            }
            return parts.Count > 0 ? string.Join(I18n.Get("join.and"), parts) : I18n.Get("wait.another-day");
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
                return isPerson ? name : I18n.Get("place.with-article", new { place = name });
            }

            return GenericPlaces.Contains(name) || !name.Contains(' ') ? I18n.Get("place.with-article", new { place = name }) : name;
        }

        /****
        ** Helpers
        ****/
        private static string Place(EventInfo evt) => WithArticle(evt.LocationDisplayName);

        private static string StartTime(EventInfo evt, EventEvaluation eval) =>
            eval.StartTime is { } start ? PreconditionFormatter.Time(start)
            : evt.Window is { } w ? PreconditionFormatter.Time(w.Start)
            : I18n.Get("time.later");

        /// <summary>" (tomorrow works!)" or " (not tomorrow either)", from the forecast.</summary>
        private static string TomorrowHint(EventEvaluation eval, bool includeNo) => eval.WorksTomorrow switch
        {
            true => I18n.Get("tomorrow.yes"),
            false when includeNo => I18n.Get("tomorrow.no"),
            _ => ""
        };

        /// <summary>Picks one of a message's numbered variants by a stable hash of the event key, so an event always reads the same way.</summary>
        private static string Pick(EventInfo evt, string key, int count, object tokens)
        {
            uint hash = 2166136261;
            foreach (char ch in evt.Key)
                hash = (hash ^ ch) * 16777619;
            return I18n.Variant(key, hash, count, tokens);
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
                string gender = data?.Gender switch
                {
                    Gender.Male => "male",
                    Gender.Female => "female",
                    _ => "neutral"
                };
                this.Obj = I18n.Get($"pronoun.{gender}.object");
                this.Poss = I18n.Get($"pronoun.{gender}.possessive");

                this.IsHome = data?.Home?.Any(h => h.Location == evt.LocationName) == true
                    || Game1.getCharacterFromName(evt.Owner)?.DefaultMap == evt.LocationName;
                this.Rainy = evt.Conditions.Any(c => c.Is("Weather") && !c.Negated && c.Args.Any(a => a.Equals("rainy", StringComparison.OrdinalIgnoreCase)));
                this.Evening = evt.Window is { } w1 && w1.Start >= 1700;
                this.Morning = evt.Window is { } w2 && w2.End <= 1200;
            }

            /// <summary>Message tokens: {{name}}, {{place}}, {{obj}}, {{poss}} plus any extras.</summary>
            public object Tokens(string time = "", string where = "", string away = "", string window = "", string lead = "") =>
                new { name = this.Name, place = this.Place, obj = this.Obj, poss = this.Poss, time, where, away, window, lead };
        }
    }
}
