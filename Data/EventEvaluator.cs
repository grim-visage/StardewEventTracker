using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace NpcEventTracker.Data
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

        public EventEvaluation(EventStatus status, IReadOnlyList<ConditionState> states, bool timeOpen, int? minutesUntilStart, int? startTime = null, FestivalInfo? festival = null, bool? worksTomorrow = null)
        {
            this.Status = status;
            this.States = states;
            this.TimeOpen = timeOpen;
            this.MinutesUntilStart = minutesUntilStart;
            this.StartTime = startTime;
            this.Festival = festival;
            this.WorksTomorrow = worksTomorrow;
        }

        /// <summary>Requirements that aren't met, not counting the time window.</summary>
        public int UnmetCount => this.States.Count(s => s is ConditionState.Unmet or ConditionState.Unknown);
    }

    internal static class EventEvaluator
    {
        public static EventEvaluation Evaluate(EventInfo evt)
        {
            int? untilStart = evt.Window?.MinutesUntilStart(Game1.timeOfDay);
            if (evt.Seen)
                return new EventEvaluation(EventStatus.Seen, evt.Conditions.Select(_ => ConditionState.Met).ToArray(), true, untilStart);

            GameLocation location = Game1.getLocationFromName(evt.LocationName) ?? Game1.currentLocation;
            var states = new ConditionState[evt.Conditions.Count];
            FestivalInfo? festival = CalendarInfo.GetFestivalAt(evt.LocationName, SDate.Now());
            int? startTime = evt.Window?.Start;
            bool timeOpen = true, locked = false, unreachable = false, progressUnmet = false, calendarUnmet = false;

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

                states[i] = state;
                if (state == ConditionState.Unknown)
                    progressUnmet = true;
                if (state != ConditionState.Unmet)
                    continue;

                if (condition.Is("Friendship") && !condition.Negated)
                    locked = true;
                else if (condition.Is("SawEvent") && condition.Negated)
                    unreachable = true;
                else if (condition.Is("Year") && !condition.Negated && condition.Args.FirstOrDefault() == "1")
                    unreachable = true;
                else if (condition.Category == ConditionCategory.Calendar)
                    calendarUnmet = true;
                else
                    progressUnmet = true;
            }

            EventStatus status;
            if (unreachable)
                status = EventStatus.Unreachable;
            else if (evt.IsSpecial)
                status = EventStatus.Special;
            else if (locked)
                status = EventStatus.Locked;
            else if (progressUnmet)
                status = EventStatus.NotYet;
            else if (calendarUnmet)
                status = EventStatus.WrongDay;
            else if (SafeIsGreenRaining(location))
                status = EventStatus.GreenRain;
            else
                (status, untilStart, startTime) = WithFestival(evt, festival, timeOpen, untilStart);

            // only worth predicting once nothing but the day is in the way
            bool? worksTomorrow = status is EventStatus.WrongDay or EventStatus.GreenRain or EventStatus.FestivalHere or EventStatus.MissedToday
                ? SafeWorksTomorrow(evt, location)
                : null;

            return new EventEvaluation(status, states, timeOpen, untilStart, startTime, festival, worksTomorrow);
        }

        /// <summary>
        /// Applies the game's festival rule: while a festival runs at a location, entering it loads the festival
        /// instead of any event, so the event can only start outside festival hours.
        /// </summary>
        private static (EventStatus Status, int? UntilStart, int? StartTime) WithFestival(EventInfo evt, FestivalInfo? festival, bool timeOpen, int? untilStart)
        {
            int now = Game1.timeOfDay;
            int start = evt.Window?.Start ?? 600;
            int end = evt.Window?.End ?? 2600;

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

            if (timeOpen && now >= start)
                return (EventStatus.AvailableNow, null, start);
            int minutes = TimeWindow.ToMinutes(start) - TimeWindow.ToMinutes(now);
            if (minutes > 0 && start < end)
                return (EventStatus.LaterToday, minutes, start);
            return (timeOpen ? EventStatus.AvailableNow : EventStatus.MissedToday, untilStart, start);
        }

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

        private static ConditionState Check(GameLocation location, string eventId, Precondition condition)
        {
            // SendMail has side effects (it queues a letter), so never run it; Tile/Random can't be predicted.
            if (condition.Is("SendMail") || condition.Is("Tile") || condition.Is("Random") || condition.IsNeverTrue)
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
