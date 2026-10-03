using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewEventTracker.Data;
using StardewValley;
using StardewValley.Menus;

namespace StardewEventTracker.UI
{
    /// <summary>A compact always-on box listing each pinned NPC's next event and each pinned story event.</summary>
    internal sealed class HudTracker
    {
        private static readonly Color ReadyColor = new(20, 130, 40);
        private static readonly Color MutedColor = new(110, 100, 90);
        private static readonly Color SoonColor = new(185, 105, 0);

        private const int Padding = 20;

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
            if (!this.mod.Config.ShowHud || !this.mod.State.HasPins || Game1.eventUp || Game1.activeClickableMenu != null || !Game1.displayHUD)
                return;

            // what fits depends on the screen height and where the box sits
            if (this.builtVersion != this.mod.Index.Version || this.pinCount != this.PinCount
                || this.builtForHeight != Game1.uiViewport.Height || (!this.Dragging && this.builtForY != this.mod.Config.HudY))
                this.Rebuild();

            SpriteFont font = Game1.smallFont;
            int lineHeight = LineHeight;
            int width = (int)this.lines.Max(l => font.MeasureString(l.Text).X) + Padding * 2;
            int height = this.lines.Count * lineHeight + Padding * 2;
            int x = Math.Clamp(this.mod.Config.HudX, 0, Math.Max(0, Game1.uiViewport.Width - width));
            int y = Math.Clamp(this.mod.Config.HudY, 0, Math.Max(0, Game1.uiViewport.Height - height));

            this.Bounds = new Rectangle(x, y, width, height);
            IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), x, y, width, height, this.Dragging ? Color.Wheat : Color.White * 0.9f, 4f, drawShadow: false);
            for (int i = 0; i < this.lines.Count; i++)
                Utility.drawTextWithShadow(b, this.lines[i].Text, font, new Vector2(x + Padding, y + Padding + i * lineHeight), this.lines[i].Color, shadowIntensity: 0.25f);
        }

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
            var lines = new List<(string, Color)> { (I18n.Get("hud.title"), Game1.textColor) };
            int maxEntries = Math.Max(1, this.mod.Config.HudMaxNpcs);
            int maxLines = Math.Max(3, (Game1.uiViewport.Height - Math.Max(0, this.mod.Config.HudY) - Padding * 2) / LineHeight);
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

            var hidden = entries.Skip(shown).ToList();
            if (hidden.Count > 0)
            {
                // highlight the note if something you could act on soon didn't fit
                bool urgentHidden = hidden.Any(e => e.Urgency <= Urgency.SetOff);
                lines.Add((I18n.Get("hud.overflow", new { count = hidden.Count, key = this.mod.Config.OpenMenuKey }), urgentHidden ? ReadyColor : MutedColor));
            }

            this.lines = lines;
            this.builtVersion = this.mod.Index.Version;
            this.pinCount = this.PinCount;
            this.builtForHeight = Game1.uiViewport.Height;
            this.builtForY = this.mod.Config.HudY;
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
                    ? I18n.Get("hud.next-locked", new
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
            string more = awake > 1 ? I18n.Get("hud.more", new { count = awake - 1 }) : "";
            string shortHead = I18n.Get("hud.event-hidden", new { name, title = evt.Title });
            string fullHead = this.mod.HidesDetails(eval)
                ? shortHead
                : I18n.Get("hud.event", new { name, title = evt.Title, location = EventNarrator.WithArticle(evt.LocationDisplayName) });
            return this.EventEntry(evt, eval, name, fullHead, shortHead, more, isStory: false);
        }

        private Entry BuildStoryEntry(EventInfo evt, EventEvaluation eval)
        {
            string place = EventNarrator.WithArticle(evt.LocationDisplayName);
            string sortName = evt.LocationDisplayName;
            if (this.mod.IsSnoozed(evt))
                return OneLine(sortName, I18n.Get("hud.story-snoozed", new { location = place }), isStory: true);

            string head = I18n.Get("hud.story", new { location = place, title = evt.Title });
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
                string line = I18n.Get("hud.compact", new { head = shortHead + more, stage });
                return new Entry(urgency, minutes, sortName, isStory, new List<(string, Color)> { (line, MutedColor) });
            }

            var lines = new List<(string, Color)>
            {
                (fullHead + more, color),
                ($"   {stage}", tone == EventNarrator.HudTone.Normal ? MutedColor : color)
            };
            return new Entry(urgency, minutes, sortName, isStory, lines);
        }

        private static Entry OneLine(string sortName, string text, bool isStory = false) =>
            new(Urgency.Nothing, 0, sortName, isStory, new List<(string, Color)> { (text, MutedColor) });

        /// <summary>The coloured status line for an event, from <see cref="EventNarrator.HudLine"/>.</summary>
        private (string Stage, Color Color, EventNarrator.HudTone Tone) Status(EventInfo evt, EventEvaluation eval)
        {
            // spoiler-free mode doesn't say what's still needed
            if (this.mod.HidesDetails(eval))
                return (I18n.Get("status.hidden"), MutedColor, EventNarrator.HudTone.Normal);

            int? travel = eval.Status is EventStatus.AvailableNow or EventStatus.LaterToday ? this.mod.State.Travel.MinutesTo(evt.LocationName) : null;
            (string stage, EventNarrator.HudTone tone) = EventNarrator.HudLine(evt, eval, this.mod.Config.ReminderMinutesBefore, travel, this.mod.Config.TravelBufferMinutes, this.mod.Index);
            Color color = tone switch
            {
                EventNarrator.HudTone.Go => ReadyColor,
                EventNarrator.HudTone.Urgent or EventNarrator.HudTone.Soon => SoonColor,
                _ => Game1.textColor
            };
            return (stage, color, tone);
        }
    }
}
