#if STORM_TRAILER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using StormEngine.Core;
using StormEngine.UI;
using StormEngine.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.Trailer
{
    /// <summary>
    /// Trailer recorder, compiled only into the trailer build (define STORM_TRAILER, see
    /// Editor/TrailerBuild.cs). Run the player with <c>-trailer &lt;folder&gt; -shots &lt;shots.json&gt;</c>:
    /// it plays the scripted takes on its own copy of a save (<c>folder/save</c>), offline and without
    /// ads, writes every frame of each take as a JPEG (<c>folder/takes/&lt;take&gt;/00000.jpg</c>) at a
    /// fixed frame rate, and quits. Without <c>-trailer</c> the game runs normally.
    /// </summary>
    static class TrailerBoot
    {
        internal static string Folder, Shots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot()
        {
            Folder = Arg("-trailer");
            Shots = Arg("-shots");
            if (string.IsNullOrEmpty(Folder)) return;
            string save = Path.Combine(Folder, "save");
            Directory.CreateDirectory(save);
            SaveSystem.FolderOverride = save;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin()
        {
            if (string.IsNullOrEmpty(Folder)) return;
            // No ads, nothing online: the recording plays on a private copy of a save.
            AdService.Provider = null;
            var app = StormApp.Instance;
            if (app != null && app.Save != null) app.Save.settings.online = false;
            new GameObject("TrailerDirector").AddComponent<TrailerDirector>();
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }

    [Serializable]
    sealed class ShotList
    {
        public int fps = 30;
        /// <summary>
        /// Size to film world takes (no interface) at, off screen, when it is bigger than the window
        /// allows (a 1080x1920 portrait video on a 1440-pixel-tall display). 0 = film the screen.
        /// </summary>
        public int width, height;
        public Take[] takes;
    }

    /// <summary>One continuous recording. Cue times are seconds from its first saved frame.</summary>
    [Serializable]
    sealed class Take
    {
        public string name;
        public float seconds = 4f;
        /// <summary>Frames played before recording starts, so effects and weather have settled.</summary>
        public float preroll = 1f;
        public bool ui;
        /// <summary>0 = camera keys at constant speed, 1 = ease in and out over the whole take.</summary>
        public float ease = 0.5f;
        public string[] setup;
        public Cue[] cues;
        /// <summary>Camera keys; none = the game's own camera.</summary>
        public Key[] camera;
    }

    [Serializable]
    sealed class Cue
    {
        public float t;
        public string a;
    }

    [Serializable]
    sealed class Key
    {
        public float t;
        /// <summary>"" or "island" (the island centre), "dome", or a machine id.</summary>
        public string anchor;
        public float x, y, z;
        public float yaw, pitch, dist;
        public float fov = 30f;
        public float sx, sy;
    }

    sealed class TrailerDirector : MonoBehaviour
    {
        sealed class Ramp
        {
            public string What;
            public float From, To, Start, Length;
        }

        StormApp _app;
        ShotList _list;
        RenderTexture _offscreen;
        Texture2D _readback;
        int _fps = 30;
        string _log;
        readonly Dictionary<int, int> _hidden = new Dictionary<int, int>();
        readonly List<Ramp> _ramps = new List<Ramp>();

        Game G => _app.Game;
        WorldView W => _app.World;

        IEnumerator Start()
        {
            _log = Path.Combine(TrailerBoot.Folder, "director.log");
            File.WriteAllText(_log, "");
            AudioListener.volume = 0f;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            while ((_app = StormApp.Instance) == null || _app.World == null || _app.Ui == null) yield return null;
            var list = _list = JsonUtility.FromJson<ShotList>(File.ReadAllText(TrailerBoot.Shots));
            _fps = Mathf.Max(1, list.fps);
            Time.captureDeltaTime = 1f / _fps;

            // Let the launch intro run out and the start-up popups appear, then clear them.
            for (int i = 0; i < 600 && W.IntroPlaying; i++)
            {
                W.SkipIntro();
                yield return null;
            }
            _app.SetLanguage("en");
            _app.Save.settings.quality = 2;
            _app.Save.settings.reduceMotion = false;
            _app.Save.settings.showFps = false;
            W.ReduceMotion = false;
            W.ApplyQuality(2);
            for (int i = 0; i < 30; i++)
            {
                ClosePopups();
                yield return null;
            }
            Log("frame step: unscaled " + Time.unscaledDeltaTime + ", scaled " + Time.deltaTime + ", capture " + Time.captureDeltaTime
                + ", FrameTime " + FrameTime.Delta + ", screen " + Screen.width + "x" + Screen.height);

            foreach (var take in list.takes)
            {
                Log("take " + take.name);
                yield return Record(take);
            }
            Log("done");
            Application.Quit();
        }

        IEnumerator Record(Take take)
        {
            _ramps.Clear();
            if (take.setup != null)
                foreach (var a in take.setup)
                {
                    if (a == "waitCalm")
                    {
                        for (int i = 0; i < _fps * 120 && G.IsStorming; i++)
                        {
                            ClosePopups();
                            yield return null;
                        }
                        continue;
                    }
                    Do(a, 0f);
                }
            SetUi(take.ui);
            // World takes can be filmed off screen at full size; takes with the interface film the window.
            bool offscreen = !take.ui && _list.width > 0 && _list.height > 0;
            if (offscreen)
            {
                if (_offscreen == null)
                {
                    _offscreen = new RenderTexture(_list.width, _list.height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    _readback = new Texture2D(_list.width, _list.height, TextureFormat.RGB24, false);
                }
                W.Camera.targetTexture = _offscreen;
            }

            string dir = Path.Combine(TrailerBoot.Folder, "takes", take.name);
            Directory.CreateDirectory(dir);
            var cues = take.cues ?? Array.Empty<Cue>();
            Array.Sort(cues, (p, q) => p.t.CompareTo(q.t));
            int next = 0;
            int pre = Mathf.Max(1, Mathf.RoundToInt(take.preroll * _fps));
            int frames = Mathf.RoundToInt(take.seconds * _fps);
            for (int f = -pre; f < frames; f++)
            {
                float t = f / (float)_fps;
                while (next < cues.Length && cues[next].t <= t) Do(cues[next++].a, t);
                Tick(t);
                Aim(take, t);
                ClosePopups();
                yield return new WaitForEndOfFrame();
                if (f >= 0)
                {
                    if (offscreen) SaveOffscreen(dir, f);
                    else SaveFrame(dir, f);
                }
            }
            if (offscreen) W.Camera.targetTexture = null;

            foreach (var kv in _hidden) G.Build.Machines[kv.Key] = Mathf.Max(G.Build.Machines[kv.Key], kv.Value);
            _hidden.Clear();
            G.MarkDirty();
            W.Cinematic = null;
            W.CinematicStorm = 0f;
            _ramps.Clear();
            Log("  " + frames + " frames");
        }

        void SaveFrame(string dir, int f)
        {
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(Path.Combine(dir, f.ToString("00000") + ".jpg"), tex.EncodeToJPG(93));
            }
            finally
            {
                Destroy(tex);
            }
        }

        void SaveOffscreen(string dir, int f)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _offscreen;
            _readback.ReadPixels(new Rect(0, 0, _offscreen.width, _offscreen.height), 0, 0, false);
            _readback.Apply(false);
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(dir, f.ToString("00000") + ".jpg"), _readback.EncodeToJPG(93));
        }

        // ------------------------------------------------------------------ camera

        void Aim(Take take, float t)
        {
            var keys = take.camera;
            if (keys == null || keys.Length == 0)
            {
                W.Cinematic = null;
                return;
            }
            float len = Mathf.Max(0.001f, take.seconds);
            float lin = Mathf.Clamp01(t / len);
            float u = Mathf.Lerp(lin, lin * lin * (3f - 2f * lin), Mathf.Clamp01(take.ease)) * len;
            int i = 0;
            while (i < keys.Length - 1 && keys[i + 1].t <= u) i++;
            Key a = keys[i], b = keys[Mathf.Min(i + 1, keys.Length - 1)];
            Key p0 = keys[Mathf.Max(i - 1, 0)], p3 = keys[Mathf.Min(i + 2, keys.Length - 1)];
            float s = b.t > a.t ? Mathf.Clamp01((u - a.t) / (b.t - a.t)) : 0f;
            W.Cinematic = new WorldView.CameraShot
            {
                Focus = Curve(Focus(p0), Focus(a), Focus(b), Focus(p3), s),
                Yaw = Curve(p0.yaw, a.yaw, b.yaw, p3.yaw, s),
                Pitch = Curve(p0.pitch, a.pitch, b.pitch, p3.pitch, s),
                Distance = Curve(p0.dist, a.dist, b.dist, p3.dist, s),
                Fov = Curve(p0.fov, a.fov, b.fov, p3.fov, s),
                ScreenX = Curve(p0.sx, a.sx, b.sx, p3.sx, s),
                ScreenY = Curve(p0.sy, a.sy, b.sy, p3.sy, s),
            };
        }

        Vector3 Focus(Key k) => Anchor(k.anchor) + new Vector3(k.x, k.y, k.z);

        Vector3 Anchor(string anchor)
        {
            if (string.IsNullOrEmpty(anchor) || anchor == "island") return Vector3.zero;
            if (anchor == "dome") return W.DomeWorld;
            int m = _app.Content.MachineIndex(anchor);
            return m >= 0 ? W.GeneratorWorld(m) : Vector3.zero;
        }

        /// <summary>Catmull-Rom: smooth through every key.</summary>
        static float Curve(float p0, float p1, float p2, float p3, float s) =>
            0.5f * (2f * p1 + (-p0 + p2) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * s * s + (-p0 + 3f * p1 - 3f * p2 + p3) * s * s * s);

        static Vector3 Curve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float s) =>
            new Vector3(Curve(p0.x, p1.x, p2.x, p3.x, s), Curve(p0.y, p1.y, p2.y, p3.y, s), Curve(p0.z, p1.z, p2.z, p3.z, s));

        // ------------------------------------------------------------------ actions

        void Tick(float t)
        {
            foreach (var r in _ramps)
            {
                float v = Mathf.Lerp(r.From, r.To, Mathf.Clamp01((t - r.Start) / Mathf.Max(0.001f, r.Length)));
                if (r.What == "sky") W.CinematicStorm = v;
                else if (r.What == "charge") G.StormCharge = v;
            }
        }

        void Do(string action, float t)
        {
            try
            {
                var p = action.Split(':');
                var c = _app.Content;
                switch (p[0])
                {
                    case "calm":
                        G.StormCharge = 0.15;
                        W.CinematicStorm = 0f;
                        W.SettleWeather(0f);
                        break;
                    case "charged": G.StormCharge = 1; break;
                    case "sky": W.CinematicStorm = F(p[1]); break;
                    case "skyRamp": _ramps.Add(new Ramp { What = "sky", From = W.CinematicStorm, To = F(p[1]), Start = t, Length = F(p[2]) }); break;
                    case "chargeRamp":
                        G.StormCharge = F(p[1]);
                        _ramps.Add(new Ramp { What = "charge", From = F(p[1]), To = F(p[2]), Start = t, Length = F(p[3]) });
                        break;
                    case "settle": W.SettleWeather(F(p[1])); break;
                    case "unleash":
                        G.StormCharge = 1;
                        if (!G.IsStorming) G.Unleash();
                        break;
                    case "strike": W.CinematicStrike(false); break;
                    case "bigStrike": W.CinematicStrike(true); break;
                    case "shake": W.Shake(F(p[1])); break;
                    case "flash": W.Flash(F(p[1])); break;
                    case "boost": G.ActivateBoost(int.Parse(p[1], CultureInfo.InvariantCulture)); break;
                    case "hide":
                        foreach (var id in p[1].Split(','))
                        {
                            int m = c.MachineIndex(id);
                            if (m < 0) continue;
                            if (!_hidden.ContainsKey(m)) _hidden[m] = G.Build.Machines[m];
                            G.Build.Machines[m] = 0;
                        }
                        G.MarkDirty();
                        break;
                    case "buy":
                    {
                        int m = c.MachineIndex(p[1]);
                        Rich(() => G.BuyMachine(m, 1));
                        break;
                    }
                    case "level":
                    {
                        int m = c.MachineIndex(p[1]);
                        Rich(() => G.BuyMachine(m, p.Length > 2 ? int.Parse(p[2], CultureInfo.InvariantCulture) : 10));
                        var at = W.GeneratorWorld(m);
                        W.Burst(at + Vector3.up * 0.8f, new Color(1f, 0.85f, 0.3f), 40, 6f);
                        W.Puff(at, 10);
                        break;
                    }
                    case "decor":
                    {
                        int d = c.DecorationIndex(p[1]);
                        if (d < 0) break;
                        Rich(() => G.BuyDecoration(d));
                        W.AutoPlace(p[1]);
                        break;
                    }
                    case "equip":
                    {
                        int i = c.CosmeticIndex(p[1]);
                        if (i < 0) break;
                        G.OwnedCosmetics.Add(p[1]);
                        G.EquipCosmetic(i);
                        break;
                    }
                    case "home": _app.Ui.ShowHome(p[1] == "1"); break;
                    case "open": _app.Ui.Open(p[1]); break;
                    case "ui": SetUi(p[1] == "1"); break;
                    case "intro": W.PlayIntro(); break;
                    case "tournament":
                        G.Tournament.AttemptsUsed = 0;
                        G.StormCharge = 1;
                        G.StartTournament();
                        break;
                    default:
                        Log("unknown action " + action);
                        break;
                }
            }
            catch (Exception e)
            {
                Log("action " + action + " failed: " + e.Message);
            }
        }

        /// <summary>Runs a purchase as if the player could afford it, then puts the wallet back.</summary>
        void Rich(Action buy)
        {
            double energy = G.Energy;
            long cores = G.Cores, gems = G.Gems;
            G.Energy = 1e300;
            G.Cores = long.MaxValue / 4;
            G.Gems = 1000000000;
            try
            {
                buy();
            }
            finally
            {
                G.Energy = energy;
                G.Cores = cores;
                G.Gems = gems;
            }
        }

        void SetUi(bool on)
        {
            var doc = _app.GetComponent<UIDocument>();
            if (doc != null) doc.rootVisualElement.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void ClosePopups()
        {
            var doc = _app.GetComponent<UIDocument>();
            if (doc == null) return;
            foreach (var popup in doc.rootVisualElement.Query<Popup>().ToList()) popup.Close();
        }

        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        void Log(string line) => File.AppendAllText(_log, line + "\n");
    }
}
#endif
