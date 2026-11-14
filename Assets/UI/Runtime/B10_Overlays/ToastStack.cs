using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>Mobile-style toasts that drop in from the top and fade away.</summary>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class ToastStack : VisualElement
    {
        public static readonly string ussClassName = "be-toast-stack";
        public static readonly string toastUssClassName = "be-toast";
        public static readonly UniqueStyleString toastStagedUssClassName = new("be-toast--staged");
        public static readonly UniqueStyleString toastLeavingUssClassName = new("be-toast--leaving");

        const long k_ShowMs = 1800;
        const long k_LeaveMs = 300;

        public ToastStack()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
        }

        public void Show(string message)
        {
            var toast = new Label(message) { pickingMode = PickingMode.Ignore };
            toast.AddToClassList(toastUssClassName);
            toast.AddToClassList(toastStagedUssClassName);
            Add(toast);

            // Remove the staged class after first paint so the entrance transition plays.
            toast.schedule.Execute(() => toast.RemoveFromClassList(toastStagedUssClassName)).ExecuteLater(30);
            toast.schedule.Execute(() => toast.AddToClassList(toastLeavingUssClassName)).ExecuteLater(k_ShowMs);
            // Gone for good: its resources go back to be reused now, not when the GC finds it.
            toast.schedule.Execute(() => Discard.Element(toast)).ExecuteLater(k_ShowMs + k_LeaveMs);
        }
    }
}
