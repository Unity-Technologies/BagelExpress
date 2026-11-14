using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// A small drawing context on top of <see cref="Painter2D"/> used by every Bagel art element.
    /// Shapes are authored in a normalized space (the [-1, 1] square, y down) and mapped into pixels.
    /// A local transform stack (offset, scale, rotation, squash) lets the same helper draw a face,
    /// a flower or a leaf anywhere. The pen also knows the ink outline width/color and can flatten
    /// everything to a single silhouette color (used for locked recipes).
    /// One instance is cached per element and <see cref="Reset"/> on every repaint, so drawing allocates nothing.
    /// </summary>
    public sealed class ArtPen
    {
        struct Xf
        {
            public Vector2 origin;
            public Vector2 axisX;
            public Vector2 axisY;
            public float scale;
        }

        const float k_Kappa = 0.5522848f;
        const int k_MaxDepth = 16;

        readonly Xf[] m_Stack = new Xf[k_MaxDepth];
        int m_Depth;
        Xf m_Xf;
        Vector2 m_Current;

        public Painter2D painter { get; private set; }

        /// <summary>Pixels per normalized unit at the root transform.</summary>
        public float unit { get; private set; }

        /// <summary>Ink outline width in pixels.</summary>
        public float ink { get; set; }

        public Color inkColor { get; set; }

        public bool silhouette { get; set; }

        public Color silhouetteColor { get; set; }

        /// <summary>False when drawing a silhouette: interior details (faces, seeds, highlights) are skipped.</summary>
        public bool details => !silhouette;

        /// <summary>Current local scale (pixels per local unit).</summary>
        public float pixelsPerUnit => m_Xf.scale;

        /// <summary>
        /// Prepare the pen for a repaint. The normalized square is centered in <paramref name="rect"/>
        /// and fits its smaller side, so art keeps its aspect.
        /// </summary>
        public void Reset(Painter2D painter, Rect rect, Color inkColor, float inkRatio = 0.12f, float minInk = 1.5f)
        {
            Reset(painter, rect.center, Mathf.Min(rect.width, rect.height) * 0.5f, inkColor, inkRatio, minInk);
        }

        public void Reset(Painter2D painter, Vector2 center, float unit, Color inkColor, float inkRatio = 0.12f, float minInk = 1.5f)
        {
            this.painter = painter;
            this.unit = unit;
            this.inkColor = inkColor;
            ink = Mathf.Max(minInk, unit * inkRatio);
            silhouette = false;
            m_Depth = 0;
            m_Xf = new Xf { origin = center, axisX = new Vector2(unit, 0f), axisY = new Vector2(0f, unit), scale = unit };
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Round;
        }

        // ------------------------------------------------------------------ transforms

        /// <summary>
        /// Push a local frame: local (x, y) is squashed by (sx, sy), rotated by <paramref name="rotationDeg"/>,
        /// scaled by <paramref name="scale"/> and placed at (ox, oy) of the current frame.
        /// </summary>
        public void Push(float ox, float oy, float scale = 1f, float rotationDeg = 0f, float sx = 1f, float sy = 1f)
        {
            m_Stack[m_Depth++] = m_Xf;
            var rad = rotationDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            var parent = m_Xf;
            m_Xf.origin = P(ox, oy);
            m_Xf.axisX = (parent.axisX * cos + parent.axisY * sin) * (scale * sx);
            m_Xf.axisY = (parent.axisX * -sin + parent.axisY * cos) * (scale * sy);
            m_Xf.scale = parent.scale * scale * Mathf.Max(Mathf.Abs(sx), Mathf.Abs(sy));
        }

        public void Pop()
        {
            if (m_Depth > 0)
                m_Xf = m_Stack[--m_Depth];
        }

        /// <summary>Map a local point to pixels.</summary>
        public Vector2 P(float x, float y) => m_Xf.origin + m_Xf.axisX * x + m_Xf.axisY * y;

        /// <summary>Map a local length to pixels.</summary>
        public float S(float v) => v * m_Xf.scale;

        /// <summary>The color to paint with (the silhouette color when drawing a silhouette).</summary>
        public Color C(Color color) => silhouette ? silhouetteColor : color;

        // ------------------------------------------------------------------ path building

        public void Begin() => painter.BeginPath();

        public void MoveTo(float x, float y)
        {
            m_Current = P(x, y);
            painter.MoveTo(m_Current);
        }

        public void LineTo(float x, float y)
        {
            m_Current = P(x, y);
            painter.LineTo(m_Current);
        }

        /// <summary>
        /// Quadratic curve, emitted as the equivalent cubic: a closed sub-path that starts with
        /// Painter2D.QuadraticCurveTo strokes a stray segment from the origin.
        /// </summary>
        public void QuadTo(float cx, float cy, float x, float y)
        {
            var c = P(cx, cy);
            var end = P(x, y);
            painter.BezierCurveTo(m_Current + (c - m_Current) * (2f / 3f), end + (c - end) * (2f / 3f), end);
            m_Current = end;
        }

        public void CubicTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
        {
            m_Current = P(x, y);
            painter.BezierCurveTo(P(c1x, c1y), P(c2x, c2y), m_Current);
        }

        public void Close() => painter.ClosePath();

        /// <summary>Add a closed ellipse contour (works with any local transform).</summary>
        public void AddEllipse(float cx, float cy, float rx, float ry, float rotationDeg = 0f, bool counterClockwise = false)
        {
            var rad = rotationDeg * Mathf.Deg2Rad;
            var ux = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * rx;
            var uy = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * (counterClockwise ? -ry : ry);
            var c = new Vector2(cx, cy);
            var k = k_Kappa;

            MoveToV(c + ux);
            CubicToV(c + ux + uy * k, c + ux * k + uy, c + uy);
            CubicToV(c - ux * k + uy, c - ux + uy * k, c - ux);
            CubicToV(c - ux - uy * k, c - ux * k - uy, c - uy);
            CubicToV(c + ux * k - uy, c + ux - uy * k, c + ux);
            painter.ClosePath();
        }

        public void AddCircle(float cx, float cy, float r, bool counterClockwise = false) => AddEllipse(cx, cy, r, r, 0f, counterClockwise);

        /// <summary>
        /// Add an elliptical arc (angles in degrees, 0 = +x, 90 = down). Continues the current sub-path
        /// when <paramref name="connect"/> is true, otherwise starts a new one.
        /// </summary>
        public void AddArc(float cx, float cy, float rx, float ry, float startDeg, float endDeg, bool connect = false)
        {
            var span = endDeg - startDeg;
            var segments = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(span) / 90f));
            var step = span / segments * Mathf.Deg2Rad;
            var t = 4f / 3f * Mathf.Tan(step / 4f);
            var a0 = startDeg * Mathf.Deg2Rad;

            var p0 = new Vector2(cx + Mathf.Cos(a0) * rx, cy + Mathf.Sin(a0) * ry);
            if (connect) LineToV(p0); else MoveToV(p0);

            for (int i = 0; i < segments; i++)
            {
                var a1 = a0 + step;
                var p1 = new Vector2(cx + Mathf.Cos(a1) * rx, cy + Mathf.Sin(a1) * ry);
                var c1 = p0 + new Vector2(-Mathf.Sin(a0) * rx, Mathf.Cos(a0) * ry) * t;
                var c2 = p1 - new Vector2(-Mathf.Sin(a1) * rx, Mathf.Cos(a1) * ry) * t;
                CubicToV(c1, c2, p1);
                p0 = p1;
                a0 = a1;
            }
        }

        /// <summary>Add a closed polygon from (x, y) pairs placed at (cx, cy) with scale and rotation.</summary>
        public void AddPolygon(float[] xy, float cx = 0f, float cy = 0f, float scale = 1f, float rotationDeg = 0f, float sx = 1f)
        {
            var rad = rotationDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(rad);
            var sin = Mathf.Sin(rad);
            for (int i = 0; i < xy.Length; i += 2)
            {
                var x = xy[i] * scale * sx;
                var y = xy[i + 1] * scale;
                var px = cx + x * cos - y * sin;
                var py = cy + x * sin + y * cos;
                if (i == 0) MoveTo(px, py); else LineTo(px, py);
            }
            painter.ClosePath();
        }

        public void AddRoundRect(float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w * 0.5f, h * 0.5f);
            var k = 1f - k_Kappa;
            MoveTo(x + r, y);
            LineTo(x + w - r, y);
            CubicTo(x + w - r * k, y, x + w, y + r * k, x + w, y + r);
            LineTo(x + w, y + h - r);
            CubicTo(x + w, y + h - r * k, x + w - r * k, y + h, x + w - r, y + h);
            LineTo(x + r, y + h);
            CubicTo(x + r * k, y + h, x, y + h - r * k, x, y + h - r);
            LineTo(x, y + r);
            CubicTo(x, y + r * k, x + r * k, y, x + r, y);
            painter.ClosePath();
        }

        /// <summary>Add a "puck" (short cylinder seen from above-front): top ellipse, straight sides, rounded bottom.</summary>
        public void AddPuck(float cx, float topY, float rx, float ry, float height)
        {
            AddArc(cx, topY, rx, ry, 180f, 360f);
            AddArc(cx, topY + height, rx, ry, 0f, 180f, true);
            painter.ClosePath();
        }

        void MoveToV(Vector2 v) => MoveTo(v.x, v.y);
        void LineToV(Vector2 v) => LineTo(v.x, v.y);
        void CubicToV(Vector2 a, Vector2 b, Vector2 c) => CubicTo(a.x, a.y, b.x, b.y, c.x, c.y);

        // ------------------------------------------------------------------ painting

        public void Fill(Color color, FillRule rule = FillRule.NonZero)
        {
            painter.fillColor = C(color);
            painter.Fill(rule);
        }

        /// <summary>Stroke the current path; <paramref name="width"/> is in local units.</summary>
        public void Stroke(Color color, float width, LineCap cap = LineCap.Round)
        {
            painter.strokeColor = C(color);
            painter.lineWidth = Mathf.Max(0.5f, S(width));
            painter.lineCap = cap;
            painter.Stroke();
        }

        /// <summary>Stroke the current path with the ink color; width is a multiple of the ink width.</summary>
        public void Ink(float widthScale = 1f)
        {
            painter.strokeColor = C(inkColor);
            painter.lineWidth = Mathf.Max(1f, ink * widthScale);
            painter.lineCap = LineCap.Round;
            painter.Stroke();
        }

        /// <summary>Fill, then ink-outline the current path.</summary>
        public void FillInk(Color color, float inkScale = 1f, FillRule rule = FillRule.NonZero)
        {
            Fill(color, rule);
            Ink(inkScale);
        }

        /// <summary>
        /// Outline pass: stroke the path at twice the ink width. Fill the same shapes afterwards and only
        /// the outer half remains, which gives a clean outline around unions of overlapping shapes.
        /// </summary>
        public void Outline(float widthScale = 1f)
        {
            painter.strokeColor = C(inkColor);
            painter.lineWidth = Mathf.Max(2f, ink * 2f * widthScale);
            painter.lineCap = LineCap.Round;
            painter.Stroke();
        }
    }
}
