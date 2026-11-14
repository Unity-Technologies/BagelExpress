using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// A feature's details, the way the Art mode shows art: its live sample on a stage that stays in view
    /// (<see cref="BagelFeaturePreviews"/>), the controls under it, then a few words, the code and the links, and at the
    /// end the parts of the game's screens that use it, each live on its own (<see cref="PartPreview"/>).
    /// </summary>
    public partial class BagelFeaturesWindow
    {
        const float k_PreviewHeight = 170f;
        const float k_UseHeight = 150f;
        const long k_UsesDelayMs = 160;

        VisualElement m_PreviewStage;
        VisualElement m_PreviewContent;
        BagelFeaturePreviews.Preview m_Preview;
        readonly Dictionary<string, object> m_PreviewValues = new();
        readonly Dictionary<string, Action<object>> m_PreviewSync = new();
        readonly Dictionary<string, VisualElement> m_PreviewFields = new();
        readonly List<StyleSheet> m_PreviewSheets = new();
        readonly List<PartPreview> m_Uses = new();
        IVisualElementScheduledItem m_BuildUses;

        /// <summary>The feature's live sample (or thumbnail) on its stage.</summary>
        public VisualElement previewContent => m_PreviewContent;
        /// <summary>The parts of the screens that use the feature, live, once they're built.</summary>
        public IReadOnlyList<PartPreview> uses => m_Uses;

        // ------------------------------------------------------------------ the stage and its controls

        void BuildFeaturePreview(BagelFeatureCatalog.Feature feature)
        {
            m_Preview = BagelFeaturePreviews.Get(feature.id, this);
            m_PreviewValues.Clear();
            foreach (var control in m_Preview.controls)
                m_PreviewValues[control.name] = control.value;

            m_PreviewStage = new VisualElement { name = "preview-stage" };
            m_PreviewStage.AddToClassList("bgf-preview-stage");
            m_PreviewStage.AddToClassList(ArtKit.animateInEditModeUssClassName);
            m_PreviewStage.dataSource = state;
            AddArtThemedRoot(m_PreviewStage);
            BagelFeatureSamples.AddStyleSheets(m_PreviewStage);
            BagelFeatureTiles.AddStyleSheets(m_PreviewStage);

            m_PreviewContent = BagelFeatureSamples.Create(feature.id, this);
            var isTile = m_PreviewContent == null;
            m_PreviewContent ??= BagelFeatureTiles.Create(feature.id, this);
            if (m_PreviewContent != null)
            {
                m_PreviewContent.AddToClassList(isTile ? "bgf-preview-tile" : "bgf-preview-sample");
                m_PreviewStage.Add(m_PreviewContent);
                m_PreviewContent.RegisterCallback<GeometryChangedEvent>(_ => FitPreview(isTile));
                m_PreviewStage.RegisterCallback<GeometryChangedEvent>(_ => FitPreview(isTile));
                // The theme's values are on the stage (its :root), the sample's own on the sample: either changes them.
                m_PreviewContent.RegisterCallback<CustomStyleResolvedEvent>(_ => SyncPreviewCustomFields());
                m_PreviewStage.RegisterCallback<CustomStyleResolvedEvent>(_ => SyncPreviewCustomFields());
            }
            AddSticky(m_PreviewStage, "preview");

            var bar = new VisualElement();
            bar.AddToClassList("bgf-art-bar");
            foreach (var (label, tooltip, run) in m_Preview.actions)
                bar.Add(Action(label, tooltip, "bgf-action--scene", () =>
                {
                    if (m_PreviewContent != null)
                        run(m_PreviewContent);
                }));
            if (m_Preview.controls.Length > 0)
                bar.Add(Action("↺ Defaults", "Back to the sample as authored", "bgf-action--asset", () => SelectFeature(feature.id)));
            if (bar.childCount > 0)
                m_Details.Add(bar);

            if (m_Preview.controls.Length > 0)
            {
                m_Details.Add(Text("TRY IT", "bgf-section"));
                var box = new VisualElement { name = "preview-controls" };
                box.AddToClassList("bgf-art-controls");
                foreach (var control in m_Preview.controls)
                {
                    var value = control.value ?? control.fallback;
                    var field = CreateField(control, value, v => SetPreviewValue(control, v), out var sync);
                    m_PreviewSync[control.name] = sync;
                    m_PreviewFields[control.name] = field;
                    if (control.target == BagelArtGuide.Target.CustomProperty && !BagelArtGuide.canOverrideCustomProperties)
                        field.SetEnabled(false);
                    box.Add(field);
                }
                AddEditorStyledPart(box);
                m_DetailsEditorParts.Add(box);
                m_Details.Add(box);
            }
            m_PreviewStage.schedule.Execute(SyncPreviewCustomFields);
            if (m_PreviewContent != null)
                m_Preview.wire?.Invoke(m_PreviewContent, name => m_PreviewFields.TryGetValue(name, out var field) ? field : null);
        }

        /// <summary>A control changed: set what it sets on the sample (a custom property: a new style sheet on it).</summary>
        void SetPreviewValue(BagelArtGuide.Control control, object value)
        {
            m_PreviewValues[control.name] = value;
            if (m_PreviewContent == null)
                return;
            if (control.target == BagelArtGuide.Target.CustomProperty)
                ApplyPreviewCustom();
            else
                control.Set(m_PreviewContent, value);
        }

        /// <summary>Set a preview's control from code, as if from its field.</summary>
        public void SetPreviewValue(string controlName, object value)
        {
            var control = m_Preview?.controls.FirstOrDefault(c => c.name == controlName);
            if (control == null)
                return;
            SetPreviewValue(control, value);
            if (value != null && m_PreviewSync.TryGetValue(controlName, out var sync))
                sync(value);
        }

        /// <summary>The custom properties set by the controls, in a style sheet on the sample (its :root).</summary>
        void ApplyPreviewCustom()
        {
            foreach (var sheet in m_PreviewSheets)
            {
                m_PreviewContent.styleSheets.Remove(sheet);
                RetireSheet(sheet);
            }
            m_PreviewSheets.Clear();
            var declarations = m_Preview.controls
                .Where(c => c.target == BagelArtGuide.Target.CustomProperty && m_PreviewValues.TryGetValue(c.name, out var v) && v != null)
                .Select(c => "    " + c.Uss(m_PreviewValues[c.name])).ToList();
            if (declarations.Count == 0)
                return;
            var created = BagelArtGuide.CreateStyleSheet(":root {\n" + string.Join("\n", declarations) + "\n}");
            if (created == null)
                return;
            m_PreviewSheets.Add(created);
            m_PreviewContent.styleSheets.Add(created);
        }

        /// <summary>A custom property left to the theme shows the theme's value.</summary>
        void SyncPreviewCustomFields()
        {
            if (m_Preview == null || m_PreviewContent == null)
                return;
            foreach (var control in m_Preview.controls)
            {
                if (control.target != BagelArtGuide.Target.CustomProperty || m_PreviewValues.TryGetValue(control.name, out var v) && v != null)
                    continue;
                if (m_PreviewSync.TryGetValue(control.name, out var sync))
                    sync(BagelArtGuide.Resolved(m_PreviewContent, control) ?? control.fallback);
            }
        }

        /// <summary>The sample as big as the stage allows (a thumbnail grows, a sample only shrinks a little to fit).</summary>
        void FitPreview(bool isTile)
        {
            if (m_PreviewContent?.panel == null || m_PreviewStage == null)
                return;
            var size = m_PreviewContent.layout.size;
            var room = m_PreviewStage.contentRect.size;
            if (!(size.x >= 1f) || !(size.y >= 1f) || !(room.x >= 10f))
                return;
            var scale = Mathf.Min(isTile ? 2.6f : 1.2f, room.x * 0.88f / size.x, room.y * 0.8f / size.y);
            if (Mathf.Abs(m_PreviewContent.resolvedStyle.scale.value.x - scale) > 0.01f)
                m_PreviewContent.style.scale = new Scale(new Vector2(scale, scale));
        }

        void ClearPreview()
        {
            m_BuildUses?.Pause();
            m_BuildUses = null;
            foreach (var use in m_Uses)
                use.Release();
            m_Uses.Clear();
            foreach (var sheet in m_PreviewSheets)
                RetireSheet(sheet);
            m_PreviewSheets.Clear();
            m_PreviewSync.Clear();
            m_PreviewFields.Clear();
            m_PreviewValues.Clear();
            m_Preview = null;
            m_PreviewStage = null;
            m_PreviewContent = null;
        }

        // ------------------------------------------------------------------ where the game uses it

        /// <summary>
        /// The parts that use the feature, each a card with the part live on its own. They're built a moment after the
        /// feature is picked, so walking the list with the arrows doesn't build every feature's.
        /// </summary>
        void AddUses(BagelFeatureCatalog.Feature feature)
        {
            var parts = BagelFeatureGuide.PartsOf(feature.id).ToList();
            if (parts.Count == 0)
                return;
            m_Details.Add(Text("IN BAGEL EXPRESS", "bgf-section"));
            var frames = new List<(PartPreview frame, VisualElement card)>();
            foreach (var (screen, part) in parts)
            {
                var screenIndex = BagelFeatureGuide.screens.ToList().IndexOf(screen);
                var partIndex = Array.IndexOf(screen.parts, part);
                var frame = new PartPreview(part, k_UseHeight) { name = "use-" + m_Uses.Count };
                var card = PartCard(part, partIndex, screen.screen.title, () => ShowPart(screenIndex, partIndex), frame);
                m_Uses.Add(frame);
                frames.Add((frame, card));
                m_Details.Add(card);
            }
            m_BuildUses = m_Details.schedule.Execute(() =>
            {
                m_BuildUses = null;
                foreach (var (frame, card) in frames)
                    if (frame.panel != null && !frame.Build(this))
                        frame.style.display = DisplayStyle.None;
            });
            m_BuildUses.ExecuteLater(k_UsesDelayMs);
        }

        /// <summary>Build the uses now (the tests don't wait).</summary>
        public void BuildUsesNow()
        {
            if (m_BuildUses == null)
                return;
            m_BuildUses.Pause();
            m_BuildUses = null;
            foreach (var frame in m_Uses)
                if (frame.panel != null && !frame.Build(this))
                    frame.style.display = DisplayStyle.None;
        }
    }
}
