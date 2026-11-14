using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Bagel › Bagel Features: the game's UI Toolkit features, one at a time, made to fit the Bagel UI window's space and to
    /// keep as little of the game UI running as it can.
    /// <list type="bullet">
    /// <item><b>Game</b>: the game's screens as pages, one live screen at a time, picked from a row of their icons, with a
    /// numbered dot on each key part. The right side explains how each part is made, with buttons into the scene, UI
    /// Builder, the Project window and the code.</item>
    /// <item><b>Category</b> and <b>Version</b>: a list of the features, grouped, each row with a small live use of
    /// the feature (only the rows in view run theirs). The feature picked shows on the right: where the game uses it,
    /// buttons into the Editor, and short C#, UXML and USS snippets.</item>
    /// <item><b>Art</b>: the game's code-drawn art and SVG icons, a row each; the one picked shows on the right with
    /// controls for its attributes, size and custom properties, and the variations the game uses (BagelFeaturesWindow.Art.cs).</item>
    /// </list>
    /// Content: <see cref="BagelFeatureCatalog"/>, <see cref="BagelFeatureGuide"/> and <see cref="BagelArtGuide"/>; samples:
    /// <see cref="BagelFeatureTiles"/>.
    /// </summary>
    public partial class BagelFeaturesWindow : BagelGuideWindow
    {
        public const string windowTitle = "Bagel Features";
        public const string uxmlPath = "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeaturesWindow.uxml";

        public enum Mode { Game, Category, Version, Art }

        public static readonly string ussClassName = "bgf";
        public static readonly UniqueStyleString screensModeUssClassName = new(ussClassName + "--screens");
        public static readonly UniqueStyleString modeButtonSelectedUssClassName = new("bgf-modes__button--selected");
        public static readonly string rowUssClassName = "bgf-row";
        public static readonly UniqueStyleString rowSelectedUssClassName = new(rowUssClassName + "--selected");
        public static readonly UniqueStyleString rowUnusedUssClassName = new(rowUssClassName + "--unused");
        public static readonly string partUssClassName = "bgf-part";
        public static readonly UniqueStyleString partFlashUssClassName = new(partUssClassName + "--flash");
        public static readonly string screenPickUssClassName = "bgf-screen-pick";
        public static readonly UniqueStyleString screenPickCurrentUssClassName = new(screenPickUssClassName + "--current");
        public static readonly string filterPillUssClassName = "bgf-filter__pill";
        public static readonly UniqueStyleString filterPillSelectedUssClassName = new(filterPillUssClassName + "--selected");
        /// <summary>A group's header in the list: looked for on every scroll (the sticky header).</summary>
        public static readonly UniqueStyleString groupUssClassName = new("bgf-group");

        /// <summary>The phones' scale in the Bagel UI window, which this window matches when there's room.</summary>
        public const float maxZoom = BagelExpressAllUIWindow.defaultZoom;
        const float k_DeviceWidth = 410f;
        const float k_DeviceHeight = 864f;
        const int k_VisibleSources = 3;
        const float k_LiveMargin = 96f;
        const float k_CodeFontSize = 11f;
        const float k_MinCodeFontSize = 8f;
        const float k_MonospaceAdvance = 0.6f; // RobotoMono: every character is 0.6 em wide

        const string k_DefaultFeature = "backdrop-filter";

        // It opens, and Reset goes back, on the Game mode's first screen: the title.
        [SerializeField] Mode m_Mode = Mode.Game;
        [SerializeField] string m_Feature = k_DefaultFeature;
        [SerializeField] int m_Screen = 0;
        // Each list's filter: a group id, or empty for all of them.
        [SerializeField] string m_CategoryFilter = "";
        [SerializeField] string m_VersionFilter = "";

        VisualTreeAsset m_AppTree;
        VisualElement m_Body;
        readonly Dictionary<Mode, Button> m_ModeButtons = new();
        ScrollView m_List;
        VisualElement m_Screens;
        VisualElement m_ScreenStage;
        VisualElement m_PhoneHost;
        Button m_Previous;
        Button m_Next;
        VisualElement m_Picker;
        VisualElement m_Filter;
        ScrollView m_DetailsScroll;
        VisualElement m_Details;

        // What's loaded: the list's rows and the samples (only the rows in view hold theirs), or one phone.
        Dictionary<string, VisualElement> m_Tiles;
        readonly Dictionary<string, VisualElement> m_Rows = new();
        readonly Dictionary<string, string> m_RowGroups = new();
        readonly Dictionary<string, VisualElement> m_GroupHeaders = new();
        readonly Dictionary<VisualElement, Color> m_FilterColors = new();
        readonly List<string> m_AllRows = new();
        /// <summary>The features the list shows, in its order (the filter's group, or all).</summary>
        readonly List<string> m_Order = new();
        // The group the list is scrolled into, pinned over the list's top (its own header has scrolled past).
        VisualElement m_StickyGroup;
        // The row to bring into view once the list is laid out.
        VisualElement m_RowToReveal;
        IVisualElementScheduledItem m_Reveal;
        PhoneFrame m_Phone;
        readonly List<VisualElement> m_PartCards = new();
        IVisualElementScheduledItem m_Flash;

        public Mode mode => m_Mode;
        /// <summary>The list's filter: a group id (a category, or a version such as "6.7"), or empty for all.</summary>
        public string filter => m_Mode == Mode.Version ? m_VersionFilter : m_Mode == Mode.Category ? m_CategoryFilter : "";
        public IReadOnlyList<string> shownFeatures => m_Order;
        public string selectedFeature => m_Feature;
        public int screenIndex => m_Screen;
        public PhoneFrame phone => m_Phone;
        public IReadOnlyDictionary<string, VisualElement> rows => m_Rows;
        public IReadOnlyList<VisualElement> partCards => m_PartCards;
        public VisualElement details => m_Details;
        /// <summary>How many rows of the list run their live sample right now.</summary>
        public int liveSampleCount => m_Rows.Values.Count(r => r.Q("sample")?.childCount > 0);

        protected override string windowUxmlPath => uxmlPath;

        [MenuItem("Bagel/Bagel Features", priority = 20, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static BagelFeaturesWindow Open()
        {
            // Docked with the Bagel UI window when it's open: the same space, which is what this window is made for.
            var window = GetWindow<BagelFeaturesWindow>(windowTitle, true, typeof(BagelExpressAllUIWindow));
            window.titleContent = BagelWindowIcons.Title(windowTitle, BagelWindowIcons.bagelFeatures);
            window.minSize = new Vector2(640f, 480f);
            return window;
        }

        protected override void BuildContent()
        {
            titleContent = BagelWindowIcons.Title(windowTitle, BagelWindowIcons.bagelFeatures);
            m_AppTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelExpressAllUIWindow.appUxmlPath);
            m_Body = rootVisualElement.Q("body");
            m_List = rootVisualElement.Q<ScrollView>("list");
            m_Screens = rootVisualElement.Q("screens");
            m_ScreenStage = rootVisualElement.Q("screen-stage");
            m_PhoneHost = rootVisualElement.Q("phone-host");
            // The screen is the game, live: its season's particles fall and Begu-chan blinks without Play mode.
            m_PhoneHost.AddToClassList(ArtKit.animateInEditModeUssClassName);
            m_Previous = rootVisualElement.Q<Button>("previous");
            m_Next = rootVisualElement.Q<Button>("next");
            m_Picker = rootVisualElement.Q("screen-picker");
            m_Filter = rootVisualElement.Q("list-filter");
            m_DetailsScroll = rootVisualElement.Q<ScrollView>("details-scroll");
            m_Details = rootVisualElement.Q("details");

            m_ModeButtons.Clear();
            BindModeButton(Mode.Game, "mode-game");
            BindModeButton(Mode.Category, "mode-category");
            BindModeButton(Mode.Version, "mode-version");
            BindModeButton(Mode.Art, "mode-art");

            m_Previous.clicked += () => StepScreenAndShow(-1);
            m_Next.clicked += () => StepScreenAndShow(1);
            m_ScreenStage.RegisterCallback<GeometryChangedEvent>(_ => FitPhone());
            m_List.verticalScroller.valueChanged += _ =>
            {
                UpdateLiveSamples();
                UpdateStickyGroup();
            };
            m_List.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateLiveSamples());
            m_List.contentContainer.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                UpdateLiveSamples();
                UpdateStickyGroup();
            });
            m_StickyGroup = GroupHeader(null, 0);
            m_StickyGroup.name = "sticky-group";
            m_StickyGroup.AddToClassList("bgf-group--sticky");
            m_StickyGroup.style.display = DisplayStyle.None;
            m_List.contentViewport.Add(m_StickyGroup);
            m_DetailsScroll.verticalScroller.valueChanged += _ => PositionSticky();
            m_DetailsScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => PositionSticky());

            // Arrow keys: ↑↓ walk the list, ←→ turn the screens' pages.
            rootVisualElement.focusable = true;
            m_Body.RegisterCallback<PointerDownEvent>(_ => rootVisualElement.Focus(), TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            if (rootVisualElement.Q<BagelOptionStrip>("reset-strip") is { } resetStrip)
                BagelPickerIcons.AddReset(resetStrip, ResetWindow);

            BindSearch();
            UpdateModeButtons();
            ShowMode(m_Mode);
        }

        /// <summary>
        /// Back to how the window first opens: a fresh copy of the demo start state, the Game mode on the title, the
        /// first season and the project's language, and the lists, their filters and the art at their defaults.
        /// </summary>
        public void ResetWindow()
        {
            m_Mode = Mode.Game;
            m_Screen = 0;
            m_Feature = k_DefaultFeature;
            m_Art = k_DefaultArt;
            m_CategoryFilter = m_VersionFilter = "";
            m_Search = "";
            m_SearchWords = Array.Empty<string>();
            // A tab that hasn't been shown since scripts loaded isn't built yet: it will be, like this.
            if (m_Body == null)
                return;
            m_SearchField?.SetValueWithoutNotify("");
            UpdateModeButtons();
            ResetState(null);
            ResetSeasonAndLanguage();
            // The title again from the start, on the new state.
            UnloadPhone();
            ShowMode(Mode.Game);
            m_List.scrollOffset = Vector2.zero;
            m_DetailsScroll.scrollOffset = Vector2.zero;
        }

        protected override void OnDisable()
        {
            UnloadList();
            UnloadPhone();
            ClearArtDetails();
            base.OnDisable();
        }

        void BindModeButton(Mode mode, string name)
        {
            var button = rootVisualElement.Q<Button>(name);
            if (button == null)
                return;
            button.clicked += () => ShowMode(mode);
            m_ModeButtons[mode] = button;
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            // Typing (the search, a field of the Art mode) keeps its arrows.
            if (evt.target is VisualElement target && (target is BaseField<string> || target.GetFirstAncestorOfType<BaseField<string>>() != null))
                return;
            if (m_Mode == Mode.Game && (evt.keyCode == KeyCode.LeftArrow || evt.keyCode == KeyCode.RightArrow))
            {
                StepScreenAndShow(evt.keyCode == KeyCode.LeftArrow ? -1 : 1);
                evt.StopPropagation();
            }
            else if (m_Mode != Mode.Game && (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow))
            {
                var index = m_Order.IndexOf(m_Mode == Mode.Art ? m_Art : m_Feature) + (evt.keyCode == KeyCode.UpArrow ? -1 : 1);
                if (index >= 0 && index < m_Order.Count)
                {
                    if (m_Mode == Mode.Art)
                        SelectArt(m_Order[index]);
                    else
                        SelectFeature(m_Order[index]);
                }
                evt.StopPropagation();
            }
        }

        // ------------------------------------------------------------------ modes

        /// <summary>Switch the left side between the screens, the grouped list and the art; what it leaves is unloaded.</summary>
        public void ShowMode(Mode mode)
        {
            m_Mode = mode;
            foreach (var (m, button) in m_ModeButtons)
                button.EnableInClassList(modeButtonSelectedUssClassName, m == mode);
            m_Body.EnableInClassList(screensModeUssClassName, mode == Mode.Game);
            m_Body.EnableInClassList(artModeUssClassName, mode == Mode.Art);

            if (mode == Mode.Game)
            {
                UnloadList();
                BuildScreenPicker();
                ShowScreen(ScreenMatches(m_Screen) ? m_Screen : Mathf.Max(0, StepScreen(-1, 1)));
                ApplySearchToScreens();
            }
            else if (mode == Mode.Art)
            {
                UnloadPhone();
                UnloadScreenPicker();
                BuildArtList();
                ApplyFilter();
                SelectArt(m_Order.Contains(m_Art) ? m_Art : m_Order.FirstOrDefault() ?? m_Art);
            }
            else
            {
                UnloadPhone();
                UnloadScreenPicker();
                BuildList(mode);
                SelectFeature(m_Order.Contains(m_Feature) ? m_Feature : m_Order.FirstOrDefault());
            }
        }

        /// <summary>A feature in the Category list (from a part's feature chip).</summary>
        public void ShowFeature(string featureId)
        {
            m_Feature = featureId;
            var mode = m_Mode == Mode.Game || m_Mode == Mode.Art ? Mode.Category : m_Mode;
            // A filter that would hide it shows them all again.
            var feature = BagelFeatureCatalog.Get(featureId);
            if (feature != null && mode == Mode.Category && m_CategoryFilter != "" && BagelFeatureGuide.CategoryOf(feature).id != m_CategoryFilter)
                m_CategoryFilter = "";
            if (feature != null && mode == Mode.Version && m_VersionFilter != "" && BagelFeatureGuide.VersionOf(feature).id != m_VersionFilter)
                m_VersionFilter = "";
            ShowMode(mode);
        }

        /// <summary>A part on its screen, in Game mode: its dot pulses and its entry is brought into view.</summary>
        public void ShowPart(int screen, int part)
        {
            m_Screen = screen;
            ShowMode(Mode.Game);
            rootVisualElement.schedule.Execute(() => FocusPart(part, true)).ExecuteLater(450);
        }

        // ------------------------------------------------------------------ the list

        void BuildList(Mode mode)
        {
            UnloadList();
            m_List.scrollOffset = Vector2.zero;
            m_Tiles = BagelFeatureTiles.CreateAll(this);
            var groups = mode == Mode.Version ? BagelFeatureGuide.versions : BagelFeatureGuide.categories;
            Func<BagelFeatureCatalog.Feature, BagelFeatureGuide.Group> groupOf = mode == Mode.Version ? BagelFeatureGuide.VersionOf : BagelFeatureGuide.CategoryOf;
            m_Filter.Add(FilterPill("", "All", $"Every feature · {BagelFeatureGuide.allFeatures.Count()}", new Color(1f, 0.77f, 0.16f)));
            foreach (var group in groups)
            {
                var features = BagelFeatureGuide.allFeatures.Where(f => groupOf(f) == group).ToList();
                if (features.Count == 0)
                    continue;
                m_Filter.Add(FilterPill(group.id, group.shortTitle, $"{group.title} · {features.Count}", group.color));
                var header = GroupHeader(group, features.Count);
                m_List.Add(header);
                m_GroupHeaders[group.id] = header;
                foreach (var feature in features)
                {
                    var row = Row(feature);
                    m_List.Add(row);
                    m_Rows[feature.id] = row;
                    m_RowGroups[feature.id] = group.id;
                    m_AllRows.Add(feature.id);
                }
            }
            ApplyFilter();
            m_List.schedule.Execute(UpdateLiveSamples);
        }

        /// <summary>A pill of the list's filter: a group (its short name, in its color), or all of them.</summary>
        VisualElement FilterPill(string groupId, string text, string tooltip, Color color)
        {
            var pill = new VisualElement { name = "filter-" + (groupId == "" ? "all" : groupId), tooltip = tooltip, userData = groupId };
            pill.AddToClassList(filterPillUssClassName);
            if (groupId == "")
                pill.AddToClassList(filterPillUssClassName + "--all");
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("bgf-filter__dot");
            dot.style.backgroundColor = color;
            pill.Add(dot);
            pill.Add(Text(text, "bgf-filter__label"));
            pill.RegisterCallback<ClickEvent>(_ => SetFilter(groupId));
            m_FilterColors[pill] = color;
            return pill;
        }

        /// <summary>Show one group of the list (a category or a version), or all of them with an empty id.</summary>
        public void SetFilter(string groupId)
        {
            if (m_Mode == Mode.Game || m_Mode == Mode.Art)
                return;
            groupId ??= "";
            if (m_Mode == Mode.Category)
                m_CategoryFilter = groupId;
            else
                m_VersionFilter = groupId;
            ApplyFilter();
            m_List.scrollOffset = Vector2.zero;
            if (!m_Order.Contains(m_Feature))
            {
                if (m_Order.Count > 0)
                    SelectFeature(m_Order[0]);
                return;
            }
            // Still shown: once the list is laid out again, bring it into view.
            if (m_Rows.TryGetValue(m_Feature, out var selected))
                ScrollToRow(selected);
        }

        /// <summary>
        /// Show the rows of the filter's group that match the search, and the headers and pills of the groups that have
        /// some (hidden rows don't run their samples); light the pill. A group the search empties shows them all.
        /// </summary>
        void ApplyFilter()
        {
            var matches = new Dictionary<string, int>();
            foreach (var id in m_AllRows)
                if (RowMatches(id))
                    matches[m_RowGroups[id]] = matches.TryGetValue(m_RowGroups[id], out var n) ? n + 1 : 1;
            var current = filter;
            if (current != "" && (!m_GroupHeaders.ContainsKey(current) || !matches.ContainsKey(current)))
                current = "";
            foreach (var (groupId, header) in m_GroupHeaders)
            {
                header.style.display = (current == "" || groupId == current) && matches.ContainsKey(groupId) ? DisplayStyle.Flex : DisplayStyle.None;
                if (header.userData is (BagelFeatureGuide.Group group, int _))
                    ShowGroup(header, group, matches.TryGetValue(groupId, out var count) ? count : 0);
            }
            if (m_StickyGroup != null)
                m_StickyGroup.userData = null;
            m_Order.Clear();
            foreach (var id in m_AllRows)
            {
                var shown = (current == "" || m_RowGroups[id] == current) && RowMatches(id);
                m_Rows[id].style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                if (shown)
                    m_Order.Add(id);
            }
            foreach (var pill in m_Filter.Children())
            {
                pill.style.display = (string)pill.userData == "" || matches.ContainsKey((string)pill.userData) ? DisplayStyle.Flex : DisplayStyle.None;
                var selected = (string)pill.userData == current;
                var color = m_FilterColors[pill];
                pill.EnableInClassList(filterPillSelectedUssClassName, selected);
                pill.style.backgroundColor = selected ? color : new StyleColor(StyleKeyword.Null);
                // On the group's color, the dot takes the label's: ink on All's gold, white on the others.
                var dot = pill.Q(className: "bgf-filter__dot");
                if (dot != null)
                    dot.style.backgroundColor = !selected ? color : (string)pill.userData == "" ? new Color(59f / 255f, 31f / 255f, 20f / 255f) : Color.white;
            }
        }

        void UnloadList()
        {
            // Made again when the list is: the thumbnails (in view or not), the rows and the pills are dropped for good.
            if (m_Tiles != null)
                foreach (var tile in m_Tiles.Values)
                    Discard.Element(tile);
            m_Tiles = null;
            foreach (var row in m_Rows.Values)
            {
                var slot = row.Q("sample");
                if (slot != null)
                    RemoveThemedRoot(slot);
            }
            m_Rows.Clear();
            m_RowGroups.Clear();
            m_GroupHeaders.Clear();
            if (m_StickyGroup != null)
                m_StickyGroup.style.display = DisplayStyle.None;
            m_AllRows.Clear();
            m_Order.Clear();
            m_FilterColors.Clear();
            UnloadArtRows();
            Discard.Children(m_List);
            Discard.Children(m_Filter);
        }

        static VisualElement GroupHeader(BagelFeatureGuide.Group group, int count)
        {
            var header = new VisualElement { name = "group-" + group?.id };
            header.AddToClassList(groupUssClassName);
            var bar = new VisualElement();
            bar.AddToClassList("bgf-group__bar");
            header.Add(bar);
            var text = new VisualElement();
            text.AddToClassList("bgf-group__text");
            var titleRow = new VisualElement();
            titleRow.AddToClassList("bgf-group__title-row");
            titleRow.Add(Text("", "bgf-group__title"));
            titleRow.Add(Text("", "bgf-group__count"));
            text.Add(titleRow);
            text.Add(Text("", "bgf-group__sub"));
            header.Add(text);
            if (group != null)
                ShowGroup(header, group, count);
            return header;
        }

        static void ShowGroup(VisualElement header, BagelFeatureGuide.Group group, int count)
        {
            header.userData = (group, count);
            header.Q(className: "bgf-group__bar").style.backgroundColor = group.color;
            header.Q<Label>(className: "bgf-group__title").text = group.title;
            header.Q<Label>(className: "bgf-group__count").text = count.ToString();
            header.Q<Label>(className: "bgf-group__sub").text = group.subtitle;
        }

        /// <summary>
        /// The pinned header: the group of the top of the list, once its own header has scrolled past. The next group's
        /// header pushes it up as it arrives. There's no position: sticky in UI Toolkit, so it's a copy over the list.
        /// </summary>
        void UpdateStickyGroup()
        {
            if (m_StickyGroup == null)
                return;
            VisualElement current = null, next = null;
            var top = m_List.scrollOffset.y;
            if (m_Mode == Mode.Category || m_Mode == Mode.Version)
            {
                foreach (var child in m_List.contentContainer.Children())
                {
                    if (!child.ClassListContains(groupUssClassName) || child.resolvedStyle.display == DisplayStyle.None)
                        continue;
                    if (child.layout.yMin < top + 1f)
                        current = child;
                    else
                    {
                        next = child;
                        break;
                    }
                }
            }
            if (current?.userData is not (BagelFeatureGuide.Group group, int count))
            {
                m_StickyGroup.style.display = DisplayStyle.None;
                return;
            }
            if (m_StickyGroup.userData is not (BagelFeatureGuide.Group shown, int _) || shown != group)
                ShowGroup(m_StickyGroup, group, count);
            m_StickyGroup.style.display = DisplayStyle.Flex;
            var height = m_StickyGroup.layout.height;
            var push = next == null || float.IsNaN(height) ? 0f : Mathf.Min(0f, next.layout.yMin - next.resolvedStyle.marginTop - top - height);
            m_StickyGroup.style.translate = new Translate(0f, push);
        }

        VisualElement Row(BagelFeatureCatalog.Feature feature)
        {
            var row = new VisualElement { name = "row-" + feature.id, tooltip = $"{feature.title} · {BagelFeatureGuide.SinceText(feature)}" };
            row.AddToClassList(rowUssClassName);
            row.EnableInClassList(rowUnusedUssClassName, !feature.isUsed);

            // The live sample: game UI under the season theme, given the window's state for the ones that bind (or a
            // small Editor window, for a feature that lives in the Editor).
            var slot = new VisualElement { name = "sample", pickingMode = PickingMode.Ignore };
            slot.AddToClassList("bgf-row__sample");
            slot.dataSource = state;
            AddThemedRoot(slot);
            BagelFeatureTiles.AddStyleSheets(slot);
            row.Add(slot);

            var text = new VisualElement { pickingMode = PickingMode.Ignore };
            text.AddToClassList("bgf-row__text");
            text.Add(Text(feature.title, "bgf-row__title"));
            var meta = new VisualElement { pickingMode = PickingMode.Ignore };
            meta.AddToClassList("bgf-row__meta");
            // The grouping that isn't the list's: the version under a category, the category under a version (with
            // the experimental pill under 7.x).
            if (m_Mode == Mode.Version)
            {
                if (feature.isExperimental)
                    meta.Add(ExperimentalChip());
                meta.Add(CategoryChip(BagelFeatureGuide.CategoryOf(feature)));
            }
            else
            {
                AddVersionChips(meta, feature);
            }
            meta.Add(UsedChip(feature));
            text.Add(meta);
            row.Add(text);

            row.RegisterCallback<ClickEvent>(_ => SelectFeature(feature.id));
            return row;
        }

        /// <summary>Put the live samples in the rows in view (and a row around), take them out of the others.</summary>
        void UpdateLiveSamples()
        {
            if (m_Tiles == null || m_List == null)
                return;
            // Rows are the content's children, so their layout is in content space, like the scroll offset.
            var viewHeight = m_List.contentViewport.layout.height;
            if (float.IsNaN(viewHeight) || viewHeight < 1f)
                return;
            var top = m_List.scrollOffset.y - k_LiveMargin;
            var bottom = m_List.scrollOffset.y + viewHeight + k_LiveMargin;
            foreach (var (id, row) in m_Rows)
            {
                var slot = row.Q("sample");
                if (slot == null)
                    continue;
                var bounds = row.layout;
                var inView = bounds.height > 0f && bounds.yMax >= top && bounds.yMin <= bottom;
                if (inView && slot.childCount == 0 && m_Tiles.TryGetValue(id, out var tile))
                    slot.Add(tile);
                else if (!inView && slot.childCount > 0)
                    slot.Clear();
            }
        }

        /// <summary>Select a feature in the list and show it on the right.</summary>
        public void SelectFeature(string featureId)
        {
            var feature = BagelFeatureCatalog.Get(featureId);
            if (feature == null)
                return;
            m_Feature = featureId;
            foreach (var (id, row) in m_Rows)
                row.EnableInClassList(rowSelectedUssClassName, id == featureId);
            if (m_Rows.TryGetValue(featureId, out var selected))
                ScrollToRow(selected);
            BuildFeatureDetails(feature);
        }

        /// <summary>
        /// Bring a row into view once the list is laid out again: right after it's built, filtered or searched, the rows
        /// haven't moved to their new places yet. Its next layout, and the next frame, both bring it in.
        /// </summary>
        void ScrollToRow(VisualElement row)
        {
            m_RowToReveal = row;
            m_Reveal?.Pause();
            m_Reveal = m_List.schedule.Execute(RevealRow);
            m_List.contentContainer.UnregisterCallback<GeometryChangedEvent>(RevealRowAfterLayout);
            m_List.contentContainer.RegisterCallback<GeometryChangedEvent>(RevealRowAfterLayout);
        }

        void RevealRowAfterLayout(GeometryChangedEvent evt)
        {
            m_List.contentContainer.UnregisterCallback<GeometryChangedEvent>(RevealRowAfterLayout);
            RevealRow();
        }

        void RevealRow()
        {
            var row = m_RowToReveal;
            if (row?.panel != null && row.resolvedStyle.display == DisplayStyle.Flex && row.layout.height > 0f)
                CenterRow(row);
        }

        /// <summary>
        /// A row that isn't all in view goes to the middle of the list (one that is stays where it is). The top of the
        /// list is under the pinned group header.
        /// </summary>
        void CenterRow(VisualElement row)
        {
            var viewHeight = m_List.contentViewport.layout.height;
            var top = m_List.scrollOffset.y;
            var pinned = m_StickyGroup != null && m_StickyGroup.resolvedStyle.display == DisplayStyle.Flex ? m_StickyGroup.layout.height : 0f;
            if (row.layout.yMin >= top + pinned && row.layout.yMax <= top + viewHeight)
                return;
            m_List.scrollOffset = new Vector2(0f, Mathf.Max(0f, row.layout.center.y - viewHeight * 0.5f));
        }

        // ------------------------------------------------------------------ a feature

        void BuildFeatureDetails(BagelFeatureCatalog.Feature feature)
        {
            ClearDetails();
            m_Details.Add(Text(feature.title, "bgf-details__title"));
            var chips = new VisualElement();
            chips.AddToClassList("bgf-details__chips");
            AddVersionChips(chips, feature);
            chips.Add(CategoryChip(BagelFeatureGuide.CategoryOf(feature)));
            chips.Add(UsedChip(feature));
            chips.Add(Text(BagelFeatureGuide.SinceText(feature), "bgf-details__since"));
            m_Details.Add(chips);
            if (!feature.isUsed)
            {
                // So you know it exists: what it looks like, what it is and what you'd use it for, and its code.
                BuildFeaturePreview(feature);
                var note = new VisualElement { name = "not-used" };
                note.AddToClassList("bgf-unused");
                note.Add(Text("Not used in Bagel Express", "bgf-unused__title"));
                note.Add(Text(feature.about, "bgf-unused__about"));
                note.Add(Text(feature.summary, "bgf-unused__usage"));
                m_Details.Add(note);
                var examples = BagelFeatureGuide.SnippetsOf(feature.id);
                if (examples.Count > 0)
                {
                    m_Details.Add(Text("HOW YOU'D USE IT", "bgf-section"));
                    foreach (var snippet in examples)
                        m_Details.Add(SnippetView(snippet));
                }
                return;
            }
            // Its live sample, on a stage that stays in view, and what to try on it.
            BuildFeaturePreview(feature);

            // A few words: what it is, and how the game uses it.
            var about = Text(feature.about, "bgf-details__about");
            about.AddToClassList("bgf-details__about--lead");
            m_Details.Add(about);
            m_Details.Add(Text(feature.summary, "bgf-details__summary"));

            var snippets = BagelFeatureGuide.SnippetsOf(feature.id);
            if (snippets.Count > 0)
            {
                m_Details.Add(Text("HOW TO USE IT", "bgf-section"));
                foreach (var snippet in snippets)
                    m_Details.Add(SnippetView(snippet));
            }

            if (feature.links.Length > 0)
            {
                m_Details.Add(Text("SEE IT IN THE EDITOR", "bgf-section"));
                var links = new VisualElement();
                links.AddToClassList("bgg-links");
                links.AddToClassList("bgf-details__links");
                foreach (var link in feature.links)
                    links.Add(link.label == "Art Sampler" ? ArtLink(feature.id) : LinkButton(link));
                m_Details.Add(links);
            }

            if (feature.sources.Length > 0)
            {
                m_Details.Add(Text("IN THE PROJECT", "bgf-section"));
                var more = new List<VisualElement>();
                for (int i = 0; i < feature.sources.Length; i++)
                {
                    var source = feature.sources[i];
                    var line = new VisualElement();
                    line.AddToClassList("bgg-source");
                    line.Add(LinkButton(source.link));
                    line.Add(Text(source.what, "bgg-source__what"));
                    m_Details.Add(line);
                    if (i >= k_VisibleSources)
                    {
                        line.style.display = DisplayStyle.None;
                        more.Add(line);
                    }
                }
                if (more.Count > 0)
                {
                    Button showMore = null;
                    showMore = new Button(() =>
                    {
                        foreach (var line in more)
                            line.style.display = DisplayStyle.Flex;
                        showMore.RemoveFromHierarchy();
                    }) { text = $"{more.Count} more" };
                    showMore.AddToClassList("bgf-more");
                    m_Details.Add(showMore);
                }
            }

            // Last, where the game uses it: each part live on its own.
            AddUses(feature);
        }

        void ClearDetails()
        {
            ClearPreview();
            ClearArtDetails();
            m_Flash?.Pause();
            m_PartCards.Clear();
            // Made again for the next pick: what they show gives its resources back to be reused now.
            Discard.Children(m_Details);
            m_DetailsScroll.scrollOffset = Vector2.zero;
        }

        static VisualElement SnippetView(BagelFeatureGuide.Snippet snippet)
        {
            var view = new VisualElement();
            view.AddToClassList("bgf-snippet");
            var head = new VisualElement();
            head.AddToClassList("bgf-snippet__head");
            var language = Text(snippet.languageLabel, "bgf-snippet__language");
            language.AddToClassList("bgf-snippet__language--" + snippet.language.ToString().ToLowerInvariant());
            head.Add(language);
            head.Add(Text(snippet.caption ?? "", "bgf-snippet__caption"));
            var copy = new Button(() => EditorGUIUtility.systemCopyBuffer = snippet.code) { text = "Copy", tooltip = "Copy this snippet" };
            copy.AddToClassList("bgf-snippet__copy");
            head.Add(copy);
            view.Add(head);

            // Selectable, and as wide as its longest line: what doesn't fit scrolls sideways (the wheel still scrolls the
            // details).
            var code = new Label(BagelCodeHighlighter.ToRichText(snippet.code, snippet.language)) { enableRichText = true };
            code.AddToClassList("bgf-snippet__code");
            code.selection.isSelectable = true;
            if (monospaceFont != null)
                code.style.unityFontDefinition = FontDefinition.FromFont(monospaceFont);
            var scroll = new ScrollView(ScrollViewMode.Horizontal) { horizontalScrollerVisibility = ScrollerVisibility.Auto };
            scroll.AddToClassList("bgf-code-scroll");
            scroll.Add(code);
            var longest = snippet.code.Split('\n').Max(line => line.Length);
            scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => FitCode(code, scroll.contentViewport.layout.width, longest));
            view.Add(scroll);
            return view;
        }

        /// <summary>
        /// The code's font, as large as its longest line allows in <paramref name="width"/> (8 to 11 points): the window's
        /// width fits it; a narrower one scrolls it.
        /// </summary>
        static void FitCode(Label code, float width, int longest)
        {
            var padding = code.resolvedStyle.paddingLeft + code.resolvedStyle.paddingRight;
            width -= padding;
            if (float.IsNaN(width) || width < 50f || longest == 0)
                return;
            var size = Mathf.Clamp(Mathf.Floor(width / (longest * k_MonospaceAdvance) * 10f) / 10f, k_MinCodeFontSize, k_CodeFontSize);
            if (Mathf.Abs(code.resolvedStyle.fontSize - size) > 0.05f)
                code.style.fontSize = size;
            // A label is never measured wider than the room it's given: when the lines don't fit, their own width keeps
            // them whole (and scrolls).
            var linesWidth = longest * k_MonospaceAdvance * size;
            code.style.minWidth = linesWidth > width ? Mathf.Ceil(linesWidth + padding + 1f) : new StyleLength(StyleKeyword.Null);
        }

        static Font s_Monospace;
        static Font monospaceFont => s_Monospace ??= EditorGUIUtility.Load("Fonts/RobotoMono/RobotoMono-Regular.ttf") as Font;

        // ------------------------------------------------------------------ the screens

        /// <summary>The screens' icons, above the phone: the nav's for the pages, the game's own art for the others.</summary>
        void BuildScreenPicker()
        {
            UnloadScreenPicker();
            AddThemedRoot(m_Picker); // the art reads the season's colors
            var screens = BagelFeatureGuide.screens;
            for (int i = 0; i < screens.Count; i++)
            {
                var index = i;
                var screen = screens[i].screen;
                var pick = new VisualElement { name = "pick-" + screen.id, tooltip = $"{i + 1:00} · {screen.title} · {screen.japaneseTitle}" };
                pick.AddToClassList(screenPickUssClassName);
                var icon = BagelPickerIcons.Screen(screen);
                icon.AddToClassList("bgf-screen-icon");
                pick.Add(icon);
                pick.RegisterCallback<ClickEvent>(_ => ShowScreen(index));
                m_Picker.Add(pick);
            }
        }

        void UnloadScreenPicker()
        {
            Discard.Children(m_Picker);
            RemoveThemedRoot(m_Picker);
        }

        /// <summary>Show one screen, live, with its parts' dots; the screen it leaves is unloaded.</summary>
        public void ShowScreen(int index)
        {
            var screens = BagelFeatureGuide.screens;
            if (screens.Count == 0)
                return;
            index = Mathf.Clamp(index, 0, screens.Count - 1);
            // Not built yet (just opened): it opens on this screen.
            if (m_Body == null)
            {
                m_Mode = Mode.Game;
                m_Screen = index;
                return;
            }
            if (m_Mode != Mode.Game)
            {
                m_Screen = index;
                ShowMode(Mode.Game);
                return;
            }
            if (m_Phone == null || index != m_Screen || m_Phone.screen != screens[index].screen)
                LoadScreen(index);
            UpdatePager();
        }

        void LoadScreen(int index)
        {
            m_Screen = index;
            UnloadPhone();

            var screenParts = BagelFeatureGuide.screens[index];
            // Pinned: a click plays, but doesn't leave the screen and its dots (LEVEL UP!'s AWESOME!, the nav).
            m_Phone = new PhoneFrame(screenParts.screen, m_AppTree, state, (index + 1).ToString("00")) { pinScreen = true };
            AddThemedRoot(m_Phone.screenRoot);
            for (int i = 0; i < screenParts.parts.Length; i++)
            {
                var part = screenParts.parts[i];
                var feature = part.featureInfo;
                var color = feature != null ? BagelFeatureGuide.CategoryOf(feature).color : Color.gray;
                var partIndex = i;
                m_Phone.AddHotspot(part.selector, i.ToString(), (i + 1).ToString(), $"{i + 1}. {part.title} · {feature?.title}", color, _ => FocusPart(partIndex, false));
            }
            m_PhoneHost.Add(m_Phone);
            FitPhone();
            BuildScreenDetails(screenParts, index);
        }

        /// <summary>The previous or next screen (of the ones the search shows).</summary>
        void StepScreenAndShow(int direction)
        {
            var index = StepScreen(m_Screen, direction);
            if (index >= 0)
                ShowScreen(index);
        }

        /// <summary>The arrows and the screens' icons, for the screen shown.</summary>
        void UpdatePager()
        {
            var screens = BagelFeatureGuide.screens;
            var index = m_Screen;
            var previous = StepScreen(index, -1);
            var next = StepScreen(index, 1);
            m_Previous.SetEnabled(previous >= 0);
            m_Next.SetEnabled(next >= 0);
            m_Previous.tooltip = previous >= 0 ? "‹ " + screens[previous].screen.title : "";
            m_Next.tooltip = next >= 0 ? screens[next].screen.title + " ›" : "";
            for (int i = 0; i < m_Picker.childCount; i++)
                m_Picker[i].EnableInClassList(screenPickCurrentUssClassName, i == index);
        }

        void UnloadPhone()
        {
            if (m_Phone == null)
                return;
            RemoveThemedRoot(m_Phone.screenRoot);
            // The next screen is a new copy of the app: this one's resources go back to be reused now.
            Discard.Element(m_Phone);
            m_Phone = null;
        }

        /// <summary>The Bagel UI window's phone size when it fits; smaller when the window is.</summary>
        void FitPhone()
        {
            if (m_Phone == null)
                return;
            var width = m_ScreenStage.resolvedStyle.width;
            var height = m_ScreenStage.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height) || height < 10f)
            {
                m_Phone.SetZoom(maxZoom);
                return;
            }
            const float captionHeight = 34f;
            const float arrowsWidth = 2f * (36f + 10f);
            var zoom = Mathf.Min(maxZoom, (height - captionHeight) / k_DeviceHeight, (width - arrowsWidth) / k_DeviceWidth);
            zoom = Mathf.Max(0.3f, zoom);
            if (Mathf.Abs(m_Phone.zoom - zoom) > 0.002f)
                m_Phone.SetZoom(zoom);
        }

        void BuildScreenDetails(BagelFeatureGuide.ScreenParts screenParts, int index)
        {
            ClearDetails();
            var screen = screenParts.screen;
            var head = new VisualElement();
            head.AddToClassList("bgf-screen-head");
            head.Add(Text((index + 1).ToString("00"), "bgf-screen-head__index"));
            head.Add(Text(screen.title, "bgf-details__title"));
            m_Details.Add(head);
            var subtitle = Text($"{screen.japaneseTitle} · {screenParts.parts.Length} parts", "bgf-details__about");
            subtitle.tooltip = "Hover a part to find it on the screen; click a dot to come back here";
            subtitle.pickingMode = PickingMode.Position;
            m_Details.Add(subtitle);
            for (int i = 0; i < screenParts.parts.Length; i++)
            {
                var partIndex = i;
                var card = PartCard(screenParts.parts[i], i, null, () => FocusPart(partIndex, true));
                card.RegisterCallback<PointerEnterEvent>(_ => HighlightSpot(partIndex, true));
                card.RegisterCallback<PointerLeaveEvent>(_ => HighlightSpot(partIndex, false));
                m_PartCards.Add(card);
                m_Details.Add(card);
            }
            MarkMatchingParts();
        }

        void HighlightSpot(int part, bool on)
        {
            var spot = m_Phone?.hotspots.FirstOrDefault(s => s.key == part.ToString());
            if (spot == null)
                return;
            spot.outline.EnableInClassList(PhoneFrame.hotspotHoverUssClassName, on);
            spot.badge.EnableInClassList(PhoneFrame.hotspotPulseUssClassName, on);
        }

        /// <summary>Bring a part's entry into view and flash it; <paramref name="pulseSpot"/> flashes its dot on the screen too.</summary>
        public void FocusPart(int part, bool pulseSpot)
        {
            if (part < 0 || part >= m_PartCards.Count)
                return;
            var card = m_PartCards[part];
            m_DetailsScroll.ScrollTo(card);
            foreach (var other in m_PartCards)
                other.RemoveFromClassList(partFlashUssClassName);
            card.AddToClassList(partFlashUssClassName);
            m_Flash?.Pause();
            m_Flash = card.schedule.Execute(() => card.RemoveFromClassList(partFlashUssClassName));
            m_Flash.ExecuteLater(1400);
            if (pulseSpot)
                m_Phone?.Pulse(part.ToString());
        }

        // ------------------------------------------------------------------ a part: how it's made, and buttons into the Editor

        /// <summary>
        /// A part's entry. <paramref name="screenTitle"/> non-null: it's listed under a feature, with a button that shows it on
        /// its screen (<paramref name="activate"/>); null: it's listed under its screen, with a chip that opens its feature.
        /// </summary>
        VisualElement PartCard(BagelFeatureGuide.Part part, int index, string screenTitle, Action activate, VisualElement live = null)
        {
            var feature = part.featureInfo;
            var category = feature != null ? BagelFeatureGuide.CategoryOf(feature) : null;
            var card = new VisualElement { name = "part-" + index };
            card.AddToClassList(partUssClassName);

            var head = new VisualElement();
            head.AddToClassList("bgf-part__head");
            if (screenTitle == null)
            {
                var number = Text((index + 1).ToString(), "bgf-part__number");
                if (category != null)
                    number.style.backgroundColor = category.color;
                head.Add(number);
                head.Add(Text(part.title, "bgf-part__title"));
                if (feature != null)
                {
                    var chip = new Button(() => ShowFeature(feature.id)) { tooltip = $"Open {feature.title} in the feature list" };
                    chip.AddToClassList("bgf-part__feature");
                    var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                    dot.AddToClassList("bgf-chip__dot");
                    dot.style.backgroundColor = category.color;
                    chip.Add(dot);
                    chip.Add(Text(feature.title, "bgf-part__feature-label"));
                    card.Add(head);
                    card.Add(chip);
                }
                else
                {
                    card.Add(head);
                }
            }
            else
            {
                // The screen's tag is the way there: Game mode, on that screen, with the part's dot pulsing.
                var screen = new Button(activate) { text = screenTitle + "  ›", tooltip = $"Show it on the {screenTitle} screen" };
                screen.AddToClassList("bgf-part__screen");
                head.Add(screen);
                head.Add(Text(part.title, "bgf-part__title"));
                card.Add(head);
            }

            if (live != null)
                card.Add(live);
            card.Add(Text(part.how, "bgf-part__how"));

            var actions = new VisualElement();
            actions.AddToClassList("bgf-part__actions");
            actions.Add(Action("◎ Scene", $"Select {part.sceneSelector} in Main.unity's UI (opens the scene if it isn't): Hierarchy and Inspector",
                "bgf-action--scene", () => EditorLink.Select(part.sceneSelector).Open()));
            var uxml = BagelFeatureGuide.UxmlOf(part);
            if (uxml != null)
            {
                var select = BagelFeatureGuide.UIBuilderSelection(part);
                actions.Add(Action("UI Builder", $"Open {uxml} in UI Builder" + (select != null ? $" with #{select} selected" : ""),
                    "bgf-action--builder", () => EditorLink.UIBuilder(uxml, select).Open()));
                actions.Add(Action("Project", $"Select {uxml} in the Project window", "bgf-action--asset", () => EditorLink.Asset(uxml).Open()));
            }
            var uss = BagelFeatureGuide.UssOf(part);
            if (uss != null)
                actions.Add(Action("USS", $"Open {uss.path} at its rule for {uss.find}", "bgf-action--code", uss.Open));
            var code = BagelFeatureGuide.CodeOf(part);
            if (code != null)
                actions.Add(Action("C#", $"Open {code.path}" + (string.IsNullOrEmpty(code.find) ? "" : $" at \"{code.find}\""), "bgf-action--code", code.Open));
            card.Add(actions);

            if (screenTitle == null)
                card.RegisterCallback<ClickEvent>(evt =>
                {
                    if (evt.target is not Button)
                        activate?.Invoke();
                });
            return card;
        }

        static Button Action(string text, string tooltip, string className, Action clicked)
        {
            var button = new Button(clicked) { text = text, tooltip = tooltip };
            button.AddToClassList("bgf-action");
            button.AddToClassList(className);
            return button;
        }

        // ------------------------------------------------------------------ chips and text

        /// <summary>The version's pill; an experimental feature's is 7.x, after a 6.7-exp pill.</summary>
        static void AddVersionChips(VisualElement parent, BagelFeatureCatalog.Feature feature)
        {
            if (feature.isExperimental)
                parent.Add(ExperimentalChip());
            var version = BagelFeatureGuide.VersionOf(feature);
            var chip = Text(version.shortTitle, "bgf-chip");
            chip.AddToClassList("bgf-chip--version");
            chip.style.backgroundColor = version.color;
            chip.tooltip = feature.isExperimental ? "Meant to be fully released in Unity 7.x" : BagelFeatureGuide.SinceText(feature);
            chip.pickingMode = PickingMode.Position;
            parent.Add(chip);
        }

        /// <summary>Whether the game uses it.</summary>
        static VisualElement UsedChip(BagelFeatureCatalog.Feature feature)
        {
            var chip = Text(feature.isUsed ? "In Bagel" : "Not in Bagel", "bgf-chip");
            chip.AddToClassList(feature.isUsed ? "bgf-chip--used" : "bgf-chip--unused");
            chip.tooltip = feature.isUsed ? "Bagel Express uses it" : "Bagel Express doesn't use it";
            chip.pickingMode = PickingMode.Position;
            return chip;
        }

        static VisualElement ExperimentalChip()
        {
            var chip = Text("6.7-exp", "bgf-chip");
            chip.AddToClassList("bgf-chip--experimental");
            chip.tooltip = "Experimental in Unity 6.7, behind a Project Settings › UI Toolkit flag";
            chip.pickingMode = PickingMode.Position;
            return chip;
        }

        static VisualElement CategoryChip(BagelFeatureGuide.Group category)
        {
            var chip = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.AddToClassList("bgf-chip");
            chip.AddToClassList("bgf-chip--category");
            var dot = new VisualElement { pickingMode = PickingMode.Ignore };
            dot.AddToClassList("bgf-chip__dot");
            dot.style.backgroundColor = category.color;
            chip.Add(dot);
            chip.Add(Text(category.title, "bgf-chip__label"));
            return chip;
        }

        static Label Text(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
