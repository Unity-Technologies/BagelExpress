using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Main toolbar strips for the live demo, the preview (<see cref="DemoOverrides"/>): the screen the game's UI shows in
    /// the UI Builder and the UI Viewport, in Edit and Play mode (on the left), the season and the language it shows in
    /// them and the Scene view outside Play mode (on the right). A strip of icons each, like the
    /// Bagel windows' (<see cref="BagelOptionStrip"/>), "as authored" first. Toolbar elements start hidden: these are
    /// shown once per user, and by the layout reset. The resets (<see cref="DemoReset"/>) are in the Bagel › Demo menu.
    /// </summary>
    public static class DemoToolbar
    {
        public const string screenPath = "Bagel Express/Demo Screen";
        public const string seasonPath = "Bagel Express/Demo Season";
        public const string languagePath = "Bagel Express/Demo Language";
        public static readonly string[] paths = { screenPath, seasonPath, languagePath };

        const string k_ShownKey = "Bagel.DemoToolbar.StripsShown";
        const string k_Where = "in the UI Builder, the UI Viewport and the Scene view";
        const string k_ScreenWhere = "in the UI Builder and the UI Viewport";

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            DemoOverrides.changed += Refresh;
            BagelLocalization.localeChanged += Refresh;
            if (EditorUserSettings.GetConfigValue(k_ShownKey) != bool.TrueString)
                EditorApplication.update += ShowOnce;
        }

        // The screens' art follows the season, so each change rebuilds the three.
        static void Refresh()
        {
            foreach (var path in paths)
                MainToolbar.Refresh(path);
        }

        // The toolbar builds its elements a little after scripts load: try until they're there (10 seconds at most).
        static void ShowOnce()
        {
            if (Show())
                EditorUserSettings.SetConfigValue(k_ShownKey, bool.TrueString);
            else if (EditorApplication.timeSinceStartup - s_LoadTime < 10.0)
                return;
            EditorApplication.update -= ShowOnce;
        }

        static readonly double s_LoadTime = EditorApplication.timeSinceStartup;

        [MainToolbarElement(screenPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = 1000)]
        static IEnumerable<MainToolbarElement> CreateScreenStrip()
        {
            var current = DemoOverrides.screen;
            yield return Option("demo-screen-authored", () => BagelPickerIcons.AsAuthored(BagelExpressAllUIWindow.appUxmlPath),
                $"Screen {k_ScreenWhere}: as authored (the app's UXML: the title, over Home)", current == null, () => DemoOverrides.screen = null);
            // The art in the season shown, or the first one's.
            var collection = DemoOverrides.themes != null ? DemoOverrides.themes.collection : null;
            var theme = DemoOverrides.seasonTheme;
            if (theme == null && collection != null && collection.Count > 0)
                theme = collection[0].theme;
            foreach (var screen in GameScreen.all)
            {
                var chosen = screen;
                yield return Option("demo-screen-" + screen.id, () => BagelPickerIcons.Screen(chosen, theme),
                    $"Screen {k_ScreenWhere}: {screen.title} · {screen.japaneseTitle}", current == screen, () => DemoOverrides.screen = chosen);
            }
        }

        // The season and language strips are on the right, next to the Search button (the right zone runs from right to
        // left: the higher index, the further left).
        [MainToolbarElement(seasonPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 1001)]
        static IEnumerable<MainToolbarElement> CreateSeasonStrip()
        {
            var current = DemoOverrides.season;
            var collection = DemoOverrides.themes != null ? DemoOverrides.themes.collection : null;
            var authored = collection != null && collection.Count > 0 ? AssetDatabase.GetAssetPath(collection[0].theme) : string.Empty;
            yield return Option("demo-season-authored", () => BagelPickerIcons.AsAuthored(authored),
                $"Season {k_Where}: as authored (each panel's own theme)", current < 0, () => DemoOverrides.season = -1);
            for (int i = 0; collection != null && i < collection.Count; i++)
            {
                var index = i;
                yield return Option("demo-season-" + i, () => BagelPickerIcons.Season(index),
                    $"Season {k_Where}: {BagelPickerIcons.SeasonName(collection[i])}", current == i, () => DemoOverrides.season = index);
            }
        }

        [MainToolbarElement(languagePath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 1000)]
        static IEnumerable<MainToolbarElement> CreateLanguageStrip()
        {
            var current = BagelLocalization.localeCode;
            foreach (var (code, name) in BagelPickerIcons.languages)
            {
                var chosen = code;
                yield return Option("demo-language-" + code, () => BagelPickerIcons.Language(chosen),
                    $"Language {k_Where}: {name}", current == code, () => DemoOverrides.language = chosen);
            }
        }

        // An option of a strip, a toolbar element of its own: the main toolbar draws the group around them. Named, such as
        // demo-screen-home or demo-language-ja, so scripted demos can find it.
        static MainToolbarElement Option(string name, Func<VisualElement> icon, string tooltip, bool selected, Action choose) =>
            new MainToolbarCustomElement(() =>
            {
                var option = BagelOptionStrip.CreateOption(icon(), tooltip, selected, () =>
                {
                    if (!selected)
                        choose();
                });
                option.name = name;
                BagelOptionStrip.AddStyleSheet(option);
                return option;
            });

        /// <summary>Shows the strips on the main toolbar; false while the toolbar isn't there yet.</summary>
        public static bool Show()
        {
            var window = typeof(EditorWindow).Assembly.GetType("UnityEditor.MainToolbarWindow")?
                .GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as EditorWindow;
            if (window == null)
                return false;
            foreach (var path in paths)
            {
                if (!window.TryGetOverlay(path, out var overlay))
                    return false;
                overlay.displayed = true;
            }
            return true;
        }
    }
}
