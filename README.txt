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
  Key            Action
  -------------  ------------------------------
  F2             Open or close the tracker menu
  Shift + F2     Show or hide the HUD tracker


Tracker menu (F2)
~~~~~~~~~~~~~~~~~
  * Pinned     Pending heart events for the NPCs you've pinned.
  * Hearts     Every NPC with heart events. Click a name to expand it, and
               click "Pin" to track that NPC.
  * Story      Story events (no friendship needed), grouped by location,
               with the characters who appear in each one.
  * Completed  Events you've already seen. Switch between Hearts and Story
               at the top.


Search and filters
~~~~~~~~~~~~~~~~~~
Type in the search box to find an NPC, a location, an event ID, or anything
in an event's requirements. While it's selected, keys go to the search box,
so typing doesn't close the menu. Press Enter or Escape when you're done.

Filter buttons next to the search box (combine as many as you like):

  Button          Shows
  -------------   ---------------------------------------------------------
  Available now   Every requirement is met and it's the right time of day
  Today           Available now, or everything is met and it can start
                  later today
  Right day       Only the weather, day of the week, or season is wrong
                  today
  Show locked     Also list events that need more hearts

While a search or filter is on, matching groups expand automatically.


Event status
~~~~~~~~~~~~
Each event shows where it stands right now:

  Status                Meaning
  --------------------  ------------------------------------------------
  Available now         Walk into the location and it plays
  Later today           Everything is met; it can start later today
  Wait for ...          Needs a different day, e.g. "a sunny day"
  Missed today          Today's time window has passed; try tomorrow
  Not yet               Something still needs doing first
  Special trigger       Started by the game or a mod some other way, not
                        by visiting the location
  Locked                Needs more hearts

Each requirement has a mark showing whether it's met right now:

  Mark          Meaning
  ------------  ---------------------------------------------------------
  +  (green)    Met
  x  (red)      Not met yet
  ~             The time of day it can happen: green while it can happen,
                orange before it starts, grey once it's over for the day
  -  (grey)     Can't be predicted ahead of time (random chance, standing
                on a specific tile)
  ?  (grey)     The game couldn't check it


HUD tracker (Shift + F2)
~~~~~~~~~~~~~~~~
A small box in the top-left corner showing each pinned NPC's next event and
where it happens, with a short status that follows your reminder settings:

  Status              Meaning
  ------------------  -----------------------------------------------------
  Time to visit!      The event can happen right now
  Head out soon       It starts within your shortest reminder (1 hour by
                      default)
  Get ready           It starts within your longest reminder (2 hours by
                      default)
  Later today         It starts later today
  Wait for ...        Needs a different day, e.g. "a sunny day"
  Missed today        Today's time window has passed; try tomorrow
  Not yet             Something still needs doing first


Reminders
~~~~~~~~~
For NPCs you've pinned, the mod sends gentle reminders so you have time to get
there:

  * Morning heads-up   At the start of the day, which events can happen
                       today ("Today looks like a good day to see Abigail at
                       the Mountain...").
  * Before it starts   By default 2 hours and 1 hour before an event can
                       start (in-game time). Choose from 3 hours,
                       2 hours, 1 hour, 30 minutes, and 15 minutes.
  * When it's ready    When the event can happen right now ("It's time to
                       visit Robin at her home, the Carpenter's Shop.").

Each reminder is sent once per day and stays on screen for 10 seconds (change
this with the PopupSeconds setting). Missed one? The Pinned tab has a "Today's
messages" list at the top with every reminder from today.


SETTINGS
--------
Change these in Generic Mod Config Menu, or in config.json after the first
launch:

  Setting                Default          Description
  ---------------------  ---------------  -----------------------------------
  OpenMenuKey            F2               Opens the tracker menu
  ToggleHudKey           LeftShift + F2   Shows or hides the HUD tracker
  ShowHud                true             Whether the HUD tracker is visible
  MorningHeadsUp         true             Day-start message about today's
                                          events
  ReminderMinutesBefore  [120, 60]        When to remind you before an
                                          event can start, in in-game
                                          minutes (allowed: 180, 120, 60,
                                          30, 15)
  AlertWhenAvailable     true             Message when an event can happen
                                          now
  PopupSeconds           10               How long reminder messages stay
                                          on screen, in seconds (3 to 30)
  HudX / HudY            16 / 120         HUD position on screen, in pixels
  HudMaxNpcs             5                Most pinned NPCs shown on the HUD

Pinned NPCs are saved separately for each save file.


CONSOLE COMMANDS
----------------
Type these in the SMAPI console window:

  net_dump <name>  Print every heart event for an NPC (or story event for a
                   location), with each requirement's status and raw code
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
  * Reminders are only sent for pinned NPCs, not for story events.
  * Split-screen co-op isn't supported. The menu and HUD show the main
    player's events only.


BUILDING FROM SOURCE
--------------------
Requires the .NET 8 SDK or later:

    dotnet build -c Release

This installs the mod into your Mods folder and creates a release zip in
bin/Release/net6.0/.
