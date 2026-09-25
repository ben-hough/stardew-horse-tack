using StardewModdingAPI;

namespace MrGlim.HorseTack
{
    /// <summary>Access to the mod's translations (i18n/default.json).</summary>
    internal static class I18n
    {
        private static ITranslationHelper? Translations;

        public static void Init(ITranslationHelper translations) => Translations = translations;

        public static string Get(string key, object? tokens = null)
        {
            if (Translations == null)
                return key;
            return Translations.Get(key, tokens).Default(key).ToString();
        }
    }
}
