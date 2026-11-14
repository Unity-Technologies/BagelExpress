using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// Begu-chan (ベグちゃん), the sesame bagel yuru-chara shopkeeper. Wears a seasonal accessory picked by the
    /// theme through <c>--be-mascot-accessory</c> (maple | sakura | matsuri | snow), blinks now and then,
    /// and has three moods.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class BeguChan : VisualElement
    {
        public static readonly string ussClassName = "be-begu-chan";

        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");
        static readonly CustomStyleProperty<string> s_AccessoryProperty = new("--be-mascot-accessory");

        enum Accessory
        {
            Maple,
            Sakura,
            Matsuri,
            Snow
        }

        // Body geometry (same language as the bagel PastryIcon, a little more upright).
        const float k_TopY = 0.04f;
        const float k_Rx = 0.78f;
        const float k_Ry = 0.64f;
        const float k_Height = 0.14f;
        const float k_HoleY = -0.25f;
        const float k_HoleRx = 0.25f;
        const float k_HoleRy = 0.125f;
        const float k_FaceY = 0.27f;

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        readonly IVisualElementScheduledItem m_Ticker;

        MascotMood m_Mood;
        bool m_Blink = true;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        Accessory m_Accessory = Accessory.Maple;

        bool m_EyesClosed;
        float m_Clock;
        float m_NextBlink = 2.2f;
        float m_BlinkEnd;
        int m_BlinkCount;

        [UxmlAttribute, CreateProperty]
        public MascotMood mood
        {
            get => m_Mood;
            set
            {
                if (m_Mood == value)
                    return;
                m_Mood = value;
                UpdateTicker();
                MarkDirtyRepaint();
            }
        }

        /// <summary>Blink every few seconds.</summary>
        [UxmlAttribute, CreateProperty]
        public bool blink
        {
            get => m_Blink;
            set
            {
                if (m_Blink == value)
                    return;
                m_Blink = value;
                m_EyesClosed = false;
                UpdateTicker();
                MarkDirtyRepaint();
            }
        }

        public BeguChan()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
            m_Ticker = schedule.Execute(Tick).Every(50);
        }

        void UpdateTicker()
        {
            if (m_Blink || m_Mood == MascotMood.Sleepy)
                m_Ticker.Resume();
            else
                m_Ticker.Pause();
        }

        void OnStyleChanged()
        {
            var ink = m_Style.Get(s_InkProperty, Ink);
            var inkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            var accessory = m_Style.Get(s_AccessoryProperty, "maple") switch
            {
                "sakura" => Accessory.Sakura,
                "matsuri" => Accessory.Matsuri,
                "snow" => Accessory.Snow,
                _ => Accessory.Maple
            };
            if (ink == m_Ink && inkWidth == m_InkWidth && accessory == m_Accessory)
                return;
            m_Ink = ink;
            m_InkWidth = inkWidth;
            m_Accessory = accessory;
            MarkDirtyRepaint();
        }

        void Tick(TimerState state)
        {
            if (!ShouldAnimate(this))
                return;

            m_Clock += Mathf.Min(state.deltaTime, 200) / 1000f;

            if (m_Mood == MascotMood.Sleepy)
            {
                // Breathing nose bubble.
                MarkDirtyRepaint();
                return;
            }

            if (!m_Blink)
                return;

            if (!m_EyesClosed && m_Clock >= m_NextBlink)
            {
                m_EyesClosed = true;
                m_BlinkEnd = m_Clock + 0.13f;
                MarkDirtyRepaint();
            }
            else if (m_EyesClosed && m_Clock >= m_BlinkEnd)
            {
                m_EyesClosed = false;
                m_BlinkCount++;
                // Mostly single blinks every 2-4.5 s, sometimes a quick double blink.
                m_NextBlink = m_Clock + (Hash(m_BlinkCount, 7) < 0.2f ? 0.22f : Range(m_BlinkCount, 11, 2f, 4.5f));
                MarkDirtyRepaint();
            }
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 2f) || !(rect.height >= 2f))
                return;

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, rect, m_Ink, 0.1f);
            pen.ink *= m_InkWidth;

            var excited = m_Mood == MascotMood.Excited;
            var sleepy = m_Mood == MascotMood.Sleepy;

            // Soft ground shadow.
            pen.Begin();
            pen.AddEllipse(0f, 0.93f, 0.6f, 0.06f);
            pen.Fill(Alpha(m_Ink, 0.2f));

            // Feet and arms tuck behind the body.
            pen.Begin();
            pen.AddEllipse(-0.3f, 0.86f, 0.17f, 0.09f);
            pen.AddEllipse(0.3f, 0.86f, 0.17f, 0.09f);
            pen.FillInk(CrustSide);

            for (int side = -1; side <= 1; side += 2)
            {
                pen.Begin();
                if (excited)
                    pen.AddEllipse(side * 0.8f, 0.02f, 0.11f, 0.17f, side * 28f);
                else if (sleepy)
                    pen.AddEllipse(side * 0.76f, 0.5f, 0.11f, 0.16f, side * -12f);
                else
                    pen.AddEllipse(side * 0.8f, 0.42f, 0.11f, 0.16f, side * -38f);
                pen.FillInk(CrustTop);
            }

            DrawBody(pen);
            DrawFace(pen, sleepy, excited);
            DrawAccessory(pen);

            if (excited)
            {
                Sparkle(pen, -0.84f, -0.42f, 0.13f, GoldLight, 0.4f);
                Sparkle(pen, 0.9f, -0.62f, 0.09f, White, 0.4f);
                Sparkle(pen, 0.72f, 0.82f, 0.07f, GoldLight, 0.4f);
            }
            else if (sleepy)
            {
                DrawZ(pen, 0.66f, -0.66f, 0.09f);
                DrawZ(pen, 0.86f, -0.86f, 0.065f);
            }
        }

        void DrawBody(ArtPen pen)
        {
            // Far inner wall seen through the hole.
            pen.Begin();
            pen.AddArc(0f, k_HoleY, k_HoleRx, k_HoleRy, 180f, 360f);
            pen.QuadTo(0f, k_HoleY + 0.06f, -k_HoleRx, k_HoleY);
            pen.Close();
            pen.Fill(Rgb(156, 82, 32));

            pen.Begin();
            pen.AddPuck(0f, k_TopY, k_Rx, k_Ry, k_Height);
            pen.Outline();
            pen.Begin();
            pen.AddPuck(0f, k_TopY, k_Rx, k_Ry, k_Height);
            pen.AddEllipse(0f, k_HoleY, k_HoleRx, k_HoleRy);
            pen.Fill(CrustSide, FillRule.OddEven);

            pen.Begin();
            pen.AddEllipse(0f, k_TopY, k_Rx, k_Ry);
            pen.AddEllipse(0f, k_HoleY, k_HoleRx, k_HoleRy);
            pen.Fill(CrustTop, FillRule.OddEven);

            // Rounded rim and highlights.
            pen.Begin();
            pen.AddEllipse(0f, k_TopY, k_Rx, k_Ry);
            pen.AddEllipse(0f, k_TopY - 0.07f, k_Rx - 0.08f, k_Ry - 0.1f);
            pen.Fill(Alpha(Darker(CrustTop, 0.18f), 0.55f), FillRule.OddEven);

            const float midY = (k_HoleY + k_TopY) * 0.5f - 0.03f;
            const float midRx = (k_HoleRx + k_Rx) * 0.5f;
            const float midRy = (k_HoleRy + k_Ry) * 0.5f;
            pen.Begin();
            pen.AddArc(0f, midY, midRx, midRy, 194f, 258f);
            pen.Stroke(CrustLight, 0.14f);
            pen.Begin();
            pen.AddArc(0f, midY, midRx, midRy, 296f, 318f);
            pen.Stroke(CrustLight, 0.1f);
            pen.Begin();
            pen.AddArc(0f, midY, midRx, midRy, 204f, 224f);
            pen.Stroke(Alpha(White, 0.8f), 0.055f);

            pen.Begin();
            pen.AddArc(0f, k_TopY, k_Rx, k_Ry, 12f, 168f);
            pen.Stroke(Alpha(pen.inkColor, 0.45f), 0.03f);

            pen.Begin();
            pen.AddEllipse(0f, k_HoleY, k_HoleRx, k_HoleRy);
            pen.Ink(0.75f);

            // Sesame seeds around the top of the ring (never on the face).
            for (int i = 0; i < 14; i++)
            {
                var angle = 150f + i * 17f + Range(i, 3, -5f, 5f);
                var t = Range(i, 5, 0.2f, 0.85f);
                var a = angle * Mathf.Deg2Rad;
                var inner = new Vector2(k_HoleRx * Mathf.Cos(a), k_HoleY + k_HoleRy * Mathf.Sin(a));
                var outer = new Vector2(k_Rx * Mathf.Cos(a), k_TopY + k_Ry * Mathf.Sin(a));
                var p = Vector2.Lerp(inner, outer, t);
                if (p.y > -0.02f && Mathf.Abs(p.x) < 0.66f)
                    continue;
                pen.Begin();
                pen.AddEllipse(p.x, p.y, 0.065f, 0.034f, Range(i, 9, 0f, 180f));
                pen.Fill(Sesame);
                pen.Stroke(Alpha(pen.inkColor, 0.8f), 0.022f);
            }
        }

        void DrawFace(ArtPen pen, bool sleepy, bool excited)
        {
            var style = m_Mood switch
            {
                MascotMood.Excited => FaceStyle.Excited,
                MascotMood.Sleepy => FaceStyle.Sleepy,
                _ => m_EyesClosed ? FaceStyle.Blink : FaceStyle.Happy
            };
            if (excited && m_EyesClosed)
                style = FaceStyle.Excited;

            Face(pen, 0f, k_FaceY, 1.22f, style, 0.29f, 1.3f);

            if (sleepy)
            {
                // Hanachōchin: the manga sleep bubble, gently breathing.
                var breath = 0.75f + 0.25f * Mathf.Sin(m_Clock * 2.2f);
                var r = 0.15f * breath;
                var cx = 0.07f + r;
                var cy = k_FaceY + 0.2f + r * 0.2f;
                pen.Begin();
                pen.AddCircle(cx, cy, r);
                pen.Fill(Rgb(190, 228, 255, 0.6f));
                pen.Ink(0.4f);
                pen.Begin();
                pen.AddArc(cx, cy, r * 0.65f, r * 0.65f, 200f, 260f);
                pen.Stroke(Alpha(White, 0.95f), 0.035f);
            }
        }

        static void DrawZ(ArtPen pen, float x, float y, float s)
        {
            pen.Begin();
            pen.MoveTo(x - s, y - s);
            pen.LineTo(x + s, y - s);
            pen.LineTo(x - s, y + s);
            pen.LineTo(x + s, y + s);
            pen.Stroke(pen.inkColor, s * 0.75f);
            pen.Stroke(White, s * 0.38f);
        }

        void DrawAccessory(ArtPen pen)
        {
            switch (m_Accessory)
            {
                case Accessory.Maple:
                    MapleLeaf(pen, 0.7f, -0.46f, 0.17f, 58f, Rgb(246, 150, 40), 0.7f);
                    MapleLeaf(pen, 0.48f, -0.62f, 0.28f, 24f, MapleRed, 0.7f);
                    break;
                case Accessory.Sakura:
                    // Hairpin: a bar with a blossom and a bud.
                    pen.Begin();
                    pen.MoveTo(-0.16f, -0.62f);
                    pen.LineTo(-0.66f, -0.36f);
                    pen.Stroke(pen.inkColor, 0.1f);
                    pen.Stroke(Rgb(255, 214, 70), 0.05f);
                    Sakura(pen, -0.6f, -0.52f, 0.13f, -20f, SakuraPink, 0.7f);
                    Sakura(pen, -0.36f, -0.58f, 0.22f, 8f, null, 0.7f);
                    break;
                case Accessory.Matsuri:
                    DrawHachimaki(pen);
                    break;
                case Accessory.Snow:
                    DrawToque(pen);
                    break;
            }
        }

        static Vector2 BandPoint(float t)
        {
            // Quadratic band over the top of the ring, dipping in the middle (seen from above-front).
            var a = new Vector2(-0.66f, -0.5f);
            var c = new Vector2(0f, -0.38f);
            var b = new Vector2(0.66f, -0.5f);
            var u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        void DrawHachimaki(ArtPen pen)
        {
            // Knot tails flapping out on the right.
            pen.Begin();
            pen.MoveTo(0.6f, -0.5f);
            pen.CubicTo(0.78f, -0.62f, 0.9f, -0.56f, 1f, -0.66f);
            pen.LineTo(0.96f, -0.5f);
            pen.CubicTo(0.86f, -0.44f, 0.76f, -0.46f, 0.62f, -0.44f);
            pen.Close();
            pen.MoveTo(0.6f, -0.46f);
            pen.CubicTo(0.76f, -0.38f, 0.86f, -0.26f, 0.98f, -0.24f);
            pen.LineTo(0.9f, -0.14f);
            pen.CubicTo(0.8f, -0.2f, 0.7f, -0.3f, 0.58f, -0.38f);
            pen.Close();
            pen.FillInk(MapleRed, 0.7f);

            // Twisted red/white rope: overlapping tilted beads.
            const int beads = 12;
            pen.Begin();
            for (int i = 0; i <= beads; i++)
            {
                var p = BandPoint(i / (float)beads);
                pen.AddEllipse(p.x, p.y, 0.08f, 0.062f, 35f);
            }
            pen.Outline(0.7f);
            for (int i = 0; i <= beads; i++)
            {
                var p = BandPoint(i / (float)beads);
                pen.Begin();
                pen.AddEllipse(p.x, p.y, 0.08f, 0.062f, 35f);
                pen.Fill(i % 2 == 0 ? MapleRed : White);
                pen.Begin();
                pen.MoveTo(p.x - 0.045f, p.y + 0.04f);
                pen.LineTo(p.x + 0.04f, p.y - 0.045f);
                pen.Stroke(Alpha(pen.inkColor, 0.5f), 0.02f);
            }

            // Knot.
            var k = BandPoint(0.95f);
            pen.Begin();
            pen.AddCircle(k.x, k.y, 0.085f);
            pen.FillInk(MapleRed, 0.7f);
            pen.Begin();
            pen.AddCircle(k.x - 0.02f, k.y - 0.025f, 0.025f);
            pen.Fill(Alpha(White, 0.7f));
        }

        void DrawToque(ArtPen pen)
        {
            var red = MapleRed;
            var cream = Rgb(255, 246, 228);

            // Knitted dome.
            pen.Begin();
            pen.MoveTo(-0.6f, -0.42f);
            pen.CubicTo(-0.62f, -0.82f, -0.3f, -0.98f, 0f, -0.98f);
            pen.CubicTo(0.3f, -0.98f, 0.62f, -0.82f, 0.6f, -0.42f);
            pen.Close();
            pen.Outline();
            pen.Fill(red);

            // Cream stripe across the dome (clipped to it) + knit stitches.
            pen.Begin();
            pen.MoveTo(-0.6f, -0.42f);
            pen.CubicTo(-0.62f, -0.82f, -0.3f, -0.98f, 0f, -0.98f);
            pen.CubicTo(0.3f, -0.98f, 0.62f, -0.82f, 0.6f, -0.42f);
            pen.Close();
            pen.painter.PushClip();
            pen.Begin();
            pen.MoveTo(-0.7f, -0.62f);
            pen.QuadTo(0f, -0.76f, 0.7f, -0.62f);
            pen.LineTo(0.7f, -0.53f);
            pen.QuadTo(0f, -0.67f, -0.7f, -0.53f);
            pen.Close();
            pen.Fill(cream);
            pen.Begin();
            for (int i = -3; i <= 3; i++)
            {
                var x = i * 0.15f;
                pen.MoveTo(x * 0.92f, -0.9f + Mathf.Abs(i) * 0.02f);
                pen.QuadTo(x * 1.05f, -0.7f, x * 1.1f, -0.5f);
            }
            pen.Stroke(Alpha(Darker(red, 0.3f), 0.5f), 0.025f);
            pen.painter.PopClip();
            pen.Begin();
            pen.MoveTo(-0.6f, -0.42f);
            pen.CubicTo(-0.62f, -0.82f, -0.3f, -0.98f, 0f, -0.98f);
            pen.CubicTo(0.3f, -0.98f, 0.62f, -0.82f, 0.6f, -0.42f);
            pen.Ink(0.6f);

            // Folded ribbed cuff.
            pen.Begin();
            pen.MoveTo(-0.7f, -0.48f);
            pen.QuadTo(0f, -0.6f, 0.7f, -0.48f);
            pen.LineTo(0.68f, -0.32f);
            pen.QuadTo(0f, -0.42f, -0.68f, -0.32f);
            pen.Close();
            pen.FillInk(cream, 0.8f);
            pen.Begin();
            for (int i = -6; i <= 6; i++)
            {
                var x = i * 0.1f;
                var top = -0.54f + x * x * 0.12f;
                pen.MoveTo(x, top + 0.04f);
                pen.LineTo(x, top + 0.13f);
            }
            pen.Stroke(Rgb(214, 196, 170), 0.025f);

            // Pompom.
            pen.Begin();
            pen.AddCircle(0f, -0.95f, 0.12f);
            pen.FillInk(cream, 0.8f);
            pen.Begin();
            pen.AddCircle(-0.04f, -0.99f, 0.035f);
            pen.Fill(White);

            // A maple leaf patch, of course.
            MapleLeaf(pen, 0.3f, -0.78f, 0.1f, 10f, cream, 0.5f, 1f, false);
        }
    }
}
