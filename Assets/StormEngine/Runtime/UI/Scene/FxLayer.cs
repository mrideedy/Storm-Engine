using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Screen-space effects: rain, lightning flashes, floating numbers and sparkles.</summary>
    public sealed class FxLayer : VisualElement
    {
        struct Drop
        {
            public Vector2 Pos;
            public float Speed;
            public float Len;
        }

        struct Popup
        {
            public Label Label;
            public Vector2 Start;
            public float Age;
            public float Life;
            public float Rise;
        }

        struct Spark
        {
            public Vector2 Pos;
            public Vector2 Vel;
            public float Age;
            public float Life;
            public Color Color;
            public float Size;
        }

        readonly VisualElement _rain;
        readonly VisualElement _flash;
        readonly VisualElement _sparksLayer;
        readonly List<Drop> _drops = new List<Drop>();
        readonly List<Popup> _popups = new List<Popup>();
        readonly Stack<Label> _labelPool = new Stack<Label>();
        readonly List<Spark> _sparks = new List<Spark>();
        readonly System.Random _rng = new System.Random(3);
        float _rainAmount;
        float _flashAlpha;

        public bool ReduceMotion;

        public FxLayer()
        {
            AddToClassList("se-layer");
            pickingMode = PickingMode.Ignore;

            _rain = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_rain);
            _rain.generateVisualContent += PaintRain;
            Add(_rain);

            _sparksLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            Ui.Fill(_sparksLayer);
            _sparksLayer.generateVisualContent += PaintSparks;
            Add(_sparksLayer);

            _flash = Ui.Div("se-flash");
            _flash.pickingMode = PickingMode.Ignore;
            Add(_flash);
        }

        public float Rain
        {
            get => _rainAmount;
            set => _rainAmount = Mathf.Clamp01(value);
        }

        public void Flash(float strength)
        {
            if (ReduceMotion) strength *= 0.3f;
            _flashAlpha = Mathf.Max(_flashAlpha, Mathf.Clamp01(strength));
        }

        /// <summary>Spawns floating text at a panel-space position (relative to this layer).</summary>
        public void PopText(string text, Vector2 at, string cls = null, float life = 1.1f)
        {
            if (_popups.Count > 24) return;
            var l = _labelPool.Count > 0 ? _labelPool.Pop() : new Label();
            l.ClearClassList();
            l.AddToClassList("se-popnum");
            if (!string.IsNullOrEmpty(cls)) l.AddToClassList(cls);
            l.text = text;
            l.pickingMode = PickingMode.Ignore;
            l.style.opacity = 1;
            l.style.left = at.x - 100;
            l.style.top = at.y - 20;
            l.style.width = 200;
            Add(l);
            _popups.Add(new Popup { Label = l, Start = at, Age = 0, Life = life, Rise = 70 + (float)_rng.NextDouble() * 30 });
        }

        public void Burst(Vector2 at, Color color, int count = 14, float speed = 220)
        {
            if (ReduceMotion) count /= 3;
            for (int i = 0; i < count && _sparks.Count < 160; i++)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2;
                float v = speed * (0.4f + (float)_rng.NextDouble() * 0.8f);
                _sparks.Add(new Spark
                {
                    Pos = at,
                    Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * v,
                    Life = 0.5f + (float)_rng.NextDouble() * 0.4f,
                    Color = color,
                    Size = 3 + (float)_rng.NextDouble() * 4
                });
            }
        }

        public void Tick(float dt)
        {
            var r = contentRect;

            // Rain drops.
            int target = Mathf.RoundToInt(_rainAmount * (ReduceMotion ? 40 : 110));
            while (_drops.Count < target) _drops.Add(NewDrop(r, true));
            if (_drops.Count > target) _drops.RemoveRange(target, _drops.Count - target);
            for (int i = 0; i < _drops.Count; i++)
            {
                var d = _drops[i];
                d.Pos += new Vector2(-0.25f, 1f) * d.Speed * dt;
                if (d.Pos.y > r.height + 20) d = NewDrop(r, false);
                _drops[i] = d;
            }
            if (_drops.Count > 0 || _rain.userData != null)
            {
                _rain.userData = _drops.Count > 0 ? (object)true : null;
                _rain.MarkDirtyRepaint();
            }

            // Floating numbers.
            for (int i = _popups.Count - 1; i >= 0; i--)
            {
                var p = _popups[i];
                p.Age += dt;
                float t = p.Age / p.Life;
                if (t >= 1)
                {
                    p.Label.RemoveFromHierarchy();
                    _labelPool.Push(p.Label);
                    _popups.RemoveAt(i);
                    continue;
                }
                float ease = 1 - (1 - t) * (1 - t);
                p.Label.style.translate = new Translate(0, -p.Rise * ease);
                p.Label.style.opacity = t < 0.7f ? 1 : 1 - (t - 0.7f) / 0.3f;
                float pop = t < 0.12f ? Mathf.Lerp(0.6f, 1.15f, t / 0.12f) : Mathf.Lerp(1.15f, 1f, Mathf.Min(1, (t - 0.12f) / 0.2f));
                p.Label.style.scale = new Scale(new Vector3(pop, pop, 1));
                _popups[i] = p;
            }

            // Sparks.
            for (int i = _sparks.Count - 1; i >= 0; i--)
            {
                var s = _sparks[i];
                s.Age += dt;
                if (s.Age >= s.Life)
                {
                    _sparks.RemoveAt(i);
                    continue;
                }
                s.Vel += new Vector2(0, 380) * dt;
                s.Pos += s.Vel * dt;
                _sparks[i] = s;
            }
            if (_sparks.Count > 0 || _sparksLayer.userData != null)
            {
                _sparksLayer.userData = _sparks.Count > 0 ? (object)true : null;
                _sparksLayer.MarkDirtyRepaint();
            }

            // Lightning flash.
            if (_flashAlpha > 0)
            {
                _flashAlpha = Mathf.Max(0, _flashAlpha - dt * 3.5f);
                _flash.style.backgroundColor = new Color(1f, 1f, 0.95f, _flashAlpha * 0.55f);
            }
        }

        Drop NewDrop(Rect r, bool anywhere)
        {
            return new Drop
            {
                Pos = new Vector2((float)_rng.NextDouble() * (r.width + 100), anywhere ? (float)_rng.NextDouble() * r.height : -20 - (float)_rng.NextDouble() * 60),
                Speed = 700 + (float)_rng.NextDouble() * 400,
                Len = 12 + (float)_rng.NextDouble() * 14
            };
        }

        void PaintRain(MeshGenerationContext ctx)
        {
            if (_drops.Count == 0) return;
            var p = ctx.painter2D;
            p.strokeColor = new Color(0.78f, 0.88f, 1f, 0.55f);
            p.lineWidth = 2f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            foreach (var d in _drops)
            {
                p.MoveTo(d.Pos);
                p.LineTo(d.Pos + new Vector2(-0.25f, 1f) * d.Len);
            }
            p.Stroke();
        }

        void PaintSparks(MeshGenerationContext ctx)
        {
            if (_sparks.Count == 0) return;
            var p = ctx.painter2D;
            foreach (var s in _sparks)
            {
                float a = 1 - s.Age / s.Life;
                Draw.Star(p, s.Pos, s.Size * 1.6f, s.Size * 0.6f, 4, s.Age * 400);
                p.fillColor = s.Color.WithAlpha(a);
                p.Fill();
            }
        }
    }
}
