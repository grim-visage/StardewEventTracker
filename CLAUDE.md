# CLAUDE.md

The folder name is historical. The project, DLL, namespace and UniqueID are all `StardewEventTracker` / `grim-visage.StardewEventTracker`. Targets Stardew Valley 1.6 with SMAPI 4.x, net6.0 (build with .NET SDK 8).

## Build

`Pathoschild.Stardew.ModBuildConfig` deploys straight into the game's `Mods/StardewEventTracker/` on every build and also writes a release zip. **Overwriting the DLL while the game has it loaded corrupts it** (`BadImageFormatException: Bad IL range`, which looks like a UI bug). Before building:

```bash
pgrep -f '^\./StardewModdingAPI'    # bare `pgrep -f StardewModdingAPI` matches the shell itself
```

- Game not running: `dotnet build -c Release`
- Game running (compile check only, writes to `bin/` only): `dotnet build -p:EnableModDeploy=false -p:EnableModZip=false`

There are no automated tests. Check changes in-game, or with the SMAPI console commands `tracker_dump <npc|location>`, `tracker_export` (writes `StardewEventTracker-events.json` to SMAPI's log folder, `Constants.LogDir`), `tracker_travel`, `tracker_reach` and `tracker_reindex`.

## Releases

Bump the version in **both** `StardewEventTracker.csproj` and `manifest.json`. Copy the release zip to `dist/StardewEventTracker-<ver>.zip`. `README.txt` is plain text, ships inside the zip, and is the user-facing documentation, so update it when behavior, keys or settings change. GitHub releases are titled `v<ver>`.

Git workflow: commit after each change, but push only when the user asks. Never push the `backup-before-rewrite` branch.

## Architecture

- `ModEntry.cs` is the hub. It registers SMAPI events and console commands and owns pins, reminders, HUD dragging and config normalization. UI classes call back into it (`TogglePin`, `GetPinnedStoryEvents`, etc.).
- `PlayerState.cs` holds per-local-player state (split-screen gets one per screen): its own `EventIndex` plus pins. Pins persist per save as JSON (`PinData`) via `Helper.Data`.
- `Data/` is the event model, built from the game's live event data so content-pack events (SVE, Ridgeside, etc.) are included:
  - `EventIndex` collects events into `EventInfo`s grouped by NPC (heart events) or location (story events). It re-indexes on `AssetsInvalidated`, because some mods (Ridgeside) patch events only after the save loads.
  - `EventEvaluator` checks preconditions against the current player and day and produces an `EventEvaluation` with an `EventStatus`. `PendingEvents.GetNext` decides which event to follow next.
  - `PreconditionFormatter` and `EventNarrator` turn preconditions and status into display text. `TravelEstimator`, `AreaAccess` and `DoorAccess` decide whether and when the player can reach a location.
  - `Hints` loads `assets/hints.json`: hand-written explanations for mail flags, conversation topics and preconditions.
- `UI/`: `TrackerMenu` (F2 menu with Hearts, Story and Completed tabs), `HudTracker` (draggable overlay), `MapMarkers`, and `Themes/` (HUD skins that derive from `HudTheme`).
- `I18n.cs` wraps SMAPI translations. All user-facing strings live in `i18n/default.json`.
- `Integrations/` holds the Generic Mod Config Menu API interface. `ModConfig.cs` holds the settings.

Mods like Ridgeside and GI Extra Locations give events impossible preconditions (mail flags containing "nonexistent"/"Inexistent", `GameStateQuery FALSE`) and start them from code. The tracker reports these as "special trigger" rather than unreachable.

To check how the game itself evaluates something, decompile it instead of guessing: `DOTNET_ROLL_FORWARD=Major ilspycmd -t StardewValley.GameLocation "<game>/Stardew Valley.dll" -r "<game>"` (the game is at `~/.local/share/Steam/steamapps/common/Stardew Valley`).

## Issues

Issues live in GitHub Issues on `grim-visage/StardewEventTracker`; use the `gh` CLI.
