using System;
using System.Collections.Generic;
using Unity.Localization;
using Unity.Properties;
using Unity.SmartStrings.PersistentVariables;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The features' live samples. They're authored in BagelFeatureSamples.uxml, one container per feature
    /// (<c>sample-&lt;feature id&gt;</c>), and run under the season theme like the game. This class takes each one out and
    /// adds what runs by itself (a counter, a binding); Bagel Features puts it on the feature's preview stage, with the
    /// controls of <see cref="BagelFeaturePreviews"/> under it.
    /// </summary>
    public static class BagelFeatureSamples
    {
        public const string uxmlPath = "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeatureSamples.uxml";
        public const string samplePrefix = "sample-";

        /// <summary>Style sheets a sample stage needs besides the season theme: the samples' own and the game's app and Settings sheets.</summary>
        public static readonly string[] styleSheetPaths =
        {
            "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeatureSamples.uss",
            "Assets/UI/Runtime/B03_App/BakeryAppStyles.uss",
            "Assets/UI/Runtime/B09_Settings/SettingsStyles.uss",
        };

        /// <summary>Every sample, by feature id, wired up for <paramref name="window"/>.</summary>
        public static Dictionary<string, VisualElement> CreateAll(BagelGuideWindow window)
        {
            var samples = new Dictionary<string, VisualElement>();
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (tree == null)
                return samples;
            var root = tree.Instantiate();
            var all = new List<VisualElement>();
            root.Query<VisualElement>().Where(e => e.name != null && e.name.StartsWith(samplePrefix, StringComparison.Ordinal)).ForEach(all.Add);
            foreach (var sample in all)
            {
                sample.RemoveFromHierarchy();
                var id = sample.name.Substring(samplePrefix.Length);
                try
                {
                    Wire(id, sample, window);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bagel] Feature sample '{id}': {e.Message}");
                }
                samples[id] = sample;
            }
            Discard.Element(root);
            return samples;
        }

        /// <summary>One feature's sample, wired up for <paramref name="window"/>, or null when it has none.</summary>
        public static VisualElement Create(string featureId, BagelGuideWindow window)
        {
            // A copy of every sample, for one: the others are dropped now, not left for the GC.
            var all = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath)?.Instantiate();
            var sample = all?.Q(samplePrefix + featureId);
            sample?.RemoveFromHierarchy();
            Discard.Element(all);
            if (sample == null)
                return null;
            try
            {
                Wire(featureId, sample, window);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Feature sample '{featureId}': {e.Message}");
            }
            return sample;
        }

        /// <summary>Give a sample stage the style sheets the samples need (the season theme comes from the window).</summary>
        public static void AddStyleSheets(VisualElement stage)
        {
            foreach (var path in styleSheetPaths)
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                if (sheet != null && !stage.styleSheets.Contains(sheet))
                    stage.styleSheets.Add(sheet);
            }
        }

        static void Wire(string id, VisualElement sample, BagelGuideWindow window)
        {
            switch (id)
            {
                case "zero-alloc-settext":
                {
                    // Counts every frame; the preview's Running switch pauses it (the scheduled item is the sample's userData).
                    var label = sample.Q<Label>("settext-counter");
                    var value = 0;
                    sample.userData = label?.schedule.Execute(() =>
                    {
                        value = (value + 7) % 1000000;
                        label.SetText(value);
                    }).Every(16);
                    break;
                }

                case "uxml-databinding":
                    // The pill's binding is declared in the UXML (data-source-path="coins"); the data source is the window's state.
                    sample.dataSource = window.state;
                    Action<BakeryState> rebind = state => sample.dataSource = state;
                    sample.RegisterCallback<AttachToPanelEvent>(_ => window.stateReplaced += rebind);
                    sample.RegisterCallback<DetachFromPanelEvent>(_ => window.stateReplaced -= rebind);
                    break;

                case "ui-components":
                {
                    var button = sample.Q<Button>("badge-button");
                    button?.RegisterCallback<ClickEvent>(_ => AddToBadge(button, 1));
                    break;
                }

                case "pointer-events":
                {
                    var target = sample.Q("tap-target");
                    var label = sample.Q<Label>("tap-count");
                    var taps = 0;
                    target?.RegisterCallback<PointerDownEvent>(evt => label.text = $"Tap {++taps} · pointer {evt.pointerId} at ({evt.localPosition.x:0}, {evt.localPosition.y:0})");
                    break;
                }

                case "scheduler":
                {
                    var clock = sample.Q<Label>("clock");
                    var end = DateTime.Now.AddHours(2);
                    clock?.schedule.Execute(() =>
                    {
                        var left = end - DateTime.Now;
                        clock.text = $"{(int)left.TotalHours:00}:{left.Minutes:00}:{left.Seconds:00}";
                    }).Every(1000);
                    break;
                }

                case "localizedstring-uxml":
                    sample.Q<Label>("localized-label")?.SetBinding("text", BagelLocalization.String("title.tap-to-start"));
                    break;

                case "locale-switching":
                    sample.Q<Label>("localized-label")?.SetBinding("text", BagelLocalization.String("page.shops.title"));
                    sample.Q<Label>("localized-line")?.SetBinding("text", BagelLocalization.String("home.line.welcome"));
                    break;

                case "smart-strings":
                {
                    // The preview's count control sets the variable (the sample's userData).
                    var label = sample.Q<Label>("smart-label");
                    var count = new IntVariable { Value = 2 };
                    var text = BagelLocalization.String("shops.open-count");
                    text.LocalVariables.Add(count, "count");
                    label?.SetBinding("text", text);
                    sample.userData = (count, text);
                    break;
                }
            }
        }

        /// <summary>Add to a button's NotificationBadge (a UI Component), and let it know.</summary>
        public static void AddToBadge(Button button, int amount)
        {
            if (button == null || !button.HasComponent<NotificationBadge>())
                return;
            ref var badge = ref button.GetComponent<NotificationBadge>();
            badge.count = Mathf.Max(0, badge.count + amount);
            button.MarkComponentDirty<NotificationBadge>();
        }
    }
}
