using System;
using System.IO;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine
{
    /// <summary>
    /// Game time that changing the phone's date cannot fake. It advances with the phone's uptime
    /// counter (time since boot, including sleep), which players cannot set: between launches on the
    /// same boot the game continues from the saved moment by uptime alone. After a reboot the phone's
    /// clock is used again, but never earlier than the time the game already reached. When online
    /// features are on, the time from a web server corrects it (<see cref="SyncFromNetwork"/>).
    /// </summary>
    public static class Clock
    {
        static double _anchorUnix = double.NaN, _anchorUptime;

        /// <summary>Counts corrections from the network; offline progress must not count such jumps.</summary>
        public static int Corrections { get; private set; }

        public static double NowPrecise
        {
            get
            {
                double up = Uptime;
                // First use, or the phone rebooted (uptime restarted): start from the phone's clock.
                if (double.IsNaN(_anchorUnix) || up < _anchorUptime) Anchor(Wall, up);
                return _anchorUnix + (up - _anchorUptime);
            }
        }

        public static long Now => (long)Math.Floor(NowPrecise);

        /// <summary>
        /// Continues from a saved moment: on the same boot by uptime alone (whatever the phone's date
        /// says); after a reboot from the phone's clock, but never earlier than the saved time.
        /// </summary>
        public static void Restore(double savedUnix, double savedUptime)
        {
            double up = Uptime;
            if (savedUnix > 0 && savedUptime > 0 && up >= savedUptime) Anchor(savedUnix + (up - savedUptime), up);
            else Anchor(Math.Max(Wall, savedUnix), up);
        }

        /// <summary>Seconds since the phone booted, including deep sleep. Players cannot change it.</summary>
        public static double Uptime
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    _systemClock ??= new AndroidJavaClass("android.os.SystemClock");
                    return _systemClock.CallStatic<long>("elapsedRealtime") / 1000.0;
                }
                catch (Exception)
                {
                    // Fall back to the process timer below.
                }
#endif
                return System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaClass _systemClock;
#endif

        /// <summary>
        /// Reads the real time from a web server (the HTTP Date header of Google's connectivity check)
        /// and corrects game time when the phone's clock is off by more than half a minute.
        /// </summary>
        public static System.Collections.IEnumerator SyncFromNetwork()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable) yield break;
            using (var request = UnityEngine.Networking.UnityWebRequest.Head("https://www.google.com/generate_204"))
            {
                request.timeout = 8;
                double sent = Uptime;
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) yield break;
                string date = request.GetResponseHeader("Date");
                if (string.IsNullOrEmpty(date) || !DateTimeOffset.TryParse(date, System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out var server)) yield break;
                double up = Uptime;
                double trusted = server.ToUnixTimeMilliseconds() / 1000.0 + (up - sent) / 2;
                if (Math.Abs(trusted - NowPrecise) < 30) yield break;
                Anchor(trusted, up);
                Corrections++;
            }
        }

        static void Anchor(double unix, double uptime)
        {
            _anchorUnix = unix;
            _anchorUptime = uptime;
        }

        static double Wall => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    /// <summary>
    /// Versioned local save with atomic writes and a rolling backup. Each file is signed, so a save
    /// edited by hand (for example to add Gems) is rejected; on Android the files live in the app's
    /// private storage, which file browsers and other apps cannot open.
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "storm_save.json";
        static string MainPath => Path.Combine(Folder, IsTestProfile ? "storm_save." + TestProfile + ".json" : FileName);
        static string BackupPath => MainPath + ".bak";
        static string TempPath => MainPath + ".tmp";

        static string _folder;

        /// <summary>Where saves live: private internal storage on Android, persistentDataPath elsewhere.</summary>
        public static string Folder => _folder ??= !string.IsNullOrEmpty(FolderOverride) ? FolderOverride : ResolveFolder();

        /// <summary>
        /// Set before the first save is read to keep saves in another folder: the trailer recorder
        /// plays on its own copy and never touches the player's save.
        /// </summary>
        public static string FolderOverride;

        static string ResolveFolder()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                // Saves from builds before this one (on shared storage) are not carried over: a file
                // copied there over USB must never become a save.
                using (var dir = UnityEngine.Android.AndroidApplication.currentActivity.Call<AndroidJavaObject>("getFilesDir"))
                    return dir.Call<string>("getAbsolutePath");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[StormEngine] Private storage unavailable, using persistentDataPath: " + e.Message);
            }
#endif
            return Application.persistentDataPath;
        }

#if UNITY_EDITOR
        const string ProfileKey = "StormEngine.TestProfile";

        /// <summary>
        /// Editor only: a named test profile uses its own save file ("storm_save.&lt;name&gt;.json") and
        /// stays offline, so test sessions never touch the real save or its cloud data. Kept for the
        /// Editor session (survives entering Play mode); empty means the normal save.
        /// </summary>
        public static string TestProfile
        {
            get => UnityEditor.SessionState.GetString(ProfileKey, "");
            set => UnityEditor.SessionState.SetString(ProfileKey, value ?? "");
        }

        public static bool IsTestProfile => !string.IsNullOrEmpty(TestProfile);
#else
        public static string TestProfile => "";
        public static bool IsTestProfile => false;
#endif

        /// <summary>The file the game reads and writes (for tools and tests).</summary>
        public static string CurrentPath => MainPath;

        /// <summary>
        /// Loads the newest save whose signature checks out (the main file, else the backup). If only an
        /// edited save is left, its progress is kept but its Gems are not.
        /// </summary>
        public static SaveData Load()
        {
            SaveData edited = null;
            foreach (var path in new[] { MainPath, BackupPath })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    bool genuine = Unseal(File.ReadAllText(path), out string json);
                    var data = FromJson(json);
                    if (data == null) continue;
                    if (genuine) return data;
                    Debug.LogWarning($"[StormEngine] Save {Path.GetFileName(path)} was edited outside the game; ignoring it.");
                    edited ??= data;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[StormEngine] Could not read save {path}: {e.Message}");
                }
            }
            if (edited != null) edited.gems = 0;
            return edited;
        }

        public static bool Write(SaveData data)
        {
            try
            {
                File.WriteAllText(TempPath, Seal(data));
                if (File.Exists(MainPath))
                {
                    File.Copy(MainPath, BackupPath, true);
                    File.Delete(MainPath);
                }
                File.Move(TempPath, MainPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[StormEngine] Save failed: " + e.Message);
                return false;
            }
        }

        public static void Delete()
        {
            foreach (var path in new[] { MainPath, BackupPath, TempPath })
            {
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[StormEngine] Could not delete {path}: {e.Message}");
                }
            }
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data);

        /// <summary>The save as signed text: the local file and the cloud copy.</summary>
        public static string Seal(SaveData data)
        {
            string json = ToJson(data);
            return json + SignatureMark + Sign(json);
        }

        /// <summary>Reads a cloud copy made by <see cref="Seal"/>; null if the game did not write it.</summary>
        public static SaveData Open(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            if (Unseal(text, out string json)) return FromJson(json);
            Debug.LogWarning("[StormEngine] Cloud save was not written by the game; ignoring it.");
            return null;
        }

        // ------------------------------------------------------------ signing

        const string SignatureMark = "\n#sig:";

        // Signing key, stored masked so it is not a plain string in the build.
        static readonly byte[] KeyMask =
        {
            0x32, 0xE3, 0x52, 0xAA, 0x73, 0x1D, 0xA2, 0x61, 0xEA, 0x46, 0x96, 0xD7, 0x07, 0x49, 0xB9, 0xB4,
            0x70, 0xA3, 0xDC, 0xA4, 0x82, 0x03, 0xD5, 0xE8, 0xF5, 0x48, 0x6C, 0x2B, 0x4D, 0x69, 0x58, 0x2F
        };

        static string Sign(string json)
        {
            var key = new byte[KeyMask.Length];
            for (int i = 0; i < key.Length; i++) key[i] = (byte)(KeyMask[i] ^ (0x5A + i * 29));
            using (var hmac = new System.Security.Cryptography.HMACSHA256(key))
                return Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json)));
        }

        /// <summary>
        /// Splits a saved file into its JSON; true when the signature matches. Unsigned files (saves
        /// from before signing) only count in the Editor and development builds: in a release build a
        /// missing signature means the file was rewritten by hand.
        /// </summary>
        static bool Unseal(string text, out string json)
        {
            int mark = text.LastIndexOf(SignatureMark, StringComparison.Ordinal);
            if (mark < 0)
            {
                json = text;
                return Application.isEditor || Debug.isDebugBuild;
            }
            json = text.Substring(0, mark);
            string signature = text.Substring(mark + SignatureMark.Length).Trim();
            return signature == Sign(json);
        }

        public static SaveData FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return null;
            Migrate(data);
            return data;
        }

        /// <summary>
        /// Fills structures missing from older saves. The version number itself is kept so that
        /// <see cref="SaveMapper.Apply"/> can run version-specific migrations; the next save
        /// writes the current version.
        /// </summary>
        static void Migrate(SaveData data)
        {
            if (data.version > SaveData.CurrentVersion)
                Debug.LogWarning("[StormEngine] Save was written by a newer version of the game.");
            data.settings ??= new SettingsSave();
            data.stats ??= new StatsSave();
            data.records ??= new RecordsSave();
            data.pending ??= new System.Collections.Generic.List<PendingSave>();
            data.upgrades ??= new System.Collections.Generic.List<IdLevel>();
            data.cosmetics ??= new System.Collections.Generic.List<string>();
            data.equippedCosmetics ??= new System.Collections.Generic.List<string>();
        }
    }
}
