using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Step = Bagel.AutoPerformance.Step;

namespace Bagel.B09_Settings
{
    /// <summary>
    /// Settings › Performance › Auto mode: the steps down (a lower resolution while the long side keeps 1500 pixels,
    /// then no glass, then no filters), when it takes them (only when the game stays under 20 fps), the steps back up
    /// (at the frame cap, slowly, never while hot, never again after one that didn't last), and the Settings page while
    /// it runs them. On pretend phone screens with a 60 fps cap.
    /// </summary>
    public class AutoPerformanceTests : BakeryUITestFixture
    {
        static readonly Vector2Int s_GalaxyS10 = new Vector2Int(1440, 3040);
        static readonly Vector2Int s_IPhone12Pro = new Vector2Int(1170, 2532);

        readonly List<Vector2Int> m_Applied = new();
        BakeryAppManager m_App;

        [SetUp]
        public void SetUp()
        {
            RenderResolution.nativeOverride = s_GalaxyS10;
            RenderResolution.apply = m_Applied.Add;
            AutoPerformance.capOverride = 60f;
            AutoPerformance.thermalOverride = DeviceThermal.Level.Cool;
            // On, from the best settings, watching from scratch.
            AutoPerformance.on = false;
            AutoPerformance.on = true;
        }

        [TearDown]
        public void TearDown()
        {
            AutoPerformance.on = true;
            AutoPerformance.TakeControl();
            AutoPerformance.Restart();
            AutoPerformance.capOverride = null;
            AutoPerformance.thermalOverride = null;
            RenderResolution.apply = null;
            RenderResolution.nativeOverride = null;
            m_Applied.Clear();
        }

        static List<Step> TakeEveryStep()
        {
            var steps = new List<Step>();
            for (int i = 0; i < 6; i++)
                steps.Add(AutoPerformance.TakeStep());
            return steps;
        }

        /// <summary>Hands Auto mode <paramref name="seconds"/> of frames at <paramref name="fps"/>; returns the steps it took.</summary>
        static List<Step> Play(float fps, float seconds, float gpuSeconds = 0f, float cpuSeconds = 0f)
        {
            var steps = new List<Step>();
            var frame = 1f / fps;
            for (float t = 0f; t < seconds; t += frame)
            {
                var step = AutoPerformance.Watch(frame, gpuSeconds, cpuSeconds);
                if (step != Step.None)
                    steps.Add(step);
            }
            return steps;
        }

        [Test]
        public void TheResolutionGoesDownFirstThenTheGlassThenTheFilters()
        {
            Assert.That(TakeEveryStep(), Is.EqualTo(new[]
            {
                Step.LowerResolution, Step.LowerResolution, Step.NoGlass, Step.NoFilters, Step.None, Step.None
            }));
            Assert.That(m_Applied, Is.EqualTo(new[] { new Vector2Int(1080, 2280), new Vector2Int(720, 1520) }), "1/4 would be 760 tall");
            Assert.That(BakeryAppManager.noBlur, Is.True);
            Assert.That(BakeryAppManager.noFilters, Is.True);
            Assert.That(BakeryAppManager.noAnimations, Is.False, "animations aren't Auto mode's to turn off");
        }

        [Test]
        public void TheResolutionKeepsFifteenHundredPixelsOnTheLongSide()
        {
            RenderResolution.nativeOverride = s_IPhone12Pro;

            Assert.That(TakeEveryStep().Take(4), Is.EqualTo(new[] { Step.LowerResolution, Step.NoGlass, Step.NoFilters, Step.None }));
            Assert.That(m_Applied, Is.EqualTo(new[] { new Vector2Int(878, 1899) }), "1/2 would be 1266 tall");
        }

        [Test]
        public void WhereTheResolutionCantChangeTheGlassGoesFirst()
        {
            RenderResolution.apply = null; // the Editor, desktop, the Web

            Assert.That(TakeEveryStep().Take(3), Is.EqualTo(new[] { Step.NoGlass, Step.NoFilters, Step.None }));
        }

        [Test]
        public void OnlyAGameConsistentlyUnderTwentyFpsTakesAStep()
        {
            Assert.That(Play(60f, 10f), Is.Empty);
            Assert.That(Play(25f, 10f), Is.Empty, "slow, but not under 20 fps");

            // One slow stretch at a time isn't consistent.
            Assert.That(Play(12f, AutoPerformance.stretchSeconds), Is.Empty);
            Assert.That(Play(60f, AutoPerformance.stretchSeconds), Is.Empty);
            Assert.That(Play(12f, AutoPerformance.stretchSeconds), Is.Empty);
            Assert.That(Play(60f, AutoPerformance.stretchSeconds), Is.Empty);

            // Hitches (loading, coming back from the background) aren't the game running slowly.
            Assert.That(Play(1f / 1.5f, 30f), Is.Empty);

            // Two slow stretches in a row (from the start of one): one step.
            AutoPerformance.Restart();
            Assert.That(Play(12f, AutoPerformance.stretchSeconds * 2f + 0.25f), Is.EqualTo(new[] { Step.LowerResolution }));
            Assert.That(AutoPerformance.Describe(Step.LowerResolution), Is.EqualTo("Auto mode: resolution 1080×2280"));

            // Still too slow: after settling, two more slow stretches, the next step.
            Assert.That(Play(12f, AutoPerformance.settleSeconds), Is.Empty);
            Assert.That(Play(12f, AutoPerformance.stretchSeconds * 2f + 0.25f), Is.EqualTo(new[] { Step.LowerResolution }));
            Assert.That(Play(12f, 30f), Is.EqualTo(new[] { Step.NoGlass, Step.NoFilters }), "and nothing after that");
        }

        [Test]
        public void ABusyGpuCountsEvenWhenFramesLookOnTime()
        {
            // An iPhone at 3/4 with the glass: frames came every 16.7 ms while its GPU took 52 ms for each.
            Assert.That(Play(60f, AutoPerformance.stretchSeconds * 2f + 0.25f, gpuSeconds: 0.052f), Is.EqualTo(new[] { Step.LowerResolution }));
        }

        [Test]
        public void AtTheFrameCapItTakesTheStepsBackUpOneAtATimeLastOneFirst()
        {
            TakeEveryStep(); // 1080×2280, 720×1520, no glass, no filters
            m_Applied.Clear();

            Assert.That(Play(60f, AutoPerformance.stretchSeconds * (AutoPerformance.capStretchesToStepUp - 1)), Is.Empty,
                "much longer than a step down takes");
            Assert.That(Play(60f, 120f), Is.EqualTo(new[] { Step.FiltersOn, Step.GlassOn, Step.HigherResolution, Step.HigherResolution }));
            Assert.That(m_Applied, Is.EqualTo(new[] { new Vector2Int(1080, 2280), new Vector2Int(1440, 3040) }));
            Assert.That(BakeryAppManager.noBlur, Is.False);
            Assert.That(BakeryAppManager.noFilters, Is.False);
            Assert.That(AutoPerformance.level, Is.Zero);
            Assert.That(AutoPerformance.Describe(Step.HigherResolution), Is.EqualTo("Auto mode: resolution 1440×3040"));
            Assert.That(AutoPerformance.Describe(Step.GlassOn), Is.EqualTo("Auto mode: glass on"));
            Assert.That(Play(60f, 60f), Is.Empty, "nothing left to turn up");
        }

        [Test]
        public void AtTheCapMeansAtWhatTheDeviceAllows()
        {
            AutoPerformance.TakeStep();
            Assert.That(Play(50f, 60f), Is.Empty, "50 fps isn't at a 60 fps cap");

            // Held at 30 fps by the device (Low Power Mode), with 12 ms of GPU and 5 of CPU in each frame: at its cap.
            Assert.That(Play(30f, 20f, gpuSeconds: 0.012f, cpuSeconds: 0.005f), Is.EqualTo(new[] { Step.HigherResolution }));

            AutoPerformance.capOverride = null;
            Assert.That(AutoPerformance.capFps, Is.GreaterThanOrEqualTo(24f), "the real one, here");
        }

        [Test]
        public void FramesTheDeviceHoldsBackArentTooSlow()
        {
            // An iPhone holds a hot game at 30 fps, and lower when it's hotter: the work in each frame stays small.
            Assert.That(Play(15f, 30f, gpuSeconds: 0.015f, cpuSeconds: 0.005f), Is.Empty);

            // The same 15 fps with the GPU busy all frame long: too slow.
            Assert.That(Play(15f, AutoPerformance.stretchSeconds * 3f, gpuSeconds: 0.066f, cpuSeconds: 0.005f),
                Is.EqualTo(new[] { Step.LowerResolution }));
        }

        [Test]
        public void NothingGoesBackUpWhileTheDeviceIsHot()
        {
            AutoPerformance.TakeStep();
            AutoPerformance.thermalOverride = DeviceThermal.Level.Hot;
            Assert.That(Play(60f, 60f), Is.Empty);

            AutoPerformance.thermalOverride = DeviceThermal.Level.Warm;
            Assert.That(Play(60f, AutoPerformance.stretchSeconds + 0.1f), Is.EqualTo(new[] { Step.HigherResolution }), "once it cools down");
        }

        [Test]
        public void AStepUpThatDoesntLastIsntTriedAgain()
        {
            // An S10: 12 fps at its full resolution, 60 at 3/4.
            Assert.That(Play(12f, AutoPerformance.stretchSeconds * 2f + 0.25f), Is.EqualTo(new[] { Step.LowerResolution }));
            Assert.That(Play(60f, AutoPerformance.firstHoldSeconds - 1f), Is.Empty, "held after a step down");
            Assert.That(Play(60f, 4f), Is.EqualTo(new[] { Step.HigherResolution }));

            // Under 20 fps again within its trial: back down, for good.
            Assert.That(Play(12f, AutoPerformance.settleSeconds + AutoPerformance.stretchSeconds * 2f + 0.5f), Is.EqualTo(new[] { Step.LowerResolution }));
            Assert.That(Play(60f, 20f * 60f), Is.Empty);
            Assert.That(RenderResolution.index, Is.EqualTo(1));

            // Until Auto mode starts over.
            AutoPerformance.on = false;
            AutoPerformance.on = true;
            Assert.That(RenderResolution.index, Is.Zero);
        }

        [Test]
        public void EachStepDownAfterAStepUpHoldsTheNextOneTwiceAsLong()
        {
            Assert.That(Play(12f, AutoPerformance.stretchSeconds * 2f + 0.25f), Is.EqualTo(new[] { Step.LowerResolution }));
            Assert.That(Play(60f, AutoPerformance.firstHoldSeconds + 4f), Is.EqualTo(new[] { Step.HigherResolution }));

            // It lasts past its trial. Later, a heavier page.
            Assert.That(Play(60f, AutoPerformance.trialSeconds + 10f), Is.Empty);
            Assert.That(Play(12f, AutoPerformance.stretchSeconds * 3f), Is.EqualTo(new[] { Step.LowerResolution }));
            Assert.That(Play(60f, AutoPerformance.firstHoldSeconds * 2f - 3f), Is.Empty, "held twice as long");
            Assert.That(Play(60f, 6f), Is.EqualTo(new[] { Step.HigherResolution }), "then tried again: it lasted before");
        }

        [Test]
        public void AutoModeOffWatchesNothing()
        {
            AutoPerformance.on = false;

            Assert.That(Play(5f, 30f), Is.Empty);
            Assert.That(RenderResolution.index, Is.Zero);
        }

        SettingsPaneManager OpenSettings()
        {
            m_App = Show(assets.appScreenUxml).Q<BakeryAppManager>();
            m_App.showTitle = false;
            simulate.FrameUpdate();
            m_App.ShowPage(AppPage.Settings);
            simulate.FrameUpdate();
            // Scroll the Performance panel to the middle of the page's window, clear of the glass and the nav.
            var scroll = m_App.Q<ScrollView>("settings-scroll");
            var panel = m_App.Q("performance-panel");
            var inContent = scroll.contentContainer.WorldToLocal(panel.worldBound.center).y;
            scroll.scrollOffset = new Vector2(0, inContent - scroll.contentViewport.WorldToLocal(ClearArea(m_App, AppPage.Settings).center).y);
            simulate.FrameUpdate();
            return m_App.Q<SettingsPaneManager>();
        }

        [Test]
        public void ThePerformanceOverlayComesFirstThenAutoMode()
        {
            var settings = OpenSettings();
            var toggles = settings.Q("performance-panel").Query<Toggle>().ToList();

            Assert.That(toggles.Take(2), Is.EqualTo(new[] { settings.perfOverlayToggle, settings.autoModeToggle }));
            Assert.That(settings.autoModeToggle.label, Is.EqualTo("Auto mode"));
            Assert.That(settings.autoSettings.Contains(settings.resolutionButtons[0]), Is.True, "the settings it runs come under it");
        }

        [Test]
        public void WhileAutoModeRunsTheSettingsTheyShowItsChoicesAndCantBeChanged()
        {
            var settings = OpenSettings();
            Assert.That(settings.autoModeToggle.value, Is.True);
            Assert.That(settings.noBlurToggle.enabledInHierarchy, Is.False);
            Assert.That(settings.resolutionButtons[1].enabledInHierarchy, Is.False);
            Assert.That(settings.perfOverlayToggle.enabledInHierarchy, Is.True, "the overlay is never Auto mode's");

            simulate.Click(settings.resolutionButtons[1]);
            Assert.That(RenderResolution.index, Is.Zero, "locked");

            AutoPerformance.TakeStep();
            AutoPerformance.TakeStep();
            AutoPerformance.TakeStep();
            simulate.FrameUpdate();
            Assert.That(settings.resolutionButtons[2].ClassListContains(SettingsPaneManager.resolutionSelectedUssClassName), Is.True);
            Assert.That(settings.noBlurToggle.value, Is.True, "the switches show what Auto mode picked");

            // Off: the settings stay as Auto mode left them, to change by hand.
            simulate.Click(settings.autoModeToggle.Q(className: Toggle.inputUssClassName));
            Assert.That(AutoPerformance.on, Is.False);
            Assert.That(settings.noBlurToggle.enabledInHierarchy, Is.True);
            Assert.That(BakeryAppManager.noBlur, Is.True);
            Assert.That(RenderResolution.index, Is.EqualTo(2));

            // On again: from the best settings.
            simulate.Click(settings.autoModeToggle.Q(className: Toggle.inputUssClassName));
            Assert.That(AutoPerformance.on, Is.True);
            Assert.That(BakeryAppManager.noBlur, Is.False);
            Assert.That(RenderResolution.index, Is.Zero);
            Assert.That(settings.noBlurToggle.value, Is.False);
            Assert.That(settings.noBlurToggle.enabledInHierarchy, Is.False);
        }
    }
}
