using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewValley;

namespace NpcEventTracker.Data
{
    /// <summary>Turns event preconditions into readable text.</summary>
    internal static class PreconditionFormatter
    {
        /// <summary>Conditions worth showing in the compact HUD summary.</summary>
        private static readonly HashSet<string> SummaryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Time", "Weather", "DayOfWeek", "DayOfMonth", "Season"
        };

        public static string Describe(Precondition c, EventIndex index)
        {
            if (c.IsNeverTrue)
                return "Started by the mod's own code or story, not by entering the location";

            string[] a = c.Args;
            string all = string.Join(" ", a);
            string? text = c.Name.ToLowerInvariant() switch
            {
                "friendship" => string.Join("; ", EventInfo.FriendshipPairs(c).Select(p => DescribeFriendship(p.Npc, p.Points))),
                "time" when a.Length >= 2 => $"Between {Time(a[0])} and {Time(a[1])}",
                "weather" => $"Weather: {string.Join(" or ", a.Select(Capitalize))}",
                "dayofweek" => $"{(c.Negated ? "Not on" : "On")} {string.Join("/", a.Select(Capitalize))}",
                "dayofmonth" => $"{(c.Negated ? "Not on" : "On")} day {string.Join(", ", a)}",
                "season" => $"{(c.Negated ? "Not in" : "In")} {string.Join(" or ", a.Select(Capitalize))}",
                "daysplayed" => $"Played at least {all} days",
                "year" => a.FirstOrDefault() == "1" ? "During year 1" : $"Year {all} or later",
                "sawevent" => $"{(c.Negated ? "Haven't seen" : "Seen")} {string.Join(" or ", a.Select(index.DescribeEvent))}",
                "dating" => $"Dating {Npc(a)}",
                "spouse" => $"{(c.Negated ? "Not married to" : "Married to")} {Npc(a)}",
                "roommate" => $"{(c.Negated ? "Not roommates with" : "Roommates with")} {Npc(a)}",
                "hostmail" or "hostorlocalmail" => $"{(c.Negated ? "Hasn't received" : "Has received")} mail/flag '{all}'",
                "tile" => a.Length >= 2 ? $"Step on tile ({a[0]}, {a[1]})" : "Step on a specific tile",
                "ishost" => "Must be the host player",
                "earnedmoney" => $"Earned {all}g in total",
                "hasitem" => $"Has {string.Join(", ", a.Select(ItemName))} in inventory",
                "shipped" => $"Shipped {DescribeShipped(a)}",
                "random" => double.TryParse(a.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double chance)
                    ? $"{chance * 100:0.#}% chance on entry"
                    : "Random chance on entry",
                "sendmail" => "Special: sends a letter instead of playing a scene",
                "gamestatequery" => $"Condition: {all}",
                "gender" => $"Farmer is {all}",
                "festivalday" => c.Negated ? "Not on a festival day" : "On a festival day",
                "upcomingfestival" => c.Negated ? $"No festival in the next {all} days" : $"Festival within {all} days",
                "goldenwalnuts" => $"Found {all} golden walnuts",
                "reachedminebottom" => "Reached the bottom of the mines",
                "communitycenterorwarehousedone" => "Community Center or Joja Warehouse completed",
                "jojabundlesdone" => "Joja Warehouse route completed",
                "inupgradedhouse" => $"Farmhouse upgraded to level {(a.Length > 0 ? all : "1")}",
                "npcvisiblehere" => $"{Npc(a)} is in this location",
                "npcvisible" => $"{Npc(a)} is around today",
                "sawsecretnote" => $"Read secret note #{all}",
                "chosedialogueanswers" => $"Picked dialogue answer {all}",
                "missingpet" => "Pet condition",
                _ => null
            };

            if (text == null)
                return c.Raw;

            // conditions whose text above doesn't already reflect negation
            bool handlesNegation = c.Name.ToLowerInvariant() is "dayofweek" or "dayofmonth" or "season" or "sawevent"
                or "spouse" or "roommate" or "hostmail" or "hostorlocalmail" or "festivalday" or "upcomingfestival";
            return c.Negated && !handlesNegation ? $"Not: {text}" : text;
        }

        /// <summary>A one-line summary like "6:00am-12:00pm | Sunny | Not on Tue".</summary>
        public static string Summarize(EventInfo evt, EventIndex index)
        {
            var parts = evt.Conditions
                .Where(c => SummaryNames.Contains(c.Name))
                .Select(c => c.Is("Time") && c.Args.Length >= 2 ? $"{Time(c.Args[0])}-{Time(c.Args[1])}" : Describe(c, index).Replace("Weather: ", ""))
                .ToList();
            return parts.Count > 0 ? string.Join(" | ", parts) : "any time";
        }

        /// <summary>The time window as a schedule, e.g. "9:00 am-12:00 pm, opens in 1h 20m".</summary>
        public static string DescribeTimeWindow(EventInfo evt, EventEvaluation eval)
        {
            if (evt.Window is not { } window)
                return "Only at certain times";

            string range = $"{Time(window.Start)}-{Time(window.End)}";
            if (eval.TimeOpen)
                return $"{range}, open until {Time(window.End)}";
            if (eval.MinutesUntilStart > 0)
                return $"{range}, opens in {FormatDuration(eval.MinutesUntilStart.Value)}";
            return $"{range}, closed for today";
        }

        /// <summary>Formats in-game minutes as "1h 20m", "2h" or "40m".</summary>
        public static string FormatDuration(int minutes)
        {
            minutes = Math.Max(0, minutes);
            int hours = minutes / 60, rest = minutes % 60;
            if (hours == 0)
                return $"{rest}m";
            return rest == 0 ? $"{hours}h" : $"{hours}h {rest}m";
        }

        public static string DescribeFriendship(string npc, int points)
        {
            string name = EventIndex.GetNpcDisplayName(npc);
            int current = Game1.player.getFriendshipLevelForNPC(npc);
            int perHeart = NPC.friendshipPointsPerHeartLevel;
            return points % perHeart == 0
                ? $"{points / perHeart} hearts with {name} (you: {current / perHeart})"
                : $"{points} friendship points with {name} (you: {current})";
        }

        public static string Time(int time) => Game1.getTimeOfDayString(time);

        private static string Time(string raw) =>
            int.TryParse(raw, out int time) ? Time(time) : raw;

        private static string Npc(string[] args) =>
            args.Length > 0 ? EventIndex.GetNpcDisplayName(args[0]) : "someone";

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
                parts.Add($"{args[i + 1]}x {ItemName(args[i])}");
            return parts.Count > 0 ? string.Join(", ", parts) : string.Join(" ", args);
        }
    }
}
