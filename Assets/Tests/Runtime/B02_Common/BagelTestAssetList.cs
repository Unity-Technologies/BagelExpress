using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Every asset the UI tests load, in one place (Resources/BagelTestAssetList.asset),
    /// so tests run the same in Edit mode, Play mode and players.
    /// </summary>
    [CreateAssetMenu(fileName = "BagelTestAssetList", menuName = "Bagel/Bagel Test Asset List")]
    public class BagelTestAssetList : ScriptableObject
    {
        public string mainSceneName = "Main";

        [Header("Theme and game state")]
        public ThemeStyleSheet theme;
        public BakeryState startState;

        [Header("3 - App")]
        public VisualTreeAsset appScreenUxml;

        [Header("4 - Title")]
        public VisualTreeAsset titleScreenUxml;

        [Header("5 - Home")]
        public VisualTreeAsset homePageUxml;

        [Header("6 - Shops")]
        public VisualTreeAsset shopsPageUxml;
        public VisualTreeAsset shopCardUxml;

        [Header("7 - Lucky Bag")]
        public VisualTreeAsset luckyBagPageUxml;

        [Header("8 - Recipes")]
        public VisualTreeAsset recipesPageUxml;
        public VisualTreeAsset recipeCardUxml;

        [Header("9 - Settings")]
        public VisualTreeAsset settingsPageUxml;
    }
}
