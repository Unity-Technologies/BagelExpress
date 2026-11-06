using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The Editor's windows recreated for the Bagel Demo window's slides, at the Editor's own size: the Hierarchy, the
    /// Inspector (an element's, a rule's, a GameObject's), the UI Viewport, Style Sheets, the UI Library, the Animation and
    /// Profiler windows, Resource Tables, UI Builder, the Game view and the Bagel menu. Pictures: nothing in them can be
    /// clicked. In the Editor's theme (<see cref="Themed{T}"/>: its sheets, colors, font and icons), laid out like the
    /// demo layout (each window's tab next to the others of its group), styled in BagelDemoEditorKit.uss.
    /// <para>
    /// A window that shows the game (the UI Viewport, the Game view) keeps the Editor's theme off its canvas, the way the
    /// Bagel windows keep it off their phones: only its tabs and toolbars get it.
    /// </para>
    /// </summary>
    public sealed class BagelDemoEditorKit
    {
        public const string styleSheetPath = "Assets/UI/Editor/B10_EditorTools/BagelDemo/BagelDemoEditorKit.uss";
        public static readonly string ussClassName = "bgd-ed";

        public const string sceneIcon = "SceneAsset Icon";
        public const string uxmlIcon = "UxmlScript Icon";

        [Flags]
        public enum Mark
        {
            None = 0,
            /// <summary>A foldout arrow, open (▾) or closed (▸); neither: a leaf.</summary>
            Open = 1,
            Closed = 2,
            Selected = 4,
            /// <summary>Unsaved: a * after the name.</summary>
            Dirty = 8,
            /// <summary>Being dragged: faded.</summary>
            Dragged = 16,
            /// <summary>Where the dragged row drops: a line under this one.</summary>
            DropBelow = 32,
            /// <summary>Just added.</summary>
            New = 64,
            /// <summary>A template instance: the › that opens it at the end of the row.</summary>
            Instance = 128,
            Bold = 256,
        }

        [Flags]
        public enum FieldMark
        {
            None = 0,
            /// <summary>Its value is a USS variable: the { } before the label.</summary>
            Variable = 1,
            /// <summary>A sub-field, indented (a gradient's type, angle…).</summary>
            Sub = 2,
            /// <summary>What the beat changes: lit.</summary>
            Highlighted = 4,
        }

        public enum ClassState { Kept, Removed, Added }

        /// <summary>A row of a tree (the Hierarchy, the UI Library): an element is shown by its #name and .classes.</summary>
        public readonly struct Node
        {
            public readonly int depth;
            public readonly string text;
            public readonly string icon;
            public readonly string classes;
            public readonly string asset;
            public readonly Mark mark;

            public Node(int depth, string text, string icon, string classes, string asset, Mark mark)
            {
                this.depth = depth;
                this.text = text;
                this.icon = icon;
                this.classes = classes;
                this.asset = asset;
                this.mark = mark;
            }

            /// <summary>Named elements (lowercase names) show as #name; GameObjects, types and folders as they are.</summary>
            public bool isElement => !string.IsNullOrEmpty(text) && char.IsLower(text[0]);
        }

        public static Node H(int depth, string text, string icon, string classes = null, string asset = null, Mark mark = Mark.None) =>
            new(depth, text, icon, classes, asset, mark);

        // The demo layout's groups of tabs (BagelExpressDemo.wlt), and each window's icon.
        static readonly string[][] s_Groups =
        {
            new[] { "Hierarchy", "Style Sheets" },
            new[] { "Project", "UI Library" },
            new[] { "Scene", "UI Viewport", "Animation", "Profiler", "Bagel Features", "Console" },
            new[] { "Inspector", "Game" },
        };

        static readonly Dictionary<string, string> s_Icons = new()
        {
            ["Hierarchy"] = "UnityEditor.SceneHierarchyWindow",
            ["Style Sheets"] = "UssScript Icon",
            ["Project"] = "Project",
            ["UI Library"] = "UIBuilder",
            ["Scene"] = "UnityEditor.SceneView",
            ["UI Viewport"] = "UnityEditor.GameView",
            ["Animation"] = "UnityEditor.AnimationWindow",
            ["Profiler"] = "UnityEditor.ProfilerWindow",
            ["Console"] = "UnityEditor.ConsoleWindow",
            ["Inspector"] = "UnityEditor.InspectorWindow",
            ["Game"] = "UnityEditor.GameView",
            ["UI Builder"] = "UIBuilder",
            ["Resource Tables"] = "TextAsset Icon",
        };

        readonly IReadOnlyList<StyleSheet> m_EditorSheets;
        readonly StyleSheet m_KitSheet;

        public BagelDemoEditorKit(IReadOnlyList<StyleSheet> editorSheets)
        {
            m_EditorSheets = editorSheets ?? Array.Empty<StyleSheet>();
            m_KitSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
        }

        /// <summary>Editor UI: the Editor's theme on it, then the kit's styles over it.</summary>
        public T Themed<T>(T part) where T : VisualElement
        {
            part.AddToClassList(ussClassName);
            foreach (var sheet in m_EditorSheets)
                if (sheet != null && !part.styleSheets.Contains(sheet))
                    part.styleSheets.Add(sheet);
            if (m_KitSheet != null && !part.styleSheets.Contains(m_KitSheet))
                part.styleSheets.Add(m_KitSheet);
            return part;
        }

        // ------------------------------------------------------------------ windows

        /// <summary>
        /// A docked window: its group's tabs (this one in front) over its body. <paramref name="gameInside"/>: the body shows
        /// the game, so only the tabs get the Editor's theme (and the parts the caller themes).
        /// </summary>
        public (VisualElement window, VisualElement body) Window(string title, bool gameInside = false)
        {
            var window = El("bgd-ed__window");
            var tabs = Themed(El("bgd-ed__tabs"));
            var group = Array.Find(s_Groups, g => Array.IndexOf(g, title) >= 0) ?? new[] { title };
            foreach (var name in group)
            {
                var tab = El("bgd-ed__tab");
                tab.EnableInClassList("bgd-ed__tab--active", name == title);
                tab.Add(TabIcon(name));
                tab.Add(Text(name, "bgd-ed__tab-label"));
                tabs.Add(tab);
            }
            tabs.Add(Grow());
            tabs.Add(Icon("_Menu", "bgd-ed__tab-menu"));
            window.Add(tabs);
            var body = El("bgd-ed__body");
            window.Add(body);
            if (!gameInside)
                Themed(window);
            return (window, body);
        }

        /// <summary>A window that floats over the layout (UI Builder, Resource Tables): a title bar instead of tabs.</summary>
        public (VisualElement window, VisualElement body) FloatingWindow(string title, bool gameInside = false)
        {
            var (window, body) = Window(title, gameInside);
            window.AddToClassList("bgd-ed__window--floating");
            return (window, body);
        }

        public VisualElement Hierarchy(string search, params Node[] nodes)
        {
            var (window, body) = Window("Hierarchy");
            var bar = Toolbar(body);
            bar.Add(Icon("Toolbar Plus More"));
            bar.Add(SearchField(search, grow: true));
            if (search == null)
            {
                var header = El("bgd-ed__columns");
                header.Add(Icon("scenevis_visible"));
                header.Add(Icon("scenepicking_pickable"));
                header.Add(Text("Name", "bgd-ed__column-label"));
                body.Add(header);
            }
            for (int i = 0; i < nodes.Length; i++)
                body.Add(Row(nodes[i], i));
            return window;
        }

        VisualElement Row(Node node, int index)
        {
            var row = El("bgd-ed__row");
            row.EnableInClassList("bgd-ed__row--odd", index % 2 == 1);
            row.EnableInClassList("bgd-ed__row--selected", node.mark.HasFlag(Mark.Selected));
            row.EnableInClassList("bgd-ed__row--dragged", node.mark.HasFlag(Mark.Dragged));
            row.EnableInClassList("bgd-ed__row--new", node.mark.HasFlag(Mark.New));
            row.style.paddingLeft = 6f + node.depth * 14f;
            row.Add(Text(node.mark.HasFlag(Mark.Open) ? "▾" : node.mark.HasFlag(Mark.Closed) ? "▸" : "", "bgd-ed__foldout"));
            row.Add(Icon(node.icon));
            var name = Text(node.isElement ? "#" + node.text : node.text, node.isElement ? "bgd-ed__name bgd-ed__name--element" : "bgd-ed__name");
            name.EnableInClassList("bgd-ed__name--bold", node.mark.HasFlag(Mark.Bold));
            row.Add(name);
            if (!string.IsNullOrEmpty(node.asset))
                row.Add(Text(node.asset + (node.mark.HasFlag(Mark.Dirty) ? " *" : ""), "bgd-ed__asset"));
            else if (node.mark.HasFlag(Mark.Dirty))
                row.Add(Text("*", "bgd-ed__asset"));
            if (!string.IsNullOrEmpty(node.classes))
                row.Add(Text(node.classes, "bgd-ed__classes"));
            if (node.mark.HasFlag(Mark.Instance))
            {
                row.Add(Grow());
                row.Add(Text("›", "bgd-ed__open-instance"));
            }
            if (node.mark.HasFlag(Mark.DropBelow))
                row.Add(El("bgd-ed__drop-line"));
            return row;
        }

        // ------------------------------------------------------------------ the Inspector

        public VisualElement Inspector(params VisualElement[] parts)
        {
            var (window, body) = Window("Inspector");
            foreach (var part in parts)
                body.Add(part);
            return window;
        }

        /// <summary>An element's header: its type's icon, type and name, and the UXML it comes from.</summary>
        public VisualElement ElementHeader(string type, string icon, string name, string uxml)
        {
            var header = El("bgd-ed__header");
            header.Add(Icon(icon, "bgd-ed__header-icon"));
            var column = El("bgd-ed__header-column");
            column.Add(Text(type, "bgd-ed__header-type"));
            var nameRow = El("bgd-ed__header-row");
            nameRow.Add(Check(true));
            nameRow.Add(Box(Text(name, "bgd-ed__value-label"), "bgd-ed__value"));
            column.Add(nameRow);
            header.Add(column);
            var block = El("bgd-ed__header-block");
            block.Add(header);
            var source = El("bgd-ed__header-row bgd-ed__header-source");
            source.Add(Text("UXML", "bgd-ed__field-label bgd-ed__field-label--short"));
            source.Add(ObjectValue(uxml, uxmlIcon));
            source.Add(Icon("_Menu"));
            source.Add(Text("›", "bgd-ed__open-instance"));
            block.Add(source);
            return block;
        }

        /// <summary>A USS rule's header: the selector and its style sheet.</summary>
        public VisualElement RuleHeader(string selector, string file)
        {
            var block = El("bgd-ed__header-block");
            var header = El("bgd-ed__header");
            header.Add(Icon("UssScript Icon", "bgd-ed__header-icon"));
            var column = El("bgd-ed__header-column");
            column.Add(Text("Selector", "bgd-ed__header-type"));
            column.Add(Box(Text(selector, "bgd-ed__value-label bgd-ed__selector"), "bgd-ed__value"));
            header.Add(column);
            block.Add(header);
            var source = El("bgd-ed__header-row bgd-ed__header-source");
            source.Add(Text("USS", "bgd-ed__field-label bgd-ed__field-label--short"));
            source.Add(ObjectValue(file, "UssScript Icon"));
            block.Add(source);
            return block;
        }

        public VisualElement GameObjectHeader(string name)
        {
            var block = El("bgd-ed__header-block");
            var header = El("bgd-ed__header");
            header.Add(Icon("GameObject Icon", "bgd-ed__header-icon"));
            var column = El("bgd-ed__header-column");
            var nameRow = El("bgd-ed__header-row");
            nameRow.Add(Check(true));
            nameRow.Add(Box(Text(name, "bgd-ed__value-label"), "bgd-ed__value"));
            column.Add(nameRow);
            var tags = El("bgd-ed__header-row");
            tags.Add(Text("Tag", "bgd-ed__field-label bgd-ed__field-label--short"));
            tags.Add(Dropdown("Untagged"));
            tags.Add(Text("Layer", "bgd-ed__field-label bgd-ed__field-label--short"));
            tags.Add(Dropdown("UI"));
            column.Add(tags);
            header.Add(column);
            block.Add(header);
            return block;
        }

        /// <summary>The element Inspector's search field.</summary>
        public VisualElement Search(string text)
        {
            var row = El("bgd-ed__search-row");
            row.Add(SearchField(text, grow: true));
            return row;
        }

        /// <summary>The element Inspector's filters: None, Set Values, Variables, Bindings, Animation.</summary>
        public VisualElement Filters(string selected)
        {
            var row = El("bgd-ed__filters");
            row.Add(Text("Filter:", "bgd-ed__filters-label"));
            foreach (var filter in new[] { "None", "Set Values", "Variables", "Bindings", "Animation" })
            {
                var pill = Text(filter, "bgd-ed__filter");
                pill.EnableInClassList("bgd-ed__filter--on", filter == selected);
                row.Add(pill);
            }
            return row;
        }

        /// <summary>A foldout of fields, the way the element Inspector groups styles.</summary>
        public VisualElement Section(string title, params VisualElement[] fields)
        {
            var section = El("bgd-ed__section");
            var header = El("bgd-ed__section-header");
            header.Add(Text("▾", "bgd-ed__foldout"));
            header.Add(Text(title, "bgd-ed__section-title"));
            section.Add(header);
            foreach (var field in fields)
                section.Add(field);
            return section;
        }

        /// <summary>A component on a GameObject: its title bar and fields.</summary>
        public VisualElement Component(string title, string icon, params VisualElement[] fields)
        {
            var component = El("bgd-ed__section");
            var header = El("bgd-ed__component-header");
            header.Add(Text("▾", "bgd-ed__foldout"));
            header.Add(Icon(icon));
            header.Add(Check(true));
            header.Add(Text(title, "bgd-ed__section-title"));
            header.Add(Grow());
            header.Add(Icon("_Menu"));
            component.Add(header);
            foreach (var field in fields)
                component.Add(field);
            return component;
        }

        public VisualElement Field(string label, VisualElement value, FieldMark mark = FieldMark.None)
        {
            var field = El("bgd-ed__field");
            field.EnableInClassList("bgd-ed__field--sub", mark.HasFlag(FieldMark.Sub));
            field.EnableInClassList("bgd-ed__field--highlighted", mark.HasFlag(FieldMark.Highlighted));
            var name = El("bgd-ed__field-name");
            name.Add(Text(mark.HasFlag(FieldMark.Variable) ? "{ }" : "", "bgd-ed__variable"));
            name.Add(Text(label, "bgd-ed__field-label"));
            field.Add(name);
            field.Add(value);
            return field;
        }

        public VisualElement TextValue(string text) => Box(Text(text, "bgd-ed__value-label"), "bgd-ed__value");

        public VisualElement ObjectValue(string text, string icon)
        {
            var value = Box(Icon(icon), "bgd-ed__value bgd-ed__value--object");
            value.Add(Text(text, "bgd-ed__value-label"));
            value.Add(Icon("Search On Icon", "bgd-ed__object-picker"));
            return value;
        }

        public VisualElement Dropdown(string text)
        {
            var value = Box(Text(text, "bgd-ed__value-label"), "bgd-ed__value bgd-ed__value--dropdown");
            value.Add(Text("▾", "bgd-ed__dropdown-arrow"));
            return value;
        }

        public VisualElement NumberValue(string text, string unit = null)
        {
            var value = Box(Text(text, "bgd-ed__value-label"), "bgd-ed__value bgd-ed__value--number");
            if (unit != null)
                value.Add(Text(unit, "bgd-ed__unit"));
            return value;
        }

        public VisualElement ColorValue(Color color)
        {
            var value = El("bgd-ed__value bgd-ed__value--color");
            var bar = El("bgd-ed__color-bar");
            bar.style.backgroundColor = color;
            value.Add(bar);
            value.Add(Icon("pick", "bgd-ed__eyedropper"));
            return value;
        }

        /// <summary>A gradient's stops, as the Inspector shows them: a bar through the colors.</summary>
        public VisualElement GradientValue(params Color[] colors)
        {
            var value = El("bgd-ed__value bgd-ed__value--gradient");
            const int slices = 32;
            for (int i = 0; i < slices; i++)
            {
                var t = i / (slices - 1f) * (colors.Length - 1);
                var a = Mathf.Clamp(Mathf.FloorToInt(t), 0, colors.Length - 1);
                var b = Mathf.Min(a + 1, colors.Length - 1);
                var slice = El("bgd-ed__gradient-slice");
                slice.style.backgroundColor = Color.Lerp(colors[a], colors[b], t - a);
                value.Add(slice);
            }
            return value;
        }

        /// <summary>The Style Class List: an input, then a chip per class (one taken off, one just added).</summary>
        public VisualElement ClassList(params (string name, ClassState state)[] classes)
        {
            var list = El("bgd-ed__class-list");
            var add = El("bgd-ed__header-row");
            add.Add(Box(Text("Add Style Class to List", "bgd-ed__value-label bgd-ed__placeholder"), "bgd-ed__value"));
            add.Add(Text("Add", "bgd-ed__button"));
            list.Add(add);
            var chips = El("bgd-ed__chips");
            foreach (var (name, state) in classes)
            {
                var chip = El("bgd-ed__chip");
                chip.EnableInClassList("bgd-ed__chip--removed", state == ClassState.Removed);
                chip.EnableInClassList("bgd-ed__chip--added", state == ClassState.Added);
                chip.Add(Text(name, "bgd-ed__chip-label"));
                chip.Add(Text("×", "bgd-ed__chip-close"));
                chips.Add(chip);
            }
            list.Add(chips);
            return list;
        }

        /// <summary>The element Inspector's Components: a row per component, then Add Component.</summary>
        public VisualElement ComponentsSection(params VisualElement[] rows)
        {
            var section = Section("Components", rows);
            if (rows.Length == 0)
                section.Add(Text("No components", "bgd-ed__placeholder bgd-ed__empty"));
            return section;
        }

        public VisualElement ComponentRow(string name, params VisualElement[] fields)
        {
            var component = El("bgd-ed__component");
            var header = El("bgd-ed__component-row");
            header.Add(Icon("cs Script Icon"));
            header.Add(Text(name, "bgd-ed__section-title"));
            header.Add(Grow());
            header.Add(Icon("_Menu"));
            component.Add(header);
            foreach (var field in fields)
                component.Add(field);
            return component;
        }

        /// <summary>The Add Component button, open on its menu with one item under the pointer.</summary>
        public VisualElement AddComponent(string[] items, string highlighted)
        {
            var block = El("bgd-ed__add-component");
            block.Add(Text("Add Component", "bgd-ed__button bgd-ed__button--wide"));
            var menu = El("bgd-ed__popup");
            menu.Add(SearchField("", grow: false));
            foreach (var item in items)
            {
                var row = El("bgd-ed__popup-item");
                row.EnableInClassList("bgd-ed__popup-item--on", item == highlighted);
                row.Add(Icon("cs Script Icon"));
                row.Add(Text(item, "bgd-ed__popup-label"));
                menu.Add(row);
            }
            block.Add(menu);
            return block;
        }

        /// <summary>The custom UXML property drawer for a pastry: a cell per kind, with the game's art.</summary>
        public VisualElement PastryPicker(PastryKind selected, params PastryKind[] kinds)
        {
            var picker = El("bgd-ed__picker");
            foreach (var kind in kinds)
            {
                var cell = El("bgd-ed__picker-cell");
                cell.EnableInClassList("bgd-ed__picker-cell--selected", kind == selected);
                var pastry = new PastryIcon(kind) { pickingMode = PickingMode.Ignore };
                pastry.AddToClassList("bgd-ed__pastry");
                cell.Add(pastry);
                picker.Add(cell);
            }
            return picker;
        }

        // ------------------------------------------------------------------ windows that show the game

        /// <summary>The UI Viewport: its breadcrumbs and toolbar over the document, on its dark canvas.</summary>
        public VisualElement UIViewport(string[] breadcrumb, string document, VisualElement canvas)
        {
            var (window, body) = Window("UI Viewport", gameInside: true);
            var crumbs = Themed(El("bgd-ed__breadcrumbs"));
            for (int i = 0; i < breadcrumb.Length; i++)
            {
                var crumb = El("bgd-ed__crumb");
                crumb.Add(Icon(i == 0 ? "GameObject Icon" : uxmlIcon));
                crumb.Add(Text(breadcrumb[i], "bgd-ed__crumb-label"));
                crumbs.Add(crumb);
            }
            body.Add(crumbs);
            var bar = Themed(El("bgd-ed__toolbar bgd-ed__toolbar--viewport"));
            bar.Add(Text("Save", "bgd-ed__toolbar-item bgd-ed__toolbar-item--dim"));
            bar.Add(Text("94%  ▾", "bgd-ed__toolbar-item"));
            bar.Add(Text("Fit viewport", "bgd-ed__toolbar-item"));
            bar.Add(Grow());
            bar.Add(Text("Bagel Maple Theme ▾", "bgd-ed__toolbar-item"));
            bar.Add(Text("Preview interactions", "bgd-ed__toolbar-item"));
            body.Add(bar);
            var stage = El("bgd-ed__canvas");
            var frame = El("bgd-ed__document");
            frame.Add(Themed(Text(document, "bgd-ed__document-title")));
            frame.Add(canvas);
            stage.Add(frame);
            body.Add(stage);
            return window;
        }

        /// <summary>The Game view: its toolbar over the game.</summary>
        public VisualElement GameView(VisualElement canvas)
        {
            var (window, body) = Window("Game", gameInside: true);
            var bar = Themed(El("bgd-ed__toolbar"));
            bar.Add(Text("Game ▾", "bgd-ed__toolbar-item"));
            bar.Add(Text("Bagel 2x (perf) ▾", "bgd-ed__toolbar-item"));
            body.Add(bar);
            var stage = El("bgd-ed__canvas bgd-ed__canvas--game");
            stage.Add(canvas);
            body.Add(stage);
            return window;
        }

        /// <summary>The main toolbar's Play, Pause and Step, Play on while playing.</summary>
        public VisualElement PlayButtons(bool playing)
        {
            var group = Themed(El("bgd-ed__play"));
            foreach (var (icon, on) in new[] { ("PlayButton", playing), ("PauseButton", false), ("StepButton", false) })
            {
                var button = El("bgd-ed__play-button");
                button.EnableInClassList("bgd-ed__play-button--on", on);
                button.Add(Icon(icon));
                group.Add(button);
            }
            return group;
        }

        // ------------------------------------------------------------------ the other windows

        /// <summary>The Style Sheets window: the document's rules, one selected.</summary>
        public VisualElement StyleSheets(string search, string file, string[] selectors, string selected)
        {
            var (window, body) = Window("Style Sheets");
            var searchRow = El("bgd-ed__search-row");
            searchRow.Add(SearchField(search, grow: true));
            body.Add(searchRow);
            var add = El("bgd-ed__toolbar");
            add.Add(Icon("Toolbar Plus More"));
            add.Add(Box(Text("Add new selector…", "bgd-ed__value-label bgd-ed__placeholder"), "bgd-ed__value"));
            body.Add(add);
            body.Add(Text("Rules", "bgd-ed__columns bgd-ed__column-label"));
            var fileRow = El("bgd-ed__section-header");
            fileRow.Add(Text("▾", "bgd-ed__foldout"));
            fileRow.Add(Text(file, "bgd-ed__section-title"));
            body.Add(fileRow);
            foreach (var selector in selectors)
            {
                var row = El("bgd-ed__rule-row");
                var chip = Text(selector, "bgd-ed__rule");
                chip.EnableInClassList("bgd-ed__rule--selected", selector == selected);
                row.Add(chip);
                body.Add(row);
            }
            return window;
        }

        /// <summary>The UI Library: a tab of folders and elements.</summary>
        public VisualElement Library(string tab, params Node[] nodes)
        {
            var (window, body) = Window("UI Library");
            var searchRow = El("bgd-ed__search-row");
            searchRow.Add(SearchField(null, grow: true, placeholder: "Search item"));
            body.Add(searchRow);
            var tabs = El("bgd-ed__library-tabs");
            tabs.Add(Text(tab + "  ▾", "bgd-ed__library-tab"));
            body.Add(tabs);
            body.Add(Text("Name", "bgd-ed__columns bgd-ed__column-label"));
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                // The Library shows types and folders: no #.
                body.Add(Row(new Node(node.depth, node.text, node.icon, null, null, node.mark), i));
            }
            return window;
        }

        /// <summary>The Animation window: its toolbar, the clip's tracks and their keys, the playhead (recording: red).</summary>
        public VisualElement Animation(string clip, string[] tracks, (int track, float time)[] keys, bool recording, int frame, int selectedTrack = -1)
        {
            var (window, body) = Window("Animation");
            window.EnableInClassList("bgd-ed__window--recording", recording);
            var split = El("bgd-ed__split");
            var left = El("bgd-ed__tracks");
            var controls = El("bgd-ed__toolbar");
            controls.Add(Text("Preview", "bgd-ed__toolbar-item"));
            var record = Icon("Animation.Record");
            record.EnableInClassList("bgd-ed__record--on", recording);
            controls.Add(record);
            foreach (var icon in new[] { "Animation.PrevKey", "Animation.Play", "Animation.NextKey" })
                controls.Add(Icon(icon));
            controls.Add(Grow());
            controls.Add(Box(Text(frame.ToString(), "bgd-ed__value-label"), "bgd-ed__value bgd-ed__value--frame"));
            left.Add(controls);
            var clipRow = El("bgd-ed__toolbar");
            clipRow.Add(Dropdown(clip));
            left.Add(clipRow);
            for (int i = 0; i < tracks.Length; i++)
            {
                var track = El("bgd-ed__track");
                track.EnableInClassList("bgd-ed__track--selected", i == selectedTrack);
                track.Add(Icon("VisualElement"));
                track.Add(Text(tracks[i], "bgd-ed__track-label"));
                left.Add(track);
            }
            split.Add(left);

            var timeline = El("bgd-ed__timeline");
            var ruler = El("bgd-ed__ruler");
            foreach (var mark in new[] { "0:00", "0:30", "1:00", "1:30" })
                ruler.Add(Text(mark, "bgd-ed__ruler-mark"));
            timeline.Add(ruler);
            var lanes = El("bgd-ed__lanes");
            for (int i = 0; i < tracks.Length; i++)
            {
                var lane = El("bgd-ed__lane");
                lane.EnableInClassList("bgd-ed__lane--selected", i == selectedTrack);
                foreach (var (track, time) in keys)
                {
                    if (track != i)
                        continue;
                    var key = El("bgd-ed__key");
                    key.style.left = Length.Percent(time * 100f);
                    lane.Add(key);
                }
                lanes.Add(lane);
            }
            timeline.Add(lanes);
            var playhead = El("bgd-ed__playhead");
            playhead.style.left = Length.Percent(Mathf.Clamp01(frame / 120f) * 100f);
            timeline.Add(playhead);
            split.Add(timeline);
            body.Add(split);
            return window;
        }

        /// <summary>The Profiler on its UI Toolkit modules; <paramref name="spike"/>: the frames after F are much longer.</summary>
        public VisualElement Profiler(bool spike)
        {
            var (window, body) = Window("Profiler");
            var bar = Toolbar(body);
            bar.Add(Text("Profiler Modules ▾", "bgd-ed__toolbar-item"));
            bar.Add(Icon("Animation.Record", "bgd-ed__record--on"));
            bar.Add(Grow());
            bar.Add(Text("Frame: 1,420", "bgd-ed__toolbar-item"));
            var split = El("bgd-ed__split");
            var modules = El("bgd-ed__modules");
            foreach (var (name, color) in new[] { ("CPU Usage", new Color(0.36f, 0.72f, 0.42f)), ("Rendering", new Color(0.3f, 0.55f, 0.85f)), ("UI Toolkit", new Color(0.93f, 0.55f, 0.18f)), ("UI Toolkit Details", new Color(0.85f, 0.35f, 0.6f)) })
            {
                var module = El("bgd-ed__module");
                var swatch = El("bgd-ed__swatch");
                swatch.style.backgroundColor = color;
                module.Add(swatch);
                module.Add(Text(name, "bgd-ed__module-label"));
                modules.Add(module);
            }
            split.Add(modules);
            var chart = El("bgd-ed__chart");
            var line = El("bgd-ed__chart-line");
            line.Add(Text("16ms (60FPS)", "bgd-ed__chart-line-label"));
            chart.Add(line);
            const int frames = 28;
            for (int i = 0; i < frames; i++)
            {
                var after = spike && i >= frames / 2;
                var noise = Mathf.PerlinNoise(i * 0.37f, 0.5f);
                var bar2 = El("bgd-ed__frame");
                bar2.style.height = Length.Percent((after ? 0.72f : 0.28f) * 100f + noise * 14f);
                var ui = El("bgd-ed__frame-ui");
                ui.style.height = Length.Percent(after ? 62f : 24f);
                bar2.Add(ui);
                chart.Add(bar2);
            }
            split.Add(chart);
            body.Add(split);
            return window;
        }

        /// <summary>Resource Tables: a table of strings, one cell being edited.</summary>
        public VisualElement ResourceTables(string search, string[] columns, string[][] rows, int editedRow, int editedColumn)
        {
            var (window, body) = FloatingWindow("Resource Tables");
            var bar = Toolbar(body);
            bar.Add(Dropdown("BagelStrings"));
            bar.Add(SearchField(search, grow: true));
            var table = El("bgd-ed__table");
            var header = El("bgd-ed__table-row bgd-ed__table-row--header");
            foreach (var column in columns)
                header.Add(Text(column, "bgd-ed__cell"));
            table.Add(header);
            for (int r = 0; r < rows.Length; r++)
            {
                var row = El("bgd-ed__table-row");
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var cell = Text(rows[r][c], "bgd-ed__cell");
                    cell.EnableInClassList("bgd-ed__cell--key", c == 0);
                    cell.EnableInClassList("bgd-ed__cell--edited", r == editedRow && c == editedColumn);
                    row.Add(cell);
                }
                table.Add(row);
            }
            body.Add(table);
            return window;
        }

        /// <summary>UI Builder in StyleSheet Editing Mode: the style sheet's selectors, and the selected one's Inspector.</summary>
        public VisualElement UIBuilder(string file, string[] selectors, string selected, params VisualElement[] fields)
        {
            var (window, body) = FloatingWindow("UI Builder");
            var bar = Toolbar(body);
            bar.Add(Text("File ▾", "bgd-ed__toolbar-item"));
            bar.Add(Text(file, "bgd-ed__toolbar-item bgd-ed__toolbar-item--title"));
            bar.Add(Grow());
            bar.Add(Text("StyleSheet Editing Mode", "bgd-ed__toolbar-item bgd-ed__toolbar-item--on"));
            var split = El("bgd-ed__split");
            var sheets = El("bgd-ed__builder-pane");
            sheets.Add(Text("StyleSheets", "bgd-ed__pane-title"));
            var fileRow = El("bgd-ed__component-row");
            fileRow.Add(Icon("UssScript Icon"));
            fileRow.Add(Text(file, "bgd-ed__section-title"));
            sheets.Add(fileRow);
            foreach (var selector in selectors)
            {
                var row = El("bgd-ed__rule-row");
                var chip = Text(selector, "bgd-ed__rule");
                chip.EnableInClassList("bgd-ed__rule--selected", selector == selected);
                row.Add(chip);
                sheets.Add(row);
            }
            split.Add(sheets);
            var inspector = El("bgd-ed__builder-pane bgd-ed__builder-pane--inspector");
            inspector.Add(Text("Inspector", "bgd-ed__pane-title"));
            inspector.Add(Box(Text(selected, "bgd-ed__value-label bgd-ed__selector"), "bgd-ed__value bgd-ed__value--selector"));
            var section = Section("Background", fields);
            inspector.Add(section);
            split.Add(inspector);
            body.Add(split);
            return window;
        }

        /// <summary>The Project window, filtered on the files a demo changed (each marked as modified).</summary>
        public VisualElement Files(params (string icon, string name)[] files)
        {
            var (window, body) = Window("Project");
            var bar = Toolbar(body);
            bar.Add(Icon("Toolbar Plus More"));
            bar.Add(SearchField("changed today", grow: true));
            for (int i = 0; i < files.Length; i++)
            {
                var row = El("bgd-ed__row");
                row.EnableInClassList("bgd-ed__row--odd", i % 2 == 1);
                row.Add(Icon(files[i].icon));
                row.Add(Text(files[i].name, "bgd-ed__name"));
                row.Add(Grow());
                row.Add(Text("M", "bgd-ed__modified"));
                body.Add(row);
            }
            return window;
        }

        /// <summary>The Bagel menu open on Demo › Reset All.</summary>
        public VisualElement BagelMenu()
        {
            var root = Themed(El("bgd-ed__menus"));
            var bar = El("bgd-ed__menubar");
            foreach (var item in new[] { "Window", "Help", "Bagel" })
            {
                var label = Text(item, "bgd-ed__menubar-item");
                label.EnableInClassList("bgd-ed__menubar-item--open", item == "Bagel");
                bar.Add(label);
            }
            root.Add(bar);
            var panels = El("bgd-ed__menu-panels");
            panels.Add(Menu(new[] { "Bagel UI", "Bagel Features", "Bagel Demo", "-", "Localization ›", "Dev ›", "Demo ›" }, "Demo ›"));
            var submenu = Menu(new[] { "Reset All", "-", "Discard Project Changes", "Reload Main Scene", "Reset Layout and Toolbar", "Reset Bagel Project States" }, "Reset All");
            submenu.AddToClassList("bgd-ed__menu--sub");
            panels.Add(submenu);
            root.Add(panels);
            return root;
        }

        VisualElement Menu(string[] items, string highlighted)
        {
            var menu = El("bgd-ed__menu");
            foreach (var item in items)
            {
                if (item == "-")
                {
                    menu.Add(El("bgd-ed__menu-separator"));
                    continue;
                }
                var row = El("bgd-ed__menu-item");
                row.EnableInClassList("bgd-ed__menu-item--on", item == highlighted);
                var sub = item.EndsWith("›");
                row.Add(Text(sub ? item.TrimEnd('›', ' ') : item, "bgd-ed__menu-label"));
                if (sub)
                {
                    row.Add(Grow());
                    row.Add(Text("›", "bgd-ed__menu-label"));
                }
                menu.Add(row);
            }
            return menu;
        }

        // ------------------------------------------------------------------ small things

        /// <summary>A key on the keyboard, to say out loud.</summary>
        public VisualElement Key(string text)
        {
            var key = El("bgd-ed__key-cap");
            key.Add(Text(text, "bgd-ed__key-label"));
            return key;
        }

        /// <summary>A value changed: the old one struck through, the new one lit.</summary>
        public VisualElement Change(string from, string to)
        {
            var change = Themed(El("bgd-ed__change"));
            change.Add(Text(from, "bgd-ed__change-from"));
            change.Add(Text("→", "bgd-ed__change-arrow"));
            change.Add(Text(to, "bgd-ed__change-to"));
            return change;
        }

        VisualElement Toolbar(VisualElement body)
        {
            var bar = El("bgd-ed__toolbar");
            body.Add(bar);
            return bar;
        }

        VisualElement SearchField(string text, bool grow, string placeholder = null)
        {
            var field = El("bgd-ed__search");
            field.EnableInClassList("bgd-ed__search--grow", grow);
            field.Add(Icon("Search Icon", "bgd-ed__search-icon"));
            var empty = string.IsNullOrEmpty(text);
            field.Add(Text(empty ? placeholder ?? "" : text, empty ? "bgd-ed__value-label bgd-ed__placeholder" : "bgd-ed__value-label"));
            if (!empty)
            {
                field.Add(Grow());
                field.Add(Text("×", "bgd-ed__search-clear"));
            }
            return field;
        }

        static VisualElement Check(bool on)
        {
            var box = El("bgd-ed__check");
            if (on)
                box.Add(Text("✓", "bgd-ed__check-mark"));
            return box;
        }

        static VisualElement Box(VisualElement content, string classNames)
        {
            var box = El(classNames);
            box.Add(content);
            return box;
        }

        static VisualElement Grow() => El("bgd-ed__grow");

        static VisualElement El(string classNames)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                element.AddToClassList(className);
            return element;
        }

        static Label Text(string text, string classNames)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                label.AddToClassList(className);
            return label;
        }

        static VisualElement Icon(string name, string classNames = null)
        {
            var icon = El("bgd-ed__icon" + (classNames != null ? " " + classNames : ""));
            if (name != null && EditorGUIUtility.IconContent(name)?.image is Texture2D texture)
                icon.style.backgroundImage = texture;
            return icon;
        }

        /// <summary>A window's tab icon: the Editor's, or the Bagel windows' own.</summary>
        static VisualElement TabIcon(string window)
        {
            var bagel = window switch
            {
                "Bagel Features" => BagelWindowIcons.bagelFeatures,
                "Bagel UI" => BagelWindowIcons.bagelUI,
                _ => null,
            };
            if (bagel == null)
                return Icon(s_Icons.TryGetValue(window, out var name) ? name : null);
            var icon = El("bgd-ed__icon");
            if (BagelWindowIcons.Load(bagel) is { } texture)
                icon.style.backgroundImage = texture;
            return icon;
        }
    }
}
