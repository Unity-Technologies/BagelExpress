using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// Manga "focus lines" (集中線) drawn with Painter2D: thin ink wedges that start at the
    /// element's edges and point at its center, leaving an elliptical clear zone in the middle.
    /// The pattern re-rolls a few times per second so the lines flicker like a hand-drawn panel.
    /// </summary>
    /// <remarks>
    /// Custom USS property: <c>--be-speed-lines-color</c> (defaults to the ink color).
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class SpeedLines : VisualElement
    {
        public static readonly string ussClassName = "be-speed-lines";

        static readonly CustomStyleProperty<Color> s_ColorProperty = new("--be-speed-lines-color");

        readonly ArtStyleResolver m_Style;
        Color m_Color = Alpha(Ink, 0.9f);
        int m_LineCount = 96;
        float m_ClearRadius = 0.62f;
        float m_JitterFps = 10f;
        int m_Frame;
        float m_Elapsed;

        /// <summary>Number of wedges around the circle.</summary>
        [UxmlAttribute]
        public int lineCount
        {
            get => m_LineCount;
            set
            {
                m_LineCount = Mathf.Clamp(value, 0, 400);
                MarkDirtyRepaint();
            }
        }

        /// <summary>Size of the empty center, as a fraction of the element's half-width / half-height.</summary>
        [UxmlAttribute]
        public float clearRadius
        {
            get => m_ClearRadius;
            set
            {
                m_ClearRadius = Mathf.Clamp(value, 0f, 1.5f);
                MarkDirtyRepaint();
            }
        }

        /// <summary>How many times per second the pattern re-rolls (0 = static).</summary>
        [UxmlAttribute]
        public float jitterFps
        {
            get => m_JitterFps;
            set => m_JitterFps = Mathf.Max(0f, value);
        }

        public SpeedLines()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
            schedule.Execute(Tick).Every(16);
        }

        void OnStyleChanged()
        {
            m_Color = m_Style.Get(s_ColorProperty, Alpha(Ink, 0.9f));
            MarkDirtyRepaint();
        }

        void Tick(TimerState timer)
        {
            // A display:none ancestor collapses the layout, so a tiny rect also means "hidden".
            if (m_JitterFps <= 0f || !(contentRect.width >= 4f) || resolvedStyle.display == DisplayStyle.None || resolvedStyle.opacity <= 0f ||
                !ArtKit.MotionAllowed(this))
                return;

            m_Elapsed += timer.deltaTime / 1000f;
            if (m_Elapsed < 1f / m_JitterFps)
                return;

            m_Elapsed = 0f;
            m_Frame++;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (m_LineCount == 0 || !(rect.width >= 4f) || !(rect.height >= 4f))
                return;

            var painter = mgc.painter2D;
            var center = rect.center;
            var half = rect.size * 0.5f;
            var outer = half.magnitude * 1.08f;
            var clear = half * m_ClearRadius;

            painter.fillColor = m_Color;
            painter.BeginPath();
            for (int i = 0; i < m_LineCount; i++)
            {
                var key = i * 7919 + m_Frame * 104729;
                var angle = (i + (Hash(key) - 0.5f) * 0.9f) / m_LineCount * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                // Tip sits a bit outside the clear ellipse, at a random depth.
                var ellipse = 1f / Mathf.Sqrt(dir.x * dir.x / (clear.x * clear.x) + dir.y * dir.y / (clear.y * clear.y));
                var tip = center + dir * ellipse * (1f + Hash(key + 1) * 0.45f);

                // Base width: mostly hairlines, a few bold strokes.
                var bold = Hash(key + 2);
                var halfWidthDeg = bold > 0.85f ? 1.1f + bold * 0.6f : 0.25f + bold * 0.6f;
                var spread = halfWidthDeg * Mathf.Deg2Rad;
                var left = center + new Vector2(Mathf.Cos(angle - spread), Mathf.Sin(angle - spread)) * outer;
                var right = center + new Vector2(Mathf.Cos(angle + spread), Mathf.Sin(angle + spread)) * outer;

                painter.MoveTo(tip);
                painter.LineTo(left);
                painter.LineTo(right);
                painter.ClosePath();
            }
            painter.Fill();
        }
    }
}
