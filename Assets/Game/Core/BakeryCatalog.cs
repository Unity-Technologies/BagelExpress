using System.Collections.Generic;
using UnityEngine;

namespace Bagel
{
    [CreateAssetMenu(fileName = "BakeryCatalog", menuName = "Bagel/Bakery Catalog")]
    public class BakeryCatalog : ScriptableObject
    {
        public List<ShopDefinition> shops = new();
        public List<PastryDefinition> pastries = new();

        [Header("Economy")]
        public int maxShopLevel = 50;
        public int milestoneEvery = 5;
        public int milestoneGemReward = 50;
        public float milestoneIncomeBoost = 0.5f;
        public int ticketGemCost = 60;
        public int tapBakeCoins = 5;
        public float registerCapacitySeconds = 90f;

        [Header("Lucky Bag")]
        public float[] rarityWeights = { 52f, 30f, 12f, 5f, 1f };
        public Rarity multiDrawMinimumRarity = Rarity.SR;
        [Tooltip("Beginner banner: the very first draw always gives this recipe.")]
        public string firstDrawRecipeId = "sakura-mochi-bagel";
        public long duplicateCoinValue = 150;

        public static float RarityIncomeBonus(Rarity rarity) => rarity switch
        {
            Rarity.N => 0.05f,
            Rarity.R => 0.10f,
            Rarity.SR => 0.25f,
            Rarity.SSR => 0.50f,
            Rarity.UR => 1.00f,
            _ => 0f
        };

        public PastryDefinition FindPastry(string id) => pastries.Find(p => p.id == id);
        public PastryDefinition FindPastry(PastryKind kind) => pastries.Find(p => p.kind == kind);
    }
}
