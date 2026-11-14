using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Milestone celebration overlay. The game behind turns into a manga panel (custom
    /// <c>backdrop-filter</c> halftone + speed lines), a starburst spins, LEVEL UP! is stamped
    /// in (UIAnimationClip) and waves (mesh modifier), the card pops in and confetti flies.
    /// Pure UI: data comes from <see cref="Show"/>, so it also runs in UI Builder and editor windows.
    /// </summary>
    /// <remarks>
    /// The USS default is the visible resting pose (authorable in UI Builder). Code adds the
    /// <c>--hidden</c> class in the constructor, and every Show() restarts the one-shot clips by
    /// removing <c>--celebrate</c> and adding it back a frame later.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class LevelUpDialog : VisualElement
    {
        public static readonly string ussClassName = "be-level-up";
        public static readonly UniqueStyleString hiddenUssClassName = new(ussClassName + "--hidden");
        public static readonly UniqueStyleString stagedUssClassName = new(ussClassName + "--staged");
        public static readonly UniqueStyleString celebrateUssClassName = new(ussClassName + "--celebrate");
        public static readonly UniqueStyleString leavingUssClassName = new(ussClassName + "--leaving");

        const long k_FrameMs = 16;
        const long k_ConfettiDelayMs = 180;
        const long k_LeaveMs = 260;
        const int k_FlyIconCount = 6;

        bool m_IsOpen;
        int m_ShowToken;
        Button m_OkButton;
        Label m_ShopName;
        Label m_ShopJapaneseName;
        Label m_LevelFrom;
        Label m_LevelTo;
        Label m_IncomeFrom;
        Label m_IncomeTo;
        Label m_Boost;
        Label m_RewardAmount;
        Label m_Sticker;
        PastryIcon m_Pastry;
        ConfettiBurst m_Confetti;

        public bool isOpen => m_IsOpen;

        public Button okButton => m_OkButton ??= this.Q<Button>("level-up-ok-button");
        public Label shopNameLabel => m_ShopName ??= this.Q<Label>("level-up-shop-name");
        public Label shopJapaneseNameLabel => m_ShopJapaneseName ??= this.Q<Label>("level-up-shop-jp");
        public Label levelFromLabel => m_LevelFrom ??= this.Q<Label>("level-up-level-from");
        public Label levelToLabel => m_LevelTo ??= this.Q<Label>("level-up-level-to");
        public Label incomeFromLabel => m_IncomeFrom ??= this.Q<Label>("level-up-income-from");
        public Label incomeToLabel => m_IncomeTo ??= this.Q<Label>("level-up-income-to");
        public Label boostLabel => m_Boost ??= this.Q<Label>("level-up-boost");
        public Label rewardAmountLabel => m_RewardAmount ??= this.Q<Label>("level-up-reward-amount");
        public Label stickerLabel => m_Sticker ??= this.Q<Label>("level-up-sticker");
        public PastryIcon pastryIcon => m_Pastry ??= this.Q<PastryIcon>("level-up-pastry");
        public ConfettiBurst confetti => m_Confetti ??= this.Q<ConfettiBurst>("level-up-confetti");

        public LevelUpDialog()
        {
            AddToClassList(ussClassName);
            AddToClassList(hiddenUssClassName);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            EditorPreviewHooks.WatchStandalone(this, hiddenUssClassName);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // Children come from UXML after construction; hook the button once it exists.
            if (okButton != null)
            {
                okButton.clicked -= PlayerCloses;
                okButton.clicked += PlayerCloses;
            }
        }

        // A pinned screen (an editor window showing LEVEL UP!) keeps it open.
        void PlayerCloses()
        {
            if (!BakeryAppManager.IsPinned(this))
                Close();
        }

        /// <summary>Celebrate <paramref name="shop"/> reaching its current level, rewarding <paramref name="gems"/>.</summary>
        public void Show(ShopState shop, int gems)
        {
            Fill(shop, gems);

            m_IsOpen = true;
            BagelSound.Play(this, "open");
            var token = ++m_ShowToken;

            // Frame 1: displayed but staged (transparent); frame 2: celebrate restarts every clip.
            RemoveFromClassList(celebrateUssClassName);
            RemoveFromClassList(leavingUssClassName);
            AddToClassList(stagedUssClassName);
            RemoveFromClassList(hiddenUssClassName);

            schedule.Execute(() =>
            {
                if (token != m_ShowToken || !m_IsOpen)
                    return;
                RemoveFromClassList(stagedUssClassName);
                AddToClassList(celebrateUssClassName);
            }).ExecuteLater(k_FrameMs);

            schedule.Execute(() =>
            {
                if (token == m_ShowToken && m_IsOpen)
                    confetti?.Burst(90);
            }).ExecuteLater(k_ConfettiDelayMs);
        }

        /// <summary>Collect the reward: gems fly to the HUD while the dialog leaves.</summary>
        public void Close()
        {
            if (!m_IsOpen)
                return;

            m_IsOpen = false;
            var token = ++m_ShowToken;

            GetFirstAncestorOfType<BakeryAppManager>()?.FlyCurrency(okButton ?? (VisualElement)this, CurrencyKind.Gems, k_FlyIconCount);

            AddToClassList(leavingUssClassName);
            schedule.Execute(() =>
            {
                if (token != m_ShowToken)
                    return;
                AddToClassList(hiddenUssClassName);
                RemoveFromClassList(leavingUssClassName);
                RemoveFromClassList(celebrateUssClassName);
                confetti?.Stop();
            }).ExecuteLater(k_LeaveMs);
        }

        void Fill(ShopState shop, int gems)
        {
            if (shop == null)
                return;

            var level = shop.level;
            var previousLevel = Mathf.Max(1, level - 1);
            var income = shop.incomePerSecond;
            var previousIncome = EstimateIncomeAtLevel(shop, previousLevel, income);

            if (shopNameLabel != null)
                shopNameLabel.text = shop.displayName;
            if (shopJapaneseNameLabel != null)
                shopJapaneseNameLabel.text = shop.secondaryName;
            if (pastryIcon != null)
                pastryIcon.kind = shop.signature;
            if (levelFromLabel != null)
                levelFromLabel.text = "Lv." + previousLevel.ToString(CultureInfo.InvariantCulture);
            if (levelToLabel != null)
                levelToLabel.text = "Lv." + level.ToString(CultureInfo.InvariantCulture);
            if (incomeFromLabel != null)
                incomeFromLabel.text = BagelConverters.FormatRate(previousIncome);
            if (incomeToLabel != null)
                incomeToLabel.text = BagelConverters.FormatRate(income);
            if (boostLabel != null)
            {
                var percent = previousIncome > 0 ? Mathf.RoundToInt((float)((income / previousIncome - 1.0) * 100.0)) : 0;
                boostLabel.text = "+" + percent.ToString(CultureInfo.InvariantCulture) + "%";
                boostLabel.style.display = percent > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (rewardAmountLabel != null)
                rewardAmountLabel.text = "+" + gems.ToString("N0", CultureInfo.InvariantCulture);
            if (stickerLabel != null)
                stickerLabel.text = BagelLocalization.Get(shop.milestoneCount > 1 ? "level-up.sticker-tier" : "level-up.sticker");
        }

        /// <summary>
        /// Income scales with level × (1 + boost × milestones); recipe bonuses multiply both levels
        /// equally, so the ratio of the two level terms gives the previous income.
        /// </summary>
        double EstimateIncomeAtLevel(ShopState shop, int previousLevel, double income)
        {
            var catalog = GetFirstAncestorOfType<BakeryAppManager>()?.state?.catalog;
            var milestoneEvery = catalog ? Mathf.Max(1, catalog.milestoneEvery) : 5;
            double boost = catalog ? catalog.milestoneIncomeBoost : 0.5f;

            double Term(int lv) => lv * (1.0 + boost * (lv / milestoneEvery));
            var current = Term(shop.level);
            return current > 0 ? income * Term(previousLevel) / current : 0;
        }
    }
}
