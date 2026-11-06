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
    /// Art mode: the game's code-drawn art and its SVG icons (<see cref="BagelArtGuide"/>), the Art Sampler one element at a
    /// time. A row per element, with a few of its variations (only the rows in view hold theirs). The element picked shows
    /// on the right, on the season's sky, with a control for each attribute, size and custom property, the UXML and USS
    /// for what's set, and the variations the game uses (click one to try it).
    /// </summary>
    public partial class BagelFeaturesWindow
    {
        public static readonly UniqueStyleString artModeUssClassName = new("bgf--art"); // not ussClassName + "--art": static fields of partial files initialize in no set order
        public static readonly string artCellUssClassName = "bgf-art-cell";
        public static readonly UniqueStyleString artCellSelectedUssClassName = new(artCellUssClassName + "--selected");
        /// <summary>Set as the details scroll: the preview stuck at their top.</summary>
        public static readonly UniqueStyleString stickyStuckUssClassName = new("bgf-art-sticky--stuck");

        const string k_DefaultArt = "pastry-icon";

        [SerializeField] string m_Art = k_DefaultArt;

        // The rows' style sheets (custom properties), and the details' (the preview and the gallery).
        readonly List<StyleSheet> m_ArtRowSheets = new();
        readonly List<StyleSheet> m_ArtSheets = new();
        // Replaced sheets, destroyed once the art has had a style update without them.
        readonly List<StyleSheet> m_RetiredSheets = new();
        IVisualElementScheduledItem m_DestroyRetired;
        readonly List<VisualElement> m_ArtThemedRoots = new();
        readonly List<VisualElement> m_DetailsEditorParts = new();
        readonly Dictionary<string, Action<object>> m_ArtFieldSync = new();
        readonly List<VisualElement> m_ArtCells = new();
        Dictionary<string, object> m_ArtValues = new();
        BagelArtGuide.ArtType m_ArtType;
        VisualElement m_ArtStage;
        VisualElement m_ArtPreview;
        VisualElement m_ArtMarkup;
        // The preview stays in view: it's drawn over the details (m_Sticky), above a slot that holds its place.
        VisualElement m_Sticky;
        VisualElement m_StickySlot;

        public string selectedArt => m_Art;
        /// <summary>The element in the preview (for a fill-type element, the element, not its box).</summary>
        public VisualElement artPreview => m_ArtPreview;
        public VisualElement artStage => m_ArtStage;
        /// <summary>The preview's values by control name (null: a custom property left to the theme).</summary>
        public IReadOnlyDictionary<string, object> artValues => m_ArtValues;
        /// <summary>The gallery's cells, in the order of <see cref="BagelArtGuide.ArtType.variations"/>.</summary>
        public IReadOnlyList<VisualElement> artGallery => m_ArtCells;

        // ------------------------------------------------------------------ the list

        void BuildArtList()
        {
            UnloadList();
            m_List.scrollOffset = Vector2.zero;
            m_Tiles = new Dictionary<string, VisualElement>();
            foreach (var type in BagelArtGuide.types)
            {
                var row = ArtRow(type);
                m_List.Add(row);
                m_Rows[type.id] = row;
                m_RowGroups[type.id] = "";
                m_AllRows.Add(type.id);
                m_Order.Add(type.id);
                m_Tiles[type.id] = ArtStrip(type);
            }
            m_List.schedule.Execute(UpdateLiveSamples);
        }

        VisualElement ArtRow(BagelArtGuide.ArtType type)
        {
            var row = new VisualElement { name = "row-" + type.id, tooltip = type.about };
            row.AddToClassList(rowUssClassName);
            row.AddToClassList("bgf-art-row");

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.AddToClassList("bgf-art-row__head");
            head.Add(Text(type.title, "bgf-art-row__title"));
            head.Add(Text(type.drawnWith, "bgf-art-tag"));
            row.Add(head);

            // The window's season (its theme, and its sky) for the variations, unless each brings its own season.
            var slot = new VisualElement { name = "sample", pickingMode = PickingMode.Ignore };
            slot.AddToClassList("bgf-art-row__sample");
            if (!IsSeasons(type.row))
                slot.AddToClassList("be-backdrop");
            AddThemedRoot(slot);
            row.Add(slot);

            row.RegisterCallback<ClickEvent>(_ => SelectArt(type.id));
            return row;
        }

        /// <summary>A row's variations, side by side; a season's on its own sky.</summary>
        VisualElement ArtStrip(BagelArtGuide.ArtType type)
        {
            var strip = new VisualElement { pickingMode = PickingMode.Ignore };
            strip.AddToClassList("bgf-art-strip");
            strip.EnableInClassList("bgf-art-strip--seasons", IsSeasons(type.row));
            foreach (var variation in type.row)
            {
                var (box, _) = BagelArtGuide.Create(type, BagelArtGuide.ValuesOf(type, variation), type.rowSize, false, m_ArtRowSheets);
                // Particles, a skyline, confetti: all the room there is (a Spotlight's screen is laid out for its size).
                if (type.fills && type.scene == null)
                    box.style.width = box.style.height = Length.Percent(100f);
                strip.Add(variation.season >= 0 ? SeasonFrame(box, variation.season, "bgf-art-strip__season") : box);
            }
            return strip;
        }

        static bool IsSeasons(IEnumerable<BagelArtGuide.Variation> variations) => variations.All(v => v.season >= 0);

        /// <summary>Art on a season's sky: the season's theme on a frame of its own, whatever the window's season.</summary>
        VisualElement SeasonFrame(VisualElement content, int season, string className)
        {
            var frame = new VisualElement { pickingMode = PickingMode.Ignore, tooltip = SeasonName(season) };
            frame.AddToClassList(className);
            frame.AddToClassList("be-backdrop");
            var theme = SeasonTheme(season);
            if (theme != null)
                frame.styleSheets.Add(theme);
            frame.Add(content);
            return frame;
        }

        ThemeStyleSheet SeasonTheme(int season)
        {
            var themes = themeCollection != null ? themeCollection.collection : null;
            return themes != null && season >= 0 && season < themes.Count ? themes[season].theme : null;
        }

        /// <summary>"Maple Autumn" → "Maple".</summary>
        string SeasonName(int season)
        {
            var themes = themeCollection != null ? themeCollection.collection : null;
            if (themes == null || season < 0 || season >= themes.Count)
                return "";
            var name = themes[season].themeName ?? "";
            var space = name.IndexOf(' ');
            return space > 0 ? name.Substring(0, space) : name;
        }

        int currentSeason => themeCollection != null && themeCollection.collection != null ? themeCollection.collection.FindIndex(e => e.theme == currentTheme) : -1;

        void UnloadArtRows()
        {
            foreach (var sheet in m_ArtRowSheets)
                if (sheet != null)
                    DestroyImmediate(sheet);
            m_ArtRowSheets.Clear();
        }

        /// <summary>Select an art element in the list and show it on the right.</summary>
        public void SelectArt(string id)
        {
            var type = BagelArtGuide.Get(id) ?? BagelArtGuide.types[0];
            m_Art = type.id;
            foreach (var (rowId, row) in m_Rows)
                row.EnableInClassList(rowSelectedUssClassName, rowId == type.id);
            if (m_Rows.TryGetValue(type.id, out var selected))
                ScrollToRow(selected);
            BuildArtDetails(type);
        }

        // ------------------------------------------------------------------ the details

        void BuildArtDetails(BagelArtGuide.ArtType type)
        {
            ClearDetails();
            m_ArtType = type;
            m_ArtValues = BagelArtGuide.ValuesOf(type, null);

            m_Details.Add(Text(type.title, "bgf-details__title"));
            var chips = new VisualElement();
            chips.AddToClassList("bgf-details__chips");
            var tag = Text(type.drawnWith, "bgf-art-tag");
            tag.AddToClassList("bgf-art-tag--large");
            chips.Add(tag);
            chips.Add(Text($"<{type.uxmlTag}>", "bgf-details__since"));
            m_Details.Add(chips);

            // The preview: the element as set, centered on the season's sky; the only art here that moves. It stays in
            // view at the top of the details as they scroll.
            m_ArtStage = new VisualElement { name = "art-stage" };
            m_ArtStage.AddToClassList("bgf-art-stage");
            m_ArtStage.AddToClassList("be-backdrop");
            m_ArtStage.AddToClassList(ArtKit.animateInEditModeUssClassName);
            AddArtThemedRoot(m_ArtStage);
            var (box, art) = BagelArtGuide.Create(type, m_ArtValues, type.previewSize, true, m_ArtSheets);
            m_ArtPreview = art;
            m_ArtStage.Add(box);
            // A season change (the stage's theme) or a style sheet taken off the art (Defaults, a variation) changes what
            // the custom properties left to the theme show: once the styles are resolved, the fields show it.
            m_ArtStage.RegisterCallback<CustomStyleResolvedEvent>(_ => SyncResolvedFields());
            m_ArtPreview.RegisterCallback<CustomStyleResolvedEvent>(_ => SyncResolvedFields());
            if (type.action.run != null)
                m_ArtStage.RegisterCallback<ClickEvent>(_ => type.action.run(m_ArtPreview));
            AddSticky(m_ArtStage, "art");

            var bar = new VisualElement();
            bar.AddToClassList("bgf-art-bar");
            if (type.action.run != null)
                bar.Add(Action(type.action.label, "Run it again (or click the preview)", "bgf-action--scene", () => type.action.run(m_ArtPreview)));
            bar.Add(Action("↺ Defaults", "Back to the element's defaults and the season's custom properties", "bgf-action--asset", ResetArt));
            m_Details.Add(bar);

            AddControls("ATTRIBUTES", "UXML attributes, set through their C# properties",
                type.controls.Where(c => c.target == BagelArtGuide.Target.Attribute));
            AddControls("STYLES", "Inline styles", type.controls.Where(c => c.target == BagelArtGuide.Target.Style));
            AddControls("CUSTOM PROPERTIES", "USS custom properties the art reads, from the season's theme until you set them",
                type.controls.Where(c => c.target == BagelArtGuide.Target.CustomProperty));

            var about = Text(type.about, "bgf-details__about");
            about.AddToClassList("bgf-details__about--lead");
            m_Details.Add(about);

            m_Details.Add(Text("AS UXML AND USS", "bgf-section"));
            m_ArtMarkup = new VisualElement { name = "art-markup" };
            m_Details.Add(m_ArtMarkup);
            UpdateArtMarkup();

            m_Details.Add(Text("SEE IT IN THE EDITOR", "bgf-section"));
            var links = new VisualElement();
            links.AddToClassList("bgg-links");
            links.Add(LinkButton(type.script.EndsWith(".cs") ? EditorLink.File(type.script) : EditorLink.Asset(type.script)));
            m_Details.Add(links);
            AddUses(type);

            m_Details.Add(Text("IN THE GAME", "bgf-section"));
            var gallery = new VisualElement { name = "art-gallery", tooltip = "Click one to try it above (a season's switches the window's season)" };
            gallery.AddToClassList("bgf-art-gallery");
            foreach (var variation in type.variations)
                gallery.Add(GalleryCell(type, variation));
            m_Details.Add(gallery);

            m_ArtStage.schedule.Execute(SyncResolvedFields);
        }

        void AddArtThemedRoot(VisualElement root)
        {
            AddThemedRoot(root);
            m_ArtThemedRoots.Add(root);
        }

        /// <summary>One variation the game uses: the art on its sky, and its name. A click loads it into the preview.</summary>
        VisualElement GalleryCell(BagelArtGuide.ArtType type, BagelArtGuide.Variation variation)
        {
            var cell = new VisualElement { name = "variation-" + m_ArtCells.Count, tooltip = variation.tooltip };
            cell.AddToClassList(artCellUssClassName);
            var (box, _) = BagelArtGuide.Create(type, BagelArtGuide.ValuesOf(type, variation), type.gallerySize, false, m_ArtSheets);
            VisualElement frame;
            if (variation.season >= 0)
            {
                frame = SeasonFrame(box, variation.season, "bgf-art-cell__frame");
            }
            else
            {
                frame = new VisualElement { pickingMode = PickingMode.Ignore };
                frame.AddToClassList("bgf-art-cell__frame");
                frame.AddToClassList("be-backdrop");
                AddArtThemedRoot(frame);
                frame.Add(box);
            }
            if (type.fills)
                frame.AddToClassList("bgf-art-cell__frame--fill");
            frame.pickingMode = PickingMode.Ignore;
            cell.Add(frame);
            var caption = Text(variation.caption ?? SeasonName(variation.season), "bgf-art-cell__caption");
            caption.style.maxWidth = type.gallerySize.x + (type.fills ? 0f : 10f);
            cell.Add(caption);
            var index = m_ArtCells.Count;
            cell.RegisterCallback<ClickEvent>(_ => LoadVariation(index));
            m_ArtCells.Add(cell);
            return cell;
        }

        /// <summary>Put a variation of the gallery in the preview (and its season in the window).</summary>
        public void LoadVariation(int index)
        {
            if (m_ArtType == null || index < 0 || index >= m_ArtType.variations.Length)
                return;
            var variation = m_ArtType.variations[index];
            var values = BagelArtGuide.ValuesOf(m_ArtType, variation);
            // The size stays as set: a variation is a look, not a size.
            foreach (var control in m_ArtType.controls.Where(BagelArtGuide.IsSize))
                values[control.name] = m_ArtValues[control.name];
            m_ArtValues = values;
            BagelArtGuide.Apply(m_ArtType, m_ArtPreview, m_ArtValues, true, m_ArtSheets, RetireSheet);
            SyncFields();
            UpdateArtMarkup();
            for (int i = 0; i < m_ArtCells.Count; i++)
                m_ArtCells[i].EnableInClassList(artCellSelectedUssClassName, i == index);
            if (variation.season >= 0 && variation.season != currentSeason)
                SelectSeason(variation.season);
            m_ArtType.onShown?.Invoke(m_ArtPreview);
        }

        /// <summary>Back to the element's defaults, and the theme's custom properties.</summary>
        public void ResetArt()
        {
            if (m_ArtType == null)
                return;
            m_ArtValues = BagelArtGuide.ValuesOf(m_ArtType, null);
            BagelArtGuide.Apply(m_ArtType, m_ArtPreview, m_ArtValues, true, m_ArtSheets, RetireSheet);
            SyncFields();
            UpdateArtMarkup();
            foreach (var cell in m_ArtCells)
                cell.RemoveFromClassList(artCellSelectedUssClassName);
        }

        void ClearArtDetails()
        {
            foreach (var root in m_ArtThemedRoots)
                RemoveThemedRoot(root);
            m_ArtThemedRoots.Clear();
            foreach (var part in m_DetailsEditorParts)
                RemoveEditorStyledPart(part);
            m_DetailsEditorParts.Clear();
            foreach (var sheet in m_ArtSheets.Concat(m_RetiredSheets))
                if (sheet != null)
                    DestroyImmediate(sheet);
            m_ArtSheets.Clear();
            m_RetiredSheets.Clear();
            m_DestroyRetired?.Pause();
            m_ArtFieldSync.Clear();
            m_ArtCells.Clear();
            m_ArtType = null;
            m_ArtStage = null;
            m_ArtPreview = null;
            m_ArtMarkup = null;
            Discard.Element(m_Sticky);
            m_Sticky = null;
            m_StickySlot = null;
        }

        /// <summary>The preview over its slot, or at the top of the details once the slot is scrolled past.</summary>
        void PositionSticky()
        {
            if (m_Sticky == null || m_StickySlot?.panel == null || m_Sticky.hierarchy.parent == null)
                return;
            var slot = m_Sticky.hierarchy.parent.WorldToLocal(m_StickySlot.worldBound);
            if (float.IsNaN(slot.width) || slot.width < 1f)
                return;
            var top = slot.y - m_Sticky.resolvedStyle.paddingTop;
            m_Sticky.style.left = slot.x;
            m_Sticky.style.width = slot.width;
            m_Sticky.style.top = Mathf.Max(0f, top);
            m_Sticky.EnableInClassList(stickyStuckUssClassName, top < 0f);
        }

        // ------------------------------------------------------------------ where the game uses it

        /// <summary>The screens that use it (each opens Game mode there, its part's dot pulsing), and Scene and UI Builder for one.</summary>
        void AddUses(BagelArtGuide.ArtType type)
        {
            if (type.uses.Length == 0)
                return;
            var uses = new VisualElement { name = "art-uses" };
            uses.AddToClassList("bgf-art-uses");
            uses.Add(Text("Used on", "bgf-art-uses__label"));
            foreach (var use in type.uses)
            {
                var chip = new Button(() => ShowUse(use)) { text = $"{use.screen.title} › {use.title}", tooltip = $"Show it on the {use.screen.title} screen" };
                chip.AddToClassList("bgf-part__screen");
                chip.AddToClassList("bgf-art-use");
                uses.Add(chip);
            }
            m_Details.Add(uses);

            var representative = type.representative;
            if (representative == null)
                return;
            var actions = new VisualElement();
            actions.AddToClassList("bgf-part__actions");
            actions.AddToClassList("bgf-art-uses__actions");
            var what = $"#{representative.element} ({representative.screen.title} › {representative.title})";
            actions.Add(Action("◎ Scene", $"Select {what} in Main.unity's UI (opens the scene if it isn't): Hierarchy and Inspector",
                "bgf-action--scene", () => EditorLink.Select(representative.element).Open()));
            if (representative.uxml != null)
            {
                actions.Add(Action("UI Builder", $"Open {representative.uxml} in UI Builder with {what} selected",
                    "bgf-action--builder", () => EditorLink.UIBuilder(representative.uxml, representative.element).Open()));
                actions.Add(Action("Project", $"Select {representative.uxml} in the Project window", "bgf-action--asset",
                    () => EditorLink.Asset(representative.uxml).Open()));
            }
            actions.Add(Text($"#{representative.element}", "bgf-art-uses__what"));
            m_Details.Add(actions);
        }

        /// <summary>Game mode, on the use's screen; its part's dot pulses and its entry comes into view when there's one.</summary>
        public void ShowUse(BagelArtGuide.Use use)
        {
            var screens = BagelFeatureGuide.screens;
            var screenIndex = screens.ToList().FindIndex(s => s.screen == use.screen);
            if (screenIndex < 0)
                return;
            var partIndex = use.part == null ? -1 : Array.FindIndex(screens[screenIndex].parts, p => p.selector == use.part);
            if (partIndex >= 0)
                ShowPart(screenIndex, partIndex);
            else
                ShowScreen(screenIndex);
        }

        /// <summary>The Art tab, on an element's row.</summary>
        public void ShowArt(string id)
        {
            m_Art = BagelArtGuide.Get(id)?.id ?? m_Art;
            ShowMode(Mode.Art);
        }

        /// <summary>A feature's link into the Art tab (where the catalog links to the Art Sampler).</summary>
        Button ArtLink(string featureId)
        {
            var id = featureId switch
            {
                "custom-style-properties" => "begu-chan",
                "generate-visual-content" => "starburst",
                _ => "pastry-icon",
            };
            var type = BagelArtGuide.Get(id);
            var button = new Button(() => ShowArt(id)) { text = "↗  Art › " + type.title, tooltip = $"Open {type.title} in the Art tab: every element drawn in code, its attributes and custom properties to try" };
            button.AddToClassList("bgg-link");
            button.AddToClassList("bgg-link--window");
            return button;
        }

        // ------------------------------------------------------------------ the controls

        void AddControls(string title, string tooltip, IEnumerable<BagelArtGuide.Control> controls)
        {
            var list = controls.ToList();
            if (list.Count == 0)
                return;
            var heading = Text(title, "bgf-section");
            heading.tooltip = tooltip;
            heading.pickingMode = PickingMode.Position;
            m_Details.Add(heading);
            // Editor fields, in the editor's look: the window's theme is lifted, so they get it back here.
            var box = new VisualElement();
            box.AddToClassList("bgf-art-controls");
            foreach (var control in list)
                box.Add(Field(control));
            AddEditorStyledPart(box);
            m_DetailsEditorParts.Add(box);
            m_Details.Add(box);
        }

        VisualElement Field(BagelArtGuide.Control control)
        {
            var field = CreateField(control, m_ArtValues[control.name] ?? control.fallback, v => SetArtValue(control, v), out var sync);
            m_ArtFieldSync[control.name] = sync;
            if (control.target == BagelArtGuide.Target.CustomProperty && !BagelArtGuide.canOverrideCustomProperties)
            {
                field.SetEnabled(false);
                field.tooltip = "The USS importer couldn't be reached: custom properties can't be set here.";
            }
            return field;
        }

        /// <summary>An Editor field for a control (in the Editor's look), and how to show a value in it without notice.</summary>
        static VisualElement CreateField(BagelArtGuide.Control control, object value, Action<object> changed, out Action<object> sync)
        {
            VisualElement field;
            switch (control.type)
            {
                case BagelArtGuide.ValueType.Bool:
                {
                    var toggle = new Toggle(control.label) { value = value is bool b && b };
                    toggle.RegisterValueChangedCallback(e => changed(e.newValue));
                    sync = v => toggle.SetValueWithoutNotify((bool)v);
                    field = toggle;
                    break;
                }
                case BagelArtGuide.ValueType.Int:
                {
                    var slider = new SliderInt(control.label, (int)control.min, (int)control.max) { value = Convert.ToInt32(value), showInputField = true };
                    slider.RegisterValueChangedCallback(e => changed(e.newValue));
                    sync = v => slider.SetValueWithoutNotify(Convert.ToInt32(v));
                    field = slider;
                    break;
                }
                case BagelArtGuide.ValueType.Float:
                {
                    var slider = new Slider(control.label, control.min, control.max) { value = Convert.ToSingle(value), showInputField = true };
                    slider.RegisterValueChangedCallback(e => changed(e.newValue));
                    sync = v => slider.SetValueWithoutNotify(Convert.ToSingle(v));
                    field = slider;
                    break;
                }
                case BagelArtGuide.ValueType.Text:
                {
                    var text = new TextField(control.label) { value = (string)value };
                    text.RegisterValueChangedCallback(e => changed(e.newValue));
                    sync = v => text.SetValueWithoutNotify((string)v);
                    field = text;
                    break;
                }
                case BagelArtGuide.ValueType.Choice:
                {
                    var choices = control.choices.ToList();
                    var dropdown = new DropdownField(control.label, choices, Mathf.Max(0, choices.IndexOf((string)value)));
                    dropdown.RegisterValueChangedCallback(e => changed(e.newValue));
                    sync = v => dropdown.SetValueWithoutNotify((string)v);
                    field = dropdown;
                    break;
                }
                default:
                {
                    var color = new ColorField(control.label) { value = value is Color c ? c : Color.white, showAlpha = true, hdr = false };
                    color.RegisterValueChangedCallback(e => changed(e.newValue));
                    sync = v => color.SetValueWithoutNotify((Color)v);
                    field = color;
                    break;
                }
            }
            field.name = "field-" + control.name.TrimStart('-');
            field.tooltip = control.tooltip ?? (control.target == BagelArtGuide.Target.Attribute ? control.name : null);
            field.AddToClassList("bgf-art-field");
            return field;
        }

        /// <summary>
        /// A preview that stays in view: drawn over the details, in the place of a slot it leaves at this point of them,
        /// and pinned to their top once the slot scrolls past. The wheel over it still scrolls the details.
        /// </summary>
        void AddSticky(VisualElement stage, string name)
        {
            m_StickySlot = new VisualElement { name = name + "-stage-slot", pickingMode = PickingMode.Ignore };
            m_StickySlot.AddToClassList("bgf-art-stage-slot");
            m_StickySlot.RegisterCallback<GeometryChangedEvent>(_ => PositionSticky());
            m_Details.Add(m_StickySlot);
            m_Sticky = new VisualElement { name = name + "-sticky" };
            m_Sticky.AddToClassList("bgf-art-sticky");
            m_Sticky.RegisterCallback<WheelEvent>(evt =>
            {
                m_DetailsScroll.scrollOffset += new Vector2(0f, evt.delta.y * m_DetailsScroll.mouseWheelScrollSize);
                evt.StopPropagation();
            });
            m_Sticky.Add(stage);
            rootVisualElement.Q("details-pane").Add(m_Sticky);
        }

        /// <summary>A control changed: set the one thing it sets (a custom property rebuilds the art's style sheet).</summary>
        void SetArtValue(BagelArtGuide.Control control, object value)
        {
            if (m_ArtType == null || m_ArtPreview == null)
                return;
            m_ArtValues[control.name] = value;
            if (control.target == BagelArtGuide.Target.CustomProperty)
                BagelArtGuide.ApplyCustom(m_ArtType, m_ArtPreview, m_ArtValues, m_ArtSheets, RetireSheet);
            else
                control.Set(m_ArtPreview, value);
            foreach (var cell in m_ArtCells)
                cell.RemoveFromClassList(artCellSelectedUssClassName);
            UpdateArtMarkup();
        }

        /// <summary>Set a control from code, as if from its field.</summary>
        public void SetArtValue(string controlName, object value)
        {
            var control = m_ArtType?.Get(controlName);
            if (control == null)
                return;
            SetArtValue(control, value);
            if (value != null && m_ArtFieldSync.TryGetValue(controlName, out var sync))
                sync(value);
        }

        void RetireSheet(StyleSheet sheet)
        {
            m_RetiredSheets.Add(sheet);
            m_DestroyRetired?.Pause();
            m_DestroyRetired = rootVisualElement.schedule.Execute(() =>
            {
                foreach (var retired in m_RetiredSheets)
                    if (retired != null)
                        DestroyImmediate(retired);
                m_RetiredSheets.Clear();
            });
            m_DestroyRetired.ExecuteLater(500);
        }

        /// <summary>The fields show the values; the custom properties left to the theme, once the art's style is updated.</summary>
        void SyncFields()
        {
            foreach (var (name, sync) in m_ArtFieldSync)
                if (m_ArtValues.TryGetValue(name, out var value) && value != null)
                    sync(value);
            m_ArtPreview?.schedule.Execute(SyncResolvedFields);
        }

        /// <summary>The custom properties left to the theme show the theme's value (or the element's own default).</summary>
        void SyncResolvedFields()
        {
            if (m_ArtType == null || m_ArtPreview == null)
                return;
            foreach (var control in m_ArtType.controls)
            {
                if (control.target != BagelArtGuide.Target.CustomProperty || m_ArtValues[control.name] != null)
                    continue;
                if (m_ArtFieldSync.TryGetValue(control.name, out var sync))
                    sync(BagelArtGuide.Resolved(m_ArtPreview, control) ?? control.fallback);
            }
        }

        void UpdateArtMarkup()
        {
            if (m_ArtMarkup == null || m_ArtType == null)
                return;
            var (uxml, uss) = BagelArtGuide.Markup(m_ArtType, m_ArtValues);
            m_ArtMarkup.Clear();
            m_ArtMarkup.Add(SnippetView(new BagelFeatureGuide.Snippet(m_ArtType.id, BagelFeatureGuide.Language.Uxml, "As set above", uxml)));
            m_ArtMarkup.Add(SnippetView(new BagelFeatureGuide.Snippet(m_ArtType.id, BagelFeatureGuide.Language.Uss, "Size, and what changed", uss)));
        }
    }
}
