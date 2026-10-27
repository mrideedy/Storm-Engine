using System;
using System.Threading.Tasks;
using StormEngine.Audio;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Procedural audio: every effect, the music tracks and the weather ambience are synthesized
    /// (see <see cref="SoundBank"/> and <see cref="MusicLibrary"/>), so the game ships without audio
    /// assets. Call sites only name the event; swapping in authored clips later needs no other changes.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int Voices = 12;

        readonly AudioSource[] _voices = new AudioSource[Voices];
        int _next;
        AudioSource _musicA, _musicB, _calm, _rain;
        bool _aActive = true;
        AudioClip _click, _popup, _deny, _buy, _upgrade, _equip, _boost, _stormStart, _stormEnd, _charged, _discovery, _goal, _levelUp;
        AudioClip[] _thunder, _rumbles;
        Func<SettingsSave> _settings;
        float _thunderCooldown, _buyCooldown, _rumbleTimer = 4f;
        float _storm;
        float _duck = 1f, _thunderDuck = 1f;

        // Long loops are rendered on a worker thread and turned into clips on the main thread.
        // [NonSerialized]: a script reload in Play mode must not turn these into empty arrays.
        [NonSerialized] volatile float[] _calmData, _rainData, _trackData;
        string _trackId, _loadingId, _renderedId;
        bool _loopsRequested;
        readonly object _gate = new object();

        /// <summary>0..1 storm intensity, drives the rain/wind crossfade, rolling thunder and music ducking.</summary>
        public float StormLevel
        {
            set => _storm = Mathf.Clamp01(value);
        }

        /// <summary>The music track now playing (or about to).</summary>
        public string CurrentTrack => _loadingId ?? _trackId;

        public bool TrackLoading => _loadingId != null;

        AudioSource ActiveMusic => _aActive ? _musicA : _musicB;
        AudioSource IdleMusic => _aActive ? _musicB : _musicA;

        public void Init(Func<SettingsSave> settings)
        {
            _settings = settings;
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0;
                _voices[i] = s;
            }
            _musicA = Loop("MusicA");
            _musicB = Loop("MusicB");
            _calm = Loop("Calm");
            _rain = Loop("Rain");

            _click = Clip("click", SoundBank.Click(), SoundBank.SfxRate);
            _popup = Clip("popup", SoundBank.Popup(), SoundBank.SfxRate);
            _deny = Clip("deny", SoundBank.Deny(), SoundBank.SfxRate);
            _buy = Clip("buy", SoundBank.Buy(), SoundBank.SfxRate);
            _upgrade = Clip("upgrade", SoundBank.Upgrade(), SoundBank.SfxRate);
            _equip = Clip("equip", SoundBank.Equip(), SoundBank.SfxRate);
            _boost = Clip("boost", SoundBank.Boost(), SoundBank.SfxRate);
            _stormStart = Clip("storm_start", SoundBank.StormStart(), SoundBank.SfxRate);
            _stormEnd = Clip("storm_end", SoundBank.StormEnd(), SoundBank.SfxRate);
            _charged = Clip("charged", SoundBank.Charged(), SoundBank.SfxRate);
            _discovery = Clip("discovery", SoundBank.Discovery(), SoundBank.SfxRate);
            _goal = Clip("goal", SoundBank.Goal(), SoundBank.SfxRate);
            _levelUp = Clip("level_up", SoundBank.LevelUp(), SoundBank.SfxRate);
            _thunder = new AudioClip[4];
            for (int i = 0; i < _thunder.Length; i++)
                _thunder[i] = Clip("thunder" + i, SynthKit.Thunder(SoundBank.SfxRate, 17 + i * 31, i * 0.12), SoundBank.SfxRate);
            _rumbles = new AudioClip[3];
            for (int i = 0; i < _rumbles.Length; i++)
                _rumbles[i] = Clip("rumble" + i, SynthKit.Thunder(SoundBank.SfxRate, 211 + i * 17, 0.75 + i * 0.1), SoundBank.SfxRate);
        }

        AudioSource Loop(string name)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = true;
            s.spatialBlend = 0;
            s.volume = 0;
            return s;
        }

        static AudioClip Clip(string name, float[] data, int rate, int channels = 1)
        {
            var clip = AudioClip.Create("se_" + name, data.Length / channels, channels, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void RequestLoops()
        {
            _loopsRequested = true;
            string track = MusicLibrary.Find(_settings?.Invoke()?.musicTrack) != null ? _settings().musicTrack : MusicLibrary.DefaultTrack;
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                _calmData = SoundBank.Calm();
                _rainData = SoundBank.Rain();
                RenderTrack(track, false);
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    _calmData = SoundBank.Calm();
                    _rainData = SoundBank.Rain();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[StormEngine] Audio rendering failed: " + e.Message);
                }
            });
            RenderTrack(track, true);
        }

        /// <summary>Switches the background music, crossfading once the new track has been rendered.</summary>
        public void PlayTrack(string id)
        {
            if (MusicLibrary.Find(id) == null || id == CurrentTrack) return;
            if (!_loopsRequested) return;
            RenderTrack(id, Application.platform != RuntimePlatform.WebGLPlayer);
        }

        void RenderTrack(string id, bool background)
        {
            _loadingId = id;
            if (!background)
            {
                _trackData = MusicLibrary.Render(id);
                _renderedId = id;
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    var data = MusicLibrary.Render(id);
                    lock (_gate)
                    {
                        if (_loadingId != id) return;
                        _renderedId = id;
                        _trackData = data;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[StormEngine] Music rendering failed: " + e.Message);
                }
            });
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_thunderCooldown > 0) _thunderCooldown -= dt;
            if (_thunderHeat > 0) _thunderHeat = Mathf.Max(0f, _thunderHeat - dt * 0.9f);
            if (_buyCooldown > 0) _buyCooldown -= dt;
            if (!_loopsRequested && Time.frameCount > 2) RequestLoops();

            var calm = _calmData;
            if (calm != null && calm.Length > 0)
            {
                _calmData = null;
                StartLoop(_calm, "calm", calm, SoundBank.AmbienceRate);
            }
            var rain = _rainData;
            if (rain != null && rain.Length > 0)
            {
                _rainData = null;
                StartLoop(_rain, "rain", rain, SoundBank.AmbienceRate);
            }
            float[] track = null;
            string trackId = null;
            lock (_gate)
            {
                if (_trackData != null && _trackData.Length > 0 && _renderedId == _loadingId)
                {
                    track = _trackData;
                    trackId = _renderedId;
                    _trackData = null;
                }
            }
            if (track != null) SwapTrack(trackId, track);

            var s = _settings?.Invoke();
            if (s == null) return;

            // Now and then a soft roll in the distance while a storm rages.
            _rumbleTimer -= dt;
            if (_storm > 0.55f && _rumbleTimer <= 0)
            {
                _rumbleTimer = UnityEngine.Random.Range(6f, 12f);
                Play(_rumbles[UnityEngine.Random.Range(0, _rumbles.Length)], 0.14f + 0.08f * _storm, UnityEngine.Random.Range(0.95f, 1.05f));
            }

            _duck = Mathf.MoveTowards(_duck, _storm > 0.5f ? 0.7f : 1f, dt * 0.6f);
            _thunderDuck = Mathf.MoveTowards(_thunderDuck, 1f, dt * 0.7f);
            float ambience = s.ambience ? Mathf.Clamp01(s.sfxVolume) : 0f;
            float music = s.music ? 0.6f * Mathf.Clamp01(s.musicVolume) * _duck * _thunderDuck : 0f;
            Fade(ActiveMusic, music, dt);
            var idle = IdleMusic;
            if (idle.clip != null)
            {
                idle.volume = Mathf.MoveTowards(idle.volume, 0, dt * 0.35f);
                if (idle.volume <= 0.001f)
                {
                    idle.Stop();
                    var old = idle.clip;
                    idle.clip = null;
                    Destroy(old);
                }
            }
            Fade(_calm, ambience * 0.32f * (1 - _storm), dt);
            Fade(_rain, ambience * 0.45f * _storm, dt);
        }

        void SwapTrack(string id, float[] data)
        {
            _loadingId = null;
            _trackId = id;
            var clip = Clip("music_" + id, data, MusicLibrary.Rate, 2);
            _aActive = !_aActive;
            var next = ActiveMusic;
            if (next.clip != null)
            {
                var old = next.clip;
                next.Stop();
                Destroy(old);
            }
            next.clip = clip;
            next.volume = 0;
            next.time = 0;
            next.Play();
        }

        static void StartLoop(AudioSource source, string name, float[] data, int rate)
        {
            source.clip = Clip(name, data, rate);
            source.time = 0;
            source.Play();
        }

        static void Fade(AudioSource source, float target, float dt)
        {
            source.volume = Mathf.MoveTowards(source.volume, target, dt * 0.35f);
        }

        bool On(out float volume)
        {
            var s = _settings?.Invoke();
            volume = s != null ? Mathf.Clamp01(s.sfxVolume) : 1f;
            return s == null || s.sfx;
        }

        void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || !On(out float master)) return;
            var v = _voices[_next];
            _next = (_next + 1) % Voices;
            v.pitch = pitch;
            v.PlayOneShot(clip, volume * master);
        }

        // Every event also has a matching vibration (see Haptics), independent of the sound settings.
        public void Click()
        {
            Haptics.Play(HapticKind.Tick);
            Play(_click, 0.5f, UnityEngine.Random.Range(0.96f, 1.04f));
        }

        public void Popup() => Play(_popup, 0.45f);

        public void Deny()
        {
            Haptics.Play(HapticKind.Double);
            Play(_deny, 0.55f);
        }

        public void Equip()
        {
            Haptics.Play(HapticKind.Click);
            Play(_equip, 0.6f);
        }

        public void Upgrade()
        {
            Haptics.Play(HapticKind.Heavy);
            Play(_upgrade, 0.7f);
        }

        public void Boost()
        {
            Haptics.Play(HapticKind.Heavy);
            Play(_boost, 0.7f, UnityEngine.Random.Range(0.97f, 1.03f));
        }

        public void StormStart()
        {
            Haptics.Play(HapticKind.Rumble);
            Play(_stormStart, 0.68f);
        }

        public void StormEnd()
        {
            Haptics.Play(HapticKind.Click);
            Play(_stormEnd, 0.7f);
        }

        public void Charged()
        {
            Haptics.Play(HapticKind.Click);
            Play(_charged, 0.55f);
        }

        public void Discovery()
        {
            Haptics.Play(HapticKind.Success);
            Play(_discovery, 0.85f);
        }

        public void Goal()
        {
            Haptics.Play(HapticKind.Success);
            Play(_goal, 0.65f);
        }

        public void LevelUp()
        {
            Haptics.Play(HapticKind.Success);
            Play(_levelUp, 0.85f);
        }

        public void Buy()
        {
            // Rapid buying (MAX / hold) should not machine-gun the chime.
            if (_buyCooldown > 0) return;
            _buyCooldown = 0.06f;
            Haptics.Play(HapticKind.Tick);
            Play(_buy, 0.55f, UnityEngine.Random.Range(0.97f, 1.06f));
        }

        /// <param name="haptic">Buzz too (the launch intro only buzzes on its last strike).</param>
        /// <param name="big">A single big strike: full volume, however many came just before.</param>
        public void Thunder(bool haptic = true, bool big = false)
        {
            if (_thunderCooldown > 0 && !big) return;
            _thunderCooldown = 0.45f;
            if (haptic) Haptics.Play(HapticKind.Thunder);
            // Light, playful strikes. Back-to-back strikes get quieter so a lightning-heavy storm
            // crackles instead of blasting, and the music only dips a little.
            float volume = big ? 0.56f : UnityEngine.Random.Range(0.42f, 0.5f) / (1f + _thunderHeat * 0.55f);
            _thunderHeat = Mathf.Min(3f, _thunderHeat + 1f);
            Play(_thunder[UnityEngine.Random.Range(0, _thunder.Length)], volume, UnityEngine.Random.Range(0.95f, 1.1f));
            _thunderDuck = Mathf.Min(_thunderDuck, 0.85f);
            _rumbleTimer = Mathf.Max(_rumbleTimer, 2.5f);
        }

        /// <summary>Recent strikes (decays over time); keeps rapid thunder from stacking up loud.</summary>
        float _thunderHeat;
    }
}
