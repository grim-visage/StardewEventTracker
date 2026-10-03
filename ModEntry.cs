using System;
using System.Collections.Generic;
using System.Linq;
using NpcEventTracker.Data;
using NpcEventTracker.Integrations;
using NpcEventTracker.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace NpcEventTracker
{
    public sealed class ModEntry : Mod
    {
        internal ModConfig Config { get; private set; } = new();

        /// <summary>Each local player's tracking state; split-screen co-op gets one per screen.</summary>
        private PerScreen<PlayerState> screen = null!;
        private PerScreen<HudTracker> hud = null!;

        /// <summary>While dragging the HUD, the cursor's offset from its top-left corner.</summary>
        private readonly PerScreen<Point?> hudDragOffset = new();

        internal PlayerState State => this.screen.Value;
        internal EventIndex Index => this.State.Index;
        internal HashSet<string> PinnedNpcs => this.State.PinnedNpcs;
        private HashSet<string> alertedToday => this.State.AlertedToday;

        /// <summary>Today's reminder pop-ups, oldest first, so missed ones can be re-read in the menu.</summary>
        internal IReadOnlyList<(int Time, string Text)> MessagesToday => this.State.MessagesToday;

        /// <summary>The game tick a pop-up sound last played on, so several pop-ups at once only play one sound.</summary>
        private int lastSoundTick = -1;

        /// <summary>Pins are per player: the main player keeps the original path, other local players get their own file.</summary>
        private string PinDataPath => Context.IsMainPlayer
            ? $"data/{Constants.SaveFolderName}.json"
            : $"data/{Constants.SaveFolderName}-{Game1.player.UniqueMultiplayerID}.json";

        public override void Entry(IModHelper helper)
        {
            I18n.Init(helper.Translation);
            this.Config = helper.ReadConfig<ModConfig>();
            this.NormalizeConfig();
            this.screen = new PerScreen<PlayerState>(() => new PlayerState(this.Monitor));
            this.hud = new PerScreen<HudTracker>(() => new HudTracker(this));

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.UpdateTicked += (_, _) =>
            {
                if (!Context.IsWorldReady)
                    return;
                this.State.Travel.OnUpdateTicked();
                this.UpdateHudDrag();
            };
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Player.Warped += this.OnWarped;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.Display.RenderedHud += (_, e) => this.hud.Value.Draw(e.SpriteBatch);
            var mapMarkers = new MapMarkers(this);
            helper.Events.Display.RenderedActiveMenu += (_, e) =>
            {
                if (Context.IsWorldReady && this.Config.ShowMapMarkers)
                    mapMarkers.Draw(e.SpriteBatch);
            };
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

            helper.ConsoleCommands.Add("net_dump", "Lists an NPC's heart events (or a location's story events) and the status of each requirement.\n\nUsage: net_dump <npc or location name>", this.OnDumpCommand);
            helper.ConsoleCommands.Add("net_export", "Writes every indexed event and its current status to exports/events.json in the mod folder.\n\nUsage: net_export", this.OnExportCommand);
            helper.ConsoleCommands.Add("net_travel", "Estimates the walk from you to a location, in in-game minutes.\n\nUsage: net_travel <location name>", (_, args) =>
            {
                if (!Context.IsWorldReady || args.Length == 0)
                    return;
                this.State.Travel.Invalidate();
                int? minutes = this.State.Travel.MinutesTo(args[0]);
                this.Monitor.Log(
                    $"{Game1.player.currentLocation.NameOrUniqueName} -> {args[0]}: {(minutes == null ? "no route found" : $"about {minutes} in-game minutes")} "
                    + $"(clock measured at {this.State.Travel.RealMsPerGameMinute:0} real ms per in-game minute).",
                    LogLevel.Info);
            });
            helper.ConsoleCommands.Add("net_reindex", "Re-reads all event data.\n\nUsage: net_reindex", (_, _) =>
            {
                if (Context.IsWorldReady)
                    this.Index.Rebuild();
            });
        }

        internal void TogglePin(string npc)
        {
            if (this.PinnedNpcs.Remove(npc))
            {
                // remember unpinned partners so auto-pin doesn't add them back
                if (IsPartner(npc))
                    this.State.AutoPinDismissed.Add(npc);
            }
            else
            {
                this.PinnedNpcs.Add(npc);
                this.State.AutoPinDismissed.Remove(npc);
            }

            this.SavePins();
            this.Index.Invalidate();
            this.RunReminders();
        }

        /// <summary>Whether spoiler-free mode hides this event's details: only once it's unlocked are they shown.</summary>
        internal bool HidesDetails(EventEvaluation eval) =>
            this.Config.SpoilerFree && eval.Status is EventStatus.Locked or EventStatus.NotYet or EventStatus.Special or EventStatus.Unreachable;

        internal bool IsStoryPinned(EventInfo evt) => this.State.PinnedStoryEvents.Contains(evt.Key);

        /// <summary>Pins or unpins one story event.</summary>
        internal void ToggleStoryPin(EventInfo evt)
        {
            if (!this.State.PinnedStoryEvents.Remove(evt.Key))
                this.State.PinnedStoryEvents.Add(evt.Key);
            this.SavePins();
            this.Index.Invalidate();
            this.RunReminders();
        }

        /// <summary>The pinned story events that haven't been seen yet, ordered by status and location.</summary>
        internal List<(EventInfo Event, EventEvaluation Eval)> GetPinnedStoryEvents()
        {
            return this.State.PinnedStoryEvents
                .Select(this.Index.FindByKey)
                .Where(evt => evt != null && !evt.Seen)
                .Select(evt => (Event: evt!, Eval: this.Index.Evaluate(evt!)))
                .OrderBy(p => p.Eval.Status)
                .ThenBy(p => p.Event.LocationDisplayName)
                .ToList();
        }

        internal bool IsSnoozed(EventInfo evt) => this.State.SnoozedToday.Contains(evt.Key);

        /// <summary>Silences (or un-silences) an event's reminders until tomorrow.</summary>
        internal void ToggleSnooze(EventInfo evt)
        {
            if (!this.State.SnoozedToday.Remove(evt.Key))
                this.State.SnoozedToday.Add(evt.Key);
            this.Index.Invalidate();
        }

        private void SavePins()
        {
            this.Helper.Data.WriteJsonFile(this.PinDataPath, new PinData
            {
                PinnedNpcs = this.PinnedNpcs.OrderBy(p => p).ToList(),
                PinnedStoryEvents = this.State.PinnedStoryEvents.OrderBy(p => p).ToList(),
                AutoPinDismissed = this.State.AutoPinDismissed.OrderBy(p => p).ToList()
            });
        }

        /****
        ** Game events
        ****/
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            var m = this.ModManifest;
            gmcm.Register(m, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));

            gmcm.AddSectionTitle(m, () => I18n.Get("config.section.controls"));
            gmcm.AddKeybindList(m, () => this.Config.OpenMenuKey, v => this.Config.OpenMenuKey = v, () => I18n.Get("config.menu-key"));
            gmcm.AddKeybindList(m, () => this.Config.ToggleHudKey, v => this.Config.ToggleHudKey = v, () => I18n.Get("config.hud-key"));
            gmcm.AddKeybindList(m, () => this.Config.PinKey, v => this.Config.PinKey = v, () => I18n.Get("config.pin-key"), () => I18n.Get("config.pin-key.tip"));
            gmcm.AddBoolOption(m, () => this.Config.AutoPinPartners, v => this.Config.AutoPinPartners = v, () => I18n.Get("config.auto-pin"), () => I18n.Get("config.auto-pin.tip"));
            gmcm.AddBoolOption(m, () => this.Config.SpoilerFree, v => this.Config.SpoilerFree = v, () => I18n.Get("config.spoiler-free"), () => I18n.Get("config.spoiler-free.tip"));

            gmcm.AddSectionTitle(m, () => I18n.Get("config.section.reminders"), () => I18n.Get("config.section.reminders.tip"));
            gmcm.AddBoolOption(m, () => this.Config.StoryReminders, v => this.Config.StoryReminders = v, () => I18n.Get("config.story-reminders"), () => I18n.Get("config.story-reminders.tip"));
            gmcm.AddBoolOption(m, () => this.Config.MorningHeadsUp, v => this.Config.MorningHeadsUp = v, () => I18n.Get("config.morning"), () => I18n.Get("config.morning.tip"));
            foreach (int minutes in ModConfig.AllowedReminderMinutes)
            {
                gmcm.AddBoolOption(
                    m,
                    () => this.Config.ReminderMinutesBefore.Contains(minutes),
                    v =>
                    {
                        this.Config.ReminderMinutesBefore.Remove(minutes);
                        if (v)
                            this.Config.ReminderMinutesBefore.Add(minutes);
                    },
                    () => I18n.Get("config.remind-before", new { interval = FormatInterval(minutes) }),
                    () => I18n.Get("config.remind-before.tip", new { interval = FormatInterval(minutes) }));
            }
            gmcm.AddBoolOption(m, () => this.Config.TravelReminders, v => this.Config.TravelReminders = v, () => I18n.Get("config.leave-now"), () => I18n.Get("config.leave-now.tip"));
            gmcm.AddNumberOption(m, () => this.Config.TravelBufferMinutes, v => this.Config.TravelBufferMinutes = v, () => I18n.Get("config.leave-buffer"), () => I18n.Get("config.leave-buffer.tip"),
                min: 0, max: 60, interval: 10, formatValue: v => PreconditionFormatter.FormatDuration(v));
            gmcm.AddBoolOption(m, () => this.Config.AlertWhenAvailable, v => this.Config.AlertWhenAvailable = v, () => I18n.Get("config.available"), () => I18n.Get("config.available.tip"));
            gmcm.AddBoolOption(m, () => this.Config.TomorrowHeadsUp, v => this.Config.TomorrowHeadsUp = v, () => I18n.Get("config.tomorrow"), () => I18n.Get("config.tomorrow.tip"));
            gmcm.AddTextOption(m, () => this.Config.ReminderSound, v => this.Config.ReminderSound = v, () => I18n.Get("config.reminder-sound"), () => I18n.Get("config.reminder-sound.tip"),
                allowedValues: ModConfig.SoundCues, formatAllowedValue: SoundLabel, fieldId: "ReminderSound");
            gmcm.AddTextOption(m, () => this.Config.AvailableSound, v => this.Config.AvailableSound = v, () => I18n.Get("config.available-sound"), () => I18n.Get("config.available-sound.tip"),
                allowedValues: ModConfig.SoundCues, formatAllowedValue: SoundLabel, fieldId: "AvailableSound");
            gmcm.OnFieldChanged(m, (fieldId, value) =>
            {
                if (fieldId is "ReminderSound" or "AvailableSound" && value is string cue)
                    PlaySound(cue);
            });
            gmcm.AddNumberOption(m, () => this.Config.PopupSeconds, v => this.Config.PopupSeconds = v, () => I18n.Get("config.popup-seconds"), () => I18n.Get("config.popup-seconds.tip"),
                min: 3, max: 30, formatValue: v => I18n.Get("config.seconds", new { seconds = v }));

            gmcm.AddSectionTitle(m, () => I18n.Get("config.section.hud"));
            gmcm.AddBoolOption(m, () => this.Config.ShowMapMarkers, v => this.Config.ShowMapMarkers = v, () => I18n.Get("config.map-markers"), () => I18n.Get("config.map-markers.tip"));
            gmcm.AddBoolOption(m, () => this.Config.ShowHud, v => this.Config.ShowHud = v, () => I18n.Get("config.show-hud"));
            gmcm.AddKeybindList(m, () => this.Config.HudDragKey, v => this.Config.HudDragKey = v, () => I18n.Get("config.drag-key"), () => I18n.Get("config.drag-key.tip"));
            gmcm.AddNumberOption(m, () => this.Config.HudX, v => this.Config.HudX = v, () => I18n.Get("config.hud-x"), min: 0, max: 3000, interval: 4);
            gmcm.AddNumberOption(m, () => this.Config.HudY, v => this.Config.HudY = v, () => I18n.Get("config.hud-y"), min: 0, max: 2000, interval: 4);
            gmcm.AddNumberOption(m, () => this.Config.HudMaxNpcs, v => this.Config.HudMaxNpcs = v, () => I18n.Get("config.hud-max"), min: 1, max: 15);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.State.Reset();
            PinData? data = this.Helper.Data.ReadJsonFile<PinData>(this.PinDataPath);
            if (data != null)
            {
                this.PinnedNpcs.UnionWith(data.PinnedNpcs);
                this.State.PinnedStoryEvents.UnionWith(data.PinnedStoryEvents);
                this.State.AutoPinDismissed.UnionWith(data.AutoPinDismissed);
            }

            this.Index.Rebuild();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // content packs can add or change events from day to day
            this.Index.Rebuild();
            this.State.StartDay();
            this.AutoPinPartners();
            this.DropSeenStoryPins();
            this.RunReminders(morning: true);
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            this.State.Travel.OnTimeChanged(e.OldTime, e.NewTime);
            this.State.Travel.Invalidate();
            this.Index.Invalidate();
            this.RunReminders();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer)
                return;
            this.State.Travel.Invalidate();
            this.Index.Invalidate();
            this.RunReminders();
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            // friendship and seen events often change behind dialogue/gift menus
            if (Context.IsWorldReady && e.NewMenu is not TrackerMenu)
                this.Index.Invalidate();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            // every screen's state goes when the save closes
            foreach ((_, PlayerState state) in this.screen.GetActiveValues())
                state.Reset();
        }

        /// <summary>Starts dragging the HUD when the drag key is held and the box is clicked.</summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (e.Button != SButton.MouseLeft || !Context.IsPlayerFree || !this.Config.HudDragKey.IsDown())
                return;

            Vector2 cursor = e.Cursor.GetScaledScreenPixels();
            Rectangle bounds = this.hud.Value.Bounds;
            if (!bounds.Contains((int)cursor.X, (int)cursor.Y))
                return;

            // don't swing a tool at whatever's under the box
            this.Helper.Input.Suppress(SButton.MouseLeft);
            this.hudDragOffset.Value = new Point((int)cursor.X - bounds.X, (int)cursor.Y - bounds.Y);
            this.hud.Value.Dragging = true;
        }

        private void UpdateHudDrag()
        {
            if (this.hudDragOffset.Value is not { } offset)
                return;

            Vector2 cursor = this.Helper.Input.GetCursorPosition().GetScaledScreenPixels();
            this.Config.HudX = Math.Max(0, (int)cursor.X - offset.X);
            this.Config.HudY = Math.Max(0, (int)cursor.Y - offset.Y);

            bool held = this.Helper.Input.IsDown(SButton.MouseLeft) || this.Helper.Input.IsSuppressed(SButton.MouseLeft);
            if (!held)
            {
                this.hudDragOffset.Value = null;
                this.hud.Value.Dragging = false;
                this.Helper.WriteConfig(this.Config);
            }
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // modifier combos are checked first: Ctrl/Shift + F2 also count as pressing the menu's F2
            if (this.Config.PinKey.JustPressed())
            {
                this.PinUnderCursor();
                return;
            }

            if (this.Config.ToggleHudKey.JustPressed())
            {
                if (Context.IsPlayerFree)
                {
                    this.Config.ShowHud = !this.Config.ShowHud;
                    this.Helper.WriteConfig(this.Config);
                }
                return;
            }

            if (this.Config.OpenMenuKey.JustPressed())
            {
                if (Game1.activeClickableMenu is TrackerMenu menu)
                    menu.exitThisMenu();
                else if (Context.IsPlayerFree)
                {
                    this.Index.Invalidate();
                    Game1.activeClickableMenu = new TrackerMenu(this);
                }
            }
        }

        /****
        ** Helpers
        ****/
        /// <summary>Pins the NPC hovered on the Social tab, or standing under the cursor in the world.</summary>
        private void PinUnderCursor()
        {
            string? npc = null;
            if (Game1.activeClickableMenu is GameMenu gameMenu && gameMenu.GetCurrentPage() is SocialPage social)
                npc = GetHoveredSocialEntry(social);
            else if (Context.IsPlayerFree)
                npc = this.GetNpcUnderCursor();
            else
                return;

            if (npc == null)
                return;

            string name = EventIndex.GetNpcDisplayName(npc);
            if (!this.Index.ByOwner.ContainsKey(npc))
            {
                ShowToast(I18n.Get("toast.no-events", new { name }));
                return;
            }

            this.TogglePin(npc);
            Game1.playSound("smallSelect");
            ShowToast(I18n.Get(this.PinnedNpcs.Contains(npc) ? "toast.pinned" : "toast.unpinned", new { name }));
        }

        private static string? GetHoveredSocialEntry(SocialPage page)
        {
            int x = Game1.getMouseX(ui_scale: true), y = Game1.getMouseY(ui_scale: true);
            int count = Math.Min(page.characterSlots.Count, page.SocialEntries.Count);

            // only the five visible rows can be hovered
            for (int i = page.slotPosition; i < Math.Min(count, page.slotPosition + 5); i++)
            {
                if (page.characterSlots[i].containsPoint(x, y))
                    return page.SocialEntries[i].InternalName;
            }

            // with a controller, use the highlighted row
            int snapped = page.characterSlots.IndexOf(page.currentlySnappedComponent as ClickableTextureComponent);
            return snapped >= 0 && snapped < count ? page.SocialEntries[snapped].InternalName : null;
        }

        private string? GetNpcUnderCursor()
        {
            Vector2 tile = this.Helper.Input.GetCursorPosition().Tile;
            Vector2 grabTile = this.Helper.Input.GetCursorPosition().GrabTile;

            // NPCs are two tiles tall, so the cursor may be over their head
            NPC? npc = Game1.currentLocation?.characters.FirstOrDefault(c =>
                c.IsVillager && (c.Tile == tile || c.Tile == tile + new Vector2(0, 1) || c.Tile == grabTile));
            return npc?.Name;
        }

        private static void ShowToast(string text)
        {
            Game1.addHUDMessage(new HUDMessage(text) { noIcon = true, timeLeft = 2500 });
        }

        private static bool IsPartner(string npc) =>
            Game1.player.friendshipData.TryGetValue(npc, out Friendship? friendship)
            && (friendship.IsDating() || friendship.IsEngaged() || friendship.IsMarried() || friendship.IsRoommate());

        /// <summary>Unpins story events the player has seen. Missing ones are kept, since content packs can add events only on some days.</summary>
        private void DropSeenStoryPins()
        {
            int removed = this.State.PinnedStoryEvents.RemoveWhere(key => this.Index.FindByKey(key) is { Seen: true });
            if (removed > 0)
                this.SavePins();
        }

        /// <summary>Pins the player's spouse, roommate and partners, unless they unpinned them before.</summary>
        private void AutoPinPartners()
        {
            if (!this.Config.AutoPinPartners)
                return;

            var added = Game1.player.friendshipData.Keys
                .Where(npc => IsPartner(npc) && this.Index.ByOwner.ContainsKey(npc) && !this.PinnedNpcs.Contains(npc) && !this.State.AutoPinDismissed.Contains(npc))
                .ToList();
            if (added.Count == 0)
                return;

            this.PinnedNpcs.UnionWith(added);
            this.SavePins();
            this.Index.Invalidate();
        }
        /// <summary>Sends reminders, available-now alerts and (at day start) heads-ups for pinned NPCs' events.</summary>
        private void RunReminders(bool morning = false)
        {
            if (!Context.IsWorldReady || Game1.eventUp)
                return;

            // ascending, so the first interval the remaining time fits in is the smallest one crossed
            var intervals = this.Config.ReminderMinutesBefore.Where(m => m > 0).Distinct().OrderBy(m => m).ToList();
            int headsUps = 0;

            // pinned NPCs' heart events, then pinned story events if their reminders are on
            var tracked = this.PinnedNpcs
                .OrderBy(EventIndex.GetNpcDisplayName)
                .SelectMany(npc => this.Index.GetPending(npc).Pending)
                .Concat(this.Config.StoryReminders ? this.GetPinnedStoryEvents() : Enumerable.Empty<(EventInfo Event, EventEvaluation Eval)>());

            foreach ((EventInfo evt, EventEvaluation eval) in tracked)
            {
                if (this.IsSnoozed(evt) || this.HidesDetails(eval))
                    continue;

                if (eval.Status == EventStatus.AvailableNow)
                {
                    if (this.Config.AlertWhenAvailable && this.alertedToday.Add($"now:{evt.Key}"))
                        this.Notify(EventNarrator.AvailableNow(evt), this.Config.AvailableSound);
                    continue;
                }

                // in the evening, look ahead to tomorrow's forecast
                if (eval.WorksTomorrow == true && Game1.timeOfDay >= 1800 && this.Config.TomorrowHeadsUp && this.alertedToday.Add($"tomorrow:{evt.Key}"))
                {
                    GameLocation location = Game1.getLocationFromName(evt.LocationName) ?? Game1.currentLocation;
                    this.Notify(EventNarrator.TomorrowHeadsUp(evt, CalendarInfo.WeatherTomorrow(location)), this.Config.ReminderSound);
                    continue;
                }

                if (eval.Status != EventStatus.LaterToday || eval.MinutesUntilStart is not { } minutesLeft)
                    continue;

                // time to leave, based on the walk there
                if (this.Config.TravelReminders
                    && this.State.Travel.MinutesTo(evt.LocationName) is > 0 and int travel
                    && minutesLeft <= travel + this.Config.TravelBufferMinutes)
                {
                    if (this.alertedToday.Add($"leave:{evt.Key}"))
                    {
                        // it covers any fixed reminder crossed at the same time
                        foreach (int crossed in intervals.Where(m => minutesLeft <= m))
                            this.alertedToday.Add($"remind:{crossed}:{evt.Key}");
                        this.Notify(EventNarrator.LeaveNow(evt, eval, travel), this.Config.ReminderSound);
                    }
                    continue;
                }

                int due = intervals.FirstOrDefault(m => minutesLeft <= m);
                if (due > 0)
                {
                    // mark every interval already crossed, so loading in late doesn't send a stack of reminders
                    bool sent = this.alertedToday.Contains($"remind:{due}:{evt.Key}");
                    foreach (int crossed in intervals.Where(m => m >= due))
                        this.alertedToday.Add($"remind:{crossed}:{evt.Key}");
                    if (!sent)
                        this.Notify(EventNarrator.Reminder(evt, eval, minutesLeft), this.Config.ReminderSound);
                }
                else if (morning && this.Config.MorningHeadsUp && headsUps < 3 && this.alertedToday.Add($"morning:{evt.Key}"))
                {
                    headsUps++;
                    this.Notify(EventNarrator.MorningHeadsUp(evt), this.Config.ReminderSound);
                }
            }
        }

        private void Notify(string text, string sound)
        {
            this.State.MessagesToday.Add((Game1.timeOfDay, text));
            if (Game1.ticks != this.lastSoundTick)
            {
                this.lastSoundTick = Game1.ticks;
                PlaySound(sound);
            }
            Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type) { timeLeft = Math.Clamp(this.Config.PopupSeconds, 3, 30) * 1000 });
        }

        private static void PlaySound(string cue)
        {
            if (cue == "none")
                return;
            try
            {
                Game1.playSound(cue);
            }
            catch (Exception)
            {
                // a missing cue shouldn't break the pop-up
            }
        }

        private static string FormatInterval(int minutes) =>
            minutes % 60 == 0
                ? I18n.Get(minutes == 60 ? "config.interval.hour" : "config.interval.hours", new { hours = minutes / 60 })
                : I18n.Get("config.interval.minutes", new { minutes });

        private static string SoundLabel(string cue) => I18n.GetOr($"sound.{cue}", ModConfig.AllowedSounds.TryGetValue(cue, out string? label) ? label : cue);

        /// <summary>Migrates settings from pre-release builds and cleans up the reminder list.</summary>
        private void NormalizeConfig()
        {
            bool changed = false;
            if (this.Config.ShowAlerts is { } showAlerts)
            {
                this.Config.AlertWhenAvailable = showAlerts;
                this.Config.ShowAlerts = null;
                changed = true;
            }

            // pre-release builds defaulted to F8/F9, which UI Info Suite 2 also uses
            if (this.Config.OpenMenuKey.ToString() == "F8")
            {
                this.Config.OpenMenuKey = KeybindList.Parse("F2");
                changed = true;
            }
            // F9 and F4 were pre-release defaults; F4 opens the game's screenshot mode
            if (this.Config.ToggleHudKey.ToString() is "F9" or "F4")
            {
                this.Config.ToggleHudKey = KeybindList.Parse("LeftShift + F2");
                changed = true;
            }

            if (!ModConfig.AllowedSounds.ContainsKey(this.Config.ReminderSound))
            {
                this.Config.ReminderSound = new ModConfig().ReminderSound;
                changed = true;
            }
            if (!ModConfig.AllowedSounds.ContainsKey(this.Config.AvailableSound))
            {
                this.Config.AvailableSound = new ModConfig().AvailableSound;
                changed = true;
            }

            var reminders = this.Config.ReminderMinutesBefore.Where(m => m > 0).Distinct().OrderByDescending(m => m).ToList();
            if (!reminders.SequenceEqual(this.Config.ReminderMinutesBefore))
            {
                this.Config.ReminderMinutesBefore = reminders;
                changed = true;
            }

            if (changed)
                this.Helper.WriteConfig(this.Config);
        }

        private void OnExportCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            this.Index.Invalidate();
            var rows = this.Index.ByOwner.Values
                .SelectMany(list => list)
                .Select(evt =>
                {
                    EventEvaluation eval = this.Index.Evaluate(evt);
                    return new
                    {
                        evt.Id,
                        evt.LocationName,
                        Location = evt.LocationDisplayName,
                        evt.Owner,
                        evt.IsHeartEvent,
                        evt.Actors,
                        evt.Title,
                        Status = eval.Status.ToString(),
                        Conditions = evt.Conditions.Select((c, i) => $"{eval.States[i]}: {c.Raw}").ToArray()
                    };
                })
                .ToList();

            const string path = "exports/events.json";
            this.Helper.Data.WriteJsonFile(path, rows);
            this.Monitor.Log($"Exported {rows.Count} events to {System.IO.Path.Combine(this.Helper.DirectoryPath, path)}.", LogLevel.Info);
        }

        private void OnDumpCommand(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            string query = string.Join(" ", args);
            string? owner = query.Length > 0 ? this.Index.FindOwner(query) : null;
            string? location = owner == null && query.Length > 0 ? this.Index.FindStoryLocation(query) : null;
            if (owner == null && location == null)
            {
                string known = string.Join(", ", this.Index.ByOwner.Keys.OrderBy(k => k));
                this.Monitor.Log($"No heart events for an NPC or story events for a location named '{query}'. NPCs with heart events: {known}", LogLevel.Info);
                return;
            }

            this.Index.Invalidate();
            var events = owner != null ? this.Index.GetEvents(owner) : this.Index.GetStoryEvents(location!);
            foreach (EventInfo evt in events)
            {
                EventEvaluation eval = this.Index.Evaluate(evt);
                this.Monitor.Log($"[{eval.Status}] {evt.Title} at {evt.LocationDisplayName} ({evt.LocationName}) #{evt.Id}  {EventNarrator.StatusTag(evt, eval, this.Index)}", LogLevel.Info);
                for (int i = 0; i < evt.Conditions.Count; i++)
                    this.Monitor.Log($"    {eval.States[i],-7} {PreconditionFormatter.Describe(evt.Conditions[i], this.Index)}   <{evt.Conditions[i].Raw}>", LogLevel.Info);
            }
        }
    }
}
