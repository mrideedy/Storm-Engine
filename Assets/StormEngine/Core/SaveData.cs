using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    [Serializable]
    public sealed class IdLevel
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class BoostSave
    {
        public string id;
        public double active;
        public double cooldown;
    }

    [Serializable]
    public sealed class IdScore
    {
        public string id;
        public double score;
    }

    [Serializable]
    public sealed class StatsSave
    {
        public long storms;
        public long strikes;
        public long boostsUsed;
        public long megaStorms;
        public double bestChain = 1;
        public double playSeconds;
        public double bestPower;
    }

    [Serializable]
    public sealed class RecordsSave
    {
        public double bestScore;
        public string bestClass;
        public int weeklyWeek = -1;
        public double weeklyBest;
        public string weeklyClass;
        public List<IdScore> classBest = new List<IdScore>();
    }

    [Serializable]
    public sealed class IdCount
    {
        public string id;
        public int count;
    }

    [Serializable]
    public sealed class DecorSave
    {
        public string id;
        public float x;
        public float z;
        public float rot;
    }

    [Serializable]
    public sealed class TournamentSave
    {
        public int week = -1;
        public double best;
        public string bestClass;
        public long day = -1;
        public int attemptsUsed;
        public int bonusAttempts;
        public int runs;
        public double allTimeBest;
        public string allTimeClass;
        public List<IdScore> classBest = new List<IdScore>();
        public int rewardedWeek = -1;
        public int playedWeek = -1;
    }

    [Serializable]
    public sealed class DailySave
    {
        public long day = -1;
        public List<string> missions = new List<string>();
        public List<double> progress = new List<double>();
        public List<bool> claimed = new List<bool>();
        public bool bonusClaimed;
        public int streak;
        public int bestStreak;
        public long loginDay = -1;
        public int lostStreak;
        public long lostDay = -1;
    }

    /// <summary>A storm waiting to be sent to the leaderboards (kept while offline).</summary>
    [Serializable]
    public sealed class PendingSave
    {
        public string json;
        public string summary;
        public double score;
        public string cls;
        public int week;
        public long unix;
        public bool allTime;
        public bool weekly;
        public bool classBoard;
        /// <summary>Equipped title cosmetic id shown next to the name on the boards.</summary>
        public string title;
    }

    [Serializable]
    public sealed class SettingsSave
    {
        public bool sfx = true;
        public bool music = true;
        public bool ambience = true;
        public float sfxVolume = 0.8f;
        public float musicVolume = 0.5f;
        public bool vibrate = true;
        public int notation;
        public bool reduceMotion;
        public int buyMode;
        public int quality = 2;
        /// <summary>Show the category chips on the HUD (hidden by default for a clean view).</summary>
        public bool showStats;
        /// <summary>Selected background music track id.</summary>
        public string musicTrack = "drift";
        /// <summary>0 = Battery saver (30 FPS), 1 = Standard (60), 2 = Smooth (the screen's top rate, up to 120).</summary>
        public int frameRate = 1;
        /// <summary>Lower graphics quality automatically when the phone cannot keep up.</summary>
        public bool autoQuality = true;
        /// <summary>Local reminders: daily gift, tournament closing, new week, full Offline Generator.</summary>
        public bool notifications = true;
        /// <summary>The player has answered the in-game "want reminders?" question.</summary>
        public bool notifyAsked;
        /// <summary>Language code (see Languages); empty means "detect from the phone".</summary>
        public string language = "";
        /// <summary>Show guided tips (the walkthrough and feature introductions).</summary>
        public bool tips = true;
        /// <summary>Ad privacy choice: 0 = not asked yet, 1 = personalized ads allowed, 2 = non-personalized only.</summary>
        public int adConsent;
        /// <summary>Development builds: show a frame-rate readout.</summary>
        public bool showFps;
        /// <summary>Online features (leaderboards, tournaments, cloud save, island visits). Off = nothing leaves the phone.</summary>
        public bool online = true;
        /// <summary>The HUD goal card is folded into a small chip (tap it to switch).</summary>
        public bool goalFolded;
    }

    /// <summary>Versioned save file. Plain fields so Unity's JsonUtility can serialize it.</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public string playerId;
        public string playerName;
        public long createdUnix;
        public long lastSaveUnix;
        /// <summary>Trusted game time at the last save and the phone's uptime then (see Clock).</summary>
        public double clockUnix;
        public double clockUptime;
        public double energy;
        public double runEnergy;
        public double lifetimeEnergy;
        public long cores;
        public long research;
        public long researchLifetime;
        public int prestiges;
        public List<IdLevel> machines = new List<IdLevel>();
        public List<string> unlockedMachines = new List<string>();
        public List<IdLevel> modules = new List<IdLevel>();
        public List<string> equipped = new List<string>();
        public List<IdLevel> researchNodes = new List<IdLevel>();
        public List<BoostSave> boosts = new List<BoostSave>();
        public double stormCharge;
        public bool autoStorm = true;
        public List<string> discovered = new List<string>();
        public List<string> goalsDone = new List<string>();
        public StatsSave stats = new StatsSave();
        public RecordsSave records = new RecordsSave();
        public List<PendingSave> pending = new List<PendingSave>();
        public SettingsSave settings = new SettingsSave();
        public int engineLevel = 1;
        public double xp;
        public List<IdLevel> upgrades = new List<IdLevel>();
        public List<string> cosmetics = new List<string>();
        public List<string> equippedCosmetics = new List<string>();
        /// <summary>Panels the player has opened at least once (for "new" badges).</summary>
        public List<string> seenFeatures = new List<string>();
        public long gems;
        public List<IdCount> decorOwned = new List<IdCount>();
        public List<DecorSave> decor = new List<DecorSave>();
        public List<string> expansions = new List<string>();
        public List<IdCount> bundleAds = new List<IdCount>();
        public List<string> products = new List<string>();
        public TournamentSave tournament = new TournamentSave();
        public long adDay = -1;
        public List<IdCount> adUsed = new List<IdCount>();
        public long adTotal;
        public double surgeSeconds;
        public long lastStormCores;
        /// <summary>Stormpedia entries the player has opened (for "new" dots).</summary>
        public List<string> pediaSeen = new List<string>();
        /// <summary>Weekly conditions the player has experienced (listed in the Stormpedia).</summary>
        public List<string> weeklySeen = new List<string>();
        /// <summary>Music tracks unlocked with rewarded ads.</summary>
        public List<string> musicOwned = new List<string>();
        /// <summary>Login streak and today's missions (null in saves from before daily rewards).</summary>
        public DailySave daily;
        /// <summary>Guided tips already shown.</summary>
        public List<string> tutorials = new List<string>();
        /// <summary>False in saves from before tips: tips for things the player already knows are skipped once.</summary>
        public bool tutorialsMigrated;
        /// <summary>How many times Google Play's rating card has been requested (at most twice).</summary>
        public int reviewAsks;

        /// <summary>Rough progress measure used to pick between a local and a cloud save.</summary>
        public double ProgressScore() => Math.Log10(1 + lifetimeEnergy) + researchLifetime * 0.5 + prestiges + engineLevel * 0.05 + gems * 0.0001;
    }

    public static class SaveMapper
    {
        public static SaveData Capture(Game g, SaveData into)
        {
            var c = g.C;
            var d = into ?? new SaveData();
            d.version = SaveData.CurrentVersion;
            d.lastSaveUnix = g.NowUnix;
            d.energy = g.Energy;
            d.runEnergy = g.RunEnergy;
            d.lifetimeEnergy = g.LifetimeEnergy;
            d.cores = g.Cores;
            d.research = g.Research;
            d.researchLifetime = g.ResearchLifetime;
            d.prestiges = g.Prestiges;

            d.machines.Clear();
            d.unlockedMachines.Clear();
            for (int i = 0; i < c.Machines.Length; i++)
            {
                if (g.Build.Machines[i] > 0) d.machines.Add(new IdLevel { id = c.Machines[i].Id, level = g.Build.Machines[i] });
                if (g.MachineUnlocked[i]) d.unlockedMachines.Add(c.Machines[i].Id);
            }

            d.modules.Clear();
            for (int i = 0; i < c.Modules.Length; i++)
                if (g.Build.ModuleLevels[i] > 0) d.modules.Add(new IdLevel { id = c.Modules[i].Id, level = g.Build.ModuleLevels[i] });
            d.equipped.Clear();
            foreach (int m in g.Build.Equipped) d.equipped.Add(c.Modules[m].Id);

            d.researchNodes.Clear();
            for (int i = 0; i < c.Research.Length; i++)
                if (g.Build.Research[i] > 0) d.researchNodes.Add(new IdLevel { id = c.Research[i].Id, level = g.Build.Research[i] });

            d.boosts.Clear();
            for (int i = 0; i < c.Boosts.Length; i++)
                d.boosts.Add(new BoostSave { id = c.Boosts[i].Id, active = g.BoostActive[i], cooldown = g.BoostCooldown[i] });

            d.stormCharge = g.StormCharge;
            d.autoStorm = g.AutoStorm;

            d.discovered.Clear();
            for (int p = 0; p < c.Phenomena.Length; p++)
                if (g.Discovered[p]) d.discovered.Add(c.Phenomena[p].Id);
            d.goalsDone.Clear();
            d.goalsDone.AddRange(g.GoalsDone);

            var s = g.Stats;
            d.stats.storms = s.Storms;
            d.stats.strikes = s.Strikes;
            d.stats.boostsUsed = s.BoostsUsed;
            d.stats.megaStorms = s.MegaStorms;
            d.stats.bestChain = s.BestChain;
            d.stats.playSeconds = s.PlaySeconds;
            d.stats.bestPower = s.BestPower;

            d.engineLevel = g.Level;
            d.xp = g.Xp;
            d.upgrades.Clear();
            for (int i = 0; i < c.Upgrades.Length; i++)
                if (g.Build.Upgrades[i] > 0) d.upgrades.Add(new IdLevel { id = c.Upgrades[i].Id, level = g.Build.Upgrades[i] });
            d.cosmetics.Clear();
            d.cosmetics.AddRange(g.OwnedCosmetics);
            d.equippedCosmetics.Clear();
            foreach (var id in g.EquippedCosmetics) if (!string.IsNullOrEmpty(id)) d.equippedCosmetics.Add(id);

            d.gems = g.Gems;
            d.decorOwned.Clear();
            foreach (var kv in g.DecorOwned) if (kv.Value > 0) d.decorOwned.Add(new IdCount { id = kv.Key, count = kv.Value });
            d.decor.Clear();
            foreach (var pd in g.Placed) d.decor.Add(new DecorSave { id = pd.Id, x = pd.X, z = pd.Z, rot = pd.Rot });
            d.expansions.Clear();
            d.expansions.AddRange(g.Expansions);
            d.bundleAds.Clear();
            foreach (var kv in g.BundleAds) d.bundleAds.Add(new IdCount { id = kv.Key, count = kv.Value });
            d.products.Clear();
            d.products.AddRange(g.OwnedProducts);
            d.adDay = g.Ads.Day;
            d.adUsed.Clear();
            foreach (var kv in g.Ads.Used) d.adUsed.Add(new IdCount { id = kv.Key, count = kv.Value });
            d.adTotal = g.Ads.Total;
            d.surgeSeconds = g.SurgeSeconds;
            d.lastStormCores = g.LastStormCores;

            var t = g.Tournament;
            var ts = d.tournament ?? (d.tournament = new TournamentSave());
            ts.week = t.Week;
            ts.best = t.Best;
            ts.bestClass = t.Best > 0 ? StormSubmission.ClassId(c, t.BestClass) : null;
            ts.day = t.Day;
            ts.attemptsUsed = t.AttemptsUsed;
            ts.bonusAttempts = t.BonusAttempts;
            ts.runs = t.Runs;
            ts.allTimeBest = t.AllTimeBest;
            ts.allTimeClass = t.AllTimeBest > 0 ? StormSubmission.ClassId(c, t.AllTimeClass) : null;
            ts.classBest.Clear();
            for (int i = 0; i < t.ClassBest.Length; i++)
                if (t.ClassBest[i] > 0) ts.classBest.Add(new IdScore { id = StormSubmission.ClassId(c, i - 1), score = t.ClassBest[i] });
            ts.rewardedWeek = t.RewardedWeek;
            ts.playedWeek = t.PlayedWeek;

            var dy = g.Daily;
            var ds = d.daily ?? (d.daily = new DailySave());
            ds.day = dy.Day;
            ds.missions.Clear();
            ds.progress.Clear();
            ds.claimed.Clear();
            for (int i = 0; i < DailyState.MissionCount; i++)
            {
                ds.missions.Add(dy.Missions[i] ?? "");
                ds.progress.Add(dy.Progress[i]);
                ds.claimed.Add(dy.Claimed[i]);
            }
            ds.bonusClaimed = dy.BonusClaimed;
            ds.streak = dy.Streak;
            ds.bestStreak = dy.BestStreak;
            ds.loginDay = dy.LoginDay;
            ds.lostStreak = dy.LostStreak;
            ds.lostDay = dy.LostDay;

            var r = g.Records;
            d.records.bestScore = r.BestScore;
            d.records.bestClass = r.BestClass >= -1 && r.BestScore > 0 ? StormSubmission.ClassId(c, r.BestClass) : null;
            d.records.weeklyWeek = r.WeeklyWeek;
            d.records.weeklyBest = r.WeeklyBest;
            d.records.weeklyClass = r.WeeklyBest > 0 ? StormSubmission.ClassId(c, r.WeeklyClass) : null;
            d.records.classBest.Clear();
            for (int i = 0; i < r.ClassBest.Length; i++)
                if (r.ClassBest[i] > 0) d.records.classBest.Add(new IdScore { id = StormSubmission.ClassId(c, i - 1), score = r.ClassBest[i] });
            return d;
        }

        /// <summary>Applies a save to a fresh game. Unknown ids (removed content) are ignored.</summary>
        public static void Apply(SaveData d, Game g)
        {
            var c = g.C;
            g.Energy = Safe(d.energy);
            g.RunEnergy = Safe(d.runEnergy);
            g.LifetimeEnergy = Safe(d.lifetimeEnergy);
            g.Cores = Math.Max(0, d.cores);
            g.Research = Math.Max(0, d.research);
            g.ResearchLifetime = Math.Max(0, d.researchLifetime);
            g.Prestiges = Math.Max(0, d.prestiges);

            if (d.machines != null)
                foreach (var m in d.machines)
                {
                    int i = c.MachineIndex(m.id);
                    if (i < 0) continue;
                    int max = c.Machines[i].MaxLevel;
                    g.Build.Machines[i] = Math.Max(0, max > 0 ? Math.Min(max, m.level) : m.level);
                }
            if (d.unlockedMachines != null)
                foreach (var id in d.unlockedMachines)
                {
                    int i = c.MachineIndex(id);
                    if (i >= 0) g.MachineUnlocked[i] = true;
                }
            for (int i = 0; i < c.Machines.Length; i++)
                if (g.Build.Machines[i] > 0) g.MachineUnlocked[i] = true;

            if (d.modules != null)
                foreach (var m in d.modules)
                {
                    int i = c.ModuleIndex(m.id);
                    if (i >= 0) g.Build.ModuleLevels[i] = Math.Max(0, Math.Min(c.Modules[i].MaxLevel, m.level));
                }
            g.Build.Equipped.Clear();
            if (d.equipped != null)
                foreach (var id in d.equipped)
                {
                    int i = c.ModuleIndex(id);
                    if (i >= 0 && g.Build.ModuleLevels[i] > 0 && !g.Build.Equipped.Contains(i)) g.Build.Equipped.Add(i);
                }

            if (d.researchNodes != null)
                foreach (var r in d.researchNodes)
                {
                    int i = c.ResearchIndex(r.id);
                    if (i >= 0) g.Build.Research[i] = Math.Max(0, Math.Min(c.Research[i].MaxLevel, r.level));
                }

            if (d.boosts != null)
                foreach (var b in d.boosts)
                {
                    int i = c.BoostIndex(b.id);
                    if (i < 0) continue;
                    g.BoostActive[i] = Math.Max(0, Safe(b.active));
                    g.BoostCooldown[i] = Math.Max(0, Safe(b.cooldown));
                }

            g.StormCharge = Math.Max(0, Math.Min(1, Safe(d.stormCharge)));
            g.AutoStorm = d.autoStorm;

            if (d.discovered != null)
                foreach (var id in d.discovered)
                {
                    int p = c.PhenoIndex(id);
                    if (p >= 0) g.Discovered[p] = true;
                }
            g.GoalsDone.Clear();
            if (d.goalsDone != null)
                foreach (var id in d.goalsDone) g.GoalsDone.Add(id);

            if (d.stats != null)
            {
                g.Stats.Storms = d.stats.storms;
                g.Stats.Strikes = d.stats.strikes;
                g.Stats.BoostsUsed = d.stats.boostsUsed;
                g.Stats.MegaStorms = d.stats.megaStorms;
                g.Stats.BestChain = Math.Max(1, d.stats.bestChain);
                g.Stats.PlaySeconds = Safe(d.stats.playSeconds);
                g.Stats.BestPower = Safe(d.stats.bestPower);
            }

            if (d.records != null)
            {
                var r = g.Records;
                r.BestScore = Safe(d.records.bestScore);
                r.BestClass = ClassIndex(c, d.records.bestClass);
                r.WeeklyWeek = d.records.weeklyWeek;
                r.WeeklyBest = Safe(d.records.weeklyBest);
                r.WeeklyClass = ClassIndex(c, d.records.weeklyClass);
                if (d.records.classBest != null)
                    foreach (var cb in d.records.classBest)
                    {
                        if (cb.id == "squall") r.ClassBest[0] = Safe(cb.score);
                        int p = c.PhenoIndex(cb.id);
                        if (p >= 0) r.ClassBest[p + 1] = Safe(cb.score);
                    }
            }

            if (d.upgrades != null)
                foreach (var u in d.upgrades)
                {
                    int i = c.UpgradeIndex(u.id);
                    if (i >= 0) g.Build.Upgrades[i] = Math.Max(0, Math.Min(c.Upgrades[i].MaxLevel, u.level));
                }
            if (d.cosmetics != null)
                foreach (var id in d.cosmetics)
                    if (c.CosmeticIndex(id) >= 0) g.OwnedCosmetics.Add(id);
            if (d.equippedCosmetics != null)
                foreach (var id in d.equippedCosmetics)
                {
                    int i = c.CosmeticIndex(id);
                    if (i >= 0 && g.OwnedCosmetics.Contains(id)) g.EquippedCosmetics[(int)c.Cosmetics[i].Slot] = id;
                }

            g.Gems = Math.Max(0, d.gems);
            if (d.decorOwned != null)
                foreach (var dc in d.decorOwned)
                {
                    int i = c.DecorationIndex(dc.id);
                    if (i >= 0 && dc.count > 0) g.DecorOwned[dc.id] = Math.Min(c.Decorations[i].MaxCopies, dc.count);
                }
            g.Placed.Clear();
            if (d.decor != null)
                foreach (var pd in d.decor)
                {
                    if (c.DecorationIndex(pd.id) < 0 || g.PlacedCopies(pd.id) >= g.DecorCopies(pd.id)) continue;
                    g.Placed.Add(new PlacedDecor { Id = pd.id, X = SafeF(pd.x), Z = SafeF(pd.z), Rot = SafeF(pd.rot) });
                }
            if (d.expansions != null)
                foreach (var id in d.expansions)
                    if (c.ExpansionIndex(id) >= 0) g.Expansions.Add(id);
            if (d.bundleAds != null)
                foreach (var ba in d.bundleAds)
                    if (c.BundleIndex(ba.id) >= 0) g.BundleAds[ba.id] = Math.Max(0, ba.count);
            if (d.products != null)
                foreach (var id in d.products) g.OwnedProducts.Add(id);
            g.Ads.Day = d.adDay;
            g.Ads.Used.Clear();
            if (d.adUsed != null)
                foreach (var au in d.adUsed) g.Ads.Used[au.id] = Math.Max(0, au.count);
            g.Ads.Total = Math.Max(0, d.adTotal);
            g.SurgeSeconds = Math.Max(0, Math.Min(c.B.SurgeMaxSeconds, Safe(d.surgeSeconds)));
            g.LastStormCores = Math.Max(0, d.lastStormCores);

            if (d.tournament != null)
            {
                var ts = d.tournament;
                var t = g.Tournament;
                t.Week = ts.week;
                t.Best = Safe(ts.best);
                t.BestClass = ClassIndex(c, ts.bestClass);
                t.Day = ts.day;
                t.AttemptsUsed = Math.Max(0, ts.attemptsUsed);
                t.BonusAttempts = Math.Max(0, ts.bonusAttempts);
                t.Runs = Math.Max(0, ts.runs);
                t.AllTimeBest = Safe(ts.allTimeBest);
                t.AllTimeClass = ClassIndex(c, ts.allTimeClass);
                if (ts.classBest != null)
                    foreach (var cb in ts.classBest)
                    {
                        if (cb.id == "squall") t.ClassBest[0] = Safe(cb.score);
                        int p = c.PhenoIndex(cb.id);
                        if (p >= 0) t.ClassBest[p + 1] = Safe(cb.score);
                    }
                t.RewardedWeek = ts.rewardedWeek;
                t.PlayedWeek = ts.playedWeek;
            }

            // Without saved daily state the next clock update picks fresh missions for this player.
            g.Daily = new DailyState();
            if (d.daily != null)
            {
                var ds = d.daily;
                var dy = g.Daily;
                dy.Day = ds.day;
                for (int i = 0; i < DailyState.MissionCount; i++)
                {
                    string id = ds.missions != null && i < ds.missions.Count ? ds.missions[i] : null;
                    dy.Missions[i] = c.MissionIndex(id) >= 0 ? id : null;
                    dy.Progress[i] = ds.progress != null && i < ds.progress.Count ? Math.Max(0, Safe(ds.progress[i])) : 0;
                    dy.Claimed[i] = ds.claimed != null && i < ds.claimed.Count && ds.claimed[i];
                }
                dy.BonusClaimed = ds.bonusClaimed;
                dy.Streak = Math.Max(0, ds.streak);
                dy.BestStreak = Math.Max(dy.Streak, ds.bestStreak);
                dy.LoginDay = ds.loginDay;
                dy.LostStreak = Math.Max(0, ds.lostStreak);
                dy.LostDay = ds.lostDay;
            }

            if (d.version < 2)
            {
                // Saves from before Engine Levels: turn existing progress into XP.
                double xp = 0;
                foreach (int l in g.Build.Machines) xp += l * c.B.XpPerMachineLevel;
                xp += g.Stats.Storms * c.B.XpPerStorm;
                foreach (bool found in g.Discovered) if (found) xp += c.B.XpPerDiscovery;
                xp += g.Prestiges * c.B.XpPerPrestige;
                foreach (var goal in c.Goals) if (g.GoalsDone.Contains(goal.Id)) xp += goal.RewardXp;
                g.SetProgress(1, 0);
                g.AddXp(xp);
            }
            else
            {
                g.SetProgress(d.engineLevel, d.xp);
            }

            g.SyncStyle();
            g.SyncDecor();
            g.MarkDirty();
            g.Refresh();
        }

        static float SafeF(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0 : v;

        static int ClassIndex(Content c, string id) => string.IsNullOrEmpty(id) || id == "squall" ? -1 : c.PhenoIndex(id);

        static double Safe(double v) => double.IsNaN(v) || double.IsInfinity(v) ? 0 : v;
    }
}
