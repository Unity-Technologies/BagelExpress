using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// What a Bagel Demo slide's pictures are made of: the real game UI (live screens framed on what a beat is about,
    /// whole phones, the game's art) and the Editor's windows (<see cref="ed"/>, a <see cref="BagelDemoEditorKit"/>).
    /// One per slide: it remembers every live screen it made, and <see cref="Release"/> lets them all go when the slide
    /// does. The screens hold still (<see cref="ArtKit.HoldStill"/>): they're pictures, and a slide left open in Play
    /// mode shouldn't cost the game its frames.
    /// </summary>
    public sealed class BagelDemoVisuals
    {
        public static readonly string ussClassName = "bgd-vis";
        public static readonly string rowUssClassName = ussClassName + "__row";
        public static readonly string columnUssClassName = ussClassName + "__column";
        public static readonly string arrowUssClassName = ussClassName + "__arrow";
        public static readonly string cropUssClassName = ussClassName + "__crop";
        public static readonly string selectedCropUssClassName = ussClassName + "__crop--selected";
        public static readonly string afterUssClassName = ussClassName + "__crop--after";
        public static readonly string phoneUssClassName = ussClassName + "__phone";
        public static readonly string tapColorHex = "#FFC428";
        const float k_PhoneZoom = 0.42f;
        // The Bagel Features window's styles: its thumbnails of Editor features are styled there (under .bgf).
        const string k_FeaturesStylesPath = "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeaturesWindowStyles.uss";

        readonly BagelGuideWindow m_Window;
        readonly VisualTreeAsset m_AppTree;
        readonly List<ScreenCrop> m_Crops = new();
        readonly List<PhoneFrame> m_Phones = new();
        readonly List<VisualElement> m_ThemedRoots = new();
        readonly List<VisualElement> m_StillRoots = new();

        /// <summary>The Editor's windows, in the Editor's theme.</summary>
        public readonly BagelDemoEditorKit ed;

        public IReadOnlyList<ScreenCrop> crops => m_Crops;
        public IReadOnlyList<PhoneFrame> phones => m_Phones;

        public BagelDemoVisuals(BagelGuideWindow window)
        {
            m_Window = window;
            m_AppTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelExpressAllUIWindow.appUxmlPath);
            ed = new BagelDemoEditorKit(window.editorThemeSheets);
        }

        /// <summary>Let go of every live screen and themed root this slide made.</summary>
        public void Release()
        {
            foreach (var root in m_StillRoots)
                ArtKit.HoldStill(root, false);
            m_StillRoots.Clear();
            foreach (var crop in m_Crops)
                crop.Release();
            m_Crops.Clear();
            foreach (var phone in m_Phones)
            {
                m_Window.RemoveThemedRoot(phone.screenRoot);
                Discard.Element(phone);
            }
            m_Phones.Clear();
            foreach (var root in m_ThemedRoots)
                m_Window.RemoveThemedRoot(root);
            m_ThemedRoots.Clear();
        }

        // ------------------------------------------------------------------ the game

        /// <summary>A live screen framed on some elements, ringed (none: the whole screen).</summary>
        public VisualElement Crop(GameScreen screen, params string[] focus) => BuildCrop(screen, focus, zoom: true);

        /// <summary>A whole live screen, rings on <paramref name="rings"/>.</summary>
        public VisualElement Screen(GameScreen screen, params string[] rings) => BuildCrop(screen, rings, zoom: false);

        /// <summary>
        /// A live screen with one element selected the way the UI Viewport shows it, an outline and its name: the whole
        /// screen, or <paramref name="zoomed"/> in on it.
        /// </summary>
        public VisualElement Selected(GameScreen screen, string selector, string label, bool zoomed = false)
        {
            var crop = BuildCrop(screen, new[] { selector }, zoom: zoomed, label);
            crop.AddToClassList(selectedCropUssClassName);
            return crop;
        }

        /// <summary>
        /// A live screen after a beat's edit, framed on <paramref name="focus"/> (null: the whole screen): <paramref name="change"/>
        /// is done to its elements once it's set up, and <paramref name="uss"/> (rules with an id, to win over the game's) is
        /// added as a style sheet of its own.
        /// </summary>
        public VisualElement Changed(GameScreen screen, string[] focus, Action<VisualElement> change = null, string uss = null)
        {
            var crop = BuildCrop(screen, focus ?? Array.Empty<string>(), zoom: focus != null && focus.Length > 0);
            crop.AddToClassList(afterUssClassName);
            var root = crop.screenRoot;
            if (root == null)
                return crop;
            if (!string.IsNullOrEmpty(uss))
            {
                var sheet = BagelArtGuide.CreateStyleSheet(uss);
                if (sheet != null)
                    root.styleSheets.Add(sheet);
            }
            if (change != null)
            {
                // After the screen's own set-up (300 ms), so it isn't undone.
                crop.schedule.Execute(() =>
                {
                    try
                    {
                        change(root);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Bagel] Bagel Demo: a picture's change on {screen.title} failed: {e.Message}");
                    }
                }).ExecuteLater(450);
            }
            return crop;
        }

        ScreenCrop BuildCrop(GameScreen screen, string[] focus, bool zoom, string label = null)
        {
            var crop = new ScreenCrop(screen, focus, 160f) { zoomToFocus = zoom, ringLabel = label };
            crop.AddToClassList(cropUssClassName);
            m_Crops.Add(crop);
            crop.Build(m_AppTree, m_Window.state, m_Window.AddThemedRoot, m_Window.RemoveThemedRoot);
            HoldStill(crop.screenRoot);
            return crop;
        }

        /// <summary>A whole phone, live and pinned to its screen, its taps ringed in gold.</summary>
        public VisualElement Phone(GameScreen screen, params string[] taps)
        {
            var phone = new PhoneFrame(screen, m_AppTree, m_Window.state) { pinScreen = true };
            phone.AddToClassList(phoneUssClassName);
            phone.SetZoom(k_PhoneZoom);
            ColorUtility.TryParseHtmlString(tapColorHex, out var gold);
            foreach (var tap in taps)
                phone.AddHotspot(tap, tap, "", null, gold, null);
            m_Window.AddThemedRoot(phone.screenRoot);
            HoldStill(phone.screenRoot);
            m_Phones.Add(phone);
            return phone;
        }

        /// <summary>The game's art drawn in code, on a card in the season's colors.</summary>
        public VisualElement Art(params VisualElement[] art)
        {
            var card = Themed(El(ussClassName + "__art"));
            foreach (var item in art)
            {
                item.pickingMode = PickingMode.Ignore;
                item.AddToClassList(ussClassName + "__art-item");
                card.Add(item);
            }
            return card;
        }

        /// <summary>A feature's thumbnail from Bagel Features.</summary>
        public VisualElement Tile(string featureId)
        {
            var host = Themed(El(ussClassName + "__tile bgf"));
            BagelFeatureTiles.AddStyleSheets(host);
            var featuresSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_FeaturesStylesPath);
            if (featuresSheet != null)
                host.styleSheets.Add(featuresSheet);
            host.dataSource = m_Window.state;
            var tile = BagelFeatureTiles.Create(featureId, m_Window);
            if (tile != null)
                host.Add(tile);
            var feature = BagelFeatureCatalog.Get(featureId);
            if (feature != null)
                host.Add(Label(feature.title, ussClassName + "__tile-title"));
            return host;
        }

        /// <summary>A feature's first snippet from Bagel Features, its code colored.</summary>
        public VisualElement Snippet(string featureId)
        {
            var view = El(ussClassName + "__snippet");
            var snippet = BagelFeatureGuide.SnippetsOf(featureId).FirstOrDefault();
            if (snippet == null)
                return view;
            var head = El(ussClassName + "__snippet-head");
            head.Add(Label(snippet.languageLabel, ussClassName + "__snippet-language"));
            head.Add(Label(snippet.caption ?? "", ussClassName + "__snippet-caption"));
            view.Add(head);
            var code = Label(BagelCodeHighlighter.ToRichText(snippet.code, snippet.language), ussClassName + "__snippet-code");
            code.enableRichText = true;
            if (monospaceFont != null)
                code.style.unityFontDefinition = FontDefinition.FromFont(monospaceFont);
            view.Add(code);
            return view;
        }

        static Font s_Monospace;
        static Font monospaceFont => s_Monospace ??= EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;

        /// <summary>The demo toolbar's Language strip, the language now lit.</summary>
        public VisualElement LanguageStrip()
        {
            var strip = new BagelOptionStrip { pickingMode = PickingMode.Ignore };
            strip.AddToClassList(ussClassName + "__strip");
            BagelPickerIcons.AddLanguages(strip);
            strip.SetSelectedWithoutNotify(BagelPickerIcons.LanguageIndex(BagelLocalization.localeCode));
            var row = Row(Label("Language", ussClassName + "__strip-label"), strip);
            return row;
        }

        /// <summary>The demo toolbar's Screen option for a screen, with its name: what to pick before a beat.</summary>
        public VisualElement ScreenChip(GameScreen screen)
        {
            var chip = El(ussClassName + "__chip");
            var option = BagelOptionStrip.CreateOption(BagelPickerIcons.Screen(screen, m_Window.currentTheme), screen.title, true, null);
            option.pickingMode = PickingMode.Ignore;
            BagelOptionStrip.AddStyleSheet(option);
            option.AddToClassList(ussClassName + "__chip-option");
            chip.Add(option);
            chip.Add(Label(screen.title, ussClassName + "__chip-label"));
            return chip;
        }

        /// <summary>The Bagel Features window in Version mode: a column per version, its features' thumbnails under it.</summary>
        public VisualElement FeaturesWindow(params (string version, string[] features)[] groups)
        {
            var (window, body) = ed.Window("Bagel Features", gameInside: true);
            var content = El(ussClassName + "__features");
            var modes = El(ussClassName + "__features-modes");
            foreach (var mode in new[] { "Game", "Category", "Version", "Art" })
            {
                var button = Label(mode, ussClassName + "__features-mode");
                button.EnableInClassList(ussClassName + "__features-mode--on", mode == "Version");
                modes.Add(button);
            }
            content.Add(modes);
            var columns = El(ussClassName + "__features-groups");
            content.Add(columns);
            foreach (var (version, features) in groups)
            {
                var column = El(ussClassName + "__features-group-column");
                columns.Add(column);
                var group = BagelFeatureGuide.versions.FirstOrDefault(g => g.id == version);
                var header = El(ussClassName + "__features-group");
                var chip = Label(group?.shortTitle ?? version, ussClassName + "__version");
                if (group != null)
                    chip.style.backgroundColor = group.color;
                header.Add(chip);
                column.Add(header);
                var row = El(ussClassName + "__features-row");
                foreach (var id in features)
                    row.Add(Tile(id));
                column.Add(row);
            }
            body.Add(content);
            return window;
        }

        /// <summary>The Bagel UI window: a few of its phones, live, side by side.</summary>
        public VisualElement BagelUIWindow(params GameScreen[] screens)
        {
            var (window, body) = ed.Window("Bagel UI", gameInside: true);
            var stage = El(ussClassName + "__bagel-ui");
            foreach (var screen in screens)
            {
                var phone = (PhoneFrame)Phone(screen);
                phone.SetZoom(0.3f);
                stage.Add(phone);
            }
            body.Add(stage);
            return window;
        }

        VisualElement Themed(VisualElement root)
        {
            m_Window.AddThemedRoot(root);
            m_ThemedRoots.Add(root);
            HoldStill(root);
            return root;
        }

        void HoldStill(VisualElement root)
        {
            if (root == null)
                return;
            ArtKit.HoldStill(root, true);
            m_StillRoots.Add(root);
        }

        // ------------------------------------------------------------------ layout

        public static VisualElement Row(params VisualElement[] children) => Group(rowUssClassName, children);

        public static VisualElement Column(params VisualElement[] children) => Group(columnUssClassName, children);

        /// <summary>Before, then after: an arrow between.</summary>
        public static VisualElement Then(VisualElement before, VisualElement after) => Row(before, Arrow(), after);

        /// <summary>A small arrow, drawn: what happens next, or where something goes.</summary>
        public static VisualElement Arrow()
        {
            var arrow = new BagelDemoArrow { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList(arrowUssClassName);
            return arrow;
        }

        static VisualElement Group(string className, VisualElement[] children)
        {
            var group = El(className);
            foreach (var child in children)
                if (child != null)
                    group.Add(child);
            return group;
        }

        static VisualElement El(string classNames)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                element.AddToClassList(className);
            return element;
        }

        static Label Label(string text, string classNames)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                label.AddToClassList(className);
            return label;
        }
    }

    public static class BagelDemoVisualsExtensions
    {
        /// <summary>A picture's own size (0: as its content makes it).</summary>
        public static T Sized<T>(this T element, float width, float height) where T : VisualElement
        {
            if (width > 0f)
                element.style.width = width;
            if (height > 0f)
                element.style.height = height;
            element.style.flexShrink = 0f;
            return element;
        }
    }

    /// <summary>A short arrow pointing right, drawn with Painter2D in the slide's ink.</summary>
    public sealed class BagelDemoArrow : VisualElement
    {
        static readonly CustomStyleProperty<Color> s_Color = new("--bgd-arrow-color");
        Color m_Color = new(1f, 0.85f, 0.55f);

        public BagelDemoArrow()
        {
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(s_Color, out var color))
                {
                    m_Color = color;
                    MarkDirtyRepaint();
                }
            });
        }

        void Draw(MeshGenerationContext context)
        {
            var r = contentRect;
            if (!(r.width > 4f) || !(r.height > 4f))
                return;
            var p = context.painter2D;
            var y = r.height * 0.5f;
            var head = Mathf.Min(10f, r.width * 0.4f);
            p.lineWidth = 4f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.strokeColor = m_Color;
            p.BeginPath();
            p.MoveTo(new Vector2(4f, y));
            p.LineTo(new Vector2(r.width - 5f, y));
            p.MoveTo(new Vector2(r.width - 5f - head, y - head));
            p.LineTo(new Vector2(r.width - 5f, y));
            p.LineTo(new Vector2(r.width - 5f - head, y + head));
            p.Stroke();
        }
    }
}
