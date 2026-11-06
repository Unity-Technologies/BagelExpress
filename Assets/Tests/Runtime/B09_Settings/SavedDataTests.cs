using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Bagel.B09_Settings
{
    /// <summary>
    /// Settings › Saved data: the game state round-trips through a save, and the two Reset buttons (after their
    /// confirmation) put the progress and the settings back. Saving to PlayerPrefs is off in tests (BagelSave.enabled).
    /// </summary>
    public class SavedDataTests : BakeryUITestFixture
    {
        BakeryAppManager m_App;

        /// <summary>A save from an earlier Play session, written before the test run enters Play mode.</summary>
        public class EarlierSession : IPrebuildSetup
        {
            public void Setup()
            {
                PlayerPrefs.SetString(BagelSave.gameKey, "{\"version\":1,\"coins\":4321,\"tutorialDone\":true}");
                PlayerPrefs.SetString(BagelSave.settingsKey, JsonUtility.ToJson(new BagelSave.Settings { season = 2, noBlur = true }));
                PlayerPrefs.Save();
            }
        }

        [TearDown]
        public void ResetStatics()
        {
            BakeryAppManager.noFilters = false;
            BakeryAppManager.noBlur = false;
            BakeryAppManager.filterShadows = false;
            BagelSound.enabled = true;
            BagelSave.haptics = true;
            AutoPerformance.on = true;
        }

        /// <summary>Scrolls Settings so the element sits in the middle of the page's window (not under the glass).</summary>
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
            m_App = Show(assets.appScreenUxml).Q<BakeryAppManager>();
            m_App.showTitle = false;
            simulate.FrameUpdate();
            simulate.Click(m_App.GetNavButton(AppPage.Settings));
            // Let the page slide all the way in (park + un-hide, then the 380 ms transition), so it's laid out.
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            simulate.FrameUpdateMs(500);
            simulate.FrameUpdate();
            return m_App.Q<SettingsPaneManager>();
        }

        [Test]
        public void AGameStateSurvivesASaveAndALoad()
        {
            var played = NewState();
            played.coins = 4321;
            played.tickets = 2;
            played.tutorialDone = true;
            Assert.That(played.TryUpgradeShop(0, out _), Is.True);
            var json = JsonUtility.ToJson(played.ToSaved());

            var loaded = NewState();
            loaded.Load(JsonUtility.FromJson<BakeryState.Saved>(json));

            Assert.That(loaded.coins, Is.EqualTo(played.coins));
            Assert.That(loaded.tickets, Is.EqualTo(2));
            Assert.That(loaded.tutorialDone, Is.True);
            Assert.That(loaded.shopStates[0].level, Is.EqualTo(played.shopStates[0].level));
            Assert.That(loaded.ownedRecipeCount, Is.EqualTo(played.ownedRecipeCount));
        }

        [Test, PrebuildSetup(typeof(EarlierSession))]
        public void EveryPlaySessionInTheEditorStartsFromScratch()
        {
            Assert.That(PlayerPrefs.HasKey(BagelSave.gameKey), Is.False, "the game is forgotten: the tutorial plays again");
            Assert.That(PlayerPrefs.HasKey(BagelSave.settingsKey), Is.False, "the settings are forgotten: every default");
        }

        [Test]
        public void ResetProgressGoesBackToTheStartAfterAConfirmation()
        {
            var settings = OpenSettings();
            var start = m_App.state.coins;
            m_App.state.coins = 99999;
            m_App.state.tickets = 0;

            BringIntoWindow(settings, settings.resetProgressButton);
            simulate.Click(settings.resetProgressButton);
            simulate.FrameUpdate();
            var dialog = m_App.Q<ConfirmDialog>("confirm-dialog");
            Assert.That(dialog.isOpen, Is.True, "it asks first");
            Assert.That(m_App.state.coins, Is.EqualTo(99999));

            simulate.Click(dialog.confirmButton);
            simulate.FrameUpdate();
            Assert.That(m_App.state.coins, Is.EqualTo(start));
            Assert.That(m_App.state.tickets, Is.EqualTo(assets.startState.tickets));
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Home), "and the tutorial plays again, from Home");
        }

        [Test]
        public void ResetSettingsPutsEverySettingBackAfterAConfirmation()
        {
            var settings = OpenSettings();
            AutoPerformance.on = false;
            BakeryAppManager.noFilters = true;
            BakeryAppManager.noBlur = true;
            BakeryAppManager.filterShadows = true;
            BagelSound.enabled = false;
            BagelSave.haptics = false;

            BringIntoWindow(settings, settings.resetSettingsButton);
            simulate.Click(settings.resetSettingsButton);
            simulate.FrameUpdate();
            simulate.Click(m_App.Q<ConfirmDialog>("confirm-dialog").confirmButton);
            simulate.FrameUpdate();

            Assert.That(BakeryAppManager.noFilters, Is.False);
            Assert.That(BakeryAppManager.noBlur, Is.False, "the glass on, on every platform");
            Assert.That(BakeryAppManager.filterShadows, Is.False);
            Assert.That(AutoPerformance.on, Is.True, "Auto mode runs the performance settings again");
            Assert.That(BagelSound.enabled, Is.True);
            Assert.That(settings.hapticsToggle.value, Is.True, "the switches show it");
            Assert.That(settings.noFiltersToggle.value, Is.False);
            Assert.That(settings.autoModeToggle.value, Is.True);
        }

        [Test]
        public void ASaveFromBeforeAutoModeKeepsItsChoicesButNotItsPerformanceSettings()
        {
            // Version 1: phones measured themselves and turned the glass off.
            var old = BagelSave.Parse("{\"version\":1,\"season\":2,\"language\":\"fr\",\"sound\":false,\"performanceOverlay\":true," +
                "\"resolution\":1,\"noBlur\":true,\"noFilters\":true,\"tuned\":true,\"tuningReport\":\"→ 1440×3040, glass off\"}");
            Assert.That(old.season, Is.EqualTo(2));
            Assert.That(old.language, Is.EqualTo("fr"));
            Assert.That(old.sound, Is.False);
            Assert.That(old.performanceOverlay, Is.True);
            Assert.That(old.autoMode, Is.True);
            Assert.That(old.noBlur, Is.False);
            Assert.That(old.noFilters, Is.False);
            Assert.That(old.resolution, Is.Zero);
            Assert.That(old.version, Is.EqualTo(2));

            // Version 2 keeps everything, Auto mode off and what was picked by hand.
            AutoPerformance.on = false;
            BakeryAppManager.noBlur = true;
            var saved = BagelSave.Parse(JsonUtility.ToJson(BagelSave.CurrentSettings()));
            Assert.That(saved.autoMode, Is.False);
            Assert.That(saved.noBlur, Is.True);
        }
    }
}
