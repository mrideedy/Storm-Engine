using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine.Online
{
    public enum OnlineState { Disabled, Offline, Connecting, Online }

    public sealed class BoardEntry
    {
        public int Rank;
        public string PlayerId;
        public string Name;
        public double Score;
        public string Summary;
        public bool IsMe;
    }

    [Serializable]
    public sealed class BoardPage
    {
        public string Id;
        public List<BoardEntry> Entries = new List<BoardEntry>();
        public BoardEntry Me;
        public BoardEntry Rival;
        public long FetchedUnix;
        public string Error;
    }

    /// <summary>The player's rank in the most recent archived version of a board (last week's tournament).</summary>
    public sealed class ArchivedRank
    {
        public string VersionId;
        /// <summary>When that version closed (unix seconds).</summary>
        public long EndUnix;
        /// <summary>1-based rank, 0 = no entry that week, -1 = unknown.</summary>
        public int Rank = -1;
    }

    public sealed class SubmitOutcome
    {
        /// <summary>The server accepted (or already had) this storm; drop it from the queue.</summary>
        public bool Done;
        /// <summary>Transient failure; keep it queued and try again later.</summary>
        public bool Retry;
        public string Message;
        public double ServerScore;
    }

    /// <summary>
    /// Backend contract. The Unity Gaming Services implementation lives in the StormEngine.Online
    /// assembly, which only compiles when the UGS packages are installed.
    /// </summary>
    public interface IOnlineBackend
    {
        string Name { get; }
        bool IsSignedIn { get; }
        string PlayerId { get; }
        string PlayerName { get; }
        /// <summary>Human readable reason for the last failed connection (or null).</summary>
        string LastError { get; }
        Task<bool> ConnectAsync();
        Task<string> SetNameAsync(string name);
        Task<SubmitOutcome> SubmitAsync(PendingSave storm);
        Task<BoardPage> GetBoardAsync(string boardId, int limit);
        Task<string> LoadCloudAsync(string key);
        Task<bool> SaveCloudAsync(string key, string json);
        /// <summary>Publishes this player's island layout as public data others can read.</summary>
        Task<bool> PublishIslandAsync(string json);
        /// <summary>Reads another player's public island layout (null if they have none).</summary>
        Task<string> LoadIslandAsync(string playerId);
        Task<ArchivedRank> GetArchivedRankAsync(string boardId);
        /// <summary>
        /// Deletes this player's account and everything the game servers keep for it (cloud save,
        /// public island, leaderboard entries). A later connection starts a new anonymous player.
        /// </summary>
        Task<bool> DeleteAccountAsync();
    }

    public static class OnlineBackends
    {
        /// <summary>Set by the online assembly at load time. Null means "play offline".</summary>
        public static Func<IOnlineBackend> Factory;
    }

    public static class Boards
    {
        public const string AllTime = "storm_alltime";
        public const string Weekly = "storm_weekly";
        public const string ClassPrefix = "storm_class_";

        public static string Class(string classId) => ClassPrefix + classId;
    }

    /// <summary>
    /// Offline-first online layer: queues storms while offline, flushes them when a connection
    /// appears, caches leaderboards so the last known standings are always visible.
    /// </summary>
    public sealed class OnlineManager
    {
        const float RetrySeconds = 30f;
        const float MaxRetrySeconds = 300f;
        const float CloudSaveInterval = 90f;
        const int MaxPending = 12;

        readonly SaveData _save;
        readonly Dictionary<string, BoardPage> _cache = new Dictionary<string, BoardPage>();
        readonly string _cachePath;
        IOnlineBackend _backend;
        float _retryTimer;
        float _retryDelay = RetrySeconds;
        float _cloudTimer = 20f;
        bool _flushing;
        float _submitDelay = RetrySeconds;
        bool _cloudDirty;
        string _pendingCloudJson;
        string _islandJson;
        string _publishedIsland;
        float _islandTimer = 15f;
        const float IslandInterval = 60f;

        public OnlineState State { get; private set; } = OnlineState.Offline;
        public string Status { get; private set; } = L.T("Offline");
        public string LastSubmitMessage { get; private set; }
        public string BackendName => _backend?.Name ?? "Offline";
        public string PlayerId => _backend?.PlayerId;
        public string PlayerName => _backend != null && !string.IsNullOrEmpty(_backend.PlayerName) ? _backend.PlayerName : _save.playerName;
        public int PendingCount => _save.pending.Count;
        public bool IsOnline => State == OnlineState.Online;

        /// <summary>
        /// False until the leaderboards feature is unlocked. Record storms are still queued (and saved)
        /// so they are posted the moment the boards open.
        /// </summary>
        public bool SubmissionsEnabled = true;

        public event Action Changed;
        public event Action<string> CloudSaveFound;

        public OnlineManager(SaveData save)
        {
            _save = save;
            _cachePath = Path.Combine(Application.persistentDataPath, "storm_boards.json");
            LoadCache();
        }

        public void Start()
        {
            if (SaveSystem.IsTestProfile)
            {
                // Editor test profiles must never reach the real player's cloud data.
                State = OnlineState.Disabled;
                Status = "Offline (Editor test profile).";
                Changed?.Invoke();
                return;
            }
            _backend = OnlineBackends.Factory?.Invoke();
            if (_backend == null)
            {
                State = OnlineState.Disabled;
                Status = L.T("Online services are not installed. Playing offline.");
                Changed?.Invoke();
                return;
            }
            Connect();
        }

        /// <summary>Connects now (for example after the player turns online features back on).</summary>
        public void Reconnect()
        {
            _retryDelay = RetrySeconds;
            if (State == OnlineState.Offline) Connect();
        }

        /// <summary>Turns online features on or off (Settings). Off: no uploads, no leaderboards, no visits.</summary>
        public void SetEnabled(bool on)
        {
            _save.settings.online = on;
            if (on)
            {
                Reconnect();
                return;
            }
            if (State == OnlineState.Disabled) return;
            State = OnlineState.Offline;
            Status = L.T("Online features are off.");
            Changed?.Invoke();
        }

        /// <summary>
        /// Deletes this player's online account and what the servers keep for it. Online features are
        /// then switched off, so nothing is uploaded again until the player turns them back on.
        /// Progress on the phone stays.
        /// </summary>
        public async System.Threading.Tasks.Task<bool> DeleteAccount()
        {
            if (_backend == null || !IsOnline) return false;
            try
            {
                if (!await _backend.DeleteAccountAsync()) return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Account deletion failed: " + e.Message);
                return false;
            }
            _save.settings.online = false;
            _save.pending.Clear();
            _cloudDirty = false;
            _pendingCloudJson = null;
            _islandJson = null;
            _publishedIsland = null;
            CloudChecked = false;
            CloudFloor = -1;
            State = OnlineState.Offline;
            Status = L.T("Online features are off.");
            Changed?.Invoke();
            return true;
        }

        async void Connect()
        {
            if (_backend == null || State == OnlineState.Connecting) return;
            if (!_save.settings.online)
            {
                State = OnlineState.Offline;
                Status = L.T("Online features are off.");
                Changed?.Invoke();
                return;
            }
            State = OnlineState.Connecting;
            Status = L.T("Connecting...");
            Changed?.Invoke();
            bool ok = false;
            try
            {
                ok = Application.internetReachability != NetworkReachability.NotReachable && await _backend.ConnectAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Online connect failed: " + e.Message);
            }
            State = ok ? OnlineState.Online : OnlineState.Offline;
            if (ok)
            {
                Status = L.F("Online as {0}", PlayerName);
                _retryDelay = RetrySeconds;
            }
            else
            {
                string reason = _backend.LastError;
                Status = string.IsNullOrEmpty(reason)
                    ? L.T("Offline. Progress is saved locally and storms will be submitted later.")
                    : L.F("Offline: {0}", reason);
                _retryDelay = Mathf.Min(MaxRetrySeconds, _retryDelay * 2);
            }
            _retryTimer = ok ? RetrySeconds : _retryDelay;
            Changed?.Invoke();
            if (ok)
            {
                CheckCloudSave();
                Flush();
            }
        }

        public void Tick(float dt)
        {
            if (_backend == null) return;
            if (State == OnlineState.Offline)
            {
                if (!_save.settings.online) return;
                _retryTimer -= dt;
                if (_retryTimer <= 0) Connect();
                return;
            }
            if (State != OnlineState.Online) return;
            if (_save.pending.Count > 0 && !_flushing && SubmissionsEnabled)
            {
                _retryTimer -= dt;
                if (_retryTimer <= 0) Flush();
            }
            _islandTimer -= dt;
            if (_islandTimer <= 0 && _islandJson != null && _islandJson != _publishedIsland)
            {
                _islandTimer = IslandInterval;
                PushIsland();
            }
            _cloudTimer -= dt;
            if (_cloudTimer <= 0 && _cloudDirty && _pendingCloudJson != null)
            {
                _cloudTimer = CloudSaveInterval;
                PushCloud();
            }
        }

        // ------------------------------------------------------------ storms

        /// <summary>
        /// Queues a record-breaking storm. Boards keep each player's best score, so a queued storm is
        /// always sent to the all-time, weekly and class boards; a newer, higher storm of the same
        /// class and week makes an older queued one redundant.
        /// </summary>
        public void Queue(PendingSave p)
        {
            var list = _save.pending;
            list.RemoveAll(o => o.cls == p.cls && o.week == p.week && o.score <= p.score);
            list.Add(p);
            list.Sort((a, b) => b.score.CompareTo(a.score));
            if (list.Count > MaxPending) list.RemoveRange(MaxPending, list.Count - MaxPending);
            Changed?.Invoke();
            if (IsOnline && !_flushing) Flush();
        }

        async void Flush()
        {
            if (_backend == null || _flushing || !IsOnline || !SubmissionsEnabled) return;
            _flushing = true;
            try
            {
                var snapshot = new List<PendingSave>(_save.pending);
                foreach (var p in snapshot)
                {
                    SubmitOutcome outcome;
                    try
                    {
                        outcome = await _backend.SubmitAsync(p);
                    }
                    catch (Exception e)
                    {
                        outcome = new SubmitOutcome { Retry = true, Message = e.Message };
                    }
                    LastSubmitMessage = outcome.Message;
                    if (outcome.Retry)
                    {
                        // Back off so a misconfigured or unreachable backend is not hammered.
                        _retryTimer = _submitDelay;
                        _submitDelay = Mathf.Min(MaxRetrySeconds * 2, _submitDelay * 2);
                        break;
                    }
                    _submitDelay = RetrySeconds;
                    _save.pending.Remove(p);
                    InvalidateBoards();
                }
            }
            finally
            {
                _flushing = false;
                Changed?.Invoke();
            }
        }

        void InvalidateBoards()
        {
            foreach (var page in _cache.Values) page.FetchedUnix = 0;
        }

        // ------------------------------------------------------------ leaderboards

        public BoardPage Cached(string id) => _cache.TryGetValue(id, out var page) ? page : null;

        public async Task<BoardPage> FetchBoard(string id, bool force)
        {
            var cached = Cached(id);
            long now = Clock.Now;
            if (!force && cached != null && now - cached.FetchedUnix < 60) return cached;
            if (!IsOnline) return cached;
            try
            {
                var page = await _backend.GetBoardAsync(id, 50);
                if (page != null && page.Error == null)
                {
                    page.Id = id;
                    page.FetchedUnix = now;
                    _cache[id] = page;
                    SaveCache();
                }
                return page ?? cached;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Leaderboard fetch failed: " + e.Message);
                return cached;
            }
        }

        public async Task<bool> Rename(string name)
        {
            name = SanitizeName(name);
            if (string.IsNullOrEmpty(name) || !NameFilter.IsAllowed(name)) return false;
            _save.playerName = name;
            if (!IsOnline)
            {
                Changed?.Invoke();
                return true;
            }
            try
            {
                var final = await _backend.SetNameAsync(name);
                if (!string.IsNullOrEmpty(final)) _save.playerName = final;
                Status = L.F("Online as {0}", PlayerName);
                Changed?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Rename failed: " + e.Message);
                return false;
            }
        }

        public static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name.Trim())
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-') sb.Append(ch);
                else if (ch == ' ') sb.Append('_');
            var s = sb.ToString();
            return s.Length > 20 ? s.Substring(0, 20) : s;
        }

        // ------------------------------------------------------------ islands

        /// <summary>Queues the island layout for publishing (at most once a minute while online).</summary>
        public void PublishIsland(string json) => _islandJson = json;

        async void PushIsland()
        {
            string json = _islandJson;
            try
            {
                if (await _backend.PublishIslandAsync(json)) _publishedIsland = json;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Island publish failed: " + e.Message);
            }
        }

        /// <summary>Loads another player's island, or null when offline or they have not published one.</summary>
        public async Task<IslandLayout> LoadIsland(string playerId, Content content)
        {
            if (!IsOnline || string.IsNullOrEmpty(playerId)) return null;
            try
            {
                var json = await _backend.LoadIslandAsync(playerId);
                return IslandLayout.FromJson(json, content);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Island load failed: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Last week's final tournament rank. Null when offline or the request failed (try again later);
        /// Rank -1 with no VersionId when the board has no archived week yet.
        /// </summary>
        public async Task<ArchivedRank> LastWeekRank()
        {
            if (!IsOnline) return null;
            try
            {
                return await _backend.GetArchivedRankAsync(Boards.Weekly) ?? new ArchivedRank();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Could not read last week's tournament: " + e.Message);
                return null;
            }
        }

        // ------------------------------------------------------------ cloud save

        /// <summary>True once this session has looked at the cloud save; nothing is uploaded before that.</summary>
        public bool CloudChecked { get; private set; }

        /// <summary>
        /// Progress of a cloud save that has more than this device (set when one is found). The local
        /// save is only uploaded once it beats it, so a fresh or restored install never wipes out
        /// better progress in the cloud.
        /// </summary>
        public double CloudFloor { get; set; } = -1;

        double _pendingProgress;

        public void MarkCloudDirty(string json, double progress)
        {
            _pendingCloudJson = json;
            _pendingProgress = progress;
            _cloudDirty = true;
        }

        public void PushCloudNow(string json, double progress)
        {
            MarkCloudDirty(json, progress);
            if (IsOnline) PushCloud();
        }

        async void PushCloud()
        {
            if (_backend == null || !_cloudDirty || _pendingCloudJson == null) return;
            if (!CloudChecked || _pendingProgress <= CloudFloor) return;
            _cloudDirty = false;
            try
            {
                if (!await _backend.SaveCloudAsync("save", _pendingCloudJson)) _cloudDirty = true;
            }
            catch (Exception e)
            {
                _cloudDirty = true;
                Debug.LogWarning("[StormEngine] Cloud save failed: " + e.Message);
            }
        }

        async void CheckCloudSave()
        {
            try
            {
                var json = await _backend.LoadCloudAsync("save");
                if (!string.IsNullOrEmpty(json)) CloudSaveFound?.Invoke(json);
                CloudChecked = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Cloud load failed: " + e.Message);
            }
        }

        // ------------------------------------------------------------ cache persistence

        [Serializable]
        sealed class CacheFile
        {
            public List<CachedBoard> boards = new List<CachedBoard>();
        }

        [Serializable]
        sealed class CachedBoard
        {
            public string id;
            public long fetched;
            public List<CachedEntry> entries = new List<CachedEntry>();
            public CachedEntry me;
            public CachedEntry rival;
        }

        [Serializable]
        sealed class CachedEntry
        {
            public int rank;
            public string playerId;
            public string name;
            public double score;
            public string summary;
            public bool isMe;
        }

        static CachedEntry ToCache(BoardEntry e) => e == null ? null : new CachedEntry
        {
            rank = e.Rank, playerId = e.PlayerId, name = e.Name, score = e.Score, summary = e.Summary, isMe = e.IsMe
        };

        static BoardEntry FromCache(CachedEntry e) => e == null || string.IsNullOrEmpty(e.name) ? null : new BoardEntry
        {
            Rank = e.rank, PlayerId = e.playerId, Name = e.name, Score = e.score, Summary = e.summary, IsMe = e.isMe
        };

        void SaveCache()
        {
            try
            {
                var file = new CacheFile();
                foreach (var kv in _cache)
                {
                    var b = new CachedBoard { id = kv.Key, fetched = kv.Value.FetchedUnix, me = ToCache(kv.Value.Me), rival = ToCache(kv.Value.Rival) };
                    foreach (var e in kv.Value.Entries) b.entries.Add(ToCache(e));
                    file.boards.Add(b);
                }
                File.WriteAllText(_cachePath, JsonUtility.ToJson(file));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Could not write leaderboard cache: " + e.Message);
            }
        }

        void LoadCache()
        {
            try
            {
                if (!File.Exists(_cachePath)) return;
                var file = JsonUtility.FromJson<CacheFile>(File.ReadAllText(_cachePath));
                if (file?.boards == null) return;
                foreach (var b in file.boards)
                {
                    var page = new BoardPage { Id = b.id, FetchedUnix = 0, Me = FromCache(b.me), Rival = FromCache(b.rival) };
                    foreach (var e in b.entries)
                    {
                        var entry = FromCache(e);
                        if (entry != null) page.Entries.Add(entry);
                    }
                    _cache[b.id] = page;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Could not read leaderboard cache: " + e.Message);
            }
        }
    }
}
