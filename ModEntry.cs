using System;
using System.Collections.Generic;
using System.Linq;
using NpcEventTracker.Data;
using NpcEventTracker.Integrations;
using NpcEventTracker.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace NpcEventTracker
{
    /// <summary>Per-save data: which NPCs the player is tracking.</summary>
    internal sealed class PinData
    {
        public List<string> PinnedNpcs { get; set; } = new();
    }

    public sealed class ModEntry : Mod
    {
        internal ModConfig Config { get; private set; } = new();
        internal EventIndex Index { get; private set; } = null!;
        internal HashSet<string> PinnedNpcs { get; } = new();

        private HudTracker hud = null!;

        /// <summary>Event keys already announced today, so each alert fires once per day.</summary>
        private readonly HashSet<string> alertedToday = new();

        private readonly List<(int Time, string Text)> messagesToday = new();

        /// <summary>Today's reminder pop-ups, oldest first, so missed ones can be re-read in the menu.</summary>
        internal IReadOnlyList<(int Time, string Text)> MessagesToday => this.messagesToday;

        private string PinDataPath => $"data/{Constants.SaveFolderName}.json";

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            this.NormalizeConfig();
            this.Index = new EventIndex(this.Monitor);
            this.hud = new HudTracker(this);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.GameLoop.TimeChanged += this.OnTimeChanged;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            helper.Events.Player.Warped += this.OnWarped;
            helper.Events.Display.MenuChanged += this.OnMenuChanged;
            helper.Events.Display.RenderedHud += (_, e) => this.hud.Draw(e.SpriteBatch);
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;

            helper.ConsoleCommands.Add("net_dump", "Lists an NPC's heart events (or a location's story events) and the status of each requirement.\n\nUsage: net_dump <npc or location name>", this.OnDumpCommand);
            helper.ConsoleCommands.Add("net_export", "Writes every indexed event and its current status to exports/events.json in the mod folder.\n\nUsage: net_export", this.OnExportCommand);
            helper.ConsoleCommands.Add("net_reindex", "Re-reads all event data.\n\nUsage: net_reindex", (_, _) =>
            {
                if (Context.IsWorldReady)
                    this.Index.Rebuild();
            });
        }

        internal void TogglePin(string npc)
        {
            if (!this.PinnedNpcs.Remove(npc))
                this.PinnedNpcs.Add(npc);

            this.Helper.Data.WriteJsonFile(this.PinDataPath, new PinData { PinnedNpcs = this.PinnedNpcs.OrderBy(p => p).ToList() });
            this.Index.Invalidate();
            this.RunReminders();
        }

        /****
        ** Game events
        ****/
        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            gmcm.Register(this.ModManifest, () => this.Config = new ModConfig(), () => this.Helper.WriteConfig(this.Config));
            gmcm.AddSectionTitle(this.ModManifest, () => "Controls");
            gmcm.AddKeybindList(this.ModManifest, () => this.Config.OpenMenuKey, v => this.Config.OpenMenuKey = v, () => "Open tracker menu");
            gmcm.AddKeybindList(this.ModManifest, () => this.Config.ToggleHudKey, v => this.Config.ToggleHudKey = v, () => "Toggle HUD tracker");

            gmcm.AddSectionTitle(this.ModManifest, () => "Reminders", () => "Messages for pinned NPCs' events. Times are in-game time.");
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.MorningHeadsUp, v => this.Config.MorningHeadsUp = v, () => "Morning heads-up",
                () => "When the day starts, mention pinned NPC events that can happen today.");
            foreach (int minutes in ModConfig.AllowedReminderMinutes)
            {
                gmcm.AddBoolOption(
                    this.ModManifest,
                    () => this.Config.ReminderMinutesBefore.Contains(minutes),
                    v =>
                    {
                        this.Config.ReminderMinutesBefore.Remove(minutes);
                        if (v)
                            this.Config.ReminderMinutesBefore.Add(minutes);
                    },
                    () => $"Remind {FormatInterval(minutes)} before",
                    () => $"Remind you {FormatInterval(minutes)} before a pinned NPC's event time window opens, so you have time to get there.");
            }
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.AlertWhenAvailable, v => this.Config.AlertWhenAvailable = v, () => "When it's available",
                () => "Show a message when a pinned NPC's event can happen right now.");
            gmcm.AddNumberOption(this.ModManifest, () => this.Config.PopupSeconds, v => this.Config.PopupSeconds = v, () => "Pop-up duration",
                () => "How many seconds reminder messages stay on screen.", min: 3, max: 30, formatValue: v => $"{v}s");

            gmcm.AddSectionTitle(this.ModManifest, () => "HUD tracker");
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowHud, v => this.Config.ShowHud = v, () => "Show HUD tracker");
            gmcm.AddNumberOption(this.ModManifest, () => this.Config.HudX, v => this.Config.HudX = v, () => "HUD X position", min: 0, max: 3000, interval: 4);
            gmcm.AddNumberOption(this.ModManifest, () => this.Config.HudY, v => this.Config.HudY = v, () => "HUD Y position", min: 0, max: 2000, interval: 4);
            gmcm.AddNumberOption(this.ModManifest, () => this.Config.HudMaxNpcs, v => this.Config.HudMaxNpcs = v, () => "Max NPCs on HUD", min: 1, max: 15);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.PinnedNpcs.Clear();
            PinData? data = this.Helper.Data.ReadJsonFile<PinData>(this.PinDataPath);
            if (data != null)
                this.PinnedNpcs.UnionWith(data.PinnedNpcs);

            this.Index.Rebuild();
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            // content packs can add or change events from day to day
            this.Index.Rebuild();
            this.alertedToday.Clear();
            this.messagesToday.Clear();
            this.RunReminders(morning: true);
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            this.Index.Invalidate();
            this.RunReminders();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer)
                return;
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
            this.PinnedNpcs.Clear();
            this.alertedToday.Clear();
            this.messagesToday.Clear();
            this.Index.Clear();
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // checked first: the default Shift + F2 also counts as pressing the menu's F2
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
        /// <summary>Sends reminders, available-now alerts and (at day start) heads-ups for pinned NPCs' events.</summary>
        private void RunReminders(bool morning = false)
        {
            if (!Context.IsWorldReady || Game1.eventUp)
                return;

            // ascending, so the first interval the remaining time fits in is the smallest one crossed
            var intervals = this.Config.ReminderMinutesBefore.Where(m => m > 0).Distinct().OrderBy(m => m).ToList();
            int headsUps = 0;

            foreach (string npc in this.PinnedNpcs.OrderBy(EventIndex.GetNpcDisplayName))
            {
                foreach ((EventInfo evt, EventEvaluation eval) in this.Index.GetPending(npc).Pending)
                {
                    if (eval.Status == EventStatus.AvailableNow)
                    {
                        if (this.Config.AlertWhenAvailable && this.alertedToday.Add($"now:{evt.Key}"))
                            Notify(EventNarrator.AvailableNow(evt));
                        continue;
                    }

                    if (eval.Status != EventStatus.LaterToday || eval.MinutesUntilStart is not { } minutesLeft)
                        continue;

                    int due = intervals.FirstOrDefault(m => minutesLeft <= m);
                    if (due > 0)
                    {
                        // mark every interval already crossed, so loading in late doesn't send a stack of reminders
                        bool sent = this.alertedToday.Contains($"remind:{due}:{evt.Key}");
                        foreach (int crossed in intervals.Where(m => m >= due))
                            this.alertedToday.Add($"remind:{crossed}:{evt.Key}");
                        if (!sent)
                            Notify(EventNarrator.Reminder(evt, minutesLeft));
                    }
                    else if (morning && this.Config.MorningHeadsUp && headsUps < 3 && this.alertedToday.Add($"morning:{evt.Key}"))
                    {
                        headsUps++;
                        Notify(EventNarrator.MorningHeadsUp(evt));
                    }
                }
            }
        }

        private void Notify(string text)
        {
            this.messagesToday.Add((Game1.timeOfDay, text));
            Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type) { timeLeft = Math.Clamp(this.Config.PopupSeconds, 3, 30) * 1000 });
        }

        private static string FormatInterval(int minutes) =>
            minutes % 60 == 0 ? $"{minutes / 60} hour{(minutes == 60 ? "" : "s")}" : $"{minutes} minutes";

        /// <summary>Migrates 1.0 settings and cleans up the reminder list.</summary>
        private void NormalizeConfig()
        {
            bool changed = false;
            if (this.Config.ShowAlerts is { } showAlerts)
            {
                this.Config.AlertWhenAvailable = showAlerts;
                this.Config.ShowAlerts = null;
                changed = true;
            }

            // 1.0 defaulted to F8/F9, which UI Info Suite 2 also uses
            if (this.Config.OpenMenuKey.ToString() == "F8")
            {
                this.Config.OpenMenuKey = KeybindList.Parse("F2");
                changed = true;
            }
            // F9 was the 1.0 default; F4 (an early 1.1 default) opens the game's screenshot mode
            if (this.Config.ToggleHudKey.ToString() is "F9" or "F4")
            {
                this.Config.ToggleHudKey = KeybindList.Parse("LeftShift + F2");
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
