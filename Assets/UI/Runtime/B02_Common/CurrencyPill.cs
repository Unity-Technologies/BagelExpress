using System;
using System.Globalization;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    public enum CurrencyKind
    {
        Coins,
        Gems,
        Tickets
    }

    /// <summary>
    /// HUD currency pill. When its bound value changes it counts up/down to the
    /// new value every frame using the zero-allocation TextElement.SetText(ReadOnlySpan&lt;char&gt;)
    /// overload, so a ticking counter never creates garbage strings.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Common")]
    public partial class CurrencyPill : VisualElement
    {
        public static readonly string ussClassName = "be-currency-pill";
        public static readonly string iconUssClassName = ussClassName + "__icon";
        public static readonly string badgeUssClassName = ussClassName + "__badge";
        public static readonly string valueUssClassName = ussClassName + "__value";
        public static readonly string plusUssClassName = ussClassName + "__plus";
        public static readonly UniqueStyleString bumpUssClassName = new(ussClassName + "--bump");
        static readonly UniqueStyleString[] s_KindClassNames =
        {
            new(ussClassName + "--coins"),
            new(ussClassName + "--gems"),
            new(ussClassName + "--tickets")
        };

        const float k_CountDurationSeconds = 0.6f;
        const long k_BumpDurationMs = 450;

        readonly CurrencyIcon m_Icon;
        readonly TextElement m_ValueLabel;
        readonly Button m_PlusButton;

        CurrencyKind m_Kind;
        long m_Value;
        bool m_HasValue;
        long m_DisplayedValue;
        long m_FromValue;
        float m_Elapsed;
        IVisualElementScheduledItem m_CountItem;
        IVisualElementScheduledItem m_UnbumpItem;

        public event Action plusClicked;

        [UxmlAttribute]
        public CurrencyKind kind
        {
            get => m_Kind;
            set
            {
                m_Kind = value;
                foreach (var className in s_KindClassNames)
                    RemoveFromClassList(className);
                AddToClassList(s_KindClassNames[(int)value]);
                m_Icon.kind = value;
                // Named for the Hierarchy and the Inspector: coins-icon, gems-icon, tickets-icon.
                m_Icon.name = value.ToString().ToLowerInvariant() + "-icon";
            }
        }

        [UxmlAttribute, CreateProperty]
        public long value
        {
            get => m_Value;
            set
            {
                if (m_Value == value)
                    return;

                var increased = value > m_Value;
                m_Value = value;

                // The first value (UXML import, first binding after a live reload) snaps.
                if (panel == null || !m_HasValue)
                {
                    m_HasValue = true;
                    // Not on screen yet (e.g. UXML import): just show the value.
                    m_DisplayedValue = value;
                    m_ValueLabel.text = value.ToString("N0", CultureInfo.InvariantCulture);
                    return;
                }

                StartCount();
                if (increased)
                    Bump();
            }
        }

        [UxmlAttribute]
        public bool showPlus
        {
            get => m_PlusButton.style.display == DisplayStyle.Flex;
            set => m_PlusButton.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public VisualElement icon => m_Icon;
        public Button plusButton => m_PlusButton;

        public CurrencyPill()
        {
            AddToClassList(ussClassName);

            // The icon sits in a round socket sized to the pill.
            var badge = new VisualElement { pickingMode = PickingMode.Ignore };
            badge.AddToClassList(badgeUssClassName);
            Add(badge);

            m_Icon = new CurrencyIcon { name = "coins-icon", pickingMode = PickingMode.Ignore };
            m_Icon.AddToClassList(iconUssClassName);
            badge.Add(m_Icon);

            m_ValueLabel = new TextElement { pickingMode = PickingMode.Ignore, text = "0" };
            m_ValueLabel.AddToClassList(valueUssClassName);
            Add(m_ValueLabel);

            m_PlusButton = new Button(() => plusClicked?.Invoke()) { text = "+" };
            m_PlusButton.AddToClassList(plusUssClassName);
            Add(m_PlusButton);

            kind = CurrencyKind.Coins;
            showPlus = false;
        }

        void StartCount()
        {
            if (!ArtKit.MotionAllowed(this))
            {
                m_DisplayedValue = m_Value;
                WriteDisplayedValue();
                return;
            }
            m_FromValue = m_DisplayedValue;
            m_Elapsed = 0f;
            m_CountItem ??= schedule.Execute(CountTick).Every(16);
            m_CountItem.Resume();
        }

        void CountTick(TimerState timer)
        {
            m_Elapsed += timer.deltaTime / 1000f;
            var t = Mathf.Clamp01(m_Elapsed / k_CountDurationSeconds);
            var eased = 1f - Mathf.Pow(1f - t, 3f);
            m_DisplayedValue = m_FromValue + (long)Math.Round((m_Value - m_FromValue) * (double)eased);
            WriteDisplayedValue();

            if (t >= 1f)
                m_CountItem.Pause();
        }

        /// <summary>Format into a stack buffer and hand the span to SetText: no string allocation.</summary>
        void WriteDisplayedValue()
        {
            Span<char> buffer = stackalloc char[32];
            if (m_DisplayedValue.TryFormat(buffer, out var written, "N0", CultureInfo.InvariantCulture))
                m_ValueLabel.SetText(buffer[..written]);
        }

        void Bump()
        {
            // The bump itself is a UIAnimationClip assigned to this class in USS.
            RemoveFromClassList(bumpUssClassName);
            AddToClassList(bumpUssClassName);
            m_UnbumpItem ??= schedule.Execute(() => RemoveFromClassList(bumpUssClassName));
            m_UnbumpItem.ExecuteLater(k_BumpDurationMs);
        }
    }
}
