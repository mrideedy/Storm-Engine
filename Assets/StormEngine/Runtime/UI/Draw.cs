using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Painter2D helpers shared by icons and the procedural scene art.</summary>
    public static class Draw
    {
        public static void Circle(Painter2D p, Vector2 c, float r)
        {
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
        }

        public static void FillCircle(Painter2D p, Vector2 c, float r, Color fill)
        {
            p.fillColor = fill;
            Circle(p, c, r);
            p.Fill();
        }

        public static void Ellipse(Painter2D p, Vector2 c, float rx, float ry)
        {
            const float k = 0.5522848f;
            p.BeginPath();
            p.MoveTo(new Vector2(c.x + rx, c.y));
            p.BezierCurveTo(new Vector2(c.x + rx, c.y + ry * k), new Vector2(c.x + rx * k, c.y + ry), new Vector2(c.x, c.y + ry));
            p.BezierCurveTo(new Vector2(c.x - rx * k, c.y + ry), new Vector2(c.x - rx, c.y + ry * k), new Vector2(c.x - rx, c.y));
            p.BezierCurveTo(new Vector2(c.x - rx, c.y - ry * k), new Vector2(c.x - rx * k, c.y - ry), new Vector2(c.x, c.y - ry));
            p.BezierCurveTo(new Vector2(c.x + rx * k, c.y - ry), new Vector2(c.x + rx, c.y - ry * k), new Vector2(c.x + rx, c.y));
            p.ClosePath();
        }

        public static void RoundRect(Painter2D p, Rect r, float radius)
        {
            radius = Mathf.Min(radius, r.width * 0.5f, r.height * 0.5f);
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin + radius, r.yMin));
            p.LineTo(new Vector2(r.xMax - radius, r.yMin));
            p.ArcTo(new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMin + radius), radius);
            p.LineTo(new Vector2(r.xMax, r.yMax - radius));
            p.ArcTo(new Vector2(r.xMax, r.yMax), new Vector2(r.xMax - radius, r.yMax), radius);
            p.LineTo(new Vector2(r.xMin + radius, r.yMax));
            p.ArcTo(new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMax - radius), radius);
            p.LineTo(new Vector2(r.xMin, r.yMin + radius));
            p.ArcTo(new Vector2(r.xMin, r.yMin), new Vector2(r.xMin + radius, r.yMin), radius);
            p.ClosePath();
        }

        public static void Poly(Painter2D p, Vector2[] pts)
        {
            p.BeginPath();
            p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath();
        }

        public static void Star(Painter2D p, Vector2 c, float outer, float inner, int points, float rotationDeg)
        {
            p.BeginPath();
            for (int i = 0; i < points * 2; i++)
            {
                float a = (rotationDeg + i * 180f / points - 90f) * Mathf.Deg2Rad;
                float r = i % 2 == 0 ? outer : inner;
                var v = new Vector2(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r);
                if (i == 0) p.MoveTo(v);
                else p.LineTo(v);
            }
            p.ClosePath();
        }

        /// <summary>Fills the current path, then strokes it with an ink outline.</summary>
        public static void FillOutline(Painter2D p, Color fill, Color outline, float width)
        {
            p.fillColor = fill;
            p.Fill();
            if (width <= 0) return;
            p.strokeColor = outline;
            p.lineWidth = width;
            p.lineJoin = LineJoin.Round;
            p.Stroke();
        }

        public static void Line(Painter2D p, Vector2 a, Vector2 b, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(a);
            p.LineTo(b);
            p.Stroke();
        }

        public static void ArcStroke(Painter2D p, Vector2 c, float r, float fromDeg, float toDeg, Color color, float width)
        {
            if (Mathf.Abs(toDeg - fromDeg) < 0.01f) return;
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.Arc(c, r, Angle.Degrees(fromDeg), Angle.Degrees(toDeg));
            p.Stroke();
        }

        /// <summary>Pie slice used for cooldown overlays (0..1 of a full turn, clockwise from 12 o'clock).</summary>
        public static void Pie(Painter2D p, Vector2 c, float r, float fraction, Color fill)
        {
            if (fraction <= 0.001f) return;
            p.fillColor = fill;
            if (fraction >= 0.999f)
            {
                Circle(p, c, r);
                p.Fill();
                return;
            }
            p.BeginPath();
            p.MoveTo(c);
            p.Arc(c, r, Angle.Degrees(-90), Angle.Degrees(-90 + 360 * fraction));
            p.ClosePath();
            p.Fill();
        }

        /// <summary>Soft cartoon cloud made of overlapping circles, with an optional outline pass.</summary>
        public static void CloudShape(Painter2D p, Vector2 c, float w, Color fill, Color outline, float outlineWidth)
        {
            float h = w * 0.55f;
            var parts = new[]
            {
                new Vector3(c.x - w * 0.28f, c.y + h * 0.10f, w * 0.20f),
                new Vector3(c.x - w * 0.06f, c.y - h * 0.18f, w * 0.26f),
                new Vector3(c.x + w * 0.20f, c.y - h * 0.05f, w * 0.22f),
                new Vector3(c.x + w * 0.34f, c.y + h * 0.16f, w * 0.16f),
            };
            var baseRect = new Rect(c.x - w * 0.44f, c.y + h * 0.02f, w * 0.88f, h * 0.36f);
            if (outlineWidth > 0)
            {
                p.fillColor = outline;
                foreach (var q in parts)
                {
                    Circle(p, new Vector2(q.x, q.y), q.z + outlineWidth);
                    p.Fill();
                }
                RoundRect(p, new Rect(baseRect.x - outlineWidth, baseRect.y - outlineWidth, baseRect.width + outlineWidth * 2, baseRect.height + outlineWidth * 2), baseRect.height * 0.5f + outlineWidth);
                p.Fill();
            }
            p.fillColor = fill;
            foreach (var q in parts)
            {
                Circle(p, new Vector2(q.x, q.y), q.z);
                p.Fill();
            }
            RoundRect(p, baseRect, baseRect.height * 0.5f);
            p.Fill();
        }

        public static void LinearGradient(Painter2D p, Color from, Color to, Vector2 start, Vector2 end)
        {
            p.fillGradient = FillGradient.MakeLinearGradient(from, to, start, end, AddressMode.Clamp);
        }

        public static void RadialGradient(Painter2D p, Color inner, Color outer, Vector2 center, float radius)
        {
            p.fillGradient = FillGradient.MakeRadialGradient(inner, outer, center, radius, center, AddressMode.Clamp);
        }

        public static void ClearGradient(Painter2D p)
        {
            p.fillGradient = default;
        }
    }
}
