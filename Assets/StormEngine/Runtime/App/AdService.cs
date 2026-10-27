using System;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Rewarded ads. A network (Unity LevelPlay, registered from the StormEngine.Ads assembly) sets
    /// <see cref="Provider"/>; it starts once the player has answered the ad privacy question.
    /// Without one, the Editor and Development builds show a simulated ad so every reward can be
    /// tested; release builds without a provider simply offer no ads. Ads never touch tournament scores.
    /// </summary>
    public sealed class AdService
    {
        public interface IRewardedProvider
        {
            bool IsReady { get; }
            /// <summary>Starts the network with the player's privacy choice (called once).</summary>
            void Start(bool personalized);
            /// <summary>The player changed their ad privacy choice in Settings.</summary>
            void SetPersonalized(bool personalized);
            /// <summary>Shows a rewarded ad; the callback says whether the reward was earned.</summary>
            void Show(Action<bool> done);
            /// <summary>Opens the network's own test tool (development builds).</summary>
            void OpenTestSuite();
            void Tick(float dt);
        }

        public static IRewardedProvider Provider;

        /// <summary>Shows the simulated ad (set by the UI). Receives the completion callback.</summary>
        public Action<Action<bool>> SimulatedPresenter;

        bool _showing, _started;

        public bool Simulated => Provider == null && (Application.isEditor || Debug.isDebugBuild);

        /// <summary>A real ad network is part of this build.</summary>
        public bool HasNetwork => Provider != null;

        public bool Available => !_showing && ((Provider != null && _started && Provider.IsReady) || (Simulated && SimulatedPresenter != null));

        /// <summary>Starts the network (first call) or updates the privacy choice (later calls).</summary>
        public void Begin(bool personalized)
        {
            if (Provider == null) return;
            if (_started)
            {
                Provider.SetPersonalized(personalized);
                return;
            }
            _started = true;
            try
            {
                Provider.Start(personalized);
            }
            catch (Exception e)
            {
                // A missing native SDK must never take the game down with it.
                Debug.LogWarning("[StormEngine] Ads could not start: " + e.Message);
                Provider = null;
            }
        }

        public void Tick(float dt)
        {
            if (!_started || Provider == null) return;
            try
            {
                Provider.Tick(dt);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Ads stopped: " + e.Message);
                Provider = null;
            }
        }

        public void OpenTestSuite()
        {
            if (_started) Provider?.OpenTestSuite();
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (!Available)
            {
                done?.Invoke(false);
                return;
            }
            _showing = true;
            void Finish(bool ok)
            {
                _showing = false;
                done?.Invoke(ok);
            }
            if (Provider != null) Provider.Show(Finish);
            else SimulatedPresenter(Finish);
        }
    }
}
