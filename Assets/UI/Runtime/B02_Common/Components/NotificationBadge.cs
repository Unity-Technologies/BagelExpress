using System.Globalization;
using Unity.Properties;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// UI Component: the red mobile-game badge on any element. Its count is
    /// data-bindable from UXML with property="${component:NotificationBadge}.count".
    /// </summary>
    [VisualElementComponent]
    public partial struct NotificationBadge : IVisualElementComponent
    {
        public const string ussClassName = "be-badge";
        public static readonly UniqueStyleString popUssClassName = new("be-badge--pop");

        [UxmlAttribute, CreateProperty] public int count;

        Label m_Label;
        int m_ShownCount;

        [OnComponentAdded]
        static void OnAdded(ref NotificationBadge self, VisualElement owner)
        {
            self.m_Label = new Label { pickingMode = PickingMode.Ignore };
            self.m_Label.AddToClassList(ussClassName);
            owner.hierarchy.Add(self.m_Label);
            // In UXML, authored values are written after Added: refresh once they are in.
            owner.MarkComponentDirty<NotificationBadge>();
        }

        [OnComponentChanged]
        static void OnChanged(ref NotificationBadge self, VisualElement owner)
        {
            if (self.m_Label == null)
                return;

            self.m_Label.text = self.count > 99 ? "99+" : self.count.ToString(CultureInfo.InvariantCulture);
            self.m_Label.style.display = self.count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            self.m_Label.BringToFront();

            // Pop (a UIAnimationClip on the class) when the count goes up.
            if (self.count > self.m_ShownCount)
            {
                var label = self.m_Label;
                label.RemoveFromClassList(popUssClassName);
                label.schedule.Execute(() => label.AddToClassList(popUssClassName)).ExecuteLater(16);
            }
            self.m_ShownCount = self.count;
        }

        [OnComponentRemoved]
        static void OnRemoved(ref NotificationBadge self, VisualElement owner)
        {
            self.m_Label?.RemoveFromHierarchy();
            self.m_Label = null;
        }
    }
}
