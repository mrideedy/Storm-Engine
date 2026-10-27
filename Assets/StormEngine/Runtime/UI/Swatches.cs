using StormEngine.Core;
using StormEngine.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Small painted preview of a style item. Skins show their own silhouette.</summary>
    public sealed class Swatch : VisualElement
    {
        readonly CosmeticDef _def;

        public Swatch(CosmeticDef def)
        {
            _def = def;
            AddToClassList("se-swatch");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        static Color Hex(int rgb) => Palette.Hex(rgb);

        void Paint(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 4) return;
            var p = ctx.painter2D;
            float s = Mathf.Min(r.width, r.height);
            var c = r.center;
            switch (_def.Slot)
            {
                case CosmeticSlot.Skin: PaintSkin(p, c, s); break;
                case CosmeticSlot.Cloud: PaintCloud(p, c, s); break;
                case CosmeticSlot.Bolt: PaintBolt(p, c, s); break;
                case CosmeticSlot.World: PaintWorld(p, c, s); break;
                default: PaintTitle(p, c, s); break;
            }
        }

        void PaintSkin(Painter2D p, Vector2 c, float s)
        {
            var ink = Palette.Ink;
            var a = Hex(_def.Primary);
            var b = Hex(_def.Secondary);
            var acc = Hex(_def.Accent);
            var shape = SkinShape.For(_def.Style);
            const float ow = 2.2f;
            Vector2 V(float x, float y) => new Vector2(c.x + x * s, c.y + y * s);

            if (shape.Glow > 0) Draw.FillCircle(p, V(0, -0.02f), s * 0.46f, acc.WithAlpha(Mathf.Min(0.35f, 0.12f * shape.Glow)));

            // Fins behind the deck.
            if (shape.Fins > 0)
            {
                Draw.Poly(p, new[] { V(-0.3f, 0.02f), V(-0.46f, 0.34f), V(-0.24f, 0.26f) });
                Draw.FillOutline(p, acc, ink, ow);
                Draw.Poly(p, new[] { V(0.3f, 0.02f), V(0.46f, 0.34f), V(0.24f, 0.26f) });
                Draw.FillOutline(p, acc, ink, ow);
            }

            // Deck.
            float deckTop = 0.1f;
            switch (shape.Deck)
            {
                case "tiered":
                    Draw.RoundRect(p, new Rect(V(-0.4f, 0.24f), new Vector2(s * 0.8f, s * 0.14f)), s * 0.04f);
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.31f, 0.12f), new Vector2(s * 0.62f, s * 0.13f)), s * 0.04f);
                    Draw.FillOutline(p, a, ink, ow);
                    deckTop = 0.12f;
                    break;
                case "bowl":
                    p.BeginPath();
                    p.MoveTo(V(-0.38f, 0.1f));
                    p.LineTo(V(0.38f, 0.1f));
                    p.BezierCurveTo(V(0.36f, 0.34f), V(-0.36f, 0.34f), V(-0.38f, 0.1f));
                    p.ClosePath();
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.4f, 0.06f), new Vector2(s * 0.8f, s * 0.07f)), s * 0.03f);
                    Draw.FillOutline(p, a, ink, ow);
                    deckTop = 0.07f;
                    break;
                case "pedestal":
                    Draw.RoundRect(p, new Rect(V(-0.1f, 0.18f), new Vector2(s * 0.2f, s * 0.2f)), s * 0.03f);
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.22f, 0.34f), new Vector2(s * 0.44f, s * 0.07f)), s * 0.03f);
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.34f, 0.08f), new Vector2(s * 0.68f, s * 0.11f)), s * 0.04f);
                    Draw.FillOutline(p, a, ink, ow);
                    deckTop = 0.09f;
                    break;
                case "hull":
                    Draw.Poly(p, new[] { V(-0.44f, 0.08f), V(0.44f, 0.08f), V(0.32f, 0.34f), V(-0.32f, 0.34f) });
                    Draw.FillOutline(p, b, ink, ow);
                    for (int i = 0; i < 3; i++) Draw.FillCircle(p, V(-0.18f + i * 0.18f, 0.2f), s * 0.035f, acc);
                    Draw.Line(p, V(-0.42f, 0.04f), V(0.42f, 0.04f), acc, s * 0.025f);
                    deckTop = 0.08f;
                    break;
                case "rock":
                    Draw.Poly(p, new[] { V(-0.42f, 0.14f), V(-0.3f, 0.06f), V(0.1f, 0.04f), V(0.4f, 0.1f), V(0.36f, 0.3f), V(0.1f, 0.36f), V(-0.3f, 0.33f) });
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.Line(p, V(-0.2f, 0.2f), V(0.02f, 0.26f), acc, s * 0.03f);
                    Draw.Line(p, V(0.1f, 0.16f), V(0.26f, 0.24f), acc, s * 0.03f);
                    deckTop = 0.07f;
                    break;
                default:
                    Draw.RoundRect(p, new Rect(V(-0.38f, 0.14f), new Vector2(s * 0.76f, s * 0.22f)), s * 0.07f);
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.31f, 0.06f), new Vector2(s * 0.62f, s * 0.1f)), s * 0.04f);
                    Draw.FillOutline(p, a, ink, ow);
                    for (int i = 0; i < 3; i++) Draw.FillCircle(p, V(-0.16f + i * 0.16f, 0.25f), s * 0.035f, acc);
                    deckTop = 0.07f;
                    break;
            }
            if (shape.Teeth > 0)
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.3f + i * 0.15f;
                    Draw.RoundRect(p, new Rect(V(x - 0.03f, deckTop + 0.01f), new Vector2(s * 0.06f, s * 0.05f)), s * 0.01f);
                    Draw.FillOutline(p, acc, ink, 1.5f);
                }

            // Dome.
            var glass = new Color(0.8f, 0.94f, 1f, 1f);
            float baseY = deckTop;
            float top;
            switch (shape.Dome)
            {
                case "bubble":
                    Draw.Circle(p, V(0, baseY - 0.17f), s * 0.22f);
                    Draw.FillOutline(p, glass, ink, ow);
                    top = baseY - 0.39f;
                    break;
                case "onion":
                    p.BeginPath();
                    p.MoveTo(V(-0.2f, baseY));
                    p.BezierCurveTo(V(-0.34f, baseY - 0.2f), V(-0.06f, baseY - 0.3f), V(0, baseY - 0.42f));
                    p.BezierCurveTo(V(0.06f, baseY - 0.3f), V(0.34f, baseY - 0.2f), V(0.2f, baseY));
                    p.ClosePath();
                    Draw.FillOutline(p, glass, ink, ow);
                    top = baseY - 0.42f;
                    break;
                case "faceted":
                {
                    int n = Mathf.Clamp(shape.DomeSides, 4, 8);
                    var pts = new Vector2[n + 1];
                    for (int i = 0; i <= n; i++)
                    {
                        float ang = Mathf.PI * i / n;
                        pts[i] = V(-Mathf.Cos(ang) * 0.26f, baseY - Mathf.Sin(ang) * 0.3f);
                    }
                    Draw.Poly(p, pts);
                    Draw.FillOutline(p, glass, ink, ow);
                    Draw.Line(p, V(-0.08f, baseY - 0.02f), V(0, baseY - 0.3f), Color.white.WithAlpha(0.8f), s * 0.02f);
                    top = baseY - 0.3f;
                    break;
                }
                case "capsule":
                    Draw.RoundRect(p, new Rect(V(-0.15f, baseY - 0.4f), new Vector2(s * 0.3f, s * 0.4f)), s * 0.15f);
                    Draw.FillOutline(p, glass, ink, ow);
                    Draw.Line(p, V(-0.15f, baseY - 0.16f), V(0.15f, baseY - 0.16f), acc, s * 0.03f);
                    top = baseY - 0.4f;
                    break;
                case "cage":
                    p.BeginPath();
                    p.Arc(V(0, baseY), s * 0.25f, Angle.Degrees(180), Angle.Degrees(360));
                    p.ClosePath();
                    Draw.FillOutline(p, glass.WithAlpha(0.55f), ink, ow);
                    for (int i = 1; i < 4; i++)
                    {
                        float x = -0.25f + i * 0.125f;
                        float h = Mathf.Sqrt(Mathf.Max(0, 0.0625f - x * x));
                        Draw.Line(p, V(x, baseY), V(x, baseY - h), acc, s * 0.02f);
                    }
                    top = baseY - 0.25f;
                    break;
                case "pagoda":
                    p.BeginPath();
                    p.Arc(V(0, baseY), s * 0.18f, Angle.Degrees(180), Angle.Degrees(360));
                    p.ClosePath();
                    Draw.FillOutline(p, glass, ink, ow);
                    Draw.Poly(p, new[] { V(-0.34f, baseY - 0.14f), V(0, baseY - 0.3f), V(0.34f, baseY - 0.14f) });
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Poly(p, new[] { V(-0.22f, baseY - 0.28f), V(0, baseY - 0.42f), V(0.22f, baseY - 0.28f) });
                    Draw.FillOutline(p, a, ink, ow);
                    top = baseY - 0.42f;
                    break;
                case "orb":
                    Draw.RoundRect(p, new Rect(V(-0.05f, baseY - 0.1f), new Vector2(s * 0.1f, s * 0.1f)), s * 0.02f);
                    Draw.FillOutline(p, b, ink, ow);
                    Draw.Circle(p, V(0, baseY - 0.26f), s * 0.18f);
                    Draw.FillOutline(p, glass, ink, ow);
                    for (int i = 0; i < 8; i++)
                    {
                        float ang = i * Mathf.PI / 4;
                        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Draw.Line(p, V(d.x * 0.22f, baseY - 0.26f + d.y * 0.22f), V(d.x * 0.3f, baseY - 0.26f + d.y * 0.3f), acc, s * 0.03f);
                    }
                    top = baseY - 0.44f;
                    break;
                default:
                    p.BeginPath();
                    p.Arc(V(0, baseY), s * 0.25f, Angle.Degrees(180), Angle.Degrees(360));
                    p.ClosePath();
                    Draw.FillOutline(p, glass, ink, ow);
                    top = baseY - 0.25f;
                    break;
            }
            // Glass shine.
            Draw.FillCircle(p, V(-0.08f, Mathf.Lerp(baseY, top, 0.6f)), s * 0.035f, Color.white.WithAlpha(0.9f));

            // Topper.
            switch (shape.Topper)
            {
                case "spike":
                    Draw.Poly(p, new[] { V(-0.04f, top + 0.02f), V(0, top - 0.12f), V(0.04f, top + 0.02f) });
                    Draw.FillOutline(p, acc, ink, 1.5f);
                    break;
                case "cone":
                    Draw.Poly(p, new[] { V(-0.06f, top + 0.02f), V(0, top - 0.1f), V(0.06f, top + 0.02f) });
                    Draw.FillOutline(p, a, ink, 1.5f);
                    Draw.FillCircle(p, V(0, top - 0.11f), s * 0.03f, acc);
                    break;
                case "gear":
                    Draw.Star(p, V(0, top - 0.05f), s * 0.07f, s * 0.05f, 8, 0);
                    Draw.FillOutline(p, acc, ink, 1.5f);
                    break;
                case "crystal":
                    Draw.Poly(p, new[] { V(-0.04f, top + 0.01f), V(-0.02f, top - 0.1f), V(0.02f, top - 0.14f), V(0.05f, top + 0.01f) });
                    Draw.FillOutline(p, acc, ink, 1.5f);
                    break;
                case "fin":
                    Draw.Poly(p, new[] { V(-0.02f, top + 0.01f), V(0.02f, top - 0.12f), V(0.07f, top + 0.01f) });
                    Draw.FillOutline(p, acc, ink, 1.5f);
                    break;
                case "lantern":
                    Draw.RoundRect(p, new Rect(V(-0.035f, top - 0.1f), new Vector2(s * 0.07f, s * 0.09f)), s * 0.02f);
                    Draw.FillOutline(p, acc, ink, 1.5f);
                    break;
                default:
                    Draw.Line(p, V(0, top + 0.01f), V(0, top - 0.08f), ink, 2f);
                    Draw.FillCircle(p, V(0, top - 0.1f), s * 0.045f, acc);
                    break;
            }
        }

        void PaintCloud(Painter2D p, Vector2 c, float s)
        {
            var ink = Palette.Ink;
            var a = Hex(_def.Primary);
            var b = Hex(_def.Secondary);
            var acc = Hex(_def.Accent);
            Vector2 V(float x, float y) => new Vector2(c.x + x * s, c.y + y * s);
            Draw.FillCircle(p, c, s * 0.44f, acc.WithAlpha(0.22f));
            switch (_def.Style)
            {
                case "wisp":
                    for (int i = 0; i < 3; i++)
                    {
                        float y = -0.16f + i * 0.14f;
                        p.BeginPath();
                        p.MoveTo(V(-0.36f, y));
                        p.BezierCurveTo(V(-0.16f, y - 0.12f), V(0.06f, y + 0.12f), V(0.36f, y - 0.02f));
                        p.strokeColor = Color.Lerp(a, b, i * 0.3f);
                        p.lineWidth = s * 0.07f;
                        p.lineCap = LineCap.Round;
                        p.Stroke();
                    }
                    break;
                case "flame":
                    for (int i = -1; i <= 1; i++)
                    {
                        float x = i * 0.18f, h = i == 0 ? 0.42f : 0.3f;
                        p.BeginPath();
                        p.MoveTo(V(x - 0.13f, 0.22f));
                        p.BezierCurveTo(V(x - 0.16f, 0.02f), V(x - 0.02f, 0.1f - h * 0.5f), V(x, 0.22f - h));
                        p.BezierCurveTo(V(x + 0.04f, 0.1f - h * 0.4f), V(x + 0.18f, 0.02f), V(x + 0.13f, 0.22f));
                        p.ClosePath();
                        Draw.FillOutline(p, i == 0 ? a : Color.Lerp(a, acc, 0.4f), ink, 2f);
                    }
                    break;
                case "faceted":
                    Draw.Poly(p, new[] { V(-0.38f, 0.16f), V(-0.3f, -0.04f), V(-0.12f, -0.12f), V(0.02f, -0.26f), V(0.2f, -0.14f), V(0.36f, -0.04f), V(0.4f, 0.16f) });
                    Draw.FillOutline(p, a, ink, 2.2f);
                    Draw.Poly(p, new[] { V(-0.12f, -0.12f), V(0.02f, -0.26f), V(0.06f, 0.16f) });
                    p.fillColor = Color.white.WithAlpha(0.35f);
                    p.Fill();
                    break;
                case "petal":
                    for (int i = 0; i < 5; i++)
                    {
                        float ang = i * Mathf.PI * 2 / 5 - Mathf.PI / 2;
                        var d = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        Draw.Ellipse(p, V(d.x * 0.16f, d.y * 0.16f), s * 0.14f, s * 0.14f);
                        Draw.FillOutline(p, i % 2 == 0 ? a : Color.Lerp(a, acc, 0.5f), ink, 2f);
                    }
                    Draw.FillCircle(p, c, s * 0.08f, Palette.Yellow);
                    break;
                case "cubes":
                    float[,] boxes = { { -0.3f, 0.02f, 0.2f }, { -0.1f, -0.14f, 0.24f }, { 0.14f, -0.04f, 0.22f }, { -0.02f, 0.06f, 0.2f } };
                    for (int i = 0; i < 4; i++)
                    {
                        Draw.RoundRect(p, new Rect(V(boxes[i, 0], boxes[i, 1]), new Vector2(s * boxes[i, 2], s * boxes[i, 2])), s * 0.02f);
                        Draw.FillOutline(p, i % 2 == 0 ? a : Color.Lerp(a, acc, 0.35f), ink, 2f);
                    }
                    break;
                default:
                    Draw.CloudShape(p, new Vector2(c.x, c.y + s * 0.05f), s * 0.8f, Color.Lerp(a, b, 0.15f), ink, 2.5f);
                    Draw.CloudShape(p, new Vector2(c.x + s * 0.04f, c.y + s * 0.1f), s * 0.45f, b.WithAlpha(0.35f), default, 0);
                    break;
            }
        }

        void PaintBolt(Painter2D p, Vector2 c, float s)
        {
            var ink = Palette.Ink;
            var a = Hex(_def.Primary);
            var acc = Hex(_def.Accent);
            Draw.FillCircle(p, c, s * 0.42f, acc.WithAlpha(0.3f));
            void Bolt(Vector2 o, float scale, float width)
            {
                Vector2 V(float x, float y) => new Vector2(o.x + x * s * scale * width, o.y + y * s * scale);
                Draw.Poly(p, new[] { V(0.08f, -0.42f), V(-0.2f, 0.04f), V(-0.01f, 0.04f), V(-0.1f, 0.42f), V(0.22f, -0.08f), V(0.02f, -0.08f) });
                Draw.FillOutline(p, a, ink, 2.5f);
            }
            switch (_def.Style)
            {
                case "thick":
                    Bolt(c, 1f, 1.55f);
                    break;
                case "twin":
                    Bolt(c + new Vector2(-s * 0.13f, s * 0.02f), 0.85f, 1f);
                    Bolt(c + new Vector2(s * 0.14f, -s * 0.03f), 0.85f, 1f);
                    break;
                case "zigzag":
                {
                    var pts = new[]
                    {
                        new Vector2(c.x - s * 0.06f, c.y - s * 0.42f), new Vector2(c.x + s * 0.14f, c.y - s * 0.22f),
                        new Vector2(c.x - s * 0.12f, c.y - s * 0.04f), new Vector2(c.x + s * 0.14f, c.y + s * 0.14f),
                        new Vector2(c.x - s * 0.04f, c.y + s * 0.42f)
                    };
                    for (int pass = 0; pass < 2; pass++)
                    {
                        p.BeginPath();
                        p.MoveTo(pts[0]);
                        for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
                        p.strokeColor = pass == 0 ? ink : a;
                        p.lineWidth = pass == 0 ? s * 0.13f : s * 0.08f;
                        p.lineJoin = LineJoin.Miter;
                        p.lineCap = LineCap.Round;
                        p.Stroke();
                    }
                    break;
                }
                default:
                    Bolt(c, 1f, 1f);
                    break;
            }
        }

        void PaintWorld(Painter2D p, Vector2 c, float s)
        {
            var ink = Palette.Ink;
            var a = Hex(_def.Primary);
            var b = Hex(_def.Secondary);
            var acc = Hex(_def.Accent);
            var rect = new Rect(c.x - s * 0.44f, c.y - s * 0.4f, s * 0.88f, s * 0.8f);
            Vector2 V(float x, float y) => new Vector2(c.x + x * s, c.y + y * s);
            Draw.RoundRect(p, rect, s * 0.12f);
            Draw.LinearGradient(p, b, acc, new Vector2(c.x, rect.y), new Vector2(c.x, rect.yMax));
            p.Fill();
            Draw.ClearGradient(p);
            bool night = _def.Style == "night" || _def.Style == "neon" || _def.Style == "alien";
            if (night)
                for (int i = 0; i < 5; i++) Draw.FillCircle(p, V(-0.3f + i * 0.14f, -0.28f + (i % 2) * 0.08f), s * 0.018f, Color.white);
            else Draw.FillCircle(p, V(0.22f, -0.2f), s * 0.07f, new Color(1f, 0.95f, 0.7f));
            Draw.Ellipse(p, V(0, 0.2f), s * 0.36f, s * 0.12f);
            Draw.FillOutline(p, a, ink, 2.5f);
            switch (_def.Style)
            {
                case "desert":
                    Draw.RoundRect(p, new Rect(V(-0.2f, -0.04f), new Vector2(s * 0.07f, s * 0.24f)), s * 0.035f);
                    Draw.FillOutline(p, Palette.Hex(0x4AA23A), ink, 1.8f);
                    Draw.RoundRect(p, new Rect(V(-0.26f, 0.02f), new Vector2(s * 0.06f, s * 0.1f)), s * 0.03f);
                    Draw.FillOutline(p, Palette.Hex(0x4AA23A), ink, 1.8f);
                    break;
                case "snow":
                    Draw.Poly(p, new[] { V(-0.26f, 0.18f), V(-0.17f, -0.1f), V(-0.08f, 0.18f) });
                    Draw.FillOutline(p, Palette.Hex(0x2E7D5B), ink, 1.8f);
                    Draw.Poly(p, new[] { V(0.08f, 0.18f), V(0.15f, -0.02f), V(0.22f, 0.18f) });
                    Draw.FillOutline(p, Palette.Hex(0x2E7D5B), ink, 1.8f);
                    break;
                case "tropic":
                    Draw.Line(p, V(-0.14f, 0.16f), V(-0.1f, -0.1f), Palette.Hex(0x8A5A2B), s * 0.04f);
                    Draw.Ellipse(p, V(-0.1f, -0.12f), s * 0.12f, s * 0.04f);
                    Draw.FillOutline(p, Palette.Hex(0x3DBE6B), ink, 1.8f);
                    break;
                case "alien":
                    Draw.Poly(p, new[] { V(-0.2f, 0.18f), V(-0.16f, -0.08f), V(-0.1f, 0.18f) });
                    Draw.FillOutline(p, acc, ink, 1.8f);
                    Draw.Poly(p, new[] { V(0.1f, 0.18f), V(0.15f, -0.02f), V(0.2f, 0.18f) });
                    Draw.FillOutline(p, Color.Lerp(acc, Color.white, 0.3f), ink, 1.8f);
                    break;
                case "volcano":
                    Draw.Poly(p, new[] { V(-0.26f, 0.18f), V(-0.06f, -0.16f), V(0.06f, -0.16f), V(0.26f, 0.18f) });
                    Draw.FillOutline(p, Palette.Hex(0x3A2A26), ink, 1.8f);
                    Draw.Ellipse(p, V(0, -0.16f), s * 0.06f, s * 0.02f);
                    p.fillColor = acc;
                    p.Fill();
                    Draw.Line(p, V(0.02f, -0.14f), V(0.06f, 0.04f), acc, s * 0.03f);
                    break;
                case "sakura":
                    Draw.Line(p, V(-0.12f, 0.16f), V(-0.12f, -0.02f), Palette.Hex(0x5A3A2A), s * 0.04f);
                    Draw.FillCircle(p, V(-0.12f, -0.08f), s * 0.12f, Palette.Hex(0xFFB7D0));
                    Draw.FillCircle(p, V(-0.02f, -0.02f), s * 0.07f, Palette.Hex(0xF48FB1));
                    break;
                case "neon":
                    for (int i = 0; i < 4; i++) Draw.Line(p, V(-0.28f + i * 0.18f, 0.14f), V(-0.2f + i * 0.13f, 0.26f), acc, s * 0.015f);
                    Draw.Line(p, V(-0.32f, 0.2f), V(0.32f, 0.2f), acc, s * 0.015f);
                    break;
                case "night":
                    Draw.FillCircle(p, V(0.22f, -0.2f), s * 0.07f, new Color(1f, 0.97f, 0.85f));
                    Draw.FillCircle(p, V(0.25f, -0.22f), s * 0.06f, b);
                    break;
                default:
                    Draw.Line(p, V(-0.16f, 0.16f), V(-0.16f, 0.02f), Palette.Hex(0x8A5A2B), s * 0.04f);
                    Draw.FillCircle(p, V(-0.16f, -0.02f), s * 0.09f, Palette.Hex(0x3DBE6B));
                    break;
            }
            Draw.RoundRect(p, rect, s * 0.12f);
            p.strokeColor = ink;
            p.lineWidth = 2.5f;
            p.Stroke();
        }

        void PaintTitle(Painter2D p, Vector2 c, float s)
        {
            var color = _def.RequiresDiscovery != null ? Palette.ForPheno(_def.RequiresDiscovery) : _def.Primary != 0 ? Hex(_def.Primary) : Palette.Yellow;
            if (_def.Exclusive) Draw.FillCircle(p, c, s * 0.44f, Palette.Yellow.WithAlpha(0.3f));
            Draw.Star(p, c, s * 0.34f, s * 0.15f, 5, 0);
            Draw.FillOutline(p, color, Palette.Ink, 2.5f);
        }
    }

    /// <summary>Painted preview of an island decoration.</summary>
    public sealed class DecorSwatch : VisualElement
    {
        readonly DecorationDef _def;

        public DecorSwatch(DecorationDef def)
        {
            _def = def;
            AddToClassList("se-decorswatch");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 4) return;
            var p = ctx.painter2D;
            float s = Mathf.Min(r.width, r.height);
            var c = r.center;
            var ink = Palette.Ink;
            var a = Palette.Hex(_def.Primary);
            var acc = Palette.Hex(_def.Accent);
            Vector2 V(float x, float y) => new Vector2(c.x + x * s, c.y + y * s);
            const float ow = 2f;

            // Backdrop by kind.
            var box = new Rect(c.x - s * 0.46f, c.y - s * 0.46f, s * 0.92f, s * 0.92f);
            Draw.RoundRect(p, box, s * 0.18f);
            var top = _def.Kind == DecorKind.Air ? Palette.Hex(0x9FD8FF) : _def.Kind == DecorKind.Water ? Palette.Hex(0xBFEFFF) : Palette.Hex(0xDDF3FF);
            Draw.LinearGradient(p, top, top.Lighten(0.5f), new Vector2(c.x, box.y), new Vector2(c.x, box.yMax));
            p.Fill();
            Draw.ClearGradient(p);
            if (_def.Kind == DecorKind.Water)
            {
                Draw.Ellipse(p, V(0, 0.22f), s * 0.38f, s * 0.13f);
                Draw.FillOutline(p, Palette.Hex(0x35C0E6), ink, ow);
            }
            else if (_def.Kind == DecorKind.Land)
            {
                Draw.Ellipse(p, V(0, 0.26f), s * 0.36f, s * 0.1f);
                p.fillColor = Palette.Grass;
                p.Fill();
            }
            else
            {
                Draw.CloudShape(p, V(-0.16f, 0.26f), s * 0.36f, Color.white.WithAlpha(0.9f), default, 0);
            }

            switch (_def.Model)
            {
                case "flowerbed":
                    for (int i = 0; i < 4; i++)
                    {
                        var at = V(-0.2f + i * 0.13f, 0.12f - (i % 2) * 0.06f);
                        Draw.Line(p, at, at + new Vector2(0, s * 0.12f), Palette.GrassDark, s * 0.025f);
                        Draw.FillCircle(p, at, s * 0.06f, i % 2 == 0 ? a : acc);
                    }
                    break;
                case "bench":
                    Draw.RoundRect(p, new Rect(V(-0.26f, 0.04f), new Vector2(s * 0.52f, s * 0.07f)), s * 0.02f);
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.26f, -0.1f), new Vector2(s * 0.52f, s * 0.07f)), s * 0.02f);
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Line(p, V(-0.2f, 0.1f), V(-0.2f, 0.22f), acc, s * 0.04f);
                    Draw.Line(p, V(0.2f, 0.1f), V(0.2f, 0.22f), acc, s * 0.04f);
                    break;
                case "lamppost":
                case "toro":
                case "beacon":
                case "vane":
                    Draw.Line(p, V(0, 0.24f), V(0, -0.14f), a, s * 0.06f);
                    Draw.RoundRect(p, new Rect(V(-0.08f, -0.3f), new Vector2(s * 0.16f, s * 0.16f)), s * 0.05f);
                    Draw.FillOutline(p, acc, ink, ow);
                    if (_def.Model == "vane") Draw.Poly(p, new[] { V(-0.2f, -0.34f), V(0.2f, -0.34f), V(0.12f, -0.4f) });
                    break;
                case "gnome":
                    Draw.FillCircle(p, V(0, 0.08f), s * 0.14f, acc);
                    Draw.FillCircle(p, V(0, -0.04f), s * 0.08f, Palette.Hex(0xFFD7B0));
                    Draw.Poly(p, new[] { V(-0.12f, -0.08f), V(0, -0.38f), V(0.12f, -0.08f) });
                    Draw.FillOutline(p, a, ink, ow);
                    break;
                case "kite":
                    Draw.Poly(p, new[] { V(0, -0.36f), V(0.2f, -0.1f), V(0, 0.14f), V(-0.2f, -0.1f) });
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Line(p, V(0, 0.14f), V(0.12f, 0.34f), acc, s * 0.025f);
                    break;
                case "balloon":
                case "lanterns":
                    Draw.FillCircle(p, V(0, -0.1f), s * 0.22f, a);
                    Draw.Circle(p, V(0, -0.1f), s * 0.22f);
                    p.strokeColor = ink;
                    p.lineWidth = ow;
                    p.Stroke();
                    Draw.Line(p, V(-0.12f, 0.02f), V(0.12f, 0.02f), acc, s * 0.04f);
                    Draw.RoundRect(p, new Rect(V(-0.06f, 0.16f), new Vector2(s * 0.12f, s * 0.08f)), s * 0.02f);
                    Draw.FillOutline(p, Palette.Hex(0x8A5A2B), ink, ow);
                    break;
                case "blimp":
                case "whale":
                    Draw.Ellipse(p, V(0, -0.08f), s * 0.32f, s * 0.15f);
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Poly(p, new[] { V(0.28f, -0.08f), V(0.42f, -0.2f), V(0.42f, 0.04f) });
                    Draw.FillOutline(p, acc, ink, ow);
                    Draw.FillCircle(p, V(-0.18f, -0.12f), s * 0.025f, ink);
                    break;
                case "pond":
                case "lavapool":
                    Draw.Ellipse(p, V(0, 0.1f), s * 0.32f, s * 0.14f);
                    Draw.FillOutline(p, acc, ink, ow);
                    Draw.Ellipse(p, V(0, 0.1f), s * 0.24f, s * 0.09f);
                    p.fillColor = a;
                    p.Fill();
                    break;
                case "lilypads":
                case "koi":
                case "ducks":
                case "sailboat":
                    Draw.Ellipse(p, V(0, 0.14f), s * 0.2f, s * 0.08f);
                    Draw.FillOutline(p, a, ink, ow);
                    if (_def.Model == "sailboat")
                    {
                        Draw.Poly(p, new[] { V(0, 0.06f), V(0, -0.3f), V(0.18f, 0.06f) });
                        Draw.FillOutline(p, acc, ink, ow);
                    }
                    else Draw.FillCircle(p, V(0.08f, 0.02f), s * 0.07f, acc);
                    break;
                case "windmill":
                    Draw.Poly(p, new[] { V(-0.12f, 0.24f), V(-0.06f, -0.12f), V(0.06f, -0.12f), V(0.12f, 0.24f) });
                    Draw.FillOutline(p, a, ink, ow);
                    for (int i = 0; i < 4; i++)
                    {
                        float ang = i * Mathf.PI / 2 + 0.4f;
                        Draw.Line(p, V(0, -0.14f), V(Mathf.Cos(ang) * 0.24f, -0.14f + Mathf.Sin(ang) * 0.24f), acc, s * 0.05f);
                    }
                    break;
                case "arch":
                    Draw.ArcStroke(p, V(0, 0.22f), s * 0.3f, 180, 360, a, s * 0.07f);
                    Draw.ArcStroke(p, V(0, 0.22f), s * 0.22f, 180, 360, acc, s * 0.07f);
                    break;
                case "snowman":
                    Draw.FillCircle(p, V(0, 0.12f), s * 0.15f, a);
                    Draw.FillCircle(p, V(0, -0.1f), s * 0.1f, a);
                    Draw.Poly(p, new[] { V(0.02f, -0.1f), V(0.14f, -0.08f), V(0.02f, -0.06f) });
                    p.fillColor = acc;
                    p.Fill();
                    break;
                case "pumpkin":
                    Draw.Ellipse(p, V(0, 0.06f), s * 0.24f, s * 0.18f);
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Poly(p, new[] { V(-0.1f, 0.02f), V(-0.04f, -0.04f), V(-0.02f, 0.04f) });
                    p.fillColor = acc;
                    p.Fill();
                    Draw.Poly(p, new[] { V(0.1f, 0.02f), V(0.04f, -0.04f), V(0.02f, 0.04f) });
                    p.Fill();
                    break;
                case "crystal":
                case "obsidian":
                case "totem":
                case "neonsign":
                    Draw.Poly(p, new[] { V(-0.16f, 0.22f), V(-0.12f, -0.12f), V(-0.04f, -0.3f), V(0.04f, -0.12f), V(0.04f, 0.22f) });
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Poly(p, new[] { V(0.04f, 0.22f), V(0.1f, -0.04f), V(0.18f, 0.22f) });
                    Draw.FillOutline(p, acc, ink, ow);
                    break;
                case "statue":
                    Draw.RoundRect(p, new Rect(V(-0.16f, 0.12f), new Vector2(s * 0.32f, s * 0.12f)), s * 0.02f);
                    Draw.FillOutline(p, a.Darken(0.15f), ink, ow);
                    Draw.RoundRect(p, new Rect(V(-0.08f, -0.2f), new Vector2(s * 0.16f, s * 0.32f)), s * 0.06f);
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Poly(p, new[] { V(0.12f, -0.36f), V(0.04f, -0.2f), V(0.1f, -0.2f), V(0.06f, -0.08f), V(0.18f, -0.26f), V(0.12f, -0.26f) });
                    Draw.FillOutline(p, acc, ink, 1.5f);
                    break;
                case "fountain":
                    Draw.RoundRect(p, new Rect(V(-0.2f, 0.04f), new Vector2(s * 0.4f, s * 0.12f)), s * 0.04f);
                    Draw.FillOutline(p, a, ink, ow);
                    Draw.Line(p, V(0, 0.04f), V(0, -0.2f), a, s * 0.05f);
                    Draw.ArcStroke(p, V(0, -0.2f), s * 0.14f, 200, 340, acc, s * 0.04f);
                    break;
                default:
                    // Trees and garden shapes.
                    Draw.Line(p, V(0, 0.24f), V(0, 0.02f), Palette.Hex(0x8A5A2B), s * 0.06f);
                    Draw.FillCircle(p, V(0, -0.08f), s * 0.2f, a);
                    Draw.Circle(p, V(0, -0.08f), s * 0.2f);
                    p.strokeColor = ink;
                    p.lineWidth = ow;
                    p.Stroke();
                    Draw.FillCircle(p, V(0.06f, -0.14f), s * 0.05f, acc);
                    break;
            }

            Draw.RoundRect(p, box, s * 0.18f);
            p.strokeColor = ink;
            p.lineWidth = 2.5f;
            p.Stroke();
        }
    }
}
