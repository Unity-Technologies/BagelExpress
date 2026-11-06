using Unity.Properties;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>A recipe in the Recipe Book grid. Bindable properties only toggle USS classes.</summary>
    [UxmlElement(libraryPath = "Bagel/Recipes")]
    public partial class RecipeCard : VisualElement
    {
        public static readonly string ussClassName = "be-recipe-card";
        public static readonly UniqueStyleString lockedUssClassName = new(ussClassName + "--locked");
        public static readonly UniqueStyleString newUssClassName = new(ussClassName + "--new");
        static readonly UniqueStyleString[] s_RarityClassNames =
        {
            new(ussClassName + "--n"), new(ussClassName + "--r"), new(ussClassName + "--sr"), new(ussClassName + "--ssr"), new(ussClassName + "--ur")
        };

        bool m_Owned = true;
        bool m_IsNew;
        Rarity m_Rarity;

        [UxmlAttribute, CreateProperty]
        public bool owned
        {
            get => m_Owned;
            set { m_Owned = value; EnableInClassList(lockedUssClassName, !value); }
        }

        [UxmlAttribute, CreateProperty]
        public bool isNew
        {
            get => m_IsNew;
            set { m_IsNew = value; EnableInClassList(newUssClassName, value); }
        }

        [UxmlAttribute, CreateProperty]
        public Rarity rarity
        {
            get => m_Rarity;
            set
            {
                m_Rarity = value;
                for (int i = 0; i < s_RarityClassNames.Length; i++)
                    EnableInClassList(s_RarityClassNames[i], i == (int)value);
            }
        }

        public RecipeCard()
        {
            AddToClassList(ussClassName);
            rarity = Rarity.N;
        }
    }
}
