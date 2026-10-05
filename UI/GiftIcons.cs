using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewEventTracker.Data;
using StardewValley;

namespace StardewEventTracker.UI
{
    /// <summary>Draws an NPC's loved gifts as a row of small item icons, for the menu and the HUD.</summary>
    internal static class GiftIcons
    {
        /// <summary>The space between icons, in pixels.</summary>
        private const int Gap = 4;

        /// <summary>How opaque the icons are when the NPC can't be given a gift today.</summary>
        public const float CantGiftAlpha = 0.35f;

        /// <summary>The width a row of this many icons takes.</summary>
        public static int Width(int count, int size) => count <= 0 ? 0 : count * size + (count - 1) * Gap;

        /// <summary>Draws the gifts left to right from <paramref name="x"/>, centred on <paramref name="centerY"/>, stopping at <paramref name="maxRight"/>.</summary>
        /// <returns>The area each drawn icon covers, for hover text.</returns>
        public static List<Rectangle> Draw(SpriteBatch b, IReadOnlyList<OwnedGift> gifts, int x, int centerY, int size, float alpha, int maxRight)
        {
            var areas = new List<Rectangle>();
            foreach (OwnedGift gift in gifts)
            {
                if (x + size > maxRight)
                    break;

                // drawInMenu centres the item on location + (32, 32) at 4x its scale, whatever the scale
                var area = new Rectangle(x, centerY - size / 2, size, size);
                gift.Item.drawInMenu(b, new Vector2(area.Center.X - 32, area.Center.Y - 32), size / 64f, alpha, 0.88f, StackDrawType.Hide, Color.White, drawShadow: false);
                areas.Add(area);
                x += size + Gap;
            }
            return areas;
        }

        /// <summary>Hover text listing the gifts and where they are, and whether the NPC can take one today.</summary>
        public static string Tooltip(string npc, IReadOnlyList<OwnedGift> gifts)
        {
            var lines = new List<string> { I18n.Get("gifts.tooltip") };
            lines.AddRange(gifts.Select(g => I18n.Get(g.Carried ? "gifts.carried" : "gifts.stored", new { item = g.Item.DisplayName, count = g.Count })));
            lines.Add(I18n.Get(LovedGifts.GiftReason(npc).ReasonKey, new { name = EventIndex.GetNpcDisplayName(npc) }));
            return string.Join("\n", lines);
        }
    }
}
