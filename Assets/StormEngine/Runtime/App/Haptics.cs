using System;
using UnityEngine;

namespace StormEngine
{
    /// <summary>Vibration feedback, from a light tick for taps to a heavy hit for thunder.</summary>
    public enum HapticKind { Tick, Click, Heavy, Double, Success, Thunder, Rumble }

    /// <summary>
    /// Phone vibration. On Android 10+ taps use the system's predefined effects (crisp on linear
    /// motors), longer patterns use amplitude waveforms (Android 8+), and older phones get a plain
    /// buzz for the strong kinds only. iOS falls back to Handheld.Vibrate for big moments.
    /// Toggle with <see cref="Enabled"/> (Settings > Vibration).
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        static float _last = -1f, _lastThunder = -1f;

        public static void Play(HapticKind kind)
        {
            if (!Enabled || !Application.isMobilePlatform) return;
            float now = Time.unscaledTime;
            // Taps closer than this blur into one buzz; thunder during a storm must not become a drone.
            if (now - _last < (kind == HapticKind.Tick ? 0.05f : 0.03f)) return;
            if (kind == HapticKind.Thunder)
            {
                if (now - _lastThunder < 0.8f) return;
                _lastThunder = now;
            }
            _last = now;
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayAndroid(kind);
#elif UNITY_IOS && !UNITY_EDITOR
            if (kind == HapticKind.Heavy || kind == HapticKind.Success || kind == HapticKind.Thunder) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool _init;
        static int _sdk;
        static AndroidJavaObject _vibrator;
        static AndroidJavaObject[] _effects;

        static void Init()
        {
            _init = true;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator")) _vibrator = null;
                if (_vibrator == null || _sdk < 26) return;

                using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                {
                    AndroidJavaObject Predefined(int id, long fallbackMs, int amplitude) => _sdk >= 29
                        ? effect.CallStatic<AndroidJavaObject>("createPredefined", id)
                        : effect.CallStatic<AndroidJavaObject>("createOneShot", fallbackMs, amplitude);
                    AndroidJavaObject Wave(long[] timings, int[] amplitudes) =>
                        effect.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1);

                    _effects = new AndroidJavaObject[7];
                    _effects[(int)HapticKind.Tick] = Predefined(2, 8, 70);       // EFFECT_TICK
                    _effects[(int)HapticKind.Click] = Predefined(0, 14, 130);    // EFFECT_CLICK
                    _effects[(int)HapticKind.Heavy] = Predefined(5, 26, 255);    // EFFECT_HEAVY_CLICK
                    _effects[(int)HapticKind.Double] = Predefined(1, 14, 160);   // EFFECT_DOUBLE_CLICK
                    _effects[(int)HapticKind.Success] = Wave(new long[] { 0, 18, 70, 24, 60, 40 }, new[] { 0, 120, 0, 190, 0, 255 });
                    _effects[(int)HapticKind.Thunder] = Wave(new long[] { 0, 26, 24, 70 }, new[] { 0, 255, 0, 110 });
                    _effects[(int)HapticKind.Rumble] = Wave(new long[] { 0, 90, 50, 160 }, new[] { 0, 110, 40, 70 });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Vibration is unavailable: " + e.Message);
                _vibrator = null;
            }
        }

        static void PlayAndroid(HapticKind kind)
        {
            if (!_init) Init();
            try
            {
                if (_vibrator != null && _effects != null)
                {
                    _vibrator.Call("vibrate", _effects[(int)kind]);
                    return;
                }
                // Android 7 and older (or no vibrator service): only the big moments, as a plain buzz.
                if (kind == HapticKind.Heavy || kind == HapticKind.Success || kind == HapticKind.Thunder) Handheld.Vibrate();
            }
            catch (Exception)
            {
                _vibrator = null;
                _effects = null;
            }
        }
#endif
    }
}
