using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The demo toolbar's screen, season and language (<see cref="DemoToolbar"/>): what the game's UI shows in the
    /// Editor. For authoring and demos only:
    /// <list type="bullet">
    /// <item>Nothing in the UXML, the scene or the settings changes. Each document root reports itself as it attaches
    /// (<see cref="EditorPreviewHooks"/>).</item>
    /// <item>The screen is the UI Builder's and the UI Viewport's, in Edit and Play mode alike: each app they show gets
    /// it (<see cref="BakeryAppManager.ShowScreen"/>, then the <see cref="GameScreen"/>'s set-up). They show clones of
    /// the UXML in panels of their own, so the Game view, and the Scene view's UI preview of the same panel, keep the
    /// game as it runs.</item>
    /// <item>The season and the language are outside Play mode only. The season is picked in the UI Builder's and the
    /// UI Viewport's own theme menus, for the windows that show the game's UI; elsewhere (the Scene view, the scene's
    /// panel) its theme is added to each root's style sheets, the app or a page, the title or a dialog opened on its
    /// own alike. The language is the Editor's locale, which isn't saved.</item>
    /// <item>They're kept in <see cref="SessionState"/>: through script reloads and Play mode, forgotten when the
    /// Editor quits. So is the UI Viewport's theme menu; the UI Builder's isn't saved as the user's preferred preview
    /// theme. Back to as authored, the windows still on the season go back to their own theme.</item>
    /// <item>Play mode and players start as always, on the title, in the first season and the project's language: the
    /// game's own panel gets no season while playing, the game loads its UXML again as Play mode starts (the scene isn't
    /// reloaded, so its panel would keep the season added here), and Localization goes back to the project's
    /// locale.</item>
    /// <item>The Bagel windows (Bagel UI, Bagel Features, Bagel Demo) keep their own screens and seasons.</item>
    /// </list>
    /// </summary>
    [InitializeOnLoad]
    public static class DemoOverrides
    {
        const string k_ScreenKey = "Bagel.Demo.Screen";
        const string k_SeasonKey = "Bagel.Demo.Season";
        const string k_LanguageKey = "Bagel.Demo.Language";

        // A screen's set-up waits like the windows' phones do, for the app to be laid out.
        const long k_SetUpDelayMs = 400;

        static readonly ConditionalWeakTable<VisualElement, ThemeStyleSheet> s_AddedThemes = new();
        static readonly ConditionalWeakTable<BakeryAppManager, GameScreen> s_StagedScreens = new();
        static BagelThemeCollection s_Themes;
        static bool s_ThemeMenusFailed;

        /// <summary>Raised when the screen, the season or the language changes.</summary>
        public static event Action changed;

        static DemoOverrides()
        {
            EditorPreviewHooks.rootAttached += OnRootAttached;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Roots that attached before this code loaded, and the locale, which a script reload resets.
            OnNextUpdate(ApplyEverything);
        }

        // EditorApplication.delayCall only runs while the Editor is the active application; update always does.
        internal static void OnNextUpdate(Action action)
        {
            EditorApplication.CallbackFunction once = null;
            once = () =>
            {
                EditorApplication.update -= once;
                action();
            };
            EditorApplication.update += once;
        }

        /// <summary>
        /// The screen every app in the UI Builder and the UI Viewport shows, or null: as the UXML has it (the title,
        /// over Home).
        /// </summary>
        public static GameScreen screen
        {
            get => GameScreen.Get(SessionState.GetString(k_ScreenKey, string.Empty));
            set
            {
                if (screen == value)
                    return;
                SessionState.SetString(k_ScreenKey, value?.id ?? string.Empty);
                foreach (var root in EditorPreviewHooks.roots)
                    if (root is BakeryAppManager app && !IsInBagelWindow(app))
                        ApplyScreen(app, again: true);
                changed?.Invoke();
            }
        }

        /// <summary>
        /// Screen, season and language as authored: each app on its UXML's screen, each panel on its own theme, and the
        /// project's language.
        /// </summary>
        public static void ResetToAuthored()
        {
            screen = null;
            season = -1;
            SessionState.EraseString(k_LanguageKey);
            var locale = LocalizationSettings.Instance?.ProjectLocale;
            if (!Application.isPlaying && locale != null && BagelLocalization.localeCode != locale.Identifier.Code)
                BagelLocalization.SetLocale(locale.Identifier.Code);
            changed?.Invoke();
        }

        public static BagelThemeCollection themes =>
            s_Themes != null ? s_Themes : s_Themes = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(BagelExpressAllUIWindow.themeCollectionPath);

        /// <summary>The season's index in <see cref="themes"/>, or -1: each panel's own theme.</summary>
        public static int season
        {
            get => SessionState.GetInt(k_SeasonKey, -1);
            set
            {
                if (season == value)
                    return;
                var previous = seasonTheme;
                SessionState.SetInt(k_SeasonKey, value);
                foreach (var root in EditorPreviewHooks.roots)
                    if (!IsInBagelWindow(root))
                        ApplySeason(root);
                ApplyThemeMenus(previous);
                changed?.Invoke();
            }
        }

        /// <summary>The season's theme, or null.</summary>
        public static ThemeStyleSheet seasonTheme
        {
            get
            {
                var index = season;
                var collection = themes?.collection;
                return collection != null && index >= 0 && index < collection.Count ? collection[index].theme : null;
            }
        }

        /// <summary>The language's locale code ("en", "ja", "fr"), or empty: the Editor's locale, as it is.</summary>
        public static string language
        {
            get => SessionState.GetString(k_LanguageKey, string.Empty);
            set
            {
                SessionState.SetString(k_LanguageKey, value ?? string.Empty);
                ApplyLanguage();
                changed?.Invoke();
            }
        }

        static void OnRootAttached(VisualElement root)
        {
            if (IsInBagelWindow(root))
                return;
            ApplySeason(root);
            // Attached again, an app goes back to its UXML's screen: the screen goes on again.
            if (root is BakeryAppManager app)
                ApplyScreen(app, again: true);
            // Once the window around it is set up, it can tell: a UI Viewport's canvas takes its sub panel after the
            // document attaches to it, and UI Builder picks its own theme as it loads a document.
            OnNextUpdate(() =>
            {
                if (root.panel != null)
                {
                    ApplySeason(root);
                    if (root is BakeryAppManager later)
                        ApplyScreen(later, again: false);
                }
                if (season >= 0)
                    ApplyThemeMenus(null);
            });
        }

        static void ApplyEverything()
        {
            ApplyLanguage();
            foreach (var root in EditorPreviewHooks.roots)
                OnRootAttached(root);
            ApplyThemeMenus(null);
            changed?.Invoke();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            // Localization goes back to the project's locale around Play mode: the Editor's comes back after it.
            if (change == PlayModeStateChange.EnteredEditMode)
                OnNextUpdate(ApplyEverything);
        }

        static void ApplyLanguage()
        {
            var code = language;
            if (Application.isPlaying || string.IsNullOrEmpty(code))
                return;
            // After Play mode, Localization initializes again and picks its startup locale when done: ours comes after.
            var settings = LocalizationSettings.Instance;
            if (settings != null && !settings.IsInitialized)
            {
                LocalizationSettings.InitializationCompleted -= ApplyLanguage;
                LocalizationSettings.InitializationCompleted += ApplyLanguage;
            }
            if (BagelLocalization.localeCode != code)
                BagelLocalization.SetLocale(code);
        }

        /// <summary>
        /// The season's theme on a root, in place of the one added before; none: the panel's own. Not in UI Builder or
        /// the UI Viewport, whose theme menu has it (<see cref="ApplyThemeMenus"/>).
        /// </summary>
        static void ApplySeason(VisualElement root)
        {
            var wanted = InThemeMenuWindow(root) || Application.isPlaying ? null : seasonTheme;
            if (s_AddedThemes.TryGetValue(root, out var added))
            {
                if (added == wanted)
                    return;
                root.styleSheets.Remove(added);
                s_AddedThemes.Remove(root);
            }
            if (wanted == null)
                return;
            root.styleSheets.Add(wanted);
            s_AddedThemes.Add(root, wanted);
        }

        /// <summary>
        /// The season in the theme menu of each UI Builder and UI Viewport that shows the game's UI, as if picked there.
        /// Back to as authored, the ones still on <paramref name="previous"/> (the season this set) go back to their own
        /// theme; one picked there since stays.
        /// </summary>
        static void ApplyThemeMenus(ThemeStyleSheet previous)
        {
            var wanted = seasonTheme;
            if (wanted == null && previous == null)
                return;
            TryThemeMenus(() =>
            {
                foreach (var window in EditorWindowInternals.ThemeMenuWindows())
                {
                    var current = EditorWindowInternals.GetMenuTheme(window);
                    if (wanted != null && current != wanted && ShowsGameUI(window))
                        EditorWindowInternals.SetMenuTheme(window, wanted);
                    else if (wanted == null && current == previous)
                        EditorWindowInternals.SetMenuTheme(window, null);
                }
                return true;
            });
        }

        static bool ShowsGameUI(EditorWindow window)
        {
            foreach (var root in EditorPreviewHooks.roots)
                if (!IsInBagelWindow(root) && EditorWindowInternals.ShowsPanel(window, root.panel))
                    return true;
            return false;
        }

        static bool InThemeMenuWindow(VisualElement root) =>
            TryThemeMenus(() => EditorWindowInternals.ThemeMenuWindows().Any(window => EditorWindowInternals.ShowsPanel(window, root.panel)));

        // The theme menus are internal: in an Editor that has them otherwise, the season goes on top of every root.
        static bool TryThemeMenus(Func<bool> action)
        {
            if (s_ThemeMenusFailed)
                return false;
            try
            {
                return action();
            }
            catch (Exception e)
            {
                s_ThemeMenusFailed = true;
                Debug.LogWarning("[Bagel] The demo toolbar's season can't use the UI Builder's and UI Viewport's theme menus here; it's added on top instead. " + (e.InnerException ?? e).Message);
                return false;
            }
        }

        // UI Builder takes its own theme again when an asset its document uses is imported: the season goes back on after.
        sealed class ThemeMenusAfterImport : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (season >= 0)
                    OnNextUpdate(() => ApplyThemeMenus(null));
            }
        }

        /// <summary>
        /// An app in the UI Builder or the UI Viewport on the screen, closing what the last one opened; none, or an app
        /// elsewhere (the game's own panel): back to the UXML's own. <paramref name="again"/>: even if it's on it already
        /// (attached again, or the screen changed).
        /// </summary>
        static void ApplyScreen(BakeryAppManager app, bool again)
        {
            var wanted = InThemeMenuWindow(app) ? screen : null;
            var wasStaged = s_StagedScreens.TryGetValue(app, out var staged);
            if (!again && (wasStaged ? staged == wanted : wanted == null))
                return;
            s_StagedScreens.Remove(app);
            // What the last one's set-up left on (the Performance screen's overlay) goes first.
            if (wasStaged)
                staged.TearDown(app);
            if (wanted == null)
            {
                if (wasStaged)
                    app.ShowScreen(app.startPage, app.showTitle);
                return;
            }

            s_StagedScreens.Add(app, wanted);
            app.ShowScreen(wanted.page, wanted.showTitle);
            app.schedule.Execute(() =>
            {
                if (app.panel != null && s_StagedScreens.TryGetValue(app, out var staged) && staged == wanted)
                    wanted.SetUp(app);
            }).ExecuteLater(k_SetUpDelayMs);
        }

        /// <summary>Inside the Bagel UI, Bagel Features or Bagel Demo window, which set their own screens and seasons.</summary>
        static bool IsInBagelWindow(VisualElement element)
        {
            if (element?.panel == null)
                return false;
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if ((window is BagelGuideWindow || window is BagelExpressAllUIWindow) && window.rootVisualElement.panel == element.panel)
                    return true;
            return false;
        }
    }
}
