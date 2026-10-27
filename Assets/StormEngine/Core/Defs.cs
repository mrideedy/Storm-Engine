using System;

namespace StormEngine.Core
{
    /// <summary>The five atmosphere categories. Each one is a multiplier in the Storm Power formula.</summary>
    public enum Cat { Temperature, Humidity, Wind, Cloud, Electricity }

    /// <summary>
    /// Every number the formula engine can modify. Order matters: the Cloud Code validator
    /// receives this list by name, so append new stats at the end (before Count).
    /// </summary>
    public enum StatId
    {
        BasePressure,
        Temperature,
        Humidity,
        Wind,
        Cloud,
        Electricity,
        GlobalPower,
        PhenoAll,
        PhenoThunderstorm,
        PhenoHeatStorm,
        PhenoMonsoon,
        PhenoCyclone,
        PhenoSupercell,
        PhenoOvercharge,
        PhenoAnomaly,
        StormIntensity,
        StormDuration,
        StormChargeSpeed,
        LightningRate,
        ChainPerStrike,
        MegaStormPower,
        BoostDuration,
        BoostCooldown,
        BoostPower,
        OfflineEfficiency,
        OfflineCapHours,
        CoreGain,
        ResearchGain,
        ModuleSlots,
        InstabilityRate,
        Resonance,
        /// <summary>Multiplies Energy income (production and storms) but never storm scores.</summary>
        EnergyGain,
        Count
    }

    /// <summary>
    /// Operation order inside one stat: (base + Flat) x (1 + Add x AddEff) x Mult.
    /// Convert moves a share of another stat's effective additive pool into this stat's Add pool.
    /// </summary>
    public enum ModOp { Flat, Add, AddEff, Mult, Convert }

    public enum ValueMode
    {
        /// <summary>Base + PerLevel x (level - 1). Used by modules.</summary>
        Linear,
        /// <summary>PerLevel x level. Used by machines and research.</summary>
        Scaled,
        /// <summary>Base ^ level. Used by stacking multiplicative research.</summary>
        Power
    }

    public enum CondKind { None, StormActive, BoostActive, PhenoActive }

    public enum SourceKind { Base, Machine, Milestone, Module, Research, Weekly, Boost, Conversion, Electricity, Upgrade, Style, Decoration }

    /// <summary>Evaluation phase of a stat. Later phases may read earlier phases.</summary>
    public enum StatPhase { Meta, Atmosphere, Phenomena, Output }

    /// <summary>What the dominant phenomenon does to a running storm.</summary>
    public enum PerkKind { None, LightningRate, ExtraDuration, RampingIntensity, ChainBoost, IntensityMult, Anomaly }

    public enum GoalKind
    {
        MachineLevel,
        StormsTotal,
        Discover,
        ModulesUnlocked,
        ModulesEquipped,
        BoostsUsed,
        Prestiges,
        RunEnergy,
        ResearchOwned,
        StormScore,
        ChainPeak,
        EngineLevel,
        UpgradeLevel,
        CosmeticsOwned,
        DecorationsOwned,
        TournamentRuns
    }

    public sealed class EffectDef
    {
        public StatId Stat;
        public ModOp Op;
        public ValueMode Mode;
        public double Base;
        public double PerLevel;
        public StatId From;
        public CondKind Cond;
        public int CondIndex = -1;

        public double ValueAt(int level)
        {
            switch (Mode)
            {
                case ValueMode.Scaled: return PerLevel * level;
                case ValueMode.Power: return Math.Pow(Base, level);
                default: return Base + PerLevel * (level - 1);
            }
        }
    }

    public sealed class StatInfo
    {
        public StatId Id;
        public string Name;
        public double Base;
        public StatPhase Phase;
        /// <summary>x = multiplier, s = seconds, p = percent, r = per second, n = plain number.</summary>
        public char Format;
    }

    public sealed class MachineDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Icon;
        /// <summary>-1 when the machine does not belong to a category.</summary>
        public int Cat = -1;
        public double BaseCost;
        public double Growth;
        /// <summary>Extra cost growth per level squared. Guarantees a soft wall inside every run.</summary>
        public double Super = 1;
        public int MaxLevel;
        public double UnlockAt;
        /// <summary>Engine Level required before the machine can be built.</summary>
        public int UnlockLevel = 1;
        public EffectDef[] Effects = Array.Empty<EffectDef>();
        public StatId MilestoneStat;
        /// <summary>0 disables milestones.</summary>
        public double MilestoneMult;
    }

    public sealed class BoostDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Icon;
        public StatId Target;
        public double Factor;
        public double Duration;
        public double Cooldown;
        public string UnlockMachine;
        public int UnlockLevel;
        /// <summary>Optional second effect (multiplier) while active.</summary>
        public StatId ExtraStat;
        public double ExtraFactor;
    }

    public struct Ingredient
    {
        public Cat Cat;
        public double Threshold;

        public Ingredient(Cat cat, double threshold)
        {
            Cat = cat;
            Threshold = threshold;
        }
    }

    public sealed class PhenoDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string PerkText;
        public StatId Stat;
        public Ingredient[] Ingredients = Array.Empty<Ingredient>();
        public double BaseMult;
        public double Alpha;
        public PerkKind Perk;
        public double PerkValue;
        /// <summary>Anomaly only appears inside storms driven by the Chaos Reactor.</summary>
        public bool Special;
    }

    public sealed class ModuleDef
    {
        public string Id;
        public string Name;
        /// <summary>Compact label for equipment slots.</summary>
        public string Short;
        public string Desc;
        public string Icon;
        public long UnlockCost;
        public int UnlockLevel = 1;
        /// <summary>Phenomenon id that must be discovered first (or null).</summary>
        public string RequiresDiscovery;
        public int RequiresPrestiges;
        public int MaxLevel = 10;
        public EffectDef[] Effects = Array.Empty<EffectDef>();
    }

    public sealed class ResearchDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Icon;
        public double BaseCost;
        public double CostGrowth;
        public int MaxLevel;
        public int RequiresPrestiges;
        public int UnlockLevel = 1;
        public EffectDef[] Effects = Array.Empty<EffectDef>();
    }

    public sealed class WeeklyDef
    {
        public string Id;
        public string Name;
        public string[] Lines = Array.Empty<string>();
        public EffectDef[] Effects = Array.Empty<EffectDef>();
    }

    public sealed class GoalDef
    {
        public string Id;
        public string Text;
        public GoalKind Kind;
        public string Target;
        public double Amount;
        public long RewardCores;
        /// <summary>Energy reward expressed as seconds of current base production.</summary>
        public double RewardSeconds;
        public double RewardXp;
        public long RewardGems;
    }

    /// <summary>A game system that switches on at an Engine Level (tabs, leaderboards, offline...).</summary>
    public sealed class FeatureDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Icon;
        public int Level;
    }

    /// <summary>Permanent engine upgrade bought with Energy. Survives atmosphere resets.</summary>
    public sealed class UpgradeDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Icon;
        public string Feature;
        public int MaxLevel;
        public double BaseCost;
        public double CostGrowth;
        /// <summary>Storm Cores also required per level (0 = none).</summary>
        public double BaseCores;
        public double CoresGrowth = 1;
        public EffectDef[] Effects = Array.Empty<EffectDef>();
    }

    public enum CosmeticSlot { Skin, Cloud, Bolt, World, Title }

    /// <summary>
    /// A style item. Besides its look, every non-default item grants a direct multiplier to one
    /// stat (see <see cref="Effects"/>). In tournaments each style bonus is capped (Balance).
    /// </summary>
    public sealed class CosmeticDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public CosmeticSlot Slot;
        public int UnlockLevel = 1;
        public long CostCores;
        /// <summary>Premium price in Gems (0 = not sold for Gems).</summary>
        public long CostGems;
        /// <summary>Only obtainable in this ads-only bundle (never sold on its own).</summary>
        public string Bundle;
        /// <summary>Gem-only showcase item, marked EXCLUSIVE in the shop.</summary>
        public bool Exclusive;
        public string RequiresDiscovery;
        public int RequiresPrestiges;
        /// <summary>Main, secondary and accent colours as 0xRRGGBB.</summary>
        public int Primary;
        public int Secondary;
        public int Accent;
        public bool Default;
        /// <summary>Visual family: machine shape for skins, blob style for clouds, bolt style for lightning, props for worlds.</summary>
        public string Style;
        public EffectDef[] Effects = Array.Empty<EffectDef>();
    }

    /// <summary>Everything in an ads-only bundle unlocks together once enough rewarded ads are watched.</summary>
    public sealed class BundleDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public int AdsRequired;
        public int UnlockLevel = 1;
        public string[] Cosmetics = Array.Empty<string>();
        public string[] Decorations = Array.Empty<string>();
        /// <summary>Card colours (0xRRGGBB).</summary>
        public int Primary;
        public int Accent;
    }

    public enum DecorKind { Land, Air, Water }

    /// <summary>A placeable island decoration. Each distinct decoration owned adds a small Energy bonus.</summary>
    public sealed class DecorationDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public DecorKind Kind;
        /// <summary>Procedural model id used by the renderer.</summary>
        public string Model;
        public int UnlockLevel = 1;
        public long CostCores;
        public long CostGems;
        /// <summary>Energy income bonus (0.01 = +1%) granted once for owning it.</summary>
        public double Bonus = 0.01;
        public string Bundle;
        /// <summary>Limited decorations are only sold during their featured week.</summary>
        public bool Limited;
        public bool Exclusive;
        /// <summary>How many copies can be owned (placed) at most.</summary>
        public int MaxCopies = 5;
        public int Primary;
        public int Accent;
    }

    /// <summary>A terrain expansion: the island grows and holds more decorations.</summary>
    public sealed class ExpansionDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public long CostGems;
        public int UnlockLevel;
        /// <summary>Island radius after this expansion.</summary>
        public float Radius;
        /// <summary>Extra decoration capacity.</summary>
        public int Capacity;
    }

    public enum AdReward { Gems, EnergySurge, InstantCharge, TournamentAttempt, BundleProgress, DoubleCores, DoubleOffline, MusicTrack, StreakRestore }

    /// <summary>A rewarded-ad placement with a daily limit (0 = unlimited).</summary>
    public sealed class AdPlacementDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public string Icon;
        public AdReward Reward;
        public double Amount;
        public int DailyLimit;
        public int UnlockLevel = 1;
    }

    /// <summary>A real-money store product (Gems pack or bundle). Prices come from the store.</summary>
    public sealed class ProductDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public long Gems;
        public long Cores;
        public string[] Cosmetics = Array.Empty<string>();
        /// <summary>Price shown when the store cannot provide a localized one.</summary>
        public string FallbackPrice;
        /// <summary>One purchase per player.</summary>
        public bool OneTime;
        /// <summary>Badge text such as "+20%" or "BEST VALUE".</summary>
        public string Tag;
    }
}
