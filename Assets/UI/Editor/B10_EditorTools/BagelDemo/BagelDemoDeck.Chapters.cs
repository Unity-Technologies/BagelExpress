using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.BagelDemoDeck.Area;
using static Bagel.BagelDemoDeck.Presenter;
using static Bagel.BagelDemoEditorKit;
using static Bagel.BagelDemoVisuals;
using static Bagel.EditorLink.InspectorFilter;

namespace Bagel
{
    public static partial class BagelDemoDeck
    {
        // Files the beats open when lost.
        const string k_Runtime = "Assets/UI/Runtime/";
        const string k_MascotBob = k_Runtime + "B02_Common/Animations/MascotBob.asset";
        const string k_TitleIntro = k_Runtime + "B04_Title/TitleIntro.anim";
        const string k_CommonStyles = k_Runtime + "B00_Themes/T0_Common/BagelExpressCommonStyles.uss";
        const string k_Sampler = k_Runtime + "B00_Themes/T0_Common/BagelArtSampler.uxml";

        // Colors the pictures show.
        static readonly Color s_SurfaceTop = new(1f, 0.97f, 0.9f);
        static readonly Color s_SurfaceBottom = new(0.98f, 0.9f, 0.78f);
        static readonly Color s_SkyMid = new(0.67f, 0.8f, 0.92f);

        // What the pictures change in their "after" screens (always with an id: the game's own rules are on deeper elements).
        const string k_BubbleSky = "#speech-bubble.be-bubble { background-color: var(--be-sky-mid); }";
        const string k_RegisterGap = "#register-card.be-register { gap: 16px; }";
        const string k_GoldRadius = "#app .be-btn--gold { border-radius: 4px; }";

        static Chapter[] CreateChapters() => new[]
        {
            // ================================================================== the game, played (Play stays on until the wrap-up)
            Strip("meet", "Meet Bagel Express", "ゲーム紹介", Designer, 180, V(),
                new Setup(null, note: "Maple Autumn · English · sound on · Play stays on until the wrap-up"),
                "A Montréal × Tokyo bakery, and every bit of it is UI Toolkit.",
                "“No textures, and I change it myself, while it plays. So where does it all live?”",
                B("TAP TO START", "Press Play. The title intro: BAGEL and EXPRESS drop in, the hanko stamps, Begu-chan spins in. Let it breathe, then tap TAP TO START.",
                    At(Game), v => v.Phone(GameScreen.titleScreen, "#tap-to-start"), play: true, link: EditorLink.Play()),
                B("<b>COLLECT</b>", "The tutorial points at COLLECT: ka-ching, the coins fly into the HUD, each one chiming higher.",
                    At(Game), v => v.Phone(GameScreen.tutorial, "#collect-button")),
                B("Tap the shop: <b>+9</b>", "Tap the storefront a few times: a puff of flour, coins, and a +9 pop every time.",
                    At(Game), v => v.Phone(GameScreen.home, "#storefront")),
                B("Shops › <b>UPGRADE</b>", "The tutorial leads to Shops: UPGRADE Mile End, Lv 4 → 5 (a milestone).",
                    At(Game), v => v.Phone(GameScreen.shops, ".be-shop-card #action-button")),
                B("<b>LEVEL UP!</b>", "A fanfare, a manga screentone, speed lines, a starburst and confetti.",
                    At(Game), v => v.Phone(GameScreen.levelUp)),
                B("Lucky Bag › <b>DRAW ×1</b>", "The gold tab in the middle › DRAW ×1: the bag rustles, shakes and glows… SSR Sakura Mochi Bagel!",
                    At(Game), v => v.Phone(GameScreen.reveal)),
                B("<b>Sakura</b> · <b>日本語</b> · back", "Settings › Sakura Spring: the app re-themes and the buttons pluck a koto. Tap 日本語: everything switches. Back to Maple Autumn and English, then Home. Don't stop Play: the whole talk happens while the game runs.",
                    At(Game), v => v.Phone(GameScreen.settings, "#season-grid", "#language-segments"))),

            // ================================================================== 6.7: the in-scene workflow
            C("scene", "The UI is in the scene", "UIはシーンの中に", Developer, 240, V("6.7"),
                new Setup(GameScreen.home, play: true, note: "Still playing"),
                "In 6.7 the UI is in the scene: one Hierarchy and one Inspector for GameObjects and elements, while it plays.",
                "“Everything I just clicked on, you can change, while it plays. Want to drive?”",
                B("Three <b>GameObjects</b>", "Main has three GameObjects: Main Camera, BagelExpressApp and Tutorial. Select BagelExpressApp: its driver, an Animation, and a Panel Renderer with its Panel Settings and its Source Asset, BakeryAppScreen.uxml.",
                    At(Hierarchy, Inspector), v => Column(
                        v.ed.Hierarchy(null,
                            H(0, "Main", sceneIcon, mark: Mark.Open | Mark.Bold),
                            H(1, "Main Camera", "Camera Icon"),
                            H(1, "BagelExpressApp", "PanelRenderer Icon", mark: Mark.Closed | Mark.Selected),
                            H(1, "Tutorial", "cs Script Icon")).Sized(330, 0),
                        v.ed.Inspector(
                            v.ed.GameObjectHeader("BagelExpressApp"),
                            v.ed.Component("Panel Renderer", "PanelRenderer Icon",
                                v.ed.Field("Panel Settings", v.ed.ObjectValue("ScreenSpacePanelSettings", "PanelSettings Icon")),
                                v.ed.Field("Source Asset", v.ed.ObjectValue("BakeryAppScreen", "UxmlScript Icon"), FieldMark.Highlighted))).Sized(330, 0)),
                    link: EditorLink.GameObject("BagelExpressApp")),
                B("Its <b>elements</b>, in the Scene too", "Expand it: BakeryAppScreen.uxml, #home-page HomePage.uxml, then every element. Select one: the Inspector shows it, and the Scene view previews the panel, the selection outlined.",
                    At(Hierarchy, Scene, Inspector), v => v.ed.Hierarchy(null,
                        H(0, "BagelExpressApp", "PanelRenderer Icon", mark: Mark.Open),
                        H(1, "BakeryAppScreen", uxmlIcon, asset: ".uxml", mark: Mark.Open | Mark.Instance),
                        H(2, "app", "cs Script Icon", ".be-app", mark: Mark.Open),
                        H(3, "pages", "VisualElement", ".be-fill .be-pages", mark: Mark.Open),
                        H(4, "home-page", uxmlIcon, asset: "HomePage.uxml", mark: Mark.Open | Mark.Instance),
                        H(5, "home", "cs Script Icon", ".be-home", mark: Mark.Open),
                        H(6, "home-content", "VisualElement", ".be-page-content", mark: Mark.Open),
                        H(7, "event-banner", "Button", ".be-home__event", mark: Mark.Closed),
                        H(7, "storefront", "VisualElement", ".be-store", mark: Mark.Closed),
                        H(7, "mascot-row", "VisualElement", ".be-home__mascot-row", mark: Mark.Closed),
                        H(7, "register-card", "VisualElement", ".be-panel .be-register", mark: Mark.Open),
                        H(8, "collect-button", "Button", ".be-btn .be-btn--gold", mark: Mark.Selected),
                        H(4, "shops-page", uxmlIcon, asset: "ShopsPage.uxml", mark: Mark.Closed | Mark.Instance)).Sized(380, 0),
                    link: EditorLink.Select("collect-button")),
                B("The <b>UI Viewport</b> · Preview", "The UI Viewport tab: click COLLECT on the canvas, the Hierarchy and the Inspector follow. Preview mode makes its buttons work. Every page is in the document: #pages holds Home, Shops, the Lucky Bag, the Recipe Book and Settings.",
                    At(UIViewport, Hierarchy), v => v.ed.UIViewport(new[] { "BagelExpressApp", "BakeryAppScreen" }, "BakeryAppScreen.uxml",
                        v.Selected(GameScreen.home, "#collect-button", "collect-button", zoomed: true).Sized(360, 200)),
                    link: EditorLink.Select("collect-button")),
                B("A <b>custom toolbar</b>", "The main toolbar's demo strips are custom toolbar elements: the screen, the season and the language, for the UI Viewport, the UI Builder and the Scene preview. The Game view keeps playing.",
                    At(Toolbar), v => Column(v.ScreenChip(GameScreen.home), v.LanguageStrip())),
                B("Search, then <b>switch screens</b>", "Search the Hierarchy for uit:PastryIcon: every pastry in the game, one custom element. classes:be-btn finds every button. Then demo toolbar › Screen › Shops: the UI Viewport shows the page the result is on.",
                    At(Hierarchy, Toolbar), v => Column(
                        v.ed.Hierarchy("uit:PastryIcon",
                            H(0, "event-pastry", "VisualElement", ".be-home__event-pastry"),
                            H(0, "pastry", "VisualElement", ".be-store__pastry", mark: Mark.Selected),
                            H(0, "pastry", "VisualElement", ".be-store__pastry"),
                            H(0, "signature", "VisualElement", ".be-shop-card__pastry"),
                            H(0, "level-up-pastry", "VisualElement", ".be-level-up__pastry")).Sized(330, 0),
                        v.ScreenChip(GameScreen.shops)),
                    screen: GameScreen.shops, link: EditorLink.HierarchySearch("uit:PastryIcon")),
                B("In context · in <b>isolation</b>", "Back to Home. Right-click #store-pastry-1 › Open Instance › In Context. Then the Style Sheets tab, and #home-page in isolation (the › on its row): click .be-home__event and the Inspector shows the rule.",
                    At(Hierarchy, StyleSheets, Inspector), v => Column(
                        v.ed.StyleSheets("be-home", "HomeStyles.uss", new[] { ".be-home", ".be-home__content", ".be-home__event", ".be-home__event-art", ".be-home__mascot-row" }, ".be-home__event").Sized(330, 0),
                        v.ed.Inspector(
                            v.ed.RuleHeader(".be-home__event", "HomeStyles.uss"),
                            v.ed.Section("Background",
                                v.ed.Field("Image", v.ed.Dropdown("Gradient")),
                                v.ed.Field("Angle", v.ed.NumberValue("110", "deg"), FieldMark.Sub))).Sized(330, 0)),
                    screen: GameScreen.home, link: EditorLink.StyleSheets(".be-home__event"))),

            C("change", "I can change it myself", "自分で変えられる", Designer, 270, V("6.7"),
                new Setup(GameScreen.home, play: true, note: "Still playing"),
                "Every edit lands in a file, in Play mode too: the page's UXML or a style sheet.",
                "“I love the shadows and the glass. Can we afford them on a phone?”",
                B("<b>gold</b> → <b>rose</b>", "Click COLLECT on the canvas. In the Inspector's Style Class List, swap be-btn--gold for be-btn--rose. The Game view follows, and the Hierarchy shows HomePage.uxml *: the edit is in the page's UXML.",
                    At(UIViewport, Inspector), v => Column(
                        v.ed.Inspector(
                            v.ed.Section("Style Class List",
                                v.ed.ClassList(("be-btn", ClassState.Kept), ("be-btn--gold", ClassState.Removed), ("be-btn--rose", ClassState.Added)))).Sized(330, 0),
                        Then(v.Crop(GameScreen.home, "#collect-button").Sized(150, 84),
                            v.Changed(GameScreen.home, new[] { "#collect-button" }, root =>
                            {
                                var button = root.Q("collect-button");
                                button?.RemoveFromClassList("be-btn--gold");
                                button?.AddToClassList("be-btn--rose");
                            }).Sized(150, 84))),
                    link: EditorLink.Select("collect-button")),
                B("Banner <b>down</b>, and back up", "In the Hierarchy, drag #event-banner below #storefront: the banner moves, in the UI Viewport and the game. Drag it back up.",
                    At(Hierarchy, UIViewport), v => Column(
                        v.ed.Hierarchy(null,
                            H(0, "home-content", "VisualElement", ".be-page-content", mark: Mark.Open),
                            H(1, "event-banner", "Button", ".be-home__event", mark: Mark.Closed | Mark.Dragged),
                            H(1, "storefront", "VisualElement", ".be-store", mark: Mark.Closed | Mark.DropBelow),
                            H(1, "mascot-row", "VisualElement", ".be-home__mascot-row", mark: Mark.Closed),
                            H(1, "register-card", "VisualElement", ".be-panel .be-register", mark: Mark.Closed)).Sized(330, 0),
                        Then(v.Crop(GameScreen.home, "#event-banner", "#storefront").Sized(150, 150),
                            v.Changed(GameScreen.home, new[] { "#event-banner", "#storefront" }, root =>
                            {
                                var banner = root.Q("event-banner");
                                var store = root.Q("storefront");
                                if (banner != null && store != null)
                                    store.parent.Insert(store.parent.IndexOf(store) + 1, banner);
                            }).Sized(150, 150))),
                    link: EditorLink.Select("event-banner")),
                B("One template, <b>six pastries</b>", "#store-pastry-1 StorePastry.uxml is open in context: select its #pastry, search width, and make it 46 × 46. The six pastries on the shelves change together.",
                    At(Hierarchy, UIViewport, Inspector), v => Column(
                        v.ed.Hierarchy(null,
                            H(0, "store-window", "VisualElement", ".be-store__window", mark: Mark.Open),
                            H(1, "store-pastry-1", uxmlIcon, asset: "StorePastry.uxml", mark: Mark.Open | Mark.Instance | Mark.Selected),
                            H(2, "pastry", "VisualElement", ".be-store__pastry"),
                            H(1, "store-pastry-2", uxmlIcon, asset: "StorePastry.uxml", mark: Mark.Closed | Mark.Instance)).Sized(330, 0),
                        Row(
                            v.ed.Inspector(
                                v.ed.Search("width"),
                                v.ed.Section("Size",
                                    v.ed.Field("Width", v.ed.NumberValue("46", "px"), FieldMark.Highlighted),
                                    v.ed.Field("Height", v.ed.NumberValue("46", "px"), FieldMark.Highlighted))).Sized(170, 0),
                            v.Changed(GameScreen.home, new[] { "#store-window" }, root =>
                            {
                                root.Q("store-window")?.Query(name: "pastry").ForEach(pastry =>
                                {
                                    pastry.style.width = 46f;
                                    pastry.style.height = 46f;
                                });
                            }).Sized(150, 120))),
                    link: EditorLink.UIViewport("store-pastry-1", "pastry", "width", inContext: true)),
                B("A <b>PastryIcon</b>, from the UI Library", "UI Library › Project › Bagel › Art: drag PastryIcon onto #mascot-row, after #speech-bubble: it sits right of Begu-chan's bubble. Its Inspector is custom: pick the pastry with its own picker (a UXML property drawer, 6.7).",
                    At(UILibrary, Hierarchy, Inspector), v => Column(
                        Row(
                            v.ed.Library("Project",
                                H(0, "Bagel", "FolderOpened Icon", mark: Mark.Open),
                                H(1, "Art", "FolderOpened Icon", mark: Mark.Open),
                                H(2, "BeguChan", "VisualElement"),
                                H(2, "PastryIcon", "VisualElement", mark: Mark.Selected),
                                H(2, "Starburst", "VisualElement")).Sized(170, 0),
                            Arrow(),
                            v.ed.Hierarchy(null,
                                H(0, "mascot-row", "VisualElement", mark: Mark.Open),
                                H(1, "mascot", "VisualElement"),
                                H(1, "speech-bubble", "VisualElement"),
                                H(1, "PastryIcon", "VisualElement", mark: Mark.Selected | Mark.New)).Sized(170, 0)),
                        Row(
                            v.ed.Inspector(v.ed.Field("Kind", v.ed.PastryPicker(PastryKind.MelonPan, PastryKind.Anpan, PastryKind.MelonPan, PastryKind.Taiyaki), FieldMark.Highlighted)).Sized(200, 0),
                            v.Changed(GameScreen.home, new[] { "#mascot-row" }, root =>
                            {
                                var pastry = new PastryIcon(PastryKind.MelonPan) { name = "new-pastry" };
                                pastry.style.width = 56f;
                                pastry.style.height = 56f;
                                pastry.style.alignSelf = Align.Center;
                                root.Q("mascot-row")?.Add(pastry);
                            }).Sized(150, 90))),
                    link: EditorLink.Select("mascot-row")),
                B("Find the <b>bubble</b> · Set Values", "Search the Hierarchy for classes:be-bubble and select #speech-bubble. In the Inspector, search background: the search finds any style by name. Set Values shows only what this element sets.",
                    At(Hierarchy, Inspector), v => Column(
                        v.ed.Hierarchy("classes:be-bubble",
                            H(0, "speech-bubble", "VisualElement", ".be-bubble", mark: Mark.Selected)).Sized(330, 0),
                        v.ed.Inspector(
                            v.ed.ElementHeader("SpeechBubble", "VisualElement", "speech-bubble", "HomePage (Visual Tree Asset)"),
                            v.ed.Search("background"),
                            v.ed.Filters("Set Values")).Sized(330, 0)),
                    link: EditorLink.HierarchySearch("classes:be-bubble")),
                B("Its color: <b>var(--be-sky-mid)</b>", "Background › Color: pick the variable --be-sky-mid instead of a color. The bubble takes the sky's color, and each season's theme sets its own.",
                    At(Inspector, UIViewport), v => Column(
                        v.ed.Inspector(
                            v.ed.Section("Background",
                                v.ed.Field("Color", v.ed.TextValue("var(--be-sky-mid)"), FieldMark.Variable | FieldMark.Highlighted))).Sized(330, 0),
                        Row(v.ed.ColorValue(s_SkyMid),
                            v.Changed(GameScreen.home, new[] { "#mascot-row" }, uss: k_BubbleSky).Sized(170, 90))),
                    link: EditorLink.Select("speech-bubble", "background"))),

            // ================================================================== 6.7: shadows, filters, and what they cost
            C("effects", "Shadows, filters and their cost", "影・フィルター・コスト", Developer, 240, V("6.7"),
                new Setup(GameScreen.home, play: true, note: "Still playing"),
                "box-shadow is geometry. A filter is an offscreen pass: the Profiler shows the difference.",
                "“Cheap shadows, then. Can I make it prettier?”",
                B("<b>box-shadow</b>: the storefront", "Select #store-facade and search shadow: box-shadow: 0 6px 0 var(--be-ink), new in 6.7. Change it inline (0 12px 0), then Unset it.",
                    At(UIViewport, Inspector), v => Column(
                        v.Crop(GameScreen.home, "#store-facade").Sized(400, 170),
                        v.ed.Inspector(
                            v.ed.Search("shadow"),
                            v.ed.Section("Box Shadow",
                                v.ed.Field("Box Shadow", v.ed.TextValue("0 6px 0 var(--be-ink)"), FieldMark.Highlighted))).Sized(400, 0)),
                    link: EditorLink.Select("store-facade", "shadow")),
                B("<b>filter</b>: the Home icon", "Select #nav-home-icon and search filter: three chained drop-shadow()s, a glow that hugs the icon. Change it inline, then Unset it.",
                    At(UIViewport, Inspector), v => Column(
                        v.Crop(GameScreen.home, "#nav-home-icon").Sized(400, 150),
                        v.ed.Inspector(
                            v.ed.Search("filter"),
                            v.ed.Section("Filter",
                                v.ed.Field("Filter", v.ed.TextValue("drop-shadow(0 0 4px var(--be-gold-dark)) ×2 …"), FieldMark.Highlighted))).Sized(400, 0)),
                    link: EditorLink.Select("nav-home-icon", "filter")),
                B("<b>backdrop-filter</b>: the Recipe Book", "Demo toolbar › Screen › Recipes. Select #recipe-glass and search backdrop: the frosted glass is blur(10px). Raise it inline, then Unset it.",
                    At(Toolbar, UIViewport, Inspector), v => Column(
                        v.Crop(GameScreen.recipes, "#recipe-glass").Sized(400, 170),
                        v.ed.Inspector(
                            v.ed.Search("backdrop"),
                            v.ed.Section("Backdrop Filter",
                                v.ed.Field("Backdrop Filter", v.ed.TextValue("blur(10px)"), FieldMark.Highlighted))).Sized(400, 0)),
                    screen: GameScreen.recipes, link: EditorLink.Select("recipe-glass", "backdrop")),
                B("The <b>in-game profiler</b> · shadows", "In the game: Settings › Performance, turn on the performance overlay, then Blurred drop-shadows on panes (or press F): every pane gets a soft drop-shadow.",
                    At(Game), v => Column(
                        v.Crop(GameScreen.settingsPerformance, "#perf-overlay-toggle", "#filter-shadows-toggle").Sized(340, 150),
                        v.ed.Key("F"))),
                B("<b>Profiler</b>: cost and batches", "Open the Recipe Book in the game, then the Profiler tab on its UI Toolkit modules: scroll the tables sideways for each panel's cost, then the batch count and why each batch broke.",
                    At(Game, Profiler), v => v.ed.Profiler(true).Sized(420, 210),
                    link: EditorLink.Profiler()),
                B("Now <b>box-shadow</b>: the diff", "Settings › Performance › Blurred box-shadows on panes (or F again): the same shadows as geometry. The Profiler shows the frame time and the batches fall back.",
                    At(Game, Profiler), v => Column(
                        Row(v.ed.Profiler(false).Sized(300, 150), v.ed.Key("F")),
                        v.Crop(GameScreen.settingsPerformance, "#box-shadows-toggle").Sized(340, 110)),
                    link: EditorLink.Profiler())),

            // ================================================================== 6.7: style and motion
            C("style", "Style and motion", "スタイルと動き", Designer, 270, V("6.7"),
                new Setup(GameScreen.home, play: true, note: "Still playing"),
                "Gradients, gap, a custom filter and clips: assets I change in the Editor, while it plays.",
                "“Lovely. And where does the data come from? In Japanese? In French?”",
                B("A <b>gradient</b>: the register card", "Select #register-card and search background: its paper is a linear-gradient from .be-panel, new in 6.7. Open it and change a stop.",
                    At(UIViewport, Inspector), v => Row(
                        v.Crop(GameScreen.home, "#register-card").Sized(220, 150),
                        v.ed.Inspector(
                            v.ed.Search("background"),
                            v.ed.Section("Background",
                                v.ed.Field("Image", v.ed.Dropdown("Gradient"), FieldMark.Variable),
                                v.ed.Field("Type", v.ed.Dropdown("Linear"), FieldMark.Sub),
                                v.ed.Field("Stops", v.ed.GradientValue(s_SurfaceTop, s_SurfaceBottom), FieldMark.Sub))).Sized(220, 0)),
                    link: EditorLink.Select("register-card", "background")),
                B("<b>gap</b> around COLLECT", "Still on #register-card, search gap: one gap spaces the header, the meter and COLLECT, new in 6.7. Raise it: COLLECT moves away.",
                    At(Inspector, UIViewport), v => Column(
                        v.ed.Inspector(
                            v.ed.Search("gap"),
                            v.ed.Field("Gap", v.ed.NumberValue("8 → 16", "px"), FieldMark.Highlighted)).Sized(330, 0),
                        Then(v.Crop(GameScreen.home, "#register-card").Sized(150, 110),
                            v.Changed(GameScreen.home, new[] { "#register-card" }, uss: k_RegisterGap).Sized(150, 110))),
                    link: EditorLink.Select("register-card", "gap")),
                B("LEVEL UP!'s halftone: <b>spacing</b>", "Demo toolbar › Screen › LEVEL UP!. Select #level-up-scrim and search backdrop: a custom filter function, MangaHalftone, new in 6.7. Change its spacing, 10px → 16px.",
                    At(Toolbar, UIViewport, Inspector), v => Row(
                        v.Screen(GameScreen.levelUp).Sized(150, 324),
                        v.ed.Inspector(
                            v.ed.Search("backdrop"),
                            v.ed.Section("Backdrop Filter",
                                v.ed.Field("Filter", v.ed.ObjectValue("MangaHalftone", "ScriptableObject Icon")),
                                v.ed.Field("Spacing", v.ed.NumberValue("10 → 16", "px"), FieldMark.Sub | FieldMark.Highlighted),
                                v.ed.Field("Blur", v.ed.NumberValue("1", "px"), FieldMark.Sub))).Sized(210, 0)),
                    screen: GameScreen.levelUp, link: EditorLink.Select("level-up-scrim", "backdrop")),
                B("<b>MascotBob</b>, a UI Animation Clip", "Screen › Home, select #mascot, search animation: MascotBob, a UI Animation Clip from USS. Double-click it: the Animation window opens it. Dock it at the bottom, and change Translate.y.value's second key, -7 → -14.",
                    At(Inspector, Area.Animation), v => Column(
                        v.ed.Inspector(
                            v.ed.Search("animation"),
                            v.ed.Field("Animation", v.ed.ObjectValue("MascotBob", "AnimationClip Icon"), FieldMark.Highlighted)).Sized(340, 0),
                        Row(v.ed.Animation("MascotBob", new[] { "Translate.y.value", "Scale.x" }, new[] { (0, 0f), (0, 0.33f), (0, 0.66f), (1, 0.33f), (1, 0.66f) }, false, 20, 0).Sized(360, 110),
                            v.ed.Change("-7", "-14"))),
                    screen: GameScreen.home, link: EditorLink.Animation(k_MascotBob)),
                B("TitleIntro, on the <b>GameObject</b>", "Screen › Title. Select BagelExpressApp and dock the Animation window back: its Animation component plays TitleIntro, a clip with a track per element.",
                    At(Hierarchy, Inspector, Area.Animation), v => Column(
                        v.ed.Inspector(
                            v.ed.GameObjectHeader("BagelExpressApp"),
                            v.ed.Component("Animation", "Animation Icon",
                                v.ed.Field("Animation", v.ed.ObjectValue("TitleIntro", "AnimationClip Icon"), FieldMark.Highlighted))).Sized(380, 0),
                        v.ed.Animation("TitleIntro", new[] { "#logo-bagel : Translate", "#logo-express : Translate", "#logo-hanko : Rotate", "#logo-hanko : Scale" },
                            new[] { (0, 0.08f), (0, 0.3f), (1, 0.15f), (1, 0.38f), (2, 0.5f), (2, 0.78f), (3, 0.5f), (3, 0.66f) }, false, 40, -1).Sized(380, 150)),
                    screen: GameScreen.titleScreen, link: EditorLink.Animation(k_TitleIntro)),
                B("<b>Scrub</b> it in the Game view", "The Game tab, then scrub TitleIntro's timeline: the title follows. It's an Animation Clip on a GameObject, so it can play in step with any other GameObject's.",
                    At(Area.Animation, Game), v => Column(
                        v.ed.Animation("TitleIntro", new[] { "#logo-bagel : Translate", "#logo-hanko : Rotate" },
                            new[] { (0, 0.08f), (0, 0.3f), (1, 0.5f), (1, 0.78f) }, false, 60, -1).Sized(380, 100),
                        v.Crop(GameScreen.titleScreen, "#logo-bagel", "#logo-hanko").Sized(380, 150)),
                    link: EditorLink.Animation(k_TitleIntro))),

            // ================================================================== 6.7: bindings and words
            C("words", "Data and words, bound", "データと言葉", Developer, 210, V("6.7"),
                new Setup(GameScreen.home, play: true, note: "Still playing"),
                "A ScriptableObject drives the UI with data binding, and Localization tables drive its words.",
                "“That's everything that shipped. But I know you have a wishlist…”",
                B("A <b>ScriptableObject</b>, bound", "Select BagelExpressApp › Bakery App Driver: its Start State is DemoStartState, a BakeryState ScriptableObject. It's the data source of the UI's bindings, and it notifies them when a value changes.",
                    At(Hierarchy, Inspector), v => v.ed.Inspector(
                        v.ed.GameObjectHeader("BagelExpressApp"),
                        v.ed.Component("Bakery App Driver", "cs Script Icon",
                            v.ed.Field("Start State", v.ed.ObjectValue("DemoStartState", "ScriptableObject Icon"), FieldMark.Highlighted),
                            v.ed.Field("Show Title", v.ed.TextValue("✓")))).Sized(400, 0),
                    link: EditorLink.GameObject("BagelExpressApp")),
                B("<b>Bagel UI</b>: bindings, live", "Bagel › Bagel UI, full screen: every screen at once, each a PanelElement showing the game's UXML. One shared state: change it in its Inspector, and every bound label, meter and badge follows.",
                    At(BagelUI), v => v.BagelUIWindow(GameScreen.home, GameScreen.shops, GameScreen.luckyBag, GameScreen.recipes),
                    link: EditorLink.Menu("Bagel/Bagel UI", "Bagel UI")),
                B("<b>EN</b> · <b>あ</b> · <b>FR</b>, built in", "Demo toolbar › Language: 日本語, then Français, then English. The strings come from Localization tables, built into 6.7, with the Japanese font as a fallback.",
                    At(Toolbar, BagelUI), v => Column(v.LanguageStrip(), v.Screen(GameScreen.titleScreen, "#tap-to-start").Sized(160, 346))),
                B("TAP TO START's <b>binding</b>", "Back to the UI Viewport, Screen › Title. Select #tap-to-start: Bindings shows a LocalizedString to BagelStrings › title.tap-to-start. Open its binding window, then Edit: Resource Tables.",
                    At(UIViewport, Inspector), v => v.ed.Inspector(
                        v.ed.ElementHeader("Label", "Label", "tap-to-start", "TitleScreen (Visual Tree Asset)"),
                        v.ed.Filters("Bindings"),
                        v.ed.Section("Bindings",
                            v.ed.Field("text", v.ed.ObjectValue("LocalizedString", "TextAsset Icon")),
                            v.ed.Field("Table", v.ed.TextValue("BagelStrings"), FieldMark.Sub),
                            v.ed.Field("Entry", v.ed.TextValue("title.tap-to-start"), FieldMark.Sub | FieldMark.Highlighted))).Sized(360, 0),
                    screen: GameScreen.titleScreen, link: EditorLink.Select("tap-to-start", null, Bindings)),
                B("<b>LET'S BAKE!</b>", "In Resource Tables, search tap-to-start and change the English to LET'S BAKE!",
                    At(ResourceTables, UIViewport), v => Column(
                        v.ed.ResourceTables("tap-to-start", new[] { "Key", "English", "日本語", "Français" },
                            new[] { new[] { "title.tap-to-start", "LET'S BAKE!", "タップしてスタート", "APPUYER POUR JOUER" } }, 0, 1).Sized(380, 0),
                        Then(v.Crop(GameScreen.titleScreen, "#tap-to-start").Sized(150, 70),
                            v.Changed(GameScreen.titleScreen, new[] { "#tap-to-start" }, root =>
                            {
                                if (root.Q<Label>("tap-to-start") is { } label)
                                    label.text = "LET'S BAKE!";
                            }).Sized(150, 70))),
                    link: EditorLink.ResourceTables("tap-to-start")),
                B("<b>Plurals</b>: 2 bakeries, 1 bakery", "Search open-count: a Smart String, {count:plural…}. In Bagel UI's Shops, the 2 bakeries open pill in the three languages. Set the state to 1 bakery, then back to 2.",
                    At(ResourceTables, BagelUI), v => Column(
                        v.ed.ResourceTables("open-count", new[] { "Key", "English", "Français" },
                            new[] { new[] { "shops.open-count", "{count:plural(en):1 bakery|{} bakeries} open", "{count:plural(fr):{} boulangerie ouverte|{} boulangeries ouvertes}" } }, 0, 1).Sized(480, 0),
                        v.Crop(GameScreen.shops, ".be-shops__summary").Sized(300, 90)),
                    link: EditorLink.ResourceTables("open-count"))),

            // ================================================================== 7.x
            C("next", "Today, behind a flag", "実験的機能", Designer, 210, V("7.x"),
                new Setup(GameScreen.home, play: true, note: "Still playing · the experimental flags on"),
                "Experimental in 6.7, one flag away: components, Grid, and style sheets edited in place.",
                "“Can we ship those? And where do I find all of this?”",
                B("<b>PressSquish</b>: remove, add, tune", "Select #collect-button: its Components list PressSquish. Remove it, then Add Component › PressSquish again, and set its pressed scale 0.92 → 0.7. Press COLLECT in the game: it squishes harder.",
                    At(Inspector, Game), v => Row(
                        v.ed.Inspector(
                            v.ed.ElementHeader("Button", "Button", "collect-button", "HomePage (Visual Tree Asset)"),
                            v.ed.ComponentsSection(v.ed.ComponentRow("PressSquish",
                                v.ed.Field("Pressed Scale", v.ed.NumberValue("0.92 → 0.7"), FieldMark.Sub | FieldMark.Highlighted))),
                            v.ed.AddComponent(new[] { "NotificationBadge", "PressSquish", "WavyText" }, "PressSquish")).Sized(260, 0),
                        v.Crop(GameScreen.home, "#collect-button").Sized(130, 130)),
                    link: EditorLink.Select("collect-button")),
                B("<b>WavyText</b>, on the ribbon too", "Screen › Title. #logo-bagel waves with a WavyText component. Select #logo-ribbon (MONTRÉAL × TOKYO), Add Component › WavyText, and play with its amplitude and speed.",
                    At(Toolbar, Inspector), v => Row(
                        v.ed.Inspector(
                            v.ed.ElementHeader("Label", "Label", "logo-ribbon", "TitleScreen (Visual Tree Asset)"),
                            v.ed.ComponentsSection(v.ed.ComponentRow("WavyText",
                                v.ed.Field("Amplitude", v.ed.NumberValue("5 → 8"), FieldMark.Sub | FieldMark.Highlighted),
                                v.ed.Field("Speed", v.ed.NumberValue("4"), FieldMark.Sub),
                                v.ed.Field("Wavelength", v.ed.NumberValue("110"), FieldMark.Sub)))).Sized(260, 0),
                        v.Crop(GameScreen.titleScreen, "#logo-ribbon", "#logo-bagel").Sized(150, 130)),
                    screen: GameScreen.titleScreen, link: EditorLink.Select("logo-ribbon")),
                B("<b>Grid</b>: 3 → 4 columns", "Screen › Recipes. Search the Hierarchy for recipe-grid, then the Inspector for grid: the Recipe Book is a CSS Grid. Set its columns inline to 4.",
                    At(Toolbar, Hierarchy, Inspector), v => Row(
                        v.Crop(GameScreen.recipes, "#recipe-grid").Sized(220, 300),
                        Column(
                            v.ed.Inspector(
                                v.ed.Search("grid"),
                                v.ed.Field("Template Columns", v.ed.TextValue("repeat(4, 1fr)"), FieldMark.Highlighted)).Sized(220, 0),
                            v.ed.Change("3 columns", "4"))),
                    screen: GameScreen.recipes, link: EditorLink.Select("recipe-grid", "grid")),
                B("The <b>Sampler</b>, previewed", "Project › UI › Runtime › B00_Themes › T0_Common. Select BagelArtSampler.uxml: the Inspector previews it, every art piece and style in one document.",
                    At(Project, Inspector), v => v.ed.Files(
                        ("UxmlScript Icon", "BagelArtSampler.uxml"), ("UssScript Icon", "BagelArtSampler.uss"),
                        ("UssScript Icon", "BagelExpressCommonStyles.uss"), ("Folder Icon", "Fonts")).Sized(330, 0),
                    link: EditorLink.Asset(k_Sampler)),
                B("Edit the <b>common styles</b>", "Double-click BagelExpressCommonStyles.uss. Select .be-btn--gold and set its border radius to 4px. Save: COLLECT changes in the UI Viewport and the game, and so does the Lucky Bag's DRAW ×5.",
                    At(Project, UIBuilder, Game), v => Column(
                        v.ed.UIBuilder("BagelExpressCommonStyles.uss", new[] { ".be-btn", ".be-btn--gold", ".be-btn--mint", ".be-btn--rose" }, ".be-btn--gold",
                            v.ed.Field("Radius", v.ed.NumberValue("4", "px"), FieldMark.Highlighted)).Sized(380, 0),
                        Row(v.Changed(GameScreen.home, new[] { "#collect-button" }, uss: k_GoldRadius).Sized(120, 80),
                            v.Changed(GameScreen.luckyBag, new[] { "#draw-five-button" }, uss: k_GoldRadius).Sized(120, 80))),
                    link: EditorLink.UIBuilderStyleSheet(k_CommonStyles))),

            // ================================================================== the toolbox
            C("features", "Every feature, in Bagel Features", "機能一覧", Developer, 120, V("6.6", "6.7", "7.x"),
                new Setup(null, play: true, note: "The Bagel Features tab · still playing"),
                "It's all in the project, on GitHub: every feature, on the live game, with its code.",
                "“Every one of these windows is in the project. Want to wrap up?”",
                B("Every feature, <b>on GitHub</b>", "Bagel Features (the tab next to the UI Viewport). The project is on GitHub: this is how to find your way in it. Its tabs, Category, Version and Art, and its filters, In Bagel and Not in Bagel.",
                    At(BagelFeatures), v => v.FeaturesWindow(("6.6", new[] { "drop-shadow", "backdrop-filter" }), ("6.7", new[] { "box-shadow", "background-gradients" }), ("7.x", new[] { "grid-layout", "ui-components" })),
                    link: EditorLink.Menu("Bagel/Bagel Features", "Bagel Features")),
                B("<b>Blur</b>: try it, pick it", "The glass blur card: drag its TRY IT slider and the preview blurs. Pick in scene selects it in the Hierarchy and the UI Viewport.",
                    At(BagelFeatures, Hierarchy), v => v.Tile("backdrop-filter")),
                B("<b>UI Components</b>, in code", "Version › 7.x › UI Components: its code sample, a partial struct with [VisualElementComponent], and hooks for added, changed and removed.",
                    At(BagelFeatures), v => v.Snippet("ui-components")),
                B("<b>Art</b>: the awning, procedural", "The Art tab › Awning: the storefront's striped awning is Painter2D, drawn in C#, with its stripes and colors to try.",
                    At(BagelFeatures), v => v.Art(new Awning(), new Lantern(), new PastryIcon(PastryKind.SesameBagel), new BeguChan()))),

            C("wrap", "Wrap-up", "まとめ", Designer, 60, V(),
                new Setup(null, play: true, note: "Stop Play first"),
                "The UI is authored where the game is, and Play mode keeps every edit.",
                null,
                B("Stop: <b>every edit stays</b>", "Stop Play. The rose COLLECT, the bigger pastries, the new PastryIcon, the sky-blue bubble, LET'S BAKE!, the 4-column Recipe Book: all still there.",
                    At(Game, UIViewport), v => Row(
                        v.ed.PlayButtons(false),
                        v.Changed(GameScreen.home, new[] { "#mascot-row", "#register-card" }, root =>
                        {
                            var button = root.Q("collect-button");
                            button?.RemoveFromClassList("be-btn--gold");
                            button?.AddToClassList("be-btn--rose");
                        }, uss: k_BubbleSky).Sized(200, 200)),
                    link: EditorLink.Play()),
                B("Everything is a <b>file</b>", "Every change went into an asset, edited from the Hierarchy, the Inspector, the UI Viewport and the Animation window, while the game played.",
                    At(Project), v => v.ed.Files(
                        ("UxmlScript Icon", "HomePage.uxml"), ("UxmlScript Icon", "StorePastry.uxml"), ("UxmlScript Icon", "LevelUpDialog.uxml"),
                        ("UxmlScript Icon", "TitleScreen.uxml"), ("UxmlScript Icon", "RecipesPage.uxml"), ("AnimationClip Icon", "MascotBob.asset"),
                        ("TextAsset Icon", "BagelStrings_en.asset"), ("UssScript Icon", "BagelExpressCommonStyles.uss")).Sized(330, 0)),
                B("Back to the <b>slides</b>", "Thank you! ありがとうございました! Back to the slides.",
                    At(Game), v => v.Phone(GameScreen.titleScreen))),
        };
    }
}
