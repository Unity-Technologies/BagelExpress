using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.UIElements.TestFramework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B10_EditorTools
{
    /// <summary>The Bagel Demo deck: a 30-minute talk in chapters of one presenter each, in the order of what's new.</summary>
    public class BagelDemoDeckTests
    {
        // This folder is published on its own: no document outside it (*.md), no absolute paths, no issue-tracker IDs.
        static readonly Regex s_PointsOutside = new(@"\b\w+\.md\b|(^|[\s(""'])/(Users|home|opt|tmp)/|\b[A-Z]{3,}-\d{4,}\b");
        static readonly Regex s_Tags = new(@"<[^>]+>");

        [Test]
        public void TheTalkIsThirtyMinutesOfChaptersThatAlternatePresenters()
        {
            var chapters = BagelDemoDeck.chapters;
            Assert.That(BagelDemoDeck.totalSeconds, Is.EqualTo(BagelDemoDeck.talkSeconds));
            Assert.That(chapters[0].presenter, Is.EqualTo(BagelDemoDeck.Presenter.Designer), "the Designer plays the game first");
            Assert.That(chapters[chapters.Count - 1].presenter, Is.EqualTo(BagelDemoDeck.Presenter.Designer), "and closes the talk");
            for (int i = 1; i < chapters.Count; i++)
                Assert.That(chapters[i].presenter, Is.Not.EqualTo(chapters[i - 1].presenter), $"{chapters[i].title} has the same presenter as the chapter before");
            for (int i = 0; i < chapters.Count; i++)
            {
                Assert.That(chapters[i].number, Is.EqualTo(i + 1));
                Assert.That(chapters[i].start, Is.EqualTo(chapters.Take(i).Sum(c => c.seconds)));
                Assert.That(string.IsNullOrEmpty(chapters[i].handoff), Is.EqualTo(i == chapters.Count - 1), $"{chapters[i].title}: a handoff, but for the last");
            }
        }

        /// <summary>6.7's in-scene workflow first (everything after is shown with it), then 6.6, 6.7 and 7.x, in that order.</summary>
        [Test]
        public void TheWorkflowComesFirstThenTheVersionsInOrder()
        {
            var order = new[] { "6.6", "6.7", "7.x" };
            var chapters = BagelDemoDeck.chapters;
            Assert.That(chapters[1].versions, Is.EqualTo(new[] { "6.7" }), "the in-scene workflow, right after the game");
            var last = -1;
            foreach (var chapter in chapters.Skip(3).Where(c => c.versions.Length == 1))
            {
                var index = System.Array.IndexOf(order, chapter.versions[0]);
                Assert.That(index, Is.GreaterThanOrEqualTo(last), $"{chapter.title} ({chapter.versions[0]}) comes after a later version");
                last = index;
            }
            foreach (var chapter in chapters)
                foreach (var version in chapter.versions)
                    Assert.That(BagelFeatureGuide.versions.Any(g => g.id == version), Is.True, $"{chapter.title}: no version group {version}");
        }

        /// <summary>A slide is pictures and a few words: a handful of beats, each with a short caption, a place, and a hint.</summary>
        [Test]
        public void EveryBeatIsAPictureAPlaceAndAFewWords()
        {
            foreach (var chapter in BagelDemoDeck.chapters)
            {
                Assert.That(chapter.beats.Length, Is.InRange(3, chapter.layout == BagelDemoDeck.Layout.Strip ? 7 : 6), chapter.title);
                Assert.That(chapter.line.Length, Is.LessThanOrEqualTo(110), $"{chapter.title}: one line to say");
                foreach (var beat in chapter.beats)
                {
                    var where = $"{chapter.number}.{beat.number} {beat.caption}";
                    Assert.That(beat.visual, Is.Not.Null, where);
                    Assert.That(beat.areas, Is.Not.Empty, $"{where}: where in the Editor");
                    Assert.That(s_Tags.Replace(beat.caption, "").Length, Is.InRange(3, 36), $"{where}: a few words");
                    Assert.That(beat.hint, Is.Not.Empty, where);
                }
            }
        }

        [Test]
        public void ItsTextPointsNowhereOutsideTheProject()
        {
            foreach (var chapter in BagelDemoDeck.chapters)
            {
                var texts = new[] { chapter.title, chapter.line, chapter.handoff ?? "", chapter.setup.note ?? "" }
                    .Concat(chapter.beats.SelectMany(b => new[] { b.caption, b.hint }));
                foreach (var text in texts)
                    Assert.That(s_PointsOutside.IsMatch(text), Is.False, text);
            }
        }
    }

    /// <summary>The Bagel Demo window: every slide whole in the window, its pictures live and released with it.</summary>
    public class BagelDemoWindowTests : EditorWindowUITestFixture<BagelDemoWindow>
    {
        [Test]
        public void EverySlideIsWholeInTheWindow()
        {
            panelSize = new Vector2(1200f, 800f);
            for (int page = 0; page < window.pageCount; page++)
            {
                window.ShowPage(page);
                for (int i = 0; i < 3; i++)
                    simulate.FrameUpdate();
                var slide = window.slide;
                Assert.That(slide.layout.size, Is.EqualTo(BagelDemoWindow.slideSize), $"page {page}: laid out at the deck's size");
                var room = rootVisualElement.Q("viewport").worldBound;
                var shown = slide.worldBound;
                Assert.That(shown.xMin, Is.GreaterThanOrEqualTo(room.xMin - 1f), $"page {page}");
                Assert.That(shown.xMax, Is.LessThanOrEqualTo(room.xMax + 1f), $"page {page}");
                Assert.That(shown.yMin, Is.GreaterThanOrEqualTo(room.yMin - 1f), $"page {page}");
                Assert.That(shown.yMax, Is.LessThanOrEqualTo(room.yMax + 1f), $"page {page}");

                var count = page == BagelDemoWindow.overviewPage ? BagelDemoDeck.chapters.Count : window.chapter.beats.Length;
                Assert.That(window.beats.Count, Is.EqualTo(count), $"page {page}");
                foreach (var beat in window.beats)
                {
                    Assert.That(beat.worldBound.width, Is.GreaterThan(100f), $"page {page}: {beat.name}");
                    Assert.That(slide.worldBound.Contains(beat.worldBound.min) && slide.worldBound.Contains(beat.worldBound.max - Vector2.one), Is.True,
                        $"page {page}: {beat.name} is outside the slide");
                    if (page == BagelDemoWindow.overviewPage)
                        continue;
                    var fit = beat.Q<BagelDemoFit>();
                    Assert.That(fit?.content, Is.Not.Null, $"page {page}: {beat.name} has no picture");
                    Assert.That(fit.contentScale, Is.GreaterThan(0.25f), $"page {page}: {beat.name}'s picture is too big for its box");
                }
            }
        }

        /// <summary>Every live screen on a slide finds what it frames, rings or changes, in the game's UXML.</summary>
        [Test]
        public void EveryLiveScreenFindsItsElements()
        {
            panelSize = new Vector2(1200f, 800f);
            for (int page = 1; page < window.pageCount; page++)
            {
                window.ShowPage(page);
                simulate.FrameUpdate();
                foreach (var crop in window.visuals.crops)
                {
                    Assert.That(crop.isBuilt, Is.True, $"page {page}: {crop.screen.title}");
                    foreach (var selector in crop.focus)
                        Assert.That(GameScreen.Find(crop.screenRoot, selector), Is.Not.Null, $"page {page}: {crop.screen.title} has no {selector}");
                }
                foreach (var phone in window.visuals.phones)
                    foreach (var spot in phone.hotspots)
                        Assert.That(phone.FindTarget(spot), Is.Not.Null, $"page {page}: {phone.screen.title} has no {spot.selector}");
            }
        }

        [Test]
        public void ALiveScreenGoesWithItsSlide()
        {
            window.ShowPage(3);
            simulate.FrameUpdate();
            var crops = window.visuals.crops.ToList();
            Assert.That(crops, Is.Not.Empty);
            window.ShowPage(BagelDemoWindow.overviewPage);
            simulate.FrameUpdate();
            Assert.That(crops.All(c => !c.isBuilt), Is.True, "the slide's screens were let go");
            Assert.That(window.visuals.crops, Is.Empty, "the overview has no live screen");

            window.ShowPage(1);
            var phones = window.visuals.phones.ToList();
            Assert.That(phones.Count, Is.EqualTo(BagelDemoDeck.chapters[0].beats.Length), "the game, played: a phone per beat");
            window.ShowPage(2);
            Assert.That(phones.All(p => p.panel == null), Is.True, "its phones are gone");
        }

        [Test]
        public void KeysAndThePageStripTurnThePages()
        {
            window.ShowPage(BagelDemoWindow.overviewPage);
            Press(KeyCode.RightArrow);
            Assert.That(window.page, Is.EqualTo(1));
            Press(KeyCode.PageDown);
            Assert.That(window.page, Is.EqualTo(2));
            Press(KeyCode.LeftArrow);
            Assert.That(window.page, Is.EqualTo(1));
            Press(KeyCode.End);
            Assert.That(window.page, Is.EqualTo(window.pageCount - 1));
            Press(KeyCode.RightArrow);
            Assert.That(window.page, Is.EqualTo(window.pageCount - 1), "the last page stays");
            Press(KeyCode.Alpha4);
            Assert.That(window.page, Is.EqualTo(4));
            Press(KeyCode.Home);
            Assert.That(window.page, Is.EqualTo(BagelDemoWindow.overviewPage));

            var strip = rootVisualElement.Q<BagelOptionStrip>("page-strip");
            Assert.That(strip.childCount, Is.EqualTo(window.pageCount), "the overview, then a number per chapter");
            simulate.Click(strip.Q("page-5"));
            Assert.That(window.page, Is.EqualTo(5));
            Assert.That(BagelOptionStrip.IsOn(strip.Q("page-5")), Is.True);
            Assert.That(BagelOptionStrip.IsOn(strip.Q("page-0")), Is.False);
        }

        /// <summary>The overview's cards open their chapter.</summary>
        [Test]
        public void TheOverviewOpensEachChapter()
        {
            window.ShowPage(BagelDemoWindow.overviewPage);
            simulate.FrameUpdate();
            var card = rootVisualElement.Q<Button>("card-6");
            Assert.That(card, Is.Not.Null);
            simulate.Click(card);
            simulate.FrameUpdate();
            Assert.That(window.page, Is.EqualTo(6));
            Assert.That(window.chapter, Is.SameAs(BagelDemoDeck.chapters[5]));
        }

        void Press(KeyCode key)
        {
            using (var down = KeyDownEvent.GetPooled('\0', key, EventModifiers.None))
            {
                down.target = rootVisualElement;
                rootVisualElement.SendEvent(down);
            }
            simulate.FrameUpdate();
        }
    }
}
