using System.Collections.Generic;
using System.Linq;
using NpcEventTracker.Data;
using NpcEventTracker.Integrations;
using NpcEventTracker.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
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

        private string PinDataPath => $"data/{Constants.SaveFolderName}.json";

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
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

            helper.ConsoleCommands.Add("net_dump", "Lists an NPC's events and the status of each requirement.\n\nUsage: net_dump <npc name>", this.OnDumpCommand);
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
            this.CheckAlerts();
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
            gmcm.AddSectionTitle(this.ModManifest, () => "HUD tracker");
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowHud, v => this.Config.ShowHud = v, () => "Show HUD tracker");
            gmcm.AddBoolOption(this.ModManifest, () => this.Config.ShowAlerts, v => this.Config.ShowAlerts = v, () => "Event-ready alerts",
                () => "Show a message when a pinned NPC's event can trigger right now.");
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
            this.CheckAlerts();
        }

        private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
        {
            this.Index.Invalidate();
            this.CheckAlerts();
        }

        private void OnWarped(object? sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer)
                return;
            this.Index.Invalidate();
            this.CheckAlerts();
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
            this.Index.Clear();
        }

        private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

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
            else if (this.Config.ToggleHudKey.JustPressed() && Context.IsPlayerFree)
            {
                this.Config.ShowHud = !this.Config.ShowHud;
                this.Helper.WriteConfig(this.Config);
            }
        }

        /****
        ** Helpers
        ****/
        private void CheckAlerts()
        {
            if (!this.Config.ShowAlerts || !Context.IsWorldReady || Game1.eventUp)
                return;

            foreach (string npc in this.PinnedNpcs)
            {
                foreach ((EventInfo evt, EventEvaluation eval) in this.Index.GetPending(npc).Pending)
                {
                    if (eval.Status == EventStatus.Ready && this.alertedToday.Add(evt.Key))
                    {
                        string name = EventIndex.GetNpcDisplayName(npc);
                        Game1.addHUDMessage(new HUDMessage($"{name}'s {evt.Title.ToLowerInvariant()} is ready at {evt.LocationDisplayName}!", HUDMessage.newQuest_type));
                    }
                }
            }
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
            if (owner == null)
            {
                string known = string.Join(", ", this.Index.ByOwner.Keys.Where(k => k != EventIndex.OtherKey).OrderBy(k => k));
                this.Monitor.Log($"No events found for '{query}'. Known NPCs: {known}", LogLevel.Info);
                return;
            }

            this.Index.Invalidate();
            foreach (EventInfo evt in this.Index.GetEvents(owner))
            {
                EventEvaluation eval = this.Index.Evaluate(evt);
                this.Monitor.Log($"[{eval.Status}] {evt.Title} at {evt.LocationDisplayName} ({evt.LocationName}) #{evt.Id}", LogLevel.Info);
                for (int i = 0; i < evt.Conditions.Count; i++)
                    this.Monitor.Log($"    {eval.States[i],-7} {PreconditionFormatter.Describe(evt.Conditions[i], this.Index)}   <{evt.Conditions[i].Raw}>", LogLevel.Info);
            }
        }
    }
}
