using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormEngine.World
{
    /// <summary>
    /// The floating island, its props, drifting clouds, distant mountains, sky, fog and sunlight.
    /// Rebuilt whenever the equipped world changes.
    /// </summary>
    public sealed class EnvironmentRig
    {
        public const float BaseRadius = 9.5f;

        /// <summary>Current island radius (grows with terrain expansions).</summary>
        public float Radius { get; private set; } = BaseRadius;

        /// <summary>Where props (trees, rocks...) stand, so decorations can keep clear of them.</summary>
        public readonly List<Vector2> PropSpots = new List<Vector2>();

        /// <summary>Tree positions for blob shadows: x, scale, z.</summary>
        public readonly List<Vector3> TreeSpots = new List<Vector3>();

        static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        static readonly int GroundTintId = Shader.PropertyToID("_GroundTint");
        static readonly int WindStrengthId = Shader.PropertyToID("_WindStrength");

        readonly Transform _root;
        readonly List<GameObject> _built = new List<GameObject>();
        readonly List<Cloud> _clouds = new List<Cloud>();
        readonly List<Transform> _trees = new List<Transform>();
        Material _sky;
        Light _sun;
        Theme _theme;
        float _time;
        float _flash;

        /// <summary>A cloud drifting sideways along x behind (or below) the island, wrapping at the edges.</summary>
        struct Cloud
        {
            public Transform T;
            public float X;
            public float Z;
            public float Speed;
            public float Height;
            public float Bob;
            public Material Mat;
        }

        const float CloudSpan = 46f;

        public Light Sun => _sun;

        public EnvironmentRig(Transform parent)
        {
            _root = new GameObject("Environment").transform;
            _root.SetParent(parent, false);

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(_root, false);
            _sun = sunGo.AddComponent<Light>();
            _sun.type = LightType.Directional;
            _sun.shadows = LightShadows.Soft;
            _sun.shadowStrength = 0.85f;
            _sun.shadowBias = 0.04f;
            _sun.shadowNormalBias = 0.35f;
            RenderSettings.sun = _sun;

            _sky = Mats.Sky();
            RenderSettings.skybox = _sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
        }

        public void Build(Theme theme, float radius, IList<Vector2> avoid = null)
        {
            _theme = theme;
            Radius = radius;
            _avoid = avoid;
            PropSpots.Clear();
            TreeSpots.Clear();
            foreach (var go in _built)
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Object.Destroy(mf.sharedMesh);
                Object.Destroy(go);
            }
            _built.Clear();
            _clouds.Clear();
            _trees.Clear();
            _islets.Clear();

            // Sky, fog and light.
            _sky.SetColor("_TopColor", theme.SkyTop);
            _sky.SetColor("_HorizonColor", theme.SkyHorizon);
            _sky.SetColor("_BottomColor", Color.Lerp(theme.SkyBottom, theme.SkyTop, 0.35f));
            _sky.SetColor("_SunColor", theme.Sun);
            _sky.SetFloat("_Stars", theme.Stars);
            _sun.color = theme.LightColor;
            _sun.intensity = theme.LightIntensity;
            _sun.transform.rotation = Quaternion.Euler(theme.LightEuler);
            _sky.SetVector("_SunDirection", -_sun.transform.forward);
            RenderSettings.fogColor = theme.Fog;
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 120f;
            RenderSettings.ambientLight = Color.Lerp(theme.SkyHorizon, Color.white, 0.4f);
            // Hemisphere tint used by the toon shader: sky colour from above, ground bounce from below.
            Shader.SetGlobalColor(SkyTintId, Color.Lerp(new Color(1f, 0.98f, 0.94f), theme.SkyHorizon, 0.15f) * 1.05f);
            Shader.SetGlobalColor(GroundTintId, Color.Lerp(Color.white, theme.Grass, 0.5f) * 0.86f);

            // Island.
            var top = Spawn("IslandTop", MeshKit.IslandTop(Radius, theme.Grass, theme.GrassDark), Mats.Toon(Color.white, 0.0025f));
            var rock = Spawn("IslandRock", MeshKit.IslandRock(Radius, theme.Rock, theme.RockDark), Mats.Toon(Color.white, 0.003f));
            top.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            rock.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var collider = top.AddComponent<MeshCollider>();
            collider.sharedMesh = top.GetComponent<MeshFilter>().sharedMesh;

            if (theme.Water)
            {
                var water = Spawn("Sea", MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(160, 0) }, 48, theme.WaterColor), Mats.Toon(Color.white, 0f, null, new Color(0.8f, 0.9f, 1f)));
                water.transform.localPosition = new Vector3(0, -7.5f, 0);
            }

            ScatterProps(theme);
            BuildWorldFeatures(theme);
            BuildClouds(theme);
            BuildIslets(theme);
        }

        /// <summary>World-specific extras: lava pools and a glowing rim, neon edge light, ponds...</summary>
        void BuildWorldFeatures(Theme theme)
        {
            if (theme.GroundGlow.maxColorComponent > 0.01f)
            {
                var rim = Spawn("GlowRim", MeshKit.Torus(Radius * 0.985f, 0.08f, Color.white, 96, 6), Mats.Toon(theme.GroundGlow, 0f, theme.GroundGlow * 1.8f));
                rim.transform.localPosition = new Vector3(0, -0.28f, 0);
                rim.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            if (theme.Props == PropKind.Volcanic)
            {
                var rng = new System.Random(91);
                int pools = 0;
                for (int attempt = 0; attempt < 200 && pools < 5; attempt++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2;
                    float r = Mathf.Lerp(4.2f, Radius - 1.2f, (float)rng.NextDouble());
                    var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                    bool blocked = pos.z < -3f && Mathf.Abs(pos.x) < 5f;
                    foreach (var q in MachineRig.Slots) if ((new Vector3(q.x, 0, q.y) - pos).sqrMagnitude < 4f) blocked = true;
                    foreach (var q in PropSpots) if ((q - new Vector2(pos.x, pos.z)).sqrMagnitude < 2f) blocked = true;
                    if (blocked || NearDecoration(pos, 1.4f)) continue;
                    pools++;
                    PropSpots.Add(new Vector2(pos.x, pos.z));
                    pos.y = MeshKit.IslandHeight(pos.x, pos.z, Radius) + 0.03f;
                    float size = 0.5f + (float)rng.NextDouble() * 0.5f;
                    var pool = Spawn("LavaPool", MeshKit.Lathe(MeshKit.CapsuleProfile(size, 0.05f, 0.02f), 20, Color.white), Mats.Toon(new Color(1f, 0.45f, 0.1f), 0.003f, new Color(1f, 0.35f, 0.05f) * 1.6f));
                    pool.transform.localPosition = pos;
                    pool.transform.localScale = new Vector3(1f, 1f, 0.7f + (float)rng.NextDouble() * 0.4f);
                    pool.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        IList<Vector2> _avoid;

        bool NearDecoration(Vector3 pos, float clearance)
        {
            if (_avoid == null) return false;
            foreach (var d in _avoid)
                if ((new Vector2(pos.x, pos.z) - d).sqrMagnitude < clearance * clearance) return true;
            return false;
        }

        GameObject Spawn(string name, Mesh mesh, Material mat, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : _root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (parent == null) _built.Add(go);
            return go;
        }

        void ScatterProps(Theme theme)
        {
            var rng = new System.Random(1234);
            var reserved = new List<Vector3> { Vector3.zero };
            foreach (var p in MachineRig.Slots) reserved.Add(new Vector3(p.x, 0, p.y));

            float area = Radius * Radius / (BaseRadius * BaseRadius);
            int count = Mathf.RoundToInt((theme.Props == PropKind.Crystals || theme.Props == PropKind.NeonPylons ? 11 : 14) * area);
            int placed = 0;
            for (int attempt = 0; attempt < 600 && placed < count; attempt++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                float r = Mathf.Lerp(6.0f, Radius - 0.8f, (float)rng.NextDouble());
                var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                bool blocked = false;
                foreach (var q in reserved)
                    if ((q - pos).sqrMagnitude < (q == Vector3.zero ? 20f : 5.5f)) blocked = true;
                // Only behind the back machines or out on the flanks: the camera side stays open.
                if (pos.z < 5.6f && Mathf.Abs(pos.x) < 6.9f) blocked = true;
                if (pos.z < -4.5f) blocked = true;
                if (blocked || NearDecoration(pos, 1.3f)) continue;
                PropSpots.Add(new Vector2(pos.x, pos.z));
                reserved.Add(pos);
                placed++;
                pos.y = MeshKit.IslandHeight(pos.x, pos.z, Radius) - 0.05f;
                float s = 0.72f + (float)rng.NextDouble() * 0.38f;
                Mesh mesh;
                switch (theme.Props)
                {
                    case PropKind.Cacti: mesh = MeshKit.Cactus(theme.Leaves, s); break;
                    case PropKind.Pines: mesh = MeshKit.PineTree(theme.Trunk, theme.Leaves, new Color(0.95f, 0.98f, 1f), s * 1.2f); break;
                    case PropKind.Palms: mesh = MeshKit.PalmTree(theme.Trunk, theme.Leaves, s * 1.2f); break;
                    case PropKind.Crystals: mesh = MeshKit.Crystal(placed % 2 == 0 ? theme.Leaves : theme.LeavesAlt, s * 1.1f); break;
                    case PropKind.Volcanic: mesh = MeshKit.Spires(Color.Lerp(theme.Rock, Color.white, 0.12f), s * 1.2f, placed); break;
                    case PropKind.NeonPylons: mesh = MeshKit.Pylon(new Color(0.16f, 0.14f, 0.3f), placed % 2 == 0 ? theme.Leaves : theme.LeavesAlt, s * 1.3f); break;
                    default: mesh = MeshKit.RoundTree(theme.Trunk, placed % 3 == 0 ? theme.LeavesAlt : theme.Leaves, s, placed); break;
                }
                Color? emission = null;
                if (theme.Props == PropKind.Crystals) emission = Color.Lerp(theme.Leaves, Color.black, 0.5f);
                else if (theme.Props == PropKind.NeonPylons) emission = theme.Leaves * 0.35f;
                var go = Spawn("Prop", mesh, Mats.Toon(Color.white, 0.003f, emission));
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                _trees.Add(go.transform);
                TreeSpots.Add(new Vector3(pos.x, s, pos.z));
            }

            // Rocks and flowers for texture.
            for (int i = 0; i < 18; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2;
                float r = Mathf.Lerp(3.2f, Radius - 0.4f, (float)rng.NextDouble());
                var pos = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                bool nearSlot = false;
                foreach (var q in MachineRig.Slots) if ((new Vector3(q.x, 0, q.y) - pos).sqrMagnitude < 2.5f) nearSlot = true;
                if (nearSlot || NearDecoration(pos, 0.9f)) continue;
                PropSpots.Add(new Vector2(pos.x, pos.z));
                pos.y = MeshKit.IslandHeight(pos.x, pos.z, Radius);
                bool rock = i % 3 == 0;
                var mesh = rock ? MeshKit.Rock(Color.Lerp(theme.Rock, Color.white, 0.2f), 0.4f + (float)rng.NextDouble() * 0.4f, i)
                                : MeshKit.Flower(i % 2 == 0 ? theme.Flower1 : theme.Flower2, theme.Leaves, 1.3f);
                var go = Spawn(rock ? "Rock" : "Flower", mesh, Mats.Toon(Color.white, rock ? 0.003f : 0.0015f));
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0);
                if (!rock) go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void BuildClouds(Theme theme)
        {
            var rng = new System.Random(77);
            var cloudColor = theme.Stars > 0.5f ? new Color(0.55f, 0.6f, 0.85f) : Color.white;
            for (int i = 0; i < 11; i++)
            {
                var mat = Mats.NewToon(cloudColor, 0.0f, null, new Color(0.78f, 0.85f, 0.97f));
                mat.SetColor("_RimColor", new Color(1, 1, 1, 0.6f));
                var go = Spawn("Cloud", MeshKit.CloudPuff(Color.white, i * 13), mat);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                // Upper clouds stay behind the island so they never cover the machine; a few drift below it.
                bool below = i >= 8;
                var c = new Cloud
                {
                    T = go.transform,
                    X = ((float)rng.NextDouble() * 2 - 1) * CloudSpan,
                    Z = below ? -10f + (float)rng.NextDouble() * 18f : Radius + 1.5f + (float)rng.NextDouble() * 26f,
                    Speed = 0.35f + (float)rng.NextDouble() * 0.5f,
                    Height = below ? -6.5f - (float)rng.NextDouble() * 3f : -1f + (float)rng.NextDouble() * 8f,
                    Bob = (float)rng.NextDouble() * 10,
                    Mat = mat
                };
                float scale = below ? 1.7f + (float)rng.NextDouble() : 1.2f + (float)rng.NextDouble() * 1.4f;
                go.transform.localScale = Vector3.one * scale;
                go.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 40f - 20f, 0);
                _clouds.Add(c);
            }
        }

        /// <summary>Small floating islands far away and a sea of clouds below, for depth.</summary>
        void BuildIslets(Theme theme)
        {
            var rng = new System.Random(5);
            var topMesh = MeshKit.IslandTop(Radius, theme.Grass, theme.GrassDark, 8, 32);
            var rockMesh = MeshKit.IslandRock(Radius, theme.Rock, theme.RockDark, 32);
            var ground = Mats.Toon(Color.white, 0.004f);
            // Mostly behind and beside the main island so they read as distant scenery.
            float[] angles = { 30f, 62f, 95f, 128f, 158f, 200f, 338f };
            for (int i = 0; i < angles.Length; i++)
            {
                float a = (angles[i] + (float)rng.NextDouble() * 12f) * Mathf.Deg2Rad;
                float dist = 30f + (float)rng.NextDouble() * 22f;
                float scale = 0.13f + (float)rng.NextDouble() * 0.14f;
                var root = new GameObject("Islet").transform;
                root.SetParent(_root, false);
                root.localPosition = new Vector3(Mathf.Cos(a) * dist, -5f + (float)rng.NextDouble() * 9f, Mathf.Sin(a) * dist);
                root.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                root.localScale = Vector3.one * scale;
                _built.Add(root.gameObject);
                var top = Spawn("IsletTop", topMesh, ground, root);
                var rock = Spawn("IsletRock", rockMesh, ground, root);
                top.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                rock.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                int trees = 1 + rng.Next(0, 3);
                for (int t = 0; t < trees; t++)
                {
                    float ta = (float)rng.NextDouble() * Mathf.PI * 2, tr = (float)rng.NextDouble() * Radius * 0.5f;
                    var tpos = new Vector3(Mathf.Cos(ta) * tr, 0, Mathf.Sin(ta) * tr);
                    tpos.y = MeshKit.IslandHeight(tpos.x, tpos.z, Radius);
                    Mesh mesh;
                    switch (theme.Props)
                    {
                        case PropKind.Cacti: mesh = MeshKit.Cactus(theme.Leaves, 4f); break;
                        case PropKind.Pines: mesh = MeshKit.PineTree(theme.Trunk, theme.Leaves, new Color(0.95f, 0.98f, 1f), 5f); break;
                        case PropKind.Palms: mesh = MeshKit.PalmTree(theme.Trunk, theme.Leaves, 5f); break;
                        case PropKind.Crystals: mesh = MeshKit.Crystal(theme.Leaves, 4.5f); break;
                        default: mesh = MeshKit.RoundTree(theme.Trunk, theme.Leaves, 4f, t + i * 3); break;
                    }
                    var tree = Spawn("IsletTree", mesh, ground, root);
                    tree.transform.localPosition = tpos;
                    tree.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                }
                _islets.Add(new Islet { T = root, Base = root.localPosition, Phase = (float)rng.NextDouble() * 10f });
            }

            // Sea of clouds far below.
            var seaColor = theme.Stars > 0.5f ? new Color(0.42f, 0.46f, 0.72f) : new Color(0.97f, 0.98f, 1f);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2 + (float)rng.NextDouble() * 0.3f;
                float dist = 14f + (float)rng.NextDouble() * 34f;
                var mat = Mats.Toon(seaColor, 0f, null, Color.Lerp(seaColor, theme.SkyTop, 0.35f));
                var go = Spawn("CloudSea", MeshKit.CloudPuff(Color.white, 200 + i * 7, 5), mat);
                go.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, -13f - (float)rng.NextDouble() * 5f, Mathf.Sin(a) * dist);
                go.transform.localRotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
                go.transform.localScale = new Vector3(3.2f, 1.6f, 3.2f) * (0.8f + (float)rng.NextDouble() * 0.7f);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        struct Islet
        {
            public Transform T;
            public Vector3 Base;
            public float Phase;
        }

        readonly List<Islet> _islets = new List<Islet>();

        public void Flash(float amount) => _flash = Mathf.Max(_flash, amount);

        /// <summary>Animates clouds and trees; storm (0..1) darkens the sky and speeds the wind.</summary>
        public void Tick(float dt, float storm, float wind)
        {
            if (_theme == null) return;
            _time += dt;
            _flash = Mathf.Max(0, _flash - dt * 4f);

            var stormTop = new Color(0.16f, 0.17f, 0.3f);
            var stormHorizon = new Color(0.36f, 0.36f, 0.5f);
            _sky.SetColor("_TopColor", Color.Lerp(_theme.SkyTop, stormTop, storm));
            _sky.SetColor("_HorizonColor", Color.Lerp(_theme.SkyHorizon, stormHorizon, storm));
            // The camera looks down on the island, so most of the visible sky is below the horizon.
            _sky.SetColor("_BottomColor", Color.Lerp(Color.Lerp(_theme.SkyBottom, _theme.SkyTop, 0.35f), new Color(0.2f, 0.21f, 0.36f), storm));
            _sky.SetFloat("_SunGlow", Mathf.Lerp(0.35f, 0.05f, storm));
            _sky.SetFloat("_Flash", _flash);
            RenderSettings.fogColor = Color.Lerp(_theme.Fog, stormHorizon, storm);
            _sun.intensity = Mathf.Lerp(_theme.LightIntensity, _theme.LightIntensity * 0.45f, storm) + _flash * 1.5f;
            Mats.SetStormGlobals(new Color(0.55f, 0.58f, 0.8f), storm * 0.55f);
            Shader.SetGlobalFloat(WindStrengthId, wind * 0.8f + storm * 1.6f);

            var cloudTint = Color.Lerp(Color.white, new Color(0.42f, 0.44f, 0.6f), storm);
            float speed = 1 + storm * 4f + wind * 1.5f;
            for (int i = 0; i < _clouds.Count; i++)
            {
                var c = _clouds[i];
                c.X += c.Speed * speed * dt;
                if (c.X > CloudSpan) c.X -= CloudSpan * 2;
                float y = c.Height + Mathf.Sin(_time * 0.3f + c.Bob) * 0.4f;
                c.T.localPosition = new Vector3(c.X, y, c.Z);
                c.Mat.SetColor(Mats.BaseColorId, (_theme.Stars > 0.5f ? new Color(0.55f, 0.6f, 0.85f) : Color.white) * cloudTint);
                _clouds[i] = c;
            }

            foreach (var islet in _islets)
                islet.T.localPosition = islet.Base + Vector3.up * Mathf.Sin(_time * 0.35f + islet.Phase) * 0.35f;

            float sway = (0.6f + storm * 4f + wind * 2f);
            for (int i = 0; i < _trees.Count; i++)
            {
                var t = _trees[i];
                float s = Mathf.Sin(_time * (1.2f + storm * 2f) + i) * sway;
                var e = t.localEulerAngles;
                t.localRotation = Quaternion.Euler(s, e.y, s * 0.5f);
            }
        }
    }
}
