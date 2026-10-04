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

        /// <summary>The day the maps were last scanned, or null to scan them again.</summary>
        private static int? scannedDay;

        /// <summary>Scans the maps again next time, e.g. after a map changes or another save loads.</summary>
        public static void Invalidate() => scannedDay = null;

        /// <summary>
        /// Scans the maps unless they've already been scanned today. Scanning reads every tile of every map, and maps
        /// rarely change mid-day (<see cref="Invalidate"/> covers that), so event data changes and other split-screen
        /// players don't each need their own scan.
        /// </summary>
        public static void EnsureScanned(IMonitor monitor)
        {
            int today = SDate.Now().DaysSinceStart;
            if (scannedDay == today)
                return;
            Rebuild(monitor);
            scannedDay = today;
        }

        private static void Rebuild(IMonitor monitor)
        {
            var timer = Stopwatch.StartNew();
            var lockedInto = new Dictionary<string, List<DoorLock>>();

            // where each location's unlocked ways out lead (warps, and warp actions), and which locations are outdoors
            var openTo = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            var outdoors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // where each location's warps, doors and map actions lead, to find areas there's no way into yet
            var names = new HashSet<string>(Game1.locationData.Keys, StringComparer.OrdinalIgnoreCase);
            Utility.ForEachLocation(location =>
            {
                names.Add(location.NameOrUniqueName);
                return true;
            });
            var routes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            Utility.ForEachLocation(location =>
            {
                try
                {
                    string from = location.NameOrUniqueName;
                    if (!routes.TryGetValue(from, out HashSet<string>? leadsTo))
                        routes[from] = leadsTo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (location.IsOutdoors)
                        outdoors.Add(from);

                    foreach (Warp warp in location.warps)
                    {
                        if (!warp.npcOnly.Value)
                        {
                            AddOpen(openTo, from, warp.TargetName);
                            leadsTo.Add(warp.TargetName);
                        }
                    }
                    foreach (KeyValuePair<Microsoft.Xna.Framework.Point, string> door in location.doors.Pairs)
                        leadsTo.Add(door.Value);

                    ScanLayer(location, location.Map?.GetLayer("Buildings"), "Action", lockedInto, openTo, names, leadsTo);
                    ScanLayer(location, location.Map?.GetLayer("Back"), "TouchAction", lockedInto, openTo, names, leadsTo);
                }
                catch (Exception ex)
                {
                    monitor.Log($"Couldn't scan doors in {location.NameOrUniqueName}: {ex.Message}", LogLevel.Trace);
                }
                return true;
            });

            // what can be walked to from outdoors without a locked door in the way: an unlocked way in from there makes a
            // LockedDoorWarp moot, but one from inside (Sebastian's stairs down into Robin's house) doesn't
            var free = new HashSet<string>(outdoors, StringComparer.OrdinalIgnoreCase);
            var queue = new Queue<string>(free);
            while (queue.Count > 0)
            {
                if (!openTo.TryGetValue(queue.Dequeue(), out HashSet<string>? next))
                    continue;
                foreach (string target in next)
                {
                    if (free.Add(target))
                        queue.Enqueue(target);
                }
            }

            // the game's special doors are the only way in for players, wherever else warps lead from
            var found = lockedInto
                .Select(p => (p.Key, Doors: free.Contains(p.Key) ? p.Value.Where(IsSpecial).ToList() : p.Value))
                .Where(p => p.Doors.Count > 0)
                .ToDictionary(p => p.Key, p => p.Doors, StringComparer.OrdinalIgnoreCase);

            // rooms only reached from inside a locked building (Sebastian's room, Harvey's) share its door
            var inside = new Queue<string>(found.Keys);
            while (inside.Count > 0)
            {
                string building = inside.Dequeue();
                if (!openTo.TryGetValue(building, out HashSet<string>? rooms))
                    continue;
                foreach (string room in rooms)
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
            AreaAccess.Rebuild(names, routes, monitor);
            monitor.Log($"Found {locks.Count} locations behind locked doors in {timer.ElapsedMilliseconds}ms.", LogLevel.Trace);
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

        /// <param name="names">Every location's name, to spot actions that lead to one (including other mods' warp actions).</param>
        /// <param name="leadsTo">Where this location's map actions lead.</param>
        private static void ScanLayer(GameLocation location, Layer? layer, string property, Dictionary<string, List<DoorLock>> lockedInto, Dictionary<string, HashSet<string>> openTo, HashSet<string> names, HashSet<string> leadsTo)
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

                    ReadAction(location, action, lockedInto, openTo);
                    foreach (string arg in ArgUtility.SplitBySpace(action))
                    {
                        if (names.Contains(arg))
                            leadsTo.Add(arg);
                    }
                }
            }
        }

        private static bool IsSpecial(DoorLock door) => door.RequiredMail != null || door.Inner;

        private static void Add(Dictionary<string, List<DoorLock>> lockedInto, DoorLock door)
        {
            if (!lockedInto.TryGetValue(door.ToLocation, out List<DoorLock>? list))
                lockedInto[door.ToLocation] = list = new List<DoorLock>();
            list.Add(door);
        }

        private static string? Read(IPropertyCollection? properties, string key) =>
            properties != null && properties.TryGetValue(key, out PropertyValue? value) ? value?.ToString() : null;

        /// <summary>Records an unlocked way from one location into another.</summary>
        private static void AddOpen(Dictionary<string, HashSet<string>> openTo, string from, string to)
        {
            if (!openTo.TryGetValue(from, out HashSet<string>? targets))
                openTo[from] = targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            targets.Add(to);
        }

        private static void ReadAction(GameLocation location, string action, Dictionary<string, List<DoorLock>> lockedInto, Dictionary<string, HashSet<string>> openTo)
        {
            string[] args = ArgUtility.SplitBySpace(action);
            if (args.Length == 0)
                return;

            switch (args[0])
            {
                // LockedDoorWarp <x> <y> <location> <open> <close> [npc] [minFriendship]
                case "LockedDoorWarp" when args.Length >= 6 && int.TryParse(args[4], out int open) && int.TryParse(args[5], out int close):
                {
                    string? npc = args.Length > 6 && args[6].Length > 0 ? args[6] : null;
                    int min = args.Length > 7 && int.TryParse(args[7], out int points) ? points : 0;
                    if (!lockedInto.TryGetValue(args[3], out List<DoorLock>? list))
                        lockedInto[args[3]] = list = new List<DoorLock>();
                    list.Add(new DoorLock(location.NameOrUniqueName, args[3], open, close, npc, min));
                    break;
                }

                // special doors, from GameLocation.performAction and FishShop.performAction
                case "WarpBoatTunnel":
                    Add(lockedInto, new DoorLock(location.NameOrUniqueName, "BoatTunnel", 600, 2600, null, 0, RequiredMail: "willyBackRoomInvitation", Inner: true));
                    break;
                case "Warp_Sunroom_Door":
                    Add(lockedInto, new DoorLock(location.NameOrUniqueName, "Sunroom", 600, 2600, "Caroline", 2 * NPC.friendshipPointsPerHeartLevel, Inner: true));
                    break;
                case "WarpCommunityCenter":
                    Add(lockedInto, new DoorLock(location.NameOrUniqueName, "CommunityCenter", 600, 2600, null, 0, RequiredMail: "ccDoorUnlock|JojaMember", HostMail: true));
                    break;

                // Warp <x> <y> <location>
                case "Warp" or "WarpMensLocker" or "WarpWomensLocker" when args.Length >= 4 && !int.TryParse(args[3], out _):
                    AddOpen(openTo, location.NameOrUniqueName, args[3]);
                    break;

                // TouchAction Warp <location> <x> <y> and MagicWarp <location> <x> <y>
                case "Warp" or "MagicWarp" when args.Length >= 2:
                    AddOpen(openTo, location.NameOrUniqueName, args[1]);
                    break;
            }
        }
    }
}
