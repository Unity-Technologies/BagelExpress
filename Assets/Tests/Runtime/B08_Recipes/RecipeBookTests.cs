using NUnit.Framework;
using UnityEngine.UIElements;

namespace Bagel.B08_Recipes
{
    public class RecipeBookTests : BakeryUITestFixture
    {
        RecipesPaneManager m_Recipes;

        [SetUp]
        public void SetUp()
        {
            m_Recipes = Show(assets.recipesPageUxml).Q<RecipesPaneManager>();
        }

        [Test]
        public void GridShowsAllTwelveRecipesWithTheURCardSpanningTwoByTwo()
        {
            Assert.That(m_Recipes.grid.resolvedStyle.display, Is.EqualTo(DisplayStyle.Grid));
            Assert.That(m_Recipes.grid.childCount, Is.EqualTo(12));

            Assert.That(m_Recipes.grid[0].Q<RecipeCard>().rarity, Is.EqualTo(Rarity.UR));
            var featured = m_Recipes.grid[0].layout;
            var regular = m_Recipes.grid[1].layout;
            var gap = m_Recipes.grid.resolvedStyle.columnGap;
            Assert.That(featured.width, Is.EqualTo(2 * regular.width + gap).Within(1f));
            Assert.That(featured.height, Is.GreaterThan(2 * regular.height));
        }

        [Test]
        public void TappingACardOpensItsDetailSheet()
        {
            var card = m_Recipes.grid[0];

            simulate.Click(card);

            Assert.That(m_Recipes.ClassListContains(RecipesPaneManager.sheetOpenUssClassName), Is.True);
            Assert.That(m_Recipes.sheet.dataSource, Is.SameAs(card.dataSource));
        }
    }
}
