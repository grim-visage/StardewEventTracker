using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace StardewEventTracker.Data
{
    internal enum ConditionState
    {
        Met,
        Unmet,

        /// <summary>The time window: shown as a schedule (open, opens later, closed) rather than met/unmet.</summary>
        Soft,

        /// <summary>Can't be judged ahead of time (standing on a tile, random chance); doesn't block availability.</summary>
        Neutral,

        /// <summary>The game threw while checking it.</summary>
        Unknown
    }

    /// <summary>Where an unseen event stands right now. The order is also the display sort order.</summary>
    internal enum EventStatus
    {
        /// <summary>Everything is met and the time window is open: entering the location plays it.</summary>
        AvailableNow,

        /// <summary>Everything is met except the time window, which opens later today.</summary>
        LaterToday,

        /// <summary>
        /// A story event with no real requirements: it plays the next time the player enters its location, which may
        /// not be reachable yet (e.g. Ginger Island or a modded area's intro). Not treated as available now.
        /// </summary>
        OnEntry,

        /// <summary>Progress requirements are met; only weather, day or season is wrong today.</summary>
        WrongDay,

        /// <summary>Green Rain is falling, which stops every location event for the day.</summary>
        GreenRain,

        /// <summary>A festival at the event's location covers the time it could start.</summary>
        FestivalHere,

        /// <summary>Everything is met except the time window, which has already closed today.</summary>
        MissedToday,

        /// <summary>A non-friendship progress requirement isn't met (earlier event, mail flag, item...).</summary>
        NotYet,

        /// <summary>Can't be reached on a normal entry (it only sends mail, or a mod's code starts it).</summary>
        Special,

        /// <summary>Not enough friendship with the owner yet.</summary>
        Locked,

        /// <summary>Blocked for good, e.g. an alternate version of the event was already seen.</summary>
        Unreachable,

        Seen
    }

    internal sealed class EventEvaluation
    {
        public EventStatus Status { get; }
        public IReadOnlyList<ConditionState> States { get; }

        /// <summary>Whether the current time is inside the event's time window (true if it has none).</summary>
        public bool TimeOpen { get; }

        /// <summary>In-game minutes until the event can start, if it can't yet (its time window, or a festival here ending).</summary>
        public int? MinutesUntilStart { get; }

        /// <summary>The time the event can start today (HHMM), accounting for festivals at its location.</summary>
        public int? StartTime { get; }

        /// <summary>The festival at the event's location today, if any.</summary>
        public FestivalInfo? Festival { get; }

        /// <summary>Whether the calendar, weather and festivals allow it tomorrow (null if unpredictable or not relevant).</summary>
        public bool? WorksTomorrow { get; }

        /// <summary>The locked door into the event's location, if it's behind one.</summary>
        public DoorState? Door { get; }

        /// <summary>The door's hours and the event's time window never overlap, so it can't start without the Town Key.</summary>
        public bool DoorNeverOpen { get; }

        /// <summary>The event is in an area a mod added that there's no way into yet (see <see cref="AreaAccess"/>).</summary>
        public bool CantReach { get; }

        /// <summary>When the NPC the event needs there should be there today (HHMM), from their schedule; null if it needs nobody or that's unknown.</summary>
        public TimeWindow? NpcStay { get; }

        public EventEvaluation(EventStatus status, IReadOnlyList<ConditionState> states, bool timeOpen, int? minutesUntilStart, int? startTime = null, FestivalInfo? festival = null, bool? worksTomorrow = null, DoorState? door = null, bool doorNeverOpen = false, bool cantReach = false, TimeWindow? npcStay = null)
        {
            this.NpcStay = npcStay;
            this.CantReach = cantReach;
            this.Door = door;
            this.DoorNeverOpen = doorNeverOpen;
            this.Status = status;
            this.States = states;
            this.TimeOpen = timeOpen;
            this.MinutesUntilStart = minutesUntilStart;
            this.StartTime = startTime;
            this.Festival = festival;
            this.WorksTomorrow = worksTomorrow;
        }

        /// <summary>Requirements that aren't met, not counting the time window.</summary>
        public int UnmetCount =>
            this.States.Count(s => s is ConditionState.Unmet or ConditionState.Unknown)
            + (this.Door is { HeartsOk: false } or { MailOk: false } ? 1 : 0)
            + (this.DoorNeverOpen ? 1 : 0)
            + (this.CantReach ? 1 : 0);
    }

    internal static class EventEvaluator
    {
        public static EventEvaluation Evaluate(EventInfo evt, FlagSources flags)
        {
            int? untilStart = evt.Window?.MinutesUntilStart(Game1.timeOfDay);
            if (evt.Seen)
                return new EventEvaluation(EventStatus.Seen, evt.Conditions.Select(_ => ConditionState.Met).ToArray(), true, untilStart);

            GameLocation location = Game1.getLocationFromName(evt.LocationName) ?? Game1.currentLocation;
            var states = new ConditionState[evt.Conditions.Count];
            FestivalInfo? festival = CalendarInfo.GetFestivalAt(evt.LocationName, SDate.Now());
            int? startTime = evt.Window?.Start;
            DoorState? door = DoorAccess.GetState(evt.LocationName);
            bool timeOpen = true, locked = false, unreachable = false, progressUnmet = false, calendarUnmet = false;
            TimeWindow? npcStay = null;

            for (int i = 0; i < states.Length; i++)
            {
                Precondition condition = evt.Conditions[i];
                ConditionState state = Check(location, evt.Id, condition);

                if (condition.Category == ConditionCategory.Time && state is ConditionState.Met or ConditionState.Unmet)
                {
                    timeOpen &= state == ConditionState.Met;
                    states[i] = ConditionState.Soft;
                    continue;
                }

                // "wait N days after X": the topic isn't active yet only because X hasn't happened, so the wait hasn't started
                if (state == ConditionState.Met && WaitNotStarted(condition, flags))
                    state = ConditionState.Unmet;

                // an NPC who should be there today: when they're there works like a time window
                if (condition.Is("NpcVisibleHere") && !condition.Negated && state is ConditionState.Met or ConditionState.Unmet && NpcStay(condition, evt, state) is { } stay)
                {
                    npcStay = npcStay is { } other ? new TimeWindow(Math.Max(other.Start, stay.Start), Math.Min(other.End, stay.End)) : stay;
                    states[i] = state == ConditionState.Met ? state : ConditionState.Soft;
                    continue;
                }

                states[i] = state;
                if (state == ConditionState.Unknown)
                    progressUnmet = true;
                if (state != ConditionState.Unmet)
                    continue;

                if (!condition.Negated && (condition.Is("Dating") || condition.Is("Spouse")) && condition.Args.Length > 0 && !PreconditionFormatter.IsCharacter(condition.Args[0]))
                    unreachable = true;
                else if (!condition.Negated && (condition.Is("Friendship") || condition.Is("Dating") || condition.Is("Spouse") || condition.Is("Roommate")))
                    locked = true;
                else if (condition.Is("SawEvent") && condition.Negated)
                    unreachable = true;
                else if (condition.Is("Year") && !condition.Negated && condition.Args.FirstOrDefault() == "1")
                    unreachable = true;
                else if (IsMissedChoice(condition, flags))
                    unreachable = true;
                else if (condition.Category == ConditionCategory.Calendar)
                    calendarUnmet = true;
                else
                    progressUnmet = true;
            }

            // the hearts the door needs are this NPC's heart-event requirement (Caroline's Sunroom)
            if (door is { HeartsOk: false } residentDoor && evt.IsHeartEvent && residentDoor.Door.Npc == evt.Owner)
                locked = true;

            // the door's hours, unless the Town Key opens it any time
            (int Open, int Close)? doorHours = door is { AllDay: false } d ? (d.Open, d.Close) : null;
            bool doorNeverOpen = doorHours is { } hours && Math.Max(evt.Window?.Start ?? 600, hours.Open) >= Math.Min(evt.Window?.End ?? 2600, hours.Close);

            // the event can start while the door is open and the NPC is there
            (int Open, int Close)? openHours = doorHours;
            if (npcStay is { } npcHours)
                openHours = doorHours is { } h ? (Math.Max(h.Open, npcHours.Start), Math.Min(h.Close, npcHours.End)) : (npcHours.Start, npcHours.End);

            // a shop shut for the day (Pierre's on Wednesdays) is a matter of waiting for another day
            if (door is { ClosedToday: true })
                calendarUnmet = true;

            // nothing inside matters until there's a way there
            bool cantReach = evt.LocationName != EventIndex.AnywhereKey && !AreaAccess.IsReachable(evt.LocationName);

            EventStatus status;
            if (unreachable)
                status = EventStatus.Unreachable;
            else if (evt.IsSpecial)
                status = EventStatus.Special;
            else if (locked)
                status = EventStatus.Locked;
            else if (progressUnmet || door is { HeartsOk: false } or { MailOk: false } || doorNeverOpen || cantReach)
                status = EventStatus.NotYet;
            else if (calendarUnmet)
                status = EventStatus.WrongDay;
            else if (SafeIsGreenRaining(location))
                status = EventStatus.GreenRain;
            else if (door is { FestivalClosed: true })
                status = EventStatus.FestivalHere;
            else
                (status, untilStart, startTime) = WithFestival(evt, festival, openHours, timeOpen, untilStart);

            if (status == EventStatus.AvailableNow && evt.IsStory && evt.Conditions.All(c =>
                    (c.Category is ConditionCategory.Time or ConditionCategory.Calendar && !c.Is("NpcVisibleHere")) || IsExclusion(c) || c.Is("IsHost") || c.Is("Random") || c.Is("Tile")))
                status = EventStatus.OnEntry;

            // only worth predicting once nothing but the day is in the way
            bool? worksTomorrow = status is EventStatus.WrongDay or EventStatus.GreenRain or EventStatus.FestivalHere or EventStatus.MissedToday
                ? SafeWorksTomorrow(evt, location)
                : null;
            if (worksTomorrow == true && door is { } lockedDoor
                && (DoorAccess.FestivalClosesTomorrow(lockedDoor.Door, CalendarInfo.GetFestival(SDate.Now().AddDays(1))) || DoorAccess.ClosedForTheDay(lockedDoor.Door, SDate.Now().AddDays(1))))
                worksTomorrow = false;

            return new EventEvaluation(status, states, timeOpen, untilStart, startTime, festival, worksTomorrow, door, doorNeverOpen, cantReach, npcStay);
        }

        /// <summary>
        /// Works out when the event can start today: within its time window, while the door into its location is open,
        /// and outside festival hours there (entering during a festival loads the festival instead of any event).
        /// </summary>
        private static (EventStatus Status, int? UntilStart, int? StartTime) WithFestival(EventInfo evt, FestivalInfo? festival, (int Open, int Close)? doorHours, bool timeOpen, int? untilStart)
        {
            int now = Game1.timeOfDay;
            int start = evt.Window?.Start ?? 600;
            int end = evt.Window?.End ?? 2600;
            if (doorHours is { } hours)
            {
                start = Math.Max(start, hours.Open);
                end = Math.Min(end, hours.Close);
            }

            if (festival is { } f && f.Start < end && f.End > start)
            {
                if (CalendarInfo.CoversWindow(f, evt.Window) || (now >= f.Start && f.End >= end))
                    return (EventStatus.FestivalHere, null, null);

                // the usable window starts once the festival is over
                if (start >= f.Start && start < f.End)
                    start = f.End;
                if (now >= f.Start && now < f.End)
                    return (EventStatus.LaterToday, TimeWindow.ToMinutes(f.End) - TimeWindow.ToMinutes(now), f.End);
            }

            if (timeOpen && now >= start && now < end)
                return (EventStatus.AvailableNow, null, start);
            int minutes = TimeWindow.ToMinutes(start) - TimeWindow.ToMinutes(now);
            if (minutes > 0 && start < end)
                return (EventStatus.LaterToday, minutes, start);
            return (EventStatus.MissedToday, untilStart, start);
        }

        /// <summary>
        /// When an "NPC is here" requirement can be met today, from the NPC's schedule: the stay going on now if they're
        /// there, else the next one today, else the last one (they've left for the day). Null if they don't go there
        /// today or their schedule can't be read, so it's a matter of waiting for another day.
        /// </summary>
        private static TimeWindow? NpcStay(Precondition condition, EventInfo evt, ConditionState state)
        {
            if (condition.Args.Length == 0 || NpcPresence.Today(condition.Args[0], evt.LocationName) is not { Count: > 0 } stays)
                return null;

            int now = Game1.timeOfDay;
            if (state == ConditionState.Met)
                return stays.Where(s => s.Start <= now && now < s.End).Cast<TimeWindow?>().FirstOrDefault();

            if (stays.Where(s => s.End > now).Cast<TimeWindow?>().FirstOrDefault() is not { } next)
                return stays[^1];

            // due there by now but not there yet: still on the way
            return next.Start <= now ? next with { Start = Utility.ModifyTime(now, 10) } : next;
        }

        /// <summary>
        /// Whether a "conversation topic isn't active" requirement (a wait after an event) is only met because the event
        /// that starts the topic hasn't been seen yet. Seeing it starts the topic, and the wait, so it isn't really met.
        /// </summary>
        private static bool WaitNotStarted(Precondition condition, FlagSources flags)
        {
            if (!condition.Is("ActiveDialogueEvent") || !condition.Negated || condition.Args.Length == 0)
                return false;
            if (flags.GetTopic(condition.Args[0]) is not { EventKey: { } key })
                return false;

            // event keys are "<location>|<event ID>"
            string eventId = key[(key.IndexOf('|') + 1)..];
            return !Game1.player.eventsSeen.Contains(eventId) && !Game1.player.activeDialogueEvents.ContainsKey(condition.Args[0]);
        }

        /// <summary>A mail flag requirement that an earlier event only sets on a path the player didn't take, e.g. another answer to Leah's question.</summary>
        private static bool IsMissedChoice(Precondition condition, FlagSources flags)
        {
            if (condition.Negated || condition.Args.Length == 0)
                return false;
            Farmer? player = condition.Is("LocalMail") ? Game1.player : condition.Is("HostMail") ? Game1.MasterPlayer : null;
            return player != null && condition.Args.All(flag => flags.IsMissedChoice(flag, player));
        }

        /// <summary>
        /// A "hasn't seen/received/done X" requirement. It's true from the start and only rules the event out later, so
        /// on its own it doesn't stop a first-visit event from playing when you arrive.
        /// </summary>
        private static bool IsExclusion(Precondition condition) => condition.Negated && condition.Category == ConditionCategory.Progress;

        private static bool SafeIsGreenRaining(GameLocation location)
        {
            try
            {
                return location.IsGreenRainingHere();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool? SafeWorksTomorrow(EventInfo evt, GameLocation location)
        {
            try
            {
                return CalendarInfo.WorksTomorrow(evt, location);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A game state query's RANDOM clause (not SYNCED_RANDOM, which is fixed for the day).</summary>
        private static readonly Regex RandomQuery = new(@"\bRANDOM\b", RegexOptions.IgnoreCase);

        private static ConditionState Check(GameLocation location, string eventId, Precondition condition)
        {
            // SendMail has side effects (it queues a letter), so never run it; Tile/Random can't be predicted.
            // A RANDOM query can't be predicted either, and checking it would use up the game's random numbers.
            if (condition.Is("SendMail") || condition.Is("Tile") || condition.Is("Random") || condition.IsNeverTrue
                || (condition.Is("GameStateQuery") && RandomQuery.IsMatch(condition.Raw)))
                return ConditionState.Neutral;

            try
            {
                return Event.CheckPrecondition(location, eventId, condition.Raw) ? ConditionState.Met : ConditionState.Unmet;
            }
            catch (Exception)
            {
                return ConditionState.Unknown;
            }
        }
    }
}
