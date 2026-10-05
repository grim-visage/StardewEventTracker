using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;
using SObject = StardewValley.Object;

namespace StardewEventTracker.Data
{
    /// <summary>A loved gift the player has: one of the item, whether it's in their inventory, and how many they have in all.</summary>
    internal sealed record OwnedGift(Item Item, bool Carried, int Count);

    /// <summary>
    /// Each NPC's loved gifts that the player owns, in their inventory or in any chest, fridge or Junimo chest. The
    /// scan of what's owned is cached until something changes (see <see cref="Invalidate"/>), and each NPC's list is
    /// worked out the first time it's asked for.
    /// </summary>
    internal sealed class LovedGifts
    {
        /// <summary>How many gifts to show per NPC.</summary>
        public const int MaxShown = 3;

        /// <summary>One of each giftable item owned, by qualified item ID.</summary>
        private readonly Dictionary<string, Item> owned = new();

        /// <summary>How many of each item are owned, carried or stored.</summary>
        private readonly Dictionary<string, int> counts = new();

        /// <summary>Items in the player's inventory.</summary>
        private readonly HashSet<string> carried = new();

        /// <summary>Each NPC's loved gifts, by NPC name and whether only tastes the player has discovered count.</summary>
        private readonly Dictionary<(string Npc, bool KnownOnly), IReadOnlyList<OwnedGift>> perNpc = new();

        private readonly IMonitor monitor;

        private bool dirty = true;

        public LovedGifts(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        /// <summary>Forgets what's owned, so it's scanned again on next use (the inventory changed, the menu opened, or a new day).</summary>
        public void Invalidate()
        {
            this.dirty = true;
        }

        /// <summary>Up to <see cref="MaxShown"/> loved gifts the player owns for this NPC: carried ones first, then the most plentiful.</summary>
        /// <param name="knownOnly">Only gifts the player has found out they love (spoiler-free mode).</param>
        public IReadOnlyList<OwnedGift> For(string npcName, bool knownOnly)
        {
            this.Refresh();
            if (this.perNpc.TryGetValue((npcName, knownOnly), out IReadOnlyList<OwnedGift>? cached))
                return cached;

            var gifts = new List<OwnedGift>();
            NPC? npc = Game1.getCharacterFromName(npcName);
            if (npc != null && SafeCanReceiveGifts(npc))
            {
                foreach ((string id, Item item) in this.owned)
                {
                    if (knownOnly && !Game1.player.hasGiftTasteBeenRevealed(npc, item.ItemId))
                        continue;
                    if (IsLoved(npc, item))
                        gifts.Add(new OwnedGift(item, this.carried.Contains(id), this.counts[id]));
                }
            }

            IReadOnlyList<OwnedGift> result = gifts
                .OrderByDescending(g => g.Carried)
                .ThenByDescending(g => g.Count)
                .ThenBy(g => g.Item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxShown)
                .ToList();
            this.perNpc[(npcName, knownOnly)] = result;
            return result;
        }

        /// <summary>Whether the player can give this NPC a gift today: one a day, two a week (more on their birthday, or for a spouse).</summary>
        public static bool CanGiftToday(string npcName) => GiftReason(npcName).Can;

        /// <summary>Whether the player can give this NPC a gift today, and the translation key saying why (or why not).</summary>
        public static (bool Can, string ReasonKey) GiftReason(string npcName)
        {
            bool birthday = Game1.getCharacterFromName(npcName)?.isBirthday() == true;
            if (!Game1.player.friendshipData.TryGetValue(npcName, out Friendship? friendship))
                return (true, birthday ? "gifts.birthday" : "gifts.can");
            if (friendship.GiftsToday >= 1)
                return (false, "gifts.given-today");
            if (birthday)
                return (true, "gifts.birthday");
            if (friendship.GiftsThisWeek < 2)
                return (true, friendship.GiftsThisWeek == 1 ? "gifts.can-one-more" : "gifts.can");
            if (friendship.IsMarried() || friendship.IsRoommate())
                return (true, "gifts.spouse");

            // the weekly count resets when a new week starts on Sunday
            return (false, "gifts.week-full");
        }

        private void Refresh()
        {
            if (!this.dirty)
                return;
            this.dirty = false;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            this.owned.Clear();
            this.counts.Clear();
            this.carried.Clear();
            this.perNpc.Clear();

            foreach (Item? item in Game1.player.Items)
                this.Add(item, carried: true);

            long playerId = Game1.player.UniqueMultiplayerID;
            Utility.ForEachLocation(location =>
            {
                foreach (SObject obj in location.objects.Values)
                {
                    // chests, big and stone chests, mini-fridges and Junimo chests (which share one inventory)
                    if (obj is Chest { playerChest.Value: true } chest)
                    {
                        foreach (Item? item in chest.GetItemsForPlayer(playerId))
                            this.Add(item, carried: false);
                    }
                }

                // the kitchen fridge isn't a placed object
                Chest? fridge = location switch
                {
                    FarmHouse house => house.fridge.Value,
                    IslandFarmHouse islandHouse => islandHouse.fridge.Value,
                    _ => null
                };
                if (fridge != null)
                {
                    foreach (Item? item in fridge.Items)
                        this.Add(item, carried: false);
                }
                return true;
            });
            this.monitor.Log($"Scanned for loved gifts: {this.owned.Count} giftable items owned ({timer.ElapsedMilliseconds}ms).");
        }

        private void Add(Item? item, bool carried)
        {
            if (item is not SObject { bigCraftable.Value: false } obj || !SafeIsGiftable(obj))
                return;

            string id = item.QualifiedItemId;
            this.owned.TryAdd(id, item);
            this.counts[id] = this.counts.GetValueOrDefault(id) + item.Stack;
            if (carried)
                this.carried.Add(id);
        }

        private static bool IsLoved(NPC npc, Item item)
        {
            try
            {
                return npc.getGiftTasteForThisItem(item) == NPC.gift_taste_love;
            }
            catch (Exception)
            {
                // a mod's broken gift taste data
                return false;
            }
        }

        private static bool SafeIsGiftable(SObject obj)
        {
            try
            {
                return obj.canBeGivenAsGift();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool SafeCanReceiveGifts(NPC npc)
        {
            try
            {
                return npc.CanReceiveGifts();
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
