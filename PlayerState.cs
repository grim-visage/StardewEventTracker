using System.Collections.Generic;
using NpcEventTracker.Data;
using StardewModdingAPI;

namespace NpcEventTracker
{
    /// <summary>Per-save data for one player: which NPCs they're tracking.</summary>
    internal sealed class PinData
    {
        public List<string> PinnedNpcs { get; set; } = new();

        /// <summary>Spouses/partners the player unpinned, so auto-pinning doesn't pin them again.</summary>
        public List<string> AutoPinDismissed { get; set; } = new();
    }

    /// <summary>Everything the mod tracks for one local player. In split-screen co-op each screen has its own.</summary>
    internal sealed class PlayerState
    {
        /// <summary>Events and their status for this player (friendship and seen events differ per player).</summary>
        public EventIndex Index { get; }

        public HashSet<string> PinnedNpcs { get; } = new();

        /// <summary>Partners the player unpinned, so auto-pinning leaves them alone.</summary>
        public HashSet<string> AutoPinDismissed { get; } = new();

        /// <summary>Message keys already sent today, so each reminder fires once per day.</summary>
        public HashSet<string> AlertedToday { get; } = new();

        /// <summary>Today's reminder pop-ups, oldest first, so missed ones can be re-read in the menu.</summary>
        public List<(int Time, string Text)> MessagesToday { get; } = new();

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
        }

        /// <summary>Clears everything, e.g. when returning to the title screen.</summary>
        public void Reset()
        {
            this.StartDay();
            this.PinnedNpcs.Clear();
            this.AutoPinDismissed.Clear();
            this.Index.Clear();
        }
    }
}
