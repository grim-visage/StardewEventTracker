# Development

Notes for contributors. Player documentation is in `README.txt`, which ships inside the release zip.

## Building

Requires the .NET 8 SDK or later (the mod targets net6.0):

```bash
dotnet build -c Release
```

This installs the mod into your `Mods/StardewEventTracker/` folder and writes a release zip to `bin/Release/net6.0/`. **Close the game first.** Replacing the DLL while the game has it loaded corrupts it and causes `BadImageFormatException: Bad IL range` errors. To only check that it compiles while the game is running:

```bash
dotnet build -p:EnableModDeploy=false -p:EnableModZip=false
```

For a release, bump the version in both `StardewEventTracker.csproj` and `manifest.json`, and update `README.txt` if behaviour, keys or settings changed.

## Console commands

Type these in the SMAPI console:

| Command | What it does |
|---|---|
| `tracker_dump <name>` | Prints every heart event for an NPC (or story event for a location), with each requirement's status and raw code. |
| `tracker_travel <location>` | Estimates the walk from the player to a location. |
| `tracker_export` | Writes every event and its status to `exports/events.json` in the mod folder, plus counts of every NPC (befriendable, with heart events). |
| `tracker_reach` | Lists mod-added areas with no way in yet. Events there show "Not yet" until one opens up. |
| `tracker_reindex` | Re-reads event data. This also happens each morning and whenever a mod changes events, letters or dialogue. |

## Translations

All text is in `i18n/default.json`. To translate, copy it to a file named after the language (for example `i18n/de.json` or `i18n/pt-BR.json`) and translate the values. Keep the `{{tokens}}` as they are.

## How story flags are explained

Events often depend on mail flags and conversation topics. The tracker works out where each one comes from using the game's data: letters and what sends them, special orders, quests, trigger actions, events that set the flag (including only on some answers), and `MarkEventSeen` markers. It also scans Content Patcher packs for events added conditionally later.

Some flags aren't set by anything in the data, because the game's or a mod's own code sets them. For those, `assets/hints.json` holds short hand-written hints, researched from the game code, mod wikis and handbooks. A hint always takes priority over the generated explanation. The same file explains requirement types that other mods add (e.g. Ridgeside's "Arrive riding your horse") and how to open up areas that start with no way in. Any flag still unexplained is shown as words, e.g. "Story flag: Duskspire defeated".

Hint contributions are welcome. Keep them short and in your own words, and say where the information came from.

## Events that can't happen

Some mods (Ridgeside Village, GI Extra Locations) give events a requirement that can never be met, such as mail flags containing "nonexistent" or `GameStateQuery FALSE`, and start them from their own code instead. The tracker marks these "special trigger" rather than unreachable.

Events whose requirements can never be met by anything in the data, or that need a flag set only by a choice the player has already passed, are dropped from tracking entirely.
