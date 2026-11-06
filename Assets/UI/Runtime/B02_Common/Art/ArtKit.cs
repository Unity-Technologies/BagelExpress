using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>Expressions shared by the pastry faces and Begu-chan.</summary>
    public enum FaceStyle
    {
        Happy,
        Excited,
        Sleepy,
        Blink
    }

    /// <summary>
    /// Palette and reusable motifs (faces, sakura, maple leaves, sparkles) for the Painter2D art.
    /// Every motif is drawn in the pen's current local frame, so callers position it with
    /// <see cref="ArtPen.Push"/> or the motif's own position parameters.
    /// </summary>
    public static class ArtKit
    {
        // ------------------------------------------------------------------ palette

        public static Color Rgb(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

        public static readonly Color Ink = Rgb(59, 31, 20);
        public static readonly Color White = Color.white;
        public static readonly Color CrustTop = Rgb(214, 140, 69);
        public static readonly Color CrustSide = Rgb(178, 98, 42);
        public static readonly Color CrustLight = Rgb(240, 182, 108);
        public static readonly Color Dough = Rgb(255, 236, 200);
        public static readonly Color Sesame = Rgb(255, 246, 220);
        public static readonly Color Poppy = Rgb(40, 30, 40);
        public static readonly Color Matcha = Rgb(122, 170, 74);
        public static readonly Color SakuraPink = Rgb(255, 170, 200);
        public static readonly Color SakuraPetal = Rgb(255, 196, 214);
        public static readonly Color SakuraDeep = Rgb(236, 104, 148);
        public static readonly Color Anko = Rgb(110, 40, 45);
        public static readonly Color MelonCrust = Rgb(255, 214, 120);
        public static readonly Color MapleRed = Rgb(220, 60, 40);
        public static readonly Color MapleSyrup = Rgb(208, 116, 30);
        public static readonly Color Gold = Rgb(255, 200, 40);
        public static readonly Color GoldLight = Rgb(255, 236, 150);
        public static readonly Color LeafGreen = Rgb(118, 160, 64);
        public static readonly Color Blush = Rgb(255, 120, 150, 0.62f);
        public static readonly Color MouthDark = Rgb(120, 36, 40);
        public static readonly Color Tongue = Rgb(255, 120, 140);

        public static Color Alpha(Color c, float a) => new(c.r, c.g, c.b, a);
        public static Color Lighter(Color c, float t) => Alpha(Color.Lerp(c, Color.white, t), c.a);
        public static Color Darker(Color c, float t) => Alpha(Color.Lerp(c, Color.black, t), c.a);

        /// <summary>
        /// Opt-in for ambient animations (falling particles, blinking, breathing) outside Play mode. Off by default:
        /// a continuously animating element forces its whole panel to repaint every frame, and in edit mode that means
        /// editor windows full of filters (or an edit-mode PanelRenderer's Scene/Game views) redraw nonstop and starve
        /// the editor. One-shot effects (ConfettiBurst, Spotlight transitions) always animate.
        /// </summary>
        public static bool animateInEditMode { get; set; }

        /// <summary>
        /// Turns ambient animations on in edit mode for everything under the element that has it, whatever
        /// <see cref="animateInEditMode"/> says: the Art Sampler window, a page of art meant to be watched.
        /// </summary>
        public static readonly UniqueStyleString animateInEditModeUssClassName = new("be-animate-in-edit-mode");

        static readonly CustomStyleProperty<string> s_MotionProperty = new("--be-motion");

        /// <summary>
        /// <c>--be-ink-width</c>: a multiple of the ink outlines' width (default 1), read with <c>--be-ink</c> by every art
        /// element that has an outline. The game doesn't set it; the Bagel Features window's Art mode does.
        /// </summary>
        public static readonly CustomStyleProperty<float> inkWidthProperty = new("--be-ink-width");

        /// <summary>
        /// <c>--be-particle-size</c>: a multiple of the size of SeasonParticles' and ConfettiBurst's pieces (default 1). The
        /// game doesn't set it; the Bagel Features window's Art mode does.
        /// </summary>
        public static readonly CustomStyleProperty<float> particleSizeProperty = new("--be-particle-size");

        /// <summary>
        /// False when <c>--be-motion: none</c> is set on the element or an ancestor: Settings › Performance › Disable all
        /// animations adds a style sheet that sets it on the app root, and the code-drawn art holds still. (Custom
        /// properties aren't inherited, so this walks up.)
        /// </summary>
        public static bool MotionAllowed(VisualElement element)
        {
            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                if (e.customStyle.TryGetValue(s_MotionProperty, out var motion))
                    return motion != "none";
            }
            return true;
        }

        static readonly HashSet<VisualElement> s_StillRoots = new();

        /// <summary>
        /// Holds the code-drawn art under <paramref name="root"/> still (or lets it move again) without touching its
        /// styles, which would restyle everything under it: for UI that's there but out of view, like the Bagel UI
        /// window's phones. Let go of a root before dropping it.
        /// </summary>
        public static void HoldStill(VisualElement root, bool still)
        {
            if (still)
                s_StillRoots.Add(root);
            else
                s_StillRoots.Remove(root);
        }

        public static bool IsHeldStill(VisualElement element)
        {
            if (s_StillRoots.Count == 0)
                return false;
            for (var e = element; e != null; e = e.hierarchy.parent)
                if (s_StillRoots.Contains(e))
                    return true;
            return false;
        }

        /// <summary>Whether an ambient animation should tick for this element right now.</summary>
        public static bool ShouldAnimate(VisualElement element)
        {
            if (element.panel == null || !MotionAllowed(element) || IsHeldStill(element))
                return false;
            if (!Application.isPlaying && !animateInEditMode && !IsInEditModeAnimatedSubtree(element))
                return false;
            var style = element.resolvedStyle;
            return style.display != DisplayStyle.None && style.visibility != Visibility.Hidden && style.opacity > 0f;
        }

        internal static bool IsInEditModeAnimatedSubtree(VisualElement element)
        {
            for (var ancestor = element; ancestor != null; ancestor = ancestor.hierarchy.parent)
            {
                if (ancestor.ClassListContains(animateInEditModeUssClassName))
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ shader time

        /// <summary>
        /// <c>_BagelTime</c>: the shader global, in seconds, that the UI Shader Graph materials animate with
        /// (SeasonParticles.shadergraph). <see cref="UpdateShaderTime"/> sets it.
        /// </summary>
        public static readonly int shaderTimeId = Shader.PropertyToID("_BagelTime");

        /// <summary>
        /// <c>_BagelTime</c> starts again from 0 after this many seconds, so a float keeps a millisecond's precision however
        /// long the game or the Editor runs. <see cref="shaderTimeLaps"/> counts the restarts.
        /// </summary>
        public const float shaderTimeLap = 4096f;

        public static int shaderTimeLaps { get; private set; }

        static double s_ShaderTimeStart = double.NaN;

        /// <summary>
        /// Sets <c>_BagelTime</c> to now and returns it. Art that animates through a material calls it from its scheduled
        /// item, every frame it animates: setting a global float is all the CPU does for it.
        /// </summary>
        public static float UpdateShaderTime()
        {
            // A Stopwatch, not Time.realtimeSinceStartup: that one starts again from 0 when the Editor enters Play mode.
            var now = System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
            if (double.IsNaN(s_ShaderTimeStart))
                s_ShaderTimeStart = now;
            while (now - s_ShaderTimeStart >= shaderTimeLap)
            {
                s_ShaderTimeStart += shaderTimeLap;
                shaderTimeLaps++;
            }
            var time = (float)(now - s_ShaderTimeStart);
            Shader.SetGlobalFloat(shaderTimeId, time);
            return time;
        }

        /// <summary>Deterministic hash of an integer into [0, 1).</summary>
        public static float Hash(int n)
        {
            unchecked
            {
                var x = (uint)n * 747796405u + 2891336453u;
                x = ((x >> (int)((x >> 28) + 4u)) ^ x) * 277803737u;
                x = (x >> 22) ^ x;
                return (x & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Hash(int a, int b) => Hash(a * 73856093 ^ b * 19349663);

        /// <summary>Hash into [min, max).</summary>
        public static float Range(int a, int b, float min, float max) => min + Hash(a, b) * (max - min);

        // ------------------------------------------------------------------ faces

        /// <summary>
        /// A yuru-chara face: black oval eyes with catchlights, pink blush and a tiny mouth.
        /// Drawn around (cx, cy); at scale 1 the eyes are ±<paramref name="eyeSpacing"/> apart and the face is ~1.1 wide.
        /// </summary>
        public static void Face(ArtPen pen, float cx, float cy, float scale, FaceStyle style = FaceStyle.Happy,
            float eyeSpacing = 0.3f, float eyeSize = 1f, bool blush = true)
        {
            pen.Push(cx, cy, scale);

            if (blush)
            {
                pen.Begin();
                pen.AddEllipse(-eyeSpacing - 0.18f, 0.15f, 0.12f, 0.07f);
                pen.AddEllipse(eyeSpacing + 0.18f, 0.15f, 0.12f, 0.07f);
                pen.Fill(Blush);
            }

            for (int side = -1; side <= 1; side += 2)
                Eye(pen, side * eyeSpacing, 0f, eyeSize, style, side);

            Mouth(pen, 0f, 0.09f, style);
            pen.Pop();
        }

        static void Eye(ArtPen pen, float x, float y, float size, FaceStyle style, int side)
        {
            var rx = 0.085f * size;
            var ry = 0.12f * size;
            switch (style)
            {
                case FaceStyle.Sleepy:
                case FaceStyle.Blink:
                {
                    // Closed eyes: a gentle sagging arc.
                    var sag = style == FaceStyle.Sleepy ? 0.07f : 0.035f;
                    pen.Begin();
                    pen.MoveTo(x - rx * 1.3f, y + 0.01f);
                    pen.QuadTo(x, y + 0.01f + sag * 2f, x + rx * 1.3f, y + 0.01f);
                    pen.Ink(0.75f);
                    break;
                }
                case FaceStyle.Excited:
                {
                    rx *= 1.18f;
                    ry *= 1.18f;
                    pen.Begin();
                    pen.AddEllipse(x, y, rx, ry);
                    pen.Fill(pen.inkColor);
                    Sparkle(pen, x - rx * 0.25f, y - ry * 0.3f, ry * 0.62f, White);
                    pen.Begin();
                    pen.AddCircle(x + rx * 0.4f, y + ry * 0.45f, ry * 0.16f);
                    pen.Fill(White);
                    break;
                }
                default:
                {
                    pen.Begin();
                    pen.AddEllipse(x, y, rx, ry);
                    pen.Fill(pen.inkColor);
                    pen.Begin();
                    pen.AddEllipse(x - rx * 0.28f, y - ry * 0.36f, rx * 0.42f, ry * 0.34f);
                    pen.Fill(White);
                    pen.Begin();
                    pen.AddCircle(x + rx * 0.35f, y + ry * 0.45f, ry * 0.14f);
                    pen.Fill(Alpha(White, 0.85f));
                    break;
                }
            }
        }

        static void Mouth(ArtPen pen, float x, float y, FaceStyle style)
        {
            switch (style)
            {
                case FaceStyle.Excited:
                {
                    pen.Begin();
                    MouthOpenPath(pen, x, y);
                    pen.Fill(MouthDark);
                    pen.Begin();
                    pen.AddEllipse(x, y + 0.16f, 0.065f, 0.04f);
                    pen.Fill(Tongue);
                    pen.Begin();
                    MouthOpenPath(pen, x, y);
                    pen.Ink(0.6f);
                    break;
                }
                case FaceStyle.Sleepy:
                {
                    pen.Begin();
                    pen.AddEllipse(x, y + 0.04f, 0.035f, 0.045f);
                    pen.Fill(MouthDark);
                    pen.Ink(0.5f);
                    break;
                }
                default:
                {
                    // Cat-like "ω" smile reads as Japanese-cute even at small sizes.
                    pen.Begin();
                    pen.MoveTo(x - 0.11f, y);
                    pen.QuadTo(x - 0.055f, y + 0.09f, x, y + 0.02f);
                    pen.QuadTo(x + 0.055f, y + 0.09f, x + 0.11f, y);
                    pen.Ink(0.7f);
                    break;
                }
            }
        }

        static void MouthOpenPath(ArtPen pen, float x, float y)
        {
            pen.MoveTo(x - 0.12f, y);
            pen.QuadTo(x, y + 0.03f, x + 0.12f, y);
            pen.CubicTo(x + 0.11f, y + 0.15f, x + 0.06f, y + 0.22f, x, y + 0.22f);
            pen.CubicTo(x - 0.06f, y + 0.22f, x - 0.11f, y + 0.15f, x - 0.12f, y);
            pen.Close();
        }

        // ------------------------------------------------------------------ motifs

        /// <summary>A 4-point sparkle with concave sides.</summary>
        public static void Sparkle(ArtPen pen, float cx, float cy, float r, Color color, float inkScale = 0f)
        {
            pen.Begin();
            SparklePath(pen, cx, cy, r);
            if (inkScale > 0f)
                pen.Outline(inkScale);
            pen.Begin();
            SparklePath(pen, cx, cy, r);
            pen.Fill(color);
        }

        public static void SparklePath(ArtPen pen, float cx, float cy, float r)
        {
            var k = r * 0.16f;
            pen.MoveTo(cx, cy - r);
            pen.QuadTo(cx + k, cy - k, cx + r, cy);
            pen.QuadTo(cx + k, cy + k, cx, cy + r);
            pen.QuadTo(cx - k, cy + k, cx - r, cy);
            pen.QuadTo(cx - k, cy - k, cx, cy - r);
            pen.Close();
        }

        /// <summary>One notched sakura petal pointing up (local -y), base at the origin, length 1.</summary>
        public static void PetalPath(ArtPen pen)
        {
            pen.MoveTo(0f, -0.04f);
            pen.CubicTo(-0.44f, -0.2f, -0.56f, -0.74f, -0.26f, -0.98f);
            pen.QuadTo(-0.1f, -1.04f, 0f, -0.82f);
            pen.QuadTo(0.1f, -1.04f, 0.26f, -0.98f);
            pen.CubicTo(0.56f, -0.74f, 0.44f, -0.2f, 0f, -0.04f);
            pen.Close();
        }

        /// <summary>A five-petal sakura blossom with stamens.</summary>
        public static void Sakura(ArtPen pen, float cx, float cy, float radius, float rotationDeg = 0f,
            Color? petal = null, float inkScale = 0.8f)
        {
            var petalColor = petal ?? SakuraPetal;
            pen.Push(cx, cy, radius, rotationDeg);

            pen.Begin();
            for (int i = 0; i < 5; i++)
            {
                pen.Push(0f, 0f, 1f, i * 72f);
                PetalPath(pen);
                pen.Pop();
            }
            pen.Outline(inkScale);
            pen.Fill(petalColor);

            if (pen.details)
            {
                // Deeper pink toward the heart of each petal.
                pen.Begin();
                for (int i = 0; i < 5; i++)
                {
                    pen.Push(0f, 0f, 0.5f, i * 72f);
                    PetalPath(pen);
                    pen.Pop();
                }
                pen.Fill(Alpha(SakuraDeep, 0.45f));

                for (int i = 0; i < 5; i++)
                {
                    var a = (i * 72f + 36f - 90f) * Mathf.Deg2Rad;
                    var tip = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.34f;
                    pen.Begin();
                    pen.MoveTo(0f, 0f);
                    pen.LineTo(tip.x, tip.y);
                    pen.Stroke(SakuraDeep, 0.05f);
                    pen.Begin();
                    pen.AddCircle(tip.x, tip.y, 0.065f);
                    pen.Fill(Rgb(255, 214, 70));
                }

                pen.Begin();
                pen.AddCircle(0f, 0f, 0.12f);
                pen.Fill(Rgb(255, 214, 70));
            }

            pen.Pop();
        }

        /// <summary>A maple leaf (Montréal flag leaf meets momiji). <paramref name="squash"/> fakes a 3D flip.</summary>
        public static void MapleLeaf(ArtPen pen, float cx, float cy, float size, float rotationDeg, Color color,
            float inkScale = 0.8f, float squash = 1f, bool veins = true)
        {
            pen.Push(cx, cy, size, rotationDeg, squash);
            pen.Begin();
            pen.AddPolygon(PainterShapes.MapleLeaf);
            if (inkScale > 0f)
                pen.Outline(inkScale);
            pen.Fill(color);

            if (veins && pen.details && pen.pixelsPerUnit > 10f)
            {
                pen.Begin();
                pen.MoveTo(0f, 0.8f);
                pen.LineTo(0f, -0.62f);
                pen.MoveTo(0f, 0.18f);
                pen.LineTo(0.62f, -0.2f);
                pen.MoveTo(0f, 0.18f);
                pen.LineTo(-0.62f, -0.2f);
                pen.MoveTo(0f, 0.3f);
                pen.LineTo(0.36f, 0.3f);
                pen.MoveTo(0f, 0.3f);
                pen.LineTo(-0.36f, 0.3f);
                pen.Stroke(Lighter(color, 0.45f), 0.07f);
            }
            pen.Pop();
        }

        /// <summary>A soft glow made of stacked translucent circles (no gradients needed).</summary>
        public static void Glow(ArtPen pen, float cx, float cy, float radius, Color color, int rings = 5)
        {
            for (int i = 0; i < rings; i++)
            {
                var t = (i + 1f) / rings;
                pen.Begin();
                pen.AddCircle(cx, cy, radius * (1.05f - t * 0.8f));
                pen.Fill(Alpha(color, color.a * 0.22f));
            }
        }
    }
}
