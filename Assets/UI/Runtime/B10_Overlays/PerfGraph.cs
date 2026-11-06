using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The performance overlay's graph: one bar per frame, newest on the right, as tall as the frame took (up to
    /// <see cref="maxMs"/>), green under 60 fps' budget, yellow under 30 fps', red above. Painter2D, one fill per color.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class PerfGraph : VisualElement
    {
        public const int capacity = 120;
        public const float maxMs = 50f;
        public const float budget60 = 1000f / 60f;
        public const float budget30 = 1000f / 30f;

        static readonly Color s_Good = new Color32(110, 230, 130, 230);
        static readonly Color s_Slow = new Color32(255, 210, 80, 230);
        static readonly Color s_Bad = new Color32(255, 95, 90, 235);
        static readonly Color s_Guide = new Color(1f, 1f, 1f, 0.4f);

        readonly float[] m_Samples = new float[capacity];
        int m_Count;
        int m_Head;

        /// <summary>How many frames the graph holds (up to <see cref="capacity"/>).</summary>
        public int count => m_Count;

        /// <summary>The most recent frame time, in milliseconds.</summary>
        public float latest => m_Count == 0 ? 0f : m_Samples[(m_Head + capacity - 1) % capacity];

        public PerfGraph()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Push(float milliseconds)
        {
            m_Samples[m_Head] = milliseconds;
            m_Head = (m_Head + 1) % capacity;
            m_Count = Mathf.Min(m_Count + 1, capacity);
            MarkDirtyRepaint();
        }

        public void Clear()
        {
            m_Count = 0;
            m_Head = 0;
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext context)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;
            var painter = context.painter2D;
            FillBars(painter, rect, s_Good, 0f, budget60 + 1f);
            FillBars(painter, rect, s_Slow, budget60 + 1f, budget30 + 1f);
            FillBars(painter, rect, s_Bad, budget30 + 1f, float.MaxValue);

            painter.lineWidth = 1f;
            painter.strokeColor = s_Guide;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, Y(rect, budget60)));
            painter.LineTo(new Vector2(rect.xMax, Y(rect, budget60)));
            painter.MoveTo(new Vector2(rect.xMin, Y(rect, budget30)));
            painter.LineTo(new Vector2(rect.xMax, Y(rect, budget30)));
            painter.Stroke();
        }

        static float Y(Rect rect, float ms) => rect.yMax - Mathf.Clamp(ms, 0f, maxMs) / maxMs * rect.height;

        // The frames that took [from, to) ms: one bar each, the newest at the right edge.
        void FillBars(Painter2D painter, Rect rect, Color color, float from, float to)
        {
            var bar = rect.width / capacity;
            var any = false;
            for (int i = 0; i < m_Count; i++)
            {
                var ms = m_Samples[(m_Head + capacity - m_Count + i) % capacity];
                if (ms < from || ms >= to)
                    continue;
                if (!any)
                {
                    painter.fillColor = color;
                    painter.BeginPath();
                    any = true;
                }
                var x = rect.xMax - (m_Count - i) * bar;
                var top = Y(rect, ms);
                painter.MoveTo(new Vector2(x, rect.yMax));
                painter.LineTo(new Vector2(x, top));
                painter.LineTo(new Vector2(x + bar * 0.8f, top));
                painter.LineTo(new Vector2(x + bar * 0.8f, rect.yMax));
                painter.ClosePath();
            }
            if (any)
                painter.Fill();
        }
    }
}
