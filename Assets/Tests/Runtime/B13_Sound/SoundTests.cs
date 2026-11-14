using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B13_Sound
{
    /// <summary>
    /// The UI asks for sounds by cue and its styles (BagelSounds.uss and the season themes) pick the patch.
    /// These tests listen to <see cref="BagelSound.played"/>; no synth is plugged in, so they're silent.
    /// </summary>
    public class SoundTests : BakeryUITestFixture
    {
        readonly List<SoundEvent> m_Played = new();
        TemplateContainer m_Screen;
        BakeryAppManager m_App;

        [SetUp]
        public void SetUp()
        {
            BagelSound.played += m_Played.Add;
            m_Screen = Show(assets.appScreenUxml);
            m_App = m_Screen.Q<BakeryAppManager>();
        }

        [TearDown]
        public void TearDown()
        {
            BagelSound.played -= m_Played.Add;
            BagelSound.enabled = true;
            BagelSound.output = null;
            m_Played.Clear();
        }

        void SkipTitle()
        {
            m_App.showTitle = false;
            simulate.FrameUpdate();
            m_Played.Clear();
        }

        void GoTo(AppPage page)
        {
            m_App.GoTo(page);
            simulate.FrameUpdateMs(400); // slide in
            simulate.FrameUpdate();      // lists create and bind their items
            m_Played.Clear();
        }

        List<string> Played() => m_Played.Select(e => e.ToString()).ToList();

        // A drag that starts on the storefront also bakes: only look at the page sounds.
        List<string> PageSounds() => m_Played.Where(e => e.cue is "swipe" or "settle").Select(e => e.ToString()).ToList();

        [Test]
        public void TheTitleOpensWithTheShopsDoorBell()
        {
            simulate.Click(m_App.titleScreen);

            Assert.That(Played(), Is.EqualTo(new[] { "start: doorbell" }));
        }

        [Test]
        public void EachNavItemPlaysTheNextNoteOfAScale()
        {
            SkipTitle();
            foreach (var page in new[] { AppPage.Shops, AppPage.LuckyBag, AppPage.Recipes, AppPage.Settings, AppPage.Home })
            {
                simulate.Click(m_App.GetNavButton(page));
                simulate.FrameUpdateMs(400);
            }

            Assert.That(m_Played.Select(e => e.patch), Is.All.EqualTo("tab"));
            Assert.That(m_Played.Select(e => e.semitones), Is.EqualTo(new[] { 2f, 4f, 7f, 9f, 0f }));
        }

        [Test]
        public void CollectRingsTheRegisterAndEachCoinLandsAStepHigher()
        {
            SkipTitle();
            var home = m_App.Q<HomePaneManager>();

            simulate.Click(home.collectButton);
            Assert.That(Played(), Is.EqualTo(new[] { "collect: register" }), "the button itself is silent: the register is its sound");

            simulate.FrameUpdateMs(1400); // the coins fly to the HUD
            var landings = m_Played.Skip(1).ToList();
            Assert.That(landings.Count, Is.InRange(4, 10));
            Assert.That(landings.Select(e => e.ToString()), Is.All.EqualTo("land: coin"));
            Assert.That(landings.Select(e => e.semitones), Is.Ordered.Ascending);
            Assert.That(landings.Select(e => e.semitones), Is.Unique);
        }

        [Test]
        public void AMilestoneUpgradePlaysTheFanfareAndAnOrdinaryUpgradeTheArpeggio()
        {
            SkipTitle();
            m_App.state.coins += 100000;
            GoTo(AppPage.Shops);
            var shops = m_App.Q<ShopsPaneManager>();

            simulate.Click(shops.shopList.GetRootElementForIndex(0).Q<ShopCard>().actionButton); // Mile End: Lv 4 -> 5
            Assert.That(Played(), Is.EqualTo(new[] { "open: fanfare" }), "LEVEL UP! opens with its own sound; the button is silent");

            m_App.Q<LevelUpDialog>().Close();
            simulate.FrameUpdateMs(1500);
            m_Played.Clear();
            simulate.Click(shops.shopList.GetRootElementForIndex(1).Q<ShopCard>().actionButton); // Plateau: Lv 1 -> 2
            Assert.That(Played(), Is.EqualTo(new[] { "upgrade: upgrade" }));
        }

        [Test]
        public void ADisabledButtonSaysNope()
        {
            SkipTitle();
            GoTo(AppPage.Shops);
            var shibuya = m_App.Q<ShopsPaneManager>().shopList.GetRootElementForIndex(2).Q<ShopCard>();
            Assert.That(shibuya.actionButton.enabledInHierarchy, Is.False, "locked and too expensive");

            simulate.Click(shibuya.actionButton);

            Assert.That(Played(), Is.EqualTo(new[] { "disabled: nope" }));
        }

        [Test]
        public void ASwipeThatTurnsThePageSwishesAndOneThatSettlesBackSettles()
        {
            SkipTitle();
            var center = m_App.worldBound.center;

            simulate.DragAndDrop(center + new Vector2(80, 0), center - new Vector2(80, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(PageSounds(), Is.EqualTo(new[] { "swipe: swish" }));

            m_Played.Clear();
            simulate.MouseDown(center + new Vector2(30, 0));
            simulate.MouseMove(center);
            simulate.MouseMove(center - new Vector2(30, 0));
            simulate.FrameUpdateMs(200);
            simulate.MouseUp(center - new Vector2(30, 0));
            simulate.FrameUpdateMs(400);
            Assert.That(PageSounds(), Is.EqualTo(new[] { "settle: settle" }));
        }

        [Test]
        public void TheFirstLuckyBagRustlesAndItsSSRRevealSparkles()
        {
            SkipTitle();
            GoTo(AppPage.LuckyBag);
            var luckyBag = m_App.Q<LuckyBagPaneManager>();

            simulate.Click(luckyBag.drawOneButton);
            simulate.FrameUpdateMs(1000); // the bag shakes, then the card flips
            simulate.FrameUpdateMs(500);

            Assert.That(Played(), Is.EqualTo(new[] { "draw: rustle", "reveal: reveal-ssr" }));
        }

        [TestCase(1, SoundTone.Koto)]
        [TestCase(2, SoundTone.Furin)]
        [TestCase(3, SoundTone.Bell)]
        public void EachSeasonPlaysItsOwnInstrument(int season, SoundTone tone)
        {
            SkipTitle();
            var themes = m_App.Q<SettingsPaneManager>().themeCollection;
            m_Screen.styleSheets.Remove(assets.theme);
            m_Screen.styleSheets.Add(themes.collection[season].theme);
            simulate.FrameUpdate();

            simulate.Click(m_App.GetNavButton(AppPage.Shops));

            Assert.That(m_Played.Single().patch, Is.EqualTo("tab"));
            Assert.That(m_Played.Single().tone, Is.EqualTo(tone));
        }

        [Test]
        public void TurningSoundOffInSettingsSendsNothingToTheSynth()
        {
            SkipTitle();
            var sent = 0;
            BagelSound.output = notes => { sent++; return true; };
            GoTo(AppPage.Settings);
            var settings = m_App.Q<SettingsPaneManager>();

            settings.soundToggle.value = false; // it's below the fold of the Settings scroll view
            Assert.That(BagelSound.enabled, Is.False);
            sent = 0;
            simulate.Click(m_App.GetNavButton(AppPage.Home));

            Assert.That(m_Played.Last().patch, Is.EqualTo("tab"), "still raised, e.g. for tests");
            Assert.That(sent, Is.Zero);
        }
    }
}
