using System.Collections.Generic;
using Newtonsoft.Json;
using StardewModdingAPI.Utilities;

namespace NpcEventTracker
{
    internal sealed class ModConfig
    {
        /// <summary>Reminder intervals players can choose from, in in-game minutes before an event's time window opens.</summary>
        public static readonly int[] AllowedReminderMinutes = { 180, 120, 60, 30, 15 };

        /// <summary>Opens or closes the tracker menu.</summary>
        public KeybindList OpenMenuKey { get; set; } = KeybindList.Parse("F2");

        /// <summary>Shows or hides the HUD tracker.</summary>
        public KeybindList ToggleHudKey { get; set; } = KeybindList.Parse("LeftShift + F2");

        /// <summary>Whether the HUD tracker for pinned NPCs is visible.</summary>
        public bool ShowHud { get; set; } = true;

        /// <summary>How long before a pinned NPC's event window opens to remind the player, in in-game minutes.</summary>
        public List<int> ReminderMinutesBefore { get; set; } = new() { 120, 60 };

        /// <summary>Whether to show a message when a pinned NPC's event becomes available.</summary>
        public bool AlertWhenAvailable { get; set; } = true;

        /// <summary>How many seconds reminder pop-ups stay on screen.</summary>
        public int PopupSeconds { get; set; } = 10;

        /// <summary>Whether to list pinned NPC events that can happen today when the day starts.</summary>
        public bool MorningHeadsUp { get; set; } = true;

        /// <summary>HUD tracker position from the left edge of the screen, in UI pixels.</summary>
        public int HudX { get; set; } = 16;

        /// <summary>HUD tracker position from the top edge of the screen, in UI pixels.</summary>
        public int HudY { get; set; } = 120;

        /// <summary>Maximum number of pinned NPCs listed in the HUD tracker.</summary>
        public int HudMaxNpcs { get; set; } = 5;

        /// <summary>The 1.0 alert toggle, read once to migrate to <see cref="AlertWhenAvailable"/>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShowAlerts { get; set; }
    }
}
