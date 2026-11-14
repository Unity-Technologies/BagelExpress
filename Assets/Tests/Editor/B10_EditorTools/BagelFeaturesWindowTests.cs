using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B10_EditorTools
{
    /// <summary>
    /// The Bagel Features window's content stays true to the project: every feature has a group and a snippet, every
    /// part of a screen is found where it says, and the code lines it opens are still there.
    /// </summary>
    public class BagelFeatureGuideTests
    {
        // This folder is published on its own: no document outside it (*.md), no absolute paths, no issue-tracker IDs.
        static readonly System.Text.RegularExpressions.Regex s_PointsOutside =
            new(@"\b\w+\.md\b|(^|[\s(""'])/(Users|home|opt|tmp)/|\b[A-Z]{3,}-\d{4,}\b");

        [Test]
        public void EveryFeatureHasACategoryAndAVersion()
        {
            foreach (var feature in BagelFeatureGuide.allFeatures)
            {
                Assert.That(BagelFeatureGuide.categoryOf.ContainsKey(feature.id), Is.True, $"{feature.id} has no category in BagelFeatureGuide.Categories.cs");
                Assert.That(BagelFeatureGuide.versions, Does.Contain(BagelFeatureGuide.VersionOf(feature)), feature.id);
            }
            foreach (var (id, category) in BagelFeatureGuide.categoryOf)
            {
                Assert.That(BagelFeatureCatalog.Get(id), Is.Not.Null, $"{id} isn't a feature");
                Assert.That(BagelFeatureGuide.categories.Any(c => c.id == category), Is.True, $"{id}: no category {category}");
            }
        }

        [Test]
        public void TheUnusedFeaturesAreOnlyListed()
        {
            var used = BagelFeatureCatalog.features.Select(f => f.id).ToList();
            foreach (var feature in BagelFeatureCatalog.unused)
            {
                Assert.That(feature.isUsed, Is.False, feature.id);
                Assert.That(used, Does.Not.Contain(feature.id), $"{feature.id} is also a used feature");
                Assert.That(BagelFeatureCatalog.Get(feature.id), Is.SameAs(feature), feature.id);
                Assert.That(feature.about.Length, Is.InRange(20, 160), $"{feature.id}: one short line");
                Assert.That(feature.summary.Length, Is.InRange(20, 120), $"{feature.id}: what you'd use it for, in a line");
                Assert.That(BagelFeatureGuide.PartsOf(feature.id), Is.Empty, $"{feature.id} has a part on a screen");
                Assert.That(feature.sources, Is.Empty, feature.id);
            }
            Assert.That(BagelFeatureCatalog.features.All(f => f.isUsed), Is.True);
        }

        [Test]
        public void EveryFeatureHasShortSnippets()
        {
            BagelFeatureGuide.ReloadSnippets();
            foreach (var feature in BagelFeatureCatalog.features)
                Assert.That(BagelFeatureGuide.SnippetsOf(feature.id), Is.Not.Empty, $"{feature.id} has no snippet in BagelFeatureSnippets.txt");
            foreach (var snippet in BagelFeatureGuide.allSnippets)
            {
                var where = $"{snippet.feature} › {snippet.caption}";
                Assert.That(BagelFeatureCatalog.Get(snippet.feature), Is.Not.Null, $"{where}: not a feature");
                Assert.That(snippet.caption, Is.Not.Empty, where);
                var lines = snippet.code.Split('\n');
                Assert.That(lines.Length, Is.InRange(1, 12), $"{where}: keep it short");
                Assert.That(lines.Max(l => l.Length), Is.LessThanOrEqualTo(58), $"{where}: a line is too wide for the window");
                Assert.That(snippet.code, Does.Not.Contain("\t"), where);
            }
        }

        [Test]
        public void EveryPartIsWhereItSays()
        {
            var app = BagelFeatureGuide.appInstance;
            foreach (var screen in BagelFeatureGuide.screens)
            {
                Assert.That(screen.parts, Is.Not.Empty, screen.screen.id);
                foreach (var part in screen.parts)
                {
                    var where = $"{screen.screen.id}: {part.title} ({part.selector})";
                    Assert.That(part.featureInfo, Is.Not.Null, $"{where}: no feature {part.feature}");
                    Assert.That(part.title, Is.Not.Empty, where);
                    Assert.That(part.how.Length, Is.InRange(20, 110), $"{where}: one short sentence");
                    Assert.That(GameScreen.Find(app, part.sceneSelector), Is.Not.Null, $"{where}: {part.sceneSelector} isn't in the app's UXML (give it a host)");
                    Assert.That(BagelFeatureGuide.UxmlOf(part), Is.Not.Null, $"{where}: no UXML found");
                    Assert.That(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelFeatureGuide.UxmlOf(part)), Is.Not.Null, where);
                    if (part.code != null)
                        Assert.That(EditorLink.FindLine(part.code.path, part.code.find), Is.GreaterThan(0), $"{where}: {part.code.path} › \"{part.code.find}\" is gone");
                }
            }
        }

        [Test]
        public void EveryUssAndCodeLinkOpensALine()
        {
            foreach (var screen in BagelFeatureGuide.screens)
                foreach (var part in screen.parts)
                {
                    var uss = BagelFeatureGuide.UssOf(part);
                    if (uss != null)
                        Assert.That(EditorLink.FindLine(uss.path, uss.find), Is.GreaterThan(0), $"{screen.screen.id}: {part.title}");
                    var code = BagelFeatureGuide.CodeOf(part);
                    if (code != null)
                        Assert.That(EditorLink.FindLine(code.path, code.find), Is.GreaterThan(0), $"{screen.screen.id}: {part.title}");
                }
        }

        [Test]
        public void NothingPointsOutsideTheProject()
        {
            foreach (var screen in BagelFeatureGuide.screens)
                foreach (var part in screen.parts)
                    foreach (var text in new[] { part.title, part.how })
                        Assert.That(s_PointsOutside.IsMatch(text), Is.False, $"{screen.screen.id}: {text}");
            foreach (var snippet in BagelFeatureGuide.allSnippets)
                foreach (var text in new[] { snippet.caption, snippet.code })
                    Assert.That(s_PointsOutside.IsMatch(text), Is.False, $"{snippet.feature}: {text}");
            foreach (var group in BagelFeatureGuide.versions.Concat(BagelFeatureGuide.categories))
                Assert.That(s_PointsOutside.IsMatch(group.title + " " + group.subtitle), Is.False, group.id);
        }

        [Test]
        public void EveryArtElementIsDescribedByWhatItReallyHas()
        {
            Assert.That(BagelArtGuide.types.Count, Is.EqualTo(14));
            Assert.That(BagelArtGuide.types.Select(t => t.id).Distinct().Count(), Is.EqualTo(BagelArtGuide.types.Count), "unique ids");
            foreach (var type in BagelArtGuide.types)
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(type.script), Is.Not.Null, $"{type.id}: {type.script}");
                Assert.That(type.controls.Select(c => c.name).Distinct().Count(), Is.EqualTo(type.controls.Length), $"{type.id}: unique controls");
                Assert.That(type.row.Length, Is.InRange(1, 7), $"{type.id}: a few variations in its row");
                Assert.That(type.variations.Length, Is.GreaterThan(0), $"{type.id}: what the game uses");
                Assert.That(s_PointsOutside.IsMatch(type.about), Is.False, type.id);
                Assert.That(type.about.Length, Is.InRange(20, 110), $"{type.id}: one short sentence");
                foreach (var variation in type.row.Concat(type.variations))
                    foreach (var (name, value) in variation.values)
                    {
                        var control = type.Get(name);
                        Assert.That(control, Is.Not.Null, $"{type.id}: a variation sets {name}, which no control has");
                        if (control.type == BagelArtGuide.ValueType.Choice && control.target != BagelArtGuide.Target.CustomProperty)
                            Assert.That(control.choices, Does.Contain(value), $"{type.id}: {name}");
                    }

                // Every attribute control is one the element's UXML really has.
                var element = type.create();
                var uxmlNames = element.GetType().GetProperties()
                    .Where(p => p.GetCustomAttributes(typeof(UxmlAttributeAttribute), true).Length > 0)
                    .Select(p => ToUxmlName(p.Name)).ToList();
                foreach (var control in type.controls.Where(c => c.target == BagelArtGuide.Target.Attribute))
                    Assert.That(uxmlNames, Does.Contain(control.name), $"{type.id}: {control.name}");
            }
        }

        [Test]
        public void EveryArtUseIsWhereItSays()
        {
            foreach (var type in BagelArtGuide.types)
            {
                Assert.That(type.uses.Length, Is.GreaterThan(0), type.id);
                foreach (var use in type.uses)
                {
                    var screen = BagelFeatureGuide.screens.FirstOrDefault(s => s.screen == use.screen);
                    Assert.That(screen, Is.Not.Null, $"{type.id}: {use.title}");
                    if (use.part != null)
                        Assert.That(screen.parts.Any(p => p.selector == use.part), Is.True, $"{type.id}: no part {use.part} on {use.screen.id}");
                    if (use.uxml != null)
                        Assert.That(System.IO.File.ReadAllText(use.uxml), Does.Contain($"name=\"{use.element}\""), $"{type.id}: {use.element} in {use.uxml}");
                }
            }
        }

        static string ToUxmlName(string property) =>
            string.Concat(property.Select((ch, i) => char.IsUpper(ch) ? (i > 0 ? "-" : "") + char.ToLowerInvariant(ch) : ch.ToString()));
    }

    /// <summary>The Bagel Features window: a grouped list with few live samples, or one live screen at a time.</summary>
    public class BagelFeaturesWindowTests : EditorWindowUITestFixture<BagelFeaturesWindow>
    {
        const int k_MaxFrames = 30;

        public BagelFeaturesWindowTests()
        {
            debugMode = true;
        }

        [Test]
        public void TheListRunsOnlyTheSamplesInView()
        {
            panelSize = new Vector2(802f, 829f);
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();

            Assert.That(window.rows.Count, Is.EqualTo(BagelFeatureGuide.allFeatures.Count()), "a row per feature, used or not");
            Assert.That(window.phone, Is.Null, "no game screen in the list modes");
            Assert.That(window.liveSampleCount, Is.GreaterThan(0).And.LessThan(window.rows.Count / 2), "only the rows in view (and around) run their sample");
            Assert.That(GroupHeaders().Count, Is.EqualTo(BagelFeatureGuide.categories.Count));

            window.ShowMode(BagelFeaturesWindow.Mode.Version);
            simulate.FrameUpdate();
            Assert.That(GroupHeaders().Count, Is.EqualTo(BagelFeatureGuide.versions.Count));
            Assert.That(window.rows.Count, Is.EqualTo(BagelFeatureGuide.allFeatures.Count()));
        }

        // The list's group headers (not the pinned copy over the list).
        System.Collections.Generic.List<VisualElement> GroupHeaders() =>
            rootVisualElement.Query(className: "bgf-group").Where(e => !e.ClassListContains("bgf-group--sticky")).ToList();

        [Test]
        public void TheFilterShowsOneGroupOrAll()
        {
            panelSize = new Vector2(802f, 829f);
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SetFilter("");
            simulate.FrameUpdate();
            var pills = rootVisualElement.Query(className: BagelFeaturesWindow.filterPillUssClassName).ToList();
            Assert.That(pills.Count, Is.EqualTo(BagelFeatureGuide.categories.Count + 1), "All, then a pill per category");

            window.SetFilter("effects");
            simulate.FrameUpdate();
            var effects = BagelFeatureGuide.allFeatures.Where(f => BagelFeatureGuide.CategoryOf(f).id == "effects").Select(f => f.id).ToList();
            Assert.That(window.shownFeatures, Is.EqualTo(effects));
            Assert.That(window.rows.Values.Count(r => r.resolvedStyle.display == DisplayStyle.Flex), Is.EqualTo(effects.Count));
            Assert.That(effects, Does.Contain(window.selectedFeature), "the selection moves into the group");
            Assert.That(rootVisualElement.Q("filter-effects").ClassListContains(BagelFeaturesWindow.filterPillSelectedUssClassName), Is.True);
            Assert.That(window.rows.Where(r => !effects.Contains(r.Key)).All(r => (r.Value.Q("sample")?.childCount ?? 0) == 0), Is.True, "hidden rows run no sample");

            // A part's feature chip reaches a feature the filter hides: the filter shows them all again.
            window.ShowFeature("grid-layout");
            Assert.That(window.filter, Is.Empty);
            Assert.That(window.shownFeatures, Does.Contain("grid-layout"));

            window.ShowMode(BagelFeaturesWindow.Mode.Version);
            window.SetFilter("7.x");
            simulate.FrameUpdate();
            Assert.That(rootVisualElement.Query(className: BagelFeaturesWindow.filterPillUssClassName).ToList().Count, Is.EqualTo(BagelFeatureGuide.versions.Count + 1));
            Assert.That(window.shownFeatures, Is.EqualTo(BagelFeatureGuide.allFeatures.Where(f => f.isExperimental).Select(f => f.id).ToList()));
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            Assert.That(window.filter, Is.Empty, "each list keeps its own filter");
            window.ShowMode(BagelFeaturesWindow.Mode.Version);
            Assert.That(window.filter, Is.EqualTo("7.x"));
            window.SetFilter("");
        }

        [Test]
        public void SelectingAFeatureShowsItsPartsAndSnippets()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SelectFeature("backdrop-filter");
            simulate.FrameUpdate();

            Assert.That(window.selectedFeature, Is.EqualTo("backdrop-filter"));
            Assert.That(window.rows["backdrop-filter"].ClassListContains(BagelFeaturesWindow.rowSelectedUssClassName), Is.True);
            Assert.That(window.rows["gap"].ClassListContains(BagelFeaturesWindow.rowSelectedUssClassName), Is.False);
            Assert.That(window.details.Query(className: BagelFeaturesWindow.partUssClassName).ToList().Count, Is.EqualTo(BagelFeatureGuide.PartsOf("backdrop-filter").Count()));
            Assert.That(window.details.Query(className: "bgf-snippet").ToList().Count, Is.EqualTo(BagelFeatureGuide.SnippetsOf("backdrop-filter").Count));
        }

        [Test]
        public void AFeatureTheGameDoesntUseSaysSo()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Version);
            window.SetFilter("");
            simulate.FrameUpdate();
            foreach (var feature in BagelFeatureCatalog.unused)
            {
                var row = window.rows[feature.id];
                Assert.That(row.ClassListContains(BagelFeaturesWindow.rowUnusedUssClassName), Is.True, feature.id);
                Assert.That(row.Q(className: "bgf-chip--unused"), Is.Not.Null, $"{feature.id}: its row says it isn't used");
            }
            Assert.That(window.rows["backdrop-filter"].Q(className: "bgf-chip--used"), Is.Not.Null, "a used feature's row says so");

            // What it looks like, what it is and what you'd use it for, and how: no parts of the game.
            window.SelectFeature("curved-ui");
            simulate.FrameUpdate();
            Assert.That(window.details.Q("not-used"), Is.Not.Null);
            Assert.That(window.previewContent, Is.Not.Null, "a picture of it");
            Assert.That(window.details.Query(className: "bgf-snippet").ToList().Count, Is.EqualTo(BagelFeatureGuide.SnippetsOf("curved-ui").Count));
            Assert.That(window.details.Query(className: BagelFeaturesWindow.partUssClassName).ToList(), Is.Empty);
            window.SetFilter("");
        }

        [Test]
        public void TheSearchFiltersEveryMode()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SetSearch("VisualElementReference");
            simulate.FrameUpdate();

            // The Art mode has nothing for it: the window moves to the first mode that has, and grays the Art button.
            Assert.That(window.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Game));
            var art = rootVisualElement.Q<Button>("mode-art");
            Assert.That(art.ClassListContains(BagelFeaturesWindow.modeButtonEmptyUssClassName), Is.True);
            Assert.That(art.enabledSelf, Is.False);
            var tutorial = BagelFeatureGuide.screens.ToList().FindIndex(s => s.screen == GameScreen.tutorial);
            Assert.That(window.screenIndex, Is.EqualTo(tutorial), "the only screen with a part that uses it");
            var picks = rootVisualElement.Query(className: BagelFeaturesWindow.screenPickUssClassName).ToList();
            Assert.That(picks.Count(p => p.resolvedStyle.display == DisplayStyle.Flex), Is.EqualTo(1));
            Assert.That(window.partCards.Count(c => c.ClassListContains(BagelFeaturesWindow.partMatchUssClassName)), Is.EqualTo(1));

            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            simulate.FrameUpdate();
            Assert.That(window.shownFeatures, Is.EqualTo(new[] { "visualelementreference" }));
            Assert.That(window.selectedFeature, Is.EqualTo("visualelementreference"));
            var pills = rootVisualElement.Query(className: BagelFeaturesWindow.filterPillUssClassName).ToList();
            Assert.That(pills.Count(p => p.resolvedStyle.display == DisplayStyle.Flex), Is.EqualTo(2), "All, and the one group that has it");

            // A screen's name finds the screen, and the features used on it.
            window.SetSearch("recipe book");
            simulate.FrameUpdate();
            Assert.That(window.shownFeatures, Does.Contain("grid-layout"));
            Assert.That(window.CountMatches(BagelFeaturesWindow.Mode.Game), Is.GreaterThanOrEqualTo(1));

            window.SetSearch("");
            simulate.FrameUpdate();
            Assert.That(window.shownFeatures.Count, Is.EqualTo(BagelFeatureGuide.allFeatures.Count()));
            Assert.That(art.enabledSelf, Is.True);
        }

        [Test]
        public void EveryFeatureHasALivePreviewAndItsUsesLiveOnTheirOwn()
        {
            window.SetSearch("");
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SetFilter("");
            foreach (var feature in BagelFeatureGuide.allFeatures)
            {
                window.SelectFeature(feature.id);
                window.BuildUsesNow();
                Assert.That(window.previewContent, Is.Not.Null, $"{feature.id}: its sample, its thumbnail or its Editor window");
                if (!feature.isUsed)
                    continue;
                Assert.That(window.uses.Count, Is.EqualTo(BagelFeatureGuide.PartsOf(feature.id).Count()), feature.id);
                foreach (var use in window.uses)
                {
                    Assert.That(use.isBuilt, Is.True, $"{feature.id}: {use.part.title} ({use.part.selector}) on its own");
                    // Stand-ins only around it: none of the game's managers run there.
                    Assert.That(use.Query<BakeryAppManager>().ToList(), Is.Empty, $"{feature.id}: {use.part.title}");
                    Assert.That(use.Query<BakeryPane>().ToList(), Is.Empty, $"{feature.id}: {use.part.title}");
                }
            }
        }

        [Test]
        public void APreviewsControlsChangeItsSample()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SetFilter("");

            window.SelectFeature("gap");
            window.SetPreviewValue("column-gap", 30f);
            Assert.That(window.previewContent.Q(className: "bgfs-gap").style.columnGap.value.value, Is.EqualTo(30f));
            Assert.That(window.details.Q("field-column-gap"), Is.Not.Null, "its control, under the stage");

            // A custom property: in a style sheet on the sample, read by var(--be-tile-lip).
            window.SelectFeature("geometry-shadows");
            window.SetPreviewValue("--be-tile-lip", 12f);
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            Assert.That(window.previewContent.Q(className: "be-card").resolvedStyle.borderBottomWidth, Is.EqualTo(12f));

            // An inline style that several controls make together: box-shadow, and its drop-shadow() twin.
            window.SelectFeature("box-shadow");
            window.SetPreviewValue("spread", 6f);
            window.SetPreviewValue("blur", 20f);
            var shadow = window.previewContent.Q(className: "bgfs-box-shadow").style.boxShadow.value;
            Assert.That(shadow.spreadRadius, Is.EqualTo(6f));
            Assert.That(shadow.blurRadius, Is.EqualTo(20f));
            Assert.That(window.previewContent.Q(className: "bgfs-drop-twin").style.filter.value[0].GetParameter(2).floatValue, Is.EqualTo(10f),
                "drop-shadow()'s sigma is half the blur radius");

            // A binding whose source is the control itself.
            window.SelectFeature("csharp-bindings");
            var slider = window.details.Q<Slider>("field-source");
            slider.value = 0.25f;
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            Assert.That(window.previewContent.Q<MeterBar>("bound-meter").value, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void TheHalftoneFilterIsInTheArtTabWithItsParameters()
        {
            window.ShowArt("manga-halftone");
            simulate.FrameUpdate();
            Assert.That(window.selectedArt, Is.EqualTo("manga-halftone"));
            window.SetArtValue("spacing", 12f);
            var filter = window.artPreview.style.filter.value;
            Assert.That(filter.Count, Is.EqualTo(1));
            Assert.That(filter[0].customDefinition, Is.Not.Null, "the custom filter's definition");

            window.SetArtValue("as", "backdrop-filter");
            Assert.That(window.artPreview.style.filter.value, Is.Empty, "off the art");
            Assert.That(window.artPreview.Q("pane").style.backdropFilter.value.Count, Is.EqualTo(1), "on the pane over it");
            Assert.That(window.details.Q("art-markup").Query<Label>().ToList().Any(l => l.text.Contains("backdrop-filter")), Is.True, "its USS says so");
        }

        [Test]
        public void AResultPickedThenTheSearchClearedStaysPicked()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SetFilter("");
            window.SetSearch("smart");
            window.SelectFeature("smart-strings");
            window.SetSearch("");
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            Assert.That(window.selectedFeature, Is.EqualTo("smart-strings"));
            Assert.That(window.shownFeatures.Count, Is.EqualTo(BagelFeatureGuide.allFeatures.Count()));
            // And in view, below the pinned group header.
            var list = rootVisualElement.Q<ScrollView>("list");
            var row = window.rows["smart-strings"];
            Assert.That(row.layout.yMin, Is.GreaterThanOrEqualTo(list.scrollOffset.y));
            Assert.That(row.layout.yMax, Is.LessThanOrEqualTo(list.scrollOffset.y + list.contentViewport.layout.height));
        }

        [Test]
        public void ScaledGameUIRendersTextAsSdfAndEditorFeaturesShowEditorWindows()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SetFilter("");
            window.SelectFeature("smart-strings");
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            // The Editor's bitmap glyphs blur when scaled: the game UI's text is SDF, as in the game.
            foreach (var label in window.previewContent.Query<Label>().ToList())
                Assert.That(label.resolvedStyle.unityEditorTextRenderingMode, Is.EqualTo(EditorTextRenderingMode.SDF), label.text);

            window.SelectFeature("in-scene-authoring");
            simulate.FrameUpdate();
            Assert.That(window.previewContent.ClassListContains(BagelEditorMockups.ussClassName), Is.True, "a small Hierarchy window");
            Assert.That(window.previewContent.styleSheets.count, Is.GreaterThan(0), "in the Editor's theme");
        }

        [Test]
        public void GameModeRunsOneScreenAtATime()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Game);
            window.ShowScreen(0);
            simulate.FrameUpdate();
            var first = window.phone;
            Assert.That(first, Is.Not.Null);
            Assert.That(window.rows.Count, Is.Zero, "the list is unloaded");
            Assert.That(window.partCards.Count, Is.EqualTo(BagelFeatureGuide.screens[0].parts.Length));
            Assert.That(first.hotspots.Count, Is.EqualTo(BagelFeatureGuide.screens[0].parts.Length), "a dot per part");
            Assert.That(first.app.state, Is.SameAs(window.state));

            var picks = rootVisualElement.Query(className: BagelFeaturesWindow.screenPickUssClassName).ToList();
            Assert.That(picks.Count, Is.EqualTo(BagelFeatureGuide.screens.Count), "an icon per screen, above the phone");
            Assert.That(picks[0].ClassListContains(BagelFeaturesWindow.screenPickCurrentUssClassName), Is.True);

            window.ShowScreen(1);
            simulate.FrameUpdate();
            Assert.That(picks[1].ClassListContains(BagelFeaturesWindow.screenPickCurrentUssClassName), Is.True, "the current screen's icon glows");
            Assert.That(picks[0].ClassListContains(BagelFeaturesWindow.screenPickCurrentUssClassName), Is.False);
            Assert.That(window.phone, Is.Not.SameAs(first));
            Assert.That(first.panel, Is.Null, "the screen left behind is unloaded");
            Assert.That(rootVisualElement.Query<PhoneFrame>().ToList().Count, Is.EqualTo(1), "one live screen");

            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            Assert.That(window.phone, Is.Null, "leaving the screens unloads the last one");
            Assert.That(rootVisualElement.Query(className: BagelFeaturesWindow.screenPickUssClassName).ToList(), Is.Empty, "and their icons");
        }

        [Test]
        public void ClicksDontLeaveThePinnedScreen()
        {
            var levelUp = BagelFeatureGuide.screens.ToList().FindIndex(s => s.screen == GameScreen.levelUp);
            window.ShowMode(BagelFeaturesWindow.Mode.Game);
            window.ShowScreen(levelUp);
            simulate.FrameUpdateMs(700); // the set-up opens LEVEL UP!, then the screen is pinned
            var app = window.phone.app;
            var dialog = app.Q<LevelUpDialog>();
            Assert.That(app.pinned, Is.True);
            Assert.That(dialog.isOpen, Is.True);

            simulate.Click(dialog.okButton);
            simulate.FrameUpdateMs(700);
            Assert.That(dialog.isOpen, Is.True, "AWESOME! doesn't close LEVEL UP!, so its dots keep their parts");

            window.ShowScreen(BagelFeatureGuide.screens.ToList().FindIndex(s => s.screen == GameScreen.home));
            simulate.FrameUpdateMs(700);
            simulate.Click(window.phone.app.GetNavButton(AppPage.Shops));
            simulate.FrameUpdateMs(500);
            Assert.That(window.phone.app.currentPage, Is.EqualTo(AppPage.Home), "the nav doesn't leave Home");
        }

        [Test]
        public void EveryPartFindsItsElementOnTheLiveScreen()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Game);
            for (int s = 0; s < BagelFeatureGuide.screens.Count; s++)
            {
                window.ShowScreen(s);
                var phone = window.phone;
                if (phone.screen.animatedSetUp)
                    continue; // its elements appear as the reveal plays
                // Lists and set-ups (dialogs, the tutorial) make their elements a few panel updates later.
                for (int i = 0; i < k_MaxFrames && phone.hotspots.Any(h => phone.FindTarget(h) == null); i++)
                    simulate.FrameUpdate();
                foreach (var spot in phone.hotspots)
                    Assert.That(phone.FindTarget(spot), Is.Not.Null, $"{phone.screen.id}: {spot.tooltip} ({spot.selector})");
            }
        }

        [Test]
        public void APartsFeatureChipAndShowOnTheScreenCrossOver()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Game);
            window.ShowFeature("drop-shadow");
            Assert.That(window.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Category));
            Assert.That(window.selectedFeature, Is.EqualTo("drop-shadow"));

            var (screen, part) = BagelFeatureGuide.PartsOf("drop-shadow").First();
            var screenIndex = BagelFeatureGuide.screens.ToList().IndexOf(screen);
            window.ShowPart(screenIndex, System.Array.IndexOf(screen.parts, part));
            Assert.That(window.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Game));
            Assert.That(window.screenIndex, Is.EqualTo(screenIndex));
            Assert.That(window.phone.screen, Is.SameAs(screen.screen));
        }

        [Test]
        public void ArtModeRunsOnlyTheRowsInViewAndOnePreview()
        {
            panelSize = new Vector2(802f, 829f);
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();

            Assert.That(window.rows.Count, Is.EqualTo(BagelArtGuide.types.Count), "a row per art element");
            Assert.That(window.phone, Is.Null);
            Assert.That(window.liveSampleCount, Is.GreaterThan(0).And.LessThan(window.rows.Count), "only the rows in view hold their art");
            Assert.That(rootVisualElement.Q("list-filter").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "no filter");
            Assert.That(window.artPreview, Is.Not.Null);
            Assert.That(window.artStage.ClassListContains(ArtKit.animateInEditModeUssClassName), Is.True, "the preview moves");
            Assert.That(window.artGallery.Count, Is.EqualTo(BagelArtGuide.Get(window.selectedArt).variations.Length));

            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            Assert.That(window.artPreview, Is.Null, "the art is unloaded");
            Assert.That(window.artGallery.Count, Is.Zero);
        }

        [Test]
        public void EveryArtElementShowsWithItsControlsAndGallery()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            foreach (var type in BagelArtGuide.types)
            {
                window.SelectArt(type.id);
                simulate.FrameUpdate();
                Assert.That(window.selectedArt, Is.EqualTo(type.id));
                Assert.That(window.rows[type.id].ClassListContains(BagelFeaturesWindow.rowSelectedUssClassName), Is.True);
                Assert.That(window.artPreview.panel, Is.Not.Null, type.id);
                Assert.That(window.details.Query(className: "bgf-art-field").ToList().Count, Is.EqualTo(type.controls.Length), type.id);
                Assert.That(window.artGallery.Count, Is.EqualTo(type.variations.Length), type.id);
                Assert.That(window.details.Query(className: "bgf-snippet").ToList().Count, Is.EqualTo(2), $"{type.id}: its UXML and USS");
            }
        }

        [Test]
        public void ArtControlsChangeThePreviewAndItsMarkup()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SelectArt("pastry-icon");
            simulate.FrameUpdate();
            var pastry = (PastryIcon)window.artPreview;

            window.SetArtValue("kind", "Taiyaki");
            window.SetArtValue("size", 90f);
            window.SetArtValue("--be-ink", Color.red);
            simulate.FrameUpdate();
            Assert.That(pastry.kind, Is.EqualTo(PastryKind.Taiyaki));
            Assert.That(pastry.style.width.value.value, Is.EqualTo(90f));
            Assert.That(pastry.customStyle.TryGetValue(new CustomStyleProperty<Color>("--be-ink"), out var ink), Is.True, "a custom property, through a style sheet made for it");
            Assert.That(ink, Is.EqualTo(Color.red));
            var markup = string.Join("\n", window.details.Query(className: "bgf-snippet__code").ToList().Select(l => ((Label)l).text));
            Assert.That(markup, Does.Contain("Taiyaki").And.Contain("--be-ink").And.Contain("90px"));

            // A variation is a look: it keeps the size and drops the rest.
            window.LoadVariation(System.Array.FindIndex(BagelArtGuide.Get("pastry-icon").variations, v => v.caption == "Locked"));
            simulate.FrameUpdate();
            Assert.That(pastry.silhouette, Is.True);
            Assert.That(pastry.style.width.value.value, Is.EqualTo(90f));
            Assert.That(pastry.customStyle.TryGetValue(new CustomStyleProperty<Color>("--be-ink"), out _), Is.False, "back to the theme's ink");
        }

        [Test]
        public void ASeasonsVariationSwitchesTheWindowsSeason()
        {
            var themes = window.themeCollection.collection;
            var original = themes.FindIndex(e => e.theme == window.currentTheme);
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SelectArt("lucky-bag");
            var type = BagelArtGuide.Get("lucky-bag");
            var target = (original + 1) % themes.Count;
            try
            {
                window.LoadVariation(System.Array.FindIndex(type.variations, v => v.season == target));
                simulate.FrameUpdate();
                Assert.That(window.currentTheme, Is.SameAs(themes[target].theme));
                Assert.That(window.artGallery[System.Array.FindIndex(type.variations, v => v.season == target)].ClassListContains(BagelFeaturesWindow.artCellSelectedUssClassName), Is.True);
            }
            finally
            {
                window.SelectSeason(original);
            }
        }

        [Test]
        public void DefaultsPutsTheFieldsBackToo()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SelectArt("begu-chan");
            simulate.FrameUpdate();
            var ink = window.details.Q<UnityEditor.UIElements.ColorField>("field-be-ink");
            var accessory = window.details.Q<DropdownField>("field-be-mascot-accessory");
            var themeInk = ink.value;
            var themeAccessory = accessory.value;

            window.SetArtValue("--be-ink", Color.green);
            window.SetArtValue("--be-mascot-accessory", themeAccessory == "snow" ? "sakura" : "snow");
            simulate.FrameUpdate();
            window.ResetArt();
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            Assert.That(ink.value, Is.EqualTo(themeInk), "the theme's ink again");
            Assert.That(accessory.value, Is.EqualTo(themeAccessory));
        }

        [Test]
        public void ThePreviewStaysInViewWhenTheDetailsScroll()
        {
            panelSize = new Vector2(802f, 829f);
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SelectArt("pastry-icon");
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            var pane = rootVisualElement.Q("details-pane");
            var before = window.artStage.worldBound.y - pane.worldBound.y;
            Assert.That(before, Is.GreaterThan(50f), "under the title at first");

            rootVisualElement.Q<ScrollView>("details-scroll").scrollOffset = new Vector2(0f, 900f);
            for (int i = 0; i < 3; i++)
                simulate.FrameUpdate();
            var after = window.artStage.worldBound.y - pane.worldBound.y;
            Assert.That(after, Is.InRange(0f, 20f), "at the top of the details");
        }

        [Test]
        public void ArtLinksGoToTheGameAndBack()
        {
            // A feature's Art Sampler link opens the Art tab on a row.
            panelSize = new Vector2(802f, 829f);
            window.ShowMode(BagelFeaturesWindow.Mode.Category);
            window.SelectFeature("custom-style-properties");
            simulate.FrameUpdate();
            var link = window.details.Query<Button>(className: "bgg-link").ToList().First(b => b.text.Contains("Art ›"));
            rootVisualElement.Q<ScrollView>("details-scroll").ScrollTo(link);
            simulate.FrameUpdate();
            simulate.Click(link);
            Assert.That(window.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Art));
            Assert.That(window.selectedArt, Is.EqualTo("begu-chan"));

            // A use opens its screen in Game mode.
            var use = BagelArtGuide.Get("begu-chan").uses[0];
            Assert.That(window.details.Query(className: "bgf-art-use").ToList().Count, Is.EqualTo(BagelArtGuide.Get("begu-chan").uses.Length));
            window.ShowUse(use);
            Assert.That(window.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Game));
            Assert.That(window.phone.screen, Is.SameAs(use.screen));
        }

        [Test]
        public void TheConfettiLoops()
        {
            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SelectArt("confetti");
            var confetti = (ConfettiBurst)window.artPreview;
            simulate.FrameUpdateMs(500);
            Assert.That(confetti.isPlaying, Is.True);
            simulate.FrameUpdateMs(6000);
            Assert.That(confetti.isPlaying, Is.True, "thrown again, and again");

            window.SetArtValue("--be-particle-size", 2f);
            simulate.FrameUpdate();
            Assert.That(confetti.customStyle.TryGetValue(ArtKit.particleSizeProperty, out var size) && Mathf.Approximately(size, 2f), Is.True, "its pieces' size");
        }

        [Test]
        public void ThePerformanceScreenShowsItsOverlayWithoutTurningItOn()
        {
            var setting = PerfOverlay.shown;
            window.ShowScreen(BagelFeatureGuide.screens.ToList().FindIndex(s => s.screen == GameScreen.settingsPerformance));
            simulate.FrameUpdateMs(600);

            var app = window.phone.Q<BakeryAppManager>();
            Assert.That(app.Q<PerfOverlay>().ClassListContains(PerfOverlay.hiddenUssClassName), Is.False, "the overlay is shown");
            var toggle = app.Q<SettingsPaneManager>().perfOverlayToggle;
            Assert.That(toggle.value, Is.True, "its toggle shows it on");
            Assert.That(toggle.enabledSelf, Is.False, "and can't be changed here");
            Assert.That(PerfOverlay.shown, Is.EqualTo(setting), "the setting, for every other screen, is as it was");
        }

        [Test]
        public void LeavingThePerformanceScreenPutsItsOverlayBack()
        {
            var setting = PerfOverlay.shown;
            window.ShowScreen(BagelFeatureGuide.screens.ToList().FindIndex(s => s.screen == GameScreen.settingsPerformance));
            simulate.FrameUpdateMs(600);
            var app = window.phone.Q<BakeryAppManager>();
            try
            {
                // As the demo toolbar's apps do when they move on to another screen.
                GameScreen.settingsPerformance.TearDown(app);
                simulate.FrameUpdate();
                var overlay = app.Q<PerfOverlay>();
                Assert.That(overlay.ClassListContains(PerfOverlay.hiddenUssClassName), Is.EqualTo(!setting), "the overlay as the setting has it");
                Assert.That(overlay.window.style.left.keyword, Is.EqualTo(StyleKeyword.Null), "where it sits by default");
                var toggle = app.Q<SettingsPaneManager>().perfOverlayToggle;
                Assert.That(toggle.enabledSelf, Is.True, "its switch is free again");
                Assert.That(toggle.value, Is.EqualTo(setting));
            }
            finally
            {
                // Another screen, so the next test that asks for this one gets it set up again.
                window.ShowScreen(0);
            }
        }

        [Test]
        public void WhatOpensOverAPageComesRightAfterIt()
        {
            var screens = BagelFeatureGuide.screens.Select(s => s.screen).ToList();
            Assert.That(screens.IndexOf(GameScreen.levelUp), Is.EqualTo(screens.IndexOf(GameScreen.shops) + 1));
            Assert.That(screens.IndexOf(GameScreen.reveal), Is.EqualTo(screens.IndexOf(GameScreen.luckyBag) + 1));
            Assert.That(screens.IndexOf(GameScreen.settingsPerformance), Is.EqualTo(screens.IndexOf(GameScreen.settings) + 1));
        }

        [Test]
        public void TheToolbarHugsItsStripsWithResetAfterTheBrandAndTheSearchOnTheRight()
        {
            panelSize = new Vector2(802f, 829f);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            var toolbar = rootVisualElement.Q<UnityEditor.UIElements.Toolbar>("toolbar").worldBound;
            var brand = rootVisualElement.Q("brand").worldBound;
            var search = rootVisualElement.Q<UnityEditor.UIElements.ToolbarSearchField>("search").worldBound;
            var seasons = rootVisualElement.Q<BagelOptionStrip>("season-strip").worldBound;
            var languages = rootVisualElement.Q<BagelOptionStrip>("language-strip").worldBound;
            var reset = rootVisualElement.Q<BagelOptionStrip>("reset-strip");
            Assert.That(toolbar.height, Is.EqualTo(35f).Within(0.5f), "one row");
            Assert.That(seasons.y - toolbar.y, Is.EqualTo(6f).Within(0.5f), "6 px above the strips");
            Assert.That(toolbar.yMax - seasons.yMax, Is.EqualTo(7f).Within(0.5f), "and 6 below, then the border");

            Assert.That(reset[0].name, Is.EqualTo("reset-button"));
            Assert.That(reset[0].Q<Label>(), Is.Null, "Reset is an icon, no text");
            Assert.That(reset.worldBound.x - brand.xMax, Is.LessThan(12f), "right after the brand");
            Assert.That(reset.worldBound.center.y, Is.EqualTo(seasons.center.y).Within(0.5f), "in line with the strips");
            Assert.That(search.height, Is.EqualTo(seasons.height), "the search as tall as the strips");
            Assert.That(search.center.y, Is.EqualTo(seasons.center.y).Within(0.5f), "in line with them");
            Assert.That(seasons.x - search.xMax, Is.EqualTo(languages.x - seasons.xMax).Within(0.5f), "next to them");
            Assert.That(toolbar.xMax - languages.xMax, Is.LessThan(12f), "on the right");
        }

        [Test]
        public void ItOpensOnTheTitleAndResetGoesBackThere()
        {
            // A new window (this fixture's is kept from test to test).
            var fresh = ScriptableObject.CreateInstance<BagelFeaturesWindow>();
            Assert.That(fresh.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Game));
            Assert.That(fresh.screenIndex, Is.EqualTo(0));
            Object.DestroyImmediate(fresh);

            panelSize = new Vector2(802f, 829f);

            window.ShowMode(BagelFeaturesWindow.Mode.Art);
            window.SelectArt("lantern");
            window.SetSearch("lantern");
            window.SelectSeason(2);
            BagelLocalization.SetLocale("ja");
            var oldState = window.state;
            simulate.FrameUpdate();

            simulate.Click(rootVisualElement.Q("reset-button"));
            simulate.FrameUpdate();
            Assert.That(window.mode, Is.EqualTo(BagelFeaturesWindow.Mode.Game));
            Assert.That(window.screenIndex, Is.EqualTo(0));
            Assert.That(window.phone.screen, Is.SameAs(GameScreen.titleScreen));
            Assert.That(window.selectedArt, Is.EqualTo("pastry-icon"));
            Assert.That(window.search, Is.Empty, "no search");
            Assert.That(rootVisualElement.Q<UnityEditor.UIElements.ToolbarSearchField>("search").value, Is.Empty);
            Assert.That(ReferenceEquals(window.state, oldState), Is.False, "a fresh game state");
            Assert.That(window.currentTheme, Is.SameAs(window.themeCollection.collection[0].theme), "the first season");
            Assert.That(BagelLocalization.localeCode, Is.EqualTo("en"));
            Assert.That(rootVisualElement.Q<BagelOptionStrip>("season-strip").selectedIndex, Is.Zero, "its strips say so");
            Assert.That(rootVisualElement.Q<BagelOptionStrip>("language-strip").selectedIndex, Is.EqualTo(BagelPickerIcons.LanguageIndex("en")));
        }
    }
}
