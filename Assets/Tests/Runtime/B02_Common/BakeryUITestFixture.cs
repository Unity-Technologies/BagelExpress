using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.TestFramework;

namespace Bagel
{
    /// <summary>
    /// Base for the Bagel Express UI tests. The panel is phone-sized (the PanelSettings
    /// reference resolution) and screens wear the game's Maple theme, so simulated taps
    /// land on the same layout players see.
    /// </summary>
    public abstract class BakeryUITestFixture : UITestFixture
    {
        public static readonly Vector2 phoneSize = new Vector2(390, 844);

        readonly List<BakeryState> m_States = new();

        protected BagelTestAssetList assets { get; private set; }

        bool m_SaveWasEnabled;
        string m_LocaleWas;

        [OneTimeSetUp]
        public void LoadTestAssets()
        {
            assets = Resources.Load<BagelTestAssetList>("BagelTestAssetList");
            // Never read or overwrite the player's saved game and settings.
            m_SaveWasEnabled = BagelSave.enabled;
            BagelSave.enabled = false;
            // Screens exactly as authored: no demo toolbar screen or season, and English, whatever the Editor previews in
            // (Play mode starts in English anyway).
            EditorPreviewHooks.paused = true;
            m_LocaleWas = BagelLocalization.localeCode;
            BagelLocalization.SetLocale("en");
        }

        [OneTimeTearDown]
        public void RestoreSaving()
        {
            BagelSave.enabled = m_SaveWasEnabled;
            EditorPreviewHooks.paused = false;
            if (!string.IsNullOrEmpty(m_LocaleWas))
                BagelLocalization.SetLocale(m_LocaleWas);
        }

        /// <summary>
        /// Where a scrolling page's content is in the clear, in world space: between the glass over its header and the
        /// nav's own glass, from side to side.
        /// </summary>
        protected static Rect ClearArea(BakeryAppManager app, AppPage page)
        {
            var glass = app.GetPage(page).Q(className: "be-page-glass");
            return Rect.MinMaxRect(app.worldBound.xMin, glass.worldBound.yMax, app.worldBound.xMax, app.navGlass.worldBound.yMin);
        }

        /// <summary>Clone a UXML screen into the panel and let one frame run (attach, bind, layout).</summary>
        protected TemplateContainer Show(VisualTreeAsset uxml, object dataSource = null)
        {
            panelSize = phoneSize;
            var screen = uxml.Instantiate();
            screen.style.flexGrow = 1;
            screen.styleSheets.Add(assets.theme);
            screen.dataSource = dataSource;
            rootVisualElement.Add(screen);
            simulate.FrameUpdate();
            return screen;
        }

        /// <summary>A fresh copy of the demo start state (1,250 coins, Mile End Lv 4...).</summary>
        protected BakeryState NewState()
        {
            var state = BakeryState.CreateRuntimeCopy(assets.startState);
            m_States.Add(state);
            return state;
        }

        [TearDown]
        public void DestroyStates()
        {
            // Screens opened on their own create a throwaway state copy: clean those up too.
            rootVisualElement.Query<BakeryAppManager>().ForEach(app => m_States.Add(app.state));
            rootVisualElement.Query<BakeryPane>().ForEach(pane => { if (pane.app == null) m_States.Add(pane.state); });

            foreach (var state in m_States)
                if (state != null)
                    Object.DestroyImmediate(state);
            m_States.Clear();
        }
    }
}
