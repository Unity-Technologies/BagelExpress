using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The toolbar's search: every word typed must be in what it filters. It filters each mode at once: the Game mode's
    /// screens (a screen's name, or one of its parts and their features), the Category and Version lists and their
    /// filter pills (a feature's text, its groups and the screens it's on), and the Art rows. A mode with nothing to
    /// show is grayed out.
    /// </summary>
    public partial class BagelFeaturesWindow
    {
        public static readonly UniqueStyleString modeButtonEmptyUssClassName = new("bgf-modes__button--empty");
        public static readonly UniqueStyleString partMatchUssClassName = new("bgf-part--match");

        [SerializeField] string m_Search = "";
        ToolbarSearchField m_SearchField;
        string[] m_SearchWords = Array.Empty<string>();

        static Dictionary<string, string> s_FeatureText;
        static Dictionary<string, string> s_ArtText;
        static List<string> s_ScreenText;

        public string search => m_Search;
        bool isSearching => m_SearchWords.Length > 0;

        void BindSearch()
        {
            m_SearchWords = Words(m_Search);
            m_SearchField = rootVisualElement.Q<ToolbarSearchField>("search");
            if (m_SearchField == null)
                return;
            m_SearchField.SetValueWithoutNotify(m_Search);
            m_SearchField.RegisterValueChangedCallback(evt => SetSearch(evt.newValue));
            var text = m_SearchField.Q<TextField>();
            if (text != null)
                text.textEdition.placeholder = "Search features, screens, art";
        }

        /// <summary>Filter every mode by these words (empty: show everything).</summary>
        public void SetSearch(string query)
        {
            m_Search = query ?? "";
            m_SearchWords = Words(m_Search);
            if (m_SearchField != null && m_SearchField.value != m_Search)
                m_SearchField.SetValueWithoutNotify(m_Search);
            if (m_Body == null)
                return;
            UpdateModeButtons();
            // The mode shown has nothing: the first one that has something.
            if (CountMatches(m_Mode) == 0)
            {
                foreach (var mode in new[] { Mode.Game, Mode.Category, Mode.Version, Mode.Art })
                {
                    if (CountMatches(mode) > 0)
                    {
                        ShowMode(mode);
                        return;
                    }
                }
            }
            ApplySearch();
        }

        /// <summary>The search on the mode shown: its screens, or its rows (and their group headers and pills).</summary>
        void ApplySearch()
        {
            if (m_Mode == Mode.Game)
            {
                ApplySearchToScreens();
                return;
            }
            ApplyFilter();
            var selected = m_Mode == Mode.Art ? m_Art : m_Feature;
            if (m_Order.Contains(selected))
            {
                // Still shown (a result picked, then the search cleared): it stays picked, and in view.
                if (m_Rows.TryGetValue(selected, out var row))
                    ScrollToRow(row);
                return;
            }
            m_List.scrollOffset = Vector2.zero;
            if (m_Order.Count > 0)
            {
                if (m_Mode == Mode.Art)
                    SelectArt(m_Order[0]);
                else
                    SelectFeature(m_Order[0]);
            }
        }

        /// <summary>How many screens, features or art elements a mode shows for the search.</summary>
        public int CountMatches(Mode mode) => mode switch
        {
            Mode.Game => Enumerable.Range(0, BagelFeatureGuide.screens.Count).Count(ScreenMatches),
            Mode.Art => BagelArtGuide.types.Count(t => ArtMatches(t.id)),
            _ => BagelFeatureGuide.allFeatures.Count(f => FeatureMatches(f.id)),
        };

        void UpdateModeButtons()
        {
            foreach (var (mode, button) in m_ModeButtons)
            {
                var empty = isSearching && CountMatches(mode) == 0;
                button.EnableInClassList(modeButtonEmptyUssClassName, empty);
                button.SetEnabled(!empty);
            }
        }

        /// <summary>A row of the list shown matches the search (a feature, or an art element in the Art mode).</summary>
        bool RowMatches(string id) => m_Mode == Mode.Art ? ArtMatches(id) : FeatureMatches(id);

        public bool FeatureMatches(string featureId) => Matches(FeatureText(featureId));

        public bool ArtMatches(string artId) => Matches(ArtText(artId));

        public bool ScreenMatches(int index) => index >= 0 && index < BagelFeatureGuide.screens.Count && Matches(ScreenText(index));

        // A part by its own name and its feature's (like the screens are searched), not by all of the feature's text.
        bool PartMatches(BagelFeatureGuide.Part part) =>
            isSearching && Matches(string.Join("\n", part.title, part.feature, part.featureInfo?.title).ToLowerInvariant());

        bool Matches(string text) => m_SearchWords.All(word => text.Contains(word));

        static string[] Words(string query) =>
            (query ?? "").ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

        // ------------------------------------------------------------------ the Game mode

        /// <summary>Only the matching screens' icons; off a screen that doesn't match, to the first that does.</summary>
        void ApplySearchToScreens()
        {
            for (int i = 0; i < m_Picker.childCount; i++)
                m_Picker[i].style.display = ScreenMatches(i) ? DisplayStyle.Flex : DisplayStyle.None;
            if (!ScreenMatches(m_Screen))
            {
                var first = StepScreen(-1, 1);
                if (first >= 0)
                {
                    ShowScreen(first);
                    return;
                }
            }
            MarkMatchingParts();
            UpdatePager();
        }

        /// <summary>The next screen that matches the search from <paramref name="from"/>, in a direction; -1 if none.</summary>
        int StepScreen(int from, int direction)
        {
            for (var i = from + direction; i >= 0 && i < BagelFeatureGuide.screens.Count; i += direction)
                if (ScreenMatches(i))
                    return i;
            return -1;
        }

        /// <summary>The screen's parts that match the search light up, and the first comes into view.</summary>
        void MarkMatchingParts()
        {
            if (m_Mode != Mode.Game || m_Screen < 0 || m_Screen >= BagelFeatureGuide.screens.Count)
                return;
            var parts = BagelFeatureGuide.screens[m_Screen].parts;
            VisualElement first = null;
            for (int i = 0; i < m_PartCards.Count && i < parts.Length; i++)
            {
                var match = PartMatches(parts[i]);
                m_PartCards[i].EnableInClassList(partMatchUssClassName, match);
                if (match)
                    first ??= m_PartCards[i];
            }
            if (first != null)
                m_DetailsScroll.schedule.Execute(() =>
                {
                    if (first.panel != null)
                        m_DetailsScroll.ScrollTo(first);
                });
        }

        // ------------------------------------------------------------------ what each thing is searched by

        static string FeatureText(string featureId)
        {
            if (s_FeatureText == null)
            {
                s_FeatureText = new Dictionary<string, string>();
                foreach (var feature in BagelFeatureGuide.allFeatures)
                {
                    var screens = BagelFeatureGuide.PartsOf(feature.id).SelectMany(p => new[] { p.screen.screen.title, p.screen.screen.japaneseTitle, p.part.title });
                    s_FeatureText[feature.id] = string.Join("\n", new[]
                        {
                            feature.searchText, BagelFeatureGuide.CategoryOf(feature).title, BagelFeatureGuide.VersionOf(feature).title,
                            feature.isUsed ? "in bagel" : "not in bagel",
                        }.Concat(screens)).ToLowerInvariant();
                }
            }
            return s_FeatureText.TryGetValue(featureId ?? "", out var text) ? text : "";
        }

        static string ScreenText(int index)
        {
            s_ScreenText ??= BagelFeatureGuide.screens.Select(s => string.Join("\n",
                new[] { s.screen.id, s.screen.title, s.screen.japaneseTitle }
                    .Concat(s.parts.SelectMany(p => new[] { p.title, p.feature, p.featureInfo?.title })))
                .ToLowerInvariant()).ToList();
            return s_ScreenText[index];
        }

        static string ArtText(string artId)
        {
            s_ArtText ??= BagelArtGuide.types.ToDictionary(t => t.id, t => string.Join("\n",
                new[] { t.id, t.title, t.drawnWith, t.about, t.uxmlTag, t.script }
                    .Concat(t.uses.SelectMany(u => new[] { u.title, u.screen.title }))).ToLowerInvariant());
            return s_ArtText.TryGetValue(artId ?? "", out var text) ? text : "";
        }
    }
}
