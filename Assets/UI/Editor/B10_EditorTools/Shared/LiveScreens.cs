using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// A state of the game to show live in an editor window: a page, plus what's open on it (the tutorial tip, LEVEL UP!,
    /// a Lucky Bag reveal…). Every screen is the real BakeryAppScreen.uxml, set up through the managers' public API.
    /// </summary>
    public sealed class GameScreen
    {
        public readonly string id;
        public readonly string title;
        public readonly string japaneseTitle;
        public readonly AppPage page;
        public readonly bool showTitle;
        /// <summary>The set-up creates elements over time (the Lucky Bag reveal): they only exist once it has played.</summary>
        public readonly bool animatedSetUp;
        readonly Action<BakeryAppManager> m_SetUp;
        readonly Action<BakeryAppManager> m_TearDown;

        GameScreen(string id, string title, string japaneseTitle, AppPage page, bool showTitle = false, Action<BakeryAppManager> setUp = null,
            bool animatedSetUp = false, Action<BakeryAppManager> tearDown = null)
        {
            this.animatedSetUp = animatedSetUp;
            m_TearDown = tearDown;
            this.id = id;
            this.title = title;
            this.japaneseTitle = japaneseTitle;
            this.page = page;
            this.showTitle = showTitle;
            m_SetUp = setUp;
        }

        /// <summary>Whether this state opens something over its page (a tip, a dialog, a reveal…).</summary>
        public bool hasSetUp => m_SetUp != null;

        /// <summary>Open what this state needs, once the app is in a panel and laid out.</summary>
        public void SetUp(BakeryAppManager app)
        {
            if (app == null || m_SetUp == null)
                return;
            try
            {
                m_SetUp(app);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Couldn't set up the {title} screen: {e.Message}");
            }
        }

        /// <summary>
        /// Undo what the set-up leaves behind once the app moves on to another screen (the demo toolbar's apps do; the
        /// Bagel windows' phones keep theirs). Showing a screen closes the dialogs and the tip; only the Performance
        /// screen leaves something else: its overlay on and its switch locked.
        /// </summary>
        public void TearDown(BakeryAppManager app)
        {
            if (app == null || m_TearDown == null)
                return;
            try
            {
                m_TearDown(app);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Couldn't leave the {title} screen: {e.Message}");
            }
        }

        public override string ToString() => title;

        public static readonly GameScreen titleScreen = new("title", "Title", "タイトル", AppPage.Home, showTitle: true);
        public static readonly GameScreen tutorial = new("tutorial", "Tutorial", "チュートリアル", AppPage.Home, setUp: app =>
            app.tutorial?.Show(app.Q("collect-button"), BagelLocalization.Get("tutorial.collect"), BagelLocalization.Get("tutorial.tip", 1, 3), () => { }));
        public static readonly GameScreen home = new("home", "Home", "ホーム", AppPage.Home);
        public static readonly GameScreen shops = new("shops", "Shops", "ショップ", AppPage.Shops);
        public static readonly GameScreen luckyBag = new("lucky-bag", "Lucky Bag", "福袋", AppPage.LuckyBag);
        public static readonly GameScreen reveal = new("reveal", "Lucky Bag reveal", "開封", AppPage.LuckyBag, setUp: app =>
        {
            if (app.state != null && app.state.tickets < 1)
                app.state.tickets = 1;
            app.Q<LuckyBagPaneManager>()?.Draw(1);
        }, animatedSetUp: true);
        public static readonly GameScreen recipes = new("recipes", "Recipe Book", "レシピ帳", AppPage.Recipes);
        public static readonly GameScreen settings = new("settings", "Settings", "設定", AppPage.Settings);
        public static readonly GameScreen settingsPerformance = new("settings-performance", "Settings · Performance", "設定・パフォーマンス", AppPage.Settings, setUp: app =>
        {
            // The overlay open on this screen only: its setting is shown on and locked, since changing it here would
            // turn it on (or off) in every other screen.
            var overlay = app.Q<PerfOverlay>();
            if (overlay != null)
            {
                // Bottom right, above the nav, once it's shown and has a size: clear of the panel's toggle, resolution
                // and shadows rows.
                var window = overlay.window;
                EventCallback<GeometryChangedEvent> place = null;
                place = _ =>
                {
                    if (window.layout.width < 1f)
                        return;
                    window.UnregisterCallback(place);
                    window.style.left = app.layout.width - window.layout.width - 12f;
                    window.style.top = app.layout.height - window.layout.height - 150f;
                };
                window.RegisterCallback(place);
                overlay.alwaysShown = true;
            }
            var settingsPane = app.Q<SettingsPaneManager>();
            if (settingsPane != null)
                settingsPane.perfOverlayLocked = true;
            // The panel where the first one sits unscrolled: just under the page's header.
            var scroll = app.Q<ScrollView>("settings-scroll");
            var performance = app.Q("performance-panel");
            if (scroll != null && performance != null && performance.parent.childCount > 0)
                scroll.scrollOffset = new Vector2(0, performance.layout.y - performance.parent[0].layout.y);
        }, tearDown: app =>
        {
            // The overlay as the setting has it, where it sits by default; the switch free; the page at its top.
            var overlay = app.Q<PerfOverlay>();
            if (overlay != null)
            {
                overlay.alwaysShown = false;
                overlay.window.style.left = StyleKeyword.Null;
                overlay.window.style.top = StyleKeyword.Null;
            }
            var settingsPane = app.Q<SettingsPaneManager>();
            if (settingsPane != null)
                settingsPane.perfOverlayLocked = false;
            var scroll = app.Q<ScrollView>("settings-scroll");
            if (scroll != null)
                scroll.scrollOffset = Vector2.zero;
        });
        public static readonly GameScreen levelUp = new("level-up", "LEVEL UP!", "レベルアップ", AppPage.Shops, setUp: app =>
        {
            if (app.state != null && app.state.shopStates.Count > 0)
                app.Q<LevelUpDialog>()?.Show(app.state.shopStates[0], 50);
        });
        public static readonly GameScreen confirm = new("confirm", "Confirm dialog", "確認", AppPage.LuckyBag, setUp: app => app.ConfirmBuyTicket());
        public static readonly GameScreen playerStats = new("player-stats", "Player stats", "プレイヤー情報", AppPage.Home, setUp: app => app.ShowPlayerStats());

        // Each page, then what opens over it (LEVEL UP! after Shops, the reveal and the dialog after the Lucky Bag…).
        public static readonly GameScreen[] all = { titleScreen, tutorial, home, shops, levelUp, luckyBag, reveal, confirm, recipes, settings, settingsPerformance, playerStats };

        public static GameScreen Get(string id) => all.FirstOrDefault(s => s.id == id);

        /// <summary>
        /// An element of a screen: <c>#name</c> or <c>name</c>, <c>.class</c>, a descendant chain (<c>#shops-page .be-shop-card</c>),
        /// and <c>@n</c> for the n-th match (0-based), e.g. <c>.be-shop-card@1</c>.
        /// </summary>
        public static VisualElement Find(VisualElement root, string selector)
        {
            if (root == null || string.IsNullOrEmpty(selector))
                return null;
            int nth = 0;
            var at = selector.LastIndexOf('@');
            if (at > 0 && int.TryParse(selector.Substring(at + 1), out var parsed))
            {
                nth = parsed;
                selector = selector.Substring(0, at);
            }
            var parts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var current = root;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var query = part.StartsWith(".") ? current.Query(null, part.Substring(1)) : current.Query(part.TrimStart('#'));
                current = i == parts.Length - 1 ? query.AtIndex(nth) : query.First();
                if (current == null)
                    return null;
            }
            return current;
        }

        /// <summary>Whether an element is showing: in a panel, displayed and visible all the way up, and not zero-sized.</summary>
        public static bool IsShowing(VisualElement element)
        {
            if (element?.panel == null || element.worldBound.width < 1f || element.worldBound.height < 1f)
                return false;
            for (var e = element; e != null; e = e.hierarchy.parent)
                if (e.resolvedStyle.display == DisplayStyle.None || e.resolvedStyle.visibility == Visibility.Hidden || e.resolvedStyle.opacity < 0.05f)
                    return false;
            return true;
        }
    }

    /// <summary>
    /// A phone with a live game screen in it, styled like the Bagel UI window's phones, with a layer on top for
    /// hotspots: badges and outlines over elements, which stay on them as the screen changes.
    /// </summary>
    public sealed class PhoneFrame : VisualElement
    {
        const float k_ScreenWidth = 390f;
        const float k_ScreenHeight = 844f;
        const float k_Bezel = 10f;

        public static readonly string hotspotLayerUssClassName = "bgg-hotspots";
        public static readonly string hotspotUssClassName = "bgg-hotspot";
        public static readonly string hotspotBadgeUssClassName = hotspotUssClassName + "__badge";
        public static readonly UniqueStyleString hotspotDimmedUssClassName = new(hotspotUssClassName + "--dimmed");
        public static readonly UniqueStyleString hotspotPulseUssClassName = new(hotspotUssClassName + "--pulse");
        public static readonly UniqueStyleString hotspotHoverUssClassName = new(hotspotUssClassName + "--hover");

        public readonly GameScreen screen;
        public readonly VisualElement slot;
        public readonly VisualElement device;
        public readonly TemplateContainer screenRoot;
        public readonly BakeryAppManager app;
        public readonly VisualElement hotspotLayer;
        float m_Zoom = 0.5f;

        public sealed class Hotspot
        {
            public string selector;
            public string key;
            public string number;
            public string tooltip;
            public Color color;
            public VisualElement outline;
            public Label badge;
        }

        readonly List<Hotspot> m_Hotspots = new();
        IVisualElementScheduledItem m_Tracker;

        public IReadOnlyList<Hotspot> hotspots => m_Hotspots;

        /// <summary>
        /// Pin the screen once it's set up (<see cref="BakeryAppManager.pinned"/>): clicks in it play, but can't leave it
        /// (another page, the title dismissed, a dialog closed). Set it before the phone is added to a panel.
        /// </summary>
        public bool pinScreen { get; set; }

        public PhoneFrame(GameScreen screen, VisualTreeAsset appTree, BakeryState state, string index = null)
        {
            this.screen = screen;
            name = "phone-" + screen.id;
            AddToClassList(BagelExpressAllUIWindow.phoneUssClassName);

            slot = new VisualElement { name = "slot", pickingMode = PickingMode.Ignore };
            slot.AddToClassList(BagelExpressAllUIWindow.slotUssClassName);
            Add(slot);
            slot.Add(Decoration(BagelExpressAllUIWindow.glowUssClassName));

            device = new VisualElement { name = "device" };
            device.AddToClassList(BagelExpressAllUIWindow.deviceUssClassName);
            slot.Add(device);

            screenRoot = appTree.Instantiate();
            screenRoot.name = "screen";
            screenRoot.AddToClassList(BagelExpressAllUIWindow.screenUssClassName);
            device.Add(screenRoot);

            app = screenRoot.Q<BakeryAppManager>();
            if (app != null)
            {
                app.startPage = screen.page;
                app.showTitle = screen.showTitle;
                app.SetState(state);
            }

            var notch = Decoration(BagelExpressAllUIWindow.notchUssClassName);
            notch.Add(Decoration(BagelExpressAllUIWindow.cameraUssClassName));
            device.Add(notch);

            hotspotLayer = new VisualElement { name = "hotspots", pickingMode = PickingMode.Ignore };
            hotspotLayer.AddToClassList(hotspotLayerUssClassName);
            slot.Add(hotspotLayer);

            var caption = new VisualElement { name = "caption", pickingMode = PickingMode.Ignore };
            caption.AddToClassList(BagelExpressAllUIWindow.captionUssClassName);
            if (index != null)
                caption.Add(CaptionLabel(index, "__index"));
            caption.Add(CaptionLabel(screen.title, "__title"));
            caption.Add(CaptionLabel("·", "__dot"));
            caption.Add(CaptionLabel(screen.japaneseTitle, "__jp"));
            Add(caption);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                schedule.Execute(() =>
                {
                    if (app == null)
                        return;
                    app.pinned = false; // the set-up opens what the screen shows
                    screen.SetUp(app);
                    app.pinned = pinScreen;
                }).ExecuteLater(400);
                m_Tracker ??= schedule.Execute(UpdateHotspots).Every(200);
                m_Tracker.Resume();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => m_Tracker?.Pause());
            SetZoom(m_Zoom);
        }

        static VisualElement Decoration(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        static Label CaptionLabel(string text, string elementSuffix)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(BagelExpressAllUIWindow.phoneUssClassName + elementSuffix);
            return label;
        }

        public float zoom => m_Zoom;

        public void SetZoom(float value)
        {
            m_Zoom = value;
            slot.style.width = (k_ScreenWidth + 2f * k_Bezel) * value;
            slot.style.height = (k_ScreenHeight + 2f * k_Bezel) * value;
            style.width = (k_ScreenWidth + 2f * k_Bezel) * value; // long captions wrap instead of widening the phone
            device.style.scale = new Scale(new Vector2(value, value));
        }

        // ------------------------------------------------------------------ hotspots

        public Hotspot AddHotspot(string selector, string key, string number, string tooltip, Color color, Action<Hotspot> clicked)
        {
            var spot = new Hotspot { selector = selector, key = key, number = number, tooltip = tooltip, color = color };
            spot.outline = new VisualElement { pickingMode = PickingMode.Ignore };
            spot.outline.AddToClassList(hotspotUssClassName);
            spot.outline.style.borderTopColor = spot.outline.style.borderBottomColor = spot.outline.style.borderLeftColor = spot.outline.style.borderRightColor = color;
            spot.badge = new Label(number) { tooltip = tooltip };
            spot.badge.AddToClassList(hotspotBadgeUssClassName);
            spot.badge.style.backgroundColor = color;
            spot.badge.RegisterCallback<ClickEvent>(evt =>
            {
                clicked?.Invoke(spot);
                evt.StopPropagation();
            });
            spot.badge.RegisterCallback<PointerEnterEvent>(_ => spot.outline.AddToClassList(hotspotHoverUssClassName));
            spot.badge.RegisterCallback<PointerLeaveEvent>(_ => spot.outline.RemoveFromClassList(hotspotHoverUssClassName));
            hotspotLayer.Add(spot.outline);
            hotspotLayer.Add(spot.badge);
            m_Hotspots.Add(spot);
            return spot;
        }

        public void SetHotspotsVisible(bool visible) => hotspotLayer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        /// <summary>Dim the hotspots whose key isn't in <paramref name="keys"/> (null: none dimmed).</summary>
        public void FilterHotspots(ICollection<string> keys)
        {
            foreach (var spot in m_Hotspots)
            {
                var dim = keys != null && !keys.Contains(spot.key);
                spot.outline.EnableInClassList(hotspotDimmedUssClassName, dim);
                spot.badge.EnableInClassList(hotspotDimmedUssClassName, dim);
            }
        }

        /// <summary>Make the hotspots of one key flash for a moment.</summary>
        public void Pulse(string key)
        {
            foreach (var spot in m_Hotspots.Where(s => s.key == key))
            {
                spot.outline.AddToClassList(hotspotPulseUssClassName);
                spot.badge.AddToClassList(hotspotPulseUssClassName);
                var s = spot;
                schedule.Execute(() =>
                {
                    s.outline.RemoveFromClassList(hotspotPulseUssClassName);
                    s.badge.RemoveFromClassList(hotspotPulseUssClassName);
                }).ExecuteLater(1600);
            }
        }

        public VisualElement FindTarget(Hotspot spot) => GameScreen.Find(screenRoot, spot.selector);

        /// <summary>An element's world rect clipped by its ancestors that hide their overflow (scroll views, masks), up to <paramref name="root"/>.</summary>
        public static Rect VisibleWorldRect(VisualElement element, VisualElement root)
        {
            var rect = element.worldBound;
            for (var ancestor = element.hierarchy.parent; ancestor != null && ancestor != root; ancestor = ancestor.hierarchy.parent)
            {
                if (ancestor.resolvedStyle.overflow != Overflow.Hidden)
                    continue;
                var clip = ancestor.worldBound;
                rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, clip.xMin), Mathf.Max(rect.yMin, clip.yMin),
                    Mathf.Max(Mathf.Max(rect.xMin, clip.xMin), Mathf.Min(rect.xMax, clip.xMax)), Mathf.Max(Mathf.Max(rect.yMin, clip.yMin), Mathf.Min(rect.yMax, clip.yMax)));
            }
            return rect;
        }

        void UpdateHotspots()
        {
            if (m_Hotspots.Count == 0 || hotspotLayer.resolvedStyle.display == DisplayStyle.None)
                return;
            var screenRect = slot.WorldToLocal(screenRoot.worldBound);
            var badgeOffsets = new Dictionary<Vector2Int, int>();
            foreach (var spot in m_Hotspots)
            {
                var target = FindTarget(spot);
                if (!GameScreen.IsShowing(target))
                {
                    spot.outline.style.display = DisplayStyle.None;
                    spot.badge.style.display = DisplayStyle.None;
                    continue;
                }
                var rect = slot.WorldToLocal(VisibleWorldRect(target, screenRoot));
                var xMin = Mathf.Max(rect.xMin, screenRect.xMin);
                var yMin = Mathf.Max(rect.yMin, screenRect.yMin);
                var xMax = Mathf.Min(rect.xMax, screenRect.xMax);
                var yMax = Mathf.Min(rect.yMax, screenRect.yMax);
                if (xMax - xMin < 2f || yMax - yMin < 2f)
                {
                    spot.outline.style.display = DisplayStyle.None;
                    spot.badge.style.display = DisplayStyle.None;
                    continue;
                }
                spot.outline.style.display = DisplayStyle.Flex;
                spot.badge.style.display = DisplayStyle.Flex;
                spot.outline.style.left = xMin;
                spot.outline.style.top = yMin;
                spot.outline.style.width = xMax - xMin;
                spot.outline.style.height = yMax - yMin;

                // Badges of spots on the same corner line up instead of covering each other.
                var corner = new Vector2Int(Mathf.RoundToInt(xMin / 6f), Mathf.RoundToInt(yMin / 6f));
                badgeOffsets.TryGetValue(corner, out var count);
                badgeOffsets[corner] = count + 1;
                spot.badge.style.left = Mathf.Clamp(xMin - 6f + count * 22f, screenRect.xMin - 6f, screenRect.xMax - 16f);
                spot.badge.style.top = Mathf.Clamp(yMin - 6f, screenRect.yMin - 6f, screenRect.yMax - 16f);
            }
        }
    }

    /// <summary>
    /// A fixed-height window into a live game screen, framed on some elements (scaled to fit and centered, with a ring
    /// around them), or the whole phone screen when there's nothing to frame. Built on demand and released when it
    /// scrolls far out of view, so a long list can hold many.
    /// </summary>
    public sealed class ScreenCrop : VisualElement
    {
        public static readonly string ussClassName = "bgg-crop";
        public static readonly string stageUssClassName = ussClassName + "__stage";
        public static readonly string ringUssClassName = ussClassName + "__ring";
        public static readonly string ringLabelUssClassName = ussClassName + "__ring-label";
        public static readonly string captionUssClassName = ussClassName + "__caption";
        public static readonly string placeholderUssClassName = ussClassName + "__placeholder";
        const float k_ScreenWidth = 390f;
        const float k_ScreenHeight = 844f;
        const float k_Padding = 28f;

        public readonly GameScreen screen;
        public readonly string[] focus;
        readonly VisualElement m_Stage;
        readonly Label m_Caption;
        readonly Label m_Placeholder;
        readonly List<VisualElement> m_Rings = new();
        TemplateContainer m_ScreenRoot;
        BakeryAppManager m_App;
        IVisualElementScheduledItem m_Framer;
        Action<VisualElement> m_Unthemed;

        public bool isBuilt => m_ScreenRoot != null;

        /// <summary>Whether it zooms in on its focus (the default), or shows the whole screen with rings on the focus.</summary>
        public bool zoomToFocus { get; set; } = true;

        /// <summary>A tag on the first ring, the way the UI Viewport names its selection (null: none).</summary>
        public string ringLabel { get; set; }
        public BakeryAppManager app => m_App;
        public TemplateContainer screenRoot => m_ScreenRoot;

        public ScreenCrop(GameScreen screen, string[] focus, float height)
        {
            this.screen = screen;
            this.focus = focus ?? Array.Empty<string>();
            AddToClassList(ussClassName);
            style.height = height;

            m_Stage = new VisualElement { name = "stage" };
            m_Stage.AddToClassList(stageUssClassName);
            Add(m_Stage);

            m_Placeholder = new Label(screen.title) { pickingMode = PickingMode.Ignore };
            m_Placeholder.AddToClassList(placeholderUssClassName);
            Add(m_Placeholder);

            m_Caption = new Label(screen.title + (this.focus.Length > 0 ? "  ›  " + string.Join("  ", this.focus) : "")) { pickingMode = PickingMode.Ignore };
            m_Caption.AddToClassList(captionUssClassName);
            Add(m_Caption);

            RegisterCallback<GeometryChangedEvent>(_ => Frame());
        }

        /// <summary>Instantiate the live screen (the window's theme is applied through <paramref name="themed"/>).</summary>
        public void Build(VisualTreeAsset appTree, BakeryState state, Action<VisualElement> themed, Action<VisualElement> unthemed)
        {
            if (isBuilt || appTree == null)
                return;
            m_ScreenRoot = appTree.Instantiate();
            m_ScreenRoot.name = "screen";
            m_ScreenRoot.AddToClassList(BagelExpressAllUIWindow.screenUssClassName);
            m_ScreenRoot.style.borderTopLeftRadius = m_ScreenRoot.style.borderTopRightRadius =
                m_ScreenRoot.style.borderBottomLeftRadius = m_ScreenRoot.style.borderBottomRightRadius = 0f;
            themed?.Invoke(m_ScreenRoot);
            m_Unthemed = unthemed;
            m_App = m_ScreenRoot.Q<BakeryAppManager>();
            if (m_App != null)
            {
                m_App.startPage = screen.page;
                m_App.showTitle = screen.showTitle;
                m_App.SetState(state);
            }
            m_Stage.Add(m_ScreenRoot);
            m_Placeholder.style.display = DisplayStyle.None;
            schedule.Execute(() => screen.SetUp(m_App)).ExecuteLater(300);
            m_Framer = schedule.Execute(Frame).Every(250);
            Frame();
        }

        public void Release()
        {
            if (!isBuilt)
                return;
            m_Framer?.Pause();
            m_Framer = null;
            m_Unthemed?.Invoke(m_ScreenRoot);
            // A whole copy of the app: made again when it's shown again, so its resources go back to be reused now.
            Discard.Element(m_ScreenRoot);
            m_ScreenRoot = null;
            m_App = null;
            foreach (var ring in m_Rings)
                ring.RemoveFromHierarchy();
            m_Rings.Clear();
            m_Placeholder.style.display = DisplayStyle.Flex;
        }

        void Frame()
        {
            var width = resolvedStyle.width;
            var height = resolvedStyle.height;
            if (!isBuilt || float.IsNaN(width) || width < 10f)
                return;

            // What to frame, in the screen's own (unscaled) coordinates.
            var targets = new List<VisualElement>();
            foreach (var selector in focus)
            {
                var target = GameScreen.Find(m_ScreenRoot, selector);
                if (GameScreen.IsShowing(target))
                    targets.Add(target);
            }
            float scale;
            Vector2 offset;
            var rects = targets.Select(t => m_ScreenRoot.WorldToLocal(t.worldBound)).ToList();
            if (rects.Count == 0 || !zoomToFocus)
            {
                scale = Mathf.Min(height / k_ScreenHeight, width / k_ScreenWidth);
                offset = new Vector2((width - k_ScreenWidth * scale) * 0.5f, (height - k_ScreenHeight * scale) * 0.5f);
            }
            else
            {
                var union = rects[0];
                foreach (var r in rects.Skip(1))
                    union = Rect.MinMaxRect(Mathf.Min(union.xMin, r.xMin), Mathf.Min(union.yMin, r.yMin), Mathf.Max(union.xMax, r.xMax), Mathf.Max(union.yMax, r.yMax));
                scale = Mathf.Min(width / (union.width + 2f * k_Padding), height / (union.height + 2f * k_Padding));
                scale = Mathf.Clamp(scale, Mathf.Max(height / k_ScreenHeight, 0.3f), 1.15f);
                offset = new Vector2(width * 0.5f - union.center.x * scale, height * 0.5f - union.center.y * scale);
                // Stay on the screen: no empty space past its edges when it's bigger than the crop.
                offset.x = k_ScreenWidth * scale > width ? Mathf.Clamp(offset.x, width - k_ScreenWidth * scale, 0f) : (width - k_ScreenWidth * scale) * 0.5f;
                offset.y = k_ScreenHeight * scale > height ? Mathf.Clamp(offset.y, height - k_ScreenHeight * scale, 0f) : (height - k_ScreenHeight * scale) * 0.5f;
            }
            m_Stage.style.scale = new Scale(new Vector2(scale, scale));
            m_Stage.style.translate = new Translate(offset.x, offset.y);

            while (m_Rings.Count < rects.Count)
            {
                var ring = new VisualElement { pickingMode = PickingMode.Ignore };
                ring.AddToClassList(ringUssClassName);
                if (m_Rings.Count == 0 && ringLabel != null)
                {
                    var tag = new Label(ringLabel) { pickingMode = PickingMode.Ignore };
                    tag.AddToClassList(ringLabelUssClassName);
                    ring.Add(tag);
                }
                Insert(IndexOf(m_Caption), ring);
                m_Rings.Add(ring);
            }
            for (int i = 0; i < m_Rings.Count; i++)
            {
                var ring = m_Rings[i];
                if (i >= rects.Count)
                {
                    ring.style.display = DisplayStyle.None;
                    continue;
                }
                var r = rects[i];
                ring.style.display = DisplayStyle.Flex;
                ring.style.left = offset.x + r.x * scale - 4f;
                ring.style.top = offset.y + r.y * scale - 4f;
                ring.style.width = r.width * scale + 8f;
                ring.style.height = r.height * scale + 8f;
            }
        }
    }
}
