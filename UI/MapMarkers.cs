using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewEventTracker.Data;
using StardewModdingAPI.Utilities;
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

        /// <summary>
        /// How near a controller's cursor has to be to a marker to show its tooltip. The cursor jumps between the map's
        /// areas rather than moving freely, so it rarely lands on the heart itself.
        /// </summary>
        private const int GamepadReach = 64;

        private readonly ModEntry mod;

        /// <summary>The markers last worked out, and for which map region and evaluations.</summary>
        private sealed class Cache
        {
            public List<(Vector2 Pixel, bool Now, string Label)> Markers = new();
            public int Version = -1;
            public string? Region;
        }

        // each split-screen player has their own pins and events
        private readonly PerScreen<Cache> cache = new(() => new Cache());

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

            // statuses only change when the index does, so this isn't worked out again every frame
            Cache cache = this.cache.Value;
            if (cache.Version != this.mod.Index.Version || cache.Region != page.mapRegion.Id)
            {
                cache.Markers.Clear();
                foreach ((EventInfo evt, EventEvaluation eval, string label) in this.GetMarkers())
                {
                    if (GetMapPixel(evt.LocationName, page) is { } markerPixel)
                        cache.Markers.Add((markerPixel, eval.Status == EventStatus.AvailableNow, label));
                }
                cache.Version = this.mod.Index.Version;
                cache.Region = page.mapRegion.Id;
            }

            var hovered = new List<string>();
            int mouseX = Game1.getMouseX(), mouseY = Game1.getMouseY();
            var stacked = new Dictionary<Point, int>();
            float bob = (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 250.0) * 3f;

            foreach ((Vector2 pixel, bool now, string label) in cache.Markers)
            {
                // several events at one spot sit side by side
                Point spot = new((int)pixel.X, (int)pixel.Y);
                stacked.TryGetValue(spot, out int count);
                stacked[spot] = count + 1;

                var position = new Vector2(
                    page.mapBounds.X + pixel.X - HeartSprite.Width * Scale / 2 + count * 30,
                    page.mapBounds.Y + pixel.Y - HeartSprite.Height * Scale - 8 + (now ? bob : 0));
                var area = new Rectangle((int)position.X, (int)position.Y, (int)(HeartSprite.Width * Scale), (int)(HeartSprite.Height * Scale));

                b.Draw(Game1.mouseCursors, position + new Vector2(2, 2), HeartSprite, Color.Black * 0.35f, 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);
                b.Draw(Game1.mouseCursors, position, HeartSprite, now ? Color.White : Color.White * 0.55f, 0f, Vector2.Zero, Scale, SpriteEffects.None, 1f);

                bool near = Game1.options.gamepadControls
                    && Vector2.Distance(new Vector2(mouseX, mouseY), new Vector2(area.Center.X, area.Center.Y)) <= GamepadReach;
                if (area.Contains(mouseX, mouseY) || near)
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
