using NUnit.Framework;
using UnityEngine.UIElements;

namespace Bagel.B12_Tutorial
{
    public class TutorialOverlayTests : BakeryUITestFixture
    {
        [Test]
        public void TappingTheSpotlitTargetAdvancesTheTutorial()
        {
            var app = Show(assets.appScreenUxml).Q<BakeryAppManager>();
            app.showTitle = false;
            simulate.FrameUpdate();
            var overlay = app.Q<TutorialOverlay>();
            var collect = app.Q<HomePaneManager>().collectButton;
            var advanced = false;

            overlay.Show(collect, "Tap COLLECT to bank your coins!", "TIP 1/3", () => advanced = true);
            simulate.FrameUpdate();
            Assert.That(overlay.isActive, Is.True);

            simulate.Click(collect);
            simulate.FrameUpdateMs(500);

            Assert.That(overlay.isActive, Is.False);
            Assert.That(advanced, Is.True);
        }

        [Test]
        public void TheSkipButtonEndsTheTutorial()
        {
            var app = Show(assets.appScreenUxml).Q<BakeryAppManager>();
            app.showTitle = false;
            simulate.FrameUpdate();
            var overlay = app.Q<TutorialOverlay>();
            var collect = app.Q<HomePaneManager>().collectButton;
            var advanced = false;
            var skipped = false;

            overlay.Show(collect, "Tap COLLECT to bank your coins!", "TIP 1/3", () => advanced = true, () => skipped = true);
            simulate.FrameUpdate();
            simulate.Click(overlay.skipButton);
            simulate.FrameUpdateMs(500);

            Assert.That(skipped, Is.True);
            Assert.That(advanced, Is.False);
            Assert.That(overlay.isActive, Is.False);
            Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }
    }
}
