using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Settings that change per release rather than per build: store links and the ad network's ids.
    /// Edit Assets/StormEngine/Resources/StormEngine/ReleaseConfig.asset (menu: Storm Engine > Release Settings).
    /// Nothing here is secret: app keys and ad unit ids ship inside every app.
    /// </summary>
    [CreateAssetMenu(menuName = "Storm Engine/Release Config", fileName = "ReleaseConfig")]
    public sealed class ReleaseConfig : ScriptableObject
    {
        [Header("Links")]
        [Tooltip("Public web page with the privacy policy. Also entered in Play Console.")]
        public string privacyPolicyUrl = "";

        [Tooltip("The game's Google Play page, used by Share and Rate. Leave empty to build it from the package name.")]
        public string storeUrl = "";

        [Header("Unity LevelPlay (rewarded ads)")]
        [Tooltip("LevelPlay dashboard > Apps: the Android app key.")]
        public string levelPlayAndroidAppKey = "";

        [Tooltip("LevelPlay dashboard > Ad units: the Android rewarded ad unit id.")]
        public string levelPlayAndroidRewardedAdUnitId = "";

        public string levelPlayIosAppKey = "";
        public string levelPlayIosRewardedAdUnitId = "";

        [Tooltip("Development builds show an 'Ad test suite' button in Settings to check every ad network.")]
        public bool showAdTestSuite = true;

        static ReleaseConfig _instance;
        static bool _loaded;

        /// <summary>The project's release settings, or an empty default when the asset is missing.</summary>
        public static ReleaseConfig Instance
        {
            get
            {
                if (_loaded) return _instance;
                _loaded = true;
                _instance = Resources.Load<ReleaseConfig>("StormEngine/ReleaseConfig");
                if (_instance == null) _instance = CreateInstance<ReleaseConfig>();
                return _instance;
            }
        }

        public string AdAppKey => Application.platform == RuntimePlatform.IPhonePlayer ? levelPlayIosAppKey : levelPlayAndroidAppKey;
        public string RewardedAdUnitId => Application.platform == RuntimePlatform.IPhonePlayer ? levelPlayIosRewardedAdUnitId : levelPlayAndroidRewardedAdUnitId;
        public bool HasAds => !string.IsNullOrWhiteSpace(AdAppKey) && !string.IsNullOrWhiteSpace(RewardedAdUnitId);

        /// <summary>The Play Store page (the configured one, or built from the package name).</summary>
        public string StoreUrl => !string.IsNullOrWhiteSpace(storeUrl) ? storeUrl.Trim() : "https://play.google.com/store/apps/details?id=" + Application.identifier;
    }
}
