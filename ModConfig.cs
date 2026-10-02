using StardewModdingAPI.Utilities;

namespace NpcEventTracker
{
    internal sealed class ModConfig
    {
        /// <summary>Opens or closes the tracker menu.</summary>
        public KeybindList OpenMenuKey { get; set; } = KeybindList.Parse("F8");

        /// <summary>Shows or hides the HUD tracker.</summary>
        public KeybindList ToggleHudKey { get; set; } = KeybindList.Parse("F9");

        /// <summary>Whether the HUD tracker for pinned NPCs is visible.</summary>
        public bool ShowHud { get; set; } = true;

        /// <summary>Whether to show a HUD message when a pinned NPC's event becomes ready.</summary>
        public bool ShowAlerts { get; set; } = true;

        /// <summary>HUD tracker position from the left edge of the screen, in UI pixels.</summary>
        public int HudX { get; set; } = 16;

        /// <summary>HUD tracker position from the top edge of the screen, in UI pixels.</summary>
        public int HudY { get; set; } = 120;

        /// <summary>Maximum number of pinned NPCs listed in the HUD tracker.</summary>
        public int HudMaxNpcs { get; set; } = 5;
    }
}
