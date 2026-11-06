using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditorInternal;

namespace Bagel
{
    /// <summary>
    /// Before a player build, writes the Editor's full version (with its revision) and the time into BagelBuildInfo,
    /// for Settings › About. The version only changes when the Editor does, and the date is cleared once the build is
    /// done (or, after a failed build, the next time scripts load), so builds don't change the committed asset.
    /// </summary>
    sealed class BagelBuildInfoStamp : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Stamp();
            SetBuildDate(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        }

        public void OnPostprocessBuild(BuildReport report) => SetBuildDate(string.Empty);

        // A failed build never gets to OnPostprocessBuild.
        [InitializeOnLoadMethod]
        static void ClearLeftoverDate() => EditorApplication.delayCall += () =>
        {
            if (!BuildPipeline.isBuildingPlayer)
                SetBuildDate(string.Empty);
        };

        /// <summary>Points the asset at the running Editor; returns whether it changed.</summary>
        public static bool Stamp() => Write(info => info.fullUnityVersion, (info, value) => info.fullUnityVersion = value,
            InternalEditorUtility.GetFullUnityVersion());

        static void SetBuildDate(string date) => Write(info => info.buildDate, (info, value) => info.buildDate = value, date);

        static bool Write(Func<BagelBuildInfo, string> get, Action<BagelBuildInfo, string> set, string value)
        {
            var changed = false;
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(BagelBuildInfo)))
            {
                var info = AssetDatabase.LoadAssetAtPath<BagelBuildInfo>(AssetDatabase.GUIDToAssetPath(guid));
                if (info == null || (get(info) ?? string.Empty) == value)
                    continue;
                set(info, value);
                EditorUtility.SetDirty(info);
                AssetDatabase.SaveAssetIfDirty(info);
                changed = true;
            }
            return changed;
        }
    }
}
