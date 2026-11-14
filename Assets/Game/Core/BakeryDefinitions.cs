using System;
using Unity.Localization;
using UnityEngine;

namespace Bagel
{
    public enum Origin
    {
        Montreal,
        Japan
    }

    public enum Rarity
    {
        N,
        R,
        SR,
        SSR,
        UR
    }

    /// <summary>
    /// Begu-chan's mood (<see cref="BakeryState.mood"/>), which the mascot art shows: Sleepy when the register is full,
    /// Excited when something good is in reach, otherwise Happy.
    /// </summary>
    public enum MascotMood
    {
        Happy,
        Excited,
        Sleepy
    }

    public enum PastryKind
    {
        SesameBagel,
        PoppySeedBagel,
        EverythingBagel,
        MatchaBagel,
        SakuraMochiBagel,
        Anpan,
        MelonPan,
        MapleMelonPan,
        CurryPan,
        Taiyaki,
        GoldenMapleTaiyaki,
        Dorayaki
    }

    [Serializable]
    public class ShopDefinition
    {
        public string id;
        [Tooltip("English source name, used when the localized name can't resolve.")]
        public string displayName;
        [Tooltip("Decorative Japanese name shown under the English one.")]
        public string japaneseName;
        [Tooltip("Name in the selected language (BagelStrings table).")]
        public LocalizedString localizedName = new();
        public Origin origin;
        public PastryKind signature;
        public double baseIncome = 1;
        public double baseUpgradeCost = 50;
        public double upgradeCostGrowth = 1.18;
        public double unlockCost;
    }

    [Serializable]
    public class PastryDefinition
    {
        public string id;
        [Tooltip("English source name, used when the localized name can't resolve.")]
        public string displayName;
        [Tooltip("Decorative Japanese name shown under the English one.")]
        public string japaneseName;
        [TextArea] public string description;
        [Tooltip("Name in the selected language (BagelStrings table).")]
        public LocalizedString localizedName = new();
        [Tooltip("Description in the selected language (BagelStrings table).")]
        public LocalizedString localizedDescription = new();
        public PastryKind kind;
        public Rarity rarity;
        public Origin origin;
    }

    public struct DrawResult
    {
        public PastryDefinition pastry;
        public bool isNew;
        public long duplicateCoins;
    }
}
