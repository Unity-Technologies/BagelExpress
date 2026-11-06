using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// A Montréal × Tokyo crossover skyline strip, anchored to the bottom and stretched to the element width.
    /// Left: Mount Royal and its broadcast mast, the Jacques Cartier Bridge, the Biosphère and Plateau row-houses with
    /// spiral staircases. Right: Mount Fuji, the Rainbow Bridge, a five-story pagoda, a torii and houses.
    /// </summary>
    /// <remarks>
    /// Every landmark is one anyone may draw: no tower or stadium whose owner licenses its shape (Tokyo Tower,
    /// Tokyo Skytree and the Olympic Stadium's tower were replaced for that reason).
    /// Custom USS properties: <c>--be-skyline-far</c>, <c>--be-skyline-near</c> (the depth layers; the bridges sit
    /// between them), <c>--be-window-light</c> (lit windows, the beacon on Mount Royal's mast, bridge lights),
    /// <c>--be-skyline-accent</c> (the torii). Landmark sizes follow the element height (capped by its width) so they
    /// keep their proportions.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class SkylineBackdrop : VisualElement
    {
        public static readonly string ussClassName = "be-skyline-backdrop";

        static readonly CustomStyleProperty<Color> s_FarProperty = new("--be-skyline-far");
        static readonly CustomStyleProperty<Color> s_NearProperty = new("--be-skyline-near");
        static readonly CustomStyleProperty<Color> s_WindowProperty = new("--be-window-light");
        static readonly CustomStyleProperty<Color> s_AccentProperty = new("--be-skyline-accent");

        static readonly Color k_DefaultFar = Rgb(240, 160, 120);
        static readonly Color k_DefaultNear = Rgb(150, 60, 50);
        static readonly Color k_DefaultWindow = Rgb(255, 226, 140);
        static readonly Color k_DefaultAccent = Rgb(240, 96, 56);

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;

        Color m_Far = k_DefaultFar;
        Color m_Near = k_DefaultNear;
        Color m_Window = k_DefaultWindow;
        Color m_Accent = k_DefaultAccent;

        // Per-repaint layout.
        float m_X0;
        float m_W;
        float m_Base;
        float m_S;

        public SkylineBackdrop()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
        }

        void OnStyleChanged()
        {
            var far = m_Style.Get(s_FarProperty, k_DefaultFar);
            var near = m_Style.Get(s_NearProperty, k_DefaultNear);
            var window = m_Style.Get(s_WindowProperty, k_DefaultWindow);
            var accent = m_Style.Get(s_AccentProperty, k_DefaultAccent);
            if (far == m_Far && near == m_Near && window == m_Window && accent == m_Accent)
                return;
            m_Far = far;
            m_Near = near;
            m_Window = window;
            m_Accent = accent;
            MarkDirtyRepaint();
        }

        // Layout helpers: X from a fraction of the width, Y from a height above the baseline (in S units).
        float X(float f) => m_X0 + f * m_W;
        float Y(float h) => m_Base - h * m_S;
        float L(float v) => v * m_S;

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 8f) || !(rect.height >= 8f))
                return;

            m_X0 = rect.x;
            m_W = rect.width;
            m_Base = rect.yMax;
            m_S = Mathf.Min(rect.height, rect.width * 0.75f);

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, Vector2.zero, 1f, Ink, 0f, 1f);

            // ---------------- far layer
            DrawMountRoyal(pen);
            DrawFuji(pen);
            DrawFarCity(pen);

            // ---------------- the bridges, between the layers
            DrawJacquesCartierBridge(pen, X(0.1f), X(0.46f));
            DrawRainbowBridge(pen, X(0.81f), X(0.915f), X(1.09f));

            // ---------------- near layer
            DrawBiosphere(pen, X(0.065f));
            DrawPlateau(pen, X(0.31f));
            DrawJapaneseHouses(pen, X(0.505f), 1, 11);
            DrawTorii(pen, X(0.6f));
            DrawPagoda(pen, X(0.715f));
            DrawJapaneseHouses(pen, X(0.77f), 1, 17);

            // Ground strip ties the layers together.
            pen.Begin();
            pen.AddRoundRect(rect.x - 1f, m_Base - L(0.025f), rect.width + 2f, L(0.025f) + 1f, 0f);
            pen.Fill(m_Near);
        }

        // ================================================================== far

        void DrawMountRoyal(ArtPen pen)
        {
            pen.Begin();
            pen.MoveTo(X(0f) - 2f, m_Base);
            pen.LineTo(X(0f) - 2f, Y(0.28f));
            pen.CubicTo(X(0.04f), Y(0.4f), X(0.1f), Y(0.45f), X(0.16f), Y(0.43f));
            pen.CubicTo(X(0.24f), Y(0.4f), X(0.3f), Y(0.2f), X(0.42f), Y(0.1f));
            pen.LineTo(X(0.42f), m_Base);
            pen.Close();
            pen.Fill(m_Far);

            // The broadcast mast on the summit: a slim lattice tower, its beacon lit.
            var mx = X(0.12f);
            var foot = Y(0.44f) + L(0.01f);
            var tip = foot - L(0.17f);
            var halfFoot = L(0.022f);
            var halfTip = L(0.004f);
            pen.Begin();
            pen.MoveTo(mx - halfFoot, foot);
            pen.LineTo(mx - halfTip, tip);
            pen.LineTo(mx + halfTip, tip);
            pen.LineTo(mx + halfFoot, foot);
            pen.Close();
            pen.Fill(m_Far);

            // Lattice: an X in each bay, narrowing with the legs.
            pen.Begin();
            const int k_Bays = 4;
            var top = tip + L(0.03f);
            for (int i = 0; i < k_Bays; i++)
            {
                var y0 = Mathf.Lerp(foot, top, i / (float)k_Bays);
                var y1 = Mathf.Lerp(foot, top, (i + 1) / (float)k_Bays);
                var h0 = Mathf.Lerp(halfFoot, halfTip, i / (float)k_Bays) * 0.7f;
                var h1 = Mathf.Lerp(halfFoot, halfTip, (i + 1) / (float)k_Bays) * 0.7f;
                pen.MoveTo(mx - h0, y0);
                pen.LineTo(mx + h1, y1);
                pen.MoveTo(mx + h0, y0);
                pen.LineTo(mx - h1, y1);
            }
            pen.Stroke(Darker(m_Far, 0.25f), Mathf.Max(0.6f, L(0.004f)));

            // The beacon: a glow and a bright dot, in the window light.
            pen.Begin();
            pen.AddCircle(mx, tip, L(0.02f));
            pen.Fill(Alpha(m_Window, 0.3f));
            pen.Begin();
            pen.AddCircle(mx, tip, L(0.008f));
            pen.Fill(m_Window);
        }

        void DrawFuji(ArtPen pen)
        {
            var peak = X(0.83f);
            var h = 0.4f;
            pen.Begin();
            pen.MoveTo(peak - L(0.56f), m_Base);
            pen.CubicTo(peak - L(0.3f), Y(0.1f), peak - L(0.12f), Y(h - 0.04f), peak - L(0.05f), Y(h));
            pen.LineTo(peak + L(0.05f), Y(h));
            pen.CubicTo(peak + L(0.12f), Y(h - 0.04f), peak + L(0.3f), Y(0.1f), peak + L(0.56f), m_Base);
            pen.Close();
            pen.Fill(Lighter(m_Far, 0.12f));

            // Snow cap with a zig-zag edge.
            pen.Begin();
            pen.MoveTo(peak - L(0.05f), Y(h));
            pen.LineTo(peak + L(0.05f), Y(h));
            pen.LineTo(peak + L(0.11f), Y(h - 0.07f));
            pen.LineTo(peak + L(0.07f), Y(h - 0.06f));
            pen.LineTo(peak + L(0.04f), Y(h - 0.09f));
            pen.LineTo(peak + L(0.0f), Y(h - 0.06f));
            pen.LineTo(peak - L(0.04f), Y(h - 0.09f));
            pen.LineTo(peak - L(0.07f), Y(h - 0.06f));
            pen.LineTo(peak - L(0.11f), Y(h - 0.07f));
            pen.Close();
            pen.Fill(Lighter(m_Far, 0.65f));
        }

        void DrawFarCity(ArtPen pen)
        {
            // Downtown Montréal: generic towers, one with a copper pyramid roof.
            Tower(pen, X(0.27f), 0.07f, 0.34f, 1);
            Tower(pen, X(0.33f), 0.08f, 0.42f, 2);
            pen.Begin();
            var px = X(0.33f);
            pen.MoveTo(px - L(0.04f), Y(0.42f));
            pen.LineTo(px, Y(0.5f));
            pen.LineTo(px + L(0.04f), Y(0.42f));
            pen.Close();
            pen.Fill(m_Far);
            Tower(pen, X(0.4f), 0.09f, 0.36f, 3);
            Tower(pen, X(0.46f), 0.06f, 0.27f, 4);

            // Tokyo high-rises.
            Tower(pen, X(0.55f), 0.07f, 0.32f, 5);
            Tower(pen, X(0.6f), 0.06f, 0.42f, 6);
            Tower(pen, X(0.69f), 0.08f, 0.34f, 7);
        }

        void Tower(ArtPen pen, float cx, float w, float h, int salt)
        {
            var x = cx - L(w) * 0.5f;
            pen.Begin();
            pen.AddRoundRect(x, Y(h), L(w), L(h) + 1f, L(0.006f));
            pen.Fill(m_Far);
            Windows(pen, x, Y(h), L(w), L(h), salt, 0.55f, 0.35f);
        }

        // ================================================================== bridges

        /// <summary>The color of the bridges: between the far and the near layer.</summary>
        Color Mid => Color.Lerp(m_Far, m_Near, 0.4f);

        /// <summary>
        /// The Jacques Cartier Bridge (1930): a long deck on piers with a cantilever truss over the main channel,
        /// two humps over the main piers and a lower suspended span between them.
        /// </summary>
        void DrawJacquesCartierBridge(ArtPen pen, float x0, float x1)
        {
            var mid = Mid;
            var deck = 0.17f;
            var thick = L(0.02f);
            var span = x1 - x0;
            var a = x0 + span * 0.2f;   // where the truss rises from the deck
            var b = x0 + span * 0.8f;
            var p1 = x0 + span * 0.36f; // main piers
            var p2 = x0 + span * 0.64f;
            var cxm = (x0 + x1) * 0.5f;

            // Piers.
            pen.Begin();
            for (int i = 0; i <= 8; i++)
            {
                var px = x0 + span * i / 8f;
                var w = (Mathf.Abs(px - p1) < span * 0.07f || Mathf.Abs(px - p2) < span * 0.07f) ? L(0.022f) : L(0.012f);
                pen.AddRoundRect(px - w * 0.5f, Y(deck), w, L(deck) + 1f, 0f);
            }
            pen.AddRoundRect(p1 - L(0.016f), Y(deck), L(0.032f), L(deck) + 1f, 0f);
            pen.AddRoundRect(p2 - L(0.016f), Y(deck), L(0.032f), L(deck) + 1f, 0f);
            pen.Fill(mid);

            // The truss outline: top chord with two humps, down to the deck.
            pen.Begin();
            TrussOutline(pen, a, b, p1, p2, cxm, deck);
            pen.Fill(Alpha(mid, 0.35f));

            // Lattice, clipped to the truss.
            pen.Begin();
            TrussOutline(pen, a, b, p1, p2, cxm, deck);
            pen.painter.PushClip();
            pen.Begin();
            var bays = 14;
            for (int i = 0; i < bays; i++)
            {
                var xa = a + (b - a) * i / bays;
                var xb = a + (b - a) * (i + 1) / bays;
                pen.MoveTo(xa, Y(deck));
                pen.LineTo(xa, Y(deck + 0.4f));
                pen.MoveTo(i % 2 == 0 ? xa : xb, Y(deck));
                pen.LineTo(i % 2 == 0 ? xb : xa, Y(deck + 0.4f));
            }
            pen.Stroke(mid, Mathf.Max(1f, L(0.005f)));
            pen.painter.PopClip();

            // Top chord, drawn over the lattice.
            pen.Begin();
            TopChord(pen, a, b, p1, p2, cxm, deck);
            pen.Stroke(mid, Mathf.Max(1.5f, L(0.009f)));

            // Deck, with its lights.
            pen.Begin();
            pen.AddRoundRect(x0, Y(deck) - thick * 0.5f, span, thick, 0f);
            pen.Fill(mid);
            pen.Begin();
            for (int i = 1; i < 18; i++)
                pen.AddCircle(x0 + span * i / 18f, Y(deck) - thick * 0.05f, Mathf.Max(0.8f, L(0.0045f)));
            pen.Fill(Alpha(m_Window, 0.85f));
        }

        void TrussOutline(ArtPen pen, float a, float b, float p1, float p2, float cxm, float deck)
        {
            pen.MoveTo(a, Y(deck));
            TopChordPath(pen, a, b, p1, p2, cxm, deck);
            pen.LineTo(b, Y(deck));
            pen.Close();
        }

        void TopChord(ArtPen pen, float a, float b, float p1, float p2, float cxm, float deck)
        {
            pen.MoveTo(a, Y(deck));
            TopChordPath(pen, a, b, p1, p2, cxm, deck);
        }

        void TopChordPath(ArtPen pen, float a, float b, float p1, float p2, float cxm, float deck)
        {
            var hump = deck + 0.2f;
            var dip = deck + 0.11f;
            pen.QuadTo((a + p1) * 0.5f, Y(hump - 0.01f), p1, Y(hump));
            pen.QuadTo((p1 + cxm) * 0.5f, Y(dip), cxm, Y(dip));
            pen.QuadTo((cxm + p2) * 0.5f, Y(dip), p2, Y(hump));
            pen.QuadTo((p2 + b) * 0.5f, Y(hump - 0.01f), b, Y(deck));
        }

        /// <summary>
        /// The Rainbow Bridge (1993): a suspension bridge with two white towers, its cables and a double deck, seen
        /// across the bay. <paramref name="t2"/> may sit past the right edge.
        /// </summary>
        void DrawRainbowBridge(ArtPen pen, float x0, float t1, float t2)
        {
            var mid = Mid;
            var white = Lighter(m_Far, 0.62f);
            var deck = 0.13f;
            var top = 0.5f;
            var x1 = t2 + (t2 - t1) * 0.5f;

            // Main cable: from tower to tower, dipping almost to the deck; side spans down to the anchorages.
            pen.Begin();
            pen.MoveTo(x0, Y(deck + 0.02f));
            pen.QuadTo((x0 + t1) * 0.5f, Y(deck + 0.12f), t1, Y(top - 0.02f));
            pen.QuadTo((t1 + t2) * 0.5f, Y(deck - 0.12f), t2, Y(top - 0.02f));
            pen.QuadTo((t2 + x1) * 0.5f, Y(deck + 0.12f), x1, Y(deck + 0.02f));
            pen.Stroke(white, Mathf.Max(1.2f, L(0.006f)));

            // Hangers under the main span.
            pen.Begin();
            for (int i = 1; i < 12; i++)
            {
                var t = i / 12f;
                var x = Mathf.Lerp(t1, t2, t);
                // The quadratic cable's height at t (control point at the middle, (deck - 0.12)).
                var u = 1f - t;
                var h = u * u * (top - 0.02f) + 2f * u * t * (deck - 0.12f) + t * t * (top - 0.02f);
                pen.MoveTo(x, Y(h));
                pen.LineTo(x, Y(deck));
            }
            pen.Stroke(Alpha(white, 0.55f), Mathf.Max(0.8f, L(0.0035f)));

            // Double deck.
            pen.Begin();
            pen.AddRoundRect(x0, Y(deck), x1 - x0, L(0.022f), 0f);
            pen.AddRoundRect(x0, Y(deck - 0.045f), x1 - x0, L(0.014f), 0f);
            pen.Fill(mid);
            pen.Begin();
            for (int i = 1; i < 16; i++)
                pen.AddCircle(x0 + (x1 - x0) * i / 16f, Y(deck) + L(0.011f), Mathf.Max(0.8f, L(0.0045f)));
            pen.Fill(Alpha(m_Window, 0.85f));

            // The two white towers.
            RainbowTower(pen, t1, top, deck, white);
            RainbowTower(pen, t2, top, deck, white);
        }

        /// <summary>A Rainbow Bridge tower: two legs, three cross-beams, warning lights on top.</summary>
        void RainbowTower(ArtPen pen, float t, float top, float deck, Color white)
        {
            var leg = L(0.012f);
            var gap = L(0.022f);
            pen.Begin();
            pen.AddRoundRect(t - gap - leg, Y(top), leg, L(top) + 1f, 0f);
            pen.AddRoundRect(t + gap, Y(top), leg, L(top) + 1f, 0f);
            pen.AddRoundRect(t - gap - leg, Y(top - 0.015f), (gap + leg) * 2f, L(0.016f), 0f);
            pen.AddRoundRect(t - gap - leg, Y(top - 0.16f), (gap + leg) * 2f, L(0.016f), 0f);
            pen.AddRoundRect(t - gap - leg, Y(deck + 0.06f), (gap + leg) * 2f, L(0.016f), 0f);
            pen.Fill(white);
            pen.Begin();
            pen.AddCircle(t - gap - leg * 0.5f, Y(top) - L(0.006f), Mathf.Max(0.9f, L(0.005f)));
            pen.AddCircle(t + gap + leg * 0.5f, Y(top) - L(0.006f), Mathf.Max(0.9f, L(0.005f)));
            pen.Fill(Rgb(255, 90, 80));
        }

        // ================================================================== near

        void DrawBiosphere(ArtPen pen, float cx)
        {
            var r = L(0.15f);
            var cy = Y(0.14f);

            pen.Begin();
            pen.AddCircle(cx, cy, r);
            pen.Fill(m_Near);

            // Geodesic lattice, clipped to the sphere.
            pen.Begin();
            pen.AddCircle(cx, cy, r - 1f);
            pen.painter.PushClip();
            pen.Begin();
            var step = r * 0.36f;
            for (int i = -4; i <= 4; i++)
            {
                var y = cy + i * step * 0.866f;
                pen.MoveTo(cx - r, y);
                pen.LineTo(cx + r, y);
                pen.MoveTo(cx + i * step - r, cy - r * 1.2f);
                pen.LineTo(cx + i * step + r * 0.7f, cy + r * 1.2f);
                pen.MoveTo(cx + i * step + r, cy - r * 1.2f);
                pen.LineTo(cx + i * step - r * 0.7f, cy + r * 1.2f);
            }
            pen.Stroke(Alpha(m_Window, 0.32f), Mathf.Max(1f, L(0.005f)));
            pen.painter.PopClip();

            // Rim light.
            pen.Begin();
            pen.AddArc(cx, cy, r - L(0.012f), r - L(0.012f), 200f, 260f);
            pen.Stroke(Alpha(m_Window, 0.5f), L(0.008f));
        }

        void DrawPlateau(ArtPen pen, float x)
        {
            float[] heights = { 0.22f, 0.26f, 0.23f };
            var w = L(0.08f);
            for (int i = 0; i < heights.Length; i++)
            {
                var hx = x + i * (w + L(0.004f));
                var h = heights[i];
                pen.Begin();
                pen.AddRoundRect(hx, Y(h), w, L(h) + 1f, 0f);
                // Cornice.
                pen.AddRoundRect(hx - L(0.006f), Y(h) - L(0.012f), w + L(0.012f), L(0.016f), L(0.004f));
                pen.Fill(m_Near);

                // Two windows per floor, three floors.
                pen.Begin();
                for (int floor = 0; floor < 3; floor++)
                {
                    for (int col = 0; col < 2; col++)
                    {
                        if (Hash(i * 7 + floor * 3 + col, 29) < 0.35f)
                            continue;
                        var wx = hx + w * (col == 0 ? 0.2f : 0.6f);
                        var wy = Y(h) + L(0.025f) + floor * L(h - 0.04f) / 3f;
                        pen.AddRoundRect(wx, wy, w * 0.2f, L(0.03f), L(0.004f));
                    }
                }
                pen.Fill(m_Window);

                // Balcony and the famous outdoor staircase curling down to the sidewalk.
                if (i % 2 == 0)
                {
                    var balcony = Y(h * 0.5f);
                    pen.Begin();
                    pen.AddRoundRect(hx - L(0.004f), balcony, w + L(0.008f), L(0.008f), 0f);
                    pen.Fill(Lighter(m_Near, 0.35f));

                    var sx = hx + w * 0.92f;
                    pen.Begin();
                    pen.MoveTo(sx, balcony);
                    pen.CubicTo(sx + w * 0.42f, balcony + L(0.02f), sx + w * 0.4f, m_Base - L(0.05f), sx + w * 0.02f, m_Base - L(0.025f));
                    pen.Stroke(Lighter(m_Near, 0.35f), Mathf.Max(1f, L(0.007f)));

                    // Steps as rungs along the curve.
                    pen.Begin();
                    for (int k = 1; k < 6; k++)
                    {
                        var t = k / 6f;
                        var u = 1f - t;
                        var px = u * u * u * sx + 3f * u * u * t * (sx + w * 0.42f) + 3f * u * t * t * (sx + w * 0.4f) + t * t * t * (sx + w * 0.02f);
                        var py = u * u * u * balcony + 3f * u * u * t * (balcony + L(0.02f)) + 3f * u * t * t * (m_Base - L(0.05f)) + t * t * t * (m_Base - L(0.025f));
                        pen.MoveTo(px - L(0.014f), py);
                        pen.LineTo(px + L(0.004f), py);
                    }
                    pen.Stroke(Lighter(m_Near, 0.35f), Mathf.Max(1f, L(0.005f)));
                }
            }
        }

        void DrawJapaneseHouses(ArtPen pen, float x, int count, int salt)
        {
            for (int i = 0; i < count; i++)
            {
                var w = L(0.075f + Hash(i, salt) * 0.03f);
                var h = 0.07f + Hash(i, salt + 1) * 0.04f;
                var hx = x + i * (w + L(0.012f));
                pen.Begin();
                pen.AddRoundRect(hx, Y(h), w, L(h) + 1f, 0f);
                // Tiled roof with slightly upturned eaves.
                pen.MoveTo(hx - L(0.016f), Y(h) + L(0.006f));
                pen.QuadTo(hx + w * 0.15f, Y(h) - L(0.004f), hx + w * 0.2f, Y(h + 0.04f));
                pen.LineTo(hx + w * 0.8f, Y(h + 0.04f));
                pen.QuadTo(hx + w * 0.85f, Y(h) - L(0.004f), hx + w + L(0.016f), Y(h) + L(0.006f));
                pen.Close();
                pen.Fill(m_Near);

                // Warm shoji window.
                pen.Begin();
                pen.AddRoundRect(hx + w * 0.25f, Y(h) + L(0.022f), w * 0.5f, L(0.032f), L(0.003f));
                pen.Fill(m_Window);
                pen.Begin();
                pen.MoveTo(hx + w * 0.5f, Y(h) + L(0.022f));
                pen.LineTo(hx + w * 0.5f, Y(h) + L(0.054f));
                pen.Stroke(m_Near, Mathf.Max(1f, L(0.004f)));
            }
        }

        void DrawPagoda(ArtPen pen, float cx)
        {
            const int tiers = 5;
            var y = 0f;
            for (int i = 0; i < tiers; i++)
            {
                var t = i / (float)(tiers - 1);
                var half = L(Mathf.Lerp(0.065f, 0.04f, t));
                var bodyH = 0.045f;
                var roofHalf = half + L(0.045f);

                pen.Begin();
                pen.AddRoundRect(cx - half, Y(y + bodyH), half * 2f, L(bodyH) + 1f, 0f);
                // Roof with upturned eaves.
                var ry = Y(y + bodyH);
                pen.MoveTo(cx - roofHalf, ry - L(0.012f));
                pen.QuadTo(cx - half, ry + L(0.002f), cx - half * 0.6f, ry - L(0.03f));
                pen.LineTo(cx + half * 0.6f, ry - L(0.03f));
                pen.QuadTo(cx + half, ry + L(0.002f), cx + roofHalf, ry - L(0.012f));
                pen.LineTo(cx + roofHalf - L(0.01f), ry + L(0.004f));
                pen.LineTo(cx - roofHalf + L(0.01f), ry + L(0.004f));
                pen.Close();
                pen.Fill(m_Near);

                if (i < 3)
                {
                    pen.Begin();
                    pen.AddRoundRect(cx - half * 0.35f, Y(y + bodyH) + L(0.012f), half * 0.7f, L(0.02f), 0f);
                    pen.Fill(Alpha(m_Window, 0.85f));
                }

                y += bodyH + 0.028f;
            }

            // Sōrin spire with rings.
            pen.Begin();
            pen.MoveTo(cx, Y(y));
            pen.LineTo(cx, Y(y + 0.12f));
            pen.Stroke(m_Near, Mathf.Max(1.5f, L(0.008f)));
            pen.Begin();
            for (int i = 0; i < 4; i++)
                pen.AddEllipse(cx, Y(y + 0.025f + i * 0.022f), L(0.012f), L(0.004f));
            pen.Fill(m_Near);
        }

        void DrawTorii(ArtPen pen, float cx)
        {
            var dark = m_Near;
            pen.Begin();
            // Pillars, leaning in slightly.
            pen.MoveTo(cx - L(0.1f), m_Base);
            pen.LineTo(cx - L(0.088f), Y(0.25f));
            pen.LineTo(cx - L(0.066f), Y(0.25f));
            pen.LineTo(cx - L(0.076f), m_Base);
            pen.Close();
            pen.MoveTo(cx + L(0.1f), m_Base);
            pen.LineTo(cx + L(0.088f), Y(0.25f));
            pen.LineTo(cx + L(0.066f), Y(0.25f));
            pen.LineTo(cx + L(0.076f), m_Base);
            pen.Close();
            // Nuki (tie beam) and the center strut.
            pen.AddRoundRect(cx - L(0.12f), Y(0.2f), L(0.24f), L(0.02f), 0f);
            pen.AddRoundRect(cx - L(0.008f), Y(0.25f), L(0.016f), L(0.05f), 0f);
            // Shimaki under the top beam.
            pen.AddRoundRect(cx - L(0.13f), Y(0.255f), L(0.26f), L(0.015f), 0f);
            pen.Fill(m_Accent);

            // Kasagi: the dark top beam with upturned ends.
            pen.Begin();
            pen.MoveTo(cx - L(0.16f), Y(0.29f));
            pen.QuadTo(cx, Y(0.26f), cx + L(0.16f), Y(0.29f));
            pen.LineTo(cx + L(0.15f), Y(0.27f));
            pen.QuadTo(cx, Y(0.245f), cx - L(0.15f), Y(0.27f));
            pen.Close();
            pen.Fill(dark);
        }

        // ================================================================== windows

        void Windows(ArtPen pen, float x, float y, float w, float h, int salt, float density, float alpha)
        {
            var size = L(0.014f);
            if (size < 1.5f)
                return;
            var cols = Mathf.Max(1, Mathf.FloorToInt((w - size) / (size * 2f)));
            var rows = Mathf.Max(1, Mathf.FloorToInt((h - size * 2f) / (size * 2.2f)));
            var ox = x + (w - (cols * size * 2f - size)) * 0.5f;
            pen.Begin();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (Hash(salt * 131 + r * 17 + c, 3) > density)
                        continue;
                    pen.AddRoundRect(ox + c * size * 2f, y + size * 1.5f + r * size * 2.2f, size, size * 1.2f, 0f);
                }
            }
            pen.Fill(Alpha(m_Window, alpha));
        }
    }
}
