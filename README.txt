================================================================================
                               NPC EVENT TRACKER
================================================================================

A SMAPI mod for Stardew Valley 1.6 that tracks heart events and story events.
For each NPC it shows which events you haven't seen, where and when each one
triggers, and what you still need: hearts, time of day, weather, day of the
week, earlier events, and so on. Pin the NPCs you care about and it reminds
you in time to get there.

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
  Key                 Action
  ------------------  ---------------------------------------------------
  F2                  Open or close the tracker menu
  Shift + F2          Show or hide the HUD tracker
  Ctrl + F2           Pin or unpin the NPC under your cursor (in the world
                      or on the Social tab)
  Shift + drag        Move the HUD tracker with the mouse


Pinning NPCs
~~~~~~~~~~~~
Pin the NPCs you want to follow. Reminders, the HUD tracker and map markers
cover pinned NPCs only. You can pin someone by:

  * clicking "Pin" next to their name on the Hearts tab
  * pointing at them in the world and pressing Ctrl + F2
  * hovering their row on the Social tab and pressing Ctrl + F2

Your spouse, roommate and anyone you're dating are pinned automatically. If
you unpin one of them, they stay unpinned.


Tracker menu (F2)
~~~~~~~~~~~~~~~~~
  * Pinned     Pending heart events for the NPCs you've pinned, plus a
               "Today's messages" list of every reminder from today.
  * Hearts     Every NPC with heart events. Click a name to expand it.
  * Story      Story events (no friendship needed), grouped by location,
               with the characters who appear in each one.
  * Completed  Events you've already seen. Switch between Hearts and Story
               at the top.

Each event also shows:
  * how far away it is on foot, when it can happen today
  * what seeing it leads to ("Leads to: ..."), for event chains
  * a Snooze button, for pinned NPCs' events: no reminders for that event
    until tomorrow, and the HUD moves on to the NPC's next event


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
  Right day       Only the weather, day, season, Green Rain or a festival is
                  in the way today
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
  No events during      Green Rain stops every location event that day
  Green Rain
  Festival here today   A festival is on at that location; events there
                        can't start during festival hours
  Missed today          Today's time window has passed
  Not yet               Something still needs doing first
  Special trigger       Started by the game or a mod some other way, not
                        by visiting the location
  Locked                Needs more hearts

When only the day is in the way, the status also checks tomorrow's weather
forecast and date: "(tomorrow works!)" or "(not tomorrow either)".

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

Events only start when you arrive at a location. If you're already there
when one becomes available, step out and come back in.


HUD tracker (Shift + F2)
~~~~~~~~~~~~~~~~~~~~~~~~
A small box showing each pinned NPC's next event and where it happens, with
a short status:

  Status              Meaning
  ------------------  -----------------------------------------------------
  Time to visit!      The event can happen right now
  Leave now           The walk there takes about as long as you have left
  Head out soon       It starts within your shortest reminder (1 hour by
                      default)
  Get ready           It starts within your longest reminder (2 hours by
                      default)
  Later today         It starts later today
  Wait for ...        Needs a different day, e.g. "a sunny day"
  Missed today        Today's time window has passed
  Not yet             Something still needs doing first

Hold Shift and drag the box to move it.


Map markers
~~~~~~~~~~~
Open the map to see a heart where a pinned NPC's event can happen today. It
bobs when the event can happen right now and is faded when it's later today.
Hover a heart for details. Works with NPC Map Locations and World Maps.


Reminders
~~~~~~~~~
For NPCs you've pinned, the mod sends gentle reminders so you have time to get
there:

  * Morning heads-up   At the start of the day, which events can happen
                       today ("Today looks like a good day to see Abigail at
                       the Mountain...").
  * Before it starts   By default 2 hours and 1 hour before an event can
                       start (in-game time). Choose from 3 hours, 2 hours,
                       1 hour, 30 minutes, and 15 minutes.
  * Leave now          When the walk there (from where you are, on foot or
                       horse, plus 10 minutes to spare) takes about as long
                       as you have left.
  * When it's ready    When the event can happen right now ("It's time to
                       visit Robin at her home, the Carpenter's Shop.").
  * Evening look-ahead From 6:00 pm, events that couldn't happen today but
                       should work tomorrow ("Tomorrow looks rainy...").

Each reminder is sent once per day, plays a sound, and stays on screen for
10 seconds. You can pick the sounds (or turn them off) and the duration in the
settings; picking a sound in Generic Mod Config Menu plays a preview.

Missed one? The Pinned tab has a "Today's messages" list at the top with every
reminder from today.


Spoiler-free mode
~~~~~~~~~~~~~~~~~
Turn on SpoilerFree for a first playthrough. Events that aren't unlocked yet
only show their title: no location, requirements, or what they lead to.
Story events that aren't unlocked are hidden completely.


Split-screen co-op
~~~~~~~~~~~~~~~~~~
Each player on the same screen has their own pins, reminders, HUD, search and
filters, based on their own friendships and seen events.


SETTINGS
--------
Change these in Generic Mod Config Menu, or in config.json after the first
launch:

  Setting                Default          Description
  ---------------------  ---------------  -----------------------------------
  OpenMenuKey            F2               Opens the tracker menu
  ToggleHudKey           LeftShift + F2   Shows or hides the HUD tracker
  PinKey                 LeftControl      Pins or unpins the NPC under the
                         + F2             cursor
  AutoPinPartners        true             Pin your spouse, roommate and
                                          partners automatically
  SpoilerFree            false            Hide details of locked events
  ShowHud                true             Whether the HUD tracker is visible
  HudDragKey             LeftShift        Hold to drag the HUD tracker
  ShowMapMarkers         true             Hearts on the map for pinned
                                          NPCs' events
  MorningHeadsUp         true             Day-start message about today's
                                          events
  ReminderMinutesBefore  [120, 60]        When to remind you before an
                                          event can start, in in-game
                                          minutes (allowed: 180, 120, 60,
                                          30, 15)
  TravelReminders        true             "Leave now" reminder based on the
                                          walk there
  TravelBufferMinutes    10               Extra minutes added to the walk
  AlertWhenAvailable     true             Message when an event can happen
                                          now
  TomorrowHeadsUp        true             Evening look-ahead to tomorrow
  ReminderSound          newArtifact      Sound for reminders and heads-ups
                                          ("none" for silence)
  AvailableSound         questcomplete    Sound for "time to visit" messages
  PopupSeconds           10               How long reminder messages stay
                                          on screen, in seconds (3 to 30)
  HudX / HudY            16 / 120         HUD position on screen, in pixels
  HudMaxNpcs             5                Most pinned NPCs shown on the HUD

Pinned NPCs are saved separately for each save file and each player.


TRANSLATIONS
------------
All text is in i18n/default.json. To translate the mod, copy it to a file
named after your language (for example i18n/de.json or i18n/pt-BR.json) and
translate the values. Keep the {{tokens}} as they are.


CONSOLE COMMANDS
----------------
Type these in the SMAPI console window:

  net_dump <name>       Print every heart event for an NPC (or story event
                        for a location), with each requirement's status and
                        raw code
  net_travel <location> Estimate the walk from you to a location
  net_export            Write every event and its status to
                        exports/events.json in the mod folder
  net_reindex           Re-read event data


KNOWN LIMITATIONS
-----------------
  * Some mods add an event only after a story flag or quest is done. Those
    events appear in the tracker from that point on, not before.
  * Events started some other way than entering a location (clicking a tile,
    using an item, a trigger action) are marked "special trigger". The
    tracker can't predict when they'll happen.
  * Requirement types the mod doesn't recognize are shown as their raw code.
    Whether they're met is still checked correctly.
  * Reminders, the HUD and map markers cover pinned NPCs, not story events.
  * Walking estimates count walking and riding only, not minecarts, the bus,
    boats or totems.


BUILDING FROM SOURCE
--------------------
Requires the .NET 8 SDK or later:

    dotnet build -c Release

This installs the mod into your Mods folder and creates a release zip in
bin/Release/net6.0/. Close the game first: replacing the mod while the game
is running can crash it.
