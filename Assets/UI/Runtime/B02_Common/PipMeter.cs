using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>Row of pips (e.g. progress to the next milestone). Bind <see cref="value"/> (0..1).</summary>
    [UxmlElement(libraryPath = "Bagel/Common")]
    public partial class PipMeter : VisualElement
    {
        public static readonly string ussClassName = "be-pips";
        public static readonly string pipUssClassName = ussClassName + "__pip";
        public static readonly UniqueStyleString pipFilledUssClassName = new(pipUssClassName + "--filled");

        int m_Count = 5;
        float m_Value;

        [UxmlAttribute]
        public int count
        {
            get => m_Count;
            set { m_Count = Mathf.Max(1, value); Rebuild(); }
        }

        [UxmlAttribute, CreateProperty]
        public float value
        {
            get => m_Value;
            set { m_Value = Mathf.Clamp01(value); Refresh(); }
        }

        public PipMeter()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            Rebuild();
        }

        void Rebuild()
        {
            Discard.Children(this);
            for (int i = 0; i < m_Count; i++)
            {
                var pip = new VisualElement { pickingMode = PickingMode.Ignore };
                pip.AddToClassList(pipUssClassName);
                Add(pip);
            }
            Refresh();
        }

        void Refresh()
        {
            var filled = Mathf.RoundToInt(m_Value * m_Count);
            for (int i = 0; i < childCount; i++)
                this[i].EnableInClassList(pipFilledUssClassName, i < filled);
        }
    }
}
