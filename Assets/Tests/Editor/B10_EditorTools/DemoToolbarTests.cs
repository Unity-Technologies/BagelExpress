using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Bagel.B10_EditorTools
{
    /// <summary>
    /// The main toolbar's demo strips (<see cref="DemoToolbar"/>): an icon per choice, "as authored" first for the screen
    /// and the season, and the one chosen lit.
    /// </summary>
    public class DemoToolbarTests
    {
        [UnityTest]
        public IEnumerator EachStripHasAnIconPerChoiceAndLightsTheChosenOne()
        {
            var (screen, season, language) = (DemoOverrides.screen, DemoOverrides.season, DemoOverrides.language);
            var locale = BagelLocalization.localeCode;
            Assert.That(DemoToolbar.Show(), Is.True, "the strips are on the main toolbar");
            try
            {
                DemoOverrides.screen = GameScreen.shops;
                DemoOverrides.season = 1;
                DemoOverrides.language = "ja";
                yield return null;
                yield return null;

                AssertStrip(DemoToolbar.screenPath, GameScreen.all.Length + 1, Array.IndexOf(GameScreen.all, GameScreen.shops) + 1);
                AssertStrip(DemoToolbar.seasonPath, DemoOverrides.themes.collection.Count + 1, 2);
                AssertStrip(DemoToolbar.languagePath, BagelPickerIcons.languages.Length, BagelPickerIcons.LanguageIndex("ja"));

                DemoOverrides.ResetToAuthored();
                yield return null;
                yield return null;
                AssertStrip(DemoToolbar.screenPath, GameScreen.all.Length + 1, 0);
                AssertStrip(DemoToolbar.seasonPath, DemoOverrides.themes.collection.Count + 1, 0);
            }
            finally
            {
                if (string.IsNullOrEmpty(language))
                    DemoOverrides.ResetToAuthored();
                else
                    DemoOverrides.language = language;
                (DemoOverrides.screen, DemoOverrides.season) = (screen, season);
                BagelLocalization.SetLocale(locale);
            }
        }

        [Test]
        public void TheDemoPresetPutsTheScreenOnTheLeftAndTheSeasonAndLanguageNextToSearch()
        {
            var text = System.IO.File.ReadAllText("Assets/Settings/Layouts/BagelExpressToolbar.preset");
            var entries = Regex.Matches(text, @"- dockPosition: (\d+)\s+containerId: \S+\s+displayed: (\d)\s+id: (.+?)\s+index: (\d+)")
                .Select(m => (dock: (MainToolbarDockPosition)int.Parse(m.Groups[1].Value), shown: m.Groups[2].Value == "1",
                    id: m.Groups[3].Value.Trim(), index: int.Parse(m.Groups[4].Value)))
                .ToList();
            (MainToolbarDockPosition dock, bool shown, string id, int index) Entry(string id) => entries.Single(e => e.id == id);

            var screen = Entry(DemoToolbar.screenPath);
            var search = Entry("Editor Controls/Search");
            var season = Entry(DemoToolbar.seasonPath);
            var language = Entry(DemoToolbar.languagePath);
            Assert.That((screen.dock, screen.shown), Is.EqualTo((MainToolbarDockPosition.Left, true)), "the screen strip on the left");
            Assert.That((search.dock, search.shown), Is.EqualTo((MainToolbarDockPosition.Right, true)));
            // The right zone runs from right to left: Search, then the language, then the season.
            Assert.That((language.dock, language.shown, language.index), Is.EqualTo((MainToolbarDockPosition.Right, true, search.index + 1)), "the language next to Search");
            Assert.That((season.dock, season.shown, season.index), Is.EqualTo((MainToolbarDockPosition.Right, true, search.index + 2)), "the season next to the language");
        }

        static void AssertStrip(string path, int count, int selected)
        {
            var options = Options(path);
            Assert.That(options.Count, Is.EqualTo(count), path);
            for (int i = 0; i < options.Count; i++)
                Assert.That(options[i].ClassListContains(BagelOptionStrip.selectedOptionUssClassName), Is.EqualTo(i == selected), $"{path}: option {i}");
            Assert.That(options.All(o => !string.IsNullOrEmpty(o.tooltip)), Is.True, $"{path}: every icon says what it is");
        }

        // The strip's options, in the main toolbar's element for it (named after its path, without spaces).
        static List<VisualElement> Options(string path)
        {
            var window = typeof(EditorWindow).Assembly.GetType("UnityEditor.MainToolbarWindow")?
                .GetField("instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null) as EditorWindow;
            Assert.That(window, Is.Not.Null, "the main toolbar");
            var name = path.Replace(" ", string.Empty);
            var overlay = Find(window.rootVisualElement.panel.visualTree, e => e.name == name && e.ClassListContains("unity-overlay"));
            Assert.That(overlay, Is.Not.Null, path);
            var options = new List<VisualElement>();
            Collect(overlay, e => e.ClassListContains(BagelOptionStrip.optionUssClassName), options);
            return options;
        }

        static VisualElement Find(VisualElement element, Func<VisualElement, bool> match)
        {
            if (match(element))
                return element;
            foreach (var child in element.hierarchy.Children())
            {
                var found = Find(child, match);
                if (found != null)
                    return found;
            }
            return null;
        }

        static void Collect(VisualElement element, Func<VisualElement, bool> match, List<VisualElement> found)
        {
            if (match(element))
                found.Add(element);
            foreach (var child in element.hierarchy.Children())
                Collect(child, match, found);
        }
    }
}
