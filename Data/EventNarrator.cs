using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;
using StardewValley.GameData.Characters;

namespace StardewEventTracker.Data
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
            if (minutesLeft >= 180)
                return I18n.Get("msg.reminder.later", tokens);
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
        public static string StatusTag(EventInfo evt, EventEvaluation eval, EventIndex? index)
        {
            string tomorrow = TomorrowHint(eval, includeNo: true);
            return eval.Status switch
            {
                EventStatus.AvailableNow => evt.Window is { } w
                    ? I18n.Get("status.available-until", new { end = PreconditionFormatter.Time(w.End) })
                    : I18n.Get("status.available"),
                EventStatus.LaterToday => I18n.Get("status.later-today", new { start = StartTime(evt, eval), duration = PreconditionFormatter.FormatDuration(eval.MinutesUntilStart ?? 0) }),
                EventStatus.OnEntry => I18n.Get("status.on-entry"),
                EventStatus.WrongDay => I18n.Get("status.wait-for", new { day = WaitFor(evt, eval) }) + tomorrow,
                EventStatus.GreenRain => I18n.Get("status.green-rain") + tomorrow,
                EventStatus.FestivalHere => FestivalTag(evt, eval) + tomorrow,
                EventStatus.MissedToday => I18n.Get("status.missed") + tomorrow,
                EventStatus.NotYet => I18n.Get("status.not-yet", new { step = NextStep(evt, eval, index) }) + MoreSteps(eval),
                EventStatus.Special => I18n.Get("status.special"),
                EventStatus.Locked => I18n.Get("status.locked", new { step = NextStep(evt, eval, index) }),
                EventStatus.Unreachable => I18n.Get("status.unreachable"),
                _ => I18n.Get("status.seen")
            };
        }

        /// <summary>How urgent the HUD should make an event look, most urgent first.</summary>
        public enum HudTone
        {
            /// <summary>It can happen right now.</summary>
            Go,

            /// <summary>Time to set off: "Leave now" or "Head out soon".</summary>
            Urgent,

            /// <summary>Coming up later today: "Get ready".</summary>
            Soon,

            Normal
        }

        /// <summary>The short second HUD line, e.g. "Head out soon - starts 9:00 am (in 45m)".</summary>
        /// <param name="reminderMinutes">The player's reminder intervals, which set the "Get ready" and "Head out soon" stages.</param>
        /// <param name="travelMinutes">Walking time to the event's location, if known.</param>
        /// <param name="travelBuffer">Slack added to the walk before it's time to leave.</param>
        /// <param name="index">Used to name events the player needs to see first.</param>
        /// <param name="compact">Whether to say it in as few words as possible, for the HUD's compact layout: the colour (from the tone) says how soon.</param>
        public static (string Text, HudTone Tone) HudLine(EventInfo evt, EventEvaluation eval, IEnumerable<int> reminderMinutes, int? travelMinutes = null, int travelBuffer = 0, EventIndex? index = null, bool compact = false)
        {
            string away = travelMinutes > 0 && !compact ? I18n.Get("hud.away", new { duration = PreconditionFormatter.FormatDuration(travelMinutes.Value) }) : "";
            string tomorrow = compact ? "" : TomorrowHint(eval, includeNo: false);
            switch (eval.Status)
            {
                case EventStatus.AvailableNow when compact:
                    return (evt.Window is { } open ? I18n.Get("hud.short.now-until", new { end = PreconditionFormatter.Time(open.End) }) : I18n.Get("hud.short.now"), HudTone.Go);

                case EventStatus.AvailableNow:
                    string go = I18n.Get(evt.IsStory ? "hud.go.story" : "hud.go");
                    string until = evt.Window is { } w ? I18n.Get("hud.until", new { go, end = PreconditionFormatter.Time(w.End) }) : go;
                    return (until + away, HudTone.Go);

                case EventStatus.LaterToday:
                    int minutes = eval.MinutesUntilStart ?? 0;
                    string start = StartTime(evt, eval);
                    if (travelMinutes > 0 && minutes <= travelMinutes + travelBuffer)
                        return (I18n.Get(compact ? "hud.short.leave-now" : "hud.leave-now", new { duration = PreconditionFormatter.FormatDuration(travelMinutes.Value), start }), HudTone.Urgent);

                    string when = I18n.Get("hud.starts", new { start, duration = PreconditionFormatter.FormatDuration(minutes) }) + away;
                    var stages = reminderMinutes.Where(m => m > 0).Distinct().OrderBy(m => m).ToList();
                    if (stages.Count == 0)
                        stages = new List<int> { 60, 120 };
                    HudTone tone = minutes <= stages[0] ? HudTone.Urgent : minutes <= stages[^1] ? HudTone.Soon : HudTone.Normal;
                    if (compact)
                        return (when, tone);
                    return (I18n.Get(tone switch { HudTone.Urgent => "hud.head-out", HudTone.Soon => "hud.get-ready", _ => "hud.later-today" }, new { when }), tone);

                case EventStatus.OnEntry:
                    return (I18n.Get("hud.on-entry"), HudTone.Normal);
                case EventStatus.WrongDay:
                    return (I18n.Get("status.wait-for", new { day = WaitFor(evt, eval) }) + tomorrow, HudTone.Normal);
                case EventStatus.GreenRain:
                    return (I18n.Get("hud.green-rain") + tomorrow, HudTone.Normal);
                case EventStatus.FestivalHere:
                    return (FestivalTag(evt, eval) + tomorrow, HudTone.Normal);
                case EventStatus.MissedToday:
                    return (I18n.Get("hud.missed") + tomorrow, HudTone.Normal);
                case EventStatus.NotYet:
                    return (I18n.Get("hud.not-yet", new { step = NextStep(evt, eval, index) }) + (compact ? "" : MoreSteps(eval)), HudTone.Normal);
                case EventStatus.Special:
                    return (I18n.Get("hud.special"), HudTone.Normal);
                default:
                    return (StatusTag(evt, eval, index), HudTone.Normal);
            }
        }

        /// <summary>
        /// What day an event is waiting for, e.g. "a sunny day", "Winter" or "a Sat or Sun". Season and weekday rules are
        /// combined, so three "not in X" seasons read as the one season that's left.
        /// </summary>
        public static string WaitFor(EventInfo evt, EventEvaluation eval)
        {
            var parts = new List<string>();
            string or = I18n.Get("join.or");
            var seasons = new HashSet<Season>(Enum.GetValues<Season>());
            var days = new HashSet<DayOfWeek>(Enum.GetValues<DayOfWeek>());
            bool seasonUnmet = false, dayUnmet = false;

            for (int i = 0; i < evt.Conditions.Count; i++)
            {
                Precondition c = evt.Conditions[i];
                if (c.Category != ConditionCategory.Calendar)
                    continue;
                bool unmet = eval.States[i] == ConditionState.Unmet;
                string neg = c.Negated ? ".not" : "";

                switch (c.Name.ToLowerInvariant())
                {
                    // like the game, only the first season listed counts
                    case "season":
                        if (c.Args.Length > 0 && Enum.TryParse(c.Args[0], ignoreCase: true, out Season season))
                        {
                            if (c.Negated)
                                seasons.Remove(season);
                            else
                                seasons.IntersectWith(new[] { season });
                        }
                        seasonUnmet |= unmet;
                        break;

                    case "dayofweek":
                        var listed = c.Args.Select(a => WorldDate.TryGetDayOfWeekFor(a, out DayOfWeek day) ? day : (DayOfWeek?)null).OfType<DayOfWeek>().ToList();
                        if (c.Negated)
                            days.ExceptWith(listed);
                        else
                            days.IntersectWith(listed);
                        dayUnmet |= unmet;
                        break;

                    // like the game, only the first weather listed counts
                    case "weather" when unmet && c.Args.Length > 0:
                        parts.Add(I18n.Get("wait.weather", new { weather = PreconditionFormatter.WeatherName(c.Args[0]).ToLower() }));
                        break;
                    case "dayofmonth" when unmet:
                        parts.Add(I18n.Get("wait.day-of-month" + neg, new { days = PreconditionFormatter.JoinList(c.Args, or) }));
                        break;
                    case "festivalday" when unmet:
                        parts.Add(I18n.Get("wait.festival-day" + neg));
                        break;
                    case "upcomingfestival" when unmet:
                        parts.Add(I18n.Get("wait.upcoming-festival" + neg));
                        break;
                    case "npcvisiblehere" when unmet:
                        parts.Add(I18n.Get("wait.npc-here", new { name = c.Args.Length > 0 ? EventIndex.GetNpcDisplayName(c.Args[0]) : I18n.Get("someone"), place = Place(evt) }));
                        break;
                    default:
                        if (unmet)
                            parts.Add(c.Raw);
                        break;
                }
            }

            if (dayUnmet && days.Count > 0)
            {
                // Monday first
                var ordered = days.OrderBy(d => ((int)d + 6) % 7).Select(d => PreconditionFormatter.LongDayName(d.ToString())).ToList();
                var others = Enum.GetValues<DayOfWeek>().Except(days).OrderBy(d => ((int)d + 6) % 7).Select(d => PreconditionFormatter.LongDayName(d.ToString())).ToList();
                parts.Insert(0, days.Count <= 3
                    ? I18n.Get("wait.day-of-week", new { days = PreconditionFormatter.JoinList(ordered, or) })
                    : I18n.Get("wait.day-of-week.not", new { days = PreconditionFormatter.JoinList(others, or) }));
            }
            if (seasonUnmet && seasons.Count > 0)
            {
                // every season but one ("not in winter"): that's the current one, so the next season works
                var excluded = Enum.GetValues<Season>().Except(seasons).ToList();
                var waitFor = excluded.Count == 1
                    ? new List<Season> { (Season)(((int)excluded[0] + 1) % 4) }
                    : seasons.OrderBy(x => x).ToList();
                parts.Insert(0, I18n.Get("wait.season", new { seasons = PreconditionFormatter.JoinList(waitFor.Select(x => PreconditionFormatter.SeasonName(x.ToString())), or) }));
            }

            if (eval.Door is { ClosedToday: true })
                parts.Add(I18n.Get("wait.not-wednesday"));

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
                // "Eli & Dylan's House" names two people
                string[] owners = name[..possessive].Split(new[] { " & ", " and " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                bool isPerson = owners.Any(owner => Game1.characterData.ContainsKey(owner) || Game1.characterData.Keys.Any(k => EventIndex.GetNpcDisplayName(k).Equals(owner, StringComparison.OrdinalIgnoreCase)));
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

        /// <summary>
        /// The first thing still needed before an event can happen, e.g. "needs 2 hearts with Leah to get in (you: 1)"
        /// or "see Leah's 4-heart event first". The menu lists every requirement; this is the one-line version.
        /// </summary>
        public static string NextStep(EventInfo evt, EventEvaluation eval, EventIndex? index)
        {
            // getting there comes first, then the door: nothing inside matters until you can get in
            if (eval.CantReach)
                return PreconditionFormatter.DescribeReach(evt, forStep: true);

            if (eval.Door is { HeartsOk: false } door && door.Door.Npc is { } resident)
            {
                return I18n.Get("step.door-hearts", new
                {
                    name = EventIndex.GetNpcDisplayName(resident),
                    more = PreconditionFormatter.MoreFriendship(resident, door.Door.MinFriendship) ?? ""
                });
            }

            if (eval.Door is { MailOk: false } letterDoor && letterDoor.Door.RequiredMail?.Split('|')[0] is { } letter)
                return (index != null ? PreconditionFormatter.ExplainFlag(letter, index, forStep: true) : null) ?? I18n.Get("step.door-letter");

            // a locked event is waiting on friendship, so say that first even if something else is listed earlier
            IEnumerable<int> order = Enumerable.Range(0, evt.Conditions.Count);
            if (eval.Status == EventStatus.Locked)
                order = order.OrderBy(i => evt.Conditions[i].Is("Friendship") || evt.Conditions[i].Is("Dating") || evt.Conditions[i].Is("Spouse") || evt.Conditions[i].Is("Roommate") ? 0 : 1);

            foreach (int i in order)
            {
                Precondition c = evt.Conditions[i];
                if (eval.States[i] == ConditionState.Unknown)
                    return I18n.Get("step.unknown");
                if (eval.States[i] != ConditionState.Unmet || c.Category != ConditionCategory.Progress)
                    continue;

                string first = c.Args.FirstOrDefault() ?? "";
                return c.Name.ToLowerInvariant() switch
                {
                    "friendship" => string.Join("; ", EventInfo.FriendshipPairs(c)
                        .Where(p => PreconditionFormatter.MoreFriendship(p.Npc, p.Points) != null)
                        .Select(p => I18n.Get("step.hearts", new
                        {
                            name = EventIndex.GetNpcDisplayName(p.Npc),
                            more = PreconditionFormatter.MoreFriendship(p.Npc, p.Points)
                        }))),
                    // events the index doesn't know are usually started by a mod's own code: just more story to go
                    "sawevent" when !c.Negated => index?.FindById(first) != null
                        ? I18n.Get("step.see-event", new { @event = index.DescribeEventShort(first) })
                        : (index != null ? PreconditionFormatter.DescribeMarker(first, index, forStep: true) ?? PreconditionFormatter.DescribeAddedLater(first, index, forStep: true) ?? PreconditionFormatter.DescribeTileMarker(first, index, forStep: true) : null)
                            ?? I18n.Get("step.story-progress"),
                    "hostmail" or "hostorlocalmail" or "localmail" or "worldstate" when !c.Negated =>
                        (index != null ? PreconditionFormatter.ExplainFlag(first, index, forStep: true) : null) ?? I18n.Get("step.story-progress"),
                    "activedialogueevent" when index != null && PreconditionFormatter.ExplainTopic(first, c.Negated, index, forStep: true) is { } wait => wait,
                    "year" when !c.Negated && first != "1" => I18n.Get("step.year", new { year = first }),
                    "inupgradedhouse" => I18n.Get("step.house-upgrade", new { level = c.Args.Length > 0 ? first : "1" }),
                    "dating" when !c.Negated => I18n.Get("step.dating", new { name = EventIndex.GetNpcDisplayName(first) }),
                    "spouse" when !c.Negated => I18n.Get("step.spouse", new { name = EventIndex.GetNpcDisplayName(first) }),
                    "roommate" when !c.Negated => I18n.Get("step.roommate", new { name = EventIndex.GetNpcDisplayName(first) }),
                    "daysplayed" => I18n.Get("step.days-played", new { count = first }),
                    "jojabundlesdone" => I18n.Get("step.joja-done"),
                    "communitycenterorwarehousedone" when !c.Negated => I18n.Get("step.cc-or-joja-done"),
                    "reachedminebottom" => I18n.Get("step.mine-bottom"),
                    "goldenwalnuts" => I18n.Get("step.golden-walnuts", new { count = first }),
                    "earnedmoney" => I18n.Get("step.earned-money", new { amount = first }),
                    "hasmoney" => I18n.Get("step.has-money", new { amount = first }),
                    "freeinventoryslots" => I18n.Get("step.free-slots", new { count = first }),
                    "skill" when !c.Negated && c.Args.Length >= 2 => I18n.Get("step.skill", new { skill = first.Length > 0 ? char.ToUpperInvariant(first[0]) + first[1..].ToLowerInvariant() : first, level = c.Args[1] }),
                    "sawsecretnote" when !c.Negated => I18n.Get("step.secret-note", new { note = first }),
                    // anything else: the requirement itself, lower-cased to fit after "Not yet:"
                    _ => LowerFirst(index != null ? PreconditionFormatter.Describe(c, index, evt) : c.Raw)
                };
            }

            return eval.DoorNeverOpen ? I18n.Get("step.door-closed") : I18n.Get("step.unknown");
        }

        /// <summary>A sentence fragment with its first letter lower-cased, unless it starts with a name or "I".</summary>
        private static string LowerFirst(string text)
        {
            if (text.Length < 2 || !char.IsUpper(text[0]) || char.IsUpper(text[1]))
                return text;
            string firstWord = text.Split(' ', 2)[0].TrimEnd('\'', 's', ':', ',');
            bool isName = Game1.characterData.ContainsKey(firstWord) || Game1.characterData.Keys.Any(k => EventIndex.GetNpcDisplayName(k) == firstWord);
            return isName ? text : char.ToLowerInvariant(text[0]) + text[1..];
        }

        /// <summary>An NPC's possessive pronoun from their gender in the game data ("her", "his", "their").</summary>
        public static string Possessive(string npc)
        {
            Game1.characterData.TryGetValue(npc, out CharacterData? data);
            string gender = data?.Gender switch
            {
                Gender.Male => "male",
                Gender.Female => "female",
                _ => "neutral"
            };
            return I18n.Get($"pronoun.{gender}.possessive");
        }

        /// <summary>" (+1 more)" when more than one thing is still needed.</summary>
        private static string MoreSteps(EventEvaluation eval) =>
            eval.UnmetCount > 1 ? I18n.Get("hud.more", new { count = eval.UnmetCount - 1 }) : "";

        /// <summary>A festival at the location itself, or one that locks every shop and house door in the valley.</summary>
        private static string FestivalTag(EventInfo evt, EventEvaluation eval) =>
            eval.TakenOverBy is { } passive ? I18n.Get("status.taken-over", new { festival = passive, place = Place(evt) })
            : I18n.Get(eval.Festival == null && eval.Door is { FestivalClosed: true } ? "status.door-festival" : "status.festival");

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
