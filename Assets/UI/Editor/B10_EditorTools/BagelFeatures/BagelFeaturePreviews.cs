using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Properties;
using Unity.SmartStrings.PersistentVariables;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Control = Bagel.BagelArtGuide.Control;
using Target = Bagel.BagelArtGuide.Target;
using ValueType = Bagel.BagelArtGuide.ValueType;

namespace Bagel
{
    /// <summary>
    /// What Bagel Features shows on a feature's preview stage, and the controls under it: the feature's live sample
    /// (<see cref="BagelFeatureSamples"/>) or, without one, its thumbnail (<see cref="BagelFeatureTiles"/>), bigger. Each
    /// control changes one thing of the sample, the way the feature does it (an attribute, an inline style, a class, a
    /// custom property, a binding's source); a button runs something once.
    /// </summary>
    public static class BagelFeaturePreviews
    {
        public sealed class Preview
        {
            public Control[] controls = Array.Empty<Control>();
            public (string label, string tooltip, Action<VisualElement> run)[] actions = Array.Empty<(string, string, Action<VisualElement>)>();
            /// <summary>Run once the controls exist: (the stage's content, a control's field by name).</summary>
            public Action<VisualElement, Func<string, VisualElement>> wire;
        }

        const string k_HalftonePath = "Assets/UI/Runtime/B11_Filters/MangaHalftone.asset";
        static readonly string[] s_LocaleCodes = { "en", "ja", "fr" };
        static readonly string[] s_LocaleNames = { "English", "日本語", "Français" };

        /// <summary>A feature's controls and buttons (empty for one without); made for the window, with its current values.</summary>
        public static Preview Get(string featureId, BagelGuideWindow window)
        {
            switch (featureId)
            {
                // ------------------------------------------------------------------ Styling
                case "uss-variables":
                    return P(Custom("--be-primary", "--be-primary", ArtKit.Rgb(226, 74, 50)), Custom("--be-accent", "--be-accent", ArtKit.Rgb(240, 160, 40)),
                        Custom("--be-gold", "--be-gold", ArtKit.Rgb(255, 196, 40)), Custom("--be-mint", "--be-mint", ArtKit.Rgb(64, 201, 132)));
                case "tss-themes":
                {
                    var themes = window.themeCollection != null ? window.themeCollection.collection : null;
                    var names = themes?.Select(t => t.themeName).ToArray() ?? Array.Empty<string>();
                    var current = themes?.FindIndex(t => t.theme == window.currentTheme) ?? -1;
                    return P(Choice("season", "Season", names, current >= 0 ? names[current] : names.FirstOrDefault(), (_, v) => window.SelectSeason(Array.IndexOf(names, v)),
                        "The ThemeStyleSheet every game panel in this window uses"));
                }
                case "custom-style-properties":
                    return P(Keyword("--be-mascot-accessory", "Accessory", new[] { "maple", "sakura", "matsuri", "snow" }, "maple"),
                        Custom("--be-lantern-color", "Lantern", ArtKit.Rgb(226, 54, 40)), Custom("--be-lantern-glow", "Glow", ArtKit.Rgb(255, 140, 60, 0.8f)));
                case "pseudo-and-state-classes":
                    return P(Bool("be-card--locked", ".be-card--locked", false, (s, v) => s.Q("state-card")?.EnableInClassList("be-card--locked", v), "A state class on the card"),
                        Bool("enabled", "Buttons enabled", true, (s, v) => s.Query<Button>().ForEach(b => b.SetEnabled(v)), "SetEnabled: :disabled"));

                // ------------------------------------------------------------------ Effects
                case "geometry-shadows":
                    return P(new Control
                    {
                        name = "--be-tile-lip", label = "--be-tile-lip", tooltip = "The lip: a thick bottom border, var(--be-tile-lip)",
                        target = Target.CustomProperty, type = ValueType.Float, fallback = 7f, min = 0f, max = 16f,
                        uss = v => $"--be-tile-lip: {BagelArtGuide.Number(Convert.ToSingle(v))}px;",
                    });
                case "drop-shadow":
                    return P(Float("offset-y", "Offset Y", 4f, 0f, 16f, (s, _) => DropShadow(s), "drop-shadow(0 <y> <blur> color)"),
                        Float("blur", "Blur", 0f, 0f, 16f, (s, _) => DropShadow(s)),
                        Colour("color", "Color", ArtKit.Ink, (s, _) => DropShadow(s)));
                case "box-shadow":
                    return P(Float("offset-x", "Offset X", 0f, -16f, 16f, (s, _) => SetBoxShadow(s), "box-shadow: <x> <y> <blur> <spread> <color>"),
                        Float("offset-y", "Offset Y", 8f, -16f, 24f, (s, _) => SetBoxShadow(s)),
                        Float("blur", "Blur", 16f, 0f, 48f, (s, _) => SetBoxShadow(s), "CSS's blur radius; its drop-shadow() twin gets half, a sigma"),
                        Float("spread", "Spread", 0f, -8f, 16f, (s, _) => SetBoxShadow(s), "Grows or shrinks the shadow: box-shadow only"),
                        Colour("color", "Color", k_BoxShadowColor, (s, _) => SetBoxShadow(s)),
                        Bool("inset", "inset", false, (s, _) => SetBoxShadow(s), "Inside the border instead: box-shadow only"));
                case "backdrop-filter":
                    return P(Float("blur", "blur()", 6f, 0f, 16f, (s, v) =>
                    {
                        var glass = s.Q("glass");
                        if (glass != null)
                            glass.style.backdropFilter = new List<FilterFunction> { Filter(FilterFunctionType.Blur, Convert.ToSingle(v)) };
                    }, "backdrop-filter: blur(<px>)"));
                case "custom-filter-function":
                    return P(Float("spacing", "spacing", 6f, 2f, 20f, (s, _) => Halftone(s), "The filter's first parameter: the dot spacing in pixels"),
                        Colour("ink", "ink", ArtKit.Rgb(120, 30, 20), (s, _) => Halftone(s), "Its second: the dots' color"));
                case "ui-shader-graph":
                    return P(new Control
                    {
                        name = "-unity-material", label = "-unity-material", tooltip = "Off: no material moves the particles, so they stand where they start",
                        target = Target.Style, type = ValueType.Bool, value = true,
                        set = Recorded("-unity-material", (s, v) => s.Query<SeasonParticles>().ForEach(particles =>
                            particles.style.unityMaterial = (bool)v ? new StyleMaterialDefinition(StyleKeyword.Null) : new StyleMaterialDefinition(StyleKeyword.None))),
                    });
                case "ui-shader-graph-vertices":
                    return P(Keyword("--be-particle-kind", "--be-particle-kind", new[] { "maple", "sakura", "firefly", "snow" }, "maple"),
                        new Control
                        {
                            name = "--be-particle-count", label = "--be-particle-count", tooltip = "How many particles: each one is drawn once",
                            target = Target.CustomProperty, type = ValueType.Float, fallback = 18f, min = 0f, max = SeasonParticles.maxCount,
                            uss = v => $"--be-particle-count: {BagelArtGuide.Number(Convert.ToSingle(v))};",
                        },
                        Keyword("--be-motion", "--be-motion", new[] { "auto", "none" }, "auto"));

                // ------------------------------------------------------------------ Layout
                case "flexbox-layout":
                    return P(Choice("flex-direction", "flex-direction", new[] { "row", "row-reverse", "column" }, "row",
                            (s, v) => s.style.flexDirection = v == "column" ? FlexDirection.Column : v == "row-reverse" ? FlexDirection.RowReverse : FlexDirection.Row),
                        Choice("justify-content", "justify-content", new[] { "center", "flex-start", "flex-end", "space-between" }, "center",
                            (s, v) => s.style.justifyContent = v switch { "flex-start" => Justify.FlexStart, "flex-end" => Justify.FlexEnd, "space-between" => Justify.SpaceBetween, _ => Justify.Center }));
                case "gap":
                    return P(Float("column-gap", "column-gap", 12f, 0f, 40f, (s, v) =>
                    {
                        var row = s.Q(className: "bgfs-gap");
                        if (row != null)
                            row.style.columnGap = Convert.ToSingle(v);
                    }));
                case "z-index":
                    return P(Int("z-index", "z-index (red)", 1, -1, 5, (s, v) =>
                    {
                        var card = s.Q(className: "bgfs-stack__card--a");
                        if (card != null)
                            card.style.zIndex = Convert.ToInt32(v);
                    }, "The others are 3 (gold) and 2 (mint)"));
                case "grid-layout":
                    return P(Float("gap", "gap", 6f, 0f, 20f, (s, v) =>
                    {
                        s.style.rowGap = Convert.ToSingle(v);
                        s.style.columnGap = Convert.ToSingle(v);
                    }));
                case "transforms":
                    return P(Float("rotate", "rotate (NEW!)", -8f, -45f, 45f, (s, v) =>
                        {
                            var sticker = s.Q(className: "bgfs-transform--rotate");
                            if (sticker != null)
                                sticker.style.rotate = new Rotate(new Angle(Convert.ToSingle(v), AngleUnit.Degree));
                        }),
                        Float("scale", "scale (SALE)", 1.15f, 0.5f, 1.8f, (s, v) =>
                        {
                            var sticker = s.Q(className: "bgfs-transform--scale");
                            if (sticker != null)
                                sticker.style.scale = new Scale(Vector2.one * Convert.ToSingle(v));
                        }));

                // ------------------------------------------------------------------ Vector art
                case "painter2d":
                    return P(Choice("kind", "kind", Enum.GetNames(typeof(PastryKind)), PastryKind.SesameBagel.ToString(), (s, v) =>
                        {
                            if (s.Q<PastryIcon>() is { } pastry && Enum.TryParse<PastryKind>(v, out var kind))
                                pastry.kind = kind;
                        }, "The first pastry's [UxmlAttribute] kind"),
                        Bool("silhouette", "silhouette", false, (s, v) =>
                        {
                            if (s.Q<PastryIcon>() is { } pastry)
                                pastry.silhouette = v;
                        }));
                case "generate-visual-content":
                    return P(Int("ray-count", "ray-count", 10, 3, 32, (s, v) =>
                    {
                        if (s.Q<Starburst>() is { } burst)
                            burst.rayCount = Convert.ToInt32(v);
                    }, "Its generateVisualContent redraws with MarkDirtyRepaint"));
                case "procedural-animated-art":
                    return P(Choice("mood", "mood", Enum.GetNames(typeof(MascotMood)), MascotMood.Excited.ToString(), (s, v) =>
                        {
                            if (s.Q<BeguChan>() is { } mascot && Enum.TryParse<MascotMood>(v, out var mood))
                                mascot.mood = mood;
                        }),
                        Bool("blink", "blink", true, (s, v) =>
                        {
                            if (s.Q<BeguChan>() is { } mascot)
                                mascot.blink = v;
                        }));
                case "svg-vectorimage":
                    return P(Colour("tint", "-unity-background-image-tint-color", Color.white, (s, v) =>
                        s.Query(className: "bgfs-icon").ForEach(icon => icon.style.unityBackgroundImageTintColor = (Color)v)));
                case "mesh-modifiers":
                    return P(Float("amplitude", "amplitude", 5f, 0f, 14f, (s, v) => Wave(s, w => w.amplitude = Convert.ToSingle(v))),
                        Float("speed", "speed", 4f, 0f, 12f, (s, v) => Wave(s, w => w.speed = Convert.ToSingle(v))),
                        Float("wavelength", "wavelength", 110f, 20f, 240f, (s, v) => Wave(s, w => w.wavelength = Convert.ToSingle(v))));

                // ------------------------------------------------------------------ Text and localization
                case "display-text":
                    return P(Text("text", "text", "LEVEL UP!", (s, v) =>
                    {
                        if (s.Q<Label>() is { } label)
                            label.text = v;
                    }));
                case "advanced-text-fallback":
                    return P(Text("text", "text", "メロンパン、たい焼き、どら焼き 🥯 🍞", (s, v) =>
                    {
                        var labels = s.Query<Label>().ToList();
                        if (labels.Count > 1)
                            labels[1].text = v;
                    }, "Kana, kanji and emoji from the fallback fonts"));
                case "zero-alloc-settext":
                    return P(Bool("running", "Counting", true, (s, v) =>
                    {
                        if (s.userData is IVisualElementScheduledItem counter)
                        {
                            if (v)
                                counter.Resume();
                            else
                                counter.Pause();
                        }
                    }));
                case "localizedstring-uxml":
                case "locale-switching":
                case "per-language-typography":
                case "third-locale-french":
                case "localizedstring-csharp":
                case "resource-tables":
                    return P(Locale());
                case "smart-strings":
                    return P(Int("count", "{count}", 2, 0, 6, (s, v) =>
                    {
                        if (s.userData is (IntVariable count, Unity.Localization.LocalizedString text))
                        {
                            count.Value = Convert.ToInt32(v);
                            text.RefreshString();
                        }
                    }, "An IntVariable in the LocalizedString's local variables"), Locale());

                // ------------------------------------------------------------------ Animation
                case "uss-transitions":
                    return P(Bool("moved", ".bgfs-slide--moved", false, (s, v) => s.Q(className: "bgfs-slide")?.EnableInClassList("bgfs-slide--moved", v),
                            "A class with other values: the transition animates to them"),
                        Float("duration", "transition-duration", 0.45f, 0.05f, 2f, (s, v) =>
                        {
                            var box = s.Q(className: "bgfs-slide");
                            if (box != null)
                                box.style.transitionDuration = new List<TimeValue> { new(Convert.ToSingle(v), TimeUnit.Second) };
                        }));
                case "uianimationclip-uss":
                    return P(Bool("playing", "animation", true, (s, v) =>
                    {
                        s.Q<BeguChan>()?.EnableInClassList("bgfs-anim--bob", v);
                        s.Q<Lantern>()?.EnableInClassList("bgfs-anim--sway", v);
                    }, "The classes that set animation: url(\"…asset\")"));
                case "clip-restart-classes":
                    return Actions(("Play it again", "Take the class off, put it back a frame later", s =>
                    {
                        var pop = s.Q<Label>("pop-label");
                        if (pop == null)
                            return;
                        pop.RemoveFromClassList("bgfs-pop--play");
                        pop.schedule.Execute(() => pop.AddToClassList("bgfs-pop--play")).ExecuteLater(16);
                    }));

                // ------------------------------------------------------------------ Binding and controls
                case "uxml-databinding":
                    return Actions(("+100 coins", "Change the state: the bound pill follows", _ =>
                    {
                        if (window.state != null)
                            window.state.coins += 100;
                    }));
                case "csharp-bindings":
                {
                    var preview = P(Float("source", "value (the source)", 0.6f, 0f, 1f, null, "The MeterBar's value is bound to this slider's value in C#"));
                    // The binding's source is the control itself: the meter follows it.
                    preview.wire = (s, field) =>
                    {
                        if (s.Q<MeterBar>("bound-meter") is { } meter && field("source") is Slider slider)
                        {
                            meter.SetBinding(nameof(MeterBar.value), new DataBinding
                            {
                                dataSource = slider,
                                dataSourcePath = new PropertyPath(nameof(Slider.value)),
                                bindingMode = BindingMode.ToTarget,
                            });
                        }
                    };
                    return preview;
                }
                case "uxml-element-controls":
                    return P(Int("value", "value (coins)", 1250, 0, 99999, (s, v) =>
                        {
                            if (s.Q<CurrencyPill>() is { } pill)
                                pill.value = Convert.ToInt64(v);
                        }, "A [UxmlAttribute, CreateProperty]"),
                        Float("pips", "value (pips)", 3f, 0f, 5f, (s, v) =>
                        {
                            if (s.Q<PipMeter>() is { } pips)
                                pips.value = Convert.ToSingle(v);
                        }));
                case "ui-components":
                    return Actions(("+1 badge", "NotificationBadge.count, then MarkComponentDirty", s => BagelFeatureSamples.AddToBadge(s.Q<Button>("badge-button"), 1)),
                        ("Clear", "count = 0", s => BagelFeatureSamples.AddToBadge(s.Q<Button>("badge-button"), -999)));
            }
            return new Preview();

            Control Locale()
            {
                var current = Array.IndexOf(s_LocaleCodes, BagelLocalization.localeCode);
                return Choice("locale", "Locale", s_LocaleNames, s_LocaleNames[Mathf.Max(0, current)],
                    (_, v) => BagelLocalization.SetLocale(s_LocaleCodes[Mathf.Max(0, Array.IndexOf(s_LocaleNames, v))]),
                    "LocalizationSettings.SelectedLocale: every LocalizedString follows");
            }
        }

        // ------------------------------------------------------------------ what some controls set

        static FilterFunction Filter(FilterFunctionType type, params float[] values)
        {
            var filter = new FilterFunction(type);
            foreach (var value in values)
                filter.AddParameter(new FilterParameter(value));
            return filter;
        }

        /// <summary>The samples' hard shadow, from the drop-shadow controls (kept on the sample).</summary>
        static void DropShadow(VisualElement sample)
        {
            var values = Values(sample);
            var shadow = new FilterFunction(FilterFunctionType.DropShadow);
            shadow.AddParameter(new FilterParameter(0f));
            shadow.AddParameter(new FilterParameter(Get(values, "offset-y", 4f)));
            shadow.AddParameter(new FilterParameter(Get(values, "blur", 0f)));
            shadow.AddParameter(new FilterParameter(values.TryGetValue("color", out var c) && c is Color color ? color : ArtKit.Ink));
            sample.Query(className: "bgfs-shadow--hard").ForEach(e => e.style.filter = new List<FilterFunction> { shadow });
        }

        static readonly Color k_BoxShadowColor = ArtKit.Rgb(59, 31, 20, 0.6f);

        /// <summary>The box-shadow sample's shadow, from its controls, and the same shadow as its drop-shadow() twin.</summary>
        static void SetBoxShadow(VisualElement sample)
        {
            var values = Values(sample);
            var offset = new Vector2(Get(values, "offset-x", 0f), Get(values, "offset-y", 8f));
            var blur = Get(values, "blur", 16f);
            var color = values.TryGetValue("color", out var c) && c is Color colour ? colour : k_BoxShadowColor;
            var shadow = new BoxShadow
            {
                offset = offset,
                blurRadius = blur,
                spreadRadius = Get(values, "spread", 0f),
                color = color,
                inset = values.TryGetValue("inset", out var i) && i is bool inset && inset,
            };
            sample.Query(className: "bgfs-box-shadow").ForEach(e => e.style.boxShadow = shadow);

            // drop-shadow() takes a sigma, half the blur radius, and has no spread or inset.
            var twin = new FilterFunction(FilterFunctionType.DropShadow);
            twin.AddParameter(new FilterParameter(offset.x));
            twin.AddParameter(new FilterParameter(offset.y));
            twin.AddParameter(new FilterParameter(blur * 0.5f));
            twin.AddParameter(new FilterParameter(color));
            sample.Query(className: "bgfs-drop-twin").ForEach(e => e.style.filter = new List<FilterFunction> { twin });
        }

        static FilterFunctionDefinition s_Halftone;

        static void Halftone(VisualElement sample)
        {
            s_Halftone ??= AssetDatabase.LoadAssetAtPath<FilterFunctionDefinition>(k_HalftonePath);
            var box = sample.Q(className: "bgfs-halftone");
            if (box == null || s_Halftone == null)
                return;
            var values = Values(sample);
            var filter = new FilterFunction(s_Halftone);
            filter.AddParameter(new FilterParameter(Get(values, "spacing", 6f)));
            filter.AddParameter(new FilterParameter(values.TryGetValue("ink", out var c) && c is Color color ? color : ArtKit.Rgb(120, 30, 20)));
            box.style.filter = new List<FilterFunction> { filter };
        }

        static void Wave(VisualElement sample, Action<WavyTextSettings> change)
        {
            var label = sample.Q<Label>(className: "bgfs-wavy");
            if (label == null || !label.HasComponent<WavyText>())
                return;
            ref var wave = ref label.GetComponent<WavyText>();
            var settings = new WavyTextSettings { amplitude = wave.amplitude, speed = wave.speed, wavelength = wave.wavelength };
            change(settings);
            wave.amplitude = settings.amplitude;
            wave.speed = settings.speed;
            wave.wavelength = settings.wavelength;
        }

        sealed class WavyTextSettings
        {
            public float amplitude, speed, wavelength;
        }

        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, Dictionary<string, object>> s_Values = new();

        /// <summary>The values the controls set on a sample, for the ones that set several at once (a filter's parameters).</summary>
        public static Dictionary<string, object> Values(VisualElement sample) => s_Values.GetValue(sample, _ => new Dictionary<string, object>());

        static float Get(Dictionary<string, object> values, string name, float fallback) =>
            values.TryGetValue(name, out var v) && v != null ? Convert.ToSingle(v) : fallback;

        // ------------------------------------------------------------------ making them

        static Preview P(params Control[] controls) => new() { controls = controls };

        static Preview Actions(params (string label, string tooltip, Action<VisualElement> run)[] actions) => new() { actions = actions };

        // Each setter also records its value on the sample, for the ones that read the others (Values).
        static Action<VisualElement, object> Recorded(string name, Action<VisualElement, object> set) => (sample, v) =>
        {
            Values(sample)[name] = v;
            set?.Invoke(sample, v);
        };

        static Control Bool(string name, string label, bool value, Action<VisualElement, bool> set, string tooltip = null) =>
            new() { name = name, label = label, tooltip = tooltip, target = Target.Attribute, type = ValueType.Bool, value = value, set = Recorded(name, (s, v) => set(s, (bool)v)) };

        static Control Int(string name, string label, int value, int min, int max, Action<VisualElement, object> set, string tooltip = null) =>
            new() { name = name, label = label, tooltip = tooltip, target = Target.Attribute, type = ValueType.Int, value = value, min = min, max = max, set = Recorded(name, set) };

        static Control Float(string name, string label, float value, float min, float max, Action<VisualElement, object> set, string tooltip = null) =>
            new() { name = name, label = label, tooltip = tooltip, target = Target.Style, type = ValueType.Float, value = value, min = min, max = max, set = Recorded(name, set) };

        static Control Text(string name, string label, string value, Action<VisualElement, string> set, string tooltip = null) =>
            new() { name = name, label = label, tooltip = tooltip, target = Target.Attribute, type = ValueType.Text, value = value, set = Recorded(name, (s, v) => set(s, (string)v)) };

        static Control Choice(string name, string label, string[] choices, string value, Action<VisualElement, string> set, string tooltip = null) =>
            new() { name = name, label = label, tooltip = tooltip, target = Target.Attribute, type = ValueType.Choice, value = value, choices = choices, set = Recorded(name, (s, v) => set(s, (string)v)) };

        static Control Colour(string name, string label, Color value, Action<VisualElement, object> set, string tooltip = null) =>
            new() { name = name, label = label, tooltip = tooltip, target = Target.Style, type = ValueType.Color, value = value, set = Recorded(name, set) };

        /// <summary>A USS custom property, set in a style sheet on the sample (null: the season's value).</summary>
        static Control Custom(string property, string label, Color fallback) =>
            new() { name = property, label = label, tooltip = property, target = Target.CustomProperty, type = ValueType.Color, fallback = fallback };

        static Control Keyword(string property, string label, string[] choices, string fallback) =>
            new() { name = property, label = label, tooltip = property, target = Target.CustomProperty, type = ValueType.Choice, choices = choices, fallback = fallback };
    }
}
