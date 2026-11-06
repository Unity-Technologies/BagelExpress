using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// The phones the game is checked on, as fixed resolutions in the Game view's size menu, each time the project opens.
    ///
    /// The Game view keeps its custom sizes in the Editor's preferences (GameViewSizes.asset, shared by every project and
    /// Unity version on a computer), not in the project, so a clone on another computer would start without them. This
    /// script is the project's copy of them: it adds each one that's missing from its group, by name, and leaves the rest
    /// of the menu alone (sizes already there, even with other resolutions, and anyone's own). The iPhones go in the iOS
    /// group, the Galaxies in Android's, and all of them in Standalone's (macOS, Windows and Web), with
    /// <see cref="perfSize"/>.
    ///
    /// It runs once per Editor session: a size deleted in the Game view comes back when the Editor restarts. To drop one
    /// for good, remove it from <see cref="sizes"/>. Native pixels; the foldables' inner screens have both orientations.
    /// </summary>
    [InitializeOnLoad]
    public static class GameViewDeviceSizes
    {
        const string k_SessionKey = "Bagel.GameViewDeviceSizes.Applied";

        /// <summary>The Game view's groups that get sizes.</summary>
        public static readonly GameViewSizeGroupType[] groups = { GameViewSizeGroupType.Standalone, GameViewSizeGroupType.iOS, GameViewSizeGroupType.Android };

        /// <summary>The Bagel UI window's phone at 2x, the size the Editor's performance sweeps use.</summary>
        public static readonly (string name, int width, int height) perfSize = ("Bagel 2x (perf)", 780, 1688);

        public static readonly (GameViewSizeGroupType group, string name, int width, int height)[] sizes =
        {
            (GameViewSizeGroupType.iOS, "iPhone 12 / 12 Pro / 17e", 1170, 2532),
            (GameViewSizeGroupType.iOS, "iPhone 17 / 18 Pro", 1206, 2622),
            (GameViewSizeGroupType.iOS, "iPhone Air", 1260, 2736),
            (GameViewSizeGroupType.iOS, "iPhone 18 Pro Max", 1320, 2868),
            (GameViewSizeGroupType.iOS, "iPhone Duo, closed", 1398, 2034),
            (GameViewSizeGroupType.iOS, "iPhone Duo, open portrait", 1878, 2670),
            (GameViewSizeGroupType.iOS, "iPhone Duo, open landscape", 2670, 1878),
            (GameViewSizeGroupType.Android, "Galaxy S26", 1080, 2340),
            (GameViewSizeGroupType.Android, "Galaxy S26+ / S26 Ultra", 1440, 3120),
            (GameViewSizeGroupType.Android, "Galaxy Z Flip8, open", 1080, 2520),
            (GameViewSizeGroupType.Android, "Galaxy Z Flip8, cover screen", 948, 1048),
            (GameViewSizeGroupType.Android, "Galaxy Z Fold8, closed", 1248, 1972),
            (GameViewSizeGroupType.Android, "Galaxy Z Fold8, open portrait", 1848, 2448),
            (GameViewSizeGroupType.Android, "Galaxy Z Fold8, open landscape", 2448, 1848),
            (GameViewSizeGroupType.Android, "Galaxy Z Fold8 Ultra, closed", 1080, 2520),
            (GameViewSizeGroupType.Android, "Galaxy Z Fold8 Ultra, open portrait", 2256, 2504),
            (GameViewSizeGroupType.Android, "Galaxy Z Fold8 Ultra, open landscape", 2504, 2256),
        };

        /// <summary>The sizes of a group, in the menu's order: Standalone's start with <see cref="perfSize"/>.</summary>
        public static (string name, int width, int height)[] SizesOf(GameViewSizeGroupType group) =>
            group == GameViewSizeGroupType.Standalone
                ? sizes.Select(s => (s.name, s.width, s.height)).Prepend(perfSize).ToArray()
                : sizes.Where(s => s.group == group).Select(s => (s.name, s.width, s.height)).ToArray();

        static GameViewDeviceSizes()
        {
            if (SessionState.GetBool(k_SessionKey, false))
                return;
            SessionState.SetBool(k_SessionKey, true);
            EditorApplication.delayCall += () => Apply();
        }

        /// <summary>Adds the missing sizes and saves the preferences if it added any. Returns how many it added.</summary>
        public static int Apply()
        {
            try
            {
                var editor = typeof(EditorWindow).Assembly;
                var sizeType = editor.GetType("UnityEditor.GameViewSize", true);
                var fixedResolution = Enum.Parse(editor.GetType("UnityEditor.GameViewSizeType", true), "FixedResolution");
                var added = 0;
                foreach (var group in groups)
                {
                    var groupObject = GetGroup(group);
                    var names = NamesIn(group);
                    foreach (var (name, width, height) in SizesOf(group))
                    {
                        if (names.Contains(name))
                            continue;
                        var size = Activator.CreateInstance(sizeType, fixedResolution, width, height, name);
                        groupObject.GetType().GetMethod("AddCustomSize").Invoke(groupObject, new[] { size });
                        added++;
                    }
                }
                if (added > 0)
                    GameViewSizes().GetType().GetMethod("SaveToHDD").Invoke(GameViewSizes(), null);
                return added;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] The Game view's phone sizes couldn't be added: this Editor's GameViewSizes differ. " + (e.InnerException ?? e).Message);
                return 0;
            }
        }

        /// <summary>
        /// Every open Game view shows the size called <paramref name="name"/> (<see cref="perfSize"/> by default), in the
        /// menu of the group the Editor's build target uses: found by name, since the menu's order differs on each
        /// computer (a layout file keeps only an index). The project's sizes are added first if one is missing. Returns how
        /// many Game views show it.
        /// </summary>
        public static int SelectInGameViews(string name = null)
        {
            name ??= perfSize.name;
            try
            {
                var current = (GameViewSizeGroupType)GameViewSizes().GetType().GetProperty("currentGroupType").GetValue(GameViewSizes());
                var index = Array.IndexOf(NamesIn(current), name);
                if (index < 0 && Apply() > 0)
                    index = Array.IndexOf(NamesIn(current), name);
                if (index < 0)
                {
                    Debug.LogWarning($"[Bagel] The Game view has no size called {name} for {current}.");
                    return 0;
                }
                var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView", true);
                var select = gameViewType.GetMethod("SizeSelectionCallback");
                var gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
                foreach (var gameView in gameViews)
                {
                    select.Invoke(gameView, new object[] { index, null });
                    ((EditorWindow)gameView).Repaint();
                }
                return gameViews.Length;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] The Game view couldn't show {name}: this Editor's GameView differs. " + (e.InnerException ?? e).Message);
                return 0;
            }
        }

        /// <summary>The names in a group's menu, built-in and custom.</summary>
        public static string[] NamesIn(GameViewSizeGroupType group)
        {
            var groupObject = GetGroup(group);
            var type = groupObject.GetType();
            var count = (int)type.GetMethod("GetTotalCount").Invoke(groupObject, null);
            var getSize = type.GetMethod("GetGameViewSize");
            return Enumerable.Range(0, count)
                .Select(i => getSize.Invoke(groupObject, new object[] { i }))
                .Select(size => (string)size.GetType().GetProperty("baseText").GetValue(size))
                .ToArray();
        }

        // The Editor's (internal) GameViewSizes, and one of its groups (GameViewSizeGroup).
        static object GameViewSizes()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes", true);
            return typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
        }

        static object GetGroup(GameViewSizeGroupType group) =>
            GameViewSizes().GetType().GetMethod("GetGroup").Invoke(GameViewSizes(), new object[] { group });
    }
}
