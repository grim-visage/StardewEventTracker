using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewEventTracker.Data;
using StardewEventTracker.UI.Themes;
using StardewValley;

namespace StardewEventTracker.UI
{
    /// <summary>A compact always-on box listing each pinned NPC's next event and each pinned story event.</summary>
    internal sealed class HudTracker
    {
        /// <summary>How soon something needs doing; entries sort by this when the HUD is ordered by urgency.</summary>
        private enum Urgency
        {
            /// <summary>Time to visit!</summary>
            Now,

            /// <summary>Leave now / Head out soon.</summary>
            SetOff,

            /// <summary>Get ready / Later today.</summary>
            LaterToday,

            /// <summary>Waiting on the weather, the day, Green Rain or a festival.</summary>
            NotToday,

            /// <summary>Not yet, locked, snoozed or all caught up.</summary>
            Nothing
        }

        /// <summary>One pinned NPC or story event on the HUD.</summary>
        private sealed record Entry(Urgency Urgency, int Minutes, string SortName, bool IsStory, List<(string Text, Color Color)> Lines);

        private readonly ModEntry mod;
        private List<(string Text, Color Color)> lines = new();
        private int builtVersion = -1;
        private int pinCount = -1;
        private int builtForHeight = -1;
        private int builtForY = -1;
        private HudTheme? builtForTheme;

        /// <summary>How long a full fade in or out takes, in milliseconds.</summary>
        private const float FadeMs = 250;

        /// <summary>How opaque the HUD is now, easing towards fully opaque or the faded opacity in the config.</summary>
        private float opacity = 1f;

        /// <summary>Whether an entry on the HUD can happen right now, for themes that react to it.</summary>
        private bool anyAvailableNow;

        /// <summary>Where the box was last drawn, in UI pixels; empty if it's hidden.</summary>
        public Rectangle Bounds { get; private set; }

        /// <summary>Where the box itself (not its title) was last drawn, in UI pixels.</summary>
        public Point BoxPosition { get; private set; }

        /// <summary>Whether the player is dragging the box, which highlights it.</summary>
        public bool Dragging { get; set; }

        /// <summary>
        /// The mouse in UI pixels, which the HUD is drawn in. Not SMAPI's scaled screen pixels: outside drawing those are
        /// in zoomed game pixels, which put the grab area off from the box whenever zoom and UI scale differ.
        /// </summary>
        public static Point UiMouse => new(Game1.getMouseX(ui_scale: true), Game1.getMouseY(ui_scale: true));

        public HudTracker(ModEntry mod)
        {
            this.mod = mod;
        }

        public void Draw(SpriteBatch b)
        {
            this.Bounds = Rectangle.Empty;
            if (!this.mod.Config.ShowHud || !this.mod.State.HasPins || Game1.eventUp || Game1.activeClickableMenu != null || !Game1.displayHUD)
                return;

            // what fits depends on the screen height, where the box sits and the theme's title
            HudTheme theme = this.Theme;
            if (this.builtVersion != this.mod.Index.Version || this.pinCount != this.PinCount || this.builtForTheme != theme
                || this.builtForHeight != Game1.uiViewport.Height || (!this.Dragging && this.builtForY != this.TopLimit(theme)))
                this.Rebuild();

            SpriteFont font = Game1.smallFont;
            int lineHeight = LineHeight;
            int padding = theme.Padding;
            int textWidth = this.lines.Count > 0 ? (int)this.lines.Max(l => font.MeasureString(l.Text).X) : 0;
            int width = Math.Max(textWidth + padding * 2, theme.MinBoxWidth);
            int height = theme.TitleInside + this.lines.Count * lineHeight + padding * 2 + theme.FooterInside;

            // a title drawn above the box stays on screen too
            (int startX, int startY) = this.Position(width, height, theme);
            int x = Math.Clamp(startX, 0, Math.Max(0, Game1.uiViewport.Width - width));
            int y = Math.Clamp(startY, theme.TitleAbove, Math.Max(theme.TitleAbove, Game1.uiViewport.Height - height));

            var box = new Rectangle(x, y, width, height);
            Rectangle title = theme.TitleArea(box);
            this.BoxPosition = box.Location;
            this.Bounds = title.IsEmpty ? box : Rectangle.Union(box, title);

            this.UpdateOpacity();
            HudTheme.Opacity = this.opacity;
            try
            {
                theme.DrawBox(b, box, this.Dragging);
                int top = y + padding + theme.TitleInside;
                for (int i = 0; i < this.lines.Count; i++)
                    theme.DrawLine(b, this.lines[i].Text, new Vector2(x + padding, top + i * lineHeight), this.lines[i].Color);
                theme.DrawTitle(b, box, new HudState(this.anyAvailableNow));
            }
            finally
            {
                HudTheme.Opacity = 1f;
            }
        }

        /// <summary>Eases the HUD towards faded while it's in the way, or back to opaque; it stays opaque while it's being moved.</summary>
        private void UpdateOpacity()
        {
            bool inWay = this.mod.Config.HudFade && !this.Dragging && this.IsInWay();
            float faded = Math.Clamp(this.mod.Config.HudFadeOpacity, 0, 100) / 100f;
            float target = inWay ? faded : 1f;
            float step = (float)(Game1.currentGameTime?.ElapsedGameTime.TotalMilliseconds ?? FadeMs) / FadeMs;
            this.opacity = this.opacity < target ? Math.Min(target, this.opacity + step) : Math.Max(target, this.opacity - step);
        }

        /// <summary>Whether the player is standing under the HUD, or the mouse is over it (if that fades it too).</summary>
        private bool IsInWay()
        {
            if (this.mod.Config.HudFadeOnHover)
            {
                if (this.Bounds.Contains(UiMouse))
                    return true;
            }

            // the player's sprite (a tile wide, two tall, standing on their bounding box), from world to UI pixels
            Rectangle feet = Game1.player.GetBoundingBox();
            Vector2 topLeft = Game1.GlobalToLocal(Game1.viewport, new Vector2(feet.Center.X - 32, feet.Bottom - 128));
            float scale = Game1.options.zoomLevel / Game1.options.uiScale;
            var sprite = new Rectangle((int)(topLeft.X * scale), (int)(topLeft.Y * scale), (int)(64 * scale), (int)(128 * scale));
            return this.Bounds.Intersects(sprite);
        }

        /// <summary>The theme the player picked in the config.</summary>
        /// <summary>Space kept clear of the clock in the top right, and of the health and energy bars in the bottom right.</summary>
        private const int ClockHeight = 300, BarsWidth = 136, Margin = 16;

        /// <summary>The bottom presets sit above the toolbar's height, so they don't cover it on narrow screens.</summary>
        private const int ToolbarHeight = 112;

        /// <summary>Where the box goes for the position setting: a corner, or the saved custom position.</summary>
        private (int X, int Y) Position(int width, int height, HudTheme theme)
        {
            int right = Game1.uiViewport.Width - width - Margin;
            int bottom = Game1.uiViewport.Height - height - Margin;
            return this.mod.Config.HudPosition switch
            {
                ModConfig.HudPositionTopLeft => (Margin, Margin + theme.TitleAbove),
                ModConfig.HudPositionTopRight => (right, ClockHeight + theme.TitleAbove),
                ModConfig.HudPositionBottomLeft => (Margin, bottom - ToolbarHeight),
                ModConfig.HudPositionBottomRight => (right - BarsWidth, bottom),
                _ => (this.mod.Config.HudX, this.mod.Config.HudY)
            };
        }

        /// <summary>The highest the box can start, which limits how many lines fit below it.</summary>
        private int TopLimit(HudTheme theme) => this.mod.Config.HudPosition switch
        {
            ModConfig.HudPositionTopLeft => Margin + theme.TitleAbove,
            ModConfig.HudPositionTopRight => ClockHeight + theme.TitleAbove,
            // the bottom corners grow upwards, so the whole screen is room
            ModConfig.HudPositionBottomLeft => theme.TitleAbove + ToolbarHeight + Margin,
            ModConfig.HudPositionBottomRight => theme.TitleAbove + Margin,
            _ => Math.Max(theme.TitleAbove, this.mod.Config.HudY)
        };

        private HudTheme Theme => HudTheme.Get(this.mod.Config.HudTheme, this.mod.Config.HudMode == ModConfig.HudModeDark);

        private HudPalette Palette => this.Theme.Palette;

        /// <summary>Whether every entry takes one short line, so more fit.</summary>
        private bool Compact => this.mod.Config.HudLayout == ModConfig.HudLayoutCompact;

        private static int LineHeight => (int)Game1.smallFont.MeasureString("Ag").Y;

        private int PinCount => this.mod.PinnedNpcs.Count + this.mod.State.PinnedStoryEvents.Count;

        private void Rebuild()
        {
            var entries = this.mod.PinnedNpcs
                .Select(this.BuildNpcEntry)
                .Concat(this.mod.GetPinnedStoryEvents().Select(p => this.BuildStoryEntry(p.Event, p.Eval)))
                .ToList();

            entries = this.mod.Config.HudSortOrder == ModConfig.HudOrderAlphabetical
                // NPCs A-Z, then story events by location
                ? entries.OrderBy(e => e.IsStory).ThenBy(e => e.SortName, StringComparer.CurrentCultureIgnoreCase).ToList()
                : entries.OrderBy(e => e.Urgency).ThenBy(e => e.Minutes).ThenBy(e => e.SortName, StringComparer.CurrentCultureIgnoreCase).ToList();

            // fit as many entries as the setting allows and the screen has room for, keeping a line for the overflow note
            HudTheme theme = this.Theme;
            var lines = new List<(string, Color)>();
            int maxEntries = Math.Max(1, this.mod.Config.HudMaxNpcs);
            int boxY = this.TopLimit(theme);
            int maxLines = Math.Max(2, (Game1.uiViewport.Height - boxY - theme.Padding * 2 - theme.TitleInside - theme.FooterInside) / LineHeight);
            int shown = 0;
            foreach (Entry entry in entries)
            {
                bool moreAfter = shown + 1 < entries.Count;
                bool fits = shown < maxEntries && lines.Count + entry.Lines.Count + (moreAfter ? 1 : 0) <= maxLines;
                if (!fits && shown > 0)
                    break;
                lines.AddRange(entry.Lines);
                shown++;
            }

            // e.g. a pinned story event was just seen (it's unpinned at the end of the day) and the other pins aren't around today
            if (entries.Count == 0)
                lines.Add((I18n.Get("hud.nothing", new { key = this.mod.Config.OpenMenuKey }), this.Palette.Muted));

            var hidden = entries.Skip(shown).ToList();
            if (hidden.Count > 0)
            {
                // highlight the note if something you could act on soon didn't fit
                bool urgentHidden = hidden.Any(e => e.Urgency <= Urgency.SetOff);
                lines.Add((I18n.Get("hud.overflow", new { count = hidden.Count, key = this.mod.Config.OpenMenuKey }), urgentHidden ? this.Palette.Ready : this.Palette.Muted));
            }

            this.lines = lines;
            this.builtVersion = this.mod.Index.Version;
            this.pinCount = this.PinCount;
            this.builtForHeight = Game1.uiViewport.Height;
            this.builtForY = boxY;
            this.builtForTheme = theme;
            this.anyAvailableNow = entries.Any(e => e.Urgency == Urgency.Now);
        }

        private Entry BuildNpcEntry(string npc)
        {
            string name = EventIndex.GetNpcDisplayName(npc);
            PendingEvents pending = this.mod.Index.GetPending(npc);
            var next = pending.GetNext(this.mod.IsSnoozed);

            // everything pending is snoozed
            if (next == null && pending.Pending.Count > 0)
                return OneLine(name, I18n.Get("hud.snoozed", new { name }));

            if (next == null)
                return OneLine(name, I18n.Get("hud.caught-up", new { name }));

            (EventInfo evt, EventEvaluation eval) = next.Value;
            if (eval.Status == EventStatus.Locked)
            {
                string text = evt.Relationship == null && evt.RequiredPoints > 0 && evt.ProgressRank == evt.RequiredPoints
                    ? I18n.Get(this.Compact ? "hud.short.next-locked" : "hud.next-locked", new
                    {
                        name,
                        more = PreconditionFormatter.MoreFriendship(npc, evt.RequiredPoints) ?? "",
                        poss = EventNarrator.Possessive(npc),
                        title = evt.TitleInline
                    })
                    : I18n.Get("hud.next-step", new { name, step = this.mod.HidesDetails(eval) ? I18n.Get("status.hidden") : EventNarrator.NextStep(evt, eval, this.mod.Index) });
                return OneLine(name, text);
            }

            int awake = pending.Pending.Count(p => !this.mod.IsSnoozed(p.Event));
            bool hidden = this.mod.HidesDetails(eval);
            string location = EventNarrator.WithArticle(evt.LocationDisplayName);
            if (this.Compact)
            {
                // "Abigail at the Mountain" for what's on today, just "Abigail" otherwise
                string at = hidden ? name : I18n.Get("hud.short.at", new { name, location });
                return this.EventEntry(evt, eval, name, at, name, "", isStory: false);
            }

            string more = awake > 1 ? I18n.Get("hud.more", new { count = awake - 1 }) : "";
            string shortHead = I18n.Get("hud.event-hidden", new { name, title = evt.Title });
            string fullHead = hidden ? shortHead : I18n.Get("hud.event", new { name, title = evt.Title, location });
            return this.EventEntry(evt, eval, name, fullHead, shortHead, more, isStory: false);
        }

        private Entry BuildStoryEntry(EventInfo evt, EventEvaluation eval)
        {
            string place = EventNarrator.WithArticle(evt.LocationDisplayName);
            string sortName = evt.LocationDisplayName;
            if (this.mod.IsSnoozed(evt))
                return OneLine(sortName, I18n.Get("hud.story-snoozed", new { location = place }), isStory: true);

            string head = this.Compact ? sortName : I18n.Get("hud.story", new { location = place, title = evt.Title });
            return this.EventEntry(evt, eval, sortName, head, head, "", isStory: true);
        }

        /// <summary>
        /// An event entry: two lines (heading, then status) for things happening today, or one compact line for things
        /// that aren't, so the space goes to what you can act on.
        /// </summary>
        private Entry EventEntry(EventInfo evt, EventEvaluation eval, string sortName, string fullHead, string shortHead, string more, bool isStory)
        {
            (string stage, Color color, EventNarrator.HudTone tone) = this.Status(evt, eval);
            Urgency urgency = eval.Status switch
            {
                EventStatus.AvailableNow => Urgency.Now,
                EventStatus.LaterToday => tone == EventNarrator.HudTone.Urgent ? Urgency.SetOff : Urgency.LaterToday,
                EventStatus.WrongDay or EventStatus.GreenRain or EventStatus.FestivalHere or EventStatus.MissedToday => Urgency.NotToday,
                _ => Urgency.Nothing
            };
            int minutes = eval.Status == EventStatus.LaterToday ? eval.MinutesUntilStart ?? 0 : 0;

            if (urgency >= Urgency.NotToday)
            {
                string line = I18n.Get(this.Compact ? "hud.short.line" : "hud.compact", new { head = shortHead + more, stage });
                return new Entry(urgency, minutes, sortName, isStory, new List<(string, Color)> { (line, this.Palette.Muted) });
            }

            // compact: what's on today in one line too, coloured by how soon
            if (this.Compact)
                return new Entry(urgency, minutes, sortName, isStory, new List<(string, Color)> { (I18n.Get("hud.short.line", new { head = fullHead, stage }), color) });

            var lines = new List<(string, Color)>
            {
                (fullHead + more, color),
                ($"   {stage}", tone == EventNarrator.HudTone.Normal ? this.Palette.Muted : color)
            };
            return new Entry(urgency, minutes, sortName, isStory, lines);
        }

        private Entry OneLine(string sortName, string text, bool isStory = false) =>
            new(Urgency.Nothing, 0, sortName, isStory, new List<(string, Color)> { (text, this.Palette.Muted) });

        /// <summary>The coloured status line for an event, from <see cref="EventNarrator.HudLine"/>.</summary>
        private (string Stage, Color Color, EventNarrator.HudTone Tone) Status(EventInfo evt, EventEvaluation eval)
        {
            // spoiler-free mode doesn't say what's still needed
            if (this.mod.HidesDetails(eval))
                return (I18n.Get("status.hidden"), this.Palette.Muted, EventNarrator.HudTone.Normal);

            int? travel = eval.Status is EventStatus.AvailableNow or EventStatus.LaterToday ? this.mod.State.Travel.MinutesTo(evt.LocationName) : null;
            (string stage, EventNarrator.HudTone tone) = EventNarrator.HudLine(evt, eval, this.mod.Config.ReminderMinutesBefore, travel, this.mod.Config.TravelBufferMinutes, this.mod.Index, this.Compact);
            Color color = tone switch
            {
                EventNarrator.HudTone.Go => this.Palette.Ready,
                EventNarrator.HudTone.Urgent or EventNarrator.HudTone.Soon => this.Palette.Soon,
                _ => this.Palette.Text
            };
            return (stage, color, tone);
        }
    }
}
