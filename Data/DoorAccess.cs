using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StardewModdingAPI;
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
        /// <summary>Locked doors by the location they lead to. Locations with any unlocked entrance aren't listed.</summary>
        private static Dictionary<string, List<DoorLock>> locks = new();

        /// <summary>How many doors deep an inner door's outer door is followed, so doors that lead into each other can't loop.</summary>
        private const int MaxInnerDepth = 8;

        public static void Rebuild(IMonitor monitor)
        {
            var timer = Stopwatch.StartNew();
            var lockedInto = new Dictionary<string, List<DoorLock>>();
            var openEntrances = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Utility.ForEachLocation(location =>
            {
                try
                {
                    foreach (Warp warp in location.warps)
                    {
                        if (!warp.npcOnly.Value)
                            openEntrances.Add(warp.TargetName);
                    }
                    ScanLayer(location, location.Map?.GetLayer("Buildings"), "Action", lockedInto, openEntrances);
                    ScanLayer(location, location.Map?.GetLayer("Back"), "TouchAction", lockedInto, openEntrances);
                }
                catch (Exception ex)
                {
                    monitor.Log($"Couldn't scan doors in {location.NameOrUniqueName}: {ex.Message}", LogLevel.Trace);
                }
                return true;
            });

            // another way in makes a LockedDoorWarp moot, but the game's special doors are the only way in for players
            locks = lockedInto
                .Select(p => (p.Key, Doors: openEntrances.Contains(p.Key) ? p.Value.Where(IsSpecial).ToList() : p.Value))
                .Where(p => p.Doors.Count > 0)
                .ToDictionary(p => p.Key, p => p.Doors);
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

        private static void ScanLayer(GameLocation location, Layer? layer, string property, Dictionary<string, List<DoorLock>> lockedInto, HashSet<string> openEntrances)
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
                    if (action != null)
                        ReadAction(location, action, lockedInto, openEntrances);
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

        private static void ReadAction(GameLocation location, string action, Dictionary<string, List<DoorLock>> lockedInto, HashSet<string> openEntrances)
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
                    openEntrances.Add(args[3]);
                    break;

                // TouchAction Warp <location> <x> <y> and MagicWarp <location> <x> <y>
                case "Warp" or "MagicWarp" when args.Length >= 2:
                    openEntrances.Add(args[1]);
                    break;
            }
        }
    }
}
