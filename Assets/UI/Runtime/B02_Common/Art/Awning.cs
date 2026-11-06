using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// A striped shop awning that stretches to the element width: a rod on top, a sloped canopy whose
    /// stripes converge slightly toward the wall, and a valance with a scalloped bottom edge (one scallop
    /// per stripe). Stripes alternate <c>--be-awning-a</c> / <c>--be-awning-b</c>.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class Awning : VisualElement
    {
        public static readonly string ussClassName = "be-awning";

        static readonly CustomStyleProperty<Color> s_AProperty = new("--be-awning-a");
        static readonly CustomStyleProperty<Color> s_BProperty = new("--be-awning-b");
        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");

        static readonly Color k_DefaultA = Rgb(222, 72, 44);
        static readonly Color k_DefaultB = Rgb(255, 248, 234);
        static readonly Color k_Rod = Rgb(120, 70, 40);

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;

        Color m_A = k_DefaultA;
        Color m_B = k_DefaultB;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        int m_StripeCount;

        /// <summary>Number of stripes; 0 picks a count from the element's proportions.</summary>
        [UxmlAttribute]
        public int stripeCount
        {
            get => m_StripeCount;
            set
            {
                m_StripeCount = Mathf.Clamp(value, 0, 64);
                MarkDirtyRepaint();
            }
        }

        public Awning()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
        }

        void OnStyleChanged()
        {
            var a = m_Style.Get(s_AProperty, k_DefaultA);
            var b = m_Style.Get(s_BProperty, k_DefaultB);
            var ink = m_Style.Get(s_InkProperty, Ink);
            var inkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            if (a == m_A && b == m_B && ink == m_Ink && inkWidth == m_InkWidth)
                return;
            m_A = a;
            m_B = b;
            m_Ink = ink;
            m_InkWidth = inkWidth;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 8f) || !(rect.height >= 8f))
                return;

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, Vector2.zero, 1f, m_Ink, 0f, 1f);
            var ink = Mathf.Clamp(rect.height * 0.045f, 1.5f, 6f) * m_InkWidth;
            pen.ink = ink;

            var pad = ink;
            var x0 = rect.x + pad;
            var x1 = rect.xMax - pad;
            var w = x1 - x0;
            var h = rect.height - pad * 2f;
            var top = rect.y + pad;

            var n = m_StripeCount > 0 ? m_StripeCount : Mathf.Max(3, Mathf.CeilToInt(w / (h * 0.36f)));
            var stripe = w / n;
            var r = stripe * 0.5f;
            var rodH = h * 0.14f;
            var canopyTop = top + rodH * 0.5f;
            var valanceTop = top + h * 0.56f;
            var valanceBottom = Mathf.Max(valanceTop + h * 0.08f, top + h - r);
            var inset = w * 0.035f;

            // Silhouette outline (canopy + valance + scallops).
            pen.Begin();
            OutlinePath(pen, x0, x1, inset, canopyTop, valanceTop, valanceBottom, n, stripe, r);
            pen.Outline();

            for (int i = 0; i < n; i++)
            {
                var color = i % 2 == 0 ? m_A : m_B;
                var sx0 = x0 + i * stripe;
                var sx1 = sx0 + stripe;
                // Canopy stripe converges toward the top.
                var tx0 = Mathf.Lerp(x0 + inset, x1 - inset, i / (float)n);
                var tx1 = Mathf.Lerp(x0 + inset, x1 - inset, (i + 1f) / n);
                var overlap = i < n - 1 ? 0.6f : 0f;

                pen.Begin();
                pen.MoveTo(tx0, canopyTop);
                pen.LineTo(tx1 + overlap, canopyTop);
                pen.LineTo(sx1 + overlap, valanceTop);
                pen.LineTo(sx1 + overlap, valanceBottom);
                pen.AddArc(sx0 + r, valanceBottom, r, r, 0f, 180f, true);
                pen.LineTo(sx0, valanceTop);
                pen.Close();
                pen.Fill(color);
            }

            // Shadow under the rod, shade on the valance and its scallops.
            pen.Begin();
            pen.MoveTo(x0 + inset, canopyTop);
            pen.LineTo(x1 - inset, canopyTop);
            pen.LineTo(x1 - inset * 0.8f, canopyTop + h * 0.1f);
            pen.LineTo(x0 + inset * 0.8f, canopyTop + h * 0.1f);
            pen.Close();
            pen.Fill(Alpha(m_Ink, 0.16f));

            pen.Begin();
            for (int i = 0; i < n; i++)
            {
                var sx0 = x0 + i * stripe;
                pen.MoveTo(sx0, valanceBottom - h * 0.02f);
                pen.LineTo(sx0 + stripe, valanceBottom - h * 0.02f);
                pen.AddArc(sx0 + r, valanceBottom, r, r, 0f, 180f, true);
                pen.Close();
            }
            pen.Fill(Alpha(m_Ink, 0.1f));

            // Hem between canopy and valance + a highlight on the canopy fold.
            pen.Begin();
            pen.MoveTo(x0, valanceTop);
            pen.LineTo(x1, valanceTop);
            pen.Ink(0.7f);
            pen.Begin();
            pen.MoveTo(x0 + ink, valanceTop + ink * 0.9f);
            pen.LineTo(x1 - ink, valanceTop + ink * 0.9f);
            pen.Stroke(Alpha(White, 0.45f), Mathf.Max(1f, ink * 0.5f));

            // Scallop joints.
            pen.Begin();
            for (int i = 1; i < n; i++)
            {
                var sx = x0 + i * stripe;
                pen.MoveTo(sx, valanceBottom - h * 0.03f);
                pen.LineTo(sx, valanceBottom + ink * 0.2f);
            }
            pen.Ink(0.55f);

            pen.Begin();
            OutlinePath(pen, x0, x1, inset, canopyTop, valanceTop, valanceBottom, n, stripe, r);
            pen.Ink(0.5f);

            // Rod with end knobs.
            pen.Begin();
            pen.AddRoundRect(x0 - pad * 0.5f, top, w + pad, rodH, rodH * 0.5f);
            pen.FillInk(k_Rod, 0.8f);
            pen.Begin();
            pen.MoveTo(x0 + rodH, top + rodH * 0.32f);
            pen.LineTo(x1 - rodH, top + rodH * 0.32f);
            pen.Stroke(Alpha(White, 0.4f), Mathf.Max(1f, rodH * 0.18f));
        }

        static void OutlinePath(ArtPen pen, float x0, float x1, float inset, float canopyTop, float valanceTop, float valanceBottom, int n, float stripe, float r)
        {
            pen.MoveTo(x0 + inset, canopyTop);
            pen.LineTo(x1 - inset, canopyTop);
            pen.LineTo(x1, valanceTop);
            pen.LineTo(x1, valanceBottom);
            for (int i = n - 1; i >= 0; i--)
            {
                var sx0 = x0 + i * stripe;
                pen.AddArc(sx0 + r, valanceBottom, r, r, 0f, 180f, true);
            }
            pen.LineTo(x0, valanceTop);
            pen.Close();
        }
    }
}
