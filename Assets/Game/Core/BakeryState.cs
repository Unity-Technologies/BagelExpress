using System;
using System.Collections.Generic;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The whole game state. Plain data + rules, no UI. It is a data source for
    /// UI Toolkit runtime bindings: [CreateProperty] exposes values, and
    /// INotifyBindablePropertyChanged / IDataSourceViewHashProvider tell the
    /// binding system exactly when something changed.
    /// </summary>
    [CreateAssetMenu(fileName = "BakeryState", menuName = "Bagel/Bakery State")]
    public class BakeryState : ScriptableObject, INotifyBindablePropertyChanged, IDataSourceViewHashProvider
    {
        [SerializeField] BakeryCatalog m_Catalog;
        [SerializeField] string m_PlayerName = "Begu-chan";
        [SerializeField] int m_PlayerLevel = 12;
        [SerializeField] long m_Coins = 1250;
        [SerializeField] int m_Gems = 300;
        [SerializeField] int m_Tickets = 6;
        [SerializeField] double m_Register = 380;
        [SerializeField] int m_TotalDraws;
        [SerializeField] bool m_TutorialDone;
        [SerializeField] List<ShopState> m_Shops = new();
        [SerializeField] List<RecipeState> m_Recipes = new();

        long m_Version;
        MascotMood m_NotifiedMood;
        System.Random m_Random = new System.Random();

        public event EventHandler<BindablePropertyChangedEventArgs> propertyChanged;

        /// <summary>Raised after coins are collected from the register (amount).</summary>
        public event Action<long> coinsCollected;
        /// <summary>Raised when a shop reaches a milestone level (shop, gem reward).</summary>
        public event Action<ShopState, int> milestoneReached;

        public BakeryCatalog catalog => m_Catalog;
        public IReadOnlyList<ShopState> shopStates => m_Shops;
        public IReadOnlyList<RecipeState> recipeStates => m_Recipes;

        // ---------------------------------------------------------------- Bindable properties

        [CreateProperty]
        public string playerName
        {
            get => m_PlayerName;
            set { if (m_PlayerName == value) return; m_PlayerName = value; Notify(); }
        }

        [CreateProperty]
        public int playerLevel
        {
            get => m_PlayerLevel;
            set { if (m_PlayerLevel == value) return; m_PlayerLevel = value; Notify(); }
        }

        [CreateProperty]
        public long coins
        {
            get => m_Coins;
            set
            {
                if (m_Coins == value) return;
                m_Coins = value;
                Notify();
                Notify(nameof(upgradeableShopCount));
                NotifyMoodIfChanged();
            }
        }

        [CreateProperty]
        public int gems
        {
            get => m_Gems;
            set { if (m_Gems == value) return; m_Gems = value; Notify(); Notify(nameof(canBuyTicket)); }
        }

        [CreateProperty]
        public int tickets
        {
            get => m_Tickets;
            set
            {
                if (m_Tickets == value) return;
                m_Tickets = value;
                Notify();
                Notify(nameof(canDrawOne));
                Notify(nameof(canDrawFive));
            }
        }

        /// <summary>The first-time tutorial was played through or skipped: it doesn't come back on the next launch.</summary>
        [CreateProperty]
        public bool tutorialDone
        {
            get => m_TutorialDone;
            set { if (m_TutorialDone == value) return; m_TutorialDone = value; Notify(); }
        }

        [CreateProperty] public List<ShopState> shops => m_Shops;
        [CreateProperty] public List<RecipeState> recipes => m_Recipes;

        [CreateProperty] public double register => m_Register;
        [CreateProperty] public double registerCapacity => Math.Max(1.0, incomePerSecond * (m_Catalog ? m_Catalog.registerCapacitySeconds : 90f));
        [CreateProperty] public float registerFill => (float)Math.Clamp(m_Register / registerCapacity, 0.0, 1.0);
        [CreateProperty] public bool isRegisterFull => registerFill >= 0.999f;

        [CreateProperty]
        public double incomePerSecond
        {
            get
            {
                double total = 0;
                foreach (var shop in m_Shops)
                    total += shop.incomePerSecond;
                return total;
            }
        }

        /// <summary>Global multiplier from owned recipes (1.0 = no bonus).</summary>
        [CreateProperty]
        public float recipeMultiplier
        {
            get
            {
                float bonus = 1f;
                foreach (var recipe in m_Recipes)
                    if (recipe.owned)
                        bonus += BakeryCatalog.RarityIncomeBonus(recipe.definition.rarity);
                return bonus;
            }
        }

        [CreateProperty] public int ownedRecipeCount => m_Recipes.FindAll(r => r.owned).Count;
        [CreateProperty] public int recipeCount => m_Recipes.Count;
        [CreateProperty] public int newRecipeCount => m_Recipes.FindAll(r => r.isNew).Count;
        [CreateProperty] public int openShopCount => m_Shops.FindAll(s => s.unlocked).Count;
        [CreateProperty] public int upgradeableShopCount => m_Shops.FindAll(s => s.unlocked ? s.canAffordUpgrade : s.canAffordUnlock).Count;
        [CreateProperty] public bool canDrawOne => m_Tickets >= 1;
        [CreateProperty] public bool canDrawFive => m_Tickets >= 5;
        [CreateProperty] public bool canBuyTicket => m_Catalog && m_Gems >= m_Catalog.ticketGemCost;
        [CreateProperty] public int tapBakeValue => (m_Catalog ? m_Catalog.tapBakeCoins : 5) + (m_Shops.Count > 0 ? m_Shops[0].level : 0);

        /// <summary>An open shop's next upgrade is a milestone, and there are coins for it.</summary>
        [CreateProperty] public bool canAffordMilestone => m_Shops.Exists(s => s.unlocked && s.isNextUpgradeMilestone && s.canAffordUpgrade);

        /// <summary>
        /// Begu-chan's mood, from how the bakery is doing: Sleepy when the register is full (the ovens wait for a
        /// collect, so no coins come in), Excited when a milestone upgrade is affordable or a new recipe is waiting,
        /// otherwise Happy.
        /// </summary>
        [CreateProperty]
        public MascotMood mood
        {
            get
            {
                if (isRegisterFull)
                    return MascotMood.Sleepy;
                if (canAffordMilestone || HasNewRecipe())
                    return MascotMood.Excited;
                return MascotMood.Happy;
            }
        }

        // Without FindAll: the mood is worked out on every tick.
        bool HasNewRecipe()
        {
            foreach (var recipe in m_Recipes)
                if (recipe.isNew)
                    return true;
            return false;
        }

        /// <summary>
        /// The open shop closest to its next milestone (the fewest upgrades, then the cheapest), the way to the next
        /// player level; null if every open shop is at the max level.
        /// </summary>
        public ShopState nextMilestoneShop
        {
            get
            {
                ShopState best = null;
                foreach (var shop in m_Shops)
                {
                    if (!shop.unlocked || shop.isMaxLevel)
                        continue;
                    if (best == null || shop.upgradesToNextMilestone < best.upgradesToNextMilestone ||
                        (shop.upgradesToNextMilestone == best.upgradesToNextMilestone && shop.costToNextMilestone < best.costToNextMilestone))
                        best = shop;
                }
                return best;
            }
        }

        // ---------------------------------------------------------------- Saving

        /// <summary>
        /// What a save keeps of the state, for JsonUtility. Shops and recipes are matched by id, so a save survives
        /// catalog changes (new shops start closed, removed ones are dropped); the catalog itself comes from the game.
        /// </summary>
        [Serializable]
        public class Saved
        {
            public int version = 1;
            public string playerName;
            public int playerLevel;
            public long coins;
            public int gems;
            public int tickets;
            public double register;
            public int totalDraws;
            public bool tutorialDone;
            public List<SavedShop> shops = new();
            public List<SavedRecipe> recipes = new();
        }

        [Serializable]
        public class SavedShop
        {
            public string id;
            public int level;
            public bool unlocked;
        }

        [Serializable]
        public class SavedRecipe
        {
            public string id;
            public bool owned;
            public bool isNew;
        }

        public Saved ToSaved()
        {
            var saved = new Saved
            {
                playerName = m_PlayerName, playerLevel = m_PlayerLevel, coins = m_Coins, gems = m_Gems, tickets = m_Tickets,
                register = m_Register, totalDraws = m_TotalDraws, tutorialDone = m_TutorialDone
            };
            foreach (var shop in m_Shops)
                saved.shops.Add(new SavedShop { id = shop.id, level = shop.level, unlocked = shop.unlocked });
            foreach (var recipe in m_Recipes)
                saved.recipes.Add(new SavedRecipe { id = recipe.id, owned = recipe.owned, isNew = recipe.isNew });
            return saved;
        }

        /// <summary>Take the values of a save (or of another state, through its <see cref="ToSaved"/>) and refresh every bound UI.</summary>
        public void Load(Saved saved)
        {
            if (saved == null)
                return;
            m_PlayerName = saved.playerName;
            m_PlayerLevel = saved.playerLevel;
            m_Coins = saved.coins;
            m_Gems = saved.gems;
            m_Tickets = saved.tickets;
            m_Register = saved.register;
            m_TotalDraws = saved.totalDraws;
            m_TutorialDone = saved.tutorialDone;
            foreach (var shop in m_Shops)
            {
                var s = saved.shops.Find(x => x.id == shop.id);
                shop.SetLevel(s?.level ?? 1);
                shop.SetUnlocked(s?.unlocked ?? false);
            }
            foreach (var recipe in m_Recipes)
            {
                var r = saved.recipes.Find(x => x.id == recipe.id);
                recipe.SetOwned(r?.owned ?? false, r?.isNew ?? false);
            }
            NotifyAll();
        }

        /// <summary>Back to a template's values, e.g. the demo's start state (Settings › Reset progress).</summary>
        public void ResetTo(BakeryState template)
        {
            if (template != null)
                Load(template.ToSaved());
        }

        // ---------------------------------------------------------------- Lifetime

        /// <summary>Clone a template (e.g. the demo start state) into a throwaway runtime instance.</summary>
        public static BakeryState CreateRuntimeCopy(BakeryState template)
        {
            var copy = template != null ? Instantiate(template) : CreateInstance<BakeryState>();
            copy.name = template != null ? template.name + " (Runtime)" : "BakeryState (Runtime)";
            copy.hideFlags = HideFlags.DontSave;
            copy.Initialize();
            return copy;
        }

        /// <summary>Destroy a copy made by <see cref="CreateRuntimeCopy"/> (works in Edit and Play mode).</summary>
        public static void DestroyRuntimeCopy(BakeryState copy)
        {
            if (copy == null)
                return;
            if (Application.isPlaying)
                Destroy(copy);
            else
                DestroyImmediate(copy);
        }

        void OnEnable() => Initialize();

        void OnValidate()
        {
            // Inspector edits (e.g. in the Bagel UI window) refresh every bound UI.
            Initialize();
            NotifyAll();
        }

        /// <summary>Make sure shop/recipe states line up with the catalog and know their owner.</summary>
        public void Initialize()
        {
            if (m_Catalog == null)
                return;

            for (int i = 0; i < m_Catalog.shops.Count; i++)
            {
                if (i >= m_Shops.Count)
                    m_Shops.Add(new ShopState());
                m_Shops[i].Bind(this, m_Catalog.shops[i]);
            }
            if (m_Shops.Count > m_Catalog.shops.Count)
                m_Shops.RemoveRange(m_Catalog.shops.Count, m_Shops.Count - m_Catalog.shops.Count);

            for (int i = 0; i < m_Catalog.pastries.Count; i++)
            {
                if (i >= m_Recipes.Count)
                    m_Recipes.Add(new RecipeState());
                m_Recipes[i].Bind(this, m_Catalog.pastries[i]);
            }
            if (m_Recipes.Count > m_Catalog.pastries.Count)
                m_Recipes.RemoveRange(m_Catalog.pastries.Count, m_Recipes.Count - m_Catalog.pastries.Count);
        }

        // ---------------------------------------------------------------- Rules

        /// <summary>Advance the idle simulation: shops fill the register.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || isRegisterFull)
                return;

            m_Register = Math.Min(registerCapacity, m_Register + incomePerSecond * deltaSeconds);
            Notify(nameof(register));
            Notify(nameof(registerFill));
            Notify(nameof(isRegisterFull));
            NotifyMoodIfChanged();
        }

        /// <summary>Empty the register into the wallet. Returns the amount collected.</summary>
        public long Collect()
        {
            var amount = (long)Math.Floor(m_Register);
            if (amount <= 0)
                return 0;

            m_Register -= amount;
            coins += amount;
            Notify(nameof(register));
            Notify(nameof(registerFill));
            Notify(nameof(isRegisterFull));
            NotifyMoodIfChanged();
            coinsCollected?.Invoke(amount);
            return amount;
        }

        /// <summary>Tap the storefront to bake by hand.</summary>
        public long TapBake()
        {
            var amount = tapBakeValue;
            coins += amount;
            return amount;
        }

        public bool TryUpgradeShop(int index, out bool milestone)
        {
            milestone = false;
            if (index < 0 || index >= m_Shops.Count)
                return false;

            var shop = m_Shops[index];
            if (!shop.unlocked || shop.isMaxLevel || !shop.canAffordUpgrade)
                return false;

            coins -= shop.upgradeCost;
            shop.SetLevel(shop.level + 1);

            milestone = shop.level % m_Catalog.milestoneEvery == 0;
            if (milestone)
            {
                gems += m_Catalog.milestoneGemReward;
                playerLevel += 1;
                milestoneReached?.Invoke(shop, m_Catalog.milestoneGemReward);
            }

            NotifyEconomy();
            return true;
        }

        public bool TryUnlockShop(int index)
        {
            if (index < 0 || index >= m_Shops.Count)
                return false;

            var shop = m_Shops[index];
            if (shop.unlocked || !shop.canAffordUnlock)
                return false;

            coins -= (long)shop.definition.unlockCost;
            shop.SetUnlocked(true);
            NotifyEconomy();
            Notify(nameof(openShopCount));
            return true;
        }

        /// <summary>Every shop open, at the level it has: a cheat (the Editor's Bagel UI window), free.</summary>
        public void UnlockAllShops()
        {
            foreach (var shop in m_Shops)
                if (!shop.unlocked)
                    shop.SetUnlocked(true);
            NotifyEconomy();
            Notify(nameof(openShopCount));
        }

        /// <summary>Every recipe in the book, already seen (no NEW on them): a cheat (the Editor's Bagel UI window).</summary>
        public void OwnAllRecipes()
        {
            foreach (var recipe in m_Recipes)
                if (!recipe.owned)
                    recipe.SetOwned(true, isNew: false);
            NotifyEconomy();
            Notify(nameof(ownedRecipeCount));
            Notify(nameof(newRecipeCount));
        }

        public bool TryBuyTicket()
        {
            if (!canBuyTicket)
                return false;

            gems -= m_Catalog.ticketGemCost;
            tickets += 1;
            return true;
        }

        /// <summary>Lucky Bag draw. Fills results and returns true if tickets were spent.</summary>
        public bool TryDraw(int count, List<DrawResult> results)
        {
            results.Clear();
            if (m_Catalog == null || count <= 0 || m_Tickets < count)
                return false;

            tickets -= count;
            for (int i = 0; i < count; i++)
            {
                PastryDefinition pastry;
                if (m_TotalDraws == 0 && !string.IsNullOrEmpty(m_Catalog.firstDrawRecipeId))
                    pastry = m_Catalog.FindPastry(m_Catalog.firstDrawRecipeId);
                else if (count > 1 && i == count - 1 && !results.Exists(r => r.pastry.rarity >= m_Catalog.multiDrawMinimumRarity))
                    pastry = RollPastry(m_Catalog.multiDrawMinimumRarity);
                else
                    pastry = RollPastry(Rarity.N);

                m_TotalDraws++;
                var recipe = m_Recipes.Find(r => r.definition == pastry);
                var result = new DrawResult { pastry = pastry, isNew = !recipe.owned };
                if (recipe.owned)
                {
                    result.duplicateCoins = m_Catalog.duplicateCoinValue * (1 + (int)pastry.rarity);
                    coins += result.duplicateCoins;
                }
                else
                {
                    recipe.SetOwned(true, isNew: true);
                }
                results.Add(result);
            }

            NotifyEconomy();
            Notify(nameof(ownedRecipeCount));
            Notify(nameof(newRecipeCount));
            NotifyMoodIfChanged();
            return true;
        }

        public void MarkRecipesSeen()
        {
            foreach (var recipe in m_Recipes)
                recipe.SetOwned(recipe.owned, isNew: false);
            Notify(nameof(newRecipeCount));
            NotifyMoodIfChanged();
        }

        PastryDefinition RollPastry(Rarity minimum)
        {
            float total = 0f;
            for (int r = (int)minimum; r < m_Catalog.rarityWeights.Length; r++)
                total += m_Catalog.rarityWeights[r];

            var roll = (float)m_Random.NextDouble() * total;
            var rarity = minimum;
            for (int r = (int)minimum; r < m_Catalog.rarityWeights.Length; r++)
            {
                roll -= m_Catalog.rarityWeights[r];
                if (roll <= 0f) { rarity = (Rarity)r; break; }
            }

            var pool = m_Catalog.pastries.FindAll(p => p.rarity == rarity);
            if (pool.Count == 0)
                pool = m_Catalog.pastries;
            return pool[m_Random.Next(pool.Count)];
        }

        // ---------------------------------------------------------------- Notifications

        internal long version => m_Version;

        public long GetViewHashCode() => m_Version;

        void NotifyEconomy()
        {
            Notify(nameof(incomePerSecond));
            Notify(nameof(recipeMultiplier));
            Notify(nameof(registerCapacity));
            Notify(nameof(registerFill));
            Notify(nameof(upgradeableShopCount));
            Notify(nameof(canAffordMilestone));
            NotifyMoodIfChanged();
        }

        void NotifyMoodIfChanged()
        {
            var current = mood;
            if (current == m_NotifiedMood)
                return;
            m_NotifiedMood = current;
            Notify(nameof(mood));
        }

        public void NotifyAll()
        {
            foreach (var property in s_AllProperties)
                Notify(property);
        }

        static readonly string[] s_AllProperties =
        {
            nameof(playerName), nameof(playerLevel), nameof(coins), nameof(gems), nameof(tickets),
            nameof(shops), nameof(recipes), nameof(register), nameof(registerCapacity), nameof(registerFill),
            nameof(isRegisterFull), nameof(incomePerSecond), nameof(recipeMultiplier), nameof(ownedRecipeCount),
            nameof(recipeCount), nameof(newRecipeCount), nameof(openShopCount), nameof(upgradeableShopCount),
            nameof(canDrawOne), nameof(canDrawFive), nameof(canBuyTicket), nameof(tapBakeValue),
            nameof(canAffordMilestone), nameof(mood), nameof(tutorialDone)
        };

        void Notify([System.Runtime.CompilerServices.CallerMemberName] string property = "")
        {
            m_Version++;
            propertyChanged?.Invoke(this, new BindablePropertyChangedEventArgs(property));
        }
    }
}
