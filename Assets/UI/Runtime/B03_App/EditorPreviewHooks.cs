using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Lets the editor style and stage the game's UI wherever the Editor shows it: the UI Builder, the UI Viewport, the
    /// Scene view's UI preview, the scene's own panel. The demo toolbar (DemoOverrides, editor code) uses it to force a
    /// screen, a season and a language there without changing the UXML, nor what Play mode and players start with. Its
    /// calls compile away in players. In Play mode it reports roots too, the UI Builder's and the UI Viewport's (which
    /// show clones of the UXML in panels of their own): the editor code leaves the game's own panel alone then.
    /// </summary>
    public static class EditorPreviewHooks
    {
        /// <summary>While set (the UI tests), roots aren't reported.</summary>
        public static bool paused { get; set; }

#if UNITY_EDITOR
        static readonly List<WeakReference<VisualElement>> s_Roots = new();
        // The roots last attached in Play mode, of those above.
        static readonly ConditionalWeakTable<VisualElement, object> s_AttachedInPlayMode = new();

        /// <summary>
        /// A document's root attached: the app, or a page, the title or a dialog on its own. In Play mode too (see
        /// <see cref="attachedInPlayMode"/>).
        /// </summary>
        public static event Action<VisualElement> rootAttached;

        /// <summary>Whether the root last attached in Play mode.</summary>
        public static bool attachedInPlayMode(VisualElement root) => root != null && s_AttachedInPlayMode.TryGetValue(root, out _);

        /// <summary>The roots reported so far that are still in a panel, for editor code that changes them later.</summary>
        public static List<VisualElement> roots
        {
            get
            {
                var alive = new List<VisualElement>();
                s_Roots.RemoveAll(reference => !reference.TryGetTarget(out var root) || root.panel == null);
                foreach (var reference in s_Roots)
                    if (reference.TryGetTarget(out var root))
                        alive.Add(root);
                return alive;
            }
        }
#endif

        /// <summary>
        /// Whether the root attached outside Play mode and is still in its panel: in Play mode, a scene's UI the Editor
        /// built before it (Play mode doesn't reload the scene in this project), maybe staged by the demo toolbar.
        /// </summary>
        public static bool AttachedOutsidePlayMode(VisualElement root)
        {
#if UNITY_EDITOR
            return root != null && roots.Contains(root) && !attachedInPlayMode(root);
#else
            return false;
#endif
        }

        /// <summary>A document's root calls this when it attaches to a panel.</summary>
        [Conditional("UNITY_EDITOR")]
        public static void Attached(VisualElement root)
        {
#if UNITY_EDITOR
            if (paused || root == null)
                return;
            s_AttachedInPlayMode.Remove(root);
            if (Application.isPlaying)
                s_AttachedInPlayMode.Add(root, null);
            var known = false;
            foreach (var reference in s_Roots)
                known |= reference.TryGetTarget(out var seen) && seen == root;
            if (!known)
                s_Roots.Add(new WeakReference<VisualElement>(root));
            rootAttached?.Invoke(root);
#endif
        }

        /// <summary>
        /// For the title or a dialog: reports it when it attaches with no app around it (opened on its own). A dialog
        /// starts hidden until the game shows it (<paramref name="hiddenUssClassName"/>): opened on its own outside Play
        /// mode, in the UI Builder or a UXML preview, it shows its resting pose instead of an empty canvas.
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public static void WatchStandalone(VisualElement element, UniqueStyleString hiddenUssClassName = default)
        {
#if UNITY_EDITOR
            element.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (element.GetFirstAncestorOfType<BakeryAppManager>() != null)
                    return;
                if (!hiddenUssClassName.IsNull && !Application.isPlaying && !paused)
                    element.RemoveFromClassList(hiddenUssClassName);
                Attached(element);
            });
#endif
        }
    }
}
