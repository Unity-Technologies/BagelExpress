using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Bagel
{
    /// <summary>
    /// "Show me this in the Editor", as a button: a source file at the right line, a UXML in UI Builder, a page of the
    /// scene's UI in the UI Viewport, an element in the Hierarchy and the Inspector (with the Inspector's search filled
    /// in), the Animation window on a clip, the Resource Tables, the Profiler, and so on. The Bagel Features and the Demo
    /// Flow windows are made of these.
    ///
    /// A few of them reach into Editor windows that have no public API for it yet (see <see cref="EditorWindowInternals"/>).
    /// If one of those changes in a later Unity version, the link still opens the window and logs a warning.
    /// </summary>
    public sealed class EditorLink
    {
        public enum Kind
        {
            /// <summary>Opens a file in the code editor (or selects an asset that isn't text).</summary>
            File,
            /// <summary>Opens an Editor window, or selects something in one.</summary>
            Window,
        }

        public const string mainScenePath = "Assets/Scenes/Main.unity";

        /// <summary>The element Inspector's Filter buttons (next to its search): what it lists.</summary>
        public enum InspectorFilter { All, SetValues, Variables, Bindings, Animations }

        static string FilterLabel(InspectorFilter filter) => filter switch
        {
            InspectorFilter.SetValues => "set values",
            InspectorFilter.Variables => "variables",
            InspectorFilter.Bindings => "bindings",
            InspectorFilter.Animations => "animations",
            _ => null,
        };

        static string Describe(string search, InspectorFilter filter)
        {
            var parts = new List<string>();
            if (search != null)
                parts.Add(search);
            if (FilterLabel(filter) != null)
                parts.Add(FilterLabel(filter));
            return parts.Count == 0 ? "" : " › " + string.Join(" · ", parts);
        }

        static void ApplyInspector(string search, InspectorFilter filter)
        {
            EditorWindowInternals.Retry(() => EditorWindowInternals.SetInspectorFilter(search ?? ""), "search the Inspector");
            EditorWindowInternals.Retry(() => EditorWindowInternals.SetInspectorFilterMode((int)filter), "set the Inspector's filter");
        }

        public readonly Kind kind;
        public readonly string label;
        public readonly string tooltip;
        /// <summary>File links: the project-relative path. Window links: the asset they open, if any.</summary>
        public readonly string path;
        /// <summary>File links: text on the line to open the file at (its first occurrence). Null or empty: the top.</summary>
        public readonly string find;
        /// <summary>Window links: the element of the scene's UI they select, if any (checked by the tests).</summary>
        public readonly string element;

        readonly Action m_Open;

        EditorLink(Kind kind, string label, string tooltip, Action open, string path = null, string find = null, string element = null)
        {
            this.kind = kind;
            this.label = label;
            this.tooltip = tooltip;
            this.path = path;
            this.find = find;
            this.element = element;
            m_Open = open;
        }

        public override string ToString() => label;

        public void Open()
        {
            try
            {
                m_Open();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] \"{label}\" couldn't finish: {e.Message}");
            }
        }

        // ------------------------------------------------------------------ files and assets

        static readonly HashSet<string> s_TextExtensions = new() { ".cs", ".uss", ".tss", ".uxml", ".shader", ".hlsl", ".cginc", ".json", ".svg", ".asmdef", ".md", ".txt", ".xml" };

        /// <summary>Open a project file in the code editor at the first line containing <paramref name="find"/>.</summary>
        public static EditorLink File(string path, string find = null, string label = null)
        {
            var name = label ?? System.IO.Path.GetFileName(path);
            return new EditorLink(Kind.File, name, path, () => OpenFile(path, find), path, find);
        }

        /// <summary>
        /// Text files open in the External Script Editor (Preferences › External Tools) at the line; other assets
        /// (scenes, ScriptableObjects, clips) are selected and pinged so the Inspector shows them.
        /// </summary>
        public static void OpenFile(string path, string find)
        {
            if (!s_TextExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant()))
            {
                PingAsset(path);
                return;
            }
            var fullPath = System.IO.Path.GetFullPath(path);
            if (!System.IO.File.Exists(fullPath))
                throw new FileNotFoundException("No file " + path);
            InternalEditorUtility.OpenFileAtLineExternal(fullPath, Mathf.Max(1, FindLine(path, find)));
        }

        /// <summary>The 1-based line of the first occurrence of <paramref name="find"/> in the file; 1 for none, 0 if it's missing.</summary>
        public static int FindLine(string path, string find)
        {
            if (string.IsNullOrEmpty(find))
                return 1;
            var fullPath = System.IO.Path.GetFullPath(path);
            if (!System.IO.File.Exists(fullPath))
                return 0;
            var lines = System.IO.File.ReadAllLines(fullPath);
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains(find, StringComparison.Ordinal))
                    return i + 1;
            return 0;
        }

        /// <summary>Select an asset and ping it in the Project window.</summary>
        public static EditorLink Asset(string path, string label = null) =>
            new(Kind.Window, label ?? System.IO.Path.GetFileName(path), $"Select {path} in the Project window", () => PingAsset(path), path);

        static void PingAsset(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path) ?? throw new FileNotFoundException("No asset " + path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // ------------------------------------------------------------------ UI Builder

        /// <summary>Open a UXML document in UI Builder, optionally selecting an element by name.</summary>
        public static EditorLink UIBuilder(string uxmlPath, string select = null) =>
            new(Kind.Window, "UI Builder › " + System.IO.Path.GetFileName(uxmlPath) + (select != null ? " › #" + select : ""),
                $"Open {uxmlPath} in UI Builder" + (select != null ? $" with #{select} selected" : ""),
                () =>
                {
                    var document = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath) ?? throw new FileNotFoundException("No UXML " + uxmlPath);
                    var builder = EditorWindowInternals.OpenInUIBuilder(document);
                    if (select != null)
                        EditorWindowInternals.Retry(() => EditorWindowInternals.SelectInUIBuilder(builder, select), "select #" + select + " in UI Builder");
                }, uxmlPath);

        /// <summary>Open a USS or TSS in UI Builder (StyleSheet Editing Mode, experimental: turned on for you, per user).</summary>
        public static EditorLink UIBuilderStyleSheet(string styleSheetPath) =>
            new(Kind.Window, "UI Builder › " + System.IO.Path.GetFileName(styleSheetPath),
                $"Open {styleSheetPath} in UI Builder (StyleSheet Editing Mode, Project Settings › UI Toolkit, experimental)",
                () =>
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath) ?? throw new FileNotFoundException("No style sheet " + styleSheetPath);
                    EditorWindowInternals.EnsureUIToolkitFlag("enableStyleSheetEditingMode");
                    EditorWindowInternals.OpenStyleSheetInUIBuilder(sheet);
                }, styleSheetPath);

        // ------------------------------------------------------------------ the scene's UI: Hierarchy, Inspector, UI Viewport

        /// <summary>
        /// Select an element of the scene's UI by name, the way a click in the Hierarchy does, so the Inspector shows it.
        /// <paramref name="inspectorFilter"/> is typed into the element Inspector's search.
        /// </summary>
        public static EditorLink Select(string elementName, string inspectorFilter = null, InspectorFilter filter = InspectorFilter.All) =>
            new(Kind.Window, "Inspector › " + Selector(elementName) + Describe(inspectorFilter, filter),
                $"Select {Selector(elementName)} of the scene's UI (Hierarchy + Inspector)" + (inspectorFilter != null ? $", search its Inspector for \"{inspectorFilter}\"" : "")
                + (FilterLabel(filter) != null ? $", show only its {FilterLabel(filter)}" : ""),
                () => WithSceneElement(elementName, target =>
                {
                    EditorWindowInternals.SelectVisualElement(target);
                    EditorWindowInternals.RevealInHierarchy(target);
                    ApplyInspector(inspectorFilter, filter);
                }), element: elementName);

        static string Selector(string element) => element.StartsWith("#") || element.StartsWith(".") ? element : "#" + element;

        /// <summary>Select a GameObject of the scene by name (its components in the Inspector).</summary>
        public static EditorLink GameObject(string gameObjectName) =>
            new(Kind.Window, "Hierarchy › " + gameObjectName, $"Select the {gameObjectName} GameObject of Main.unity", () =>
            {
                EnsureMainScene();
                var go = UnityEngine.GameObject.Find(gameObjectName) ?? throw new InvalidOperationException("No GameObject " + gameObjectName);
                Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            });

        /// <summary>
        /// Open a page (a template instance of the scene's UI, e.g. home-page) in isolation in the UI Viewport, like
        /// the Hierarchy's Open Instance › In Isolation, optionally selecting an element in it.
        /// </summary>
        public static EditorLink UIViewport(string instanceName, string select = null, string inspectorFilter = null, InspectorFilter filter = InspectorFilter.All, bool inContext = false) =>
            new(Kind.Window, "UI Viewport › " + instanceName + (inContext ? " (in context)" : "") + (select != null ? " › " + Selector(select) : "") + Describe(inspectorFilter, filter),
                $"Open the #{instanceName} instance of the scene's UI {(inContext ? "in context (its document around it)" : "in isolation")} in the UI Viewport" + (select != null ? $", with #{select} selected" : ""),
                () => WithSceneElement(instanceName, instance =>
                {
                    if (StageUtility.GetCurrentStage() is not MainStage)
                        StageUtility.GoToMainStage();
                    EditorWindowInternals.OpenTemplateInstance(instance, inContext ? "Open Instance/In Context" : "Open Instance/In Isolation");
                    if (select != null)
                    {
                        EditorWindowInternals.Retry(() =>
                        {
                            var root = EditorWindowInternals.GetEditingStageRoot();
                            var target = GameScreen.Find(root, select);
                            if (target == null)
                                return false;
                            EditorWindowInternals.SelectVisualElement(target);
                            ApplyInspector(inspectorFilter, filter);
                            return true;
                        }, "select #" + select + " in the UI Viewport");
                    }
                }), element: instanceName);

        /// <summary>Search the Hierarchy, e.g. <c>uit:PastryIcon</c>, <c>veRef:any</c>, <c>classes:be-btn</c>.</summary>
        public static EditorLink HierarchySearch(string query) =>
            new(Kind.Window, "Hierarchy › " + query, $"Search the Hierarchy for \"{query}\"", () =>
            {
                EnsureMainScene();
                var hierarchy = EditorWindow.GetWindow<HierarchyWindow>();
                hierarchy.Show();
                hierarchy.Focus();
                hierarchy.SetSearchText(query);
            });

        /// <summary>The Style Sheets window (Window › UI Toolkit › Style Sheets), filtered.</summary>
        public static EditorLink StyleSheets(string search, string documentInstance = "home-page") =>
            new(Kind.Window, "Style Sheets › " + search, $"Open the Style Sheets window, filtered by \"{search}\" (it lists the style sheets of the UI being edited)", () =>
            {
                if (documentInstance != null && StageUtility.GetCurrentStage() is MainStage)
                {
                    var instance = FindInSceneUI(documentInstance);
                    if (instance != null)
                        EditorWindowInternals.OpenTemplateInstance(instance, "Open Instance/In Isolation");
                }
                EditorApplication.ExecuteMenuItem("Window/UI Toolkit/Style Sheets");
                EditorWindowInternals.Retry(() => EditorWindowInternals.SetSearchField("StyleSheetsWindow", "stylesheet-search-field", search), "filter the Style Sheets window");
            });

        // ------------------------------------------------------------------ other windows

        /// <summary>The Animation window on a clip: a UIAnimationClip asset, or an AnimationClip of the scene's app (TitleIntro).</summary>
        public static EditorLink Animation(string clipPath) =>
            new(Kind.Window, "Animation › " + System.IO.Path.GetFileNameWithoutExtension(clipPath), $"Open {clipPath} in the Animation window", () =>
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(clipPath) ?? throw new FileNotFoundException("No clip " + clipPath);
                if (asset is AnimationClip clip)
                {
                    EnsureMainScene();
                    var renderer = FindSceneRenderer();
                    if (renderer != null)
                        Selection.activeGameObject = renderer.gameObject;
                    var window = EditorWindow.GetWindow<AnimationWindow>();
                    window.Show();
                    window.animationClip = clip;
                }
                else
                {
                    AssetDatabase.OpenAsset(asset);
                }
            }, clipPath);

        /// <summary>Window › Localization › Resource Tables, on the game's string tables, optionally searched.</summary>
        public static EditorLink ResourceTables(string search = null) =>
            new(Kind.Window, "Resource Tables" + (search != null ? " › " + search : ""), "Open the BagelStrings tables in the Resource Tables window" + (search != null ? $", searched for \"{search}\"" : ""), () =>
            {
                var window = EditorWindowInternals.OpenResourceTables("Assets/Localization/BagelStrings/BagelStrings.asset");
                if (search != null)
                    EditorWindowInternals.Retry(() => EditorWindowInternals.SetFirstSearchField(window, search), "search the Resource Tables");
            });

        /// <summary>The Profiler with the UI Toolkit and UI Toolkit Details modules.</summary>
        public static EditorLink Profiler() =>
            new(Kind.Window, "Profiler › UI Toolkit", "Open the Profiler on the UI Toolkit and UI Toolkit Details modules (record in Play mode)", () =>
            {
                var window = EditorWindow.GetWindow<ProfilerWindow>();
                window.Show();
                EditorWindowInternals.ShowUIToolkitProfilerModules(window);
            });

        /// <summary>Window › General › Test Runner; <paramref name="tests"/> names the tests to look for (its tooltip says so).</summary>
        public static EditorLink TestRunner(string tests = null) =>
            new(Kind.Window, "Test Runner" + (tests != null ? " › " + tests : ""), "Open the Test Runner" + (tests != null ? $" (the tests: {tests})" : ""),
                () => EditorApplication.ExecuteMenuItem("Window/General/Test Runner"));

        /// <summary>A Project Settings page, e.g. <c>Project/UI Toolkit</c>.</summary>
        public static EditorLink ProjectSettings(string settingsPath, string label) =>
            new(Kind.Window, label, "Open " + label, () => SettingsService.OpenProjectSettings(settingsPath));

        /// <summary>Any menu item, e.g. <c>Bagel/Bagel UI</c>.</summary>
        public static EditorLink Menu(string menuPath, string label) =>
            new(Kind.Window, label, "Menu: " + menuPath.Replace("/", " › "), () =>
            {
                if (!EditorApplication.ExecuteMenuItem(menuPath))
                    throw new InvalidOperationException("No menu item " + menuPath);
            });

        /// <summary>Open the scene and enter Play mode.</summary>
        public static EditorLink Play() =>
            new(Kind.Window, "▶ Play", "Open Main.unity and enter Play mode", () =>
            {
                EnsureMainScene();
                EditorApplication.isPlaying = true;
            });

        // ------------------------------------------------------------------ the scene's UI

        /// <summary>Open Main.unity if it isn't (asking to save the current scene first).</summary>
        public static void EnsureMainScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var active = EditorSceneManager.GetActiveScene();
            if (active.path == mainScenePath)
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                throw new OperationCanceledException("The scene wasn't opened");
            EditorSceneManager.OpenScene(mainScenePath);
        }

        static PanelRenderer FindSceneRenderer() =>
            Object.FindObjectsByType<PanelRenderer>(FindObjectsInactive.Exclude).FirstOrDefault(r => r.GetComponent<BakeryAppDriver>() != null)
            ?? Object.FindAnyObjectByType<PanelRenderer>();

        /// <summary>The root of the scene's UI (the PanelRenderer on BagelExpressApp), in edit mode or Play mode.</summary>
        public static VisualElement GetSceneUIRoot()
        {
            EnsureMainScene();
            var renderer = FindSceneRenderer();
            if (renderer == null)
                return null;
            VisualElement root = null;
            // The callback runs right away when the UI is loaded: the public way to reach the root.
            PanelRenderer.VersionedUIReloadCallback grab = (_, element, _) => root = element;
            renderer.RegisterUIReloadCallback(grab);
            renderer.UnregisterUIReloadCallback(grab);
            return root;
        }

        /// <summary>
        /// Run <paramref name="action"/> on an element of the scene's UI. Right after the scene opens its UI isn't built
        /// yet, so this waits for it (a few Editor updates).
        /// </summary>
        public static void WithSceneElement(string selector, Action<VisualElement> action)
        {
            var element = FindInSceneUI(selector);
            if (element != null)
            {
                action(element);
                return;
            }
            EditorWindowInternals.Retry(() =>
            {
                EditorApplication.QueuePlayerLoopUpdate();
                var found = FindInSceneUI(selector);
                if (found == null)
                    return false;
                action(found);
                return true;
            }, $"find #{selector.TrimStart('#')} in the scene's UI", 5.0);
        }

        /// <summary>An element of the scene's UI by name, or by ".class".</summary>
        public static VisualElement FindInSceneUI(string selector)
        {
            var root = GetSceneUIRoot();
            return root == null ? null : GameScreen.Find(root, selector);
        }
    }
}
