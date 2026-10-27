using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Vector icon drawn with Painter2D. Chunky fills with an ink outline, matching the cartoon style.</summary>
    public sealed class Icon : VisualElement
    {
        string _kind;
        Color? _tint;
        bool _outline = true;

        public Icon(string kind, float size = 24, Color? tint = null)
        {
            _kind = kind;
            _tint = tint;
            AddToClassList("se-icon");
            pickingMode = PickingMode.Ignore;
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            generateVisualContent += OnGenerate;
        }

        public string Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                MarkDirtyRepaint();
            }
        }

        public Color? Tint
        {
            get => _tint;
            set
            {
                _tint = value;
                MarkDirtyRepaint();
            }
        }

        public bool Outline
        {
            get => _outline;
            set
            {
                _outline = value;
                MarkDirtyRepaint();
            }
        }

        public static Color DefaultColor(string kind)
        {
            switch (kind)
            {
                case "energy":
                case "bolt": return Palette.Energy;
                case "core": return Palette.Core;
                case "research": return Palette.Research;
                case "heat": return Palette.Heat;
                case "water": return Palette.Water;
                case "wind": return Palette.Wind;
                case "cloud": return Palette.Cloud;
                case "cyclone": return Palette.Wind;
                case "chaos": return Palette.Chaos;
                case "synergy":
                case "brush": return Palette.Hex(0xFF7AD9);
                case "gem": return Palette.Hex(0xFF4FA3);
                case "gift": return Palette.Hex(0xFF5E7E);
                case "globe": return Palette.Hex(0x3A8DFF);
                case "nimbus": return Palette.Hex(0xEAF3FF);
                case "flame": return Palette.Orange;
                case "island": return Palette.Hex(0x9FB6D6);
                case "book": return Palette.Hex(0x9B6BFF);
                case "home": return Palette.Hex(0x39D3E6);
                case "menu": return Palette.Hex(0xEAF3FF);
                case "share": return Palette.Hex(0x4CD07D);
                case "shop": return Palette.Hex(0xFF7AB6);
                case "edit": return Palette.Hex(0xFFC83D);
                case "eye": return Palette.Hex(0x3A8DFF);
                case "rotate": return Palette.Hex(0x4CD07D);
                case "trophy":
                case "star":
                case "leaders": return Palette.Yellow;
                case "check": return Palette.Green;
                case "close": return Palette.Red;
                case "lock": return Palette.Metal;
                default: return Palette.Hex(0x9FB6D6);
            }
        }

        void OnGenerate(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 2 || r.height < 2) return;
            float s = Mathf.Min(r.width, r.height);
            var box = new Rect(r.x + (r.width - s) * 0.5f, r.y + (r.height - s) * 0.5f, s, s);
            Paint(ctx.painter2D, _kind, box, _tint ?? DefaultColor(_kind), _outline);
        }

        public static void Paint(Painter2D p, string kind, Rect box, Color color, bool outline)
        {
            float s = box.width;
            float ow = outline ? Mathf.Max(1.2f, s * 0.075f) : 0f;
            var ink = Palette.Ink;
            Vector2 V(float x, float y) => new Vector2(box.x + x * s, box.y + y * s);

            switch (kind)
            {
                case "energy":
                case "bolt":
                    Draw.Poly(p, new[] { V(0.60f, 0.03f), V(0.19f, 0.56f), V(0.47f, 0.56f), V(0.36f, 0.97f), V(0.81f, 0.41f), V(0.53f, 0.41f), V(0.67f, 0.03f) });
                    Draw.FillOutline(p, color, ink, ow);
                    break;

                case "core":
                    Draw.Poly(p, new[] { V(0.14f, 0.36f), V(0.32f, 0.12f), V(0.68f, 0.12f), V(0.86f, 0.36f), V(0.5f, 0.92f) });
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Poly(p, new[] { V(0.32f, 0.14f), V(0.68f, 0.14f), V(0.62f, 0.34f), V(0.38f, 0.34f) });
                    p.fillColor = color.Lighten(0.55f);
                    p.Fill();
                    Draw.Line(p, V(0.18f, 0.36f), V(0.82f, 0.36f), ink.WithAlpha(0.5f), ow * 0.5f);
                    break;

                case "research":
                    p.BeginPath();
                    p.MoveTo(V(0.38f, 0.08f));
                    p.LineTo(V(0.62f, 0.08f));
                    p.LineTo(V(0.62f, 0.38f));
                    p.LineTo(V(0.86f, 0.80f));
                    p.QuadraticCurveTo(V(0.90f, 0.93f), V(0.76f, 0.93f));
                    p.LineTo(V(0.24f, 0.93f));
                    p.QuadraticCurveTo(V(0.10f, 0.93f), V(0.14f, 0.80f));
                    p.LineTo(V(0.38f, 0.38f));
                    p.ClosePath();
                    Draw.FillOutline(p, Palette.Cloud, ink, ow);
                    p.BeginPath();
                    p.MoveTo(V(0.27f, 0.60f));
                    p.LineTo(V(0.73f, 0.60f));
                    p.LineTo(V(0.83f, 0.80f));
                    p.QuadraticCurveTo(V(0.86f, 0.88f), V(0.76f, 0.88f));
                    p.LineTo(V(0.24f, 0.88f));
                    p.QuadraticCurveTo(V(0.14f, 0.88f), V(0.17f, 0.80f));
                    p.ClosePath();
                    p.fillColor = color;
                    p.Fill();
                    Draw.FillCircle(p, V(0.44f, 0.74f), s * 0.05f, Palette.Cloud.WithAlpha(0.8f));
                    break;

                case "heat":
                    p.BeginPath();
                    p.MoveTo(V(0.52f, 0.04f));
                    p.BezierCurveTo(V(0.64f, 0.24f), V(0.86f, 0.38f), V(0.84f, 0.62f));
                    p.BezierCurveTo(V(0.82f, 0.86f), V(0.64f, 0.96f), V(0.50f, 0.96f));
                    p.BezierCurveTo(V(0.34f, 0.96f), V(0.16f, 0.86f), V(0.16f, 0.62f));
                    p.BezierCurveTo(V(0.16f, 0.46f), V(0.28f, 0.36f), V(0.34f, 0.22f));
                    p.BezierCurveTo(V(0.42f, 0.32f), V(0.44f, 0.40f), V(0.46f, 0.44f));
                    p.BezierCurveTo(V(0.54f, 0.32f), V(0.56f, 0.18f), V(0.52f, 0.04f));
                    p.ClosePath();
                    Draw.FillOutline(p, color, ink, ow);
                    p.BeginPath();
                    p.MoveTo(V(0.50f, 0.50f));
                    p.BezierCurveTo(V(0.62f, 0.62f), V(0.68f, 0.70f), V(0.66f, 0.80f));
                    p.BezierCurveTo(V(0.64f, 0.90f), V(0.56f, 0.92f), V(0.50f, 0.92f));
                    p.BezierCurveTo(V(0.42f, 0.92f), V(0.34f, 0.88f), V(0.34f, 0.78f));
                    p.BezierCurveTo(V(0.34f, 0.68f), V(0.44f, 0.62f), V(0.50f, 0.50f));
                    p.ClosePath();
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    break;

                case "water":
                    p.BeginPath();
                    p.MoveTo(V(0.5f, 0.04f));
                    p.BezierCurveTo(V(0.62f, 0.24f), V(0.86f, 0.44f), V(0.86f, 0.64f));
                    p.BezierCurveTo(V(0.86f, 0.84f), V(0.70f, 0.96f), V(0.5f, 0.96f));
                    p.BezierCurveTo(V(0.30f, 0.96f), V(0.14f, 0.84f), V(0.14f, 0.64f));
                    p.BezierCurveTo(V(0.14f, 0.44f), V(0.38f, 0.24f), V(0.5f, 0.04f));
                    p.ClosePath();
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Ellipse(p, V(0.36f, 0.64f), s * 0.07f, s * 0.12f);
                    p.fillColor = Palette.White.WithAlpha(0.75f);
                    p.Fill();
                    break;

                case "wind":
                {
                    float w = Mathf.Max(1.6f, s * 0.11f);
                    if (outline) WindStrokes(p, V, ink, w + ow * 1.4f);
                    WindStrokes(p, V, color, w);
                    break;
                }

                case "cloud":
                    Draw.CloudShape(p, V(0.5f, 0.5f), s * 0.92f, color, ink, ow);
                    break;

                case "cyclone":
                {
                    float w = Mathf.Max(1.5f, s * 0.09f);
                    for (int pass = outline ? 0 : 1; pass < 2; pass++)
                    {
                        p.strokeColor = pass == 0 ? ink : color;
                        p.lineWidth = pass == 0 ? w + ow * 1.4f : w;
                        p.lineCap = LineCap.Round;
                        p.BeginPath();
                        for (int i = 0; i <= 40; i++)
                        {
                            float t = i / 40f;
                            float a = t * Mathf.PI * 4.2f;
                            float rad = s * (0.05f + 0.38f * t);
                            var pt = new Vector2(box.center.x + Mathf.Cos(a) * rad, box.center.y + Mathf.Sin(a) * rad * 0.8f);
                            if (i == 0) p.MoveTo(pt);
                            else p.LineTo(pt);
                        }
                        p.Stroke();
                    }
                    break;
                }

                case "pump":
                    Draw.Circle(p, V(0.5f, 0.54f), s * 0.40f);
                    Draw.FillOutline(p, Palette.Cloud, ink, ow);
                    Draw.ArcStroke(p, V(0.5f, 0.54f), s * 0.28f, 150, 390, color.a > 0 ? Palette.Metal : color, s * 0.06f);
                    Draw.ArcStroke(p, V(0.5f, 0.54f), s * 0.28f, 330, 390, Palette.Red, s * 0.07f);
                    Draw.Line(p, V(0.5f, 0.54f), V(0.70f, 0.36f), ink, s * 0.08f);
                    Draw.FillCircle(p, V(0.5f, 0.54f), s * 0.07f, Palette.Orange);
                    break;

                case "chamber":
                    p.BeginPath();
                    p.MoveTo(V(0.16f, 0.62f));
                    p.Arc(V(0.5f, 0.62f), s * 0.34f, Angle.Degrees(180), Angle.Degrees(360));
                    p.ClosePath();
                    Draw.FillOutline(p, Palette.Hex(0xBFEAFF), ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.08f, box.y + s * 0.62f, s * 0.84f, s * 0.26f), s * 0.06f);
                    Draw.FillOutline(p, Palette.Orange, ink, ow);
                    Draw.FillCircle(p, V(0.40f, 0.44f), s * 0.06f, Palette.White.WithAlpha(0.8f));
                    break;

                case "chip":
                    for (int i = 0; i < 3; i++)
                    {
                        float t = 0.3f + i * 0.2f;
                        Draw.Line(p, V(t, 0.06f), V(t, 0.94f), ink, s * 0.08f);
                        Draw.Line(p, V(0.06f, t), V(0.94f, t), ink, s * 0.08f);
                    }
                    Draw.RoundRect(p, new Rect(box.x + s * 0.18f, box.y + s * 0.18f, s * 0.64f, s * 0.64f), s * 0.1f);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.34f, box.y + s * 0.34f, s * 0.32f, s * 0.32f), s * 0.05f);
                    p.fillColor = color.Lighten(0.5f);
                    p.Fill();
                    break;

                case "gear":
                case "settings":
                {
                    var c = V(0.5f, 0.5f);
                    p.BeginPath();
                    int teeth = 8;
                    for (int i = 0; i < teeth * 2; i++)
                    {
                        float a0 = (i * 180f / teeth - 11f) * Mathf.Deg2Rad;
                        float a1 = (i * 180f / teeth + 11f) * Mathf.Deg2Rad;
                        float rr = i % 2 == 0 ? s * 0.46f : s * 0.34f;
                        var p0 = c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * rr;
                        var p1 = c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * rr;
                        if (i == 0) p.MoveTo(p0);
                        else p.LineTo(p0);
                        p.LineTo(p1);
                    }
                    p.ClosePath();
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Circle(p, c, s * 0.14f);
                    Draw.FillOutline(p, Palette.Cloud, ink, ow * 0.8f);
                    break;
                }

                case "trophy":
                    p.BeginPath();
                    p.MoveTo(V(0.24f, 0.10f));
                    p.LineTo(V(0.76f, 0.10f));
                    p.LineTo(V(0.72f, 0.44f));
                    p.QuadraticCurveTo(V(0.66f, 0.62f), V(0.5f, 0.62f));
                    p.QuadraticCurveTo(V(0.34f, 0.62f), V(0.28f, 0.44f));
                    p.ClosePath();
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.ArcStroke(p, V(0.22f, 0.28f), s * 0.12f, 90, 270, ink, ow * 1.2f);
                    Draw.ArcStroke(p, V(0.78f, 0.28f), s * 0.12f, -90, 90, ink, ow * 1.2f);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.42f, box.y + s * 0.60f, s * 0.16f, s * 0.18f), 1);
                    Draw.FillOutline(p, color.Darken(0.15f), ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.26f, box.y + s * 0.76f, s * 0.48f, s * 0.14f), s * 0.04f);
                    Draw.FillOutline(p, Palette.Orange, ink, ow);
                    break;

                case "clock":
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.42f);
                    Draw.FillOutline(p, Palette.Cloud, ink, ow);
                    Draw.Line(p, V(0.5f, 0.5f), V(0.5f, 0.24f), ink, s * 0.08f);
                    Draw.Line(p, V(0.5f, 0.5f), V(0.68f, 0.58f), ink, s * 0.08f);
                    Draw.FillCircle(p, V(0.5f, 0.5f), s * 0.06f, color.a > 0 ? Palette.Orange : color);
                    break;

                case "battery":
                    Draw.RoundRect(p, new Rect(box.x + s * 0.40f, box.y + s * 0.06f, s * 0.20f, s * 0.12f), s * 0.03f);
                    Draw.FillOutline(p, Palette.Metal, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.24f, box.y + s * 0.16f, s * 0.52f, s * 0.78f), s * 0.08f);
                    Draw.FillOutline(p, Palette.Cloud, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.31f, box.y + s * 0.50f, s * 0.38f, s * 0.37f), s * 0.04f);
                    p.fillColor = Palette.Green;
                    p.Fill();
                    break;

                case "auto":
                case "refresh":
                {
                    float w = Mathf.Max(1.6f, s * 0.12f);
                    if (outline) Draw.ArcStroke(p, V(0.5f, 0.52f), s * 0.32f, -60, 220, ink, w + ow * 1.3f);
                    Draw.ArcStroke(p, V(0.5f, 0.52f), s * 0.32f, -60, 220, color, w);
                    Draw.Poly(p, new[] { V(0.70f, 0.10f), V(0.90f, 0.36f), V(0.60f, 0.38f) });
                    Draw.FillOutline(p, color, ink, ow * 0.8f);
                    break;
                }

                case "synergy":
                {
                    float w = Mathf.Max(1.6f, s * 0.12f);
                    for (int pass = outline ? 0 : 1; pass < 2; pass++)
                    {
                        var col = pass == 0 ? ink : color;
                        float lw = pass == 0 ? w + ow * 1.3f : w;
                        Draw.ArcStroke(p, V(0.36f, 0.5f), s * 0.24f, 0, 360, col, lw);
                        Draw.ArcStroke(p, V(0.64f, 0.5f), s * 0.24f, 0, 360, pass == 0 ? col : Palette.Yellow, lw);
                    }
                    break;
                }

                case "chaos":
                    Draw.Star(p, V(0.5f, 0.5f), s * 0.47f, s * 0.22f, 8, 0);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.FillCircle(p, V(0.5f, 0.5f), s * 0.12f, Palette.Ink);
                    break;

                case "star":
                    Draw.Star(p, V(0.5f, 0.54f), s * 0.46f, s * 0.2f, 5, 0);
                    Draw.FillOutline(p, color, ink, ow);
                    break;

                case "gem":
                    Draw.Poly(p, new[] { V(0.18f, 0.34f), V(0.34f, 0.14f), V(0.66f, 0.14f), V(0.82f, 0.34f), V(0.5f, 0.9f) });
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Poly(p, new[] { V(0.34f, 0.16f), V(0.5f, 0.34f), V(0.66f, 0.16f) });
                    p.fillColor = color.Lighten(0.45f);
                    p.Fill();
                    Draw.Poly(p, new[] { V(0.2f, 0.35f), V(0.8f, 0.35f), V(0.5f, 0.86f) });
                    p.fillColor = color.Darken(0.12f);
                    p.Fill();
                    Draw.Line(p, V(0.2f, 0.35f), V(0.8f, 0.35f), ink.WithAlpha(0.6f), ow * 0.6f);
                    Draw.FillCircle(p, V(0.36f, 0.26f), s * 0.05f, Color.white.WithAlpha(0.85f));
                    break;

                case "island":
                    Draw.Poly(p, new[] { V(0.1f, 0.5f), V(0.9f, 0.5f), V(0.66f, 0.74f), V(0.54f, 0.92f), V(0.4f, 0.76f) });
                    Draw.FillOutline(p, Palette.Hex(0xA0785A), ink, ow);
                    Draw.Ellipse(p, V(0.5f, 0.5f), s * 0.42f, s * 0.12f);
                    Draw.FillOutline(p, Palette.Grass, ink, ow);
                    Draw.Line(p, V(0.32f, 0.46f), V(0.32f, 0.3f), Palette.Hex(0x8A5A2B), s * 0.07f);
                    Draw.FillCircle(p, V(0.32f, 0.24f), s * 0.13f, Palette.GrassDark);
                    Draw.Poly(p, new[] { V(0.56f, 0.46f), V(0.64f, 0.2f), V(0.72f, 0.46f) });
                    Draw.FillOutline(p, color, ink, ow * 0.7f);
                    break;

                case "book":
                    Draw.RoundRect(p, new Rect(box.x + s * 0.16f, box.y + s * 0.1f, s * 0.68f, s * 0.8f), s * 0.08f);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.28f, box.y + s * 0.1f, s * 0.56f, s * 0.72f), s * 0.06f);
                    p.fillColor = Palette.Cloud;
                    p.Fill();
                    Draw.Line(p, V(0.38f, 0.3f), V(0.72f, 0.3f), color.Darken(0.2f), s * 0.05f);
                    Draw.Line(p, V(0.38f, 0.44f), V(0.66f, 0.44f), color.Darken(0.2f), s * 0.05f);
                    Draw.Poly(p, new[] { V(0.54f, 0.52f), V(0.44f, 0.68f), V(0.52f, 0.68f), V(0.48f, 0.8f), V(0.62f, 0.62f), V(0.54f, 0.62f), V(0.6f, 0.52f) });
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    break;

                case "home":
                    Draw.Poly(p, new[] { V(0.5f, 0.1f), V(0.9f, 0.46f), V(0.78f, 0.46f), V(0.78f, 0.88f), V(0.22f, 0.88f), V(0.22f, 0.46f), V(0.1f, 0.46f) });
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.42f, box.y + s * 0.6f, s * 0.16f, s * 0.28f), s * 0.03f);
                    Draw.FillOutline(p, Palette.Orange, ink, ow * 0.7f);
                    break;

                case "shop":
                    Draw.RoundRect(p, new Rect(box.x + s * 0.14f, box.y + s * 0.34f, s * 0.72f, s * 0.56f), s * 0.1f);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.ArcStroke(p, V(0.5f, 0.36f), s * 0.2f, 180, 360, ink, ow * 1.3f);
                    Draw.Star(p, V(0.5f, 0.62f), s * 0.14f, s * 0.06f, 5, 0);
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    break;

                case "edit":
                    Draw.Poly(p, new[] { V(0.7f, 0.1f), V(0.9f, 0.3f), V(0.38f, 0.82f), V(0.14f, 0.88f), V(0.2f, 0.62f) });
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Poly(p, new[] { V(0.2f, 0.62f), V(0.38f, 0.82f), V(0.14f, 0.88f) });
                    p.fillColor = Palette.Cloud;
                    p.Fill();
                    break;

                case "eye":
                    p.BeginPath();
                    p.MoveTo(V(0.06f, 0.5f));
                    p.QuadraticCurveTo(V(0.5f, 0.08f), V(0.94f, 0.5f));
                    p.QuadraticCurveTo(V(0.5f, 0.92f), V(0.06f, 0.5f));
                    p.ClosePath();
                    Draw.FillOutline(p, Palette.Cloud, ink, ow);
                    Draw.FillCircle(p, V(0.5f, 0.5f), s * 0.17f, color);
                    Draw.FillCircle(p, V(0.5f, 0.5f), s * 0.08f, ink);
                    break;

                case "rotate":
                    Draw.ArcStroke(p, V(0.5f, 0.52f), s * 0.3f, -60, 220, color, s * 0.12f);
                    Draw.Poly(p, new[] { V(0.62f, 0.08f), V(0.86f, 0.24f), V(0.6f, 0.34f) });
                    p.fillColor = color;
                    p.Fill();
                    break;

                case "brush":
                    Draw.Poly(p, new[] { V(0.84f, 0.05f), V(0.96f, 0.17f), V(0.58f, 0.55f), V(0.46f, 0.43f) });
                    Draw.FillOutline(p, Palette.Orange, ink, ow);
                    Draw.Poly(p, new[] { V(0.46f, 0.43f), V(0.58f, 0.55f), V(0.49f, 0.64f), V(0.37f, 0.52f) });
                    Draw.FillOutline(p, Palette.Metal, ink, ow);
                    Draw.Poly(p, new[] { V(0.37f, 0.52f), V(0.49f, 0.64f), V(0.36f, 0.86f), V(0.08f, 0.94f), V(0.15f, 0.66f) });
                    Draw.FillOutline(p, color, ink, ow);
                    break;

                case "boost":
                case "up":
                    Draw.Poly(p, new[] { V(0.5f, 0.06f), V(0.88f, 0.48f), V(0.64f, 0.48f), V(0.64f, 0.92f), V(0.36f, 0.92f), V(0.36f, 0.48f), V(0.12f, 0.48f) });
                    Draw.FillOutline(p, kind == "up" ? Palette.Green : color, ink, ow);
                    break;

                case "charge":
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.44f);
                    Draw.FillOutline(p, Palette.Navy, ink, ow);
                    Paint(p, "bolt", new Rect(box.x + s * 0.22f, box.y + s * 0.18f, s * 0.56f, s * 0.64f), Palette.Yellow, false);
                    break;

                case "lock":
                    Draw.ArcStroke(p, V(0.5f, 0.42f), s * 0.2f, 180, 360, ink, s * 0.12f);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.2f, box.y + s * 0.42f, s * 0.6f, s * 0.48f), s * 0.08f);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.FillCircle(p, V(0.5f, 0.62f), s * 0.07f, ink);
                    break;

                case "leaders":
                    Draw.RoundRect(p, new Rect(box.x + s * 0.36f, box.y + s * 0.30f, s * 0.28f, s * 0.62f), s * 0.04f);
                    Draw.FillOutline(p, Palette.Yellow, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.08f, box.y + s * 0.50f, s * 0.28f, s * 0.42f), s * 0.04f);
                    Draw.FillOutline(p, Palette.Metal, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.64f, box.y + s * 0.62f, s * 0.28f, s * 0.30f), s * 0.04f);
                    Draw.FillOutline(p, Palette.Orange, ink, ow);
                    Draw.Star(p, V(0.5f, 0.16f), s * 0.13f, s * 0.06f, 5, 0);
                    Draw.FillOutline(p, Palette.Yellow, ink, ow * 0.6f);
                    break;

                case "modules":
                    Paint(p, "chip", box, Palette.Hex(0x7C8CFF), outline);
                    break;

                case "atmosphere":
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.42f);
                    Draw.FillOutline(p, Palette.Water, ink, ow);
                    Draw.Ellipse(p, V(0.38f, 0.40f), s * 0.14f, s * 0.10f);
                    p.fillColor = Palette.Grass;
                    p.Fill();
                    Draw.Ellipse(p, V(0.64f, 0.64f), s * 0.12f, s * 0.08f);
                    p.fillColor = Palette.Grass;
                    p.Fill();
                    Draw.ArcStroke(p, V(0.5f, 0.5f), s * 0.42f, 0, 360, ink, ow);
                    break;

                case "machines":
                    Draw.RoundRect(p, new Rect(box.x + s * 0.10f, box.y + s * 0.42f, s * 0.80f, s * 0.48f), s * 0.06f);
                    Draw.FillOutline(p, Palette.Orange, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.16f, box.y + s * 0.12f, s * 0.18f, s * 0.36f), s * 0.03f);
                    Draw.FillOutline(p, Palette.Metal, ink, ow);
                    Draw.Poly(p, new[] { V(0.40f, 0.42f), V(0.62f, 0.26f), V(0.62f, 0.42f), V(0.86f, 0.26f), V(0.86f, 0.42f) });
                    Draw.FillOutline(p, Palette.Orange.Darken(0.1f), ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.24f, box.y + s * 0.58f, s * 0.16f, s * 0.16f), s * 0.03f);
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    Draw.RoundRect(p, new Rect(box.x + s * 0.54f, box.y + s * 0.58f, s * 0.16f, s * 0.16f), s * 0.03f);
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    break;

                case "more":
                    for (int i = 0; i < 3; i++)
                    {
                        Draw.Circle(p, V(0.2f + i * 0.3f, 0.5f), s * 0.11f);
                        Draw.FillOutline(p, color, ink, ow);
                    }
                    break;

                case "menu":
                    for (int i = 0; i < 3; i++)
                    {
                        Draw.RoundRect(p, new Rect(box.x + s * 0.14f, box.y + s * (0.17f + i * 0.26f), s * 0.72f, s * 0.15f), s * 0.075f);
                        Draw.FillOutline(p, color, ink, ow);
                    }
                    break;

                case "share":
                {
                    var a = V(0.26f, 0.5f);
                    var b1 = V(0.72f, 0.24f);
                    var b2 = V(0.72f, 0.76f);
                    Draw.Line(p, a, b1, ink, s * 0.1f + ow);
                    Draw.Line(p, a, b2, ink, s * 0.1f + ow);
                    Draw.Line(p, a, b1, color, s * 0.1f);
                    Draw.Line(p, a, b2, color, s * 0.1f);
                    foreach (var c in new[] { a, b1, b2 })
                    {
                        Draw.Circle(p, c, s * 0.15f);
                        Draw.FillOutline(p, color, ink, ow);
                    }
                    break;
                }

                case "close":
                    Draw.Line(p, V(0.22f, 0.22f), V(0.78f, 0.78f), ink, s * 0.2f + ow);
                    Draw.Line(p, V(0.78f, 0.22f), V(0.22f, 0.78f), ink, s * 0.2f + ow);
                    Draw.Line(p, V(0.22f, 0.22f), V(0.78f, 0.78f), color, s * 0.2f);
                    Draw.Line(p, V(0.78f, 0.22f), V(0.22f, 0.78f), color, s * 0.2f);
                    break;

                case "check":
                    p.strokeColor = ink;
                    p.lineWidth = s * 0.2f + ow;
                    p.lineCap = LineCap.Round;
                    p.lineJoin = LineJoin.Round;
                    p.BeginPath();
                    p.MoveTo(V(0.18f, 0.52f));
                    p.LineTo(V(0.42f, 0.76f));
                    p.LineTo(V(0.84f, 0.26f));
                    p.Stroke();
                    p.strokeColor = color;
                    p.lineWidth = s * 0.2f;
                    p.Stroke();
                    break;

                case "calendar":
                    Draw.RoundRect(p, new Rect(box.x + s * 0.12f, box.y + s * 0.18f, s * 0.76f, s * 0.70f), s * 0.08f);
                    Draw.FillOutline(p, Palette.Cloud, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.12f, box.y + s * 0.18f, s * 0.76f, s * 0.20f), s * 0.06f);
                    Draw.FillOutline(p, Palette.Red, ink, ow);
                    for (int i = 0; i < 3; i++)
                        for (int j = 0; j < 2; j++)
                            Draw.FillCircle(p, V(0.30f + i * 0.2f, 0.54f + j * 0.18f), s * 0.05f, Palette.NavySoft);
                    break;

                case "signal":
                    for (int i = 0; i < 3; i++)
                    {
                        float h = 0.25f + i * 0.25f;
                        Draw.RoundRect(p, new Rect(box.x + s * (0.14f + i * 0.26f), box.y + s * (0.9f - h), s * 0.2f, s * h), s * 0.04f);
                        Draw.FillOutline(p, color, ink, ow);
                    }
                    break;

                case "offline":
                    Draw.CloudShape(p, V(0.5f, 0.52f), s * 0.9f, Palette.Metal, ink, ow);
                    Draw.Line(p, V(0.16f, 0.16f), V(0.84f, 0.86f), Palette.Red, s * 0.1f);
                    break;

                case "info":
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.42f);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Line(p, V(0.5f, 0.46f), V(0.5f, 0.72f), Palette.White, s * 0.12f);
                    Draw.FillCircle(p, V(0.5f, 0.30f), s * 0.07f, Palette.White);
                    break;

                case "user":
                    Draw.Circle(p, V(0.5f, 0.34f), s * 0.2f);
                    Draw.FillOutline(p, color, ink, ow);
                    p.BeginPath();
                    p.MoveTo(V(0.14f, 0.92f));
                    p.BezierCurveTo(V(0.14f, 0.62f), V(0.86f, 0.62f), V(0.86f, 0.92f));
                    p.ClosePath();
                    Draw.FillOutline(p, color, ink, ow);
                    break;

                case "sound":
                case "mute":
                    Draw.Poly(p, new[] { V(0.12f, 0.38f), V(0.30f, 0.38f), V(0.52f, 0.18f), V(0.52f, 0.82f), V(0.30f, 0.62f), V(0.12f, 0.62f) });
                    Draw.FillOutline(p, color, ink, ow);
                    if (kind == "sound")
                    {
                        Draw.ArcStroke(p, V(0.52f, 0.5f), s * 0.18f, -45, 45, ink, s * 0.08f);
                        Draw.ArcStroke(p, V(0.52f, 0.5f), s * 0.32f, -45, 45, ink, s * 0.08f);
                    }
                    else
                    {
                        Draw.Line(p, V(0.66f, 0.36f), V(0.90f, 0.64f), Palette.Red, s * 0.1f);
                        Draw.Line(p, V(0.90f, 0.36f), V(0.66f, 0.64f), Palette.Red, s * 0.1f);
                    }
                    break;

                case "play":
                    Draw.Poly(p, new[] { V(0.26f, 0.14f), V(0.86f, 0.5f), V(0.26f, 0.86f) });
                    Draw.FillOutline(p, color, ink, ow);
                    break;

                case "gift":
                {
                    // Bow loops, box, lid and a ribbon down the middle.
                    Draw.Ellipse(p, V(0.35f, 0.21f), s * 0.15f, s * 0.10f);
                    Draw.FillOutline(p, Palette.Yellow, ink, ow);
                    Draw.Ellipse(p, V(0.65f, 0.21f), s * 0.15f, s * 0.10f);
                    Draw.FillOutline(p, Palette.Yellow, ink, ow);
                    Draw.RoundRect(p, new Rect(box.x + s * 0.15f, box.y + s * 0.44f, s * 0.70f, s * 0.48f), s * 0.06f);
                    Draw.FillOutline(p, color, ink, ow);
                    Draw.Poly(p, new[] { V(0.43f, 0.45f), V(0.57f, 0.45f), V(0.57f, 0.91f), V(0.43f, 0.91f) });
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    Draw.RoundRect(p, new Rect(box.x + s * 0.08f, box.y + s * 0.29f, s * 0.84f, s * 0.18f), s * 0.05f);
                    Draw.FillOutline(p, color.Lighten(0.15f), ink, ow);
                    Draw.Poly(p, new[] { V(0.43f, 0.30f), V(0.57f, 0.30f), V(0.57f, 0.46f), V(0.43f, 0.46f) });
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    Draw.FillCircle(p, V(0.5f, 0.27f), s * 0.075f, Palette.YellowDark);
                    break;
                }

                case "nimbus":
                {
                    // Nimbus, the little storm cloud who explains things: a cloud with a face and a spark.
                    Paint(p, "bolt", new Rect(box.x + s * 0.60f, box.y + s * 0.52f, s * 0.34f, s * 0.46f), Palette.Yellow, outline);
                    Draw.CloudShape(p, V(0.47f, 0.46f), s * 0.92f, color, ink, ow);
                    Draw.FillCircle(p, V(0.36f, 0.47f), s * 0.055f, ink);
                    Draw.FillCircle(p, V(0.56f, 0.47f), s * 0.055f, ink);
                    Draw.FillCircle(p, V(0.375f, 0.455f), s * 0.018f, Color.white);
                    Draw.FillCircle(p, V(0.575f, 0.455f), s * 0.018f, Color.white);
                    Draw.FillCircle(p, V(0.29f, 0.56f), s * 0.045f, Palette.Hex(0xFF9BB3).WithAlpha(0.8f));
                    Draw.FillCircle(p, V(0.63f, 0.56f), s * 0.045f, Palette.Hex(0xFF9BB3).WithAlpha(0.8f));
                    Draw.ArcStroke(p, V(0.46f, 0.53f), s * 0.07f, 20, 160, ink, s * 0.035f);
                    break;
                }

                case "globe":
                {
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.42f);
                    Draw.FillOutline(p, color, ink, ow);
                    var land = Palette.Green;
                    Draw.Ellipse(p, V(0.38f, 0.38f), s * 0.14f, s * 0.10f);
                    p.fillColor = land;
                    p.Fill();
                    Draw.Ellipse(p, V(0.62f, 0.62f), s * 0.12f, s * 0.14f);
                    p.fillColor = land;
                    p.Fill();
                    Draw.Line(p, V(0.08f, 0.5f), V(0.92f, 0.5f), ink.WithAlpha(0.45f), s * 0.03f);
                    Draw.Ellipse(p, V(0.5f, 0.5f), s * 0.18f, s * 0.42f);
                    p.strokeColor = ink.WithAlpha(0.45f);
                    p.lineWidth = s * 0.03f;
                    p.Stroke();
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.42f);
                    p.strokeColor = ink;
                    p.lineWidth = Mathf.Max(1.2f, ow);
                    if (outline) p.Stroke();
                    break;
                }

                case "flame":
                    p.BeginPath();
                    p.MoveTo(V(0.50f, 0.04f));
                    p.BezierCurveTo(V(0.62f, 0.26f), V(0.86f, 0.40f), V(0.84f, 0.64f));
                    p.BezierCurveTo(V(0.82f, 0.86f), V(0.66f, 0.96f), V(0.50f, 0.96f));
                    p.BezierCurveTo(V(0.32f, 0.96f), V(0.16f, 0.84f), V(0.16f, 0.62f));
                    p.BezierCurveTo(V(0.16f, 0.46f), V(0.28f, 0.36f), V(0.34f, 0.26f));
                    p.BezierCurveTo(V(0.38f, 0.40f), V(0.44f, 0.44f), V(0.48f, 0.44f));
                    p.BezierCurveTo(V(0.46f, 0.30f), V(0.46f, 0.16f), V(0.50f, 0.04f));
                    p.ClosePath();
                    Draw.FillOutline(p, color, ink, ow);
                    p.BeginPath();
                    p.MoveTo(V(0.50f, 0.52f));
                    p.BezierCurveTo(V(0.60f, 0.64f), V(0.68f, 0.72f), V(0.66f, 0.80f));
                    p.BezierCurveTo(V(0.64f, 0.90f), V(0.56f, 0.92f), V(0.50f, 0.92f));
                    p.BezierCurveTo(V(0.42f, 0.92f), V(0.34f, 0.88f), V(0.34f, 0.78f));
                    p.BezierCurveTo(V(0.34f, 0.68f), V(0.44f, 0.62f), V(0.50f, 0.52f));
                    p.ClosePath();
                    p.fillColor = Palette.Yellow;
                    p.Fill();
                    break;

                case "storm":
                    Draw.CloudShape(p, V(0.5f, 0.38f), s * 0.9f, Palette.Hex(0x6B7AA8), ink, ow);
                    Paint(p, "bolt", new Rect(box.x + s * 0.30f, box.y + s * 0.42f, s * 0.40f, s * 0.54f), Palette.Yellow, outline);
                    break;

                default:
                    Draw.Circle(p, V(0.5f, 0.5f), s * 0.4f);
                    Draw.FillOutline(p, color, ink, ow);
                    break;
            }
        }

        static void WindStrokes(Painter2D p, System.Func<float, float, Vector2> V, Color color, float width)
        {
            p.strokeColor = color;
            p.lineWidth = width;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.BeginPath();
            p.MoveTo(V(0.08f, 0.34f));
            p.LineTo(V(0.62f, 0.34f));
            p.BezierCurveTo(V(0.80f, 0.34f), V(0.84f, 0.12f), V(0.68f, 0.10f));
            p.BezierCurveTo(V(0.58f, 0.09f), V(0.54f, 0.18f), V(0.58f, 0.24f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(V(0.08f, 0.56f));
            p.LineTo(V(0.76f, 0.56f));
            p.BezierCurveTo(V(0.94f, 0.56f), V(0.96f, 0.80f), V(0.80f, 0.82f));
            p.BezierCurveTo(V(0.70f, 0.83f), V(0.66f, 0.74f), V(0.70f, 0.68f));
            p.Stroke();
            p.BeginPath();
            p.MoveTo(V(0.16f, 0.78f));
            p.LineTo(V(0.48f, 0.78f));
            p.Stroke();
        }
    }
}
