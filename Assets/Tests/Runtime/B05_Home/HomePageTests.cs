using NUnit.Framework;
using UnityEngine.UIElements;

namespace Bagel.B05_Home
{
    public class HomePageTests : BakeryUITestFixture
    {
        HomePaneManager m_Home;

        [SetUp]
        public void SetUp()
        {
            m_Home = Show(assets.homePageUxml).Q<HomePaneManager>();
        }

        [Test]
        public void CollectMovesTheRegisterIntoCoins()
        {
            var state = m_Home.state;
            var coins = state.coins;
            var register = (long)state.register;
            Assert.That(register, Is.GreaterThan(0));

            simulate.Click(m_Home.collectButton);

            Assert.That(state.coins, Is.EqualTo(coins + register));
            Assert.That(state.register, Is.LessThan(1));
        }

        [Test]
        public void PressingCollectMovesNothingAroundIt()
        {
            var button = m_Home.collectButton;
            var buttonLayout = button.layout;
            var storefrontLayout = m_Home.storefront.layout;
            var center = button.worldBound.center;

            simulate.MouseDown(center);
            // The press starts the transition on this frame; the next ones run it.
            simulate.FrameUpdateMs(200);
            simulate.FrameUpdateMs(200);
            try
            {
                Assert.That(button.resolvedStyle.translate.y, Is.GreaterThan(0f), "pressed: it moves down");
                Assert.That(button.layout, Is.EqualTo(buttonLayout), "with a transform: its size stays");
                Assert.That(m_Home.storefront.layout, Is.EqualTo(storefrontLayout), "and nothing else moves");
            }
            finally
            {
                simulate.MouseUp(center);
            }
        }

        [Test]
        public void TappingTheStorefrontBakesByHand()
        {
            var state = m_Home.state;
            var coins = state.coins;

            simulate.Click(m_Home.storefront);
            simulate.Click(m_Home.storefront);

            Assert.That(state.coins, Is.EqualTo(coins + 2 * state.tapBakeValue));
            Assert.That(m_Home.popLayer.childCount, Is.EqualTo(2), "one +coins pop per tap");
        }
    }
}
