using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// For UI that's made again rather than shown again (a draw's cards, a rebuilt grid, a toast, an editor window's
    /// copy of a page): out of the panel now, then <c>Clear(VisualElementClearOptions.RecursiveReleaseResources)</c> and
    /// <c>ReleaseResources()</c> give its layout nodes, styles and text back to be reused right away, instead of when the
    /// GC finds the elements. Never for UI that's kept to be shown again: a released element can't be used.
    /// </summary>
    /// <remarks>
    /// The release waits for the panel's next update: until then the panel can still send a pointer leave to what was
    /// under the pointer, and a released element throws. UI that was never in a panel is released at once.
    /// </remarks>
    public static class Discard
    {
        /// <summary>Removes <paramref name="parent"/>'s children for good.</summary>
        public static void Children(VisualElement parent)
        {
            if (parent == null || parent.childCount == 0)
                return;
            var children = new List<VisualElement>(parent.Children());
            var panel = parent.panel;
            parent.Clear();
            if (panel == null)
                children.ForEach(Release);
            else
                panel.visualTree.schedule.Execute(() => children.ForEach(Release));
        }

        /// <summary>Removes <paramref name="element"/> from its parent for good, and everything under it.</summary>
        public static void Element(VisualElement element)
        {
            if (element == null || element.resourcesReleased)
                return;
            var panel = element.panel;
            element.RemoveFromHierarchy();
            if (panel == null)
                Release(element);
            else
                panel.visualTree.schedule.Execute(() => Release(element));
        }

        static void Release(VisualElement element)
        {
            // Released already, or put back somewhere in the meantime: leave it.
            if (element.resourcesReleased || element.hierarchy.parent != null)
                return;
            element.hierarchy.Clear(VisualElementClearOptions.RecursiveReleaseResources);
            element.ReleaseResources();
        }
    }
}
