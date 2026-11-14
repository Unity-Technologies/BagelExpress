using Unity.Localization;
using Unity.Properties;
using Unity.SmartStrings.PersistentVariables;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Shops page. The list's data is 100% declarative: a ListView whose itemsSource is
    /// bound to BakeryState.shops in UXML, with AutoAssign item data sources and an
    /// item template full of bindings. This class handles the button actions, the
    /// "2 bakeries open" Smart String, and marks the first and last items, which hold
    /// the space that keeps them clear of the glass (the list virtualizes its content's
    /// padding, so that space can't be padding there).
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class ShopsPaneManager : BakeryPane
    {
        public static readonly string ussClassName = "be-shops";
        public static readonly UniqueStyleString firstItemUssClassName = new("be-shops__item--first");
        public static readonly UniqueStyleString lastItemUssClassName = new("be-shops__item--last");

        ListView m_ShopList;
        Label m_OpenCountLabel;

        // Smart String "{count:plural(en):1 bakery|{} bakeries} open"; its variable is pushed from code.
        readonly IntVariable m_OpenCount = new();
        readonly LocalizedString m_OpenCountText = BagelLocalization.String("shops.open-count");

        public ListView shopList => m_ShopList ??= this.Q<ListView>("shop-list");
        public Label openCountLabel => m_OpenCountLabel ??= this.Q<Label>("open-count");

        /// <summary>Data-bound to BakeryState.openShopCount; feeds the Smart String's {count}.</summary>
        [CreateProperty]
        public int openCount
        {
            get => m_OpenCount.Value;
            set
            {
                if (m_OpenCount.Value == value)
                    return;
                m_OpenCount.Value = value;
                m_OpenCountText.RefreshString();
            }
        }

        public ShopsPaneManager()
        {
            AddToClassList(ussClassName);
            m_OpenCountText.LocalVariables.Add(m_OpenCount, "count");
            SetBinding(nameof(openCount), new DataBinding
            {
                dataSourcePath = new PropertyPath(nameof(BakeryState.openShopCount)),
                bindingMode = BindingMode.ToTarget
            });
        }

        protected override void OnAttached()
        {
            openCountLabel?.SetBinding("text", m_OpenCountText);
            if (shopList != null)
            {
                // The data source is still auto-assigned: bindItem only marks the ends of the list.
                shopList.bindItem = MarkEnds;
                shopList.unbindItem = (item, _) => MarkEnds(item, -1);
                shopList.RefreshItems();
            }
        }

        void MarkEnds(VisualElement item, int index)
        {
            var count = shopList.itemsSource?.Count ?? 0;
            item.EnableInClassList(firstItemUssClassName, index == 0);
            item.EnableInClassList(lastItemUssClassName, index >= 0 && index == count - 1);
        }

        public void Act(ShopState shop, ShopCard card)
        {
            if (state == null || shop == null)
                return;

            var index = IndexOf(shop);
            if (shop.locked)
            {
                if (state.TryUnlockShop(index))
                {
                    BagelSound.Play(card, "unlock");
                    Toast(BagelLocalization.Get("toast.shop-opened", shop.displayName));
                    card.PlayUpgraded();
                }
                else
                {
                    BagelSound.Play(card, "fail");
                    Toast(BagelLocalization.Get("toast.not-enough-coins"));
                }
                return;
            }

            if (state.TryUpgradeShop(index, out var milestone))
            {
                card.PlayUpgraded();
                // A milestone opens LEVEL UP!, which has its own fanfare.
                if (!milestone)
                {
                    BagelSound.Play(card, "upgrade");
                    Toast(BagelLocalization.Get("toast.shop-upgraded", shop.displayName, shop.level));
                }
            }
            else
            {
                BagelSound.Play(card, "fail");
                Toast(BagelLocalization.Get(shop.isMaxLevel ? "toast.max-level" : "toast.not-enough-coins"));
            }
        }

        int IndexOf(ShopState shop)
        {
            for (int i = 0; i < state.shopStates.Count; i++)
                if (state.shopStates[i] == shop)
                    return i;
            return -1;
        }
    }
}
