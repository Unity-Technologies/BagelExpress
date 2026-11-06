using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B03_App
{
    public class BakeryAppTests : BakeryUITestFixture
    {
        BakeryAppManager m_App;

        [SetUp]
        public void SetUp()
        {
            m_App = Show(assets.appScreenUxml).Q<BakeryAppManager>();
        }

        [Test]
        public void TappingTheTitleDismissesIt()
        {
            Assert.That(m_App.showTitle, Is.True);

            simulate.Click(m_App.titleScreen);
            simulate.FrameUpdateMs(500); // fade-out

            Assert.That(m_App.showTitle, Is.False);
            Assert.That(m_App.titleScreen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        [TestCase(AppPage.Shops)]
        [TestCase(AppPage.LuckyBag)]
        [TestCase(AppPage.Recipes)]
        [TestCase(AppPage.Settings)]
        public void NavButtonsSwitchPages(AppPage page)
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var home = m_App.GetPage(AppPage.Home);
            var target = m_App.GetPage(page);

            simulate.Click(m_App.GetNavButton(page));
            simulate.FrameUpdateMs(400); // slide transition

            Assert.That(m_App.currentPage, Is.EqualTo(page));
            Assert.That(target.ClassListContains(BakeryAppManager.pageGoneUssClassName), Is.False);
            Assert.That(home.ClassListContains(BakeryAppManager.pageGoneUssClassName), Is.True);
            Assert.That(m_App.GetNavButton(page).ClassListContains(BakeryAppManager.navItemSelectedUssClassName), Is.True);
            Assert.That(m_App.GetNavButton(AppPage.Home).ClassListContains(BakeryAppManager.navItemSelectedUssClassName), Is.False);
        }

        [Test]
        public void SwipingLeftAndRightChangesPages()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;

            simulate.DragAndDrop(center + new Vector2(80, 0), center - new Vector2(80, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops), "swipe left: next page");

            simulate.DragAndDrop(center - new Vector2(80, 0), center + new Vector2(80, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home), "swipe right: previous page");

            simulate.DragAndDrop(center - new Vector2(80, 0), center + new Vector2(80, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home), "Home is the first page");
        }

        [Test]
        public void ADraggedScrollIsNotASwipe()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;

            simulate.DragAndDrop(center + new Vector2(20, 90), center - new Vector2(20, 90));
            simulate.FrameUpdateMs(400);

            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home));
        }

        [Test]
        public void ASwipeThatStartsOnAButtonDoesNotClickIt()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var collect = m_App.Q<HomePaneManager>().collectButton;
            var coins = m_App.state.coins;

            // Starts and ends on COLLECT (it's 320 points wide): without the swipe this would be a click.
            var start = collect.worldBound.center + new Vector2(70, 0);
            simulate.DragAndDrop(start, start - new Vector2(140, 0));
            simulate.FrameUpdateMs(400);

            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops));
            Assert.That(m_App.state.coins, Is.EqualTo(coins), "the register wasn't collected");
            Assert.That(collect.style.scale.keyword, Is.EqualTo(StyleKeyword.Null), "PressSquish let go");
        }

        [Test]
        public void AQuickShortFlickTurnsThePage()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;

            // 50 points in three frames at 30 fps: short of a quarter of the width, but fast.
            simulate.timePerSimulatedFrameMs = 33;
            try
            {
                simulate.MouseDown(center + new Vector2(30, 0));
                simulate.MouseMove(center + new Vector2(5, 0));
                simulate.MouseMove(center - new Vector2(20, 0));
                simulate.MouseUp(center - new Vector2(20, 0));
            }
            finally
            {
                simulate.ResetTimePerSimulatedFrameToDefault();
            }
            simulate.FrameUpdateMs(400);

            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops));
        }

        [Test]
        public void AThumbArcThatStartsUpwardStillSwipes()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;

            simulate.MouseDown(center + new Vector2(100, 0));
            simulate.MouseMove(center + new Vector2(97, -10));
            simulate.MouseMove(center + new Vector2(90, -16));
            simulate.MouseMove(center + new Vector2(70, -14));
            simulate.MouseMove(center + new Vector2(20, -8));
            simulate.MouseMove(center - new Vector2(40, 0));
            simulate.MouseUp(center - new Vector2(40, 0));
            simulate.FrameUpdateMs(400);

            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops));
        }

        [Test]
        public void TheNextPageFadesInWhileDraggingAndASlowShortDragSettlesBack()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;
            var home = m_App.GetPage(AppPage.Home);
            var shops = m_App.GetPage(AppPage.Shops);

            simulate.MouseDown(center + new Vector2(30, 0));
            simulate.MouseMove(center);
            simulate.MouseMove(center - new Vector2(30, 0));
            simulate.FrameUpdate();
            Assert.That(shops.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "the next page is on screen");
            Assert.That(shops.resolvedStyle.opacity, Is.InRange(0.1f, 0.9f), "and part-way faded in");
            Assert.That(home.resolvedStyle.opacity, Is.InRange(0.1f, 0.9f), "the current page part-way out");

            // Held still, then let go: 60 points is short of a quarter of the width and there's no flick.
            simulate.FrameUpdateMs(200);
            simulate.MouseUp(center - new Vector2(30, 0));
            simulate.FrameUpdateMs(500);

            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home));
            Assert.That(home.resolvedStyle.opacity, Is.EqualTo(1f));
            Assert.That(shops.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the next page is put away again");
        }

        [Test]
        public void AnotherPointerDownDuringADragDoesNotStopIt()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;

            // On an iPhone more PointerDowns arrive while one finger drags: the swipe keeps following it.
            simulate.MouseDown(center + new Vector2(80, 0));
            simulate.MouseMove(center + new Vector2(40, 0));
            simulate.MouseDown(center + new Vector2(40, 0));
            simulate.MouseMove(center - new Vector2(40, 0));
            simulate.MouseMove(center - new Vector2(80, 0));
            simulate.MouseUp(center - new Vector2(80, 0));
            simulate.FrameUpdateMs(400);

            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops));
        }

        [Test]
        public void SwipesWaitForTheTutorialTip()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var collect = m_App.Q<HomePaneManager>().collectButton;
            var skipped = false;
            m_App.tutorial.Show(collect, "Tap COLLECT", "TIP 1/3", null, () => skipped = true);
            simulate.FrameUpdate();
            var center = m_App.worldBound.center;

            simulate.DragAndDrop(center + new Vector2(80, 0), center - new Vector2(80, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home), "the tip is modal");

            // The back button skips the tutorial, then swipes work again.
            m_App.Back();
            Assert.That(skipped, Is.True);
            Assert.That(m_App.tutorial.isActive, Is.False);
            simulate.DragAndDrop(center + new Vector2(80, 0), center - new Vector2(80, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops));
        }

        [Test]
        public void TitleAndSettingsShowTheAppVersion()
        {
            // One source of truth: Project Settings › Player › Version.
            Assert.That(m_App.Q<VersionLabel>("title-version").text, Is.EqualTo("v" + UnityEngine.Application.version));
            Assert.That(m_App.Q<VersionLabel>("settings-version").text, Is.EqualTo("v" + UnityEngine.Application.version));
            Assert.That(m_App.Q<VersionLabel>("settings-footer-version").text, Is.EqualTo("v" + UnityEngine.Application.version));
        }

        [Test]
        public void SafeAreaKeepsTheHudAndNavClearOfANotch()
        {
            // The title's credits and version are one pill: it moves up as a whole, so they never overlap.
            var footer = m_App.Q("title-footer");
            var credits = m_App.Q<Label>("title-credits");
            var version = m_App.Q<VersionLabel>("title-version");
            simulate.FrameUpdate();
            var footerBottom = footer.worldBound.yMax;
            Assert.That(m_App.ApplySafeArea(m_App.hud.resolvedStyle.paddingTop, m_App.nav.resolvedStyle.paddingBottom + 16), Is.True);
            simulate.FrameUpdate();
            Assert.That(footer.worldBound.yMax, Is.EqualTo(footerBottom - 16).Within(0.01f));
            Assert.That(credits.worldBound.xMax, Is.LessThanOrEqualTo(version.worldBound.xMin), "the version sits right of the credits");
            Assert.That(credits.worldBound.center.y, Is.EqualTo(version.worldBound.center.y).Within(0.5f), "on one line");
            Assert.That(credits.worldBound.xMin - footer.worldBound.xMin, Is.EqualTo(footer.worldBound.xMax - version.worldBound.xMax).Within(0.5f),
                "the credits at the pill's left end, the version at its right");
            Assert.That(m_App.ApplySafeArea(0, 0), Is.True);

            m_App.showTitle = false;
            simulate.FrameUpdate();
            var currencies = m_App.hud.Q("hud-currencies");
            var hudTop = currencies.worldBound.y;
            var navHeight = m_App.nav.layout.height;
            var reservedTop = m_App.hud.resolvedStyle.paddingTop;        // room the layout already keeps (44)
            var reservedBottom = m_App.nav.resolvedStyle.paddingBottom;  // (18)
            var tolerance = 0.01f;

            // A Dynamic Island phone: 18 more at the top and 16 more at the bottom than the layout reserves.
            Assert.That(m_App.ApplySafeArea(reservedTop + 18, reservedBottom + 16), Is.True);
            simulate.FrameUpdate();
            Assert.That(currencies.worldBound.y, Is.EqualTo(hudTop + 18).Within(tolerance));
            Assert.That(m_App.hud.layout.y, Is.EqualTo(0).Within(tolerance), "the HUD's gradient still starts at the screen edge");
            Assert.That(m_App.pagesContainer.layout.y, Is.EqualTo(18).Within(tolerance));
            Assert.That(m_App.nav.layout.height, Is.EqualTo(navHeight + 16).Within(tolerance));

            // No cutout: nothing stays inline, so the USS layout (and live USS edits) apply again.
            Assert.That(m_App.ApplySafeArea(0, 0), Is.True);
            simulate.FrameUpdate();
            Assert.That(currencies.worldBound.y, Is.EqualTo(hudTop).Within(tolerance));
            Assert.That(m_App.nav.layout.height, Is.EqualTo(navHeight).Within(tolerance));
            Assert.That(m_App.nav.style.height.keyword, Is.EqualTo(StyleKeyword.Null));
        }

        /// <summary>Scrolls Settings so the element sits in the middle of the clear area (not under the glass).</summary>
        void BringIntoWindow(SettingsPaneManager settings, VisualElement element)
        {
            var scroll = settings.Q<ScrollView>("settings-scroll");
            // Where the element is in the content, and where the clear area's middle is in the viewport: neither moves when it scrolls.
            var inContent = scroll.contentContainer.WorldToLocal(element.worldBound.center).y;
            var windowMiddle = scroll.contentViewport.WorldToLocal(ClearArea(m_App, AppPage.Settings).center).y;
            scroll.scrollOffset = new Vector2(0, inContent - windowMiddle);
            simulate.FrameUpdate();
        }

        SettingsPaneManager OpenSettings()
        {
            OpenPage(AppPage.Settings);
            return m_App.Q<SettingsPaneManager>();
        }

        void OpenPage(AppPage page)
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            simulate.Click(m_App.GetNavButton(page));
            // Let the page slide all the way in, so the right-hand controls are on screen:
            // park + un-hide (two frames), then the 380 ms transition.
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.FrameUpdateMs(500);
            simulate.FrameUpdate();
        }

        [TestCase(AppPage.Shops, "shop-well", "shop-glass")]
        [TestCase(AppPage.Recipes, "recipe-well", "recipe-glass")]
        [TestCase(AppPage.Settings, "settings-well", "settings-glass")]
        public void ScrollingPagesScrollUnderTheGlassOverTheirHeader(AppPage page, string wellName, string glassName)
        {
            OpenPage(page);
            var pageElement = m_App.GetPage(page);
            var well = pageElement.Q(wellName);
            var title = pageElement.Q(className: "be-heading__title");
            var glass = pageElement.Q(glassName);
            var app = m_App.worldBound;
            var tolerance = 0.01f;

            // The content runs edge to edge and past both ends of the screen, under the glass.
            Assert.That(well.worldBound.xMin, Is.EqualTo(app.xMin).Within(tolerance), "no left margin");
            Assert.That(well.worldBound.xMax, Is.EqualTo(app.xMax).Within(tolerance), "no right margin");
            Assert.That(well.worldBound.yMin, Is.LessThan(app.yMin), "under the glass, past the top");
            Assert.That(well.worldBound.yMax, Is.GreaterThan(app.yMax), "under the glass, past the bottom");

            // A band of glass over the HUD and the title, from side to side, its bottom corners rounding off toward the
            // sides; nothing at the sides below it.
            Assert.That(glass.resolvedStyle.backdropFilter.Count(), Is.EqualTo(1), "it blurs");
            Assert.That(glass.worldBound.yMin, Is.LessThanOrEqualTo(app.yMin), "the glass covers the HUD");
            Assert.That(glass.worldBound.yMax, Is.GreaterThan(title.worldBound.yMax), "and the title");
            Assert.That(glass.worldBound.yMax, Is.LessThan(app.center.y), "and stops above the content");
            Assert.That(glass.worldBound.xMin, Is.EqualTo(app.xMin).Within(tolerance));
            Assert.That(glass.worldBound.xMax, Is.EqualTo(app.xMax).Within(tolerance));
            Assert.That(glass.resolvedStyle.borderBottomLeftRadius, Is.GreaterThan(0f), "its corners round off");
            Assert.That(glass.resolvedStyle.borderBottomRightRadius, Is.GreaterThan(0f));
            Assert.That(glass.resolvedStyle.borderBottomWidth, Is.GreaterThan(0f), "a line along its edge");

            // The nav's own glass stays, and the nav sits on it.
            Assert.That(m_App.navGlass.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_App.navGlass.resolvedStyle.opacity, Is.EqualTo(1f).Within(0.01f));
            foreach (AppPage navPage in System.Enum.GetValues(typeof(AppPage)))
                Assert.That(m_App.GetNavButton(navPage).worldBound.yMin, Is.GreaterThan(ClearArea(m_App, page).yMax), navPage + "'s nav item is on the nav's glass");
        }

        [TestCase(AppPage.Home)]
        [TestCase(AppPage.LuckyBag)]
        [TestCase(AppPage.Shops)]
        public void EveryPageShowsTheNavsOwnBar(AppPage page)
        {
            OpenPage(AppPage.Settings);
            OpenPage(page);
            var glass = m_App.navGlass;
            var app = m_App.worldBound;

            Assert.That(glass.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(glass.resolvedStyle.opacity, Is.EqualTo(1f).Within(0.01f));
            Assert.That(glass.resolvedStyle.backdropFilter.Count(), Is.EqualTo(1), "it blurs");
            Assert.That(glass.resolvedStyle.borderTopLeftRadius, Is.GreaterThan(0f), "its top corners round off toward the sides");
            Assert.That(glass.resolvedStyle.borderTopRightRadius, Is.GreaterThan(0f));
            Assert.That(glass.worldBound.yMax, Is.GreaterThanOrEqualTo(app.yMax), "the bar reaches the screen's bottom");
            Assert.That(glass.worldBound.xMin, Is.EqualTo(app.xMin).Within(0.01f), "from side to side");
            Assert.That(glass.worldBound.xMax, Is.EqualTo(app.xMax).Within(0.01f));
            foreach (AppPage navPage in System.Enum.GetValues(typeof(AppPage)))
                Assert.That(m_App.GetNavButton(navPage).worldBound.yMin, Is.GreaterThan(glass.worldBound.yMin), navPage + "'s nav item is on the bar");
        }

        [TestCase(AppPage.Shops, "shop-well", "shop-card", "shop-card")]
        [TestCase(AppPage.Recipes, "recipe-well", "recipe-card", "recipe-card")]
        [TestCase(AppPage.Settings, "settings-well", "season-panel", "settings-footer")]
        public void ScrollingPagesShowTheirFirstAndLastItemsInTheWindow(AppPage page, string wellName, string firstName, string lastName)
        {
            OpenPage(page);
            var pageElement = m_App.GetPage(page);
            var well = pageElement.Q(wellName);
            var scroll = well.Q<ScrollView>();
            var hero = m_App.Q("nav-lucky-bag-icon"); // the nav's Lucky Bag rises over the clear area
            var tolerance = 0.01f;

            void Check(string phone)
            {
                ScrollTo(scroll, end: false);
                Assert.That(Outermost(well, firstName, last: false).worldBound.yMin, Is.GreaterThanOrEqualTo(ClearArea(m_App, page).yMin - tolerance),
                    phone + ": at the top, the first item is clear of the glass");
                ScrollTo(scroll, end: true);
                var lastItem = Outermost(well, lastName, last: true);
                Assert.That(lastItem.worldBound.yMax, Is.LessThanOrEqualTo(hero.worldBound.yMin + tolerance), phone + ": at the end, the last item clears the nav");
                if (page == AppPage.Shops)
                    Assert.That(lastItem.Q<Label>("shop-name").text, Is.EqualTo(m_App.state.shopStates[^1].displayName), phone + ": every shop can be reached");
            }

            Check("no cutout");
            // A notch and a home indicator: the pages move in, and the glass and the scroll's ends with them.
            Assert.That(m_App.ApplySafeArea(m_App.hud.resolvedStyle.paddingTop + 18, m_App.nav.resolvedStyle.paddingBottom + 16), Is.True);
            simulate.FrameUpdate();
            Check("Dynamic Island");
            Assert.That(m_App.ApplySafeArea(0, 0), Is.True);
        }

        [Test]
        public void TheGlassCoverStaysPutWhileItsPageSlides()
        {
            OpenPage(AppPage.Shops);
            var shops = m_App.GetPage(AppPage.Shops);
            var center = m_App.worldBound.center;
            var tolerance = 0.01f;

            void AssertCoverInPlace(string when)
            {
                Assert.That(shops.worldBound.xMin, Is.LessThan(m_App.worldBound.xMin - 10f), when + ": the page has moved");
                shops.Query(className: BakeryAppManager.pageChromeUssClassName).ForEach(chrome =>
                    Assert.That(chrome.worldBound.xMin, Is.EqualTo(m_App.worldBound.xMin).Within(tolerance), when + ": its glass cover hasn't"));
            }

            // Mid-swipe toward the Lucky Bag: the nav's own glass stays as it is.
            simulate.MouseDown(center + new Vector2(60, 0));
            simulate.MouseMove(center + new Vector2(30, 0));
            simulate.MouseMove(center - new Vector2(40, 0));
            AssertCoverInPlace("mid-swipe");
            Assert.That(m_App.navGlass.resolvedStyle.opacity, Is.EqualTo(1f).Within(0.01f), "mid-swipe: the nav's glass stays");
            simulate.MouseUp(center - new Vector2(40, 0));
            simulate.FrameUpdateMs(500);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.LuckyBag));

            // Back to Shops from the nav: through the slide, frame by frame.
            simulate.Click(m_App.GetNavButton(AppPage.Shops));
            var farthest = 0f;
            for (int ms = 0; ms < 500; ms += 20)
            {
                simulate.FrameUpdateMs(20);
                farthest = Mathf.Max(farthest, Mathf.Abs(shops.worldBound.xMin - m_App.worldBound.xMin));
                shops.Query(className: BakeryAppManager.pageChromeUssClassName).ForEach(chrome =>
                    Assert.That(chrome.worldBound.xMin, Is.EqualTo(m_App.worldBound.xMin).Within(tolerance), $"{ms} ms into the slide: the glass cover hasn't moved"));
            }
            Assert.That(farthest, Is.GreaterThan(10f), "the page slid");
        }

        [Test]
        public void AWheelOrTrackpadScrollsInGentleSteps()
        {
            var settings = OpenSettings();
            var scroll = settings.Q<ScrollView>("settings-scroll");
            scroll.scrollOffset = Vector2.zero;
            simulate.FrameUpdate();

            // One wheel tick is 3 lines: 3 × 6 points (--unity-metrics-single_line-height on .be-scroll-well), not 3 × 18.
            simulate.ScrollWheel(scroll.contentViewport, new Vector2(0, 3));
            simulate.FrameUpdate();

            Assert.That(scroll.scrollOffset.y, Is.EqualTo(18f).Within(0.5f));
        }

        /// <summary>Scroll to an end; a virtualized list measures the items it binds as they come in, so settle on it.</summary>
        void ScrollTo(ScrollView scroll, bool end)
        {
            for (int i = 0; i < 4; i++)
            {
                scroll.scrollOffset = new Vector2(0, end ? scroll.verticalScroller.highValue : 0f);
                simulate.FrameUpdate();
            }
        }

        /// <summary>The topmost (or bottommost) displayed element with this name.</summary>
        static VisualElement Outermost(VisualElement root, string name, bool last)
        {
            VisualElement found = null;
            root.Query(name).ForEach(e =>
            {
                if (e.resolvedStyle.display == DisplayStyle.None || e.parent?.resolvedStyle.display == DisplayStyle.None)
                    return;
                if (found == null || (last ? e.worldBound.yMax > found.worldBound.yMax : e.worldBound.yMin < found.worldBound.yMin))
                    found = e;
            });
            Assert.That(found, Is.Not.Null, name);
            return found;
        }

        [Test]
        public void ReplayTutorialGoesHomeAndAsksForTheTutorial()
        {
            var settings = OpenSettings();
            var requested = 0;
            m_App.tutorialRequested += () => requested++;
            BringIntoWindow(settings, settings.replayTutorialButton);

            simulate.Click(settings.replayTutorialButton);
            simulate.FrameUpdate();

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home));
        }

        [TestCase(AppLanguage.French, "fr", "be-lang-fr", "RÉGLAGES", "Bar à bagels Mile End")]
        [TestCase(AppLanguage.Japanese, "ja", "be-lang-ja", "設定", "マイルエンド・ベーグルバー")]
        public void LanguageButtonsSwitchTheWholeApp(AppLanguage appLanguage, string code, string languageClass, string settingsTitle, string firstShopName)
        {
            var settings = OpenSettings();
            var button = appLanguage == AppLanguage.French ? settings.frenchButton : settings.japaneseButton;
            try
            {
                simulate.Click(button);
                simulate.FrameUpdate();

                Assert.That(LocalizationSettings.SelectedLocale.Code, Is.EqualTo(code));
                Assert.That(SettingsPaneManager.language, Is.EqualTo(appLanguage));
                Assert.That(button.ClassListContains(SettingsPaneManager.languageSelectedUssClassName), Is.True);
                Assert.That(m_App.ClassListContains(languageClass), Is.True);
                Assert.That(settings.Q<Label>(className: "be-heading__title").text, Is.EqualTo(settingsTitle));
                Assert.That(m_App.state.shopStates[0].displayName, Is.EqualTo(firstShopName));
            }
            finally
            {
                BagelLocalization.SetLocale("en");
            }
            simulate.FrameUpdate();
            Assert.That(m_App.ClassListContains(languageClass), Is.False);
        }

        [Test]
        public void APinnedAppKeepsItsScreen()
        {
            // An editor window showing one screen pins it: the player's clicks play, but don't leave it.
            m_App.pinned = true;
            simulate.Click(m_App.titleScreen);
            simulate.FrameUpdateMs(500);
            Assert.That(m_App.showTitle, Is.True, "the title stays");

            m_App.showTitle = false;
            simulate.FrameUpdate();
            simulate.Click(m_App.GetNavButton(AppPage.Shops));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home), "the nav doesn't change the page");

            simulate.Click(m_App.hud.Q<Label>("player-name"));
            simulate.FrameUpdate();
            Assert.That(m_App.Q<PlayerStatsDialog>().isOpen, Is.False, "the player row doesn't open the stats");

            // Code still changes the screen: the window's set-up opens LEVEL UP!, and its button doesn't close it.
            var levelUp = m_App.Q<LevelUpDialog>();
            levelUp.Show(m_App.state.shopStates[0], 50);
            simulate.FrameUpdateMs(300);
            simulate.Click(levelUp.okButton);
            simulate.FrameUpdateMs(600);
            Assert.That(levelUp.isOpen, Is.True, "AWESOME! doesn't close it");

            m_App.pinned = false;
            simulate.Click(levelUp.okButton);
            simulate.FrameUpdateMs(600);
            Assert.That(levelUp.isOpen, Is.False, "unpinned, it closes");
            simulate.Click(m_App.GetNavButton(AppPage.Shops));
            simulate.FrameUpdateMs(400);
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Shops));
        }

        [Test]
        public void ANavTapSquishesAndWobblesTheIconEvenWhenPinned()
        {
            m_App.showTitle = false;
            m_App.pinned = true;
            simulate.FrameUpdate();
            var shops = m_App.GetNavButton(AppPage.Shops);
            var icon = shops.Q(className: "be-nav__icon");

            simulate.Click(shops);
            Assert.That(shops.ClassListContains(BakeryAppManager.navItemPressedUssClassName), Is.True);
            simulate.FrameUpdateMs(80);
            Assert.That(icon.resolvedStyle.scale.value.x, Is.LessThan(1f), "the icon squishes");
            Assert.That(shops.ClassListContains(BakeryAppManager.navItemTappedUssClassName), Is.True, "and wobbles (NavWobble)");

            simulate.FrameUpdateMs(700);
            Assert.That(shops.ClassListContains(BakeryAppManager.navItemPressedUssClassName), Is.False);
            Assert.That(shops.ClassListContains(BakeryAppManager.navItemTappedUssClassName), Is.False, "the clip is taken off once it's done");
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home), "pinned: the page stays");
        }

        [Test]
        public void ThePlayerRowOpensTheStats()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            var dialog = m_App.Q<PlayerStatsDialog>();
            var state = m_App.state;
            Assert.That(dialog.isOpen, Is.False);

            simulate.Click(m_App.hud.Q<Label>("player-name"));
            simulate.FrameUpdate();
            Assert.That(dialog.isOpen, Is.True);

            var open = 0;
            foreach (var shop in state.shopStates)
                if (shop.unlocked)
                    open++;
            Assert.That(dialog.shopRows.childCount, Is.EqualTo(open), "a row per open shop");
            Assert.That(dialog.Q<Label>("income-total").text, Is.EqualTo(BagelConverters.FormatRate(state.incomePerSecond)));
            Assert.That(dialog.moodName.text, Is.EqualTo("Excited"), "Mile End's next upgrade is a milestone, and there are coins for it");
            Assert.That(dialog.levelNextLabel.text, Does.Contain(state.shopStates[0].displayName), "the way to the next level");

            // A full register: no more coins come in, Begu-chan dozes off, and the dialog says why.
            state.Tick(100000f);
            simulate.FrameUpdateMs(300);
            Assert.That(state.mood, Is.EqualTo(MascotMood.Sleepy));
            Assert.That(dialog.moodName.text, Is.EqualTo("Sleepy"));
            Assert.That(dialog.ClassListContains(PlayerStatsDialog.pausedUssClassName), Is.True);
            Assert.That(m_App.hud.Q<BeguChan>("avatar-art").mood, Is.EqualTo(MascotMood.Sleepy), "the HUD's avatar too");

            simulate.Click(dialog.confirmButton);
            simulate.FrameUpdateMs(300);
            Assert.That(dialog.isOpen, Is.False);
        }

        [Test]
        public void ShadowsToggleGivesThePanesBlurredDropShadows()
        {
            var settings = OpenSettings();
            AutoPerformance.on = false; // the switch is set by hand
            BringIntoWindow(settings, settings.filterShadowsToggle);
            var pane = settings.Q("performance-panel");
            var tile = settings.Q<Button>("replay-tutorial-button");
            Assert.That(pane.resolvedStyle.filter, Is.Empty, "no filter by default");
            Assert.That(pane.resolvedStyle.borderBottomWidth, Is.EqualTo(7f), "the lip is geometry");
            try
            {
                simulate.Click(settings.filterShadowsToggle.Q(className: Toggle.inputUssClassName));
                simulate.FrameUpdate();

                Assert.That(BakeryAppManager.filterShadows, Is.True);
                Assert.That(m_App.ClassListContains(BakeryAppManager.filterShadowsUssClassName), Is.True);
                // The pane holding the switch changes at once: a drop-shadow with a blur.
                var filters = new List<FilterFunction>(pane.resolvedStyle.filter);
                Assert.That(filters, Has.Count.EqualTo(1));
                Assert.That(filters[0].type, Is.EqualTo(FilterFunctionType.DropShadow));
                Assert.That(filters[0].GetParameter(2).floatValue, Is.GreaterThan(10f), "a big, soft blur");
                Assert.That(pane.resolvedStyle.borderBottomWidth, Is.EqualTo(7f), "the pane keeps its lip");
                Assert.That(tile.resolvedStyle.filter, Is.Empty, "the tiles keep their geometry shadows");
            }
            finally
            {
                BakeryAppManager.filterShadows = false;
                AutoPerformance.on = true;
            }
            simulate.FrameUpdate();
            Assert.That(m_App.ClassListContains(BakeryAppManager.filterShadowsUssClassName), Is.False);
            Assert.That(settings.filterShadowsToggle.value, Is.False);
            Assert.That(pane.resolvedStyle.filter, Is.Empty);
        }

        [Test]
        public void BoxShadowsToggleGivesThePanesTheSameShadowsAsGeometry()
        {
            var settings = OpenSettings();
            AutoPerformance.on = false; // the switch is set by hand
            BringIntoWindow(settings, settings.boxShadowsToggle);
            var pane = settings.Q("performance-panel");
            Assert.That(pane.resolvedStyle.boxShadow, Is.EqualTo(default(BoxShadow)), "no shadow by default");
            try
            {
                BakeryAppManager.filterShadows = true;
                simulate.Click(settings.boxShadowsToggle.Q(className: Toggle.inputUssClassName));
                simulate.FrameUpdate();

                Assert.That(BakeryAppManager.boxShadows, Is.True);
                Assert.That(BakeryAppManager.filterShadows, Is.False, "one side of the A/B at a time");
                Assert.That(settings.filterShadowsToggle.value, Is.False);
                Assert.That(m_App.ClassListContains(BakeryAppManager.boxShadowsUssClassName), Is.True);
                Assert.That(m_App.ClassListContains(BakeryAppManager.filterShadowsUssClassName), Is.False);
                // The same shadow as the drop-shadow side: its blur radius is twice drop-shadow()'s sigma.
                var shadow = pane.resolvedStyle.boxShadow;
                Assert.That(shadow.inset, Is.False);
                Assert.That(shadow.offset.y, Is.EqualTo(12f));
                Assert.That(shadow.blurRadius, Is.EqualTo(44f));
                Assert.That(pane.resolvedStyle.filter, Is.Empty, "no filter: no offscreen pass");

                // F cycles back to no shadows.
                BakeryAppManager.CycleShadows();
                simulate.FrameUpdate();
                Assert.That(BakeryAppManager.boxShadows || BakeryAppManager.filterShadows, Is.False);
                Assert.That(settings.boxShadowsToggle.value, Is.False);
                Assert.That(pane.resolvedStyle.boxShadow, Is.EqualTo(default(BoxShadow)));
            }
            finally
            {
                BakeryAppManager.filterShadows = false;
                BakeryAppManager.boxShadows = false;
                AutoPerformance.on = true;
            }
        }
    }
}
