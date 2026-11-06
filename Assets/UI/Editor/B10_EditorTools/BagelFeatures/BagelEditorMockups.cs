using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The thumbnails of the features that live in the Editor (a window, the Inspector, the Test Runner): small Editor
    /// windows recreated in UI Toolkit with the Editor's own theme (its colors, font and icons), just enough to show the
    /// tool. They're pictures: nothing in them can be clicked. Built in code for the Editor's icons; styled in
    /// BagelFeaturesWindowStyles.uss (bgf-mock).
    /// </summary>
    public static class BagelEditorMockups
    {
        public static readonly string ussClassName = "bgf-mock";

        static readonly Dictionary<string, Func<VisualElement>> s_Mockups = new()
        {
            ["ui-builder-library"] = UIBuilderLibrary,
            ["in-scene-authoring"] = Hierarchy,
            ["uxml-asset-attributes"] = AssetAttributes,
            ["uxml-property-drawer"] = PropertyDrawer,
            ["stylesheet-editing-mode"] = StyleSheets,
            ["multiple-ui-builder-windows"] = TwoUIBuilders,
            ["live-edit-play-mode"] = LiveEdit,
            ["profiler-modules"] = Profiler,
            ["editor-theme-handling"] = EditorTheme,
            ["all-ui-window"] = BagelUIWindow,
            ["panelrenderer"] = PanelRendererInspector,
            ["panel-settings"] = PanelSettingsInspector,
            ["visualelementreference"] = ReferenceInspector,
            ["runtime-accessibility"] = Accessibility,
            ["keyframed-ui-animation"] = AnimationWindow,
            ["resource-tables"] = ResourceTables,
            ["ui-test-fixture"] = () => TestRunner(false, ("BagelTestsEditor", 0, true), ("HomePageTestsEditor", 1, false), ("ShopCardTestsEditor", 1, false)),
            ["runtime-ui-test-fixture"] = () => TestRunner(true, ("BagelTests", 0, true), ("BakeryAppRuntimeTests", 1, false), ("RecipeBookTests", 1, false)),
            ["editorwindow-ui-test-fixture"] = () => TestRunner(false, ("BagelFeaturesWindowTests", 0, true), ("TheSearchFiltersEveryMode", 1, false), ("GameModeRunsOneScreenAtATime", 1, false)),
            ["edit-and-play-mode-tests"] = () => TestRunner(false, ("BakeryAppTestsEditor", 0, true), ("NavButtonsSwitchPages", 1, false), ("TappingTheTitleDismissesIt", 1, false)),
            ["ui-shader-graph-vertices"] = () => ShaderGraph("UI", ("UV1", "Out"), ("Vertex", "Position")),
            // Not used in the game
            ["uss-stats"] = UssStats,
            ["uxml-upgrade-service"] = UxmlUpgrade,
            ["font-subsetting"] = FontSubsetting,
            ["filter-shader-graph"] = () => ShaderGraph("Filter", ("Filter Input", "Color"), ("Halftone", "Out")),
            ["panelrenderer-find"] = FindPanelRenderer,
            ["recursive-release-resources"] = ReleaseResources,
            ["figma-connector"] = Figma,
        };

        public static bool Has(string featureId) => s_Mockups.ContainsKey(featureId);

        public static IEnumerable<string> ids => s_Mockups.Keys;

        /// <summary>A feature's Editor picture, in the Editor's theme (<paramref name="editorSheets"/>), or null.</summary>
        public static VisualElement Create(string featureId, IReadOnlyList<StyleSheet> editorSheets)
        {
            if (!s_Mockups.TryGetValue(featureId, out var build))
                return null;
            var root = new VisualElement { name = "mock-" + featureId };
            root.AddToClassList(ussClassName);
            if (editorSheets != null)
                foreach (var sheet in editorSheets)
                    if (sheet != null)
                        root.styleSheets.Add(sheet);
            root.Add(build());
            root.Query<VisualElement>().ForEach(e => e.pickingMode = PickingMode.Ignore);
            return root;
        }

        // ------------------------------------------------------------------ the windows

        static VisualElement UIBuilderLibrary()
        {
            var (window, body) = Window("Library", "UIBuilder");
            var tabs = Bar(body);
            tabs.Add(Tab("Standard", false));
            tabs.Add(Tab("Project", true));
            Row(body, 0, true, "FolderOpened Icon", "Bagel");
            Row(body, 1, false, "Folder Icon", "Art", true);
            Row(body, 1, false, "Folder Icon", "Common");
            Row(body, 1, false, "Folder Icon", "Managers");
            return window;
        }

        static VisualElement Hierarchy()
        {
            var (window, body) = Window("Hierarchy", "UnityEditor.SceneHierarchyWindow");
            Row(body, 0, true, "SceneAsset Icon", "Main");
            Row(body, 1, true, "GameObject Icon", "BagelExpressApp");
            Row(body, 2, true, "UxmlScript Icon", "#home-page");
            Row(body, 3, null, "Button Icon", "#collect-button", true);
            return window;
        }

        static VisualElement AssetAttributes()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "cs Script Icon", "BakeryAppManager");
            Field(body, "Start State", Value("DemoStartState", "ScriptableObject Icon"));
            Field(body, "Card Template", Value("RecipeCard", "UxmlScript Icon"));
            return window;
        }

        static VisualElement PropertyDrawer()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "UxmlScript Icon", "StorePastry");
            var picker = new VisualElement();
            picker.AddToClassList(ussClassName + "__picker");
            foreach (var (kind, selected) in new[] { (PastryKind.Anpan, false), (PastryKind.CurryPan, true), (PastryKind.Dorayaki, false) })
            {
                var cell = new VisualElement();
                cell.AddToClassList(ussClassName + "__picker-cell");
                cell.EnableInClassList(ussClassName + "__picker-cell--selected", selected);
                var pastry = new PastryIcon(kind);
                pastry.AddToClassList(ussClassName + "__pastry");
                cell.Add(pastry);
                picker.Add(cell);
            }
            Field(body, "Kind", picker);
            return window;
        }

        static VisualElement StyleSheets()
        {
            var (window, body) = Window("Style Sheets", "UssScript Icon");
            Row(body, 0, true, "UssScript Icon", "BagelExpressCommonStyles.uss");
            var selector = Text(".be-btn", ussClassName + "__selector");
            body.Add(selector);
            Field(body, "border-color", Value("var(--be-ink)", null));
            Field(body, "border-radius", Value("var(--be-radius-m)", null));
            return window;
        }

        static VisualElement TwoUIBuilders()
        {
            var pair = new VisualElement();
            pair.AddToClassList(ussClassName + "__pair");
            foreach (var uxml in new[] { "HomePage.uxml", "ShopCard.uxml" })
            {
                var (window, body) = Window("UI Builder", "UIBuilder", true);
                Row(body, 0, null, "UxmlScript Icon", uxml);
                var canvas = new VisualElement();
                canvas.AddToClassList(ussClassName + "__canvas");
                body.Add(canvas);
                pair.Add(window);
            }
            return pair;
        }

        static VisualElement LiveEdit()
        {
            var column = new VisualElement();
            column.AddToClassList(ussClassName + "__column");
            var toolbar = new VisualElement();
            toolbar.AddToClassList(ussClassName + "__app-toolbar");
            foreach (var (icon, on) in new[] { ("PlayButton On", true), ("PauseButton", false), ("StepButton", false) })
            {
                var button = new VisualElement();
                button.AddToClassList(ussClassName + "__app-button");
                button.EnableInClassList(ussClassName + "__app-button--on", on);
                button.Add(Icon(icon));
                toolbar.Add(button);
            }
            column.Add(toolbar);
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            window.AddToClassList(ussClassName + "__window--short");
            Header(body, "Button Icon", "#collect-button");
            Field(body, "font-size", Value("22", null));
            Field(body, "color", Swatch(new Color(1f, 0.85f, 0.3f)));
            column.Add(window);
            return column;
        }

        static VisualElement Profiler()
        {
            var (window, body) = Window("Profiler", "UnityEditor.ProfilerWindow");
            var split = new VisualElement();
            split.AddToClassList(ussClassName + "__split");
            var modules = new VisualElement();
            modules.AddToClassList(ussClassName + "__modules");
            foreach (var (name, color) in new[] { ("UI Toolkit", new Color(0.93f, 0.55f, 0.18f)), ("UI Toolkit Details", new Color(0.36f, 0.72f, 0.42f)) })
            {
                var module = new VisualElement();
                module.AddToClassList(ussClassName + "__module");
                module.Add(Swatch(color));
                module.Add(Text(name, ussClassName + "__label"));
                modules.Add(module);
            }
            split.Add(modules);
            var chart = new VisualElement();
            chart.AddToClassList(ussClassName + "__chart");
            var heights = new[] { 0.35f, 0.5f, 0.42f, 0.7f, 0.38f, 0.55f, 0.46f, 0.62f, 0.4f };
            for (int i = 0; i < heights.Length; i++)
            {
                var bar = new VisualElement();
                bar.AddToClassList(ussClassName + "__bar");
                bar.style.height = Length.Percent(heights[i] * 100f);
                var top = new VisualElement();
                top.AddToClassList(ussClassName + "__bar-top");
                top.style.height = Length.Percent(40f);
                bar.Add(top);
                chart.Add(bar);
            }
            split.Add(chart);
            body.Add(split);
            return window;
        }

        static VisualElement EditorTheme()
        {
            var (window, body) = Window("Bagel UI", "UnityEditor.InspectorWindow");
            var split = new VisualElement();
            split.AddToClassList(ussClassName + "__split");
            var editor = new VisualElement();
            editor.AddToClassList(ussClassName + "__half");
            Field(editor, "Zoom", Value("75%", null));
            Field(editor, "Season", Value("Maple", null));
            split.Add(editor);
            // The game's side: the game's classes, not a Button (the Editor theme would restyle .unity-button).
            var game = new VisualElement();
            game.AddToClassList(ussClassName + "__half");
            game.AddToClassList(ussClassName + "__game");
            var button = new VisualElement();
            button.AddToClassList("be-btn");
            button.AddToClassList("be-btn--primary");
            button.AddToClassList(ussClassName + "__game-button");
            button.Add(Text("GO", "be-btn__label be-display"));
            game.Add(button);
            split.Add(game);
            body.Add(split);
            return window;
        }

        static VisualElement BagelUIWindow()
        {
            var (window, body) = Window("Bagel UI", "UnityEditor.InspectorWindow");
            var phones = new VisualElement();
            phones.AddToClassList(ussClassName + "__phones");
            for (int i = 0; i < 4; i++)
            {
                var phone = new VisualElement();
                phone.AddToClassList(ussClassName + "__phone");
                var card = new VisualElement();
                card.AddToClassList(ussClassName + "__phone-card");
                card.EnableInClassList(ussClassName + "__phone-card--gold", i == 1);
                phone.Add(card);
                phones.Add(phone);
            }
            body.Add(phones);
            return window;
        }

        static VisualElement PanelRendererInspector()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "PanelRenderer Icon", "Panel Renderer");
            Field(body, "Source Asset", Value("BakeryAppScreen", "UxmlScript Icon"));
            Field(body, "Panel Settings", Value("ScreenSpacePanelSettings", "PanelSettings Icon"));
            return window;
        }

        static VisualElement PanelSettingsInspector()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "PanelSettings Icon", "ScreenSpacePanelSettings");
            Field(body, "Theme", Value("BagelMapleTheme", "StyleSheet Icon"));
            Field(body, "Scale Mode", Value("Scale With Screen Size", null, true));
            Field(body, "Reference", Value("390 × 844", null));
            return window;
        }

        static VisualElement ReferenceInspector()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "cs Script Icon", "TutorialDriver · Step 1");
            Field(body, "Message", Value("tutorial.collect", "TextAsset Icon"));
            Field(body, "Target", Value("#collect-button (Button)", "Button Icon"));
            return window;
        }

        static VisualElement Accessibility()
        {
            var (window, body) = Window("Accessibility", "UnityEditor.SceneHierarchyWindow");
            Row(body, 0, true, "GameObject Icon", "BagelExpressApp");
            Row(body, 1, true, "Button Icon", "Button · “Collect”", true);
            Row(body, 1, null, "TextAsset Icon", "Text · “Mile End Bagel Bar”");
            Row(body, 1, null, "Toggle Icon", "Toggle · “Sound”");
            return window;
        }

        static VisualElement AnimationWindow()
        {
            var (window, body) = Window("Animation", "UnityEditor.AnimationWindow");
            var split = new VisualElement();
            split.AddToClassList(ussClassName + "__split");
            var tracks = new VisualElement();
            tracks.AddToClassList(ussClassName + "__tracks");
            tracks.Add(Text("TitleIntro", ussClassName + "__label"));
            tracks.Add(Text("#logo-hanko : Scale", ussClassName + "__label"));
            tracks.Add(Text("#logo-hanko : Opacity", ussClassName + "__label"));
            split.Add(tracks);
            var timeline = new VisualElement();
            timeline.AddToClassList(ussClassName + "__timeline");
            foreach (var (row, x) in new[] { (1, 0.1f), (1, 0.55f), (1, 0.8f), (2, 0.1f), (2, 0.55f) })
            {
                var key = new VisualElement();
                key.AddToClassList(ussClassName + "__key");
                key.style.left = Length.Percent(x * 100f);
                key.style.top = Length.Percent(row * 33f + 8f);
                timeline.Add(key);
            }
            var playhead = new VisualElement();
            playhead.AddToClassList(ussClassName + "__playhead");
            timeline.Add(playhead);
            split.Add(timeline);
            body.Add(split);
            return window;
        }

        static VisualElement ResourceTables()
        {
            var (window, body) = Window("Resource Tables", "TextAsset Icon");
            var table = new VisualElement();
            table.AddToClassList(ussClassName + "__table");
            TableRow(table, true, "Key", "English", "日本語", "Français");
            TableRow(table, false, "nav.shops", "Shops", "ショップ", "Boutiques");
            TableRow(table, false, "nav.home", "Home", "ホーム", "Accueil");
            body.Add(table);
            return window;
        }

        static VisualElement TestRunner(bool playMode, params (string name, int depth, bool open)[] rows)
        {
            var (window, body) = Window("Test Runner", "TestPassed");
            var tabs = Bar(body);
            tabs.Add(Tab("EditMode", !playMode));
            tabs.Add(Tab("PlayMode", playMode));
            foreach (var (name, depth, open) in rows)
                Row(body, depth, open ? true : (bool?)null, "TestPassed", name);
            return window;
        }

        // ------------------------------------------------------------------ the ones the game doesn't use

        static VisualElement UssStats()
        {
            var (window, body) = Window("USS Stats", "UnityEditor.ProfilerWindow");
            var table = new VisualElement();
            table.AddToClassList(ussClassName + "__table");
            TableRow(table, true, "Selector", "Matches", "Time");
            TableRow(table, false, ".be-shop-card__name", "24", "0.08 ms");
            TableRow(table, false, ".be-btn:hover", "112", "0.21 ms");
            body.Add(table);
            return window;
        }

        static VisualElement UxmlUpgrade()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "UxmlScript Icon", "HomePage (Visual Tree Asset)");
            var button = Text("Apply Upgrades (Overwrites File)", ussClassName + "__button");
            body.Add(button);
            return window;
        }

        static VisualElement FontSubsetting()
        {
            var (window, body) = Window("Inspector", "UnityEditor.InspectorWindow");
            Header(body, "FontAsset Icon", "NotoSansJP SDF");
            Row(body, 0, true, null, "Font Subsetting");
            Field(body, "Characters", Value("From the project's text", null, true));
            return window;
        }

        static VisualElement ShaderGraph(string kind, (string title, string port) input, (string title, string port) output)
        {
            var (window, body) = Window($"Shader Graph · {kind}", "ScriptableObject Icon");
            var graph = new VisualElement();
            graph.AddToClassList(ussClassName + "__graph");
            graph.Add(Node(input.title, input.port, false));
            var wire = new VisualElement();
            wire.AddToClassList(ussClassName + "__wire");
            graph.Add(wire);
            graph.Add(Node(output.title, output.port, true));
            body.Add(graph);
            return window;
        }

        static VisualElement FindPanelRenderer()
        {
            var (window, body) = Window("Hierarchy", "UnityEditor.SceneHierarchyWindow");
            Row(body, 0, true, "PanelRenderer Icon", "HUD (PanelRenderer)");
            Row(body, 1, true, "PanelRenderer Icon", "Shop sign (PanelRenderer)", true);
            Row(body, 2, true, "UxmlScript Icon", "#sign");
            Row(body, 3, null, "Button Icon", "#open-button");
            return window;
        }

        static VisualElement ReleaseResources()
        {
            var (window, body) = Window("Hierarchy", "UnityEditor.SceneHierarchyWindow");
            Row(body, 0, true, "UxmlScript Icon", "#reveal-screen");
            var released = new[] { "#card-1 · textures released", "#card-2 · textures released", "#confetti · mesh released" };
            foreach (var name in released)
                Row(body, 1, null, "Button Icon", name).AddToClassList(ussClassName + "__row--released");
            return window;
        }

        static VisualElement Figma()
        {
            var flow = new VisualElement();
            flow.AddToClassList(ussClassName + "__flow");
            var frame = new VisualElement();
            frame.AddToClassList(ussClassName + "__figma");
            frame.Add(Text("Frame · Shops", ussClassName + "__figma-title"));
            var card = new VisualElement();
            card.AddToClassList(ussClassName + "__figma-card");
            frame.Add(card);
            flow.Add(frame);
            flow.Add(Text("→", ussClassName + "__arrow"));
            var files = new VisualElement();
            files.AddToClassList(ussClassName + "__column");
            files.Add(File("UxmlScript Icon", "Shops.uxml"));
            files.Add(File("UssScript Icon", "Shops.uss"));
            flow.Add(files);
            return flow;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>An Editor window: a tab with its icon and title, over its body.</summary>
        static (VisualElement window, VisualElement body) Window(string title, string icon, bool small = false)
        {
            var window = new VisualElement();
            window.AddToClassList(ussClassName + "__window");
            window.EnableInClassList(ussClassName + "__window--small", small);
            var tabs = new VisualElement();
            tabs.AddToClassList(ussClassName + "__tabs");
            var tab = new VisualElement();
            tab.AddToClassList(ussClassName + "__tab");
            if (icon != null)
                tab.Add(Icon(icon));
            tab.Add(Text(title, ussClassName + "__tab-label"));
            tabs.Add(tab);
            window.Add(tabs);
            var body = new VisualElement { name = "body" };
            body.AddToClassList(ussClassName + "__body");
            window.Add(body);
            return (window, body);
        }

        static VisualElement Bar(VisualElement body)
        {
            var bar = new VisualElement();
            bar.AddToClassList(ussClassName + "__bar-tabs");
            body.Add(bar);
            return bar;
        }

        static VisualElement Tab(string text, bool selected)
        {
            var tab = Text(text, ussClassName + "__toggle");
            tab.EnableInClassList(ussClassName + "__toggle--on", selected);
            return tab;
        }

        /// <summary>A tree row: indented, with a foldout arrow (null: none), an icon and a name; selected in the Editor's blue.</summary>
        static VisualElement Row(VisualElement body, int depth, bool? open, string icon, string text, bool selected = false)
        {
            var row = new VisualElement();
            row.AddToClassList(ussClassName + "__row");
            row.EnableInClassList(ussClassName + "__row--selected", selected);
            row.style.paddingLeft = 4f + depth * 11f;
            row.Add(Text(open == null ? "" : open.Value ? "▾" : "▸", ussClassName + "__arrow-small"));
            if (icon != null)
                row.Add(Icon(icon));
            row.Add(Text(text, ussClassName + "__label"));
            body.Add(row);
            return row;
        }

        /// <summary>An Inspector header: a component's icon and name on the title bar's gray.</summary>
        static void Header(VisualElement body, string icon, string title)
        {
            var header = new VisualElement();
            header.AddToClassList(ussClassName + "__header");
            header.Add(Icon(icon));
            header.Add(Text(title, ussClassName + "__header-label"));
            body.Add(header);
        }

        /// <summary>An Inspector field: its label, and its value.</summary>
        static void Field(VisualElement body, string label, VisualElement value)
        {
            var field = new VisualElement();
            field.AddToClassList(ussClassName + "__field");
            field.Add(Text(label, ussClassName + "__field-label"));
            field.Add(value);
            body.Add(field);
        }

        /// <summary>A field's value: text in an input box, an object field (with an icon) or a dropdown.</summary>
        static VisualElement Value(string text, string icon, bool dropdown = false)
        {
            var value = new VisualElement();
            value.AddToClassList(ussClassName + "__value");
            value.EnableInClassList(ussClassName + "__value--object", icon != null);
            if (icon != null)
                value.Add(Icon(icon));
            value.Add(Text(text, ussClassName + "__value-label"));
            if (dropdown)
                value.Add(Text("▾", ussClassName + "__arrow-small"));
            return value;
        }

        static VisualElement Swatch(Color color)
        {
            var swatch = new VisualElement();
            swatch.AddToClassList(ussClassName + "__swatch");
            swatch.style.backgroundColor = color;
            return swatch;
        }

        static void TableRow(VisualElement table, bool header, params string[] cells)
        {
            var row = new VisualElement();
            row.AddToClassList(ussClassName + "__table-row");
            row.EnableInClassList(ussClassName + "__table-row--header", header);
            foreach (var cell in cells)
                row.Add(Text(cell, ussClassName + "__cell"));
            table.Add(row);
        }

        static VisualElement Node(string title, string port, bool input)
        {
            var node = new VisualElement();
            node.AddToClassList(ussClassName + "__node");
            node.Add(Text(title, ussClassName + "__node-title"));
            var row = new VisualElement();
            row.AddToClassList(ussClassName + "__node-port");
            row.EnableInClassList(ussClassName + "__node-port--in", input);
            var dot = new VisualElement();
            dot.AddToClassList(ussClassName + "__port-dot");
            row.Add(dot);
            row.Add(Text(port, ussClassName + "__label"));
            node.Add(row);
            return node;
        }

        static VisualElement File(string icon, string name)
        {
            var file = new VisualElement();
            file.AddToClassList(ussClassName + "__file");
            file.Add(Icon(icon));
            file.Add(Text(name, ussClassName + "__label"));
            return file;
        }

        static Image Icon(string name)
        {
            var icon = new Image { image = EditorGUIUtility.IconContent(name)?.image, scaleMode = ScaleMode.ScaleToFit };
            icon.AddToClassList(ussClassName + "__icon");
            return icon;
        }

        static Label Text(string text, string classNames)
        {
            var label = new Label(text);
            foreach (var className in classNames.Split(' '))
                label.AddToClassList(className);
            return label;
        }
    }
}
