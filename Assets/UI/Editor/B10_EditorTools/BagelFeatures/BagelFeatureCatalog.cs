using System;
using System.Collections.Generic;
using System.Linq;

namespace Bagel
{
    /// <summary>
    /// Every UI Toolkit feature Bagel Express uses: what it is, how the game uses it, and the files it lives in
    /// (BagelFeatureCatalog.Features.cs). The Bagel Features window reads it.
    ///
    /// Sources point at a line by its text (<see cref="Source.find"/>), not its number, so they keep pointing at the right
    /// line as files change; the catalog's tests check that every one is still there.
    /// </summary>
    public static partial class BagelFeatureCatalog
    {
        public sealed class Source
        {
            public readonly string path;
            public readonly string find;
            public readonly string what;

            public Source(string path, string find, string what)
            {
                this.path = path;
                this.find = find;
                this.what = what;
            }

            public EditorLink link => EditorLink.File(path, find);
        }

        public sealed class Feature
        {
            public readonly string id;
            public readonly string since;
            public readonly string title;
            /// <summary>What the feature is, in UI Toolkit.</summary>
            public readonly string about;
            /// <summary>How Bagel Express uses it.</summary>
            public readonly string summary;
            public readonly Source[] sources;
            public readonly EditorLink[] links;

            /// <summary>False for a feature Bagel Express doesn't use (<see cref="unused"/>): it has no sources, parts or samples.</summary>
            public readonly bool isUsed;

            public Feature(string id, string since, string title, string about, string summary, Source[] sources, EditorLink[] links,
                bool used = true)
            {
                this.id = id;
                isUsed = used;
                this.since = since;
                this.title = title;
                this.about = about;
                this.summary = summary;
                this.sources = sources;
                this.links = links;
            }

            public bool isExperimental => since.StartsWith("exp", StringComparison.Ordinal);

            /// <summary>Everything the search looks at, lower case.</summary>
            public string searchText => m_SearchText ??= string.Join("\n",
                new[] { id, title, about, summary, since }
                    .Concat(sources.SelectMany(s => new[] { s.path, s.what }))
                    .Concat(links.Select(l => l.label))).ToLowerInvariant();
            string m_SearchText;
        }

        static Feature[] s_Features;
        static Dictionary<string, Feature> s_ById;

        public static IReadOnlyList<Feature> features
        {
            get
            {
                if (s_Features == null)
                {
                    s_Features = CreateFeatures();
                    s_ById = s_Features.ToDictionary(f => f.id);
                }
                return s_Features;
            }
        }

        /// <summary>A feature by id, the game's or one it doesn't use.</summary>
        public static Feature Get(string id) =>
            id != null && features.Count > 0 && (s_ById.TryGetValue(id, out var feature) || s_UnusedById.TryGetValue(id, out feature)) ? feature : null;

        /// <summary>The 6.4–7.x features Bagel Express doesn't use (BagelFeatureCatalog.Unused.cs), so you know they exist.</summary>
        public static IReadOnlyList<Feature> unused => s_Unused ??= CreateUnused();

        // Lazy, not field initializers: static fields of partial files initialize in no set order.
        static Feature[] s_Unused;
        static Dictionary<string, Feature> s_UnusedByIdCache;
        static Dictionary<string, Feature> s_UnusedById => s_UnusedByIdCache ??= unused.ToDictionary(f => f.id);

        // ------------------------------------------------------------------ helpers for the data files

        static Feature F(string id, string since, string title, string about, string summary, params object[] items) =>
            new(id, since, title, about, summary, items.OfType<Source>().ToArray(), items.OfType<EditorLink>().ToArray());

        static Source Src(string path, string find, string what) => new(path, find, what);

        /// <summary>A feature the game doesn't use: what it is, and what you'd use it for (its summary).</summary>
        static Feature Unused(string id, string since, string title, string about, string usage) =>
            new(id, since, title, about, usage, Array.Empty<Source>(), Array.Empty<EditorLink>(), used: false);
    }
}
