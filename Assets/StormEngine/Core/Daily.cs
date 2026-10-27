using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    /// <summary>What a daily mission counts.</summary>
    public enum MissionKind
    {
        Storms,
        Strikes,
        PhenoStorms,
        Chain,
        MegaStorms,
        Cores,
        Boosts,
        StormBoosts,
        MachineLevels,
        ModuleUpgrades,
        Decorations,
        Tournament
    }

    /// <summary>A daily reward: Gems, Storm Cores (they grow with Engine Level) and Energy Surge time.</summary>
    public sealed class DailyReward
    {
        public long Gems;
        /// <summary>Storm Cores at Engine Level 1; see <see cref="Game.DailyCores"/>.</summary>
        public long Cores;
        public double SurgeSeconds;
    }

    /// <summary>One of the three missions offered each day.</summary>
    public sealed class MissionDef
    {
        public string Id;
        public string Text;
        public string Icon;
        public MissionKind Kind;
        public double Amount;
        public int MinLevel = 1;
        /// <summary>Retired from the daily pool above this Engine Level (0 = never).</summary>
        public int MaxLevel;
        public DailyReward Reward = new DailyReward();
    }

    /// <summary>Login streak and today's missions. A new day starts at midnight UTC.</summary>
    public sealed class DailyState
    {
        public const int MissionCount = 3;

        /// <summary>UTC day the missions belong to.</summary>
        public long Day = -1;
        public readonly string[] Missions = new string[MissionCount];
        public readonly double[] Progress = new double[MissionCount];
        public readonly bool[] Claimed = new bool[MissionCount];
        /// <summary>The reward for finishing all three missions was collected.</summary>
        public bool BonusClaimed;
        /// <summary>Consecutive days the login reward was collected.</summary>
        public int Streak;
        public int BestStreak;
        /// <summary>Last UTC day the login reward was collected.</summary>
        public long LoginDay = -1;
        /// <summary>A streak broken by missed days. It can be restored with one rewarded ad on <see cref="LostDay"/>.</summary>
        public int LostStreak;
        public long LostDay = -1;
    }

    public sealed partial class Game
    {
        public DailyState Daily = new DailyState();

        /// <summary>Raised when a daily mission reaches its target, with the mission slot.</summary>
        public event Action<int> MissionCompleted;
        /// <summary>Raised when a new UTC day starts while playing (fresh missions, login reward ready).</summary>
        public event Action DayChanged;

        public bool DailyUnlocked => IsFeatureUnlocked("daily");
        public long Today => DayOf(NowUnix);
        public long SecondsToNewDay => Math.Max(0, (Today + 1) * 86400 - NowUnix);

        void RollDaily(long day)
        {
            var d = Daily;
            bool first = d.Day < 0;
            d.Day = day;
            // Missing a day breaks the login streak. After a short absence it can be restored with an ad.
            if (d.LoginDay >= 0 && d.LoginDay < day - 1 && d.Streak > 0)
            {
                long missed = day - d.LoginDay - 1;
                d.LostStreak = missed <= C.B.StreakRestoreMaxMissed ? d.Streak : 0;
                d.LostDay = day;
                d.Streak = 0;
            }
            PickMissions(day);
            if (!first) DayChanged?.Invoke();
        }

        void PickMissions(long day)
        {
            var d = Daily;
            var pool = new List<int>();
            for (int i = 0; i < C.Missions.Length; i++)
                if (MissionEligible(C.Missions[i])) pool.Add(i);
            ulong seed = (ulong)day * 0x9E3779B97F4A7C15UL + 0x5EED;
            var kinds = new HashSet<MissionKind>();
            for (int slot = 0; slot < DailyState.MissionCount; slot++)
            {
                d.Missions[slot] = null;
                d.Progress[slot] = 0;
                d.Claimed[slot] = false;
                pool.RemoveAll(i => kinds.Contains(C.Missions[i].Kind));
                if (pool.Count == 0) continue;
                int pick = pool[(int)(Mix(ref seed) % (ulong)pool.Count)];
                kinds.Add(C.Missions[pick].Kind);
                d.Missions[slot] = C.Missions[pick].Id;
            }
            d.BonusClaimed = false;
        }

        /// <summary>SplitMix64: the same missions for a given day on every device.</summary>
        static ulong Mix(ref ulong state)
        {
            ulong z = state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>Only missions the player can already do are offered.</summary>
        bool MissionEligible(MissionDef m)
        {
            if (Level < m.MinLevel || m.MaxLevel > 0 && Level > m.MaxLevel) return false;
            switch (m.Kind)
            {
                case MissionKind.Strikes: return Stats.Strikes >= m.Amount;
                case MissionKind.PhenoStorms: return Array.IndexOf(Discovered, true) >= 0;
                case MissionKind.Chain: return Stats.BestChain >= m.Amount;
                case MissionKind.MegaStorms: return Stats.MegaStorms > 0;
                case MissionKind.Boosts:
                case MissionKind.StormBoosts:
                    for (int b = 0; b < C.Boosts.Length; b++)
                        if (IsBoostUnlocked(b)) return true;
                    return false;
                case MissionKind.ModuleUpgrades:
                    if (!IsFeatureUnlocked("modules")) return false;
                    for (int i = 0; i < C.Modules.Length; i++)
                        if (Build.ModuleLevels[i] > 0 || ModuleRequirementsMet(i)) return true;
                    return false;
                case MissionKind.Decorations: return IsFeatureUnlocked("island");
                case MissionKind.Tournament: return TournamentUnlocked;
                default: return true;
            }
        }

        void CountMission(MissionKind kind, double amount, bool peak = false)
        {
            var d = Daily;
            if (amount <= 0 || double.IsNaN(amount) || d.Day < 0 || !DailyUnlocked) return;
            for (int i = 0; i < DailyState.MissionCount; i++)
            {
                var m = MissionAt(i);
                if (m == null || m.Kind != kind || d.Claimed[i]) continue;
                bool done = d.Progress[i] >= m.Amount;
                d.Progress[i] = peak ? Math.Max(d.Progress[i], amount) : d.Progress[i] + amount;
                if (!done && d.Progress[i] >= m.Amount) MissionCompleted?.Invoke(i);
            }
        }

        void CountStorm(StormResult r)
        {
            CountMission(MissionKind.Storms, 1);
            CountMission(MissionKind.Strikes, r.Strikes);
            if (r.ClassIndex >= 0) CountMission(MissionKind.PhenoStorms, 1);
            CountMission(MissionKind.Chain, r.PeakChain, true);
            if (r.MegaTicks > 0) CountMission(MissionKind.MegaStorms, 1);
            CountMission(MissionKind.Cores, r.Cores);
        }

        public MissionDef MissionAt(int slot)
        {
            if (slot < 0 || slot >= DailyState.MissionCount) return null;
            int i = C.MissionIndex(Daily.Missions[slot]);
            return i >= 0 ? C.Missions[i] : null;
        }

        public bool IsMissionDone(int slot)
        {
            var m = MissionAt(slot);
            return m != null && Daily.Progress[slot] >= m.Amount;
        }

        public bool CanClaimMission(int slot) => DailyUnlocked && IsMissionDone(slot) && !Daily.Claimed[slot];

        /// <summary>Collects a finished mission's reward. Returns null if it cannot be claimed.</summary>
        public DailyReward ClaimMission(int slot)
        {
            if (!CanClaimMission(slot)) return null;
            Daily.Claimed[slot] = true;
            var reward = MissionAt(slot).Reward;
            GrantDaily(reward);
            return reward;
        }

        public int MissionsOffered
        {
            get
            {
                int n = 0;
                for (int i = 0; i < DailyState.MissionCount; i++)
                    if (MissionAt(i) != null) n++;
                return n;
            }
        }

        public int MissionsClaimed
        {
            get
            {
                int n = 0;
                for (int i = 0; i < DailyState.MissionCount; i++)
                    if (MissionAt(i) != null && Daily.Claimed[i]) n++;
                return n;
            }
        }

        public bool CanClaimMissionBonus => DailyUnlocked && !Daily.BonusClaimed && MissionsOffered > 0 && MissionsClaimed == MissionsOffered;

        /// <summary>The extra reward for finishing every mission of the day.</summary>
        public DailyReward ClaimMissionBonus()
        {
            if (!CanClaimMissionBonus) return null;
            Daily.BonusClaimed = true;
            GrantDaily(C.MissionBonus);
            return C.MissionBonus;
        }

        // ---------------------------------------------------------------- login streak

        /// <summary>Only a later day than the last claim counts, so turning the clock back gives nothing.</summary>
        public bool LoginRewardReady => DailyUnlocked && Today >= 0 && Today > Daily.LoginDay;

        /// <summary>Streak day that today's login reward counts as (1-based).</summary>
        public int TodayStreakDay => Daily.LoginDay >= Today ? Math.Max(1, Daily.Streak) : Daily.Streak + 1;

        /// <summary>Login reward for a streak day (the seven-day cycle repeats).</summary>
        public DailyReward LoginRewardFor(int streakDay) => C.LoginRewards[(Math.Max(1, streakDay) - 1) % C.LoginRewards.Length];

        public DailyReward ClaimLoginReward()
        {
            if (!LoginRewardReady) return null;
            var d = Daily;
            d.Streak++;
            d.BestStreak = Math.Max(d.BestStreak, d.Streak);
            d.LoginDay = Today;
            var reward = LoginRewardFor(d.Streak);
            GrantDaily(reward);
            return reward;
        }

        public bool CanRestoreStreak => DailyUnlocked && Daily.LostStreak >= C.B.StreakRestoreMin && Daily.LostDay == Today;

        /// <summary>Brings a broken streak back (the reward of the streak ad).</summary>
        public bool RestoreStreak()
        {
            if (!CanRestoreStreak) return false;
            var d = Daily;
            d.Streak += d.LostStreak;
            d.LostStreak = 0;
            d.BestStreak = Math.Max(d.BestStreak, d.Streak);
            return true;
        }

        /// <summary>Things waiting to be collected (for badges).</summary>
        public int DailyClaimable
        {
            get
            {
                if (!DailyUnlocked) return 0;
                int n = LoginRewardReady ? 1 : 0;
                for (int i = 0; i < DailyState.MissionCount; i++)
                    if (CanClaimMission(i)) n++;
                if (CanClaimMissionBonus) n++;
                return n;
            }
        }

        /// <summary>Storm Cores in daily rewards grow with Engine Level so they stay worth collecting.</summary>
        public long DailyCores(long baseCores) =>
            baseCores <= 0 ? 0 : (long)Math.Round(baseCores * (1 + C.B.DailyCoreGrowth * (Level - 1)));

        void GrantDaily(DailyReward r)
        {
            if (r == null) return;
            Gems += Math.Max(0, r.Gems);
            Cores += DailyCores(r.Cores);
            if (r.SurgeSeconds > 0) SurgeSeconds = Math.Min(C.B.SurgeMaxSeconds, SurgeSeconds + r.SurgeSeconds);
        }
    }
}
