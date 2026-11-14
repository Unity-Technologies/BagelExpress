using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// Keeps USS <c>animation:</c> clips moving in players. A UIAnimationClip wraps an AnimationClip, and UI Toolkit
    /// samples that clip's raw curves. A player build strips the raw curves of every non-legacy AnimationClip (only
    /// Mecanim's compiled data ships), so in 6000.7.0b6 the animations play in the Editor and silently do nothing on a
    /// device. Legacy clips keep their curves. The library's clips are legacy (UIAnimationClipAuthoring); clips made
    /// from the Inspector or the Animation window are not, so they are switched before every build.
    /// </summary>
    sealed class UIAnimationClipBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => MakeClipsLegacy();

        [MenuItem("Bagel/Dev/Make UI Animation Clips Playable in Players", editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static int MakeClipsLegacy()
        {
            var changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:UIAnimationClip"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is not AnimationClip clip || clip.legacy)
                        continue;
                    clip.legacy = true;
                    EditorUtility.SetDirty(clip);
                    changed++;
                    Debug.Log($"[Bagel] {path}: made the clip legacy so players keep its curves.");
                }
            }
            if (changed > 0)
                AssetDatabase.SaveAssets();
            return changed;
        }
    }
}
