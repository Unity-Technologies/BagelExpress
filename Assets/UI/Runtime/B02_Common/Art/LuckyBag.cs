using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// The fukubukuro (福袋) lucky bag: a plump sack with a gathered, ruffled top tied by a golden cord with
    /// ribbon tails, and a big round emblem. The emblem character is a child <see cref="Label"/>
    /// (<c>be-lucky-bag__emblem-text</c>, default 福) so it uses the panel's Japanese font; with an empty
    /// <see cref="emblemText"/> a lucky star is drawn instead.
    /// </summary>
    /// <remarks>
    /// Custom USS properties: <c>--be-bag-color</c> (default red), <c>--be-bag-accent</c> (cord/rim, default gold),
    /// <c>--be-ink</c>, and <c>--be-bag-glow</c>: a glow color (default none) drawn around the outline as wide translucent
    /// strokes, the look of the nav icons' <c>drop-shadow(0 0 4px …)</c> glow without a filter pass. The filter version
    /// cost about 17 ms a frame on a Galaxy S10 on the nav's 104-point bag (2026-10-17).
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class LuckyBag : VisualElement
    {
        public static readonly string ussClassName = "be-lucky-bag-art";
        public static readonly string emblemTextUssClassName = "be-lucky-bag__emblem-text";

        static readonly CustomStyleProperty<Color> s_BagColorProperty = new("--be-bag-color");
        static readonly CustomStyleProperty<Color> s_AccentProperty = new("--be-bag-accent");
        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");
        static readonly CustomStyleProperty<Color> s_GlowProperty = new("--be-bag-glow");

        // The glow: rings out to this many points past the outline, each adding this much opacity.
        const float k_GlowSpread = 11f;
        const int k_GlowRings = 6;
        const float k_GlowRingAlpha = 0.24f;

        static readonly Color k_DefaultBag = Rgb(222, 52, 44);
        static readonly Color k_DefaultAccent = Gold;
        static readonly Color k_Paper = Rgb(255, 248, 232);

        // Emblem placement in normalized space.
        const float k_EmblemY = 0.34f;
        const float k_EmblemR = 0.36f;

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        readonly Label m_EmblemLabel;

        Color m_Bag = k_DefaultBag;
        Color m_Accent = k_DefaultAccent;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        Color m_Glow = Color.clear;
        string m_EmblemText = "福";

        /// <summary>Text in the emblem (empty draws a lucky star).</summary>
        [UxmlAttribute]
        public string emblemText
        {
            get => m_EmblemText;
            set
            {
                m_EmblemText = value ?? string.Empty;
                m_EmblemLabel.text = m_EmblemText;
                m_EmblemLabel.style.display = string.IsNullOrEmpty(m_EmblemText) ? DisplayStyle.None : DisplayStyle.Flex;
                MarkDirtyRepaint();
            }
        }

        public Label emblemLabel => m_EmblemLabel;

        public LuckyBag()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);

            m_EmblemLabel = new Label(m_EmblemText) { pickingMode = PickingMode.Ignore };
            m_EmblemLabel.AddToClassList(emblemTextUssClassName);
            m_EmblemLabel.AddToClassList("be-display");
            var s = m_EmblemLabel.style;
            s.position = Position.Absolute;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.paddingLeft = s.paddingRight = s.paddingTop = s.paddingBottom = 0;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
            s.color = m_Bag;
            Add(m_EmblemLabel);

            RegisterCallback<GeometryChangedEvent>(_ => LayoutEmblem());
        }

        void OnStyleChanged()
        {
            var bag = m_Style.Get(s_BagColorProperty, k_DefaultBag);
            var accent = m_Style.Get(s_AccentProperty, k_DefaultAccent);
            var ink = m_Style.Get(s_InkProperty, Ink);
            var inkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            var glow = m_Style.Get(s_GlowProperty, Color.clear);
            if (bag == m_Bag && accent == m_Accent && ink == m_Ink && inkWidth == m_InkWidth && glow == m_Glow)
                return;
            m_Bag = bag;
            m_Accent = accent;
            m_Ink = ink;
            m_InkWidth = inkWidth;
            m_Glow = glow;
            m_EmblemLabel.style.color = m_Bag;
            MarkDirtyRepaint();
        }

        void LayoutEmblem()
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f))
                return;
            var unit = Mathf.Min(rect.width, rect.height) * 0.5f;
            var r = k_EmblemR * unit * 0.86f;
            var c = rect.center + new Vector2(0f, k_EmblemY * unit);
            var s = m_EmblemLabel.style;
            s.left = c.x - r;
            s.top = c.y - r;
            s.width = r * 2f;
            s.height = r * 2f;
            s.fontSize = r * 1.3f;
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f))
                return;

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, rect, m_Ink, 0.1f);
            pen.ink *= m_InkWidth;

            var shade = Darker(m_Bag, 0.25f);
            var light = Lighter(m_Bag, 0.28f);
            var accentShade = Darker(m_Accent, 0.22f);

            if (m_Glow.a > 0f)
                DrawGlow(pen);

            // Ruffled top above the tie, then the sack.
            pen.Begin();
            TopPath(pen);
            pen.Outline();
            pen.Fill(m_Bag);
            pen.Begin();
            SackPath(pen);
            pen.Outline();
            pen.Fill(m_Bag);

            // Ruffle shading and folds.
            pen.Begin();
            pen.MoveTo(0.06f, -0.42f);
            pen.CubicTo(0.2f, -0.52f, 0.32f, -0.64f, 0.4f, -0.74f);
            pen.LineTo(0.5f, -0.7f);
            pen.CubicTo(0.44f, -0.56f, 0.32f, -0.44f, 0.2f, -0.38f);
            pen.Close();
            pen.Fill(shade);
            pen.Begin();
            pen.MoveTo(-0.08f, -0.42f);
            pen.QuadTo(-0.16f, -0.6f, -0.2f, -0.76f);
            pen.MoveTo(0.04f, -0.44f);
            pen.QuadTo(0.06f, -0.62f, 0.1f, -0.8f);
            pen.Stroke(Alpha(pen.inkColor, 0.4f), 0.03f);

            // Sack volume: shaded right side, light left side, glossy streak.
            pen.Begin();
            SackPath(pen);
            pen.painter.PushClip();
            pen.Begin();
            pen.AddEllipse(0.62f, 0.4f, 0.42f, 0.7f, -10f);
            pen.Fill(shade);
            pen.Begin();
            pen.AddEllipse(-0.5f, 0.1f, 0.26f, 0.42f, 18f);
            pen.Fill(light);
            pen.Begin();
            pen.AddEllipse(0.02f, 0.92f, 0.8f, 0.14f);
            pen.Fill(Alpha(shade, 0.8f));

            // Gold plum-blossom dots.
            foreach (var (x, y, r) in s_Dots)
            {
                Blossom(pen, x, y, r, m_Accent);
            }

            // Neck folds.
            pen.Begin();
            pen.MoveTo(-0.16f, -0.32f);
            pen.QuadTo(-0.36f, -0.1f, -0.46f, 0.06f);
            pen.MoveTo(0.18f, -0.32f);
            pen.QuadTo(0.36f, -0.12f, 0.44f, 0.0f);
            pen.MoveTo(0f, -0.3f);
            pen.QuadTo(0.02f, -0.18f, 0f, -0.08f);
            pen.Stroke(Alpha(pen.inkColor, 0.35f), 0.03f);
            pen.painter.PopClip();

            pen.Begin();
            SackPath(pen);
            pen.Ink();

            pen.Begin();
            pen.MoveTo(-0.64f, -0.06f);
            pen.QuadTo(-0.72f, 0.12f, -0.68f, 0.34f);
            pen.Stroke(Alpha(White, 0.7f), 0.06f);

            // Golden cord around the neck with twist marks and a bow.
            pen.Begin();
            pen.AddRoundRect(-0.3f, -0.46f, 0.6f, 0.12f, 0.06f);
            pen.FillInk(m_Accent, 0.8f);
            pen.Begin();
            for (int i = 0; i < 6; i++)
            {
                var x = -0.24f + i * 0.096f;
                pen.MoveTo(x, -0.35f);
                pen.LineTo(x + 0.05f, -0.45f);
            }
            pen.Stroke(accentShade, 0.025f);

            // Ribbon tails hang over the front, then the bow loops and the knot.
            pen.Begin();
            RibbonTail(pen, -0.05f, -0.4f, -0.3f, 0.02f);
            RibbonTail(pen, 0.05f, -0.4f, 0.34f, 0.0f);
            pen.FillInk(m_Accent, 0.7f);

            pen.Begin();
            BowLoop(pen, -1f);
            BowLoop(pen, 1f);
            pen.Outline(0.7f);
            pen.Fill(m_Accent);
            pen.Begin();
            pen.MoveTo(-0.08f, -0.44f);
            pen.QuadTo(-0.18f, -0.48f, -0.26f, -0.52f);
            pen.MoveTo(0.08f, -0.44f);
            pen.QuadTo(0.18f, -0.48f, 0.26f, -0.52f);
            pen.Stroke(accentShade, 0.03f);
            pen.Begin();
            pen.AddRoundRect(-0.07f, -0.5f, 0.14f, 0.13f, 0.04f);
            pen.FillInk(m_Accent, 0.7f);
            pen.Begin();
            pen.AddCircle(-0.25f, -0.58f, 0.025f);
            pen.AddCircle(0.2f, -0.6f, 0.025f);
            pen.Fill(Alpha(White, 0.85f));

            // Emblem: gold rim, paper face, thin inner ring.
            pen.Begin();
            pen.AddCircle(0f, k_EmblemY, k_EmblemR);
            pen.Outline(0.7f);
            pen.Fill(m_Accent);
            pen.Begin();
            pen.AddCircle(0f, k_EmblemY, k_EmblemR - 0.075f);
            pen.FillInk(k_Paper, 0.35f);
            pen.Begin();
            pen.AddCircle(0f, k_EmblemY, k_EmblemR - 0.11f);
            pen.Stroke(Alpha(m_Bag, 0.85f), 0.018f);
            pen.Begin();
            pen.AddArc(0f, k_EmblemY, k_EmblemR - 0.03f, k_EmblemR - 0.03f, 200f, 250f);
            pen.Stroke(Alpha(White, 0.8f), 0.03f);

            if (string.IsNullOrEmpty(m_EmblemText))
            {
                pen.Push(0f, k_EmblemY + 0.01f, 0.24f);
                pen.Begin();
                pen.painter.BeginPath();
                StarPath(pen);
                pen.FillInk(m_Bag, 0.7f);
                pen.Pop();
            }

            Sparkle(pen, 0.7f, -0.62f, 0.13f, White, 0.45f);
            Sparkle(pen, -0.78f, -0.3f, 0.08f, GoldLight, 0.45f);
        }

        static readonly (float x, float y, float r)[] s_Dots =
        {
            (-0.6f, 0.46f, 0.07f), (0.62f, 0.1f, 0.06f), (-0.34f, -0.08f, 0.05f), (0.5f, 0.66f, 0.06f), (-0.2f, 0.78f, 0.05f), (0.36f, -0.1f, 0.045f)
        };

        static void Blossom(ArtPen pen, float x, float y, float r, Color color)
        {
            pen.Begin();
            for (int i = 0; i < 5; i++)
            {
                var a = (i * 72f - 90f) * Mathf.Deg2Rad;
                pen.AddCircle(x + Mathf.Cos(a) * r * 0.55f, y + Mathf.Sin(a) * r * 0.55f, r * 0.5f);
            }
            pen.Fill(Alpha(color, 0.85f));
            pen.Begin();
            pen.AddCircle(x, y, r * 0.28f);
            pen.Fill(Alpha(White, 0.85f));
        }

        static void StarPath(ArtPen pen)
        {
            for (int i = 0; i < 10; i++)
            {
                var r = (i % 2 == 0) ? 1f : 0.45f;
                var a = (-90f + i * 36f) * Mathf.Deg2Rad;
                var x = Mathf.Cos(a) * r;
                var y = Mathf.Sin(a) * r;
                if (i == 0) pen.MoveTo(x, y); else pen.LineTo(x, y);
            }
            pen.Close();
        }

        /// <summary>A flat ribbon loop of the bow, pointing left (-1) or right (+1).</summary>
        static void BowLoop(ArtPen pen, float side)
        {
            pen.MoveTo(0f, -0.44f);
            pen.CubicTo(side * 0.12f, -0.62f, side * 0.34f, -0.66f, side * 0.36f, -0.52f);
            pen.CubicTo(side * 0.38f, -0.4f, side * 0.18f, -0.36f, 0f, -0.44f);
            pen.Close();
        }

        /// <summary>
        /// The glow, under the bag: its outline stroked wide and translucent, widest first, so the opacity builds up
        /// toward the bag like a blur's falloff. The bag drawn over it hides the inner half of every stroke.
        /// </summary>
        void DrawGlow(ArtPen pen)
        {
            var ring = Alpha(m_Glow, m_Glow.a * k_GlowRingAlpha);
            for (int k = k_GlowRings; k >= 1; k--)
            {
                var width = 2f * (pen.ink + k_GlowSpread * k / k_GlowRings) / pen.unit;
                pen.Begin();
                TopPath(pen);
                SackPath(pen);
                pen.Stroke(ring, width);
            }
        }

        static void SackPath(ArtPen pen)
        {
            pen.MoveTo(-0.24f, -0.38f);
            pen.CubicTo(-0.56f, -0.26f, -0.9f, 0.02f, -0.88f, 0.4f);
            pen.CubicTo(-0.86f, 0.78f, -0.52f, 0.9f, 0f, 0.9f);
            pen.CubicTo(0.52f, 0.9f, 0.86f, 0.78f, 0.88f, 0.4f);
            pen.CubicTo(0.9f, 0.02f, 0.56f, -0.26f, 0.24f, -0.38f);
            pen.Close();
        }

        static void TopPath(ArtPen pen)
        {
            // Gathered cloth flaring above the cord with a ruffled edge.
            pen.MoveTo(-0.2f, -0.38f);
            pen.CubicTo(-0.3f, -0.5f, -0.44f, -0.6f, -0.52f, -0.7f);
            pen.QuadTo(-0.44f, -0.84f, -0.3f, -0.76f);
            pen.QuadTo(-0.22f, -0.92f, -0.08f, -0.8f);
            pen.QuadTo(0.02f, -0.94f, 0.14f, -0.8f);
            pen.QuadTo(0.28f, -0.92f, 0.36f, -0.76f);
            pen.QuadTo(0.5f, -0.82f, 0.54f, -0.68f);
            pen.CubicTo(0.46f, -0.58f, 0.32f, -0.48f, 0.2f, -0.38f);
            pen.Close();
        }

        static void RibbonTail(ArtPen pen, float x0, float y0, float x1, float y1)
        {
            var dir = new Vector2(x1 - x0, y1 - y0).normalized;
            var side = new Vector2(-dir.y, dir.x) * 0.055f;
            pen.MoveTo(x0 - side.x, y0 - side.y);
            pen.QuadTo((x0 + x1) * 0.5f + side.x * 2f, (y0 + y1) * 0.5f, x1 - side.x, y1 - side.y);
            // V-cut end.
            pen.LineTo(x1 - dir.x * 0.05f, y1 - dir.y * 0.05f);
            pen.LineTo(x1 + side.x, y1 + side.y);
            pen.QuadTo((x0 + x1) * 0.5f + side.x * 3.5f, (y0 + y1) * 0.5f + 0.02f, x0 + side.x, y0 + side.y);
            pen.Close();
        }
    }
}
