using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// UI Component: gives any element a squishy "mochi" press. Attach it in UXML
    /// (&lt;Bagel.PressSquish pressed-scale="0.9" /&gt;), in UI Builder, or from C#,
    /// without subclassing the element.
    /// </summary>
    [VisualElementComponent]
    public partial struct PressSquish
    {
        [UxmlAttribute] public float pressedScale;

        [UxmlCreateInstanceMethod]
        internal static PressSquish CreateDefault() => new PressSquish { pressedScale = 0.9f };

        // TrickleDown: Buttons stop PointerDown propagation in the bubble phase.
        [RegisterCallback(typeof(PointerDownEvent), CallbackOptions.TrickleDown)]
        static void OnPointerDown(ref PressSquish self, VisualElement owner, PointerDownEvent evt)
        {
            var scale = self.pressedScale > 0f ? self.pressedScale : 0.9f;
            owner.style.scale = new Scale(new Vector3(scale + 0.06f, scale, 1f));
        }

        [RegisterCallback(typeof(PointerUpEvent), CallbackOptions.TrickleDown)]
        [RegisterCallback(typeof(PointerLeaveEvent))]
        [RegisterCallback(typeof(PointerCancelEvent))]
        [RegisterCallback(typeof(PointerCaptureOutEvent))] // a swipe took the finger (PageSwipe)
        static void OnRelease(ref PressSquish self, VisualElement owner, EventBase evt)
        {
            owner.style.scale = StyleKeyword.Null;
        }

        [OnComponentRemoved]
        static void OnRemoved(ref PressSquish self, VisualElement owner)
        {
            owner.style.scale = StyleKeyword.Null;
        }
    }
}
