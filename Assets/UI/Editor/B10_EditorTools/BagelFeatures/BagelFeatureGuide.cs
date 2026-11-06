using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// What the Bagel Features window adds to the feature catalog (<see cref="BagelFeatureCatalog"/>): each
    /// feature's Unity version group and category, short code snippets (BagelFeatureSnippets.txt), and how the key parts
    /// of each game screen are made (BagelFeatureGuide.Parts.cs).
    /// </summary>
    public static partial class BagelFeatureGuide
    {
        public const string snippetsPath = "Assets/UI/Editor/B10_EditorTools/BagelFeatures/BagelFeatureSnippets.txt";
        const string k_RuntimeUIFolder = "Assets/UI/Runtime";

        // ------------------------------------------------------------------ groups

        public sealed class Group
        {
            public readonly string id;
            public readonly string title;
            /// <summary>The name on the list's filter pills.</summary>
            public readonly string shortTitle;
            public readonly string subtitle;
            public readonly Color color;

            public Group(string id, string title, string shortTitle, string subtitle, string hex)
            {
                this.id = id;
                this.title = title;
                this.shortTitle = shortTitle;
                this.subtitle = subtitle;
                ColorUtility.TryParseHtmlString(hex, out color);
            }
        }

        /// <summary>
        /// The Unity versions a feature is listed under: 6.3 (and earlier), 6.6 (6.4 to 6.6), 6.7, 7.x (experimental in
        /// 6.7), and Other (separate products, none of them used).
        /// </summary>
        public static readonly IReadOnlyList<Group> versions = new[]
        {
            new Group("6.3", "Unity 6.3", "6.3", "LTS: everything up to 6.3", "#6f86a6"),
            new Group("6.6", "Unity 6.6", "6.6", "Released in 6.4, 6.5 and 6.6", "#1fa7c4"),
            new Group("6.7", "Unity 6.7", "6.7", "New in 6.7", "#f0a020"),
            new Group("7.x", "Unity 7.x", "7.x", "Experimental in 6.7, behind a Project Settings › UI Toolkit flag", "#e0569b"),
            new Group("other", "Other", "Other", "Separate products", "#8a7a6c"),
        };

        public static Group VersionOf(BagelFeatureCatalog.Feature feature)
        {
            if (feature.isExperimental)
                return versions[3];
            if (feature.since == "external")
                return versions[4];
            return feature.since switch
            {
                "6.7" => versions[2],
                "6.4" or "6.5" or "6.6" => versions[1],
                _ => versions[0],
            };
        }

        /// <summary>The release a feature came with, as the version chip's tooltip says it.</summary>
        public static string SinceText(BagelFeatureCatalog.Feature feature) =>
            feature.isExperimental ? "Experimental in Unity 6.7 (Project Settings › UI Toolkit)"
            : feature.since == "core" ? "In UI Toolkit before 6.3"
            : feature.since == "external" ? "A separate product"
            : "Released in Unity " + feature.since;

        /// <summary>Every feature the window lists: the game's, then the ones it doesn't use.</summary>
        public static IEnumerable<BagelFeatureCatalog.Feature> allFeatures => BagelFeatureCatalog.features.Concat(BagelFeatureCatalog.unused);

        public static readonly IReadOnlyList<Group> categories = new[]
        {
            new Group("authoring", "Authoring & controls", "Authoring", "UXML, templates, custom controls and components", "#2b8fd8"),
            new Group("styling", "Styling & themes", "Styling", "USS, variables, theme style sheets", "#8e5cd9"),
            new Group("layout", "Layout", "Layout", "Flexbox, grid, gap, z-index, transforms", "#2fa58a"),
            new Group("effects", "Filters & effects", "Effects", "Gradients, shadows, filters, glass", "#e0567a"),
            new Group("vector", "Vector art", "Vector", "Painter2D, generated meshes, SVG", "#e88a1a"),
            new Group("text", "Text & localization", "Text", "Fonts, text effects, strings in three languages", "#c2452d"),
            new Group("animation", "Animation", "Animation", "Transitions and UIAnimationClips", "#d4a017"),
            new Group("binding", "Data binding", "Binding", "Runtime bindings from UXML and C#", "#1f9fb0"),
            new Group("runtime", "Runtime", "Runtime", "Panels, input, events, scheduling, accessibility", "#4c9a2a"),
            new Group("performance", "Performance", "Perf", "Profiling, measuring on a device, no-garbage APIs", "#c2389f"),
            new Group("tooling", "Editor tools & tests", "Tools", "Editor windows, UI Builder, live edit, tests", "#7a6a58"),
        };

        public static Group CategoryOf(BagelFeatureCatalog.Feature feature) =>
            s_CategoryOf.TryGetValue(feature.id, out var id) ? categories.First(c => c.id == id) : categories[^1];

        /// <summary>Every feature's category, by feature id.</summary>
        public static IReadOnlyDictionary<string, string> categoryOf => s_CategoryOf;

        // ------------------------------------------------------------------ snippets

        public enum Language { CSharp, Uxml, Uss, Hlsl }

        public sealed class Snippet
        {
            public readonly string feature;
            public readonly Language language;
            public readonly string caption;
            public readonly string code;

            public Snippet(string feature, Language language, string caption, string code)
            {
                this.feature = feature;
                this.language = language;
                this.caption = caption;
                this.code = code;
            }

            public string languageLabel => language switch { Language.Uxml => "UXML", Language.Uss => "USS", Language.Hlsl => "HLSL", _ => "C#" };
        }

        static Dictionary<string, List<Snippet>> s_Snippets;

        /// <summary>
        /// The snippets of a feature. BagelFeatureSnippets.txt holds them all: each starts with a line
        /// <c>## feature-id | cs|uxml|uss|hlsl | caption</c>, and runs to the next one.
        /// </summary>
        public static IReadOnlyList<Snippet> SnippetsOf(string featureId)
        {
            s_Snippets ??= LoadSnippets();
            return s_Snippets.TryGetValue(featureId, out var list) ? list : (IReadOnlyList<Snippet>)Array.Empty<Snippet>();
        }

        public static IEnumerable<Snippet> allSnippets
        {
            get
            {
                s_Snippets ??= LoadSnippets();
                return s_Snippets.Values.SelectMany(s => s);
            }
        }

        /// <summary>Read the snippets file again (after an edit).</summary>
        public static void ReloadSnippets() => s_Snippets = null;

        static Dictionary<string, List<Snippet>> LoadSnippets()
        {
            var snippets = new Dictionary<string, List<Snippet>>();
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(snippetsPath);
            if (asset == null)
                return snippets;

            string feature = null, caption = null;
            var language = Language.CSharp;
            var lines = new List<string>();
            void Flush()
            {
                if (feature == null)
                    return;
                while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
                    lines.RemoveAt(lines.Count - 1);
                if (!snippets.TryGetValue(feature, out var list))
                    snippets[feature] = list = new List<Snippet>();
                list.Add(new Snippet(feature, language, caption, string.Join("\n", lines)));
                lines.Clear();
            }

            foreach (var raw in asset.text.Replace("\r", "").Split('\n'))
            {
                if (raw.StartsWith("## ", StringComparison.Ordinal))
                {
                    Flush();
                    var fields = raw.Substring(3).Split('|').Select(f => f.Trim()).ToArray();
                    feature = fields[0];
                    language = fields.Length > 1 ? ParseLanguage(fields[1]) : Language.CSharp;
                    caption = fields.Length > 2 && fields[2].Length > 0 ? fields[2] : null;
                    continue;
                }
                if (feature == null || (lines.Count == 0 && string.IsNullOrWhiteSpace(raw)))
                    continue;
                lines.Add(raw);
            }
            Flush();
            return snippets;
        }

        static Language ParseLanguage(string text) => text.ToLowerInvariant() switch
        {
            "uxml" => Language.Uxml,
            "uss" or "tss" => Language.Uss,
            "hlsl" => Language.Hlsl,
            _ => Language.CSharp,
        };

        // ------------------------------------------------------------------ screen parts

        /// <summary>
        /// A key part of a game screen: the element, the feature it shows, and how it's made. Its dot on the screen and its
        /// entry in the window share a number.
        /// </summary>
        public sealed class Part
        {
            public readonly string feature;
            /// <summary>The element on the live screen (<see cref="GameScreen.Find"/> selector).</summary>
            public readonly string selector;
            public readonly string title;
            public readonly string how;
            /// <summary>
            /// The element that stands for it in the scene's UI and in UI Builder, when the part is made in code (list
            /// items, cards, overlays): it's authored in the app's UXML. Null: the selector itself.
            /// </summary>
            public readonly string host;
            /// <summary>The UXML the part is authored in, when it isn't the one its host comes from (a card template). Null: found from the host.</summary>
            public readonly string uxml;
            /// <summary>The C# that drives it. Null: the feature's first C# source.</summary>
            public readonly EditorLink code;

            public Part(string feature, string selector, string title, string how, string host, string uxml, EditorLink code)
            {
                this.feature = feature;
                this.selector = selector;
                this.title = title;
                this.how = how;
                this.host = host;
                this.uxml = uxml;
                this.code = code;
            }

            public BagelFeatureCatalog.Feature featureInfo => BagelFeatureCatalog.Get(feature);
            public string sceneSelector => host ?? selector;
        }

        public sealed class ScreenParts
        {
            public readonly GameScreen screen;
            public readonly Part[] parts;

            public ScreenParts(GameScreen screen, params Part[] parts)
            {
                this.screen = screen;
                this.parts = parts;
            }
        }

        static Part P(string feature, string selector, string title, string how, string host = null, string uxml = null, EditorLink code = null) =>
            new(feature, selector, title, how, host, uxml, code);

        static EditorLink Code(string path, string find) => EditorLink.File(path, find, "C#");

        /// <summary>Every place on the screens where a feature shows, with the screen it's on.</summary>
        public static IEnumerable<(ScreenParts screen, Part part)> PartsOf(string featureId) =>
            screens.SelectMany(s => s.parts.Where(p => p.feature == featureId).Select(p => (s, p)));

        // ------------------------------------------------------------------ where a part lives in the project

        static TemplateContainer s_AppInstance;

        /// <summary>A raw instance of the app's UXML (no panel, nothing running): to find where elements are authored.</summary>
        public static TemplateContainer appInstance =>
            s_AppInstance ??= AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(BagelExpressAllUIWindow.appUxmlPath)?.Instantiate();

        /// <summary>The UXML a part is authored in: its own, or the one its host element was cloned from.</summary>
        public static string UxmlOf(Part part)
        {
            if (part.uxml != null)
                return part.uxml;
            var element = GameScreen.Find(appInstance, part.sceneSelector);
            return element?.visualTreeAssetSource != null ? AssetDatabase.GetAssetPath(element.visualTreeAssetSource) : null;
        }

        /// <summary>The element name UI Builder selects for a part, if it has one.</summary>
        public static string UIBuilderSelection(Part part)
        {
            var selector = part.uxml != null ? part.selector : part.sceneSelector;
            var last = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            var at = last.IndexOf('@');
            if (at > 0)
                last = last.Substring(0, at);
            return last.StartsWith(".") ? null : last.TrimStart('#');
        }

        static Dictionary<string, string> s_StyleSheetText;
        static readonly Dictionary<string, TemplateContainer> s_TemplateInstances = new();

        /// <summary>
        /// The USS rule for a part: the first rule on its name or one of its classes, looked for in the style sheets of
        /// its own UXML, then the common styles, then the game's other style sheets (not the A/B variants or sounds).
        /// </summary>
        public static EditorLink UssOf(Part part)
        {
            s_StyleSheetText ??= Directory.GetFiles(k_RuntimeUIFolder, "*.uss", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !IsVariantStyleSheet(p))
                .OrderBy(p => p, StringComparer.Ordinal)
                .ToDictionary(p => p, File.ReadAllText);

            var tokens = new List<string>();
            var last = LastToken(part.selector);
            tokens.Add(last.StartsWith(".") || last.StartsWith("#") ? last : "#" + last);
            var element = FindAuthored(part);
            if (element != null)
                foreach (var className in element.GetClasses())
                    if (!className.StartsWith("unity-", StringComparison.Ordinal) && !tokens.Contains("." + className))
                        tokens.Add("." + className);

            var sheets = new List<string>();
            var uxml = UxmlOf(part);
            var tree = uxml != null ? AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxml) : null;
            if (tree != null)
                sheets.AddRange(tree.stylesheets.Where(sheet => sheet != null).Select(AssetDatabase.GetAssetPath));
            sheets.Add(k_RuntimeUIFolder + "/B00_Themes/T0_Common/BagelExpressCommonStyles.uss");
            sheets.AddRange(s_StyleSheetText.Keys);

            // A rule that starts with the name or class first (".be-shop-card {"), then any that has it.
            foreach (var startsWith in new[] { true, false })
                foreach (var token in tokens)
                    foreach (var path in sheets.Distinct())
                        if (s_StyleSheetText.TryGetValue(path, out var text) && FindRuleLine(text, token, startsWith) is { } line)
                            return EditorLink.File(path, line, "USS");
            return null;
        }

        static bool IsVariantStyleSheet(string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            return name.EndsWith("AB", StringComparison.Ordinal) || name.StartsWith("BagelNo", StringComparison.Ordinal) || name == "BagelSounds";
        }

        static string LastToken(string selector)
        {
            var last = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            var at = last.IndexOf('@');
            return at > 0 ? last.Substring(0, at) : last;
        }

        /// <summary>The part's element as authored: in the app's UXML, or in its own template.</summary>
        static VisualElement FindAuthored(Part part)
        {
            if (part.uxml == null)
                return GameScreen.Find(appInstance, part.selector);
            if (!s_TemplateInstances.TryGetValue(part.uxml, out var instance))
                s_TemplateInstances[part.uxml] = instance = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(part.uxml)?.Instantiate();
            return instance == null ? null : GameScreen.Find(instance, LastToken(part.selector)) ?? (instance.ClassListContains(LastToken(part.selector).TrimStart('.')) ? instance : null);
        }

        /// <summary>
        /// The text of the first selector line of a rule that names <paramref name="token"/> (a whole name, not the start of a
        /// longer one); with <paramref name="startsWith"/>, only a line that starts with it.
        /// </summary>
        static string FindRuleLine(string text, string token, bool startsWith)
        {
            for (var index = text.IndexOf(token, StringComparison.Ordinal); index >= 0; index = text.IndexOf(token, index + 1, StringComparison.Ordinal))
            {
                var end = index + token.Length;
                var next = end < text.Length ? text[end] : ' ';
                if (char.IsLetterOrDigit(next) || next == '-' || next == '_')
                    continue;
                var lineStart = text.LastIndexOf('\n', index) + 1;
                var lineEnd = text.IndexOf('\n', index);
                var line = text.Substring(lineStart, (lineEnd < 0 ? text.Length : lineEnd) - lineStart).TrimEnd();
                if ((line.EndsWith("{") || line.EndsWith(",")) && (!startsWith || line.TrimStart().StartsWith(token, StringComparison.Ordinal)))
                    return line.Trim();
            }
            return null;
        }

        /// <summary>The C# that drives a part: its own, or its feature's first C# source.</summary>
        public static EditorLink CodeOf(Part part)
        {
            if (part.code != null)
                return part.code;
            var source = part.featureInfo?.sources.FirstOrDefault(s => s.path.EndsWith(".cs", StringComparison.Ordinal));
            return source != null ? EditorLink.File(source.path, source.find, "C#") : null;
        }
    }
}
