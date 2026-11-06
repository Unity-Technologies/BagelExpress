using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Bagel > Bagel UI: every screen of the game, live, side by side in phone
    /// frames, inside an EditorWindow and without entering Play mode.
    ///
    /// It works because all game UI is pure UI logic in custom VisualElements (the managers).
    /// The window clones BakeryAppScreen.uxml once per phone, picks a start page, and hands
    /// every phone the SAME BakeryState: upgrade a shop in the Shops phone and the coin pill in
    /// every other phone counts down through data binding. The Inspector on the right edits
    /// that very instance (OnValidate → NotifyAll), so typing a value updates all phones.
    /// </summary>
    public partial class BagelExpressAllUIWindow : EditorWindow
    {
        public const string windowTitle = "Bagel UI";
        public const string windowUxmlPath = "Assets/UI/Editor/B10_EditorTools/BagelExpressAllUIWindow.uxml";
        public const string appUxmlPath = "Assets/UI/Runtime/B03_App/BakeryAppScreen.uxml";
        public const string startStatePath = "Assets/Game/Core/DemoStartState.asset";
        public const string themeCollectionPath = "Assets/UI/Runtime/B00_Themes/T0_Common/BagelThemeCollection.asset";

        public const float minZoom = 0.5f;
        public const float maxZoom = 1f;
        public const float defaultZoom = 0.75f;

        // Design size of the game (portrait, logical px) and the device bezel around it.
        const float k_ScreenWidth = 390f;
        const float k_ScreenHeight = 844f;
        const float k_Bezel = 10f;

        public static readonly string phoneUssClassName = "beall-phone";
        public static readonly string slotUssClassName = phoneUssClassName + "__slot";
        public static readonly string glowUssClassName = phoneUssClassName + "__glow";
        public static readonly string deviceUssClassName = phoneUssClassName + "__device";
        public static readonly string screenUssClassName = phoneUssClassName + "__screen";
        public static readonly string notchUssClassName = phoneUssClassName + "__notch";
        public static readonly string cameraUssClassName = phoneUssClassName + "__camera";
        public static readonly string sideButtonUssClassName = phoneUssClassName + "__side-button";
        public static readonly string captionUssClassName = phoneUssClassName + "__caption";
        public static readonly string linkUssClassName = phoneUssClassName + "__link";
        public static readonly UniqueStyleString dockHiddenUssClassName = new("beall__dock--hidden");
        public static readonly string coinsUssClassName = "beall__cheat--coins";
        public static readonly string gemsUssClassName = "beall__cheat--gems";
        public static readonly string ticketsUssClassName = "beall__cheat--tickets";

        /// <summary>One phone on the stage: the game screen it shows (its page and what's open on it) and its caption.</summary>
        public readonly struct PhoneSpec
        {
            public readonly GameScreen screen;

            public PhoneSpec(GameScreen screen) => this.screen = screen;

            public string key => screen.id;
            public string title => screen.title;
            public AppPage startPage => screen.page;
            public bool showTitle => screen.showTitle;
            public string elementName => "phone-" + key;
        }

        /// <summary>The Bagel Features window's Game mode screens, in its order (and with its numbers).</summary>
        public static readonly PhoneSpec[] phoneSpecs = BagelFeatureGuide.screens.Select(s => new PhoneSpec(s.screen)).ToArray();

        /// <summary>A built phone frame: the frame elements and the app running inside.</summary>
        public class Phone
        {
            public PhoneSpec spec;
            public VisualElement root;
            public VisualElement slot;
            public VisualElement device;
            public TemplateContainer screen;
            public BakeryAppManager app;
            /// <summary>Out of view: its art holds still and its filters are off (see UpdateStillPhones).</summary>
            public bool still;
            /// <summary>The elements whose filters were turned off while it holds still.</summary>
            public readonly List<VisualElement> unfiltered = new();
        }

        [SerializeField] float m_Zoom = defaultZoom;
        [SerializeField] bool m_ShowInspector;

        BakeryState m_State;
        ThemeStyleSheet m_CurrentTheme;
        BagelThemeCollection m_ThemeCollection;
        readonly List<Phone> m_Phones = new();

        BagelOptionStrip m_SeasonStrip;
        BagelOptionStrip m_LanguageStrip;
        Slider m_ZoomSlider;
        Label m_ZoomLabel;
        VisualElement m_Dock;
        VisualElement m_InspectorToggle;
        VisualElement m_InspectorHost;
        InspectorElement m_Inspector;

        // Editor theme handling (see LiftEditorTheme).
        readonly List<StyleSheet> m_EditorSheets = new();
        readonly List<(VisualElement owner, StyleSheet sheet)> m_BorrowedSheets = new();
        readonly List<VisualElement> m_EditorStyledParts = new();

        /// <summary>The one game state every phone is bound to.</summary>
        public BakeryState state => m_State;
        public IReadOnlyList<Phone> phones => m_Phones;
        public ThemeStyleSheet currentTheme => m_CurrentTheme;
        public float zoom => m_Zoom;

        [MenuItem("Bagel/Bagel UI", priority = 10, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static BagelExpressAllUIWindow Open()
        {
            var window = GetWindow<BagelExpressAllUIWindow>();
            window.titleContent = BagelWindowIcons.Title(windowTitle, BagelWindowIcons.bagelUI);
            window.minSize = new Vector2(720f, 480f);
            return window;
        }

        // ---------------------------------------------------------------- Lifetime

        public void CreateGUI()
        {
            titleContent = BagelWindowIcons.Title(windowTitle, BagelWindowIcons.bagelUI);

            // A fresh, throwaway copy of the demo start state, shared by every phone.
            // (Recreated after each domain reload; destroyed in OnDisable.)
            DestroyState();
            m_State = BakeryState.CreateRuntimeCopy(AssetDatabase.LoadAssetAtPath<BakeryState>(startStatePath));
            m_ThemeCollection = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(themeCollectionPath);

            var windowTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(windowUxmlPath);
            if (windowTree == null)
            {
                rootVisualElement.Add(new Label($"Missing {windowUxmlPath}"));
                return;
            }

            // Remove the editor theme (BagelGame-era editors put it on rootVisualElement itself).
            m_EditorSheets.Clear();
            for (int i = 0; i < rootVisualElement.styleSheets.count; i++)
                m_EditorSheets.Add(rootVisualElement.styleSheets[i]);
            rootVisualElement.styleSheets.Clear();

            windowTree.CloneTree(rootVisualElement);

            // Toolbar and Inspector keep the editor look; the phones get the game's runtime theme.
            m_EditorStyledParts.Clear();
            m_EditorStyledParts.Add(rootVisualElement.Q<Toolbar>("toolbar"));
            m_EditorStyledParts.Add(rootVisualElement.Q("inspector-dock"));
            var phonesScroll = rootVisualElement.Q<ScrollView>("phones-scroll");
            if (phonesScroll != null)
            {
                m_EditorStyledParts.Add(phonesScroll.horizontalScroller);
                m_EditorStyledParts.Add(phonesScroll.verticalScroller);
            }
            rootVisualElement.RegisterCallback<AttachToPanelEvent>(OnRootAttached);
            rootVisualElement.RegisterCallback<DetachFromPanelEvent>(OnRootDetached);
            if (rootVisualElement.panel != null)
                LiftEditorTheme();
            else
                ShareEditorTheme();

            BuildPhones();
            BindToolbar();
            BindInspector();
            BindStillPhones();

            SettingsPaneManager.themeChanged -= OnThemeChanged;
            SettingsPaneManager.themeChanged += OnThemeChanged;
            BagelLocalization.localeChanged -= SyncLanguageStrip;
            BagelLocalization.localeChanged += SyncLanguageStrip;
        }

        // Closed for good: its phones give their resources back to be reused now.
        void OnDestroy() => rootVisualElement.Clear(VisualElementClearOptions.RecursiveReleaseResources);

        void OnDisable()
        {
            ReleaseStillPhones();
            SettingsPaneManager.themeChanged -= OnThemeChanged;
            BagelLocalization.localeChanged -= SyncLanguageStrip;
            RestoreEditorTheme();
            m_Phones.Clear();
            DestroyState();
        }

        void DestroyState()
        {
            if (m_State != null)
                DestroyImmediate(m_State);
            m_State = null;
        }

        // ---------------------------------------------------------------- Editor theme

        // The editor theme (DefaultCommonDark/Light) is a builtin-tier style sheet that would
        // leak editor looks (fonts, scrollers, field styles) into the game UI. Like BagelGame,
        // we take it off the window and give it back only to the editor-styled parts (toolbar,
        // Inspector). Older editors keep it on rootVisualElement (removed in CreateGUI); this
        // one keeps it on the dock area's panel root, so we borrow it from there while the
        // window is attached and give it back when it detaches (closed, tab switched, re-docked).

        void OnRootAttached(AttachToPanelEvent evt) => LiftEditorTheme();
        void OnRootDetached(DetachFromPanelEvent evt) => RestoreEditorTheme();

        void LiftEditorTheme()
        {
            if (m_BorrowedSheets.Count == 0)
            {
                for (var ancestor = rootVisualElement.hierarchy.parent; ancestor != null; ancestor = ancestor.hierarchy.parent)
                {
                    for (int i = 0; i < ancestor.styleSheets.count; i++)
                    {
                        var sheet = ancestor.styleSheets[i];
                        m_BorrowedSheets.Add((ancestor, sheet));
                        if (!m_EditorSheets.Contains(sheet))
                            m_EditorSheets.Add(sheet);
                    }
                    ancestor.styleSheets.Clear();
                }
            }
            ShareEditorTheme();
        }

        void ShareEditorTheme()
        {
            foreach (var part in m_EditorStyledParts)
                if (part != null)
                    foreach (var sheet in m_EditorSheets)
                        if (!part.styleSheets.Contains(sheet))
                            part.styleSheets.Add(sheet);
        }

        void RestoreEditorTheme()
        {
            foreach (var (owner, sheet) in m_BorrowedSheets)
                if (owner != null && !owner.styleSheets.Contains(sheet))
                    owner.styleSheets.Add(sheet);
            m_BorrowedSheets.Clear();
        }

        // ---------------------------------------------------------------- Phones

        void BuildPhones()
        {
            m_Phones.Clear();
            var container = rootVisualElement.Q("phones");
            var appTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(appUxmlPath);
            if (container == null || appTree == null)
                return;
            // The phones are the game, live: its season's particles fall and Begu-chan blinks without Play mode.
            container.AddToClassList(ArtKit.animateInEditModeUssClassName);

            var seasonIndex = Mathf.Clamp(SettingsPaneManager.seasonIndex, 0, ThemeCount() - 1);
            m_CurrentTheme = ThemeCount() > 0 ? m_ThemeCollection.collection[seasonIndex].theme : null;

            for (int i = 0; i < phoneSpecs.Length; i++)
            {
                var phone = CreatePhone(i, phoneSpecs[i], appTree);
                m_Phones.Add(phone);
                // Added last: by now the app has its page and the shared state, so attaching
                // to the panel doesn't create (and leak) a state copy per phone.
                container.Add(phone.root);
            }

            ApplyZoom(m_Zoom);
        }

        Phone CreatePhone(int index, PhoneSpec spec, VisualTreeAsset appTree)
        {
            var phone = new Phone { spec = spec };

            phone.root = new VisualElement { name = spec.elementName };
            phone.root.AddToClassList(phoneUssClassName);

            phone.slot = new VisualElement { name = "slot", pickingMode = PickingMode.Ignore };
            phone.slot.AddToClassList(slotUssClassName);
            phone.root.Add(phone.slot);

            phone.slot.Add(Decoration(glowUssClassName));

            phone.device = new VisualElement { name = "device" };
            phone.device.AddToClassList(deviceUssClassName);
            phone.slot.Add(phone.device);

            // The game itself: the same UXML the PanelRenderer loads at runtime.
            phone.screen = appTree.Instantiate();
            phone.screen.name = "screen";
            phone.screen.AddToClassList(screenUssClassName);
            if (m_CurrentTheme != null)
                phone.screen.styleSheets.Add(m_CurrentTheme);
            phone.device.Add(phone.screen);

            phone.app = phone.screen.Q<BakeryAppManager>();
            if (phone.app != null)
            {
                phone.app.startPage = spec.startPage;
                phone.app.showTitle = spec.showTitle;
                phone.app.SetState(m_State);
                // Each phone stays on its screen: clicks play (upgrades, collecting, seasons) but don't navigate away.
                phone.app.pinned = true;
                // Then what the screen has open over its page (a tip, a dialog, a reveal), once it's laid out.
                if (spec.screen.hasSetUp)
                    phone.app.schedule.Execute(() =>
                    {
                        phone.app.pinned = false;
                        spec.screen.SetUp(phone.app);
                        phone.app.pinned = true;
                    }).ExecuteLater(400);
            }

            var notch = Decoration(notchUssClassName);
            notch.Add(Decoration(cameraUssClassName));
            phone.device.Add(notch);
            foreach (var side in new[] { "action", "volume-up", "volume-down", "power" })
                phone.device.Add(Decoration(sideButtonUssClassName, sideButtonUssClassName + "--" + side));

            var caption = new VisualElement { name = "caption" };
            caption.AddToClassList(captionUssClassName);
            caption.Add(CaptionLabel((index + 1).ToString("00"), "__index"));
            caption.Add(CaptionLabel(spec.title, "__title"));
            phone.root.Add(caption);

            var link = new Button(() => ShowInBagelFeatures(index))
            {
                name = "features-link",
                text = "Bagel Features ›",
                tooltip = $"The {spec.title} screen in Bagel Features › Game: its key parts, how each is made, and buttons into the Editor",
            };
            link.AddToClassList(linkUssClassName);
            phone.root.Add(link);

            return phone;
        }

        static VisualElement Decoration(params string[] classNames)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames)
                element.AddToClassList(className);
            return element;
        }

        static Label CaptionLabel(string text, string elementSuffix)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(phoneUssClassName + elementSuffix);
            return label;
        }

        public Phone GetPhone(string key) => m_Phones.Find(p => p.spec.key == key);

        /// <summary>The phone that shows the given page as it is (not the title, nor a tip or dialog over it).</summary>
        public Phone GetPhone(AppPage page) => m_Phones.Find(p => p.spec.startPage == page && !p.spec.showTitle && !p.spec.screen.hasSetUp);

        /// <summary>A phone's screen in the Bagel Features window's Game mode, docked by this window.</summary>
        public static BagelFeaturesWindow ShowInBagelFeatures(int screenIndex)
        {
            var window = BagelFeaturesWindow.Open();
            window.ShowScreen(screenIndex);
            window.Focus();
            return window;
        }

        /// <summary>Scale every phone. The slot keeps the scaled size in the layout so phones don't overlap or leave gaps.</summary>
        public void ApplyZoom(float value)
        {
            m_Zoom = Mathf.Clamp(value, minZoom, maxZoom);
            var width = (k_ScreenWidth + 2f * k_Bezel) * m_Zoom;
            var height = (k_ScreenHeight + 2f * k_Bezel) * m_Zoom;
            foreach (var phone in m_Phones)
            {
                // Every screen's column is the phone's width, whatever its caption: the phones are evenly spaced.
                phone.root.style.width = width;
                phone.slot.style.width = width;
                phone.slot.style.height = height;
                phone.device.style.scale = new Scale(new Vector2(m_Zoom, m_Zoom));
            }

            m_ZoomSlider?.SetValueWithoutNotify(m_Zoom);
            if (m_ZoomLabel != null)
                m_ZoomLabel.text = Mathf.RoundToInt(m_Zoom * 100f) + "%";
        }

        // ---------------------------------------------------------------- Theme

        int ThemeCount() => m_ThemeCollection != null && m_ThemeCollection.collection != null ? m_ThemeCollection.collection.Count : 0;

        /// <summary>Swap the season ThemeStyleSheet on every phone screen (each phone is its own theme root).</summary>
        public void ApplyRuntimeTheme(ThemeStyleSheet theme)
        {
            if (theme == null)
                return;

            foreach (var phone in m_Phones)
            {
                if (m_CurrentTheme != null)
                    phone.screen.styleSheets.Remove(m_CurrentTheme);
                if (!phone.screen.styleSheets.Contains(theme))
                    phone.screen.styleSheets.Add(theme);
            }
            m_CurrentTheme = theme;
            SyncSeasonStrip();
        }

        /// <summary>
        /// Pick a season like the in-game Settings page does, so the Settings phones stay in sync.
        /// The page raises SettingsPaneManager.themeChanged, which lands in OnThemeChanged.
        /// </summary>
        public void SelectSeason(int index)
        {
            if (index < 0 || index >= ThemeCount())
                return;

            var settings = GetPhone(AppPage.Settings)?.app?.Q<SettingsPaneManager>();
            if (settings != null && settings.themeCollection != null)
                settings.SelectSeason(index);
            else
                ApplyRuntimeTheme(m_ThemeCollection.collection[index].theme);
        }

        void OnThemeChanged(ThemeStyleSheet theme) => ApplyRuntimeTheme(theme);

        void SyncSeasonStrip()
        {
            if (m_SeasonStrip != null && m_ThemeCollection != null)
                m_SeasonStrip.SetSelectedWithoutNotify(m_ThemeCollection.collection.FindIndex(e => e.theme == m_CurrentTheme));
        }

        // ---------------------------------------------------------------- Toolbar

        void BindToolbar()
        {
            var toolbar = rootVisualElement.Q<Toolbar>("toolbar");
            toolbar?.RegisterCallback<GeometryChangedEvent>(_ => BagelGuideWindow.FitToolbar(toolbar));

            m_SeasonStrip = rootVisualElement.Q<BagelOptionStrip>("season-strip");
            if (m_SeasonStrip != null)
            {
                BagelPickerIcons.AddSeasons(m_SeasonStrip, m_ThemeCollection);
                SyncSeasonStrip();
                m_SeasonStrip.chosen += SelectSeason;
            }

            // Language preview: the phones' LocalizedString bindings and localized names follow the locale.
            m_LanguageStrip = rootVisualElement.Q<BagelOptionStrip>("language-strip");
            if (m_LanguageStrip != null)
            {
                BagelPickerIcons.AddLanguages(m_LanguageStrip);
                SyncLanguageStrip();
                m_LanguageStrip.chosen += index => BagelLocalization.SetLocale(BagelPickerIcons.languages[index].code);
            }

            if (rootVisualElement.Q<BagelOptionStrip>("reset-strip") is { } resetStrip)
                BagelPickerIcons.AddReset(resetStrip, ResetWindow);

            // New Game and the cheats: words in strips like the others. New Game alone, then what you earn, then what you unlock.
            if (rootVisualElement.Q<BagelOptionStrip>("new-game-strip") is { } newGameStrip)
                newGameStrip.AddTextAction("New Game", "Fresh copy of DemoStartState, shared by every phone", NewGame).name = "new-game-button";
            if (rootVisualElement.Q<BagelOptionStrip>("currency-strip") is { } currencyStrip)
            {
                var coins = currencyStrip.AddTextAction("+1,000", "Cheat: add 1,000 coins to the shared state", () => AddCoins(1000), BagelPickerIcons.Currency(CurrencyKind.Coins));
                coins.name = "add-coins-button";
                coins.AddToClassList(coinsUssClassName);
                var gems = currencyStrip.AddTextAction("+60", "Cheat: add 60 Maple Gems to the shared state", () => AddGems(60), BagelPickerIcons.Currency(CurrencyKind.Gems));
                gems.name = "add-gems-button";
                gems.AddToClassList(gemsUssClassName);
                var tickets = currencyStrip.AddTextAction("+5", "Cheat: add 5 Lucky Tickets to the shared state", () => AddTickets(5), BagelPickerIcons.Currency(CurrencyKind.Tickets));
                tickets.name = "add-tickets-button";
                tickets.AddToClassList(ticketsUssClassName);
            }
            if (rootVisualElement.Q<BagelOptionStrip>("unlock-strip") is { } unlockStrip)
            {
                unlockStrip.AddTextAction("All", "Cheat: open every shop, free", UnlockAllShops, BagelPickerIcons.Page(AppPage.Shops)).name = "unlock-shops-button";
                unlockStrip.AddTextAction("All", "Cheat: every recipe in the Recipe Book", OwnAllRecipes, BagelPickerIcons.Page(AppPage.Recipes)).name = "unlock-recipes-button";
            }

            m_ZoomLabel = rootVisualElement.Q<Label>("zoom-label");
            m_ZoomSlider = rootVisualElement.Q<Slider>("zoom-slider");
            if (m_ZoomSlider != null)
            {
                m_ZoomSlider.lowValue = minZoom;
                m_ZoomSlider.highValue = maxZoom;
                m_ZoomSlider.RegisterValueChangedCallback(evt => ApplyZoom(evt.newValue));
            }
            ApplyZoom(m_Zoom);

            m_Dock = rootVisualElement.Q("inspector-dock");
            if (rootVisualElement.Q<BagelOptionStrip>("inspector-strip") is { } inspectorStrip)
            {
                m_InspectorToggle = inspectorStrip.AddToggle(BagelPickerIcons.Inspector(), "Inspector", m_ShowInspector, ShowInspector);
                m_InspectorToggle.name = "inspector-toggle";
            }
            ShowInspector(m_ShowInspector);
        }

        void SyncLanguageStrip() => m_LanguageStrip?.SetSelectedWithoutNotify(BagelPickerIcons.LanguageIndex(BagelLocalization.localeCode));

        public void ShowInspector(bool show)
        {
            m_ShowInspector = show;
            m_Dock?.EnableInClassList(dockHiddenUssClassName, !show);
            BagelOptionStrip.SetOnWithoutNotify(m_InspectorToggle, show);
        }

        /// <summary>
        /// Back to how the window first opens: a fresh copy of the demo start state, the first season and the project's
        /// language (in every Bagel window, as Bagel Features' Reset does), the phones at the default zoom from the top
        /// left, and no Inspector.
        /// </summary>
        public void ResetWindow()
        {
            BagelGuideWindow.ResetSeasonAndLanguage();
            NewGame(announce: false);
            ApplyZoom(defaultZoom);
            ShowInspector(false);
            if (rootVisualElement.Q<ScrollView>("phones-scroll") is { } scroll)
                scroll.scrollOffset = Vector2.zero;
        }

        /// <summary>Start over: a fresh copy of the demo start state, handed to every phone.</summary>
        public void NewGame() => NewGame(announce: true);

        void NewGame(bool announce)
        {
            var old = m_State;
            m_State = BakeryState.CreateRuntimeCopy(AssetDatabase.LoadAssetAtPath<BakeryState>(startStatePath));
            foreach (var phone in m_Phones)
                phone.app?.SetState(m_State);
            BindInspector();
            if (old != null)
                DestroyImmediate(old);

            if (announce)
                GetPhone(AppPage.Home)?.app?.ShowToast("New game! · ニューゲーム");
        }

        public void AddCoins(long amount)
        {
            if (m_State != null)
                m_State.coins += amount;
        }

        public void AddGems(int amount)
        {
            if (m_State != null)
                m_State.gems += amount;
        }

        public void AddTickets(int amount)
        {
            if (m_State != null)
                m_State.tickets += amount;
        }

        public void UnlockAllShops()
        {
            if (m_State != null)
                m_State.UnlockAllShops();
        }

        public void OwnAllRecipes()
        {
            if (m_State != null)
                m_State.OwnAllRecipes();
        }

        // ---------------------------------------------------------------- Inspector

        void BindInspector()
        {
            if (m_InspectorHost == null || m_InspectorHost.panel != rootVisualElement.panel)
                m_InspectorHost = rootVisualElement.Q("inspector-host");
            if (m_InspectorHost == null)
                return;

            m_InspectorHost.Clear();
            m_Inspector = null;
            if (m_State == null)
                return;

            m_Inspector = new InspectorElement(m_State) { name = "state-inspector" };
            m_InspectorHost.Add(m_Inspector);
        }
    }
}
