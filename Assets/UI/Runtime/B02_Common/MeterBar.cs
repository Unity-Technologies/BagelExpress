using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Glossy progress meter. The fill width animates through a USS transition;
    /// bind <see cref="value"/> (0..1) from UXML.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Common")]
    public partial class MeterBar : VisualElement
    {
        public static readonly string ussClassName = "be-meter";
        public static readonly string trackUssClassName = ussClassName + "__track";
        public static readonly string fillUssClassName = ussClassName + "__fill";
        /// <summary>At zero: the fill (never narrower than it is tall, so its ends stay round) hides.</summary>
        public static readonly UniqueStyleString emptyUssClassName = new(ussClassName + "--empty");
        public static readonly string glossUssClassName = ussClassName + "__gloss";
        public static readonly UniqueStyleString fullUssClassName = new(ussClassName + "--full");

        readonly VisualElement m_Fill;
        float m_Value;

        [UxmlAttribute, CreateProperty]
        public float value
        {
            get => m_Value;
            set
            {
                m_Value = Mathf.Clamp01(value);
                m_Fill.style.width = Length.Percent(m_Value * 100f);
                EnableInClassList(fullUssClassName, m_Value >= 0.999f);
                EnableInClassList(emptyUssClassName, m_Value <= 0f);
            }
        }

        public MeterBar()
        {
            AddToClassList(ussClassName);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList(trackUssClassName);
            Add(track);

            m_Fill = new VisualElement { pickingMode = PickingMode.Ignore };
            m_Fill.AddToClassList(fillUssClassName);
            track.Add(m_Fill);

            var gloss = new VisualElement { pickingMode = PickingMode.Ignore };
            gloss.AddToClassList(glossUssClassName);
            m_Fill.Add(gloss);

            value = 0.5f;
        }
    }
}
