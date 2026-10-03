using System.Collections.Generic;
using StardewEventTracker.Data;
using StardewModdingAPI;

namespace StardewEventTracker
{
    /// <summary>Per-save data for one player: which NPCs they're tracking.</summary>
    internal sealed class PinData
    {
        public List<string> PinnedNpcs { get; set; } = new();

        /// <summary>Story events the player is tracking, by <see cref="Data.EventInfo.Key"/>.</summary>
        public List<string> PinnedStoryEvents { get; set; } = new();

        /// <summary>Spouses/partners the player unpinned, so auto-pinning doesn't pin them again.</summary>
        public List<string> AutoPinDismissed { get; set; } = new();
    }

    /// <summary>Everything the mod tracks for one local player. In split-screen co-op each screen has its own.</summary>
    internal sealed class PlayerState
    {
        /// <summary>Events and their status for this player (friendship and seen events differ per player).</summary>
        public EventIndex Index { get; }

        public HashSet<string> PinnedNpcs { get; } = new();

        /// <summary>Pinned story events, by <see cref="EventInfo.Key"/> (story events aren't tied to one NPC).</summary>
        public HashSet<string> PinnedStoryEvents { get; } = new();

        /// <summary>Whether anything at all is pinned.</summary>
        public bool HasPins => this.PinnedNpcs.Count > 0 || this.PinnedStoryEvents.Count > 0;

        /// <summary>Partners the player unpinned, so auto-pinning leaves them alone.</summary>
        public HashSet<string> AutoPinDismissed { get; } = new();

        /// <summary>Message keys already sent today, so each reminder fires once per day.</summary>
        public HashSet<string> AlertedToday { get; } = new();

        /// <summary>Today's reminder pop-ups, oldest first, so missed ones can be re-read in the menu.</summary>
        public List<(int Time, string Text)> MessagesToday { get; } = new();

        /// <summary>Walking-time estimates from this player's position.</summary>
        public TravelEstimator Travel { get; } = new();

        /// <summary>Event keys the player snoozed until tomorrow.</summary>
        public HashSet<string> SnoozedToday { get; } = new();

        public PlayerState(IMonitor monitor)
        {
            this.Index = new EventIndex(monitor);
        }

        /// <summary>Clears what only lasts for the current day.</summary>
        public void StartDay()
        {
            this.AlertedToday.Clear();
            this.MessagesToday.Clear();
            this.SnoozedToday.Clear();
            this.Travel.Invalidate();
        }

        /// <summary>Clears everything, e.g. when returning to the title screen.</summary>
        public void Reset()
        {
            this.StartDay();
            this.PinnedNpcs.Clear();
            this.PinnedStoryEvents.Clear();
            this.AutoPinDismissed.Clear();
            this.Index.Clear();
        }
    }
}
