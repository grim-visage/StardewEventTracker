================================================================================
                             STARDEW EVENT TRACKER
================================================================================

A SMAPI mod for Stardew Valley 1.6 that tracks heart events and story events.
For each NPC it shows which events you haven't seen, where and when each one
triggers, and what you still need: hearts, time of day, weather, day of the
week, earlier events, and so on. Pin the NPCs and story events you care about
and it reminds you in time to get there.

It reads the game's live event data, so events added by mods like Stardew
Valley Expanded, Ridgeside Village, East Scarp, and custom NPC mods are
included automatically.

FIRST PLAYTHROUGH? The tracker shows where every event happens and what it
needs, including ones you haven't unlocked yet. If you'd rather discover them
yourself, turn on spoiler-free mode (see "Spoiler-free mode" below). The
first time you open the menu, it asks whether you want it on.


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
     up with "Mods/StardewEventTracker/".
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


Pinning
~~~~~~~
Pin the NPCs and story events you want to follow. Reminders, the HUD tracker
and map markers cover what you've pinned. You can pin someone by:

  * clicking "Pin" next to their name on the Hearts tab
  * pointing at them in the world and pressing Ctrl + F2
  * hovering their row on the Social tab and pressing Ctrl + F2

Your spouse, roommate and anyone you're dating are pinned automatically. If
you unpin one of them, they stay unpinned.

Story events aren't tied to one NPC, so you pin them one at a time: click
"Pin" next to the event on the Story tab. Pinned story events show in the
Pinned tab, on the HUD and on the map, and get reminders too (turn that off
with the StoryReminders setting). They're unpinned once you've seen them.


Tracker menu (F2)
~~~~~~~~~~~~~~~~~
  * Pinned     Pending heart events for the NPCs you've pinned, your pinned
               story events, and a "Today's messages" list of every
               reminder from today.
  * Hearts     Every NPC with heart events, with their hearts shown like on
               the game's Social tab ("Not met" until you meet them).
               Click a name to expand it.
  * Story      Story events (no friendship needed), grouped by location,
               with the characters who appear in each one.
  * Completed  Events you've already seen. Switch between Hearts and Story
               at the top.

Each NPC's events are listed in story order (when two need the same hearts,
the one with fewer requirements left comes first), followed by a "Next up" line
for the next locked one, e.g. "Next up: 4-heart event at Leah's Cottage (1 more
heart to go)" or "(marry Leah first)". Only the event to follow next shows its
details; click any event's heading to open or close it.

Each event also shows:
  * how far away it is on foot, when it can happen today
  * what seeing it leads to ("Leads to: ..."), for event chains
  * a Snooze button, for pinned events: no reminders for that event until
    tomorrow, and the HUD moves on to the next one. Click Wake to undo it.


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
  Not today       Everything's done, it just can't happen today: waiting
                  on the weather, day, season, Green Rain or a festival,
                  or today's time window has passed
  Show locked     Also list events that need more hearts

A search lists the matching groups closed; click a group's heading to open
it. The filter buttons open matching groups automatically, and you can click
a heading to close one again.


Event status
~~~~~~~~~~~~
Each event shows where it stands right now:

  Status                Meaning
  --------------------  ------------------------------------------------
  Available now         Walk into the location and it plays
  Later today           Everything is met; it can start later today
  Plays when you go     A story event with no requirements (other than
  there                 "hasn't seen ..." ones): it plays the next time
                        you enter that place, once you can get there
                        (often the first visit to a new area)
  Wait for ...          Needs a different day, e.g. "a sunny day"
  No events during      Green Rain stops every location event that day
  Green Rain
  Festival here today   A festival is on at that location; events there
                        can't start during festival hours
  Doors are locked for  On festival days every shop and house in the
  today's festival      valley is locked all day
  Missed today          Today's time window has passed
  Not yet: ...          Something still needs doing first. Names the first
                        thing, e.g. "see Leah's 4-heart event first"
  Special trigger       Started by the game or a mod some other way, not
                        by visiting the location
  Locked: ...           Needs more hearts, or to be dating or married.
                        Names what's needed, e.g. "marry Leah first"

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

Events inside shops and houses also account for the door: its opening hours
(the Town Key opens it any time), any hearts the resident requires before
letting you in (Leah's cottage needs 2 hearts, for example), and festival days,
when every shop and house in the valley is locked. The game's special doors
count too: the Community Center (locked until Lewis opens it, or you join
Joja), Caroline's Sunroom (2 hearts with Caroline) and Willy's back room, the
Boat Tunnel (locked until his invitation letter arrives). These show as "Door"
lines under the event.

When a door needs hearts with someone and they're in the event, it's their
heart event even if the event itself doesn't ask for hearts. Caroline's
2-heart event in the Sunroom is listed under Hearts for that reason.

Many events (especially in mods) depend on hidden "story flags" and
conversation topics. Where the game's data says how they're set, the tracker
explains them instead of showing an internal ID:

  * a letter:          Get the letter 'Invitation from Jade' (sent once you
                       have 10 hearts with Jade and are dating Jade)
  * a special order:   Complete the special order 'Luma's Slime Eggs': Deliver
                       10 Slime Eggs to the fridge in the Orchard House
  * an automatic rule: Once you have Cherry Pit
  * a cooldown:        Wait 4 days after Cirrus's 1-heart event
  * another event:     See Corwin's 6-heart event (including choices you
                       make during it)
  * a quest:           Complete the quest 'Stone for a garden'
  * a conversation:    Talk to Kataryna

A few flags aren't set by anything in the game's data (the game's or a mod's
own code, or content that isn't finished yet). For those, assets/hints.json
has short hand-written hints researched from the game code, mod wikis and
handbooks, e.g. "Complete Jade's quest 'Iron Bar for Jade' (from the East
Scarp wiki)". A hint is also used when the data's explanation is unclear, and
always wins. Any flag still unexplained is shown as words, e.g. "Story flag:
Duskspire defeated". The same file explains requirements that other mods add
to the game, e.g. Ridgeside's "Arrive riding your horse"; ones it doesn't
know read "Other requirement: ...". Hint contributions are welcome on GitHub:
keep them short, in your own words, and say where the information came from.

Some mods (Ridgeside Village, for example) start certain events from their own
code and give them a requirement that can never be met, so the game won't
also start them on its own. The tracker recognizes these and marks them
"special trigger" instead of showing that requirement as missing.

Some areas that mods add have no way in at first, e.g. Stardew Valley
Expanded's Highlands, which open up once Marlon lets you use his boat. Events
there show "Not yet" with a "Getting there" line until a way in exists, and
assets/hints.json can say how to open it up.

Events only start when you arrive at a location. If you're already there
when one becomes available, step out and come back in.


HUD tracker (Shift + F2)
~~~~~~~~~~~~~~~~~~~~~~~~
A small box showing each pinned NPC's next event and each pinned story event,
where it happens, and a short status. For an NPC, "next" means an event you
can act on today if there is one, otherwise the earliest one in their story.

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
  Green Rain today    No events today because of Green Rain
  Festival here       A festival blocks it today (or every shop and house
  today               is locked for one)
  Missed today        Today's time window has passed
  Not yet - ...       The first thing you still need, e.g. "1 more heart
                      with Leah to get in" or "see Leah's 4-heart event
                      first", plus how many more
  N more hearts to    The next event is locked, e.g. "Abigail: 1 more heart
  unlock ...          to unlock her 8-heart event"; or "next - marry Leah
                      first" for dating and marriage events
  snoozed until       You snoozed the NPC's events for today
  tomorrow
  all caught up       You've seen all of the NPC's events

Events that can happen today also show how far away they are ("40m away").
Statuses held up by the day add "(tomorrow works!)" when the forecast says
tomorrow is fine. If more events are waiting, the first line ends in
"(+N more)".

Entries are sorted by urgency: things you can do now first, then time to set
off, later today, not today, and finally locked or caught-up entries. Within
each group, whatever starts soonest comes first. Entries with nothing to do
today take a single line. Prefer a fixed list? Set "HUD order" to
Alphabetical (NPCs A-Z, then story events).

The HUD shows up to 5 entries (HudMaxNpcs), and fewer if they wouldn't fit on
screen. Anything left over is summed up in a last line, e.g. "+2 more pinned
(F2 to see all)", which turns green if one of them can happen now or it's
time to set off.

Hold Shift and drag the box (or its title) to move it.

The HUD fades almost all the way out while you walk under it or the mouse is
over it, so it never hides what's behind it. Holding Shift brings it back so
you can move it. Turn this off with "Fade HUD when in the way" (HudFade).

Pick a look with the "HUD theme" setting:
  * Community Center  The title on the bundle book's title tab, with three
                      Junimos perched on the box. They cheer when an event
                      can happen right now. (The default.)
  * Joja              A dark mode: Joja's "Social Planner" and the Joja
                      logo on a banner in Joja Cola blues, over a navy box.
                      The logo's sunburst sparkles, and status lights blink
                      like an office computer's (the green one flashes
                      when an event can happen right now)
  * Spirit's Eve      A dark mode: a night-purple box with light text, and
                      a ghost and a flickering jack-o'-lantern by the title
  * Seasonal          Follows the calendar: Spring, Summer, Fall or Winter
  * Spring            A frame of vines and blossoms, butterflies on the title
  * Summer            A frame of vines and yellow flowers, sunflowers
  * Fall              Fallen leaves piled on top, pumpkins on the title
  * Winter            Snow on top, short icicles underneath, crystal fruit

The season themes come in a light and a dark mode: pick one with the
"HUD mode" setting.


Map markers
~~~~~~~~~~~
Open the map to see a heart where a pinned event can happen today. It
bobs when the event can happen right now and is faded when it's later today.
Hover a heart for details. Works with NPC Map Locations and World Maps.


Reminders
~~~~~~~~~
For NPCs and story events you've pinned, the mod sends gentle reminders so you
have time to get there:

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

Messages fit the situation: they use the NPC's name and pronouns, mention
their home when the event is there, and change with rain and the time of day
("On a rainy day like this, Abigail might be at the Mountain.").

The leave-now estimate follows the map's real routes. The mod measures how
fast the in-game clock is actually running, so mods that slow time down or
speed it up (like It's Stardew Time) are accounted for. If your clock runs at
different speeds indoors and outdoors, the estimate follows where you've been
recently, so it's approximate.

Each reminder is sent once per day, plays a sound, and stays on screen for
10 seconds. You can pick the sounds (or turn them off) and the duration in the
settings; picking a sound in Generic Mod Config Menu plays a preview.

Missed one? The Pinned tab has a "Today's messages" list at the top with every
reminder from today.


Spoiler-free mode
~~~~~~~~~~~~~~~~~
Turn on SpoilerFree for a first playthrough. Events that aren't unlocked yet
only show their title: no location, requirements, or what they lead to.
Story events that aren't unlocked are hidden completely. Events you can
already do still show everything you need to find them.

The first time you open the menu, a message asks whether you want it on.
After that, change it any time in Generic Mod Config Menu or config.json.


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
  HudFade                true             Fade the HUD while you or the
                                          mouse are under it
  ShowMapMarkers         true             Hearts on the map for pinned
                                          events
  StoryReminders         true             Reminders for pinned story events
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
  HudSortOrder           urgency          HUD order: "urgency" or
                                          "alphabetical"
  HudTheme               community-center HUD look: "community-center",
                                          "joja", "spirits-eve",
                                          "seasonal", "spring", "summer",
                                          "fall" or "winter"
  HudMode                light            "light" or "dark", for the season
                                          themes
  HudMaxNpcs             5                Most entries (NPCs and story
                                          events) shown on the HUD

Pins are saved separately for each save file and each player.


TRANSLATIONS
------------
All text is in i18n/default.json. To translate the mod, copy it to a file
named after your language (for example i18n/de.json or i18n/pt-BR.json) and
translate the values. Keep the {{tokens}} as they are.


CONSOLE COMMANDS
----------------
Type these in the SMAPI console window:

  tracker_dump <name>
      Print every heart event for an NPC (or story event for a location),
      with each requirement's status and raw code.
  tracker_travel <location>
      Estimate the walk from you to a location.
  tracker_export
      Write every event and its status to exports/events.json in the mod
      folder, with a count of every NPC in your game (how many you can
      befriend and how many have heart events).
  tracker_reach
      List the areas mods added that there's no way into yet. Events there
      show "Not yet" until a way in opens up.
  tracker_reindex
      Re-read event data. The tracker also does this by itself each morning
      and whenever a mod changes events, letters or dialogue.


COMPATIBILITY
-------------
  * Data Layers also uses F2 (and Ctrl to switch layers), so pressing F2
    opens both. Rebind one of them in Generic Mod Config Menu.
  * NPC Map Locations and World Maps: map hearts work with both.
  * Event Lookup does a similar job (on the N key). They can run together.
  * Expanded Preconditions Utility and other mods that add event
    requirements: those requirements are still checked, but may be shown as
    raw code.
  * Time mods such as It's Stardew Time: walk estimates follow the actual
    clock speed.
  * Content packs that add NPCs, locations or events are picked up
    automatically.


KNOWN LIMITATIONS
-----------------
  * Some mods add an event only after a story flag or quest is done. Those
    events appear in the tracker from that point on, not before.
  * Events started some other way than entering a location (clicking a tile,
    using an item, a trigger action) are marked "special trigger". The
    tracker can't predict when they'll happen.
  * Requirement types the mod doesn't recognize are shown as their raw code.
    Whether they're met is still checked correctly.
  * Reminders, the HUD and map markers only cover what you've pinned.
  * Walking estimates count walking and riding only, not minecarts, the bus,
    boats or totems.


ABOUT THIS MOD
--------------
This mod was built with the help of an AI coding assistant (Claude, by
Anthropic). The design, testing in a real modded playthrough, and ongoing
maintenance are mine. If something doesn't work, please report it on GitHub:
https://github.com/grim-visage/StardewEventTracker/issues


LICENSE
-------
MIT License. See the LICENSE file. Source code:
https://github.com/grim-visage/StardewEventTracker


BUILDING FROM SOURCE
--------------------
Requires the .NET 8 SDK or later:

    dotnet build -c Release

This installs the mod into your Mods folder and creates a release zip in
bin/Release/net6.0/. Close the game first: replacing the mod while the game
is running can crash it.
