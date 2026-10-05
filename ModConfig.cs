using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using StardewModdingAPI.Utilities;

namespace StardewEventTracker
{
    internal sealed class ModConfig
    {
        /// <summary>Reminder intervals players can choose from, in in-game minutes before an event's time window opens.</summary>
        public static readonly int[] AllowedReminderMinutes = { 180, 120, 60, 30, 15 };

        /// <summary>Built-in game sounds players can pick for pop-ups, by cue name, with a friendly label.</summary>
        public static readonly IReadOnlyDictionary<string, string> AllowedSounds = new Dictionary<string, string>
        {
            ["none"] = "None",
            ["newArtifact"] = "Sparkle",
            ["jingle1"] = "Jingle",
            ["give_gift"] = "Gift",
            ["dwop"] = "Soft pop",
            ["crystal"] = "Crystal chime",
            ["toyPiano"] = "Toy piano",
            ["flute"] = "Flute",
            ["bubbles"] = "Bubbles",
            ["junimoMeep1"] = "Junimo",
            ["discoverMineral"] = "Mineral found",
            ["reward"] = "Reward",
            ["questcomplete"] = "Quest complete",
            ["achievement"] = "Achievement"
        };

        public static string[] SoundCues => AllowedSounds.Keys.ToArray();

        /// <summary>HUD entries sorted with what needs doing soonest first.</summary>
        public const string HudOrderUrgency = "urgency";

        /// <summary>HUD entries sorted by name: NPCs A-Z, then story events by location.</summary>
        public const string HudOrderAlphabetical = "alphabetical";

        public static readonly string[] HudOrders = { HudOrderUrgency, HudOrderAlphabetical };

        /// <summary>The season themes' light and dark modes.</summary>
        public const string HudModeLight = "light", HudModeDark = "dark";

        public static readonly string[] HudModes = { HudModeLight, HudModeDark };

        /// <summary>The HUD's layouts: two lines for what's happening today, or one short line for everything.</summary>
        public const string HudLayoutDetailed = "detailed", HudLayoutCompact = "compact";

        public static readonly string[] HudLayouts = { HudLayoutDetailed, HudLayoutCompact };

        /// <summary>Opens or closes the tracker menu.</summary>
        public KeybindList OpenMenuKey { get; set; } = KeybindList.Parse("F2");

        /// <summary>Shows or hides the HUD tracker.</summary>
        public KeybindList ToggleHudKey { get; set; } = KeybindList.Parse("LeftShift + F2");

        /// <summary>Pins or unpins the NPC under the cursor, in the world or on the Social tab.</summary>
        public KeybindList PinKey { get; set; } = KeybindList.Parse("LeftControl + F2");

        /// <summary>Whether to pin your spouse, roommate and anyone you're dating automatically.</summary>
        public bool AutoPinPartners { get; set; } = true;

        /// <summary>Hide the details of events that aren't unlocked yet (location, requirements, what they lead to).</summary>
        public bool SpoilerFree { get; set; }

        /// <summary>Show up to three loved gifts you own (carried or stored) next to each NPC in the menu and on the HUD.</summary>
        public bool ShowLovedGifts { get; set; } = true;

        /// <summary>Whether the player has been asked, on first opening the menu, if they want spoiler-free mode.</summary>
        public bool SpoilerPromptShown { get; set; }

        /// <summary>Whether the HUD tracker for pinned NPCs is visible.</summary>
        public bool ShowHud { get; set; } = true;

        /// <summary>How long before a pinned NPC's event window opens to remind the player, in in-game minutes.</summary>
        public List<int> ReminderMinutesBefore { get; set; } = new() { 120, 60 };

        /// <summary>Whether pinned story events get reminders like pinned NPCs do.</summary>
        public bool StoryReminders { get; set; } = true;

        /// <summary>Whether events nobody pinned get reminders too: only those with a start time, before it and when it comes.</summary>
        public bool UnpinnedReminders { get; set; }

        /// <summary>Whether to send a "leave now" reminder based on how long the walk there takes.</summary>
        public bool TravelReminders { get; set; } = true;

        /// <summary>Extra in-game minutes of slack added to the walking estimate for "leave now" reminders.</summary>
        public int TravelBufferMinutes { get; set; } = 10;

        /// <summary>Whether to show a message when a pinned NPC's event becomes available.</summary>
        public bool AlertWhenAvailable { get; set; } = true;

        /// <summary>Sound for reminders and the morning heads-up (a key of <see cref="AllowedSounds"/>).</summary>
        public string ReminderSound { get; set; } = "dwop";

        /// <summary>Sound for the "time to visit" pop-up (a key of <see cref="AllowedSounds"/>).</summary>
        public string AvailableSound { get; set; } = "crystal";

        /// <summary>How many seconds reminder pop-ups stay on screen.</summary>
        public int PopupSeconds { get; set; } = 10;

        /// <summary>Whether to list pinned NPC events that can happen today when the day starts.</summary>
        public bool MorningHeadsUp { get; set; } = true;

        /// <summary>Whether to mention, in the evening, pinned NPC events that can't happen today but can tomorrow.</summary>
        public bool TomorrowHeadsUp { get; set; } = true;

        /// <summary>Whether to mark pinned NPCs' events that can happen today on the map page.</summary>
        public bool ShowMapMarkers { get; set; } = true;

        /// <summary>HUD tracker position from the left edge of the screen, in UI pixels.</summary>
        public int HudX { get; set; } = 16;

        /// <summary>HUD tracker position from the top edge of the screen, in UI pixels.</summary>
        public int HudY { get; set; } = 120;

        /// <summary>How the HUD tracker orders its entries: <see cref="HudOrderUrgency"/> or <see cref="HudOrderAlphabetical"/>.</summary>
        public string HudSortOrder { get; set; } = HudOrderUrgency;

        /// <summary>How the HUD tracker looks, by theme ID (see <see cref="UI.Themes.HudTheme.All"/>).</summary>
        public string HudTheme { get; set; } = "community-center";

        /// <summary>Whether the season themes use their light or dark mode: <see cref="HudModeLight"/> or <see cref="HudModeDark"/>.</summary>
        public string HudMode { get; set; } = HudModeLight;

        /// <summary>Whether the HUD tracker fades while the player walks under it or the mouse is over it.</summary>
        public bool HudFade { get; set; } = true;

        /// <summary>Whether the HUD tracker also fades while the mouse is over it (with <see cref="HudFade"/> on).</summary>
        public bool HudFadeOnHover { get; set; } = true;

        /// <summary>How opaque the HUD tracker gets while it fades, in percent.</summary>
        public int HudFadeOpacity { get; set; } = 3;

        /// <summary>How the HUD tracker lays out its entries: <see cref="HudLayoutDetailed"/> or <see cref="HudLayoutCompact"/>.</summary>
        public string HudLayout { get; set; } = HudLayoutDetailed;

        /// <summary>Maximum number of entries (pinned NPCs and story events) listed in the HUD tracker.</summary>
        public int HudMaxNpcs { get; set; } = 5;

        /// <summary>The pre-release alert toggle, read once to migrate to <see cref="AlertWhenAvailable"/>.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? ShowAlerts { get; set; }
    }
}
