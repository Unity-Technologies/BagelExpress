using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Currency icons drawn with Painter2D: the "bagel coin" (a coin with a hole, like
    /// a ¥5 coin and a bagel), the maple-leaf Maple Gem, and the Lucky Ticket.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class CurrencyIcon : VisualElement
    {
        public static readonly string ussClassName = "be-currency-icon";

        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");

        static readonly Color k_DefaultInk = new(0.23f, 0.12f, 0.08f);

        readonly ArtStyleResolver m_Style;
        CurrencyKind m_Kind;
        Color m_Ink = k_DefaultInk;
        float m_InkWidth = 1f;

        [UxmlAttribute]
        public CurrencyKind kind
        {
            get => m_Kind;
            set { m_Kind = value; MarkDirtyRepaint(); }
        }

        public CurrencyIcon()
        {
            AddToClassList(ussClassName);
            generateVisualContent += OnGenerateVisualContent;
            // Custom properties aren't inherited: the resolver also looks at ancestors (theme :root).
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
        }

        void OnStyleChanged()
        {
            m_Ink = m_Style.Get(s_InkProperty, k_DefaultInk);
            m_InkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width < 2f || rect.height < 2f)
                return;

            var painter = mgc.painter2D;
            var unit = PainterShapes.UnitSize(rect);
            var ink = Mathf.Max(1.5f, unit * 0.14f) * m_InkWidth;

            switch (m_Kind)
            {
                case CurrencyKind.Coins: DrawCoin(painter, rect, unit, ink); break;
                case CurrencyKind.Gems: DrawGem(painter, rect, unit, ink); break;
                case CurrencyKind.Tickets: DrawTicket(painter, rect, unit, ink); break;
            }
        }

        void DrawCoin(Painter2D painter, Rect rect, float unit, float ink)
        {
            var c = rect.center;
            var r = unit * 0.88f;

            // Rim, face, then the bagel hole (odd-even fill punches it out).
            PainterShapes.Circle(painter, c, r);
            PainterShapes.FillAndStroke(painter, new Color(0.96f, 0.63f, 0.09f), m_Ink, ink);

            painter.BeginPath();
            painter.Arc(c, r * 0.74f, 0f, 360f);
            painter.ClosePath();
            painter.Arc(c, r * 0.3f, 0f, 360f);
            painter.ClosePath();
            painter.fillColor = new Color(1f, 0.82f, 0.25f);
            painter.Fill(FillRule.OddEven);

            PainterShapes.Circle(painter, c, r * 0.3f);
            painter.strokeColor = m_Ink;
            painter.lineWidth = ink * 0.8f;
            painter.Stroke();

            // Sesame-seed shine.
            painter.BeginPath();
            painter.Arc(c, r * 0.56f, 200f, 250f);
            painter.strokeColor = new Color(1f, 1f, 1f, 0.85f);
            painter.lineWidth = ink * 0.9f;
            painter.lineCap = LineCap.Round;
            painter.Stroke();
        }

        void DrawGem(Painter2D painter, Rect rect, float unit, float ink)
        {
            PainterShapes.Polygon(painter, rect, PainterShapes.MapleLeaf);
            PainterShapes.FillAndStroke(painter, new Color(0.91f, 0.22f, 0.2f), m_Ink, ink);

            // Facets: lighter veins from the heart of the leaf to its tips.
            var heart = PainterShapes.Map(rect, 0f, 0.15f);
            painter.strokeColor = new Color(1f, 0.62f, 0.55f, 0.95f);
            painter.lineWidth = ink * 0.6f;
            painter.lineCap = LineCap.Round;
            foreach (var (x, y) in new[] { (0f, -0.82f), (0.78f, -0.3f), (-0.78f, -0.3f), (0.42f, 0.36f), (-0.42f, 0.36f) })
            {
                painter.BeginPath();
                painter.MoveTo(heart);
                painter.LineTo(PainterShapes.Map(rect, x * 0.8f, y * 0.8f + 0.03f));
                painter.Stroke();
            }

            // Sparkle.
            PainterShapes.Star(painter, PainterShapes.Map(rect, -0.38f, -0.42f), unit * 0.22f, unit * 0.06f, 4, 0f);
            painter.fillColor = Color.white;
            painter.Fill();
        }

        void DrawTicket(Painter2D painter, Rect rect, float unit, float ink)
        {
            var w = unit * 1.9f;
            var h = unit * 1.2f;
            var r = new Rect(rect.center.x - w * 0.5f, rect.center.y - h * 0.5f, w, h);

            // Ticket body with punched side notches.
            PainterShapes.RoundedRect(painter, r, unit * 0.18f);
            painter.Arc(new Vector2(r.xMin, r.center.y), h * 0.2f, 0f, 360f);
            painter.ClosePath();
            painter.Arc(new Vector2(r.xMax, r.center.y), h * 0.2f, 0f, 360f);
            painter.ClosePath();
            painter.fillColor = new Color(1f, 0.42f, 0.55f);
            painter.Fill(FillRule.OddEven);

            PainterShapes.RoundedRect(painter, r, unit * 0.18f);
            painter.strokeColor = m_Ink;
            painter.lineWidth = ink;
            painter.Stroke();

            // Perforation.
            painter.strokeColor = new Color(1f, 0.85f, 0.88f);
            painter.lineWidth = ink * 0.5f;
            var x = r.xMin + w * 0.3f;
            for (var y = r.yMin + h * 0.14f; y < r.yMax - h * 0.14f; y += h * 0.16f)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(x, y + h * 0.08f));
                painter.Stroke();
            }

            // Lucky star.
            PainterShapes.Star(painter, new Vector2(r.xMin + w * 0.64f, r.center.y), h * 0.3f, h * 0.13f);
            PainterShapes.FillAndStroke(painter, new Color(1f, 0.86f, 0.3f), m_Ink, ink * 0.6f);
        }
    }
}
