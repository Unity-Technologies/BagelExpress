using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Begu-chan's speech bubble: a box styled in USS (background, border, radii, lip) with a tail on its left,
    /// pointing at the mascot, drawn in the bubble's own colors and border widths.
    /// </summary>
    /// <remarks>
    /// The tail used to be a rotated square with two borders. On a box this round, whose straight left edge is only a
    /// few points tall, the square's ends landed on the corners and left notches in the outline. Here the bubble draws
    /// its whole left end with Painter2D, over what USS drew: the outline of the box's end and of the tail as one ink
    /// shape, then their inside in the background color. Box and tail meet in one outline, whatever the corners'
    /// radii and border widths (the bottom border is thicker: the lip). Past the corners, where the box's edges are
    /// straight, USS's border takes over exactly where this one ends. It reads the resolved styles when it draws, so a
    /// USS change to the bubble (its background color, say) recolors the tail too.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Home")]
    public partial class SpeechBubble : VisualElement
    {
        public static readonly string ussClassName = "be-bubble";

        const float k_DefaultTailLength = 12f;
        const float k_DefaultTailWidth = 18f;

        float m_TailLength = k_DefaultTailLength;
        float m_TailWidth = k_DefaultTailWidth;

        /// <summary>How far the tail's tip is left of the box, in points.</summary>
        [UxmlAttribute]
        public float tailLength
        {
            get => m_TailLength;
            set
            {
                m_TailLength = Mathf.Max(0f, value);
                MarkDirtyRepaint();
            }
        }

        /// <summary>How wide the tail is where it meets the box's left edge, in points.</summary>
        [UxmlAttribute]
        public float tailWidth
        {
            get => m_TailWidth;
            set
            {
                m_TailWidth = Mathf.Max(0f, value);
                MarkDirtyRepaint();
            }
        }

        public SpeechBubble()
        {
            AddToClassList(ussClassName);
            generateVisualContent += DrawLeftEnd;
        }

        /// <summary>A rect with rounded left corners (elliptical radii); its right side is left square.</summary>
        readonly struct LeftEnd
        {
            public readonly Rect rect;
            public readonly Vector2 top;
            public readonly Vector2 bottom;

            public LeftEnd(Rect rect, Vector2 top, Vector2 bottom)
            {
                this.rect = rect;
                this.top = top;
                this.bottom = bottom;
            }

            public bool Contains(Vector2 p)
            {
                if (!rect.Contains(p))
                    return false;
                if (p.x < rect.xMin + top.x && p.y < rect.yMin + top.y)
                    return InEllipse(p, new Vector2(rect.xMin + top.x, rect.yMin + top.y), top);
                if (p.x < rect.xMin + bottom.x && p.y > rect.yMax - bottom.y)
                    return InEllipse(p, new Vector2(rect.xMin + bottom.x, rect.yMax - bottom.y), bottom);
                return true;
            }

            /// <summary>The shape's outline up to <paramref name="cut"/>, closed along it.</summary>
            public void Path(Painter2D painter, float cut)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(cut, rect.yMin));
                painter.LineTo(new Vector2(rect.xMin + top.x, rect.yMin));
                QuarterTo(painter, new Vector2(rect.xMin, rect.yMin + top.y), top, topLeft: true);
                painter.LineTo(new Vector2(rect.xMin, rect.yMax - bottom.y));
                QuarterTo(painter, new Vector2(rect.xMin + bottom.x, rect.yMax), bottom, topLeft: false);
                painter.LineTo(new Vector2(cut, rect.yMax));
                painter.ClosePath();
            }

            static bool InEllipse(Vector2 p, Vector2 center, Vector2 radius)
            {
                if (radius.x <= 0f || radius.y <= 0f)
                    return true;
                var d = new Vector2((p.x - center.x) / radius.x, (p.y - center.y) / radius.y);
                return d.sqrMagnitude <= 1f;
            }
        }

        void DrawLeftEnd(MeshGenerationContext mgc)
        {
            var size = layout.size;
            if (m_TailLength <= 0f || m_TailWidth <= 0f || !(size.x >= 1f) || !(size.y >= 1f))
                return;

            var style = resolvedStyle;
            var fill = style.backgroundColor;
            var ink = style.borderLeftColor;
            float left = style.borderLeftWidth, top = style.borderTopWidth, bottom = style.borderBottomWidth;

            // The box's outer edge (corner radii as USS draws them: shrunk to fit the height) and its border's inner edge.
            var topRadius = style.borderTopLeftRadius;
            var bottomRadius = style.borderBottomLeftRadius;
            if (topRadius + bottomRadius > size.y)
            {
                var scale = size.y / (topRadius + bottomRadius);
                topRadius *= scale;
                bottomRadius *= scale;
            }
            var outer = new LeftEnd(new Rect(0f, 0f, size.x, size.y), Vector2.one * topRadius, Vector2.one * bottomRadius);
            var inner = new LeftEnd(new Rect(left, top, size.x - left - style.borderRightWidth, size.y - top - bottom),
                new Vector2(Mathf.Max(0f, topRadius - left), Mathf.Max(0f, topRadius - top)),
                new Vector2(Mathf.Max(0f, bottomRadius - left), Mathf.Max(0f, bottomRadius - bottom)));

            // Where this drawing hands over to USS's border: past both corners, on the straight top and bottom edges.
            var cut = Mathf.Max(topRadius, bottomRadius) + 2f;
            if (cut >= size.x - Mathf.Max(style.borderTopRightRadius, style.borderBottomRightRadius) || inner.rect.height <= 0f)
                return;

            // The tail: its outer edges from the tip, and its inner edges a border width inside them.
            var middle = inner.rect.center.y;
            var tip = new Vector2(-m_TailLength, middle);
            var halfAngle = Mathf.Atan2(m_TailWidth * 0.5f, m_TailLength);
            var innerTip = tip + new Vector2(left / Mathf.Sin(halfAngle), 0f);
            var slope = Mathf.Tan(halfAngle);
            Vector2 Along(Vector2 from, float x, float sign) => new(x, from.y + sign * slope * (x - from.x));
            var inside = new Vector2(cut, middle);

            var painter = mgc.painter2D;

            // Ink: the box's end and the tail, as far as each reaches the box's outer edge.
            painter.fillColor = ink;
            outer.Path(painter, cut);
            painter.Fill();
            painter.BeginPath();
            painter.MoveTo(tip);
            painter.LineTo(FirstInside(tip, Along(tip, cut, -1f), outer));
            painter.LineTo(inside);
            painter.LineTo(FirstInside(tip, Along(tip, cut, 1f), outer));
            painter.ClosePath();
            painter.Fill();

            // Inside: the box's end and the tail's inside, up to the border's inner edge. It reaches 2 points past the
            // ink so that the ink's anti-aliased right edge, inside the box, is covered.
            painter.fillColor = fill;
            inner.Path(painter, cut + 2f);
            painter.Fill();
            painter.BeginPath();
            painter.MoveTo(innerTip);
            painter.LineTo(FirstInside(innerTip, Along(innerTip, cut, -1f), inner));
            painter.LineTo(inside);
            painter.LineTo(FirstInside(innerTip, Along(innerTip, cut, 1f), inner));
            painter.ClosePath();
            painter.Fill();
        }

        /// <summary>
        /// Where the segment from <paramref name="outside"/> to <paramref name="end"/> first enters the shape: found by
        /// stepping along it (it may leave the shape again, through its top or bottom), then bisecting to well under a
        /// pixel. <paramref name="end"/> if it never enters.
        /// </summary>
        static Vector2 FirstInside(Vector2 outside, Vector2 end, LeftEnd shape)
        {
            const int steps = 64;
            for (int i = 1; i <= steps; i++)
            {
                float to = i / (float)steps;
                if (!shape.Contains(Vector2.Lerp(outside, end, to)))
                    continue;
                float from = (i - 1) / (float)steps;
                for (int j = 0; j < 16; j++)
                {
                    var t = (from + to) * 0.5f;
                    if (shape.Contains(Vector2.Lerp(outside, end, t)))
                        to = t;
                    else
                        from = t;
                }
                return Vector2.Lerp(outside, end, to);
            }
            return end;
        }

        // A cubic Bézier's control distance for a quarter ellipse, as a fraction of the radius.
        const float k_Kappa = 0.5523f;

        /// <summary>
        /// A quarter ellipse from the painter's current point to <paramref name="to"/>, counter-clockwise on screen:
        /// the top-left corner (leftward, then down) or the bottom-left one (down, then rightward).
        /// </summary>
        static void QuarterTo(Painter2D painter, Vector2 to, Vector2 radius, bool topLeft)
        {
            if (radius.x <= 0f || radius.y <= 0f)
            {
                painter.LineTo(to);
                return;
            }
            Vector2 c1, c2;
            if (topLeft)
            {
                var from = new Vector2(to.x + radius.x, to.y - radius.y);
                c1 = new Vector2(from.x - radius.x * k_Kappa, from.y);
                c2 = new Vector2(to.x, to.y - radius.y * k_Kappa);
            }
            else
            {
                var from = new Vector2(to.x - radius.x, to.y - radius.y);
                c1 = new Vector2(from.x, from.y + radius.y * k_Kappa);
                c2 = new Vector2(to.x - radius.x * k_Kappa, to.y);
            }
            painter.BezierCurveTo(c1, c2, to);
        }
    }
}
