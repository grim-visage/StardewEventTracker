using StardewModdingAPI;

namespace StardewEventTracker
{
    /// <summary>Player-facing text from the <c>i18n</c> folder, so the mod can be translated.</summary>
    internal static class I18n
    {
        private static ITranslationHelper? translations;

        public static void Init(ITranslationHelper helper) => translations = helper;

        /// <summary>The translation for a key, with <c>{{token}}</c> values filled in from an anonymous object.</summary>
        public static string Get(string key, object? tokens = null) =>
            translations?.Get(key, tokens).ToString() ?? key;

        /// <summary>The translation for a key, or a fallback if no translation has it (e.g. a mod's custom weather ID).</summary>
        public static string GetOr(string key, string fallback, object? tokens = null)
        {
            Translation? translation = translations?.Get(key, tokens);
            return translation?.HasValue() == true ? translation.ToString() : fallback;
        }

        /// <summary>The key for one of several numbered variants (<c>key.1</c>, <c>key.2</c>...), picked by a stable seed.</summary>
        public static string Variant(string key, uint seed, int count, object? tokens = null) =>
            Get($"{key}.{seed % (uint)count + 1}", tokens);
    }
}
