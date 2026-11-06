using Unity.Localization;

namespace Bagel
{
    /// <summary>
    /// Small helpers for localized game content (shop and recipe names come from the
    /// BagelStrings Resource Table through LocalizedString fields on the definitions).
    /// </summary>
    public static class LocalizedText
    {
        /// <summary>Code of the selected locale ("en", "ja", "fr"); "en" before Localization is ready.</summary>
        public static string localeCode => LocalizationSettings.SelectedLocale?.Code ?? "en";

        /// <summary>True when the selected locale is Japanese.</summary>
        public static bool isJapanese => localeCode == "ja";

        /// <summary>True when the selected locale is French.</summary>
        public static bool isFrench => localeCode == "fr";

        /// <summary>
        /// The value for the selected locale, or <paramref name="fallback"/> when the reference is
        /// unset or can't resolve (e.g. no Localization settings in a test project).
        /// </summary>
        public static string GetLocalizedStringOr(this LocalizedString text, string fallback)
        {
            if (text == null || text.IsEmpty)
                return fallback;
            var value = text.GetLocalizedString();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }
}
