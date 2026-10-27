using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StormEngine.World
{
    /// <summary>Marks a clickable machine part. Index is the machine index, -1 for the dome.</summary>
    public sealed class MachineTag : MonoBehaviour
    {
        public int Index = -1;
    }

    /// <summary>
    /// Silhouette of a machine skin: deck, dome, crown, ornaments and generator styling.
    /// Every skin combines these differently so no two engines share a shape.
    /// </summary>
    public sealed class SkinShape
    {
        public int Sides = 48;
        public string Deck = "puck";
        public int Teeth;
        public int Fins;
        public string Dome = "dome";
        public int DomeSides = 48;
        public string Cap = "cap";
        public string Ornament = "none";
        public int GenSides = 24;
        public float GenRound = 1f;
        public string Topper = "none";
        public float Glow;

        public bool Faceted => Sides <= 8;
        public bool FacetedDome => DomeSides <= 8;
        public bool FacetedGen => GenSides <= 8;

        public static SkinShape For(string style)
        {
            switch (style)
            {
                case "tide": return new SkinShape { Deck = "hull", Dome = "bubble", Cap = "dish", Ornament = "rails", Topper = "ball" };
                case "gear": return new SkinShape { Teeth = 20, Cap = "gear", Ornament = "gears", Topper = "gear" };
                case "royal": return new SkinShape { Sides = 8, Deck = "tiered", Dome = "onion", DomeSides = 8, Cap = "crown", Ornament = "jewels", GenSides = 8, GenRound = 0.6f, Topper = "spike", Glow = 0.4f };
                case "neon": return new SkinShape { Sides = 8, Deck = "pedestal", Dome = "faceted", DomeSides = 8, Cap = "spire", Ornament = "neon", GenSides = 8, GenRound = 0.25f, Topper = "cone", Glow = 1.4f };
                case "candy": return new SkinShape { Deck = "puck", Dome = "bubble", Cap = "lolly", Ornament = "gumdrops", GenRound = 1.6f, Topper = "ball" };
                case "solar": return new SkinShape { Deck = "bowl", Dome = "orb", Cap = "orb", Ornament = "rays", Topper = "cone", Glow = 0.6f };
                case "chaos": return new SkinShape { Sides = 6, Deck = "rock", Dome = "faceted", DomeSides = 5, Cap = "horns", Ornament = "shards", GenSides = 6, GenRound = 0.3f, Topper = "spike", Glow = 1.1f };
                case "steam": return new SkinShape { Deck = "tiered", Teeth = 16, Dome = "onion", Cap = "chimney", Ornament = "gears", Topper = "gear" };
                case "crystal": return new SkinShape { Sides = 6, Deck = "pedestal", Dome = "faceted", DomeSides = 6, Cap = "spire", Ornament = "crystals", GenSides = 6, GenRound = 0.2f, Topper = "crystal", Glow = 0.7f };
                case "rocket": return new SkinShape { Deck = "pedestal", Fins = 4, Dome = "capsule", Cap = "nose", Ornament = "rings", Topper = "fin" };
                case "magma": return new SkinShape { Sides = 7, Deck = "rock", Dome = "dome", DomeSides = 7, Cap = "chimney", Ornament = "lava", GenSides = 7, GenRound = 0.4f, Topper = "spike", Glow = 1.6f };
                case "pagoda": return new SkinShape { Sides = 8, Deck = "tiered", Dome = "pagoda", DomeSides = 8, Cap = "roof", Ornament = "lanterns", GenSides = 8, GenRound = 0.5f, Topper = "lantern", Glow = 0.8f };
                case "cyber": return new SkinShape { Sides = 6, Deck = "pedestal", Dome = "cage", DomeSides = 6, Cap = "dish", Ornament = "rings", GenSides = 6, GenRound = 0.25f, Topper = "cone", Glow = 1.5f };
                default: return new SkinShape();
            }
        }
    }

    /// <summary>
    /// The storm engine: platform and glass dome, the generators, pipes and the storm chamber
    /// rings. Rebuilt when the skin's shape or the island size changes; recoloured otherwise.
    /// </summary>
    public sealed class MachineRig
    {
        /// <summary>Generator positions (x, z) around the dome, indexed by <see cref="SlotOf"/>.</summary>
        public static readonly Vector2[] Slots =
        {
            new Vector2(-3.1f, -2.7f), // pump
            new Vector2(-4.4f, 0.7f),  // hydro
            new Vector2(-2.7f, 3.8f),  // condenser
            new Vector2(2.7f, 3.8f),   // tesla
            new Vector2(4.4f, 0.7f),   // furnace
            new Vector2(5.9f, 4.4f),   // turbine
        };

        public static int SlotOf(string machineId)
        {
            switch (machineId)
            {
                case "pump": return 0;
                case "hydro": return 1;
                case "condenser": return 2;
                case "tesla": return 3;
                case "furnace": return 4;
                case "turbine": return 5;
                default: return -1;
            }
        }

        public const float DomeRadius = 1.72f;
        public static readonly Vector3 DomeCenter = new Vector3(0, 0.95f, 0);

        public Transform Root => _root;
        public Vector3 AntennaTip => _root.TransformPoint(_tipLocal);
        public Vector3 DomeWorldCenter => _root.TransformPoint(DomeCenter + Vector3.up * 0.8f);
        /// <summary>Camera used to face billboards.</summary>
        public Transform ViewCamera;

        readonly Transform _root;
        readonly string[] _machineIds;
        readonly Generator[] _gens;
        Theme _theme;
        SkinShape _shape;
        string _shapeName;
        float _islandRadius = 9.5f;
        float _time;
        float _boltTimer;
        Vector3 _tipLocal = new Vector3(0, 3.75f, 0);
        readonly System.Random _rng = new System.Random(3);
        readonly List<Object> _owned = new List<Object>();

        Material _bodyMat, _trimMat, _accentMat, _deckMat, _pipeMat, _panelLightMat, _glowAccentMat;
        Material _segOn, _segOff, _segStorm, _tipMat, _cloudMat, _glowMat, _boltMat;

        readonly List<Transform> _cloudBlobs = new List<Transform>();
        Vector3 _blobScale = Vector3.one;
        Transform _glow;
        readonly List<Renderer> _chargeSegments = new List<Renderer>();
        readonly List<Transform> _panelLights = new List<Transform>();
        readonly List<LineRenderer> _domeBolts = new List<LineRenderer>();
        readonly float[] _domeBoltLife = new float[4];
        readonly GameObject[] _chamberRings = new GameObject[3];
        readonly Dictionary<Renderer, Material> _realMaterial = new Dictionary<Renderer, Material>();
        readonly List<Spinner> _spinners = new List<Spinner>();
        readonly List<Transform> _smoke = new List<Transform>();

        struct Spinner
        {
            public Transform T;
            public Vector3 Axis;
            public float Speed;
            /// <summary>Radius of an orbit around the dome (0 = spin in place).</summary>
            public float Orbit;
            public float Phase;
            public float Height;
        }

        sealed class Generator
        {
            public string Id;
            public Transform Root;
            public Transform Visual;
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public GameObject Pipe;
            public Transform Spin;
            public float SpinSpeed;
            public Vector3 SpinAxis = Vector3.up;
            public Transform Bob;
            public float BobBase;
            public Transform Needle;
            public Material GlowMat;
            public Color GlowColor;
            public readonly List<Transform> Puffs = new List<Transform>();
            public LineRenderer Arc;
            public GameObject Badge;
            public Material BadgeMat;
            public Transform Topper;
            public int Level = -1;
            public bool Unlocked;
            public bool Ghost;
            public float Pop = 1;
            public float PopVel;
        }

        public MachineRig(Transform parent, string[] machineIds)
        {
            _root = new GameObject("Machine").transform;
            _root.SetParent(parent, false);
            _machineIds = machineIds;
            _gens = new Generator[machineIds.Length];
        }

        // ------------------------------------------------------------------ construction

        public void Build(Theme theme, float islandRadius)
        {
            Clear();
            _theme = theme;
            _islandRadius = islandRadius;
            _shapeName = theme.Shape;
            _shape = SkinShape.For(theme.Shape);
            _root.localPosition = new Vector3(0, MeshKit.IslandHeight(0, 0, islandRadius) + 0.02f, 0);

            _bodyMat = Own(Mats.NewToon(theme.Body));
            _trimMat = Own(Mats.NewToon(theme.Trim));
            _accentMat = Own(Mats.NewToon(theme.Accent));
            _glowAccentMat = Own(Mats.NewToon(theme.Accent, 0.002f));
            _deckMat = Own(Mats.NewToon(theme.Trim));
            _pipeMat = Own(Mats.NewToon(theme.Trim));
            _panelLightMat = Own(Mats.NewToon(theme.Accent, 0.002f));
            _segOff = Mats.Toon(new Color(0.18f, 0.22f, 0.34f), 0.002f);
            _segOn = Own(Mats.NewToon(theme.Accent, 0.002f));
            _segStorm = Own(Mats.NewToon(theme.BoltGlow, 0.002f));
            _tipMat = Own(Mats.NewToon(new Color(1f, 0.35f, 0.3f), 0.002f, new Color(1f, 0.2f, 0.15f)));
            _cloudMat = Own(Mats.NewToon(theme.Cloud, 0f));
            _cloudMat.SetColor("_RimColor", new Color(1, 1, 1, 0.5f));
            _glowMat = Own(Mats.Glow(theme.CloudGlow, true, Mats.SoftDot));
            _boltMat = Own(Mats.Glow(theme.Bolt * 4f, true));

            BuildPlatform();
            BuildDome();
            BuildCap();
            BuildOrnaments();
            for (int i = 0; i < _machineIds.Length; i++)
            {
                _gens[i] = new Generator { Id = _machineIds[i] };
                int slot = SlotOf(_machineIds[i]);
                if (slot >= 0) BuildGenerator(_gens[i], slot);
            }
            ApplyTheme(theme);
        }

        void Clear()
        {
            for (int i = _root.childCount - 1; i >= 0; i--) Object.Destroy(_root.GetChild(i).gameObject);
            foreach (var o in _owned) if (o != null) Object.Destroy(o);
            _owned.Clear();
            _cloudBlobs.Clear();
            _chargeSegments.Clear();
            _panelLights.Clear();
            _domeBolts.Clear();
            _realMaterial.Clear();
            _spinners.Clear();
            _smoke.Clear();
            for (int i = 0; i < _domeBoltLife.Length; i++) _domeBoltLife[i] = 0;
            for (int i = 0; i < _gens.Length; i++) _gens[i] = null;
        }

        T Own<T>(T o) where T : Object
        {
            _owned.Add(o);
            return o;
        }

        Mesh M(Mesh mesh, bool flat = false)
        {
            var m = flat ? MeshKit.Flatten(mesh) : mesh;
            if (flat) Object.Destroy(mesh);
            _owned.Add(m);
            return m;
        }

        /// <summary>Recolours for new cosmetics; rebuilds when the skin changes shape.</summary>
        public void ApplyTheme(Theme theme)
        {
            if (theme.Shape != _shapeName)
            {
                Build(theme, _islandRadius);
                return;
            }
            _theme = theme;
            _bodyMat.SetColor(Mats.BaseColorId, theme.Body);
            _trimMat.SetColor(Mats.BaseColorId, theme.Trim);
            _accentMat.SetColor(Mats.BaseColorId, theme.Accent);
            _accentMat.SetColor(Mats.EmissionId, theme.Accent * (0.35f + _shape.Glow * 0.4f));
            _glowAccentMat.SetColor(Mats.BaseColorId, theme.Accent);
            _glowAccentMat.SetColor(Mats.EmissionId, theme.Accent * (0.8f + _shape.Glow * 1.6f));
            _deckMat.SetColor(Mats.BaseColorId, Color.Lerp(theme.Trim, Color.white, _shape.Deck == "rock" ? 0.08f : 0.35f));
            _pipeMat.SetColor(Mats.BaseColorId, Color.Lerp(theme.Trim, Color.white, 0.2f));
            _panelLightMat.SetColor(Mats.BaseColorId, theme.Accent);
            _panelLightMat.SetColor(Mats.EmissionId, theme.Accent * 2f);
            _segOn.SetColor(Mats.BaseColorId, theme.Accent);
            _segOn.SetColor(Mats.EmissionId, theme.Accent * 2.2f);
            _segStorm.SetColor(Mats.BaseColorId, theme.BoltGlow);
            _segStorm.SetColor(Mats.EmissionId, theme.BoltGlow * 2.6f);
            _cloudMat.SetColor(Mats.ShadeColorId, theme.CloudShade);
            _boltMat.SetColor(Mats.ColorId, theme.Bolt * 4f);
            foreach (var g in _gens)
                if (g != null && g.Id == "tesla") g.GlowColor = theme.Bolt;
            SetCloudStyle(theme.CloudStyle);
        }

        /// <summary>Rebuilds for a different island size (generators follow the terrain).</summary>
        public void SetIslandRadius(float radius)
        {
            if (Mathf.Abs(radius - _islandRadius) < 0.01f || _theme == null) return;
            Build(_theme, radius);
        }

        GameObject Part(string name, Mesh mesh, Material mat, Transform parent, Vector3 pos, Quaternion? rot = null, Vector3? scale = null, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale ?? Vector3.one;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ platform

        Vector2[] DeckProfile()
        {
            switch (_shape.Deck)
            {
                case "tiered":
                    return new[]
                    {
                        new Vector2(0, 0), new Vector2(2.55f, 0), new Vector2(2.55f, 0), new Vector2(2.55f, 0.45f), new Vector2(2.55f, 0.45f),
                        new Vector2(2.2f, 0.45f), new Vector2(2.2f, 0.45f), new Vector2(2.2f, 0.9f), new Vector2(2.2f, 0.9f), new Vector2(0, 0.9f)
                    };
                case "bowl":
                    return new[]
                    {
                        new Vector2(0, 0), new Vector2(1.45f, 0), new Vector2(1.45f, 0), new Vector2(1.8f, 0.3f), new Vector2(2.35f, 0.72f),
                        new Vector2(2.6f, 0.9f), new Vector2(2.6f, 0.9f), new Vector2(0, 0.9f)
                    };
                case "pedestal":
                    return new[]
                    {
                        new Vector2(0, 0), new Vector2(1.35f, 0), new Vector2(1.35f, 0), new Vector2(1.4f, 0.35f), new Vector2(2.4f, 0.72f),
                        new Vector2(2.5f, 0.9f), new Vector2(2.5f, 0.9f), new Vector2(0, 0.9f)
                    };
                case "hull":
                    return new[]
                    {
                        new Vector2(0, 0), new Vector2(1.3f, 0.02f), new Vector2(2.05f, 0.18f), new Vector2(2.45f, 0.48f), new Vector2(2.55f, 0.9f),
                        new Vector2(2.55f, 0.9f), new Vector2(0, 0.9f)
                    };
                default:
                    return MeshKit.CapsuleProfile(2.45f, 0.9f, _shape.Deck == "rock" ? 0.12f : 0.22f);
            }
        }

        float DeckBandHeight => _shape.Deck == "tiered" ? 0.22f : _shape.Deck == "pedestal" ? 0.78f : _shape.Deck == "bowl" ? 0.8f : 0.5f;
        float DeckBandRadius => _shape.Deck == "tiered" ? 2.57f : _shape.Deck == "pedestal" || _shape.Deck == "bowl" ? 2.52f : 2.47f;

        void BuildPlatform()
        {
            var white = Color.white;
            var baseMesh = MeshKit.Lathe(DeckProfile(), _shape.Sides, white);
            if (_shape.Deck == "rock") baseMesh = Roughen(baseMesh, 0.18f, 11);
            Part("Base", M(baseMesh, _shape.Faceted), _trimMat, _root, Vector3.zero);
            Part("Band", M(MeshKit.Torus(DeckBandRadius, _shape.Deck == "rock" ? 0.08f : 0.13f, white, _shape.Faceted ? _shape.Sides : 64, 10)),
                _shape.Deck == "rock" ? _glowAccentMat : _bodyMat, _root, new Vector3(0, DeckBandHeight, 0));
            Part("Deck", M(MeshKit.Lathe(MeshKit.CapsuleProfile(2.15f, 0.12f, 0.05f), _shape.Sides, white), _shape.Faceted), _deckMat, _root, new Vector3(0, 0.85f, 0));

            int rivets = _shape.Faceted ? _shape.Sides * 2 : 16;
            var rivet = M(MeshKit.Sphere(0.07f, white, 10, 6));
            var rivetMat = Mats.Toon(new Color(0.8f, 0.84f, 0.9f), 0.002f);
            for (int i = 0; i < rivets; i++)
            {
                float a = (i + 0.5f) / rivets * Mathf.PI * 2;
                float r = (_shape.Deck == "tiered" ? 2.62f : _shape.Deck == "pedestal" ? 1.45f : _shape.Deck == "bowl" ? 1.62f : 2.52f);
                float y = _shape.Deck == "pedestal" || _shape.Deck == "bowl" ? 0.2f : 0.28f;
                Part("Rivet", rivet, rivetMat, _root, new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r), null, null, false);
            }

            if (_shape.Teeth > 0)
            {
                var tooth = M(MeshKit.RoundedBox(new Vector3(0.34f, 0.4f, 0.3f), 0.06f, white, 2));
                for (int i = 0; i < _shape.Teeth; i++)
                {
                    float a = i / (float)_shape.Teeth * 360f;
                    var rot = Quaternion.Euler(0, -a, 0);
                    Part("Tooth", tooth, _bodyMat, _root, rot * new Vector3(2.62f, 0.45f, 0), rot);
                }
            }
            if (_shape.Fins > 0)
            {
                var fin = M(MeshKit.RoundedBox(new Vector3(0.9f, 1.3f, 0.14f), 0.06f, white, 2));
                for (int i = 0; i < _shape.Fins; i++)
                {
                    float a = 45f + i / (float)_shape.Fins * 360f;
                    var rot = Quaternion.Euler(0, -a, 0);
                    Part("Fin", fin, _bodyMat, _root, rot * new Vector3(2.35f, 0.25f, 0), rot);
                }
            }

            // Control panel facing the camera.
            var panel = Part("Panel", M(MeshKit.RoundedBox(new Vector3(1.5f, 0.62f, 0.34f), 0.1f * Mathf.Min(1, _shape.GenRound + 0.2f), white)), _trimMat, _root, new Vector3(0, 0.46f, -2.38f)).transform;
            Part("Screen", M(MeshKit.RoundedBox(new Vector3(1.2f, 0.36f, 0.06f), 0.06f, white)), Mats.Toon(new Color(0.08f, 0.12f, 0.22f), 0.002f), panel, new Vector3(0, 0.03f, -0.17f));
            var bulb = M(MeshKit.Sphere(0.075f, white, 10, 6));
            for (int i = 0; i < 4; i++)
                _panelLights.Add(Part("Light", bulb, _panelLightMat, panel, new Vector3(-0.39f + i * 0.26f, 0.03f, -0.21f), null, null, false).transform);

            Part("Collar", M(MeshKit.Torus(1.78f, 0.17f, white, _shape.FacetedDome ? _shape.DomeSides : 64, 12)), _bodyMat, _root, DomeCenter);

            // Charge ring: 24 segments, filling clockwise from the back (12 o'clock on screen).
            var seg = M(MeshKit.RoundedBox(new Vector3(0.36f, 0.08f, 0.13f), 0.035f, white, 2));
            for (int i = 0; i < 24; i++)
            {
                float a = Mathf.PI / 2 - i / 24f * Mathf.PI * 2;
                var pos = new Vector3(Mathf.Cos(a) * 2.02f, 0.93f, Mathf.Sin(a) * 2.02f);
                var rot = Quaternion.Euler(0, -a * Mathf.Rad2Deg - 90, 0);
                _chargeSegments.Add(Part("Charge", seg, _segOff, _root, pos, rot, null, false).GetComponent<Renderer>());
            }

            // Storm chamber reinforcement rings, hugging the dome (shown by chamber level).
            for (int i = 0; i < 3; i++)
            {
                float h = 0.35f + i * 0.45f;
                float r = Mathf.Sqrt(DomeRadius * DomeRadius - h * h) + 0.07f;
                var ring = Part("ChamberRing", M(MeshKit.Torus(r, 0.06f, white, _shape.FacetedDome ? _shape.DomeSides : 64, 8)), _accentMat, _root, DomeCenter + Vector3.up * h, null, null, false);
                ring.SetActive(false);
                _chamberRings[i] = ring;
            }

            var touch = new GameObject("DomeTouch");
            touch.transform.SetParent(_root, false);
            touch.transform.localPosition = DomeCenter + Vector3.up * 0.6f;
            touch.AddComponent<SphereCollider>().radius = DomeRadius + 0.5f;
            touch.AddComponent<MachineTag>().Index = -1;
        }

        /// <summary>Irregular basalt: pushes lathe vertices in and out with noise.</summary>
        static Mesh Roughen(Mesh mesh, float amount, int seed)
        {
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                float a = Mathf.Atan2(v.z, v.x);
                float n = MeshKit.Noise(Mathf.Cos(a) * 2.2f + seed, Mathf.Sin(a) * 2.2f + v.y * 1.7f);
                float s = 1 + (n - 0.5f) * amount * 2;
                verts[i] = new Vector3(v.x * s, v.y, v.z * s);
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ dome & crown

        /// <summary>Top of the dome (local), where the crown sits.</summary>
        float DomeTop
        {
            get
            {
                switch (_shape.Dome)
                {
                    case "onion": return DomeCenter.y + 2.45f;
                    case "capsule": return DomeCenter.y + 3.0f;
                    case "orb": return DomeCenter.y + 2.75f;
                    case "bubble": return DomeCenter.y + 1.95f;
                    case "pagoda": return DomeCenter.y + DomeRadius + 0.55f;
                    default: return DomeCenter.y + DomeRadius;
                }
            }
        }

        void BuildDome()
        {
            var white = Color.white;
            _blobScale = Vector3.one;
            for (int i = 0; i < 8; i++)
                _cloudBlobs.Add(Part("CloudBlob", null, _cloudMat, _root, DomeCenter, null, Vector3.one * 0.4f, false).transform);
            SetCloudStyle(_theme.CloudStyle, true);

            _glow = Part("Glow", M(MeshKit.Quad(1f, 1f, white)), _glowMat, _root, DomeCenter + Vector3.up * 0.75f, null, Vector3.one * 4f, false).transform;

            Mesh glass;
            var glassPos = DomeCenter;
            switch (_shape.Dome)
            {
                case "onion":
                    glass = MeshKit.Lathe(new[]
                    {
                        new Vector2(1.72f, 0), new Vector2(1.9f, 0.45f), new Vector2(1.78f, 1.0f), new Vector2(1.25f, 1.5f),
                        new Vector2(0.55f, 1.95f), new Vector2(0.14f, 2.25f), new Vector2(0, 2.45f)
                    }, _shape.DomeSides, white);
                    break;
                case "capsule":
                    glass = MeshKit.Lathe(MeshKit.CapsuleProfile(1.55f, 3.0f, 1.2f, 6), _shape.DomeSides, white);
                    break;
                case "orb":
                    glass = MeshKit.Sphere(1.62f, white, 32, 18);
                    glassPos = DomeCenter + Vector3.up * 1.12f;
                    Part("Neck", M(MeshKit.Cone(1.3f, 0.9f, 0.35f, white, 32)), _trimMat, _root, DomeCenter);
                    break;
                case "bubble":
                    glass = MeshKit.Lathe(MeshKit.DomeProfile(1.95f, 16, -28f), _shape.DomeSides, white);
                    glassPos = DomeCenter + Vector3.up * 0.0f;
                    break;
                default:
                    glass = MeshKit.Lathe(MeshKit.DomeProfile(DomeRadius, _shape.FacetedDome ? 4 : 16, 0), _shape.DomeSides, white);
                    break;
            }
            Part("Dome", M(glass, _shape.FacetedDome), Own(Mats.Glass()), _root, glassPos, null, null, false);

            if (_shape.Dome == "cage")
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i / 6f * Mathf.PI * 2;
                    var pts = new List<Vector3>();
                    for (int k = 0; k <= 6; k++)
                    {
                        float t = k / 6f * Mathf.PI * 0.5f;
                        float r = (DomeRadius + 0.05f) * Mathf.Cos(t);
                        pts.Add(DomeCenter + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(t) * (DomeRadius + 0.05f), Mathf.Sin(a) * r));
                    }
                    Part("Rib", M(MeshKit.Tube(pts, 0.045f, white, 6, 3)), _glowAccentMat, _root, Vector3.zero, null, null, false);
                }
            }
            if (_shape.Dome == "pagoda")
            {
                // Two flared roof tiers above the dome.
                for (int tier = 0; tier < 2; tier++)
                {
                    float y = DomeCenter.y + DomeRadius - 0.35f + tier * 0.55f;
                    float w = 1.55f - tier * 0.45f;
                    var roof = MeshKit.Lathe(new[]
                    {
                        new Vector2(0, 0), new Vector2(w + 0.35f, 0), new Vector2(w + 0.35f, 0), new Vector2(w + 0.4f, 0.1f),
                        new Vector2(w * 0.5f, 0.42f), new Vector2(0, 0.5f)
                    }, 8, white);
                    Part("Roof", M(roof, true), _bodyMat, _root, new Vector3(0, y, 0));
                }
            }

            for (int i = 0; i < _domeBoltLife.Length; i++)
            {
                var go = new GameObject("DomeBolt");
                go.transform.SetParent(_root, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.sharedMaterial = _boltMat;
                lr.widthMultiplier = 0.06f;
                lr.positionCount = 0;
                lr.numCapVertices = 2;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                _domeBolts.Add(lr);
            }
        }

        /// <summary>Swaps the cloud blobs in the dome for the equipped cloud's style.</summary>
        void SetCloudStyle(string style, bool force = false)
        {
            if (!force && _cloudStyle == style) return;
            _cloudStyle = style;
            var white = Color.white;
            Mesh mesh;
            switch (style)
            {
                case "faceted": mesh = M(MeshKit.Sphere(1f, white, 6, 4), true); _blobScale = Vector3.one; break;
                case "cubes": mesh = M(MeshKit.RoundedBox(Vector3.one * 1.5f, 0.28f, white, 2)); _blobScale = Vector3.one * 0.85f; break;
                case "wisp": mesh = M(MeshKit.Sphere(1f, white, 16, 10)); _blobScale = new Vector3(1.7f, 0.65f, 0.85f); break;
                case "flame": mesh = M(MeshKit.Lathe(new[] { new Vector2(0, -0.8f), new Vector2(0.75f, -0.5f), new Vector2(0.85f, -0.1f), new Vector2(0.5f, 0.5f), new Vector2(0, 1.3f) }, 12, white)); _blobScale = Vector3.one; break;
                case "petal": mesh = M(MeshKit.Sphere(1f, white, 14, 8)); _blobScale = new Vector3(1.25f, 0.32f, 1.0f); break;
                default: mesh = M(MeshKit.Sphere(1f, white, 16, 10)); _blobScale = Vector3.one; break;
            }
            foreach (var blob in _cloudBlobs) blob.GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        string _cloudStyle;

        void BuildCap()
        {
            var white = Color.white;
            float top = DomeTop;
            var antennaMat = Mats.Toon(new Color(0.75f, 0.8f, 0.88f), 0.002f);
            float tip;
            switch (_shape.Cap)
            {
                case "crown":
                {
                    Part("CrownRing", M(MeshKit.Torus(0.42f, 0.09f, white, 8, 6)), _bodyMat, _root, new Vector3(0, top + 0.05f, 0));
                    var spike = M(MeshKit.Cone(0.1f, 0.01f, 0.42f, white, 6), true);
                    var jewel = M(MeshKit.Sphere(0.07f, white, 8, 5));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2;
                        var p = new Vector3(Mathf.Cos(a) * 0.42f, top + 0.08f, Mathf.Sin(a) * 0.42f);
                        Part("Spike", spike, _bodyMat, _root, p);
                        Part("Jewel", jewel, _glowAccentMat, _root, p + Vector3.up * 0.44f, null, null, false);
                    }
                    tip = top + 1.0f;
                    Part("Antenna", M(MeshKit.Tube(new[] { new Vector3(0, top, 0), new Vector3(0, tip - 0.1f, 0) }, 0.045f, white, 8, 2)), antennaMat, _root, Vector3.zero);
                    break;
                }
                case "spire":
                case "nose":
                {
                    bool nose = _shape.Cap == "nose";
                    var cone = nose
                        ? MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.6f, 0), new Vector2(0.62f, 0.2f), new Vector2(0.45f, 0.7f), new Vector2(0.18f, 1.05f), new Vector2(0, 1.2f) }, 24, white)
                        : MeshKit.Cone(0.36f, 0.02f, 1.25f, white, _shape.FacetedDome ? _shape.DomeSides : 16);
                    Part("Spire", M(cone, !nose), nose ? _bodyMat : _accentMat, _root, new Vector3(0, top - 0.05f, 0));
                    tip = top + (nose ? 1.25f : 1.3f);
                    break;
                }
                case "orb":
                {
                    Part("Stem", M(MeshKit.Capsule(0.12f, 0.45f, 0.04f, white, 10)), _trimMat, _root, new Vector3(0, top - 0.05f, 0));
                    Part("Orb", M(MeshKit.Sphere(0.36f, white, 20, 12)), _glowAccentMat, _root, new Vector3(0, top + 0.72f, 0));
                    tip = top + 1.12f;
                    break;
                }
                case "roof":
                {
                    Part("Finial", M(MeshKit.Tube(new[] { new Vector3(0, top - 0.1f, 0), new Vector3(0, top + 0.75f, 0) }, 0.05f, white, 6, 2)), _accentMat, _root, Vector3.zero);
                    var ball = M(MeshKit.Sphere(0.11f, white, 10, 6));
                    for (int i = 0; i < 3; i++) Part("FinialBall", ball, _accentMat, _root, new Vector3(0, top + 0.12f + i * 0.2f, 0), null, Vector3.one * (1.2f - i * 0.2f));
                    tip = top + 0.85f;
                    break;
                }
                case "horns":
                {
                    var hornMat = _bodyMat;
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i / 3f * Mathf.PI * 2 + 0.5f;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var pts = new[]
                        {
                            new Vector3(0, top - 0.1f, 0) + dir * 0.25f,
                            new Vector3(0, top + 0.35f, 0) + dir * 0.55f,
                            new Vector3(0, top + 0.85f, 0) + dir * 0.45f,
                            new Vector3(0, top + 1.1f, 0) + dir * 0.15f
                        };
                        Part("Horn", M(MeshKit.Tube(pts, 0.09f, white, 6, 4), true), hornMat, _root, Vector3.zero);
                    }
                    tip = top + 1.0f;
                    Part("Antenna", M(MeshKit.Tube(new[] { new Vector3(0, top, 0), new Vector3(0, tip - 0.1f, 0) }, 0.05f, white, 6, 2)), _glowAccentMat, _root, Vector3.zero);
                    break;
                }
                case "chimney":
                {
                    Part("CapBase", M(MeshKit.Lathe(MeshKit.CapsuleProfile(0.55f, 0.25f, 0.08f), _shape.Sides > 8 ? 24 : _shape.Sides, white), _shape.Faceted), _trimMat, _root, new Vector3(0, top - 0.1f, 0));
                    for (int i = 0; i < 2; i++)
                    {
                        var p = new Vector3(i == 0 ? -0.28f : 0.28f, top + 0.1f, 0.05f);
                        Part("Stack", M(MeshKit.Capsule(0.13f, 0.7f + i * 0.2f, 0.03f, white, 12)), _bodyMat, _root, p);
                        Part("StackRim", M(MeshKit.Torus(0.15f, 0.04f, white, 12, 6)), _accentMat, _root, p + Vector3.up * (0.7f + i * 0.2f));
                        var smoke = Part("Smoke", M(MeshKit.CloudPuff(white, 40 + i, 3)), Mats.Toon(_shape.Ornament == "lava" ? new Color(0.35f, 0.3f, 0.3f) : new Color(0.85f, 0.85f, 0.88f), 0f),
                            _root, p + Vector3.up * 1.1f, null, Vector3.one * 0.18f, false).transform;
                        _smoke.Add(smoke);
                    }
                    tip = top + 1.25f;
                    Part("Antenna", M(MeshKit.Tube(new[] { new Vector3(0, top, 0), new Vector3(0, tip - 0.1f, 0) }, 0.04f, white, 8, 2)), antennaMat, _root, Vector3.zero);
                    break;
                }
                case "lolly":
                {
                    Part("Stick", M(MeshKit.Tube(new[] { new Vector3(0, top - 0.05f, 0), new Vector3(0, top + 0.8f, 0) }, 0.05f, white, 8, 2)), Mats.Toon(Color.white), _root, Vector3.zero);
                    var lolly = Part("Lolly", M(MeshKit.Lathe(MeshKit.CapsuleProfile(0.42f, 0.14f, 0.07f), 28, white)), _bodyMat, _root, new Vector3(0, top + 1.1f, 0), Quaternion.Euler(90, 0, 0)).transform;
                    for (int i = 0; i < 2; i++)
                        Part("Swirl", M(MeshKit.Torus(0.14f + i * 0.14f, 0.035f, white, 24, 6)), _accentMat, lolly, new Vector3(0, 0.15f, 0), null, null, false);
                    _spinners.Add(new Spinner { T = lolly, Axis = Vector3.up, Speed = 40 });
                    tip = top + 1.55f;
                    break;
                }
                case "dish":
                {
                    Part("Mast", M(MeshKit.Capsule(0.1f, 0.5f, 0.03f, white, 10)), _trimMat, _root, new Vector3(0, top - 0.05f, 0));
                    var dish = new GameObject("Dish").transform;
                    dish.SetParent(_root, false);
                    dish.localPosition = new Vector3(0, top + 0.5f, 0);
                    var bowl = MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.25f, 0.02f), new Vector2(0.5f, 0.12f), new Vector2(0.62f, 0.26f), new Vector2(0.6f, 0.28f), new Vector2(0.45f, 0.18f), new Vector2(0.2f, 0.08f), new Vector2(0, 0.06f) }, 24, white);
                    Part("Bowl", M(bowl), _bodyMat, dish, Vector3.zero, Quaternion.Euler(-35, 0, 0));
                    Part("Receiver", M(MeshKit.Tube(new[] { Vector3.zero, new Vector3(0, 0.45f, -0.3f) }, 0.03f, white, 6, 2)), _accentMat, dish, Vector3.zero);
                    _spinners.Add(new Spinner { T = dish, Axis = Vector3.up, Speed = 25 });
                    tip = top + 1.05f;
                    break;
                }
                case "gear":
                {
                    var gear = Part("CrownGear", M(MeshKit.Gear(0.55f, 0.16f, 10, white)), _bodyMat, _root, new Vector3(0, top + 0.08f, 0)).transform;
                    _spinners.Add(new Spinner { T = gear, Axis = Vector3.up, Speed = 30 });
                    tip = top + 0.95f;
                    Part("Antenna", M(MeshKit.Tube(new[] { new Vector3(0, top, 0), new Vector3(0, tip - 0.1f, 0) }, 0.045f, white, 8, 2)), antennaMat, _root, Vector3.zero);
                    break;
                }
                default:
                {
                    Part("Cap", M(MeshKit.Lathe(MeshKit.CapsuleProfile(0.42f, 0.34f, 0.1f), 24, white)), _trimMat, _root, new Vector3(0, top - 0.1f, 0));
                    tip = top + 1.0f;
                    Part("Antenna", M(MeshKit.Tube(new[] { new Vector3(0, top + 0.13f, 0), new Vector3(0, tip - 0.1f, 0) }, 0.045f, white, 8, 2)), antennaMat, _root, Vector3.zero);
                    break;
                }
            }
            _tipLocal = new Vector3(0, tip + 0.05f, 0);
            Part("Tip", M(MeshKit.Sphere(0.11f, white, 12, 8)), _tipMat, _root, new Vector3(0, tip, 0), null, null, false);
        }

        // ------------------------------------------------------------------ ornaments

        void BuildOrnaments()
        {
            var white = Color.white;
            switch (_shape.Ornament)
            {
                case "rails":
                {
                    var post = M(MeshKit.Capsule(0.04f, 0.32f, 0.02f, white, 6));
                    for (int i = 0; i < 20; i++)
                    {
                        float a = i / 20f * Mathf.PI * 2;
                        if (Mathf.Sin(a) < -0.85f) continue; // keep the control panel clear
                        Part("Post", post, _accentMat, _root, new Vector3(Mathf.Cos(a) * 2.28f, 0.95f, Mathf.Sin(a) * 2.28f), null, null, false);
                    }
                    Part("Rail", M(MeshKit.Torus(2.28f, 0.035f, white, 64, 6)), _accentMat, _root, new Vector3(0, 1.28f, 0), null, null, false);
                    var port = M(MeshKit.Torus(0.13f, 0.045f, white, 16, 6));
                    var glassMesh = M(MeshKit.Sphere(0.11f, white, 12, 6));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i + 0.5f) / 8f * Mathf.PI * 2;
                        var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);
                        Part("Porthole", port, _accentMat, _root, dir * 2.5f + Vector3.up * 0.5f, rot, null, false);
                        Part("PortGlass", glassMesh, _glowAccentMat, _root, dir * 2.46f + Vector3.up * 0.5f, null, new Vector3(1, 1, 1) * 0.9f, false);
                    }
                    break;
                }
                case "gears":
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var p = new Vector3(i == 0 ? -2.55f : 2.55f, 0.55f, 0.6f);
                        var gear = Part("SideGear", M(MeshKit.Gear(0.55f + i * 0.15f, 0.14f, 9 + i * 3, white)), _accentMat, _root, p, Quaternion.Euler(0, 0, 90)).transform;
                        _spinners.Add(new Spinner { T = gear, Axis = Vector3.up, Speed = i == 0 ? 45 : -35 });
                    }
                    break;
                }
                case "jewels":
                {
                    var gem = M(MeshKit.Shard(0.12f, 0.3f, white));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i + 0.5f) / 8f * Mathf.PI * 2;
                        Part("Jewel", gem, _glowAccentMat, _root, new Vector3(Mathf.Cos(a) * 1.8f, DomeCenter.y + 0.05f, Mathf.Sin(a) * 1.8f), null, null, false);
                    }
                    break;
                }
                case "gumdrops":
                {
                    var drop = M(MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.2f, 0), new Vector2(0.2f, 0.05f), new Vector2(0.16f, 0.2f), new Vector2(0.06f, 0.3f), new Vector2(0, 0.32f) }, 12, white));
                    var dropMats = new[] { _bodyMat, _accentMat, Mats.Toon(new Color(0.55f, 0.85f, 1f)), Mats.Toon(new Color(1f, 0.85f, 0.35f)) };
                    for (int i = 0; i < 12; i++)
                    {
                        float a = (i + 0.5f) / 12f * Mathf.PI * 2;
                        if (Mathf.Sin(a) < -0.9f) continue;
                        Part("Gumdrop", drop, dropMats[i % dropMats.Length], _root, new Vector3(Mathf.Cos(a) * 2.3f, 0.95f, Mathf.Sin(a) * 2.3f), null, null, false);
                    }
                    // Candy stripes on the base.
                    for (int i = 0; i < 3; i++)
                        Part("Stripe", M(MeshKit.Torus(2.47f, 0.05f, white, 64, 6)), i % 2 == 0 ? _accentMat : _bodyMat, _root, new Vector3(0, 0.25f + i * 0.2f, 0), null, null, false);
                    break;
                }
                case "rays":
                {
                    var ray = M(MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.16f, 0), new Vector2(0.16f, 0), new Vector2(0.02f, 1.0f), new Vector2(0, 1.0f) }, 4, white), true);
                    for (int i = 0; i < 12; i++)
                    {
                        float a = i / 12f * 360f;
                        var rot = Quaternion.Euler(0, -a, 0) * Quaternion.Euler(0, 0, -72);
                        Part("Ray", ray, i % 2 == 0 ? _glowAccentMat : _bodyMat, _root, Quaternion.Euler(0, -a, 0) * new Vector3(2.45f, 0.75f, 0), rot);
                    }
                    break;
                }
                case "shards":
                {
                    var shard = M(MeshKit.Shard(0.14f, 0.55f, white));
                    for (int i = 0; i < 6; i++)
                    {
                        var t = Part("Shard", shard, _glowAccentMat, _root, Vector3.zero, null, null, false).transform;
                        _spinners.Add(new Spinner { T = t, Axis = Vector3.up, Speed = 20, Orbit = 2.6f, Phase = i / 6f * 360f, Height = 1.6f + (i % 2) * 0.7f });
                    }
                    break;
                }
                case "crystals":
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float a = (i + 0.5f) / 4f * Mathf.PI * 2;
                        var p = new Vector3(Mathf.Cos(a) * 2.35f, 0.7f, Mathf.Sin(a) * 2.35f);
                        Part("Crystal", M(MeshKit.Crystal(Color.white, 0.8f), true), _glowAccentMat, _root, p, Quaternion.Euler(0, a * Mathf.Rad2Deg, 0));
                    }
                    break;
                }
                case "lava":
                {
                    var crack = M(MeshKit.RoundedBox(new Vector3(0.08f, 0.55f, 0.06f), 0.02f, white, 1));
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2 + 0.2f;
                        var rot = Quaternion.Euler(0, -a * Mathf.Rad2Deg, (i % 3 - 1) * 20f);
                        Part("LavaCrack", crack, _glowAccentMat, _root, new Vector3(Mathf.Cos(a) * 2.44f, 0.45f, Mathf.Sin(a) * 2.44f), rot, null, false);
                    }
                    var rock = M(MeshKit.Rock(Color.white, 0.6f, 5));
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i / 5f * Mathf.PI * 2 + 0.9f;
                        Part("Rock", rock, _trimMat, _root, new Vector3(Mathf.Cos(a) * 2.9f, 0.05f, Mathf.Sin(a) * 2.9f), Quaternion.Euler(0, i * 70, 0));
                    }
                    break;
                }
                case "neon":
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var ring = Part("NeonRing", M(MeshKit.Torus(i == 0 ? 2.52f : 1.95f, 0.035f, white, _shape.Faceted ? _shape.Sides : 64, 6)), _glowAccentMat, _root,
                            new Vector3(0, i == 0 ? 0.8f : DomeCenter.y + 0.22f, 0), null, null, false);
                    }
                    break;
                }
                case "rings":
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var pivot = new GameObject("OrbitRing").transform;
                        pivot.SetParent(_root, false);
                        pivot.localPosition = DomeCenter + Vector3.up * 0.9f;
                        pivot.localRotation = Quaternion.Euler(i == 0 ? 18 : -24, 0, i == 0 ? 12 : -16);
                        Part("Ring", M(MeshKit.Torus(2.35f + i * 0.25f, 0.045f, white, 64, 6)), _glowAccentMat, pivot, Vector3.zero, null, null, false);
                        _spinners.Add(new Spinner { T = pivot, Axis = Vector3.up, Speed = i == 0 ? 30 : -22 });
                    }
                    break;
                }
                case "lanterns":
                {
                    var lantern = M(MeshKit.Capsule(0.13f, 0.3f, 0.1f, white, 10));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i + 0.5f) / 8f * Mathf.PI * 2;
                        float r = 1.55f + 0.35f;
                        var p = new Vector3(Mathf.Cos(a) * r, DomeCenter.y + DomeRadius - 0.75f, Mathf.Sin(a) * r);
                        Part("Lantern", lantern, _glowAccentMat, _root, p, null, null, false);
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ generators

        void BuildGenerator(Generator g, int slot)
        {
            var pos2 = Slots[slot];
            float y = MeshKit.IslandHeight(pos2.x, pos2.y, _islandRadius) - _root.localPosition.y + 0.02f;
            g.Root = new GameObject("Gen_" + g.Id).transform;
            g.Root.SetParent(_root, false);
            g.Root.localPosition = new Vector3(pos2.x, y, pos2.y);
            // Turn slightly towards the platform, keeping the fronts readable from the camera.
            g.Root.localRotation = Quaternion.Euler(0, Mathf.Atan2(pos2.x, pos2.y) * Mathf.Rad2Deg * 0.35f, 0);
            g.Visual = new GameObject("Visual").transform;
            g.Visual.SetParent(g.Root, false);
            var white = Color.white;
            var trim = _trimMat;
            var body = _bodyMat;
            var v = g.Visual;
            int sides = _shape.GenSides;
            bool flat = _shape.FacetedGen;
            float round = _shape.GenRound;

            void Add(GameObject go)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) g.Renderers.Add(r);
            }

            Mesh Cap(float r, float h, float rr) => M(MeshKit.Capsule(r, h, rr * Mathf.Clamp(round, 0.15f, 1.3f), white, sides), flat);
            Mesh Box(Vector3 size, float rr) => M(MeshKit.RoundedBox(size, rr * round, white, round < 0.5f ? 1 : 4));
            Mesh Ring(float major, float minor) => M(MeshKit.Torus(major, minor, white, flat ? sides : 32, 8), false);
            Mesh Ball(float r) => M(MeshKit.Sphere(r, white, flat ? sides : 20, flat ? 4 : 12), flat);

            float topHeight = 1.8f;
            switch (g.Id)
            {
                case "pump":
                {
                    Add(Part("Body", Box(new Vector3(1.25f, 0.95f, 0.95f), 0.16f), trim, v, new Vector3(0, 0.48f, 0)));
                    Add(Part("Stripe", Box(new Vector3(1.3f, 0.16f, 1.0f), 0.06f), body, v, new Vector3(0, 0.25f, 0)));
                    Add(Part("Gauge", M(MeshKit.Lathe(MeshKit.CapsuleProfile(0.34f, 0.1f, 0.04f), flat ? sides : 28, white), flat), Mats.Toon(new Color(0.97f, 0.98f, 1f)), v, new Vector3(0, 0.56f, -0.47f), Quaternion.Euler(-90, 0, 0)));
                    Add(Part("Bezel", Ring(0.35f, 0.05f), body, v, new Vector3(0, 0.56f, -0.52f), Quaternion.Euler(-90, 0, 0)));
                    var needle = new GameObject("Needle").transform;
                    needle.SetParent(v, false);
                    needle.localPosition = new Vector3(0, 0.56f, -0.58f);
                    Add(Part("NeedleMesh", M(MeshKit.RoundedBox(new Vector3(0.04f, 0.26f, 0.02f), 0.01f, white)), Mats.Toon(new Color(1f, 0.3f, 0.25f), 0.002f), needle, new Vector3(0, 0.11f, 0), null, null, false));
                    g.Needle = needle;
                    var piston = new GameObject("Piston").transform;
                    piston.SetParent(v, false);
                    piston.localPosition = new Vector3(0, 0.95f, 0.1f);
                    Add(Part("Rod", Cap(0.17f, 0.65f, 0.06f), Mats.Toon(new Color(0.82f, 0.86f, 0.92f)), piston, Vector3.zero));
                    Add(Part("PistonCap", Cap(0.26f, 0.14f, 0.05f), body, piston, new Vector3(0, 0.62f, 0)));
                    g.Bob = piston;
                    g.BobBase = 0.95f;
                    topHeight = 1.95f;
                    break;
                }
                case "hydro":
                {
                    Add(Part("Tank", Cap(0.66f, 2.1f, 0.45f), Mats.Toon(new Color(0.23f, 0.55f, 1f)), v, Vector3.zero));
                    Add(Part("BandA", Ring(0.67f, 0.07f), trim, v, new Vector3(0, 0.45f, 0)));
                    Add(Part("BandB", Ring(0.67f, 0.07f), trim, v, new Vector3(0, 1.62f, 0)));
                    g.GlowColor = new Color(0.2f, 0.5f, 1f);
                    g.GlowMat = Own(Mats.NewToon(new Color(0.75f, 0.92f, 1f), 0.0035f, g.GlowColor * 0.4f));
                    Add(Part("Window", Ball(0.3f), g.GlowMat, v, new Vector3(0, 1.05f, -0.56f), null, new Vector3(1f, 1.3f, 0.35f)));
                    var wheel = new GameObject("Valve").transform;
                    wheel.SetParent(v, false);
                    wheel.localPosition = new Vector3(0.7f, 0.9f, 0);
                    wheel.localRotation = Quaternion.Euler(0, 0, 90);
                    Add(Part("Wheel", M(MeshKit.Torus(0.2f, 0.035f, white, 20, 6)), Mats.Toon(new Color(1f, 0.35f, 0.3f)), wheel, Vector3.zero));
                    g.Spin = wheel;
                    g.SpinSpeed = 40;
                    var bubble = M(MeshKit.Sphere(0.06f, white, 8, 6));
                    for (int i = 0; i < 3; i++)
                        g.Puffs.Add(Part("Bubble", bubble, Mats.Toon(new Color(0.9f, 0.97f, 1f), 0.002f), v, new Vector3(0, 0.9f, -0.66f), null, null, false).transform);
                    topHeight = 2.1f;
                    break;
                }
                case "condenser":
                {
                    Add(Part("Body", Cap(0.9f, 1.0f, 0.3f), Mats.Toon(new Color(0.62f, 0.55f, 1f)), v, Vector3.zero));
                    Add(Part("Rim", Ring(0.78f, 0.08f), trim, v, new Vector3(0, 1.02f, 0)));
                    var vent = Box(new Vector3(0.7f, 0.08f, 0.1f), 0.03f);
                    for (int i = 0; i < 3; i++)
                        Add(Part("Vent", vent, trim, v, new Vector3(0, 0.3f + i * 0.18f, -0.88f)));
                    var fan = new GameObject("Fan").transform;
                    fan.SetParent(v, false);
                    fan.localPosition = new Vector3(0, 1.08f, 0);
                    Add(Part("Hub", Ball(0.13f), body, fan, Vector3.zero));
                    var blade = M(MeshKit.RoundedBox(new Vector3(0.62f, 0.04f, 0.16f), 0.02f, white));
                    for (int i = 0; i < 4; i++)
                        Add(Part("Blade", blade, Mats.Toon(new Color(0.95f, 0.96f, 1f)), fan, Quaternion.Euler(0, i * 90, 0) * new Vector3(0.34f, 0, 0), Quaternion.Euler(0, i * 90, 12)));
                    g.Spin = fan;
                    g.SpinSpeed = 180;
                    for (int i = 0; i < 3; i++)
                        g.Puffs.Add(Part("Puff", M(MeshKit.CloudPuff(white, i * 7 + 3, 3)), Mats.Toon(Color.white, 0f), v, new Vector3(0, 1.3f, 0), null, Vector3.one * 0.2f, false).transform);
                    topHeight = -1;
                    break;
                }
                case "tesla":
                {
                    Add(Part("Base", Box(new Vector3(1.05f, 0.5f, 1.05f), 0.12f), trim, v, new Vector3(0, 0.25f, 0)));
                    Add(Part("Rod", Cap(0.13f, 1.8f, 0.05f), Mats.Toon(new Color(0.7f, 0.74f, 0.82f)), v, new Vector3(0, 0.45f, 0)));
                    var copper = Mats.Toon(new Color(0.95f, 0.62f, 0.3f));
                    for (int i = 0; i < 6; i++)
                        Add(Part("Coil", Ring(0.34f - i * 0.03f, 0.07f), copper, v, new Vector3(0, 0.62f + i * 0.24f, 0)));
                    g.GlowColor = _theme.Bolt;
                    g.GlowMat = Own(Mats.NewToon(new Color(0.92f, 0.95f, 1f), 0.0035f, _theme.Bolt * 0.3f));
                    Add(Part("Sphere", Ball(0.34f), g.GlowMat, v, new Vector3(0, 2.35f, 0)));
                    var arcGo = new GameObject("Arc");
                    arcGo.transform.SetParent(v, false);
                    var arc = arcGo.AddComponent<LineRenderer>();
                    arc.useWorldSpace = false;
                    arc.sharedMaterial = _boltMat;
                    arc.widthMultiplier = 0.035f;
                    arc.positionCount = 0;
                    arc.shadowCastingMode = ShadowCastingMode.Off;
                    g.Arc = arc;
                    topHeight = -1;
                    break;
                }
                case "furnace":
                {
                    Add(Part("Body", Box(new Vector3(1.35f, 1.3f, 1.15f), 0.2f), Mats.Toon(new Color(1f, 0.42f, 0.24f)), v, new Vector3(0, 0.65f, 0)));
                    Add(Part("Band", Box(new Vector3(1.42f, 0.2f, 1.22f), 0.08f), trim, v, new Vector3(0, 0.2f, 0)));
                    g.GlowColor = new Color(1f, 0.45f, 0.1f);
                    g.GlowMat = Own(Mats.NewToon(new Color(1f, 0.85f, 0.4f), 0.0035f, g.GlowColor * 1.5f));
                    Add(Part("Window", M(MeshKit.Lathe(MeshKit.CapsuleProfile(0.3f, 0.08f, 0.03f), flat ? sides : 24, white), flat), g.GlowMat, v, new Vector3(0, 0.72f, -0.56f), Quaternion.Euler(-90, 0, 0)));
                    Add(Part("WindowRim", Ring(0.32f, 0.05f), trim, v, new Vector3(0, 0.72f, -0.62f), Quaternion.Euler(-90, 0, 0)));
                    Add(Part("Chimney", Cap(0.19f, 1.1f, 0.05f), trim, v, new Vector3(0.35f, 1.1f, 0.3f)));
                    for (int i = 0; i < 3; i++)
                        g.Puffs.Add(Part("Smoke", M(MeshKit.CloudPuff(white, i * 5 + 11, 3)), Mats.Toon(new Color(0.78f, 0.78f, 0.82f), 0f), v, new Vector3(0.35f, 2.3f, 0.3f), null, Vector3.one * 0.18f, false).transform);
                    topHeight = 1.32f;
                    break;
                }
                case "turbine":
                {
                    var tower = Mats.Toon(new Color(0.96f, 0.97f, 1f));
                    Add(Part("Tower", M(MeshKit.Lathe(MeshKit.ConeProfile(0.34f, 0.16f, 5.2f), flat ? sides : 16, white), flat), tower, v, Vector3.zero));
                    Add(Part("Nacelle", Cap(0.28f, 1.0f, 0.26f), body, v, new Vector3(0, 5.3f, 0.35f), Quaternion.Euler(-90, 0, 0)));
                    var rotor = new GameObject("Rotor").transform;
                    rotor.SetParent(v, false);
                    rotor.localPosition = new Vector3(0, 5.3f, -0.72f);
                    Add(Part("Hub", Ball(0.24f), trim, rotor, Vector3.zero));
                    var bladeMesh = M(MeshKit.Sphere(0.5f, white, 12, 8));
                    var tipMesh = M(MeshKit.Sphere(0.12f, white, 8, 6));
                    for (int i = 0; i < 3; i++)
                    {
                        var rot = Quaternion.Euler(0, 0, i * 120f);
                        Add(Part("Blade", bladeMesh, tower, rotor, rot * new Vector3(0, 1.25f, 0), rot * Quaternion.Euler(0, 20, 0), new Vector3(0.36f, 2.6f, 0.08f)));
                        Add(Part("BladeTip", tipMesh, body, rotor, rot * new Vector3(0, 2.3f, 0), rot, new Vector3(1.2f, 1.4f, 0.6f), false));
                    }
                    g.Spin = rotor;
                    g.SpinAxis = Vector3.forward;
                    g.SpinSpeed = 90;
                    topHeight = -1;
                    break;
                }
            }

            if (topHeight > 0) BuildTopper(g, topHeight, Add);

            // Milestone badge ring around the foot of the machine.
            g.BadgeMat = Own(Mats.NewToon(new Color(0.8f, 0.5f, 0.25f), 0.002f));
            g.Badge = Part("Badge", M(MeshKit.Torus(0.95f, 0.05f, white, 40, 6)), g.BadgeMat, g.Root, new Vector3(0, 0.05f, 0), null, null, false);
            g.Badge.SetActive(false);

            // Pipe to the platform.
            var from = new Vector3(g.Root.localPosition.x, 0.3f + y * 0.5f, g.Root.localPosition.z);
            var dir = new Vector3(-from.x, 0, -from.z).normalized;
            var start = from + dir * 0.75f;
            var end = new Vector3(0, 0.38f, 0) - dir * 2.45f;
            var mid = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.45f;
            g.Pipe = Part("Pipe_" + g.Id, M(MeshKit.Tube(new[] { start, mid, end }, 0.1f, white, 10, 8)), _pipeMat, _root, Vector3.zero);

            var box = g.Root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0, g.Id == "turbine" ? 2.6f : 0.8f, 0);
            box.size = g.Id == "turbine" ? new Vector3(1.4f, 5.6f, 1.4f) : new Vector3(1.8f, 2.2f, 1.8f);
            g.Root.gameObject.AddComponent<MachineTag>().Index = System.Array.IndexOf(_machineIds, g.Id);
        }

        /// <summary>Small skin-specific ornament on top of a generator.</summary>
        void BuildTopper(Generator g, float height, System.Action<GameObject> add)
        {
            var white = Color.white;
            var pos = new Vector3(0, height, 0);
            GameObject go = null;
            switch (_shape.Topper)
            {
                case "gear":
                    go = Part("TopGear", M(MeshKit.Gear(0.22f, 0.08f, 7, white)), _accentMat, g.Visual, pos + Vector3.up * 0.06f);
                    _spinners.Add(new Spinner { T = go.transform, Axis = Vector3.up, Speed = 70 });
                    break;
                case "crystal":
                    go = Part("TopCrystal", M(MeshKit.Shard(0.13f, 0.45f, white)), _glowAccentMat, g.Visual, pos, null, null, false);
                    break;
                case "ball":
                    go = Part("TopBall", M(MeshKit.Sphere(0.13f, white, 12, 8)), _accentMat, g.Visual, pos + Vector3.up * 0.1f, null, null, false);
                    break;
                case "fin":
                    go = Part("TopFin", M(MeshKit.RoundedBox(new Vector3(0.06f, 0.35f, 0.35f), 0.03f, white, 1)), _bodyMat, g.Visual, pos + Vector3.up * 0.15f);
                    break;
                case "spike":
                    go = Part("TopSpike", M(MeshKit.Cone(0.1f, 0.01f, 0.38f, white, 6), true), _accentMat, g.Visual, pos);
                    break;
                case "lantern":
                    go = Part("TopLantern", M(MeshKit.Capsule(0.1f, 0.24f, 0.08f, white, 10)), _glowAccentMat, g.Visual, pos + Vector3.up * 0.05f, null, null, false);
                    break;
                case "cone":
                    go = Part("TopCone", M(MeshKit.Cone(0.12f, 0.02f, 0.3f, white, 8), true), _glowAccentMat, g.Visual, pos, null, null, false);
                    break;
            }
            if (go == null) return;
            add(go);
            g.Topper = go.transform;
        }

        // ------------------------------------------------------------------ state

        public struct State
        {
            public int[] Levels;
            public bool[] Unlocked;
            public int[] Milestones;
            public float Charge;
            public bool Storming;
            public float StormLevel;
            public Color Glow;
            public float Wind, Heat, Electric, Humidity, Cloud, Pressure;
            /// <summary>Active boosts in content order: cloud burst, arc overload, heat surge, jetstream.</summary>
            public bool[] BoostOn;
            public int ChamberLevel;
        }

        /// <summary>Syncs visibility with machine levels. Reports machines that were just built or levelled.</summary>
        public void Apply(in State s, List<int> newlyBuilt, List<int> leveled)
        {
            for (int i = 0; i < _gens.Length; i++)
            {
                var g = _gens[i];
                if (g?.Root == null) continue;
                int level = s.Levels[i];
                bool unlocked = s.Unlocked[i];
                if (level == g.Level && unlocked == g.Unlocked) continue;

                bool first = g.Level < 0;
                bool wasBuilt = g.Level > 0;
                g.Level = level;
                g.Unlocked = unlocked;
                bool built = level > 0;
                g.Root.gameObject.SetActive(unlocked || built);
                SetGhost(g, !built);
                g.Pipe.SetActive(built);
                if (!first)
                {
                    if (built && !wasBuilt)
                    {
                        g.Pop = 0.2f;
                        g.PopVel = 0;
                        newlyBuilt?.Add(i);
                    }
                    else if (built)
                    {
                        g.PopVel += 2.5f;
                        leveled?.Add(i);
                    }
                }
                int ms = s.Milestones != null ? s.Milestones[i] : 0;
                g.Badge.SetActive(built && ms > 0);
                if (ms > 0)
                {
                    var c = ms >= 3 ? new Color(1f, 0.82f, 0.25f) : ms == 2 ? new Color(0.82f, 0.86f, 0.95f) : new Color(0.85f, 0.52f, 0.28f);
                    g.BadgeMat.SetColor(Mats.BaseColorId, c);
                    g.BadgeMat.SetColor(Mats.EmissionId, c * 0.35f);
                }
            }

            int rings = s.ChamberLevel >= 20 ? 3 : s.ChamberLevel >= 10 ? 2 : s.ChamberLevel >= 1 ? 1 : 0;
            for (int i = 0; i < 3; i++) _chamberRings[i].SetActive(i < rings);
        }

        void SetGhost(Generator g, bool ghost)
        {
            if (g.Ghost == ghost) return;
            g.Ghost = ghost;
            foreach (var r in g.Renderers)
            {
                if (ghost)
                {
                    _realMaterial[r] = r.sharedMaterial;
                    r.sharedMaterial = Mats.Ghost();
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                else if (_realMaterial.TryGetValue(r, out var real))
                {
                    r.sharedMaterial = real;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
            }
            foreach (var p in g.Puffs) p.gameObject.SetActive(!ghost);
            if (g.Arc != null) g.Arc.positionCount = 0;
        }

        public void Tick(float dt, in State s)
        {
            _time += dt;

            for (int i = 0; i < _gens.Length; i++)
            {
                var g = _gens[i];
                if (g?.Root == null || !g.Root.gameObject.activeSelf) continue;
                bool built = g.Level > 0;

                // Springy pop when built or levelled; bigger machines at higher levels.
                float target = built ? 1f + 0.14f * Mathf.Clamp01(Mathf.Log10(g.Level + 1) / 2.5f) : 1f;
                g.PopVel += (target - g.Pop) * 120f * dt;
                g.PopVel *= Mathf.Exp(-9f * dt);
                g.Pop += g.PopVel * dt;
                g.Visual.localScale = Vector3.one * Mathf.Max(0.05f, g.Pop);

                if (!built) continue;
                float boost = BoostFor(g.Id, s);
                if (g.Spin != null)
                {
                    float speed = g.SpinSpeed;
                    if (g.Id == "turbine") speed = 60 + 520 * s.Wind * (1 + boost);
                    else if (g.Id == "condenser") speed = 120 + 360 * s.Cloud * (1 + boost);
                    g.Spin.Rotate(g.SpinAxis, speed * dt, Space.Self);
                }
                if (g.Bob != null)
                    g.Bob.localPosition = new Vector3(g.Bob.localPosition.x, g.BobBase + 0.12f * Mathf.Abs(Mathf.Sin(_time * (2.5f + s.Pressure * 4f))), g.Bob.localPosition.z);
                if (g.Needle != null)
                    g.Needle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(70, -70, Mathf.Clamp01(s.Pressure)) + Mathf.Sin(_time * 7) * 4);
                if (g.GlowMat != null)
                {
                    float flicker = g.Id == "furnace" ? 0.7f + 0.3f * Mathf.PerlinNoise(_time * 5f, i) : 0.6f + 0.4f * Mathf.Sin(_time * 3f + i);
                    g.GlowMat.SetColor(Mats.EmissionId, g.GlowColor * (0.35f + boost * 2.2f) * flicker);
                }
                for (int p = 0; p < g.Puffs.Count; p++)
                {
                    var puff = g.Puffs[p];
                    if (g.Id == "hydro")
                    {
                        float t = Mathf.Repeat(_time * 0.6f + p / (float)g.Puffs.Count, 1f);
                        puff.localPosition = new Vector3(-0.1f + p * 0.1f, 0.75f + t * 0.6f, -0.66f);
                        puff.localScale = Vector3.one * (0.6f + t * 0.6f) * (1 - t * 0.6f);
                    }
                    else
                    {
                        float t = Mathf.Repeat(_time * (0.35f + boost * 0.3f) + p / (float)g.Puffs.Count, 1f);
                        var basePos = g.Id == "furnace" ? new Vector3(0.35f, 2.25f, 0.3f) : new Vector3(0, 1.25f, 0);
                        puff.localPosition = basePos + new Vector3(Mathf.Sin(t * 5 + p) * 0.15f, t * 1.6f, 0);
                        float sc = (g.Id == "furnace" ? 0.16f : 0.22f) * (0.5f + t) * (1 - Mathf.SmoothStep(0.7f, 1f, t)) * (1 + boost * 0.6f);
                        puff.localScale = Vector3.one * Mathf.Max(0.001f, sc);
                    }
                }
                if (g.Arc != null)
                {
                    bool show = Mathf.Repeat(_time * (2 + s.Electric * 6 + boost * 8), 1f) < 0.35f;
                    if (show)
                    {
                        var start = new Vector3(0, 2.35f, 0);
                        var dir = Quaternion.Euler(0, (float)_rng.NextDouble() * 360, 0) * new Vector3(1, 0.2f, 0);
                        g.Arc.positionCount = 5;
                        for (int k = 0; k < 5; k++)
                            g.Arc.SetPosition(k, start + dir * (0.32f + k * 0.14f) + new Vector3(0, ((float)_rng.NextDouble() - 0.5f) * 0.2f, ((float)_rng.NextDouble() - 0.5f) * 0.2f));
                    }
                    else g.Arc.positionCount = 0;
                }
            }

            // Skin animations: spinning gears, orbiting shards and rings.
            float stormSpin = 1 + s.StormLevel * 2.5f;
            foreach (var sp in _spinners)
            {
                if (sp.T == null) continue;
                if (sp.Orbit > 0)
                {
                    float a = (sp.Phase + _time * sp.Speed * stormSpin) * Mathf.Deg2Rad;
                    sp.T.localPosition = new Vector3(Mathf.Cos(a) * sp.Orbit, sp.Height + Mathf.Sin(_time * 1.3f + sp.Phase) * 0.2f, Mathf.Sin(a) * sp.Orbit);
                    sp.T.localRotation = Quaternion.Euler(20, -a * Mathf.Rad2Deg, 15);
                }
                else sp.T.Rotate(sp.Axis, sp.Speed * stormSpin * dt, Space.Self);
            }
            for (int i = 0; i < _smoke.Count; i++)
            {
                float t = Mathf.Repeat(_time * 0.3f + i * 0.5f, 1f);
                var baseY = DomeTop + (i == 0 ? 0.8f : 1.0f);
                _smoke[i].localPosition = new Vector3(i == 0 ? -0.28f : 0.28f, baseY + t * 1.2f, 0.05f + Mathf.Sin(t * 4) * 0.1f);
                _smoke[i].localScale = Vector3.one * Mathf.Max(0.001f, 0.2f * (0.5f + t) * (1 - Mathf.SmoothStep(0.65f, 1f, t)));
            }
            if (_shape.Glow > 0.9f)
                _glowAccentMat.SetColor(Mats.EmissionId, _theme.Accent * (0.8f + _shape.Glow * 1.6f) * (0.8f + 0.2f * Mathf.Sin(_time * 3f)));

            // Dome cloud swirl: grows with charge, darkens and spins faster in a storm.
            float size = Mathf.Lerp(0.55f, 1f, Mathf.Max(s.Charge, s.StormLevel));
            float spin = _time * (0.8f + s.StormLevel * 2.6f + s.Wind * 0.8f);
            _cloudMat.SetColor(Mats.BaseColorId, Color.Lerp(_theme.Cloud, _theme.CloudShade, Mathf.Clamp01(s.Charge * 0.45f + s.StormLevel * 0.75f)));
            bool orient = _cloudStyle == "wisp" || _cloudStyle == "petal";
            for (int i = 0; i < _cloudBlobs.Count; i++)
            {
                float a = spin + i * Mathf.PI * 2 / _cloudBlobs.Count;
                float r = (0.55f + 0.18f * Mathf.Sin(_time * 1.3f + i)) * size;
                _cloudBlobs[i].localPosition = DomeCenter + new Vector3(Mathf.Cos(a) * r, 0.75f + Mathf.Sin(a * 2 + _time) * 0.12f + (i % 2) * 0.18f, Mathf.Sin(a) * r);
                float k = (0.36f + 0.08f * Mathf.Sin(_time * 2 + i * 1.7f)) * size;
                _cloudBlobs[i].localScale = Vector3.Scale(_blobScale, Vector3.one * k);
                if (orient) _cloudBlobs[i].localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
                else if (_cloudStyle == "cubes" || _cloudStyle == "faceted") _cloudBlobs[i].localRotation = Quaternion.Euler(_time * 20 + i * 40, _time * 30 + i * 25, 0);
            }
            float glowAmount = Mathf.Clamp01(0.2f + s.Charge * 0.35f + s.StormLevel * 0.7f);
            _glowMat.SetColor(Mats.ColorId, s.Glow * (0.55f * glowAmount + 0.08f * Mathf.Sin(_time * 4)));
            _glow.localScale = Vector3.one * (3.6f + 1.4f * glowAmount);
            if (ViewCamera != null) _glow.rotation = Quaternion.LookRotation(_glow.position - ViewCamera.position);

            // Charge ring: lit segments show charge, blinking when full, all hot during a storm.
            int lit = s.Storming ? 24 : Mathf.FloorToInt(s.Charge * 24 + 0.0001f);
            bool fullBlink = s.Charge >= 1 && !s.Storming && Mathf.Repeat(_time * 2, 1) < 0.5f;
            for (int i = 0; i < _chargeSegments.Count; i++)
            {
                var m = fullBlink ? _segStorm : i < lit ? (s.Storming ? _segStorm : _segOn) : _segOff;
                if (_chargeSegments[i].sharedMaterial != m) _chargeSegments[i].sharedMaterial = m;
            }

            bool blink = Mathf.Repeat(_time, s.Charge >= 1 ? 0.5f : 1.4f) < 0.25f;
            _tipMat.SetColor(Mats.EmissionId, blink ? new Color(3f, 0.6f, 0.4f) : new Color(0.4f, 0.05f, 0.05f));
            for (int i = 0; i < _panelLights.Count; i++)
                _panelLights[i].localScale = Vector3.one * (Mathf.Repeat(_time * 1.3f + i * 0.37f, 1f) < 0.6f ? 1f : 0.65f);

            // Mini lightning inside the dome.
            _boltTimer -= dt;
            if ((s.Storming || s.Charge > 0.85f) && _boltTimer <= 0)
            {
                _boltTimer = s.Storming ? 0.25f + (float)_rng.NextDouble() * 0.45f : 1.3f + (float)_rng.NextDouble() * 1.5f;
                SpawnDomeBolt(s.Storming);
            }
            float width = _theme.BoltStyle == "thick" ? 0.12f : 0.07f;
            for (int i = 0; i < _domeBolts.Count; i++)
            {
                if (_domeBoltLife[i] <= 0) continue;
                _domeBoltLife[i] -= dt;
                if (_domeBoltLife[i] <= 0) _domeBolts[i].positionCount = 0;
                else _domeBolts[i].widthMultiplier = width * Mathf.Clamp01(_domeBoltLife[i] * 6f);
            }
        }

        readonly List<Vector3> _boltPoints = new List<Vector3>();

        public void SpawnDomeBolt(bool strong)
        {
            int count = _theme != null && _theme.BoltStyle == "twin" ? 2 : 1;
            for (int n = 0; n < count; n++)
            {
                int slot = 0;
                for (int i = 0; i < _domeBolts.Count; i++)
                    if (_domeBoltLife[i] <= 0) { slot = i; break; }
                var lr = _domeBolts[slot];
                _boltPoints.Clear();
                bool zig = _theme != null && _theme.BoltStyle == "zigzag";
                var p = DomeCenter + new Vector3(((float)_rng.NextDouble() - 0.5f) * 0.8f, 1.05f, ((float)_rng.NextDouble() - 0.5f) * 0.8f);
                _boltPoints.Add(p);
                int step = 0;
                while (p.y > DomeCenter.y + 0.12f)
                {
                    float side = zig ? (step++ % 2 == 0 ? 0.3f : -0.3f) : ((float)_rng.NextDouble() - 0.5f) * 0.45f;
                    p += new Vector3(side, -0.18f - (float)_rng.NextDouble() * 0.12f, ((float)_rng.NextDouble() - 0.5f) * 0.45f);
                    p.x = Mathf.Clamp(p.x, -1.1f, 1.1f);
                    p.z = Mathf.Clamp(p.z, -1.1f, 1.1f);
                    _boltPoints.Add(p);
                }
                lr.positionCount = _boltPoints.Count;
                for (int i = 0; i < _boltPoints.Count; i++) lr.SetPosition(i, _boltPoints[i]);
                _domeBoltLife[slot] = strong ? 0.22f : 0.14f;
            }
        }

        static float BoostFor(string id, in State s)
        {
            if (s.BoostOn == null) return 0;
            int b = id == "condenser" ? 0 : id == "tesla" ? 1 : id == "furnace" ? 2 : id == "turbine" ? 3 : -1;
            return b >= 0 && b < s.BoostOn.Length && s.BoostOn[b] ? 1 : 0;
        }

        public Vector3 GeneratorWorldPosition(int machine)
        {
            var g = machine >= 0 && machine < _gens.Length ? _gens[machine] : null;
            return g?.Root != null ? g.Root.position + Vector3.up * 1.2f : DomeWorldCenter;
        }
    }
}
