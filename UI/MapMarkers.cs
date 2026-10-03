using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewEventTracker.Data;
using StardewValley;
using StardewValley.Menus;
using StardewValley.WorldMaps;

namespace StardewEventTracker.UI
{
    /// <summary>
    /// Draws a heart on the map page where pinned NPCs' events can happen today. Works with any MapPage, including
    /// the subclasses NPC Map Locations and World Maps use, since they keep the same map bounds and positions.
    /// </summary>
    internal sealed class MapMarkers
    {
        /// <summary>The filled heart from the social page.</summary>
        private static readonly Rectangle HeartSprite = new(211, 428, 7, 6);

        private const float Scale = 4f;

        private readonly ModEntry mod;

        public MapMarkers(ModEntry mod)
        {
            this.mod = mod;
        }

        public void Draw(SpriteBatch b)
        {
            MapPage? page = Game1.activeClickableMenu switch
            {
                GameMenu menu => menu.GetCurrentPage() as MapPage,
                MapPage map => map,
                _ => null
            };
            if (page == null)
                return;

            var hovered = new List<string>();
            int mouseX = Game1.getMouseX(), mouseY = Game1.getMouseY();
            var stacked = new Dictionary<Point, int>();
            float bob = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0) * 3f;

            foreach ((EventInfo evt, EventEvaluation eval, string label) in this.GetMarkers())
            {
                Vector2? pixel = GetMapPixel(evt.LocationName, page);
                if (pixel == null)
                    continue;

                // several events at one spot sit side by side
                Point spot = new((int)pixel.Value.X, (int)pixel.Value.Y);
                stacked.TryGetValue(spot, out int count);
                stacked[spot] = count + 1;

                bool now = eval.Status == EventStatus.AvailableNow;
                var position = new Vector2(
                    page.mapBounds.X + pixel.Value.X - HeartSprite.Width * Scale / 2 + count * 30,
                    page.mapBounds.Y + pixel.Value.Y - HeartSprite.Height * Scale - 8 + (now ? bob : 0));
                var area = new Rectangle((int)position.X, (int)position.Y, (int)(HeartSprite.Width * Scale), (int)(HeartSprite.Height * Scale));

                b.Draw(Game1.mouseCursors, position + new Vector2(2, 2), HeartSprite, Color.Black * 0.35f, 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);
                b.Draw(Game1.mouseCursors, position, HeartSprite, now ? Color.White : Color.White * 0.55f, 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);

                if (area.Contains(mouseX, mouseY))
                    hovered.Add(label);
            }

            if (hovered.Count > 0)
                IClickableMenu.drawHoverText(b, string.Join("\n", hovered), Game1.smallFont);

            // the menu already drew the cursor; draw it again on top of the markers
            if (hovered.Count > 0 || stacked.Count > 0)
                Game1.activeClickableMenu?.drawMouse(b);
        }

        /// <summary>Pinned NPCs' events that can happen today, with their tooltip text.</summary>
        private IEnumerable<(EventInfo Event, EventEvaluation Eval, string Label)> GetMarkers()
        {
            foreach ((EventInfo evt, EventEvaluation eval) in this.mod.GetPinnedStoryEvents())
            {
                if (eval.Status is not (EventStatus.AvailableNow or EventStatus.LaterToday) || this.mod.IsSnoozed(evt))
                    continue;

                (string status, _) = EventNarrator.HudLine(evt, eval, this.mod.Config.ReminderMinutesBefore);
                yield return (evt, eval, I18n.Get("map.tooltip.story", new { title = evt.Title, location = EventNarrator.WithArticle(evt.LocationDisplayName), status }));
            }

            foreach (string npc in this.mod.PinnedNpcs)
            {
                string name = EventIndex.GetNpcDisplayName(npc);
                foreach ((EventInfo evt, EventEvaluation eval) in this.mod.Index.GetPending(npc).Pending)
                {
                    if (eval.Status is not (EventStatus.AvailableNow or EventStatus.LaterToday) || this.mod.IsSnoozed(evt) || this.mod.HidesDetails(eval))
                        continue;

                    (string status, _) = EventNarrator.HudLine(evt, eval, this.mod.Config.ReminderMinutesBefore);
                    yield return (evt, eval, I18n.Get("map.tooltip", new { name, title = evt.Title, location = EventNarrator.WithArticle(evt.LocationDisplayName), status }));
                }
            }
        }

        /// <summary>A location's position on the shown map region, in map pixels; null if it isn't on this region.</summary>
        private static Vector2? GetMapPixel(string locationName, MapPage page)
        {
            try
            {
                GameLocation? location = Game1.getLocationFromName(locationName);
                if (location?.Map == null)
                    return null;

                var layer = location.Map.Layers[0];
                var tile = new Point(layer.LayerWidth / 2, layer.LayerHeight / 2);
                MapAreaPositionWithContext? position = WorldMapManager.GetPositionData(location, tile);
                if (position == null || position.Value.Data.Region.Id != page.mapRegion.Id)
                    return null;
                return position.Value.GetMapPixelPosition();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
