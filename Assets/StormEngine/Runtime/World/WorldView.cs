using System;
using System.Collections.Generic;
using StormEngine.Core;
using StormEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace StormEngine.World
{
    /// <summary>
    /// The 2.5D diorama: owns the floating island, the machine, the camera rig, weather effects and
    /// post-processing, and feeds them from the game state every frame. The UI sits on top of it.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        // Content framing: half extents (world units) of what must stay visible around the target.
        static readonly Vector3 Target = new Vector3(0.6f, 1.3f, 0.8f);
        const float HalfWidthWide = 8.6f;
        const float HalfWidthNarrow = 6.4f;
        const float HalfHeight = 4.8f;
        const float BaseFov = 30f;
        const float MinDistance = 22f;
        const float MaxDistance = 40f;
        const float DefaultPitch = 27f;

        StormApp _app;
        Camera _cam;
        UniversalAdditionalCameraData _camData;
        EnvironmentRig _env;
        DetailRig _detail;
        MachineRig _machine;
        DecorRig _decor;
        string[] _machineIds;
        int _quality = 2;
        float _radius = 9.5f;
        IslandLayout _visit;
        int[] _visitLevels;
        bool[] _visitUnlocked;
        readonly List<Vector2> _decorSpots = new List<Vector2>();
        MachineRig.State _state;
        int[] _milestones;
        bool[] _boostOn;
        int _chamber = -1;
        readonly List<int> _built = new List<int>();
        readonly List<int> _leveled = new List<int>();
        readonly string[] _equipped = new string[5];
        Theme _theme;
        float _stormLevel;
        float _time;

        // Camera.
        Rect _area = new Rect(0.1f, 0.1f, 0.8f, 0.8f);
        Rect _areaSmooth;
        bool _areaInit;
        float _yaw, _yawTarget, _pitch = DefaultPitch, _pitchTarget = DefaultPitch;
        float _zoom = 1f, _zoomTarget = 1f;
        float _shake;
        float _lastInteraction = -100f;
        float _distance = 28f;

        // Input.
        bool _pressed, _dragging;
        Vector2 _pressPos, _lastPos;
        float _pressTime;
        float _pinchDistance;

        // Effects.
        ParticleSystem _rain, _sparks, _puffs;
        readonly List<SkyBolt> _bolts = new List<SkyBolt>();
        Volume _volume;
        Bloom _bloom;
        Vignette _vignette;
        ColorAdjustments _grade;
        SplitToning _split;
        DepthOfField _dof;
        static readonly int HemiStrengthId = Shader.PropertyToID("_HemiStrength");
        static readonly int VibranceId = Shader.PropertyToID("_VibranceBoost");
        float _flash;

        // Launch intro (see PlayIntro): seconds in, or -1 when not playing.
        static readonly float[] IntroStrikes = { 0.6f, 1.15f, IntroTitleAt };
        const float IntroClear = 1.95f;
        float _introT = -1f, _introSpeed = 1f, _introStorm;
        int _introStrikes;

        // Pipeline settings tuned per device and quality. The originals are put back when the view
        // goes away, so the Editor never saves the runtime values into the project.
        UniversalRenderPipelineAsset _pipeline;
        float _baseRenderScale = 1f, _baseShadowDistance = 80f;
        int _baseCascades = 2, _baseMsaa = 4;
        float _shadowDistance;
        float _resolutionScale = 1f;
        readonly System.Random _rng = new System.Random(7);

        sealed class SkyBolt
        {
            public LineRenderer Core;
            public LineRenderer Glow;
            public LineRenderer Branch;
            public float Life;
            public float MaxLife;
            public float Width = 1f;
        }

        /// <summary>Raised when the player taps a machine (index) or the dome (-1).</summary>
        public event Action<int> Tapped;

        /// <summary>Island edit mode: taps select decorations and move them instead of hitting machines.</summary>
        public bool EditMode;
        /// <summary>Edit mode: a decoration was tapped (index into the placed list).</summary>
        public event Action<int> DecorTapped;
        /// <summary>Edit mode: empty ground was tapped (island x, z).</summary>
        public event Action<Vector2> GroundTapped;

        public bool IsVisiting => _visit != null;
        public IslandLayout Visiting => _visit;
        public float IslandRadius => _radius;

        /// <summary>Set by the UI: true when a panel position is covered by interactive UI.</summary>
        public Func<Vector2, bool> IsOverUi;

        /// <summary>The UI panel used for coordinate conversion.</summary>
        public IPanel Panel;

        public bool ReduceMotion;
        public Camera Camera => _cam;
        /// <summary>Smoothed 0..1 storm intensity (sky, rain, music ducking), including the launch intro's storm.</summary>
        public float StormLevel => Mathf.Max(_stormLevel, SkyStorm);

        /// <summary>Storm look asked for by the launch intro or a cinematic, on top of the game's weather.</summary>
        float SkyStorm => Mathf.Max(_introStorm, CinematicStorm);

        /// <summary>When the launch intro's last, biggest strike lands (the title appears with it), and its length.</summary>
        public const float IntroTitleAt = 1.75f, IntroLength = 3.7f;

        /// <summary>Seconds into the launch intro; negative when it is not playing.</summary>
        public float IntroTime => _introT;

        public bool IntroPlaying => _introT >= 0f;

        // ------------------------------------------------------------------ cinematics

        /// <summary>A fixed camera for cinematics such as the trailer.</summary>
        public struct CameraShot
        {
            public Vector3 Focus;
            public float Yaw, Pitch, Distance, Fov;
            /// <summary>Where the focus sits on screen, -1..1 from the centre (x to the right, y down).</summary>
            public float ScreenX, ScreenY;
        }

        /// <summary>While set, the camera holds this shot instead of the menu or play framing.</summary>
        public CameraShot? Cinematic;

        /// <summary>Storm look (sky, rain, colour grade) for cinematics, 0..1, on top of the game's weather.</summary>
        public float CinematicStorm;

        /// <summary>A lightning strike on the engine's antenna, as in the launch intro.</summary>
        public void CinematicStrike(bool big) => IntroStrike(big);

        /// <summary>Puts the weather straight at a storm level instead of easing there (for cuts).</summary>
        public void SettleWeather(float level) => _stormLevel = Mathf.Clamp01(level);

        /// <summary>
        /// Launch intro: the camera starts low beside the engine in a dark storm, the engine draws
        /// three lightning strikes (the last and biggest one brings in the title), then the sky clears
        /// and the camera glides out to the main menu view, so the intro ends exactly on the menu.
        /// </summary>
        public void PlayIntro()
        {
            _introT = 0f;
            _introSpeed = 1f;
            _introStrikes = 0;
            _introStorm = 1f;
        }

        /// <summary>A tap during the intro: play the rest quickly (every step still happens).</summary>
        public void SkipIntro()
        {
            if (_introT >= 0f) _introSpeed = Mathf.Max(3.5f, _introSpeed * 2f);
        }

        public static WorldView Create(StormApp app)
        {
            var go = new GameObject("World");
            var view = go.AddComponent<WorldView>();
            view.Init(app);
            return view;
        }

        void Init(StormApp app)
        {
            _app = app;
            CapturePipeline();
            SetupCamera();
            SetupPost();

            _env = new EnvironmentRig(transform);
            _detail = new DetailRig(transform, app.Content);
            var ids = new string[app.Content.Machines.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = app.Content.Machines[i].Id;
            _machineIds = ids;
            _chamber = app.Content.MachineIndex("chamber");
            _machine = new MachineRig(transform, ids) { ViewCamera = _cam.transform };
            _milestones = new int[ids.Length];
            _boostOn = new bool[app.Content.Boosts.Length];

            Array.Copy(app.Game.EquippedCosmetics, _equipped, _equipped.Length);
            _theme = Theme.From(app.Game);
            _radius = app.Game.IslandRadius;
            RebuildWorld(app.Game.Placed);
            _machine.Build(_theme, _radius);
            _decor = new DecorRig(transform, app.Content);
            _decor.Sync(app.Game.Placed, _radius);

            _rain = CreateRain();
            _sparks = CreateSparks();
            _puffs = CreatePuffs();
            for (int i = 0; i < 3; i++) _bolts.Add(CreateBolt());

            var g = app.Game;
            g.LightningStruck += OnStrike;
            g.StormStarted += _ =>
            {
                Flash(0.5f);
                Shake(0.25f);
                Burst(_machine.DomeWorldCenter, _theme.BoltGlow, 40, 6f);
            };
            g.StormEnded += (r, f, s) => Burst(_machine.DomeWorldCenter, new Color(1f, 0.85f, 0.3f), 30, 5f);
            g.PhenomenonDiscovered += p => Burst(_machine.DomeWorldCenter, Palette.ForPheno(app.Content.Phenomena[p].Id), 60, 7f);
            UpdateMachine(0);
        }

        void OnDestroy()
        {
            if (_app?.Game != null) _app.Game.LightningStruck -= OnStrike;
            RestorePipeline();
        }

        void CapturePipeline()
        {
            _pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_pipeline == null) return;
            _baseRenderScale = _pipeline.renderScale;
            _baseShadowDistance = _pipeline.shadowDistance;
            _baseCascades = _pipeline.shadowCascadeCount;
            _baseMsaa = _pipeline.msaaSampleCount;
        }

        void RestorePipeline()
        {
            if (_pipeline == null) return;
            _pipeline.renderScale = _baseRenderScale;
            _pipeline.shadowDistance = _baseShadowDistance;
            _pipeline.shadowCascadeCount = _baseCascades;
            _pipeline.msaaSampleCount = _baseMsaa;
        }

        /// <summary>
        /// Extra 3D resolution step-down picked by the frame-rate watchdog (1 = none). Returns false
        /// when it cannot go any lower.
        /// </summary>
        public bool LowerResolution()
        {
            if (_pipeline == null || !Application.isMobilePlatform || _resolutionScale <= 0.72f) return false;
            _resolutionScale = Mathf.Max(0.7f, _resolutionScale - 0.15f);
            ApplyRenderScale();
            return true;
        }

        /// <summary>
        /// Phones draw the 3D scene below native resolution (the interface stays sharp): with MSAA a
        /// cartoon scene looks the same, and the GPU keeps room for a smooth frame rate.
        /// </summary>
        void ApplyRenderScale()
        {
            if (_pipeline == null) return;
            float scale = 1f;
            if (Application.isMobilePlatform)
            {
                float shortSide = Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));
                float target = _quality >= 2 ? 1024f : _quality == 1 ? 864f : 720f;
                scale = Mathf.Clamp(target / shortSide, 0.5f, 1f);
            }
            _pipeline.renderScale = Mathf.Clamp(scale * _resolutionScale, 0.4f, 1f);
        }

        // ------------------------------------------------------------------ setup

        void SetupCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = camGo.AddComponent<Camera>();
            }
            _cam.orthographic = false;
            _cam.fieldOfView = BaseFov;
            _cam.nearClipPlane = 0.5f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.Skybox;
            _cam.allowHDR = true;
            _cam.allowMSAA = true;
            _camData = _cam.GetUniversalAdditionalCameraData();
            _camData.renderPostProcessing = true;
            _camData.renderShadows = true;
            _camData.antialiasing = AntialiasingMode.None;
            _camData.stopNaN = false;
            _camData.dithering = true;
        }

        void SetupPost()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "StormPost";
            _volume.sharedProfile = profile;

            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.95f);
            _bloom.intensity.Override(0.75f);
            _bloom.scatter.Override(0.68f);
            _bloom.highQualityFiltering.Override(false);

            _grade = profile.Add<ColorAdjustments>(true);
            _grade.saturation.Override(12f);
            _grade.contrast.Override(6f);
            _grade.postExposure.Override(0f);

            _vignette = profile.Add<Vignette>(true);
            _vignette.intensity.Override(0.16f);
            _vignette.smoothness.Override(0.5f);
            _vignette.color.Override(new Color(0.05f, 0.07f, 0.2f));

            // Warm highlights and cool shadows: sunnier, more colourful.
            _split = profile.Add<SplitToning>(true);
            _split.shadows.Override(new Color(0.42f, 0.5f, 0.95f));
            _split.highlights.Override(new Color(1f, 0.86f, 0.62f));
            _split.balance.Override(-15f);

            // High only: a soft tilt-shift blur on the distant scenery makes the island read as a diorama.
            _dof = profile.Add<DepthOfField>(true);
            _dof.mode.Override(DepthOfFieldMode.Gaussian);
            _dof.gaussianStart.Override(40f);
            _dof.gaussianEnd.Override(90f);
            _dof.gaussianMaxRadius.Override(1f);
            _dof.highQualitySampling.Override(false);
            _dof.active = false;
        }

        /// <summary>Rebuilds the island, props and detail layer for the current theme and radius.</summary>
        void RebuildWorld(IList<PlacedDecor> decor)
        {
            _env.Build(_theme, _radius, DecorSpots(decor));
            _detail.Build(_theme, _radius, decor, _env.PropSpots, _env.TreeSpots);
        }

        /// <summary>0 = low (no shadows or post), 1 = medium, 2 = high.</summary>
        public void ApplyQuality(int quality)
        {
            if (_camData == null) return;
            _quality = quality;
            _camData.renderPostProcessing = quality >= 1;
            _camData.renderShadows = quality >= 1;
            _cam.allowMSAA = quality >= 1;
            _detail?.SetQuality(quality);
            _bloom.highQualityFiltering.value = quality >= 2;
            // The tilt-shift blur needs a depth copy: too costly for phones.
            _dof.active = quality >= 2 && !Application.isMobilePlatform;
            if (_pipeline != null)
            {
                _pipeline.msaaSampleCount = quality >= 2 ? 4 : 2;
                // One tight cascade over the island is sharper and cheaper than two over 80 units.
                _pipeline.shadowCascadeCount = 1;
                ApplyRenderScale();
            }
            _split.active = quality >= 1;
            // Simple has no colour grade, so the shader adds the colour and depth itself.
            Shader.SetGlobalFloat(HemiStrengthId, quality == 0 ? 0.75f : 0.55f);
            Shader.SetGlobalFloat(VibranceId, quality == 0 ? 0.2f : quality == 1 ? 0.04f : 0.06f);
            _env.Sun.shadows = quality >= 2 ? LightShadows.Soft : quality == 1 ? LightShadows.Hard : LightShadows.None;
            _rainScale = quality == 0 ? 0.35f : quality == 1 ? 0.7f : 1f;
        }

        float _rainScale = 1f;

        ParticleSystem CreateRain()
        {
            var go = new GameObject("Rain");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0, 15f, 1f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.startLifetime = 1.0f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
            main.startColor = new Color(0.78f, 0.86f, 1f, 0.55f);
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(30f, 0.5f, 26f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-3f, -2f);
            vel.y = new ParticleSystem.MinMaxCurve(-26f, -22f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.035f;
            r.lengthScale = 1f;
            r.sharedMaterial = Mats.Glow(Color.white, false, Mats.SoftDot);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            ps.Play();
            return ps;
        }

        ParticleSystem CreateSparks()
        {
            var go = new GameObject("Sparks");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.gravityModifier = 0.9f;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Glow(new Color(2.2f, 2.2f, 2.2f, 1f), true, Mats.SoftDot);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        ParticleSystem CreatePuffs()
        {
            var go = new GameObject("Puffs");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.maxParticles = 200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90, 0, 0);
            var emission = ps.emission;
            emission.rateOverTime = 0;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 1.4f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Glow(Color.white, false, Mats.SoftDot);
            r.shadowCastingMode = ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        SkyBolt CreateBolt()
        {
            LineRenderer Line(string name, float width, Material mat)
            {
                var go = new GameObject(name);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.widthMultiplier = width;
                lr.numCapVertices = 3;
                lr.numCornerVertices = 2;
                lr.sharedMaterial = mat;
                lr.textureMode = LineTextureMode.Stretch;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.positionCount = 0;
                return lr;
            }
            return new SkyBolt
            {
                Core = Line("BoltCore", 0.14f, Mats.Glow(Color.white * 6f, true)),
                Glow = Line("BoltGlow", 1.1f, Mats.Glow(Color.white, true, Mats.SoftLine)),
                Branch = Line("BoltBranch", 0.08f, Mats.Glow(Color.white * 4f, true)),
            };
        }

        // ------------------------------------------------------------------ public API

        /// <summary>Area of the screen (UI panel space) the machine should be framed in.</summary>
        public void SetViewArea(Rect areaPanel, Vector2 rootSize)
        {
            // Written as !(a >= b) so NaN sizes from an unfinished layout are rejected too.
            if (!(rootSize.x >= 1) || !(rootSize.y >= 1) || !(areaPanel.width >= 1) || !(areaPanel.height >= 1)) return;
            if (float.IsNaN(areaPanel.x) || float.IsNaN(areaPanel.y)) return;
            _area = new Rect(areaPanel.x / rootSize.x, areaPanel.y / rootSize.y, areaPanel.width / rootSize.x, areaPanel.height / rootSize.y);
            if (!_areaInit)
            {
                _areaInit = true;
                _areaSmooth = _area;
            }
        }

        public Vector3 DomeWorld => _machine.DomeWorldCenter;

        public Vector3 GeneratorWorld(int machine) => _machine.GeneratorWorldPosition(machine);

        /// <summary>Converts a world position to UI panel coordinates.</summary>
        public Vector2 ToPanel(Vector3 world)
        {
            if (Panel != null) return RuntimePanelUtils.CameraTransformWorldToPanel(Panel, world, _cam);
            var sp = _cam.WorldToScreenPoint(world);
            return new Vector2(sp.x, Screen.height - sp.y);
        }

        /// <summary>Approximate on-screen radius (panel units) of the dome.</summary>
        public float DomePanelRadius()
        {
            var c = _machine.DomeWorldCenter;
            return Vector2.Distance(ToPanel(c), ToPanel(c + _cam.transform.right * MachineRig.DomeRadius));
        }

        public void Flash(float amount)
        {
            if (ReduceMotion) amount *= 0.35f;
            _flash = Mathf.Max(_flash, amount);
            _env.Flash(amount);
        }

        public void Shake(float amount)
        {
            if (ReduceMotion) return;
            _shake = Mathf.Max(_shake, amount);
        }

        public void Burst(Vector3 world, Color color, int count, float speed = 4f)
        {
            if (ReduceMotion) count = Mathf.Max(1, count / 3);
            var ep = new ParticleSystem.EmitParams
            {
                position = world,
                applyShapeToPosition = true,
                startColor = color,
            };
            var main = _sparks.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            _sparks.Emit(ep, count);
        }

        public void Puff(Vector3 world, int count = 10)
        {
            var ep = new ParticleSystem.EmitParams { position = world, applyShapeToPosition = true };
            _puffs.Emit(ep, count);
        }

        void OnStrike(int count)
        {
            var bolt = _bolts[0];
            foreach (var b in _bolts)
                if (b.Life <= 0) { bolt = b; break; }
            FireBolt(bolt, _machine.AntennaTip);
            if (_theme.BoltStyle == "twin")
                foreach (var extra in _bolts)
                    if (extra != bolt && extra.Life <= 0)
                    {
                        FireBolt(extra, _machine.AntennaTip);
                        break;
                    }
            Flash(0.55f + 0.1f * Mathf.Min(count, 4));
            Shake(0.18f);
            Burst(_machine.AntennaTip, _theme.BoltGlow, 16, 5f);
            _machine.SpawnDomeBolt(true);
        }

        readonly List<Vector3> _path = new List<Vector3>();

        void FireBolt(SkyBolt bolt, Vector3 end)
        {
            string style = _theme.BoltStyle;
            float side = (float)_rng.NextDouble() * 2 - 1;
            var start = end + new Vector3(side * 9f, 22f + (float)_rng.NextDouble() * 6f, 6f + (float)_rng.NextDouble() * 8f);
            _path.Clear();
            _path.Add(start);
            _path.Add(end);
            // Midpoint displacement for a jagged cartoon bolt.
            float offset = 3.2f;
            for (int pass = 0; pass < 4; pass++)
            {
                for (int i = _path.Count - 1; i > 0; i--)
                {
                    var a = _path[i - 1];
                    var b = _path[i];
                    var mid = (a + b) * 0.5f;
                    float jitter = style == "zigzag" ? (i % 2 == 0 ? 0.5f : -0.5f) * offset * 1.3f : ((float)_rng.NextDouble() - 0.5f) * offset;
                    mid += new Vector3(jitter, ((float)_rng.NextDouble() - 0.5f) * offset * 0.3f, ((float)_rng.NextDouble() - 0.5f) * offset);
                    _path.Insert(i, mid);
                }
                offset *= 0.5f;
            }
            SetLine(bolt.Core, _path);
            SetLine(bolt.Glow, _path);

            // A side branch from somewhere in the upper half.
            int from = _rng.Next(2, _path.Count / 2);
            var p = _path[from];
            var dir = (_path[from + 1] - _path[from - 1]).normalized;
            var sideDir = Vector3.Cross(dir, Vector3.forward).normalized * (side > 0 ? -1 : 1);
            _path.Clear();
            _path.Add(p);
            for (int i = 0; i < 5; i++)
            {
                p += (dir * 1.4f + sideDir * 1.1f) + new Vector3(((float)_rng.NextDouble() - 0.5f), ((float)_rng.NextDouble() - 0.5f) * 0.5f, ((float)_rng.NextDouble() - 0.5f));
                _path.Add(p);
            }
            SetLine(bolt.Branch, _path);

            var core = Color.Lerp(_theme.BoltCore, Color.white, 0.4f);
            bolt.Core.sharedMaterial.SetColor(Mats.ColorId, core * 7f);
            bolt.Branch.sharedMaterial.SetColor(Mats.ColorId, core * 4f);
            bolt.Glow.sharedMaterial.SetColor(Mats.ColorId, _theme.Bolt * 1.6f);
            bolt.MaxLife = bolt.Life = 0.32f;
            bolt.Width = style == "thick" ? 1.9f : style == "zigzag" ? 0.8f : 1f;
        }

        static void SetLine(LineRenderer lr, List<Vector3> pts)
        {
            lr.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) lr.SetPosition(i, pts[i]);
        }

        // ------------------------------------------------------------------ loop

        void Update()
        {
            if (_app?.Game == null) return;
            float dt = Mathf.Min(FrameTime.Delta, 0.1f);
            _time += dt;
            TickIntro(dt);
            HandleInput();
            UpdateMachine(dt);
            UpdateEffects(dt);
        }

        void LateUpdate()
        {
            if (_app?.Game == null) return;
            UpdateCamera(Mathf.Min(FrameTime.Delta, 0.1f));
        }

        void TickIntro(float dt)
        {
            if (_introT < 0f) return;
            // A loading hitch must not skip the opening: cap each step.
            _introT += Mathf.Min(dt, 1f / 30f) * _introSpeed;
            while (_introStrikes < IntroStrikes.Length && _introT >= IntroStrikes[_introStrikes])
                IntroStrike(++_introStrikes == IntroStrikes.Length);
            _introStorm = 1f - Smooth(Mathf.InverseLerp(IntroClear, IntroLength - 0.35f, _introT));
            if (_introT >= IntroLength)
            {
                _introT = -1f;
                _introStorm = 0f;
            }
        }

        void IntroStrike(bool big)
        {
            var tip = _machine.AntennaTip;
            int bolts = big ? 2 : 1;
            foreach (var b in _bolts)
                if (bolts > 0 && b.Life <= 0)
                {
                    FireBolt(b, tip);
                    // The intro camera is close: thinner, softer bolts read as lightning, not a beam of light.
                    b.Width *= 0.42f;
                    Dim(b.Core, 0.5f);
                    Dim(b.Branch, 0.5f);
                    Dim(b.Glow, 0.55f);
                    bolts--;
                }
            Flash(big ? 0.5f : 0.35f);
            Shake(big ? 0.4f : 0.15f);
            Burst(tip, _theme.BoltGlow, big ? 50 : 16, big ? 8f : 5f);
            _machine.SpawnDomeBolt(true);
            if (big) Burst(_machine.DomeWorldCenter, _theme.BoltGlow, 36, 6f);
            _app.Sfx?.Thunder(big, big);
        }

        static void Dim(LineRenderer line, float k) =>
            line.sharedMaterial.SetColor(Mats.ColorId, line.sharedMaterial.GetColor(Mats.ColorId) * k);

        static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        List<Vector2> DecorSpots(IList<PlacedDecor> placed)
        {
            _decorSpots.Clear();
            foreach (var p in placed) _decorSpots.Add(new Vector2(p.X, p.Z));
            return _decorSpots;
        }

        void UpdateMachine(float dt)
        {
            var g = _app.Game;
            var c = _app.Content;
            if (_visit != null)
            {
                UpdateVisit(dt);
                return;
            }

            // Island size (terrain expansions).
            if (Mathf.Abs(g.IslandRadius - _radius) > 0.01f)
            {
                _radius = g.IslandRadius;
                RebuildWorld(g.Placed);
                _machine.SetIslandRadius(_radius);
                Puff(new Vector3(0, 1, 0), 30);
                Burst(new Vector3(0, 2, 0), new Color(0.6f, 1f, 0.6f), 40, 7f);
            }
            _decor.Sync(g.Placed, _radius);
            _detail.SyncDecor(g.Placed);
            _detail.SetMachines(g.Build.Machines, _machineIds);

            // Cosmetics.
            bool changed = false;
            for (int i = 0; i < _equipped.Length; i++)
                if (_equipped[i] != g.EquippedCosmetics[i]) changed = true;
            if (changed)
            {
                string oldWorld = _theme.WorldId;
                Array.Copy(g.EquippedCosmetics, _equipped, _equipped.Length);
                _theme = Theme.From(g);
                if (_theme.WorldId != oldWorld) RebuildWorld(g.Placed);
                _machine.ApplyTheme(_theme);
                Puff(_machine.DomeWorldCenter, 14);
                Burst(_machine.DomeWorldCenter, _theme.Accent, 30, 5f);
            }

            float target = g.IsStorming ? 1f : g.StormCharge >= 1 ? 0.3f : (float)g.StormCharge * 0.15f;
            _stormLevel = Mathf.MoveTowards(_stormLevel, target, dt * (target > _stormLevel ? 0.8f : 0.4f));
            // The launch intro charges the engine and keeps it storming until the sky clears.
            bool introStorming = _introT >= IntroStrikes[0] && _introT < IntroClear + 0.3f;
            float introCharge = _introT >= 0f && _introT < IntroClear + 0.3f ? Mathf.Clamp01(0.6f + _introT) : 0f;

            var view = g.View;
            for (int i = 0; i < _milestones.Length; i++) _milestones[i] = c.MilestonesReached(g.Build.Machines[i]);
            for (int i = 0; i < _boostOn.Length; i++) _boostOn[i] = g.BoostActive[i] > 0;
            _state.Levels = g.Build.Machines;
            _state.Unlocked = g.MachineUnlocked;
            _state.Milestones = _milestones;
            _state.Charge = Mathf.Max((float)g.StormCharge, introCharge);
            _state.Storming = g.IsStorming || introStorming;
            _state.StormLevel = StormLevel;
            _state.Glow = view.Dominant >= 0 ? Palette.ForPheno(c.Phenomena[view.Dominant].Id) : _theme.CloudGlow;
            _state.Heat = Level01(view.CatValue(0));
            _state.Humidity = Level01(view.CatValue(1));
            _state.Wind = Level01(view.CatValue(2));
            _state.Cloud = Level01(view.CatValue(3));
            _state.Electric = Level01(view.CatValue(4));
            _state.Pressure = Level01(view.Stat[(int)StatId.BasePressure]);
            _state.BoostOn = _boostOn;
            _state.ChamberLevel = _chamber >= 0 ? g.Build.Machines[_chamber] : 0;

            _built.Clear();
            _leveled.Clear();
            _machine.Apply(_state, _built, _leveled);
            foreach (int m in _built)
            {
                var pos = _machine.GeneratorWorldPosition(m);
                Puff(pos - Vector3.up * 0.6f, 16);
                Burst(pos, new Color(1f, 0.85f, 0.3f), 30, 5f);
                Shake(0.12f);
            }
            foreach (int m in _leveled)
                Burst(_machine.GeneratorWorldPosition(m), new Color(1f, 0.9f, 0.5f), 6, 3f);

            if (dt > 0) _machine.Tick(dt, _state);
        }

        static float Level01(double v) => Mathf.Clamp01((float)(Math.Log10(Math.Max(1, v)) / 3.0));

        // ------------------------------------------------------------------ visiting

        /// <summary>Shows another player's island (view only) until <see cref="ExitVisit"/>.</summary>
        public void EnterVisit(IslandLayout layout)
        {
            if (layout == null) return;
            var c = _app.Content;
            _visit = layout;
            _visitLevels = new int[c.Machines.Length];
            _visitUnlocked = new bool[c.Machines.Length];
            for (int i = 0; i < c.Machines.Length; i++)
            {
                _visitLevels[i] = layout.MachineLevel(c.Machines[i].Id);
                _visitUnlocked[i] = _visitLevels[i] > 0;
            }
            if (_visitLevels[0] <= 0) _visitLevels[0] = 1;
            _theme = Theme.From(c, layout);
            _radius = layout.Radius;
            RebuildWorld(layout.Decor);
            _detail.SetMachines(_visitLevels, _machineIds);
            _machine.Build(_theme, _radius);
            _decor.Sync(layout.Decor, _radius);
            _yawTarget = 0;
            _zoomTarget = 1;
            Puff(new Vector3(0, 1.5f, 0), 24);
        }

        public void ExitVisit()
        {
            if (_visit == null) return;
            _visit = null;
            var g = _app.Game;
            Array.Copy(g.EquippedCosmetics, _equipped, _equipped.Length);
            _theme = Theme.From(g);
            _radius = g.IslandRadius;
            RebuildWorld(g.Placed);
            _machine.Build(_theme, _radius);
            _decor.Sync(g.Placed, _radius);
            Puff(new Vector3(0, 1.5f, 0), 24);
        }

        void UpdateVisit(float dt)
        {
            var c = _app.Content;
            _decor.Sync(_visit.Decor, _radius);
            _stormLevel = Mathf.MoveTowards(_stormLevel, 0, dt * 0.5f);
            for (int i = 0; i < _milestones.Length; i++) _milestones[i] = c.MilestonesReached(_visitLevels[i]);
            for (int i = 0; i < _boostOn.Length; i++) _boostOn[i] = false;
            _state.Levels = _visitLevels;
            _state.Unlocked = _visitUnlocked;
            _state.Milestones = _milestones;
            _state.Charge = Mathf.Repeat(_time * 0.08f, 1f);
            _state.Storming = false;
            _state.StormLevel = _stormLevel;
            _state.Glow = _theme.CloudGlow;
            _state.Heat = _state.Humidity = _state.Wind = _state.Cloud = _state.Electric = _state.Pressure = 0.5f;
            _state.BoostOn = _boostOn;
            _state.ChamberLevel = _chamber >= 0 ? _visitLevels[_chamber] : 0;
            _machine.Apply(_state, null, null);
            if (dt > 0) _machine.Tick(dt, _state);
        }

        // ------------------------------------------------------------------ decorations

        /// <summary>Places a stored copy of a decoration at a free spot. Returns false when there is no room.</summary>
        public bool AutoPlace(string id)
        {
            var g = _app.Game;
            var c = _app.Content;
            int d = c.DecorationIndex(id);
            if (_visit != null || d < 0 || g.StoredCopies(id) <= 0) return false;
            if (!DecorRig.FindSpot(c, c.Decorations[d], g.Placed, _radius, _env.PropSpots, g.Placed.Count * 7919 + d * 31 + (int)(_time * 10), out var spot))
                return false;
            var place = new PlacedDecor { Id = id, X = spot.x, Z = spot.y, Rot = (float)(_rng.NextDouble() * 360.0) };
            if (!g.AddPlacement(place)) return false;
            _decor.Sync(g.Placed, _radius);
            var pos = _decor.WorldPosition(c.Decorations[d], place);
            Puff(pos, 14);
            Burst(pos + Vector3.up * 0.6f, new Color(1f, 0.9f, 0.4f), 18, 4f);
            return true;
        }

        /// <summary>Moves a placed decoration if it fits there (ponds carry their water decorations along).</summary>
        public bool MovePlacement(int index, Vector2 spot)
        {
            var g = _app.Game;
            var c = _app.Content;
            if (_visit != null || index < 0 || index >= g.Placed.Count) return false;
            var place = g.Placed[index];
            int d = c.DecorationIndex(place.Id);
            if (d < 0) return false;
            var def = c.Decorations[d];
            if (def.Kind == DecorKind.Water) return false;
            if (!DecorRig.IsSpotFree(c, def, spot, g.Placed, _radius, _env.PropSpots, index)) return false;
            var delta = spot - new Vector2(place.X, place.Z);
            if (place.Id == "dec_pond")
                foreach (var other in g.Placed)
                {
                    int o = c.DecorationIndex(other.Id);
                    if (o < 0 || c.Decorations[o].Kind != DecorKind.Water) continue;
                    if ((new Vector2(other.X - place.X, other.Z - place.Z)).sqrMagnitude > 1.1f) continue;
                    other.X += delta.x;
                    other.Z += delta.y;
                }
            place.X = spot.x;
            place.Z = spot.y;
            _decor.Sync(g.Placed, _radius);
            Puff(_decor.WorldPosition(def, place), 8);
            return true;
        }

        public void RotatePlacement(int index, float degrees)
        {
            var g = _app.Game;
            if (_visit != null || index < 0 || index >= g.Placed.Count) return;
            g.Placed[index].Rot = Mathf.Repeat(g.Placed[index].Rot + degrees, 360f);
            _decor.Sync(g.Placed, _radius);
        }

        public void SelectDecoration(int index) => _decor.Select(index);

        public Vector3 DecorationWorld(int index) => _decor.WorldPosition(index);

        void UpdateEffects(float dt)
        {
            float storm = StormLevel;
            _env.Tick(dt, storm, _state.Wind);
            _detail.Tick(dt, storm);
            _decor.Tick(dt, _state.Wind, storm);

            var emission = _rain.emission;
            float rain = Mathf.Clamp01(Mathf.Max(_state.Storming ? _stormLevel : 0f, SkyStorm) * 1.2f);
            emission.rateOverTime = rain * 1100f * _rainScale * (ReduceMotion ? 0.4f : 1f);

            foreach (var b in _bolts)
            {
                if (b.Life <= 0) continue;
                b.Life -= dt;
                if (b.Life <= 0)
                {
                    b.Core.positionCount = 0;
                    b.Glow.positionCount = 0;
                    b.Branch.positionCount = 0;
                    continue;
                }
                float t = b.Life / b.MaxLife;
                // Flicker: bright, dip, re-strike, fade.
                float flicker = t > 0.75f ? 1f : t > 0.6f ? 0.35f : t > 0.45f ? 0.9f : t * 1.6f;
                b.Core.widthMultiplier = 0.14f * flicker * b.Width;
                b.Glow.widthMultiplier = 1.1f * flicker * b.Width;
                b.Branch.widthMultiplier = 0.07f * flicker * b.Width;
            }

            _flash = Mathf.Max(0, _flash - dt * 3f);
            _bloom.intensity.value = Mathf.Lerp(0.75f, 1.1f, storm) + _flash * 1.5f;
            _grade.postExposure.value = _flash * 0.55f;
            _grade.saturation.value = Mathf.Lerp(_quality >= 2 ? 22f : 16f, 0f, storm);
            _grade.contrast.value = Mathf.Lerp(6f, 14f, storm);
            _vignette.intensity.value = Mathf.Lerp(0.16f, 0.34f, storm);
        }

        // ------------------------------------------------------------------ camera

        void UpdateCamera(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 6f);
            if (float.IsNaN(_areaSmooth.x) || float.IsNaN(_areaSmooth.width)) _areaSmooth = _area;
            _areaSmooth = new Rect(
                Mathf.Lerp(_areaSmooth.x, _area.x, k), Mathf.Lerp(_areaSmooth.y, _area.y, k),
                Mathf.Lerp(_areaSmooth.width, _area.width, k), Mathf.Lerp(_areaSmooth.height, _area.height, k));

            // Drift back to the hero angle when the player leaves the camera alone.
            if (Time.unscaledTime - _lastInteraction > 7f && !_pressed)
            {
                float back = 1 - Mathf.Exp(-dt * 0.8f);
                _yawTarget = Mathf.Lerp(_yawTarget, 0f, back);
                _pitchTarget = Mathf.Lerp(_pitchTarget, DefaultPitch, back);
                _zoomTarget = Mathf.Lerp(_zoomTarget, 1f, back);
            }
            float s = 1 - Mathf.Exp(-dt * 10f);
            _yaw = Mathf.Lerp(_yaw, _yawTarget, s);
            _pitch = Mathf.Lerp(_pitch, _pitchTarget, s);
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, s);

            float aspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 1.6f;
            var a = _areaSmooth;
            float hw = Mathf.Max(0.05f, a.width), hh = Mathf.Max(0.05f, a.height);
            const float margin = 0.94f;
            // Wide stages show the whole island; narrow (portrait) stages frame the machine tighter.
            float areaAspect = hw * aspect / hh;
            float grow = Mathf.Lerp(1f, _radius / EnvironmentRig.BaseRadius, 0.85f);
            float halfWidth = Mathf.Lerp(HalfWidthNarrow, HalfWidthWide, Mathf.InverseLerp(0.75f, 1.35f, areaAspect)) * grow;
            float halfHeight = HalfHeight * Mathf.Lerp(1f, grow, 0.6f);
            float TanFor(float d) => Mathf.Max(halfHeight / (d * hh * margin), halfWidth / (d * aspect * hw * margin));

            float fov = BaseFov;
            float tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float dist = Mathf.Max(halfHeight / (tan * hh * margin), halfWidth / (tan * aspect * hw * margin));
            if (dist > MaxDistance * grow)
            {
                dist = MaxDistance * grow;
                fov = Mathf.Min(50f, 2f * Mathf.Atan(TanFor(dist)) * Mathf.Rad2Deg);
                tan = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
                dist = Mathf.Max(halfHeight / (tan * hh * margin), halfWidth / (tan * aspect * hw * margin));
            }
            else if (dist < MinDistance)
            {
                dist = MinDistance;
                fov = 2f * Mathf.Atan(TanFor(dist)) * Mathf.Rad2Deg;
            }
            dist *= _zoom;

            float sway = ReduceMotion ? 0f : Mathf.Sin(_time * 0.11f) * 2.5f;
            var focus = Target;
            float yaw = _yaw + sway, pitch = _pitch + Mathf.Sin(_time * 0.07f) * 0.6f;
            // Off-centre projection: the target sits in the middle of the free stage area.
            float offX = -((a.x + a.width * 0.5f) * 2f - 1f), offY = -(1f - (a.y + a.height * 0.5f) * 2f);
            if (_introT >= 0f) BlendIntroCamera(aspect, ref focus, ref yaw, ref pitch, ref dist, ref fov, ref offX, ref offY);
            if (Cinematic is CameraShot shot)
            {
                focus = shot.Focus;
                yaw = shot.Yaw;
                pitch = shot.Pitch;
                dist = shot.Distance;
                fov = shot.Fov;
                offX = -shot.ScreenX;
                offY = shot.ScreenY;
            }
            _distance = dist;
            // Shadows only need to reach the far side of the island: a tight range keeps them crisp.
            float shadowRange = dist + _radius * 1.25f + 6f;
            if (_pipeline != null && Mathf.Abs(shadowRange - _shadowDistance) > 1.5f)
            {
                _shadowDistance = shadowRange;
                _pipeline.shadowDistance = shadowRange;
            }
            if (_dof != null && _dof.active)
            {
                // Keep the whole island sharp; only the far scenery softens.
                _dof.gaussianStart.value = dist + _radius + 6f;
                _dof.gaussianEnd.value = dist + _radius + 60f;
            }

            var rot = Quaternion.Euler(pitch, yaw, 0);
            var pos = focus - rot * Vector3.forward * dist;
            if (_shake > 0.001f)
            {
                float amp = _shake * 0.6f;
                pos += rot * new Vector3((Mathf.PerlinNoise(_time * 25f, 1f) - 0.5f) * amp, (Mathf.PerlinNoise(2f, _time * 25f) - 0.5f) * amp, 0);
                _shake = Mathf.Max(0, _shake - dt * 1.6f);
            }
            _cam.transform.SetPositionAndRotation(pos, rot);

            float near = Mathf.Max(0.5f, dist - 30f), far = dist + 260f;
            _cam.fieldOfView = fov;
            _cam.nearClipPlane = near;
            _cam.farClipPlane = far;
            var proj = Matrix4x4.Perspective(fov, aspect, near, far);
            proj.m02 = offX;
            proj.m12 = offY;
            _cam.projectionMatrix = proj;

            RenderSettings.fogStartDistance = dist + 14f;
            RenderSettings.fogEndDistance = dist + 120f;
        }

        /// <summary>
        /// The intro shot: the whole engine low in the frame with stormy sky above it for the bolts,
        /// pushing in slowly while the strikes land, then a smooth crane up and out to the menu
        /// framing (every value eases from the close shot to the menu's, so the move ends exactly
        /// where the menu camera sits).
        /// </summary>
        void BlendIntroCamera(float aspect, ref Vector3 focus, ref float yaw, ref float pitch, ref float dist, ref float fov,
            ref float offX, ref float offY)
        {
            float push = Smooth(Mathf.Clamp01(_introT / IntroClear));
            const float closeFov = 36f;
            float tan = Mathf.Tan(closeFov * 0.5f * Mathf.Deg2Rad);
            // The engine's base is about 5.2 wide and its antenna tops out 3.4 up.
            float closeDist = Mathf.Max(5.2f / tan, 2.9f / (tan * Mathf.Max(0.3f, aspect)));
            closeDist *= Mathf.Lerp(1.1f, 1f, push);
            var closeFocus = _machine.DomeWorldCenter + Vector3.up * 0.35f;
            float closeYaw = Mathf.Lerp(-28f, -16f, push), closePitch = Mathf.Lerp(10f, 13f, push);
            // The engine sits a little below the middle of the screen.
            const float closeOffY = 0.2f;

            float b = Smooth(Mathf.InverseLerp(IntroTitleAt + 0.15f, IntroLength, _introT));
            focus = Vector3.Lerp(closeFocus, focus, b);
            yaw = Mathf.LerpAngle(closeYaw, yaw, b);
            pitch = Mathf.Lerp(closePitch, pitch, b);
            dist = Mathf.Exp(Mathf.Lerp(Mathf.Log(closeDist), Mathf.Log(dist), b));
            fov = Mathf.Lerp(closeFov, fov, b);
            offX *= b;
            offY = Mathf.Lerp(closeOffY, offY, b);
        }

        // ------------------------------------------------------------------ input

        void HandleInput()
        {
            if (!Application.isFocused || _introT >= 0f)
            {
                _pressed = false;
                _dragging = false;
                _pinchDistance = 0;
                return;
            }
            bool down = false, held = false, up = false;
            Vector2 pos = default;
            int touches = 0;
            float pinch = 0;

            var touch = Touchscreen.current;
            if (touch != null)
            {
                Vector2 p0 = default, p1 = default;
                foreach (var t in touch.touches)
                {
                    if (!t.isInProgress && !t.press.wasReleasedThisFrame) continue;
                    if (touches == 0) p0 = t.position.ReadValue();
                    else if (touches == 1) p1 = t.position.ReadValue();
                    touches++;
                }
                var primary = touch.primaryTouch;
                if (touches > 0)
                {
                    down = primary.press.wasPressedThisFrame;
                    up = primary.press.wasReleasedThisFrame;
                    held = primary.press.isPressed;
                    pos = primary.position.ReadValue();
                }
                if (touches >= 2)
                {
                    float d = Vector2.Distance(p0, p1);
                    if (_pinchDistance > 0) pinch = (_pinchDistance - d) / Mathf.Max(200f, Screen.height * 0.5f);
                    _pinchDistance = d;
                    _dragging = true; // Two fingers never count as a tap.
                }
                else _pinchDistance = 0;
            }
            var mouse = Mouse.current;
            if (touches == 0 && mouse != null)
            {
                pos = mouse.position.ReadValue();
                down = mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed || mouse.rightButton.isPressed;
                up = mouse.leftButton.wasReleasedThisFrame || mouse.rightButton.wasReleasedThisFrame;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !OverUi(pos))
                {
                    if (Mathf.Abs(scroll) > 20f) scroll /= 120f;
                    pinch = -scroll * 0.08f;
                }
            }

            if (Mathf.Abs(pinch) > 0.0001f)
            {
                _zoomTarget = Mathf.Clamp(_zoomTarget * (1f + pinch), 0.62f, 1.3f);
                _lastInteraction = Time.unscaledTime;
            }

            if (down)
            {
                _pressed = !OverUi(pos);
                _dragging = false;
                _pressPos = _lastPos = pos;
                _pressTime = Time.unscaledTime;
            }
            if (_pressed && held)
            {
                var delta = pos - _lastPos;
                _lastPos = pos;
                float threshold = Mathf.Max(10f, (Screen.dpi > 0 ? Screen.dpi : 160f) * 0.07f);
                if (!_dragging && (pos - _pressPos).magnitude > threshold) _dragging = true;
                if (_dragging && touches < 2)
                {
                    float scale = 180f / Mathf.Max(400f, Screen.height);
                    _yawTarget = Mathf.Clamp(_yawTarget + delta.x * scale * 0.9f, -55f, 55f);
                    _pitchTarget = Mathf.Clamp(_pitchTarget - delta.y * scale * 0.5f, 8f, 50f);
                    _lastInteraction = Time.unscaledTime;
                }
            }
            if (up && _pressed)
            {
                if (!_dragging && Time.unscaledTime - _pressTime < 0.6f) Tap(pos);
                _pressed = false;
                _dragging = false;
            }
            if (!held && !up) _pressed = false;
        }

        bool OverUi(Vector2 screenPos)
        {
            if (Panel == null || IsOverUi == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(Panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            return IsOverUi(panelPos);
        }

        void Tap(Vector2 screenPos)
        {
            var ray = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0));
            var hits = Physics.RaycastAll(ray, 500f);
            if (EditMode)
            {
                float nearest = float.MaxValue;
                DecorTag decor = null;
                Vector3? ground = null;
                float groundDist = float.MaxValue;
                foreach (var h in hits)
                {
                    var decorTag = h.collider.GetComponent<DecorTag>();
                    if (decorTag != null && h.distance < nearest)
                    {
                        nearest = h.distance;
                        decor = decorTag;
                    }
                    if (h.collider is MeshCollider && h.distance < groundDist)
                    {
                        groundDist = h.distance;
                        ground = h.point;
                    }
                }
                if (decor != null) DecorTapped?.Invoke(decor.Index);
                else if (ground.HasValue) GroundTapped?.Invoke(new Vector2(ground.Value.x, ground.Value.z));
                return;
            }
            if (_visit != null) return;
            float best = float.MaxValue;
            MachineTag tag = null;
            foreach (var h in hits)
            {
                var t = h.collider.GetComponentInParent<MachineTag>();
                if (t == null || h.distance >= best) continue;
                best = h.distance;
                tag = t;
            }
            if (tag != null) Tapped?.Invoke(tag.Index);
        }
    }
}
