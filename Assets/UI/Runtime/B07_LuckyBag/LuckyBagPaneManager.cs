using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Lucky Bag (福袋) page: draw recipes with tickets (DRAW ×1, DRAW ×5, or tap the bag for one). The whole show is UI-only:
    /// the bag shakes (UIAnimationClip), glows in the best rarity's color, then the
    /// results overlay reveals cards one by one (staggered classes + clips).
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class LuckyBagPaneManager : BakeryPane
    {
        public static readonly string ussClassName = "be-lucky-bag";
        public static readonly UniqueStyleString shakingUssClassName = new("be-lucky-bag--shaking");
        public static readonly UniqueStyleString resultsOpenUssClassName = new("be-lucky-bag--results-open");
        public static readonly string revealUssClassName = "be-reveal";
        public static readonly UniqueStyleString revealedUssClassName = new("be-reveal--revealed");
        public static readonly UniqueStyleString resultsRowManyUssClassName = new("be-lucky-bag__results-row--many");
        // The cards are made again for every draw: their class names are made once, not concatenated each time.
        static readonly UniqueStyleString s_RevealUssClassName = new(revealUssClassName);
        static readonly UniqueStyleString s_BackUssClassName = new(revealUssClassName + "__back");
        static readonly UniqueStyleString s_BackMarkUssClassName = new(revealUssClassName + "__back-mark");
        static readonly UniqueStyleString s_CardUssClassName = new(revealUssClassName + "__card");
        static readonly UniqueStyleString s_TagUssClassName = new(revealUssClassName + "__tag");
        static readonly UniqueStyleString s_TagNewUssClassName = new(revealUssClassName + "__tag--new");
        static readonly UniqueStyleString s_TagDupeUssClassName = new(revealUssClassName + "__tag--dupe");
        static readonly UniqueStyleString[] s_RarityClassNames =
        {
            new(revealUssClassName + "--n"), new(revealUssClassName + "--r"), new(revealUssClassName + "--sr"),
            new(revealUssClassName + "--ssr"), new(revealUssClassName + "--ur")
        };
        static readonly UniqueStyleString[] s_GlowClassNames =
        {
            new("be-lucky-bag--glow-n"), new("be-lucky-bag--glow-r"), new("be-lucky-bag--glow-sr"), new("be-lucky-bag--glow-ssr"), new("be-lucky-bag--glow-ur")
        };

        const long k_ShakeMs = 700;
        const long k_RevealStaggerMs = 260;

        readonly List<DrawResult> m_Results = new();
        Button m_DrawOneButton;
        Button m_DrawFiveButton;
        Button m_ResultsOkButton;
        VisualElement m_Bag;
        VisualElement m_ResultsRow;
        Label m_ResultsTitle;
        bool m_Bound;
        bool m_Busy;

        [UxmlAttribute]
        public VisualTreeAsset cardTemplate { get; set; }

        public Button drawOneButton => m_DrawOneButton ??= this.Q<Button>("draw-one-button");
        public Button drawFiveButton => m_DrawFiveButton ??= this.Q<Button>("draw-five-button");
        public Button resultsOkButton => m_ResultsOkButton ??= this.Q<Button>("results-ok-button");
        public VisualElement bag => m_Bag ??= this.Q<VisualElement>("bag-stage");
        public VisualElement resultsRow => m_ResultsRow ??= this.Q<VisualElement>("results-row");
        public Label resultsTitle => m_ResultsTitle ??= this.Q<Label>("results-title");

        public LuckyBagPaneManager()
        {
            AddToClassList(ussClassName);
        }

        protected override void OnAttached()
        {
            if (m_Bound)
                return;
            m_Bound = true;
            drawOneButton?.RegisterCallback<ClickEvent>(_ => PlayerDraws(1));
            drawFiveButton?.RegisterCallback<ClickEvent>(_ => PlayerDraws(5));
            resultsOkButton?.RegisterCallback<ClickEvent>(_ =>
            {
                if (!BakeryAppManager.IsPinned(this))
                    CloseResults();
            });
            // Tapping the bag itself is the obvious thing to try: it draws one, like DRAW ×1.
            bag?.RegisterCallback<ClickEvent>(_ => PlayerDraws(1));
        }

        /// <summary>A draw button or the bag: draws, unless the screen is pinned (the results would cover it).</summary>
        void PlayerDraws(int count)
        {
            if (!BakeryAppManager.IsPinned(this))
                Draw(count);
        }

        public void Draw(int count)
        {
            if (state == null || m_Busy)
                return;

            if (!state.TryDraw(count, m_Results))
            {
                BagelSound.Play(this, "fail");
                Toast(BagelLocalization.Get(count == 1 ? "toast.need-ticket" : "toast.need-5-tickets"));
                return;
            }

            m_Busy = true;
            var best = Rarity.N;
            foreach (var result in m_Results)
                if (result.pastry.rarity > best)
                    best = result.pastry.rarity;

            for (int i = 0; i < s_GlowClassNames.Length; i++)
                EnableInClassList(s_GlowClassNames[i], i == (int)best);
            BagelSound.Play(bag, "draw");
            Shake();
            schedule.Execute(ShowResults).ExecuteLater(k_ShakeMs);
        }

        void Shake()
        {
            RemoveFromClassList(shakingUssClassName);
            schedule.Execute(() => AddToClassList(shakingUssClassName)).ExecuteLater(16);
            schedule.Execute(() => RemoveFromClassList(shakingUssClassName)).ExecuteLater(k_ShakeMs);
        }

        void ShowResults()
        {
            if (resultsRow == null || cardTemplate == null)
            {
                m_Busy = false;
                return;
            }

            // The last draw's cards are gone for good: their resources go back to be reused now, not when the GC finds them.
            Discard.Children(resultsRow);
            resultsRow.EnableInClassList(resultsRowManyUssClassName, m_Results.Count > 1);
            if (resultsTitle != null)
                resultsTitle.text = BagelLocalization.Get(m_Results.Count > 1 ? "lucky-bag.results-5" : "lucky-bag.results");

            for (int i = 0; i < m_Results.Count; i++)
            {
                var result = m_Results[i];
                var recipe = FindRecipe(result.pastry);

                var reveal = new VisualElement { pickingMode = PickingMode.Ignore };
                reveal.AddToClassList(s_RevealUssClassName, s_RarityClassNames[(int)result.pastry.rarity]);

                var back = new VisualElement { pickingMode = PickingMode.Ignore };
                back.AddToClassList(s_BackUssClassName);
                var backMark = new Label("福") { pickingMode = PickingMode.Ignore };
                backMark.AddToClassList(s_BackMarkUssClassName);
                back.Add(backMark);
                reveal.Add(back);

                var card = cardTemplate.Instantiate();
                card.pickingMode = PickingMode.Ignore;
                card.AddToClassList(s_CardUssClassName);
                card.dataSource = recipe;
                reveal.Add(card);

                var tag = new Label(result.isNew ? "NEW!" : "+" + result.duplicateCoins.ToString("N0")) { pickingMode = PickingMode.Ignore };
                tag.AddToClassList(s_TagUssClassName, result.isNew ? s_TagNewUssClassName : s_TagDupeUssClassName);
                reveal.Add(tag);

                resultsRow.Add(reveal);
                reveal.schedule.Execute(() =>
                {
                    // Each rarity's reveal class names its own sound (--be-sound-reveal).
                    reveal.AddToClassList(revealedUssClassName);
                    BagelSound.Play(reveal, "reveal");
                }).ExecuteLater(300 + i * k_RevealStaggerMs);
            }

            AddToClassList(resultsOpenUssClassName);
            schedule.Execute(() => m_Busy = false).ExecuteLater(300 + m_Results.Count * k_RevealStaggerMs);
        }

        public void CloseResults()
        {
            RemoveFromClassList(resultsOpenUssClassName);
            foreach (var glow in s_GlowClassNames)
                RemoveFromClassList(glow);
        }

        RecipeState FindRecipe(PastryDefinition pastry)
        {
            foreach (var recipe in state.recipeStates)
                if (recipe.definition == pastry)
                    return recipe;
            return null;
        }
    }
}
