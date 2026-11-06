using NUnit.Framework;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Bagel.B02_Common
{
    public class CurrencyPillTests : BakeryUITestFixture
    {
        [Test]
        public void PillCountsUpToItsBoundValue()
        {
            var state = NewState();
            var pill = new CurrencyPill { kind = CurrencyKind.Coins, dataSource = state };
            pill.SetBinding(nameof(CurrencyPill.value), ToTarget(nameof(BakeryState.coins)));
            rootVisualElement.Add(pill);
            var label = pill.Q<TextElement>(className: CurrencyPill.valueUssClassName);

            simulate.FrameUpdate();
            Assert.That(label.text, Is.EqualTo("1,250"), "the first bound value snaps");

            state.coins = 100_000;
            simulate.FrameUpdate(); // the binding starts the count-up
            simulate.FrameUpdate(); // 200 ms into the 0.6 s count
            Assert.That(label.text, Is.Not.EqualTo("1,250").And.Not.EqualTo("100,000"), "still counting up");

            simulate.FrameUpdateMs(1000);
            Assert.That(label.text, Is.EqualTo("100,000"));
        }

        [Test]
        public void NotificationBadgeShowsAndHidesWithItsBoundCount()
        {
            var state = NewState();
            var button = new Button { text = "Lucky Bag", dataSource = state };
            button.AddComponent(new NotificationBadge());
            button.SetBinding("${component:NotificationBadge}.count", ToTarget(nameof(BakeryState.tickets)));
            rootVisualElement.Add(button);
            simulate.FrameUpdate();

            var badge = button.Q<Label>(className: NotificationBadge.ussClassName);
            Assert.That(badge.text, Is.EqualTo("6"));
            Assert.That(badge.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            state.tickets = 0;
            simulate.FrameUpdate();

            Assert.That(badge.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
        }

        static DataBinding ToTarget(string path) =>
            new DataBinding { dataSourcePath = new PropertyPath(path), bindingMode = BindingMode.ToTarget };
    }
}
