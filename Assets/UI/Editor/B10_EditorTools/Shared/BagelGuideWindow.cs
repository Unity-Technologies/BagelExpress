using System;
using System.Collections.Generic;
using UnityEditor;
using Unity.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// What the Bagel Features and the Bagel Demo windows share with the Bagel UI window: the dark stage with the game's
    /// fonts, the editor theme kept off the game UI, one throwaway <see cref="BakeryState"/> for every live screen, and
    /// the Season and Language strips.
    /// </summary>
    public abstract class BagelGuideWindow : EditorWindow
    {
        public const string themeCollectionPath = BagelExpressAllUIWindow.themeCollectionPath;

        BakeryState m_State;
        BagelThemeCollection m_ThemeCollection;
        ThemeStyleSheet m_CurrentTheme;
        BagelOptionStrip m_SeasonStrip;
        BagelOptionStrip m_LanguageStrip;

        // Editor theme handling: like the Bagel UI window, the editor theme is lifted off the window (it would leak editor
        // fonts and field styles into the game UI) and given back only to the parts that are editor UI.
        readonly List<StyleSheet> m_EditorSheets = new();
        readonly List<(VisualElement owner, StyleSheet sheet)> m_BorrowedSheets = new();
        readonly List<VisualElement> m_EditorStyledParts = new();
        readonly List<VisualElement> m_ThemedRoots = new();

        /// <summary>The Editor's theme, lifted off the window: for parts that show Editor UI (see <see cref="AddEditorStyledPart"/>).</summary>
        public IReadOnlyList<StyleSheet> editorThemeSheets => m_EditorSheets;

        /// <summary>The one game state every live screen in the window is bound to.</summary>
        public BakeryState state => m_State;
        /// <summary>Raised when the state is replaced by a fresh copy (Reset).</summary>
        public event Action<BakeryState> stateReplaced;
        public ThemeStyleSheet currentTheme => m_CurrentTheme;
        public BagelThemeCollection themeCollection => m_ThemeCollection;

        protected abstract string windowUxmlPath { get; }
        protected abstract void BuildContent();

        public void CreateGUI()
        {
            stateReplaced = null;
            DestroyState();
            m_State = BakeryState.CreateRuntimeCopy(AssetDatabase.LoadAssetAtPath<BakeryState>(BagelExpressAllUIWindow.startStatePath));
            m_ThemeCollection = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(themeCollectionPath);
            var seasonIndex = Mathf.Clamp(SettingsPaneManager.seasonIndex, 0, Mathf.Max(0, ThemeCount() - 1));
            m_CurrentTheme = ThemeCount() > 0 ? m_ThemeCollection.collection[seasonIndex].theme : null;
            m_ThemedRoots.Clear();

            var windowTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(windowUxmlPath);
            if (windowTree == null)
            {
                rootVisualElement.Add(new Label($"Missing {windowUxmlPath}"));
                return;
            }

            m_EditorSheets.Clear();
            for (int i = 0; i < rootVisualElement.styleSheets.count; i++)
                m_EditorSheets.Add(rootVisualElement.styleSheets[i]);
            rootVisualElement.styleSheets.Clear();
            windowTree.CloneTree(rootVisualElement);

            m_EditorStyledParts.Clear();
            rootVisualElement.Query<Toolbar>().ForEach(m_EditorStyledParts.Add);
            rootVisualElement.Query<ScrollView>().ForEach(AddEditorStyledScrollers);
            rootVisualElement.RegisterCallback<AttachToPanelEvent>(_ => LiftEditorTheme());
            rootVisualElement.RegisterCallback<DetachFromPanelEvent>(_ => RestoreEditorTheme());
            if (rootVisualElement.panel != null)
                LiftEditorTheme();
            else
                ShareEditorTheme();

            BindSeasonAndLanguage();
            BuildContent();
            // Its children too: one that shrinks (a search field) unwraps a row without changing the toolbar's own size.
            rootVisualElement.Query<Toolbar>().ForEach(toolbar =>
            {
                toolbar.RegisterCallback<GeometryChangedEvent>(_ => FitToolbar(toolbar));
                foreach (var child in toolbar.Children())
                    child.RegisterCallback<GeometryChangedEvent>(_ => FitToolbar(toolbar));
            });

            SettingsPaneManager.themeChanged -= OnThemeChanged;
            SettingsPaneManager.themeChanged += OnThemeChanged;
            BagelLocalization.localeChanged -= SyncLanguageStrip;
            BagelLocalization.localeChanged += SyncLanguageStrip;
        }

        protected virtual void OnDisable()
        {
            SettingsPaneManager.themeChanged -= OnThemeChanged;
            BagelLocalization.localeChanged -= SyncLanguageStrip;
            RestoreEditorTheme();
            m_ThemedRoots.Clear();
            DestroyState();
        }

        // Closed for good: everything the window made gives its resources back to be reused now (a domain reload drops
        // them all anyway, so OnDisable doesn't).
        void OnDestroy() => rootVisualElement.Clear(VisualElementClearOptions.RecursiveReleaseResources);

        void DestroyState()
        {
            if (m_State != null)
                DestroyImmediate(m_State);
            m_State = null;
        }

        /// <summary>Start over with a fresh copy of the demo start state.</summary>
        protected void ResetState(Action<BakeryState> handOut)
        {
            var old = m_State;
            m_State = BakeryState.CreateRuntimeCopy(AssetDatabase.LoadAssetAtPath<BakeryState>(BagelExpressAllUIWindow.startStatePath));
            handOut?.Invoke(m_State);
            stateReplaced?.Invoke(m_State);
            if (old != null)
                DestroyImmediate(old);
        }

        /// <summary>
        /// A wrapping toolbar measures taller than its rows (flex-wrap with auto height keeps the unwrapped line's extra
        /// height), so it's sized to them: each row as tall as its tallest control, the gaps between, its padding and
        /// border. Not from where the controls are: one row stretches to the toolbar's height whatever align-content
        /// says (the layout aligns several rows only), so they'd keep any height it had. The Bagel UI window's too.
        /// </summary>
        internal static void FitToolbar(VisualElement toolbar)
        {
            var rows = 0;
            var rowsHeight = 0f;
            var rowHeight = 0f;
            var previousX = float.PositiveInfinity;
            foreach (var child in toolbar.Children())
            {
                if (child.resolvedStyle.display == DisplayStyle.None)
                    continue;
                // A row starts back at the left.
                if (child.layout.x < previousX)
                {
                    rows++;
                    rowsHeight += rowHeight;
                    rowHeight = 0f;
                }
                previousX = child.layout.x;
                rowHeight = Mathf.Max(rowHeight, child.layout.height + child.resolvedStyle.marginTop + child.resolvedStyle.marginBottom);
            }
            rowsHeight += rowHeight;
            var style = toolbar.resolvedStyle;
            var height = rowsHeight + Mathf.Max(0, rows - 1) * style.rowGap + style.paddingTop + style.paddingBottom + style.borderTopWidth + style.borderBottomWidth;
            height = Mathf.Max(style.minHeight.value, Mathf.Ceil(height));
            if (Mathf.Abs(toolbar.resolvedStyle.height - height) > 0.5f)
                toolbar.style.height = height;
        }

        /// <summary>A pill that opens an <see cref="EditorLink"/>.</summary>
        public static Button LinkButton(EditorLink link)
        {
            var glyph = link.kind == EditorLink.Kind.File ? "‹›  " : "↗  ";
            var button = new Button(link.Open) { text = glyph + link.label, tooltip = link.tooltip };
            button.AddToClassList("bgg-link");
            button.AddToClassList(link.kind == EditorLink.Kind.File ? "bgg-link--file" : "bgg-link--window");
            if (link.kind == EditorLink.Kind.File && EditorLink.FindLine(link.path, link.find) == 0)
            {
                button.AddToClassList("bgg-link--missing");
                button.tooltip = "Not found: " + link.path + (string.IsNullOrEmpty(link.find) ? "" : " › \"" + link.find + "\"");
            }
            return button;
        }

        // ------------------------------------------------------------------ editor theme

        /// <summary>Give the editor theme to an element that's editor UI (fields, buttons in the editor look), e.g. built later.</summary>
        protected void AddEditorStyledPart(VisualElement part)
        {
            if (part == null || m_EditorStyledParts.Contains(part))
                return;
            m_EditorStyledParts.Add(part);
            ShareEditorTheme();
        }

        /// <summary>Forget an editor-styled part that's gone (rebuilt content).</summary>
        protected void RemoveEditorStyledPart(VisualElement part) => m_EditorStyledParts.Remove(part);

        protected void AddEditorStyledScrollers(ScrollView scrollView)
        {
            AddEditorStyledPart(scrollView.horizontalScroller);
            AddEditorStyledPart(scrollView.verticalScroller);
        }

        void LiftEditorTheme()
        {
            if (m_BorrowedSheets.Count == 0)
            {
                for (var ancestor = rootVisualElement.hierarchy.parent; ancestor != null; ancestor = ancestor.hierarchy.parent)
                {
                    for (int i = 0; i < ancestor.styleSheets.count; i++)
                    {
                        var sheet = ancestor.styleSheets[i];
                        m_BorrowedSheets.Add((ancestor, sheet));
                        if (!m_EditorSheets.Contains(sheet))
                            m_EditorSheets.Add(sheet);
                    }
                    ancestor.styleSheets.Clear();
                }
            }
            ShareEditorTheme();
        }

        void ShareEditorTheme()
        {
            foreach (var part in m_EditorStyledParts)
                if (part != null)
                    foreach (var sheet in m_EditorSheets)
                        if (!part.styleSheets.Contains(sheet))
                            part.styleSheets.Add(sheet);
        }

        void RestoreEditorTheme()
        {
            foreach (var (owner, sheet) in m_BorrowedSheets)
                if (owner != null && !owner.styleSheets.Contains(sheet))
                    owner.styleSheets.Add(sheet);
            m_BorrowedSheets.Clear();
        }

        // ------------------------------------------------------------------ game theme (season)

        int ThemeCount() => m_ThemeCollection != null && m_ThemeCollection.collection != null ? m_ThemeCollection.collection.Count : 0;

        /// <summary>
        /// Make an element a root of game UI: it gets the current season's ThemeStyleSheet, and follows season changes. Its
        /// text renders as SDF, as in the game: the Editor's default, bitmap glyphs made for one size, blurs when the game
        /// UI is scaled (a thumbnail, a preview, a phone at 75%).
        /// </summary>
        public void AddThemedRoot(VisualElement root)
        {
            if (root == null)
                return;
            root.style.unityEditorTextRenderingMode = EditorTextRenderingMode.SDF;
            m_ThemedRoots.RemoveAll(r => r == null || (r.panel == null && r != root && r.hierarchy.parent == null));
            if (!m_ThemedRoots.Contains(root))
                m_ThemedRoots.Add(root);
            if (m_CurrentTheme != null && !root.styleSheets.Contains(m_CurrentTheme))
                root.styleSheets.Add(m_CurrentTheme);
        }

        public void RemoveThemedRoot(VisualElement root) => m_ThemedRoots.Remove(root);

        public void ApplyRuntimeTheme(ThemeStyleSheet theme)
        {
            if (theme == null)
                return;
            foreach (var root in m_ThemedRoots)
            {
                if (m_CurrentTheme != null)
                    root.styleSheets.Remove(m_CurrentTheme);
                if (!root.styleSheets.Contains(theme))
                    root.styleSheets.Add(theme);
            }
            m_CurrentTheme = theme;
            SyncSeasonStrip();
        }

        public void SelectSeason(int index)
        {
            if (index < 0 || index >= ThemeCount())
                return;
            // Like the in-game Settings page, so every live Settings page shows the new season as selected; it raises
            // SettingsPaneManager.themeChanged, which lands in OnThemeChanged.
            foreach (var root in m_ThemedRoots)
            {
                var settings = root?.Q<SettingsPaneManager>();
                if (settings != null && settings.themeCollection != null)
                {
                    settings.SelectSeason(index);
                    return;
                }
            }
            ApplyRuntimeTheme(m_ThemeCollection.collection[index].theme);
        }

        void OnThemeChanged(ThemeStyleSheet theme) => ApplyRuntimeTheme(theme);

        /// <summary>
        /// Back to the first season and the project's language, in every Bagel window and the game's Settings page: the
        /// season is picked the way the Settings page picks it (without its strum and toast), and every window follows.
        /// </summary>
        public static void ResetSeasonAndLanguage()
        {
            var themes = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(themeCollectionPath);
            if (themes != null && themes.collection != null && themes.collection.Count > 0)
                new SettingsPaneManager { themeCollection = themes }.SelectSeason(0, announce: false);
            var locale = LocalizationSettings.Instance?.ProjectLocale;
            if (locale != null)
                BagelLocalization.SetLocale(locale.Identifier.Code);
        }

        void BindSeasonAndLanguage()
        {
            m_SeasonStrip = rootVisualElement.Q<BagelOptionStrip>("season-strip");
            if (m_SeasonStrip != null)
            {
                BagelPickerIcons.AddSeasons(m_SeasonStrip, m_ThemeCollection);
                SyncSeasonStrip();
                m_SeasonStrip.chosen += SelectSeason;
            }

            m_LanguageStrip = rootVisualElement.Q<BagelOptionStrip>("language-strip");
            if (m_LanguageStrip != null)
            {
                BagelPickerIcons.AddLanguages(m_LanguageStrip);
                SyncLanguageStrip();
                m_LanguageStrip.chosen += index => BagelLocalization.SetLocale(BagelPickerIcons.languages[index].code);
            }
        }

        void SyncSeasonStrip()
        {
            if (m_SeasonStrip != null && m_ThemeCollection != null)
                m_SeasonStrip.SetSelectedWithoutNotify(m_ThemeCollection.collection.FindIndex(e => e.theme == m_CurrentTheme));
        }

        void SyncLanguageStrip() => m_LanguageStrip?.SetSelectedWithoutNotify(BagelPickerIcons.LanguageIndex(BagelLocalization.localeCode));
    }
}
