using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace StardewEventTracker.Data
{
    /// <summary>
    /// Finds areas a mod has added that there's no way into yet, e.g. Stardew Valley Expanded's Highlands, which have
    /// no warp leading in until Marlon's boat is ready. The game's own locations always count as reachable (the game
    /// reaches some by its own code, like Willy's boat or the bus); a mod's location is reachable if a warp, door,
    /// minecart stop or map action naming it leads in from a reachable one.
    /// </summary>
    internal static class AreaAccess
    {
        /// <summary>Mod locations with no way in yet.</summary>
        private static HashSet<string> unreachable = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Where each location's warps, doors and map actions lead.</summary>
        private static Dictionary<string, HashSet<string>> routes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Whether each map path is one of the game's own maps, by path.</summary>
        private static readonly Dictionary<string, bool> VanillaMaps = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The mod locations with no way in yet.</summary>
        public static IReadOnlyCollection<string> Unreachable => unreachable;

        /// <summary>Works out what can be reached, from the routes the map scan found.</summary>
        /// <param name="names">Every location's name.</param>
        /// <param name="found">Where each location's warps, doors and map actions lead.</param>
        public static void Rebuild(IEnumerable<string> names, Dictionary<string, HashSet<string>> found, IMonitor monitor)
        {
            routes = found;
            var all = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>();
            void Reach(string name)
            {
                if (reachable.Add(name))
                    queue.Enqueue(name);
            }

            foreach (string name in all.Where(IsVanilla))
                Reach(name);

            // minecart stops are reached from the game's own carts
            try
            {
                foreach (var network in DataLoader.Minecarts(Game1.content).Values)
                {
                    foreach (var stop in network.Destinations ?? new())
                    {
                        if (!string.IsNullOrWhiteSpace(stop.TargetLocation))
                            Reach(stop.TargetLocation);
                    }
                }
            }
            catch (Exception ex)
            {
                monitor.Log($"Couldn't read minecart stops: {ex.Message}", LogLevel.Trace);
            }

            while (queue.Count > 0)
            {
                if (routes.TryGetValue(queue.Dequeue(), out HashSet<string>? next))
                {
                    foreach (string target in next)
                        Reach(target);
                }
            }

            unreachable = new HashSet<string>(all.Where(name => !reachable.Contains(name)), StringComparer.OrdinalIgnoreCase);
            monitor.Log($"Found {unreachable.Count} mod locations with no way in yet: {string.Join(", ", unreachable.OrderBy(n => n))}.", LogLevel.Trace);
        }

        /// <summary>Whether there's a way into a location now (true for anywhere this doesn't know about).</summary>
        public static bool IsReachable(string locationName) => !unreachable.Contains(locationName);

        /// <summary>
        /// How to open up the way to a location, from <c>assets/hints.json</c>: its own hint, or that of the area it's
        /// part of (the Highlands for a cave inside them), found by following its exits. Null if no hint says.
        /// </summary>
        public static Hint? GetHint(string locationName)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { locationName };
            var queue = new Queue<string>(seen);
            while (queue.Count > 0)
            {
                string name = queue.Dequeue();
                if (Hints.ForLocation(name) is { } hint)
                    return hint;
                if (!routes.TryGetValue(name, out HashSet<string>? next))
                    continue;
                foreach (string target in next.Where(t => unreachable.Contains(t) && seen.Add(t)))
                    queue.Enqueue(target);
            }
            return null;
        }

        /// <summary>Whether a location uses one of the game's own maps, so it isn't one a mod added.</summary>
        private static bool IsVanilla(string name)
        {
            string? mapPath;
            try
            {
                mapPath = Game1.getLocationFromName(name)?.mapPath.Value
                    ?? (Game1.locationData.TryGetValue(name, out var data) ? data.CreateOnLoad?.MapPath : null);
            }
            catch (Exception)
            {
                mapPath = null;
            }

            // without a map to check, don't claim it can't be reached
            if (string.IsNullOrWhiteSpace(mapPath))
                return true;

            if (!VanillaMaps.TryGetValue(mapPath, out bool vanilla))
            {
                string file = Path.Combine(Constants.ContentPath, mapPath.Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar) + ".xnb");
                VanillaMaps[mapPath] = vanilla = File.Exists(file);
            }
            return vanilla;
        }
    }
}
