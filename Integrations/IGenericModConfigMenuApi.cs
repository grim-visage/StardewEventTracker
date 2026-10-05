using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace StardewEventTracker.Integrations
{
    /// <summary>The subset of Generic Mod Config Menu's API this mod uses.</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

        void AddSectionTitle(IManifest mod, Func<string> text, Func<string>? tooltip = null);

        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);

        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);

        /// <summary>Called whenever a field's value changes in the menu, before it's saved.</summary>
        void OnFieldChanged(IManifest mod, Action<string, object> onChange);

        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);
    }

    /// <summary>
    /// Generic Mod Config Menu's way to open a config page over the current menu, added in GMCM 1.14.1. It's asked for
    /// on its own so an older GMCM still gets the settings (SMAPI can't connect an interface the API doesn't fully
    /// have), just without the tracker menu's Settings button.
    /// </summary>
    public interface IGenericModConfigMenuChildApi
    {
        /// <summary>Opens a mod's config page over the current menu, which comes back when it's closed.</summary>
        void OpenModMenuAsChildMenu(IManifest mod);
    }
}
