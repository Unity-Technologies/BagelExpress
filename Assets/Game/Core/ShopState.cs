using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// One bakery's progress. Used as the per-item data source of the Shops ListView.
    /// Its view hash follows the owner state so "can afford" refreshes when coins change.
    /// </summary>
    [Serializable]
    public class ShopState : IDataSourceViewHashProvider
    {
        [SerializeField] int m_Level = 1;
        [SerializeField] bool m_Unlocked;

        [NonSerialized] BakeryState m_Owner;
        [NonSerialized] ShopDefinition m_Definition;
        [NonSerialized] long m_LocalVersion;

        public ShopDefinition definition => m_Definition;

        internal void Bind(BakeryState owner, ShopDefinition definition)
        {
            m_Owner = owner;
            m_Definition = definition;
            m_Level = Mathf.Max(1, m_Level);
        }

        [CreateProperty] public string id => m_Definition?.id;
        /// <summary>Name in the selected language (the definition's LocalizedString).</summary>
        [CreateProperty] public string displayName => m_Definition?.localizedName.GetLocalizedStringOr(m_Definition.displayName);
        [CreateProperty] public string japaneseName => m_Definition?.japaneseName;
        /// <summary>Decorative name in the other language: Japanese under English, English under Japanese.</summary>
        [CreateProperty] public string secondaryName => LocalizedText.isJapanese ? m_Definition?.displayName : japaneseName;
        [CreateProperty] public Origin origin => m_Definition?.origin ?? Origin.Montreal;
        [CreateProperty] public PastryKind signature => m_Definition?.signature ?? PastryKind.SesameBagel;
        [CreateProperty] public bool isMontreal => origin == Origin.Montreal;

        [CreateProperty] public int level => m_Level;
        [CreateProperty] public bool unlocked => m_Unlocked;
        [CreateProperty] public bool locked => !m_Unlocked;

        int milestoneEvery => m_Owner && m_Owner.catalog ? m_Owner.catalog.milestoneEvery : 5;
        int maxLevel => m_Owner && m_Owner.catalog ? m_Owner.catalog.maxShopLevel : 50;

        [CreateProperty] public bool isMaxLevel => m_Level >= maxLevel;
        [CreateProperty] public int milestoneCount => m_Level / milestoneEvery;
        [CreateProperty] public int nextMilestone => (m_Level / milestoneEvery + 1) * milestoneEvery;
        [CreateProperty] public float milestoneProgress => (m_Level % milestoneEvery) / (float)milestoneEvery;
        [CreateProperty] public bool isNextUpgradeMilestone => (m_Level + 1) % milestoneEvery == 0;

        [CreateProperty]
        public double incomePerSecond
        {
            get
            {
                if (!m_Unlocked || m_Definition == null)
                    return 0;
                var boost = m_Owner && m_Owner.catalog ? m_Owner.catalog.milestoneIncomeBoost : 0.5f;
                var multiplier = m_Owner ? m_Owner.recipeMultiplier : 1f;
                return m_Definition.baseIncome * m_Level * (1.0 + boost * milestoneCount) * multiplier;
            }
        }

        [CreateProperty]
        public double nextIncomePerSecond
        {
            get
            {
                if (m_Definition == null)
                    return 0;
                var boost = m_Owner && m_Owner.catalog ? m_Owner.catalog.milestoneIncomeBoost : 0.5f;
                var multiplier = m_Owner ? m_Owner.recipeMultiplier : 1f;
                var next = m_Level + 1;
                return m_Definition.baseIncome * next * (1.0 + boost * (next / milestoneEvery)) * multiplier;
            }
        }

        [CreateProperty]
        public long upgradeCost => UpgradeCostAt(m_Level);

        /// <summary>The upgrades left to the next milestone (0 at the max level).</summary>
        [CreateProperty] public int upgradesToNextMilestone => isMaxLevel ? 0 : Mathf.Min(nextMilestone, maxLevel) - m_Level;

        /// <summary>What those upgrades cost in all.</summary>
        [CreateProperty]
        public long costToNextMilestone
        {
            get
            {
                long total = 0;
                for (int i = 0; i < upgradesToNextMilestone; i++)
                    total += UpgradeCostAt(m_Level + i);
                return total;
            }
        }

        long UpgradeCostAt(int level) => m_Definition == null
            ? 0
            : (long)Math.Round(m_Definition.baseUpgradeCost * Math.Pow(m_Definition.upgradeCostGrowth, level - 1));

        [CreateProperty] public long unlockCost => (long)(m_Definition?.unlockCost ?? 0);
        [CreateProperty] public bool canAffordUpgrade => m_Unlocked && !isMaxLevel && m_Owner && m_Owner.coins >= upgradeCost;
        [CreateProperty] public bool canAffordUnlock => !m_Unlocked && m_Owner && m_Owner.coins >= unlockCost;
        /// <summary>The card's main action is possible: unlock when locked, upgrade when open.</summary>
        [CreateProperty] public bool canAct => m_Unlocked ? canAffordUpgrade : canAffordUnlock;

        internal void SetLevel(int level)
        {
            m_Level = Mathf.Clamp(level, 1, maxLevel);
            m_LocalVersion++;
        }

        internal void SetUnlocked(bool unlocked)
        {
            m_Unlocked = unlocked;
            m_LocalVersion++;
        }

        public long GetViewHashCode() => (m_Owner ? m_Owner.version : 0) * 397 ^ m_LocalVersion;
    }
}
