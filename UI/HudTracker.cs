using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcEventTracker.Data;
using StardewValley;
using StardewValley.Menus;

namespace NpcEventTracker.UI
{
    /// <summary>A compact always-on box listing each pinned NPC's next event.</summary>
    internal sealed class HudTracker
    {
        private static readonly Color ReadyColor = new(20, 130, 40);
        private static readonly Color MutedColor = new(110, 100, 90);
        private static readonly Color SoonColor = new(185, 105, 0);

        private readonly ModEntry mod;
        private List<(string Text, Color Color)> lines = new();
        private int builtVersion = -1;
        private int pinCount = -1;

        /// <summary>Where the box was last drawn, in UI pixels; empty if it's hidden.</summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>Whether the player is dragging the box, which highlights it.</summary>
        public bool Dragging { get; set; }

        public HudTracker(ModEntry mod)
        {
            this.mod = mod;
        }

        public void Draw(SpriteBatch b)
        {
            this.Bounds = Rectangle.Empty;
            if (!this.mod.Config.ShowHud || this.mod.PinnedNpcs.Count == 0 || Game1.eventUp || Game1.activeClickableMenu != null || !Game1.displayHUD)
                return;

            if (this.builtVersion != this.mod.Index.Version || this.pinCount != this.mod.PinnedNpcs.Count)
                this.Rebuild();

            SpriteFont font = Game1.smallFont;
            const int padding = 20;
            int lineHeight = (int)font.MeasureString("Ag").Y;
            int width = (int)this.lines.Max(l => font.MeasureString(l.Text).X) + padding * 2;
            int height = this.lines.Count * lineHeight + padding * 2;
            int x = Math.Clamp(this.mod.Config.HudX, 0, Math.Max(0, Game1.uiViewport.Width - width));
            int y = Math.Clamp(this.mod.Config.HudY, 0, Math.Max(0, Game1.uiViewport.Height - height));

            this.Bounds = new Rectangle(x, y, width, height);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), x, y, width, height, this.Dragging ? Color.Wheat : Color.White * 0.9f, 4f, drawShadow: false);
            for (int i = 0; i < this.lines.Count; i++)
                Utility.drawTextWithShadow(b, this.lines[i].Text, font, new Vector2(x + padding, y + padding + i * lineHeight), this.lines[i].Color, shadowIntensity: 0.25f);
        }

        private void Rebuild()
        {
            EventIndex index = this.mod.Index;
            var lines = new List<(string, Color)> { (I18n.Get("hud.title"), Game1.textColor) };

            var pinned = this.mod.PinnedNpcs.OrderBy(EventIndex.GetNpcDisplayName).Take(Math.Max(1, this.mod.Config.HudMaxNpcs));
            foreach (string npc in pinned)
            {
                string name = EventIndex.GetNpcDisplayName(npc);
                PendingEvents pending = index.GetPending(npc);

                // snoozed events step aside for the next one
                var awake = pending.Pending.Where(p => !this.mod.IsSnoozed(p.Event)).ToList();
                if (pending.Pending.Count > 0 && awake.Count == 0)
                    lines.Add((I18n.Get("hud.snoozed", new { name }), MutedColor));
                else if (awake.Count > 0)
                {
                    (EventInfo evt, EventEvaluation eval) = awake[0];
                    int? travel = eval.Status is EventStatus.AvailableNow or EventStatus.LaterToday ? this.mod.State.Travel.MinutesTo(evt.LocationName) : null;
                    (string stage, EventNarrator.HudTone tone) = EventNarrator.HudLine(evt, eval, this.mod.Config.ReminderMinutesBefore, travel, this.mod.Config.TravelBufferMinutes);
                    Color color = tone switch
                    {
                        EventNarrator.HudTone.Go => ReadyColor,
                        EventNarrator.HudTone.Soon => SoonColor,
                        _ => Game1.textColor
                    };
                    string more = awake.Count > 1 ? I18n.Get("hud.more", new { count = awake.Count - 1 }) : "";
                    string line = this.mod.HidesDetails(eval)
                        ? I18n.Get("hud.event-hidden", new { name, title = evt.Title })
                        : I18n.Get("hud.event", new { name, title = evt.Title, location = EventNarrator.WithArticle(evt.LocationDisplayName) });
                    lines.Add((line + more, color));
                    lines.Add(($"   {stage}", tone == EventNarrator.HudTone.Normal ? MutedColor : color));
                }
                else if (pending.NextLocked is { } next)
                {
                    int current = Game1.player.getFriendshipHeartLevelForNPC(npc);
                    lines.Add((I18n.Get("hud.next-locked", new { name, hearts = next.Event.RequiredHearts, current }), MutedColor));
                }
                else
                    lines.Add((I18n.Get("hud.caught-up", new { name }), MutedColor));
            }

            this.lines = lines;
            this.builtVersion = index.Version;
            this.pinCount = this.mod.PinnedNpcs.Count;
        }
    }
}
