using NUnit.Framework;
using UnityEngine.UIElements;

namespace Bagel.B07_LuckyBag
{
    public class LuckyBagTests : BakeryUITestFixture
    {
        [Test]
        public void FirstDrawSpendsATicketAndRevealsTheGuaranteedSSR()
        {
            var luckyBag = Show(assets.luckyBagPageUxml).Q<LuckyBagPaneManager>();
            var tickets = luckyBag.state.tickets;

            simulate.Click(luckyBag.drawOneButton);
            Assert.That(luckyBag.state.tickets, Is.EqualTo(tickets - 1));

            simulate.FrameUpdateMs(1000); // the bag shakes, then the results open
            simulate.FrameUpdate();

            Assert.That(luckyBag.ClassListContains(LuckyBagPaneManager.resultsOpenUssClassName), Is.True);
            var card = luckyBag.resultsRow.Q<RecipeCard>();
            Assert.That(card.rarity, Is.EqualTo(Rarity.SSR));
            Assert.That(card.isNew, Is.True);
            Assert.That(DataSourceUtility.TryResolve(card, out RecipeState recipe), Is.True);
            Assert.That(recipe.id, Is.EqualTo("sakura-mochi-bagel"));
        }

        [Test]
        public void TappingTheBagDrawsOne()
        {
            var luckyBag = Show(assets.luckyBagPageUxml).Q<LuckyBagPaneManager>();
            var tickets = luckyBag.state.tickets;

            simulate.Click(luckyBag.bag);
            Assert.That(luckyBag.state.tickets, Is.EqualTo(tickets - 1));

            simulate.FrameUpdateMs(1000);
            simulate.FrameUpdate();

            Assert.That(luckyBag.ClassListContains(LuckyBagPaneManager.resultsOpenUssClassName), Is.True);
            Assert.That(luckyBag.resultsRow.Query<RecipeCard>().ToList(), Has.Count.EqualTo(1));
        }
    }
}
