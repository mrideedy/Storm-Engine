using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    public sealed class StormSetup
    {
        public Build Build;
        /// <summary>Remaining active ticks per boost when the storm starts.</summary>
        public int[] BoostActiveTicks;
        /// <summary>Remaining cooldown ticks per boost when the storm starts.</summary>
        public int[] BoostCooldownTicks;
    }

    public struct StormEvent
    {
        public int Tick;
        public int Boost;

        public StormEvent(int tick, int boost)
        {
            Tick = tick;
            Boost = boost;
        }
    }

    public sealed class StormResult
    {
        public double Score;
        /// <summary>Phenomenon index that produced most of the score, or -1 for a plain Squall.</summary>
        public int ClassIndex = -1;
        /// <summary>Score contributed per class. Index 0 is Squall, index p + 1 is phenomenon p.</summary>
        public double[] ClassScore;
        public bool[] PhenoSeen;
        public int Ticks;
        public int Strikes;
        public double PeakChain = 1;
        public double PeakPower;
        public double SumPower;
        public int MegaTicks;
        public int PerfectTicks;
        public int MaxActivePhenos;
        public bool Collapsed;
        public long Cores;
        /// <summary>True for weekly tournament attempts (the only storms submitted to leaderboards).</summary>
        public bool Tournament;
        public List<StormEvent> Events = new List<StormEvent>();

        public double Seconds => Ticks / (double)StormSim.TicksPerSecond;
        public double AveragePower => Ticks > 0 ? SumPower / Ticks : 0;
    }

    /// <summary>
    /// Deterministic fixed-step storm simulation. The live game steps it in real time and the
    /// server replays it from a <see cref="StormSubmission"/>; both must produce the same score.
    /// </summary>
    public sealed class StormSim
    {
        public const int TicksPerSecond = 10;
        public const double Dt = 0.1;

        readonly Content _c;
        readonly ModifierSet _ms;
        readonly EvalContext _ctx;
        bool _extended;
        double _strikeAccumulator;

        public readonly StormSetup Setup;
        public readonly Snapshot Snap;
        public readonly int[] ActiveTicks;
        public readonly int[] CooldownTicks;
        public readonly StormResult Result;

        public int Tick { get; private set; }
        public int DurationTicks { get; private set; }
        public bool Finished { get; private set; }
        public double Score { get; private set; }
        public double Chain { get; private set; } = 1;
        public double Instability { get; private set; }
        public int Strikes { get; private set; }

        /// <summary>Values from the most recent step, for presentation.</summary>
        public double LastStepScore { get; private set; }
        public int LastStepStrikes { get; private set; }
        public double LastIntensity { get; private set; }
        public double LastMega { get; private set; } = 1;
        public double LastPerfect { get; private set; } = 1;

        public ModifierSet Mods => _ms;
        public EvalContext Context => _ctx;
        public double Progress => DurationTicks > 0 ? Math.Min(1, Tick / (double)DurationTicks) : 1;
        public double SecondsLeft => Math.Max(0, DurationTicks - Tick) * Dt;

        public StormSim(Content c, StormSetup setup)
        {
            _c = c;
            Setup = setup;
            _ms = ModifierSet.Create(c, setup.Build);
            _ctx = new EvalContext(c.Boosts.Length) { StormActive = true };
            Snap = new Snapshot(c);
            ActiveTicks = (int[])setup.BoostActiveTicks.Clone();
            CooldownTicks = (int[])setup.BoostCooldownTicks.Clone();
            Result = new StormResult
            {
                ClassScore = new double[c.Phenomena.Length + 1],
                PhenoSeen = new bool[c.Phenomena.Length]
            };

            RefreshContext();
            Formula.Evaluate(c, _ms, _ctx, Snap);
            DurationTicks = Math.Max(1, TicksOf(Snap.Stat[(int)StatId.StormDuration]));
        }

        public static int TicksOf(double seconds) => (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);

        public bool CanActivate(int boost) =>
            !Finished && boost >= 0 && boost < ActiveTicks.Length && _ms.BoostUnlocked[boost] &&
            ActiveTicks[boost] == 0 && CooldownTicks[boost] == 0;

        void RefreshContext()
        {
            for (int i = 0; i < ActiveTicks.Length; i++) _ctx.BoostActive[i] = ActiveTicks[i] > 0;
            _ctx.Instability = Instability;
        }

        /// <summary>Advances one tick. <paramref name="activate"/> lists boosts pressed during this tick.</summary>
        /// <returns>False if an activation was not legal (the server treats that as an invalid submission).</returns>
        public bool Step(IReadOnlyList<int> activate)
        {
            if (Finished) return true;
            bool legal = true;

            if (activate != null)
            {
                for (int i = 0; i < activate.Count; i++)
                {
                    int b = activate[i];
                    if (!CanActivate(b))
                    {
                        legal = false;
                        continue;
                    }
                    ActiveTicks[b] = Math.Max(1, TicksOf(_ms.BoostDuration[b]));
                    Result.Events.Add(new StormEvent(Tick, b));
                }
            }

            RefreshContext();
            Formula.Evaluate(_c, _ms, _ctx, Snap);

            int dom = Snap.Dominant;
            var perk = dom >= 0 ? _c.Phenomena[dom].Perk : PerkKind.None;
            double perkValue = dom >= 0 ? _c.Phenomena[dom].PerkValue : 0;

            double lightning = Snap.Stat[(int)StatId.LightningRate];
            double chainPer = Snap.Stat[(int)StatId.ChainPerStrike];
            double intensity = Snap.Stat[(int)StatId.StormIntensity];
            switch (perk)
            {
                case PerkKind.LightningRate: lightning *= 1 + perkValue; break;
                case PerkKind.ChainBoost: chainPer *= 1 + perkValue; break;
                case PerkKind.IntensityMult: intensity *= perkValue; break;
                case PerkKind.RampingIntensity: intensity *= 1 + perkValue * (Tick * Dt); break;
                case PerkKind.Anomaly: intensity *= 2; break;
                case PerkKind.ExtraDuration:
                    if (!_extended)
                    {
                        DurationTicks += TicksOf(perkValue);
                        _extended = true;
                    }
                    break;
            }

            double mega = TierMult(Snap.ActivePhenos, _c.B.MegaCounts, _c.B.MegaMults);
            mega = 1 + (mega - 1) * Snap.Stat[(int)StatId.MegaStormPower];
            double perfect = TierMult(Snap.BoostsActive, _c.B.PerfectCounts, _c.B.PerfectMults);

            _strikeAccumulator += lightning * Dt;
            int strikes = (int)Math.Floor(_strikeAccumulator);
            _strikeAccumulator -= strikes;
            Strikes += strikes;
            Chain += strikes * chainPer;

            double total = intensity * Chain * mega * perfect;
            double gained = Snap.StormPower * total * Dt;
            Score += gained;

            LastStepScore = gained;
            LastStepStrikes = strikes;
            LastIntensity = total;
            LastMega = mega;
            LastPerfect = perfect;

            var r = Result;
            r.ClassScore[dom + 1] += gained;
            r.SumPower += Snap.StormPower;
            r.PeakPower = Math.Max(r.PeakPower, Snap.StormPower * total);
            r.PeakChain = Math.Max(r.PeakChain, Chain);
            if (mega > 1) r.MegaTicks++;
            if (perfect > 1) r.PerfectTicks++;
            r.MaxActivePhenos = Math.Max(r.MaxActivePhenos, Snap.ActivePhenos);
            for (int p = 0; p < Snap.PhenoActive.Length; p++)
                if (Snap.PhenoActive[p]) r.PhenoSeen[p] = true;

            if (_ms.ChaosEquipped)
            {
                Instability += Snap.Stat[(int)StatId.InstabilityRate] * Dt;
                if (Instability >= 1)
                {
                    Instability = 1;
                    r.Collapsed = true;
                    Finished = true;
                }
            }

            for (int b = 0; b < ActiveTicks.Length; b++)
            {
                if (ActiveTicks[b] > 0)
                {
                    ActiveTicks[b]--;
                    if (ActiveTicks[b] == 0) CooldownTicks[b] = TicksOf(_ms.BoostCooldown[b]);
                }
                else if (CooldownTicks[b] > 0)
                {
                    CooldownTicks[b]--;
                }
            }

            Tick++;
            if (Tick >= DurationTicks) Finished = true;
            if (Finished) Complete();
            return legal;
        }

        void Complete()
        {
            var r = Result;
            r.Score = Score;
            r.Ticks = Tick;
            r.Strikes = Strikes;
            int best = 0;
            for (int i = 1; i < r.ClassScore.Length; i++)
                if (r.ClassScore[i] > r.ClassScore[best]) best = i;
            r.ClassIndex = best - 1;
        }

        static double TierMult(int count, int[] counts, double[] mults)
        {
            double m = 1;
            for (int i = 0; i < counts.Length; i++)
                if (count >= counts[i]) m = mults[i];
            return m;
        }

        /// <summary>Replays a whole storm from its start state and activation log.</summary>
        public static StormResult Replay(Content c, StormSetup setup, IList<StormEvent> events, out bool legal)
        {
            var sim = new StormSim(c, setup);
            var pending = new List<int>();
            int e = 0;
            legal = true;
            var ordered = new List<StormEvent>(events);
            ordered.Sort((a, b) => a.Tick.CompareTo(b.Tick));
            int guard = 0;
            while (!sim.Finished && guard++ < 100000)
            {
                pending.Clear();
                while (e < ordered.Count && ordered[e].Tick == sim.Tick) pending.Add(ordered[e++].Boost);
                if (e < ordered.Count && ordered[e].Tick < sim.Tick) { legal = false; e++; }
                if (!sim.Step(pending)) legal = false;
            }
            if (e < ordered.Count) legal = false;
            return sim.Result;
        }
    }
}
