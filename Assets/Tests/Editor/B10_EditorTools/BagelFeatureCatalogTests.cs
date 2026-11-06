using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B10_EditorTools
{
    /// <summary>
    /// The feature catalog stays true to the project: every source line it links to is still there, every sample and
    /// thumbnail belongs to a feature, and nothing in it points outside the project.
    /// </summary>
    public class BagelFeatureCatalogTests
    {
        // This folder is published on its own: no document outside it (*.md), no absolute paths, no issue-tracker IDs.
        static readonly System.Text.RegularExpressions.Regex s_PointsOutside =
            new(@"\b\w+\.md\b|(^|[\s(""'])/(Users|home|opt|tmp)/|\b[A-Z]{3,}-\d{4,}\b");

        [Test]
        public void FeaturesHaveUniqueIdsAndText()
        {
            var features = BagelFeatureCatalog.features;
            Assert.That(features.Count, Is.GreaterThan(50));
            Assert.That(features.Select(f => f.id).Distinct().Count(), Is.EqualTo(features.Count), "duplicate feature ids");
            foreach (var feature in features)
            {
                Assert.That(feature.title, Is.Not.Empty, feature.id);
                Assert.That(feature.about.Length, Is.InRange(20, 120), $"{feature.id}: what it is, in one short sentence");
                Assert.That(feature.summary.Length, Is.InRange(20, 120), $"{feature.id}: how the game uses it, in one short sentence");
            }
        }

        [Test]
        public void EverySourceLineIsStillThere()
        {
            var missing = new List<string>();
            foreach (var feature in BagelFeatureCatalog.features)
                foreach (var source in feature.sources)
                    if (EditorLink.FindLine(source.path, source.find) == 0)
                        missing.Add($"{feature.id}: {source.path} › \"{source.find}\"");
            Assert.That(missing, Is.Empty, "Sources whose file or line is gone; update BagelFeatureCatalog.Features.cs");
        }

        [Test]
        public void EveryLinkedAssetExists()
        {
            foreach (var feature in BagelFeatureCatalog.features)
                foreach (var link in feature.links.Where(l => l.path != null))
                    Assert.That(AssetDatabase.LoadMainAssetAtPath(link.path), Is.Not.Null, $"{feature.id}: {link.label}");
        }

        [Test]
        public void EveryLinkedElementIsInTheAppUxml()
        {
            var app = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelExpressAllUIWindow.appUxmlPath).Instantiate();
            foreach (var feature in BagelFeatureCatalog.features)
                foreach (var link in feature.links.Where(l => l.element != null))
                    Assert.That(GameScreen.Find(app, link.element), Is.Not.Null, $"{feature.id}: {link.label}");
        }

        [Test]
        public void EverySampleNamesAFeature()
        {
            var samples = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelFeatureSamples.uxmlPath).Instantiate();
            samples.Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(BagelFeatureSamples.samplePrefix)).ForEach(e =>
                Assert.That(BagelFeatureCatalog.Get(e.name.Substring(BagelFeatureSamples.samplePrefix.Length)), Is.Not.Null, e.name));
        }

        [Test]
        public void EveryFeatureHasAThumbnail()
        {
            // A live one in BagelFeatureTiles.uxml, or a small Editor window for a feature that lives in the Editor.
            var tiles = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelFeatureTiles.uxmlPath).Instantiate();
            foreach (var feature in BagelFeatureCatalog.features.Concat(BagelFeatureCatalog.unused))
            {
                var inUxml = tiles.Q(BagelFeatureTiles.tilePrefix + feature.id) != null;
                Assert.That(inUxml || BagelEditorMockups.Has(feature.id), Is.True, $"{feature.id} has no thumbnail");
                Assert.That(inUxml && BagelEditorMockups.Has(feature.id), Is.False, $"{feature.id} has two thumbnails");
            }
            tiles.Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(BagelFeatureTiles.tilePrefix)).ForEach(e =>
                Assert.That(BagelFeatureCatalog.Get(e.name.Substring(BagelFeatureTiles.tilePrefix.Length)), Is.Not.Null, e.name));
            foreach (var id in BagelEditorMockups.ids)
            {
                Assert.That(BagelFeatureCatalog.Get(id), Is.Not.Null, $"Editor window for no feature: {id}");
                Assert.That(BagelEditorMockups.Create(id, null), Is.Not.Null, id);
            }
        }

        [Test]
        public void NothingPointsOutsideTheProject()
        {
            foreach (var feature in BagelFeatureCatalog.features.Concat(BagelFeatureCatalog.unused))
                foreach (var text in new[] { feature.title, feature.about, feature.summary }.Concat(feature.sources.SelectMany(s => new[] { s.path, s.what })))
                    Assert.That(s_PointsOutside.IsMatch(text), Is.False, $"{feature.id}: {text}");
        }
    }
}
