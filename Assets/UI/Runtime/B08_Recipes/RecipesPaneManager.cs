using Unity.Localization;
using Unity.Properties;
using Unity.SmartStrings.PersistentVariables;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Recipe Book: one RecipeCard per recipe (template + per-card data source), laid
    /// out by USS Grid (display: grid). Tapping a card opens the detail sheet.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class RecipesPaneManager : BakeryPane
    {
        public static readonly string ussClassName = "be-recipes";
        public static readonly UniqueStyleString sheetOpenUssClassName = new("be-recipes--sheet-open");

        VisualElement m_Grid;
        VisualElement m_Sheet;
        VisualElement m_SheetScrim;
        Button m_SheetClose;
        Label m_OwnedCountLabel;
        bool m_Bound;

        // Smart String "{owned} of {total:plural(en):1 recipe|{} recipes}" (JA: "レシピ {owned}/{total}").
        readonly IntVariable m_Owned = new();
        readonly IntVariable m_Total = new();
        readonly LocalizedString m_OwnedCountText = BagelLocalization.String("recipes.owned-count");

        [UxmlAttribute]
        public VisualTreeAsset cardTemplate { get; set; }

        public VisualElement grid => m_Grid ??= this.Q<VisualElement>("recipe-grid");
        public VisualElement sheet => m_Sheet ??= this.Q<VisualElement>("recipe-sheet");
        public VisualElement sheetScrim => m_SheetScrim ??= this.Q<VisualElement>("recipe-sheet-scrim");
        public Button sheetClose => m_SheetClose ??= this.Q<Button>("recipe-sheet-close");
        public Label ownedCountLabel => m_OwnedCountLabel ??= this.Q<Label>("owned-count");

        /// <summary>Data-bound to BakeryState.ownedRecipeCount; feeds the Smart String's {owned}.</summary>
        [CreateProperty]
        public int ownedCount
        {
            get => m_Owned.Value;
            set { if (m_Owned.Value != value) { m_Owned.Value = value; m_OwnedCountText.RefreshString(); } }
        }

        /// <summary>Data-bound to BakeryState.recipeCount; feeds the Smart String's {total}.</summary>
        [CreateProperty]
        public int totalCount
        {
            get => m_Total.Value;
            set { if (m_Total.Value != value) { m_Total.Value = value; m_OwnedCountText.RefreshString(); } }
        }

        public RecipesPaneManager()
        {
            AddToClassList(ussClassName);
            m_OwnedCountText.LocalVariables.Add(m_Owned, "owned");
            m_OwnedCountText.LocalVariables.Add(m_Total, "total");
            SetBinding(nameof(ownedCount), new DataBinding { dataSourcePath = new PropertyPath(nameof(BakeryState.ownedRecipeCount)), bindingMode = BindingMode.ToTarget });
            SetBinding(nameof(totalCount), new DataBinding { dataSourcePath = new PropertyPath(nameof(BakeryState.recipeCount)), bindingMode = BindingMode.ToTarget });
        }

        protected override void OnAttached()
        {
            if (!m_Bound)
            {
                m_Bound = true;
                sheetScrim?.RegisterCallback<ClickEvent>(_ => PlayerClosesSheet());
                sheetClose?.RegisterCallback<ClickEvent>(_ => PlayerClosesSheet());
                ownedCountLabel?.SetBinding("text", m_OwnedCountText);
            }
            // The detail sheet always shows a recipe, even before the first tap.
            if (sheet != null && sheet.dataSource is not RecipeState && state != null && state.recipeStates.Count > 0)
                sheet.dataSource = state.recipeStates[0];
            Rebuild();
        }

        protected override void OnStateChanged(BakeryState oldState, BakeryState newState)
        {
            if (sheet != null && newState != null && newState.recipeStates.Count > 0)
                sheet.dataSource = newState.recipeStates[0];
            if (panel != null)
                Rebuild();
        }

        /// <summary>Featured order: UR first (it spans 2x2 in the grid), then by rarity.</summary>
        public void Rebuild()
        {
            if (grid == null || state == null || cardTemplate == null)
                return;

            // New cards for every recipe: the old ones are gone for good, so their resources go back to be reused now.
            Discard.Children(grid);
            for (int r = (int)Rarity.UR; r >= (int)Rarity.N; r--)
            {
                foreach (var recipe in state.recipeStates)
                {
                    if ((int)recipe.rarity != r)
                        continue;

                    var item = cardTemplate.Instantiate();
                    item.AddToClassList("be-recipes__cell");
                    if (recipe.rarity == Rarity.UR)
                        item.AddToClassList("be-recipes__cell--featured");
                    item.dataSource = recipe;
                    var captured = recipe;
                    item.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (!BakeryAppManager.IsPinned(this))
                            OpenSheet(captured);
                    });
                    grid.Add(item);
                }
            }
        }

        public void OpenSheet(RecipeState recipe)
        {
            if (sheet == null || recipe == null)
                return;
            sheet.dataSource = recipe;
            AddToClassList(sheetOpenUssClassName);
            BagelSound.Play(sheet, "open");
        }

        public void CloseSheet() => RemoveFromClassList(sheetOpenUssClassName);

        void PlayerClosesSheet()
        {
            if (!BakeryAppManager.IsPinned(this))
                CloseSheet();
        }
    }
}
