using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Bagel › Bagel Demo: the talk as a deck of slides, one per chapter, made to be read full screen. Each chapter is one
    /// presenter's, in one part of the Editor; its slide is its beats in order, joined by arrows, each a picture (the
    /// Editor's windows recreated, the real game UI) and a few words: where it happens in the demo layout, what to set
    /// in the demo toolbar, the one thing to say and the handoff. The first page is the whole talk at a glance.
    /// <para>
    /// A slide is laid out at one size (<see cref="slideSize"/>) and scaled to fit the window, so it's always all there.
    /// ← → (and Page Up/Down, Space), Home/End and the number keys turn the pages; the toolbar's strip of numbers too. The
    /// deck is <see cref="BagelDemoDeck"/>.
    /// </para>
    /// </summary>
    public partial class BagelDemoWindow : BagelGuideWindow
    {
        public const string windowTitle = "Bagel Demo";
        public const string uxmlPath = "Assets/UI/Editor/B10_EditorTools/BagelDemo/BagelDemoWindow.uxml";
        public static readonly Vector2 slideSize = new(1600f, 960f);
        /// <summary>The first page: the talk at a glance. Chapter n is page n.</summary>
        public const int overviewPage = 0;

        [SerializeField] int m_Page = overviewPage;

        VisualElement m_Viewport;
        VisualElement m_Slide;
        BagelOptionStrip m_PageStrip;
        BagelDemoVisuals m_Visuals;
        readonly List<VisualElement> m_Beats = new();

        public int page => m_Page;
        public int pageCount => BagelDemoDeck.chapters.Count + 1;
        /// <summary>The chapter shown (null on the overview).</summary>
        public BagelDemoDeck.Chapter chapter => m_Page > overviewPage ? BagelDemoDeck.chapters[m_Page - 1] : null;
        public VisualElement slide => m_Slide;
        /// <summary>The beats of the slide shown (the chapter cards on the overview).</summary>
        public IReadOnlyList<VisualElement> beats => m_Beats;
        public BagelDemoVisuals visuals => m_Visuals;

        protected override string windowUxmlPath => uxmlPath;

        [MenuItem("Bagel/Bagel Demo", priority = 22, editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static BagelDemoWindow Open()
        {
            var window = GetWindow<BagelDemoWindow>();
            window.titleContent = BagelWindowIcons.Title(windowTitle, BagelWindowIcons.bagelDemo);
            window.minSize = new Vector2(480f, 320f);
            return window;
        }

        protected override void BuildContent()
        {
            titleContent = BagelWindowIcons.Title(windowTitle, BagelWindowIcons.bagelDemo);
            m_Viewport = rootVisualElement.Q("viewport");
            m_Viewport.RegisterCallback<GeometryChangedEvent>(_ => FitSlide());
            BindToolbar();
            rootVisualElement.focusable = true;
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerDownEvent>(_ => rootVisualElement.Focus());
            ShowPage(m_Page);
        }

        protected override void OnDisable()
        {
            UnloadSlide();
            base.OnDisable();
        }

        void BindToolbar()
        {
            if (rootVisualElement.Q<BagelOptionStrip>("reset-strip") is { } resetStrip)
                BagelPickerIcons.AddReset(resetStrip, ResetWindow);
            m_PageStrip = rootVisualElement.Q<BagelOptionStrip>("page-strip");
            if (m_PageStrip == null)
                return;
            var overview = m_PageStrip.AddTextAction("◎", "The talk at a glance (Home)", () => ShowPage(overviewPage));
            overview.name = "page-0";
            foreach (var chapter in BagelDemoDeck.chapters)
            {
                var number = chapter.number;
                var presenter = BagelDemoDeck.Info(chapter.presenter);
                var option = m_PageStrip.AddTextAction(number.ToString(),
                    $"{number} · {chapter.title}\n{presenter.glyph} {presenter.name} · {BagelDemoDeck.Minutes(chapter.seconds)}", () => ShowPage(number));
                option.name = "page-" + number;
                option.AddToClassList(PresenterClass(chapter.presenter));
            }
        }

        /// <summary>Start again: the talk at a glance, a fresh copy of the demo start state, the first season and the project's language.</summary>
        public void ResetWindow()
        {
            ResetSeasonAndLanguage();
            m_Page = overviewPage;
            ResetState(null);
            ShowPage(overviewPage);
        }

        // ------------------------------------------------------------------ pages

        /// <summary>Show a page (<see cref="overviewPage"/>, or a chapter's number); the one it leaves lets its live screens go.</summary>
        public void ShowPage(int index)
        {
            index = Mathf.Clamp(index, overviewPage, pageCount - 1);
            UnloadSlide();
            m_Page = index;
            m_Visuals = new BagelDemoVisuals(this);
            m_Slide = index == overviewPage ? BuildOverview() : BuildChapter(BagelDemoDeck.chapters[index - 1]);
            m_Slide.name = "slide";
            // Text renders as SDF: the slide is scaled to the window, and the Editor's bitmap text would blur.
            m_Slide.style.unityEditorTextRenderingMode = EditorTextRenderingMode.SDF;
            m_Viewport?.Add(m_Slide);
            m_PageStrip?.SetSelectedWithoutNotify(index);
            FitSlide();
        }

        public void NextPage() => ShowPage(m_Page + 1);

        public void PreviousPage() => ShowPage(m_Page - 1);

        void UnloadSlide()
        {
            m_Visuals?.Release();
            m_Visuals = null;
            m_Beats.Clear();
            // Every page is made again when it's shown: the one left gives its resources back to be reused now.
            Discard.Element(m_Slide);
            m_Slide = null;
        }

        /// <summary>The slide, whole and as big as the window allows, in its middle.</summary>
        void FitSlide()
        {
            if (m_Slide == null || m_Viewport == null)
                return;
            var room = m_Viewport.contentRect;
            if (!(room.width > 1f) || !(room.height > 1f))
                return;
            var scale = Mathf.Min(room.width / slideSize.x, room.height / slideSize.y);
            m_Slide.style.scale = new Scale(new Vector2(scale, scale));
            m_Slide.style.left = Mathf.Round((room.width - slideSize.x * scale) * 0.5f);
            m_Slide.style.top = Mathf.Round((room.height - slideSize.y * scale) * 0.5f);
        }

        /// <summary>The slide's scale in the window.</summary>
        public float slideScale => m_Slide != null ? m_Slide.resolvedStyle.scale.value.x : 1f;

        void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.RightArrow:
                case KeyCode.DownArrow:
                case KeyCode.PageDown:
                case KeyCode.Space:
                    NextPage();
                    break;
                case KeyCode.LeftArrow:
                case KeyCode.UpArrow:
                case KeyCode.PageUp:
                case KeyCode.Backspace:
                    PreviousPage();
                    break;
                case KeyCode.Home:
                    ShowPage(overviewPage);
                    break;
                case KeyCode.End:
                    ShowPage(pageCount - 1);
                    break;
                default:
                    if (evt.keyCode >= KeyCode.Alpha0 && evt.keyCode <= KeyCode.Alpha9)
                        ShowPage(evt.keyCode - KeyCode.Alpha0);
                    else if (evt.keyCode >= KeyCode.Keypad0 && evt.keyCode <= KeyCode.Keypad9)
                        ShowPage(evt.keyCode - KeyCode.Keypad0);
                    else
                        return;
                    break;
            }
            evt.StopPropagation();
        }

        static string PresenterClass(BagelDemoDeck.Presenter presenter) =>
            presenter == BagelDemoDeck.Presenter.Designer ? "bgd--designer" : "bgd--developer";
    }
}
