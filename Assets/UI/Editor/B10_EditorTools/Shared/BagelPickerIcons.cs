using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// The icons of the screen, season and language pickers: the toolbars' strips (<see cref="BagelOptionStrip"/>) and the
    /// Bagel Features window's screens. The game's own art and SVG icons, small; the Editor's asset icon (UXML, TSS) for
    /// "as authored", and its Refresh and Inspector icons for the windows' Reset and Bagel UI's Inspector.
    /// </summary>
    public static class BagelPickerIcons
    {
        public const string styleSheetPath = "Assets/UI/Editor/B10_EditorTools/Shared/BagelPickerIcons.uss";

        public static readonly string ussClassName = "bagel-icon";
        public static readonly string badgeUssClassName = ussClassName + "__badge";

        /// <summary>The game's languages: locale code and name.</summary>
        public static readonly (string code, string name)[] languages = { ("en", "English"), ("ja", "日本語 · Japanese"), ("fr", "Français · French") };

        public static int LanguageIndex(string code) => Array.FindIndex(languages, language => language.code == code);

        public static string SeasonName(BagelThemeCollection.ThemeEntry entry) => $"{entry.themeName} · {entry.japaneseName}";

        /// <summary>The seasons of a collection, in its order.</summary>
        public static void AddSeasons(BagelOptionStrip strip, BagelThemeCollection themes)
        {
            var collection = themes != null ? themes.collection : null;
            for (int i = 0; collection != null && i < collection.Count; i++)
                strip.AddOption(Season(i), SeasonName(collection[i]));
        }

        public static void AddLanguages(BagelOptionStrip strip)
        {
            foreach (var (code, name) in languages)
                strip.AddOption(Language(code), name);
        }

        /// <summary>A window's Reset, a strip of its own next to the season and language strips: <c>reset-button</c>.</summary>
        public static VisualElement AddReset(BagelOptionStrip strip, Action reset)
        {
            var button = strip.AddAction(Reset(), "Reset", reset);
            button.name = "reset-button";
            return button;
        }

        /// <summary>
        /// A screen's icon: its page's nav icon, with a badge for what opens over the page (LEVEL UP!'s star on Shops, the
        /// reveal's pastry and the ticket the dialog sells on the Lucky Bag, the overlay's graph on Settings); a piece of
        /// the screen's own art for the title, the tutorial and Player stats. The art reads the season's colors: from <paramref name="theme"/>, or from
        /// an ancestor's theme.
        /// </summary>
        public static VisualElement Screen(GameScreen screen, ThemeStyleSheet theme = null)
        {
            var icon = ScreenArt(screen);
            Prepare(icon);
            if (theme != null)
                icon.styleSheets.Add(theme);
            return icon;
        }

        /// <summary>A page's nav icon (the game's SVG), alone: Bagel UI's "All shops" and "All recipes" cheats.</summary>
        public static VisualElement Page(AppPage page)
        {
            var icon = PageArt(page);
            Prepare(icon);
            return icon;
        }

        /// <summary>A currency's icon (the game's art): a coin, a Maple Gem, a Lucky Ticket. Bagel UI's cheats.</summary>
        public static VisualElement Currency(CurrencyKind kind)
        {
            var icon = new CurrencyIcon { kind = kind };
            Prepare(icon);
            return icon;
        }

        static VisualElement Svg(string name)
        {
            var icon = new VisualElement();
            icon.AddToClassList(ussClassName + "--" + name);
            return icon;
        }

        static VisualElement PageArt(AppPage page) => Svg(page switch
        {
            AppPage.Shops => "shops",
            AppPage.LuckyBag => "lucky-bag",
            AppPage.Recipes => "recipes",
            AppPage.Settings => "settings",
            _ => "home",
        });

        static VisualElement ScreenArt(GameScreen screen)
        {
            VisualElement Badged(VisualElement badge)
            {
                var icon = PageArt(screen.page);
                badge.pickingMode = PickingMode.Ignore;
                badge.AddToClassList(badgeUssClassName);
                icon.Add(badge);
                return icon;
            }

            if (screen == GameScreen.titleScreen)
                return new Starburst { rayCount = 10 };
            if (screen == GameScreen.tutorial)
                return new TutorialPointer();
            if (screen == GameScreen.playerStats)
                return new BeguChan();
            if (screen == GameScreen.levelUp)
                return Badged(Svg("star"));
            if (screen == GameScreen.reveal)
                return Badged(new PastryIcon(PastryKind.SakuraMochiBagel));
            if (screen == GameScreen.confirm)
                return Badged(new CurrencyIcon { kind = CurrencyKind.Tickets });
            if (screen == GameScreen.settingsPerformance)
            {
                // The performance overlay's graph, full of frames: mostly fast, some slow, a few too slow.
                var graph = new PerfGraph();
                graph.AddToClassList(badgeUssClassName + "--graph");
                var frames = new[] { 12f, 14f, 22f, 15f, 38f, 13f, 16f, 28f, 14f, 12f, 15f, 45f };
                for (int i = 0; i < PerfGraph.capacity; i++)
                    graph.Push(frames[i * 7 / 10 % frames.Length]);
                return Badged(graph);
            }
            return PageArt(screen.page);
        }

        /// <summary>A season's emblem, its index in the theme collection: a maple leaf, a sakura, a festival lantern, a snow crystal.</summary>
        public static VisualElement Season(int index)
        {
            var keys = SettingsPaneManager.seasonKeys;
            var key = keys[Mathf.Clamp(index, 0, keys.Count - 1)];
            VisualElement icon = key == "matsuri" ? new Lantern() : new SeasonEmblem(key);
            Prepare(icon);
            return icon;
        }

        /// <summary>A language: a speech bubble with its letters (あ for Japanese).</summary>
        public static VisualElement Language(string code)
        {
            var icon = new LanguageBubble(code == "ja" ? "あ" : code.ToUpperInvariant());
            Prepare(icon);
            return icon;
        }

        /// <summary>Start again: the Editor's Refresh icon, turned to go back (↺).</summary>
        public static VisualElement Reset()
        {
            var icon = EditorIcon("Refresh");
            icon.style.scale = new Scale(new Vector2(-1f, 1f));
            return icon;
        }

        /// <summary>The Inspector window's icon.</summary>
        public static VisualElement Inspector() => EditorIcon("UnityEditor.InspectorWindow");

        /// <summary>One of the Editor's icons, for the skin (its d_ version in the dark one).</summary>
        static VisualElement EditorIcon(string name)
        {
            var icon = new VisualElement();
            if (EditorGUIUtility.IconContent(name)?.image is Texture2D texture)
                icon.style.backgroundImage = texture;
            Prepare(icon);
            return icon;
        }

        /// <summary>"As authored": the Editor's icon for the asset that says how (the app's UXML, a theme's TSS).</summary>
        public static VisualElement AsAuthored(string assetPath)
        {
            var icon = new VisualElement();
            if (AssetDatabase.GetCachedIcon(assetPath) is Texture2D texture)
                icon.style.backgroundImage = texture;
            Prepare(icon);
            return icon;
        }

        static void Prepare(VisualElement icon)
        {
            icon.pickingMode = PickingMode.Ignore;
            icon.AddToClassList(ussClassName);
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            if (sheet != null)
                icon.styleSheets.Add(sheet);
        }

        /// <summary>A maple leaf, a sakura blossom or a snow crystal, like the season's falling particles.</summary>
        sealed class SeasonEmblem : VisualElement
        {
            readonly string m_Key;
            readonly ArtPen m_Pen = new();

            public SeasonEmblem(string key)
            {
                m_Key = key;
                generateVisualContent += Draw;
            }

            void Draw(MeshGenerationContext mgc)
            {
                var rect = contentRect;
                if (!(rect.width >= 4f) || !(rect.height >= 4f))
                    return;
                var pen = m_Pen;
                pen.Reset(mgc.painter2D, rect, Ink, 0.1f, 1f);
                switch (m_Key)
                {
                    case "sakura":
                        Sakura(pen, 0f, 0.04f, 0.92f, 0f, null, 0.7f);
                        break;
                    case "snow":
                        Snow(pen);
                        break;
                    default:
                        MapleLeaf(pen, 0f, 0.02f, 0.94f, 0f, MapleRed, 0.7f, 1f, false);
                        break;
                }
            }

            static void Snow(ArtPen pen)
            {
                pen.Push(0f, 0f, 0.92f);
                pen.Begin();
                for (int arm = 0; arm < 6; arm++)
                {
                    var a = (arm * 60f - 90f) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var side = new Vector2(-dir.y, dir.x);
                    pen.MoveTo(0f, 0f);
                    pen.LineTo(dir.x, dir.y);
                    var b = dir * 0.55f;
                    pen.MoveTo(b.x, b.y);
                    pen.LineTo(b.x + (dir.x + side.x) * 0.28f, b.y + (dir.y + side.y) * 0.28f);
                    pen.MoveTo(b.x, b.y);
                    pen.LineTo(b.x + (dir.x - side.x) * 0.28f, b.y + (dir.y - side.y) * 0.28f);
                }
                pen.Stroke(Rgb(150, 186, 230), 0.3f);
                pen.Stroke(White, 0.14f);
                pen.Pop();
            }
        }

        /// <summary>A cream speech bubble in the game's ink, its letters on it.</summary>
        sealed class LanguageBubble : VisualElement
        {
            static readonly Color s_Paper = Rgb(255, 248, 234);
            readonly ArtPen m_Pen = new();

            public LanguageBubble(string letters)
            {
                generateVisualContent += Draw;
                var label = new Label(letters) { pickingMode = PickingMode.Ignore };
                var s = label.style;
                s.position = Position.Absolute;
                s.left = 0;
                s.right = 0;
                s.top = Length.Percent(8f);
                s.height = Length.Percent(66f);
                s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
                s.paddingLeft = s.paddingRight = s.paddingTop = s.paddingBottom = 0;
                s.unityTextAlign = TextAnchor.MiddleCenter;
                s.unityFontStyleAndWeight = FontStyle.Bold;
                s.fontSize = letters.Length > 1 ? 7f : 8f;
                s.color = Ink;
                Add(label);
            }

            void Draw(MeshGenerationContext mgc)
            {
                var rect = contentRect;
                if (!(rect.width >= 4f) || !(rect.height >= 4f))
                    return;
                var pen = m_Pen;
                pen.Reset(mgc.painter2D, rect, Ink, 0.1f, 1f);
                pen.Begin();
                Bubble(pen);
                pen.Outline(0.7f);
                pen.Begin();
                Bubble(pen);
                pen.Fill(s_Paper);
            }

            static void Bubble(ArtPen pen)
            {
                pen.AddRoundRect(-0.92f, -0.82f, 1.84f, 1.32f, 0.44f);
                pen.MoveTo(-0.5f, 0.4f);
                pen.LineTo(-0.62f, 0.9f);
                pen.LineTo(-0.1f, 0.42f);
                pen.Close();
            }
        }
    }
}
