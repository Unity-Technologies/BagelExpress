using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Bagel
{
    /// <summary>
    /// The demo's resets, in the Bagel › Demo menu: the project's files back to the last commit, Main.unity open as saved,
    /// the Editor's windows and main toolbar (<see cref="DemoToolbar"/>) back to the demo's layout (both from files in the
    /// repository), and the Bagel windows and the demo toolbar's previews back to their start. Reset All does the four.
    /// </summary>
    static class DemoReset
    {
        public const string layoutPath = "Assets/Settings/Layouts/BagelExpressDemo.wlt";
        public const string toolbarPresetPath = "Assets/Settings/Layouts/BagelExpressToolbar.preset";
        public const string resetAllMenuPath = "Bagel/Demo/Reset All";
        public const string discardMenuPath = "Bagel/Demo/Discard Project Changes";
        public const string reloadSceneMenuPath = "Bagel/Demo/Reload Main Scene";
        public const string resetLayoutMenuPath = "Bagel/Demo/Reset Layout and Toolbar";
        public const string resetStatesMenuPath = "Bagel/Demo/Reset Bagel Project States";

        // Reset All's next step, kept through the script reload a discard can start.
        const string k_ResetAllKey = "Bagel.Demo.ResetAll";
        const string k_LayoutStep = "layout";

        [InitializeOnLoadMethod]
        static void ResumeResetAll()
        {
            if (SessionState.GetString(k_ResetAllKey, string.Empty) == k_LayoutStep)
                DemoOverrides.OnNextUpdate(ResetAllAfterDiscard);
        }

        /// <summary>
        /// The four resets, safest first: the files (so the scene, layout and toolbar preset are the committed ones), then
        /// Main.unity, then the layout (which rebuilds every window), then the Bagel windows that layout opened and the
        /// previews. A discard that changes scripts reloads them: the rest carries on after.
        /// </summary>
        [MenuItem(resetAllMenuPath, priority = 30, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void ResetAll()
        {
            SessionState.SetString(k_ResetAllKey, k_LayoutStep);
            // The scene is reloaded next, so the discard leaves it.
            AfterPlayMode(() =>
            {
                Discard(reloadScene: false);
                DemoOverrides.OnNextUpdate(ResetAllAfterDiscard);
            });
        }

        static void ResetAllAfterDiscard()
        {
            if (SessionState.GetString(k_ResetAllKey, string.Empty) != k_LayoutStep)
                return;
            SessionState.EraseString(k_ResetAllKey);
            ReloadMainScene();
            ResetLayout(ResetProjectStates);
        }

        /// <summary>
        /// The Bagel UI, Bagel Features and Bagel Demo windows' Reset (a new game, Bagel Features on the title, Bagel Demo on
        /// the talk at a glance), the first season and the project's language, and the demo toolbar's screen, season and
        /// language as authored. No window opens or closes.
        /// </summary>
        [MenuItem(resetStatesMenuPath, priority = 44, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void ResetProjectStates()
        {
            DemoOverrides.ResetToAuthored();
            BagelGuideWindow.ResetSeasonAndLanguage();
            foreach (var window in Resources.FindObjectsOfTypeAll<BagelExpressAllUIWindow>())
                window.ResetWindow();
            foreach (var window in Resources.FindObjectsOfTypeAll<BagelFeaturesWindow>())
                window.ResetWindow();
            foreach (var window in Resources.FindObjectsOfTypeAll<BagelDemoWindow>())
                window.ResetWindow();
        }

        /// <summary>
        /// Every change in the Unity project (this folder of the repository) is discarded for good, as at the last commit:
        /// changed and deleted files come back, new files are deleted (but not the ones git ignores: Library, UserSettings,
        /// builds). The Console lists what went. Then the assets are reimported and the open scene is reloaded from disk.
        /// Leaves Play mode first. No confirmation: with -automated the Editor can't show one.
        /// </summary>
        [MenuItem(discardMenuPath, priority = 41, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void DiscardChanges() => AfterPlayMode(() => Discard(reloadScene: true));

        /// <summary>
        /// Main.unity (<see cref="EditorLink.mainScenePath"/>) as saved, alone: opened if it isn't, read again from disk if
        /// it is, so edits that weren't saved go, and other scenes close, without asking to save. Leaves Play mode first.
        /// </summary>
        [MenuItem(reloadSceneMenuPath, priority = 42, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void ReloadMainScene() => AfterPlayMode(() => EditorSceneManager.OpenScene(EditorLink.mainScenePath, OpenSceneMode.Single));

        /// <summary>Runs <paramref name="action"/> now, or once the Editor is back in edit mode if it's playing.</summary>
        static void AfterPlayMode(Action action)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                action();
                return;
            }
            Action<PlayModeStateChange> afterPlayMode = null;
            afterPlayMode = change =>
            {
                if (change != PlayModeStateChange.EnteredEditMode)
                    return;
                EditorApplication.playModeStateChanged -= afterPlayMode;
                action();
            };
            EditorApplication.playModeStateChanged += afterPlayMode;
            EditorApplication.ExitPlaymode();
        }

        static void Discard(bool reloadScene)
        {
            var project = Path.GetDirectoryName(Application.dataPath);
            var (ok, changes) = Git(project, "status --porcelain -- .");
            if (!ok)
            {
                Debug.LogError("[Bagel] Demo reset: git couldn't list the changes. " + changes);
                return;
            }

            var count = changes.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            if (count > 0)
            {
                var (restored, output) = Git(project, "restore --source=HEAD --staged --worktree -- .");
                if (restored)
                    (restored, output) = Git(project, "clean --force -d -- .");
                if (!restored)
                {
                    Debug.LogError("[Bagel] Demo reset: git couldn't discard the changes. " + output);
                    return;
                }
                Debug.Log($"[Bagel] Demo reset: {count} changed file(s) discarded.\n{changes}");
            }
            else
            {
                Debug.Log("[Bagel] Demo reset: no changes in the project.");
            }

            AssetDatabase.Refresh();
            if (!reloadScene)
                return;
            // Edits in memory go too.
            var scene = EditorSceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(scene.path) && File.Exists(Path.Combine(project, scene.path)))
                EditorSceneManager.OpenScene(scene.path, OpenSceneMode.Single);
        }

        static (bool ok, string output) Git(string directory, string arguments)
        {
            try
            {
                var info = new ProcessStartInfo("git", arguments)
                {
                    WorkingDirectory = directory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(info);
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill();
                    return (false, "git took longer than 30 seconds.");
                }
                return (process.ExitCode == 0, process.ExitCode == 0 ? output.Result : error.Result + output.Result);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        /// <summary>
        /// The windows from the demo's layout (<see cref="layoutPath"/>), then the main toolbar from its preset
        /// (<see cref="toolbarPresetPath"/>), with the demo toolbar shown, and the Game view on Bagel 2x (perf)
        /// (<see cref="GameViewDeviceSizes.perfSize"/>; the layout file keeps its size as an index in a menu that differs on
        /// each computer). Loading a layout closes and rebuilds every window, so it runs after the menu's click is done.
        /// </summary>
        [MenuItem(resetLayoutMenuPath, priority = 43, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void ResetLayout() => ResetLayout(null);

        static void ResetLayout(Action done)
        {
            DemoOverrides.OnNextUpdate(() =>
            {
                if (!EditorUtility.LoadWindowLayout(layoutPath))
                {
                    Debug.LogWarning($"[Bagel] Layout reset: {layoutPath} didn't load (a window with unsaved changes can stop it).");
                    done?.Invoke();
                    return;
                }
                // The toolbar's window is a new one now, and so are the Bagel windows: they're built on the next update.
                DemoOverrides.OnNextUpdate(() =>
                {
                    ApplyToolbarPreset();
                    GameViewDeviceSizes.SelectInGameViews();
                    if (done != null)
                        DemoOverrides.OnNextUpdate(done);
                });
            });
        }

        /// <summary>The main toolbar's preset, through the Editor's own (internal) preset loading.</summary>
        static void ApplyToolbarPreset()
        {
            try
            {
                var editor = typeof(EditorWindow).Assembly;
                var window = editor.GetType("UnityEditor.MainToolbarWindow")?
                    .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as EditorWindow;
                var load = editor.GetType("UnityEditor.Overlays.OverlayPresetManager")?
                    .GetMethod("LoadFromFile", BindingFlags.Static | BindingFlags.NonPublic);
                var apply = typeof(OverlayCanvas).GetMethod("ApplyPreset", BindingFlags.Instance | BindingFlags.NonPublic);
                if (window == null || load == null || apply == null)
                {
                    Debug.LogWarning("[Bagel] Layout reset: this Editor's main toolbar can't take a preset from code.");
                    return;
                }
                var preset = load.Invoke(null, new object[] { toolbarPresetPath, "preset" });
                if (preset == null)
                {
                    Debug.LogWarning($"[Bagel] Layout reset: {toolbarPresetPath} didn't load.");
                    return;
                }
                apply.Invoke(window.overlayCanvas, new[] { preset });
                // The demo's preset docks ours, but one saved without it would hide it.
                DemoToolbar.Show();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] Layout reset: the toolbar preset couldn't be applied. " + (e.InnerException ?? e).Message);
            }
        }
    }
}
