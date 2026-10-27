using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine.World
{
    /// <summary>Marks a decoration's tap target; Index points into the placed list.</summary>
    public sealed class DecorTag : MonoBehaviour
    {
        public int Index;
    }

    /// <summary>Draws and animates placed decorations and finds free spots for new ones.</summary>
    public sealed class DecorRig
    {
        readonly Transform _root;
        readonly Content _c;
        readonly List<Item> _items = new List<Item>();
        float _radius = 9.5f;
        float _time;
        long _signature = long.MinValue;
        int _selected = -1;

        sealed class Item
        {
            public GameObject Go;
            public DecorationDef Def;
            public PlacedDecor Place;
            public readonly List<DecorAnim> Anims = new List<DecorAnim>();
            public float Pop = 1;
            public Vector3 BaseScale = Vector3.one;
        }

        static readonly Vector2[] WaterOffsets = { new Vector2(0, 0), new Vector2(0.45f, 0.28f), new Vector2(-0.42f, -0.3f) };

        public DecorRig(Transform parent, Content c)
        {
            _c = c;
            _root = new GameObject("Decorations").transform;
            _root.SetParent(parent, false);
        }

        public int Count => _items.Count;

        /// <summary>Rebuilds when the placed list or island size changed. Cheap to call every frame.</summary>
        public void Sync(IList<PlacedDecor> placed, float radius)
        {
            long sig = Signature(placed, radius);
            if (sig == _signature) return;
            int before = _items.Count;
            _signature = sig;
            Rebuild(placed, radius);
            // Newly added decorations pop in.
            for (int i = before; i < _items.Count; i++) _items[i].Pop = 0.1f;
        }

        /// <summary>Allocation-free fingerprint of the layout (ids, rounded positions and rotations).</summary>
        static long Signature(IList<PlacedDecor> placed, float radius)
        {
            unchecked
            {
                long h = 1469598103934665603L ^ Mathf.RoundToInt(radius * 10);
                h = h * 1099511628211L + placed.Count;
                foreach (var p in placed)
                {
                    h = h * 1099511628211L + (p.Id?.GetHashCode() ?? 0);
                    h = h * 1099511628211L + Mathf.RoundToInt(p.X * 100);
                    h = h * 1099511628211L + Mathf.RoundToInt(p.Z * 100);
                    h = h * 1099511628211L + Mathf.RoundToInt(p.Rot);
                }
                return h;
            }
        }

        void Rebuild(IList<PlacedDecor> placed, float radius)
        {
            _radius = radius;
            foreach (var item in _items) Object.Destroy(item.Go);
            _items.Clear();
            for (int i = 0; i < placed.Count; i++)
            {
                int d = _c.DecorationIndex(placed[i].Id);
                if (d < 0) continue;
                var def = _c.Decorations[d];
                var item = new Item { Def = def, Place = placed[i] };
                item.Go = DecorModels.Build(def.Model, _root, Hex(def.Primary), Hex(def.Accent), item.Anims);
                item.Go.transform.localPosition = WorldPosition(def, placed[i]);
                item.Go.transform.localRotation = Quaternion.Euler(0, placed[i].Rot, 0);
                var col = item.Go.AddComponent<SphereCollider>();
                float fp = DecorModels.Footprint(def.Model);
                col.radius = Mathf.Max(0.45f, fp);
                col.center = new Vector3(0, def.Kind == DecorKind.Air ? 0.4f : Mathf.Max(0.35f, fp * 0.6f), 0);
                item.Go.AddComponent<DecorTag>().Index = i;
                _items.Add(item);
            }
            if (_selected >= _items.Count) _selected = -1;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public Vector3 WorldPosition(DecorationDef def, PlacedDecor p)
        {
            float ground = MeshKit.IslandHeight(p.X, p.Z, _radius);
            switch (def.Kind)
            {
                case DecorKind.Air: return new Vector3(p.X, ground + DecorModels.AirHeight(def.Model), p.Z);
                case DecorKind.Water: return new Vector3(p.X, ground + 0.05f, p.Z);
                default: return new Vector3(p.X, ground - 0.02f, p.Z);
            }
        }

        public Vector3 WorldPosition(int index) =>
            index >= 0 && index < _items.Count ? _items[index].Go.transform.position : _root.position;

        public void Select(int index)
        {
            _selected = index >= 0 && index < _items.Count ? index : -1;
        }

        public int Selected => _selected;

        public void Tick(float dt, float wind, float storm)
        {
            _time += dt;
            float windy = 1 + wind * 2f + storm * 3f;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Go == null) continue;
                if (item.Pop < 1)
                {
                    item.Pop = Mathf.Min(1, item.Pop + dt * 2.5f);
                    // Ease-out-back: grows in with a little bounce.
                    float t = item.Pop - 1;
                    float s = 1 + 2.70158f * t * t * t + 1.70158f * t * t;
                    item.Go.transform.localScale = Vector3.one * Mathf.Max(0.05f, s);
                }
                else
                {
                    float s = i == _selected ? 1f + 0.08f * Mathf.Sin(_time * 8f) : 1f;
                    item.Go.transform.localScale = Vector3.one * s;
                }
                foreach (var a in item.Anims)
                {
                    if (a.T == null) continue;
                    float speed = a.Windy ? a.Speed * windy : a.Speed;
                    switch (a.Type)
                    {
                        case DecorAnim.Kind.Spin:
                            a.T.Rotate(a.Axis, speed * dt, Space.Self);
                            break;
                        case DecorAnim.Kind.Bob:
                            a.T.localPosition = a.Base + Vector3.up * Mathf.Sin(_time * speed * Mathf.PI * 2 * 0.5f + a.Phase) * a.Amount;
                            break;
                        case DecorAnim.Kind.Sway:
                            a.T.localRotation = Quaternion.AngleAxis(Mathf.Sin(_time * speed + a.Phase) * a.Amount * (a.Windy ? Mathf.Min(2f, windy * 0.6f) : 1f), a.Axis);
                            break;
                        case DecorAnim.Kind.Orbit:
                        {
                            float ang = (a.Phase + _time * speed) * Mathf.Deg2Rad;
                            a.T.localPosition = a.Base + new Vector3(Mathf.Cos(ang) * a.Amount, Mathf.Sin(_time * 0.7f + a.Phase) * 0.05f * a.Amount, Mathf.Sin(ang) * a.Amount);
                            a.T.localRotation = Quaternion.Euler(0, -ang * Mathf.Rad2Deg + (speed >= 0 ? 0 : 180), 0);
                            break;
                        }
                        case DecorAnim.Kind.Pulse:
                            a.T.localScale = a.Base * (1 + Mathf.Sin(_time * speed + a.Phase) * a.Amount);
                            break;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Finds a free spot for a new decoration. Water items go into a pond with room.</summary>
        public static bool FindSpot(Content c, DecorationDef def, IList<PlacedDecor> placed, float radius, IList<Vector2> props, int seed, out Vector2 spot)
        {
            spot = default;
            if (def.Kind == DecorKind.Water)
            {
                foreach (var pond in placed)
                {
                    if (pond.Id != "dec_pond") continue;
                    var center = new Vector2(pond.X, pond.Z);
                    foreach (var off in WaterOffsets)
                    {
                        var candidate = center + off;
                        bool taken = false;
                        foreach (var p in placed)
                        {
                            int i = c.DecorationIndex(p.Id);
                            if (i < 0 || c.Decorations[i].Kind != DecorKind.Water) continue;
                            if ((new Vector2(p.X, p.Z) - candidate).sqrMagnitude < 0.09f) taken = true;
                        }
                        if (taken) continue;
                        spot = candidate;
                        return true;
                    }
                }
                return false;
            }

            var rng = new System.Random(seed);
            float fp = DecorModels.Footprint(def.Model);
            for (int attempt = 0; attempt < 400; attempt++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                float r;
                if (def.Kind == DecorKind.Air) r = Mathf.Lerp(radius * 0.35f, radius * 0.95f, (float)rng.NextDouble());
                else r = Mathf.Lerp(3.3f + fp, radius - 0.8f - fp * 0.5f, Mathf.Sqrt((float)rng.NextDouble()));
                var candidate = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (IsSpotFree(c, def, candidate, placed, radius, props, -1)) { spot = candidate; return true; }
            }
            return false;
        }

        /// <summary>True when a decoration fits at a spot (ignoring the entry at <paramref name="ignore"/>).</summary>
        public static bool IsSpotFree(Content c, DecorationDef def, Vector2 spot, IList<PlacedDecor> placed, float radius, IList<Vector2> props, int ignore)
        {
            float fp = DecorModels.Footprint(def.Model);
            float r = spot.magnitude;
            if (def.Kind == DecorKind.Air)
            {
                if (r > radius + 1f) return false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if (i == ignore) continue;
                    int d = c.DecorationIndex(placed[i].Id);
                    if (d < 0 || c.Decorations[d].Kind != DecorKind.Air) continue;
                    if ((new Vector2(placed[i].X, placed[i].Z) - spot).sqrMagnitude < 9f) return false;
                }
                return true;
            }
            if (r < 3.2f + fp || r > radius - 0.6f - fp * 0.4f) return false;
            foreach (var slot in MachineRig.Slots)
                if ((slot - spot).sqrMagnitude < (1.45f + fp) * (1.45f + fp)) return false;
            // Keep the pipes from the generators to the platform clear.
            foreach (var slot in MachineRig.Slots)
            {
                var dir = -slot.normalized;
                var start = slot + dir * 0.75f;
                var end = -dir * 2.45f;
                if (DistanceToSegment(spot, start, end) < 0.35f + fp * 0.6f) return false;
            }
            if (props != null)
                foreach (var p in props)
                    if ((p - spot).sqrMagnitude < (0.55f + fp) * (0.55f + fp)) return false;
            for (int i = 0; i < placed.Count; i++)
            {
                if (i == ignore) continue;
                int d = c.DecorationIndex(placed[i].Id);
                if (d < 0) continue;
                var other = c.Decorations[d];
                if (other.Kind != DecorKind.Land) continue;
                float gap = fp + DecorModels.Footprint(other.Model) + 0.1f;
                if ((new Vector2(placed[i].X, placed[i].Z) - spot).sqrMagnitude < gap * gap) return false;
            }
            return true;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
