using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B09_Settings
{
    /// <summary>
    /// Settings › Performance, set by hand (Auto mode off): the render resolution (on a pretend Galaxy S10 screen), the
    /// performance overlay, the blurred drop-shadows on panes, and the switches that turn off every animation or every
    /// filter. Auto mode has its own tests (AutoPerformanceTests).
    /// </summary>
    public class PerformanceSettingsTests : BakeryUITestFixture
    {
        static readonly Vector2Int s_GalaxyS10 = new Vector2Int(1440, 3040);

        readonly List<Vector2Int> m_Applied = new();
        BakeryAppManager m_App;

        [SetUp]
        public void SetUp()
        {
            RenderResolution.nativeOverride = s_GalaxyS10;
            RenderResolution.apply = m_Applied.Add;
            AutoPerformance.on = false;
        }

        [TearDown]
        public void TearDown()
        {
            RenderResolution.Select(0);
            RenderResolution.apply = null;
            RenderResolution.nativeOverride = null;
            PerfOverlay.shown = false;
            BakeryAppManager.noAnimations = false;
            BakeryAppManager.noFilters = false;
            m_Applied.Clear();
            AutoPerformance.on = true;
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
        public void EachResolutionShowsWhatItRendersAtOnThisScreen()
        {
            var settings = OpenSettings();

            Assert.That(settings.resolutionButtons.Select(b => b.text),
                Is.EqualTo(new[] { "Native\n1440×3040", "3/4\n1080×2280", "1/2\n720×1520", "1/4\n360×760" }));
            Assert.That(settings.resolutionButtons[0].ClassListContains(SettingsPaneManager.resolutionSelectedUssClassName), Is.True);

            simulate.Click(settings.resolutionButtons[2]);

            Assert.That(m_Applied, Is.EqualTo(new[] { new Vector2Int(720, 1520) }));
            Assert.That(RenderResolution.index, Is.EqualTo(2));
            Assert.That(settings.resolutionButtons.Select(b => b.ClassListContains(SettingsPaneManager.resolutionSelectedUssClassName)),
                Is.EqualTo(new[] { false, false, true, false }));
        }

        [Test]
        public void WhereTheResolutionCantChangeOnlyNativeCanBePicked()
        {
            RenderResolution.apply = null; // the Editor and desktop players
            var settings = OpenSettings();

            Assert.That(settings.resolutionButtons.Select(b => b.enabledSelf), Is.EqualTo(new[] { true, false, false, false }));
            simulate.Click(settings.resolutionButtons[1]);
            Assert.That(RenderResolution.index, Is.Zero);
        }

        [Test]
        public void ThePerformanceOverlayShowsTheFrameRateAndAGraph()
        {
            var settings = OpenSettings();
            var overlay = m_App.Q<PerfOverlay>();
            Assert.That(overlay.window.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "off by default");

            simulate.Click(settings.perfOverlayToggle.Q(className: Toggle.inputUssClassName));
            for (int i = 0; i < 40; i++)
                simulate.FrameUpdate();

            Assert.That(PerfOverlay.shown, Is.True);
            Assert.That(overlay.window.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(overlay.graph.count, Is.GreaterThan(10), "one bar per frame");
            Assert.That(int.TryParse(overlay.fpsLabel.text, out var fps) && fps > 0, Is.True, overlay.fpsLabel.text);
            Assert.That(overlay.resolutionLabel.text, Does.Match(@"^\d+×\d+$"));
            // How hot the device is, where the platform says (the Editor on macOS: its thermal state).
            Assert.That(overlay.thermalLabel.text, Is.Empty.Or.StartsWith("Thermal ").Or.StartsWith("Battery "));
            Assert.That(overlay.thermalLabel.ClassListContains(PerfOverlay.thermalGoneUssClassName), Is.EqualTo(overlay.thermalLabel.text.Length == 0));
        }

        [Test]
        public void TheThermalLineSaysWhatEachPlatformShares()
        {
            // Android: the battery's temperature, and the thermal status from Android 10 (none isn't shown).
            Assert.That(DeviceThermal.Format(DeviceThermal.FromAndroid(372, 0)), Is.EqualTo("Battery 37.2 °C"));
            Assert.That(DeviceThermal.FromAndroid(372, 0).level, Is.EqualTo(DeviceThermal.Level.Cool));
            Assert.That(DeviceThermal.Format(DeviceThermal.FromAndroid(415, 2)), Is.EqualTo("Battery 41.5 °C · moderate"));
            Assert.That(DeviceThermal.FromAndroid(415, 2).level, Is.EqualTo(DeviceThermal.Level.Warm));
            Assert.That(DeviceThermal.FromAndroid(468, 3).level, Is.EqualTo(DeviceThermal.Level.Hot));
            Assert.That(DeviceThermal.Format(DeviceThermal.FromAndroid(365, -1)), Is.EqualTo("Battery 36.5 °C"), "before Android 10");
            // iOS and macOS: only the thermal state.
            Assert.That(DeviceThermal.Format(DeviceThermal.FromAppleState(0)), Is.EqualTo("Thermal nominal"));
            Assert.That(DeviceThermal.Format(DeviceThermal.FromAppleState(2)), Is.EqualTo("Thermal serious"));
            Assert.That(DeviceThermal.FromAppleState(2).level, Is.EqualTo(DeviceThermal.Level.Hot), "iOS caps the game at 30 fps");
            // Nothing to say.
            Assert.That(DeviceThermal.Format(DeviceThermal.FromAppleState(-1)), Is.Empty);
        }

        [Test]
        public void TheProfilersThermalStatusIsThePlatformsOwnNumber()
        {
            // DeviceThermalCounters records it as is, and only when there is one.
            Assert.That(DeviceThermal.FromAppleState(2).status, Is.EqualTo(2));
            Assert.That(DeviceThermal.FromAppleState(4).status, Is.EqualTo(-1));
            Assert.That(DeviceThermal.FromAndroid(372, 0).status, Is.EqualTo(0));
            Assert.That(DeviceThermal.FromAndroid(468, 5).status, Is.EqualTo(5));
            Assert.That(DeviceThermal.FromAndroid(365, -1).status, Is.EqualTo(-1), "before Android 10");
            Assert.That(DeviceThermal.none.status, Is.EqualTo(-1));
            Assert.That(float.IsNaN(DeviceThermal.none.celsius));
        }

        [Test]
        public void TheOverlaysCrossTurnsItOff()
        {
            PerfOverlay.shown = true;
            var settings = OpenSettings();
            var overlay = m_App.Q<PerfOverlay>();

            simulate.Click(overlay.closeButton);
            simulate.FrameUpdate();

            // (Had the window taken the pointer for a drag, the button would never have seen its click.)
            Assert.That(PerfOverlay.shown, Is.False);
            Assert.That(overlay.window.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(settings.perfOverlayToggle.value, Is.False, "the setting follows");
            Assert.That(overlay.window.ClassListContains(PerfOverlay.draggingUssClassName), Is.False);
        }

        // z-index: auto is drawn in the order of the elements, under every number.
        static int ZIndex(VisualElement element)
        {
            var z = element.resolvedStyle.zIndex;
            return z.keyword == StyleKeyword.Undefined ? z.value : int.MinValue;
        }

        [Test]
        public void TheOverlayIsOverLevelUpAndTheDialogs()
        {
            PerfOverlay.shown = true;
            OpenSettings();
            var overlay = m_App.Q<PerfOverlay>();
            var levelUp = m_App.Q<LevelUpDialog>();

            // Drawn over every other layer: LEVEL UP! and the dialogs are in one at z-index 50. (Picking goes by the
            // order of the elements, where the overlay was already last, so it can't tell.)
            var over = m_App.Query<VisualElement>().Where(e => e != overlay && ZIndex(e) != int.MinValue && ZIndex(e) >= ZIndex(overlay));
            Assert.That(over.ToList().Select(e => e.name), Is.Empty);
            Assert.That(ZIndex(m_App.Q("overlay-layer")), Is.LessThan(ZIndex(overlay)));

            // Its × works over LEVEL UP!, without the dialog getting the tap.
            levelUp.Show(m_App.state.shopStates[0], 50);
            simulate.FrameUpdateMs(600);
            simulate.Click(overlay.closeButton);
            Assert.That(PerfOverlay.shown, Is.False);
            Assert.That(levelUp.isOpen, Is.True);
        }

        [Test]
        public void TheOverlayDragsAroundInsideTheScreenWithoutTurningThePage()
        {
            PerfOverlay.shown = true;
            OpenSettings();
            var overlay = m_App.Q<PerfOverlay>();
            var window = overlay.window;
            var start = window.layout.position;
            var grab = window.worldBound.center;

            simulate.MouseDown(grab);
            simulate.MouseMove(grab + new Vector2(60, 0));
            simulate.MouseMove(grab + new Vector2(120, 200));
            simulate.MouseUp(grab + new Vector2(120, 200));
            simulate.FrameUpdateMs(500);

            Assert.That(window.layout.position.x, Is.EqualTo(start.x + 120).Within(1f));
            Assert.That(window.layout.position.y, Is.EqualTo(start.y + 200).Within(1f));
            Assert.That(m_App.currentPage, Is.EqualTo(AppPage.Settings), "a sideways drag on the window isn't a swipe");

            // Way past the bottom right corner: it stops at the edges.
            grab = window.worldBound.center;
            simulate.MouseDown(grab);
            simulate.MouseMove(grab + new Vector2(2000, 3000));
            simulate.MouseUp(grab + new Vector2(2000, 3000));
            simulate.FrameUpdate();

            Assert.That(window.layout.xMax, Is.EqualTo(overlay.layout.width).Within(1f));
            Assert.That(window.layout.yMax, Is.EqualTo(overlay.layout.height).Within(1f));
        }

        [Test]
        public void TheNoFiltersAndNoAnimationsSwitchesAddAndRemoveAStyleSheet()
        {
            var settings = OpenSettings();
            var blur = settings.Q("settings-glass");
            var page = m_App.GetPage(AppPage.Settings);
            Assert.That(m_App.styleSheets.Contains(m_App.noFiltersStyleSheet), Is.False);
            Assert.That(blur.resolvedStyle.backdropFilter.Count(), Is.EqualTo(1), "the glass blurs");

            settings.noFiltersToggle.value = true;
            settings.noAnimationsToggle.value = true;
            simulate.FrameUpdate();
            Assert.That(m_App.styleSheets.Contains(m_App.noFiltersStyleSheet), Is.True, "one style sheet on the app root");
            Assert.That(m_App.styleSheets.Contains(m_App.noAnimationsStyleSheet), Is.True);
            Assert.That(blur.resolvedStyle.backdropFilter.Count(), Is.EqualTo(0), "no filter anywhere");
            Assert.That(page.resolvedStyle.transitionDuration.First().value, Is.EqualTo(0f), "no transition anywhere");
            Assert.That(ArtKit.MotionAllowed(m_App.Q("particles")), Is.False, "the code-drawn art holds still");

            settings.noFiltersToggle.value = false;
            settings.noAnimationsToggle.value = false;
            simulate.FrameUpdate();
            Assert.That(m_App.styleSheets.Contains(m_App.noFiltersStyleSheet), Is.False);
            Assert.That(m_App.styleSheets.Contains(m_App.noAnimationsStyleSheet), Is.False);
            Assert.That(blur.resolvedStyle.backdropFilter.Count(), Is.EqualTo(1));
            Assert.That(ArtKit.MotionAllowed(m_App.Q("particles")), Is.True);
        }

        [Test]
        public void TheShadowSwitchSaysWhatItSwitches()
        {
            var settings = OpenSettings();

            Assert.That(settings.filterShadowsToggle.label, Is.EqualTo("Blurred drop-shadows on panes"));
            Assert.That(settings.Q("performance-panel").Contains(settings.filterShadowsToggle), Is.True, "with the other performance settings");
            Assert.That(settings.boxShadowsToggle.label, Is.EqualTo("Blurred box-shadows on panes"), "the A/B's other side, right after it");
            Assert.That(settings.boxShadowsToggle.parent.IndexOf(settings.boxShadowsToggle),
                Is.EqualTo(settings.filterShadowsToggle.parent.IndexOf(settings.filterShadowsToggle) + 1));
        }
    }
}
