using Unity.Properties;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// One bakery row in the Shops list. Its bindable bool properties only toggle
    /// USS classes, so all visual states (locked, affordable, milestone, city) are
    /// pure USS, driven by bindings declared in ShopCard.uxml.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Shops")]
    public partial class ShopCard : VisualElement
    {
        public static readonly string ussClassName = "be-shop-card";
        public static readonly UniqueStyleString lockedUssClassName = new(ussClassName + "--locked");
        public static readonly UniqueStyleString affordableUssClassName = new(ussClassName + "--affordable");
        public static readonly UniqueStyleString milestoneUssClassName = new(ussClassName + "--milestone-next");
        public static readonly UniqueStyleString japanUssClassName = new(ussClassName + "--japan");
        public static readonly UniqueStyleString upgradedUssClassName = new(ussClassName + "--upgraded");

        Button m_ActionButton;
        bool m_Locked;
        bool m_Affordable;
        bool m_MilestoneNext;
        Origin m_Origin;

        public Button actionButton => m_ActionButton ??= this.Q<Button>("action-button");

        [UxmlAttribute, CreateProperty]
        public bool locked
        {
            get => m_Locked;
            set { m_Locked = value; EnableInClassList(lockedUssClassName, value); }
        }

        [UxmlAttribute, CreateProperty]
        public bool affordable
        {
            get => m_Affordable;
            set { m_Affordable = value; EnableInClassList(affordableUssClassName, value); actionButton?.SetEnabled(value); }
        }

        [UxmlAttribute, CreateProperty]
        public bool milestoneNext
        {
            get => m_MilestoneNext;
            set { m_MilestoneNext = value; EnableInClassList(milestoneUssClassName, value); }
        }

        [UxmlAttribute, CreateProperty]
        public Origin origin
        {
            get => m_Origin;
            set { m_Origin = value; EnableInClassList(japanUssClassName, value == Origin.Japan); }
        }

        public ShopCard()
        {
            AddToClassList(ussClassName);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                actionButton?.UnregisterCallback<ClickEvent>(OnAction);
                actionButton?.RegisterCallback<ClickEvent>(OnAction);
            });
        }

        void OnAction(ClickEvent evt)
        {
            if (!DataSourceUtility.TryResolve(this, out ShopState shop))
                return;
            GetFirstAncestorOfType<ShopsPaneManager>()?.Act(shop, this);
        }

        /// <summary>Little celebration after an upgrade (a UIAnimationClip on the class).</summary>
        public void PlayUpgraded()
        {
            RemoveFromClassList(upgradedUssClassName);
            schedule.Execute(() => AddToClassList(upgradedUssClassName)).ExecuteLater(16);
            schedule.Execute(() => RemoveFromClassList(upgradedUssClassName)).ExecuteLater(600);
        }
    }
}
