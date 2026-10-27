using StormEngine.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StormEngine
{
    /// <summary>
    /// Frame rate and power use. Applies the chosen cap (Battery saver 30, Standard 60, Smooth up to
    /// the screen's top rate), eases an untouched idle engine down a step (never during storms),
    /// caps at 30 when the battery runs low, and on phones steps graphics quality down once the
    /// game keeps missing its frame rate.
    /// </summary>
    public sealed class Performance
    {
        /// <summary>Seconds without input before an idle engine drops to the lower frame rate.</summary>
        const float IdleSeconds = 90f;
        /// <summary>Frame times are averaged over windows of this many seconds.</summary>
        const float Window = 5f;

        readonly StormApp _app;
        int _maxRefresh;
        int _applied = -1;
        float _idle;
        float _warmup = 20f, _stepCooldown, _windowTime, _batteryTimer;
        int _windowFrames, _slowWindows;
        bool _lowBattery, _lowBatteryNoted;

        public Performance(StormApp app)
        {
            _app = app;
        }

        /// <summary>The frame-rate cap in effect right now.</summary>
        public int TargetFps => _applied;

        /// <summary>Highest refresh rate the screen offers (60 to 120).</summary>
        public int MaxRefresh
        {
            get
            {
                if (_maxRefresh > 0) return _maxRefresh;
                double best = Screen.currentResolution.refreshRateRatio.value;
                foreach (var r in Screen.resolutions)
                    if (r.refreshRateRatio.value > best) best = r.refreshRateRatio.value;
                _maxRefresh = double.IsNaN(best) || best < 59 ? 60 : Mathf.Clamp(Mathf.RoundToInt((float)best), 60, 120);
                return _maxRefresh;
            }
        }

        public static string QualityName(int quality) => quality <= 0 ? L.T("Simple") : quality == 1 ? L.T("Balanced") : L.T("High");

        public void Tick(float dt)
        {
            var s = _app.Save.settings;
            if (AnyInput()) _idle = 0;
            else _idle += dt;
            UpdateBattery(dt);

            bool busy = _idle < IdleSeconds || _app.Game.IsStorming;
            int target;
            if (s.frameRate <= 0 || _lowBattery) target = 30;
            else if (s.frameRate >= 2) target = busy ? MaxRefresh : 60;
            else target = busy ? 60 : 30;
            if (target != _applied)
            {
                _applied = target;
                Application.targetFrameRate = target;
                _windowTime = 0;
                _windowFrames = 0;
                _slowWindows = 0;
            }

            if (s.autoQuality && Application.isMobilePlatform) WatchSpeed(dt, s);
        }

        static bool AnyInput()
        {
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed || mouse.scroll.ReadValue().sqrMagnitude > 0f)) return true;
            var keys = Keyboard.current;
            return keys != null && keys.anyKey.isPressed;
        }

        void UpdateBattery(float dt)
        {
            _batteryTimer -= dt;
            if (_batteryTimer > 0) return;
            _batteryTimer = 20f;
            if (!Application.isMobilePlatform) return;
            float level = SystemInfo.batteryLevel;
            bool low = level >= 0f && level < 0.15f && SystemInfo.batteryStatus == BatteryStatus.Discharging;
            if (low == _lowBattery) return;
            _lowBattery = low;
            if (low && !_lowBatteryNoted && _app.Save.settings.frameRate > 0)
            {
                _lowBatteryNoted = true;
                _app.Ui?.Toast("battery", L.T("Battery low: running at 30 FPS to save power."));
            }
        }

        /// <summary>Steps graphics down after ~15 s of clearly missed frames while running flat out.</summary>
        void WatchSpeed(float dt, Core.SettingsSave s)
        {
            if (_warmup > 0)
            {
                _warmup -= dt;
                return;
            }
            if (_stepCooldown > 0) _stepCooldown -= dt;
            // Hitches (loading, switching apps) say nothing about steady speed.
            if (dt > 0.25f) return;
            _windowTime += dt;
            _windowFrames++;
            if (_windowTime < Window) return;
            float fps = _windowFrames / _windowTime;
            _windowTime = 0;
            _windowFrames = 0;
            if (_applied < 45)
            {
                // A 30 FPS cap is a choice, not slowness.
                _slowWindows = 0;
                return;
            }
            float need = Mathf.Min(_applied, 60) * 0.75f;
            _slowWindows = fps < need ? _slowWindows + 1 : 0;
            if (_slowWindows < 2 || _stepCooldown > 0) return;
            _slowWindows = 0;
            // First drop the 3D resolution a notch (invisible under the sharp interface), then quality.
            if (_app.World != null && _app.World.LowerResolution())
            {
                _stepCooldown = 20f;
                return;
            }
            if (s.quality <= 0) return;
            _stepCooldown = 60f;
            s.quality--;
            _app.Ui?.ApplySettings();
            _app.Ui?.Toast("settings", L.F("Graphics set to {0} to keep the game smooth. You can change this in Settings.", QualityName(s.quality)), 4f);
        }
    }
}
