using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// Full-size, non-interactive overlay of seasonal particles: tumbling maple leaves, swaying sakura petals, flickering
    /// fireflies or snow. They move on the GPU, through a UI Shader Graph material.
    /// </summary>
    /// <remarks>
    /// <para>Each particle's shape is drawn once with Painter2D into a VectorImage, and the element draws them where the
    /// particles start. A mesh modifier writes each particle's motion into its vertices' extra channels (UV1–UV3: start,
    /// fall, sway, spin, kind and clock), and <c>.be-season-particles--gpu</c> sets <c>-unity-material</c> to
    /// SeasonParticles.mat, whose shader graph moves them in its vertex stage every frame. The CPU only sets the shader
    /// global <c>_BagelTime</c> (<see cref="ArtKit.UpdateShaderTime"/>); it draws again when the style, the size or the
    /// animation's state changes.</para>
    /// <para>The material needs a panel with the extra vertex channels: the game's PanelSettings has them, and the Bagel
    /// windows that animate the art turn them on. Elsewhere (UI Builder) the particles hold still where they start.</para>
    /// <para>Custom USS properties: <c>--be-particle-kind</c> (maple | sakura | firefly | snow),
    /// <c>--be-particle-color</c> (optional tint), <c>--be-particle-count</c> (default 18, max 40),
    /// <c>--be-particle-size</c> (a multiple of the size, default 1), <c>--be-ink-width</c> (a multiple of the outline).
    /// Spawning is seeded and deterministic; particles start again at the top when they leave the element.</para>
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class SeasonParticles : VisualElement
    {
        public static readonly string ussClassName = "be-season-particles";

        /// <summary>On when the panel has <see cref="vertexChannels"/>: the class sets the material that moves the particles.</summary>
        public static readonly UniqueStyleString gpuUssClassName = new(ussClassName + "--gpu");

        /// <summary>The extra vertex channels SeasonParticles.shadergraph reads.</summary>
        public const ExtraVertexChannels vertexChannels =
            ExtraVertexChannels.TexCoord1 | ExtraVertexChannels.TexCoord2 | ExtraVertexChannels.TexCoord3;

        public const int maxCount = 40;
        const int k_DefaultCount = 18;

        static readonly CustomStyleProperty<string> s_KindProperty = new("--be-particle-kind");
        static readonly CustomStyleProperty<Color> s_ColorProperty = new("--be-particle-color");
        static readonly CustomStyleProperty<float> s_CountProperty = new("--be-particle-count");
        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");

        public enum Kind
        {
            Maple,
            Sakura,
            Firefly,
            Snow
        }

        // The kind each particle tells the shader (UV2.w): how it turns, squashes and flickers.
        const float k_ShaderMaple = 1f;
        const float k_ShaderSakura = 2f;
        const float k_ShaderFirefly = 3f;
        const float k_ShaderSnowflake = 4f;
        const float k_ShaderCrystal = 5f;

        struct Particle
        {
            public float x;
            public float y;
            public float size;
            public float speed;
            public float swayAmp;
            public float swayFreq;
            public float phase;
            public float spin;
            public int color;
        }

        static readonly Color[] s_MapleColors =
        {
            Rgb(220, 60, 40), Rgb(240, 112, 40), Rgb(250, 172, 50), Rgb(196, 40, 52), Rgb(234, 86, 34)
        };

        static readonly Color[] s_SakuraColors =
        {
            Rgb(255, 184, 208), Rgb(255, 206, 222), Rgb(250, 160, 192), Rgb(255, 226, 236)
        };

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        readonly Particle[] m_Particles = new Particle[maxCount];

        Kind m_Kind = Kind.Maple;
        Color m_Tint = Color.clear;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        float m_SizeScale = 1f;
        int m_Count = k_DefaultCount;
        int m_Seed = 1;

        // The shapes, one per color or look, drawn at m_ShapeSize and centered in a square of half side m_ShapeExtent.
        VectorImage[] m_Shapes;
        float m_ShapeSize;
        float m_ShapeExtent;

        // Running: the particles' time is _BagelTime - m_Clock. Still: m_Time is their time.
        bool m_Running;
        float m_Clock;
        float m_Time;
        int m_ClockLaps;

        // What the last draw was, for the mesh modifier and the ticks.
        bool m_DrawnWithChannels;
        Vector2 m_DrawnSize;

        /// <summary>The particle kind resolved from <c>--be-particle-kind</c>.</summary>
        public Kind kind => m_Kind;

        /// <summary>Whether the particles are drawn for the material (the panel has <see cref="vertexChannels"/>).</summary>
        public bool movesOnTheGpu => m_DrawnWithChannels;

        /// <summary>Seed for the deterministic spawn pattern.</summary>
        [UxmlAttribute]
        public int seed
        {
            get => m_Seed;
            set
            {
                m_Seed = value;
                Respawn();
                MarkDirtyRepaint();
            }
        }

        public SeasonParticles()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            AddMeshModifier(WriteMotion);
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
            RegisterCallback<AttachToPanelEvent>(_ => EnableInClassList(gpuUssClassName, HasChannels(panel)));
            RegisterCallback<DetachFromPanelEvent>(_ => ReleaseShapes());
            Respawn();
            schedule.Execute(Tick).Every(0);
        }

        static bool HasChannels(IPanel panel) => panel != null && (panel.extraVertexChannels & vertexChannels) == vertexChannels;

        void OnStyleChanged()
        {
            var kind = m_Style.Get(s_KindProperty, "maple") switch
            {
                "sakura" => Kind.Sakura,
                "firefly" => Kind.Firefly,
                "fireflies" => Kind.Firefly,
                "snow" => Kind.Snow,
                _ => Kind.Maple
            };
            var count = Mathf.Clamp(Mathf.RoundToInt(m_Style.Get(s_CountProperty, k_DefaultCount)), 0, maxCount);
            m_Tint = m_Style.Get(s_ColorProperty, Color.clear);
            m_Ink = m_Style.Get(s_InkProperty, Ink);
            m_InkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            m_SizeScale = Mathf.Max(0.05f, m_Style.Get(ArtKit.particleSizeProperty, 1f));

            if (kind != m_Kind || count != m_Count)
            {
                m_Kind = kind;
                m_Count = count;
                Respawn();
            }
            ReleaseShapes();
            MarkDirtyRepaint();
        }

        void Respawn()
        {
            for (int i = 0; i < maxCount; i++)
                Spawn(ref m_Particles[i], i, Range(i, m_Seed * 31 + 5, -0.1f, 1f));
        }

        void Spawn(ref Particle p, int index, float y)
        {
            var key = index * 977 + m_Seed * 7919;
            p.x = Hash(key, 1);
            p.y = y;
            p.size = Range(key, 2, 0.7f, 1.3f);
            p.phase = Range(key, 3, 0f, Mathf.PI * 2f);
            p.color = (int)(Hash(key, 5) * 1000f);

            switch (m_Kind)
            {
                case Kind.Maple:
                    p.speed = Range(key, 7, 0.07f, 0.13f);
                    p.swayAmp = Range(key, 8, 0.02f, 0.05f);
                    p.swayFreq = Range(key, 9, 0.8f, 1.6f);
                    p.spin = Range(key, 10, -90f, 90f);
                    break;
                case Kind.Sakura:
                    p.speed = Range(key, 7, 0.06f, 0.1f);
                    p.swayAmp = Range(key, 8, 0.03f, 0.07f);
                    p.swayFreq = Range(key, 9, 1f, 1.9f);
                    p.spin = Range(key, 10, -40f, 40f);
                    break;
                case Kind.Firefly:
                    p.speed = -Range(key, 7, 0.01f, 0.03f);
                    p.swayAmp = Range(key, 8, 0.02f, 0.05f);
                    p.swayFreq = Range(key, 9, 0.3f, 0.7f);
                    p.spin = Range(key, 10, 1.5f, 3.5f);
                    break;
                default:
                    p.speed = Range(key, 7, 0.04f, 0.08f);
                    p.swayAmp = Range(key, 8, 0.01f, 0.03f);
                    p.swayFreq = Range(key, 9, 0.5f, 1.2f);
                    p.spin = Range(key, 10, -30f, 30f);
                    break;
            }
        }

        // ------------------------------------------------------------------ the clock

        void Tick()
        {
            var panel = this.panel;
            if (panel == null)
                return;

            var channels = HasChannels(panel);
            if (!channels && panel.contextType == ContextType.Editor && IsInEditModeAnimatedSubtree(this))
            {
                // A Bagel window that animates the art in edit mode: give its panel the channels the material reads.
                // That rebuilds the panel's render data once.
                panel.extraVertexChannels |= vertexChannels;
                channels = HasChannels(panel);
            }
            EnableInClassList(gpuUssClassName, channels);
            if (channels != m_DrawnWithChannels)
                MarkDirtyRepaint();

            var size = layout.size;
            var animate = channels && m_Count > 0 && size.x >= 4f && size.y >= 4f && ShouldAnimate(this);
            if (!animate && !m_Running)
                return;

            var now = UpdateShaderTime();
            if (animate != m_Running)
            {
                // Hold still where they are, or move on from there.
                if (animate)
                    m_Clock = now - m_Time;
                else
                    m_Time = now - m_Clock;
                m_Running = animate;
                m_ClockLaps = shaderTimeLaps;
                MarkDirtyRepaint();
            }
            else if (m_ClockLaps != shaderTimeLaps)
            {
                // _BagelTime started again from 0: keep the particles' time.
                m_Clock -= (shaderTimeLaps - m_ClockLaps) * shaderTimeLap;
                m_ClockLaps = shaderTimeLaps;
                MarkDirtyRepaint();
            }

            // A runtime panel in Play mode draws every frame; an Editor window, or any panel outside Play mode, only when
            // something in it changed. Its root draws next to nothing, so marking it is the cheapest way to have it draw
            // the next frame.
            if (m_Running && (panel.contextType == ContextType.Editor || !Application.isPlaying))
                panel.visualTree.MarkDirtyRepaint();
        }

        // ------------------------------------------------------------------ drawing

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var size = layout.size;
            m_DrawnWithChannels = HasChannels(panel);
            m_DrawnSize = size;
            if (m_Count == 0 || !(size.x >= 4f) || !(size.y >= 4f))
                return;

            var shapeSize = Mathf.Clamp(Mathf.Sqrt(size.x * size.y) * 0.045f, 7f, 20f) * m_SizeScale;
            if (m_Shapes == null || !Mathf.Approximately(shapeSize, m_ShapeSize))
                BuildShapes(shapeSize);

            for (int i = 0; i < m_Count; i++)
            {
                ref var p = ref m_Particles[i];
                var shape = m_Shapes[ShapeOf(ref p)];
                var center = new Vector2(p.x * size.x, p.y * size.y);
                var corner = new Vector2(-m_ShapeExtent, -m_ShapeExtent) * p.size;
                if (m_DrawnWithChannels)
                {
                    // Upright where it starts; the shader turns and moves it. userData tells the mesh modifier which one it is.
                    mgc.DrawVectorImage(shape, center + corner, Angle.Degrees(0f), Vector2.one * p.size, i + 1);
                }
                else
                {
                    var rotation = p.phase * Mathf.Rad2Deg;
                    var turned = (Vector2)(Quaternion.Euler(0f, 0f, rotation) * corner);
                    mgc.DrawVectorImage(shape, center + turned, Angle.Degrees(rotation), Vector2.one * p.size);
                }
            }
        }

        // The mesh modifier: every vertex of particle i gets particle i's motion (SeasonParticles.hlsl reads it).
        void WriteMotion(MeshModificationContext context)
        {
            if (!m_DrawnWithChannels)
                return;

            var heightScale = Mathf.Clamp(400f / m_DrawnSize.y, 0.5f, 2f);
            var clock = new Vector4(m_DrawnSize.x, m_DrawnSize.y, m_Running ? m_Clock : m_Time, m_Running ? 0f : 1f);
            foreach (var draw in context.draws)
            {
                var index = draw.userData - 1;
                if (index < 0 || index >= m_Count)
                    continue;

                ref var p = ref m_Particles[index];
                // Fall in heights a second: taller elements take a bit longer, so the speed on screen stays about the same.
                var start = new Vector4(p.x, p.y, p.speed * heightScale, p.swayAmp);
                var motion = new Vector4(p.swayFreq, p.phase, p.spin, ShaderKind(ref p));
                var uv1 = draw.GetUv1(allocate: true);
                var uv2 = draw.GetUv2(allocate: true);
                var uv3 = draw.GetUv3(allocate: true);
                for (int v = 0; v < uv1.Length; v++)
                {
                    uv1[v] = start;
                    uv2[v] = motion;
                    uv3[v] = clock;
                }
            }
        }

        bool IsCrystal(ref Particle p) => p.color % 3 == 0;

        int ShapeOf(ref Particle p) => m_Kind switch
        {
            Kind.Maple => p.color % s_MapleColors.Length,
            Kind.Sakura => p.color % s_SakuraColors.Length,
            Kind.Snow => IsCrystal(ref p) ? 1 : 0,
            _ => 0
        };

        float ShaderKind(ref Particle p) => m_Kind switch
        {
            Kind.Maple => k_ShaderMaple,
            Kind.Sakura => k_ShaderSakura,
            Kind.Firefly => k_ShaderFirefly,
            _ => IsCrystal(ref p) ? k_ShaderCrystal : k_ShaderSnowflake
        };

        // ------------------------------------------------------------------ shapes

        void BuildShapes(float size)
        {
            ReleaseShapes();
            m_ShapeSize = size;
            // Room for the largest shape and its outline, the firefly's glow.
            m_ShapeExtent = size * 1.6f + 2f;
            var count = m_Kind switch
            {
                Kind.Maple => s_MapleColors.Length,
                Kind.Sakura => s_SakuraColors.Length,
                Kind.Snow => 2,
                _ => 1
            };
            m_Shapes = new VectorImage[count];
            using var painter = new Painter2D();
            for (int i = 0; i < count; i++)
            {
                painter.Clear();
                var pen = m_Pen;
                pen.Reset(painter, Vector2.zero, 1f, Alpha(m_Ink, 0.55f), 0f, 1f);
                pen.ink = Mathf.Max(1f, size * 0.12f) * m_InkWidth;

                // A clear square around the shape: the VectorImage's bounds, so its center is the particle's.
                var e = m_ShapeExtent;
                pen.Begin();
                pen.MoveTo(-e, -e);
                pen.LineTo(e, -e);
                pen.LineTo(e, e);
                pen.LineTo(-e, e);
                pen.Close();
                pen.Fill(Color.clear);

                switch (m_Kind)
                {
                    case Kind.Maple: DrawLeaf(pen, size, i); break;
                    case Kind.Sakura: DrawPetal(pen, size, i); break;
                    case Kind.Firefly: DrawFirefly(pen, size); break;
                    default: DrawSnow(pen, size, crystal: i == 1); break;
                }

                var shape = ScriptableObject.CreateInstance<VectorImage>();
                shape.hideFlags = HideFlags.HideAndDontSave;
                painter.SaveToVectorImage(shape);
                m_Shapes[i] = shape;
            }
        }

        void ReleaseShapes()
        {
            if (m_Shapes == null)
                return;
            foreach (var shape in m_Shapes)
            {
                if (Application.isPlaying)
                    Object.Destroy(shape);
                else
                    Object.DestroyImmediate(shape);
            }
            m_Shapes = null;
        }

        Color Tinted(Color c) => m_Tint.a > 0f ? Color.Lerp(c, m_Tint, 0.65f * m_Tint.a) : c;

        void DrawLeaf(ArtPen pen, float size, int color)
        {
            MapleLeaf(pen, 0f, 0f, size, 0f, Tinted(s_MapleColors[color]), 0.6f, 1f, false);
        }

        void DrawPetal(ArtPen pen, float size, int color)
        {
            pen.Push(0f, 0f, size * 0.9f);
            pen.Begin();
            pen.Push(0f, 0.5f);
            PetalPath(pen);
            pen.Pop();
            pen.Outline(0.45f);
            pen.Fill(Tinted(s_SakuraColors[color]));
            pen.Begin();
            pen.MoveTo(0f, 0.42f);
            pen.LineTo(0f, 0.1f);
            pen.Stroke(Alpha(SakuraDeep, 0.5f), 0.08f);
            pen.Pop();
        }

        void DrawFirefly(ArtPen pen, float size)
        {
            // Fully lit: the shader fades the whole firefly as it flickers.
            Glow(pen, 0f, 0f, size * 1.5f, Alpha(Tinted(Rgb(214, 255, 110)), 0.9f), 4);
            pen.Begin();
            pen.AddCircle(0f, 0f, size * 0.26f);
            pen.Fill(Rgb(255, 255, 214));
        }

        void DrawSnow(ArtPen pen, float size, bool crystal)
        {
            var white = Tinted(White);
            var edge = Rgb(150, 186, 230, 0.85f);
            if (!crystal)
            {
                // Soft round flake.
                pen.Begin();
                pen.AddCircle(0f, 0f, size * 0.32f);
                pen.Fill(white);
                pen.Stroke(edge, Mathf.Max(1f, size * 0.07f));
                return;
            }

            // Six-armed crystal.
            pen.Push(0f, 0f, size * 0.75f);
            pen.Begin();
            for (int arm = 0; arm < 6; arm++)
            {
                var a = arm * 60f * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var side = new Vector2(-dir.y, dir.x);
                pen.MoveTo(0f, 0f);
                pen.LineTo(dir.x, dir.y);
                var b = dir * 0.55f;
                pen.MoveTo(b.x, b.y);
                pen.LineTo(b.x + (dir.x + side.x) * 0.28f, b.y + (dir.y + side.y) * 0.28f);
                pen.MoveTo(b.x, b.y);
                pen.LineTo(b.x + (dir.x - side.x) * 0.28f, b.y + (dir.y - side.y) * 0.28f);
            }
            pen.Stroke(edge, 0.34f);
            pen.Stroke(white, 0.17f);
            pen.Pop();
        }
    }
}
