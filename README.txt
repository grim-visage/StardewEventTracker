================================================================================
                             STARDEW EVENT TRACKER
                     Event tracking for Stardew Valley 1.6
================================================================================

Events are easy to miss. Many only happen in one place, at a certain time,
in certain weather, or after another event. I created Stardew Event Tracker
to show every event you haven't seen yet, what you still need to do, and
where and when to be so it plays.

FEATURES

  Event tracking
    * Every unseen event, grouped by NPC or location
    * Each requirement marked as met or unmet, in plain language, including
      hidden story flags, letters, quests and special orders
    * Live status: available now, later today, a different day, or what
      still needs doing first
    * Accounts for shop hours, door access, festivals, Green Rain and NPC
      schedules, and checks tomorrow's forecast
    * Walking time from your current position

  Staying on schedule
    * Pin NPCs and story events to follow; partners are pinned
      automatically
    * On-screen tracker with a compact layout
    * Reminders before an event can start, when it's time to leave, and
      when it becomes available
    * Map markers where pinned events can happen today
    * Loved gifts you own for each NPC, shown in the tracker menu, and
      whether you can give one today

  On-screen tracker themes
    * Community Center, with Junimos who cheer when an event can happen
    * Joja "Social Planner" and Spirit's Eve
    * Spring, Summer, Fall and Winter in light or dark, or Seasonal to
      follow the calendar

  Social tab integration (the game's own Social tab in the Esc menu)
    * NPC portraits in the tracker that open the NPC's profile on the
      Social tab
    * Open the tracker or pin an NPC straight from the Social tab

  Compatibility and options
    * Includes events from content mods such as Stardew Valley Expanded,
      Ridgeside Village and East Scarp
    * Optional spoiler-free mode for first playthroughs
    * Split-screen co-op support
    * In-game settings through Generic Mod Config Menu

The mod is currently available in English. Translations are welcome; see
"Translations" below.


REQUIREMENTS
------------
  * Stardew Valley 1.6 or later
  * SMAPI 4.0 or later (https://smapi.io)
  * Optional: Generic Mod Config Menu for in-game settings
    (https://www.nexusmods.com/stardewvalley/mods/5098). The tracker menu's
    Settings button needs version 1.14.1 or later.


INSTALLATION
------------
  1. Install SMAPI if you haven't already.
  2. Unzip this archive into your "Stardew Valley/Mods" folder. You should end
     up with "Mods/StardewEventTracker/".
  3. Launch the game through SMAPI.


HOW TO USE IT
-------------
  Key                 Action
  ------------------  ---------------------------------------------------
  F2                  Open or close the tracker menu (on the game's Social
                      tab: open it at the NPC under your cursor)
  Shift + F2          Show or hide the on-screen tracker
  Ctrl + F2           Pin or unpin the NPC under your cursor (in the world
                      or on the game's Social tab)
  Right-click + drag  Move the on-screen tracker


Pinning
~~~~~~~
Pin the NPCs and story events you want to follow. Reminders, the on-screen
tracker and map markers cover what you've pinned. You can pin someone by:

  * clicking "Pin" next to their name on the Hearts tab
  * pointing at them in the world and pressing Ctrl + F2
  * hovering their row on the game's Social tab (Esc menu) and pressing
    Ctrl + F2

Your spouse, roommate and anyone you're dating are pinned automatically. If
you unpin one of them, they stay unpinned.

Story events aren't tied to one NPC, so you pin them one at a time: click
"Pin" next to the event on the Story tab. They're unpinned once you've seen
them.


Tracker menu (F2)
~~~~~~~~~~~~~~~~~
  * Pinned     Pending events for the NPCs and story events you've pinned,
               and a "Today's messages" list of every reminder from today.
  * Hearts     Every NPC with heart events you haven't seen, with their
               portrait and hearts. Click the arrow or the row to expand
               it, or the portrait or name to open their profile on the
               game's Social tab (close it to come back).
  * Story      Story events (no friendship needed), grouped by location,
               with the characters who appear in each one.
  * Completed  Events you've already seen. Switch between Hearts and Story
               at the top. Names in green have every event seen.

Each NPC's events are listed in story order, followed by a "Next up" line for
the next locked one, e.g. "Next up: 4-heart event at Leah's Cottage (1 more
heart to go)". Only the event to follow next shows its details; click any
event's heading to open or close it.

Each event also shows:
  * how far away it is on foot, when it can happen today
  * what seeing it leads to ("Leads to: ..."), for event chains
  * a Snooze button, for pinned events: no reminders for that event until
    tomorrow, and the on-screen tracker moves on to the next one. Click
    Wake to undo it.

Events that can no longer happen (you saw another version, or an earlier
choice ruled them out) aren't listed; the NPC's entry just says how many.


Search and filters
~~~~~~~~~~~~~~~~~~
Type in the search box to find an NPC, a location, an event ID, or anything
in an event's requirements. Press Enter or Escape when you're done.

Filter buttons next to the search box (combine as many as you like):

  Button          Shows
  -------------   ---------------------------------------------------------
  Available now   Every requirement is met and it's the right time of day
  Today           Available now, or it can start later today
  Not today       Everything's done, it just can't happen today: waiting
                  on the weather, day, season, Green Rain or a festival,
                  or today's time window has passed
  Show locked     Also list events that need more hearts


Event status
~~~~~~~~~~~~
Each event shows where it stands right now:

  Status                Meaning
  --------------------  ------------------------------------------------
  Available now         Walk into the location and it plays
  Later today           Everything is met; it can start later today
  Plays when you next   A story event with no requirements: it plays the
  go there              next time you enter that place
  Wait for ...          Needs a different day ("a rainy day"), or an NPC
                        to arrive ("Alex to be at the Beach")
  No events during      Green Rain stops every location event that day
  Green Rain
  Festival here today   A festival is on at that location
  The ... takes over    A market or festival replaces the location all
  ... today             day, e.g. the Night Market on the Beach
  Doors are locked for  On festival days every shop and house in the
  today's festival      valley is locked all day
  Missed today's        Today's time window has passed
  window
  Not yet: ...          Something still needs doing first, e.g. "see
                        Leah's 4-heart event first"
  Special trigger       Started some other way, not by visiting the
                        location
  Locked: ...           Needs more hearts, or to be dating or married,
                        e.g. "marry Leah first"

When only the day is in the way, the status also checks tomorrow's forecast:
"(tomorrow works!)" or "(not tomorrow either)".

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

Events inside shops and houses also account for the door, shown as "Door"
lines under the event: opening hours (the Town Key opens it any time), hearts
the resident wants before letting you in (Leah's cottage needs 2), and
special doors like the Community Center, Caroline's Sunroom and Willy's back
room.

Many events (especially in mods) depend on hidden story flags. The tracker
explains them in plain words instead of showing an internal ID, e.g.:

  * Get the letter 'Invitation from Jade'
  * Complete the special order 'Luma's Slime Eggs'
  * Wait 4 days after Cirrus's 1-heart event
  * Complete the quest 'Stone for a garden'
  * Talk to Kataryna

Some areas that mods add have no way in at first, e.g. Stardew Valley
Expanded's Highlands. Events there show "Not yet" with a "Getting there" line
until a way in opens up.

Events only start when you arrive at a location. If you're already there
when one becomes available, step out and come back in.


On-screen tracker (Shift + F2)
~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
A small box showing each pinned NPC's next event and each pinned story event,
where it happens, and a short status. In config.json its settings start with
"Hud" (short for heads-up display).

  Status              Meaning
  ------------------  -----------------------------------------------------
  Time to visit!      The event can happen right now ("Something's
                      happening!" for story events)
  Leave now           The walk there takes about as long as you have left
  Head out soon       It starts within your shortest reminder (1 hour by
                      default)
  Get ready           It starts within your longest reminder (2 hours by
                      default)
  Later today         It starts later today
  Wait for ...        Needs a different day, e.g. "a rainy day"
  Green Rain today    No events today because of Green Rain
  Festival here       A festival blocks it today
  today
  Missed today        Today's time window has passed
  Not yet: ...        The first thing you still need, e.g. "see Leah's
                      4-heart event first"
  N more hearts to    The next event is locked, e.g. "Abigail: 1 more heart
  unlock ...          to unlock her 8-heart event", or "next - marry Leah
                      first"
  snoozed until       You snoozed the NPC's events for today
  tomorrow
  all caught up       You've seen all of the NPC's events

Entries are sorted by urgency, soonest first. Set "Tracker order" to
Alphabetical for a fixed list.

Tracking a lot? Set "Tracker layout" to Compact: every entry takes one short
line, with the colour saying how soon, e.g.

  Abigail at the Mountain: Leave now (40m walk)
  Sebastian at the Carpenter's Shop: starts 6:00 pm (in 3h)
  Leah: 1 more heart to go

The on-screen tracker shows up to 5 entries (HudMaxNpcs). The rest are summed
up in a last line, e.g. "+2 more pinned (F2 to see all)", which turns green
if one of them needs you now.

The on-screen tracker fades almost all the way out while you or the mouse are
under it, so it never hides what's behind it. Turn this off with "Fade
tracker when in the way", or keep it visible under the mouse with "Also fade
on mouse hover".

Pick a look with the "Tracker theme" setting:
  * Community Center  The bundle book's title tab, with three Junimos perched
                      on the box who cheer when an event can happen now.
                      (The default.)
  * Joja              A dark "Social Planner" in Joja Cola blues, with the
                      Joja logo in the corner and blinking status lights
  * Spirit's Eve      A night-purple box with a ghost and a flickering
                      jack-o'-lantern
  * Seasonal          Follows the calendar: Spring, Summer, Fall or Winter
  * Spring            Vines and blossoms, butterflies on the title
  * Summer            Vines and yellow flowers, sunflowers
  * Fall              Fallen leaves on top, pumpkins on the title
  * Winter            Snow and icicles, crystal fruit

The season themes come in light and dark: pick one with "Tracker mode".


Map markers
~~~~~~~~~~~
Open the map to see a heart where a pinned event can happen today. It bobs
when the event can happen right now and is faded when it's later today.
Hover a heart for details.


Reminders
~~~~~~~~~
For NPCs and story events you've pinned, the mod sends reminders so you have
time to get there:

  * Morning heads-up   Which events can happen today
  * Before it starts   2 hours and 1 hour before an event can start, by
                       default (in-game time)
  * Leave now          When the walk there (plus 10 minutes to spare) takes
                       about as long as you have left
  * When it's ready    When the event can happen right now
  * Evening look-ahead From 6:00 pm, events that should work tomorrow

Each reminder is sent once per day. Walk estimates follow the map's real
routes and the actual clock speed, so time mods are accounted for. Missed
one? The Pinned tab lists every reminder from today.

Turn on "Remind me about unpinned events" to also hear about unpinned events
with a start time ("before it starts" and "ready" reminders only).


Loved gifts
~~~~~~~~~~~
In the tracker menu (F2), each NPC on the Hearts and Pinned tabs shows up
to three items they love that you own: carried, or in any chest, fridge or
Junimo chest. Hover over them to see what they are, where they are, and
whether you can give that NPC a gift today (one a day and two a week;
birthdays and spouses are exceptions). The icons fade when you can't.
Loved gifts appear only in the tracker menu, not on the game's Social tab.

In spoiler-free mode they only show gifts you've already found out the NPC
loves.


Spoiler-free mode
~~~~~~~~~~~~~~~~~
Events that aren't unlocked yet only show their title: no location,
requirements, or what they lead to. Story events that aren't unlocked are
hidden completely. Events you can already do still show everything you need.

The first time you open the menu, it asks whether you want it on. After
that, change it in the settings.


Split-screen co-op
~~~~~~~~~~~~~~~~~~
Each player on the same screen has their own pins, reminders, on-screen
tracker, search and filters.


SETTINGS
--------
Change these in Generic Mod Config Menu (the Settings button in the tracker
menu opens it), or in config.json after the first launch.

  Setting                Default          Description
  ---------------------  ---------------  -----------------------------------
  OpenMenuKey            F2               Opens the tracker menu
  ToggleHudKey           LeftShift + F2   Shows or hides the on-screen
                                          tracker
  PinKey                 LeftControl      Pins or unpins the NPC under the
                         + F2             cursor
  AutoPinPartners        true             Pin your spouse, roommate and
                                          partners automatically
  SpoilerFree            false            Hide details of locked events
  ShowLovedGifts         true             Show loved gifts you own next to
                                          NPCs in the tracker menu
  ShowHud                true             Whether the on-screen tracker is
                                          visible
  HudFade                true             Fade the on-screen tracker while
                                          you or the mouse are under it
  HudFadeOnHover         true             Fade it on mouse hover too
  HudFadeOpacity         3                How much of it shows while
                                          faded, in percent
  ShowMapMarkers         true             Hearts on the map for pinned
                                          events
  StoryReminders         true             Reminders for pinned story events
  UnpinnedReminders      false            Reminders for unpinned events with
                                          a start time
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
  ReminderSound          dwop             Sound for reminders and heads-ups
                                          (Soft pop; "none" for silence)
  AvailableSound         crystal          Sound for "time to visit" messages
                                          (Crystal chime)
  PopupSeconds           10               How long reminder messages stay
                                          on screen, in seconds (3 to 30)
  HudX / HudY            16 / 120         Its position on screen, in pixels
  HudLayout              detailed         "detailed", or "compact" for one
                                          short line per entry
  HudSortOrder           urgency          Its order: "urgency" or
                                          "alphabetical"
  HudTheme               community-center Its look: "community-center",
                                          "joja", "spirits-eve",
                                          "seasonal", "spring", "summer",
                                          "fall" or "winter"
  HudMode                light            "light" or "dark", for the season
                                          themes
  HudMaxNpcs             5                Most entries (NPCs and story
                                          events) shown on it

Pins are saved separately for each save file and each player.


TRANSLATIONS
------------
All of the mod's text is in one file, i18n/default.json. To translate it:

  1. Copy it to a file named after your language, e.g. i18n/de.json,
     i18n/fr.json or i18n/pt-BR.json.
  2. Translate the text on the right of each line. Leave the names on the
     left, and anything in {{double braces}}, as they are.
  3. Start the game in that language to check it.

Share it on GitHub (an issue or pull request) and it'll be included in the
next release.


TROUBLESHOOTING
---------------
If an event's status looks wrong, these commands help. Type them in the
SMAPI console window (the black text window that opens with the game):

  tracker_reindex
      Re-read all event data. Try this first if an event is missing or out
      of date. (It also happens each morning and when mods change events.)
  tracker_dump <name>
      List every event for an NPC (or a location's story events), with each
      requirement and whether it's met. E.g. "tracker_dump Leah".
  tracker_travel <location>
      Show the walk estimate from where you are to a location.
  tracker_reach
      List the areas mods added that you have no way into yet.
  tracker_export
      Save every event and its status to StardewEventTracker-events.json,
      in the same folder as your SMAPI log (the console shows the full
      path). Attach this file to bug reports.

Report bugs on GitHub:
https://github.com/grim-visage/StardewEventTracker/issues

Please include your SMAPI log (https://smapi.io/log) and, if it's about a
specific event, the tracker_dump output for that NPC.


COMPATIBILITY
-------------
  * Data Layers also uses F2, so pressing F2 opens both. Rebind one of them
    in Generic Mod Config Menu.
  * NPC Map Locations and World Maps: map hearts work with both.
  * Event Lookup does a similar job (on the N key). They can run together.
  * Time mods such as It's Stardew Time: walk estimates follow the actual
    clock speed.


KNOWN LIMITATIONS
-----------------
  * Events started some other way than entering a location (clicking a tile,
    using an item, a mod's own code) are marked "special trigger". The
    tracker can't predict when they'll happen.
  * Requirements added by other mods (e.g. Expanded Preconditions Utility)
    that the tracker doesn't recognize read "Other requirement: ..." with
    their raw code. Whether they're met is still checked correctly.
  * Walk estimates count walking and riding only, not minecarts, the bus,
    boats or totems.


CONTRIBUTING
------------
Hint contributions and build instructions are in DEVELOPMENT.md on GitHub.


AI DISCLOSURE
-------------
This mod was built with the help of an AI coding assistant (Claude, by
Anthropic). The design, testing in a real modded playthrough, and ongoing
maintenance are mine.


LICENSE
-------
MIT License. See the LICENSE file. Source code:
https://github.com/grim-visage/StardewEventTracker
