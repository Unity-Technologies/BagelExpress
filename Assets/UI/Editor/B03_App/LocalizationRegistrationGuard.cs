using System.Reflection;
using Unity.Localization;
using Unity.Localization.Editor;
using Unity.Localization.Providers;
using UnityEditor;

namespace Bagel
{
    /// <summary>
    /// Editor-only safety net for live demos. The "Direct references" provider keeps its table
    /// registrations on the in-memory LocalizationSettings; after heavy asset churn they have been
    /// seen to vanish (the asset on disk is fine), which makes every localized text blank in Play
    /// mode. Before entering Play mode, re-register the tables if BagelStrings is missing.
    /// </summary>
    static class LocalizationRegistrationGuard
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode)
                    EnsureRegistered();
            };
        }

        [MenuItem("Bagel/Localization/Re-register String Tables", editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        static void EnsureRegistered()
        {
            if (!EditorBuildSettings.TryGetConfigObject(LocalizationSettings.ConfigName, out LocalizationSettings settings) || settings == null)
                return;
            var direct = settings.Database?.AssetProvider?.GetProvider<ReferencedAssetProvider>();
            if (direct == null || direct.Contains(BagelLocalization.table + "_en"))
                return;

            // The module's own "rebuild registrations" step (internal API).
            typeof(ResourceTableCollection).Assembly
                .GetType("Unity.Localization.Editor.AssetProviderEditors")
                ?.GetMethod("RebuildRegistrations", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(null, null);
        }
    }
}
