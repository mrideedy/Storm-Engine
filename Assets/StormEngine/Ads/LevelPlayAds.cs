using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace StormEngine.Ads
{
    /// <summary>
    /// Rewarded ads through Unity LevelPlay. Registers itself before the first scene when the app key
    /// and the rewarded ad unit are filled in <see cref="ReleaseConfig"/>. The Editor keeps the game's
    /// own simulated ad. One rewarded ad is kept loaded; failed loads retry with a growing delay.
    /// </summary>
    public sealed class LevelPlayAds : AdService.IRewardedProvider
    {
        /// <summary>Set to true to try LevelPlay's placeholder ads in the Editor instead of the game's own.</summary>
        const bool UseInEditor = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            if (Application.isEditor && !UseInEditor) return;
            var config = ReleaseConfig.Instance;
            if (!config.HasAds) return;
            AdService.Provider = new LevelPlayAds(config.AdAppKey.Trim(), config.RewardedAdUnitId.Trim(), config.showAdTestSuite);
        }

        readonly string _appKey, _adUnitId;
        readonly bool _testSuite;
        LevelPlayRewardedAd _ad;
        bool _initialized, _initializing;
        float _retryIn = -1f, _retryDelay = 4f, _rewardWait = -1f;
        Action<bool> _done;
        bool _rewarded, _closed;

        LevelPlayAds(string appKey, string adUnitId, bool testSuite)
        {
            _appKey = appKey;
            _adUnitId = adUnitId;
            _testSuite = testSuite && Debug.isDebugBuild;
        }

        public bool IsReady => _initialized && _ad != null && _ad.IsAdReady();

        public void Start(bool personalized)
        {
            SetPersonalized(personalized);
            // Storm Engine is made for teens and adults; nothing is directed at children.
            LevelPlayPrivacySettings.SetCOPPA(false);
            if (_testSuite) LevelPlay.SetMetaData("is_test_suite", "enable");
            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;
            Init();
        }

        void Init()
        {
            _initializing = true;
            LevelPlay.Init(_appKey);
        }

        public void SetPersonalized(bool personalized)
        {
            LevelPlayPrivacySettings.SetGDPRConsent(personalized);
            // "Do not sell or share my data" (US state privacy laws) follows the same choice.
            LevelPlayPrivacySettings.SetCCPA(!personalized);
        }

        void OnInitSuccess(LevelPlayConfiguration config)
        {
            _initializing = false;
            if (_initialized) return;
            _initialized = true;
            _ad = new LevelPlayRewardedAd(_adUnitId);
            _ad.OnAdLoaded += _ => _retryDelay = 4f;
            _ad.OnAdLoadFailed += _ => ScheduleReload();
            _ad.OnAdDisplayFailed += (info, error) =>
            {
                Finish(false);
                ScheduleReload();
            };
            _ad.OnAdRewarded += (info, reward) => OnRewarded();
            _ad.OnAdClosed += _ => OnClosed();
            _ad.LoadAd();
        }

        void OnInitFailed(LevelPlayInitError error)
        {
            _initializing = false;
            Debug.LogWarning("[StormEngine] LevelPlay did not start: " + error?.ErrorMessage);
            _retryIn = 30f;
        }

        void ScheduleReload()
        {
            _retryIn = _retryDelay;
            _retryDelay = Mathf.Min(_retryDelay * 2f, 120f);
        }

        public void Show(Action<bool> done)
        {
            if (!IsReady)
            {
                done?.Invoke(false);
                return;
            }
            _done = done;
            _rewarded = false;
            _closed = false;
            _ad.ShowAd();
        }

        void OnRewarded()
        {
            _rewarded = true;
            if (_closed) Finish(true);
        }

        void OnClosed()
        {
            _closed = true;
            // Some networks report the reward just after the close: give it a moment.
            if (_rewarded) Finish(true);
            else _rewardWait = 1.5f;
            _ad.LoadAd();
        }

        void Finish(bool rewarded)
        {
            _rewardWait = -1f;
            var done = _done;
            _done = null;
            done?.Invoke(rewarded);
        }

        public void OpenTestSuite()
        {
            if (_initialized) LevelPlay.LaunchTestSuite();
        }

        public void Tick(float dt)
        {
            if (_rewardWait > 0f)
            {
                _rewardWait -= dt;
                if (_rewardWait <= 0f) Finish(false);
            }
            if (_retryIn <= 0f) return;
            _retryIn -= dt;
            if (_retryIn > 0f) return;
            if (!_initialized)
            {
                if (!_initializing) Init();
            }
            else _ad?.LoadAd();
        }
    }
}
