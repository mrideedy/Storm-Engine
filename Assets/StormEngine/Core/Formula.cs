using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    public sealed class EvalContext
    {
        public bool StormActive;
        public bool[] BoostActive;
        public double Instability;

        public EvalContext(int boostCount)
        {
            BoostActive = new bool[boostCount];
        }
    }

    /// <summary>Result of one formula evaluation.</summary>
    public sealed class Snapshot
    {
        public readonly double[] Stat = new double[Content.StatCount];
        public readonly StatCalc[] Calc = new StatCalc[Content.StatCount];
        /// <summary>Effective additive pool (Add x AddEff) of each atmosphere stat before conversions.</summary>
        public readonly double[] PreConversionPool = new double[6];
        public readonly double[] PhenoStrength;
        public readonly double[] PhenoRatio;
        public readonly bool[] PhenoActive;
        public int ActivePhenos;
        public int Dominant = -1;
        public double Synergy = 1;
        public double StormPower;
        public int BoostsActive;

        public Snapshot(Content c)
        {
            PhenoStrength = new double[c.Phenomena.Length];
            PhenoRatio = new double[c.Phenomena.Length];
            PhenoActive = new bool[c.Phenomena.Length];
        }

        public double CatValue(int cat) => Stat[cat + 1];

        public void CopyFrom(Snapshot o)
        {
            Array.Copy(o.Stat, Stat, Stat.Length);
            Array.Copy(o.Calc, Calc, Calc.Length);
            Array.Copy(o.PreConversionPool, PreConversionPool, PreConversionPool.Length);
            Array.Copy(o.PhenoStrength, PhenoStrength, PhenoStrength.Length);
            Array.Copy(o.PhenoRatio, PhenoRatio, PhenoRatio.Length);
            Array.Copy(o.PhenoActive, PhenoActive, PhenoActive.Length);
            ActivePhenos = o.ActivePhenos;
            Dominant = o.Dominant;
            Synergy = o.Synergy;
            StormPower = o.StormPower;
            BoostsActive = o.BoostsActive;
        }
    }

    /// <summary>One line of a formula breakdown shown in the Atmosphere panel.</summary>
    public struct BreakdownLine
    {
        public ModOp Op;
        public double Value;
        public SourceKind Source;
        public int SourceIndex;
        public int Level;
        public StatId From;
        public bool Conditional;
    }

    /// <summary>
    /// The centralized formula engine. Evaluation order is explicit and fixed:
    /// Meta stats, then Atmosphere (base + categories, then conversions), then Phenomena, then Output.
    /// Storm Power = Base Pressure x Temperature x Humidity x Wind x Cloud x Electricity x Synergy x Engine Efficiency.
    /// </summary>
    public static class Formula
    {
        static readonly StatId[] AtmosphereStats =
        {
            StatId.BasePressure, StatId.Temperature, StatId.Humidity, StatId.Wind, StatId.Cloud, StatId.Electricity
        };

        static readonly StatId[] OutputStats =
        {
            StatId.GlobalPower, StatId.StormIntensity, StatId.StormDuration, StatId.LightningRate,
            StatId.ChainPerStrike, StatId.InstabilityRate
        };

        public static void Evaluate(Content c, ModifierSet ms, EvalContext ctx, Snapshot s)
        {
            // Meta: resolved once per build.
            for (int i = 0; i < Content.StatCount; i++)
            {
                if (c.Stats[i].Phase != StatPhase.Meta) continue;
                s.Calc[i] = ms.Meta[i];
                s.Stat[i] = ms.Meta[i].Value;
            }

            // Atmosphere: accumulate, then move converted additive pools, then finalize.
            foreach (var id in AtmosphereStats)
            {
                int i = (int)id;
                s.Calc[i] = Accumulate(c, ms, id, ctx, null);
                s.PreConversionPool[i] = s.Calc[i].Add * s.Calc[i].AddEff;
            }

            foreach (var conv in ms.Conversions)
            {
                int from = (int)conv.From;
                int to = (int)conv.Stat;
                if (from > (int)StatId.Electricity || to > (int)StatId.Electricity) continue;
                s.Calc[to].Add += conv.Value * s.PreConversionPool[from];
            }

            foreach (var id in AtmosphereStats)
            {
                int i = (int)id;
                Finalize(ref s.Calc[i]);
                s.Stat[i] = s.Calc[i].Value;
            }

            EvaluatePhenomena(c, ms, ctx, s);

            foreach (var id in OutputStats)
                s.Calc[(int)id] = Accumulate(c, ms, id, ctx, s);

            // Electricity drives lightning: +0.3 strikes/s per order of magnitude.
            double elec = s.Stat[(int)StatId.Electricity];
            if (elec > 1)
            {
                ref var lr = ref s.Calc[(int)StatId.LightningRate];
                lr.Base += c.B.LightningPerElecLog * Math.Log10(elec);
                Finalize(ref lr);
            }

            // A thick cloud layer keeps chaotic storms stable.
            double cloud = s.Stat[(int)StatId.Cloud];
            if (cloud > 1)
            {
                ref var ir = ref s.Calc[(int)StatId.InstabilityRate];
                ir.Mult /= 1 + c.B.CloudStabilizerPerLog * Math.Log10(cloud);
                Finalize(ref ir);
            }

            foreach (var id in OutputStats) s.Stat[(int)id] = s.Calc[(int)id].Value;

            double power = s.Stat[(int)StatId.BasePressure];
            for (int cat = 0; cat < Content.CatCount; cat++) power *= s.CatValue(cat);
            power *= s.Synergy * s.Stat[(int)StatId.GlobalPower];
            s.StormPower = power;

            int active = 0;
            for (int i = 0; i < ctx.BoostActive.Length; i++)
                if (ctx.BoostActive[i] && ms.BoostUnlocked[i]) active++;
            s.BoostsActive = active;
        }

        static void EvaluatePhenomena(Content c, ModifierSet ms, EvalContext ctx, Snapshot s)
        {
            s.Calc[(int)StatId.PhenoAll] = Accumulate(c, ms, StatId.PhenoAll, ctx, null);
            s.Stat[(int)StatId.PhenoAll] = s.Calc[(int)StatId.PhenoAll].Value;
            double all = s.Stat[(int)StatId.PhenoAll];

            int activeCount = 0;
            for (int p = 0; p < c.Phenomena.Length; p++)
            {
                var def = c.Phenomena[p];
                int stat = (int)def.Stat;
                s.Calc[stat] = Accumulate(c, ms, def.Stat, ctx, null);
                s.Stat[stat] = s.Calc[stat].Value;

                bool active;
                double raw;
                if (def.Special)
                {
                    active = ctx.StormActive && ms.ChaosEquipped && ctx.Instability >= c.B.AnomalyThreshold;
                    s.PhenoRatio[p] = ms.ChaosEquipped ? ctx.Instability / c.B.AnomalyThreshold : 0;
                    raw = c.B.AnomalyBase + c.B.AnomalyPerLevel * Math.Max(0, ms.ChaosLevel - 1);
                }
                else
                {
                    double ratio = double.MaxValue;
                    foreach (var ing in def.Ingredients)
                        ratio = Math.Min(ratio, s.CatValue((int)ing.Cat) / ing.Threshold);
                    s.PhenoRatio[p] = ratio;
                    active = ratio >= 1;
                    // Log scaling: strength keeps growing with the weakest ingredient, but slowly,
                    // so phenomena never overpower the category multipliers they depend on.
                    raw = active ? def.BaseMult * (1 + def.Alpha * Math.Log(ratio)) : 1;
                }

                s.PhenoActive[p] = active;
                s.PhenoStrength[p] = active ? raw * all * s.Stat[stat] : 1;
                if (active) activeCount++;
            }

            double resonance = ms.Meta[(int)StatId.Resonance].Value;
            if (activeCount > 1 && resonance > 0)
            {
                double f = 1 + resonance * (activeCount - 1);
                for (int p = 0; p < c.Phenomena.Length; p++)
                    if (s.PhenoActive[p]) s.PhenoStrength[p] *= f;
            }

            double synergy = 1;
            int dominant = -1;
            double best = 0;
            for (int p = 0; p < c.Phenomena.Length; p++)
            {
                if (!s.PhenoActive[p]) continue;
                synergy *= s.PhenoStrength[p];
                if (s.PhenoStrength[p] > best)
                {
                    best = s.PhenoStrength[p];
                    dominant = p;
                }
            }
            s.ActivePhenos = activeCount;
            s.Synergy = synergy;
            s.Dominant = dominant;
        }

        /// <summary>Sums base, additive, effectiveness and multiplicative modifiers for one stat.</summary>
        public static StatCalc Accumulate(Content c, ModifierSet ms, StatId stat, EvalContext ctx, Snapshot phenomena)
        {
            var calc = new StatCalc { Base = c.Stats[(int)stat].Base, Add = 0, AddEff = 1, Mult = 1 };
            var list = ms.ByStat[(int)stat];
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (!ConditionMet(m, ctx, phenomena)) continue;
                switch (m.Op)
                {
                    case ModOp.Flat: calc.Base += m.Value; break;
                    case ModOp.Add: calc.Add += m.Value; break;
                    case ModOp.AddEff: calc.AddEff *= m.Value; break;
                    case ModOp.Mult: calc.Mult *= m.Value; break;
                }
            }
            Finalize(ref calc);
            return calc;
        }

        static void Finalize(ref StatCalc calc)
        {
            calc.Value = calc.Base * (1 + calc.Add * calc.AddEff) * calc.Mult;
        }

        static bool ConditionMet(in Modifier m, EvalContext ctx, Snapshot phenomena)
        {
            switch (m.Cond)
            {
                case CondKind.None: return true;
                case CondKind.StormActive: return ctx != null && ctx.StormActive;
                case CondKind.BoostActive:
                    return ctx != null && m.CondIndex >= 0 && m.CondIndex < ctx.BoostActive.Length && ctx.BoostActive[m.CondIndex];
                case CondKind.PhenoActive:
                    return phenomena != null && m.CondIndex >= 0 && m.CondIndex < phenomena.PhenoActive.Length && phenomena.PhenoActive[m.CondIndex];
                default: return false;
            }
        }

        /// <summary>Lists every modifier that currently contributes to a stat (for the UI breakdown).</summary>
        public static void Explain(Content c, ModifierSet ms, EvalContext ctx, Snapshot s, StatId stat, List<BreakdownLine> into)
        {
            into.Clear();
            var list = ms.ByStat[(int)stat];
            foreach (var m in list)
            {
                if (!ConditionMet(m, ctx, s)) continue;
                into.Add(new BreakdownLine
                {
                    Op = m.Op, Value = m.Value, Source = m.Source, SourceIndex = m.SourceIndex, Level = m.Level,
                    Conditional = m.Cond != CondKind.None
                });
            }

            if (stat <= StatId.Electricity)
            {
                foreach (var conv in ms.Conversions)
                {
                    if (conv.Stat != stat || conv.From > StatId.Electricity) continue;
                    into.Add(new BreakdownLine
                    {
                        Op = ModOp.Convert, Value = conv.Value * s.PreConversionPool[(int)conv.From],
                        Source = conv.Source, SourceIndex = conv.SourceIndex, Level = conv.Level, From = conv.From
                    });
                }
            }

            if (stat == StatId.LightningRate)
            {
                double elec = s.Stat[(int)StatId.Electricity];
                if (elec > 1)
                    into.Add(new BreakdownLine { Op = ModOp.Flat, Value = c.B.LightningPerElecLog * Math.Log10(elec), Source = SourceKind.Electricity });
            }
        }
    }
}
