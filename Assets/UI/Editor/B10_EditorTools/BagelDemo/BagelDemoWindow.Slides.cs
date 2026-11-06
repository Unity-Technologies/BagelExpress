using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.BagelDemoDeck;

namespace Bagel
{
    public partial class BagelDemoWindow
    {
        public static readonly string slideUssClassName = "bgd-slide";
        public static readonly string beatUssClassName = "bgd-beat";
        public static readonly string beatVisualUssClassName = beatUssClassName + "__visual";
        public static readonly string cardUssClassName = "bgd-card";
        const int k_MaxBeatsPerRow = 3;

        // ------------------------------------------------------------------ a chapter

        VisualElement BuildChapter(Chapter chapter)
        {
            var slide = Slide(chapter.presenter);
            slide.AddToClassList(slideUssClassName + "--" + chapter.layout.ToString().ToLowerInvariant());
            slide.Add(BuildHead(chapter));

            var body = El("bgd-body");
            var beats = El("bgd-beats");
            m_Beats.Clear();
            var perRow = chapter.layout == Layout.Strip ? chapter.beats.Length : BeatsPerRow(chapter.beats.Length);
            VisualElement row = null;
            for (int i = 0; i < chapter.beats.Length; i++)
            {
                if (i % perRow == 0)
                {
                    row = El("bgd-beats__row");
                    beats.Add(row);
                }
                var beat = BuildBeat(chapter.beats[i]);
                m_Beats.Add(beat);
                row.Add(beat);
            }
            var arrows = new BagelDemoConnectors(m_Beats);
            beats.Add(arrows);

            if (chapter.layout == Layout.Strip)
            {
                body.Add(beats);
                var info = El("bgd-strip-info");
                info.Add(SetupView(chapter));
                info.Add(SayView(chapter));
                body.Add(info);
            }
            else
            {
                var side = El("bgd-side");
                var where = Eyebrow("WHERE · 場所");
                where.AddToClassList("bgd-eyebrow--first");
                side.Add(where);
                side.Add(new BagelDemoMap(chapter, BagelDemoDeck.Info(chapter.presenter).color));
                side.Add(Eyebrow("SET UP · 準備"));
                side.Add(SetupView(chapter));
                side.Add(Eyebrow("SAY · 一言"));
                side.Add(SayView(chapter));
                body.Add(side);
                body.Add(beats);
            }
            slide.Add(body);
            slide.Add(BuildFoot(chapter));
            return slide;
        }

        /// <summary>Two rows when there are more than three beats: 4 → 2 + 2, 5 → 3 + 2, 6 → 3 + 3.</summary>
        static int BeatsPerRow(int count) => count <= k_MaxBeatsPerRow ? count : Mathf.CeilToInt(count / 2f);

        VisualElement BuildHead(Chapter chapter)
        {
            var presenter = BagelDemoDeck.Info(chapter.presenter);
            var head = El("bgd-head");
            var number = Text(chapter.number.ToString(), "bgd-head__number");
            number.style.backgroundColor = presenter.color;
            head.Add(number);
            var titles = El("bgd-head__titles");
            titles.Add(Text(chapter.title, "bgd-head__title"));
            titles.Add(Text(chapter.japaneseTitle, "bgd-head__jp"));
            head.Add(titles);
            head.Add(El("bgd-grow"));
            foreach (var version in chapter.versions)
                head.Add(VersionChip(version, "bgd-version--big"));
            var who = El("bgd-presenter");
            who.style.backgroundColor = presenter.color;
            who.Add(Text(presenter.glyph, "bgd-presenter__glyph"));
            who.Add(Text(presenter.name.ToUpperInvariant(), "bgd-presenter__name"));
            head.Add(who);
            var time = El("bgd-head__time");
            time.Add(Text(BagelDemoDeck.Minutes(chapter.seconds), "bgd-head__minutes"));
            time.Add(Text($"{BagelDemoDeck.Clock(chapter.start)}–{BagelDemoDeck.Clock(chapter.start + chapter.seconds)}", "bgd-head__clock"));
            head.Add(time);
            return head;
        }

        VisualElement BuildBeat(Beat beat)
        {
            var presenter = BagelDemoDeck.Info(beat.chapter.presenter);
            var element = El(beatUssClassName);
            element.name = $"beat-{beat.number}";

            var fit = new BagelDemoFit();
            fit.AddToClassList(beatVisualUssClassName);
            VisualElement visual = null;
            try
            {
                visual = beat.visual?.Invoke(m_Visuals);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Bagel Demo: chapter {beat.chapter.number}, beat {beat.number}'s picture failed: {e.Message}");
            }
            if (visual != null)
                fit.SetContent(visual);
            element.Add(fit);
            // Pictures, not controls: a click doesn't reach the live screens (a hover tells what to do).
            var shield = El("bgd-beat__shield");
            shield.pickingMode = PickingMode.Position;
            shield.tooltip = beat.hint;
            fit.Add(shield);

            var foot = El("bgd-beat__foot");
            var number = Text(beat.number.ToString(), "bgd-beat__number");
            number.style.backgroundColor = presenter.color;
            foot.Add(number);
            // A phone of the strip has little room under it: Play is just its triangle, and no link.
            var strip = beat.chapter.layout == Layout.Strip;
            if (beat.play)
                foot.Add(strip ? Text("▶", "bgd-beat__play") : PlayChip());
            if (beat.screen != null)
                foot.Add(ScreenChip(beat.screen, small: true));
            foot.Add(Text(Emphasis(beat.caption), "bgd-beat__caption"));
            if (beat.link != null && !strip)
            {
                var link = new Button(beat.link.Open) { text = "↗", tooltip = "If lost: " + beat.link.tooltip };
                link.AddToClassList("bgd-beat__link");
                foot.Add(link);
            }
            element.Add(foot);
            return element;
        }

        VisualElement BuildFoot(Chapter chapter)
        {
            var foot = El("bgd-foot");
            var next = chapter.number < BagelDemoDeck.chapters.Count ? BagelDemoDeck.chapters[chapter.number] : null;
            if (next == null)
            {
                foot.Add(Text("ありがとうございました!", "bgd-foot__end"));
                return foot;
            }
            var from = BagelDemoDeck.Info(chapter.presenter);
            var to = BagelDemoDeck.Info(next.presenter);
            var handoff = El("bgd-handoff");
            handoff.Add(Text(chapter.handoff ?? "", "bgd-handoff__line"));
            foot.Add(handoff);
            foot.Add(El("bgd-grow"));
            var nextView = El("bgd-next");
            nextView.Add(Text(from.glyph, "bgd-next__from"));
            var arrow = BagelDemoVisuals.Arrow();
            arrow.AddToClassList("bgd-next__arrow");
            nextView.Add(arrow);
            var disc = Text(next.number.ToString(), "bgd-next__number");
            disc.style.backgroundColor = to.color;
            nextView.Add(disc);
            var label = El("bgd-next__label");
            var who = Text($"{to.glyph} {to.name}", "bgd-next__who");
            who.style.color = to.color;
            label.Add(who);
            label.Add(Text(next.title, "bgd-next__title"));
            nextView.Add(label);
            foot.Add(nextView);
            return foot;
        }

        /// <summary>What to set before the chapter: the demo toolbar's screen, Play mode, and anything else.</summary>
        VisualElement SetupView(Chapter chapter)
        {
            var setup = El("bgd-setup");
            var chips = El("bgd-setup__chips");
            chips.Add(chapter.setup.screen != null ? ScreenChip(chapter.setup.screen, small: false) : AsAuthoredChip());
            chips.Add(chapter.setup.play ? PlayChip() : EditModeChip());
            setup.Add(chips);
            if (!string.IsNullOrEmpty(chapter.setup.note))
                setup.Add(Text(chapter.setup.note, "bgd-setup__note"));
            return setup;
        }

        static VisualElement SayView(Chapter chapter) => Text("“" + chapter.line + "”", "bgd-say");

        /// <summary>The demo toolbar's Screen option, lit, with the screen's name; a click sets the toolbar to it.</summary>
        VisualElement ScreenChip(GameScreen screen, bool small)
        {
            var chip = El("bgd-chip");
            chip.EnableInClassList("bgd-chip--small", small);
            var option = BagelOptionStrip.CreateOption(BagelPickerIcons.Screen(screen, currentTheme), $"Demo toolbar › Screen › {screen.title}\nClick: set it", true,
                () => DemoOverrides.screen = screen);
            BagelOptionStrip.AddStyleSheet(option);
            option.AddToClassList("bgd-chip__option");
            chip.Add(option);
            chip.Add(Text(screen.title, "bgd-chip__label"));
            return chip;
        }

        VisualElement AsAuthoredChip()
        {
            var chip = El("bgd-chip");
            var option = BagelOptionStrip.CreateOption(BagelPickerIcons.AsAuthored(BagelExpressAllUIWindow.appUxmlPath), "Demo toolbar › Screen › As authored\nClick: set it", true,
                () => DemoOverrides.screen = null);
            BagelOptionStrip.AddStyleSheet(option);
            option.AddToClassList("bgd-chip__option");
            chip.Add(option);
            chip.Add(Text("As authored", "bgd-chip__label"));
            return chip;
        }

        VisualElement PlayChip()
        {
            var chip = El("bgd-chip bgd-chip--play");
            chip.tooltip = "Play mode";
            chip.Add(Text("▶", "bgd-chip__play"));
            chip.Add(Text("Play", "bgd-chip__label"));
            return chip;
        }

        VisualElement EditModeChip()
        {
            var chip = El("bgd-chip bgd-chip--edit");
            chip.tooltip = "Edit mode (Play stopped)";
            chip.Add(Text("■", "bgd-chip__play"));
            chip.Add(Text("Edit mode", "bgd-chip__label"));
            return chip;
        }

        // ------------------------------------------------------------------ the overview

        VisualElement BuildOverview()
        {
            var slide = Slide(null);
            slide.AddToClassList(slideUssClassName + "--overview");
            var head = El("bgd-head bgd-head--overview");
            var titles = El("bgd-head__titles");
            titles.Add(Text("Bagel Express", "bgd-head__brand"));
            titles.Add(Text("What's new in UI Toolkit, in one game · ベーグル急行", "bgd-head__jp"));
            head.Add(titles);
            head.Add(El("bgd-grow"));
            foreach (var version in new[] { "6.6", "6.7", "7.x" })
                head.Add(VersionChip(version, "bgd-version--big"));
            var total = El("bgd-head__time");
            total.Add(Text(BagelDemoDeck.Minutes(BagelDemoDeck.totalSeconds), "bgd-head__minutes"));
            total.Add(Text($"{BagelDemoDeck.chapters.Count} chapters", "bgd-head__clock"));
            head.Add(total);
            slide.Add(head);

            var beats = El("bgd-beats bgd-beats--overview");
            m_Beats.Clear();
            var chapters = BagelDemoDeck.chapters;
            var perRow = Mathf.CeilToInt(chapters.Count / 2f);
            VisualElement row = null;
            for (int i = 0; i < chapters.Count; i++)
            {
                if (i % perRow == 0)
                {
                    row = El("bgd-beats__row");
                    beats.Add(row);
                }
                var card = BuildCard(chapters[i]);
                m_Beats.Add(card);
                row.Add(card);
            }
            beats.Add(new BagelDemoConnectors(m_Beats));
            slide.Add(beats);

            var foot = El("bgd-foot bgd-foot--overview");
            foot.Add(BuildTimeline());
            var before = El("bgd-before");
            before.Add(Text("BEFORE", "bgd-eyebrow"));
            before.Add(Text("Bagel › Demo › Reset All · Main.unity open · Maple Autumn · English · sound on · Play from chapter 1 to the wrap-up", "bgd-before__text"));
            foot.Add(before);
            slide.Add(foot);
            return slide;
        }

        VisualElement BuildCard(Chapter chapter)
        {
            var presenter = BagelDemoDeck.Info(chapter.presenter);
            var card = new Button(() => ShowPage(chapter.number)) { name = "card-" + chapter.number, tooltip = $"{chapter.number} · {chapter.title}: {chapter.line}" };
            card.AddToClassList(cardUssClassName);
            card.style.borderTopColor = presenter.color;
            var top = El("bgd-card__top");
            var number = Text(chapter.number.ToString(), "bgd-card__number");
            number.style.backgroundColor = presenter.color;
            top.Add(number);
            var who = Text($"{presenter.glyph} {presenter.name}", "bgd-card__who");
            who.style.color = presenter.color;
            top.Add(who);
            top.Add(El("bgd-grow"));
            top.Add(Text(BagelDemoDeck.Minutes(chapter.seconds), "bgd-card__minutes"));
            card.Add(top);
            card.Add(Text(chapter.title, "bgd-card__title"));
            card.Add(Text(chapter.japaneseTitle, "bgd-card__jp"));
            var versions = El("bgd-card__versions");
            foreach (var version in chapter.versions)
                versions.Add(VersionChip(version, null));
            if (chapter.versions.Length == 0)
                versions.Add(Text(chapter.number == 1 ? "the game" : "the end", "bgd-card__plain"));
            card.Add(versions);
            var map = new BagelDemoMap(chapter, presenter.color, compact: true);
            card.Add(map);
            return card;
        }

        /// <summary>The 30 minutes, a segment per chapter in its presenter's color.</summary>
        VisualElement BuildTimeline()
        {
            var timeline = El("bgd-timeline");
            float total = BagelDemoDeck.totalSeconds;
            foreach (var chapter in BagelDemoDeck.chapters)
            {
                var segment = El("bgd-timeline__segment");
                segment.style.flexGrow = chapter.seconds / total;
                segment.style.backgroundColor = BagelDemoDeck.Info(chapter.presenter).color;
                segment.Add(Text(chapter.number.ToString(), "bgd-timeline__label"));
                timeline.Add(segment);
            }
            return timeline;
        }

        // ------------------------------------------------------------------ pieces

        VisualElement Slide(Presenter? presenter)
        {
            var slide = El(slideUssClassName + " bgg-text");
            if (presenter != null)
                slide.AddToClassList(PresenterClass(presenter.Value));
            slide.style.width = slideSize.x;
            slide.style.height = slideSize.y;
            slide.pickingMode = PickingMode.Position;
            return slide;
        }

        static VisualElement VersionChip(string version, string extraClass)
        {
            var group = BagelFeatureGuide.versions.FirstOrDefault(g => g.id == version);
            var chip = Text(group?.shortTitle ?? version, "bgd-version" + (extraClass != null ? " " + extraClass : ""));
            chip.tooltip = group?.subtitle;
            if (group != null)
                chip.style.backgroundColor = group.color;
            return chip;
        }

        static VisualElement Eyebrow(string text) => Text(text, "bgd-eyebrow");

        /// <summary>&lt;b&gt; marks what to look at: a color (the window's font has no bold face).</summary>
        static string Emphasis(string text) => text?.Replace("<b>", "<color=#FFD98A>").Replace("</b>", "</color>");

        static VisualElement El(string classNames)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                element.AddToClassList(className);
            return element;
        }

        static Label Text(string text, string classNames)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                label.AddToClassList(className);
            return label;
        }
    }

    /// <summary>
    /// A beat's picture box: its picture keeps its own size and is scaled to fit the box (up to a bit bigger), in the
    /// middle. Pictures are laid out at the Editor's size, so they read like the Editor whatever room they get.
    /// </summary>
    public sealed class BagelDemoFit : VisualElement
    {
        const float k_MaxScale = 1.3f;
        VisualElement m_Content;

        public VisualElement content => m_Content;
        /// <summary>The picture's scale in its box.</summary>
        public float contentScale { get; private set; } = 1f;

        public BagelDemoFit()
        {
            pickingMode = PickingMode.Ignore;
            RegisterCallback<GeometryChangedEvent>(_ => Fit());
        }

        public void SetContent(VisualElement content)
        {
            m_Content?.RemoveFromHierarchy();
            m_Content = content;
            if (content == null)
                return;
            content.style.position = Position.Absolute;
            content.style.left = 0f;
            content.style.top = 0f;
            content.style.transformOrigin = new TransformOrigin(0f, 0f);
            content.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            Insert(0, content);
        }

        void Fit()
        {
            if (m_Content == null)
                return;
            var room = contentRect;
            var size = m_Content.layout.size;
            if (!(room.width > 1f) || !(room.height > 1f) || !(size.x > 1f) || !(size.y > 1f))
                return;
            var scale = Mathf.Min(k_MaxScale, room.width / size.x, room.height / size.y);
            contentScale = scale;
            m_Content.style.scale = new Scale(new Vector2(scale, scale));
            m_Content.style.translate = new Translate(Mathf.Round((room.width - size.x * scale) * 0.5f), Mathf.Round((room.height - size.y * scale) * 0.5f));
        }
    }

    /// <summary>
    /// The arrows between a slide's beats, drawn with Painter2D over them: straight across a row, then down from the end
    /// of a row and over to the start of the next.
    /// </summary>
    public sealed class BagelDemoConnectors : VisualElement
    {
        static readonly CustomStyleProperty<Color> s_Color = new("--bgd-arrow-color");
        readonly IReadOnlyList<VisualElement> m_Beats;
        Color m_Color = new(1f, 0.85f, 0.55f, 0.9f);

        public BagelDemoConnectors(IReadOnlyList<VisualElement> beats)
        {
            m_Beats = beats.ToList();
            pickingMode = PickingMode.Ignore;
            AddToClassList("bgd-connectors");
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(s_Color, out var color))
                    m_Color = color;
                MarkDirtyRepaint();
            });
            RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
            foreach (var beat in m_Beats)
                beat.RegisterCallback<GeometryChangedEvent>(_ => MarkDirtyRepaint());
        }

        void Draw(MeshGenerationContext context)
        {
            if (m_Beats.Count < 2)
                return;
            var p = context.painter2D;
            p.strokeColor = m_Color;
            p.fillColor = m_Color;
            p.lineWidth = 5f;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            for (int i = 0; i + 1 < m_Beats.Count; i++)
            {
                var a = Rect(m_Beats[i]);
                var b = Rect(m_Beats[i + 1]);
                if (a.width < 1f || b.width < 1f)
                    continue;
                if (Mathf.Abs(a.center.y - b.center.y) < a.height * 0.5f)
                {
                    // Across: the gap between them.
                    var y = (a.center.y + b.center.y) * 0.5f;
                    var from = new Vector2(a.xMax + 8f, y);
                    var to = new Vector2(b.xMin - 8f, y);
                    if (to.x - from.x < 10f)
                        continue;
                    p.BeginPath();
                    p.MoveTo(from);
                    p.LineTo(to - new Vector2(6f, 0f));
                    p.Stroke();
                    Head(p, to, Vector2.right);
                }
                else
                {
                    // Down from the end of a row, along the gap between the rows, down into the start of the next.
                    var gapY = (a.yMax + b.yMin) * 0.5f;
                    var start = new Vector2(a.center.x, a.yMax + 6f);
                    var end = new Vector2(b.center.x, b.yMin - 6f);
                    const float r = 14f;
                    var dir = Mathf.Sign(end.x - start.x);
                    p.BeginPath();
                    p.MoveTo(start);
                    p.LineTo(new Vector2(start.x, gapY - r));
                    p.QuadraticCurveTo(new Vector2(start.x, gapY), new Vector2(start.x + dir * r, gapY));
                    p.LineTo(new Vector2(end.x - dir * r, gapY));
                    p.QuadraticCurveTo(new Vector2(end.x, gapY), new Vector2(end.x, gapY + r));
                    p.LineTo(end - new Vector2(0f, 6f));
                    p.Stroke();
                    Head(p, end, Vector2.up);
                }
            }
        }

        static void Head(Painter2D p, Vector2 tip, Vector2 direction)
        {
            var back = -direction * 13f;
            var side = new Vector2(-direction.y, direction.x) * 8f;
            p.BeginPath();
            p.MoveTo(tip);
            p.LineTo(tip + back + side);
            p.LineTo(tip + back - side);
            p.ClosePath();
            p.Fill();
        }

        Rect Rect(VisualElement beat) => beat.panel == null ? default : this.WorldToLocal(beat.worldBound);
    }

    /// <summary>
    /// Where a chapter happens: the demo layout in miniature (the main toolbar, the Hierarchy and Style Sheets, the
    /// Project and UI Library, the Scene's group, the Inspector and Game), the tabs it uses lit in its presenter's color
    /// with the numbers of the beats there, and the windows that open on top (UI Builder, Resource Tables, Bagel UI).
    /// </summary>
    public sealed class BagelDemoMap : VisualElement
    {
        public static readonly string ussClassName = "bgd-map";

        // The layout's groups and where they sit, in % of the map (BagelExpressDemo.wlt).
        static readonly (Area[] tabs, Rect rect)[] s_Groups =
        {
            (new[] { Area.Hierarchy, Area.StyleSheets }, new Rect(0f, 12f, 27f, 52f)),
            (new[] { Area.Project, Area.UILibrary }, new Rect(0f, 66f, 27f, 34f)),
            (new[] { Area.Scene, Area.UIViewport, Area.Animation, Area.Profiler, Area.BagelFeatures, Area.Console }, new Rect(28f, 12f, 45f, 88f)),
            (new[] { Area.Inspector, Area.Game }, new Rect(74f, 12f, 26f, 88f)),
        };

        static readonly Area[] s_Floating = { Area.UIBuilder, Area.ResourceTables, Area.BagelUI };

        public static string Name(Area area) => area switch
        {
            Area.Toolbar => "Toolbar",
            Area.Menu => "Bagel menu",
            Area.StyleSheets => "Style Sheets",
            Area.UILibrary => "UI Library",
            Area.UIViewport => "UI Viewport",
            Area.BagelFeatures => "Bagel Features",
            Area.UIBuilder => "UI Builder",
            Area.ResourceTables => "Resource Tables",
            Area.BagelUI => "Bagel UI",
            _ => area.ToString(),
        };

        public BagelDemoMap(Chapter chapter, Color color, bool compact = false)
        {
            pickingMode = PickingMode.Ignore;
            AddToClassList(ussClassName);
            EnableInClassList(ussClassName + "--compact", compact);
            // Which beats happen where.
            var beatsAt = new Dictionary<Area, List<int>>();
            foreach (var beat in chapter.beats)
                foreach (var area in beat.areas)
                {
                    if (!beatsAt.TryGetValue(area, out var list))
                        beatsAt[area] = list = new List<int>();
                    list.Add(beat.number);
                }

            m_Color = color;
            var toolbar = Box(new Rect(0f, 0f, 100f, 10f), beatsAt.ContainsKey(Area.Toolbar), color);
            toolbar.AddToClassList(ussClassName + "__toolbar");
            if (beatsAt.ContainsKey(Area.Toolbar))
                AddLabel(toolbar, Area.Toolbar, beatsAt, compact);
            Add(toolbar);

            foreach (var (tabs, rect) in s_Groups)
            {
                var used = tabs.Where(beatsAt.ContainsKey).ToList();
                var box = Box(rect, used.Count > 0, color);
                if (used.Count > 0)
                    foreach (var area in used)
                        AddLabel(box, area, beatsAt, compact);
                else if (!compact)
                    box.Add(Label(Name(tabs[0]), ussClassName + "__name " + ussClassName + "__name--dim"));
                Add(box);
            }

            var floating = s_Floating.Where(beatsAt.ContainsKey).ToList();
            for (int i = 0; i < floating.Count; i++)
            {
                var box = Box(new Rect(36f + i * 6f, 42f + i * 14f, 38f, 30f), true, color);
                box.AddToClassList(ussClassName + "__box--floating");
                AddLabel(box, floating[i], beatsAt, compact);
                Add(box);
            }

            if (beatsAt.ContainsKey(Area.Menu))
            {
                // macOS's menu bar, above the toolbar.
                var menu = Box(new Rect(0f, 0f, 40f, 10f), true, color);
                menu.AddToClassList(ussClassName + "__box--floating");
                menu.AddToClassList(ussClassName + "__toolbar");
                AddLabel(menu, Area.Menu, beatsAt, compact);
                Add(menu);
            }
        }

        static VisualElement Box(Rect rect, bool lit, Color color)
        {
            var box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList(ussClassName + "__box");
            box.EnableInClassList(ussClassName + "__box--lit", lit);
            box.style.left = Length.Percent(rect.x);
            box.style.top = Length.Percent(rect.y);
            box.style.width = Length.Percent(rect.width);
            box.style.height = Length.Percent(rect.height);
            if (lit)
            {
                box.style.borderTopColor = box.style.borderBottomColor = box.style.borderLeftColor = box.style.borderRightColor = color;
                box.style.backgroundColor = new Color(color.r, color.g, color.b, 0.28f);
            }
            return box;
        }

        readonly Color m_Color;

        void AddLabel(VisualElement box, Area area, Dictionary<Area, List<int>> beatsAt, bool compact)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(ussClassName + "__tab");
            if (!compact)
                row.Add(Label(Name(area), ussClassName + "__name"));
            foreach (var number in beatsAt[area])
            {
                var beat = Label(number.ToString(), ussClassName + "__beat");
                beat.style.backgroundColor = m_Color;
                row.Add(beat);
            }
            box.Add(row);
        }

        static Label Label(string text, string classNames)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            foreach (var className in classNames.Split(' '))
                label.AddToClassList(className);
            return label;
        }
    }
}
