using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>Title screen: logo, mascot and "tap to start". Dismissal is handled by the app.</summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class TitleScreenManager : VisualElement
    {
        public static readonly string ussClassName = "be-title";

        public TitleScreenManager()
        {
            AddToClassList(ussClassName);
            EditorPreviewHooks.WatchStandalone(this);
        }
    }
}
