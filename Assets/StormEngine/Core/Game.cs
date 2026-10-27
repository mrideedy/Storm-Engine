using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    public sealed class GameStats
    {
        public long Storms;
        public long Strikes;
        public long BoostsUsed;
        public long MegaStorms;
        public double BestChain = 1;
        public double PlaySeconds;
        public double BestPower;
    }

    public sealed class GameRecords
    {
        public double BestScore;
        public int BestClass = -1;
        public int WeeklyWeek = -1;
        public double WeeklyBest;
        public int WeeklyClass = -1;
        /// <summary>Best score per class; index 0 is Squall, p + 1 is phenomenon p.</summary>
        public double[] ClassBest;
    }

    public sealed class OfflineReport
    {
        public double Seconds;
        public double CountedSeconds;
        public double Energy;
        public long Storms;
        public long Strikes;
        public long Cores;
        public double Efficiency;
        public bool Capped;
        /// <summary>True when the Offline Generator has not been built yet (nothing was earned).</summary>
        public bool Locked;
    }

    /// <summary>A decoration placed on the island (world units; air items use X/Z as their orbit anchor).</summary>
    public sealed class PlacedDecor
    {
        public string Id;
        public float X;
        public float Z;
        public float Rot;
    }

    /// <summary>Weekly tournament progress. Attempts reset every UTC day, bests every Monday.</summary>
    public sealed class TournamentState
    {
        public int Week = -1;
        public double Best;
        public int BestClass = -1;
        public long Day = -1;
        public int AttemptsUsed;
        public int BonusAttempts;
        public int Runs;
        public double AllTimeBest;
        public int AllTimeClass = -1;
        /// <summary>Best tournament score per class; index 0 is Squall, p + 1 is phenomenon p.</summary>
        public double[] ClassBest;
        /// <summary>Last week whose final-rank reward was claimed.</summary>
        public int RewardedWeek = -1;
        /// <summary>Weeks in which the player entered at least one run (for end-of-week rewards).</summary>
        public int PlayedWeek = -1;
    }

    /// <summary>Rewarded-ad usage for the current UTC day.</summary>
    public sealed class AdState
    {
        public long Day = -1;
        public readonly Dictionary<string, int> Used = new Dictionary<string, int>();
        public long Total;
    }

    /// <summary>Which records a finished storm improved.</summary>
    public struct RecordFlags
    {
        public bool AllTime;
        public bool Weekly;
        public bool Class;
        /// <summary>First storm ever recorded for this storm class.</summary>
        public bool FirstOfClass;
        public bool Any => AllTime || Weekly || Class;
    }

    /// <summary>
    /// The complete game model. Pure C#: the Unity layer only calls <see cref="Tick"/>, the action
    /// methods, and renders the public state.
    /// </summary>
    public sealed partial class Game
    {
        public readonly Content C;
        public Build Build;

        public double Energy;
        public double RunEnergy;
        public double LifetimeEnergy;
        public long Cores;
        public long Research;
        public long ResearchLifetime;
        public int Prestiges;
        public bool[] MachineUnlocked;
        public bool[] Discovered;
        public readonly HashSet<string> GoalsDone = new HashSet<string>();
        public double StormCharge;
        public bool AutoStorm = true;
        public double[] BoostActive;
        public double[] BoostCooldown;
        public GameStats Stats = new GameStats();
        public GameRecords Records = new GameRecords();
        public long NowUnix;
        public int Week = -1;

        /// <summary>Engine Level. Rises with XP from building, storms, goals and discoveries; unlocks content.</summary>
        public int Level = 1;
        public double Xp;
        public readonly HashSet<string> OwnedCosmetics = new HashSet<string>();
        public readonly string[] EquippedCosmetics = new string[5];

        /// <summary>Premium currency (bought with real money, also earned slowly by playing).</summary>
        public long Gems;
        /// <summary>Copies owned per decoration id.</summary>
        public readonly Dictionary<string, int> DecorOwned = new Dictionary<string, int>();
        public readonly List<PlacedDecor> Placed = new List<PlacedDecor>();
        public readonly HashSet<string> Expansions = new HashSet<string>();
        /// <summary>Rewarded ads watched toward each ads-only bundle.</summary>
        public readonly Dictionary<string, int> BundleAds = new Dictionary<string, int>();
        /// <summary>One-time store products already bought.</summary>
        public readonly HashSet<string> OwnedProducts = new HashSet<string>();
        public TournamentState Tournament = new TournamentState();
        public AdState Ads = new AdState();
        /// <summary>Remaining Energy Surge time (x2 income).</summary>
        public double SurgeSeconds;
        /// <summary>Storm Cores from the most recent storm (for the Double Cores ad); 0 once doubled.</summary>
        public long LastStormCores;

        public StormSim Storm { get; private set; }
        public ModifierSet Mods { get; private set; }
        public readonly Snapshot Snap;

        readonly EvalContext _ctx;
        readonly List<int> _pendingBoosts = new List<int>();
        double _stormAccumulator;
        bool _dirty = true;

        public event Action<StormSim> StormStarted;
        public event Action<StormResult, RecordFlags, StormSubmission> StormEnded;
        public event Action<int> LightningStruck;
        public event Action<int> PhenomenonDiscovered;
        public event Action<GoalDef> GoalCompleted;
        public event Action<int> MachineUnlockedEvent;
        public event Action<int> BoostActivated;
        public event Action WeekChanged;
        /// <summary>Raised once per level gained, with the new level.</summary>
        public event Action<int> LeveledUp;
        /// <summary>Raised when an ads-only bundle completes, with the bundle index.</summary>
        public event Action<int> BundleCompleted;

        public Game(Content c)
        {
            C = c;
            Build = new Build(c);
            MachineUnlocked = new bool[c.Machines.Length];
            MachineUnlocked[0] = true;
            Discovered = new bool[c.Phenomena.Length];
            BoostActive = new double[c.Boosts.Length];
            BoostCooldown = new double[c.Boosts.Length];
            Records.ClassBest = new double[c.Phenomena.Length + 1];
            Tournament.ClassBest = new double[c.Phenomena.Length + 1];
            _ctx = new EvalContext(c.Boosts.Length);
            Snap = new Snapshot(c);
            foreach (var cos in c.Cosmetics)
            {
                if (!cos.Default) continue;
                OwnedCosmetics.Add(cos.Id);
                EquippedCosmetics[(int)cos.Slot] = cos.Id;
            }
            SyncStyle();
            UpdateMachineUnlocks(false);
        }

        // ---------------------------------------------------------------- derived values

        public bool IsStorming => Storm != null;
        public double StormPower => Storm != null ? Storm.Snap.StormPower : Snap.StormPower;
        public Snapshot View => Storm != null ? Storm.Snap : Snap;
        public ModifierSet ViewMods => Storm != null ? Storm.Mods : Mods;
        public EvalContext ViewContext => Storm != null ? Storm.Context : _ctx;
        public double ChargeSeconds => C.B.BaseChargeSeconds / Math.Max(0.01, Snap.Stat[(int)StatId.StormChargeSpeed]);
        public bool CanUnleash => Storm == null && StormCharge >= 1;
        /// <summary>True while the running storm is a tournament attempt.</summary>
        public bool IsTournamentRun { get; private set; }
        /// <summary>This week's condition, whether or not weekly conditions are unlocked yet.</summary>
        public int WeeklyCondition => Week >= 0 ? WeekClock.ConditionIndex(Week, C.Weekly.Length) : -1;
        /// <summary>Energy income multiplier: decorations and style (EnergyGain) plus an active Energy Surge.</summary>
        public double IncomeMultiplier => Math.Max(0, Snap.Stat[(int)StatId.EnergyGain]) * (SurgeSeconds > 0 ? C.B.SurgeMultiplier : 1);
        /// <summary>Energy earned per second right now.</summary>
        public double EnergyPerSecond => StormPower * IncomeMultiplier;
        public bool AutoStormActive => Mods != null && Mods.Automation && AutoStorm;
        public int ModuleSlots => Mods != null ? Mods.ModuleSlots : 2;
        public GoalDef CurrentGoal
        {
            get
            {
                foreach (var g in C.Goals)
                    if (!GoalsDone.Contains(g.Id)) return g;
                return null;
            }
        }

        public void MarkDirty() => _dirty = true;

        public void Refresh()
        {
            if (_dirty)
            {
                Mods = ModifierSet.Create(C, Build);
                _dirty = false;
            }
            for (int i = 0; i < BoostActive.Length; i++) _ctx.BoostActive[i] = BoostActive[i] > 0;
            _ctx.StormActive = false;
            _ctx.Instability = 0;
            Formula.Evaluate(C, Mods, _ctx, Snap);
        }

        // ---------------------------------------------------------------- time

        /// <summary>Sets wall-clock time. Rotates the weekly condition when the UTC week changes.</summary>
        public void SetTime(long unix)
        {
            NowUnix = unix;
            long day = DayOf(unix);
            // Days only move forward: setting the phone's date back must never hand out a day again.
            if (day >= 0 && day > Daily.Day) RollDaily(day);
            int week = WeekClock.WeekOf(unix);
            if (week == Week) return;
            Week = week;
            // A running storm keeps its own frozen copy of the build, so this is always safe.
            UpdateWeeklyBuild();
            if (Records.WeeklyWeek != week)
            {
                Records.WeeklyWeek = week;
                Records.WeeklyBest = 0;
                Records.WeeklyClass = -1;
            }
            WeekChanged?.Invoke();
        }

        /// <summary>Weekly conditions only shape the normal game once they are unlocked (Engine Level 8).</summary>
        void UpdateWeeklyBuild()
        {
            int weekly = IsFeatureUnlocked("weekly") ? WeeklyCondition : -1;
            if (Build.Weekly == weekly) return;
            Build.Weekly = weekly;
            _dirty = true;
        }

        public static long DayOf(long unix) => unix >= 0 ? unix / 86400 : -1;

        public void Tick(double dt)
        {
            if (dt <= 0) return;
            if (Mods == null || _dirty) Refresh();
            Stats.PlaySeconds += dt;
            if (SurgeSeconds > 0) SurgeSeconds = Math.Max(0, SurgeSeconds - dt);

            if (Storm != null) StepStorm(dt);
            else AdvanceBoosts(dt);

            Refresh();

            double power = StormPower;
            AddEnergy(power * dt * IncomeMultiplier);
            if (power > Stats.BestPower) Stats.BestPower = power;

            if (Storm == null && StormCharge < 1)
                StormCharge = Math.Min(1, StormCharge + dt / ChargeSeconds);

            if (Storm == null && StormCharge >= 1 && Mods.Automation && AutoStorm)
                Unleash();

            CheckDiscoveries(Snap.PhenoActive);
            CheckUnlocks();
            CheckGoals();
        }

        void AdvanceBoosts(double dt)
        {
            for (int i = 0; i < BoostActive.Length; i++)
            {
                if (BoostActive[i] > 0)
                {
                    BoostActive[i] -= dt;
                    if (BoostActive[i] <= 0)
                    {
                        double overflow = -BoostActive[i];
                        BoostActive[i] = 0;
                        BoostCooldown[i] = Math.Max(0, Mods.BoostCooldown[i] - overflow);
                    }
                }
                else if (BoostCooldown[i] > 0)
                {
                    BoostCooldown[i] = Math.Max(0, BoostCooldown[i] - dt);
                }
            }
        }

        void AddEnergy(double amount)
        {
            if (amount <= 0 || double.IsNaN(amount)) return;
            Energy += amount;
            RunEnergy += amount;
            LifetimeEnergy += amount;
        }

        // ---------------------------------------------------------------- boosts

        public bool IsBoostUnlocked(int b)
        {
            if (Mods == null) Refresh();
            return Mods.BoostUnlocked[b];
        }

        public bool IsBoostReady(int b)
        {
            if (!IsBoostUnlocked(b)) return false;
            if (Storm != null) return Storm.CanActivate(b) && !_pendingBoosts.Contains(b);
            return BoostActive[b] <= 0 && BoostCooldown[b] <= 0;
        }

        public bool ActivateBoost(int b)
        {
            if (!IsBoostReady(b)) return false;
            if (Storm != null) _pendingBoosts.Add(b);
            else BoostActive[b] = Mods.BoostDuration[b];
            Stats.BoostsUsed++;
            CountMission(MissionKind.Boosts, 1);
            if (Storm != null) CountMission(MissionKind.StormBoosts, 1);
            BoostActivated?.Invoke(b);
            return true;
        }

        // ---------------------------------------------------------------- storms

        StormSetup CreateSetup()
        {
            var setup = new StormSetup
            {
                Build = Build.Clone(),
                BoostActiveTicks = new int[BoostActive.Length],
                BoostCooldownTicks = new int[BoostActive.Length]
            };
            for (int i = 0; i < BoostActive.Length; i++)
            {
                setup.BoostActiveTicks[i] = BoostActive[i] > 0 ? Math.Max(1, (int)Math.Ceiling(BoostActive[i] * StormSim.TicksPerSecond - 1e-9)) : 0;
                setup.BoostCooldownTicks[i] = BoostActive[i] > 0 || BoostCooldown[i] <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(BoostCooldown[i] * StormSim.TicksPerSecond - 1e-9));
            }
            return setup;
        }

        /// <summary>
        /// Dry-runs the next storm with the current build and boost timers. With
        /// <paramref name="fireReadyBoosts"/> every ready boost is fired on the first tick.
        /// </summary>
        public StormResult Forecast(bool fireReadyBoosts)
        {
            if (_dirty || Mods == null) Refresh();
            var setup = CreateSetup();
            var events = new List<StormEvent>();
            if (fireReadyBoosts)
                for (int b = 0; b < BoostActive.Length; b++)
                    if (Mods.BoostUnlocked[b] && setup.BoostActiveTicks[b] == 0 && setup.BoostCooldownTicks[b] == 0)
                        events.Add(new StormEvent(0, b));
            return StormSim.Replay(C, setup, events, out _);
        }

        public bool Unleash()
        {
            if (!CanUnleash) return false;
            if (_dirty) Refresh();
            var setup = CreateSetup();
            Storm = new StormSim(C, setup);
            IsTournamentRun = false;
            StormCharge = 0;
            _stormAccumulator = 0;
            _pendingBoosts.Clear();
            StormStarted?.Invoke(Storm);
            return true;
        }

        // ---------------------------------------------------------------- tournament

        public bool TournamentUnlocked => IsFeatureUnlocked("tournament");

        void RollTournament()
        {
            long day = DayOf(NowUnix);
            if (day > Tournament.Day)
            {
                Tournament.Day = day;
                Tournament.AttemptsUsed = 0;
                Tournament.BonusAttempts = 0;
            }
            if (Tournament.Week != Week)
            {
                Tournament.Week = Week;
                Tournament.Best = 0;
                Tournament.BestClass = -1;
            }
        }

        public int TournamentAttemptsLeft
        {
            get
            {
                RollTournament();
                return Math.Max(0, C.B.TournamentAttemptsPerDay + Tournament.BonusAttempts - Tournament.AttemptsUsed);
            }
        }

        public bool CanEnterTournament => TournamentUnlocked && Storm == null && TournamentAttemptsLeft > 0;

        /// <summary>
        /// Tournament rules: your machines, modules and research as they are; this week's condition
        /// always applies; style bonuses are capped; decorations, surges and running boosts are ignored.
        /// </summary>
        public StormSetup CreateTournamentSetup()
        {
            var build = Build.Clone();
            build.Weekly = WeeklyCondition;
            build.StyleCap = C.B.TournamentStyleCap;
            build.DecorBonus = 0;
            return new StormSetup
            {
                Build = build,
                BoostActiveTicks = new int[BoostActive.Length],
                BoostCooldownTicks = new int[BoostActive.Length]
            };
        }

        /// <summary>Tournament forecast with every unlocked boost fired on the first tick.</summary>
        public StormResult TournamentForecast()
        {
            if (_dirty || Mods == null) Refresh();
            var setup = CreateTournamentSetup();
            var ms = ModifierSet.Create(C, setup.Build);
            var events = new List<StormEvent>();
            for (int b = 0; b < BoostActive.Length; b++)
                if (ms.BoostUnlocked[b]) events.Add(new StormEvent(0, b));
            return StormSim.Replay(C, setup, events, out _);
        }

        public bool StartTournament()
        {
            if (!CanEnterTournament) return false;
            if (_dirty) Refresh();
            RollTournament();
            Tournament.AttemptsUsed++;
            Tournament.Runs++;
            Tournament.PlayedWeek = Week;
            CountMission(MissionKind.Tournament, 1);
            // Boosts start fresh in a tournament run.
            Array.Clear(BoostActive, 0, BoostActive.Length);
            Array.Clear(BoostCooldown, 0, BoostCooldown.Length);
            Storm = new StormSim(C, CreateTournamentSetup());
            IsTournamentRun = true;
            _stormAccumulator = 0;
            _pendingBoosts.Clear();
            StormStarted?.Invoke(Storm);
            CheckGoals();
            return true;
        }

        /// <summary>Gems for finishing a tournament week at <paramref name="rank"/> (1-based, 0 = unranked but played).</summary>
        public long TournamentRewardFor(int rank)
        {
            var limits = C.B.TournamentRankLimits;
            var gems = C.B.TournamentRankGems;
            if (rank <= 0) return gems[gems.Length - 1];
            for (int i = 0; i < limits.Length; i++)
                if (rank <= limits[i]) return gems[i];
            return gems[gems.Length - 1];
        }

        /// <summary>Claims last week's tournament reward once. Returns the Gems granted.</summary>
        public long ClaimTournamentReward(int week, int rank)
        {
            if (week <= Tournament.RewardedWeek || Tournament.PlayedWeek < week) return 0;
            Tournament.RewardedWeek = week;
            long gems = TournamentRewardFor(rank);
            Gems += gems;
            return gems;
        }

        /// <summary>A finished week the player took part in and has not been rewarded for yet, or -1.</summary>
        public int UnclaimedTournamentWeek =>
            Tournament.PlayedWeek >= 0 && Tournament.PlayedWeek < Week && Tournament.PlayedWeek > Tournament.RewardedWeek ? Tournament.PlayedWeek : -1;

        void StepStorm(double dt)
        {
            _stormAccumulator += dt;
            while (_stormAccumulator >= StormSim.Dt - 1e-9 && !Storm.Finished)
            {
                _stormAccumulator -= StormSim.Dt;
                Storm.Step(_pendingBoosts);
                _pendingBoosts.Clear();
                AddEnergy(Storm.LastStepScore * IncomeMultiplier);
                if (Storm.LastStepStrikes > 0) LightningStruck?.Invoke(Storm.LastStepStrikes);
            }
            for (int i = 0; i < BoostActive.Length; i++)
            {
                BoostActive[i] = Storm.ActiveTicks[i] * StormSim.Dt;
                BoostCooldown[i] = Storm.CooldownTicks[i] * StormSim.Dt;
            }
            if (Storm.Finished) EndStorm();
        }

        void EndStorm()
        {
            var sim = Storm;
            var r = sim.Result;
            bool tournament = IsTournamentRun;
            Storm = null;
            IsTournamentRun = false;
            _stormAccumulator = 0;
            _pendingBoosts.Clear();
            r.Tournament = tournament;

            r.Cores = CoresFor(r.Score);
            Cores += r.Cores;
            LastStormCores = r.Cores;
            Stats.Storms++;
            AddXp(C.B.XpPerStorm + (r.ClassIndex >= 0 ? C.B.XpPerPhenomenonStorm : 0) + (tournament ? C.B.XpPerTournamentRun : 0));
            Stats.Strikes += r.Strikes;
            if (r.MegaTicks > 0) Stats.MegaStorms++;
            if (r.PeakChain > Stats.BestChain) Stats.BestChain = r.PeakChain;

            var flags = new RecordFlags();
            StormSubmission submission = null;
            if (tournament)
            {
                // Only tournament storms reach the leaderboards.
                RollTournament();
                var t = Tournament;
                if (r.Score > t.Best)
                {
                    t.Best = r.Score;
                    t.BestClass = r.ClassIndex;
                    flags.Weekly = true;
                }
                if (r.Score > t.AllTimeBest)
                {
                    t.AllTimeBest = r.Score;
                    t.AllTimeClass = r.ClassIndex;
                    flags.AllTime = true;
                }
                if (r.Score > t.ClassBest[r.ClassIndex + 1])
                {
                    flags.FirstOfClass = t.ClassBest[r.ClassIndex + 1] <= 0;
                    t.ClassBest[r.ClassIndex + 1] = r.Score;
                    flags.Class = true;
                }
                if (flags.Any) submission = StormSubmission.From(C, sim, Week, NowUnix);
            }
            else
            {
                if (r.Score > Records.BestScore)
                {
                    Records.BestScore = r.Score;
                    Records.BestClass = r.ClassIndex;
                    flags.AllTime = true;
                }
                if (Records.WeeklyWeek == Week && r.Score > Records.WeeklyBest)
                {
                    Records.WeeklyBest = r.Score;
                    Records.WeeklyClass = r.ClassIndex;
                    flags.Weekly = true;
                }
                if (r.Score > Records.ClassBest[r.ClassIndex + 1])
                {
                    flags.FirstOfClass = Records.ClassBest[r.ClassIndex + 1] <= 0;
                    Records.ClassBest[r.ClassIndex + 1] = r.Score;
                    flags.Class = true;
                }
            }

            CheckDiscoveries(r.PhenoSeen);
            CountStorm(r);
            StormEnded?.Invoke(r, flags, submission);
        }

        public long CoresFor(double score)
        {
            double gain = Snap.Stat[(int)StatId.CoreGain];
            if (gain <= 0) gain = 1;
            double cores = gain * (C.B.CoresBase + C.B.CoresPerLog * Math.Log10(Math.Max(1, score)));
            return (long)Math.Floor(cores);
        }

        // ---------------------------------------------------------------- machines

        public bool IsMachineVisible(int m) => MachineUnlocked[m];

        public int MaxAffordable(int m)
        {
            var def = C.Machines[m];
            int level = Build.Machines[m];
            int cap = def.MaxLevel > 0 ? def.MaxLevel - level : 100000;
            double budget = Energy;
            int n = 0;
            while (n < cap)
            {
                double cost = C.MachineCost(m, level + n);
                if (cost > budget) break;
                budget -= cost;
                n++;
            }
            return n;
        }

        public bool IsMaxed(int m) => C.Machines[m].MaxLevel > 0 && Build.Machines[m] >= C.Machines[m].MaxLevel;

        public double CostOf(int m, int count) => C.MachineCost(m, Build.Machines[m], Math.Max(1, count));

        public bool BuyMachine(int m, int count)
        {
            if (Storm != null || !MachineUnlocked[m] || count <= 0) return false;
            var def = C.Machines[m];
            if (def.MaxLevel > 0) count = Math.Min(count, def.MaxLevel - Build.Machines[m]);
            if (count <= 0) return false;
            double cost = CostOf(m, count);
            if (cost > Energy) return false;
            Energy -= cost;
            Build.Machines[m] += count;
            _dirty = true;
            Refresh();
            CountMission(MissionKind.MachineLevels, count);
            AddXp(C.B.XpPerMachineLevel * count);
            CheckGoals();
            return true;
        }

        void CheckUnlocks() => UpdateMachineUnlocks(true);

        void UpdateMachineUnlocks(bool notify)
        {
            for (int i = 0; i < MachineUnlocked.Length; i++)
            {
                if (MachineUnlocked[i] || Level < C.Machines[i].UnlockLevel) continue;
                MachineUnlocked[i] = true;
                if (notify) MachineUnlockedEvent?.Invoke(i);
            }
        }

        // ---------------------------------------------------------------- engine level

        public double XpToNext => C.XpToNext(Level);
        public double LevelProgress => Level >= C.B.MaxEngineLevel ? 1 : Math.Min(1, Xp / Math.Max(1, XpToNext));

        public void AddXp(double amount)
        {
            if (amount <= 0 || double.IsNaN(amount)) return;
            Xp += amount;
            while (Level < C.B.MaxEngineLevel && Xp >= C.XpToNext(Level))
            {
                Xp -= C.XpToNext(Level);
                Level++;
                Gems += C.B.GemsPerLevel;
                UpdateMachineUnlocks(true);
                UpdateWeeklyBuild();
                LeveledUp?.Invoke(Level);
            }
            if (Level >= C.B.MaxEngineLevel) Xp = Math.Min(Xp, C.XpToNext(Level));
        }

        /// <summary>Sets level and XP directly (loading and migrating saves).</summary>
        public void SetProgress(int level, double xp)
        {
            Level = Math.Max(1, Math.Min(C.B.MaxEngineLevel, level));
            Xp = Math.Max(0, xp);
            UpdateMachineUnlocks(false);
            UpdateWeeklyBuild();
        }

        public bool IsFeatureUnlocked(string id)
        {
            var f = C.Feature(id);
            return f == null || Level >= f.Level;
        }

        public int FeatureLevel(string id) => C.Feature(id)?.Level ?? 1;

        /// <summary>Everything that opens up at exactly this Engine Level (for the level-up card).</summary>
        public List<string> UnlocksAtLevel(int level)
        {
            var list = new List<string>();
            foreach (var f in C.Features) if (f.Level == level) list.Add(f.Name);
            foreach (var m in C.Machines) if (m.UnlockLevel == level) list.Add(m.Name);
            foreach (var m in C.Modules) if (m.UnlockLevel == level) list.Add("Module: " + m.Name);
            foreach (var r in C.Research) if (r.UnlockLevel == level && level > FeatureLevel("research")) list.Add("Research: " + r.Name);
            foreach (var cos in C.Cosmetics)
                if (!cos.Default && cos.UnlockLevel == level && level > 1 && cos.RequiresDiscovery == null && cos.RequiresPrestiges == 0 && cos.Bundle == null)
                    list.Add("Style: " + cos.Name);
            foreach (var d in C.Decorations)
                if (d.UnlockLevel == level && level > 1 && d.Bundle == null && !d.Limited) list.Add("Decoration: " + d.Name);
            foreach (var b in C.Bundles)
                if (b.UnlockLevel == level && level > 1) list.Add("Bundle: " + b.Name);
            foreach (var e in C.Expansions)
                if (e.UnlockLevel == level) list.Add(e.Name);
            return list;
        }

        /// <summary>Next level that unlocks anything, or -1.</summary>
        public int NextUnlockLevel()
        {
            for (int l = Level + 1; l <= C.B.MaxEngineLevel; l++)
                if (UnlocksAtLevel(l).Count > 0) return l;
            return -1;
        }

        // ---------------------------------------------------------------- permanent upgrades

        public double UpgradeCost(int u) => C.UpgradeCost(u, Build.Upgrades[u]);
        public long UpgradeCoreCost(int u) => C.UpgradeCoreCost(u, Build.Upgrades[u]);
        public bool IsUpgradeMaxed(int u) => Build.Upgrades[u] >= C.Upgrades[u].MaxLevel;
        public bool IsUpgradeAvailable(int u) => IsFeatureUnlocked(C.Upgrades[u].Feature);

        public bool CanBuyUpgrade(int u) =>
            Storm == null && IsUpgradeAvailable(u) && !IsUpgradeMaxed(u) && Energy >= UpgradeCost(u) && Cores >= UpgradeCoreCost(u);

        public bool BuyUpgrade(int u)
        {
            if (!CanBuyUpgrade(u)) return false;
            Energy -= UpgradeCost(u);
            Cores -= UpgradeCoreCost(u);
            Build.Upgrades[u]++;
            _dirty = true;
            Refresh();
            AddXp(C.B.XpPerUpgrade);
            CheckGoals();
            return true;
        }

        public int UpgradeLevel(string id)
        {
            int i = C.UpgradeIndex(id);
            return i >= 0 ? Build.Upgrades[i] : 0;
        }

        public bool OfflineUnlocked => Snap.Stat[(int)StatId.OfflineCapHours] > 0;

        // ---------------------------------------------------------------- cosmetics

        public CosmeticDef Equipped(CosmeticSlot slot)
        {
            int i = C.CosmeticIndex(EquippedCosmetics[(int)slot]);
            if (i >= 0) return C.Cosmetics[i];
            foreach (var c in C.Cosmetics) if (c.Slot == slot && c.Default) return c;
            return null;
        }

        public bool CosmeticRequirementsMet(int i)
        {
            var def = C.Cosmetics[i];
            if (Level < def.UnlockLevel || Prestiges < def.RequiresPrestiges) return false;
            if (def.RequiresDiscovery != null)
            {
                int p = C.PhenoIndex(def.RequiresDiscovery);
                if (p >= 0 && !Discovered[p]) return false;
            }
            return true;
        }

        public bool IsCosmeticOwned(int i) => OwnedCosmetics.Contains(C.Cosmetics[i].Id);

        /// <summary>Items that can be bought (or claimed for free) on their own; bundle items cannot.</summary>
        public bool IsCosmeticForSale(int i)
        {
            var def = C.Cosmetics[i];
            return !def.Default && def.Bundle == null;
        }

        public bool CanUnlockCosmetic(int i)
        {
            var def = C.Cosmetics[i];
            return !IsCosmeticOwned(i) && IsCosmeticForSale(i) && CosmeticRequirementsMet(i) && Cores >= def.CostCores && Gems >= def.CostGems;
        }

        /// <summary>Unlocks (paying Gems or Storm Cores if needed) and equips a style item.</summary>
        public bool UnlockCosmetic(int i)
        {
            if (!CanUnlockCosmetic(i)) return false;
            Cores -= C.Cosmetics[i].CostCores;
            Gems -= C.Cosmetics[i].CostGems;
            OwnedCosmetics.Add(C.Cosmetics[i].Id);
            EquipCosmetic(i);
            CheckGoals();
            return true;
        }

        public bool EquipCosmetic(int i)
        {
            if (!IsCosmeticOwned(i)) return false;
            EquippedCosmetics[(int)C.Cosmetics[i].Slot] = C.Cosmetics[i].Id;
            SyncStyle();
            return true;
        }

        /// <summary>Copies the equipped style items into the build so their bonuses apply.</summary>
        public void SyncStyle()
        {
            for (int slot = 0; slot < Build.Style.Length; slot++)
            {
                int index = C.CosmeticIndex(EquippedCosmetics[slot]);
                if (index >= 0 && !OwnedCosmetics.Contains(C.Cosmetics[index].Id)) index = -1;
                if (Build.Style[slot] == index) continue;
                Build.Style[slot] = index;
                _dirty = true;
            }
            if (_dirty && Storm == null) Refresh();
        }

        // ---------------------------------------------------------------- gems & store

        public void AddGems(long amount)
        {
            if (amount > 0) Gems += amount;
        }

        public bool SpendGems(long amount)
        {
            if (amount < 0 || Gems < amount) return false;
            Gems -= amount;
            return true;
        }

        public bool IsProductOwned(string id) => OwnedProducts.Contains(id);

        /// <summary>Grants a store product after a successful purchase.</summary>
        public bool GrantProduct(int p)
        {
            var def = C.Products[p];
            if (def.OneTime && OwnedProducts.Contains(def.Id)) return false;
            Gems += def.Gems;
            Cores += def.Cores;
            foreach (var id in def.Cosmetics)
                if (C.CosmeticIndex(id) >= 0) OwnedCosmetics.Add(id);
            if (def.OneTime) OwnedProducts.Add(def.Id);
            CheckGoals();
            return true;
        }

        // ---------------------------------------------------------------- bundles (ads only)

        public int BundleProgress(string id) => BundleAds.TryGetValue(id, out int n) ? n : 0;

        public bool IsBundleComplete(int b)
        {
            foreach (var id in C.Bundles[b].Cosmetics)
                if (!OwnedCosmetics.Contains(id)) return false;
            return true;
        }

        public bool IsBundleAvailable(int b) => Level >= C.Bundles[b].UnlockLevel && !IsBundleComplete(b);

        void AddBundleAd(int b)
        {
            var def = C.Bundles[b];
            int n = BundleProgress(def.Id) + 1;
            BundleAds[def.Id] = n;
            if (n < def.AdsRequired) return;
            foreach (var id in def.Cosmetics) OwnedCosmetics.Add(id);
            foreach (var id in def.Decorations) AddDecorCopy(id);
            SyncDecor();
            BundleCompleted?.Invoke(b);
            CheckGoals();
        }

        // ---------------------------------------------------------------- rewarded ads

        void RollAds()
        {
            long day = DayOf(NowUnix);
            if (day <= Ads.Day) return;
            Ads.Day = day;
            Ads.Used.Clear();
        }

        public int AdUses(int placement)
        {
            RollAds();
            return Ads.Used.TryGetValue(C.AdPlacements[placement].Id, out int n) ? n : 0;
        }

        public int AdUsesLeft(int placement)
        {
            var def = C.AdPlacements[placement];
            return def.DailyLimit <= 0 ? int.MaxValue : Math.Max(0, def.DailyLimit - AdUses(placement));
        }

        public bool CanWatchAd(int placement, string target = null)
        {
            var def = C.AdPlacements[placement];
            if (Level < def.UnlockLevel || AdUsesLeft(placement) <= 0) return false;
            switch (def.Reward)
            {
                case AdReward.EnergySurge: return SurgeSeconds < C.B.SurgeMaxSeconds - 1;
                case AdReward.InstantCharge: return Storm == null && StormCharge < 1;
                case AdReward.TournamentAttempt: return TournamentUnlocked && Tournament.BonusAttempts < C.B.TournamentAdAttemptsPerDay;
                case AdReward.BundleProgress:
                    int b = C.BundleIndex(target);
                    return b >= 0 && IsBundleAvailable(b);
                case AdReward.DoubleCores: return LastStormCores > 0;
                case AdReward.StreakRestore: return CanRestoreStreak;
                default: return true;
            }
        }

        /// <summary>Applies the reward of a watched ad. <paramref name="target"/> names the bundle for bundle ads.</summary>
        public bool GrantAd(int placement, string target = null)
        {
            if (!CanWatchAd(placement, target)) return false;
            var def = C.AdPlacements[placement];
            RollAds();
            Ads.Used[def.Id] = AdUses(placement) + 1;
            Ads.Total++;
            switch (def.Reward)
            {
                case AdReward.Gems: Gems += (long)def.Amount; break;
                case AdReward.EnergySurge: SurgeSeconds = Math.Min(C.B.SurgeMaxSeconds, SurgeSeconds + def.Amount); break;
                case AdReward.InstantCharge: StormCharge = 1; break;
                case AdReward.TournamentAttempt:
                    RollTournament();
                    Tournament.BonusAttempts++;
                    break;
                case AdReward.BundleProgress: AddBundleAd(C.BundleIndex(target)); break;
                case AdReward.DoubleCores:
                    Cores += LastStormCores;
                    LastStormCores = 0;
                    break;
                case AdReward.StreakRestore: RestoreStreak(); break;
            }
            return true;
        }

        // ---------------------------------------------------------------- decorations & expansions

        public int DecorCopies(string id) => DecorOwned.TryGetValue(id, out int n) ? n : 0;

        public int PlacedCopies(string id)
        {
            int n = 0;
            foreach (var p in Placed) if (p.Id == id) n++;
            return n;
        }

        public int UniqueDecorations
        {
            get
            {
                int n = 0;
                foreach (var kv in DecorOwned) if (kv.Value > 0) n++;
                return n;
            }
        }

        public int DecorCapacity
        {
            get
            {
                int capacity = C.B.BaseDecorCapacity;
                foreach (var e in C.Expansions) if (Expansions.Contains(e.Id)) capacity += e.Capacity;
                return capacity;
            }
        }

        public float IslandRadius
        {
            get
            {
                float r = C.B.BaseIslandRadius;
                foreach (var e in C.Expansions) if (Expansions.Contains(e.Id)) r = Math.Max(r, e.Radius);
                return r;
            }
        }

        /// <summary>Limited decorations can only be bought during their featured week.</summary>
        public bool IsDecorationOnSale(int i)
        {
            var d = C.Decorations[i];
            if (d.Bundle != null || Level < d.UnlockLevel || !IsFeatureUnlocked("island")) return false;
            return !d.Limited || C.FeaturedDecoration(Week) == i;
        }

        public int PondCount => PlacedCopies("dec_pond");

        /// <summary>Water decorations need a pond; each pond holds three.</summary>
        public int WaterSlotsFree
        {
            get
            {
                int water = 0;
                foreach (var p in Placed)
                {
                    int i = C.DecorationIndex(p.Id);
                    if (i >= 0 && C.Decorations[i].Kind == DecorKind.Water) water++;
                }
                return PondCount * 3 - water;
            }
        }

        public bool CanBuyDecoration(int i)
        {
            var d = C.Decorations[i];
            if (!IsDecorationOnSale(i) || DecorCopies(d.Id) >= d.MaxCopies || Placed.Count >= DecorCapacity) return false;
            if (d.Kind == DecorKind.Water && WaterSlotsFree <= 0) return false;
            return Cores >= d.CostCores && Gems >= d.CostGems;
        }

        /// <summary>Buys one copy. The world layer places it (see <see cref="AddPlacement"/>).</summary>
        public bool BuyDecoration(int i)
        {
            if (!CanBuyDecoration(i)) return false;
            var d = C.Decorations[i];
            Cores -= d.CostCores;
            Gems -= d.CostGems;
            AddDecorCopy(d.Id);
            SyncDecor();
            CountMission(MissionKind.Decorations, 1);
            AddXp(C.B.XpPerDecoration);
            CheckGoals();
            return true;
        }

        void AddDecorCopy(string id) => DecorOwned[id] = DecorCopies(id) + 1;

        /// <summary>Copies owned but not currently on the island.</summary>
        public int StoredCopies(string id) => DecorCopies(id) - PlacedCopies(id);

        public bool AddPlacement(PlacedDecor p)
        {
            if (p == null || StoredCopies(p.Id) <= 0 || Placed.Count >= DecorCapacity) return false;
            Placed.Add(p);
            return true;
        }

        public bool RemovePlacement(int index)
        {
            if (index < 0 || index >= Placed.Count) return false;
            Placed.RemoveAt(index);
            return true;
        }

        /// <summary>Recomputes the collection bonus: each distinct decoration owned counts once.</summary>
        public void SyncDecor()
        {
            double bonus = 0;
            foreach (var kv in DecorOwned)
            {
                if (kv.Value <= 0) continue;
                int i = C.DecorationIndex(kv.Key);
                if (i >= 0) bonus += C.Decorations[i].Bonus;
            }
            if (Math.Abs(Build.DecorBonus - bonus) < 1e-12) return;
            Build.DecorBonus = bonus;
            _dirty = true;
            if (Storm == null) Refresh();
        }

        public bool IsExpansionOwned(int e) => Expansions.Contains(C.Expansions[e].Id);

        public bool CanBuyExpansion(int e)
        {
            var def = C.Expansions[e];
            if (IsExpansionOwned(e) || Level < def.UnlockLevel || !IsFeatureUnlocked("island")) return false;
            if (e > 0 && !IsExpansionOwned(e - 1)) return false;
            return Gems >= def.CostGems;
        }

        public bool BuyExpansion(int e)
        {
            if (!CanBuyExpansion(e)) return false;
            Gems -= C.Expansions[e].CostGems;
            Expansions.Add(C.Expansions[e].Id);
            AddXp(C.B.XpPerUpgrade);
            return true;
        }

        /// <summary>Relative Storm Power gain per Energy spent for one more level (used for hints).</summary>
        public double MachineValue(int m)
        {
            if (!MachineUnlocked[m] || IsMaxed(m)) return 0;
            double before = Snap.StormPower;
            if (before <= 0) return 0;
            var probe = Build.Clone();
            probe.Machines[m]++;
            var ms = ModifierSet.Create(C, probe);
            var snap = new Snapshot(C);
            Formula.Evaluate(C, ms, _ctx, snap);
            return (snap.StormPower / before - 1) / CostOf(m, 1);
        }

        // ---------------------------------------------------------------- modules

        public bool IsModuleUnlocked(int m) => Build.ModuleLevels[m] > 0;

        public bool ModuleRequirementsMet(int m)
        {
            var def = C.Modules[m];
            if (!IsFeatureUnlocked("modules") || Level < def.UnlockLevel) return false;
            if (Prestiges < def.RequiresPrestiges) return false;
            if (def.RequiresDiscovery != null)
            {
                int p = C.PhenoIndex(def.RequiresDiscovery);
                if (p >= 0 && !Discovered[p]) return false;
            }
            return true;
        }

        public bool UnlockModule(int m)
        {
            if (IsModuleUnlocked(m) || !ModuleRequirementsMet(m)) return false;
            long cost = C.Modules[m].UnlockCost;
            if (Cores < cost) return false;
            Cores -= cost;
            Build.ModuleLevels[m] = 1;
            if (Storm == null && Build.Equipped.Count < ModuleSlots) Build.Equipped.Add(m);
            _dirty = true;
            Refresh();
            CountMission(MissionKind.ModuleUpgrades, 1);
            AddXp(C.B.XpPerModuleUnlock);
            CheckGoals();
            return true;
        }

        public long ModuleUpgradeCost(int m) => C.ModuleUpgradeCost(m, Build.ModuleLevels[m]);

        public bool CanUpgradeModule(int m) =>
            IsModuleUnlocked(m) && Build.ModuleLevels[m] < C.Modules[m].MaxLevel && Cores >= ModuleUpgradeCost(m);

        public bool UpgradeModule(int m)
        {
            if (Storm != null || !CanUpgradeModule(m)) return false;
            Cores -= ModuleUpgradeCost(m);
            Build.ModuleLevels[m]++;
            _dirty = true;
            Refresh();
            CountMission(MissionKind.ModuleUpgrades, 1);
            AddXp(C.B.XpPerModuleUpgrade);
            return true;
        }

        public bool Equip(int m)
        {
            if (Storm != null || !IsModuleUnlocked(m) || Build.IsEquipped(m) || Build.Equipped.Count >= ModuleSlots) return false;
            Build.Equipped.Add(m);
            _dirty = true;
            Refresh();
            CheckGoals();
            return true;
        }

        public bool Unequip(int m)
        {
            if (Storm != null || !Build.Equipped.Remove(m)) return false;
            _dirty = true;
            Refresh();
            return true;
        }

        // ---------------------------------------------------------------- research & prestige

        public long ResearchCost(int r) => C.ResearchCost(r, Build.Research[r]);

        public bool IsResearchVisible(int r) =>
            IsFeatureUnlocked("research") && Level >= C.Research[r].UnlockLevel && Prestiges >= C.Research[r].RequiresPrestiges;

        public bool CanBuyResearch(int r) =>
            Storm == null && IsResearchVisible(r) && Build.Research[r] < C.Research[r].MaxLevel && Research >= ResearchCost(r);

        public bool BuyResearch(int r)
        {
            if (!CanBuyResearch(r)) return false;
            Research -= ResearchCost(r);
            Build.Research[r]++;
            _dirty = true;
            Refresh();
            AddXp(C.B.XpPerResearch);
            CheckGoals();
            return true;
        }

        public int ResearchLevel(string id)
        {
            int i = C.ResearchIndex(id);
            return i >= 0 ? Build.Research[i] : 0;
        }

        public long ResearchOnReset()
        {
            if (RunEnergy <= 1) return 0;
            double orders = Math.Log10(RunEnergy) - C.B.ResearchOffset;
            if (orders <= 0) return 0;
            double gain = C.B.ResearchScale * Math.Pow(orders, C.B.ResearchPower) * Snap.Stat[(int)StatId.ResearchGain];
            return (long)Math.Floor(Math.Min(gain, 1e12));
        }

        /// <summary>Run energy at which a reset would yield <paramref name="research"/> points.</summary>
        public double EnergyForResearch(long research)
        {
            double k = C.B.ResearchScale * Math.Max(1e-9, Snap.Stat[(int)StatId.ResearchGain]);
            double orders = Math.Pow(Math.Max(1, research) / k, 1 / C.B.ResearchPower);
            return Math.Pow(10, C.B.ResearchOffset + orders);
        }

        public bool Prestige()
        {
            if (Storm != null || !IsFeatureUnlocked("research")) return false;
            long gain = ResearchOnReset();
            if (gain < 1) return false;
            Research += gain;
            ResearchLifetime += gain;
            Prestiges++;
            int headStart = ResearchLevel("head_start");
            Energy = C.B.HeadStartEnergy[Math.Min(headStart, C.B.HeadStartEnergy.Length - 1)];
            RunEnergy = 0;
            Array.Clear(Build.Machines, 0, Build.Machines.Length);
            Array.Clear(BoostActive, 0, BoostActive.Length);
            Array.Clear(BoostCooldown, 0, BoostCooldown.Length);
            StormCharge = 0;
            _dirty = true;
            Refresh();
            AddXp(C.B.XpPerPrestige);
            CheckGoals();
            return true;
        }

        // ---------------------------------------------------------------- offline

        public OfflineReport ApplyOffline(double seconds)
        {
            if (seconds < 1 || Storm != null) return null;
            seconds = Math.Min(seconds, C.B.MaxOfflineSeconds);
            Refresh();
            AdvanceBoosts(seconds);
            Array.Clear(BoostActive, 0, BoostActive.Length);
            Refresh();

            var report = new OfflineReport { Seconds = seconds };
            if (!OfflineUnlocked)
            {
                // Without the Offline Generator the engine idles: only the storm chamber keeps charging.
                report.Locked = true;
                StormCharge = Math.Min(1, StormCharge + seconds / ChargeSeconds);
                return report;
            }
            double cap = Snap.Stat[(int)StatId.OfflineCapHours] * 3600;
            double eff = Math.Min(1, Snap.Stat[(int)StatId.OfflineEfficiency]);
            double t = Math.Min(seconds, cap);
            report.Capped = seconds > cap;
            report.CountedSeconds = t;
            report.Efficiency = eff;

            double income = Math.Max(0, Snap.Stat[(int)StatId.EnergyGain]);
            double energy = Snap.StormPower * t * eff * income;
            double charge = ChargeSeconds;

            if (Mods.Automation && AutoStorm)
            {
                var setup = new StormSetup
                {
                    Build = Build.Clone(),
                    BoostActiveTicks = new int[BoostActive.Length],
                    BoostCooldownTicks = new int[BoostActive.Length]
                };
                var storm = StormSim.Replay(C, setup, Array.Empty<StormEvent>(), out _);
                double cycle = charge + storm.Seconds;
                double available = t + StormCharge * charge;
                long count = (long)Math.Floor(available / cycle);
                if (count > 0)
                {
                    energy += count * storm.Score * eff * income;
                    report.Storms = count;
                    report.Strikes = count * storm.Strikes;
                    report.Cores = (long)Math.Floor(count * CoresFor(storm.Score) * eff);
                    Stats.Storms += count;
                    Stats.Strikes += report.Strikes;
                    Cores += report.Cores;
                }
                StormCharge = Math.Min(1, (available - count * cycle) / charge);
            }
            else
            {
                StormCharge = Math.Min(1, StormCharge + t / charge);
            }

            report.Energy = energy;
            AddEnergy(energy);
            CheckUnlocks();
            CheckGoals();
            return report;
        }

        /// <summary>Grants the offline Energy again (used by the optional 2x reward).</summary>
        public void GrantOfflineBonus(OfflineReport report)
        {
            if (report == null) return;
            AddEnergy(report.Energy);
        }

        // ---------------------------------------------------------------- discoveries & goals

        void CheckDiscoveries(bool[] active)
        {
            for (int p = 0; p < active.Length; p++)
            {
                if (!active[p] || Discovered[p]) continue;
                Discovered[p] = true;
                Cores += C.B.DiscoveryCores;
                Gems += C.B.GemsPerDiscovery;
                AddXp(C.B.XpPerDiscovery);
                PhenomenonDiscovered?.Invoke(p);
            }
        }

        public double GoalProgress(GoalDef g)
        {
            switch (g.Kind)
            {
                case GoalKind.MachineLevel:
                    int m = C.MachineIndex(g.Target);
                    return m >= 0 ? Build.Machines[m] : 0;
                case GoalKind.StormsTotal: return Stats.Storms;
                case GoalKind.Discover:
                    int p = C.PhenoIndex(g.Target);
                    return p >= 0 && Discovered[p] ? 1 : 0;
                case GoalKind.ModulesUnlocked:
                    int unlocked = 0;
                    foreach (int l in Build.ModuleLevels) if (l > 0) unlocked++;
                    return unlocked;
                case GoalKind.ModulesEquipped: return Build.Equipped.Count;
                case GoalKind.BoostsUsed: return Stats.BoostsUsed;
                case GoalKind.Prestiges: return Prestiges;
                case GoalKind.RunEnergy: return RunEnergy;
                case GoalKind.ResearchOwned:
                    int owned = 0;
                    foreach (int l in Build.Research) if (l > 0) owned++;
                    return owned;
                case GoalKind.StormScore: return Records.BestScore;
                case GoalKind.ChainPeak: return Stats.BestChain;
                case GoalKind.EngineLevel: return Level;
                case GoalKind.UpgradeLevel: return UpgradeLevel(g.Target);
                case GoalKind.CosmeticsOwned:
                    int ownedCosmetics = 0;
                    foreach (var cos in C.Cosmetics) if (!cos.Default && OwnedCosmetics.Contains(cos.Id)) ownedCosmetics++;
                    return ownedCosmetics + 1;
                case GoalKind.DecorationsOwned: return UniqueDecorations;
                case GoalKind.TournamentRuns: return Tournament.Runs;
                default: return 0;
            }
        }

        void CheckGoals()
        {
            for (int guard = 0; guard < C.Goals.Length; guard++)
            {
                var g = CurrentGoal;
                if (g == null || GoalProgress(g) < g.Amount) return;
                GoalsDone.Add(g.Id);
                Cores += g.RewardCores;
                Gems += g.RewardGems;
                if (g.RewardSeconds > 0) AddEnergy(Math.Max(10, Snap.StormPower) * g.RewardSeconds);
                GoalCompleted?.Invoke(g);
                AddXp(g.RewardXp);
            }
        }
    }
}
