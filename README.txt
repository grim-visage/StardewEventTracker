================================================================================
                               NPC EVENT TRACKER
================================================================================

A SMAPI mod for Stardew Valley 1.6 that tracks heart events and story events.
For each NPC it shows which events you haven't seen, where and when each one
triggers, and what you still need: hearts, time of day, weather, day of the
week, earlier events, and so on.

It reads the game's live event data, so events added by mods like Stardew
Valley Expanded, Ridgeside Village, East Scarp, and custom NPC mods are
included automatically.


REQUIREMENTS
------------
  * Stardew Valley 1.6 or later
  * SMAPI 4.0 or later (https://smapi.io)
  * Optional: Generic Mod Config Menu for in-game settings
    (https://www.nexusmods.com/stardewvalley/mods/5098)


INSTALLATION
------------
  1. Install SMAPI if you haven't already.
  2. Unzip this archive into your "Stardew Valley/Mods" folder. You should end
     up with "Mods/NpcEventTracker/".
  3. Launch the game through SMAPI.

There's nothing to enable: SMAPI loads every mod in the Mods folder.


HOW TO USE IT
-------------
  Key   Action
  ----  ------------------------------
  F8    Open or close the tracker menu
  F9    Show or hide the HUD tracker


Tracker menu (F8)
~~~~~~~~~~~~~~~~~
  * Pinned     Every pending event for the NPCs you've pinned.
  * All NPCs   Everyone who has events. Click a name to expand it, and click
               "Pin" to track that NPC.
  * Completed  Events you've already seen, grouped by NPC.

Each event lists its requirements, with a mark showing whether each one is
met right now:

  Mark          Meaning
  ------------  ---------------------------------------------------------
  +  (green)    Met
  x  (red)      Not met yet
  -  (grey)     Can't be predicted ahead of time (random chance, standing
                on a specific tile)
  ?  (grey)     The game couldn't check it

An event marked READY will play the next time you enter its location.

Events that only need friendship with an NPC are listed as that NPC's events.
Scenes the NPC just appears in are in a collapsed "Other scenes featuring..."
section under their name.


HUD tracker (F9)
~~~~~~~~~~~~~~~~
A small box in the top-left corner, with one line per pinned NPC showing their
next event, where it happens, its time and weather, and whether it's ready.


Alerts
~~~~~~
When a pinned NPC's event becomes ready, a message pops up telling you where
to go. Each event alerts once per day.


SETTINGS
--------
Change these in Generic Mod Config Menu, or in config.json after the first
launch:

  Setting        Default    Description
  -------------  ---------  ------------------------------------------------
  OpenMenuKey    F8         Opens the tracker menu
  ToggleHudKey   F9         Shows or hides the HUD tracker
  ShowHud        true       Whether the HUD tracker is visible
  ShowAlerts     true       Pop-up message when a pinned NPC's event is ready
  HudX / HudY    16 / 120   HUD position on screen, in pixels
  HudMaxNpcs     5          Most pinned NPCs shown on the HUD

Pinned NPCs are saved separately for each save file.


CONSOLE COMMANDS
----------------
Type these in the SMAPI console window:

  net_dump <npc>   Print every event for an NPC, with each requirement's
                   status and raw code
  net_export       Write every event and its status to exports/events.json
                   in the mod folder
  net_reindex      Re-read event data


KNOWN LIMITATIONS
-----------------
  * Some mods add an event only after a story flag or quest is done. Those
    events appear in the tracker from that point on, not before.
  * Events started some other way than entering a location (clicking a tile,
    using an item, a trigger action) are marked "special trigger". The
    tracker can't predict when they'll happen.
  * Requirement types the mod doesn't recognize are shown as their raw code.
    Whether they're met is still checked correctly.
  * Split-screen co-op isn't supported. The menu and HUD show the main
    player's events only.


BUILDING FROM SOURCE
--------------------
Requires the .NET 8 SDK or later:

    dotnet build -c Release

This installs the mod into your Mods folder and creates a release zip in
bin/Release/net6.0/.
