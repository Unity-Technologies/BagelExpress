using System.Linq;
using NUnit.Framework;
using Unity.Localization;
using UnityEditor;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Bagel.B10_EditorTools
{
    /// <summary>
    /// The Bagel UI window runs every screen of the game inside an EditorWindow, all bound to
    /// one shared BakeryState. These tests drive it like a presenter would: click in one
    /// phone, check that another phone updated through data binding.
    /// </summary>
    public class BagelExpressAllUIWindowTests
        : EditorWindowUITestFixture<BagelExpressAllUIWindow>
    {
        const int k_MaxFrames = 30;

        public BagelExpressAllUIWindowTests()
        {
            debugMode = true;
        }

        [Test]
        public void EveryPhoneSharesOneState()
        {
            simulate.FrameUpdate();

            Assert.That(window.state, Is.Not.Null);
            Assert.That(window.phones.Count, Is.EqualTo(BagelExpressAllUIWindow.phoneSpecs.Length));
            foreach (var phone in window.phones)
            {
                Assert.That(phone.app, Is.Not.Null, phone.spec.key);
                Assert.That(phone.app.state, Is.SameAs(window.state), phone.spec.key);
                Assert.That(phone.app.currentPage, Is.EqualTo(phone.spec.startPage), phone.spec.key);
            }
        }

        [Test]
        public void EveryScreenOfBagelFeaturesIsHereWithItsLink()
        {
            simulate.FrameUpdate();

            Assert.That(window.phones.Select(p => p.spec.screen), Is.EqualTo(BagelFeatureGuide.screens.Select(s => s.screen)), "the Game mode's screens, in its order");
            foreach (var phone in window.phones)
                Assert.That(phone.root.Q<Button>("features-link"), Is.Not.Null, phone.spec.key);
            Assert.That(BagelWindowIcons.Load(BagelWindowIcons.bagelUI), Is.Not.Null);
            Assert.That(BagelWindowIcons.Load(BagelWindowIcons.bagelFeatures), Is.Not.Null);
        }

        [Test]
        public void ThePhonesOutOfViewHoldStill()
        {
            panelSize = new Vector2(1000f, 1000f);
            window.ApplyZoom(BagelExpressAllUIWindow.defaultZoom);
            window.stillOffViewPhones = true;
            var scroll = rootVisualElement.Q<ScrollView>("phones-scroll");
            var first = window.phones[0];
            var last = window.phones[window.phones.Count - 1];
            scroll.ScrollTo(first.root);
            simulate.FrameUpdate();
            simulate.FrameUpdate();

            Assert.That(first.still, Is.False);
            Assert.That(last.still, Is.True);
            Assert.That(last.screen.panel, Is.Not.Null, "still loaded");
            Assert.That(ArtKit.ShouldAnimate(last.screen.Q<SeasonParticles>()), Is.False, "its particles stop");
            Assert.That(last.unfiltered, Is.Not.Empty);
            Assert.That(last.unfiltered.All(e => !e.resolvedStyle.filter.Any() && !e.resolvedStyle.backdropFilter.Any()), Is.True, "its filters are off");

            scroll.ScrollTo(last.root);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.That(last.still, Is.False);
            Assert.That(first.still, Is.True);
            Assert.That(ArtKit.ShouldAnimate(last.screen.Q<SeasonParticles>()), Is.True, "its particles fall again");
            var filtered = 0;
            last.screen.Query<VisualElement>().ForEach(e =>
            {
                Assert.That(e.style.filter.keyword, Is.EqualTo(StyleKeyword.Null), e.name);
                Assert.That(e.style.backdropFilter.keyword, Is.EqualTo(StyleKeyword.Null), e.name);
                if (e.resolvedStyle.filter.Any() || e.resolvedStyle.backdropFilter.Any())
                    filtered++;
            });
            Assert.That(filtered, Is.GreaterThan(0), "its filters are back");
        }

        [Test]
        public void TheSeasonAndLanguageStripsPickForEveryPhone()
        {
            simulate.FrameUpdate();
            var seasons = rootVisualElement.Q<BagelOptionStrip>("season-strip");
            var languages = rootVisualElement.Q<BagelOptionStrip>("language-strip");
            var themes = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(BagelExpressAllUIWindow.themeCollectionPath).collection;
            Assert.That(seasons.childCount, Is.EqualTo(themes.Count), "a season per theme");
            Assert.That(languages.childCount, Is.EqualTo(BagelPickerIcons.languages.Length));
            var season = themes.FindIndex(e => e.theme == window.currentTheme);
            Assert.That(seasons.selectedIndex, Is.EqualTo(season));
            Assert.That(languages.selectedIndex, Is.EqualTo(BagelPickerIcons.LanguageIndex(BagelLocalization.localeCode)));

            var target = (season + 1) % themes.Count;
            try
            {
                simulate.Click(seasons[target]);
                simulate.FrameUpdate();
                Assert.That(window.currentTheme, Is.SameAs(themes[target].theme));
                Assert.That(SettingsPaneManager.seasonIndex, Is.EqualTo(target), "picked as the Settings page picks it");
                Assert.That(seasons[target].ClassListContains(BagelOptionStrip.selectedOptionUssClassName), Is.True, "lit");
                Assert.That(seasons[season].ClassListContains(BagelOptionStrip.selectedOptionUssClassName), Is.False);

                simulate.Click(languages[BagelPickerIcons.LanguageIndex("ja")]);
                Assert.That(BagelLocalization.localeCode, Is.EqualTo("ja"));
                // Picked elsewhere, here the Settings phone: the strips follow.
                var pane = window.GetPhone(AppPage.Settings).app.Q<SettingsPaneManager>();
                pane.SetLanguage(AppLanguage.French);
                pane.SelectSeason(season);
                Assert.That(languages.selectedIndex, Is.EqualTo(BagelPickerIcons.LanguageIndex("fr")));
                Assert.That(seasons.selectedIndex, Is.EqualTo(season));
            }
            finally
            {
                window.SelectSeason(season);
                BagelLocalization.SetLocale("en");
            }
        }

        [Test]
        public void TheToolbarIsLaidOutLikeBagelFeatures()
        {
            var toolbar = rootVisualElement.Q<UnityEditor.UIElements.Toolbar>("toolbar");
            var brand = rootVisualElement.Q("brand");
            var reset = rootVisualElement.Q<BagelOptionStrip>("reset-strip");
            var unlocks = rootVisualElement.Q<BagelOptionStrip>("unlock-strip");
            var zoomLabel = rootVisualElement.Q<Label>("zoom-label");
            var seasons = rootVisualElement.Q<BagelOptionStrip>("season-strip");
            var languages = rootVisualElement.Q<BagelOptionStrip>("language-strip");
            var inspector = rootVisualElement.Q<BagelOptionStrip>("inspector-strip");

            // Narrow, its controls on more rows, then full screen (as in the demo): one row, back to its height.
            panelSize = new Vector2(600f, 1000f);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.That(toolbar.worldBound.height, Is.GreaterThan(50f), "more rows");
            panelSize = new Vector2(1496f, 900f);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            Assert.That(toolbar.worldBound.height, Is.EqualTo(35f).Within(0.5f), "one row, as tall as Bagel Features'");
            Assert.That(seasons.worldBound.y - toolbar.worldBound.y, Is.EqualTo(6f).Within(0.5f), "6 px above the strips");
            Assert.That(toolbar.worldBound.yMax - seasons.worldBound.yMax, Is.EqualTo(7f).Within(0.5f), "and 6 below, then the border");

            Assert.That(reset.worldBound.x - brand.worldBound.xMax, Is.LessThan(12f), "Reset right after the brand");
            var newGame = rootVisualElement.Q<BagelOptionStrip>("new-game-strip");
            var currency = rootVisualElement.Q<BagelOptionStrip>("currency-strip");
            Assert.That(newGame.worldBound.x, Is.GreaterThan(reset.worldBound.xMax), "then New Game");
            Assert.That(currency.worldBound.x, Is.GreaterThan(newGame.worldBound.xMax), "the coins and tickets");
            Assert.That(unlocks.worldBound.x, Is.GreaterThan(currency.worldBound.xMax), "and what unlocks everything");
            Assert.That(zoomLabel.worldBound.xMax, Is.LessThan(seasons.worldBound.x), "the zoom where Bagel Features has its search");
            Assert.That(seasons.worldBound.x, Is.GreaterThan(toolbar.worldBound.center.x), "the strips on the right");
            Assert.That(inspector.worldBound.x, Is.GreaterThan(languages.worldBound.xMax), "the Inspector last");
            Assert.That(toolbar.worldBound.xMax - inspector.worldBound.xMax, Is.LessThan(12f), "all the way on the right");
            foreach (var strip in new[] { reset, newGame, currency, unlocks, languages, inspector })
            {
                Assert.That(strip.worldBound.height, Is.EqualTo(seasons.worldBound.height), strip.name + " as tall");
                Assert.That(strip.worldBound.center.y, Is.EqualTo(seasons.worldBound.center.y).Within(0.5f), strip.name + " in line");
            }
            Assert.That(inspector[0].name, Is.EqualTo("inspector-toggle"));
            Assert.That(inspector[0].Q<Label>(), Is.Null, "an icon, no text");
        }

        [Test]
        public void NewGameAloneThenTheCheatsInStrips()
        {
            simulate.FrameUpdate();
            string[] Words(string strip) => rootVisualElement.Q<BagelOptionStrip>(strip).Children().Select(option => option.Q<Label>().text).ToArray();
            Assert.That(Words("new-game-strip"), Is.EqualTo(new[] { "New Game" }), "on its own");
            Assert.That(Words("currency-strip"), Is.EqualTo(new[] { "+1,000", "+60", "+5" }), "an amount and its currency's icon");
            var currencies = rootVisualElement.Q<BagelOptionStrip>("currency-strip").Children().Select(option => option.Q<CurrencyIcon>()?.kind);
            Assert.That(currencies, Is.EqualTo(new CurrencyKind?[] { CurrencyKind.Coins, CurrencyKind.Gems, CurrencyKind.Tickets }));
            Assert.That(Words("unlock-strip"), Is.EqualTo(new[] { "All", "All" }), "All and an icon");
            var unlock = rootVisualElement.Q<BagelOptionStrip>("unlock-strip");
            Assert.That(unlock.Q("unlock-shops-button").Q(className: "bagel-icon--shops"), Is.Not.Null, "the shops' icon");
            Assert.That(unlock.Q("unlock-recipes-button").Q(className: "bagel-icon--recipes"), Is.Not.Null, "the recipes' icon");

            var state = window.state;
            var (coins, gems, tickets) = (state.coins, state.gems, state.tickets);
            simulate.Click(rootVisualElement.Q("add-coins-button"));
            simulate.Click(rootVisualElement.Q("add-gems-button"));
            simulate.Click(rootVisualElement.Q("add-tickets-button"));
            Assert.That(state.coins, Is.EqualTo(coins + 1000));
            Assert.That(state.gems, Is.EqualTo(gems + 60));
            Assert.That(state.tickets, Is.EqualTo(tickets + 5));

            Assert.That(state.openShopCount, Is.LessThan(state.shops.Count), "the demo starts with shops to open");
            Assert.That(state.ownedRecipeCount, Is.LessThan(state.recipeCount), "and recipes to find");
            var newRecipes = state.newRecipeCount;
            simulate.Click(unlock.Q("unlock-shops-button"));
            simulate.Click(unlock.Q("unlock-recipes-button"));
            Assert.That(state.openShopCount, Is.EqualTo(state.shops.Count), "every shop open");
            Assert.That(state.coins, Is.EqualTo(coins + 1000), "for free");
            Assert.That(state.ownedRecipeCount, Is.EqualTo(state.recipeCount), "every recipe");
            Assert.That(state.newRecipeCount, Is.EqualTo(newRecipes), "none of them marked NEW");

            simulate.Click(rootVisualElement.Q("new-game-button"));
            Assert.That(ReferenceEquals(window.state, state), Is.False, "a new game");
            Assert.That(window.state.openShopCount, Is.LessThan(window.state.shops.Count), "from the start state again");
        }

        [Test]
        public void TheInspectorToggleIsLitWhileTheInspectorShows()
        {
            simulate.FrameUpdate();
            var toggle = rootVisualElement.Q("inspector-toggle");
            var dock = rootVisualElement.Q("inspector-dock");
            try
            {
                window.ShowInspector(false);
                simulate.Click(toggle);
                Assert.That(dock.ClassListContains(BagelExpressAllUIWindow.dockHiddenUssClassName), Is.False, "shown");
                Assert.That(BagelOptionStrip.IsOn(toggle), Is.True, "lit");
                simulate.Click(toggle);
                Assert.That(dock.ClassListContains(BagelExpressAllUIWindow.dockHiddenUssClassName), Is.True, "hidden");
                Assert.That(BagelOptionStrip.IsOn(toggle), Is.False);
            }
            finally
            {
                window.ShowInspector(false);
            }
        }

        [Test]
        public void ResetStartsAgainAsTheWindowOpens()
        {
            simulate.FrameUpdate();
            var themes = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(BagelExpressAllUIWindow.themeCollectionPath).collection;
            var season = themes.FindIndex(e => e.theme == window.currentTheme);
            try
            {
                window.SelectSeason(2);
                BagelLocalization.SetLocale("ja");
                window.ApplyZoom(BagelExpressAllUIWindow.maxZoom);
                window.ShowInspector(true);
                var oldState = window.state;
                simulate.FrameUpdate();

                simulate.Click(rootVisualElement.Q("reset-button"));
                simulate.FrameUpdate();
                Assert.That(ReferenceEquals(window.state, oldState), Is.False, "a fresh game state");
                Assert.That(window.phones.Select(p => p.app.state), Has.All.SameAs(window.state), "on every phone");
                Assert.That(window.currentTheme, Is.SameAs(themes[0].theme), "the first season");
                Assert.That(BagelLocalization.localeCode, Is.EqualTo("en"), "the project's language");
                Assert.That(rootVisualElement.Q<BagelOptionStrip>("season-strip").selectedIndex, Is.Zero, "its strips say so");
                Assert.That(window.zoom, Is.EqualTo(BagelExpressAllUIWindow.defaultZoom));
                Assert.That(rootVisualElement.Q("inspector-dock").ClassListContains(BagelExpressAllUIWindow.dockHiddenUssClassName), Is.True, "no Inspector");
                Assert.That(BagelOptionStrip.IsOn(rootVisualElement.Q("inspector-toggle")), Is.False, "its toggle says so");
            }
            finally
            {
                window.SelectSeason(season);
                BagelLocalization.SetLocale("en");
                window.ApplyZoom(BagelExpressAllUIWindow.defaultZoom);
                window.ShowInspector(false);
            }
        }

        [Test]
        public void SettingsChangedHereOrInTheToolbarLeaveTheProjectAsItIs()
        {
            simulate.FrameUpdate();
            // What a build starts with: the game's panel and its theme, the start state, the project's locale.
            var assets = new Object[]
            {
                AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/Runtime/B01_PanelSettings/ScreenSpacePanelSettings.asset"),
                AssetDatabase.LoadAssetAtPath<BakeryState>(BagelExpressAllUIWindow.startStatePath),
                AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(BagelExpressAllUIWindow.themeCollectionPath),
                LocalizationSettings.Instance,
            };
            Assert.That(assets, Has.None.Null);
            // Their content, not just their dirty flags: a value set from code doesn't mark an asset dirty, yet a build
            // would take it.
            var contentBefore = assets.Select(asset => EditorJsonUtility.ToJson(asset)).ToArray();
            var sceneWasDirty = SceneManager.GetActiveScene().isDirty;

            var pane = window.GetPhone(AppPage.Settings).app.Q<SettingsPaneManager>();
            var toggles = new[] { pane.autoModeToggle, pane.noBlurToggle, pane.noFiltersToggle, pane.noAnimationsToggle,
                pane.filterShadowsToggle, pane.soundToggle, pane.hapticsToggle, pane.perfOverlayToggle };
            var season = SettingsPaneManager.seasonIndex;
            var (screen, toolbarSeason, language) = (DemoOverrides.screen, DemoOverrides.season, DemoOverrides.language);
            var (auto, noBlur, noFilters, noAnimations, filterShadows) = (AutoPerformance.on, BakeryAppManager.noBlur,
                BakeryAppManager.noFilters, BakeryAppManager.noAnimations, BakeryAppManager.filterShadows);
            var (sound, haptics, overlay) = (BagelSound.enabled, BagelSave.haptics, PerfOverlay.shown);
            try
            {
                DemoOverrides.screen = GameScreen.Get("shops");
                DemoOverrides.season = 2;
                DemoOverrides.language = "ja";
                pane.SelectSeason(3);
                pane.SetLanguage(AppLanguage.French);
                foreach (var toggle in toggles)
                    toggle.value = !toggle.value;
                Assert.That(window.state.TryUpgradeShop(0, out _), Is.True);
                simulate.FrameUpdate();

                Assert.That(assets.Select(asset => EditorJsonUtility.ToJson(asset)), Is.EqualTo(contentBefore), "no asset changed");
                Assert.That(SceneManager.GetActiveScene().isDirty, Is.EqualTo(sceneWasDirty), "nor the scene");
            }
            finally
            {
                (DemoOverrides.screen, DemoOverrides.season, DemoOverrides.language) = (screen, toolbarSeason, language);
                pane.SelectSeason(season);
                BagelLocalization.SetLocale("en");
                // Auto mode first: turning it on puts the settings it runs at their best.
                AutoPerformance.on = auto;
                (BakeryAppManager.noBlur, BakeryAppManager.noFilters, BakeryAppManager.noAnimations, BakeryAppManager.filterShadows) =
                    (noBlur, noFilters, noAnimations, filterShadows);
                (BagelSound.enabled, BagelSave.haptics, PerfOverlay.shown) = (sound, haptics, overlay);
            }
        }

        [Test]
        public void EachPhoneStaysOnItsScreen()
        {
            panelSize = new Vector2(1600f, 1000f);
            window.ApplyZoom(BagelExpressAllUIWindow.minZoom);
            var scroll = rootVisualElement.Q<ScrollView>("phones-scroll");
            var title = window.GetPhone("title");
            var home = window.GetPhone("home");
            simulate.FrameUpdate();

            scroll.ScrollTo(title.root);
            simulate.FrameUpdate();
            simulate.Click(title.app.titleScreen);
            simulate.FrameUpdateMs(500);
            Assert.That(title.app.showTitle, Is.True, "the title phone keeps its title");

            scroll.ScrollTo(home.root);
            simulate.FrameUpdate();
            simulate.Click(home.app.GetNavButton(AppPage.Shops));
            simulate.FrameUpdateMs(400);
            Assert.That(home.app.currentPage, Is.EqualTo(AppPage.Home), "the Home phone stays on Home");
        }

        [Test]
        public void UpgradeInShopsPhoneUpdatesHomePhoneCoins()
        {
            // Small phones in a big window, Shops phone scrolled into view so the click lands on it.
            panelSize = new Vector2(1600f, 1000f);
            window.ApplyZoom(BagelExpressAllUIWindow.minZoom);
            var shops = window.GetPhone(AppPage.Shops);
            var home = window.GetPhone(AppPage.Home);
            // Laid out at this zoom first, so the scroll lands where the phone is now.
            simulate.FrameUpdate();
            rootVisualElement.Q<ScrollView>("phones-scroll").ScrollTo(shops.root);

            var state = window.state;
            var mileEnd = state.shopStates[0];
            StringAssert.Contains("Mile End", mileEnd.displayName);

            // The Shops list is a bound ListView: its cards appear after a few panel updates.
            var card = FindShopCard(shops.app, mileEnd);
            for (int i = 0; card == null && i < k_MaxFrames; i++)
            {
                simulate.FrameUpdate();
                card = FindShopCard(shops.app, mileEnd);
            }
            Assert.That(card, Is.Not.Null, "Mile End card not found in the Shops phone");
            Assert.That(card.actionButton.enabledInHierarchy, Is.True, "Mile End upgrade should be affordable at start");

            var homeCoinsPill = home.app.GetCurrencyPill(CurrencyKind.Coins);
            simulate.FrameUpdate();
            var levelBefore = mileEnd.level;
            var coinsBefore = homeCoinsPill.value;
            Assert.That(coinsBefore, Is.EqualTo(state.coins));

            simulate.Click(card.actionButton);
            simulate.FrameUpdate();

            Assert.That(mileEnd.level, Is.EqualTo(levelBefore + 1), "Shared Mile End level");
            Assert.That(homeCoinsPill.value, Is.LessThan(coinsBefore), "Home phone coin pill");
            Assert.That(homeCoinsPill.value, Is.EqualTo(state.coins));
        }

        static ShopCard FindShopCard(VisualElement root, ShopState shop)
        {
            ShopCard found = null;
            root.Query<ShopCard>().ForEach(card =>
            {
                if (found == null && card.actionButton != null && card.panel != null
                    && DataSourceUtility.TryResolve(card, out ShopState resolved) && resolved == shop)
                    found = card;
            });
            return found;
        }
    }
}
