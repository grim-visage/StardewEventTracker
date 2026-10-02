using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NpcEventTracker.Data;
using StardewValley;
using StardewValley.Menus;

namespace NpcEventTracker.UI
{
    /// <summary>The hotkey menu: pending events for pinned NPCs, every NPC, and completed events.</summary>
    internal sealed class TrackerMenu : IClickableMenu
    {
        private enum Tab { Pinned, AllNpcs, Completed }

        private sealed class Row
        {
            public string Text = "";
            public SpriteFont Font = Game1.smallFont;
            public Color Color = Game1.textColor;
            public int Indent;
            public int Height;
            public Action? OnClick;
            public string? Button;
            public Action? OnButton;
        }

        private static readonly Color MetColor = new(30, 110, 30);
        private static readonly Color UnmetColor = new(170, 30, 30);
        private static readonly Color MutedColor = new(110, 100, 90);
        private static readonly Color ReadyColor = new(20, 130, 40);

        private const int Padding = 32;
        private const int TabHeight = 56;
        private const int ButtonWidth = 110;
        private const int ScrollStep = 64;

        // remembered between openings for the rest of the session
        private static Tab lastTab = Tab.Pinned;
        private static readonly HashSet<string> Expanded = new();

        private readonly ModEntry mod;
        private readonly List<Row> rows = new();
        private readonly List<(Rectangle Area, Action Action)> hitAreas = new();
        private Rectangle[] tabAreas = Array.Empty<Rectangle>();
        private Tab tab = lastTab;
        private int builtVersion = -1;
        private int scrollY;
        private int contentHeight;

        private EventIndex Index => this.mod.Index;

        private Rectangle ContentArea => new(
            this.xPositionOnScreen + Padding,
            this.yPositionOnScreen + Padding + TabHeight + 16,
            this.width - Padding * 2 - 24,
            this.height - Padding * 2 - TabHeight - 16);

        public TrackerMenu(ModEntry mod)
        {
            this.mod = mod;
            this.Layout();
        }

        private void Layout()
        {
            int w = Math.Min(1100, Game1.uiViewport.Width - 64);
            int h = Math.Min(820, Game1.uiViewport.Height - 64);
            this.initialize((Game1.uiViewport.Width - w) / 2, (Game1.uiViewport.Height - h) / 2, w, h, showUpperRightCloseButton: true);

            int tabWidth = 220;
            this.tabAreas = Enumerable.Range(0, 3)
                .Select(i => new Rectangle(this.xPositionOnScreen + Padding + i * (tabWidth + 12), this.yPositionOnScreen + Padding, tabWidth, TabHeight))
                .ToArray();
            this.builtVersion = -1;
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            this.Layout();
        }

        /****
        ** Rows
        ****/
        private void RebuildRows()
        {
            this.rows.Clear();
            switch (this.tab)
            {
                case Tab.Pinned:
                    this.BuildPinnedTab();
                    break;
                case Tab.AllNpcs:
                    this.BuildAllNpcsTab();
                    break;
                case Tab.Completed:
                    this.BuildCompletedTab();
                    break;
            }

            this.contentHeight = this.rows.Sum(r => r.Height);
            this.scrollY = Math.Clamp(this.scrollY, 0, this.MaxScroll);
            this.builtVersion = this.Index.Version;
        }

        private int MaxScroll => Math.Max(0, this.contentHeight - this.ContentArea.Height);

        private void BuildPinnedTab()
        {
            var pinned = this.mod.PinnedNpcs.OrderBy(EventIndex.GetNpcDisplayName).ToList();
            if (pinned.Count == 0)
            {
                this.AddRow("No NPCs pinned yet.", MutedColor);
                this.AddRow("Open the 'All NPCs' tab and click Pin next to anyone you want to track.", MutedColor);
                return;
            }

            foreach (string npc in pinned)
            {
                this.AddNpcHeader(npc, expandable: false);
                this.AddPendingDetail(npc, indent: 16);
                this.AddSpacer(16);
            }
        }

        private void BuildAllNpcsTab()
        {
            var owners = this.Index.ByOwner.Keys
                .OrderBy(k => k == EventIndex.OtherKey)
                .ThenBy(k => !this.mod.PinnedNpcs.Contains(k))
                .ThenBy(EventIndex.GetNpcDisplayName)
                .ToList();

            foreach (string owner in owners)
            {
                this.AddNpcHeader(owner, expandable: true);
                if (Expanded.Contains("all:" + owner))
                {
                    this.AddPendingDetail(owner, indent: 16);
                    this.AddSpacer(12);
                }
            }
        }

        private void BuildCompletedTab()
        {
            var groups = this.Index.ByOwner
                .Select(p => (Owner: p.Key, Seen: p.Value.Where(e => e.Seen).ToList()))
                .Where(g => g.Seen.Count > 0)
                .OrderBy(g => g.Owner == EventIndex.OtherKey)
                .ThenBy(g => EventIndex.GetNpcDisplayName(g.Owner))
                .ToList();

            if (groups.Count == 0)
            {
                this.AddRow("You haven't seen any tracked events yet.", MutedColor);
                return;
            }

            foreach ((string owner, List<EventInfo> seen) in groups)
            {
                string key = "done:" + owner;
                bool expanded = Expanded.Contains(key);
                int total = this.Index.GetEvents(owner).Count;
                this.AddRow(
                    $"{(expanded ? "v" : ">")} {EventIndex.GetNpcDisplayName(owner)}   ({seen.Count}/{total} seen)",
                    Game1.textColor,
                    font: Game1.dialogueFont,
                    onClick: () => ToggleExpanded(key));

                if (!expanded)
                    continue;
                foreach (EventInfo evt in seen)
                    this.AddRow($"+ {evt.Title} at {evt.LocationDisplayName}  (#{evt.Id})", MetColor, indent: 32);
                this.AddSpacer(12);
            }
        }

        private void AddNpcHeader(string owner, bool expandable)
        {
            string name = EventIndex.GetNpcDisplayName(owner);
            string key = "all:" + owner;
            PendingEvents pending = this.Index.GetPending(owner);
            int ready = pending.Pending.Count(p => p.Eval.Status == EventStatus.Ready);
            int seen = this.Index.GetEvents(owner).Count(e => e.Seen);

            string hearts = owner != EventIndex.OtherKey && Game1.player.friendshipData.ContainsKey(owner)
                ? $"  {Game1.player.getFriendshipHeartLevelForNPC(owner)} hearts"
                : "";
            string scenes = pending.OtherScenes.Count > 0 ? $", {pending.OtherScenes.Count} other scenes" : "";
            string locked = pending.Locked > 0 ? $", {pending.Locked} locked" : "";
            string counts = $"   {pending.Pending.Count} pending{(ready > 0 ? $", {ready} READY" : "")}{locked}, {seen} seen{scenes}";
            string prefix = expandable ? (Expanded.Contains(key) ? "v " : "> ") : "";

            bool isPinned = this.mod.PinnedNpcs.Contains(owner);
            this.AddRow(
                prefix + name + hearts,
                ready > 0 ? ReadyColor : Game1.textColor,
                font: Game1.dialogueFont,
                onClick: expandable ? () => ToggleExpanded(key) : null,
                button: owner == EventIndex.OtherKey ? null : (isPinned ? "Unpin" : "Pin"),
                onButton: () => this.mod.TogglePin(owner));
            this.AddRow(counts.Trim(), MutedColor, indent: expandable ? 28 : 0);
        }

        private void AddPendingDetail(string owner, int indent)
        {
            PendingEvents pending = this.Index.GetPending(owner);

            if (pending.Pending.Count == 0 && pending.NextLocked == null)
                this.AddRow("No pending heart events. You're all caught up!", MutedColor, indent);

            foreach ((EventInfo evt, EventEvaluation eval) in pending.Pending)
                this.AddEventDetail(evt, eval, indent);

            if (pending.NextLocked is { } next)
            {
                string need = next.Event.RequiredPoints % NPC.friendshipPointsPerHeartLevel == 0
                    ? $"needs {next.Event.RequiredHearts} hearts"
                    : $"needs {next.Event.RequiredPoints} points";
                this.AddRow($"Next up: {next.Event.Title} at {next.Event.LocationDisplayName} ({need})", MutedColor, indent);
            }

            if (pending.Unreachable > 0)
                this.AddRow($"{pending.Unreachable} event(s) can no longer happen (an alternate version was seen).", MutedColor, indent);

            if (pending.OtherScenes.Count > 0)
            {
                string key = "scenes:" + owner;
                bool expanded = Expanded.Contains(key);
                this.AddSpacer(4);
                this.AddRow(
                    $"{(expanded ? "v" : ">")} Other scenes featuring {EventIndex.GetNpcDisplayName(owner)} ({pending.OtherScenes.Count})",
                    MutedColor,
                    indent,
                    onClick: () => ToggleExpanded(key));
                if (expanded)
                {
                    foreach ((EventInfo evt, EventEvaluation eval) in pending.OtherScenes)
                        this.AddEventDetail(evt, eval, indent + 16);
                }
            }
        }

        private void AddEventDetail(EventInfo evt, EventEvaluation eval, int indent)
        {
            (string tag, Color color) = eval.Status switch
            {
                EventStatus.Ready => ("READY: go there now", ReadyColor),
                EventStatus.Special => ("special trigger: can't be started by visiting", MutedColor),
                _ => ($"{eval.UnmetCount} requirement(s) not met yet", Game1.textColor)
            };
            this.AddRow($"{evt.Title} at {evt.LocationDisplayName}  [{tag}]", color, indent);

            if (evt.Conditions.Count == 0)
                this.AddRow("No requirements, just walk in.", MetColor, indent + 28);
            for (int i = 0; i < evt.Conditions.Count; i++)
            {
                string text = PreconditionFormatter.Describe(evt.Conditions[i], this.Index);
                (string mark, Color condColor) = eval.States[i] switch
                {
                    ConditionState.Met => ("+", MetColor),
                    ConditionState.Unmet => ("x", UnmetColor),
                    ConditionState.Unknown => ("?", MutedColor),
                    _ => ("-", MutedColor)
                };
                this.AddRow($"{mark} {text}", condColor, indent + 28);
            }
            this.AddRow($"#{evt.Id}", MutedColor * 0.7f, indent + 28);
            this.AddSpacer(8);
        }

        private void AddRow(string text, Color color, int indent = 0, SpriteFont? font = null, Action? onClick = null, string? button = null, Action? onButton = null)
        {
            font ??= Game1.smallFont;
            int maxWidth = this.ContentArea.Width - indent - (button != null ? ButtonWidth + 16 : 0);
            string wrapped = Game1.parseText(text, font, Math.Max(100, maxWidth));
            int height = (int)Math.Ceiling(font.MeasureString(wrapped).Y) + 4;
            if (button != null)
                height = Math.Max(height, 52);

            this.rows.Add(new Row
            {
                Text = wrapped,
                Font = font,
                Color = color,
                Indent = indent,
                Height = height,
                OnClick = onClick,
                Button = button,
                OnButton = onButton
            });
        }

        private void AddSpacer(int height) => this.rows.Add(new Row { Height = height });

        private static void ToggleExpanded(string key)
        {
            if (!Expanded.Remove(key))
                Expanded.Add(key);
        }

        /****
        ** Input
        ****/
        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            for (int i = 0; i < this.tabAreas.Length; i++)
            {
                if (this.tabAreas[i].Contains(x, y) && this.tab != (Tab)i)
                {
                    this.tab = lastTab = (Tab)i;
                    this.scrollY = 0;
                    this.builtVersion = -1;
                    Game1.playSound("smallSelect");
                    return;
                }
            }

            // buttons are registered after their row, so check in reverse to give them priority
            for (int i = this.hitAreas.Count - 1; i >= 0; i--)
            {
                if (this.hitAreas[i].Area.Contains(x, y))
                {
                    this.hitAreas[i].Action();
                    this.builtVersion = -1;
                    Game1.playSound("smallSelect");
                    return;
                }
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            this.Scroll(direction > 0 ? -ScrollStep : ScrollStep);
        }

        public override void receiveKeyPress(Keys key)
        {
            switch (key)
            {
                case Keys.Up:
                    this.Scroll(-ScrollStep);
                    return;
                case Keys.Down:
                    this.Scroll(ScrollStep);
                    return;
                case Keys.PageUp:
                    this.Scroll(-this.ContentArea.Height);
                    return;
                case Keys.PageDown:
                    this.Scroll(this.ContentArea.Height);
                    return;
            }
            base.receiveKeyPress(key);
        }

        public override void receiveGamePadButton(Buttons b)
        {
            switch (b)
            {
                case Buttons.DPadUp or Buttons.LeftThumbstickUp or Buttons.RightThumbstickUp:
                    this.Scroll(-ScrollStep);
                    return;
                case Buttons.DPadDown or Buttons.LeftThumbstickDown or Buttons.RightThumbstickDown:
                    this.Scroll(ScrollStep);
                    return;
                case Buttons.LeftShoulder:
                    this.SwitchTab(-1);
                    return;
                case Buttons.RightShoulder:
                    this.SwitchTab(1);
                    return;
            }
            base.receiveGamePadButton(b);
        }

        private void SwitchTab(int delta)
        {
            this.tab = lastTab = (Tab)(((int)this.tab + delta + 3) % 3);
            this.scrollY = 0;
            this.builtVersion = -1;
            Game1.playSound("smallSelect");
        }

        private void Scroll(int amount)
        {
            int old = this.scrollY;
            this.scrollY = Math.Clamp(this.scrollY + amount, 0, this.MaxScroll);
            if (old != this.scrollY)
                Game1.playSound("shiny4");
        }

        /****
        ** Draw
        ****/
        public override void draw(SpriteBatch b)
        {
            if (this.builtVersion != this.Index.Version)
                this.RebuildRows();

            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            // tabs
            string[] tabLabels = { "Pinned", "All NPCs", "Completed" };
            for (int i = 0; i < this.tabAreas.Length; i++)
            {
                Rectangle area = this.tabAreas[i];
                bool selected = (int)this.tab == i;
                bool hovered = area.Contains(Game1.getMouseX(), Game1.getMouseY());
                DrawBox(b, area, selected ? Color.White : hovered ? Color.Wheat : Color.White * 0.55f);
                Vector2 size = Game1.smallFont.MeasureString(tabLabels[i]);
                Utility.drawTextWithShadow(b, tabLabels[i], Game1.smallFont, new Vector2(area.Center.X - size.X / 2, area.Center.Y - size.Y / 2), Game1.textColor);
            }

            // rows
            this.hitAreas.Clear();
            Rectangle content = this.ContentArea;
            int y = content.Y - this.scrollY;
            foreach (Row row in this.rows)
            {
                if (y >= content.Y && y + row.Height <= content.Bottom && row.Text.Length > 0)
                {
                    var rowArea = new Rectangle(content.X + row.Indent, y, content.Width - row.Indent, row.Height);
                    if (row.OnClick != null)
                    {
                        if (rowArea.Contains(Game1.getMouseX(), Game1.getMouseY()))
                            b.Draw(Game1.staminaRect, rowArea, Color.Wheat * 0.35f);
                        this.hitAreas.Add((rowArea, row.OnClick));
                    }

                    int textY = y + (row.Button != null ? (row.Height - (int)row.Font.MeasureString(row.Text).Y) / 2 : 0);
                    Utility.drawTextWithShadow(b, row.Text, row.Font, new Vector2(content.X + row.Indent, textY), row.Color, shadowIntensity: 0.25f);

                    if (row.Button != null && row.OnButton != null)
                    {
                        var buttonArea = new Rectangle(content.Right - ButtonWidth, y + (row.Height - 48) / 2, ButtonWidth, 48);
                        bool hovered = buttonArea.Contains(Game1.getMouseX(), Game1.getMouseY());
                        DrawBox(b, buttonArea, hovered ? Color.Wheat : Color.White);
                        Vector2 size = Game1.smallFont.MeasureString(row.Button);
                        Utility.drawTextWithShadow(b, row.Button, Game1.smallFont, new Vector2(buttonArea.Center.X - size.X / 2, buttonArea.Center.Y - size.Y / 2), Game1.textColor);
                        this.hitAreas.Add((buttonArea, row.OnButton));
                    }
                }
                y += row.Height;
            }

            // scrollbar
            if (this.MaxScroll > 0)
            {
                var track = new Rectangle(content.Right + 8, content.Y, 8, content.Height);
                b.Draw(Game1.staminaRect, track, Color.Black * 0.15f);
                int thumbHeight = Math.Max(40, track.Height * track.Height / this.contentHeight);
                int thumbY = track.Y + (track.Height - thumbHeight) * this.scrollY / this.MaxScroll;
                b.Draw(Game1.staminaRect, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Game1.textColor * 0.6f);
            }

            base.draw(b);
            this.drawMouse(b);
        }

        private static void DrawBox(SpriteBatch b, Rectangle area, Color color)
        {
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), area.X, area.Y, area.Width, area.Height, color, 4f, drawShadow: false);
        }
    }
}
