using System;
using Unity.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Thin glue over the built-in Localization module. All UI text lives in the
    /// "BagelStrings" Resource Table (Window > Localization > Resource Tables); most of
    /// it is bound in UXML with LocalizedString bindings, this class covers the C# side.
    /// </summary>
    public static class BagelLocalization
    {
        public const string table = "BagelStrings";

        /// <summary>
        /// USS class on the app root while Japanese is selected: per-language typography lives in
        /// BagelLanguageStyles.uss (e.g. smaller display text, since kana/kanji fall back from Bangers).
        /// </summary>
        public static readonly UniqueStyleString japaneseUssClassName = new("be-lang-ja");

        /// <summary>USS class on the app root while French is selected (longer strings, e.g. AMÉLIORER).</summary>
        public static readonly UniqueStyleString frenchUssClassName = new("be-lang-fr");

        static Action s_LocaleChanged;

        /// <summary>
        /// Raised after the selected locale changes, wherever it was changed from (Settings
        /// buttons, the Localization toolbar dropdown, a LanguageDropdown, code).
        /// </summary>
        public static event Action localeChanged
        {
            add { HookSelectedLocaleChanged(); s_LocaleChanged += value; }
            remove => s_LocaleChanged -= value;
        }

        public static bool isJapanese => LocalizedText.isJapanese;
        public static bool isFrench => LocalizedText.isFrench;
        public static string localeCode => LocalizedText.localeCode;

        /// <summary>Switch language by locale code ("en", "ja", "fr").</summary>
        public static void SetLocale(string code)
        {
            var locale = LocalizationSettings.Instance?.GetLocale(code);
            if (locale == null)
                return;
            HookSelectedLocaleChanged();
            LocalizationSettings.SelectedLocale = locale;
        }

        /// <summary>Toggle the per-language USS classes on an element to match the selected locale.</summary>
        public static void ApplyLanguageClass(VisualElement element)
        {
            element.EnableInClassList(japaneseUssClassName, isJapanese);
            element.EnableInClassList(frenchUssClassName, isFrench);
        }

        /// <summary>The entry's value, or <paramref name="fallback"/> when the table has none.</summary>
        public static string GetOr(string key, string fallback)
        {
            var value = String(key).GetLocalizedString();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        /// <summary>A LocalizedString for an entry of the table, e.g. to bind in code.</summary>
        public static LocalizedString String(string key)
        {
            var text = new LocalizedString();
            text.SetReference(table, key);
            return text;
        }

        /// <summary>One-off text (toasts): the entry formatted with Smart String arguments ({0}, {1}...).</summary>
        public static string Get(string key, params object[] args)
        {
            var value = String(key).GetLocalizedString(args);
            return string.IsNullOrEmpty(value) ? key : value;
        }

        // LocalizationSettings clears its static event when Play mode starts and stops, so
        // (re)subscribe whenever we're used and at the start of Play mode. -= then += keeps it single.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void HookSelectedLocaleChanged()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        static void OnSelectedLocaleChanged(Locale locale) => s_LocaleChanged?.Invoke();

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void HookInEditMode()
        {
            HookSelectedLocaleChanged();
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change == UnityEditor.PlayModeStateChange.EnteredEditMode)
                    UnityEditor.EditorApplication.delayCall += HookSelectedLocaleChanged;
            };
        }
#endif
    }
}
