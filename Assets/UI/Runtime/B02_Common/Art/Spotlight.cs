using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    public enum SpotlightShape
    {
        RoundedRect,
        Circle
    }

    /// <summary>
    /// Tutorial overlay: dims its whole rect with <c>--be-spotlight-color</c> and punches a hole (odd-even fill)
    /// around the focused area, outlined by a glowing ring (<c>--be-spotlight-ring</c>). The hole glides
    /// between targets. Pointer events inside the hole pass through to the UI underneath; outside it they are blocked.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class Spotlight : VisualElement
    {
        public static readonly string ussClassName = "be-spotlight";
        public static readonly UniqueStyleString focusedUssClassName = new(ussClassName + "--focused");

        static readonly CustomStyleProperty<Color> s_ColorProperty = new("--be-spotlight-color");
        static readonly CustomStyleProperty<Color> s_RingProperty = new("--be-spotlight-ring");

        static readonly Color k_DefaultColor = new(20f / 255f, 10f / 255f, 5f / 255f, 0.72f);
        static readonly Color k_DefaultRing = Rgb(255, 214, 110, 0.95f);

        const float k_AnimSeconds = 0.28f;

        readonly ArtPen m_Pen = new();
        readonly ArtStyleResolver m_Style;
        readonly IVisualElementScheduledItem m_Ticker;

        SpotlightShape m_Shape = SpotlightShape.RoundedRect;
        float m_CornerRadius = 18f;
        bool m_Animate = true;
        Color m_Color = k_DefaultColor;
        Color m_Ring = k_DefaultRing;

        bool m_HasFocus;
        Rect m_Hole;
        Rect m_From;
        Rect m_To;
        float m_T = 1f;
        Rect m_PreviewHole;

        [UxmlAttribute]
        public SpotlightShape shape
        {
            get => m_Shape;
            set
            {
                if (m_Shape == value)
                    return;
                m_Shape = value;
                MarkDirtyRepaint();
            }
        }

        /// <summary>Corner radius of the rounded-rect hole, in pixels.</summary>
        [UxmlAttribute]
        public float cornerRadius
        {
            get => m_CornerRadius;
            set
            {
                m_CornerRadius = Mathf.Max(0f, value);
                MarkDirtyRepaint();
            }
        }

        /// <summary>Glide the hole from the previous focus (or the whole element) to the new one.</summary>
        [UxmlAttribute]
        public bool animate
        {
            get => m_Animate;
            set => m_Animate = value;
        }

        /// <summary>Hole shown in the UI Builder / sampler (local space). Width 0 means none.</summary>
        [UxmlAttribute]
        public Rect previewHole
        {
            get => m_PreviewHole;
            set
            {
                m_PreviewHole = value;
                if (value.width > 0f && value.height > 0f)
                {
                    SetHole(value, false);
                }
                else
                {
                    Clear();
                }
            }
        }

        public bool hasFocus => m_HasFocus;

        /// <summary>The current hole in local space (without animation in progress).</summary>
        public Rect focusRect => m_To;

        public Spotlight()
        {
            AddToClassList(ussClassName);
            generateVisualContent += OnGenerateVisualContent;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);
            m_Ticker = schedule.Execute(Tick).Every(16);
            m_Ticker.Pause();
        }

        void OnStyleChanged()
        {
            var color = m_Style.Get(s_ColorProperty, k_DefaultColor);
            var ring = m_Style.Get(s_RingProperty, k_DefaultRing);
            if (color == m_Color && ring == m_Ring)
                return;
            m_Color = color;
            m_Ring = ring;
            MarkDirtyRepaint();
        }

        /// <summary>Punch the hole around <paramref name="localRect"/> (this element's local space), grown by <paramref name="padding"/>.</summary>
        public void Focus(Rect localRect, float padding = 8f)
        {
            var rect = new Rect(localRect.x - padding, localRect.y - padding, localRect.width + padding * 2f, localRect.height + padding * 2f);
            SetHole(rect, m_Animate && panel != null);
        }

        /// <summary>Punch the hole around another element of the same panel.</summary>
        public void Focus(VisualElement target, float padding = 8f)
        {
            if (target == null)
            {
                Clear();
                return;
            }
            Focus(this.WorldToLocal(target.worldBound), padding);
        }

        /// <summary>
        /// Remove the hole: the whole element is dimmed (hide the element to remove the overlay).
        /// Hides <see cref="VisualElement.Clear"/>; the spotlight has no children.
        /// </summary>
        public new void Clear()
        {
            m_HasFocus = false;
            m_T = 1f;
            m_Ticker.Pause();
            RemoveFromClassList(focusedUssClassName);
            MarkDirtyRepaint();
        }

        void SetHole(Rect rect, bool animated)
        {
            if (animated)
            {
                var bounds = contentRect;
                m_From = m_HasFocus ? m_Hole : (bounds.width >= 1f ? Grow(bounds, 40f) : rect);
                m_To = rect;
                m_T = 0f;
                m_Ticker.Resume();
            }
            else
            {
                m_From = m_To = m_Hole = rect;
                m_T = 1f;
                m_Ticker.Pause();
            }

            m_HasFocus = true;
            AddToClassList(focusedUssClassName);
            MarkDirtyRepaint();
        }

        static Rect Grow(Rect r, float by) => new(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);

        void Tick(TimerState state)
        {
            m_T = Mathf.Min(1f, m_T + state.deltaTime / 1000f / k_AnimSeconds);
            if (m_T >= 1f)
                m_Ticker.Pause();
            MarkDirtyRepaint();
        }

        Rect CurrentHole()
        {
            // Ease-out-back for a little "snap" onto the target.
            var t = m_T;
            const float c1 = 1.4f;
            var e = 1f + (c1 + 1f) * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            var min = Vector2.LerpUnclamped(m_From.min, m_To.min, e);
            var max = Vector2.LerpUnclamped(m_From.max, m_To.max, e);
            return Rect.MinMaxRect(min.x, min.y, Mathf.Max(min.x + 1f, max.x), Mathf.Max(min.y + 1f, max.y));
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            if (!base.ContainsPoint(localPoint))
                return false;
            if (!m_HasFocus)
                return true;

            // Let pointer events through the hole.
            var hole = m_To;
            if (m_Shape == SpotlightShape.Circle)
            {
                var radius = HoleRadius(hole);
                return (localPoint - hole.center).sqrMagnitude > radius * radius;
            }
            return !hole.Contains(localPoint);
        }

        static float HoleRadius(Rect hole) => Mathf.Max(hole.width, hole.height) * 0.5f;

        void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (!(rect.width >= 1f) || !(rect.height >= 1f))
                return;

            var pen = m_Pen;
            pen.Reset(mgc.painter2D, Vector2.zero, 1f, Ink, 0f, 1f);
            var bounds = Grow(rect, 2f);

            pen.Begin();
            pen.AddRoundRect(bounds.x, bounds.y, bounds.width, bounds.height, 0f);
            if (!m_HasFocus)
            {
                pen.Fill(m_Color);
                return;
            }

            m_Hole = m_T >= 1f ? m_To : CurrentHole();
            HolePath(pen, m_Hole, 0f);
            pen.Fill(m_Color, FillRule.OddEven);

            // Glowing ring: soft halo + crisp line.
            pen.Begin();
            HolePath(pen, m_Hole, 4f);
            pen.Stroke(Alpha(m_Ring, m_Ring.a * 0.3f), 10f);
            pen.Begin();
            HolePath(pen, m_Hole, 1.5f);
            pen.Stroke(m_Ring, 3f);
        }

        void HolePath(ArtPen pen, Rect hole, float grow)
        {
            if (m_Shape == SpotlightShape.Circle)
            {
                pen.AddCircle(hole.center.x, hole.center.y, HoleRadius(hole) + grow);
            }
            else
            {
                var r = Grow(hole, grow);
                pen.AddRoundRect(r.x, r.y, r.width, r.height, m_CornerRadius + grow);
            }
        }
    }
}
