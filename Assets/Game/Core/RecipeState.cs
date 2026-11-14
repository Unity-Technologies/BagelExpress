using System;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>One recipe in the Recipe Book: owned / new flags over a PastryDefinition.</summary>
    [Serializable]
    public class RecipeState : IDataSourceViewHashProvider
    {
        [SerializeField] bool m_Owned;
        [SerializeField] bool m_IsNew;

        [NonSerialized] BakeryState m_Owner;
        [NonSerialized] PastryDefinition m_Definition;
        [NonSerialized] long m_LocalVersion;

        public PastryDefinition definition => m_Definition;

        internal void Bind(BakeryState owner, PastryDefinition definition)
        {
            m_Owner = owner;
            m_Definition = definition;
        }

        [CreateProperty] public string id => m_Definition?.id;
        /// <summary>Name and description in the selected language (the definition's LocalizedStrings).</summary>
        [CreateProperty] public string displayName => m_Definition?.localizedName.GetLocalizedStringOr(m_Definition.displayName);
        [CreateProperty] public string description => m_Definition?.localizedDescription.GetLocalizedStringOr(m_Definition.description);
        [CreateProperty] public string japaneseName => m_Definition?.japaneseName;
        /// <summary>Decorative name in the other language: Japanese under English, English under Japanese.</summary>
        [CreateProperty] public string secondaryName => LocalizedText.isJapanese ? m_Definition?.displayName : japaneseName;
        [CreateProperty] public PastryKind kind => m_Definition?.kind ?? PastryKind.SesameBagel;
        [CreateProperty] public Rarity rarity => m_Definition?.rarity ?? Rarity.N;
        [CreateProperty] public Origin origin => m_Definition?.origin ?? Origin.Montreal;
        [CreateProperty] public bool owned => m_Owned;
        [CreateProperty] public bool locked => !m_Owned;
        [CreateProperty] public bool isNew => m_IsNew;
        [CreateProperty] public float incomeBonus => BakeryCatalog.RarityIncomeBonus(rarity);

        internal void SetOwned(bool owned, bool isNew)
        {
            m_Owned = owned;
            m_IsNew = owned && isNew;
            m_LocalVersion++;
        }

        public long GetViewHashCode() => (m_Owner ? m_Owner.version : 0) * 397 ^ m_LocalVersion;
    }
}
