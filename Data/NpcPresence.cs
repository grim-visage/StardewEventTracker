using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace StardewEventTracker.Data
{
    /// <summary>When an NPC is at a location today, read from their schedule, so "NPC is here" requirements become a time of day.</summary>
    internal static class NpcPresence
    {
        /// <summary>Roughly how many tiles an NPC walks in 10 in-game minutes (2 pixels a tick, 7 seconds per 10 minutes).</summary>
        private const int TilesPerTenMinutes = 13;

        /// <summary>
        /// The times today (HHMM, in order) the NPC should be at the location, from arriving to setting off again; empty
        /// if they don't go there today, or null if their schedule can't be read (e.g. on a farmhand, or no schedule).
        /// </summary>
        public static List<TimeWindow>? Today(string npcName, string location)
        {
            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc?.Schedule is not { Count: > 0 } schedule)
                return null;

            var stops = schedule.OrderBy(p => p.Key).ToList();
            var windows = new List<TimeWindow>();

            // they start the day where they live
            if (npc.DefaultMap == location && stops[0].Key > 600)
                windows.Add(new TimeWindow(600, stops[0].Key));

            string? previous = npc.DefaultMap;
            for (int i = 0; i < stops.Count; i++)
            {
                (int leaveAt, var path) = stops[i];
                string? target = path?.targetLocationName;
                int leave = i + 1 < stops.Count ? stops[i + 1].Key : 2600;
                bool alreadyThere = previous == location && windows.Count > 0;
                previous = target;
                if (path == null || target != location)
                    continue;

                // one stay, even if the schedule moves them around inside the location
                if (alreadyThere)
                {
                    windows[^1] = windows[^1] with { End = Math.Max(windows[^1].End, leave) };
                    continue;
                }

                // the schedule says when they set off, so allow for the walk
                int walk = ((path.route?.Count ?? 0) + TilesPerTenMinutes - 1) / TilesPerTenMinutes * 10;
                int arrive = Utility.ModifyTime(leaveAt, walk);
                if (arrive < leave)
                    windows.Add(new TimeWindow(arrive, leave));
            }
            return windows;
        }
    }
}
