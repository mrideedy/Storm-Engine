using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    /// <summary>
    /// Everything that determines the formula: machine levels, module levels, equipped modules,
    /// research levels and the weekly condition. A storm is scored from a frozen copy of this.
    /// </summary>
    public sealed class Build
    {
        public int[] Machines;
        public int[] ModuleLevels;
        public List<int> Equipped = new List<int>();
        public int[] Research;
        /// <summary>
        /// Permanent engine upgrades. They only touch offline stats, never storm scoring, so storm
        /// submissions leave them out.
        /// </summary>
        public int[] Upgrades;
        public int Weekly = -1;
        /// <summary>Equipped style item per <see cref="CosmeticSlot"/> (cosmetic index, -1 = none).</summary>
        public int[] Style = { -1, -1, -1, -1, -1 };
        /// <summary>When above zero, each style multiplier counts at most 1 + StyleCap (tournaments).</summary>
        public double StyleCap;
        /// <summary>Energy income bonus from owned decorations (0.05 = +5%). Never affects storm scores.</summary>
        public double DecorBonus;

        public Build(Content c)
        {
            Machines = new int[c.Machines.Length];
            ModuleLevels = new int[c.Modules.Length];
            Research = new int[c.Research.Length];
            Upgrades = new int[c.Upgrades.Length];
        }

        public Build Clone()
        {
            var b = (Build)MemberwiseClone();
            b.Machines = (int[])Machines.Clone();
            b.ModuleLevels = (int[])ModuleLevels.Clone();
            b.Research = (int[])Research.Clone();
            b.Upgrades = (int[])Upgrades.Clone();
            b.Style = (int[])Style.Clone();
            b.Equipped = new List<int>(Equipped);
            return b;
        }

        public bool IsEquipped(int module) => Equipped.Contains(module);
    }

    public struct Modifier
    {
        public StatId Stat;
        public ModOp Op;
        public double Value;
        public StatId From;
        public CondKind Cond;
        public int CondIndex;
        public SourceKind Source;
        public int SourceIndex;
        public int Level;
    }

    public struct StatCalc
    {
        public double Base;
        public double Add;
        public double AddEff;
        public double Mult;
        public double Value;
    }

    /// <summary>All modifiers produced by a build, grouped by the stat they target.</summary>
    public sealed class ModifierSet
    {
        public readonly List<Modifier>[] ByStat = new List<Modifier>[Content.StatCount];
        public readonly List<Modifier> Conversions = new List<Modifier>();
        public readonly StatCalc[] Meta = new StatCalc[Content.StatCount];

        public bool[] BoostUnlocked;
        public double[] BoostDuration;
        public double[] BoostCooldown;
        public bool ChaosEquipped;
        public int ChaosLevel;
        public bool Automation;
        public int ModuleSlots;

        ModifierSet()
        {
            for (int i = 0; i < ByStat.Length; i++) ByStat[i] = new List<Modifier>();
        }

        public static ModifierSet Create(Content c, Build b)
        {
            var ms = new ModifierSet();

            for (int i = 0; i < c.Machines.Length; i++)
            {
                int level = b.Machines[i];
                if (level <= 0) continue;
                var m = c.Machines[i];
                foreach (var e in m.Effects) ms.AddEffect(e, level, SourceKind.Machine, i);
                if (m.MilestoneMult > 0)
                {
                    int n = c.MilestonesReached(level);
                    if (n > 0)
                        ms.Add(new Modifier
                        {
                            Stat = m.MilestoneStat, Op = ModOp.Mult, Value = Math.Pow(m.MilestoneMult, n),
                            Source = SourceKind.Milestone, SourceIndex = i, Level = n, CondIndex = -1
                        });
                }
            }

            foreach (int mod in b.Equipped)
            {
                if (mod < 0 || mod >= c.Modules.Length) continue;
                int level = b.ModuleLevels[mod];
                if (level <= 0) continue;
                foreach (var e in c.Modules[mod].Effects) ms.AddEffect(e, level, SourceKind.Module, mod);
                if (c.Modules[mod].Id == "chaos_reactor")
                {
                    ms.ChaosEquipped = true;
                    ms.ChaosLevel = level;
                }
            }

            for (int i = 0; i < c.Research.Length; i++)
            {
                int level = b.Research[i];
                if (level <= 0) continue;
                foreach (var e in c.Research[i].Effects) ms.AddEffect(e, level, SourceKind.Research, i);
            }
            if (b.Upgrades != null)
                for (int i = 0; i < c.Upgrades.Length && i < b.Upgrades.Length; i++)
                {
                    int level = b.Upgrades[i];
                    if (level <= 0) continue;
                    foreach (var e in c.Upgrades[i].Effects) ms.AddEffect(e, level, SourceKind.Upgrade, i);
                }

            int automation = c.ResearchIndex("automation");
            ms.Automation = automation >= 0 && b.Research[automation] > 0;

            if (b.Weekly >= 0 && b.Weekly < c.Weekly.Length)
                foreach (var e in c.Weekly[b.Weekly].Effects) ms.AddEffect(e, 1, SourceKind.Weekly, b.Weekly);

            // Style items: direct multipliers, capped in tournaments.
            if (b.Style != null)
                for (int slot = 0; slot < b.Style.Length; slot++)
                {
                    int item = b.Style[slot];
                    if (item < 0 || item >= c.Cosmetics.Length) continue;
                    foreach (var e in c.Cosmetics[item].Effects)
                    {
                        double value = e.ValueAt(1);
                        if (b.StyleCap > 0)
                        {
                            if (e.Op == ModOp.Mult) value = Math.Min(value, 1 + b.StyleCap);
                            else if (e.Op == ModOp.Add || e.Op == ModOp.Flat) value = Math.Min(value, b.StyleCap);
                        }
                        ms.Add(new Modifier
                        {
                            Stat = e.Stat, Op = e.Op, Value = value, From = e.From, Cond = e.Cond, CondIndex = e.CondIndex,
                            Source = SourceKind.Style, SourceIndex = item, Level = 1
                        });
                    }
                }

            if (b.DecorBonus > 0)
                ms.Add(new Modifier { Stat = StatId.EnergyGain, Op = ModOp.Add, Value = b.DecorBonus, Source = SourceKind.Decoration, SourceIndex = -1, Level = 1, CondIndex = -1 });

            // Meta stats never depend on conditions, so they are resolved once per build.
            for (int i = 0; i < Content.StatCount; i++)
            {
                if (c.Stats[i].Phase != StatPhase.Meta) continue;
                ms.Meta[i] = Formula.Accumulate(c, ms, (StatId)i, null, null);
            }
            ms.ModuleSlots = (int)Math.Round(ms.Meta[(int)StatId.ModuleSlots].Value);

            double boostPower = ms.Meta[(int)StatId.BoostPower].Value;
            double durMult = ms.Meta[(int)StatId.BoostDuration].Value;
            double cdMult = ms.Meta[(int)StatId.BoostCooldown].Value;
            ms.BoostUnlocked = new bool[c.Boosts.Length];
            ms.BoostDuration = new double[c.Boosts.Length];
            ms.BoostCooldown = new double[c.Boosts.Length];
            for (int i = 0; i < c.Boosts.Length; i++)
            {
                var bd = c.Boosts[i];
                int mi = c.MachineIndex(bd.UnlockMachine);
                ms.BoostUnlocked[i] = mi >= 0 && b.Machines[mi] >= bd.UnlockLevel;
                ms.BoostDuration[i] = bd.Duration * durMult;
                ms.BoostCooldown[i] = bd.Cooldown * cdMult;
                if (!ms.BoostUnlocked[i]) continue;
                ms.Add(new Modifier
                {
                    Stat = bd.Target, Op = ModOp.Mult, Value = 1 + (bd.Factor - 1) * boostPower,
                    Cond = CondKind.BoostActive, CondIndex = i, Source = SourceKind.Boost, SourceIndex = i, Level = 1
                });
                if (bd.ExtraFactor > 0)
                    ms.Add(new Modifier
                    {
                        Stat = bd.ExtraStat, Op = ModOp.Mult, Value = 1 + (bd.ExtraFactor - 1) * boostPower,
                        Cond = CondKind.BoostActive, CondIndex = i, Source = SourceKind.Boost, SourceIndex = i, Level = 1
                    });
            }

            return ms;
        }

        void AddEffect(EffectDef e, int level, SourceKind source, int index)
        {
            Add(new Modifier
            {
                Stat = e.Stat, Op = e.Op, Value = e.ValueAt(level), From = e.From,
                Cond = e.Cond, CondIndex = e.CondIndex, Source = source, SourceIndex = index, Level = level
            });
        }

        void Add(Modifier m)
        {
            if (m.Op == ModOp.Convert) Conversions.Add(m);
            else ByStat[(int)m.Stat].Add(m);
        }
    }
}
