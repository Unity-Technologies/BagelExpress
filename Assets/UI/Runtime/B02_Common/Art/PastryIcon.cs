using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// The twelve Bagel Express recipes drawn with Painter2D in a chunky, cute yuru-chara style:
    /// ink outlines, flat fills with layered highlights/shades, and little faces.
    /// <c>silhouette</c> flattens the pastry into one color for locked recipes.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class PastryIcon : VisualElement
    {
        public static readonly string ussClassName = "be-pastry-icon";
        public static readonly UniqueStyleString silhouetteUssClassName = new(ussClassName + "--silhouette");

        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");
        static readonly CustomStyleProperty<Color> s_SilhouetteColorProperty = new("--be-silhouette-color");

        public static readonly Color defaultSilhouetteColor = Rgb(74, 52, 62);

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        PastryKind m_Kind;
        bool m_Silhouette;
        bool m_ShowFace = true;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        Color m_SilhouetteColor = defaultSilhouetteColor;

        [UxmlAttribute, CreateProperty]
        public PastryKind kind
        {
            get => m_Kind;
            set
            {
                if (m_Kind == value)
                    return;
                m_Kind = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Draw the whole pastry in one flat dark color (locked recipe).</summary>
        [UxmlAttribute, CreateProperty]
        public bool silhouette
        {
            get => m_Silhouette;
            set
            {
                if (m_Silhouette == value)
                    return;
                m_Silhouette = value;
                EnableInClassList(silhouetteUssClassName, value);
                MarkDirtyRepaint();
            }
        }

        [UxmlAttribute, CreateProperty]
        public bool showFace
        {
            get => m_ShowFace;
            set
            {
                if (m_ShowFace == value)
                    return;
                m_ShowFace = value;
                MarkDirtyRepaint();
            }
        }

        public PastryIcon()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
        }

        public PastryIcon(PastryKind kind) : this()
        {
            m_Kind = kind;
        }

        void OnStyleChanged()
        {
            var ink = m_Style.Get(s_InkProperty, Ink);
            var inkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            var sil = m_Style.Get(s_SilhouetteColorProperty, defaultSilhouetteColor);
            if (ink == m_Ink && inkWidth == m_InkWidth && sil == m_SilhouetteColor)
                return;
            m_Ink = ink;
            m_InkWidth = inkWidth;
            m_SilhouetteColor = sil;
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f))
                return;

            m_Pen.Reset(mgc.painter2D, rect, m_Ink);
            m_Pen.ink *= m_InkWidth;
            m_Pen.silhouette = m_Silhouette;
            m_Pen.silhouetteColor = m_SilhouetteColor;
            Draw(m_Pen, m_Kind, m_ShowFace);
        }

        /// <summary>Draw a pastry into the pen's current frame (normalized [-1, 1] square).</summary>
        public static void Draw(ArtPen pen, PastryKind kind, bool face = true)
        {
            switch (kind)
            {
                case PastryKind.SesameBagel: DrawBagel(pen, s_Sesame, face, Topping.Sesame); break;
                case PastryKind.PoppySeedBagel: DrawBagel(pen, s_Poppy, face, Topping.Poppy); break;
                case PastryKind.EverythingBagel: DrawBagel(pen, s_Everything, face, Topping.Everything); break;
                case PastryKind.MatchaBagel: DrawBagel(pen, s_Matcha, face, Topping.Matcha); break;
                case PastryKind.SakuraMochiBagel: DrawBagel(pen, s_SakuraMochi, face, Topping.Sakura); break;
                case PastryKind.Anpan: DrawAnpan(pen, face); break;
                case PastryKind.MelonPan: DrawMelonPan(pen, face, false); break;
                case PastryKind.MapleMelonPan: DrawMelonPan(pen, face, true); break;
                case PastryKind.CurryPan: DrawCurryPan(pen, face); break;
                case PastryKind.Taiyaki: DrawTaiyaki(pen, face, false); break;
                case PastryKind.GoldenMapleTaiyaki: DrawTaiyaki(pen, face, true); break;
                case PastryKind.Dorayaki: DrawDorayaki(pen, face); break;
            }
        }

        // ================================================================== bagels

        struct BagelColors
        {
            public Color top;
            public Color side;
            public Color light;
            public Color wall;

            public BagelColors(Color top, Color side, Color light, Color wall)
            {
                this.top = top;
                this.side = side;
                this.light = light;
                this.wall = wall;
            }
        }

        enum Topping
        {
            Sesame,
            Poppy,
            Everything,
            Matcha,
            Sakura
        }

        static readonly BagelColors s_Sesame = new(CrustTop, CrustSide, CrustLight, Rgb(156, 82, 32));
        static readonly BagelColors s_Poppy = new(Rgb(204, 126, 58), Rgb(166, 88, 36), Rgb(232, 168, 98), Rgb(146, 74, 30));
        static readonly BagelColors s_Everything = new(Rgb(196, 118, 52), Rgb(158, 84, 34), Rgb(228, 160, 92), Rgb(138, 70, 28));
        static readonly BagelColors s_Matcha = new(Matcha, Rgb(86, 130, 50), Rgb(170, 208, 116), Rgb(74, 114, 42));
        static readonly BagelColors s_SakuraMochi = new(SakuraPink, Rgb(232, 126, 164), Rgb(255, 214, 228), Rgb(210, 104, 144));

        // Bagel geometry: a puck (torus seen from the front-top) with the hole high up so the face fits below.
        const float k_BagelTopY = -0.08f;
        const float k_BagelRx = 0.9f;
        const float k_BagelRy = 0.64f;
        const float k_BagelHeight = 0.16f;
        const float k_HoleY = -0.27f;
        const float k_HoleRx = 0.3f;
        const float k_HoleRy = 0.15f;

        const float k_FaceY = 0.24f;

        static void DrawBagel(ArtPen pen, BagelColors colors, bool face, Topping topping)
        {
            // Far inner wall, seen through the hole (behind everything).
            if (pen.details)
            {
                pen.Begin();
                pen.AddArc(0f, k_HoleY, k_HoleRx, k_HoleRy, 180f, 360f);
                pen.QuadTo(0f, k_HoleY + 0.07f, -k_HoleRx, k_HoleY);
                pen.Close();
                pen.Fill(colors.wall);
            }

            // Silhouette: puck with a see-through hole.
            pen.Begin();
            pen.AddPuck(0f, k_BagelTopY, k_BagelRx, k_BagelRy, k_BagelHeight);
            pen.Outline();
            pen.Begin();
            pen.AddPuck(0f, k_BagelTopY, k_BagelRx, k_BagelRy, k_BagelHeight);
            pen.AddEllipse(0f, k_HoleY, k_HoleRx, k_HoleRy);
            pen.Fill(colors.side, FillRule.OddEven);

            if (!pen.details)
                return;

            // Top surface.
            pen.Begin();
            pen.AddEllipse(0f, k_BagelTopY, k_BagelRx, k_BagelRy);
            pen.AddEllipse(0f, k_HoleY, k_HoleRx, k_HoleRy);
            pen.Fill(colors.top, FillRule.OddEven);

            // Rounded rim: a darker band hugging the outer edge, thickest at the front.
            pen.Begin();
            pen.AddEllipse(0f, k_BagelTopY, k_BagelRx, k_BagelRy);
            pen.AddEllipse(0f, k_BagelTopY - 0.07f, k_BagelRx - 0.08f, k_BagelRy - 0.1f);
            pen.Fill(Alpha(Darker(colors.top, 0.18f), 0.55f), FillRule.OddEven);

            // Puffy highlight on the upper-left of the ring + a glossy glint.
            const float midY = (k_HoleY + k_BagelTopY) * 0.5f - 0.02f;
            const float midRx = (k_HoleRx + k_BagelRx) * 0.5f;
            const float midRy = (k_HoleRy + k_BagelRy) * 0.5f;
            pen.Begin();
            pen.AddArc(0f, midY, midRx, midRy, 196f, 262f);
            pen.Stroke(colors.light, 0.15f);
            pen.Begin();
            pen.AddArc(0f, midY, midRx, midRy, 300f, 318f);
            pen.Stroke(colors.light, 0.1f);
            pen.Begin();
            pen.AddArc(0f, midY, midRx, midRy, 206f, 226f);
            pen.Stroke(Alpha(White, 0.75f), 0.06f);

            // Edge between top surface and side.
            pen.Begin();
            pen.AddArc(0f, k_BagelTopY, k_BagelRx, k_BagelRy, 12f, 168f);
            pen.Stroke(Alpha(pen.inkColor, 0.45f), 0.035f);

            // Hole rim on top of the fills.
            pen.Begin();
            pen.AddEllipse(0f, k_HoleY, k_HoleRx, k_HoleRy);
            pen.Ink(0.75f);

            switch (topping)
            {
                case Topping.Sesame: SesameSeeds(pen, face, 15, 11); break;
                case Topping.Poppy: PoppySeeds(pen, face, 46, 23); break;
                case Topping.Everything: EverythingToppings(pen, face); break;
                case Topping.Matcha: MatchaPowder(pen, face); break;
                case Topping.Sakura: SakuraToppings(pen, face); break;
            }

            if (face)
                Face(pen, 0f, k_FaceY, 1f);
        }

        /// <summary>A point on the bagel's top surface: angle around the ring, t from the hole edge (0) to the outer edge (1).</summary>
        static Vector2 RingPoint(float angleDeg, float t)
        {
            var a = angleDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(a);
            var sin = Mathf.Sin(a);
            var inner = new Vector2(k_HoleRx * cos, k_HoleY + k_HoleRy * sin);
            var outer = new Vector2(k_BagelRx * cos, k_BagelTopY + k_BagelRy * sin);
            return Vector2.Lerp(inner, outer, t);
        }

        static bool InFaceZone(Vector2 p) => Mathf.Abs(p.x) < 0.66f && p.y > k_FaceY - 0.2f && p.y < k_FaceY + 0.32f;

        /// <summary>Deterministic topping spot #i (scattered evenly around the ring with jitter).</summary>
        static Vector2 ToppingSpot(int i, int count, int salt, float tMin = 0.18f, float tMax = 0.86f)
        {
            var angle = i * 360f / count + Range(i, salt, -12f, 12f) + salt * 7f;
            var t = Range(i, salt + 101, tMin, tMax);
            return RingPoint(angle, t);
        }

        static void SesameSeeds(ArtPen pen, bool face, int count, int salt)
        {
            for (int i = 0; i < count; i++)
            {
                var p = ToppingSpot(i, count, salt);
                if (face && InFaceZone(p))
                    continue;
                var rot = Range(i, salt + 7, 0f, 180f);
                Seed(pen, p, rot, Sesame, 0.075f, 0.04f);
            }
        }

        static void Seed(ArtPen pen, Vector2 p, float rot, Color color, float rx, float ry, float inkScale = 0.35f)
        {
            pen.Begin();
            pen.AddEllipse(p.x, p.y, rx, ry, rot);
            pen.Fill(color);
            if (inkScale > 0f)
                pen.Stroke(Alpha(pen.inkColor, 0.8f), Mathf.Max(0.02f, inkScale * 0.06f));
        }

        static void PoppySeeds(ArtPen pen, bool face, int count, int salt)
        {
            pen.Begin();
            for (int i = 0; i < count; i++)
            {
                var p = ToppingSpot(i, count, salt, 0.12f, 0.92f);
                if (face && InFaceZone(p))
                    continue;
                pen.AddCircle(p.x, p.y, Range(i, salt + 3, 0.022f, 0.032f));
            }
            pen.Fill(Poppy);
        }

        static void EverythingToppings(ArtPen pen, bool face)
        {
            PoppySeeds(pen, face, 26, 41);

            // Toasted onion flakes.
            for (int i = 0; i < 7; i++)
            {
                var p = ToppingSpot(i, 7, 53, 0.25f, 0.8f);
                if (face && InFaceZone(p))
                    continue;
                var r = Range(i, 59, 0.05f, 0.075f);
                var rot = Range(i, 61, 0f, 360f);
                pen.Push(p.x, p.y, r, rot);
                pen.Begin();
                pen.MoveTo(-1f, -0.3f);
                pen.LineTo(-0.2f, -0.9f);
                pen.LineTo(0.9f, -0.5f);
                pen.LineTo(0.7f, 0.6f);
                pen.LineTo(-0.5f, 0.8f);
                pen.Close();
                pen.Fill(Rgb(250, 214, 140));
                pen.Stroke(Rgb(170, 96, 36), 0.22f);
                pen.Pop();
            }

            SesameSeeds(pen, face, 9, 67);

            // Salt crystals.
            for (int i = 0; i < 6; i++)
            {
                var p = ToppingSpot(i, 6, 79, 0.2f, 0.85f);
                if (face && InFaceZone(p))
                    continue;
                pen.Push(p.x, p.y, 0.032f, Range(i, 83, 0f, 90f));
                pen.Begin();
                pen.AddRoundRect(-1f, -1f, 2f, 2f, 0.3f);
                pen.Fill(White);
                pen.Stroke(Rgb(170, 150, 150), 0.4f);
                pen.Pop();
            }
        }

        static void MatchaPowder(ArtPen pen, bool face)
        {
            // Powdered sugar dusting: dense on the far half of the ring, sparse in front.
            pen.Begin();
            for (int i = 0; i < 48; i++)
            {
                var far = i < 34;
                var angle = far ? Range(i, 91, 170f, 370f) : Range(i, 91, 0f, 180f);
                var p = RingPoint(angle, Range(i, 93, 0.1f, 0.9f));
                if (face && InFaceZone(p))
                    continue;
                pen.AddCircle(p.x, p.y, Range(i, 97, 0.018f, far ? 0.042f : 0.03f));
            }
            pen.Fill(Alpha(White, 0.92f));

            // A few soft sugar drifts.
            pen.Begin();
            pen.AddEllipse(-0.5f, -0.42f, 0.1f, 0.05f, -30f);
            pen.AddEllipse(0.46f, -0.46f, 0.09f, 0.045f, 28f);
            pen.AddEllipse(0.02f, -0.6f, 0.08f, 0.035f);
            pen.Fill(Alpha(White, 0.75f));
        }

        static void SakuraToppings(ArtPen pen, bool face)
        {
            // Domyoji mochi grains.
            pen.Begin();
            for (int i = 0; i < 20; i++)
            {
                var p = ToppingSpot(i, 20, 113, 0.15f, 0.88f);
                if ((face && InFaceZone(p)) || (p.x > 0.05f && p.y < -0.05f))
                    continue;
                pen.AddEllipse(p.x, p.y, 0.04f, 0.026f, Range(i, 117, 0f, 180f));
            }
            pen.Fill(Rgb(255, 222, 234));

            // Salted sakura leaf + blossom on the upper right.
            pen.Push(0.42f, -0.5f, 0.42f, -28f);
            pen.Begin();
            pen.MoveTo(-0.9f, 0.1f);
            pen.CubicTo(-0.5f, -0.6f, 0.5f, -0.6f, 1f, -0.05f);
            pen.CubicTo(0.5f, 0.5f, -0.4f, 0.5f, -0.9f, 0.1f);
            pen.Close();
            pen.Outline(0.7f);
            pen.Fill(Rgb(126, 160, 62));
            pen.Begin();
            pen.MoveTo(-0.8f, 0.08f);
            pen.QuadTo(0.1f, -0.12f, 0.9f, -0.04f);
            for (int i = 0; i < 3; i++)
            {
                var x = -0.4f + i * 0.4f;
                pen.MoveTo(x, -0.04f);
                pen.LineTo(x + 0.2f, -0.28f);
                pen.MoveTo(x, -0.04f);
                pen.LineTo(x + 0.2f, 0.18f);
            }
            pen.Stroke(Rgb(178, 210, 110), 0.07f);
            pen.Pop();

            Sakura(pen, 0.26f, -0.5f, 0.3f, 12f);
        }

        // ================================================================== buns

        /// <summary>A bun seen from the front-top: dome on top, elliptical base at the bottom.</summary>
        static void BunPath(ArtPen pen, float rx, float top, float shoulder, float bottom)
        {
            var up = (shoulder - top) * 0.58f;
            var down = (bottom - shoulder) * 0.75f;
            pen.MoveTo(-rx, shoulder);
            pen.CubicTo(-rx, shoulder - up, -rx * 0.56f, top, 0f, top);
            pen.CubicTo(rx * 0.56f, top, rx, shoulder - up, rx, shoulder);
            pen.CubicTo(rx, shoulder + down, rx * 0.56f, bottom, 0f, bottom);
            pen.CubicTo(-rx * 0.56f, bottom, -rx, shoulder + down, -rx, shoulder);
            pen.Close();
        }

        /// <summary>The pale band baked bread shows near its base.</summary>
        static void BunWaist(ArtPen pen, float rx, float shoulder, float bottom, float thickness, Color color)
        {
            var down = (bottom - shoulder) * 0.75f;
            pen.Begin();
            pen.MoveTo(-rx, shoulder);
            pen.CubicTo(-rx, shoulder + down, -rx * 0.56f, bottom, 0f, bottom);
            pen.CubicTo(rx * 0.56f, bottom, rx, shoulder + down, rx, shoulder);
            pen.CubicTo(rx * 0.98f, shoulder + down * 0.8f, rx * 0.56f, bottom - thickness, 0f, bottom - thickness);
            pen.CubicTo(-rx * 0.56f, bottom - thickness, -rx * 0.98f, shoulder + down * 0.8f, -rx, shoulder);
            pen.Close();
            pen.Fill(color);
        }

        static void Gloss(ArtPen pen, float x, float y, float rx, float ry, float rot, float alpha = 0.6f)
        {
            pen.Begin();
            pen.AddEllipse(x, y, rx, ry, rot);
            pen.Fill(Alpha(White, alpha));
            pen.Begin();
            pen.AddCircle(x + rx * 1.15f, y + rx * 1.0f, ry * 0.55f);
            pen.Fill(Alpha(White, alpha));
        }

        static void DrawAnpan(ArtPen pen, bool face)
        {
            const float rx = 0.9f, top = -0.66f, shoulder = 0.2f, bottom = 0.68f;

            pen.Begin();
            BunPath(pen, rx, top, shoulder, bottom);
            pen.Outline();
            pen.Fill(Rgb(190, 102, 42));
            if (!pen.details)
                return;

            // Shiny egg-washed dome.
            pen.Begin();
            pen.AddEllipse(-0.06f, -0.22f, 0.72f, 0.4f);
            pen.Fill(Rgb(212, 128, 58));
            pen.Begin();
            pen.AddEllipse(-0.16f, -0.32f, 0.44f, 0.22f, -8f);
            pen.Fill(Rgb(230, 156, 84));
            BunWaist(pen, rx, shoulder, bottom, 0.13f, Rgb(244, 196, 128));
            Gloss(pen, -0.5f, -0.3f, 0.14f, 0.055f, -42f);

            // Navel (heso) with black and white sesame.
            pen.Begin();
            pen.AddEllipse(0f, -0.44f, 0.16f, 0.07f);
            pen.Fill(Rgb(146, 70, 26));
            pen.Begin();
            pen.AddEllipse(0f, -0.455f, 0.1f, 0.04f);
            pen.Fill(Rgb(112, 50, 18));
            for (int i = 0; i < 8; i++)
            {
                var a = i * 45f + 10f;
                var r = (i % 2 == 0) ? 1f : 0.62f;
                var p = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad) * 0.22f * r, -0.44f + Mathf.Sin(a * Mathf.Deg2Rad) * 0.1f * r);
                Seed(pen, p, a + 90f, i == 2 || i == 5 ? Sesame : Poppy, 0.045f, 0.024f, i == 2 || i == 5 ? 0.3f : 0f);
            }

            if (face)
                Face(pen, 0f, 0.1f, 1f);
        }

        static void DrawMelonPan(ArtPen pen, bool face, bool maple)
        {
            const float rx = 0.88f, top = -0.72f, shoulder = 0.16f, bottom = 0.7f;

            pen.Begin();
            BunPath(pen, rx, top, shoulder, bottom);
            pen.Outline();
            pen.Fill(Rgb(246, 190, 92));
            if (!pen.details)
                return;

            // Cookie crust: sunny yellow cap, slightly golden toward the base.
            pen.Begin();
            pen.AddEllipse(0f, -0.1f, 0.84f, 0.66f);
            pen.Fill(MelonCrust);
            pen.Begin();
            pen.AddEllipse(-0.14f, -0.3f, 0.5f, 0.3f, -10f);
            pen.Fill(Rgb(255, 232, 160));

            // Crosshatch grid, curved over the dome and clipped to the bun.
            pen.Begin();
            BunPath(pen, rx, top, shoulder, bottom);
            pen.painter.PushClip();
            pen.Begin();
            for (int i = -4; i <= 4; i++)
            {
                var o = i * 0.3f;
                pen.MoveTo(o - 0.9f, 0.9f);
                pen.QuadTo(o - 0.2f, -0.05f, o + 0.9f, -0.9f);
                pen.MoveTo(o + 0.9f, 0.9f);
                pen.QuadTo(o + 0.2f, -0.05f, o - 0.9f, -0.9f);
            }
            pen.Stroke(Rgb(222, 156, 56), 0.045f);

            // Sugar glints in a few cells.
            pen.Begin();
            for (int i = 0; i < 14; i++)
            {
                var x = Range(i, 131, -0.7f, 0.7f);
                var y = Range(i, 137, -0.6f, 0.5f);
                if (face && Mathf.Abs(x) < 0.6f && y > -0.05f && y < 0.45f)
                    continue;
                pen.AddCircle(x, y, Range(i, 139, 0.018f, 0.03f));
            }
            pen.Fill(Alpha(White, 0.9f));

            if (maple)
                DrawMapleSyrup(pen);

            pen.painter.PopClip();

            BunWaist(pen, rx, shoulder, bottom, 0.1f, Rgb(236, 168, 72));

            // Re-ink the outline over the clipped content.
            pen.Begin();
            BunPath(pen, rx, top, shoulder, bottom);
            pen.Ink();

            Gloss(pen, -0.52f, -0.36f, 0.13f, 0.05f, -45f, 0.75f);

            if (maple)
                MapleLeaf(pen, 0.34f, -0.62f, 0.28f, 18f, MapleRed, 0.6f);

            if (face)
                Face(pen, 0f, maple ? 0.2f : 0.14f, 0.95f);
        }

        static void DrawMapleSyrup(ArtPen pen)
        {
            // Glaze cap with drips (inside the bun clip).
            pen.Begin();
            SyrupPath(pen);
            pen.Fill(Alpha(MapleSyrup, 0.94f));
            pen.Begin();
            SyrupPath(pen);
            pen.Stroke(Alpha(pen.inkColor, 0.7f), 0.05f);

            // Light through the glaze.
            pen.Begin();
            pen.AddEllipse(-0.2f, -0.46f, 0.42f, 0.13f, -6f);
            pen.Fill(Rgb(238, 160, 66));
            pen.Begin();
            pen.MoveTo(-0.56f, -0.36f);
            pen.QuadTo(-0.4f, -0.56f, -0.1f, -0.6f);
            pen.Stroke(Alpha(White, 0.8f), 0.06f);
            pen.Begin();
            pen.AddCircle(-0.52f, 0.0f, 0.03f);
            pen.AddCircle(0.6f, -0.02f, 0.025f);
            pen.Fill(Alpha(White, 0.85f));
        }

        static void SyrupPath(ArtPen pen)
        {
            pen.MoveTo(-1f, -0.24f);
            pen.LineTo(-1f, -1f);
            pen.LineTo(1f, -1f);
            pen.LineTo(1f, -0.2f);
            pen.QuadTo(0.86f, -0.16f, 0.78f, -0.16f);
            // drip right
            pen.CubicTo(0.66f, -0.16f, 0.68f, 0.06f, 0.6f, 0.06f);
            pen.CubicTo(0.52f, 0.06f, 0.54f, -0.14f, 0.42f, -0.16f);
            pen.QuadTo(0.3f, -0.18f, 0.2f, -0.12f);
            // drip center (short)
            pen.CubicTo(0.12f, -0.08f, 0.14f, -0.02f, 0.06f, -0.02f);
            pen.CubicTo(-0.02f, -0.02f, 0f, -0.14f, -0.12f, -0.16f);
            pen.QuadTo(-0.26f, -0.18f, -0.36f, -0.12f);
            // drip left (long)
            pen.CubicTo(-0.44f, -0.08f, -0.42f, 0.12f, -0.52f, 0.12f);
            pen.CubicTo(-0.62f, 0.12f, -0.6f, -0.12f, -0.7f, -0.16f);
            pen.QuadTo(-0.84f, -0.22f, -1f, -0.24f);
            pen.Close();
        }

        static void DrawCurryPan(ArtPen pen, bool face)
        {
            pen.Begin();
            CrumbOval(pen, 0f, 0.04f, 0.88f, 0.6f, -6f, 0.06f, 0);
            pen.Outline();
            pen.Fill(Rgb(204, 122, 40));
            if (!pen.details)
                return;

            // Golden crumb coat with a toasty underside.
            pen.Begin();
            CrumbOval(pen, -0.02f, -0.02f, 0.82f, 0.52f, -6f, 0.05f, 3);
            pen.Fill(Rgb(232, 156, 60));
            pen.Begin();
            pen.AddEllipse(-0.16f, -0.2f, 0.52f, 0.24f, -10f);
            pen.Fill(Rgb(248, 190, 98));

            // Crumbs.
            for (int i = 0; i < 54; i++)
            {
                var a = Range(i, 151, 0f, 360f) * Mathf.Deg2Rad;
                var r = Mathf.Sqrt(Hash(i, 157)) * 0.92f;
                var x = Mathf.Cos(a) * 0.8f * r;
                var y = 0.02f + Mathf.Sin(a) * 0.52f * r;
                if (face && Mathf.Abs(x) < 0.56f && y > -0.12f && y < 0.38f)
                    continue;
                var light = (i % 3) != 0;
                pen.Begin();
                pen.AddEllipse(x, y, Range(i, 163, 0.028f, 0.05f), Range(i, 167, 0.018f, 0.032f), Range(i, 173, 0f, 180f));
                pen.Fill(light ? Rgb(255, 224, 140) : Rgb(170, 88, 24));
            }

            Gloss(pen, -0.5f, -0.3f, 0.1f, 0.04f, -30f, 0.55f);

            if (face)
                Face(pen, 0f, 0.1f, 0.95f);
        }

        /// <summary>A rotated oval with a bumpy, crumb-coated edge.</summary>
        static void CrumbOval(ArtPen pen, float cx, float cy, float rx, float ry, float rotationDeg, float bump, int salt)
        {
            const int n = 38;
            var rot = rotationDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rot);
            var sin = Mathf.Sin(rot);

            Vector2 Point(int i)
            {
                var a = (i % n) / (float)n * Mathf.PI * 2f;
                var k = 1f + ((i % 2 == 0) ? bump : -bump * 0.3f) + (Hash(i % n, salt) - 0.5f) * bump * 0.8f;
                var x = Mathf.Cos(a) * rx * k;
                var y = Mathf.Sin(a) * ry * k;
                return new Vector2(cx + x * cos - y * sin, cy + x * sin + y * cos);
            }

            var start = (Point(0) + Point(1)) * 0.5f;
            pen.MoveTo(start.x, start.y);
            for (int i = 1; i <= n; i++)
            {
                var c = Point(i);
                var next = (Point(i) + Point(i + 1)) * 0.5f;
                pen.QuadTo(c.x, c.y, next.x, next.y);
            }
            pen.Close();
        }

        // ================================================================== taiyaki

        static void TaiyakiBody(ArtPen pen)
        {
            pen.MoveTo(-0.9f, -0.04f);
            pen.CubicTo(-0.84f, -0.4f, -0.52f, -0.54f, -0.14f, -0.52f);
            pen.CubicTo(0.18f, -0.5f, 0.42f, -0.3f, 0.54f, -0.14f);
            pen.CubicTo(0.64f, -0.3f, 0.78f, -0.48f, 0.94f, -0.54f);
            pen.CubicTo(0.86f, -0.34f, 0.8f, -0.16f, 0.8f, 0f);
            pen.CubicTo(0.8f, 0.16f, 0.86f, 0.34f, 0.94f, 0.54f);
            pen.CubicTo(0.78f, 0.48f, 0.64f, 0.3f, 0.54f, 0.14f);
            pen.CubicTo(0.42f, 0.32f, 0.18f, 0.5f, -0.14f, 0.5f);
            pen.CubicTo(-0.52f, 0.5f, -0.84f, 0.36f, -0.9f, 0.06f);
            pen.QuadTo(-0.8f, 0.01f, -0.9f, -0.04f);
            pen.Close();
        }

        static void TaiyakiFins(ArtPen pen)
        {
            // Dorsal fin.
            pen.MoveTo(-0.22f, -0.48f);
            pen.CubicTo(-0.1f, -0.76f, 0.2f, -0.72f, 0.36f, -0.32f);
            pen.Close();
            // Belly fin.
            pen.MoveTo(-0.08f, 0.46f);
            pen.CubicTo(0f, 0.68f, 0.2f, 0.7f, 0.3f, 0.36f);
            pen.Close();
        }

        static void DrawTaiyaki(ArtPen pen, bool face, bool golden)
        {
            var edge = golden ? Rgb(255, 232, 140) : Rgb(242, 194, 112);
            var body = golden ? Gold : Rgb(212, 136, 54);
            var detail = golden ? Rgb(214, 140, 14) : Rgb(160, 84, 28);
            var light = golden ? Rgb(255, 244, 190) : Rgb(236, 170, 92);

            pen.Push(0f, 0.06f);

            pen.Begin();
            TaiyakiFins(pen);
            TaiyakiBody(pen);
            pen.Outline();
            pen.Fill(edge);
            if (!pen.details)
            {
                pen.Pop();
                return;
            }

            // Baked body inside the pale batter rim.
            pen.Begin();
            pen.Push(0.02f, 0f, 0.88f);
            TaiyakiBody(pen);
            pen.Pop();
            pen.Push(0.04f, 0.02f, 0.82f);
            TaiyakiFins(pen);
            pen.Pop();
            pen.Fill(body);

            // Toasty light on the back.
            pen.Begin();
            pen.AddEllipse(-0.2f, -0.24f, 0.4f, 0.13f, -6f);
            pen.Fill(light);

            // Scales (the mold pattern): arcs facing the tail.
            pen.Begin();
            for (int col = 0; col < 4; col++)
            {
                var x = -0.12f + col * 0.17f;
                var rows = col < 3 ? 3 : 2;
                for (int row = 0; row < rows; row++)
                {
                    var y = (row - (rows - 1) * 0.5f) * 0.2f + ((col % 2 == 0) ? 0f : 0.02f);
                    if (golden && col >= 1 && col <= 2 && Mathf.Abs(y) < 0.12f)
                        continue;
                    pen.AddArc(x, y, 0.09f, 0.1f, -70f, 70f);
                }
            }
            // Gill.
            pen.AddArc(-0.62f, 0.02f, 0.28f, 0.3f, -48f, 48f);
            // Tail rays.
            pen.MoveTo(0.62f, -0.08f);
            pen.LineTo(0.8f, -0.32f);
            pen.MoveTo(0.64f, 0f);
            pen.LineTo(0.74f, 0f);
            pen.MoveTo(0.62f, 0.08f);
            pen.LineTo(0.8f, 0.32f);
            // Fin rays.
            pen.MoveTo(-0.06f, -0.5f);
            pen.LineTo(0f, -0.6f);
            pen.MoveTo(0.08f, -0.46f);
            pen.LineTo(0.16f, -0.56f);
            pen.Stroke(detail, 0.045f);

            if (golden)
            {
                MapleLeaf(pen, 0.14f, 0.02f, 0.2f, -12f, MapleRed, 0.55f);
                pen.Begin();
                pen.MoveTo(-0.66f, -0.3f);
                pen.QuadTo(-0.4f, -0.46f, -0.08f, -0.44f);
                pen.Stroke(Alpha(White, 0.85f), 0.06f);
            }

            // Eye.
            pen.Begin();
            pen.AddCircle(-0.55f, -0.16f, 0.13f);
            pen.Fill(White);
            pen.Ink(0.6f);
            pen.Begin();
            pen.AddEllipse(-0.57f, -0.15f, 0.075f, 0.09f);
            pen.Fill(pen.inkColor);
            pen.Begin();
            pen.AddCircle(-0.6f, -0.19f, 0.03f);
            pen.Fill(White);

            if (face)
            {
                pen.Begin();
                pen.AddEllipse(-0.5f, 0.12f, 0.1f, 0.06f);
                pen.Fill(Blush);
                pen.Begin();
                pen.MoveTo(-0.8f, 0.12f);
                pen.QuadTo(-0.74f, 0.2f, -0.68f, 0.12f);
                pen.Ink(0.6f);
            }

            pen.Pop();

            if (golden)
            {
                Sparkle(pen, -0.7f, -0.62f, 0.16f, White, 0.45f);
                Sparkle(pen, 0.74f, -0.74f, 0.12f, GoldLight, 0.45f);
                Sparkle(pen, 0.5f, 0.78f, 0.1f, White, 0.45f);
            }
        }

        // ================================================================== dorayaki

        static void DrawDorayaki(ArtPen pen, bool face)
        {
            const float rx = 0.88f, ry = 0.34f;
            const float topY = -0.38f, topH = 0.2f;
            const float botY = 0.14f, botH = 0.2f;
            const float ankoY = 0.04f;

            pen.Begin();
            pen.AddPuck(0f, botY, rx - 0.02f, ry, botH);
            CrumbOval(pen, 0f, ankoY, rx - 0.08f, ry - 0.01f, 0f, 0.035f, 5);
            pen.AddPuck(0f, topY, rx, ry + 0.02f, topH);
            pen.Outline();
            pen.Fill(Rgb(236, 176, 92));
            if (!pen.details)
                return;

            // Bottom pancake (pale inner side up).
            pen.Begin();
            pen.AddEllipse(0f, botY, rx - 0.02f, ry);
            pen.Fill(Rgb(252, 224, 160));
            pen.Begin();
            pen.AddArc(0f, botY + botH, rx - 0.02f, ry, 10f, 170f);
            pen.Stroke(Alpha(Rgb(190, 110, 40), 0.7f), 0.06f);

            // Anko filling with bean bits.
            pen.Begin();
            CrumbOval(pen, 0f, ankoY, rx - 0.08f, ry - 0.01f, 0f, 0.035f, 5);
            pen.FillInk(Anko, 0.6f);
            pen.Begin();
            for (int i = 0; i < 9; i++)
            {
                var x = -0.64f + i * 0.16f;
                pen.AddEllipse(x, ankoY + 0.26f + Mathf.Sin(i * 1.7f) * 0.03f - Mathf.Abs(x) * 0.12f, 0.045f, 0.028f, i * 40f);
            }
            pen.Fill(Rgb(154, 66, 72));
            pen.Begin();
            pen.AddCircle(-0.3f, ankoY + 0.24f, 0.018f);
            pen.AddCircle(0.34f, ankoY + 0.25f, 0.016f);
            pen.Fill(Alpha(White, 0.8f));

            // Top pancake: pale side, browned top fading to a golden rim.
            pen.Begin();
            pen.AddPuck(0f, topY, rx, ry + 0.02f, topH);
            pen.Fill(Rgb(252, 224, 160));
            pen.Begin();
            pen.AddEllipse(0f, topY, rx, ry + 0.02f);
            pen.Fill(Rgb(238, 178, 92));
            pen.Begin();
            pen.AddEllipse(0f, topY - 0.01f, rx - 0.1f, ry - 0.04f);
            pen.Fill(Rgb(214, 134, 56));
            pen.Begin();
            pen.AddEllipse(0f, topY - 0.02f, rx - 0.26f, ry - 0.11f);
            pen.Fill(Rgb(190, 104, 40));
            pen.Begin();
            pen.AddArc(0f, topY - 0.02f, rx - 0.18f, ry - 0.08f, 200f, 250f);
            pen.Stroke(Alpha(White, 0.55f), 0.06f);
            pen.Begin();
            pen.AddArc(0f, topY, rx, ry + 0.02f, 10f, 170f);
            pen.Stroke(Alpha(pen.inkColor, 0.45f), 0.035f);
            pen.Begin();
            pen.AddPuck(0f, topY, rx, ry + 0.02f, topH);
            pen.Ink(0.8f);

            if (face)
            {
                Face(pen, 0f, topY - 0.02f, 0.8f);
            }
            else
            {
                // Branded maple stamp (yakiin).
                pen.Push(0f, topY - 0.02f, 1f, 0f, 1f, 0.45f);
                MapleLeaf(pen, 0f, 0f, 0.2f, 0f, Rgb(120, 54, 22), 0f, 1f, false);
                pen.Pop();
            }
        }
    }
}
