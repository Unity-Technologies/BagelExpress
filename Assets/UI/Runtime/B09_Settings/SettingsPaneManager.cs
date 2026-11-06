using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    public enum AppLanguage
    {
        English,
        Japanese,
        French
    }

    /// <summary>
    /// Settings page: pick the season (a ThemeStyleSheet) and the language, replay the tutorial, and the performance
    /// options: the performance overlay, Auto mode, and the settings Auto mode runs (locked while it's on): the render
    /// resolution, the blurred drop-shadows on panes, and switches that turn off every animation, the glass or every filter.
    /// Pure UI: it raises events and, when given a PanelSettingsCollection, swaps the
    /// runtime panels' theme. Editor hosts (Bagel UI window) listen to the events instead.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class SettingsPaneManager : BakeryPane
    {
        public static readonly string ussClassName = "be-settings";
        public static readonly string seasonCardUssClassName = "be-season-card";
        public static readonly UniqueStyleString seasonCardSelectedUssClassName = new("be-season-card--selected");
        public static readonly UniqueStyleString languageSelectedUssClassName = new("be-segment--selected");
        public static readonly UniqueStyleString resolutionSelectedUssClassName = new("be-segment--selected");
        static readonly string[] s_ResolutionButtonNames = { "resolution-native", "resolution-3-4", "resolution-1-2", "resolution-1-4" };

        static readonly string[] s_SeasonKeys = { "maple", "sakura", "matsuri", "snow" };

        /// <summary>The seasons' keys, in the theme collection's order (the season cards' and the Editor's season icons).</summary>
        public static IReadOnlyList<string> seasonKeys => s_SeasonKeys;

        VisualElement m_SeasonGrid;
        Button m_EnglishButton;
        Button m_JapaneseButton;
        Button m_FrenchButton;
        Toggle m_SoundToggle;
        Toggle m_HapticsToggle;
        Button m_ResetProgressButton;
        Button m_ResetSettingsButton;
        Toggle m_FilterShadowsToggle;
        Toggle m_BoxShadowsToggle;
        Toggle m_NoAnimationsToggle;
        Toggle m_NoFiltersToggle;
        Toggle m_NoBlurToggle;
        Toggle m_PerfOverlayToggle;
        Toggle m_AutoModeToggle;
        VisualElement m_AutoSettings;
        Button[] m_ResolutionButtons;
        Button m_ReplayTutorialButton;
        bool m_Bound;

        static int s_SeasonIndex;

        // Enter Play Mode Options skip the domain reload in this project: every Play session starts on the first season,
        // not the one the Editor's windows were left on.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_SeasonIndex = 0;

        [UxmlAttribute]
        public BagelThemeCollection themeCollection { get; set; }

        [UxmlAttribute]
        public PanelSettingsCollection panelSettingsCollection { get; set; }

        public VisualElement seasonGrid => m_SeasonGrid ??= this.Q<VisualElement>("season-grid");
        public Button englishButton => m_EnglishButton ??= this.Q<Button>("language-en");
        public Button japaneseButton => m_JapaneseButton ??= this.Q<Button>("language-ja");
        public Button frenchButton => m_FrenchButton ??= this.Q<Button>("language-fr");
        public Toggle soundToggle => m_SoundToggle ??= this.Q<Toggle>("sound-toggle");
        public Toggle hapticsToggle => m_HapticsToggle ??= this.Q<Toggle>("haptics-toggle");
        public Button resetProgressButton => m_ResetProgressButton ??= this.Q<Button>("reset-progress-button");
        public Button resetSettingsButton => m_ResetSettingsButton ??= this.Q<Button>("reset-settings-button");
        public Toggle filterShadowsToggle => m_FilterShadowsToggle ??= this.Q<Toggle>("filter-shadows-toggle");
        public Toggle boxShadowsToggle => m_BoxShadowsToggle ??= this.Q<Toggle>("box-shadows-toggle");
        public Toggle noAnimationsToggle => m_NoAnimationsToggle ??= this.Q<Toggle>("no-animations-toggle");
        public Toggle noFiltersToggle => m_NoFiltersToggle ??= this.Q<Toggle>("no-filters-toggle");
        public Toggle noBlurToggle => m_NoBlurToggle ??= this.Q<Toggle>("no-blur-toggle");
        public Toggle perfOverlayToggle => m_PerfOverlayToggle ??= this.Q<Toggle>("perf-overlay-toggle");
        public Toggle autoModeToggle => m_AutoModeToggle ??= this.Q<Toggle>("auto-mode-toggle");
        /// <summary>The settings Auto mode runs: disabled while it's on.</summary>
        public VisualElement autoSettings => m_AutoSettings ??= this.Q<VisualElement>("auto-settings");
        /// <summary>Native, 3/4, 1/2 and 1/4, in <see cref="RenderResolution.scales"/> order.</summary>
        public Button[] resolutionButtons => m_ResolutionButtons ??= Array.ConvertAll(s_ResolutionButtonNames, n => this.Q<Button>(n));
        public Button replayTutorialButton => m_ReplayTutorialButton ??= this.Q<Button>("replay-tutorial-button");

        public static int seasonIndex => s_SeasonIndex;
        /// <summary>The selected Localization locale, as an app language.</summary>
        public static AppLanguage language => BagelLocalization.localeCode switch
        {
            "ja" => AppLanguage.Japanese,
            "fr" => AppLanguage.French,
            _ => AppLanguage.English
        };

        /// <summary>Locale code for an app language.</summary>
        public static string LocaleCode(AppLanguage appLanguage) => appLanguage switch
        {
            AppLanguage.Japanese => "ja",
            AppLanguage.French => "fr",
            _ => "en"
        };

        /// <summary>Raised with the chosen theme (all settings panes in the app, editor or runtime).</summary>
        public static event Action<ThemeStyleSheet> themeChanged;
        public static event Action<AppLanguage> languageChanged;

        // Raised for any locale change, also from the Localization toolbar or a LanguageDropdown.
        static SettingsPaneManager() => BagelLocalization.localeChanged += () => languageChanged?.Invoke(language);

        public SettingsPaneManager()
        {
            AddToClassList(ussClassName);
            // Off a phone the native resolution is the window's (the Game view): show the new one when it resizes.
            RegisterCallback<GeometryChangedEvent>(_ => RefreshResolutions());
        }

        protected override void OnAttached()
        {
            if (!m_Bound)
            {
                m_Bound = true;
                BindSeasonCards();
                englishButton?.RegisterCallback<ClickEvent>(_ => SetLanguage(AppLanguage.English));
                japaneseButton?.RegisterCallback<ClickEvent>(_ => SetLanguage(AppLanguage.Japanese));
                frenchButton?.RegisterCallback<ClickEvent>(_ => SetLanguage(AppLanguage.French));
                soundToggle?.RegisterValueChangedCallback(evt => BagelSound.enabled = evt.newValue);
                hapticsToggle?.RegisterValueChangedCallback(evt => BagelSave.haptics = evt.newValue);
                resetProgressButton?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!BakeryAppManager.IsPinned(this))
                        app?.ConfirmResetProgress();
                });
                resetSettingsButton?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!BakeryAppManager.IsPinned(this))
                        app?.ConfirmResetSettings();
                });
                filterShadowsToggle?.RegisterValueChangedCallback(evt => BakeryAppManager.filterShadows = evt.newValue);
                boxShadowsToggle?.RegisterValueChangedCallback(evt => BakeryAppManager.boxShadows = evt.newValue);
                noAnimationsToggle?.RegisterValueChangedCallback(evt => BakeryAppManager.noAnimations = evt.newValue);
                noFiltersToggle?.RegisterValueChangedCallback(evt => BakeryAppManager.noFilters = evt.newValue);
                noBlurToggle?.RegisterValueChangedCallback(evt => BakeryAppManager.noBlur = evt.newValue);
                perfOverlayToggle?.RegisterValueChangedCallback(evt => PerfOverlay.shown = evt.newValue);
                autoModeToggle?.RegisterValueChangedCallback(evt => AutoPerformance.on = evt.newValue);
                for (int i = 0; i < resolutionButtons.Length; i++)
                {
                    var choice = i;
                    resolutionButtons[i]?.RegisterCallback<ClickEvent>(_ => RenderResolution.Select(choice));
                }
                replayTutorialButton?.RegisterCallback<ClickEvent>(_ =>
                {
                    if (!BakeryAppManager.IsPinned(this))
                        app?.RestartTutorial();
                });
            }
            themeChanged += OnThemeChanged;
            languageChanged += OnLanguageChanged;
            BakeryAppManager.filterShadowsChanged += OnFilterShadowsChanged;
            BakeryAppManager.boxShadowsChanged += OnBoxShadowsChanged;
            BakeryAppManager.performanceStyleSheetsChanged += RefreshSelection;
            PerfOverlay.shownChanged += OnPerfOverlayChanged;
            AutoPerformance.changed += OnAutoModeChanged;
            RenderResolution.changed += OnResolutionChanged;
            RefreshResolutions();
            RefreshSelection();
        }

        protected override void OnDetached()
        {
            themeChanged -= OnThemeChanged;
            languageChanged -= OnLanguageChanged;
            BakeryAppManager.filterShadowsChanged -= OnFilterShadowsChanged;
            BakeryAppManager.boxShadowsChanged -= OnBoxShadowsChanged;
            BakeryAppManager.performanceStyleSheetsChanged -= RefreshSelection;
            PerfOverlay.shownChanged -= OnPerfOverlayChanged;
            AutoPerformance.changed -= OnAutoModeChanged;
            RenderResolution.changed -= OnResolutionChanged;
        }

        void OnThemeChanged(ThemeStyleSheet theme) => RefreshSelection();
        void OnLanguageChanged(AppLanguage newLanguage)
        {
            RefreshResolutions();
            RefreshSelection();
        }
        void OnFilterShadowsChanged(bool enabled) => filterShadowsToggle?.SetValueWithoutNotify(enabled);
        void OnBoxShadowsChanged(bool enabled) => boxShadowsToggle?.SetValueWithoutNotify(enabled);
        void OnPerfOverlayChanged(bool shown) => perfOverlayToggle?.SetValueWithoutNotify(shown || m_PerfOverlayLocked);

        bool m_PerfOverlayLocked;

        /// <summary>
        /// The Performance overlay toggle shown on and locked, for a view whose own overlay is always shown
        /// (<see cref="PerfOverlay.alwaysShown"/>): the setting itself, for every other app, stays as it is.
        /// </summary>
        public bool perfOverlayLocked
        {
            get => m_PerfOverlayLocked;
            set
            {
                m_PerfOverlayLocked = value;
                perfOverlayToggle?.SetEnabled(!value);
                perfOverlayToggle?.SetValueWithoutNotify(PerfOverlay.shown || value);
            }
        }
        void OnResolutionChanged(int choice) => RefreshSelection();
        void OnAutoModeChanged(bool on) => RefreshSelection();

        /// <summary>
        /// Each resolution segment shows its fraction and the resolution it renders at on this screen ("3/4" over
        /// "1080×2280"). Where the resolution can't change (the Editor, desktop), only Native can be picked.
        /// </summary>
        void RefreshResolutions()
        {
            for (int i = 0; i < resolutionButtons.Length; i++)
            {
                var button = resolutionButtons[i];
                if (button == null)
                    continue;
                var name = RenderResolution.fractions[i] ?? BagelLocalization.GetOr("settings.resolution.native", "Native");
                button.text = name + "\n" + RenderResolution.Format(RenderResolution.SizeAt(i));
                button.SetEnabled(i == 0 || RenderResolution.canChange);
            }
        }

        /// <summary>
        /// The season cards, one per <see cref="themeCollection"/> entry in its order, are in the UXML (each card's
        /// name from BagelStrings' season.* keys): a click picks its season.
        /// </summary>
        void BindSeasonCards()
        {
            if (seasonGrid == null)
                return;
            for (int i = 0; i < seasonGrid.childCount; i++)
            {
                var index = i;
                if (seasonGrid[i] is Button card)
                    card.clicked += () => SelectSeason(index);
            }
        }

        /// <summary>The season's display name in the selected language ("season.maple"... in BagelStrings).</summary>
        public string SeasonName(int index)
        {
            var fallback = themeCollection != null && index >= 0 && index < themeCollection.collection.Count
                ? themeCollection.collection[index].themeName
                : string.Empty;
            return BagelLocalization.GetOr("season." + s_SeasonKeys[Math.Clamp(index, 0, s_SeasonKeys.Length - 1)], fallback);
        }

        public void SelectSeason(int index) => SelectSeason(index, announce: true);

        /// <summary>Switch season; without <paramref name="announce"/>, no strum and no toast (restoring a saved season).</summary>
        public void SelectSeason(int index, bool announce)
        {
            if (themeCollection == null || index < 0 || index >= themeCollection.collection.Count)
                return;

            s_SeasonIndex = index;
            var theme = themeCollection.collection[index].theme;

            // At runtime the panels switch theme; in editors the host applies it (no asset edits).
            if (Application.isPlaying && panelSettingsCollection != null)
                foreach (var settings in panelSettingsCollection.collection)
                    if (settings != null)
                        settings.themeStyleSheet = theme;

            themeChanged?.Invoke(theme);
            if (!announce)
                return;
            // Once the new theme's styles are resolved, so the strum plays on the new season's instrument.
            schedule.Execute(() => BagelSound.Play(this, "season")).ExecuteLater(60);
            Toast(BagelLocalization.Get("toast.season", SeasonName(index), themeCollection.collection[index].japaneseName));
        }

        /// <summary>Switch the Localization locale: every LocalizedString binding updates.</summary>
        public void SetLanguage(AppLanguage newLanguage)
        {
            BagelLocalization.SetLocale(LocaleCode(newLanguage));
            BagelSound.Play(this, "language");
        }

        /// <summary>Shows every setting's current value (after settings change from elsewhere, e.g. a reset).</summary>
        public void RefreshSelection()
        {
            if (seasonGrid != null)
                for (int i = 0; i < seasonGrid.childCount; i++)
                    seasonGrid[i].EnableInClassList(seasonCardSelectedUssClassName, i == s_SeasonIndex);
            englishButton?.EnableInClassList(languageSelectedUssClassName, language == AppLanguage.English);
            japaneseButton?.EnableInClassList(languageSelectedUssClassName, language == AppLanguage.Japanese);
            frenchButton?.EnableInClassList(languageSelectedUssClassName, language == AppLanguage.French);
            soundToggle?.SetValueWithoutNotify(BagelSound.enabled);
            hapticsToggle?.SetValueWithoutNotify(BagelSave.haptics);
            filterShadowsToggle?.SetValueWithoutNotify(BakeryAppManager.filterShadows);
            boxShadowsToggle?.SetValueWithoutNotify(BakeryAppManager.boxShadows);
            noAnimationsToggle?.SetValueWithoutNotify(BakeryAppManager.noAnimations);
            noFiltersToggle?.SetValueWithoutNotify(BakeryAppManager.noFilters);
            noBlurToggle?.SetValueWithoutNotify(BakeryAppManager.noBlur);
            perfOverlayToggle?.SetValueWithoutNotify(PerfOverlay.shown || m_PerfOverlayLocked);
            autoModeToggle?.SetValueWithoutNotify(AutoPerformance.on);
            autoSettings?.SetEnabled(!AutoPerformance.on);
            for (int i = 0; i < resolutionButtons.Length; i++)
                resolutionButtons[i]?.EnableInClassList(resolutionSelectedUssClassName, i == RenderResolution.index);
        }
    }
}
