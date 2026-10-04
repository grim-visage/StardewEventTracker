using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using xTile.Layers;
using xTile.ObjectModel;
using xTile.Tiles;

namespace StardewEventTracker.Data
{
    /// <summary>
    /// A locked door leading into a location: a map's <c>LockedDoorWarp</c> action, or one of the game's special doors
    /// that opens with a letter (<see cref="RequiredMail"/>, any of '|'-separated flags) or friendship. An
    /// <see cref="Inner"/> door is inside another location, whose own door decides the hours.
    /// </summary>
    internal readonly record struct DoorLock(string FromLocation, string ToLocation, int Open, int Close, string? Npc, int MinFriendship, string? RequiredMail = null, bool HostMail = false, bool Inner = false);

    /// <summary>Whether the player can get through a locked door right now, mirroring <c>GameLocation.lockedDoorWarp</c>.</summary>
    internal readonly record struct DoorState(DoorLock Door, bool HeartsOk, bool FestivalClosed, int Open, int Close, bool AllDay, bool MailOk = true);

    /// <summary>
    /// Finds locations that can only be entered through locked doors (shops and houses with opening hours, or doors
    /// that need friendship with whoever lives there), so events inside aren't reported as available while locked.
    /// </summary>
    internal static class DoorAccess
    {
        /// <summary>
        /// Locked doors by the location they lead to, including rooms only reached from inside a locked building.
        /// Locations with an unlocked way in from outdoors aren't listed.
        /// </summary>
        private static Dictionary<string, List<DoorLock>> locks = new();

        /// <summary>How many doors deep an inner door's outer door is followed, so doors that lead into each other can't loop.</summary>
        private const int MaxInnerDepth = 8;

        /// <summary>What one location's map says about getting around: its locked doors, and where its ways out lead.</summary>
        private sealed class MapScan
        {
            /// <summary>The map asset it was read from, e.g. "Maps/Town".</summary>
            public string MapPath = "";

            public bool Outdoors;

            /// <summary>The locked and special doors on this map, into whichever location they lead.</summary>
            public readonly List<DoorLock> Doors = new();

            /// <summary>Where its unlocked ways out lead: warps and warp actions.</summary>
            public readonly HashSet<string> OpenTo = new(StringComparer.OrdinalIgnoreCase);

            /// <summary>Where its warps and doors lead, and every word in its map actions (some of them location names).</summary>
            public readonly HashSet<string> Mentions = new(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>Each location's scan, kept until its map changes: scanning reads every tile, so only changed maps are read again.</summary>
        private static readonly Dictionary<string, MapScan> Scans = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Map assets that changed since the last scan, e.g. "Maps/Custom_AdventurerSummit".</summary>
        private static readonly HashSet<string> ChangedMaps = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The day the maps were last scanned, or null to read every map again.</summary>
        private static int? scannedDay;

        /// <summary>Reads every map again next time, e.g. when another save loads.</summary>
        public static void Invalidate()
        {
            scannedDay = null;
            Scans.Clear();
        }

        /// <summary>
        /// Notes changed map assets, for the next <see cref="EnsureScanned"/>. Returns whether any is a location's map:
        /// other assets under Maps/ (tilesheets like springobjects) and mine floors don't matter here.
        /// </summary>
        public static bool MapsChanged(IEnumerable<string> assetNames)
        {
            bool any = false;
            var mapPaths = new HashSet<string>(Scans.Values.Select(scan => scan.MapPath), StringComparer.OrdinalIgnoreCase);
            foreach (string name in assetNames)
            {
                if (mapPaths.Contains(name))
                    any |= ChangedMaps.Add(name);
            }
            return any;
        }

        /// <summary>
        /// Brings the door and route data up to date: every map once a day, and since then only maps that changed (Content
        /// Patcher mods can change maps on every warp) or locations that are new.
        /// </summary>
        public static void EnsureScanned(IMonitor monitor)
        {
            int today = SDate.Now().DaysSinceStart;
            if (scannedDay != today)
            {
                Scans.Clear();
                scannedDay = today;
            }
            else if (ChangedMaps.Count == 0 && Scans.Count > 0)
                return;

            Rebuild(monitor);
        }

        private static void Rebuild(IMonitor monitor)
        {
            var timer = Stopwatch.StartNew();
            var names = new HashSet<string>(Game1.locationData.Keys, StringComparer.OrdinalIgnoreCase);
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int read = 0;

            Utility.ForEachLocation(location =>
            {
                string name = location.NameOrUniqueName;
                names.Add(name);

                // mine and volcano floors come and go, and don't lead anywhere a door or event cares about
                if (location is MineShaft or VolcanoDungeon)
                    return true;

                present.Add(name);
                string mapPath = (location.mapPath.Value ?? "").Replace('\\', '/');
                if (!Scans.TryGetValue(name, out MapScan? scan) || scan.MapPath != mapPath || ChangedMaps.Contains(mapPath))
                {
                    Scans[name] = Scan(location, mapPath, monitor);
                    read++;
                }
                return true;
            });
            foreach (string gone in Scans.Keys.Where(name => !present.Contains(name)).ToList())
                Scans.Remove(gone);
            ChangedMaps.Clear();

            // what can be walked to from outdoors without a locked door in the way: an unlocked way in from there makes a
            // LockedDoorWarp moot, but one from inside (Sebastian's stairs down into Robin's house) doesn't
            var free = new HashSet<string>(Scans.Where(p => p.Value.Outdoors).Select(p => p.Key), StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(free);
            while (queue.Count > 0)
            {
                if (!Scans.TryGetValue(queue.Dequeue(), out MapScan? scan))
                    continue;
                foreach (string target in scan.OpenTo)
                {
                    if (free.Add(target))
                        queue.Enqueue(target);
                }
            }

            // the game's special doors are the only way in for players, wherever else warps lead from
            var found = Scans.Values
                .SelectMany(scan => scan.Doors)
                .GroupBy(door => door.ToLocation, StringComparer.OrdinalIgnoreCase)
                .Select(g => (g.Key, Doors: free.Contains(g.Key) ? g.Where(IsSpecial).ToList() : g.ToList()))
                .Where(p => p.Doors.Count > 0)
                .ToDictionary(p => p.Key, p => p.Doors, StringComparer.OrdinalIgnoreCase);

            // rooms only reached from inside a locked building (Sebastian's room, Harvey's) share its door
            var inside = new Queue<string>(found.Keys);
            while (inside.Count > 0)
            {
                string building = inside.Dequeue();
                if (!Scans.TryGetValue(building, out MapScan? scan))
                    continue;
                foreach (string room in scan.OpenTo)
                {
                    if (free.Contains(room) || found.ContainsKey(room))
                        continue;
                    // through the building's easiest door (Robin's front door, not Maru's lab door): whoever it needs
                    // friendship with, if anyone; its hours come from the building's own door state
                    DoorLock door = found[building].OrderBy(d => d.MinFriendship).First();
                    found[room] = new List<DoorLock> { new(building, room, 600, 2600, door.Npc, door.MinFriendship, Inner: true) };
                    inside.Enqueue(room);
                }
            }
            locks = found;

            // where each location's warps, doors and map actions lead, to find areas there's no way into yet
            var routes = Scans.ToDictionary(
                p => p.Key,
                p => new HashSet<string>(p.Value.Mentions.Where(names.Contains), StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
            AreaAccess.Rebuild(names, routes, monitor);
            monitor.Log($"Read {read} of {Scans.Count} maps; {locks.Count} locations are behind locked doors ({timer.ElapsedMilliseconds}ms).", LogLevel.Trace);
        }

        /// <summary>Reads one location's warps, doors and map actions.</summary>
        private static MapScan Scan(GameLocation location, string mapPath, IMonitor monitor)
        {
            var scan = new MapScan { MapPath = mapPath };
            try
            {
                scan.Outdoors = location.IsOutdoors;
                foreach (Warp warp in location.warps)
                {
                    if (!warp.npcOnly.Value)
                    {
                        scan.OpenTo.Add(warp.TargetName);
                        scan.Mentions.Add(warp.TargetName);
                    }
                }
                foreach (KeyValuePair<Microsoft.Xna.Framework.Point, string> door in location.doors.Pairs)
                    scan.Mentions.Add(door.Value);

                ScanLayer(location, location.Map?.GetLayer("Buildings"), "Action", scan);
                ScanLayer(location, location.Map?.GetLayer("Back"), "TouchAction", scan);
            }
            catch (Exception ex)
            {
                monitor.Log($"Couldn't scan doors in {location.NameOrUniqueName}: {ex.Message}", LogLevel.Trace);
            }
            return scan;
        }

        /// <summary>The state of the most permissive locked door into a location, or null if it isn't behind a locked door.</summary>
        public static DoorState? GetState(string locationName) => GetState(locationName, depth: 0);

        private static DoorState? GetState(string locationName, int depth)
        {
            if (depth > MaxInnerDepth || !locks.TryGetValue(locationName, out List<DoorLock>? doors) || doors.Count == 0)
                return null;

            DoorState? best = null;
            foreach (DoorLock door in doors)
            {
                DoorState state = Evaluate(door, depth);
                if (best == null || Rank(state) > Rank(best.Value))
                    best = state;
            }
            return best;
        }

        /// <summary>Whether a festival tomorrow will lock this door all day.</summary>
        public static bool FestivalClosesTomorrow(DoorLock door, FestivalInfo? tomorrowsFestival)
        {
            GameLocation? from = Game1.getLocationFromName(door.FromLocation);
            return tomorrowsFestival is { } f && f.Start < 1900 && from?.InValleyContext() == true;
        }

        /// <summary>
        /// The NPC whose friendship every door into a location needs, e.g. Caroline for the Sunroom. Events there that
        /// feature them are their heart events, even without a friendship requirement of their own.
        /// </summary>
        public static (string Npc, int Points)? GetResidentFriendship(string locationName)
        {
            if (!locks.TryGetValue(locationName, out List<DoorLock>? doors) || doors.Count == 0)
                return null;
            DoorLock first = doors[0];
            if (first.Npc == null || first.MinFriendship <= 0 || doors.Any(d => d.Npc != first.Npc || d.MinFriendship <= 0))
                return null;
            return (first.Npc, doors.Min(d => d.MinFriendship));
        }

        private static int Rank(DoorState s) => (s.MailOk ? 8 : 0) + (s.HeartsOk ? 4 : 0) + (s.FestivalClosed ? 0 : 2) + (s.AllDay ? 1 : 0);

        /// <summary>Applies the game's lockedDoorWarp rules to one door.</summary>
        private static DoorState Evaluate(DoorLock door, int depth)
        {
            GameLocation? from = Game1.getLocationFromName(door.FromLocation);

            // the game's special doors: no hours of their own, but a door inside a shop keeps the shop's hours
            if (door.RequiredMail != null || door.Inner)
            {
                Farmer mailOf = door.HostMail ? Game1.MasterPlayer : Game1.player;
                bool mailOk = door.RequiredMail == null || door.RequiredMail.Split('|').Any(mailOf.mailReceived.Contains);
                bool friendsOk = door.MinFriendship <= 0
                    || (door.Npc != null && Game1.player.friendshipData.TryGetValue(door.Npc, out Friendship? f) && f.Points >= door.MinFriendship);
                return door.Inner && GetState(door.FromLocation, depth + 1) is { } outer
                    ? outer with { Door = door, MailOk = outer.MailOk && mailOk, HeartsOk = outer.HeartsOk && friendsOk }
                    : new DoorState(door, friendsOk, FestivalClosed: false, door.Open, door.Close, AllDay: true, mailOk);
            }

            bool valley = from?.InValleyContext() ?? true;

            // the Town Key opens valley doors at any hour (not the night market's)
            bool allDay = Game1.player.HasTownKey && valley && from is not BeachNightMarket;
            int open = door.ToLocation == "FishShop" && Game1.player.mailReceived.Contains("willyHours") ? 800 : door.Open;

            bool heartsOk = door.MinFriendship <= 0
                || from?.IsWinterHere() == true
                || (door.Npc != null && Game1.player.friendshipData.TryGetValue(door.Npc, out Friendship? friendship) && friendship.Points >= door.MinFriendship);

            bool festivalClosed = GameLocation.AreStoresClosedForFestival() && valley;
            return new DoorState(door, heartsOk, festivalClosed, open, door.Close, allDay);
        }

        private static void ScanLayer(GameLocation location, Layer? layer, string property, MapScan scan)
        {
            if (layer == null)
                return;

            for (int x = 0; x < layer.LayerWidth; x++)
            {
                for (int y = 0; y < layer.LayerHeight; y++)
                {
                    Tile? tile = layer.Tiles[x, y];
                    if (tile == null)
                        continue;

                    string? action = Read(tile.Properties, property) ?? Read(tile.TileIndexProperties, property);
                    if (action == null)
                        continue;

                    // any word could be a location (other mods' warp actions), checked against the names when combining
                    string[] args = ArgUtility.SplitBySpace(action);
                    ReadAction(location, args, scan);
                    scan.Mentions.UnionWith(args);
                }
            }
        }

        private static bool IsSpecial(DoorLock door) => door.RequiredMail != null || door.Inner;

        private static string? Read(IPropertyCollection? properties, string key) =>
            properties != null && properties.TryGetValue(key, out PropertyValue? value) ? value?.ToString() : null;

        private static void ReadAction(GameLocation location, string[] args, MapScan scan)
        {
            if (args.Length == 0)
                return;

            string from = location.NameOrUniqueName;
            switch (args[0])
            {
                // LockedDoorWarp <x> <y> <location> <open> <close> [npc] [minFriendship]
                case "LockedDoorWarp" when args.Length >= 6 && int.TryParse(args[4], out int open) && int.TryParse(args[5], out int close):
                {
                    string? npc = args.Length > 6 && args[6].Length > 0 ? args[6] : null;
                    int min = args.Length > 7 && int.TryParse(args[7], out int points) ? points : 0;
                    scan.Doors.Add(new DoorLock(from, args[3], open, close, npc, min));
                    break;
                }

                // special doors, from GameLocation.performAction and FishShop.performAction
                case "WarpBoatTunnel":
                    scan.Doors.Add(new DoorLock(from, "BoatTunnel", 600, 2600, null, 0, RequiredMail: "willyBackRoomInvitation", Inner: true));
                    break;
                case "Warp_Sunroom_Door":
                    scan.Doors.Add(new DoorLock(from, "Sunroom", 600, 2600, "Caroline", 2 * NPC.friendshipPointsPerHeartLevel, Inner: true));
                    break;
                case "WarpCommunityCenter":
                    scan.Doors.Add(new DoorLock(from, "CommunityCenter", 600, 2600, null, 0, RequiredMail: "ccDoorUnlock|JojaMember", HostMail: true));
                    break;

                // Warp <x> <y> <location>
                case "Warp" or "WarpMensLocker" or "WarpWomensLocker" when args.Length >= 4 && !int.TryParse(args[3], out _):
                    scan.OpenTo.Add(args[3]);
                    break;

                // TouchAction Warp <location> <x> <y> and MagicWarp <location> <x> <y>
                case "Warp" or "MagicWarp" when args.Length >= 2:
                    scan.OpenTo.Add(args[1]);
                    break;
            }
        }
    }
}
