using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormEngine.World
{
    /// <summary>
    /// Quality-dependent detail on the island. High: dense swaying grass and wildflowers, ambient
    /// particles (pollen, fireflies, snow, embers, petals, sea sparkles) and rain splashes. Balanced:
    /// about half of that. Simple: none of it, but soft blob shadows so objects still sit on the ground.
    /// </summary>
    public sealed class DetailRig
    {
        readonly Transform _root;
        readonly Content _c;
        // Grass/blobs and particles are owned separately: moving a decoration only rebuilds the grass.
        readonly List<Object> _grassOwned = new List<Object>();
        readonly List<Object> _fxOwned = new List<Object>();
        List<Object> _owned;
        readonly List<Vector2> _decorSpots = new List<Vector2>();
        readonly List<float> _decorSizes = new List<float>();
        readonly List<Vector3> _trees = new List<Vector3>();
        readonly bool[] _slots = new bool[MachineRig.Slots.Length];
        IList<Vector2> _props;
        GameObject _grass, _glowBits, _blobs;
        ParticleSystem _ambient, _extra, _splashes;
        Theme _theme;
        float _radius = EnvironmentRig.BaseRadius;
        int _quality = 2;
        long _decorSig = long.MinValue;
        bool _blobsDirty = true;
        float _storm;

        public DetailRig(Transform parent, Content c)
        {
            _c = c;
            _root = new GameObject("Details").transform;
            _root.SetParent(parent, false);
        }

        public int Quality => _quality;

        /// <summary>Rebuilds everything for a world (after the environment was rebuilt).</summary>
        public void Build(Theme theme, float radius, IList<PlacedDecor> decor, IList<Vector2> props, IList<Vector3> trees)
        {
            _theme = theme;
            _radius = radius;
            _props = props;
            _trees.Clear();
            if (trees != null) _trees.AddRange(trees);
            ReadDecor(decor);
            _decorSig = Signature(decor);
            RebuildGrass();
            RebuildParticles();
        }

        public void SetQuality(int quality)
        {
            quality = Mathf.Clamp(quality, 0, 2);
            if (quality == _quality) return;
            _quality = quality;
            RebuildGrass();
            RebuildParticles();
        }

        /// <summary>Cheap per-frame check: decorations moved, bought or stored.</summary>
        public void SyncDecor(IList<PlacedDecor> decor)
        {
            long sig = Signature(decor);
            if (sig == _decorSig) return;
            _decorSig = sig;
            ReadDecor(decor);
            // Grass would poke through new decorations, so it is rebuilt too (cheap).
            RebuildGrass();
        }

        /// <summary>Which generator slots currently hold a built machine (for blob shadows).</summary>
        public void SetMachines(int[] levels, string[] ids)
        {
            bool changed = false;
            for (int i = 0; i < ids.Length && i < levels.Length; i++)
            {
                int slot = MachineRig.SlotOf(ids[i]);
                if (slot < 0 || slot >= _slots.Length) continue;
                bool built = levels[i] > 0;
                if (_slots[slot] == built) continue;
                _slots[slot] = built;
                changed = true;
            }
            if (changed && _quality == 0) BuildBlobs();
        }

        public void Tick(float dt, float storm)
        {
            _storm = storm;
            if (_splashes != null)
            {
                var emission = _splashes.emission;
                emission.rateOverTime = storm > 0.3f ? 220f * storm : 0f;
            }
            if (_ambient != null)
            {
                // Petals, pollen and fireflies hide from the storm.
                var emission = _ambient.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(1f, 0.15f, storm);
            }
            if (_blobsDirty && _quality == 0) BuildBlobs();
        }

        // ------------------------------------------------------------------ build

        void RebuildGrass()
        {
            Clear(_grassOwned);
            _grass = _glowBits = _blobs = null;
            if (_theme == null) return;
            _owned = _grassOwned;
            if (_quality >= 1) BuildGrass(_quality >= 2 ? 1f : 0.5f);
            if (_quality == 0) BuildBlobs();
        }

        void RebuildParticles()
        {
            Clear(_fxOwned);
            _ambient = _extra = _splashes = null;
            if (_theme == null) return;
            _owned = _fxOwned;
            if (_quality >= 1) BuildParticles(_quality >= 2 ? 1f : 0.5f);
            if (_quality >= 2) BuildSplashes();
        }

        static void Clear(List<Object> owned)
        {
            foreach (var o in owned)
            {
                if (o == null) continue;
                if (o is GameObject go)
                {
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Object.Destroy(mf.sharedMesh);
                    Object.Destroy(go);
                }
                else Object.Destroy(o);
            }
            owned.Clear();
        }

        void ReadDecor(IList<PlacedDecor> decor)
        {
            _decorSpots.Clear();
            _decorSizes.Clear();
            _blobsDirty = true;
            if (decor == null) return;
            foreach (var p in decor)
            {
                int d = _c.DecorationIndex(p.Id);
                if (d < 0 || _c.Decorations[d].Kind == DecorKind.Air) continue;
                _decorSpots.Add(new Vector2(p.X, p.Z));
                _decorSizes.Add(DecorModels.Footprint(_c.Decorations[d].Model));
            }
        }

        static long Signature(IList<PlacedDecor> decor)
        {
            unchecked
            {
                long h = 17 + (decor?.Count ?? 0);
                if (decor != null)
                    foreach (var p in decor)
                    {
                        h = h * 31 + (p.Id?.GetHashCode() ?? 0);
                        h = h * 31 + Mathf.RoundToInt(p.X * 20);
                        h = h * 31 + Mathf.RoundToInt(p.Z * 20);
                    }
                return h;
            }
        }

        GameObject Spawn(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            _owned.Add(go);
            return go;
        }

        bool Blocked(Vector2 p, float clearance)
        {
            float r = p.magnitude;
            if (r < 3.05f || r > _radius - 0.35f) return true;
            foreach (var s in MachineRig.Slots)
                if ((s - p).sqrMagnitude < 1.3f * 1.3f) return true;
            for (int i = 0; i < _decorSpots.Count; i++)
            {
                float d = _decorSizes[i] + clearance;
                if ((_decorSpots[i] - p).sqrMagnitude < d * d) return true;
            }
            if (_props != null)
                foreach (var q in _props)
                    if ((q - p).sqrMagnitude < 0.36f * 0.36f) return true;
            return false;
        }

        // ------------------------------------------------------------------ grass & flowers

        enum Style { Grass, Dry, Snow, Night, Shards, Embers, Neon }

        Style StyleFor(Theme t)
        {
            switch (t.Props)
            {
                case PropKind.Cacti: return Style.Dry;
                case PropKind.Pines: return Style.Snow;
                case PropKind.NightTrees: return Style.Night;
                case PropKind.Crystals: return Style.Shards;
                case PropKind.Volcanic: return Style.Embers;
                case PropKind.NeonPylons: return Style.Neon;
                default: return Style.Grass;
            }
        }

        void BuildGrass(float density)
        {
            var t = _theme;
            var style = StyleFor(t);
            float area = _radius * _radius / (EnvironmentRig.BaseRadius * EnvironmentRig.BaseRadius);
            float perStyle = style == Style.Dry ? 0.55f : style == Style.Shards || style == Style.Neon ? 0.4f : style == Style.Embers ? 0.5f : 1f;
            int target = Mathf.RoundToInt(820 * density * area * perStyle);
            var blades = new MeshKit.Builder();
            var glow = new MeshKit.Builder();
            var rng = new System.Random(4242);

            Color baseCol, tipCol;
            switch (style)
            {
                case Style.Dry: baseCol = Palette(0xB88A4A); tipCol = Palette(0xF2D98A); break;
                case Style.Snow: baseCol = Palette(0x2E6B55); tipCol = Palette(0xE8F4FF); break;
                case Style.Night: baseCol = Palette(0x173D30); tipCol = Palette(0x3F8A66); break;
                case Style.Shards: baseCol = Color.Lerp(t.GrassDark, Color.black, 0.2f); tipCol = t.Leaves; break;
                case Style.Embers: baseCol = Palette(0x2A2020); tipCol = Palette(0x4A3A34); break;
                case Style.Neon: baseCol = Palette(0x1A1438); tipCol = t.Leaves; break;
                default:
                    baseCol = Color.Lerp(t.GrassDark, Color.black, 0.3f);
                    tipCol = Color.Lerp(t.Grass, new Color(0.95f, 1f, 0.55f), 0.45f);
                    break;
            }

            int placed = 0;
            for (int attempt = 0; attempt < target * 6 && placed < target; attempt++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                float r = Mathf.Sqrt(Mathf.Lerp(0.08f, 1f, (float)rng.NextDouble())) * (_radius - 0.4f);
                var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
                if (Blocked(p, 0.12f)) continue;
                placed++;
                var basePos = new Vector3(p.x, MeshKit.IslandHeight(p.x, p.y, _radius) - 0.02f, p.y);
                float scale = 0.75f + (float)rng.NextDouble() * 0.6f;
                switch (style)
                {
                    case Style.Shards:
                        Shard(glow, basePos, scale * 1.4f, (float)rng.NextDouble() * 6.28f, placed % 2 == 0 ? t.Leaves : t.LeavesAlt, rng);
                        break;
                    case Style.Neon:
                        Post(glow, basePos, scale * 1.6f, placed % 2 == 0 ? t.Leaves : t.LeavesAlt);
                        break;
                    case Style.Embers:
                        Pebble(blades, basePos, scale * 0.16f, Color.Lerp(baseCol, tipCol, (float)rng.NextDouble()), rng);
                        if (rng.NextDouble() < 0.45) Pebble(glow, basePos + Vector3.up * 0.03f, scale * 0.07f, new Color(1f, 0.45f, 0.1f), rng);
                        break;
                    default:
                    {
                        int count = style == Style.Snow ? 2 : 4 + rng.Next(0, 2);
                        float height = (style == Style.Snow ? 0.2f : style == Style.Dry ? 0.44f : 0.42f) * scale;
                        for (int k = 0; k < count; k++)
                        {
                            float ang = (float)rng.NextDouble() * Mathf.PI * 2;
                            float h = height * (0.7f + (float)rng.NextDouble() * 0.6f);
                            var tip = Color.Lerp(tipCol, baseCol, (float)rng.NextDouble() * 0.35f);
                            Blade(blades, basePos, ang, h, 0.1f * scale, h * 0.45f, baseCol, tip);
                        }
                        if (style == Style.Snow && rng.NextDouble() < 0.35)
                            Pebble(blades, basePos, 0.08f * scale, new Color(0.95f, 0.98f, 1f), rng);
                        double flowerChance = style == Style.Dry ? 0.12 : style == Style.Snow ? 0.06 : 0.3;
                        if (rng.NextDouble() < flowerChance)
                        {
                            var col = rng.NextDouble() < 0.5 ? t.Flower1 : t.Flower2;
                            var head = basePos + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.1f, height * 1.05f, ((float)rng.NextDouble() - 0.5f) * 0.1f);
                            if (style == Style.Night) Flower(glow, head, 0.08f * scale, col);
                            else Flower(blades, head, 0.1f * scale, col);
                        }
                        break;
                    }
                }
            }

            if (blades.VertexCount > 0)
            {
                var mat = Mats.NewToon(Color.white, 0f, null, new Color(0.72f, 0.78f, 0.9f));
                mat.SetFloat("_Sway", 1f);
                mat.SetColor("_RimColor", new Color(1, 1, 1, 0.15f));
                mat.SetColor("_HighlightColor", new Color(1, 1, 1, 0f));
                _owned.Add(mat);
                _grass = Spawn("Grass", blades.ToMesh("grass"), mat);
            }
            if (glow.VertexCount > 0)
            {
                var mat = Mats.Glow(new Color(2.1f, 2.1f, 2.1f, 1f), false);
                _owned.Add(mat);
                _glowBits = Spawn("GlowBits", glow.ToMesh("glow_bits"), mat);
            }
        }

        static Color Palette(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        /// <summary>One grass blade: a thin double-sided triangle whose tip sways (alpha = sway weight).</summary>
        static void Blade(MeshKit.Builder b, Vector3 basePos, float angle, float height, float width, float lean, Color baseCol, Color tipCol)
        {
            var dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var side = new Vector3(-dir.z, 0, dir.x) * (width * 0.5f);
            var tip = basePos + Vector3.up * height + dir * lean;
            // Up-facing normals light the grass like the ground it grows from.
            var n = (Vector3.up + dir * 0.35f).normalized;
            var bc = new Color(baseCol.r, baseCol.g, baseCol.b, 0f);
            var tc = new Color(tipCol.r, tipCol.g, tipCol.b, 1f);
            int v0 = b.Vertex(basePos - side, n, bc);
            int v1 = b.Vertex(basePos + side, n, bc);
            int v2 = b.Vertex(tip, n, tc);
            b.Tri(v0, v2, v1);
            b.Tri(v0, v1, v2);
        }

        /// <summary>Tiny flower head (an octahedron) that sways with its stem.</summary>
        static void Flower(MeshKit.Builder b, Vector3 c, float size, Color color)
        {
            var col = new Color(color.r, color.g, color.b, 1f);
            var top = c + Vector3.up * size * 0.7f;
            var bottom = c - Vector3.up * size * 0.5f;
            var ring = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.4f;
                ring[i] = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * size;
            }
            for (int i = 0; i < 4; i++)
            {
                var r0 = ring[i];
                var r1 = ring[(i + 1) % 4];
                AddTri(b, top, r1, r0, col);
                AddTri(b, bottom, r0, r1, col);
            }
        }

        static void AddTri(MeshKit.Builder b, Vector3 p0, Vector3 p1, Vector3 p2, Color col)
        {
            var n = Vector3.Cross(p1 - p0, p2 - p0).normalized;
            n = (n + Vector3.up).normalized;
            int a = b.Vertex(p0, n, col), c = b.Vertex(p1, n, col), d = b.Vertex(p2, n, col);
            b.Tri(a, c, d);
        }

        /// <summary>Small rounded stone (a squashed octahedron), no sway.</summary>
        static void Pebble(MeshKit.Builder b, Vector3 c, float size, Color color, System.Random rng)
        {
            var col = new Color(color.r, color.g, color.b, 0f);
            float squash = 0.45f + (float)rng.NextDouble() * 0.3f;
            var top = c + Vector3.up * size * squash;
            var ring = new Vector3[5];
            float spin = (float)rng.NextDouble() * 6.28f;
            for (int i = 0; i < 5; i++)
            {
                float a = spin + i * Mathf.PI * 2 / 5;
                ring[i] = c + new Vector3(Mathf.Cos(a), 0.02f, Mathf.Sin(a)) * size * (0.8f + (float)rng.NextDouble() * 0.4f);
            }
            for (int i = 0; i < 5; i++) AddTri(b, top, ring[(i + 1) % 5], ring[i], col);
        }

        /// <summary>Small glowing crystal sliver (alien worlds).</summary>
        static void Shard(MeshKit.Builder b, Vector3 basePos, float scale, float spin, Color color, System.Random rng)
        {
            float h = (0.14f + (float)rng.NextDouble() * 0.12f) * scale;
            float w = 0.035f * scale;
            var tip = basePos + new Vector3(((float)rng.NextDouble() - 0.5f) * 0.05f, h, ((float)rng.NextDouble() - 0.5f) * 0.05f);
            var ring = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float a = spin + i * Mathf.PI * 2 / 3;
                ring[i] = basePos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * w;
            }
            var col = Color.Lerp(color, Color.white, 0.2f);
            for (int i = 0; i < 3; i++) AddTri(b, tip, ring[(i + 1) % 3], ring[i], col);
        }

        /// <summary>Tiny glowing marker post (neon worlds).</summary>
        static void Post(MeshKit.Builder b, Vector3 basePos, float scale, Color color)
        {
            float h = 0.12f * scale, w = 0.018f * scale;
            var col = Color.Lerp(color, Color.white, 0.15f);
            var p = new[]
            {
                basePos + new Vector3(-w, 0, -w), basePos + new Vector3(w, 0, -w), basePos + new Vector3(w, 0, w), basePos + new Vector3(-w, 0, w)
            };
            var top = basePos + Vector3.up * h;
            for (int i = 0; i < 4; i++) AddTri(b, top, p[(i + 1) % 4], p[i], col);
        }

        // ------------------------------------------------------------------ particles

        void BuildParticles(float density)
        {
            var t = _theme;
            float span = _radius * 2.1f;
            switch (StyleFor(t))
            {
                case Style.Snow:
                    _ambient = Particles("Snowflakes", new Vector3(0, 7f, 1f), new Vector3(span * 1.4f, 1f, span * 1.4f), 34f * density,
                        new Color(1f, 1f, 1f, 0.95f), false, 0.09f, 0.18f, 9f, 12f, new Vector3(-0.25f, -0.55f, 0), 0.35f);
                    break;
                case Style.Night:
                    _ambient = Particles("Fireflies", new Vector3(0, 1.4f, 0), new Vector3(span, 2.2f, span), 5f * density,
                        new Color(1.6f, 1.8f, 0.6f, 1f), true, 0.12f, 0.2f, 6f, 9f, Vector3.zero, 0.55f, blink: true);
                    break;
                case Style.Shards:
                    _ambient = Particles("Spores", new Vector3(0, 1f, 0), new Vector3(span, 2f, span), 6f * density,
                        new Color(0.7f, 2.0f, 2.2f, 1f), true, 0.09f, 0.16f, 7f, 10f, new Vector3(0, 0.18f, 0), 0.4f, alt: new Color(2.2f, 0.9f, 2.0f, 1f));
                    break;
                case Style.Embers:
                    _ambient = Particles("Embers", new Vector3(0, 0.4f, 0), new Vector3(span, 0.6f, span), 12f * density,
                        new Color(2.4f, 1.0f, 0.25f, 1f), true, 0.06f, 0.13f, 4f, 7f, new Vector3(0.1f, 0.75f, 0), 0.45f);
                    break;
                case Style.Neon:
                    _ambient = Particles("Motes", new Vector3(0, 0.8f, 0), new Vector3(span, 1.2f, span), 8f * density,
                        new Color(0.6f, 2.2f, 2.4f, 1f), true, 0.07f, 0.12f, 5f, 8f, new Vector3(0, 0.45f, 0), 0.2f, alt: new Color(2.4f, 0.6f, 2.0f, 1f));
                    break;
                case Style.Dry:
                    _ambient = Particles("Dust", new Vector3(0, 1.2f, 0), new Vector3(span, 2f, span), 5f * density,
                        new Color(1f, 0.86f, 0.62f, 0.4f), false, 0.14f, 0.28f, 8f, 12f, new Vector3(0.35f, 0.05f, 0), 0.3f);
                    break;
                default:
                    if (t.Props == PropKind.Sakura)
                        _ambient = Particles("Petals", new Vector3(0, 6f, 1f), new Vector3(span * 1.3f, 1f, span * 1.3f), 10f * density,
                            new Color(1f, 0.72f, 0.82f, 0.95f), false, 0.12f, 0.2f, 10f, 13f, new Vector3(0.35f, -0.45f, 0), 0.5f, petals: true, alt: new Color(1f, 0.92f, 0.95f, 0.95f));
                    else
                        _ambient = Particles("Pollen", new Vector3(0, 1.4f, 0), new Vector3(span, 2.4f, span), 9f * density,
                            new Color(1f, 0.98f, 0.8f, 0.9f), false, 0.08f, 0.14f, 8f, 12f, new Vector3(0.12f, 0.1f, 0), 0.3f);
                    break;
            }
            if (t.Water)
            {
                // Twinkles on the sea far below.
                _extra = Particles("SeaSparkles", new Vector3(0, -7.3f, 0), new Vector3(70f, 0.1f, 70f), 45f * density,
                    new Color(2.2f, 2.2f, 2.0f, 1f), true, 0.12f, 0.26f, 0.6f, 1.4f, Vector3.zero, 0f, blink: true);
            }
            else if (StyleFor(t) == Style.Grass && t.Props != PropKind.Sakura)
            {
                // A few drifting petals in the flower colours.
                _extra = Particles("Petals", new Vector3(0, 1.8f, 0), new Vector3(span, 2f, span), 3f * density,
                    new Color(t.Flower1.r, t.Flower1.g, t.Flower1.b, 0.95f), false, 0.11f, 0.17f, 8f, 11f, new Vector3(0.3f, -0.05f, 0), 0.4f, petals: true,
                    alt: new Color(t.Flower2.r, t.Flower2.g, t.Flower2.b, 0.95f));
            }
        }

        ParticleSystem Particles(string name, Vector3 center, Vector3 box, float rate, Color color, bool additive, float minSize, float maxSize,
            float minLife, float maxLife, Vector3 velocity, float noise, bool blink = false, bool petals = false, Color? alt = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.transform.localPosition = center;
            _owned.Add(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(minLife, maxLife);
            main.startSpeed = 0f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            if (petals)
            {
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(minSize * 1.4f, maxSize * 1.4f);
                main.startSizeY = new ParticleSystem.MinMaxCurve(minSize * 0.8f, maxSize * 0.8f);
                main.startSizeZ = 1f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2);
            }
            else main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = alt.HasValue ? new ParticleSystem.MinMaxGradient(color, alt.Value) : new ParticleSystem.MinMaxGradient(color);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = box;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(velocity.x * 0.6f, velocity.x * 1.4f);
            vel.y = new ParticleSystem.MinMaxCurve(velocity.y * 0.6f, velocity.y * 1.4f);
            vel.z = new ParticleSystem.MinMaxCurve(velocity.z * 0.6f - 0.02f, velocity.z * 1.4f + 0.02f);

            if (noise > 0)
            {
                var n = ps.noise;
                n.enabled = true;
                n.strength = noise;
                n.frequency = 0.35f;
                n.scrollSpeed = 0.2f;
                n.quality = ParticleSystemNoiseQuality.Low;
            }
            if (petals)
            {
                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            var keys = blink
                ? new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0.2f, 0.35f), new GradientAlphaKey(1, 0.55f), new GradientAlphaKey(0.3f, 0.8f), new GradientAlphaKey(0, 1) }
                : new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.75f), new GradientAlphaKey(0, 1) };
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, keys);
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = Mats.Glow(Color.white, additive, Mats.SoftDot);
            _owned.Add(mat);
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        void BuildSplashes()
        {
            var go = new GameObject("RainSplashes");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = new Vector3(0, 0.08f, 0);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            _owned.Add(go);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.startColor = new Color(0.85f, 0.92f, 1f, 0.7f);
            main.gravityModifier = 0.6f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.95f;
            shape.arc = 360;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 1.4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = Mats.Glow(Color.white, false, Mats.SoftDot);
            _owned.Add(mat);
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            _splashes = ps;
        }

        // ------------------------------------------------------------------ blob shadows (Simple)

        void BuildBlobs()
        {
            _blobsDirty = false;
            _owned = _grassOwned;
            if (_blobs != null)
            {
                Object.Destroy(_blobs.GetComponent<MeshFilter>().sharedMesh);
                Object.Destroy(_blobs);
                _grassOwned.Remove(_blobs);
                _blobs = null;
            }
            if (_quality != 0 || _theme == null) return;
            var b = new MeshKit.Builder();
            // Slightly wider than each object so the soft edge peeks out around its base.
            Blob(b, Vector2.zero, 3.9f, 0.6f);
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i]) Blob(b, MachineRig.Slots[i], 1.45f, 0.6f);
            foreach (var tr in _trees) Blob(b, new Vector2(tr.x, tr.z), 1.15f * tr.y, 0.55f);
            for (int i = 0; i < _decorSpots.Count; i++) Blob(b, _decorSpots[i], _decorSizes[i] * 1.45f, 0.5f);
            if (b.VertexCount == 0) return;
            var mat = Mats.Glow(new Color(0.05f, 0.06f, 0.16f, 1f), false, Mats.SoftDot);
            // Drawn right after the ground, before other transparent effects.
            mat.renderQueue = (int)RenderQueue.Transparent - 50;
            _owned.Add(mat);
            _blobs = Spawn("BlobShadows", b.ToMesh("blob_shadows"), mat);
            _blobs.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        /// <summary>A soft dark disc hugging the ground (a small grid lifted slightly, so hills do not hide it).</summary>
        void Blob(MeshKit.Builder b, Vector2 c, float radius, float alpha)
        {
            const int n = 4;
            var col = new Color(1, 1, 1, alpha);
            int start = b.VertexCount;
            for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    float u = x / (float)n, v = z / (float)n;
                    float px = c.x + (u * 2 - 1) * radius, pz = c.y + (v * 2 - 1) * radius;
                    // Highest ground in the neighbourhood keeps the disc above the slope between samples.
                    float h = Mathf.Max(MeshKit.IslandHeight(px, pz, _radius),
                        Mathf.Max(MeshKit.IslandHeight(px + 0.3f, pz, _radius), MeshKit.IslandHeight(px, pz + 0.3f, _radius)));
                    b.Vertex(new Vector3(px, h + 0.06f, pz), Vector3.up, col, new Vector2(u, v));
                }
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i0 = start + z * (n + 1) + x;
                    b.Tri(i0, i0 + n + 1, i0 + n + 2);
                    b.Tri(i0, i0 + n + 2, i0 + 1);
                }
        }
    }
}
