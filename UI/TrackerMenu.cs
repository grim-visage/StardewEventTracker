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

            /// <summary>A second button, drawn to the left of <see cref="Button"/>.</summary>
            public string? Button2;
            public Action? OnButton2;
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

        private static string[] TabLabels => new[] { I18n.Get("menu.tab.pinned"), I18n.Get("menu.tab.hearts"), I18n.Get("menu.tab.story"), I18n.Get("menu.tab.completed") };

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
                this.chips.Add(new Chip { Label = I18n.Get("menu.chip.hearts"), Tooltip = I18n.Get("menu.chip.hearts.tip"), IsOn = () => !EventFilter.CompletedShowsStory, Toggle = () => EventFilter.CompletedShowsStory = false });
                this.chips.Add(new Chip { Label = I18n.Get("menu.chip.story"), Tooltip = I18n.Get("menu.chip.story.tip"), IsOn = () => EventFilter.CompletedShowsStory, Toggle = () => EventFilter.CompletedShowsStory = true });
            }
            else
            {
                this.chips.Add(new Chip { Label = I18n.Get("menu.chip.now"), Tooltip = I18n.Get("menu.chip.now.tip"), IsOn = () => EventFilter.AvailableNow, Toggle = () => EventFilter.AvailableNow = !EventFilter.AvailableNow });
                this.chips.Add(new Chip { Label = I18n.Get("menu.chip.today"), Tooltip = I18n.Get("menu.chip.today.tip"), IsOn = () => EventFilter.AvailableToday, Toggle = () => EventFilter.AvailableToday = !EventFilter.AvailableToday });
                this.chips.Add(new Chip { Label = I18n.Get("menu.chip.not-today"), Tooltip = I18n.Get("menu.chip.not-today.tip"), IsOn = () => EventFilter.WaitingOnDay, Toggle = () => EventFilter.WaitingOnDay = !EventFilter.WaitingOnDay });
                this.chips.Add(new Chip { Label = I18n.Get("menu.chip.locked"), Tooltip = I18n.Get("menu.chip.locked.tip"), IsOn = () => EventFilter.ShowLocked, Toggle = () => EventFilter.ShowLocked = !EventFilter.ShowLocked });
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
            var pinnedStory = this.mod.GetPinnedStoryEvents();
            if (pinned.Count == 0 && pinnedStory.Count == 0)
            {
                this.AddRow(I18n.Get("menu.pinned.none"), MutedColor);
                this.AddRow(I18n.Get("menu.pinned.how", new { key = this.mod.Config.PinKey }), MutedColor);
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

            var storyVisible = pinnedStory
                .Where(p => EventFilter.MatchesStatus(p.Eval.Status) && EventFilter.MatchesSearch(p.Event, this.Index, this.mod.HidesDetails(p.Eval)))
                .ToList();
            if (storyVisible.Count > 0)
            {
                any = true;
                this.AddRow(I18n.Get("menu.pinned.story"), Game1.textColor, font: Game1.dialogueFont);
                foreach ((EventInfo evt, EventEvaluation eval) in storyVisible)
                    this.AddEventDetail(evt, eval, indent: 16, showLocation: true);
            }

            if (!any)
                this.AddRow(I18n.Get("menu.pinned.no-match"), MutedColor);
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
                $"{(expanded ? "v" : ">")} {I18n.Get("menu.messages", new { count = messages.Count })}",
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
                this.AddRow(I18n.Get("menu.hearts.no-match"), MutedColor);
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
                    I18n.Get("menu.count.unseen", new { count = pending.Pending.Count })
                        + (now > 0 ? I18n.Get("menu.count.now", new { count = now }) : "")
                        + (later > 0 ? I18n.Get("menu.count.later", new { count = later }) : ""),
                    MutedColor,
                    indent: 28);

                if (expanded)
                {
                    this.AddEventList(pending, visible, indent: 16, showLocation: false);
                    this.AddSpacer(12);
                }
            }

            if (!any)
                this.AddRow(I18n.Get(EventFilter.IsActive ? "menu.story.no-match" : "menu.story.none"), MutedColor);
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
                    $"{(expanded ? "v" : ">")} {name}   {I18n.Get("menu.completed.count", new { seen = seen.Count, total })}",
                    Game1.textColor,
                    font: Game1.dialogueFont,
                    onClick: EventFilter.HasSearch ? null : () => ToggleExpanded(key));

                if (!expanded)
                    continue;
                foreach (EventInfo evt in visible)
                {
                    string text = story
                        ? "+ " + (evt.Actors.Count > 0 ? I18n.Get("menu.completed.story-with", new { title = evt.Title, actors = ActorList(evt) }) : evt.Title) + $"  (#{evt.Id})"
                        : "+ " + I18n.Get("menu.event-at", new { title = evt.Title, location = evt.LocationDisplayName }) + $"  (#{evt.Id})";
                    this.AddRow(text, MetColor, indent: 32);
                }
                this.AddSpacer(12);
            }

            if (!any)
            {
                this.AddRow(I18n.Get(EventFilter.HasSearch ? "menu.completed.no-match" : story ? "menu.completed.none-story" : "menu.completed.none-hearts"), MutedColor);
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
                ? "  " + I18n.Get("menu.npc.hearts", new { hearts = Game1.player.getFriendshipHeartLevelForNPC(owner) })
                : "";
            string counts = I18n.Get("menu.count.pending", new { count = pending.Pending.Count })
                + (now > 0 ? I18n.Get("menu.count.now", new { count = now }) : "")
                + (later > 0 ? I18n.Get("menu.count.later", new { count = later }) : "")
                + (pending.LockedEvents.Count > 0 ? I18n.Get("menu.count.locked", new { count = pending.LockedEvents.Count }) : "")
                + I18n.Get("menu.count.seen", new { count = seen });
            string prefix = expandable ? (expanded ? "v " : "> ") : "";

            bool isPinned = this.mod.PinnedNpcs.Contains(owner);
            this.AddRow(
                prefix + name + hearts,
                now > 0 ? ReadyColor : later > 0 ? SoonColor : Game1.textColor,
                font: Game1.dialogueFont,
                onClick: expandable && key != null && !EventFilter.IsActive ? () => ToggleExpanded(key) : null,
                button: I18n.Get(isPinned ? "menu.button.unpin" : "menu.button.pin"),
                onButton: () => this.mod.TogglePin(owner));
            this.AddRow(counts, MutedColor, indent: expandable ? 28 : 0);
        }

        private void AddEventList(PendingEvents pending, List<(EventInfo Event, EventEvaluation Eval)> visible, int indent, bool showLocation)
        {
            if (visible.Count == 0 && !EventFilter.IsActive)
                this.AddRow(I18n.Get(pending.NextLocked == null ? "menu.caught-up" : "menu.nothing-unlocked"), MutedColor, indent);

            foreach ((EventInfo evt, EventEvaluation eval) in visible)
                this.AddEventDetail(evt, eval, indent, showLocation);

            if (EventFilter.IsActive)
                return;

            if (pending.NextLocked is { } next)
            {
                int current = Game1.player.getFriendshipHeartLevelForNPC(next.Event.Owner);
                bool heartsOnly = next.Event.Relationship == null && next.Event.RequiredPoints > 0 && next.Event.ProgressRank == next.Event.RequiredPoints;
                string need = !heartsOnly ? EventNarrator.NextStep(next.Event, next.Eval, this.Index)
                    : next.Event.RequiredPoints % NPC.friendshipPointsPerHeartLevel == 0
                    ? I18n.Get("menu.next.hearts", new { hearts = next.Event.RequiredHearts, current })
                    : I18n.Get("menu.next.points", new { points = next.Event.RequiredPoints });
                string title = this.mod.Config.SpoilerFree
                    ? next.Event.Title
                    : I18n.Get("menu.event-at", new { title = next.Event.Title, location = next.Event.LocationDisplayName });
                this.AddRow(I18n.Get("menu.next", new { title, need }), MutedColor, indent);
            }

            if (pending.Unreachable > 0)
                this.AddRow(I18n.Get("menu.unreachable", new { count = pending.Unreachable }), MutedColor, indent);
        }

        private void AddEventDetail(EventInfo evt, EventEvaluation eval, int indent, bool showLocation)
        {
            bool hidden = this.mod.HidesDetails(eval);
            string title = showLocation && !hidden ? I18n.Get("menu.event-at", new { title = evt.Title, location = evt.LocationDisplayName }) : evt.Title;
            bool snoozed = this.mod.IsSnoozed(evt);
            bool tracked = evt.IsHeartEvent ? this.mod.PinnedNpcs.Contains(evt.Owner) : this.mod.IsStoryPinned(evt);
            bool canSnooze = snoozed || (tracked && eval.Status is not (EventStatus.Locked or EventStatus.Special));

            // story events are pinned one at a time (heart events are pinned through their NPC)
            string? pinLabel = evt.IsStory && !hidden ? I18n.Get(tracked ? "menu.button.unpin" : "menu.button.pin") : null;
            string? snoozeLabel = canSnooze ? I18n.Get(snoozed ? "menu.button.wake" : "menu.button.snooze") : null;
            this.AddRow(
                $"{title}  [{(hidden ? I18n.Get("status.hidden") : EventNarrator.StatusTag(evt, eval, this.Index))}]{(snoozed ? "  " + I18n.Get("menu.snoozed") : "")}",
                snoozed ? MutedColor : StatusColor(eval.Status),
                indent,
                button: pinLabel ?? snoozeLabel,
                onButton: pinLabel != null ? () => this.mod.ToggleStoryPin(evt) : () => this.mod.ToggleSnooze(evt),
                button2: pinLabel != null ? snoozeLabel : null,
                onButton2: () => this.mod.ToggleSnooze(evt));

            int inner = indent + 28;
            if (hidden)
            {
                this.AddRow(I18n.Get("menu.hidden"), MutedColor, inner);
                this.AddSpacer(8);
                return;
            }

            if (evt.IsStory && evt.Actors.Count > 0)
                this.AddRow(I18n.Get("menu.with", new { actors = ActorList(evt) }), MutedColor, inner);

            if (eval.Status is EventStatus.AvailableNow or EventStatus.LaterToday && this.mod.State.Travel.MinutesTo(evt.LocationName) is { } travel)
            {
                if (travel > 0)
                    this.AddRow(I18n.Get("menu.travel", new { duration = PreconditionFormatter.FormatDuration(travel) }), MutedColor, inner);
                else if (eval.Status == EventStatus.AvailableNow)
                    this.AddRow(I18n.Get("menu.travel.here"), ReadyColor, inner);
            }

            if (eval.Status == EventStatus.AvailableNow)
                this.AddRow(EventNarrator.AvailableNow(evt), ReadyColor, inner);
            else if (eval.Status == EventStatus.LaterToday && eval.MinutesUntilStart is { } minutes)
                this.AddRow(EventNarrator.Reminder(evt, eval, minutes), SoonColor, inner);

            if (eval.Festival is { } festival)
                this.AddRow(I18n.Get("menu.festival", new { start = PreconditionFormatter.Time(festival.Start), end = PreconditionFormatter.Time(festival.End) }), MutedColor, inner);

            if (eval.Door is { } door)
                this.AddDoorRows(door, eval, inner);

            if (evt.Conditions.Count == 0 && eval.Door == null)
                this.AddRow(I18n.Get("menu.no-requirements"), MetColor, inner);
            for (int i = 0; i < evt.Conditions.Count; i++)
            {
                if (eval.States[i] == ConditionState.Soft)
                {
                    Color timeColor = eval.MinutesUntilStart > 0 ? SoonColor : eval.TimeOpen ? MetColor : MutedColor;
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
                this.AddRow("> " + I18n.Get("menu.leads-to", new { events = next }) + (unlocks.Count > 2 ? I18n.Get("hud.more", new { count = unlocks.Count - 2 }) : ""), MutedColor, inner);
            }

            this.AddRow($"#{evt.Id}", MutedColor * 0.7f, inner);
            this.AddSpacer(8);
        }

        /// <summary>Rows for the locked door into the event's location: its hours, friendship rule and festival closure.</summary>
        private void AddDoorRows(DoorState door, EventEvaluation eval, int indent)
        {
            if (door.FestivalClosed)
                this.AddRow("x " + I18n.Get("door.festival"), UnmetColor, indent);

            if (door.Door.MinFriendship > 0 && door.Door.Npc != null)
            {
                string name = EventIndex.GetNpcDisplayName(door.Door.Npc);
                int hearts = (int)Math.Ceiling(door.Door.MinFriendship / (double)NPC.friendshipPointsPerHeartLevel);
                int current = Game1.player.getFriendshipHeartLevelForNPC(door.Door.Npc);
                if (door.HeartsOk)
                    this.AddRow("+ " + I18n.Get("door.hearts.ok", new { name, hearts }), MetColor, indent);
                else
                    this.AddRow("x " + I18n.Get("door.hearts", new { name, hearts, current }), UnmetColor, indent);
            }

            if (door.AllDay)
                this.AddRow("+ " + I18n.Get("door.town-key"), MetColor, indent);
            else if (eval.DoorNeverOpen)
                this.AddRow("x " + I18n.Get("door.never-open", new { open = PreconditionFormatter.Time(door.Open), close = PreconditionFormatter.Time(door.Close) }), UnmetColor, indent);
            else
            {
                int now = Game1.timeOfDay;
                Color color = now >= door.Open && now < door.Close ? MetColor : now < door.Open ? SoonColor : MutedColor;
                this.AddRow("~ " + I18n.Get("door.hours", new { open = PreconditionFormatter.Time(door.Open), close = PreconditionFormatter.Time(door.Close) }), color, indent);
            }
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

        private void AddRow(string text, Color color, int indent = 0, SpriteFont? font = null, Action? onClick = null, string? button = null, Action? onButton = null, string? button2 = null, Action? onButton2 = null)
        {
            font ??= Game1.smallFont;
            int buttons = (button != null ? 1 : 0) + (button2 != null ? 1 : 0);
            int maxWidth = this.ContentArea.Width - indent - buttons * (ButtonWidth + 16);
            string wrapped = Game1.parseText(text, font, Math.Max(100, maxWidth));
            int height = (int)Math.Ceiling(font.MeasureString(wrapped).Y) + 4;
            if (buttons > 0)
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
                OnButton = onButton,
                Button2 = button2,
                OnButton2 = onButton2
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
                Utility.drawTextWithShadow(b, I18n.Get("menu.search"), Game1.smallFont, new Vector2(this.searchBox.X + 16, this.searchBox.Y + 10), MutedColor, shadowIntensity: 0f);
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

                    int textY = y + (row.Button != null || row.Button2 != null ? (row.Height - (int)row.Font.MeasureString(row.Text).Y) / 2 : 0);
                    Utility.drawTextWithShadow(b, row.Text, row.Font, new Vector2(content.X + row.Indent, textY), row.Color, shadowIntensity: 0.25f);

                    int buttonRight = content.Right;
                    foreach ((string? label, Action? action) in new[] { (row.Button, row.OnButton), (row.Button2, row.OnButton2) })
                    {
                        if (label == null || action == null)
                            continue;
                        var buttonArea = new Rectangle(buttonRight - ButtonWidth, y + (row.Height - 48) / 2, ButtonWidth, 48);
                        DrawBox(b, buttonArea, buttonArea.Contains(mouseX, mouseY) ? Color.Wheat : Color.White);
                        DrawCentered(b, label, buttonArea, Game1.textColor);
                        this.hitAreas.Add((buttonArea, action));
                        buttonRight -= ButtonWidth + 12;
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
