using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.SmartStrings.PersistentVariables;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The features' thumbnails, in the rows of the Bagel Features list. They're authored in BagelFeatureTiles.uxml, one
    /// per feature (<c>tile-&lt;feature id&gt;</c>): a very simple, live use of the feature itself; the features that live
    /// in the Editor get a small Editor window instead (<see cref="BagelEditorMockups"/>). This class takes each one
    /// out for its row and adds the little code some need: the window's game state as a data source, a ticking counter, a list.
    /// </summary>
    public static class BagelFeatureTiles
    {
        public const string uxmlPath = "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeatureTiles.uxml";
        public const string tilePrefix = "tile-";

        /// <summary>Style sheets a tile needs besides the season theme.</summary>
        public static readonly string[] styleSheetPaths =
        {
            "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeatureTiles.uss",
            "Assets/UI/Runtime/B03_App/BakeryAppStyles.uss",
            "Assets/UI/Runtime/B05_Home/HomeStyles.uss",
            "Assets/UI/Runtime/B09_Settings/SettingsStyles.uss",
        };

        /// <summary>Every thumbnail, by feature id, wired up. Their data source is set on <paramref name="host"/>'s side by the caller.</summary>
        public static Dictionary<string, VisualElement> CreateAll(BagelGuideWindow window)
        {
            var tiles = new Dictionary<string, VisualElement>();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (tree == null)
                return tiles;
            var root = tree.Instantiate();
            var all = new List<VisualElement>();
            root.Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(tilePrefix, StringComparison.Ordinal)).ForEach(all.Add);
            foreach (var tile in all)
            {
                tile.RemoveFromHierarchy();
                var id = tile.name.Substring(tilePrefix.Length);
                if (BagelEditorMockups.Has(id))
                {
                    Discard.Element(tile);
                    continue;
                }
                try
                {
                    Wire(id, tile, window);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bagel] Feature tile '{id}': {e.Message}");
                }
                tiles[id] = tile;
            }
            Discard.Element(root);
            // The Editor features: small Editor windows, in the Editor's theme.
            foreach (var id in BagelEditorMockups.ids)
                tiles[id] = BagelEditorMockups.Create(id, window.editorThemeSheets);
            return tiles;
        }

        /// <summary>A card bent like a curved screen: its top and bottom edges and three lines of text sag in the middle.</summary>
        static void DrawCurvedCard(MeshGenerationContext mgc)
        {
            var r = mgc.visualElement.contentRect;
            if (!(r.width > 4f) || !(r.height > 4f))
                return;
            var p = mgc.painter2D;
            float w = r.width, h = r.height, sag = h * 0.14f;
            // A quadratic's middle is halfway to its control point: a control 2 × sag below the ends sags it by sag.
            void Sag(float x0, float x1, float y) => p.QuadraticCurveTo(new Vector2((x0 + x1) * 0.5f, y + 2f * sag), new Vector2(x1, y));
            p.lineWidth = 2f;
            p.strokeColor = ArtKit.Ink;
            p.fillColor = ArtKit.Rgb(255, 248, 234);
            p.BeginPath();
            p.MoveTo(new Vector2(1f, 1f));
            Sag(1f, w - 1f, 1f);
            p.LineTo(new Vector2(w - 1f, h - 1f - sag));
            p.QuadraticCurveTo(new Vector2(w * 0.5f, h - 1f + sag), new Vector2(1f, h - 1f - sag));
            p.ClosePath();
            p.Fill();
            p.Stroke();
            p.lineWidth = 2.5f;
            p.strokeColor = ArtKit.Rgb(226, 54, 40, 0.75f);
            for (int i = 0; i < 3; i++)
            {
                var y = h * (0.28f + 0.2f * i);
                var end = w * (i == 2 ? 0.62f : 0.84f);
                p.BeginPath();
                p.MoveTo(new Vector2(w * 0.16f, y));
                Sag(w * 0.16f, end, y);
                p.Stroke();
            }
        }

        /// <summary>One feature's thumbnail, wired up, or null when it has none.</summary>
        public static VisualElement Create(string featureId, BagelGuideWindow window)
        {
            if (BagelEditorMockups.Has(featureId))
                return BagelEditorMockups.Create(featureId, window.editorThemeSheets);
            // A copy of every tile, for one: the others are dropped now, not left for the GC.
            var all = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath)?.Instantiate();
            var tile = all?.Q(tilePrefix + featureId);
            tile?.RemoveFromHierarchy();
            Discard.Element(all);
            if (tile == null)
                return null;
            try
            {
                Wire(featureId, tile, window);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Feature tile '{featureId}': {e.Message}");
            }
            return tile;
        }

        public static void AddStyleSheets(VisualElement host)
        {
            foreach (var path in styleSheetPaths)
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !host.styleSheets.Contains(sheet))
                    host.styleSheets.Add(sheet);
            }
        }

        static void Wire(string id, VisualElement tile, BagelGuideWindow window)
        {
            switch (id)
            {
                case "zero-alloc-settext":
                {
                    var label = tile.Q<Label>("counter");
                    var value = 0;
                    label.schedule.Execute(() => label.SetText(value = (value + 3) % 100000)).Every(50);
                    break;
                }

                case "clip-restart-classes":
                {
                    // The game's one-shot pattern: take the class off, put it back a frame later, and the clip plays again.
                    var pop = tile.Q<Label>("pop");
                    pop.schedule.Execute(() =>
                    {
                        pop.RemoveFromClassList("bgft-pop--play");
                        pop.schedule.Execute(() => pop.AddToClassList("bgft-pop--play")).ExecuteLater(16);
                    }).Every(1600);
                    break;
                }

                case "pointer-events":
                {
                    var tap = tile.Q("tap");
                    var label = tile.Q<Label>("taps");
                    var taps = 0;
                    tap.RegisterCallback<PointerDownEvent>(_ => label.text = (++taps).ToString());
                    break;
                }

                case "scheduler":
                {
                    var clock = tile.Q<Label>("clock");
                    var start = DateTime.Now;
                    clock.schedule.Execute(() =>
                    {
                        var t = DateTime.Now - start;
                        clock.text = $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";
                    }).Every(1000);
                    break;
                }

                case "perf-overlay":
                {
                    // The overlay's graph, fed with this window's own update interval.
                    var graph = tile.Q<PerfGraph>("graph");
                    var last = EditorApplication.timeSinceStartup;
                    graph.schedule.Execute(() =>
                    {
                        var now = EditorApplication.timeSinceStartup;
                        graph.Push((float)((now - last) * 1000.0));
                        last = now;
                    }).Every(33);
                    break;
                }

                case "listview-binding":
                {
                    var list = tile.Q<ListView>("list");
                    list.makeItem = () => new Label();
                    list.bindItem = (element, index) =>
                    {
                        if (list.itemsSource is IList<ShopState> shops && index < shops.Count)
                            ((Label)element).text = shops[index].displayName;
                    };
                    list.SetBinding(nameof(ListView.itemsSource), new DataBinding
                    {
                        dataSourcePath = new PropertyPath(nameof(BakeryState.shops)),
                        bindingMode = BindingMode.ToTarget,
                    });
                    break;
                }

                case "csharp-bindings":
                    tile.Q<MeterBar>("meter").SetBinding(nameof(MeterBar.value), new DataBinding
                    {
                        dataSourcePath = new PropertyPath(nameof(BakeryState.registerFill)),
                        bindingMode = BindingMode.ToTarget,
                    });
                    break;

                case "curved-ui":
                    tile.Q("curved").generateVisualContent += DrawCurvedCard;
                    break;

                case "unique-style-string":
                {
                    // The state class as a UniqueStyleString, toggled every moment.
                    var card = tile.Q("card");
                    var locked = new UniqueStyleString("be-card--locked");
                    var on = false;
                    card.schedule.Execute(() => card.EnableInClassList(locked, on = !on)).Every(1200);
                    break;
                }

                case "gradients-filters-from-csharp":
                {
                    // What Bagel does in USS, from C#: a gradient background and a filter.
                    tile.Q("gradient").style.backgroundImage = Background.FromGradient(BackgroundGradient.Linear(Mathf.PI * 0.75f,
                        BackgroundGradientStop.Percent(ArtKit.Rgb(255, 214, 90), 0f), BackgroundGradientStop.Percent(ArtKit.Rgb(226, 54, 40), 1f)));
                    var sepia = new FilterFunction(FilterFunctionType.Sepia);
                    sepia.AddParameter(new FilterParameter(1f));
                    tile.Q("filtered").style.filter = new List<FilterFunction> { sepia };
                    break;
                }

                case "localization-controls":
                    tile.Add(new Unity.Localization.UI.LanguageDropdown());
                    break;

                case "localizedstring-csharp":
                    tile.Q<Label>("localized").SetBinding("text", BagelLocalization.String("nav.home"));
                    break;

                case "smart-strings":
                {
                    var label = tile.Q<Label>("smart");
                    var count = new IntVariable { Value = 1 };
                    var text = BagelLocalization.String("shops.open-count");
                    text.LocalVariables.Add(count, "count");
                    label.SetBinding("text", text);
                    label.schedule.Execute(() =>
                    {
                        count.Value = count.Value % 6 + 1;
                        text.RefreshString();
                    }).Every(1200);
                    break;
                }

                case "locale-switching":
                    tile.Q<Button>("en")?.RegisterCallback<ClickEvent>(_ => BagelLocalization.SetLocale("en"));
                    tile.Q<Button>("ja")?.RegisterCallback<ClickEvent>(_ => BagelLocalization.SetLocale("ja"));
                    tile.Q<Button>("fr")?.RegisterCallback<ClickEvent>(_ => BagelLocalization.SetLocale("fr"));
                    break;
            }
        }
    }
}
