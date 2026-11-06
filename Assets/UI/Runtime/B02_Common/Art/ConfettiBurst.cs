using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// Celebration confetti: <see cref="Burst"/> throws paper squares, dots, ribbons, little maple leaves and
    /// sakura petals out of the center with velocity, gravity, drag and spin. Animates for ~2.5 s, then idles
    /// (draws nothing and stops ticking). Non-interactive overlay.
    /// </summary>
    /// <remarks>Custom USS properties: <c>--be-ink</c>, <c>--be-ink-width</c>, <c>--be-particle-size</c> (a multiple of the
    /// pieces' size, default 1, applied as they're drawn).</remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class ConfettiBurst : VisualElement
    {
        public static readonly string ussClassName = "be-confetti-burst";

        const int k_Capacity = 240;
        const float k_Lifetime = 2.5f;

        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");

        static readonly Color[] s_Colors =
        {
            Rgb(240, 70, 60), Rgb(255, 200, 40), Rgb(64, 201, 132), Rgb(64, 168, 240),
            Rgb(255, 110, 170), Rgb(168, 104, 232), Rgb(255, 140, 40), Rgb(255, 255, 255)
        };

        enum Shape
        {
            Square,
            Dot,
            Ribbon,
            Leaf,
            Petal
        }

        struct Piece
        {
            public Vector2 position;
            public Vector2 velocity;
            public float rotation;
            public float spin;
            public float flip;
            public float size;
            public float age;
            public float life;
            public Shape shape;
            public Color color;
        }

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        readonly Piece[] m_Pieces = new Piece[k_Capacity];
        readonly IVisualElementScheduledItem m_Ticker;

        int m_Active;
        int m_Next;
        int m_BurstCount;
        float m_Time;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        float m_SizeScale = 1f;

        /// <summary>True while confetti is in flight.</summary>
        public bool isPlaying => m_Active > 0;

        public ConfettiBurst()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, () =>
            {
                m_Ink = m_Style.Get(s_InkProperty, Ink);
                m_InkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
                m_SizeScale = Mathf.Max(0.05f, m_Style.Get(ArtKit.particleSizeProperty, 1f));
            });
            m_Ticker = schedule.Execute(Tick).Every(16);
            m_Ticker.Pause();
        }

        /// <summary>Throw <paramref name="count"/> pieces of confetti from the center.</summary>
        public void Burst(int count = 60)
        {
            var rect = contentRect;
            var extent = (rect.width >= 4f && rect.height >= 4f) ? Mathf.Min(rect.width, rect.height) : 300f;
            extent = Mathf.Clamp(extent, 120f, 900f);
            var size = Mathf.Clamp(extent * 0.035f, 5f, 14f);

            m_BurstCount++;
            count = Mathf.Clamp(count, 0, k_Capacity);
            for (int i = 0; i < count; i++)
            {
                var key = m_BurstCount * 1009 + i;
                ref var p = ref m_Pieces[m_Next];
                m_Next = (m_Next + 1) % k_Capacity;

                // Mostly upward fan, a few shooting sideways and down.
                var angle = Range(key, 1, -170f, -10f) * Mathf.Deg2Rad;
                if (Hash(key, 2) < 0.2f)
                    angle = Range(key, 3, 0f, 360f) * Mathf.Deg2Rad;
                var speed = Range(key, 4, 0.5f, 1.25f) * extent * 1.25f;
                p.position = new Vector2(Range(key, 5, -0.04f, 0.04f), Range(key, 6, -0.04f, 0.04f)) * extent;
                p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                p.rotation = Range(key, 7, 0f, 360f);
                p.spin = Range(key, 8, -540f, 540f);
                p.flip = Range(key, 9, 4f, 11f);
                p.size = size * Range(key, 10, 0.75f, 1.3f);
                p.age = 0f;
                p.life = k_Lifetime * Range(key, 11, 0.75f, 1f);
                p.color = s_Colors[(int)(Hash(key, 12) * s_Colors.Length) % s_Colors.Length];
                var s = Hash(key, 13);
                p.shape = s < 0.3f ? Shape.Square : s < 0.5f ? Shape.Dot : s < 0.7f ? Shape.Ribbon : s < 0.85f ? Shape.Leaf : Shape.Petal;
                if (p.shape == Shape.Leaf)
                    p.color = s_Colors[(int)(Hash(key, 14) * 3f) == 1 ? 6 : 0];
                else if (p.shape == Shape.Petal)
                    p.color = Rgb(255, 184, 210);
            }

            m_Active = Mathf.Min(k_Capacity, m_Active + count);
            m_Ticker.Resume();
            MarkDirtyRepaint();
        }

        /// <summary>Remove all confetti immediately.</summary>
        public void Stop()
        {
            for (int i = 0; i < k_Capacity; i++)
                m_Pieces[i].age = m_Pieces[i].life = 0f;
            m_Active = 0;
            m_Ticker.Pause();
            MarkDirtyRepaint();
        }

        void Tick(TimerState state)
        {
            var dt = Mathf.Min(state.deltaTime, 50) / 1000f;
            m_Time += dt;

            var rect = contentRect;
            var extent = (rect.width >= 4f && rect.height >= 4f) ? Mathf.Clamp(Mathf.Min(rect.width, rect.height), 120f, 900f) : 300f;
            var gravity = extent * 2.2f;
            var drag = Mathf.Exp(-1.8f * dt);

            var alive = 0;
            for (int i = 0; i < k_Capacity; i++)
            {
                ref var p = ref m_Pieces[i];
                if (p.age >= p.life)
                    continue;
                p.age += dt;
                p.velocity *= drag;
                p.velocity.y += gravity * dt * (p.shape == Shape.Petal || p.shape == Shape.Leaf ? 0.45f : 1f);
                p.position += p.velocity * dt;
                p.rotation += p.spin * dt;
                if (p.age < p.life)
                    alive++;
            }

            m_Active = alive;
            if (alive == 0)
                m_Ticker.Pause();
            MarkDirtyRepaint();
        }

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (m_Active == 0)
                return;

            var rect = contentRect;
            if (!(rect.width >= 1f) || !(rect.height >= 1f))
                return;

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, rect.center, 1f, Alpha(m_Ink, 0.75f), 0f, 1f);

            for (int i = 0; i < k_Capacity; i++)
            {
                ref var p = ref m_Pieces[i];
                if (p.age >= p.life)
                    continue;

                var fade = Mathf.Clamp01((p.life - p.age) / 0.5f);
                var flip = Mathf.Cos(m_Time * p.flip + i);
                var squash = Mathf.Sign(flip) * Mathf.Max(0.15f, Mathf.Abs(flip));
                var color = Alpha(p.color, fade);
                var size = p.size * m_SizeScale;
                pen.inkColor = Alpha(m_Ink, 0.75f * fade);
                pen.ink = Mathf.Max(1f, size * 0.12f) * m_InkWidth;

                switch (p.shape)
                {
                    case Shape.Leaf:
                        MapleLeaf(pen, p.position.x, p.position.y, size * 0.8f, p.rotation, color, 0.5f, squash, false);
                        continue;
                    case Shape.Petal:
                        pen.Push(p.position.x, p.position.y, size * 0.8f, p.rotation, squash);
                        pen.Begin();
                        pen.Push(0f, 0.5f);
                        PetalPath(pen);
                        pen.Pop();
                        pen.Outline(0.4f);
                        pen.Fill(color);
                        pen.Pop();
                        continue;
                }

                pen.Push(p.position.x, p.position.y, size * 0.5f, p.rotation, squash);
                pen.Begin();
                switch (p.shape)
                {
                    case Shape.Square: pen.AddRoundRect(-0.8f, -1f, 1.6f, 2f, 0.2f); break;
                    case Shape.Dot: pen.AddCircle(0f, 0f, 0.8f); break;
                    default: pen.AddRoundRect(-0.35f, -1.6f, 0.7f, 3.2f, 0.3f); break;
                }
                pen.Outline(0.4f);
                pen.Fill(color);
                pen.Pop();
            }
        }
    }
}
