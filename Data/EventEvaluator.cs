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

        /// <summary>Can't be judged ahead of time (standing on a tile, random chance); doesn't block readiness.</summary>
        Neutral,

        /// <summary>The game threw while checking it.</summary>
        Unknown
    }

    internal enum EventStatus
    {
        /// <summary>Every condition passes right now; entering the location will trigger it.</summary>
        Ready,

        /// <summary>Friendship is high enough, but something else (time, weather, ...) isn't met yet.</summary>
        Pending,

        /// <summary>Not enough friendship with the owner yet.</summary>
        Locked,

        /// <summary>Can't be reached on a normal entry (it only sends mail, or a mod's code starts it).</summary>
        Special,

        /// <summary>Blocked for good, e.g. an alternate version of the event was already seen.</summary>
        Unreachable,

        Seen
    }

    internal sealed class EventEvaluation
    {
        public EventStatus Status { get; }
        public IReadOnlyList<ConditionState> States { get; }

        public EventEvaluation(EventStatus status, IReadOnlyList<ConditionState> states)
        {
            this.Status = status;
            this.States = states;
        }

        public int UnmetCount => this.States.Count(s => s is ConditionState.Unmet or ConditionState.Unknown);
    }

    internal static class EventEvaluator
    {
        public static EventEvaluation Evaluate(EventInfo evt)
        {
            if (evt.Seen)
                return new EventEvaluation(EventStatus.Seen, evt.Conditions.Select(_ => ConditionState.Met).ToArray());

            GameLocation location = Game1.getLocationFromName(evt.LocationName) ?? Game1.currentLocation;
            var states = new ConditionState[evt.Conditions.Count];
            bool locked = false, unreachable = false;

            for (int i = 0; i < states.Length; i++)
            {
                Precondition condition = evt.Conditions[i];
                states[i] = Check(location, evt.Id, condition);
                if (states[i] != ConditionState.Unmet)
                    continue;

                if (condition.Is("Friendship") && !condition.Negated)
                    locked = true;
                else if (condition.Is("SawEvent") && condition.Negated)
                    unreachable = true;
                else if (condition.Is("Year") && !condition.Negated && condition.Args.FirstOrDefault() == "1")
                    unreachable = true;
            }

            EventStatus status;
            if (unreachable)
                status = EventStatus.Unreachable;
            else if (evt.IsSpecial)
                status = EventStatus.Special;
            else if (locked)
                status = EventStatus.Locked;
            else if (states.All(s => s is ConditionState.Met or ConditionState.Neutral))
                status = EventStatus.Ready;
            else
                status = EventStatus.Pending;

            return new EventEvaluation(status, states);
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
