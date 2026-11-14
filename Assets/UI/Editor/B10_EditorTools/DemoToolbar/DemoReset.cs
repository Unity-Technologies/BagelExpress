using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
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
        /// The Bagel UI and Bagel Features windows' Reset (a new game, Bagel Features on the title), the first season and
        /// the project's language, and the demo toolbar's screen, season and language as authored. No window opens or
        /// closes.
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
        }

        /// <summary>
        /// Every change in the Unity project (this folder of the repository) is discarded for good, as at the last commit:
        /// changed and deleted files come back, new files are deleted (but not the ones git ignores: Library, UserSettings,
        /// builds). Changes not saved yet go too: everything is saved first (<see cref="SaveEverything"/>), so git finds them
        /// and the reimport replaces what the Editor holds in memory. The Console lists what went. Then the assets are
        /// reimported and the open scene is reloaded from disk. Leaves Play mode first. No confirmation: with -automated the
        /// Editor can't show one.
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
            SaveEverything();
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

        /// <summary>
        /// What Ctrl+S would save, and more: an edit kept only in memory (a UIAnimationClip changed in the Animation window,
        /// a UXML or USS edit in the UI Builder, the UI Viewport or the Inspector) isn't in git's changes, and a reimport
        /// leaves it, as the file didn't change. Saved in turn: each window that says it has unsaved changes (the UI
        /// Builder, Style Sheets), the UI documents and style sheets the UI authoring tracks, the stage being edited (a
        /// UXML or prefab in isolation), the open scenes that have a file, then every other asset.
        /// </summary>
        static void SaveEverything()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (!window.hasUnsavedChanges)
                    continue;
                try
                {
                    window.SaveChanges();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bagel] Demo reset: {window.titleContent.text} couldn't save. " + (e.InnerException ?? e).Message);
                }
            }

            try
            {
                var registry = Type.GetType("Unity.UIToolkit.Editor.UIAssetRegistry, UnityEditor.UIToolkitAuthoringModule");
                var live = registry?.GetProperty("LiveInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                if (live != null)
                    registry.GetMethod("SaveAll", k_Members, null, new[] { typeof(object) }, null)?.Invoke(live, new object[] { null });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] Demo reset: the UI documents and style sheets couldn't be saved. " + (e.InnerException ?? e).Message);
            }

            try
            {
                var stage = StageUtility.GetCurrentStage();
                if (stage != null && !(stage is MainStage)
                    && typeof(Stage).GetProperty("hasUnsavedChanges", k_Members)?.GetValue(stage) is true)
                    typeof(Stage).GetMethod("Save", k_Members, null, Type.EmptyTypes, null)?.Invoke(stage, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] Demo reset: the stage being edited couldn't be saved. " + (e.InnerException ?? e).Message);
            }

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isDirty && !string.IsNullOrEmpty(scene.path))
                    EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
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
        /// (<see cref="toolbarPresetPath"/>), with the demo toolbar shown, the Game view on Bagel 2x (perf)
        /// (<see cref="GameViewDeviceSizes.perfSize"/>; the layout file keeps its size as an index in a menu that differs on
        /// each computer), the Profiler on CPU, GPU and the two UI Toolkit modules with its captures list collapsed
        /// (<see cref="ResetProfilers"/>), the Hierarchy and Project windows collapsed (<see cref="CollapseTrees"/>) and the
        /// VisualElement Inspector's search cleared (<see cref="ClearInspectorSearch"/>). Loading a layout closes and
        /// rebuilds every window, so it runs after the menu's click is done.
        /// </summary>
        [MenuItem(resetLayoutMenuPath, priority = 43, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void ResetLayout() => ResetLayout(null);

        static void ResetLayout(Action done)
        {
            DemoOverrides.OnNextUpdate(() =>
            {
                // Read by the windows the layout builds: none of the three is in the layout file.
                InternalEditorUtility.expandedProjectWindowItemIds = Array.Empty<EntityId>();
                ClearInspectorSearch();
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
                    ResetProfilers();
                    CollapseTrees();
                    ClearInspectorSearch();
                    if (done != null)
                        DemoOverrides.OnNextUpdate(done);
                });
            });
        }

        // The Profiler modules the demo shows; every other one is turned off.
        static readonly string[] k_ProfilerModules =
        {
            "CPUProfilerModule", "GPUProfilerModule", "UIToolkitProfilerModule", "UIToolkitDetailProfilerModule"
        };

        const BindingFlags k_Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// Each Profiler window with only CPU Usage, GPU Usage, UI Toolkit and UI Toolkit Details on, and its list of
        /// captures (the left side bar) collapsed. A layout keeps neither: the Profiler saves them in the Editor's
        /// preferences, and reads a module's state when its tab is first shown, so a module not shown yet gets the
        /// saved state only.
        /// </summary>
        static void ResetProfilers()
        {
            try
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    var type = window.GetType();
                    if (type.FullName != "UnityEditor.ProfilerWindow")
                        continue;

                    var initialized = type.GetField("m_IsInitialized", k_Members)?.GetValue(window) is true;
                    if (type.GetProperty("Modules", k_Members)?.GetValue(window) is not System.Collections.IEnumerable modules)
                    {
                        Debug.LogWarning("[Bagel] Layout reset: this Editor's Profiler has no modules to set.");
                        return;
                    }

                    foreach (var module in modules)
                    {
                        var moduleType = module.GetType();
                        var wanted = Array.IndexOf(k_ProfilerModules, moduleType.Name) >= 0;
                        if (moduleType.GetProperty("activeStatePreferenceKey", k_Members)?.GetValue(module) is string key)
                            EditorPrefs.SetBool(key, wanted);
                        var active = moduleType.GetProperty("active", k_Members);
                        if (initialized && active != null && !Equals(active.GetValue(module), wanted))
                            active.SetValue(module, wanted);
                    }

                    type.GetMethod("ShowCapturesList", k_Members)?.Invoke(window, new object[] { false });
                }
                // Read by a Profiler window built later.
                EditorPrefs.SetBool("ProfilerWindow.CapturesSplitView.ToggleState", false);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] Layout reset: the Profiler couldn't be set. " + (e.InnerException ?? e).Message);
            }
        }

        /// <summary>
        /// Every Hierarchy window with each scene's GameObjects collapsed (the scenes stay open, so their root objects
        /// show), and every Project window's folders collapsed. Neither is in a layout: the Hierarchy keeps what's open
        /// per scene, the Project window in the Editor's list of open folders, which the windows the layout builds read
        /// (emptied before it loads).
        /// </summary>
        static void CollapseTrees()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<HierarchyWindow>())
            {
                var view = window.View;
                if (view == null)
                    continue;
                view.CollapseAll();
                view.Expand(view.Source.GetChildren(view.Source.Root));
            }

            InternalEditorUtility.expandedProjectWindowItemIds = Array.Empty<EntityId>();
            try
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    var type = window.GetType();
                    if (type.FullName != "UnityEditor.ProjectBrowser")
                        continue;
                    foreach (var field in new[] { "m_FolderTreeState", "m_AssetTreeState" })
                    {
                        if (type.GetField(field, k_Members)?.GetValue(window) is TreeViewState<EntityId> state)
                            state.expandedIDs = new System.Collections.Generic.List<EntityId>();
                    }
                    type.GetMethod("ResetViews", k_Members)?.Invoke(window, null);
                    window.Repaint();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] Layout reset: the Project window couldn't be collapsed. " + (e.InnerException ?? e).Message);
            }
        }

        /// <summary>
        /// The VisualElement Inspector's search text and filters cleared. It keeps them for the Editor's session (statics a
        /// search field reads when it's built), so they outlive a layout: the statics first, then the search field of each
        /// VisualElement's Inspector editor, found through the editor because an Inspector tab behind another one keeps
        /// its header out of the window.
        /// </summary>
        static void ClearInspectorSearch()
        {
            try
            {
                var type = Type.GetType("Unity.UIToolkit.Editor.InspectorSearchField, UnityEditor.UIToolkitAuthoringModule");
                var reset = type?.GetMethod("ResetSearch", k_Members);
                var search = type?.GetField("s_PersistentSearch", BindingFlags.Static | BindingFlags.NonPublic);
                var filter = type?.GetField("s_PersistentFilter", BindingFlags.Static | BindingFlags.NonPublic);
                if (reset == null || search == null || filter == null)
                {
                    Debug.LogWarning("[Bagel] Layout reset: this Editor's VisualElement Inspector search can't be cleared from code.");
                    return;
                }
                search.SetValue(null, string.Empty);
                filter.SetValue(null, Enum.ToObject(filter.FieldType, 1)); // SearchFilter.All

                foreach (var editor in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
                {
                    if (editor.GetType().FullName != "Unity.UIToolkit.Editor.VisualElementSelectionEditor")
                        continue;
                    if (editor.GetType().GetField("m_Header", k_Members)?.GetValue(editor) is not VisualElement header)
                        continue;
                    header.Query(className: "unity-inspector-search-field").ForEach(field =>
                    {
                        if (type.IsInstanceOfType(field))
                            reset.Invoke(field, null);
                    });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Bagel] Layout reset: the VisualElement Inspector search couldn't be cleared. " + (e.InnerException ?? e).Message);
            }
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
