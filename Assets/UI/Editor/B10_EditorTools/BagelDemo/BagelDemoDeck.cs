using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The Bagel Demo window's deck: the 30-minute talk as chapters, one slide each, each one presenter's, in the order of
    /// what's new: 6.7's in-scene workflow first (it's how everything after is shown), then 6.7's features and 7.x, then
    /// Bagel Features. After the game is introduced, the talk stays in Play mode until the wrap-up stops it. A chapter is a few beats; a beat is one thing done in one place of the Editor, shown as a picture (the
    /// Editor's windows recreated, the real game UI) with a few words. The content is in BagelDemoDeck.Chapters.cs.
    /// </summary>
    public static partial class BagelDemoDeck
    {
        public const int talkSeconds = 30 * 60;

        public enum Presenter { Designer, Developer }

        /// <summary>
        /// Where a beat happens: the demo layout's tabs (Assets/Settings/Layouts/BagelExpressDemo.wlt), the main toolbar,
        /// the Bagel menu, and the windows that open on top of the layout.
        /// </summary>
        public enum Area
        {
            Toolbar, Menu,
            Hierarchy, StyleSheets,
            Project, UILibrary,
            Scene, UIViewport, Animation, Profiler, BagelFeatures, Console,
            Inspector, Game,
            UIBuilder, ResourceTables, BagelUI,
        }

        public sealed class PresenterInfo
        {
            public readonly string name;
            public readonly string glyph;
            public readonly Color color;
            public readonly string role;

            public PresenterInfo(string name, string glyph, string hex, string role)
            {
                this.name = name;
                this.glyph = glyph;
                this.role = role;
                ColorUtility.TryParseHtmlString(hex, out color);
            }
        }

        public static readonly PresenterInfo designer = new("Designer", "✎", "#8b45d9",
            "Plays the game, changes it in the Editor without code, wants what's next, closes the talk.");
        public static readonly PresenterInfo developer = new("Developer", "</>", "#2f9e44",
            "Shows where the UI lives, what each feature costs and how it's built.");

        public static PresenterInfo Info(Presenter presenter) => presenter == Presenter.Designer ? designer : developer;

        /// <summary>What the Editor is set to when a chapter starts: the demo toolbar's screen, and Play mode.</summary>
        public sealed class Setup
        {
            /// <summary>The demo toolbar's Screen (null: as authored, the title over Home).</summary>
            public readonly GameScreen screen;
            public readonly bool play;
            /// <summary>Anything else, in a few words (a window to open first).</summary>
            public readonly string note;

            public Setup(GameScreen screen, bool play = false, string note = null)
            {
                this.screen = screen;
                this.play = play;
                this.note = note;
            }
        }

        public sealed class Beat
        {
            /// <summary>A few words under the picture (&lt;b&gt; marks what to look at).</summary>
            public readonly string caption;
            /// <summary>What exactly to do, as the picture's tooltip.</summary>
            public readonly string hint;
            public readonly Area[] areas;
            /// <summary>The demo toolbar's Screen to pick first (null: it stays).</summary>
            public readonly GameScreen screen;
            public readonly bool play;
            public readonly Func<BagelDemoVisuals, VisualElement> visual;
            /// <summary>If lost: puts the Editor in this beat's state.</summary>
            public readonly EditorLink link;
            public Chapter chapter { get; internal set; }
            public int number { get; internal set; }

            public Beat(string caption, string hint, Area[] areas, Func<BagelDemoVisuals, VisualElement> visual, GameScreen screen, bool play, EditorLink link)
            {
                this.caption = caption;
                this.hint = hint;
                this.areas = areas ?? Array.Empty<Area>();
                this.visual = visual;
                this.screen = screen;
                this.play = play;
                this.link = link;
            }
        }

        public enum Layout { Grid, Strip }

        public sealed class Chapter
        {
            public readonly string id;
            public readonly string title;
            public readonly string japaneseTitle;
            public readonly Presenter presenter;
            public readonly int seconds;
            /// <summary>The Unity versions it shows: "6.6", "6.7", "7.x" (BagelFeatureGuide.versions).</summary>
            public readonly string[] versions;
            public readonly Setup setup;
            /// <summary>The one idea to say.</summary>
            public readonly string line;
            /// <summary>The cue to hand over to the next presenter (null for the last chapter).</summary>
            public readonly string handoff;
            public readonly Beat[] beats;
            public readonly Layout layout;
            public int number { get; internal set; }
            public int start { get; internal set; }

            public Chapter(string id, string title, string japaneseTitle, Presenter presenter, int seconds, string[] versions, Setup setup,
                string line, string handoff, Beat[] beats, Layout layout)
            {
                this.id = id;
                this.title = title;
                this.japaneseTitle = japaneseTitle;
                this.presenter = presenter;
                this.seconds = seconds;
                this.versions = versions ?? Array.Empty<string>();
                this.setup = setup;
                this.line = line;
                this.handoff = handoff;
                this.beats = beats;
                this.layout = layout;
            }

            /// <summary>Every place the chapter happens in, in the order its beats get there.</summary>
            public IEnumerable<Area> areas => beats.SelectMany(b => b.areas).Distinct();
        }

        static Chapter[] s_Chapters;

        public static IReadOnlyList<Chapter> chapters
        {
            get
            {
                if (s_Chapters == null)
                {
                    var chapters = CreateChapters();
                    var start = 0;
                    for (int i = 0; i < chapters.Length; i++)
                    {
                        var chapter = chapters[i];
                        chapter.number = i + 1;
                        chapter.start = start;
                        start += chapter.seconds;
                        for (int b = 0; b < chapter.beats.Length; b++)
                        {
                            chapter.beats[b].chapter = chapter;
                            chapter.beats[b].number = b + 1;
                        }
                    }
                    s_Chapters = chapters;
                }
                return s_Chapters;
            }
        }

        public static int totalSeconds => chapters.Sum(c => c.seconds);

        /// <summary>A length as minutes: "4 min", "3½ min".</summary>
        public static string Minutes(int seconds)
        {
            var minutes = seconds / 60;
            var half = seconds % 60 >= 30;
            return (minutes > 0 ? minutes.ToString() : "") + (half ? "½" : "") + " min";
        }

        public static string Clock(int seconds) => $"{seconds / 60:00}:{seconds % 60:00}";

        // ------------------------------------------------------------------ building blocks for BagelDemoDeck.Chapters.cs

        static Chapter C(string id, string title, string japaneseTitle, Presenter presenter, int seconds, string[] versions, Setup setup,
            string line, string handoff, params Beat[] beats) =>
            new(id, title, japaneseTitle, presenter, seconds, versions, setup, line, handoff, beats, Layout.Grid);

        static Chapter Strip(string id, string title, string japaneseTitle, Presenter presenter, int seconds, string[] versions, Setup setup,
            string line, string handoff, params Beat[] beats) =>
            new(id, title, japaneseTitle, presenter, seconds, versions, setup, line, handoff, beats, Layout.Strip);

        static Beat B(string caption, string hint, Area[] at, Func<BagelDemoVisuals, VisualElement> visual, GameScreen screen = null, bool play = false,
            EditorLink link = null) =>
            new(caption, hint, at, visual, screen, play, link);

        static Area[] At(params Area[] areas) => areas;

        static string[] V(params string[] versions) => versions;
    }
}
