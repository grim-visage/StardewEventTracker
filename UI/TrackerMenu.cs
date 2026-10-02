using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NpcEventTracker.Data;
using StardewValley;
using StardewModdingAPI.Utilities;
using StardewValley.Menus;

namespace NpcEventTracker.UI
{
    /// <summary>The hotkey menu: pinned NPCs, heart events by NPC, story events by location, and completed events.</summary>
    internal sealed class TrackerMenu : IClickableMenu
    {
        private enum Tab { Pinned, Hearts, Story, Completed }

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

        /// <summary>A toggle button in the toolbar next to the search box.</summary>
        private sealed class Chip
        {
            public Rectangle Area;
            public string Label = "";
            public string Tooltip = "";
            public Func<bool> IsOn = () => false;
            public Action Toggle = () => { };
        }

        private static readonly Color MetColor = new(30, 110, 30);
        private static readonly Color UnmetColor = new(170, 30, 30);
        private static readonly Color MutedColor = new(110, 100, 90);
        private static readonly Color ReadyColor = new(20, 130, 40);
        private static readonly Color SoonColor = new(185, 105, 0);

        private const int Padding = 32;
        private const int TabHeight = 56;
        private const int ChipHeight = 48;
        private const int ButtonWidth = 130;
        private const int ScrollStep = 64;

        private static readonly string[] TabLabels = { "Pinned", "Hearts", "Story", "Completed" };

        // remembered between openings for the rest of the session, separately for each split-screen player
        private static readonly PerScreen<Tab> LastTab = new(() => Tab.Pinned);
        private static readonly PerScreen<HashSet<string>> ExpandedPerScreen = new(() => new HashSet<string>());
        private static HashSet<string> Expanded => ExpandedPerScreen.Value;

        private readonly ModEntry mod;
        private readonly List<Row> rows = new();
        private readonly List<(Rectangle Area, Action Action)> hitAreas = new();
        private readonly List<Chip> chips = new();
        private readonly TextBox searchBox;
        private Rectangle[] tabAreas = Array.Empty<Rectangle>();
        private Tab tab = LastTab.Value;
        private int builtVersion = -1;
        private int scrollY;
        private int contentHeight;
        private int contentTop;
        private string hoverText = "";

        private EventIndex Index => this.mod.Index;

        private Rectangle ContentArea => new(
            this.xPositionOnScreen + Padding,
            this.contentTop,
            this.width - Padding * 2 - 24,
            this.yPositionOnScreen + this.height - Padding - this.contentTop);

        public TrackerMenu(ModEntry mod)
        {
            this.mod = mod;
            this.searchBox = new TextBox(Game1.content.Load<Texture2D>("LooseSprites\\textBox"), null, Game1.smallFont, Game1.textColor)
            {
                Text = EventFilter.SearchText,
                textLimit = 40
            };
            this.Layout();
        }

        /****
        ** Layout
        ****/
        private void Layout()
        {
            int w = Math.Min(1100, Game1.uiViewport.Width - 64);
            int h = Math.Min(820, Game1.uiViewport.Height - 64);
            this.initialize((Game1.uiViewport.Width - w) / 2, (Game1.uiViewport.Height - h) / 2, w, h, showUpperRightCloseButton: true);

            const int gap = 12;
            int inner = this.width - Padding * 2;
            int tabWidth = (inner - gap * (TabLabels.Length - 1)) / TabLabels.Length;
            this.tabAreas = Enumerable.Range(0, TabLabels.Length)
                .Select(i => new Rectangle(this.xPositionOnScreen + Padding + i * (tabWidth + gap), this.yPositionOnScreen + Padding, tabWidth, TabHeight))
                .ToArray();

            this.LayoutToolbar();
        }

        /// <summary>Places the search box and this tab's chips, wrapping chips onto another line if needed.</summary>
        private void LayoutToolbar()
        {
            this.chips.Clear();
            if (this.tab == Tab.Completed)
            {
                this.chips.Add(new Chip { Label = "Hearts", Tooltip = "Show heart events you've seen.", IsOn = () => !EventFilter.CompletedShowsStory, Toggle = () => EventFilter.CompletedShowsStory = false });
                this.chips.Add(new Chip { Label = "Story", Tooltip = "Show story events you've seen.", IsOn = () => EventFilter.CompletedShowsStory, Toggle = () => EventFilter.CompletedShowsStory = true });
            }
            else
            {
                this.chips.Add(new Chip { Label = "Available now", Tooltip = "Every requirement is met and it's the right time of day.", IsOn = () => EventFilter.AvailableNow, Toggle = () => EventFilter.AvailableNow = !EventFilter.AvailableNow });
                this.chips.Add(new Chip { Label = "Today", Tooltip = "Available now, or everything is met\nand it can start later today.", IsOn = () => EventFilter.AvailableToday, Toggle = () => EventFilter.AvailableToday = !EventFilter.AvailableToday });
                this.chips.Add(new Chip { Label = "Right day", Tooltip = "Waiting on the right day: only the weather, day,\nseason, Green Rain or a festival is in the way today.", IsOn = () => EventFilter.WaitingOnDay, Toggle = () => EventFilter.WaitingOnDay = !EventFilter.WaitingOnDay });
                this.chips.Add(new Chip { Label = "Show locked", Tooltip = "Also list events that need more hearts.", IsOn = () => EventFilter.ShowLocked, Toggle = () => EventFilter.ShowLocked = !EventFilter.ShowLocked });
            }

            int left = this.xPositionOnScreen + Padding;
            int right = this.xPositionOnScreen + this.width - Padding;
            int top = this.yPositionOnScreen + Padding + TabHeight + 12;
            int lineHeight = Math.Max(this.searchBox.Height, ChipHeight);

            this.searchBox.X = left;
            this.searchBox.Y = top;
            this.searchBox.Width = Math.Min(320, (right - left) / 3);

            int chipLeft = left + this.searchBox.Width + 16;
            int x = chipLeft, y = top;
            foreach (Chip chip in this.chips)
            {
                int chipWidth = (int)Game1.smallFont.MeasureString(chip.Label).X + 40;
                if (x + chipWidth > right && x > chipLeft)
                {
                    x = chipLeft;
                    y += lineHeight + 8;
                }
                chip.Area = new Rectangle(x, y + (lineHeight - ChipHeight) / 2, chipWidth, ChipHeight);
                x += chipWidth + 10;
            }

            this.contentTop = y + lineHeight + 16;
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
                case Tab.Hearts:
                    this.BuildHeartsTab();
                    break;
                case Tab.Story:
                    this.BuildStoryTab();
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
            this.AddTodaysMessages();

            var pinned = this.mod.PinnedNpcs.OrderBy(EventIndex.GetNpcDisplayName).ToList();
            if (pinned.Count == 0)
            {
                this.AddRow("No NPCs pinned yet.", MutedColor);
                this.AddRow("Open the Hearts tab and click Pin next to anyone you want to track.", MutedColor);
                return;
            }

            bool any = false;
            foreach (string npc in pinned)
            {
                PendingEvents pending = this.Index.GetPending(npc);
                bool nameMatch = EventFilter.HasSearch && EventFilter.MatchesText(EventIndex.GetNpcDisplayName(npc));
                var visible = this.Visible(pending, nameMatch);
                if (!ShowGroup(visible, nameMatch))
                    continue;

                any = true;
                this.AddNpcHeader(npc, pending, expandable: false);
                this.AddEventList(pending, visible, indent: 16, showLocation: true);
                this.AddSpacer(16);
            }

            if (!any)
                this.AddRow("None of your pinned NPCs have events matching your search or filters.", MutedColor);
        }

        /// <summary>A collapsible list of today's reminder pop-ups, newest first.</summary>
        private void AddTodaysMessages()
        {
            if (EventFilter.HasStatusFilter)
                return;

            var messages = this.mod.MessagesToday
                .Where(m => EventFilter.MatchesText(m.Text))
                .Reverse()
                .ToList();
            if (messages.Count == 0)
                return;

            const string key = "messages";
            bool expanded = EventFilter.HasSearch || Expanded.Contains(key);
            this.AddRow(
                $"{(expanded ? "v" : ">")} Today's messages ({messages.Count})",
                Game1.textColor,
                onClick: EventFilter.HasSearch ? null : () => ToggleExpanded(key));
            if (expanded)
            {
                foreach ((int time, string text) in messages)
                    this.AddRow($"{PreconditionFormatter.Time(time)}  {text}", MutedColor, indent: 28);
            }
            this.AddSpacer(16);
        }

        private void BuildHeartsTab()
        {
            var owners = this.Index.ByOwner.Keys
                .OrderBy(k => !this.mod.PinnedNpcs.Contains(k))
                .ThenBy(EventIndex.GetNpcDisplayName)
                .ToList();

            bool any = false;
            foreach (string owner in owners)
            {
                PendingEvents pending = this.Index.GetPending(owner);
                bool nameMatch = EventFilter.HasSearch && EventFilter.MatchesText(EventIndex.GetNpcDisplayName(owner));
                var visible = this.Visible(pending, nameMatch);
                if (!ShowGroup(visible, nameMatch))
                    continue;

                any = true;
                string key = "hearts:" + owner;
                bool expanded = EventFilter.IsActive || Expanded.Contains(key);
                this.AddNpcHeader(owner, pending, expandable: true, expanded, key);
                if (expanded)
                {
                    this.AddEventList(pending, visible, indent: 16, showLocation: true);
                    this.AddSpacer(12);
                }
            }

            if (!any)
                this.AddRow("No heart events match your search or filters.", MutedColor);
        }

        private void BuildStoryTab()
        {
            var locations = this.Index.StoryByLocation.Keys
                .Select(loc => (Location: loc, Name: this.Index.GetLocationName(loc), Pending: this.Index.GetStoryPending(loc)))
                .Where(g => g.Pending.Pending.Count > 0)
                .OrderBy(g => g.Pending.Pending[0].Eval.Status)
                .ThenBy(g => g.Name)
                .ToList();

            bool any = false;
            foreach ((string location, string name, PendingEvents pending) in locations)
            {
                bool nameMatch = EventFilter.HasSearch && EventFilter.MatchesText(name);
                var visible = this.Visible(pending, nameMatch);
                if (!ShowGroup(visible, nameMatch))
                    continue;

                any = true;
                string key = "story:" + location;
                bool expanded = EventFilter.IsActive || Expanded.Contains(key);
                int now = pending.Count(EventStatus.AvailableNow);
                int later = pending.Count(EventStatus.LaterToday);

                this.AddRow(
                    $"{(expanded ? "v" : ">")} {name}",
                    now > 0 ? ReadyColor : later > 0 ? SoonColor : Game1.textColor,
                    font: Game1.dialogueFont,
                    onClick: EventFilter.IsActive ? null : () => ToggleExpanded(key));
                this.AddRow(
                    $"{pending.Pending.Count} unseen{(now > 0 ? $", {now} available now" : "")}{(later > 0 ? $", {later} later today" : "")}",
                    MutedColor,
                    indent: 28);

                if (expanded)
                {
                    this.AddEventList(pending, visible, indent: 16, showLocation: false);
                    this.AddSpacer(12);
                }
            }

            if (!any)
                this.AddRow(EventFilter.IsActive ? "No story events match your search or filters." : "No unseen story events right now.", MutedColor);
        }

        private void BuildCompletedTab()
        {
            bool story = EventFilter.CompletedShowsStory;
            var groups = (story
                    ? this.Index.StoryByLocation.Select(p => (Key: p.Key, Name: this.Index.GetLocationName(p.Key), Events: p.Value))
                    : this.Index.ByOwner.Select(p => (Key: p.Key, Name: EventIndex.GetNpcDisplayName(p.Key), Events: p.Value)))
                .Select(g => (g.Key, g.Name, Seen: g.Events.Where(e => e.Seen).ToList(), Total: g.Events.Count))
                .Where(g => g.Seen.Count > 0)
                .OrderBy(g => g.Name)
                .ToList();

            bool any = false;
            foreach ((string groupKey, string name, List<EventInfo> seen, int total) in groups)
            {
                bool nameMatch = EventFilter.HasSearch && EventFilter.MatchesText(name);
                var visible = nameMatch ? seen : seen.Where(e => EventFilter.MatchesSearch(e, this.Index)).ToList();
                if (visible.Count == 0)
                    continue;

                any = true;
                string key = (story ? "done-story:" : "done:") + groupKey;
                bool expanded = EventFilter.HasSearch || Expanded.Contains(key);
                this.AddRow(
                    $"{(expanded ? "v" : ">")} {name}   ({seen.Count}/{total} seen)",
                    Game1.textColor,
                    font: Game1.dialogueFont,
                    onClick: EventFilter.HasSearch ? null : () => ToggleExpanded(key));

                if (!expanded)
                    continue;
                foreach (EventInfo evt in visible)
                {
                    string text = story
                        ? $"+ {evt.Title}{(evt.Actors.Count > 0 ? $" with {ActorList(evt)}" : "")}  (#{evt.Id})"
                        : $"+ {evt.Title} at {evt.LocationDisplayName}  (#{evt.Id})";
                    this.AddRow(text, MetColor, indent: 32);
                }
                this.AddSpacer(12);
            }

            if (!any)
            {
                this.AddRow(EventFilter.HasSearch
                    ? "No completed events match your search."
                    : $"You haven't seen any tracked {(story ? "story" : "heart")} events yet.", MutedColor);
            }
        }

        /// <summary>The events in a group that pass the current filters and search.</summary>
        private List<(EventInfo Event, EventEvaluation Eval)> Visible(PendingEvents pending, bool groupNameMatches)
        {
            return pending.Pending
                .Concat(EventFilter.ShowLocked ? pending.LockedEvents : Enumerable.Empty<(EventInfo Event, EventEvaluation Eval)>())
                // spoiler-free mode doesn't reveal story events before they're unlocked
                .Where(p => !(p.Event.IsStory && this.mod.HidesDetails(p.Eval)))
                .Where(p => EventFilter.MatchesStatus(p.Eval.Status) && (groupNameMatches || EventFilter.MatchesSearch(p.Event, this.Index, this.mod.HidesDetails(p.Eval))))
                .ToList();
        }

        /// <summary>Groups always show unfiltered; when filtering, only if something matches (or the search names the group).</summary>
        private static bool ShowGroup(List<(EventInfo Event, EventEvaluation Eval)> visible, bool groupNameMatches) =>
            !EventFilter.IsActive || visible.Count > 0 || (groupNameMatches && !EventFilter.HasStatusFilter);

        private void AddNpcHeader(string owner, PendingEvents pending, bool expandable, bool expanded = false, string? key = null)
        {
            string name = EventIndex.GetNpcDisplayName(owner);
            int now = pending.Count(EventStatus.AvailableNow);
            int later = pending.Count(EventStatus.LaterToday);
            int seen = this.Index.GetEvents(owner).Count(e => e.Seen);

            string hearts = Game1.player.friendshipData.ContainsKey(owner)
                ? $"  {Game1.player.getFriendshipHeartLevelForNPC(owner)} hearts"
                : "";
            string counts = $"{pending.Pending.Count} pending"
                + (now > 0 ? $", {now} available now" : "")
                + (later > 0 ? $", {later} later today" : "")
                + (pending.LockedEvents.Count > 0 ? $", {pending.LockedEvents.Count} locked" : "")
                + $", {seen} seen";
            string prefix = expandable ? (expanded ? "v " : "> ") : "";

            bool isPinned = this.mod.PinnedNpcs.Contains(owner);
            this.AddRow(
                prefix + name + hearts,
                now > 0 ? ReadyColor : later > 0 ? SoonColor : Game1.textColor,
                font: Game1.dialogueFont,
                onClick: expandable && key != null && !EventFilter.IsActive ? () => ToggleExpanded(key) : null,
                button: isPinned ? "Unpin" : "Pin",
                onButton: () => this.mod.TogglePin(owner));
            this.AddRow(counts, MutedColor, indent: expandable ? 28 : 0);
        }

        private void AddEventList(PendingEvents pending, List<(EventInfo Event, EventEvaluation Eval)> visible, int indent, bool showLocation)
        {
            if (visible.Count == 0 && !EventFilter.IsActive)
                this.AddRow(pending.NextLocked == null ? "Nothing pending. You're all caught up!" : "Nothing unlocked yet.", MutedColor, indent);

            foreach ((EventInfo evt, EventEvaluation eval) in visible)
                this.AddEventDetail(evt, eval, indent, showLocation);

            if (EventFilter.IsActive)
                return;

            if (pending.NextLocked is { } next)
            {
                int current = Game1.player.getFriendshipHeartLevelForNPC(next.Event.Owner);
                string need = next.Event.RequiredPoints % NPC.friendshipPointsPerHeartLevel == 0
                    ? $"needs {next.Event.RequiredHearts} hearts, you have {current}"
                    : $"needs {next.Event.RequiredPoints} points";
                string where = this.mod.Config.SpoilerFree ? "" : $" at {next.Event.LocationDisplayName}";
                this.AddRow($"Next up: {next.Event.Title}{where} ({need})", MutedColor, indent);
            }

            if (pending.Unreachable > 0)
                this.AddRow($"{pending.Unreachable} event(s) can no longer happen (an alternate version was seen).", MutedColor, indent);
        }

        private void AddEventDetail(EventInfo evt, EventEvaluation eval, int indent, bool showLocation)
        {
            bool hidden = this.mod.HidesDetails(eval);
            string title = showLocation && !hidden ? $"{evt.Title} at {evt.LocationDisplayName}" : evt.Title;
            bool snoozed = this.mod.IsSnoozed(evt);
            bool canSnooze = snoozed || (evt.IsHeartEvent && this.mod.PinnedNpcs.Contains(evt.Owner) && eval.Status is not (EventStatus.Locked or EventStatus.Special));
            this.AddRow(
                $"{title}  [{EventNarrator.StatusTag(evt, eval, this.Index)}]{(snoozed ? "  (snoozed until tomorrow)" : "")}",
                snoozed ? MutedColor : StatusColor(eval.Status),
                indent,
                button: canSnooze ? (snoozed ? "Wake" : "Snooze") : null,
                onButton: () => this.mod.ToggleSnooze(evt));

            int inner = indent + 28;
            if (hidden)
            {
                this.AddRow("Details hidden until it's unlocked (spoiler-free mode).", MutedColor, inner);
                this.AddSpacer(8);
                return;
            }

            if (evt.IsStory && evt.Actors.Count > 0)
                this.AddRow($"With {ActorList(evt)}", MutedColor, inner);

            if (eval.Status == EventStatus.AvailableNow)
                this.AddRow(EventNarrator.AvailableNow(evt), ReadyColor, inner);
            else if (eval.Status == EventStatus.LaterToday && eval.MinutesUntilStart is { } minutes)
                this.AddRow(EventNarrator.Reminder(evt, eval, minutes), SoonColor, inner);

            if (eval.Festival is { } festival)
                this.AddRow($"Festival here today, {PreconditionFormatter.Time(festival.Start)}-{PreconditionFormatter.Time(festival.End)}. Events here can't start during it.", MutedColor, inner);

            if (evt.Conditions.Count == 0)
                this.AddRow("No requirements, just walk in.", MetColor, inner);
            for (int i = 0; i < evt.Conditions.Count; i++)
            {
                if (eval.States[i] == ConditionState.Soft)
                {
                    Color timeColor = eval.TimeOpen ? MetColor : eval.MinutesUntilStart > 0 ? SoonColor : MutedColor;
                    this.AddRow($"~ {PreconditionFormatter.DescribeTimeWindow(evt, eval)}", timeColor, inner);
                    continue;
                }

                string text = PreconditionFormatter.Describe(evt.Conditions[i], this.Index);
                (string mark, Color color) = eval.States[i] switch
                {
                    ConditionState.Met => ("+", MetColor),
                    ConditionState.Unmet => ("x", UnmetColor),
                    ConditionState.Unknown => ("?", MutedColor),
                    _ => ("-", MutedColor)
                };
                this.AddRow($"{mark} {text}", color, inner);
            }
            // what seeing this event leads to
            var unlocks = this.mod.Config.SpoilerFree
                ? new List<EventInfo>()
                : this.Index.GetUnlocks(evt.Id).Where(u => !u.Seen).ToList();
            if (unlocks.Count > 0)
            {
                string next = string.Join("; ", unlocks.Take(2).Select(u => this.Index.DescribeEvent(u.Id)));
                this.AddRow($"> Leads to: {next}{(unlocks.Count > 2 ? $" (+{unlocks.Count - 2} more)" : "")}", MutedColor, inner);
            }

            this.AddRow($"#{evt.Id}", MutedColor * 0.7f, inner);
            this.AddSpacer(8);
        }

        private static Color StatusColor(EventStatus status) => status switch
        {
            EventStatus.AvailableNow => ReadyColor,
            EventStatus.LaterToday => SoonColor,
            EventStatus.Special or EventStatus.Locked or EventStatus.Unreachable => MutedColor,
            _ => Game1.textColor
        };

        private static string ActorList(EventInfo evt)
        {
            var names = evt.Actors.Take(3).Select(EventIndex.GetNpcDisplayName).ToList();
            return string.Join(", ", names) + (evt.Actors.Count > 3 ? $" +{evt.Actors.Count - 3}" : "");
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
        public override void update(GameTime time)
        {
            base.update(time);

            if (this.searchBox.Text != EventFilter.SearchText)
            {
                EventFilter.SearchText = this.searchBox.Text;
                this.scrollY = 0;
                this.builtVersion = -1;
            }
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            bool inSearch = new Rectangle(this.searchBox.X, this.searchBox.Y, this.searchBox.Width, this.searchBox.Height).Contains(x, y);
            this.searchBox.Selected = inSearch;
            if (inSearch)
            {
                Game1.keyboardDispatcher.Subscriber = this.searchBox;
                return;
            }

            for (int i = 0; i < this.tabAreas.Length; i++)
            {
                if (this.tabAreas[i].Contains(x, y) && this.tab != (Tab)i)
                {
                    this.SetTab((Tab)i);
                    return;
                }
            }

            foreach (Chip chip in this.chips)
            {
                if (chip.Area.Contains(x, y))
                {
                    chip.Toggle();
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

        public override void performHoverAction(int x, int y)
        {
            base.performHoverAction(x, y);
            this.hoverText = this.chips.FirstOrDefault(c => c.Area.Contains(x, y))?.Tooltip ?? "";
        }

        public override void receiveScrollWheelAction(int direction)
        {
            this.Scroll(direction > 0 ? -ScrollStep : ScrollStep);
        }

        public override void receiveKeyPress(Keys key)
        {
            // while typing, keys belong to the search box (so 'E' doesn't close the menu)
            if (this.searchBox.Selected)
            {
                if (key is Keys.Escape or Keys.Enter)
                    this.searchBox.Selected = false;
                return;
            }

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
                    this.SetTab((Tab)(((int)this.tab + TabLabels.Length - 1) % TabLabels.Length));
                    return;
                case Buttons.RightShoulder:
                    this.SetTab((Tab)(((int)this.tab + 1) % TabLabels.Length));
                    return;
            }
            base.receiveGamePadButton(b);
        }

        protected override void cleanupBeforeExit()
        {
            this.searchBox.Selected = false;
            base.cleanupBeforeExit();
        }

        private void SetTab(Tab newTab)
        {
            this.tab = LastTab.Value = newTab;
            this.scrollY = 0;
            this.LayoutToolbar();
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

            int mouseX = Game1.getMouseX(), mouseY = Game1.getMouseY();

            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height), Color.Black * 0.5f);
            drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White);

            // tabs
            for (int i = 0; i < this.tabAreas.Length; i++)
            {
                Rectangle area = this.tabAreas[i];
                bool selected = (int)this.tab == i;
                DrawBox(b, area, selected ? Color.White : area.Contains(mouseX, mouseY) ? Color.Wheat : Color.White * 0.55f);
                DrawCentered(b, TabLabels[i], area, Game1.textColor);
            }

            // toolbar
            this.searchBox.Draw(b);
            if (this.searchBox.Text.Length == 0 && !this.searchBox.Selected)
                Utility.drawTextWithShadow(b, "Search...", Game1.smallFont, new Vector2(this.searchBox.X + 16, this.searchBox.Y + 10), MutedColor, shadowIntensity: 0f);
            foreach (Chip chip in this.chips)
            {
                bool on = chip.IsOn();
                DrawBox(b, chip.Area, on ? Color.White : chip.Area.Contains(mouseX, mouseY) ? Color.Wheat : Color.White * 0.55f);
                DrawCentered(b, chip.Label, chip.Area, on ? ReadyColor : MutedColor);
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
                        if (rowArea.Contains(mouseX, mouseY))
                            b.Draw(Game1.staminaRect, rowArea, Color.Wheat * 0.35f);
                        this.hitAreas.Add((rowArea, row.OnClick));
                    }

                    int textY = y + (row.Button != null ? (row.Height - (int)row.Font.MeasureString(row.Text).Y) / 2 : 0);
                    Utility.drawTextWithShadow(b, row.Text, row.Font, new Vector2(content.X + row.Indent, textY), row.Color, shadowIntensity: 0.25f);

                    if (row.Button != null && row.OnButton != null)
                    {
                        var buttonArea = new Rectangle(content.Right - ButtonWidth, y + (row.Height - 48) / 2, ButtonWidth, 48);
                        DrawBox(b, buttonArea, buttonArea.Contains(mouseX, mouseY) ? Color.Wheat : Color.White);
                        DrawCentered(b, row.Button, buttonArea, Game1.textColor);
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
            if (this.hoverText.Length > 0)
                drawHoverText(b, this.hoverText, Game1.smallFont);
            this.drawMouse(b);
        }

        private static void DrawBox(SpriteBatch b, Rectangle area, Color color)
        {
            drawTextureBox(b, Game1.mouseCursors, new Rectangle(384, 373, 18, 18), area.X, area.Y, area.Width, area.Height, color, 4f, drawShadow: false);
        }

        private static void DrawCentered(SpriteBatch b, string text, Rectangle area, Color color)
        {
            Vector2 size = Game1.smallFont.MeasureString(text);
            Utility.drawTextWithShadow(b, text, Game1.smallFont, new Vector2(area.Center.X - size.X / 2, area.Center.Y - size.Y / 2), color);
        }
    }
}
