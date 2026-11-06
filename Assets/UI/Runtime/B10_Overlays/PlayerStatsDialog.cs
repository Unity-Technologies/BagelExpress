using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The player's stats, opened by tapping the HUD's player row: Begu-chan's mood and why, the coin rate and what
    /// makes it up (each open shop, the Recipe Book bonus, the milestone boost), the player level and the way to the
    /// next one, and the counts. It's a <see cref="ConfirmDialog"/> (the Lucky Ticket dialog's panel, scrim, title
    /// band, entrance and exit) with one button and its own body.
    /// </summary>
    /// <remarks>
    /// The single values (coins, the coin rate, the level, the mood's face...) are bound to the state in UXML. The
    /// sentences and the shop rows are written by <see cref="Refresh"/>, four times a second while the dialog is open,
    /// so the numbers keep up with the idle game.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class PlayerStatsDialog : ConfirmDialog
    {
        public static readonly string statsUssClassName = "be-stats";
        public static readonly string shopRowUssClassName = statsUssClassName + "__shop";
        public static readonly UniqueStyleString pausedUssClassName = new(statsUssClassName + "--paused");
        public static readonly UniqueStyleString goneUssClassName = new(statsUssClassName + "--gone");

        const long k_RefreshMs = 250;

        readonly List<ShopState> m_RowShops = new();
        BakeryState m_State;
        IVisualElementScheduledItem m_Refresh;

        public Label moodName => this.Q<Label>("mood-name");
        public Label moodReason => this.Q<Label>("mood-reason");
        public VisualElement shopRows => this.Q("income-shops");
        public Label recipesBonusLabel => this.Q<Label>("recipes-bonus-label");
        public Label milestoneBoostLabel => this.Q<Label>("milestone-boost-label");
        public Label levelHow => this.Q<Label>("level-how");
        public VisualElement levelNext => this.Q("level-next");
        public Label levelNextLabel => this.Q<Label>("level-next-label");
        public Label levelNextCost => this.Q<Label>("level-next-cost");
        public Label levelMax => this.Q<Label>("level-max");
        public Label recipesValue => this.Q<Label>("recipes-value");
        public Label shopsValue => this.Q<Label>("shops-value");
        public Label tapValue => this.Q<Label>("tap-value");

        public PlayerStatsDialog()
        {
            AddToClassList(statsUssClassName);
        }

        /// <summary>Open on a state; the dialog follows it while it's open.</summary>
        public void Show(BakeryState state)
        {
            if (state == null)
                return;
            m_State = state;
            dataSource = state;
            Show(state.playerName, null, BagelLocalization.Get("stats.ok"), null);
            Refresh();

            m_Refresh ??= schedule.Execute(Refresh).Every(k_RefreshMs);
            m_Refresh.Resume();
        }

        /// <summary>Rewrites the text that isn't a single bound value: the mood's reason, the shop rows, the next level.</summary>
        public void Refresh()
        {
            if (m_State == null || !isOpen)
            {
                m_Refresh?.Pause();
                return;
            }

            var state = m_State;
            var mood = state.mood;
            if (moodName != null)
                moodName.text = BagelLocalization.Get("stats.mood." + mood.ToString().ToLowerInvariant());
            if (moodReason != null)
                moodReason.text = BagelLocalization.Get(mood switch
                {
                    MascotMood.Sleepy => "stats.mood.sleepy.why",
                    MascotMood.Excited => state.canAffordMilestone ? "stats.mood.excited.milestone" : "stats.mood.excited.recipe",
                    _ => "stats.mood.happy.why"
                });
            EnableInClassList(pausedUssClassName, state.isRegisterFull);

            RefreshShopRows(state);

            var catalog = state.catalog;
            var milestoneEvery = catalog ? catalog.milestoneEvery : 5;
            var gemReward = catalog ? catalog.milestoneGemReward : 50;
            var boost = catalog ? catalog.milestoneIncomeBoost : 0.5f;
            if (recipesBonusLabel != null)
                recipesBonusLabel.text = BagelLocalization.Get("stats.recipes-bonus", state.ownedRecipeCount, state.recipeCount);
            if (milestoneBoostLabel != null)
                milestoneBoostLabel.text = BagelLocalization.Get("stats.milestone-boost", Mathf.RoundToInt(boost * 100f));
            if (levelHow != null)
                levelHow.text = BagelLocalization.Get("stats.level.how", milestoneEvery, gemReward);

            var next = state.nextMilestoneShop;
            levelNext?.EnableInClassList(goneUssClassName, next == null);
            levelMax?.EnableInClassList(goneUssClassName, next != null);
            if (next != null)
            {
                if (levelNextLabel != null)
                    levelNextLabel.text = BagelLocalization.Get("stats.level.next", next.displayName, next.level + next.upgradesToNextMilestone);
                if (levelNextCost != null)
                    levelNextCost.text = next.costToNextMilestone.ToString("N0", CultureInfo.InvariantCulture);
            }

            if (recipesValue != null)
                recipesValue.text = $"{state.ownedRecipeCount}/{state.recipeCount}";
            if (shopsValue != null)
                shopsValue.text = $"{state.openShopCount}/{state.shopStates.Count}";
            if (tapValue != null)
                tapValue.text = "+" + state.tapBakeValue.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>One row per open shop: its signature pastry, name, level and coins per second.</summary>
        void RefreshShopRows(BakeryState state)
        {
            var rows = shopRows;
            if (rows == null)
                return;

            var open = new List<ShopState>();
            foreach (var shop in state.shopStates)
                if (shop.unlocked)
                    open.Add(shop);

            var same = open.Count == m_RowShops.Count;
            for (int i = 0; same && i < open.Count; i++)
                same = open[i] == m_RowShops[i];
            if (!same)
            {
                Discard.Children(rows);
                m_RowShops.Clear();
                foreach (var shop in open)
                {
                    rows.Add(MakeShopRow(shop));
                    m_RowShops.Add(shop);
                }
            }

            for (int i = 0; i < m_RowShops.Count; i++)
            {
                var shop = m_RowShops[i];
                var row = rows[i];
                row.Q<Label>("shop-name").text = shop.displayName;
                row.Q<Label>("shop-level").text = "Lv." + shop.level.ToString(CultureInfo.InvariantCulture);
                row.Q<Label>("shop-rate").text = BagelConverters.FormatRate(shop.incomePerSecond);
            }
        }

        static VisualElement MakeShopRow(ShopState shop)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(statsUssClassName + "__row");
            row.AddToClassList(shopRowUssClassName);

            var icon = new PastryIcon { kind = shop.signature, pickingMode = PickingMode.Ignore };
            icon.AddToClassList(statsUssClassName + "__row-icon");
            row.Add(icon);

            var name = new Label { name = "shop-name", pickingMode = PickingMode.Ignore };
            name.AddToClassList(statsUssClassName + "__row-label");
            row.Add(name);

            var level = new Label { name = "shop-level", pickingMode = PickingMode.Ignore };
            level.AddToClassList(statsUssClassName + "__row-level");
            row.Add(level);

            var rate = new Label { name = "shop-rate", pickingMode = PickingMode.Ignore };
            rate.AddToClassList(statsUssClassName + "__row-value");
            row.Add(rate);
            return row;
        }
    }
}
