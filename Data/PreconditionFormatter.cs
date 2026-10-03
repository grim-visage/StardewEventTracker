using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewValley;

namespace StardewEventTracker.Data
{
    /// <summary>Turns event preconditions into readable text.</summary>
    internal static class PreconditionFormatter
    {
        public static string Describe(Precondition c, EventIndex index)
        {
            if (c.IsNeverTrue)
                return I18n.Get("cond.never-true");

            string[] a = c.Args;
            string all = string.Join(" ", a);
            string or = I18n.Get("join.or");
            string neg = c.Negated ? ".not" : "";
            string? text = c.Name.ToLowerInvariant() switch
            {
                "friendship" => string.Join("; ", EventInfo.FriendshipPairs(c).Select(p => DescribeFriendship(p.Npc, p.Points))),
                "time" when a.Length >= 2 => I18n.Get("cond.time", new { start = Time(a[0]), end = Time(a[1]) }),
                "weather" => I18n.Get("cond.weather", new { weather = string.Join(or, a.Select(WeatherName)) }),
                "dayofweek" => I18n.Get("cond.day-of-week" + neg, new { days = string.Join("/", a.Select(DayName)) }),
                "dayofmonth" => I18n.Get("cond.day-of-month" + neg, new { days = string.Join(", ", a) }),
                "season" => I18n.Get("cond.season" + neg, new { seasons = string.Join(or, a.Select(SeasonName)) }),
                "daysplayed" => I18n.Get("cond.days-played", new { count = all }),
                "year" => a.FirstOrDefault() == "1" ? I18n.Get("cond.year-one") : I18n.Get("cond.year", new { year = all }),
                "sawevent" => I18n.Get("cond.saw-event" + neg, new { events = string.Join(or, a.Select(index.DescribeEvent)) }),
                "dating" => I18n.Get("cond.dating", new { name = Npc(a) }),
                "spouse" => I18n.Get("cond.spouse" + neg, new { name = Npc(a) }),
                "roommate" => I18n.Get("cond.roommate" + neg, new { name = Npc(a) }),
                "hostmail" or "hostorlocalmail" => I18n.Get("cond.mail" + neg, new { flag = all }),
                "tile" => a.Length >= 2 ? I18n.Get("cond.tile", new { x = a[0], y = a[1] }) : I18n.Get("cond.tile-any"),
                "ishost" => I18n.Get("cond.is-host"),
                "earnedmoney" => I18n.Get("cond.earned-money", new { amount = all }),
                "hasitem" => I18n.Get("cond.has-item", new { items = string.Join(", ", a.Select(ItemName)) }),
                "shipped" => I18n.Get("cond.shipped", new { items = DescribeShipped(a) }),
                "random" => double.TryParse(a.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double chance)
                    ? I18n.Get("cond.random", new { percent = (chance * 100).ToString("0.#", CultureInfo.InvariantCulture) })
                    : I18n.Get("cond.random-any"),
                "sendmail" => I18n.Get("cond.send-mail"),
                "gamestatequery" => I18n.Get("cond.game-state-query", new { query = all }),
                "gender" => I18n.Get("cond.gender", new { gender = all }),
                "festivalday" => I18n.Get("cond.festival-day" + neg),
                "upcomingfestival" => I18n.Get("cond.upcoming-festival" + neg, new { days = all }),
                "goldenwalnuts" => I18n.Get("cond.golden-walnuts", new { count = all }),
                "reachedminebottom" => I18n.Get("cond.mine-bottom"),
                "communitycenterorwarehousedone" => I18n.Get("cond.cc-or-joja-done"),
                "jojabundlesdone" => I18n.Get("cond.joja-done"),
                "inupgradedhouse" => I18n.Get("cond.house-upgrade", new { level = a.Length > 0 ? all : "1" }),
                "npcvisiblehere" => I18n.Get("cond.npc-here", new { name = Npc(a) }),
                "npcvisible" => I18n.Get("cond.npc-around", new { name = Npc(a) }),
                "sawsecretnote" => I18n.Get("cond.secret-note", new { note = all }),
                "chosedialogueanswers" => I18n.Get("cond.dialogue-answer", new { answer = all }),
                "missingpet" => I18n.Get("cond.pet"),
                _ => null
            };

            if (text == null)
                return c.Raw;

            // conditions whose text above doesn't already reflect negation
            bool handlesNegation = c.Name.ToLowerInvariant() is "dayofweek" or "dayofmonth" or "season" or "sawevent"
                or "spouse" or "roommate" or "hostmail" or "hostorlocalmail" or "festivalday" or "upcomingfestival";
            return c.Negated && !handlesNegation ? I18n.Get("cond.negated", new { text }) : text;
        }

        /// <summary>The time window as a schedule, e.g. "9:00 am-12:00 pm, starts in 1h 20m".</summary>
        public static string DescribeTimeWindow(EventInfo evt, EventEvaluation eval)
        {
            if (evt.Window is not { } window)
                return I18n.Get("time.some-times");

            var tokens = new
            {
                range = I18n.Get("time.range", new { start = Time(window.Start), end = Time(window.End) }),
                end = Time(window.End),
                duration = FormatDuration(eval.MinutesUntilStart ?? 0)
            };
            // a locked door or festival can push the start later than the event's own window
            if (eval.MinutesUntilStart > 0)
                return I18n.Get("time.starts-in", tokens);
            if (eval.TimeOpen)
                return I18n.Get("time.open", tokens);
            return I18n.Get("time.over", tokens);
        }

        /// <summary>Formats in-game minutes as "1h 20m", "2h" or "40m".</summary>
        public static string FormatDuration(int minutes)
        {
            minutes = Math.Max(0, minutes);
            int hours = minutes / 60, rest = minutes % 60;
            if (hours == 0)
                return I18n.Get("duration.minutes", new { minutes = rest });
            return rest == 0
                ? I18n.Get("duration.hours", new { hours })
                : I18n.Get("duration.hours-minutes", new { hours, minutes = rest });
        }

        public static string DescribeFriendship(string npc, int points)
        {
            string name = EventIndex.GetNpcDisplayName(npc);
            int current = Game1.player.getFriendshipLevelForNPC(npc);
            int perHeart = NPC.friendshipPointsPerHeartLevel;
            return points % perHeart == 0
                ? I18n.Get("cond.hearts", new { hearts = points / perHeart, name, current = current / perHeart })
                : I18n.Get("cond.points", new { points, name, current });
        }

        /// <summary>A weather value from an event condition, translated where known ("sunny" -> "Sunny").</summary>
        public static string WeatherName(string raw) => I18n.GetOr($"weather.{raw.ToLowerInvariant()}", Capitalize(raw));

        public static string SeasonName(string raw) => I18n.GetOr($"season.{raw.ToLowerInvariant()}", Capitalize(raw));

        public static string DayName(string raw) => I18n.GetOr($"day.{raw.ToLowerInvariant()}", Capitalize(raw));

        public static string Time(int time) => Game1.getTimeOfDayString(time);

        private static string Time(string raw) =>
            int.TryParse(raw, out int time) ? Time(time) : raw;

        private static string Npc(string[] args) =>
            args.Length > 0 ? EventIndex.GetNpcDisplayName(args[0]) : I18n.Get("someone");

        private static string Capitalize(string s) =>
            s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s;

        private static string ItemName(string id)
        {
            try
            {
                return ItemRegistry.GetDataOrErrorItem(id).DisplayName;
            }
            catch (Exception)
            {
                return id;
            }
        }

        private static string DescribeShipped(string[] args)
        {
            var parts = new List<string>();
            for (int i = 0; i + 1 < args.Length; i += 2)
                parts.Add(I18n.Get("cond.shipped-item", new { count = args[i + 1], item = ItemName(args[i]) }));
            return parts.Count > 0 ? string.Join(", ", parts) : string.Join(" ", args);
        }
    }
}
