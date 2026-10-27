using System;
using StormEngine.Core;
using StormEngine.Online;
using StormEngine.UI;
using StormEngine.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace StormEngine
{
    /// <summary>
    /// Entry point. Owns the game model, save file, online layer and UI, and drives the idle loop.
    /// Everything game-related is pure C# in StormEngine.Core; this class only glues it to Unity.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed partial class StormApp : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] PanelSettings panelSettings = default;

        public static StormApp Instance { get; private set; }
        public Content Content { get; private set; }
        public Game Game { get; private set; }
        public SaveData Save { get; private set; }
        public OnlineManager Online { get; private set; }
        public GameUI Ui { get; private set; }
        public WorldView World { get; private set; }
        public Sfx Sfx { get; private set; }
        public AdService Ads { get; } = new AdService();
        public StoreService Store { get; private set; }
        public Performance Perf { get; private set; }
        public Reminders Reminders { get; private set; }

        PanelSettings _runtimePanel;
        StyleSheet _sheet;
        OfflineReport _startupReport;
        bool _isNewPlayer;
        double _lastWall;
        long _pausedAt;
        float _saveTimer = 10f;
        Vector2Int _screen;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 60;
            Application.runInBackground = !Application.isMobilePlatform;

            if (SaveSystem.IsTestProfile)
                Debug.LogWarning("[StormEngine] Editor test save in use (offline): " + SaveSystem.CurrentPath + ". Menu Storm Engine > Debug > Use My Real Save switches back.");

            // The language comes first: content names are translated as the game is built.
            var loaded = SaveSystem.Load();
            // Game time continues from the save, so changing the phone's date does not skip ahead.
            if (loaded != null) Clock.Restore(loaded.clockUnix, loaded.clockUptime);
            string language = loaded?.settings?.language;
            if (string.IsNullOrEmpty(language) || Languages.Find(language) == null) language = Languages.Detect();
            Languages.Apply(language);

            Content = Content.CreateDefault();
            ContentText.Localize(Content);
            Game = new Game(Content);
            long now = Clock.Now;
            Game.SetTime(now);

            if (loaded != null)
            {
                Save = loaded;
                SaveMapper.Apply(Save, Game);
                Game.SetTime(now);
                double away = now - Save.lastSaveUnix;
                if (Save.lastSaveUnix > 0 && away >= 5)
                {
                    var report = Game.ApplyOffline(away);
                    if (away >= 60) _startupReport = report;
                }
            }
            else
            {
                Save = new SaveData
                {
                    playerId = Guid.NewGuid().ToString("N"),
                    createdUnix = now,
                    lastSaveUnix = now,
                    tutorialsMigrated = true
                };
                Game.Energy = 10;
                Game.Refresh();
                _isNewPlayer = true;
                // Phones with little memory start on Balanced graphics; everything else on High.
                if (Application.isMobilePlatform && SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 4000)
                    Save.settings.quality = 1;
            }
            Save.settings.language = language;
            Num.Notation = Save.settings.notation;
            if (!IsTrackOwned(Save.settings.musicTrack)) Save.settings.musicTrack = StormEngine.Audio.MusicLibrary.DefaultTrack;

            Game.StormEnded += OnStormEnded;
            Online = new OnlineManager(Save);
            Online.CloudSaveFound += OnCloudSaveFound;
            Store = new StoreService(this);

            Sfx = gameObject.AddComponent<Sfx>();
            Sfx.Init(() => Save.settings);
            Haptics.Enabled = Save.settings.vibrate;
            Perf = new Performance(this);
            Reminders = new Reminders(this);
            _lastWall = Clock.NowPrecise;
        }

        void Start()
        {
            if (document == null) document = GetComponent<UIDocument>();
            if (document == null) document = gameObject.AddComponent<UIDocument>();
            var source = panelSettings != null ? panelSettings : document.panelSettings;
            if (source == null) source = Resources.Load<PanelSettings>("StormEngine/StormPanelSettings");
            _runtimePanel = source != null ? Instantiate(source) : ScriptableObject.CreateInstance<PanelSettings>();
            document.panelSettings = _runtimePanel;
            ConfigureScaling(true);

            World = WorldView.Create(this);
            _sheet = Resources.Load<StyleSheet>("StormEngine/StormEngine");
            Ui = new GameUI(this, document.rootVisualElement, _sheet);
            Ui.PlayPressed += OnFirstPlay;
            Ui.ShowHome(true);
            Ui.PlayIntro();
            if (_startupReport != null) ShowOffline(_startupReport);
            MaybeShowDaily();
            Online.Start();
            SyncClock();
            Store.Start();
            Reminders.Clear();
            // Ads start only after the player has answered the privacy question (asked once).
            if (Ads.HasNetwork)
            {
                if (Save.settings.adConsent != 0) Ads.Begin(Save.settings.adConsent == 1);
                else Ui.ShowPopup(new AdConsentPopup(this));
            }
        }

        /// <summary>Records the player's ad privacy choice and (re)starts the ad network with it.</summary>
        public void SetAdConsent(bool personalized)
        {
            Save.settings.adConsent = personalized ? 1 : 2;
            Ads.Begin(personalized);
            SaveNow(false);
        }

        /// <summary>
        /// Asks Google Play for its rating card after a happy moment: once from Engine Level 7 and once
        /// more from Level 20. Play itself decides whether the card appears, so this never nags.
        /// </summary>
        public void MaybeAskForReview(int level)
        {
            int asks = Save.reviewAsks;
            if (asks >= 2 || level < (asks == 0 ? 7 : 20)) return;
            if (!Community.RequestReview()) return;
            Save.reviewAsks = asks + 1;
            SaveNow(false);
        }

        /// <summary>Settings: asks first, then deletes the player's online account and server data.</summary>
        public void RequestOnlineDataDeletion()
        {
            Ui.Confirm(L.T("DELETE ONLINE DATA?"),
                L.T("This deletes your player account and what the game servers keep for it: cloud save, island and leaderboard scores. Your progress on this phone stays."),
                L.T("Delete"), async () =>
                {
                    bool ok = await Online.DeleteAccount();
                    if (Ui == null) return;
                    Ui.Toast(ok ? "check" : "close", ok
                        ? L.T("Your online data was deleted. Online features are now off.")
                        : L.T("Could not delete your online data right now. Check your connection and try again."), 4f);
                    if (ok) SaveNow(false);
                }, "red");
        }

        /// <summary>Opens the privacy policy page (configured in ReleaseConfig).</summary>
        public void OpenPrivacyPolicy()
        {
            string url = ReleaseConfig.Instance.privacyPolicyUrl;
            if (!string.IsNullOrWhiteSpace(url)) Application.OpenURL(url.Trim());
        }

        void Update()
        {
            if (Game == null) return;
            double wall = Clock.NowPrecise;
            float dt = Mathf.Min(FrameTime.Delta, 1f);

            // A clock corrected from the network is not time spent away.
            if (Clock.Corrections != _clockCorrections)
            {
                _clockCorrections = Clock.Corrections;
                _lastWall = wall;
            }

            // A gap in wall-clock time that frames did not cover (sleep, suspended app) counts as offline.
            double gap = wall - _lastWall - dt;
            _lastWall = wall;
            if (gap > 5 && !Game.IsStorming)
            {
                var report = Game.ApplyOffline(gap);
                if (report != null && gap >= 60) ShowOffline(report);
            }

            Game.SetTime((long)wall);
            float remaining = dt;
            while (remaining > 0)
            {
                float step = Mathf.Min(0.25f, remaining);
                Game.Tick(step);
                remaining -= step;
            }

            Ui?.Tick(dt);
            if (Online != null)
            {
                Online.SubmissionsEnabled = Game.TournamentUnlocked;
                Online.Tick(dt);
            }
            TickMeta(dt);
            Ads.Tick(dt);
            Perf.Tick(dt);
            Reminders.Tick();
            ConfigureScaling(false);
            _clockSyncTimer -= dt;
            if (_clockSyncTimer <= 0) SyncClock();

            _saveTimer -= dt;
            if (_saveTimer <= 0)
            {
                _saveTimer = 10f;
                SaveNow(false);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (Game == null) return;
            if (paused)
            {
                _pausedAt = Clock.Now;
                SaveNow(false);
                Online?.PushCloudNow(SaveSystem.Seal(Save), Save.ProgressScore());
                Reminders?.ScheduleAll();
            }
            else if (_pausedAt > 0)
            {
                Reminders?.Clear();
                SyncClock();
                _checkDaily = true;
                _lastWall = Clock.NowPrecise;
                double away = Clock.Now - _pausedAt;
                _pausedAt = 0;
                if (away >= 5 && !Game.IsStorming)
                {
                    var report = Game.ApplyOffline(away);
                    if (report != null && away >= 60) ShowOffline(report);
                }
            }
        }

        /// <summary>
        /// Shows the welcome-back card. Before the Offline Generator is built nothing is produced
        /// offline, so new players just get a short hint instead of an empty report.
        /// </summary>
        void ShowOffline(OfflineReport report)
        {
            if (Ui == null) return;
            if (report.Locked)
            {
                if (Game.Level < 3 || report.Seconds < 300 || _shownLockedOffline) return;
                _shownLockedOffline = true;
            }
            Ui.ShowOffline(report);
        }

        bool _shownLockedOffline;
        int _clockCorrections;
        float _clockSyncTimer;

        /// <summary>Checks game time against a web server (only with online features on: nothing leaves the phone otherwise).</summary>
        void SyncClock()
        {
            _clockSyncTimer = 600f;
            if (Save.settings.online) StartCoroutine(Clock.SyncFromNetwork());
        }

        void OnApplicationQuit()
        {
            if (Game == null) return;
            SaveNow(false);
            Reminders?.ScheduleAll();
        }

        void ConfigureScaling(bool force)
        {
            if (_runtimePanel == null) return;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (!force && size == _screen) return;
            _screen = size;
            bool portrait = size.y > size.x * 1.05f;
            // Phones keep a 440-unit-wide layout (big, thumb-friendly text); tablets (squarer screens)
            // also cap the height so the stage, sheet and tab bar all get room.
            float scale = portrait ? Mathf.Min(size.x / 440f, size.y / 900f) : Mathf.Min(size.y / 820f, size.x / 1400f);
            _runtimePanel.scaleMode = PanelScaleMode.ConstantPixelSize;
            _runtimePanel.scale = Mathf.Max(0.5f, scale);
        }

        // ------------------------------------------------------------ saving

        public void SaveNow(bool manual)
        {
            SaveMapper.Capture(Game, Save);
            Save.lastSaveUnix = Clock.Now;
            Save.clockUnix = Clock.NowPrecise;
            Save.clockUptime = Clock.Uptime;
            RememberWeekly();
            bool ok = SaveSystem.Write(Save);
            Online?.MarkCloudDirty(SaveSystem.Seal(Save), Save.ProgressScore());
            if (Game.IsFeatureUnlocked("island")) Online?.PublishIsland(IslandLayout.FromGame(Game, Online.PlayerName).ToJson());
            if (manual) Ui?.Toast(ok ? "check" : "close", ok ? L.T("Game saved.") : L.T("Could not save the game."));
        }

        void OnCloudSaveFound(string json)
        {
            SaveData cloud;
            try
            {
                cloud = SaveSystem.Open(json);
            }
            catch (Exception)
            {
                return;
            }
            if (cloud == null || cloud.playerId == Save.playerId && cloud.lastSaveUnix <= Save.lastSaveUnix) return;
            SaveMapper.Capture(Game, Save);
            double cloudProgress = cloud.ProgressScore();
            if (cloudProgress <= Save.ProgressScore() + 0.01) return;
            // Until this device catches up, it never overwrites the better cloud save.
            Online.CloudFloor = Math.Max(Online.CloudFloor, cloudProgress);
            Ui?.Confirm(L.T("CLOUD SAVE FOUND"),
                L.F("Your cloud save has more progress ({0} lifetime Energy, {1} resets) than this device ({2}, {3} resets). Load it?",
                    Num.Format(cloud.lifetimeEnergy), cloud.prestiges, Num.Format(Save.lifetimeEnergy), Save.prestiges),
                L.T("Load cloud save"), () =>
                {
                    SaveSystem.Write(cloud);
                    Reload();
                }, "blue");
        }

        void Reload()
        {
            Game = null;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Switches language in place: the texts are translated again and only the interface is
        /// rebuilt; the world, sounds, music and game keep running.
        /// </summary>
        public void SetLanguage(string code)
        {
            if (Languages.Find(code) == null || code == Languages.Current.Code) return;
            Save.settings.language = code;
            Languages.Apply(code);
            ContentText.Relocalize(Content);
            bool home = Ui.HomeShown;
            Ui.Dispose();
            Ui = new GameUI(this, document.rootVisualElement, _sheet);
            Ui.PlayPressed += OnFirstPlay;
            if (home) Ui.ShowHome(true);
            else Ui.Open("more");
            SaveNow(false);
        }

        // ------------------------------------------------------------ storms & submissions

        void OnStormEnded(StormResult result, RecordFlags flags, StormSubmission submission)
        {
            if (submission == null) return;
            string summary = null;
            if (submission.TryCreateSetup(Content, out var setup, out _, out _))
                summary = StormValidator.Summary(Content, setup.Build, result);
            Online.Queue(new PendingSave
            {
                json = submission.ToJson(),
                summary = summary,
                score = result.Score,
                cls = submission.Class,
                week = submission.Week,
                unix = submission.Unix,
                allTime = flags.AllTime,
                weekly = flags.Weekly,
                classBoard = flags.Class,
                title = Game.Equipped(CosmeticSlot.Title)?.Id
            });
        }

        // ------------------------------------------------------------ player actions (with feedback)

        public void Unleash()
        {
            if (Game.Unleash()) return;
            if (!Game.IsStorming) Sfx.Deny();
        }

        public void ActivateBoost(int b)
        {
            if (Game.ActivateBoost(b)) return;
            Sfx.Deny();
            var def = Content.Boosts[b];
            if (!Game.IsBoostUnlocked(b))
            {
                var machine = Content.Machines[Content.MachineIndex(def.UnlockMachine)];
                Ui.Toast("lock", L.F("{0} unlocks at {1} Lv {2}.", def.Name, machine.Name, def.UnlockLevel) + " " + def.Desc);
            }
        }

        public bool BuyMachine(int m, int count)
        {
            bool ok = Game.BuyMachine(m, count);
            if (ok) Sfx.Buy();
            else Sfx.Deny();
            return ok;
        }

        public void UnlockModule(int m)
        {
            if (Game.UnlockModule(m))
            {
                Sfx.Discovery();
                Ui.Toast("modules", Game.Build.IsEquipped(m)
                    ? L.F("{0} unlocked and equipped.", Content.Modules[m].Name)
                    : L.F("{0} unlocked.", Content.Modules[m].Name));
            }
            else Sfx.Deny();
        }

        public void UpgradeModule(int m)
        {
            if (Game.UpgradeModule(m)) Sfx.Buy();
            else Sfx.Deny();
        }

        public void Equip(int m)
        {
            if (Game.Equip(m)) Sfx.Click();
            else
            {
                Sfx.Deny();
                if (Game.Build.Equipped.Count >= Game.ModuleSlots) Ui.Toast("lock", L.T("All module slots are full. Unequip one first."));
            }
        }

        public void Unequip(int m)
        {
            if (Game.Unequip(m)) Sfx.Click();
            else Sfx.Deny();
        }

        public bool BuyUpgrade(int u)
        {
            if (Game.BuyUpgrade(u))
            {
                Sfx.Upgrade();
                Ui.Toast(Content.Upgrades[u].Icon, Game.Build.Upgrades[u] == 1
                    ? L.F("{0} built!", Content.Upgrades[u].Name)
                    : L.F("{0} upgraded to Lv {1}.", Content.Upgrades[u].Name, Game.Build.Upgrades[u]));
                SaveNow(false);
                return true;
            }
            Sfx.Deny();
            if (Game.IsStorming) Ui.Toast("lock", L.T("Upgrades are locked while a storm is running."));
            return false;
        }

        public void BuyResearch(int r)
        {
            if (Game.BuyResearch(r))
            {
                Sfx.Buy();
                SaveNow(false);
            }
            else Sfx.Deny();
        }

        public void RequestPrestige()
        {
            long gain = Game.ResearchOnReset();
            if (gain < 1 || Game.IsStorming)
            {
                Sfx.Deny();
                return;
            }
            Ui.Confirm(L.T("RESET THE ATMOSPHERE?"),
                L.F("You will gain {0} Research. Energy and machine levels reset to zero. Modules, Storm Cores, Research, discoveries and records are kept.", Num.Int(gain)),
                L.F("Reset for +{0}", Num.Int(gain)), () =>
                {
                    if (!Game.Prestige()) return;
                    Sfx.Discovery();
                    Ui.Toast("research", L.F("Atmosphere reset! +{0} Research.", Num.Int(gain)));
                    Ui.Open("research");
                    SaveNow(false);
                }, "purple");
        }

        public void RequestHardReset()
        {
            Ui.Confirm(L.T("DELETE ALL PROGRESS?"),
                L.T("This permanently deletes your local save. Leaderboard entries stay online. This cannot be undone."),
                L.T("Delete everything"), () =>
                {
                    SaveSystem.Delete();
                    Reload();
                }, "red");
        }
    }
}
