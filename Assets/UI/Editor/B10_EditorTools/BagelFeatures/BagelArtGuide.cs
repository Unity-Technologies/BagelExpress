using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The Art mode of <see cref="BagelFeaturesWindow"/>: the game's art drawn in code (the Painter2D elements) and its SVG
    /// icons, one element at a time. For each element: what it can change (its UXML attributes, its size, the USS custom
    /// properties it reads), a few variations for its row, and the variations the game uses. The Art Sampler window
    /// shows the same art all on one page.
    /// </summary>
    public static class BagelArtGuide
    {
        public enum ValueType { Bool, Int, Float, Text, Choice, Color }

        /// <summary>
        /// What a control sets: a UXML attribute (through its C# property), an inline style, or a USS custom property. A
        /// custom property can't be set inline, so those go in a style sheet made for the element (<see cref="CreateStyleSheet"/>).
        /// </summary>
        public enum Target { Attribute, Style, CustomProperty }

        public sealed class Control
        {
            /// <summary>The attribute's UXML name, a name for the style, or the custom property (<c>--be-ink</c>).</summary>
            public string name;
            public string label;
            public string tooltip;
            public Target target;
            public ValueType type;
            /// <summary>The default; null for a custom property, which then takes the theme's value (or <see cref="fallback"/>).</summary>
            public object value;
            /// <summary>A custom property's value when neither the theme nor a rule sets it: the element's own default.</summary>
            public object fallback;
            public float min, max;
            public string[] choices;
            internal Action<VisualElement, object> set;
            internal Func<object, string> uss;

            /// <summary>The attribute as UXML (<c>kind="Taiyaki"</c>).</summary>
            public string Uxml(object v) => $"{name}=\"{Escape(Text(type, v))}\"";

            /// <summary>The style or custom property as USS declarations (<c>--be-ink: rgb(59, 31, 20);</c>).</summary>
            public string Uss(object v) => uss != null ? uss(v) : $"{name}: {Text(type, v)};";

            public void Set(VisualElement art, object v) => set?.Invoke(art, v);
        }

        /// <summary>A look the game uses: some controls' values, and a season (its theme) or the window's.</summary>
        public sealed class Variation
        {
            /// <summary>Null for a season: the season's name.</summary>
            public string caption;
            public string tooltip;
            public int season = -1;
            public (string name, object value)[] values = Array.Empty<(string, object)>();
        }

        /// <summary>
        /// Where the game uses it: a screen, the part of the screen that shows it (its selector in
        /// <see cref="BagelFeatureGuide.screens"/>, null if none does), and the element's name and UXML when it's in one.
        /// </summary>
        public sealed class Use
        {
            public GameScreen screen;
            public string title;
            public string part;
            public string element;
            public string uxml;
        }

        public sealed class ArtType
        {
            public string id;
            public string title;
            public string drawnWith;
            public string about;
            public string script;
            public string uxmlTag;
            public Func<VisualElement> create;
            /// <summary>The element's size in the preview; for one that <see cref="fills"/> its box, the box's.</summary>
            public Vector2 previewSize;
            public Vector2 rowSize;
            public Vector2 gallerySize;
            /// <summary>Fills a box (particles, a skyline, a dimmed screen) rather than having a size of its own.</summary>
            public bool fills;
            /// <summary>What's around it in its box: the screen a Spotlight dims.</summary>
            public Action<VisualElement, VisualElement, Vector2> scene;
            /// <summary>Run whenever it's shown (and when a variation is loaded): the confetti starts its loop.</summary>
            public Action<VisualElement> onShown;
            /// <summary>A button under the preview (label, action).</summary>
            public (string label, Action<VisualElement> run) action;
            public Control[] controls;
            public Variation[] row;
            public Variation[] variations;
            /// <summary>Where the game uses it; the first one with an element is the one the Scene and UI Builder buttons open.</summary>
            public Use[] uses = Array.Empty<Use>();
            /// <summary>USS for what several controls set together (a filter and its parameters), after the controls' own.</summary>
            public Func<IReadOnlyDictionary<string, object>, IEnumerable<string>> markupUss;

            public Use representative => uses.FirstOrDefault(u => u.element != null);

            public Control Get(string controlName) => controls.FirstOrDefault(c => c.name == controlName);
        }

        public const string iconsFolder = "Assets/UI/Runtime/B02_Common/Icons/";
        const string k_Art = "Assets/UI/Runtime/B02_Common/Art/";

        public static readonly string[] iconNames =
            { "home", "shops", "lucky-bag", "recipes", "settings", "back", "close", "plus", "lock", "star", "sparkle", "clock", "montreal", "japan" };

        static readonly Color k_Ink = ArtKit.Ink;

        static List<ArtType> s_Types;

        /// <summary>Every art element, in the list's order.</summary>
        public static IReadOnlyList<ArtType> types => s_Types ??= Build();

        public static ArtType Get(string id) => types.FirstOrDefault(t => t.id == id);

        // ------------------------------------------------------------------ the art

        static List<ArtType> Build() => new()
        {
            new ArtType
            {
                id = "pastry-icon", title = "PastryIcon", drawnWith = "Painter2D", script = k_Art + "PastryIcon.cs", uxmlTag = "Bagel.PastryIcon",
                about = "Twelve pastries with faces, drawn in code; a locked shop or recipe binds its silhouette.",
                create = () => new PastryIcon(),
                previewSize = new(150, 150), rowSize = new(54, 54), gallerySize = new(62, 62),
                controls = new[]
                {
                    Enum<PastryIcon, PastryKind>("kind", "Kind", PastryKind.SesameBagel, (e, v) => e.kind = v),
                    Bool<PastryIcon>("silhouette", "Silhouette", false, (e, v) => e.silhouette = v),
                    Bool<PastryIcon>("show-face", "Face", true, (e, v) => e.showFace = v),
                    Size(150, 24, 200),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                    CustomColor("--be-silhouette-color", "Silhouette color", PastryIcon.defaultSilhouetteColor),
                },
                row = new[] { PastryKind.SesameBagel, PastryKind.MatchaBagel, PastryKind.MelonPan, PastryKind.Taiyaki, PastryKind.Dorayaki }
                    .Select(k => V(k.ToString(), null, ("kind", k.ToString()))).ToArray(),
                variations = System.Enum.GetValues(typeof(PastryKind)).Cast<PastryKind>()
                    .Select(k => V(Spaced(k.ToString()), "The menu, the shops, the recipes and the reveal", ("kind", k.ToString())))
                    .Append(V("Locked", "A locked shop or recipe: silhouette is bound to the data", ("kind", "Taiyaki"), ("silhouette", true)))
                    .Append(V("No face", "show-face=\"false\"", ("kind", "Anpan"), ("show-face", false)))
                    .ToArray(),
                uses = new[] { U(GameScreen.home, "Shelf pastry", "#store-pastry-1", "store-pastry-1", "Assets/UI/Runtime/B05_Home/HomePage.uxml"), U(GameScreen.recipes, "Locked pastry", "#recipe-grid .be-pastry-icon--silhouette"), U(GameScreen.reveal, "Reveal cards", ".be-reveal") },
            },
            new ArtType
            {
                id = "begu-chan", title = "BeguChan", drawnWith = "Painter2D", script = k_Art + "BeguChan.cs", uxmlTag = "Bagel.BeguChan",
                about = "The mascot, a bagel in three moods; a keyword custom property picks the season's accessory.",
                create = () => new BeguChan(),
                previewSize = new(170, 170), rowSize = new(62, 62), gallerySize = new(70, 70),
                controls = new[]
                {
                    Enum<BeguChan, MascotMood>("mood", "Mood", MascotMood.Happy, (e, v) => e.mood = v),
                    Bool<BeguChan>("blink", "Blink", true, (e, v) => e.blink = v),
                    Size(170, 40, 220),
                    CustomKeyword("--be-mascot-accessory", "Accessory", new[] { "maple", "sakura", "matsuri", "snow" }, "maple"),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                },
                row = new[] { V("Happy", null, ("mood", "Happy")), V("Excited", null, ("mood", "Excited")), V("Sleepy", null, ("mood", "Sleepy")) },
                variations = new[]
                    {
                        V("Happy", "Player stats, the tutorial", ("mood", "Happy")),
                        V("Excited", "Player stats after a level up", ("mood", "Excited")),
                        V("Sleepy", "Player stats", ("mood", "Sleepy")),
                    }
                    .Concat(Seasons("The season's accessory (--be-mascot-accessory)")).ToArray(),
                uses = new[] { U(GameScreen.home, "Blinking Begu-chan", "#mascot", "mascot", "Assets/UI/Runtime/B05_Home/HomePage.uxml"), U(GameScreen.titleScreen, "Begu-chan", "#title-mascot", "title-mascot", "Assets/UI/Runtime/B04_Title/TitleScreen.uxml"), U(GameScreen.playerStats, "Begu-chan's mood", "#stats-mascot", "stats-mascot", "Assets/UI/Runtime/B10_Overlays/PlayerStatsDialog.uxml") },
            },
            new ArtType
            {
                id = "currency-icon", title = "CurrencyIcon", drawnWith = "Painter2D", script = "Assets/UI/Runtime/B02_Common/CurrencyIcon.cs",
                uxmlTag = "Bagel.CurrencyIcon",
                about = "Coins, gems and tickets in one element, used by the HUD, the prices and the dialogs.",
                create = () => new CurrencyIcon(),
                previewSize = new(110, 110), rowSize = new(48, 48), gallerySize = new(56, 56),
                controls = new[]
                {
                    Enum<CurrencyIcon, CurrencyKind>("kind", "Kind", CurrencyKind.Coins, (e, v) => e.kind = v),
                    Size(110, 16, 200),
                    CustomColor("--be-ink", "Ink", new Color(0.23f, 0.12f, 0.08f)), InkWidth(),
                },
                row = new[] { V("Coins", null, ("kind", "Coins")), V("Gems", null, ("kind", "Gems")), V("Tickets", null, ("kind", "Tickets")) },
                variations = new[]
                {
                    V("Coins", "The HUD, the shops' prices, the register", ("kind", "Coins")),
                    V("Gems", "The HUD, the Level Up reward", ("kind", "Gems")),
                    V("Tickets", "The Lucky Bag's draws, the confirm dialog", ("kind", "Tickets")),
                },
                uses = new[] { U(GameScreen.home, "COLLECT button", "#collect-button", "collect-icon", "Assets/UI/Runtime/B05_Home/HomePage.uxml"), U(GameScreen.home, "Coins pill", "#coins-pill"), U(GameScreen.levelUp, "Gems reward", null, "level-up-reward-icon", "Assets/UI/Runtime/B10_Overlays/LevelUpDialog.uxml") },
            },
            new ArtType
            {
                id = "lucky-bag", title = "LuckyBag", drawnWith = "Painter2D", script = k_Art + "LuckyBag.cs", uxmlTag = "Bagel.LuckyBag",
                about = "The fukubukuro of the nav and the Lucky Bag page; its glow is drawn by the art, not a filter.",
                create = () => new LuckyBag(),
                previewSize = new(170, 170), rowSize = new(58, 58), gallerySize = new(70, 70),
                controls = new[]
                {
                    Text<LuckyBag>("emblem-text", "Emblem", "福", (e, v) => e.emblemText = v),
                    Size(170, 40, 220),
                    CustomColor("--be-bag-color", "Bag", ArtKit.Rgb(222, 52, 44)),
                    CustomColor("--be-bag-accent", "Accent", ArtKit.Gold),
                    CustomColor("--be-bag-glow", "Glow", Color.clear),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                },
                row = Seasons(null),
                variations = Seasons("The nav and the Lucky Bag page, in the season's color")
                    .Append(V("Nav, current", "The nav's bag on the Lucky Bag page: --be-bag-glow", ("--be-bag-glow", ArtKit.Rgb(214, 132, 0))))
                    .Append(V("Star emblem", "emblem-text=\"\": a lucky star", ("emblem-text", "")))
                    .ToArray(),
                uses = new[] { U(GameScreen.luckyBag, "Lucky Bag art", "#bag", "bag", "Assets/UI/Runtime/B07_LuckyBag/LuckyBagPage.uxml"), U(GameScreen.luckyBag, "Nav Lucky Bag", ".be-nav__hero-bag", "nav-lucky-bag-art", "Assets/UI/Runtime/B03_App/BakeryAppScreen.uxml") },
            },
            new ArtType
            {
                id = "lantern", title = "Lantern", drawnWith = "Painter2D", script = k_Art + "Lantern.cs", uxmlTag = "Bagel.Lantern",
                about = "The storefront's paper lanterns; the mark is a Label, or a drawn maple leaf without one.",
                create = () => new Lantern(),
                previewSize = new(100, 145), rowSize = new(40, 58), gallerySize = new(46, 66),
                controls = new[]
                {
                    Text<Lantern>("mark", "Mark", "", (e, v) => e.mark = v),
                    Width(100, 30, 200), Height(145, 40, 210),
                    CustomColor("--be-lantern-color", "Paper", ArtKit.Rgb(226, 54, 40)),
                    CustomColor("--be-lantern-glow", "Glow", ArtKit.Rgb(255, 140, 60, 0.8f)),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                },
                row = Seasons(null),
                variations = Seasons("The storefront, on Home")
                    .Append(V("祭", "mark=\"祭\"", ("mark", "祭")))
                    .Append(V("パン", "mark=\"パン\"", ("mark", "パン")))
                    .ToArray(),
                uses = new[] { U(GameScreen.home, "Swaying lantern", "#lantern-left", "lantern-left", "Assets/UI/Runtime/B05_Home/HomePage.uxml") },
            },
            new ArtType
            {
                id = "awning", title = "Awning", drawnWith = "Painter2D", script = k_Art + "Awning.cs", uxmlTag = "Bagel.Awning",
                about = "The storefront's striped, scalloped awning; with no stripe count, its proportions pick one.",
                create = () => new Awning(),
                previewSize = new(320, 80), rowSize = new(160, 44), gallerySize = new(150, 40),
                controls = new[]
                {
                    Int<Awning>("stripe-count", "Stripes", 0, 0, 24, (e, v) => e.stripeCount = v),
                    Width(320, 80, 340), Height(80, 24, 160),
                    CustomColor("--be-awning-a", "Stripe A", ArtKit.Rgb(222, 72, 44)),
                    CustomColor("--be-awning-b", "Stripe B", ArtKit.Rgb(255, 248, 234)),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                },
                row = new[] { Season(0), Season(1) },
                variations = Seasons("The storefront, on Home"),
                uses = new[] { U(GameScreen.home, "Striped awning", "#store-awning", "store-awning", "Assets/UI/Runtime/B05_Home/HomePage.uxml") },
            },
            new ArtType
            {
                id = "noren", title = "NorenCurtain", drawnWith = "Painter2D", script = k_Art + "NorenCurtain.cs", uxmlTag = "Bagel.NorenCurtain",
                about = "The shop's split curtain: three swaying cloth panels, with the text on the middle one.",
                create = () => new NorenCurtain(),
                previewSize = new(240, 170), rowSize = new(96, 62), gallerySize = new(110, 78),
                controls = new[]
                {
                    Text<NorenCurtain>("text", "Text", "パン", (e, v) => e.text = v),
                    Bool<NorenCurtain>("vertical-text", "Vertical text", true, (e, v) => e.verticalText = v),
                    Width(240, 80, 340), Height(170, 60, 210),
                    CustomColor("--be-noren-color", "Cloth", ArtKit.Rgb(44, 54, 120)),
                    CustomColor("--be-noren-text", "Text color", ArtKit.Rgb(255, 250, 240)),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                },
                row = new[] { Season(0), Season(1), Season(2) },
                variations = Seasons("The storefront, on Home")
                    .Append(V("ベーグル", "text=\"ベーグル\"", ("text", "ベーグル")))
                    .ToArray(),
                uses = new[] { U(GameScreen.home, "Noren curtain", "#store-noren", "store-noren", "Assets/UI/Runtime/B05_Home/HomePage.uxml") },
            },
            new ArtType
            {
                id = "starburst", title = "Starburst", drawnWith = "Painter2D", script = k_Art + "Starburst.cs", uxmlTag = "Bagel.Starburst",
                about = "Long and short spikes around a soft center, behind every celebration, spun by a USS animation.",
                create = () => new Starburst(),
                previewSize = new(210, 210), rowSize = new(62, 62), gallerySize = new(72, 72),
                controls = new[]
                {
                    Int<Starburst>("ray-count", "Rays", 10, 3, 32, (e, v) => e.rayCount = v),
                    Float<Starburst>("inner-radius", "Inner radius", 0.18f, 0f, 1f, (e, v) => e.innerRadius = v),
                    Float<Starburst>("short-length", "Short spikes", 0.55f, 0.1f, 1f, (e, v) => e.shortLength = v),
                    Float<Starburst>("center-size", "Center", 0.22f, 0f, 1f, (e, v) => e.centerSize = v),
                    Float<Starburst>("edge-width", "Edge width", 2f, 0f, 12f, (e, v) => e.edgeWidth = v),
                    Bool<Starburst>("fade-out", "Fade out", true, (e, v) => e.fadeOut = v),
                    Size(210, 40, 220),
                    CustomColor("--be-burst-color-a", "Color A", ArtKit.Rgb(255, 220, 120, 0.9f)),
                    CustomColor("--be-burst-color-b", "Color B", ArtKit.Rgb(255, 160, 80, 0.5f)),
                    CustomColor("--be-burst-center", "Center color", ArtKit.Rgb(255, 250, 220, 0.95f)),
                    CustomColor("--be-burst-edge", "Edge color", Color.clear),
                },
                row = new[] { s_TitleBurst, s_LevelUpBurst, s_EventBurst },
                variations = Seasons("The title, the Lucky Bag's banner and results, the recipe sheet: the season's colors")
                    .Append(s_LevelUpBurst).Append(s_EventBurst).ToArray(),
                uses = new[] { U(GameScreen.titleScreen, "Title starburst", "#title-burst", "title-burst", "Assets/UI/Runtime/B04_Title/TitleScreen.uxml"), U(GameScreen.luckyBag, "Banner starburst", "#banner-burst", "banner-burst", "Assets/UI/Runtime/B07_LuckyBag/LuckyBagPage.uxml"), U(GameScreen.levelUp, "LEVEL UP! burst", "#level-up-burst", "level-up-burst", "Assets/UI/Runtime/B10_Overlays/LevelUpDialog.uxml") },
            },
            new ArtType
            {
                id = "confetti", title = "ConfettiBurst", drawnWith = "Painter2D", script = k_Art + "ConfettiBurst.cs", uxmlTag = "Bagel.ConfettiBurst",
                about = "LEVEL UP!'s one-shot confetti: it runs from Burst() until the last piece lands.",
                create = () => new ConfettiBurst(),
                fills = true, previewSize = new(330, 210), rowSize = new(200, 62), gallerySize = new(150, 90),
                // On a loop while shown (a scheduled item pauses while its element is out of the panel).
                onShown = art =>
                {
                    if (art is ConfettiBurst confetti && confetti.userData == null)
                        confetti.userData = confetti.schedule.Execute(() => confetti.Burst()).StartingIn(120).Every(2400);
                },
                action = ("✦ Burst()", art => (art as ConfettiBurst)?.Burst()),
                controls = new[] { CustomNumber("--be-particle-size", "Particle size", 1f, 0.25f, 3f), CustomColor("--be-ink", "Ink", k_Ink), InkWidth() },
                row = new[] { V("Level Up", null) },
                variations = new[] { V("Level Up", "LEVEL UP!'s confetti; click to throw it again") },
                uses = new[] { U(GameScreen.levelUp, "Confetti", null, "level-up-confetti", "Assets/UI/Runtime/B10_Overlays/LevelUpDialog.uxml") },
            },
            new ArtType
            {
                id = "spotlight", title = "Spotlight", drawnWith = "Painter2D", script = k_Art + "Spotlight.cs", uxmlTag = "Bagel.Spotlight",
                about = "The tutorial's dimmed screen with a hole around what to tap; Focus() glides it to a new target.",
                create = () => new Spotlight(),
                fills = true, previewSize = new(330, 210), rowSize = new(120, 62), gallerySize = new(150, 90),
                scene = SpotlightScene,
                controls = new[]
                {
                    Enum<Spotlight, SpotlightShape>("shape", "Shape", SpotlightShape.RoundedRect, (e, v) => e.shape = v),
                    Float<Spotlight>("corner-radius", "Corner radius", 18f, 0f, 60f, (e, v) => e.cornerRadius = v),
                    new Control
                    {
                        name = "preview-hole", label = "Hole", tooltip = "What the hole is around (preview-hole: x, y, width, height)",
                        target = Target.Attribute, type = ValueType.Choice, value = "Button", choices = new[] { "Button", "Pastry", "None" },
                        set = (e, v) => { if (e is Spotlight s) s.previewHole = HoleOf(s, (string)v); },
                    },
                    CustomColor("--be-spotlight-color", "Dim", new Color(20f / 255f, 10f / 255f, 5f / 255f, 0.72f)),
                    CustomColor("--be-spotlight-ring", "Ring", ArtKit.Rgb(255, 214, 110, 0.95f)),
                },
                row = new[] { V("Button", null), V("Circle", null, ("shape", "Circle"), ("preview-hole", "Pastry")) },
                variations = new[]
                {
                    V("A button", "The tutorial: a rounded hole around the button to tap"),
                    V("Circle", "shape=\"Circle\"", ("shape", "Circle"), ("preview-hole", "Pastry")),
                },
                uses = new[] { U(GameScreen.tutorial, "Tip target", "#collect-button") },
            },
            new ArtType
            {
                id = "particles", title = "SeasonParticles", drawnWith = "Painter2D + Shader Graph", script = k_Art + "SeasonParticles.cs", uxmlTag = "Bagel.SeasonParticles",
                about = "Falling leaves, petals, fireflies or snow, moved on the GPU; the season sets kind and count.",
                create = () => new SeasonParticles(),
                fills = true, previewSize = new(330, 210), rowSize = new(80, 62), gallerySize = new(150, 90),
                controls = new[]
                {
                    Int<SeasonParticles>("seed", "Seed", 1, 0, 99, (e, v) => e.seed = v),
                    CustomKeyword("--be-particle-kind", "Kind", new[] { "maple", "sakura", "firefly", "snow" }, "maple"),
                    CustomNumber("--be-particle-count", "Count", 18f, 0f, SeasonParticles.maxCount),
                    CustomNumber("--be-particle-size", "Particle size", 1f, 0.25f, 3f),
                    CustomColor("--be-particle-color", "Tint", Color.clear),
                    CustomColor("--be-ink", "Ink", k_Ink), InkWidth(),
                },
                row = Seasons(null),
                variations = Seasons("The title and Home: the season's particles"),
                uses = new[] { U(GameScreen.titleScreen, "Title particles", null, "title-particles", "Assets/UI/Runtime/B04_Title/TitleScreen.uxml"), U(GameScreen.home, "Falling particles", null, "particles", "Assets/UI/Runtime/B03_App/BakeryAppScreen.uxml") },
            },
            new ArtType
            {
                id = "skyline", title = "SkylineBackdrop", drawnWith = "Painter2D", script = k_Art + "SkylineBackdrop.cs", uxmlTag = "Bagel.SkylineBackdrop",
                about = "Montréal and Tokyo rooftops in two layers with lit windows, in four colors from the season.",
                create = () => new SkylineBackdrop(),
                fills = true, previewSize = new(330, 210), rowSize = new(80, 62), gallerySize = new(150, 90),
                controls = new[]
                {
                    CustomColor("--be-skyline-far", "Far", ArtKit.Rgb(240, 160, 120)),
                    CustomColor("--be-skyline-near", "Near", ArtKit.Rgb(150, 60, 50)),
                    CustomColor("--be-window-light", "Windows", ArtKit.Rgb(255, 226, 140)),
                    CustomColor("--be-skyline-accent", "Accent", ArtKit.Rgb(240, 96, 56)),
                },
                row = Seasons(null),
                variations = Seasons("The title and the pages' backdrop"),
                uses = new[] { U(GameScreen.home, "City skyline", "#skyline", "skyline", "Assets/UI/Runtime/B03_App/BakeryAppScreen.uxml"), U(GameScreen.titleScreen, "Title skyline", null, "title-skyline", "Assets/UI/Runtime/B04_Title/TitleScreen.uxml") },
            },
            new ArtType
            {
                id = "svg-icons", title = "SVG icons", drawnWith = "VectorImage", uxmlTag = "ui:VisualElement",
                script = "Assets/UI/Runtime/B02_Common/Icons",
                about = "SVG icons as VectorImage background-image, tinted with -unity-background-image-tint-color.",
                create = () => new VisualElement { style = { unityBackgroundScaleMode = ScaleMode.ScaleToFit } },
                previewSize = new(96, 96), rowSize = new(34, 34), gallerySize = new(38, 38),
                controls = new[]
                {
                    new Control
                    {
                        name = "background-image", label = "Icon", tooltip = "An SVG file, imported as a VectorImage",
                        target = Target.Style, type = ValueType.Choice, value = "home", choices = iconNames,
                        set = (e, v) => e.style.backgroundImage = new StyleBackground(Icon((string)v)),
                        uss = v => $"background-image: url(\"/{iconsFolder}icon-{v}.svg\");",
                    },
                    Size(96, 16, 200),
                    new Control
                    {
                        name = "-unity-background-image-tint-color", label = "Tint", tooltip = "Multiplies the icon's colors",
                        target = Target.Style, type = ValueType.Color, value = Color.white,
                        set = (e, v) => e.style.unityBackgroundImageTintColor = (Color)v,
                    },
                },
                row = new[] { "home", "shops", "lucky-bag", "recipes", "settings", "star", "lock" }.Select(n => V(n, null, ("background-image", n))).ToArray(),
                variations = iconNames.Select(n => V(n, $"icon-{n}.svg", ("background-image", n))).ToArray(),
                uses = new[] { U(GameScreen.home, "Home icon", "#nav-home-icon", "nav-home-icon", "Assets/UI/Runtime/B03_App/BakeryAppScreen.uxml"), U(GameScreen.shops, "City flag", ".be-shop-card__flag") },
            },
            new ArtType
            {
                id = "manga-halftone", title = "MangaHalftone", drawnWith = "Custom filter", script = k_HalftonePath, uxmlTag = "ui:VisualElement",
                about = "A custom filter: a FilterFunctionDefinition and its shader turn what's under it into manga halftone dots.",
                create = HalftoneScene,
                previewSize = new(300, 180), rowSize = new(110, 62), gallerySize = new(120, 72),
                onShown = ApplyHalftone,
                controls = new[]
                {
                    HalftoneControl("as", "Applied as", ValueType.Choice, "filter", choices: new[] { "filter", "backdrop-filter" },
                        tooltip: "On the art itself, or on a pane over it (what's behind the pane)"),
                    HalftoneControl("spacing", "spacing", ValueType.Float, 6f, 2f, 20f, tooltip: "The filter's first parameter: the dot spacing, in pixels"),
                    HalftoneControl("ink", "ink", ValueType.Color, ArtKit.Rgb(120, 30, 20), tooltip: "Its second parameter: the dots' color"),
                    HalftoneControl("then-blur", "then blur()", ValueType.Float, 0f, 0f, 6f, tooltip: "A built-in filter after it in the chain"),
                },
                row = new[] { V("6px", null), V("10px", null, ("spacing", 10f)), V("Ink", null, ("spacing", 4f), ("ink", ArtKit.Ink)) },
                variations = new[]
                {
                    V("LEVEL UP!", "LEVEL UP!'s scrim: backdrop-filter, 10px dots and a 1px blur", ("as", "backdrop-filter"), ("spacing", 10f), ("then-blur", 1f)),
                    V("Thumbnail", "Bagel Features' thumbnail: 4px dots", ("spacing", 4f)),
                    V("Sample", "Bagel Features' sample: 6px dots"),
                },
                markupUss = values => new[] { HalftoneUss(values) },
                uses = new[] { U(GameScreen.levelUp, "Halftone backdrop", "#level-up-scrim", "level-up-scrim", "Assets/UI/Runtime/B10_Overlays/LevelUpDialog.uxml") },
            },
        };

        static readonly Variation s_TitleBurst = V("Title", "The title screen, in the season's colors");
        static readonly Variation s_LevelUpBurst = V("Level Up", "LEVEL UP!: twelve rays and its own gold",
            ("ray-count", 12), ("--be-burst-color-a", ArtKit.Rgb(255, 226, 120, 0.9f)), ("--be-burst-color-b", ArtKit.Rgb(255, 160, 60, 0.45f)),
            ("--be-burst-center", ArtKit.Rgb(255, 252, 230)));
        static readonly Variation s_EventBurst = V("Home event", "The event banner on Home: the prize colors",
            ("--be-burst-color-a", new Color(1f, 1f, 1f, 0.55f)), ("--be-burst-color-b", ArtKit.Rgb(255, 214, 90, 0.35f)));

        // ------------------------------------------------------------------ the Spotlight's screen

        /// <summary>A light screen with a pastry and a Bake! button, under the Spotlight: something to dim and something to point at.</summary>
        static void SpotlightScene(VisualElement box, VisualElement spotlight, Vector2 size)
        {
            box.AddToClassList("bgf-art-scene");
            var pastry = new PastryIcon(PastryKind.MelonPan) { pickingMode = PickingMode.Ignore };
            var p = PastryRect(size);
            pastry.style.position = Position.Absolute;
            pastry.style.left = p.x;
            pastry.style.top = p.y;
            pastry.style.width = p.width;
            pastry.style.height = p.height;
            box.Insert(0, pastry);
            var button = new Label("Bake!") { pickingMode = PickingMode.Ignore };
            button.AddToClassList("bgf-art-scene__button");
            var b = ButtonRect(size);
            button.style.left = b.x;
            button.style.top = b.y;
            button.style.width = b.width;
            button.style.height = b.height;
            button.style.fontSize = Mathf.Max(7f, b.height * 0.42f);
            var radius = b.height * 0.5f;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = radius;
            box.Insert(1, button);
            spotlight.userData = size;
        }

        static Rect PastryRect(Vector2 size)
        {
            var s = size.y * 0.42f;
            return new Rect(size.x * 0.16f, size.y * 0.29f, s, s);
        }

        static Rect ButtonRect(Vector2 size) => new(size.x * 0.56f, size.y * 0.39f, size.x * 0.26f, size.y * 0.22f);

        static Rect HoleOf(VisualElement spotlight, string what)
        {
            if (spotlight.userData is not Vector2 size)
                return default;
            var pad = Mathf.Max(3f, size.y * 0.04f);
            return what switch
            {
                "Button" => Pad(ButtonRect(size), pad),
                "Pastry" => Pad(PastryRect(size), pad),
                _ => default,
            };
        }

        static Rect Pad(Rect r, float pad) => new(Mathf.Round(r.x - pad), Mathf.Round(r.y - pad), Mathf.Round(r.width + 2f * pad), Mathf.Round(r.height + 2f * pad));

        // ------------------------------------------------------------------ the halftone filter's scene

        const string k_HalftonePath = "Assets/UI/Runtime/B11_Filters/MangaHalftone.asset";
        static FilterFunctionDefinition s_Halftone;
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Dictionary<string, object>> s_FilterValues = new();

        /// <summary>Something colorful to filter: a burst, Begu-chan and a title, and a pane for the backdrop-filter.</summary>
        static VisualElement HalftoneScene()
        {
            var scene = new VisualElement();
            scene.AddToClassList("bgf-art-filter-scene");
            var burst = new Starburst { rayCount = 12, pickingMode = PickingMode.Ignore };
            burst.AddToClassList("bgf-art-filter-scene__burst");
            scene.Add(burst);
            var mascot = new BeguChan { mood = MascotMood.Excited, pickingMode = PickingMode.Ignore };
            mascot.AddToClassList("bgf-art-filter-scene__mascot");
            scene.Add(mascot);
            var title = new Label("LEVEL UP!") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("bgf-art-filter-scene__title");
            scene.Add(title);
            var pane = new VisualElement { name = "pane", pickingMode = PickingMode.Ignore };
            pane.AddToClassList("bgf-art-filter-scene__pane");
            scene.Add(pane);
            // The title's size follows the scene's (a row's, the gallery's, the preview's).
            scene.RegisterCallback<GeometryChangedEvent>(_ => title.style.fontSize = Mathf.Max(7f, Mathf.Round(scene.layout.height * 0.15f)));
            return scene;
        }

        static Control HalftoneControl(string name, string label, ValueType type, object value, float min = 0f, float max = 1f,
            string[] choices = null, string tooltip = null) => new()
        {
            name = name, label = label, tooltip = tooltip, target = Target.Style, type = type, value = value, min = min, max = max, choices = choices,
            set = (art, v) =>
            {
                s_FilterValues.GetValue(art, _ => new Dictionary<string, object>())[name] = v;
                ApplyHalftone(art);
            },
            // The filter's USS is one declaration, for all of them (markupUss).
            uss = _ => null,
        };

        static object FilterValue(VisualElement art, string name, object fallback) =>
            s_FilterValues.TryGetValue(art, out var values) && values.TryGetValue(name, out var v) && v != null ? v : fallback;

        /// <summary>The filter chain from the controls: on the scene, or on its pane as a backdrop-filter.</summary>
        static void ApplyHalftone(VisualElement art)
        {
            s_Halftone ??= AssetDatabase.LoadAssetAtPath<FilterFunctionDefinition>(k_HalftonePath);
            if (s_Halftone == null)
                return;
            var halftone = new FilterFunction(s_Halftone);
            halftone.AddParameter(new FilterParameter(Convert.ToSingle(FilterValue(art, "spacing", 6f))));
            halftone.AddParameter(new FilterParameter((Color)FilterValue(art, "ink", ArtKit.Rgb(120, 30, 20))));
            var chain = new List<FilterFunction> { halftone };
            var blur = Convert.ToSingle(FilterValue(art, "then-blur", 0f));
            if (blur > 0f)
            {
                var then = new FilterFunction(FilterFunctionType.Blur);
                then.AddParameter(new FilterParameter(blur));
                chain.Add(then);
            }
            var pane = art.Q("pane");
            var asBackdrop = (string)FilterValue(art, "as", "filter") == "backdrop-filter";
            // An empty list, not none: the inline style wins over the class, and none can't be set inline.
            art.style.filter = asBackdrop ? new List<FilterFunction>() : chain;
            if (pane != null)
            {
                pane.style.display = asBackdrop ? DisplayStyle.Flex : DisplayStyle.None;
                pane.style.backdropFilter = asBackdrop ? chain : new List<FilterFunction>();
            }
        }

        static string HalftoneUss(IReadOnlyDictionary<string, object> values)
        {
            object Get(string name, object fallback) => values.TryGetValue(name, out var v) && v != null ? v : fallback;
            var chain = $"filter(\"/{k_HalftonePath}\" {Number(Convert.ToSingle(Get("spacing", 6f)))}px {ColorText((Color)Get("ink", ArtKit.Rgb(120, 30, 20)))})";
            var blur = Convert.ToSingle(Get("then-blur", 0f));
            if (blur > 0f)
                chain += $" blur({Number(blur)}px)";
            return ((string)Get("as", "filter") == "backdrop-filter" ? "backdrop-filter: " : "filter: ") + chain + ";";
        }

        // ------------------------------------------------------------------ making one

        /// <summary>
        /// The art in a variation, sized: the box to add (the element itself, or the box it fills) and the art element.
        /// Custom properties go in a style sheet made for it, added to <paramref name="sheets"/> (destroy them when done).
        /// </summary>
        public static (VisualElement box, VisualElement art) Create(ArtType type, IReadOnlyDictionary<string, object> values, Vector2 size, bool withSize, List<StyleSheet> sheets)
        {
            var art = type.create();
            art.name = "art";
            art.pickingMode = PickingMode.Ignore;
            VisualElement box = art;
            if (type.fills)
            {
                box = new VisualElement { pickingMode = PickingMode.Ignore };
                box.AddToClassList("bgf-art-box");
                box.style.width = size.x;
                box.style.height = size.y;
                art.style.position = Position.Absolute;
                art.style.left = art.style.top = art.style.right = art.style.bottom = 0f;
                box.Add(art);
                type.scene?.Invoke(box, art, size);
            }
            else
            {
                art.style.width = size.x;
                art.style.height = size.y;
            }
            if (type.onShown != null)
                art.RegisterCallback<AttachToPanelEvent>(_ => type.onShown(art));
            Apply(type, art, values, withSize, sheets);
            return (box, art);
        }

        /// <summary>The defaults, with a variation's values over them.</summary>
        public static Dictionary<string, object> ValuesOf(ArtType type, Variation variation)
        {
            var values = type.controls.ToDictionary(c => c.name, c => c.value);
            if (variation != null)
                foreach (var (name, value) in variation.values)
                    if (values.ContainsKey(name))
                        values[name] = value;
            return values;
        }

        /// <summary>Set the attributes and styles, and the custom properties that have a value (<see cref="ApplyCustom"/>).</summary>
        public static void Apply(ArtType type, VisualElement art, IReadOnlyDictionary<string, object> values, bool withSize, List<StyleSheet> sheets,
            Action<StyleSheet> retire = null)
        {
            foreach (var control in type.controls)
            {
                if (control.target == Target.CustomProperty || !values.TryGetValue(control.name, out var value) || value == null)
                    continue;
                if (!withSize && IsSize(control))
                    continue;
                control.Set(art, value);
            }
            ApplyCustom(type, art, values, sheets, retire);
        }

        /// <summary>
        /// The custom properties that have a value, in a new style sheet on the art. Its old one is removed and handed to
        /// <paramref name="retire"/>: the art's computed custom properties point into it until the next style update, so
        /// it can only be destroyed after that one.
        /// </summary>
        public static void ApplyCustom(ArtType type, VisualElement art, IReadOnlyDictionary<string, object> values, List<StyleSheet> sheets,
            Action<StyleSheet> retire = null)
        {
            var custom = type.controls.Where(c => c.target == Target.CustomProperty && values.TryGetValue(c.name, out var v) && v != null)
                .Select(c => "    " + c.Uss(values[c.name])).ToList();
            for (int i = art.styleSheets.count - 1; i >= 0; i--)
            {
                var old = art.styleSheets[i];
                if (sheets.Remove(old))
                {
                    art.styleSheets.Remove(old);
                    if (retire != null)
                        retire(old);
                    else
                        sheets.Add(old); // destroyed with the others, when the caller is done
                }
            }
            if (custom.Count == 0)
                return;
            // :root is the element that holds the sheet: the art itself, as a theme's :root is the element it's on.
            var sheet = CreateStyleSheet(":root {\n" + string.Join("\n", custom) + "\n}");
            if (sheet == null)
                return;
            sheets.Add(sheet);
            art.styleSheets.Add(sheet);
        }

        public static bool IsSize(Control control) => control.target == Target.Style && (control.name == "size" || control.name == "width" || control.name == "height");

        /// <summary>A custom property's value on the element: its own rules', else the nearest ancestor's (a theme's :root), else null.</summary>
        public static object Resolved(VisualElement art, Control control)
        {
            for (var e = art; e != null; e = e.hierarchy.parent)
            {
                switch (control.type)
                {
                    case ValueType.Color when e.customStyle.TryGetValue(new CustomStyleProperty<Color>(control.name), out var color):
                        return color;
                    case ValueType.Float when e.customStyle.TryGetValue(new CustomStyleProperty<float>(control.name), out var number):
                        return number;
                    case ValueType.Choice when e.customStyle.TryGetValue(new CustomStyleProperty<string>(control.name), out var keyword) && !string.IsNullOrEmpty(keyword):
                        return keyword.Trim().Trim('"', '\'').ToLowerInvariant();
                }
            }
            return null;
        }

        /// <summary>The UXML and USS for the art as set: the attributes that changed, the size, and the styles and custom properties that changed.</summary>
        public static (string uxml, string uss) Markup(ArtType type, IReadOnlyDictionary<string, object> values)
        {
            const string className = "my-art";
            var attributes = type.controls
                .Where(c => c.target == Target.Attribute && values.TryGetValue(c.name, out var v) && !Equals(v, c.value))
                .Select(c => " " + c.Uxml(values[c.name]));
            var tag = type.uxmlTag ?? "ui:VisualElement";
            var uxml = $"<{tag}{string.Concat(attributes)} class=\"{className}\" />";

            var declarations = new List<string>();
            if (type.fills)
                declarations.Add("position: absolute; left: 0; top: 0; right: 0; bottom: 0;");
            foreach (var c in type.controls)
            {
                if (c.target == Target.Attribute || !values.TryGetValue(c.name, out var v) || v == null)
                    continue;
                if (IsSize(c) || c.target == Target.CustomProperty || !Equals(v, c.value) || c.name == "background-image")
                {
                    var declaration = c.Uss(v);
                    if (!string.IsNullOrEmpty(declaration))
                        declarations.Add(declaration);
                }
            }
            if (type.markupUss != null)
                declarations.AddRange(type.markupUss(values));
            var uss = $".{className} {{\n" + string.Join("\n", declarations.Select(d => "    " + d)) + "\n}";
            return (uxml, uss);
        }

        // ------------------------------------------------------------------ style sheets from text

        static Type s_ImporterType;
        static MethodInfo s_Import;
        static bool s_ImporterLooked;

        /// <summary>
        /// A style sheet from USS text, as UI Builder and the engine's own tests make them (the USS importer, internal to the
        /// Editor, reached by reflection). Null if it can't be reached; the caller destroys the sheet.
        /// </summary>
        public static StyleSheet CreateStyleSheet(string uss)
        {
            if (!s_ImporterLooked)
            {
                s_ImporterLooked = true;
                s_ImporterType = typeof(Editor).Assembly.GetType("UnityEditor.UIElements.StyleSheets.StyleSheetImporterImpl")
                    ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.UIElements.StyleSheets.StyleSheetImporterImpl")).FirstOrDefault(t => t != null);
                s_Import = s_ImporterType?.GetMethod("Import", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new[] { typeof(StyleSheet), typeof(string) }, null);
                if (s_Import == null)
                    Debug.LogWarning("[Bagel] Bagel Features: the USS importer wasn't found; the art's custom properties can't be overridden.");
            }
            if (s_Import == null)
                return null;
            var sheet = ScriptableObject.CreateInstance<StyleSheet>();
            sheet.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var importer = Activator.CreateInstance(s_ImporterType, true);
                s_Import.Invoke(importer, new object[] { sheet, uss });
                return sheet;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Bagel Features: couldn't make a style sheet: {e.GetBaseException().Message}");
                UnityEngine.Object.DestroyImmediate(sheet);
                return null;
            }
        }

        static bool? s_CanOverride;

        /// <summary>Whether custom properties can be set (a style sheet can be made from text).</summary>
        public static bool canOverrideCustomProperties => s_CanOverride ??= CreateProbe();

        static bool CreateProbe()
        {
            var sheet = CreateStyleSheet(":root { --be-probe: 1; }");
            if (sheet == null)
                return false;
            UnityEngine.Object.DestroyImmediate(sheet);
            return true;
        }

        // ------------------------------------------------------------------ helpers

        static readonly Dictionary<string, VectorImage> s_Icons = new();

        public static VectorImage Icon(string name)
        {
            if (!s_Icons.TryGetValue(name, out var icon) || icon == null)
                s_Icons[name] = icon = AssetDatabase.LoadAssetAtPath<VectorImage>(iconsFolder + "icon-" + name + ".svg");
            return icon;
        }

        static Use U(GameScreen screen, string title, string part, string element = null, string uxml = null) =>
            new() { screen = screen, title = title, part = part, element = element, uxml = uxml };

        static Variation V(string caption, string tooltip, params (string, object)[] values) =>
            new() { caption = caption, tooltip = tooltip, values = values };

        static Variation Season(int season, params (string, object)[] values) => new() { season = season, values = values };

        static Variation[] Seasons(string tooltip, params (string, object)[] values) =>
            Enumerable.Range(0, 4).Select(i => new Variation { season = i, tooltip = tooltip, values = values }).ToArray();

        static Control Bool<T>(string name, string label, bool value, Action<T, bool> set) where T : VisualElement =>
            new() { name = name, label = label, target = Target.Attribute, type = ValueType.Bool, value = value, set = (e, v) => { if (e is T t) set(t, (bool)v); } };

        static Control Int<T>(string name, string label, int value, int min, int max, Action<T, int> set) where T : VisualElement =>
            new() { name = name, label = label, target = Target.Attribute, type = ValueType.Int, value = value, min = min, max = max, set = (e, v) => { if (e is T t) set(t, Convert.ToInt32(v)); } };

        static Control Float<T>(string name, string label, float value, float min, float max, Action<T, float> set) where T : VisualElement =>
            new() { name = name, label = label, target = Target.Attribute, type = ValueType.Float, value = value, min = min, max = max, set = (e, v) => { if (e is T t) set(t, Convert.ToSingle(v)); } };

        static Control Text<T>(string name, string label, string value, Action<T, string> set) where T : VisualElement =>
            new() { name = name, label = label, target = Target.Attribute, type = ValueType.Text, value = value, set = (e, v) => { if (e is T t) set(t, (string)v); } };

        static Control Enum<T, TEnum>(string name, string label, TEnum value, Action<T, TEnum> set) where T : VisualElement where TEnum : struct, System.Enum =>
            new()
            {
                name = name, label = label, target = Target.Attribute, type = ValueType.Choice, value = value.ToString(), choices = System.Enum.GetNames(typeof(TEnum)),
                set = (e, v) => { if (e is T t && System.Enum.TryParse<TEnum>((string)v, out var parsed)) set(t, parsed); },
            };

        static Control Size(float value, float min, float max) => new()
        {
            name = "size", label = "Size", tooltip = "width and height", target = Target.Style, type = ValueType.Float, value = value, min = min, max = max,
            set = (e, v) => e.style.width = e.style.height = Convert.ToSingle(v),
            uss = v => $"width: {Number(Convert.ToSingle(v))}px; height: {Number(Convert.ToSingle(v))}px;",
        };

        static Control Width(float value, float min, float max) => new()
        {
            name = "width", label = "Width", target = Target.Style, type = ValueType.Float, value = value, min = min, max = max,
            set = (e, v) => e.style.width = Convert.ToSingle(v), uss = v => $"width: {Number(Convert.ToSingle(v))}px;",
        };

        static Control Height(float value, float min, float max) => new()
        {
            name = "height", label = "Height", target = Target.Style, type = ValueType.Float, value = value, min = min, max = max,
            set = (e, v) => e.style.height = Convert.ToSingle(v), uss = v => $"height: {Number(Convert.ToSingle(v))}px;",
        };

        static Control CustomColor(string property, string label, Color fallback) => new()
        {
            name = property, label = label, tooltip = property, target = Target.CustomProperty, type = ValueType.Color, fallback = fallback,
        };

        static Control CustomKeyword(string property, string label, string[] choices, string fallback) => new()
        {
            name = property, label = label, tooltip = property, target = Target.CustomProperty, type = ValueType.Choice, choices = choices, fallback = fallback,
        };

        /// <summary>The ink outlines' width, a multiple of the element's own (the game leaves it at 1).</summary>
        static Control InkWidth() => CustomNumber("--be-ink-width", "Ink width", 1f, 0.25f, 4f);

        static Control CustomNumber(string property, string label, float fallback, float min, float max) => new()
        {
            name = property, label = label, tooltip = property, target = Target.CustomProperty, type = ValueType.Float, fallback = fallback, min = min, max = max,
        };

        public static string Text(ValueType type, object v) => v switch
        {
            null => "",
            Color c => ColorText(c),
            float f => Number(f),
            double d => Number((float)d),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(v, CultureInfo.InvariantCulture),
        };

        public static string Number(float f) => (Mathf.Round(f * 100f) / 100f).ToString("0.##", CultureInfo.InvariantCulture);

        public static string ColorText(Color c)
        {
            int R(float x) => Mathf.RoundToInt(Mathf.Clamp01(x) * 255f);
            return c.a >= 0.999f ? $"rgb({R(c.r)}, {R(c.g)}, {R(c.b)})" : $"rgba({R(c.r)}, {R(c.g)}, {R(c.b)}, {Number(c.a)})";
        }

        static string Escape(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;");

        /// <summary>"SakuraMochiBagel" → "Sakura Mochi Bagel".</summary>
        static string Spaced(string s) => string.Concat(s.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
    }
}
