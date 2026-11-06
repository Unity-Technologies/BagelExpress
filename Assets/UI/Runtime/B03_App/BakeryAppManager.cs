using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    public enum AppPage
    {
        Home,
        Shops,
        LuckyBag,
        Recipes,
        Settings
    }

    /// <summary>
    /// Root of the app. Pure UI logic: owns the shared BakeryState (as the data
    /// source every binding below inherits), page navigation (nav bar and swipes),
    /// the idle tick and the overlay services (toasts, coin fly). No GameObject
    /// needed, so the whole app runs in UI Builder, in the Bagel UI window and in tests.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class BakeryAppManager : VisualElement
    {
        public static readonly string ussClassName = "be-app";
        public static readonly string pageUssClassName = "be-page";
        public static readonly UniqueStyleString pageHiddenLeftUssClassName = new("be-page--hidden-left");
        public static readonly UniqueStyleString pageHiddenRightUssClassName = new("be-page--hidden-right");
        public static readonly UniqueStyleString pageGoneUssClassName = new("be-page--gone");
        public static readonly UniqueStyleString navItemSelectedUssClassName = new("be-nav__item--selected");
        /// <summary>For a moment after a tap: the icon squishes.</summary>
        public static readonly UniqueStyleString navItemPressedUssClassName = new("be-nav__item--pressed");
        /// <summary>While a tap's clip plays: the icon wobbles, the bag shakes.</summary>
        public static readonly UniqueStyleString navItemTappedUssClassName = new("be-nav__item--tapped");
        public static readonly UniqueStyleString titleGoneUssClassName = new("be-title-screen--gone");
        public static readonly UniqueStyleString titleLeavingUssClassName = new("be-title-screen--leaving");
        /// <summary>Perf A/B: the panes inside the pages get blurred filter: drop-shadow()s (BagelFilterShadowsAB.uss).</summary>
        public static readonly UniqueStyleString filterShadowsUssClassName = new("be-filter-shadows");
        public static readonly UniqueStyleString boxShadowsUssClassName = new("be-box-shadows");
        static readonly UniqueStyleString s_FlyIconUssClassName = new("be-fly-icon");
        static readonly UniqueStyleString s_FlyIconArrivedUssClassName = new("be-fly-icon--arrived");
        /// <summary>A scrolling page's glass over its header: it stays put while its page slides.</summary>
        public static readonly string pageChromeUssClassName = "be-page-chrome";

        static readonly string[] s_PageNames = { "home-page", "shops-page", "lucky-bag-page", "recipes-page", "settings-page" };
        static readonly string[] s_NavNames = { "nav-home", "nav-shops", "nav-lucky-bag", "nav-recipes", "nav-settings" };
        // Each currency icon that lands in its pill plays a step higher (C major pentatonic).
        static readonly float[] s_LandSemitones = { 0f, 2f, 4f, 7f, 9f, 12f, 14f, 16f, 19f, 21f };

        const long k_TickIntervalMs = 100;
        const long k_TitleLeaveMs = 500;
        const long k_PageTransitionMs = 380;
        const long k_FlyStartMs = 30;
        const long k_FlyDurationMs = 600;    // .be-fly-icon transition
        const long k_FlyStaggerMs = 60;
        const long k_FrameMs = 16;
        const long k_NavPressMs = 120;
        const long k_NavTapMs = 650;          // NavWobble and BagShake are 0.6 s

        // Swipes: the share of the width a drag takes to fully swap the pages, the share past which a release
        // turns the page, the flick speed (points per second) that turns it anyway, and the hidden pages' offset
        // (as in .be-page--hidden-left/right), so a drag runs along the same path as the slide transition.
        const float k_SwipeFullDrag = 0.5f;
        const float k_SwipeTurnAt = 0.25f;
        const float k_SwipeFlickSpeed = 300f;
        const float k_SwipeFlickMinDrag = 16f;
        const float k_HiddenPageOffsetPercent = 35f;
        const float k_SwipeEdgeResistance = 0.25f;

        BakeryState m_State;
        BakeryState m_OwnedState;
        AppPage m_CurrentPage;
        bool m_ShowTitle;
        bool m_Bound;
        IVisualElementScheduledItem m_TickItem;
        PageSwipe m_Swipe;
        int m_SwipeNeighbor = -1;

        VisualElement m_TitleScreen;
        VisualElement m_Hud;
        VisualElement m_PagesContainer;
        VisualElement m_Nav;
        VisualElement m_NavGlass;
        VisualElement m_OverlayLayer;
        float m_HudPaddingTop = -1f;
        float m_NavHeight;
        float m_NavPaddingBottom;
        ToastStack m_ToastStack;
        TutorialOverlay m_Tutorial;
        readonly VisualElement[] m_Pages = new VisualElement[s_PageNames.Length];
        readonly Button[] m_NavButtons = new Button[s_NavNames.Length];
        readonly int[] m_NavTapTokens = new int[s_NavNames.Length];

        public VisualElement titleScreen => m_TitleScreen ??= this.Q<VisualElement>("title-screen");
        public VisualElement hud => m_Hud ??= this.Q<VisualElement>("hud");
        public VisualElement pagesContainer => m_PagesContainer ??= this.Q<VisualElement>("pages");
        public VisualElement nav => m_Nav ??= this.Q<VisualElement>("nav");
        /// <summary>The nav's own frosted bar, on every page.</summary>
        public VisualElement navGlass => m_NavGlass ??= this.Q<VisualElement>("nav-bar");
        public VisualElement overlayLayer => m_OverlayLayer ??= this.Q<VisualElement>("overlay-layer");
        public ToastStack toastStack => m_ToastStack ??= this.Q<ToastStack>();
        public TutorialOverlay tutorial => m_Tutorial ??= this.Q<TutorialOverlay>();

        public VisualElement GetPage(AppPage page) => m_Pages[(int)page] ??= this.Q<VisualElement>(s_PageNames[(int)page]);
        public Button GetNavButton(AppPage page) => m_NavButtons[(int)page] ??= this.Q<Button>(s_NavNames[(int)page]);

        /// <summary>Template state cloned when nobody provides one (UI Builder, editor windows).</summary>
        [UxmlAttribute]
        public BakeryState startState { get; set; }

        [UxmlAttribute]
        public AppPage startPage { get; set; } = AppPage.Home;

        [UxmlAttribute]
        public bool showTitle
        {
            get => m_ShowTitle;
            set
            {
                m_ShowTitle = value;
                titleScreen?.EnableInClassList(titleGoneUssClassName, !value);
                titleScreen?.RemoveFromClassList(titleLeavingUssClassName);
            }
        }

        public BakeryState state => m_State;
        public AppPage currentPage => m_CurrentPage;

        /// <summary>
        /// The screen stays as it is: the player can't change the page, dismiss the title, or open or close a dialog,
        /// the recipe sheet, the Lucky Bag's results or a tutorial tip. Everything else still plays (taps, upgrades,
        /// scrolling, the season, the language), and code can still change the screen. The editor windows that show
        /// one screen of the game pin it once it's set up (Bagel UI, Bagel Features), so a click doesn't leave it.
        /// </summary>
        public bool pinned { get; set; }

        /// <summary>Whether <paramref name="element"/> is in a pinned app, whose screen the player can't change.</summary>
        public static bool IsPinned(VisualElement element)
        {
            for (var e = element; e != null; e = e.hierarchy.parent)
                if (e is BakeryAppManager app)
                    return app.pinned;
            return false;
        }

        public event Action<BakeryState> stateChanged;
        public event Action<AppPage> pageChanged;
        public event Action titleDismissed;
        /// <summary>Settings › Replay tutorial. The TutorialDriver listens and starts again from the first step.</summary>
        public event Action tutorialRequested;

        static bool s_FilterShadows;
        static bool s_BoxShadows;

        /// <summary>
        /// Perf A/B switch: true gives the panes inside the pages big, blurred filter: drop-shadow()s (an offscreen
        /// pass and a blur per pane, every frame) on every app. Settings › Performance › Blurred drop-shadows on panes,
        /// or F in Play mode. Turning it on turns <see cref="boxShadows"/> off.
        /// </summary>
        public static bool filterShadows
        {
            get => s_FilterShadows;
            set
            {
                if (s_FilterShadows == value)
                    return;
                s_FilterShadows = value;
                if (value)
                    boxShadows = false;
                filterShadowsChanged?.Invoke(value);
            }
        }

        public static event Action<bool> filterShadowsChanged;

        /// <summary>
        /// The A/B's third state: the same blurred shadows as <see cref="filterShadows"/>, as box-shadow, which is
        /// geometry like a border, with no offscreen pass. Settings › Performance › Blurred box-shadows on panes. Turning
        /// it on turns <see cref="filterShadows"/> off.
        /// </summary>
        public static bool boxShadows
        {
            get => s_BoxShadows;
            set
            {
                if (s_BoxShadows == value)
                    return;
                s_BoxShadows = value;
                if (value)
                    filterShadows = false;
                boxShadowsChanged?.Invoke(value);
            }
        }

        public static event Action<bool> boxShadowsChanged;

        /// <summary>F in Play mode: no shadows → drop-shadow() → box-shadow → no shadows.</summary>
        public static void CycleShadows()
        {
            if (s_FilterShadows)
                boxShadows = true;
            else if (s_BoxShadows)
                boxShadows = false;
            else
                filterShadows = true;
        }

        static bool s_NoAnimations;
        static bool s_NoFilters;
        static bool s_NoBlur;

        /// <summary>
        /// Settings › Performance › Disable all animations: every app adds <see cref="noAnimationsStyleSheet"/> to its
        /// root (BagelNoAnimations.uss), and takes it off again. Nothing else changes.
        /// </summary>
        public static bool noAnimations
        {
            get => s_NoAnimations;
            set
            {
                if (s_NoAnimations == value)
                    return;
                s_NoAnimations = value;
                performanceStyleSheetsChanged?.Invoke();
            }
        }

        /// <summary>Settings › Performance › Disable all filters: every app adds <see cref="noFiltersStyleSheet"/> to its root.</summary>
        public static bool noFilters
        {
            get => s_NoFilters;
            set
            {
                if (s_NoFilters == value)
                    return;
                s_NoFilters = value;
                performanceStyleSheetsChanged?.Invoke();
            }
        }

        /// <summary>
        /// Settings › Performance › Disable glass: every app adds <see cref="noBlurStyleSheet"/> to its root. A phone
        /// can need it: a full-screen blur cost up to 200 ms a frame on the ones measured.
        /// </summary>
        public static bool noBlur
        {
            get => s_NoBlur;
            set
            {
                if (s_NoBlur == value)
                    return;
                s_NoBlur = value;
                performanceStyleSheetsChanged?.Invoke();
            }
        }

        /// <summary>Raised when <see cref="noAnimations"/>, <see cref="noFilters"/> or <see cref="noBlur"/> changes.</summary>
        public static event Action performanceStyleSheetsChanged;

        /// <summary>The style sheet that stops every animation (BagelNoAnimations.uss).</summary>
        [UxmlAttribute]
        public StyleSheet noAnimationsStyleSheet { get; set; }

        /// <summary>The style sheet that turns every filter off (BagelNoFilters.uss).</summary>
        [UxmlAttribute]
        public StyleSheet noFiltersStyleSheet { get; set; }

        /// <summary>The style sheet that stops the glass and the backdrops blurring (BagelNoBlur.uss).</summary>
        [UxmlAttribute]
        public StyleSheet noBlurStyleSheet { get; set; }

        // Enter Play Mode Options skip the domain reload in this project: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_FilterShadows = false;
            s_BoxShadows = false;
            s_NoAnimations = false;
            s_NoFilters = false;
            s_NoBlur = false;
        }

        public BakeryAppManager()
        {
            AddToClassList(ussClassName);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (m_State == null)
            {
                // Nobody gave us a state (UI Builder, an editor window): play with our own copy.
                if (dataSource is BakeryState provided)
                {
                    SetState(provided);
                }
                else
                {
                    var copy = BakeryState.CreateRuntimeCopy(startState);
                    SetState(copy);
                    m_OwnedState = copy;
                }
            }

            if (!m_Bound)
            {
                m_Bound = true;
                for (int i = 0; i < s_NavNames.Length; i++)
                {
                    var page = (AppPage)i;
                    GetNavButton(page)?.RegisterCallback<ClickEvent>(_ =>
                    {
                        PlayNavTap(page);
                        if (!pinned)
                            GoTo(page);
                    });
                }
                titleScreen?.RegisterCallback<PointerUpEvent>(_ =>
                {
                    if (!pinned)
                        DismissTitle();
                });
                BagelSound.ListenForPresses(this);
                m_Swipe = new PageSwipe(() => !pinned && !m_ShowTitle && !IsOverlayOpen());
                m_Swipe.dragged += OnSwipeDragged;
                m_Swipe.released += OnSwipeReleased;
                this.AddManipulator(m_Swipe);
                var ticketsPill = GetCurrencyPill(CurrencyKind.Tickets);
                if (ticketsPill != null)
                    ticketsPill.plusClicked += () =>
                    {
                        if (!pinned)
                            ConfirmBuyTicket();
                    };
                hud?.Q("hud-player")?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!pinned)
                        ShowPlayerStats();
                });
            }

            // Attached again with the state it had: listen to it again.
            if (m_State != null)
            {
                m_State.milestoneReached -= OnMilestoneReached;
                m_State.milestoneReached += OnMilestoneReached;
            }

            showTitle = m_ShowTitle;
            m_CurrentPage = startPage;
            GoTo(startPage, animate: false);

            m_TickItem ??= schedule.Execute(Tick).Every(k_TickIntervalMs);
            m_TickItem.Resume();
            BagelLocalization.localeChanged += OnLocaleChanged;
            BagelLocalization.ApplyLanguageClass(this);
            filterShadowsChanged += OnFilterShadowsChanged;
            OnFilterShadowsChanged(s_FilterShadows);
            boxShadowsChanged += OnBoxShadowsChanged;
            OnBoxShadowsChanged(s_BoxShadows);
            performanceStyleSheetsChanged += ApplyPerformanceStyleSheets;
            ApplyPerformanceStyleSheets();
            EditorPreviewHooks.Attached(this);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_TickItem?.Pause();
            BagelLocalization.localeChanged -= OnLocaleChanged;
            filterShadowsChanged -= OnFilterShadowsChanged;
            boxShadowsChanged -= OnBoxShadowsChanged;
            performanceStyleSheetsChanged -= ApplyPerformanceStyleSheets;

            // A state that outlives the app (an editor window's, shared by its screens) mustn't hold on to it.
            if (m_State != null)
                m_State.milestoneReached -= OnMilestoneReached;
            if (m_OwnedState != null && m_State == m_OwnedState)
                SetState(null);
        }

        void OnLocaleChanged()
        {
            // Shop and recipe names are localized computed properties: ask bindings to re-read them.
            m_State?.NotifyAll();
            BagelLocalization.ApplyLanguageClass(this);
        }

        void OnFilterShadowsChanged(bool enabled) => EnableInClassList(filterShadowsUssClassName, enabled);

        void OnBoxShadowsChanged(bool enabled) => EnableInClassList(boxShadowsUssClassName, enabled);

        void ApplyPerformanceStyleSheets()
        {
            UseStyleSheet(noAnimationsStyleSheet, s_NoAnimations);
            UseStyleSheet(noFiltersStyleSheet, s_NoFilters);
            UseStyleSheet(noBlurStyleSheet, s_NoBlur);
        }

        /// <summary>Adds or removes one of the performance style sheets on the app root.</summary>
        void UseStyleSheet(StyleSheet sheet, bool use)
        {
            if (sheet == null || styleSheets.Contains(sheet) == use)
                return;
            if (use)
                styleSheets.Add(sheet);
            else
                styleSheets.Remove(sheet);
        }

        void Tick(TimerState timer)
        {
            m_State?.Tick(timer.deltaTime / 1000f);
        }

        /// <summary>
        /// Keeps the HUD, pages, toasts and nav bar clear of a notch, Dynamic Island or home indicator.
        /// <paramref name="top"/> and <paramref name="bottom"/> are safe-area insets in panel units. The
        /// layout already reserves some space (the HUD's top padding, the nav's bottom padding), so only the
        /// excess is added, and nothing is set inline when there is none. The backdrop stays full-bleed. The scrolling
        /// pages need nothing: their glass and scroll areas are placed from the pages, and reach past the screen's edges.
        /// </summary>
        /// <returns>False if the layout isn't ready yet; call again after the next layout.</returns>
        public bool ApplySafeArea(float top, float bottom)
        {
            if (hud == null || nav == null || pagesContainer == null)
                return false;
            if (m_HudPaddingTop < 0f)
            {
                // What the USS reserves, read once before anything is set inline.
                var navStyle = nav.resolvedStyle;
                if (float.IsNaN(navStyle.height) || navStyle.height <= 0f || float.IsNaN(hud.resolvedStyle.paddingTop))
                    return false;
                m_HudPaddingTop = hud.resolvedStyle.paddingTop;
                m_NavHeight = navStyle.height;
                m_NavPaddingBottom = navStyle.paddingBottom;
            }

            var extraTop = Mathf.Max(0f, top - m_HudPaddingTop);
            var extraBottom = Mathf.Max(0f, bottom - m_NavPaddingBottom);

            // The HUD grows its padding rather than moving, so its gradient still starts at the screen edge.
            hud.style.paddingTop = extraTop > 0f ? new StyleLength(m_HudPaddingTop + extraTop) : new StyleLength(StyleKeyword.Null);
            pagesContainer.style.top = Inset(extraTop);
            pagesContainer.style.bottom = Inset(extraBottom);
            if (toastStack != null)
                toastStack.style.translate = extraTop > 0f ? new StyleTranslate(new Translate(0, extraTop)) : new StyleTranslate(StyleKeyword.Null);
            nav.style.height = extraBottom > 0f ? new StyleLength(m_NavHeight + extraBottom) : new StyleLength(StyleKeyword.Null);
            nav.style.paddingBottom = extraBottom > 0f ? new StyleLength(m_NavPaddingBottom + extraBottom) : new StyleLength(StyleKeyword.Null);
            var titleFooter = this.Q("title-footer");
            if (titleFooter != null)
                titleFooter.style.translate = extraBottom > 0f ? new StyleTranslate(new Translate(0, -extraBottom)) : new StyleTranslate(StyleKeyword.Null);
            return true;
        }

        static StyleLength Inset(float value) => value > 0f ? new StyleLength(value) : new StyleLength(StyleKeyword.Null);

        /// <summary>Swap the shared game state (e.g. the Bagel UI window shares one state between phones).</summary>
        public void SetState(BakeryState newState)
        {
            if (m_State == newState)
                return;

            if (m_State != null)
                m_State.milestoneReached -= OnMilestoneReached;

            if (m_OwnedState != null && m_OwnedState != newState)
            {
                BakeryState.DestroyRuntimeCopy(m_OwnedState);
                m_OwnedState = null;
            }

            m_State = newState;
            dataSource = newState;

            if (m_State != null)
                m_State.milestoneReached += OnMilestoneReached;

            stateChanged?.Invoke(newState);
        }

        public void GoTo(AppPage page) => GoTo(page, animate: true);

        /// <summary>
        /// A nav item's tap, whether the page changes or not (pinned): the icon squishes and wobbles, the bag shakes. The
        /// clip plays when --tapped starts to apply, so it's taken off and put back a frame later, and off once it's done.
        /// </summary>
        void PlayNavTap(AppPage page)
        {
            var item = GetNavButton(page);
            if (item == null)
                return;
            var token = ++m_NavTapTokens[(int)page];
            item.RemoveFromClassList(navItemTappedUssClassName);
            item.AddToClassList(navItemPressedUssClassName);
            item.schedule.Execute(() =>
            {
                if (token == m_NavTapTokens[(int)page])
                    item.AddToClassList(navItemTappedUssClassName);
            }).ExecuteLater(k_FrameMs);
            item.schedule.Execute(() =>
            {
                if (token == m_NavTapTokens[(int)page])
                    item.RemoveFromClassList(navItemPressedUssClassName);
            }).ExecuteLater(k_NavPressMs);
            item.schedule.Execute(() =>
            {
                if (token == m_NavTapTokens[(int)page])
                    item.RemoveFromClassList(navItemTappedUssClassName);
            }).ExecuteLater(k_NavTapMs);
        }

        /// <summary>The page shown on the other side of the current swipe, or null.</summary>
        internal VisualElement swipeNeighbor => m_SwipeNeighbor >= 0 ? GetPage((AppPage)m_SwipeNeighbor) : null;

        /// <summary>Jump to a page without the slide (e.g. restoring the page after a live reload).</summary>
        public void ShowPage(AppPage page) => GoTo(page, animate: false);

        /// <summary>
        /// Pages slide with USS transitions. A page is first parked on the correct side
        /// (hidden-left / hidden-right), made displayable, then un-hidden next frame so the
        /// transition plays. The page we leave slides out and becomes display:none after.
        /// </summary>
        void GoTo(AppPage page, bool animate, bool fromSwipe = false)
        {
            var previous = m_CurrentPage;
            m_CurrentPage = page;

            for (int i = 0; i < s_PageNames.Length; i++)
            {
                var element = GetPage((AppPage)i);
                GetNavButton((AppPage)i)?.EnableInClassList(navItemSelectedUssClassName, i == (int)page);
                if (element == null)
                    continue;

                var isTarget = i == (int)page;
                if (!animate)
                {
                    element.EnableInClassList(pageHiddenLeftUssClassName, i < (int)page);
                    element.EnableInClassList(pageHiddenRightUssClassName, i > (int)page);
                    element.EnableInClassList(pageGoneUssClassName, !isTarget);
                    continue;
                }

                if (isTarget && fromSwipe)
                {
                    // Already on screen, part-way in: its transition runs from where the finger left it.
                    element.RemoveFromClassList(pageHiddenLeftUssClassName);
                    element.RemoveFromClassList(pageHiddenRightUssClassName);
                    element.RemoveFromClassList(pageGoneUssClassName);
                }
                else if (isTarget)
                {
                    element.EnableInClassList(pageHiddenLeftUssClassName, i < (int)previous);
                    element.EnableInClassList(pageHiddenRightUssClassName, i > (int)previous);
                    element.RemoveFromClassList(pageGoneUssClassName);
                    element.schedule.Execute(() =>
                    {
                        element.RemoveFromClassList(pageHiddenLeftUssClassName);
                        element.RemoveFromClassList(pageHiddenRightUssClassName);
                    }).ExecuteLater(16);
                }
                else
                {
                    element.EnableInClassList(pageHiddenLeftUssClassName, i < (int)page);
                    element.EnableInClassList(pageHiddenRightUssClassName, i > (int)page);
                    var index = i;
                    element.schedule.Execute(() =>
                    {
                        if ((int)m_CurrentPage != index && m_SwipeNeighbor != index)
                            element.AddToClassList(pageGoneUssClassName);
                    }).ExecuteLater(k_PageTransitionMs);
                }
            }

            if (page == AppPage.Recipes)
                m_State?.MarkRecipesSeen();

            if (animate && previous != page)
                pageChanged?.Invoke(page);
        }

        // ---------------------------------------------------------------- Swipes: the pages follow the finger

        /// <summary>
        /// A horizontal drag: the current page moves toward its hidden side and fades, and the neighbor in the drag's
        /// direction comes in from its side and fades in, along the same path as the slide transition. Inline
        /// styles (with the transition switched off) follow the finger; at the first and last page the current
        /// page only gives a little.
        /// </summary>
        void OnSwipeDragged(float offset)
        {
            var width = layout.width;
            var current = GetPage(m_CurrentPage);
            if (current == null || float.IsNaN(width) || width <= 0f)
                return;

            var direction = offset < 0f ? 1 : -1; // finger to the left: the next page
            var neighborIndex = (int)m_CurrentPage + direction;
            if (neighborIndex != m_SwipeNeighbor)
                EndSwipeNeighbor();

            if (neighborIndex < 0 || neighborIndex >= s_PageNames.Length)
            {
                SetSwipeStyle(current, Length.Pixels(offset * k_SwipeEdgeResistance), 1f);
                return;
            }

            var neighbor = GetPage((AppPage)neighborIndex);
            if (neighbor == null)
                return;
            if (m_SwipeNeighbor != neighborIndex)
            {
                m_SwipeNeighbor = neighborIndex;
                neighbor.RemoveFromClassList(pageGoneUssClassName);
            }

            var progress = Mathf.Clamp01(Mathf.Abs(offset) / (width * k_SwipeFullDrag));
            SetSwipeStyle(current, Length.Percent(-direction * k_HiddenPageOffsetPercent * progress), 1f - progress);
            SetSwipeStyle(neighbor, Length.Percent(direction * k_HiddenPageOffsetPercent * (1f - progress)), progress);
        }

        /// <summary>
        /// The finger lifted: past a quarter of the width, or flicked that way, the neighbor becomes the current
        /// page; otherwise both settle back. Either way the inline styles go and the USS transitions finish the move
        /// from where the finger left the pages.
        /// </summary>
        void OnSwipeReleased(float offset, float velocity)
        {
            var width = layout.width;
            var neighborIndex = m_SwipeNeighbor;
            var turn = neighborIndex >= 0 && !float.IsNaN(width) &&
                (Mathf.Abs(offset) >= width * k_SwipeTurnAt ||
                 (Mathf.Abs(offset) >= k_SwipeFlickMinDrag && Mathf.Abs(velocity) >= k_SwipeFlickSpeed && Mathf.Sign(velocity) == Mathf.Sign(offset)));

            ClearSwipeStyle(GetPage(m_CurrentPage));
            if (turn)
            {
                BagelSound.Play(this, "swipe");
                ClearSwipeStyle(GetPage((AppPage)neighborIndex));
                m_SwipeNeighbor = -1;
                GoTo((AppPage)neighborIndex, animate: true, fromSwipe: true);
            }
            else
            {
                BagelSound.Play(this, "settle");
                EndSwipeNeighbor();
            }
        }

        /// <summary>The neighbor slides back to its hidden side and is put away once its transition is over.</summary>
        void EndSwipeNeighbor()
        {
            if (m_SwipeNeighbor < 0)
                return;
            var index = m_SwipeNeighbor;
            var neighbor = GetPage((AppPage)index);
            m_SwipeNeighbor = -1;
            if (neighbor == null)
                return;
            ClearSwipeStyle(neighbor);
            neighbor.schedule.Execute(() =>
            {
                if ((int)m_CurrentPage != index && m_SwipeNeighbor != index)
                    neighbor.AddToClassList(pageGoneUssClassName);
            }).ExecuteLater(k_PageTransitionMs);
        }

        static readonly StyleList<TimeValue> s_NoTransition = new(new List<TimeValue> { new TimeValue(0f) });

        static void SetSwipeStyle(VisualElement page, Length x, float opacity)
        {
            page.style.transitionDuration = s_NoTransition;
            page.style.translate = new Translate(x, 0);
            page.style.opacity = opacity;
            // A page's glass stays put (.be-page-chrome): it moves back as far as the page moves.
            var back = new Translate(new Length(-x.value, x.unit), 0);
            page.Query(className: pageChromeUssClassName).ForEach(chrome =>
            {
                chrome.style.transitionDuration = s_NoTransition;
                chrome.style.translate = back;
            });
        }

        static void ClearSwipeStyle(VisualElement page)
        {
            if (page == null)
                return;
            page.style.transitionDuration = StyleKeyword.Null;
            page.style.translate = StyleKeyword.Null;
            page.style.opacity = StyleKeyword.Null;
            page.Query(className: pageChromeUssClassName).ForEach(chrome =>
            {
                chrome.style.transitionDuration = StyleKeyword.Null;
                chrome.style.translate = StyleKeyword.Null;
            });
        }

        /// <summary>
        /// Closes whatever is open and shows <paramref name="page"/>, with the title screen over it or not, at once. Editor
        /// previews use it (the demo toolbar's screen): <see cref="startPage"/> and <see cref="showTitle"/>, what the UXML
        /// asks for, stay as they are.
        /// </summary>
        public void ShowScreen(AppPage page, bool title)
        {
            for (int i = 0; i < 8 && CloseTopOverlay(close: true); i++) { }
            titleScreen?.EnableInClassList(titleGoneUssClassName, !title);
            titleScreen?.RemoveFromClassList(titleLeavingUssClassName);
            GoTo(page, animate: false);
        }

        public void DismissTitle()
        {
            if (!m_ShowTitle || titleScreen == null)
                return;

            m_ShowTitle = false;
            BagelSound.Play(titleScreen, "start");
            titleScreen.AddToClassList(titleLeavingUssClassName);
            titleScreen.schedule.Execute(() =>
            {
                titleScreen.AddToClassList(titleGoneUssClassName);
                titleScreen.RemoveFromClassList(titleLeavingUssClassName);
            }).ExecuteLater(k_TitleLeaveMs);
            titleDismissed?.Invoke();
        }

        /// <summary>Back button / Escape: close the top-most overlay (the tutorial is skipped), else go Home.</summary>
        public void Back()
        {
            if (pinned)
                return;
            if (m_ShowTitle)
            {
                DismissTitle();
                return;
            }

            if (!CloseTopOverlay(close: true) && m_CurrentPage != AppPage.Home)
                GoTo(AppPage.Home);
        }

        /// <summary>A dialog, sheet or tutorial tip is on screen: swipes don't change pages.</summary>
        public bool IsOverlayOpen() => CloseTopOverlay(close: false);

        /// <summary>Finds the top-most open overlay; with <paramref name="close"/>, closes it.</summary>
        bool CloseTopOverlay(bool close)
        {
            var found = false;
            this.Query<ConfirmDialog>().ForEach(d =>
            {
                if (!found && d.isOpen) { found = true; if (close) d.Close(); }
            });
            this.Query<LuckyBagPaneManager>().ForEach(p =>
            {
                if (!found && p.ClassListContains(LuckyBagPaneManager.resultsOpenUssClassName)) { found = true; if (close) p.CloseResults(); }
            });
            this.Query<RecipesPaneManager>().ForEach(p =>
            {
                if (!found && p.ClassListContains(RecipesPaneManager.sheetOpenUssClassName)) { found = true; if (close) p.CloseSheet(); }
            });
            this.Query<LevelUpDialog>().ForEach(d =>
            {
                if (!found && d.isOpen) { found = true; if (close) d.Close(); }
            });
            if (!found && tutorial != null && tutorial.isActive && !tutorial.isPaused)
            {
                found = true;
                if (close)
                    tutorial.Skip();
            }
            return found;
        }

        /// <summary>Go Home and play the tutorial again from the first step (Settings › Replay tutorial).</summary>
        public void RestartTutorial()
        {
            if (m_ShowTitle)
                DismissTitle();
            GoTo(AppPage.Home);
            tutorialRequested?.Invoke();
        }

        // ---------------------------------------------------------------- Overlay services

        public void ShowToast(string message) => toastStack?.Show(message);

        void OnMilestoneReached(ShopState shop, int gems)
        {
            // Pinned: the gems are won, but LEVEL UP! doesn't cover the screen.
            if (!pinned)
                this.Q<LevelUpDialog>()?.Show(shop, gems);
        }

        /// <summary>The HUD's player row: Begu-chan's stats, mood, and what makes up the coin rate and the level.</summary>
        public void ShowPlayerStats()
        {
            if (m_State != null && !IsOverlayOpen())
                this.Q<PlayerStatsDialog>()?.Show(m_State);
        }

        /// <summary>Settings › Saved data › Reset progress: after a confirmation, back to the start state, the saved game forgotten, and the tutorial again.</summary>
        public void ConfirmResetProgress()
        {
            this.Q<ConfirmDialog>("confirm-dialog")?.Show(BagelLocalization.Get("confirm.reset-progress.title"),
                BagelLocalization.Get("confirm.reset-progress.message"), BagelLocalization.Get("confirm.reset.yes"), ResetProgress);
        }

        public void ResetProgress()
        {
            if (m_State == null)
                return;
            m_State.ResetTo(startState);
            BagelSave.DeleteGame();
            RestartTutorial();
            ShowToast(BagelLocalization.Get("toast.progress-reset"));
        }

        /// <summary>Settings › Saved data › Reset settings: after a confirmation, every setting back to its default and the saved settings forgotten.</summary>
        public void ConfirmResetSettings()
        {
            this.Q<ConfirmDialog>("confirm-dialog")?.Show(BagelLocalization.Get("confirm.reset-settings.title"),
                BagelLocalization.Get("confirm.reset-settings.message"), BagelLocalization.Get("confirm.reset.yes"), ResetSettings);
        }

        public void ResetSettings()
        {
            var settings = this.Q<SettingsPaneManager>();
            BagelSave.ApplySettings(new BagelSave.Settings(), settings);
            BagelSave.DeleteSettings();
            settings?.RefreshSelection();
            ShowToast(BagelLocalization.Get("toast.settings-reset"));
        }

        /// <summary>Tickets pill "+": confirm, then trade Maple Gems for a Lucky Ticket.</summary>
        public void ConfirmBuyTicket()
        {
            var dialog = this.Q<ConfirmDialog>("confirm-dialog");
            if (dialog == null || m_State == null)
                return;

            var cost = m_State.catalog ? m_State.catalog.ticketGemCost : 60;
            var from = dialog.confirmButton;
            dialog.Show(BagelLocalization.Get("ticket.confirm.title"), BagelLocalization.Get("ticket.confirm.message", cost),
                BagelLocalization.Get("ticket.confirm.yes"), () =>
            {
                if (m_State.TryBuyTicket())
                {
                    FlyCurrency(from, CurrencyKind.Tickets, 3);
                    ShowToast(BagelLocalization.Get("toast.ticket-bought"));
                }
                else
                {
                    BagelSound.Play(this, "fail");
                    ShowToast(BagelLocalization.Get("toast.not-enough-gems"));
                }
            }, CurrencyKind.Tickets);
        }

        /// <summary>Find the HUD pill for a currency (target of coin fly effects).</summary>
        public CurrencyPill GetCurrencyPill(CurrencyKind kind)
        {
            CurrencyPill result = null;
            hud?.Query<CurrencyPill>().ForEach(p => { if (p.kind == kind) result = p; });
            return result;
        }

        /// <summary>
        /// Fly a few currency icons from an element to its HUD pill. The icons live in the
        /// overlay layer (high z-index) and move with USS transitions.
        /// </summary>
        public void FlyCurrency(VisualElement from, CurrencyKind kind, int count = 6)
        {
            var target = GetCurrencyPill(kind);
            if (from == null || target == null || overlayLayer == null || overlayLayer.panel == null)
                return;

            var start = overlayLayer.WorldToLocal(from.worldBound.center);
            var end = overlayLayer.WorldToLocal(target.icon.worldBound.center);

            for (int i = 0; i < count; i++)
            {
                var icon = new CurrencyIcon { kind = kind, pickingMode = PickingMode.Ignore };
                icon.AddToClassList(s_FlyIconUssClassName);
                var jitter = new Vector2(UnityEngine.Random.Range(-28f, 28f), UnityEngine.Random.Range(-20f, 20f));
                icon.style.left = start.x + jitter.x;
                icon.style.top = start.y + jitter.y;
                icon.style.transitionDelay = new List<TimeValue> { new TimeValue(i * k_FlyStaggerMs, TimeUnit.Millisecond) };
                overlayLayer.Add(icon);

                // Next frame: move to the pill; the transition animates it.
                icon.schedule.Execute(() =>
                {
                    icon.AddToClassList(s_FlyIconArrivedUssClassName);
                    icon.style.left = end.x;
                    icon.style.top = end.y;
                }).ExecuteLater(k_FlyStartMs);
                // Each landing plays the pill's --be-sound-land, a step higher than the last.
                var semitones = s_LandSemitones[i % s_LandSemitones.Length];
                icon.schedule.Execute(() => BagelSound.Play(target, "land", semitones)).ExecuteLater(k_FlyStartMs + k_FlyDurationMs + i * k_FlyStaggerMs);
                icon.schedule.Execute(() => Discard.Element(icon)).ExecuteLater(900 + i * k_FlyStaggerMs);
            }
        }
    }
}
