using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// A red paper chōchin (提灯) lantern for the storefront: black top/bottom caps, curved rib lines,
    /// a warm inner light and an outer glow. The mark is a child <see cref="Label"/> (<c>be-lantern__mark</c>)
    /// when <see cref="mark"/> is set (e.g. 祭 or パン), otherwise a brush-black maple leaf is drawn.
    /// Drawn anchored to the top of the element and designed to swing with a USS rotate animation
    /// (its transform origin is set to the top center, where the hook is).
    /// </summary>
    /// <remarks>Custom USS properties: <c>--be-lantern-color</c> (paper), <c>--be-lantern-glow</c>, <c>--be-ink</c>, <c>--be-ink-width</c>.</remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class Lantern : VisualElement
    {
        public static readonly string ussClassName = "be-lantern";
        public static readonly string markUssClassName = ussClassName + "__mark";

        static readonly CustomStyleProperty<Color> s_ColorProperty = new("--be-lantern-color");
        static readonly CustomStyleProperty<Color> s_GlowProperty = new("--be-lantern-glow");
        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");

        static readonly Color k_DefaultColor = Rgb(226, 54, 40);
        static readonly Color k_DefaultGlow = Rgb(255, 140, 60, 0.8f);
        static readonly Color k_Cap = Rgb(44, 30, 30);

        const float k_BodyRx = 0.6f;
        const float k_BodyTop = -0.7f;
        const float k_BodyBottom = 0.7f;

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        readonly Label m_MarkLabel;

        Color m_Color = k_DefaultColor;
        Color m_Glow = k_DefaultGlow;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        string m_Mark = string.Empty;

        /// <summary>Text painted on the paper (one or two characters read best). Empty draws a maple leaf.</summary>
        [UxmlAttribute]
        public string mark
        {
            get => m_Mark;
            set
            {
                m_Mark = value ?? string.Empty;
                m_MarkLabel.text = m_Mark;
                m_MarkLabel.style.display = string.IsNullOrEmpty(m_Mark) ? DisplayStyle.None : DisplayStyle.Flex;
                LayoutMark();
                MarkDirtyRepaint();
            }
        }

        public Label markLabel => m_MarkLabel;

        public Lantern()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(0f));
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);

            m_MarkLabel = new Label { pickingMode = PickingMode.Ignore };
            m_MarkLabel.AddToClassList(markUssClassName);
            m_MarkLabel.AddToClassList("be-display");
            var s = m_MarkLabel.style;
            s.position = Position.Absolute;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.color = k_Cap;
            // Brush-black ink on paper: keep the display font, drop its outline and drop shadow.
            s.unityTextOutlineWidth = 0f;
            s.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 0f, color = Color.clear };
            s.paddingLeft = s.paddingRight = s.paddingTop = s.paddingBottom = 0;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
            s.display = DisplayStyle.None;
            Add(m_MarkLabel);

            RegisterCallback<GeometryChangedEvent>(_ => LayoutMark());
        }

        void OnStyleChanged()
        {
            var color = m_Style.Get(s_ColorProperty, k_DefaultColor);
            var glow = m_Style.Get(s_GlowProperty, k_DefaultGlow);
            var ink = m_Style.Get(s_InkProperty, Ink);
            var inkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            if (color == m_Color && glow == m_Glow && ink == m_Ink && inkWidth == m_InkWidth)
                return;
            m_Color = color;
            m_Glow = glow;
            m_Ink = ink;
            m_InkWidth = inkWidth;
            MarkDirtyRepaint();
        }

        void LayoutMark()
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f) || string.IsNullOrEmpty(m_Mark))
                return;
            var unit = Mathf.Min(rect.width, rect.height) * 0.5f;
            var w = k_BodyRx * unit * 1.3f;
            var h = (k_BodyBottom - k_BodyTop) * unit * 0.7f;
            var lines = m_Mark.Split('\n').Length;
            var chars = Mathf.Max(1, m_Mark.Replace("\n", string.Empty).Length / lines);
            var s = m_MarkLabel.style;
            s.left = rect.center.x - w * 0.5f;
            s.top = rect.y + unit - h * 0.5f;
            s.width = w;
            s.height = h;
            s.fontSize = Mathf.Min(w / chars, h / lines) * 0.82f;
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f))
                return;

            // Anchored to the top so the hook sits on the swing pivot (transform-origin: top).
            var pen = m_Pen;
            var unit = Mathf.Min(rect.width, rect.height) * 0.5f;
            pen.Reset(mgc.painter2D, new Vector2(rect.center.x, rect.y + unit), unit, m_Ink, 0.09f);
            pen.ink *= m_InkWidth;

            // Outer glow.
            if (m_Glow.a > 0f)
                Glow(pen, 0f, 0f, 1.05f, m_Glow, 6);

            // Hanging cord and hook ring.
            pen.Begin();
            pen.MoveTo(0f, -1f);
            pen.LineTo(0f, -0.82f);
            pen.Ink(0.6f);
            pen.Begin();
            pen.AddCircle(0f, -0.9f, 0.05f);
            pen.Ink(0.55f);

            // Paper body.
            pen.Begin();
            BodyPath(pen);
            pen.Outline();
            pen.Fill(m_Color);

            // Inner light, then the ribs that curve with the paper.
            pen.Begin();
            pen.AddEllipse(-0.04f, 0.02f, 0.44f, 0.56f);
            pen.Fill(Alpha(Lighter(m_Color, 0.3f), 0.85f));
            pen.Begin();
            pen.AddEllipse(-0.08f, -0.04f, 0.24f, 0.36f);
            pen.Fill(Alpha(Lighter(m_Color, 0.55f), 0.6f));

            pen.Begin();
            for (int i = -5; i <= 5; i++)
            {
                var y = i * 0.12f;
                var t = y / ((k_BodyBottom - k_BodyTop) * 0.5f);
                var half = k_BodyRx * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t * 0.92f)) - 0.02f;
                pen.MoveTo(-half, y);
                pen.QuadTo(0f, y + 0.05f, half, y);
            }
            pen.Stroke(Alpha(Darker(m_Color, 0.35f), 0.75f), 0.022f);

            // Side shading.
            pen.Begin();
            BodyPath(pen);
            pen.painter.PushClip();
            pen.Begin();
            pen.AddEllipse(0.56f, 0f, 0.22f, 0.8f);
            pen.Fill(Alpha(Darker(m_Color, 0.25f), 0.8f));
            pen.painter.PopClip();
            pen.Begin();
            BodyPath(pen);
            pen.Ink();

            pen.Begin();
            pen.AddArc(0f, 0f, k_BodyRx - 0.1f, 0.6f, 200f, 240f);
            pen.Stroke(Alpha(White, 0.7f), 0.05f);

            if (string.IsNullOrEmpty(m_Mark))
                MapleLeaf(pen, 0f, 0.02f, 0.3f, 0f, k_Cap, 0f, 1f, false);

            // Caps.
            pen.Begin();
            pen.AddRoundRect(-0.3f, -0.84f, 0.6f, 0.16f, 0.04f);
            pen.FillInk(k_Cap, 0.7f);
            pen.Begin();
            pen.AddRoundRect(-0.3f, 0.68f, 0.6f, 0.16f, 0.04f);
            pen.FillInk(k_Cap, 0.7f);
            pen.Begin();
            pen.MoveTo(-0.22f, -0.8f);
            pen.LineTo(0.1f, -0.8f);
            pen.MoveTo(-0.22f, 0.72f);
            pen.LineTo(0.1f, 0.72f);
            pen.Stroke(Alpha(White, 0.35f), 0.03f);

            // Tassel.
            pen.Begin();
            pen.MoveTo(-0.07f, 0.84f);
            pen.LineTo(0.07f, 0.84f);
            pen.LineTo(0.1f, 0.99f);
            pen.LineTo(-0.1f, 0.99f);
            pen.Close();
            pen.FillInk(Gold, 0.5f);
        }

        static void BodyPath(ArtPen pen)
        {
            pen.MoveTo(-0.26f, k_BodyTop);
            pen.CubicTo(-0.62f, -0.62f, -0.66f, -0.3f, -k_BodyRx - 0.04f, 0f);
            pen.CubicTo(-0.66f, 0.3f, -0.62f, 0.62f, -0.26f, k_BodyBottom);
            pen.LineTo(0.26f, k_BodyBottom);
            pen.CubicTo(0.62f, 0.62f, 0.66f, 0.3f, k_BodyRx + 0.04f, 0f);
            pen.CubicTo(0.66f, -0.3f, 0.62f, -0.62f, 0.26f, k_BodyTop);
            pen.Close();
        }
    }
}
