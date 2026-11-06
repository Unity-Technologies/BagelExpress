using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;

namespace Bagel.B03_App
{
    /// <summary>
    /// Plays the real Main scene: the BagelExpressApp GameObject's PanelRenderer,
    /// its BakeryAppDriver and state, driven through the actual runtime panel.
    /// </summary>
    public class BakeryAppRuntimeTests : RuntimeUITestFixture
    {
        BakeryAppDriver m_Driver;
        bool m_SaveWasEnabled;
        bool m_AutoWasOn;

        [UnityOneTimeSetUp]
        public IEnumerator LoadMainScene()
        {
            // The real scene loads and saves the game: not in tests (the player's save stays as it was).
            m_SaveWasEnabled = BagelSave.enabled;
            BagelSave.enabled = false;
            // Nor does Auto mode turn settings down when the Editor runs the tests slowly.
            m_AutoWasOn = AutoPerformance.on;
            AutoPerformance.on = false;
            var assets = Resources.Load<BagelTestAssetList>("BagelTestAssetList");
            yield return SceneManager.LoadSceneAsync(assets.mainSceneName, LoadSceneMode.Single);
            yield return null; // Give Awake/OnEnable/Start a frame to run.
        }

        [OneTimeTearDown]
        public void RestoreSaving()
        {
            // The scene stays loaded through the tests after these: its driver would save their game (every 2 s and on
            // quitting) into the Editor's PlayerPrefs.
            if (m_Driver != null)
                Object.DestroyImmediate(m_Driver.gameObject);
            BagelSave.enabled = m_SaveWasEnabled;
            AutoPerformance.on = m_AutoWasOn;
        }

        [OneTimeSetUp]
        public void UseTheGamePanel()
        {
            // Runs after the scene is loaded: test the PanelRenderer the game itself uses.
            m_Driver = Object.FindAnyObjectByType<BakeryAppDriver>();
            SetPanelRenderer(m_Driver.GetComponent<PanelRenderer>());
            panelSize = BakeryUITestFixture.phoneSize;
        }

        [UnityTest]
        public IEnumerator CollectInTheRealGameAddsCoins()
        {
            var app = m_Driver.app;
            simulate.Click(app.titleScreen);
            simulate.FrameUpdateMs(500);
            Assert.That(app.showTitle, Is.False);

            var coins = m_Driver.state.coins;
            simulate.Click(app.Q<HomePaneManager>().collectButton);
            yield return null;

            Assert.That(m_Driver.state.coins, Is.GreaterThan(coins));
        }

        [UnityTest]
        public IEnumerator TheTutorialSpotlightsEachStepsButton()
        {
            var app = m_Driver.app;
            if (app.showTitle)
            {
                simulate.Click(app.titleScreen);
                simulate.FrameUpdateMs(500);
            }
            app.RestartTutorial();
            var overlay = app.tutorial;
            var screen = app.worldBound;

            // The third target is made at runtime (a shop card's button in a ListView): the step names it in Focus.
            foreach (var name in new[] { "collect-button", "nav-shops", "action-button" })
            {
                for (int i = 0; i < 30 && (!overlay.isActive || overlay.isPaused); i++)
                    simulate.FrameUpdateMs(100);
                Assert.That(overlay.isActive && !overlay.isPaused, Is.True, name + ": the tip is up");
                var target = overlay.target;
                Assert.That(target.name, Is.EqualTo(name));
                var bounds = target.worldBound;
                Assert.That(screen.Contains(bounds.min) && screen.Contains(bounds.max), Is.True, name + " is on screen, so the spotlight dims around it");
                Assert.That(bounds.width * bounds.height, Is.LessThan(screen.width * screen.height * 0.1f), name + ": the spotlight is on a button, not a page");
                simulate.Click(target);
                simulate.FrameUpdateMs(100);
            }

            simulate.FrameUpdateMs(1000);
            Assert.That(overlay.isActive, Is.False, "three tips, then done");
            yield return null;
        }
    }
}
