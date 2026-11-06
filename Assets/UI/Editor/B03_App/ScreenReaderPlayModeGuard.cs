using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Editor-only safety net for live demos. With Enter Play Mode Options (no domain or scene
    /// reload), the experimental screen reader support registers the app's PanelRenderer as Play
    /// mode starts, but the Editor then recreates the panel under it, and the support never notices
    /// the move: the Accessibility Hierarchy Viewer stays empty. Once Play mode has started, turn
    /// it off and on again, so it registers the panel the app actually runs in.
    /// </summary>
    static class ScreenReaderPlayModeGuard
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.EnteredPlayMode)
                    EditorApplication.delayCall += Reregister;
            };
        }

        static void Reregister()
        {
            // UITKAccessibilityBridge.featureEnabled (internal): set from Project Settings › UI Toolkit ›
            // Accessibility as Play mode starts; turning it on registers the live panels again.
            var featureEnabled = typeof(VisualElement).Assembly
                .GetType("UnityEngine.UIElements.UITKAccessibilityBridge")
                ?.GetProperty("featureEnabled", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (!EditorApplication.isPlaying || featureEnabled == null || !(bool)featureEnabled.GetValue(null))
                return;
            featureEnabled.SetValue(null, false);
            featureEnabled.SetValue(null, true);
        }
    }
}
