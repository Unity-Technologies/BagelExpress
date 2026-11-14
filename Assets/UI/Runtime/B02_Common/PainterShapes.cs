using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Small helpers on top of Painter2D so the vector art elements read like drawing code.
    /// All shapes are built in a normalized space and mapped into a rect.
    /// </summary>
    public static class PainterShapes
    {
        /// <summary>Map a point from the [-1, 1] square into the given rect (keeps aspect, centered).</summary>
        public static Vector2 Map(Rect rect, float x, float y)
        {
            var size = Mathf.Min(rect.width, rect.height) * 0.5f;
            return rect.center + new Vector2(x * size, y * size);
        }

        public static float UnitSize(Rect rect) => Mathf.Min(rect.width, rect.height) * 0.5f;

        public static void Circle(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.ClosePath();
        }

        public static void Ellipse(Painter2D painter, Vector2 center, Vector2 radii, int segments = 48)
        {
            painter.BeginPath();
            for (int i = 0; i <= segments; i++)
            {
                var angle = i / (float)segments * Mathf.PI * 2f;
                var point = center + new Vector2(Mathf.Cos(angle) * radii.x, Mathf.Sin(angle) * radii.y);
                if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
            }
            painter.ClosePath();
        }

        /// <summary>Closed polygon from normalized points (pairs of x, y in [-1, 1]).</summary>
        public static void Polygon(Painter2D painter, Rect rect, params float[] xy)
        {
            painter.BeginPath();
            painter.MoveTo(Map(rect, xy[0], xy[1]));
            for (int i = 2; i < xy.Length; i += 2)
                painter.LineTo(Map(rect, xy[i], xy[i + 1]));
            painter.ClosePath();
        }

        public static void RoundedRect(Painter2D painter, Rect r, float radius)
        {
            radius = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            painter.LineTo(new Vector2(r.xMax - radius, r.yMin));
            painter.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + radius), radius);
            painter.LineTo(new Vector2(r.xMax, r.yMax - radius));
            painter.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - radius, r.yMax), radius);
            painter.LineTo(new Vector2(r.xMin + radius, r.yMax));
            painter.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMax - radius), radius);
            painter.LineTo(new Vector2(r.xMin, r.yMin + radius));
            painter.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + radius, r.yMin), radius);
            painter.ClosePath();
        }

        public static void Star(Painter2D painter, Vector2 center, float outer, float inner, int points = 5, float rotationDeg = -90f)
        {
            painter.BeginPath();
            for (int i = 0; i < points * 2; i++)
            {
                var radius = (i % 2 == 0) ? outer : inner;
                var angle = (rotationDeg + i * 180f / points) * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (i == 0) painter.MoveTo(p); else painter.LineTo(p);
            }
            painter.ClosePath();
        }

        /// <summary>Fill then outline the current path in one call.</summary>
        public static void FillAndStroke(Painter2D painter, Color fill, Color ink, float inkWidth)
        {
            painter.fillColor = fill;
            painter.Fill();
            if (inkWidth > 0f)
            {
                painter.strokeColor = ink;
                painter.lineWidth = inkWidth;
                painter.lineJoin = LineJoin.Round;
                painter.Stroke();
            }
        }

        // Stylized maple leaf, normalized (Montréal flag leaf meets Japanese momiji).
        public static readonly float[] MapleLeaf =
        {
            0f, -1f, 0.17f, -0.6f, 0.42f, -0.72f, 0.36f, -0.26f, 0.92f, -0.4f, 0.74f, -0.08f,
            0.9f, 0.1f, 0.46f, 0.22f, 0.52f, 0.44f, 0.08f, 0.36f, 0.06f, 0.95f, -0.06f, 0.95f,
            -0.08f, 0.36f, -0.52f, 0.44f, -0.46f, 0.22f, -0.9f, 0.1f, -0.74f, -0.08f, -0.92f, -0.4f,
            -0.36f, -0.26f, -0.42f, -0.72f, -0.17f, -0.6f
        };
    }
}
