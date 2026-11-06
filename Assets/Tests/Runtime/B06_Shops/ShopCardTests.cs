using NUnit.Framework;
using UnityEngine.UIElements;

namespace Bagel.B06_Shops
{
    public class ShopCardTests : BakeryUITestFixture
    {
        [Test]
        public void UpgradingMileEndToLevel5CelebratesTheMilestone()
        {
            var app = Show(assets.appScreenUxml).Q<BakeryAppManager>();
            app.showTitle = false;
            app.GoTo(AppPage.Shops);
            simulate.FrameUpdateMs(400); // slide in
            simulate.FrameUpdate();      // the ListView creates and binds its cards

            var state = app.state;
            var mileEnd = state.shopStates[0];
            var (coins, gems, cost) = (state.coins, state.gems, mileEnd.upgradeCost);
            Assert.That(mileEnd.level, Is.EqualTo(4));

            var card = app.Q<ShopsPaneManager>().shopList.GetRootElementForIndex(0).Q<ShopCard>();
            simulate.Click(card.actionButton);

            Assert.That(mileEnd.level, Is.EqualTo(5));
            Assert.That(state.coins, Is.EqualTo(coins - cost));
            Assert.That(state.gems, Is.EqualTo(gems + 50));
            Assert.That(app.Q<LevelUpDialog>().isOpen, Is.True);
        }

        [Test]
        public void LockedShopButtonIsDisabledUntilAffordable()
        {
            var state = NewState();
            var shibuya = state.shopStates[2];
            var card = Show(assets.shopCardUxml, shibuya).Q<ShopCard>();

            Assert.That(card.locked, Is.True);
            Assert.That(card.actionButton.enabledSelf, Is.False, $"{state.coins} < {shibuya.unlockCost}");

            state.coins = shibuya.unlockCost;
            simulate.FrameUpdate();

            Assert.That(card.actionButton.enabledSelf, Is.True);
        }
    }
}
