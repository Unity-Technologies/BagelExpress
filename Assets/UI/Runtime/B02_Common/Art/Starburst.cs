using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// A starburst behind LEVEL UP, the Lucky Bag and other prize moments, spun by a USS animation: long, slim diamonds
    /// that grow out of a soft glowing center (<c>--be-burst-center</c>), long ones in <c>--be-burst-color-a</c> and
    /// short ones in <c>--be-burst-color-b</c> between them, with the background showing in between. Each is one
    /// four-point shape, its tip faded out by stacking shorter copies, so the whole burst is a few hundred vertices.
    /// <c>--be-burst-edge</c> (none by default) draws a thin rim along the long ones' sides, fading toward their tips,
    /// where the spikes need to stand out from a pale background (the title's sky).
    /// </summary>
    /// <remarks>
    /// Rays around a center can read as the Rising Sun Flag: wedges that widen to the edges and fill the whole field
    /// in two alternating colors. These are pointed at both ends, widest a quarter of the way out, in two lengths and
    /// with gaps between them, so they read as a sparkle. (0.8 had curved arms instead, a swirl; each arm was a tessellated spiral,
    /// about 34 000 vertices for the title alone.)
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class Starburst : VisualElement
    {
        public static readonly string ussClassName = "be-starburst";

        static readonly CustomStyleProperty<Color> s_ColorAProperty = new("--be-burst-color-a");
        static readonly CustomStyleProperty<Color> s_ColorBProperty = new("--be-burst-color-b");
        static readonly CustomStyleProperty<Color> s_CenterProperty = new("--be-burst-center");
        static readonly CustomStyleProperty<Color> s_EdgeProperty = new("--be-burst-edge");

        static readonly Color k_DefaultA = Rgb(255, 220, 120, 0.9f);
        static readonly Color k_DefaultB = Rgb(255, 160, 80, 0.5f);
        static readonly Color k_DefaultCenter = Rgb(255, 250, 220, 0.95f);

        // The fade's layers: each a copy of the spikes this far out (a share of their length), stacked.
        static readonly float[] s_FadeLayers = { 1f, 0.72f, 0.46f };

        // A spike is widest this far along its length, where it fills this share of its slot.
        const float k_WidestAt = 0.25f;
        const float k_Width = 0.8f;

        // The shortest a spike gets, as a share of the longest: the long ones, then the short ones.
        const float k_LongShortest = 0.72f;
        const float k_ShortShortest = 0.55f;

        // The rim: its full color out to this share of a spike's length, then fading to nothing at the tip.
        const float k_EdgeHoldsTo = 0.3f;

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;

        int m_RayCount = 10;
        float m_InnerRadius = 0.18f;
        float m_ShortLength = 0.55f;
        float m_CenterSize = 0.22f;
        float m_EdgeWidth = 2f;
        bool m_FadeOut = true;
        Color m_ColorA = k_DefaultA;
        Color m_ColorB = k_DefaultB;
        Color m_Center = k_DefaultCenter;
        Color m_Edge = Color.clear;
        Gradient m_EdgeGradient;

        /// <summary>Number of long spikes (the same number of short ones sits in between).</summary>
        [UxmlAttribute]
        public int rayCount
        {
            get => m_RayCount;
            set
            {
                value = Mathf.Clamp(value, 3, 48);
                if (m_RayCount == value)
                    return;
                m_RayCount = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Where the spikes start: this far from the center, as a fraction of the half-size of the element.</summary>
        [UxmlAttribute]
        public float innerRadius
        {
            get => m_InnerRadius;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_InnerRadius, value))
                    return;
                m_InnerRadius = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>How long the short spikes are, as a fraction of the long ones (which reach the element's corners).</summary>
        [UxmlAttribute]
        public float shortLength
        {
            get => m_ShortLength;
            set
            {
                value = Mathf.Clamp(value, 0.1f, 1f);
                if (Mathf.Approximately(m_ShortLength, value))
                    return;
                m_ShortLength = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Radius of the soft center glow, as a fraction of the half-size of the element (0 hides it).</summary>
        [UxmlAttribute]
        public float centerSize
        {
            get => m_CenterSize;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_CenterSize, value))
                    return;
                m_CenterSize = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Width of the rim along the long spikes' sides, in points (its color is <c>--be-burst-edge</c>).</summary>
        [UxmlAttribute]
        public float edgeWidth
        {
            get => m_EdgeWidth;
            set
            {
                value = Mathf.Clamp(value, 0f, 12f);
                if (Mathf.Approximately(m_EdgeWidth, value))
                    return;
                m_EdgeWidth = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Fade the spikes out toward their tips (stacked translucent layers).</summary>
        [UxmlAttribute]
        public bool fadeOut
        {
            get => m_FadeOut;
            set
            {
                if (m_FadeOut == value)
                    return;
                m_FadeOut = value;
                MarkDirtyRepaint();
            }
        }

        public Starburst()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
        }

        void OnStyleChanged()
        {
            var a = m_Style.Get(s_ColorAProperty, k_DefaultA);
            var b = m_Style.Get(s_ColorBProperty, k_DefaultB);
            var c = m_Style.Get(s_CenterProperty, k_DefaultCenter);
            var edge = m_Style.Get(s_EdgeProperty, Color.clear);
            if (a == m_ColorA && b == m_ColorB && c == m_Center && edge == m_Edge)
                return;
            m_ColorA = a;
            m_ColorB = b;
            m_Center = c;
            m_Edge = edge;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f))
                return;

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, rect.center, 1f, Ink, 0f, 1f);
            var halfSize = Mathf.Min(rect.width, rect.height) * 0.5f;
            var inner = halfSize * m_InnerRadius;
            // The longest spikes reach the corners, so a rotate animation never shows where they'd end.
            var length = rect.size.magnitude * 0.5f + 1f - inner;
            var layers = m_FadeOut ? s_FadeLayers.Length : 1;
            for (int layer = 0; layer < layers; layer++)
            {
                var share = m_FadeOut ? s_FadeLayers[layer] : 1f;
                Spikes(pen, 0, m_ColorA, inner, length * share, k_LongShortest, layers);
                Spikes(pen, 1, m_ColorB, inner, length * m_ShortLength * share, k_ShortShortest, layers);
            }
            Edges(pen, inner, length);

            if (m_CenterSize > 0f)
            {
                var r = halfSize * m_CenterSize;
                Glow(pen, 0f, 0f, r * 2.2f, m_Center, 6);
                pen.Begin();
                pen.AddCircle(0f, 0f, r);
                pen.Fill(m_Center);
            }
        }

        /// <summary>
        /// Every other spike (the long ones from 0, the short ones from 1), one diamond each: from <paramref name="inner"/>
        /// out to its tip, widest a quarter of the way. Each spike has its own length, between <paramref name="shortest"/>
        /// and all of <paramref name="length"/>, the same every frame: irregular, so the burst reads as a sparkle, not a
        /// regular ray pattern. <paramref name="layers"/> stacked copies composite to the full color alpha.
        /// </summary>
        void Spikes(ArtPen pen, int first, Color color, float inner, float length, float shortest, int layers)
        {
            if (color.a <= 0f || length <= 0f)
                return;
            var step = 360f / (m_RayCount * 2);
            var half = step * 0.5f * k_Width;
            pen.Begin();
            for (int i = first; i < m_RayCount * 2; i += 2)
            {
                var angle = i * step - 90f;
                var tip = inner + length * Mathf.Lerp(shortest, 1f, Jitter(i));
                var widest = inner + (tip - inner) * k_WidestAt;
                Polar(pen, angle, inner, move: true);
                Polar(pen, angle - half, widest, move: false);
                Polar(pen, angle, tip, move: false);
                Polar(pen, angle + half, widest, move: false);
                pen.Close();
            }
            pen.Fill(Alpha(color, 1f - Mathf.Pow(1f - Mathf.Min(color.a, 0.999f), 1f / layers)));
        }

        /// <summary>
        /// The rim: both sides of every long spike, each its own stroke so it can fade from <c>--be-burst-edge</c> near
        /// the center to nothing at the tip, like the spike under it. Two short lines per side: about 20 vertices each.
        /// </summary>
        void Edges(ArtPen pen, float inner, float length)
        {
            if (m_Edge.a <= 0f || m_EdgeWidth <= 0f || length <= 0f)
                return;
            var painter = pen.painter;
            m_EdgeGradient ??= new Gradient();
            m_EdgeGradient.SetKeys(
                new[] { new GradientColorKey(m_Edge, 0f), new GradientColorKey(m_Edge, 1f) },
                new[] { new GradientAlphaKey(m_Edge.a, 0f), new GradientAlphaKey(m_Edge.a, k_EdgeHoldsTo), new GradientAlphaKey(0f, 1f) });
            painter.strokeGradient = m_EdgeGradient;
            painter.lineWidth = m_EdgeWidth;
            painter.lineCap = LineCap.Butt;
            painter.lineJoin = LineJoin.Round;
            var step = 360f / (m_RayCount * 2);
            var half = step * 0.5f * k_Width;
            for (int i = 0; i < m_RayCount * 2; i += 2)
            {
                var angle = i * step - 90f;
                var tip = inner + length * Mathf.Lerp(k_LongShortest, 1f, Jitter(i));
                var widest = inner + (tip - inner) * k_WidestAt;
                for (int side = -1; side <= 1; side += 2)
                {
                    pen.Begin();
                    Polar(pen, angle, inner, move: true);
                    Polar(pen, angle + half * side, widest, move: false);
                    Polar(pen, angle, tip, move: false);
                    painter.Stroke();
                }
            }
            painter.strokeGradient = null;
        }

        /// <summary>A fixed pseudo-random number in [0, 1) for spike <paramref name="i"/>.</summary>
        static float Jitter(int i)
        {
            var x = Mathf.Sin(i * 12.9898f + 4.1414f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        static void Polar(ArtPen pen, float degrees, float radius, bool move)
        {
            var a = degrees * Mathf.Deg2Rad;
            if (move)
                pen.MoveTo(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
            else
                pen.LineTo(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
    }
}
