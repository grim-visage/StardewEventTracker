using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>In-game minutes until the time window opens, if the event has one.</summary>
        public int? MinutesUntilStart { get; }

        public EventEvaluation(EventStatus status, IReadOnlyList<ConditionState> states, bool timeOpen, int? minutesUntilStart)
        {
            this.Status = status;
            this.States = states;
            this.TimeOpen = timeOpen;
            this.MinutesUntilStart = minutesUntilStart;
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
            else if (timeOpen)
                status = EventStatus.AvailableNow;
            else if (untilStart > 0)
                status = EventStatus.LaterToday;
            else
                status = EventStatus.MissedToday;

            return new EventEvaluation(status, states, timeOpen, untilStart);
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
