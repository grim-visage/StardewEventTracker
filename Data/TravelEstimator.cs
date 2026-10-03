using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;

namespace StardewEventTracker.Data
{
    /// <summary>
    /// Estimates how many in-game minutes it takes to walk from the player to a location, by searching the map's
    /// warps and doors. It's an on-foot estimate: minecarts, the bus and totems aren't counted.
    /// </summary>
    internal sealed class TravelEstimator
    {
        /// <summary>Paths wind around buildings and fences, so straight-line distances are stretched by this much.</summary>
        private const float PathFactor = 1.35f;

        /// <summary>In-game minutes lost to each screen transition.</summary>
        private const float WarpMinutes = 1f;

        /// <summary>Stops the search on huge modded maps; far enough for any real route.</summary>
        private const int MaxSteps = 20000;

        /// <summary>The search from the player's position, resumed for each location asked about until <see cref="Invalidate"/>.</summary>
        private Search? search;

        /// <summary>Measured real milliseconds per in-game minute, since mods like It's Stardew Time change the clock speed.</summary>
        private double realMsPerGameMinute = 700;

        /// <summary>The measured clock speed, in real milliseconds per in-game minute.</summary>
        public double RealMsPerGameMinute => this.realMsPerGameMinute;

        /// <summary>Real time that passed while the clock was running, since the last time change.</summary>
        private double runningMs;

        /// <summary>Forgets cached estimates, e.g. after the player moves or time passes.</summary>
        public void Invalidate() => this.search = null;

        /// <summary>Counts real time while the clock is running. Call every update tick.</summary>
        public void OnUpdateTicked()
        {
            if (Game1.shouldTimePass())
                this.runningMs += Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? 0;
        }

        /// <summary>Updates the clock-speed measurement. Call when the in-game time changes.</summary>
        public void OnTimeChanged(int oldTime, int newTime)
        {
            int minutes = TimeWindow.ToMinutes(newTime) - TimeWindow.ToMinutes(oldTime);
            double sample = minutes > 0 ? this.runningMs / minutes : 0;
            this.runningMs = 0;

            // ignore jumps (sleeping, console commands) and stalls
            if (minutes is > 0 and <= 30 && sample is >= 100 and <= 20000)
                this.realMsPerGameMinute = this.realMsPerGameMinute * 0.7 + sample * 0.3;
        }

        /// <summary>In-game minutes to walk from the player to a location, or null if there's no known route.</summary>
        public int? MinutesTo(string targetLocation)
        {
            GameLocation? from = Game1.player?.currentLocation;
            if (from == null)
                return null;
            // events that can start anywhere need no walk
            if (from.NameOrUniqueName == targetLocation || targetLocation == EventIndex.AnywhereKey)
                return 0;

            try
            {
                if (this.search == null || this.search.Origin != from.NameOrUniqueName)
                    this.search = new Search(from.NameOrUniqueName, Game1.player!.TilePoint, this.MinutesPerTile());
                float? minutes = this.search.MinutesTo(targetLocation);
                return minutes != null ? (int)Math.Ceiling(minutes.Value) : null;
            }
            catch (Exception)
            {
                // a broken modded map shouldn't break reminders
                return null;
            }
        }

        /// <summary>
        /// Dijkstra over (location, arrival tile) states, with walking cost between warps. It stops once it reaches the
        /// location asked about and carries on from there for the next one, so every route from one spot is one search.
        /// </summary>
        private sealed class Search
        {
            private readonly PriorityQueue<(string Location, Point Tile), float> queue = new();
            private readonly Dictionary<(string, Point), float> best = new();

            /// <summary>The shortest walk to each location reached so far.</summary>
            private readonly Dictionary<string, float> reached = new();

            /// <summary>Where each door puts the player, by target and the location the door is in.</summary>
            private readonly Dictionary<(string, string), Point> arrivals = new();

            private readonly float minutesPerTile;

            /// <summary>The location the search starts from.</summary>
            public string Origin { get; }

            public Search(string origin, Point startTile, float minutesPerTile)
            {
                this.Origin = origin;
                this.minutesPerTile = minutesPerTile;
                this.queue.Enqueue((origin, startTile), 0);
                this.best[(origin, startTile)] = 0;
            }

            public float? MinutesTo(string target)
            {
                for (int steps = 0; !this.reached.ContainsKey(target) && steps < MaxSteps && this.queue.TryDequeue(out var state, out float cost); steps++)
                {
                    if (this.best.TryGetValue(state, out float known) && known < cost)
                        continue;
                    this.reached.TryAdd(state.Location, cost);
                    this.Expand(state.Location, state.Tile, cost);
                }
                return this.reached.TryGetValue(target, out float minutes) ? minutes : null;
            }

            private void Expand(string locationName, Point tile, float cost)
            {
                GameLocation? location = Game1.getLocationFromName(locationName);
                if (location == null)
                    return;

                try
                {
                    foreach (Warp warp in location.warps)
                        this.Visit(tile, cost, new Point(warp.X, warp.Y), warp.TargetName, new Point(warp.TargetX, warp.TargetY));

                    foreach (KeyValuePair<Point, string> door in location.doors.Pairs)
                    {
                        var key = (door.Value, location.NameOrUniqueName);
                        if (!this.arrivals.TryGetValue(key, out Point arrival))
                            this.arrivals[key] = arrival = ArrivalTile(door.Value, location.NameOrUniqueName);
                        this.Visit(tile, cost, door.Key, door.Value, arrival);
                    }
                }
                catch (Exception)
                {
                    // a broken modded map: route around it
                }
            }

            private void Visit(Point from, float cost, Point exit, string next, Point arrival)
            {
                float total = cost + Distance(from, exit) * this.minutesPerTile + WarpMinutes;
                var key = (next, arrival);
                if (!this.best.TryGetValue(key, out float old) || total < old)
                {
                    this.best[key] = total;
                    this.queue.Enqueue(key, total);
                }
            }
        }

        /// <summary>Where a door into a location puts the player: next to the warp leading back out, or the map's middle.</summary>
        private static Point ArrivalTile(string locationName, string cameFrom)
        {
            GameLocation? location = Game1.getLocationFromName(locationName);
            if (location == null)
                return Point.Zero;

            foreach (Warp warp in location.warps)
            {
                if (warp.TargetName == cameFrom)
                    return new Point(warp.X, warp.Y - 1);
            }

            var layer = location.Map?.Layers[0];
            return layer != null ? new Point(layer.LayerWidth / 2, layer.LayerHeight / 2) : Point.Zero;
        }

        private static float Distance(Point a, Point b) =>
            (Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y)) * PathFactor;

        /// <summary>In-game minutes per tile at the player's current speed (walking, or riding if mounted).</summary>
        private float MinutesPerTile()
        {
            Farmer player = Game1.player;

            // same formula as Farmer.getMovementSpeed: pixels per millisecond
            float speed = player.speed + player.addedSpeed + (player.isRidingHorse() ? 4.6f : 0f);
            float pixelsPerMs = Math.Max(1f, speed) * 0.066f;
            float realMsPerTile = Game1.tileSize / pixelsPerMs;
            return (float)(realMsPerTile / this.realMsPerGameMinute);
        }
    }
}
