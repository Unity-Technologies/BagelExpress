using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Hierarchy;
using Unity.Hierarchy.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assemblies;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Bagel
{
    /// <summary>
    /// What <see cref="EditorLink"/> needs from Editor windows that have no public API for it in Unity 6.7: selecting a
    /// VisualElement like the Hierarchy does, opening a template instance in the UI Viewport, loading a document in
    /// UI Builder, the Resource Tables and the Profiler's modules. And the UI Builder's and UI Viewport's theme menus,
    /// for the demo toolbar's season (<see cref="DemoOverrides"/>). Everything non-public is here, by name, so a later
    /// Unity version only needs this file updated; a missing member throws <see cref="MissingMemberException"/>.
    /// </summary>
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup] // a type cache, valid across reloads of other assemblies
    static class EditorWindowInternals
    {
        const BindingFlags k_Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        // ------------------------------------------------------------------ reflection

        static readonly Dictionary<string, Type> s_Types = new();

        static Type FindType(string fullName)
        {
            if (s_Types.TryGetValue(fullName, out var cached))
                return cached;
            foreach (var assembly in CurrentAssemblies.GetLoadedAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null)
                    return s_Types[fullName] = type;
            }
            throw new MissingMemberException("type " + fullName);
        }

        static MethodInfo Method(Type type, string name, Func<MethodInfo, bool> match = null) =>
            type.GetMethods(k_Any).FirstOrDefault(m => m.Name == name && (match == null || match(m)))
            ?? throw new MissingMemberException(type.Name, name);

        static FieldInfo Field(Type type, string name) =>
            type.GetField(name, k_Any) ?? throw new MissingMemberException(type.Name, name);

        static PropertyInfo Property(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var property = t.GetProperty(name, k_Any | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property;
            }
            throw new MissingMemberException(type.Name, name);
        }

        public static EditorWindow FindWindow(string typeName) =>
            Resources.FindObjectsOfTypeAll<EditorWindow>().FirstOrDefault(w => w.GetType().Name == typeName);

        // ------------------------------------------------------------------ retries

        /// <summary>
        /// Run <paramref name="attempt"/> on the next Editor updates until it returns true (windows build their UI a
        /// frame or more after they open), for up to about three seconds.
        /// </summary>
        public static void Retry(Func<bool> attempt, string what, double seconds = 3.0)
        {
            var deadline = EditorApplication.timeSinceStartup + seconds;
            void Tick()
            {
                bool done;
                try
                {
                    done = attempt();
                }
                catch (Exception e)
                {
                    EditorApplication.update -= Tick;
                    Debug.LogWarning($"[Bagel] Couldn't {what}: {e.Message}");
                    return;
                }
                if (done)
                    EditorApplication.update -= Tick;
                else if (EditorApplication.timeSinceStartup > deadline)
                {
                    EditorApplication.update -= Tick;
                    Debug.LogWarning($"[Bagel] Couldn't {what} (timed out)");
                }
            }
            EditorApplication.update += Tick;
        }

        public static void Retry(Action attempt, string what) => Retry(() => { attempt(); return true; }, what);

        // ------------------------------------------------------------------ search fields and the Inspector filter

        /// <summary>Type into a window's ToolbarSearchField by element name. False while the window or the field isn't there yet.</summary>
        public static bool SetSearchField(string windowTypeName, string fieldName, string text)
        {
            var window = FindWindow(windowTypeName);
            var field = window?.rootVisualElement.Q<ToolbarSearchField>(fieldName);
            if (field == null)
                return false;
            field.value = text;
            window.Repaint();
            return true;
        }

        /// <summary>Type into the first ToolbarSearchField of a window.</summary>
        public static bool SetFirstSearchField(EditorWindow window, string text)
        {
            var field = window?.rootVisualElement.Q<ToolbarSearchField>();
            if (field == null)
                return false;
            field.value = text;
            window.Repaint();
            return true;
        }

        /// <summary>
        /// Type into the element Inspector's "Filter attributes and properties" field, in every Inspector window.
        /// False while no Inspector shows a VisualElement yet.
        /// </summary>
        public static bool SetInspectorFilter(string text)
        {
            bool found = false;
            foreach (var inspector in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "InspectorWindow"))
            {
                inspector.rootVisualElement.Query<TextField>().ForEach(field =>
                {
                    var placeholder = field.textEdition.placeholder;
                    if (placeholder == null || !placeholder.Contains("Filter"))
                        return;
                    field.value = text;
                    found = true;
                });
            }
            return found;
        }

        /// <summary>
        /// Press one of the element Inspector's Filter buttons (None / Set Values / Variables / Bindings / Animations), in
        /// every Inspector. False while no Inspector shows them yet.
        /// </summary>
        public static bool SetInspectorFilterMode(int index)
        {
            bool found = false;
            foreach (var inspector in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "InspectorWindow"))
            {
                inspector.rootVisualElement.Query<ToggleButtonGroup>().ForEach(group =>
                {
                    var buttons = group.Query<Button>().ToList();
                    if (buttons.Count != 5 || buttons[1].text != "Set Values")
                        return;
                    var state = new ToggleButtonGroupState(0, buttons.Count);
                    state[index] = true;
                    group.value = state;
                    found = true;
                });
            }
            return found;
        }

        // ------------------------------------------------------------------ VisualElement selection and the Hierarchy

        /// <summary>Select a VisualElement like a click in the Hierarchy (Unity.UIToolkit.Editor.VisualElementSelectionRegistry).</summary>
        public static void SelectVisualElement(VisualElement element) => Selection.activeEntityId = GetEntityId(element);

        static EntityId GetEntityId(VisualElement element)
        {
            var registryType = FindType("Unity.UIToolkit.Editor.VisualElementSelectionRegistry");
            var registry = Property(registryType, "Instance").GetValue(null) ?? throw new MissingMemberException("VisualElementSelectionRegistry.Instance is null");
            registryType.GetMethod("EnsureInitialized", k_Any)?.Invoke(registry, null);
            return (EntityId)Method(registryType, "GetOrCreateEntityId", m => m.GetParameters().Length == 1).Invoke(registry, new object[] { element });
        }

        /// <summary>Expand the Hierarchy down to an element of the scene's UI and scroll to it (public HierarchyView.Frame).</summary>
        public static void RevealInHierarchy(VisualElement element)
        {
            var window = Resources.FindObjectsOfTypeAll<HierarchyWindow>().FirstOrDefault();
            if (window?.View == null)
                return;
            var node = window.View.Source.GetNodeFromEntityId(GetEntityId(element));
            if (node != HierarchyNode.Null)
                window.View.Frame(in node);
        }

        // ------------------------------------------------------------------ UI Viewport (UI editing stage)

        /// <summary>Run one of the Hierarchy's "Open Instance/…" actions on a template instance (StageContextMenuUtility).</summary>
        public static void OpenTemplateInstance(VisualElement instance, string actionName)
        {
            var utility = FindType("Unity.UIToolkit.Editor.StageContextMenuUtility");
            var sources = FindType("Unity.UIToolkit.Editor.CommandSources");
            var hierarchySource = sources.GetField("Hierarchy", k_Any)?.GetValue(null) ?? throw new MissingMemberException("CommandSources", "Hierarchy");
            var menu = new DropdownMenu();
            Method(utility, "PopulateOpenActions", m => m.GetParameters().Length == 3).Invoke(null, new[] { instance, menu, hierarchySource });
            foreach (var item in menu.MenuItems())
            {
                if (item is DropdownMenuAction action && action.name == actionName)
                {
                    action.Execute();
                    return;
                }
            }
            throw new MissingMemberException($"Hierarchy action '{actionName}' on #{instance.name}");
        }

        /// <summary>The authoring tree of the UI editing stage (VisualElementEditingStage.GetAuthoringPanel), or null on the main stage.</summary>
        public static VisualElement GetEditingStageRoot()
        {
            var stage = StageUtility.GetCurrentStage();
            if (stage == null || stage.GetType().Name != "VisualElementEditingStage")
                return null;
            var panel = (IPanel)Method(stage.GetType(), "GetAuthoringPanel", m => m.GetParameters().Length == 0).Invoke(stage, null);
            return panel?.visualTree;
        }

        // ------------------------------------------------------------------ UI Builder

        static Type builderType => FindType("Unity.UI.Builder.Builder");

        /// <summary>Show UI Builder (Builder.ShowWindow) and load a document in it (Builder.LoadDocument).</summary>
        public static EditorWindow OpenInUIBuilder(VisualTreeAsset document)
        {
            var builder = (EditorWindow)Method(builderType, "ShowWindow", m => m.GetParameters().Length == 0).Invoke(null, null);
            var current = Property(builderType, "document").GetValue(builder);
            var loaded = current != null ? Property(current.GetType(), "visualTreeAsset").GetValue(current) as VisualTreeAsset : null;
            if (loaded != document)
                Method(builderType, "LoadDocument", m => m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(VisualTreeAsset))
                    .Invoke(builder, new object[] { document, true });
            return builder;
        }

        /// <summary>Select an element of the open document by name (Builder.selection, BuilderHierarchy expand). False until it's there.</summary>
        public static bool SelectInUIBuilder(EditorWindow builder, string elementName)
        {
            var root = Property(builderType, "documentRootElement").GetValue(builder) as VisualElement;
            var element = root?.Q(elementName);
            if (element == null)
                return false;
            var selection = Property(builderType, "selection").GetValue(builder);
            Method(selection.GetType(), "ClearSelection", m => m.GetParameters().Length == 2).Invoke(selection, new object[] { null, false });
            var hierarchy = Property(builderType, "hierarchy").GetValue(builder);
            var view = Property(hierarchy.GetType(), "elementHierarchyView").GetValue(hierarchy);
            Method(view.GetType(), "RecursivelyExpandToItem").Invoke(view, new object[] { element });
            Method(selection.GetType(), "AddToSelection", m => m.GetParameters().Length == 4).Invoke(selection, new object[] { null, element, false, false });
            return true;
        }

        /// <summary>Show UI Builder and load a USS/TSS in it (Builder.LoadStyleSheetDocument; StyleSheet Editing Mode).</summary>
        public static void OpenStyleSheetInUIBuilder(StyleSheet styleSheet)
        {
            var builder = (EditorWindow)Method(builderType, "ShowWindow", m => m.GetParameters().Length == 0).Invoke(null, null);
            builder.Focus();
            Method(builderType, "LoadStyleSheetDocument", m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(StyleSheet))
                .Invoke(builder, new object[] { styleSheet });
        }

        /// <summary>Turn on a Project Settings › UI Toolkit flag (UIToolkitProjectSettings.enable…), e.g. a per-user experimental one.</summary>
        public static void EnsureUIToolkitFlag(string name)
        {
            var property = Property(FindType("UnityEditor.UIElements.UIToolkitProjectSettings"), name);
            if (!(bool)property.GetValue(null))
                property.SetValue(null, true);
        }

        // ------------------------------------------------------------------ UI Builder and UI Viewport theme menus

        static Type viewportType => FindType("Unity.UIToolkit.Editor.UIViewportWindow");

        /// <summary>The open UI Builder and UI Viewport windows, which have a theme menu.</summary>
        public static IEnumerable<EditorWindow> ThemeMenuWindows() =>
            Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType() == builderType || w.GetType() == viewportType).ToList();

        /// <summary>
        /// Whether the window shows what's in this panel: UI Builder's document is in its own panel, the UI Viewport's
        /// in a sub panel of its canvas (UICanvas.PanelElement.SubPanel).
        /// </summary>
        public static bool ShowsPanel(EditorWindow window, IPanel panel)
        {
            if (panel == null)
                return false;
            if (window.GetType() == builderType)
                return window.rootVisualElement.panel == panel;
            var canvas = Field(viewportType, "m_Canvas").GetValue(window);
            var panelElement = canvas != null ? Property(canvas.GetType(), "PanelElement").GetValue(canvas) : null;
            return panelElement != null && Property(panelElement.GetType(), "SubPanel").GetValue(panelElement) == panel;
        }

        /// <summary>
        /// The theme picked in the window's theme menu (BuilderDocument.currentCanvasThemeStyleSheet,
        /// UIViewportThemeMenu.SelectedTheme); null: the UI Viewport's Panel Settings entry, or a UI Builder editor theme.
        /// </summary>
        public static ThemeStyleSheet GetMenuTheme(EditorWindow window)
        {
            if (window.GetType() == builderType)
            {
                var document = Property(builderType, "document").GetValue(window);
                return document != null ? Property(document.GetType(), "currentCanvasThemeStyleSheet").GetValue(document) as ThemeStyleSheet : null;
            }
            var menu = ViewportThemeMenu(window);
            return menu != null ? Property(menu.GetType(), "SelectedTheme").GetValue(menu) as ThemeStyleSheet : null;
        }

        /// <summary>
        /// Pick a theme in the window's theme menu as a click there does; null goes back to the window's own. UI Builder:
        /// BuilderToolbar.ChangeCanvasTheme(Custom, theme) as on load (isInit), so it isn't kept as the user's preferred
        /// preview theme, and InitCanvasTheme for its own (the user's preference or the project's). UI Viewport:
        /// UIViewportThemeMenu.OnThemeSelected, which the window keeps per document in SessionState; null is its Panel
        /// Settings entry.
        /// </summary>
        public static void SetMenuTheme(EditorWindow window, ThemeStyleSheet theme)
        {
            if (window.GetType() == builderType)
            {
                var toolbar = Property(builderType, "toolbar").GetValue(window);
                if (toolbar == null)
                    return;
                if (theme == null)
                {
                    Method(toolbar.GetType(), "InitCanvasTheme").Invoke(toolbar, null);
                    return;
                }
                var change = Method(toolbar.GetType(), "ChangeCanvasTheme", m => m.GetParameters().Length == 3);
                var custom = Enum.Parse(change.GetParameters()[0].ParameterType, "Custom");
                change.Invoke(toolbar, new[] { custom, theme, true });
                return;
            }
            var menu = ViewportThemeMenu(window);
            if (menu != null)
                Method(menu.GetType(), "OnThemeSelected").Invoke(menu, new object[] { theme });
        }

        static VisualElement ViewportThemeMenu(EditorWindow window) =>
            window.rootVisualElement.Q(className: "unity-ui-viewport__toolbar-theme-selection");

        // ------------------------------------------------------------------ Resource Tables, Profiler

        /// <summary>Open the Resource Tables window on a table collection (ResourceTablesWindow.ShowWindow).</summary>
        public static EditorWindow OpenResourceTables(string collectionPath)
        {
            var collection = AssetDatabase.LoadMainAssetAtPath(collectionPath) ?? throw new FileNotFoundException("No table collection " + collectionPath);
            var windowType = FindType("Unity.Localization.Editor.ResourceTablesWindow");
            Method(windowType, "ShowWindow", m => m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(collection))
                .Invoke(null, new object[] { collection });
            return EditorWindow.GetWindow(windowType);
        }

        /// <summary>Activate the UI Toolkit and UI Toolkit Details modules and select the first (ProfilerWindow.SetProfilerModuleActiveState / SelectModule).</summary>
        public static void ShowUIToolkitProfilerModules(ProfilerWindow window)
        {
            var setActive = Method(typeof(ProfilerWindow), "SetProfilerModuleActiveState", m => m.GetParameters().Length == 2);
            object first = null;
            foreach (var module in (IEnumerable)Property(typeof(ProfilerWindow), "Modules").GetValue(window))
            {
                var name = module.GetType().Name;
                if (!name.StartsWith("UIToolkit", StringComparison.Ordinal) && !name.StartsWith("UIElements", StringComparison.Ordinal))
                    continue;
                setActive.Invoke(window, new[] { module, (object)true });
                first ??= module;
            }
            if (first != null)
                Method(typeof(ProfilerWindow), "SelectModule", m => m.GetParameters().Length == 1).Invoke(window, new[] { first });
        }
    }
}
