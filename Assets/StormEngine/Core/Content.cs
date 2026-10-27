using System;
using System.Collections.Generic;

namespace StormEngine.Core
{
    /// <summary>Global tuning constants that are not attached to a specific content entry.</summary>
    public sealed class Balance
    {
        public double TickSeconds = 0.1;
        public double BaseChargeSeconds = 60;
        public double LightningPerElecLog = 0.3;
        public double CloudStabilizerPerLog = 0.15;
        public double AnomalyThreshold = 0.4;
        public double AnomalyBase = 3;
        public double AnomalyPerLevel = 0.25;
        public int[] MegaCounts = { 3, 5, 7 };
        public double[] MegaMults = { 1.5, 2.0, 3.0 };
        public int[] PerfectCounts = { 3, 4 };
        public double[] PerfectMults = { 1.25, 1.5 };

        /// <summary>Research on reset = Scale x (log10(run energy) - Offset) ^ Power.</summary>
        public double ResearchScale = 1;
        public double ResearchOffset = 5;
        public double ResearchPower = 2;

        public double CoresBase = 1;
        public double CoresPerLog = 0.25;
        public long DiscoveryCores = 10;
        public double[] HeadStartEnergy = { 0, 500, 5e3, 5e4, 5e5, 5e6 };
        public double MaxOfflineSeconds = 7 * 24 * 3600;

        // Engine Level: XP needed to go from level L to L + 1 is XpBase x L ^ XpExponent.
        public double XpBase = 22;
        public double XpExponent = 1.05;
        public double XpFlat = 20;
        public int MaxEngineLevel = 100;
        public double XpPerMachineLevel = 1;
        public double XpPerStorm = 6;
        public double XpPerPhenomenonStorm = 3;
        public double XpPerDiscovery = 40;
        public double XpPerModuleUnlock = 15;
        public double XpPerModuleUpgrade = 5;
        public double XpPerResearch = 10;
        public double XpPerPrestige = 80;
        public double XpPerUpgrade = 20;
        public double XpPerDecoration = 8;
        public double XpPerTournamentRun = 10;

        // Gems (premium currency) that can also be earned by playing.
        public long GemsPerLevel = 10;
        public long GemsPerDiscovery = 20;

        // Tournaments: your real engine, fixed rules.
        public int TournamentAttemptsPerDay = 3;
        public int TournamentAdAttemptsPerDay = 2;
        /// <summary>Each style item's multiplier counts at most 1 + this in tournament storms.</summary>
        public double TournamentStyleCap = 0.10;
        /// <summary>Gem rewards for last week's final tournament rank (index = bracket).</summary>
        public int[] TournamentRankLimits = { 1, 3, 10, 50, 100, int.MaxValue };
        public long[] TournamentRankGems = { 1000, 600, 350, 150, 80, 30 };

        /// <summary>Energy Surge ad: income multiplier and the most surge time that can be banked.</summary>
        public double SurgeMultiplier = 2;
        public double SurgeMaxSeconds = 3600;

        /// <summary>Decorations that fit on the starting island.</summary>
        public int BaseDecorCapacity = 20;
        public float BaseIslandRadius = 9.5f;

        /// <summary>Storm Cores in daily rewards grow by this share per Engine Level.</summary>
        public double DailyCoreGrowth = 0.05;
        /// <summary>A broken login streak can be won back with an ad after missing at most this many days...</summary>
        public int StreakRestoreMaxMissed = 2;
        /// <summary>...and only when it was at least this long.</summary>
        public int StreakRestoreMin = 2;
    }

    /// <summary>
    /// All game content: machines, boosts, phenomena, modules, research, weekly conditions, goals,
    /// Engine Level features, permanent upgrades and cosmetics. This is the single source of truth
    /// for balancing. The Cloud Code validator receives an export of the parts that affect storms.
    /// </summary>
    public sealed class Content
    {
        public const int CatCount = 5;
        public const int StatCount = (int)StatId.Count;

        /// <summary>Bump when a balance change alters storm scores so old clients are rejected by the server.</summary>
        public int Version = 3;

        public Balance B = new Balance();
        public StatInfo[] Stats;
        public MachineDef[] Machines;
        public int[] MilestoneLevels;
        public BoostDef[] Boosts;
        public PhenoDef[] Phenomena;
        public ModuleDef[] Modules;
        public ResearchDef[] Research;
        public WeeklyDef[] Weekly;
        public GoalDef[] Goals;
        public FeatureDef[] Features;
        public UpgradeDef[] Upgrades;
        public CosmeticDef[] Cosmetics;
        public BundleDef[] Bundles;
        public DecorationDef[] Decorations;
        public ExpansionDef[] Expansions;
        public AdPlacementDef[] AdPlacements;
        public ProductDef[] Products;
        /// <summary>Login rewards for streak days 1-7 (the cycle repeats).</summary>
        public DailyReward[] LoginRewards;
        public MissionDef[] Missions;
        /// <summary>Extra reward for finishing all of the day's missions.</summary>
        public DailyReward MissionBonus;

        readonly Dictionary<string, int> _machineIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _boostIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _phenoIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _moduleIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _researchIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _weeklyIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _featureIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _upgradeIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _cosmeticIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _bundleIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _decorIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _expansionIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _adIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _productIndex = new Dictionary<string, int>();
        readonly Dictionary<string, int> _missionIndex = new Dictionary<string, int>();

        public int MachineIndex(string id) => Lookup(_machineIndex, id);
        public int BoostIndex(string id) => Lookup(_boostIndex, id);
        public int PhenoIndex(string id) => Lookup(_phenoIndex, id);
        public int ModuleIndex(string id) => Lookup(_moduleIndex, id);
        public int ResearchIndex(string id) => Lookup(_researchIndex, id);
        public int WeeklyIndex(string id) => Lookup(_weeklyIndex, id);
        public int FeatureIndex(string id) => Lookup(_featureIndex, id);
        public int UpgradeIndex(string id) => Lookup(_upgradeIndex, id);
        public int CosmeticIndex(string id) => Lookup(_cosmeticIndex, id);
        public int BundleIndex(string id) => Lookup(_bundleIndex, id);
        public int DecorationIndex(string id) => Lookup(_decorIndex, id);
        public int ExpansionIndex(string id) => Lookup(_expansionIndex, id);
        public int AdIndex(string id) => Lookup(_adIndex, id);
        public int ProductIndex(string id) => Lookup(_productIndex, id);
        public int MissionIndex(string id) => Lookup(_missionIndex, id);

        /// <summary>The limited decoration featured in a given week (FOMO rotation), or -1.</summary>
        public int FeaturedDecoration(int week)
        {
            int limited = 0;
            foreach (var d in Decorations) if (d.Limited) limited++;
            if (limited == 0 || week < 0) return -1;
            int pick = week % limited;
            for (int i = 0; i < Decorations.Length; i++)
            {
                if (!Decorations[i].Limited) continue;
                if (pick-- == 0) return i;
            }
            return -1;
        }

        public FeatureDef Feature(string id)
        {
            int i = FeatureIndex(id);
            return i >= 0 ? Features[i] : null;
        }

        public static StatId CatStat(int cat) => (StatId)(cat + 1);
        public static int StatCat(StatId s) => s >= StatId.Temperature && s <= StatId.Electricity ? (int)s - 1 : -1;

        static int Lookup(Dictionary<string, int> map, string id) =>
            id != null && map.TryGetValue(id, out var i) ? i : -1;

        public void Index()
        {
            Fill(_machineIndex, Machines, m => m.Id);
            Fill(_boostIndex, Boosts, b => b.Id);
            Fill(_phenoIndex, Phenomena, p => p.Id);
            Fill(_moduleIndex, Modules, m => m.Id);
            Fill(_researchIndex, Research, r => r.Id);
            Fill(_weeklyIndex, Weekly, w => w.Id);
            Fill(_featureIndex, Features, f => f.Id);
            Fill(_upgradeIndex, Upgrades, u => u.Id);
            Fill(_cosmeticIndex, Cosmetics, c => c.Id);
            Fill(_bundleIndex, Bundles, b => b.Id);
            Fill(_decorIndex, Decorations, d => d.Id);
            Fill(_expansionIndex, Expansions, e => e.Id);
            Fill(_adIndex, AdPlacements, a => a.Id);
            Fill(_productIndex, Products, p => p.Id);
            Fill(_missionIndex, Missions, m => m.Id);
        }

        static void Fill<T>(Dictionary<string, int> map, T[] items, Func<T, string> id)
        {
            map.Clear();
            for (int i = 0; i < items.Length; i++) map[id(items[i])] = i;
        }

        public double MachineCost(int machine, int level)
        {
            var m = Machines[machine];
            double exponent = level * Math.Log(m.Growth) + 0.5 * level * (level - 1.0) * Math.Log(m.Super);
            return Math.Floor(m.BaseCost * Math.Exp(exponent));
        }

        /// <summary>Cost of buying <paramref name="count"/> levels starting at <paramref name="level"/>.</summary>
        public double MachineCost(int machine, int level, int count)
        {
            double total = 0;
            for (int i = 0; i < count; i++) total += MachineCost(machine, level + i);
            return total;
        }

        public int MilestonesReached(int level)
        {
            int n = 0;
            for (int i = 0; i < MilestoneLevels.Length; i++)
                if (level >= MilestoneLevels[i]) n++;
            return n;
        }

        /// <summary>Next milestone level above the given level, or -1.</summary>
        public int NextMilestone(int level)
        {
            for (int i = 0; i < MilestoneLevels.Length; i++)
                if (MilestoneLevels[i] > level) return MilestoneLevels[i];
            return -1;
        }

        public long ModuleUpgradeCost(int module, int level)
        {
            // Cost to go from level to level + 1.
            return (long)Math.Round(10 * Math.Pow(1.5, level - 1));
        }

        public long ResearchCost(int node, int level)
        {
            var r = Research[node];
            return (long)Math.Round(r.BaseCost * Math.Pow(r.CostGrowth, level));
        }

        public double UpgradeCost(int upgrade, int level)
        {
            var u = Upgrades[upgrade];
            return Math.Floor(u.BaseCost * Math.Pow(u.CostGrowth, level));
        }

        public long UpgradeCoreCost(int upgrade, int level)
        {
            var u = Upgrades[upgrade];
            return u.BaseCores <= 0 ? 0 : (long)Math.Round(u.BaseCores * Math.Pow(u.CoresGrowth, level));
        }

        /// <summary>XP needed to go from <paramref name="level"/> to the next Engine Level.</summary>
        public double XpToNext(int level) => Math.Round(B.XpBase * Math.Pow(Math.Max(1, level), B.XpExponent) + B.XpFlat);

        public static Content CreateDefault()
        {
            var c = new Content();
            c.Stats = BuildStats();
            c.MilestoneLevels = new[] { 25, 50, 100, 150, 200, 300, 400, 500, 600, 700, 800, 900, 1000 };

            // Unlock order follows the design: learn the Thunderstorm ingredients first (humidity,
            // cloud, charge), then temperature and wind open the advanced storm builds.
            c.Machines = new[]
            {
                new MachineDef
                {
                    Id = "pump", Name = "Pressure Pump", Icon = "pump", UnlockLevel = 1,
                    Desc = "Pressurises the chamber. Each level adds +1 Base Pressure.",
                    BaseCost = 10, Growth = 1.3, Super = 1.008,
                    Effects = new[] { FlatScaled(StatId.BasePressure, 1) },
                    MilestoneStat = StatId.BasePressure, MilestoneMult = 1.5
                },
                new MachineDef
                {
                    Id = "hydro", Name = "Hydro Pump", Icon = "water", Cat = (int)Cat.Humidity, UnlockLevel = 1,
                    Desc = "Pumps water into the air. Each level adds +5% Humidity.",
                    BaseCost = 40, Growth = 1.35, Super = 1.01,
                    Effects = new[] { AddScaled(StatId.Humidity, 0.05) },
                    MilestoneStat = StatId.Humidity, MilestoneMult = 1.5
                },
                new MachineDef
                {
                    Id = "condenser", Name = "Cloud Condenser", Icon = "cloud", Cat = (int)Cat.Cloud, UnlockLevel = 2,
                    Desc = "Condenses vapour into clouds. Each level adds +5% Cloud density.",
                    BaseCost = 250, Growth = 1.35, Super = 1.01,
                    Effects = new[] { AddScaled(StatId.Cloud, 0.05) },
                    MilestoneStat = StatId.Cloud, MilestoneMult = 1.5
                },
                new MachineDef
                {
                    Id = "tesla", Name = "Tesla Coil", Icon = "bolt", Cat = (int)Cat.Electricity, UnlockLevel = 3,
                    Desc = "Builds electrical charge. Each level adds +5% Electricity.",
                    BaseCost = 1500, Growth = 1.35, Super = 1.01,
                    Effects = new[] { AddScaled(StatId.Electricity, 0.05) },
                    MilestoneStat = StatId.Electricity, MilestoneMult = 1.5
                },
                new MachineDef
                {
                    Id = "furnace", Name = "Thermal Furnace", Icon = "heat", Cat = (int)Cat.Temperature, UnlockLevel = 5,
                    Desc = "Heats the air. Each level adds +5% Temperature.",
                    BaseCost = 12000, Growth = 1.35, Super = 1.01,
                    Effects = new[] { AddScaled(StatId.Temperature, 0.05) },
                    MilestoneStat = StatId.Temperature, MilestoneMult = 1.5
                },
                new MachineDef
                {
                    Id = "chamber", Name = "Storm Chamber", Icon = "chamber", UnlockLevel = 6,
                    Desc = "Reinforces the dome. +0.5s storm duration and +4% storm charge speed per level.",
                    BaseCost = 5000, Growth = 1.6, MaxLevel = 30,
                    Effects = new[] { FlatScaled(StatId.StormDuration, 0.5), AddScaled(StatId.StormChargeSpeed, 0.04) },
                    MilestoneMult = 0
                },
                new MachineDef
                {
                    Id = "turbine", Name = "Wind Turbine", Icon = "wind", Cat = (int)Cat.Wind, UnlockLevel = 7,
                    Desc = "Pushes air around. Each level adds +5% Wind.",
                    BaseCost = 90000, Growth = 1.35, Super = 1.01,
                    Effects = new[] { AddScaled(StatId.Wind, 0.05) },
                    MilestoneStat = StatId.Wind, MilestoneMult = 1.5
                },
            };

            c.Boosts = new[]
            {
                new BoostDef
                {
                    Id = "cloud_burst", Name = "Cloud Burst", Icon = "cloud",
                    Desc = "Cloud x2 for 20s. Cooldown 45s.",
                    Target = StatId.Cloud, Factor = 2, Duration = 20, Cooldown = 45,
                    UnlockMachine = "condenser", UnlockLevel = 10
                },
                new BoostDef
                {
                    Id = "arc_overload", Name = "Arc Overload", Icon = "bolt",
                    Desc = "Electricity x2.5 and lightning +50% for 15s. Cooldown 50s.",
                    Target = StatId.Electricity, Factor = 2.5, Duration = 15, Cooldown = 50,
                    UnlockMachine = "tesla", UnlockLevel = 10,
                    ExtraStat = StatId.LightningRate, ExtraFactor = 1.5
                },
                new BoostDef
                {
                    Id = "heat_surge", Name = "Heat Surge", Icon = "heat",
                    Desc = "Temperature x2 for 30s. Cooldown 60s.",
                    Target = StatId.Temperature, Factor = 2, Duration = 30, Cooldown = 60,
                    UnlockMachine = "furnace", UnlockLevel = 10
                },
                new BoostDef
                {
                    Id = "jetstream", Name = "Jetstream", Icon = "wind",
                    Desc = "Wind x3 for 10s. Cooldown 40s.",
                    Target = StatId.Wind, Factor = 3, Duration = 10, Cooldown = 40,
                    UnlockMachine = "turbine", UnlockLevel = 10
                },
            };

            // Order must match the Pheno* stats in StatId.
            c.Phenomena = new[]
            {
                new PhenoDef
                {
                    Id = "thunderstorm", Name = "Thunderstorm", Stat = StatId.PhenoThunderstorm,
                    Desc = "Clouds, charge and moisture in balance. The all-rounder.",
                    PerkText = "Lightning strikes 50% more often.",
                    Ingredients = new[] { new Ingredient(Cat.Humidity, 4), new Ingredient(Cat.Cloud, 4), new Ingredient(Cat.Electricity, 4) },
                    BaseMult = 1.18, Alpha = 0.2, Perk = PerkKind.LightningRate, PerkValue = 0.5
                },
                new PhenoDef
                {
                    Id = "heat_storm", Name = "Heat Storm", Stat = StatId.PhenoHeatStorm,
                    Desc = "Scorching updrafts. Temperature-focused build.",
                    PerkText = "Storm lasts 3s longer.",
                    Ingredients = new[] { new Ingredient(Cat.Temperature, 10), new Ingredient(Cat.Wind, 2) },
                    BaseMult = 1.21, Alpha = 0.2, Perk = PerkKind.ExtraDuration, PerkValue = 3
                },
                new PhenoDef
                {
                    Id = "monsoon", Name = "Monsoon", Stat = StatId.PhenoMonsoon,
                    Desc = "Endless rain carried by steady wind. Humidity-focused build.",
                    PerkText = "Storm intensity grows +4% every second.",
                    Ingredients = new[] { new Ingredient(Cat.Humidity, 14), new Ingredient(Cat.Cloud, 8), new Ingredient(Cat.Wind, 3) },
                    BaseMult = 1.24, Alpha = 0.2, Perk = PerkKind.RampingIntensity, PerkValue = 0.04
                },
                new PhenoDef
                {
                    Id = "cyclone", Name = "Cyclone", Stat = StatId.PhenoCyclone,
                    Desc = "A spinning wall of wind and water. Wind-focused build.",
                    PerkText = "Each lightning strike adds 50% more chain.",
                    Ingredients = new[] { new Ingredient(Cat.Wind, 14), new Ingredient(Cat.Humidity, 8) },
                    BaseMult = 1.3, Alpha = 0.2, Perk = PerkKind.ChainBoost, PerkValue = 0.5
                },
                new PhenoDef
                {
                    Id = "supercell", Name = "Supercell", Stat = StatId.PhenoSupercell,
                    Desc = "Heat, moisture and wind at extreme levels. Huge multipliers.",
                    PerkText = "Storm intensity x1.5.",
                    Ingredients = new[] { new Ingredient(Cat.Temperature, 30), new Ingredient(Cat.Humidity, 30), new Ingredient(Cat.Wind, 30) },
                    BaseMult = 1.48, Alpha = 0.2, Perk = PerkKind.IntensityMult, PerkValue = 1.5
                },
                new PhenoDef
                {
                    Id = "overcharge", Name = "Overcharge", Stat = StatId.PhenoOvercharge,
                    Desc = "Massive charge over a stable cloud layer. Electricity-focused build.",
                    PerkText = "Lightning strikes twice as often.",
                    Ingredients = new[] { new Ingredient(Cat.Electricity, 40), new Ingredient(Cat.Cloud, 16) },
                    BaseMult = 1.36, Alpha = 0.2, Perk = PerkKind.LightningRate, PerkValue = 1.0
                },
                new PhenoDef
                {
                    Id = "anomaly", Name = "Anomaly", Stat = StatId.PhenoAnomaly,
                    Desc = "Chaos Reactor storms tear reality apart past 40% instability.",
                    PerkText = "Massive multiplier, but the storm collapses at 100% instability.",
                    BaseMult = 3, Alpha = 0, Perk = PerkKind.Anomaly, Special = true
                },
            };

            c.Modules = new[]
            {
                new ModuleDef
                {
                    Id = "atmo_condenser", Name = "Atmospheric Condenser", Short = "Condenser", Icon = "water", UnlockCost = 10, UnlockLevel = 5,
                    Desc = "Humidity and Cloud x1.3 (+0.1/lv each).",
                    Effects = new[] { MultLin(StatId.Humidity, 1.3, 0.1), MultLin(StatId.Cloud, 1.3, 0.1) }
                },
                new ModuleDef
                {
                    Id = "tesla_array", Name = "Tesla Array", Short = "Tesla", Icon = "bolt", UnlockCost = 15, UnlockLevel = 6,
                    Desc = "Electricity x1.5 (+0.15/lv). +0.25 lightning strikes/s in storms.",
                    Effects = new[] { MultLin(StatId.Electricity, 1.5, 0.15), FlatLin(StatId.LightningRate, 0.25, 0.05) }
                },
                new ModuleDef
                {
                    Id = "thermal_core", Name = "Thermal Core", Short = "Thermal", Icon = "heat", UnlockCost = 20, UnlockLevel = 7,
                    Desc = "Temperature x1.5 (+0.15/lv). 10% of Temperature bonus also feeds Base Pressure.",
                    Effects = new[] { MultLin(StatId.Temperature, 1.5, 0.15), ConvertLin(StatId.Temperature, StatId.BasePressure, 0.10, 0.01) }
                },
                new ModuleDef
                {
                    Id = "jet_accelerator", Name = "Jet Accelerator", Short = "Jet", Icon = "wind", UnlockCost = 25, UnlockLevel = 9,
                    Desc = "Wind x1.5 (+0.15/lv). Storms charge 20% faster.",
                    Effects = new[] { MultLin(StatId.Wind, 1.5, 0.15), AddLin(StatId.StormChargeSpeed, 0.2, 0.02) }
                },
                new ModuleDef
                {
                    Id = "barometric_lens", Name = "Barometric Lens", Short = "Lens", Icon = "pump", UnlockCost = 30, UnlockLevel = 10,
                    Desc = "Base Pressure x2 (+0.25/lv).",
                    Effects = new[] { MultLin(StatId.BasePressure, 2, 0.25) }
                },
                new ModuleDef
                {
                    Id = "capacitor_bank", Name = "Capacitor Bank", Short = "Capacitor", Icon = "battery", UnlockCost = 40, UnlockLevel = 12,
                    Desc = "Storms last +4s (+0.5/lv). Chain per strike +2%.",
                    Effects = new[] { FlatLin(StatId.StormDuration, 4, 0.5), FlatLin(StatId.ChainPerStrike, 0.02, 0.005) }
                },
                new ModuleDef
                {
                    Id = "heat_exchanger", Name = "Heat Exchanger", Short = "Exchanger", Icon = "heat", UnlockCost = 60, UnlockLevel = 13, RequiresDiscovery = "heat_storm",
                    Desc = "Heat Storm x2 (+0.2/lv). 25% of Temperature bonus also feeds Wind.",
                    Effects = new[] { MultLin(StatId.PhenoHeatStorm, 2, 0.2), ConvertLin(StatId.Temperature, StatId.Wind, 0.25, 0.025) }
                },
                new ModuleDef
                {
                    Id = "rain_collector", Name = "Rain Collector", Short = "Rain", Icon = "water", UnlockCost = 60, UnlockLevel = 14, RequiresDiscovery = "monsoon",
                    Desc = "Monsoon x2 (+0.2/lv). Offline efficiency +10%.",
                    Effects = new[] { MultLin(StatId.PhenoMonsoon, 2, 0.2), FlatLin(StatId.OfflineEfficiency, 0.10, 0.01) }
                },
                new ModuleDef
                {
                    Id = "cyclone_core", Name = "Cyclone Core", Short = "Cyclone", Icon = "cyclone", UnlockCost = 80, UnlockLevel = 15, RequiresDiscovery = "cyclone",
                    Desc = "Cyclone x2 (+0.2/lv). Wind x1.25.",
                    Effects = new[] { MultLin(StatId.PhenoCyclone, 2, 0.2), MultLin(StatId.Wind, 1.25, 0.05) }
                },
                new ModuleDef
                {
                    Id = "overcharge_relay", Name = "Overcharge Relay", Short = "Relay", Icon = "bolt", UnlockCost = 100, UnlockLevel = 17, RequiresDiscovery = "overcharge",
                    Desc = "Overcharge x2 (+0.2/lv). Chain per strike +3%.",
                    Effects = new[] { MultLin(StatId.PhenoOvercharge, 2, 0.2), FlatLin(StatId.ChainPerStrike, 0.03, 0.005) }
                },
                new ModuleDef
                {
                    Id = "supercell_processor", Name = "Supercell Processor", Short = "Processor", Icon = "chip", UnlockCost = 120, UnlockLevel = 18, RequiresDiscovery = "supercell",
                    Desc = "All phenomena x1.2 (+0.04/lv).",
                    Effects = new[] { MultLin(StatId.PhenoAll, 1.2, 0.04) }
                },
                new ModuleDef
                {
                    Id = "chaos_reactor", Name = "Chaos Reactor", Short = "Chaos", Icon = "chaos", UnlockCost = 150, UnlockLevel = 20, RequiresPrestiges = 1,
                    Desc = "Storm Power x2 (+0.25/lv). Storms build instability: Anomaly past 40%, collapse at 100%.",
                    Effects = new[] { MultLin(StatId.GlobalPower, 2, 0.25), FlatLin(StatId.InstabilityRate, 0.10, -0.004) }
                },
            };

            c.Research = new[]
            {
                new ResearchDef
                {
                    Id = "engine_efficiency", Name = "Engine Efficiency", Icon = "gear", UnlockLevel = 11,
                    Desc = "+25% Storm Power per level.", BaseCost = 1, CostGrowth = 1.35, MaxLevel = 500,
                    Effects = new[] { AddScaled(StatId.GlobalPower, 0.25) }
                },
                new ResearchDef
                {
                    Id = "automation", Name = "Storm Automation", Icon = "auto", UnlockLevel = 11,
                    Desc = "Storms unleash themselves when charged, even while you are away (with the Offline Generator).", BaseCost = 3, CostGrowth = 1, MaxLevel = 1
                },
                new ResearchDef
                {
                    Id = "module_bay", Name = "Module Bay", Icon = "chip", UnlockLevel = 12,
                    Desc = "+1 module slot.", BaseCost = 5, CostGrowth = 3, MaxLevel = 3,
                    Effects = new[] { FlatScaled(StatId.ModuleSlots, 1) }
                },
                new ResearchDef
                {
                    Id = "long_storms", Name = "Long Storms", Icon = "clock", UnlockLevel = 12,
                    Desc = "+2s storm duration per level.", BaseCost = 2, CostGrowth = 1.7, MaxLevel = 5,
                    Effects = new[] { FlatScaled(StatId.StormDuration, 2) }
                },
                new ResearchDef
                {
                    Id = "quick_charge", Name = "Quick Charge", Icon = "charge", UnlockLevel = 12,
                    Desc = "Storms charge 10% faster per level.", BaseCost = 2, CostGrowth = 1.6, MaxLevel = 5,
                    Effects = new[] { AddScaled(StatId.StormChargeSpeed, 0.1) }
                },
                new ResearchDef
                {
                    Id = "hydro_mastery", Name = "Hydro Mastery", Icon = "water", UnlockLevel = 13,
                    Desc = "Humidity additive bonuses are 10% more effective per level.", BaseCost = 2, CostGrowth = 1.6, MaxLevel = 10,
                    Effects = new[] { AddEffPow(StatId.Humidity, 1.1) }
                },
                new ResearchDef
                {
                    Id = "cloud_mastery", Name = "Cloud Mastery", Icon = "cloud", UnlockLevel = 13,
                    Desc = "Cloud additive bonuses are 10% more effective per level.", BaseCost = 2, CostGrowth = 1.6, MaxLevel = 10,
                    Effects = new[] { AddEffPow(StatId.Cloud, 1.1) }
                },
                new ResearchDef
                {
                    Id = "charge_mastery", Name = "Charge Mastery", Icon = "bolt", UnlockLevel = 13,
                    Desc = "Electricity additive bonuses are 10% more effective per level.", BaseCost = 2, CostGrowth = 1.6, MaxLevel = 10,
                    Effects = new[] { AddEffPow(StatId.Electricity, 1.1) }
                },
                new ResearchDef
                {
                    Id = "thermal_mastery", Name = "Thermal Mastery", Icon = "heat", UnlockLevel = 14,
                    Desc = "Temperature additive bonuses are 10% more effective per level.", BaseCost = 2, CostGrowth = 1.6, MaxLevel = 10,
                    Effects = new[] { AddEffPow(StatId.Temperature, 1.1) }
                },
                new ResearchDef
                {
                    Id = "aero_mastery", Name = "Aero Mastery", Icon = "wind", UnlockLevel = 14,
                    Desc = "Wind additive bonuses are 10% more effective per level.", BaseCost = 2, CostGrowth = 1.6, MaxLevel = 10,
                    Effects = new[] { AddEffPow(StatId.Wind, 1.1) }
                },
                new ResearchDef
                {
                    Id = "offline_capacitor", Name = "Offline Capacitor", Icon = "battery", UnlockLevel = 14,
                    Desc = "+2h offline time cap and +5% offline efficiency per level (needs the Offline Generator).", BaseCost = 3, CostGrowth = 1.8, MaxLevel = 5,
                    Effects = new[] { FlatScaled(StatId.OfflineCapHours, 2), FlatScaled(StatId.OfflineEfficiency, 0.05) }
                },
                new ResearchDef
                {
                    Id = "cooldown_tuning", Name = "Cooldown Tuning", Icon = "clock", UnlockLevel = 15,
                    Desc = "Boost cooldowns -6% per level (multiplicative).", BaseCost = 2, CostGrowth = 1.7, MaxLevel = 5,
                    Effects = new[] { MultPow(StatId.BoostCooldown, 0.94) }
                },
                new ResearchDef
                {
                    Id = "core_extraction", Name = "Core Extraction", Icon = "core", UnlockLevel = 15,
                    Desc = "+20% Storm Cores from storms per level.", BaseCost = 3, CostGrowth = 1.7, MaxLevel = 5,
                    Effects = new[] { AddScaled(StatId.CoreGain, 0.2) }
                },
                new ResearchDef
                {
                    Id = "boost_amplifier", Name = "Boost Amplifier", Icon = "boost", UnlockLevel = 16,
                    Desc = "Boost effects +10% stronger per level.", BaseCost = 3, CostGrowth = 1.7, MaxLevel = 5,
                    Effects = new[] { AddScaled(StatId.BoostPower, 0.1) }
                },
                new ResearchDef
                {
                    Id = "head_start", Name = "Head Start", Icon = "energy", UnlockLevel = 16,
                    Desc = "Start every run with more Energy (500, 5K, 50K, 500K, 5M).", BaseCost = 2, CostGrowth = 2.2, MaxLevel = 5
                },
                new ResearchDef
                {
                    Id = "synergy_resonance", Name = "Synergy Resonance", Icon = "synergy", UnlockLevel = 18,
                    Desc = "Every active phenomenon increases all others by +3% per level.", BaseCost = 4, CostGrowth = 1.8, MaxLevel = 5,
                    Effects = new[] { FlatScaled(StatId.Resonance, 0.03) }
                },
                new ResearchDef
                {
                    Id = "pressure_conversion", Name = "Pressure Conversion", Icon = "pump", UnlockLevel = 20,
                    Desc = "5% of the Temperature bonus per level also feeds Base Pressure.", BaseCost = 3, CostGrowth = 1.8, MaxLevel = 5,
                    Effects = new[] { ConvertScaled(StatId.Temperature, StatId.BasePressure, 0.05) }
                },
            };

            c.Weekly = new[]
            {
                new WeeklyDef
                {
                    Id = "extreme_gradient", Name = "Extreme Temperature Gradient",
                    Lines = new[] { "+Temperature x1.4", "+Lightning rate x1.1", "-Humidity x0.85" },
                    Effects = new[] { WeeklyMult(StatId.Temperature, 1.4), WeeklyMult(StatId.LightningRate, 1.1), WeeklyMult(StatId.Humidity, 0.85) }
                },
                new WeeklyDef
                {
                    Id = "saturated_atmosphere", Name = "Saturated Atmosphere",
                    Lines = new[] { "+Humidity x1.5", "+Cloud x1.2", "-Wind x0.8" },
                    Effects = new[] { WeeklyMult(StatId.Humidity, 1.5), WeeklyMult(StatId.Cloud, 1.2), WeeklyMult(StatId.Wind, 0.8) }
                },
                new WeeklyDef
                {
                    Id = "high_wind_season", Name = "High Wind Season",
                    Lines = new[] { "+Wind x1.5", "+Cyclone x1.5", "-Temperature x0.85" },
                    Effects = new[] { WeeklyMult(StatId.Wind, 1.5), WeeklyMult(StatId.PhenoCyclone, 1.5), WeeklyMult(StatId.Temperature, 0.85) }
                },
                new WeeklyDef
                {
                    Id = "electrical_instability", Name = "Electrical Instability",
                    Lines = new[] { "+Electricity x1.6", "+Chain per strike x1.5", "-Cloud x0.85" },
                    Effects = new[] { WeeklyMult(StatId.Electricity, 1.6), WeeklyMult(StatId.ChainPerStrike, 1.5), WeeklyMult(StatId.Cloud, 0.85) }
                },
                new WeeklyDef
                {
                    Id = "monsoon_season", Name = "Monsoon Season",
                    Lines = new[] { "+Monsoon x2", "+Humidity x1.3", "-Electricity x0.8" },
                    Effects = new[] { WeeklyMult(StatId.PhenoMonsoon, 2), WeeklyMult(StatId.Humidity, 1.3), WeeklyMult(StatId.Electricity, 0.8) }
                },
                new WeeklyDef
                {
                    Id = "heat_dome", Name = "Heat Dome",
                    Lines = new[] { "+Heat Storm x2", "+Temperature x1.3", "-Cloud x0.8" },
                    Effects = new[] { WeeklyMult(StatId.PhenoHeatStorm, 2), WeeklyMult(StatId.Temperature, 1.3), WeeklyMult(StatId.Cloud, 0.8) }
                },
                new WeeklyDef
                {
                    Id = "still_air", Name = "Still Air",
                    Lines = new[] { "+Storms last 4s longer", "+Boost effects x1.25", "-Wind x0.8" },
                    Effects = new[] { WeeklyFlat(StatId.StormDuration, 4), WeeklyMult(StatId.BoostPower, 1.25), WeeklyMult(StatId.Wind, 0.8) }
                },
                new WeeklyDef
                {
                    Id = "supercell_outbreak", Name = "Supercell Outbreak",
                    Lines = new[] { "+Supercell x1.5", "+Storm intensity x1.1", "-Electricity x0.9" },
                    Effects = new[] { WeeklyMult(StatId.PhenoSupercell, 1.5), WeeklyMult(StatId.StormIntensity, 1.1), WeeklyMult(StatId.Electricity, 0.9) }
                },
            };

            c.Features = new[]
            {
                Feature("storms", "Storm Chamber", "Charge and unleash storms.", "storm", 1),
                Feature("daily", "Daily Rewards", "A gift for every day you visit, a growing streak and three fresh missions each day.", "gift", 2),
                Feature("atmosphere", "Atmosphere Lab", "See exactly how Storm Power is calculated and forecast your next storm.", "atmosphere", 3),
                Feature("customize", "Style Shop", "Skins, storm clouds, lightning, worlds and titles. Each one boosts a stat.", "brush", 4),
                Feature("modules", "Module Workshop", "Equip modules that bend the formula. Paid with Storm Cores.", "modules", 5),
                Feature("island", "Island Decor", "Decorate your island for bonus Energy and show it off to visitors.", "island", 6),
                Feature("tournament", "Weekly Tournament", "Take your engine into the weekly tournament and climb the global leaderboards.", "trophy", 8),
                Feature("weekly", "Weekly Conditions", "A new atmospheric condition every Monday changes the best build.", "calendar", 8),
                Feature("offline", "Offline Generator", "Keep producing while you are away. Expensive to build.", "battery", 10),
                Feature("research", "Research Lab", "Reset the atmosphere for Research and permanent upgrades.", "research", 11),
            };

            c.Upgrades = new[]
            {
                new UpgradeDef
                {
                    Id = "offline_generator", Name = "Offline Generator", Icon = "battery", Feature = "offline",
                    Desc = "Keeps the engine running while you are away. Level 1: 1 hour at 20% efficiency. Each level adds +1 hour and +5% efficiency. Permanent.",
                    MaxLevel = 8, BaseCost = 5e8, CostGrowth = 4, BaseCores = 40, CoresGrowth = 1.6,
                    Effects = new[] { FlatScaled(StatId.OfflineCapHours, 1), FlatLin(StatId.OfflineEfficiency, 0.20, 0.05) }
                },
            };

            c.Cosmetics = new[]
            {
                // Machine skins: each has its own shape and boosts one stat.
                Skin("skin_classic", "Classic Workshop", "classic", 0xFF8A3D, 0x2C416B, 0xFFC83D, 1, 0, null, isDefault: true,
                    desc: "The trusty original: round deck, glass dome, one very proud antenna."),
                Skin("skin_ocean", "Ocean Breeze", "tide", 0x2FC7B0, 0x1E3A6E, 0x7FE7FF, 4, 80, M(StatId.Humidity, 1.10),
                    desc: "A ship-shape engine with portholes and brass rails. Humidity x1.10."),
                Skin("skin_emerald", "Emerald Works", "gear", 0x3DBE6B, 0x2E4636, 0xC8FF6A, 6, 120, M(StatId.Cloud, 1.10),
                    desc: "Cog-toothed deck and a gear crown. Cloud x1.10."),
                Skin("skin_royal", "Royal Violet", "royal", 0x8E5CFF, 0x3A2466, 0xFFD166, 9, 200, M(StatId.Electricity, 1.15),
                    desc: "Octagonal throne with a jewelled crown. Electricity x1.15."),
                Skin("skin_midnight", "Midnight Neon", "neon", 0x3A3F5C, 0x14161F, 0x39F3FF, 12, 250, M(StatId.Wind, 1.15),
                    desc: "Sharp angles and glowing strips. Wind x1.15."),
                Skin("skin_candy", "Candy Pop", "candy", 0xFF7AB6, 0x5AC8B0, 0xFFFFFF, 15, 250, M(StatId.Temperature, 1.15),
                    desc: "Gumdrops, swirls and a lollipop antenna. Temperature x1.15."),
                Skin("skin_gold", "Solar Gold", "solar", 0xF3C13A, 0x6B4A1A, 0xFFF1B0, 20, 400, M(StatId.BasePressure, 1.20), prestiges: 1,
                    desc: "A radiant sun engine with golden rays. Base Pressure x1.20."),
                Skin("skin_chaos", "Chaos Core", "chaos", 0xE04BD8, 0x1A0F24, 0x7CFF6B, 1, 0, M(StatId.GlobalPower, 1.15), discovery: "anomaly",
                    desc: "Spiked and unstable, with shards orbiting the dome. Engine Efficiency x1.15."),
                Skin("skin_steam", "Brass Steamworks", "steam", 0xC9853A, 0x4A2F1E, 0xFFD27A, 5, 600, M(StatId.BasePressure, 1.30), exclusive: true,
                    desc: "Exclusive. Spinning gears, rivets and an onion dome. Base Pressure x1.30."),
                Skin("skin_crystal", "Crystal Spire", "crystal", 0x7FE7FF, 0x2B3F7A, 0xE8FBFF, 8, 750, M(StatId.Electricity, 1.30), exclusive: true,
                    desc: "Exclusive. A faceted crystal engine ringed with shards. Electricity x1.30."),
                Skin("skin_rocket", "Sky Rocket", "rocket", 0xFF4F5E, 0xEDF3FA, 0x3B82F6, 10, 900, M(StatId.Wind, 1.30), exclusive: true,
                    desc: "Exclusive. Tail fins, a capsule dome and orbit rings. Wind x1.30."),
                Skin("skin_magma", "Magma Forge", "magma", 0xFF6A1A, 0x2A1A18, 0xFFC23D, 1, 0, M(StatId.Temperature, 1.25), bundle: "bundle_volcano",
                    desc: "Volcano bundle. Basalt deck with glowing lava seams. Temperature x1.25."),
                Skin("skin_blossom", "Blossom Pagoda", "pagoda", 0xE8495F, 0x3A2A2A, 0xFFD1E1, 1, 0, M(StatId.Humidity, 1.25), bundle: "bundle_sakura",
                    desc: "Sakura bundle. Tiered pagoda roofs over the dome. Humidity x1.25."),
                Skin("skin_cyber", "Cyber Core", "cyber", 0x1E1B3A, 0x0D0B1F, 0x39F3FF, 1, 0, M(StatId.Electricity, 1.25), bundle: "bundle_neon",
                    desc: "Neon bundle. Hexagonal circuitry with holographic rings. Electricity x1.25."),

                // Storm clouds inside the dome.
                Cloud("cloud_classic", "Silver Lining", "puffy", 0xEEF4FF, 0x4A4F82, 0x7FD3FF, 1, 0, null, isDefault: true,
                    desc: "Soft, fluffy, dependable."),
                Cloud("cloud_candy", "Cotton Candy", "puffy", 0xFFD1F0, 0x9B6BFF, 0xFF8BF0, 4, 60, M(StatId.Cloud, 1.10),
                    desc: "Sweet pink puffs. Cloud x1.10."),
                Cloud("cloud_toxic", "Toxic Fog", "wisp", 0xC9FF9E, 0x2F6B3A, 0x7CFF6B, 9, 150, M(StatId.StormChargeSpeed, 1.15),
                    desc: "Bubbling green wisps. Storm charge speed x1.15."),
                Cloud("cloud_inferno", "Inferno", "flame", 0xFFC38A, 0x7A2A1A, 0xFF5A3C, 1, 0, M(StatId.Temperature, 1.15), discovery: "heat_storm",
                    desc: "Flickering flame clouds. Temperature x1.15."),
                Cloud("cloud_glacier", "Glacier", "faceted", 0xE0F7FF, 0x3E6E9E, 0x9FE8FF, 1, 0, M(StatId.Humidity, 1.15), discovery: "monsoon",
                    desc: "Icy crystalline clouds. Humidity x1.15."),
                Cloud("cloud_void", "The Void", "wisp", 0x6E5A9E, 0x0E0A1C, 0xB16BFF, 1, 0, M(StatId.PhenoAll, 1.10), discovery: "supercell",
                    desc: "Clouds from somewhere else. All phenomena x1.10."),
                Cloud("cloud_tempest", "Tempest", "faceted", 0x9FB3FF, 0x1C2350, 0x5E7BFF, 7, 500, M(StatId.StormDuration, 1.20), exclusive: true,
                    desc: "Exclusive. A crackling thunderhead. Storm duration x1.20."),
                Cloud("cloud_ash", "Ash Plume", "flame", 0x9A8C88, 0x2B2220, 0xFF7A2A, 1, 0, M(StatId.Temperature, 1.12), bundle: "bundle_volcano",
                    desc: "Volcano bundle. Smoky ash with embers. Temperature x1.12."),
                Cloud("cloud_petal", "Petal Drift", "petal", 0xFFE0EC, 0xE97AA0, 0xFFB7D0, 1, 0, M(StatId.Humidity, 1.12), bundle: "bundle_sakura",
                    desc: "Sakura bundle. Clouds of drifting petals. Humidity x1.12."),
                Cloud("cloud_synth", "Synthwave", "cubes", 0x7A5CFF, 0x1A1240, 0xFF3DDC, 1, 0, M(StatId.Electricity, 1.12), bundle: "bundle_neon",
                    desc: "Neon bundle. Blocky digital clouds. Electricity x1.12."),

                // Lightning.
                Bolt("bolt_classic", "Golden Bolt", "classic", 0xFFE36B, 0xFFFFFF, 0xFFB020, 1, 0, null, isDefault: true,
                    desc: "Bright, loud and golden."),
                Bolt("bolt_electric", "Electric Blue", "classic", 0x7FDBFF, 0xFFFFFF, 0x2F8BFF, 5, 60, M(StatId.LightningRate, 1.10),
                    desc: "Crisp blue arcs. Lightning rate x1.10."),
                Bolt("bolt_plasma", "Plasma Purple", "thick", 0xD49BFF, 0xFFFFFF, 0x8E3CFF, 9, 150, M(StatId.ChainPerStrike, 1.15),
                    desc: "Heavy plasma columns. Chain per strike x1.15."),
                Bolt("bolt_emerald", "Emerald Arc", "zigzag", 0x9BFFB5, 0xFFFFFF, 0x2EDC6B, 1, 0, M(StatId.Electricity, 1.10), discovery: "thunderstorm",
                    desc: "Jagged green arcs. Electricity x1.10."),
                Bolt("bolt_crimson", "Crimson Fury", "zigzag", 0xFF8A8A, 0xFFFFFF, 0xFF2A3C, 14, 250, M(StatId.StormIntensity, 1.10),
                    desc: "Furious red forks. Storm intensity x1.10."),
                Bolt("bolt_prism", "Prism", "twin", 0xFFFFFF, 0xFFFFFF, 0xFF8BF0, 1, 0, M(StatId.LightningRate, 1.20), discovery: "overcharge",
                    desc: "Twin rainbow bolts. Lightning rate x1.20."),
                Bolt("bolt_hammer", "Thunder Hammer", "thick", 0xFFF3A0, 0xFFFFFF, 0x5AC8FF, 11, 450, M(StatId.ChainPerStrike, 1.25), exclusive: true,
                    desc: "Exclusive. Each strike lands like a hammer. Chain per strike x1.25."),
                Bolt("bolt_lava", "Lava Lash", "thick", 0xFFB25A, 0xFFF0C0, 0xFF4A10, 1, 0, M(StatId.StormIntensity, 1.10), bundle: "bundle_volcano",
                    desc: "Volcano bundle. Molten whips. Storm intensity x1.10."),
                Bolt("bolt_blossom", "Blossom Spark", "twin", 0xFFC2DA, 0xFFFFFF, 0xFF6FA5, 1, 0, M(StatId.LightningRate, 1.12), bundle: "bundle_sakura",
                    desc: "Sakura bundle. Twin pink sparks. Lightning rate x1.12."),
                Bolt("bolt_neon", "Neon Pulse", "zigzag", 0x7FFFF4, 0xFFFFFF, 0xFF3DDC, 1, 0, M(StatId.ChainPerStrike, 1.12), bundle: "bundle_neon",
                    desc: "Neon bundle. Square-wave lightning. Chain per strike x1.12."),

                // Worlds: sky, terrain and props.
                World("world_meadow", "Sunny Meadow", "meadow", 0x6CCB4E, 0x3FA9EE, 0xBFE9FF, 1, 0, null, isDefault: true,
                    desc: "Green hills and blue skies."),
                World("world_desert", "Canyon Desert", "desert", 0xE9B872, 0xF08A4B, 0xFFE3B0, 7, 150, M(StatId.Temperature, 1.10),
                    desc: "Red rock and cacti under an orange sky. Temperature x1.10."),
                World("world_snow", "Frost Peaks", "snow", 0xEAF6FF, 0x6FA8DC, 0xDDEFFF, 10, 150, M(StatId.Wind, 1.10),
                    desc: "Snowfields and pines. Wind x1.10."),
                World("world_tropic", "Tropical Isle", "tropic", 0xF2DFA0, 0x35C0E6, 0xB8F2FF, 13, 300, M(StatId.Humidity, 1.15),
                    desc: "Palm trees and a turquoise sea. Humidity x1.15."),
                World("world_night", "Starry Night", "night", 0x2E4A3C, 0x0B1030, 0x3A3F7A, 16, 300, M(StatId.Electricity, 1.15),
                    desc: "A starlit sky and glowing flowers. Electricity x1.15."),
                World("world_alien", "Alien Planet", "alien", 0x8E5CFF, 0x1B0F3A, 0xFF8BF0, 20, 800, M(StatId.GlobalPower, 1.10), exclusive: true,
                    desc: "Exclusive. Crystal forests under a violet sky. Engine Efficiency x1.10."),
                World("world_volcano", "Volcano Isle", "volcano", 0x4A3A36, 0xB0302A, 0xFF9A5A, 1, 0, M(StatId.Temperature, 1.20), bundle: "bundle_volcano",
                    desc: "Volcano bundle. Black rock, lava pools and a burning sky. Temperature x1.20."),
                World("world_sakura", "Sakura Garden", "sakura", 0x8FD18A, 0xF4A6C8, 0xFFE6F0, 1, 0, M(StatId.Humidity, 1.20), bundle: "bundle_sakura",
                    desc: "Sakura bundle. Cherry blossoms and a soft pink sky. Humidity x1.20."),
                World("world_neon", "Neon Grid", "neon", 0x1B1B3A, 0x0A0620, 0x6A2BD8, 1, 0, M(StatId.Electricity, 1.20), bundle: "bundle_neon",
                    desc: "Neon bundle. Glowing grid ground under a synthwave sky. Electricity x1.20."),

                // Titles: shown on the leaderboards; each adds Storm Core gain.
                Title("title_chaser", "Storm Chaser", 1, null, null, isDefault: true),
                Title("title_thunder", "Thunderlord", 1, "thunderstorm", M(StatId.CoreGain, 1.05)),
                Title("title_scorcher", "Scorcher", 1, "heat_storm", M(StatId.CoreGain, 1.05)),
                Title("title_rainmaker", "Rainmaker", 1, "monsoon", M(StatId.CoreGain, 1.05)),
                Title("title_whisperer", "Wind Whisperer", 1, "cyclone", M(StatId.CoreGain, 1.05)),
                Title("title_architect", "Supercell Architect", 1, "supercell", M(StatId.CoreGain, 1.08)),
                Title("title_overcharger", "Overcharger", 1, "overcharge", M(StatId.CoreGain, 1.08)),
                Title("title_chaos", "Chaos Engineer", 1, "anomaly", M(StatId.CoreGain, 1.10)),
                Title("title_veteran", "Veteran Engineer", 20, null, M(StatId.CoreGain, 1.05)),
                Title("title_legend", "Storm Legend", 40, null, M(StatId.CoreGain, 1.10)),
                Title("title_champion", "Tournament Champion", 1, null, M(StatId.CoreGain, 1.10), gems: 1500, exclusive: true),
                Title("title_founder", "Founder", 1, null, M(StatId.CoreGain, 1.05), bundle: "starter_pack"),
                Title("title_firelord", "Firelord", 1, null, M(StatId.CoreGain, 1.05), bundle: "bundle_volcano"),
                Title("title_zen", "Zen Master", 1, null, M(StatId.CoreGain, 1.05), bundle: "bundle_sakura"),
                Title("title_neonghost", "Neon Ghost", 1, null, M(StatId.CoreGain, 1.05), bundle: "bundle_neon"),
            };

            // Complete environments that can only be earned by watching rewarded ads.
            c.Bundles = new[]
            {
                new BundleDef
                {
                    Id = "bundle_volcano", Name = "Volcano Forge", AdsRequired = 12, UnlockLevel = 6, Primary = 0xB0302A, Accent = 0xFFB020,
                    Desc = "Magma Forge skin, Ash Plume clouds, Lava Lash lightning, the Volcano Isle world, the Firelord title and two volcanic decorations.",
                    Cosmetics = new[] { "skin_magma", "cloud_ash", "bolt_lava", "world_volcano", "title_firelord" },
                    Decorations = new[] { "dec_lavapool", "dec_obsidian" }
                },
                new BundleDef
                {
                    Id = "bundle_sakura", Name = "Sakura Garden", AdsRequired = 16, UnlockLevel = 9, Primary = 0xE97AA0, Accent = 0xFFE6F0,
                    Desc = "Blossom Pagoda skin, Petal Drift clouds, Blossom Spark lightning, the Sakura Garden world, the Zen Master title, koi and a stone lantern.",
                    Cosmetics = new[] { "skin_blossom", "cloud_petal", "bolt_blossom", "world_sakura", "title_zen" },
                    Decorations = new[] { "dec_koi", "dec_toro" }
                },
                new BundleDef
                {
                    Id = "bundle_neon", Name = "Neon Nights", AdsRequired = 20, UnlockLevel = 12, Primary = 0x6A2BD8, Accent = 0x39F3FF,
                    Desc = "Cyber Core skin, Synthwave clouds, Neon Pulse lightning, the Neon Grid world, the Neon Ghost title, a neon sign and a holo tree.",
                    Cosmetics = new[] { "skin_cyber", "cloud_synth", "bolt_neon", "world_neon", "title_neonghost" },
                    Decorations = new[] { "dec_neonsign", "dec_holotree" }
                },
            };

            c.Decorations = new[]
            {
                Decor("dec_flowerbed", "Flower Bed", DecorKind.Land, "flowerbed", 4, 25, 0, 0.01, 0xFF7AB6, 0xFFE36B, "A cheerful patch of flowers."),
                Decor("dec_bench", "Park Bench", DecorKind.Land, "bench", 4, 30, 0, 0.01, 0x8A5A2B, 0x2C416B, "For watching storms roll in."),
                Decor("dec_lamppost", "Lamp Post", DecorKind.Land, "lamppost", 5, 40, 0, 0.01, 0x2C416B, 0xFFE38A, "Glows brighter when a storm arrives."),
                Decor("dec_gnome", "Storm Gnome", DecorKind.Land, "gnome", 5, 60, 0, 0.01, 0xFF5A4E, 0x3A8DFF, "Wears a tiny lightning-rod hat."),
                Decor("dec_kite", "Storm Kite", DecorKind.Air, "kite", 6, 70, 0, 0.01, 0xFF5A4E, 0xFFC83D, "Dances in the wind above the island."),
                Decor("dec_pond", "Pond", DecorKind.Land, "pond", 6, 100, 0, 0.01, 0x35C0E6, 0x6CCB4E, "Room for three water decorations.", maxCopies: 3),
                Decor("dec_lilypads", "Lily Pads", DecorKind.Water, "lilypads", 6, 30, 0, 0.01, 0x3DBE6B, 0xFF9EC8, "Floats on a pond."),
                Decor("dec_ducks", "Rubber Ducks", DecorKind.Water, "ducks", 6, 40, 0, 0.01, 0xFFD23F, 0xFF8A3D, "Squeak."),
                Decor("dec_mushroom", "Mushroom Ring", DecorKind.Land, "mushroom", 6, 80, 0, 0.01, 0xFF5A4E, 0xFFFFFF, "Appears after every good rain."),
                Decor("dec_windmill", "Tiny Windmill", DecorKind.Land, "windmill", 7, 120, 0, 0.02, 0xEDF3FA, 0xB0302A, "Spins faster with the Wind."),
                Decor("dec_vane", "Weather Vane", DecorKind.Land, "vane", 7, 90, 0, 0.01, 0x5B6F8E, 0xFFC83D, "Always points into the storm."),
                Decor("dec_fountain", "Fountain", DecorKind.Water, "fountain", 8, 100, 0, 0.02, 0xDCE6F2, 0x7FDBFF, "Sprays in the middle of a pond."),
                Decor("dec_balloon", "Hot Air Balloon", DecorKind.Air, "balloon", 8, 150, 0, 0.02, 0xFF5A4E, 0xFFC83D, "Drifts lazily around the island."),
                Decor("dec_statue", "Storm Titan", DecorKind.Land, "statue", 8, 0, 150, 0.03, 0xB8C4D6, 0xFFC83D, "A stone giant holding a lightning bolt.", maxCopies: 2),
                Decor("dec_crystal", "Crystal Cluster", DecorKind.Land, "crystal", 10, 0, 120, 0.02, 0x7FE7FF, 0xB99CFF, "Hums with stored charge."),
                Decor("dec_lanterns", "Sky Lanterns", DecorKind.Air, "lanterns", 10, 0, 120, 0.02, 0xFFB020, 0xFFE9A8, "Warm lights floating in the sky."),
                Decor("dec_sailboat", "Toy Sailboat", DecorKind.Water, "sailboat", 10, 0, 80, 0.02, 0xFFFFFF, 0x3A8DFF, "Sails in circles on a pond."),
                Decor("dec_beacon", "Lightning Beacon", DecorKind.Land, "beacon", 12, 0, 200, 0.03, 0x2C416B, 0x7FDBFF, "Crackles with sparks.", maxCopies: 2),
                Decor("dec_arch", "Rainbow Arch", DecorKind.Land, "arch", 14, 0, 250, 0.03, 0xFF5A4E, 0x3A8DFF, "A rainbow you can walk under.", maxCopies: 2),
                Decor("dec_blimp", "Weather Blimp", DecorKind.Air, "blimp", 15, 0, 300, 0.03, 0xEDF3FA, 0xFF8A3D, "Broadcasts the forecast.", maxCopies: 1),
                Decor("dec_whale", "Cloud Whale", DecorKind.Air, "whale", 18, 0, 600, 0.05, 0x9FC4FF, 0xFFFFFF, "Exclusive. A gentle giant made of cloud.", maxCopies: 1, exclusive: true),
                // Limited: one is featured each week, then it is gone until it rotates back.
                Decor("dec_snowman", "Snowman", DecorKind.Land, "snowman", 4, 0, 80, 0.02, 0xFFFFFF, 0xFF8A3D, "Limited. Never melts, somehow.", limited: true),
                Decor("dec_pumpkin", "Storm-o'-Lantern", DecorKind.Land, "pumpkin", 4, 0, 80, 0.02, 0xFF8A3D, 0xFFE36B, "Limited. Grins at thunder.", limited: true),
                Decor("dec_totem", "Thunder Totem", DecorKind.Land, "totem", 4, 0, 100, 0.02, 0x8A5A2B, 0x39D3E6, "Limited. Carved by ancient storm chasers.", limited: true),
                Decor("dec_topiary", "Storm Topiary", DecorKind.Land, "topiary", 4, 0, 100, 0.02, 0x3DBE6B, 0xFFC83D, "Limited. A hedge trimmed into a lightning bolt.", limited: true),
                // Bundle decorations (ads only).
                Decor("dec_lavapool", "Lava Pool", DecorKind.Land, "lavapool", 1, 0, 0, 0.03, 0xFF6A1A, 0x2A1A18, "Volcano bundle. Bubbling and very warm.", bundle: "bundle_volcano"),
                Decor("dec_obsidian", "Obsidian Totem", DecorKind.Land, "obsidian", 1, 0, 0, 0.02, 0x2A1A18, 0xFF6A1A, "Volcano bundle. Glassy black stone.", bundle: "bundle_volcano"),
                Decor("dec_koi", "Koi Fish", DecorKind.Water, "koi", 1, 0, 0, 0.03, 0xFF8A3D, 0xFFFFFF, "Sakura bundle. Lucky fish for your pond.", bundle: "bundle_sakura"),
                Decor("dec_toro", "Stone Lantern", DecorKind.Land, "toro", 1, 0, 0, 0.02, 0xB8C4D6, 0xFFB7D0, "Sakura bundle. A quiet garden light.", bundle: "bundle_sakura"),
                Decor("dec_neonsign", "Neon Sign", DecorKind.Land, "neonsign", 1, 0, 0, 0.02, 0x39F3FF, 0xFF3DDC, "Neon bundle. It says STORM.", bundle: "bundle_neon"),
                Decor("dec_holotree", "Holo Tree", DecorKind.Land, "holotree", 1, 0, 0, 0.03, 0x39F3FF, 0x6A2BD8, "Neon bundle. A tree made of light.", bundle: "bundle_neon"),
            };

            c.Expansions = new[]
            {
                new ExpansionDef { Id = "exp_1", Name = "Island Expansion I", Desc = "The island grows. Room for 10 more decorations.", CostGems = 150, UnlockLevel = 8, Radius = 11f, Capacity = 10 },
                new ExpansionDef { Id = "exp_2", Name = "Island Expansion II", Desc = "Even more land. Room for 12 more decorations.", CostGems = 350, UnlockLevel = 12, Radius = 12.5f, Capacity = 12 },
                new ExpansionDef { Id = "exp_3", Name = "Island Expansion III", Desc = "A grand island. Room for 15 more decorations.", CostGems = 700, UnlockLevel = 16, Radius = 14f, Capacity = 15 },
            };

            c.AdPlacements = new[]
            {
                new AdPlacementDef { Id = "ad_gems", Name = "Free Gems", Desc = "+8 Gems.", Icon = "gem", Reward = AdReward.Gems, Amount = 8, DailyLimit = 5, UnlockLevel = 2 },
                new AdPlacementDef { Id = "ad_surge", Name = "Energy Surge", Desc = "x2 Energy income for 15 minutes (stacks up to 1 hour).", Icon = "energy", Reward = AdReward.EnergySurge, Amount = 900, DailyLimit = 4, UnlockLevel = 3 },
                new AdPlacementDef { Id = "ad_charge", Name = "Instant Charge", Desc = "Fully charges the storm chamber.", Icon = "charge", Reward = AdReward.InstantCharge, Amount = 1, DailyLimit = 8, UnlockLevel = 2 },
                new AdPlacementDef { Id = "ad_cores", Name = "Double Cores", Desc = "Doubles the Storm Cores from your last storm.", Icon = "core", Reward = AdReward.DoubleCores, Amount = 1, DailyLimit = 5, UnlockLevel = 3 },
                new AdPlacementDef { Id = "ad_tourney", Name = "Extra Attempt", Desc = "+1 tournament attempt today.", Icon = "trophy", Reward = AdReward.TournamentAttempt, Amount = 1, DailyLimit = 2, UnlockLevel = 8 },
                new AdPlacementDef { Id = "ad_bundle", Name = "Bundle Progress", Desc = "+1 toward an ads-only bundle.", Icon = "play", Reward = AdReward.BundleProgress, Amount = 1, DailyLimit = 12, UnlockLevel = 6 },
                new AdPlacementDef { Id = "ad_offline", Name = "Double Offline", Desc = "Doubles the Energy from your time away.", Icon = "clock", Reward = AdReward.DoubleOffline, Amount = 1, DailyLimit = 0, UnlockLevel = 10 },
                new AdPlacementDef { Id = "ad_music", Name = "Music Track", Desc = "Unlocks a new music track to keep.", Icon = "sound", Reward = AdReward.MusicTrack, Amount = 1, DailyLimit = 3, UnlockLevel = 1 },
                new AdPlacementDef { Id = "ad_streak", Name = "Keep Your Streak", Desc = "Restores a login streak you just lost.", Icon = "gift", Reward = AdReward.StreakRestore, Amount = 1, DailyLimit = 1, UnlockLevel = 2 },
            };

            c.LoginRewards = new[]
            {
                new DailyReward { Gems = 10 },
                new DailyReward { Cores = 30 },
                new DailyReward { Gems = 15 },
                new DailyReward { SurgeSeconds = 1800 },
                new DailyReward { Gems = 20 },
                new DailyReward { Cores = 80 },
                new DailyReward { Gems = 50, Cores = 50 },
            };

            // Three a day, each of a different kind, only from what the player can already do.
            c.Missions = new[]
            {
                Mission("m_storms5", "Unleash 5 storms", "storm", MissionKind.Storms, 5, new DailyReward { Gems = 4 }, maxLevel: 7),
                Mission("m_storms12", "Unleash 12 storms", "storm", MissionKind.Storms, 12, new DailyReward { Gems = 7 }, minLevel: 6),
                Mission("m_levels20", "Buy 20 machine levels", "machines", MissionKind.MachineLevels, 20, new DailyReward { Cores = 25 }, maxLevel: 9),
                Mission("m_levels60", "Buy 60 machine levels", "machines", MissionKind.MachineLevels, 60, new DailyReward { Gems = 6 }, minLevel: 8),
                Mission("m_strikes", "Land 40 lightning strikes", "bolt", MissionKind.Strikes, 40, new DailyReward { Gems = 4 }),
                Mission("m_boosts", "Fire 4 boosts", "boost", MissionKind.Boosts, 4, new DailyReward { Cores = 20 }),
                Mission("m_stormboosts", "Fire 3 boosts while a storm rages", "boost", MissionKind.StormBoosts, 3, new DailyReward { SurgeSeconds = 900 }, minLevel: 4),
                Mission("m_pheno", "Unleash 3 phenomenon storms", "star", MissionKind.PhenoStorms, 3, new DailyReward { Gems = 6 }),
                Mission("m_chain", "Reach a x2 chain reaction in one storm", "synergy", MissionKind.Chain, 2, new DailyReward { Gems = 5 }),
                Mission("m_mega", "Trigger a Mega Storm", "chaos", MissionKind.MegaStorms, 1, new DailyReward { Gems = 8 }),
                Mission("m_cores", "Earn 10 Storm Cores from storms", "core", MissionKind.Cores, 10, new DailyReward { SurgeSeconds = 900 }),
                Mission("m_module", "Unlock or upgrade a module", "modules", MissionKind.ModuleUpgrades, 1, new DailyReward { Gems = 4 }),
                Mission("m_decor", "Buy a decoration for your island", "island", MissionKind.Decorations, 1, new DailyReward { Gems = 5 }),
                Mission("m_tourney", "Enter the weekly tournament", "trophy", MissionKind.Tournament, 1, new DailyReward { Gems = 6 }),
            };
            c.MissionBonus = new DailyReward { Gems = 10, Cores = 20 };

            c.Products = new[]
            {
                new ProductDef { Id = "com.stormengine.starter", Name = "Starter Pack", Desc = "400 Gems, 500 Storm Cores and the Founder title. Once per player.", Gems = 400, Cores = 500, Cosmetics = new[] { "title_founder" }, FallbackPrice = "$2.99", OneTime = true, Tag = "ONE TIME" },
                new ProductDef { Id = "com.stormengine.gems.100", Name = "Pouch of Gems", Desc = "100 Gems", Gems = 100, FallbackPrice = "$0.99" },
                new ProductDef { Id = "com.stormengine.gems.550", Name = "Bag of Gems", Desc = "550 Gems", Gems = 550, FallbackPrice = "$4.99", Tag = "+10%" },
                new ProductDef { Id = "com.stormengine.gems.1200", Name = "Chest of Gems", Desc = "1,200 Gems", Gems = 1200, FallbackPrice = "$9.99", Tag = "+20%" },
                new ProductDef { Id = "com.stormengine.gems.2600", Name = "Vault of Gems", Desc = "2,600 Gems", Gems = 2600, FallbackPrice = "$19.99", Tag = "+30%" },
                new ProductDef { Id = "com.stormengine.gems.7000", Name = "Storm Hoard", Desc = "7,000 Gems", Gems = 7000, FallbackPrice = "$49.99", Tag = "BEST VALUE" },
            };

            c.Goals = new[]
            {
                Goal("g_pump5", "Upgrade the Pressure Pump to Lv 5", GoalKind.MachineLevel, "pump", 5, 0, 20, 5),
                Goal("g_hydro", "Build a Hydro Pump", GoalKind.MachineLevel, "hydro", 1, 0, 20, 5),
                Goal("g_storm1", "Unleash your first storm", GoalKind.StormsTotal, null, 1, 3, 30, 10),
                Goal("g_level2", "Reach Engine Level 2", GoalKind.EngineLevel, null, 2, 0, 20, 5),
                Goal("g_condenser", "Build a Cloud Condenser", GoalKind.MachineLevel, "condenser", 1, 0, 30, 20),
                Goal("g_hydro10", "Reach Hydro Pump Lv 10", GoalKind.MachineLevel, "hydro", 10, 0, 30, 20),
                Goal("g_condenser10", "Reach Cloud Condenser Lv 10 to unlock Cloud Burst", GoalKind.MachineLevel, "condenser", 10, 2, 30, 25),
                Goal("g_boost1", "Activate a boost", GoalKind.BoostsUsed, null, 1, 2, 30, 20),
                Goal("g_tesla", "Build a Tesla Coil", GoalKind.MachineLevel, "tesla", 1, 3, 30, 25),
                Goal("g_style", "Get a new style item in the Style Shop", GoalKind.CosmeticsOwned, null, 2, 0, 0, 20),
                Goal("g_storms10", "Unleash 10 storms", GoalKind.StormsTotal, null, 10, 5, 60, 30),
                Goal("g_furnace", "Build a Thermal Furnace", GoalKind.MachineLevel, "furnace", 1, 3, 60, 30),
                Goal("g_module1", "Unlock a Module with Storm Cores", GoalKind.ModulesUnlocked, null, 1, 0, 60, 30),
                Goal("g_equip1", "Equip a Module", GoalKind.ModulesEquipped, null, 1, 3, 60, 30),
                Goal("g_decor1", "Decorate your island", GoalKind.DecorationsOwned, null, 1, 0, 60, 20, gems: 20),
                Goal("g_decor5", "Own 5 different decorations", GoalKind.DecorationsOwned, null, 5, 10, 60, 40, gems: 30),
                Goal("g_thunder", "Discover a Thunderstorm", GoalKind.Discover, "thunderstorm", 1, 5, 120, 50),
                Goal("g_chamber", "Reinforce the Storm Chamber", GoalKind.MachineLevel, "chamber", 1, 3, 60, 30),
                Goal("g_turbine", "Build a Wind Turbine", GoalKind.MachineLevel, "turbine", 1, 5, 60, 40),
                Goal("g_chain", "Reach a x2 chain reaction in one storm", GoalKind.ChainPeak, null, 2, 5, 120, 40),
                Goal("g_level8", "Reach Engine Level 8 to enter tournaments", GoalKind.EngineLevel, null, 8, 10, 60, 30),
                Goal("g_tourney1", "Enter the weekly tournament", GoalKind.TournamentRuns, null, 1, 5, 0, 40, gems: 30),
                Goal("g_heat", "Discover a Heat Storm", GoalKind.Discover, "heat_storm", 1, 5, 120, 60),
                Goal("g_offline", "Build the Offline Generator", GoalKind.UpgradeLevel, "offline_generator", 1, 10, 0, 60),
                Goal("g_run10m", "Generate 10M Energy in one run", GoalKind.RunEnergy, null, 1e7, 10, 120, 60),
                Goal("g_prestige1", "Reset the atmosphere to earn Research", GoalKind.Prestiges, null, 1, 10, 0, 100),
                Goal("g_research3", "Own 3 Research upgrades", GoalKind.ResearchOwned, null, 3, 10, 0, 80),
                Goal("g_monsoon", "Discover a Monsoon", GoalKind.Discover, "monsoon", 1, 10, 120, 80),
                Goal("g_cyclone", "Discover a Cyclone", GoalKind.Discover, "cyclone", 1, 10, 120, 80),
                Goal("g_supercell", "Discover a Supercell", GoalKind.Discover, "supercell", 1, 15, 120, 100),
                Goal("g_overcharge", "Discover an Overcharge storm", GoalKind.Discover, "overcharge", 1, 15, 120, 100),
                Goal("g_prestige3", "Reset the atmosphere 3 times", GoalKind.Prestiges, null, 3, 20, 0, 150),
                Goal("g_anomaly", "Create an Anomaly", GoalKind.Discover, "anomaly", 1, 30, 120, 200),
            };

            c.Index();
            return c;
        }

        static StatInfo[] BuildStats()
        {
            var s = new StatInfo[StatCount];
            void Set(StatId id, string name, double b, StatPhase phase, char fmt) =>
                s[(int)id] = new StatInfo { Id = id, Name = name, Base = b, Phase = phase, Format = fmt };

            Set(StatId.BasePressure, "Base Pressure", 1, StatPhase.Atmosphere, 'n');
            Set(StatId.Temperature, "Temperature", 1, StatPhase.Atmosphere, 'x');
            Set(StatId.Humidity, "Humidity", 1, StatPhase.Atmosphere, 'x');
            Set(StatId.Wind, "Wind", 1, StatPhase.Atmosphere, 'x');
            Set(StatId.Cloud, "Cloud", 1, StatPhase.Atmosphere, 'x');
            Set(StatId.Electricity, "Electricity", 1, StatPhase.Atmosphere, 'x');
            Set(StatId.GlobalPower, "Engine Efficiency", 1, StatPhase.Output, 'x');
            Set(StatId.PhenoAll, "All Phenomena", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoThunderstorm, "Thunderstorm", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoHeatStorm, "Heat Storm", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoMonsoon, "Monsoon", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoCyclone, "Cyclone", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoSupercell, "Supercell", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoOvercharge, "Overcharge", 1, StatPhase.Phenomena, 'x');
            Set(StatId.PhenoAnomaly, "Anomaly", 1, StatPhase.Phenomena, 'x');
            Set(StatId.StormIntensity, "Storm Intensity", 2.5, StatPhase.Output, 'x');
            Set(StatId.StormDuration, "Storm Duration", 12, StatPhase.Output, 's');
            Set(StatId.StormChargeSpeed, "Storm Charge Speed", 1, StatPhase.Meta, 'x');
            Set(StatId.LightningRate, "Lightning Rate", 0.5, StatPhase.Output, 'r');
            Set(StatId.ChainPerStrike, "Chain per Strike", 0.06, StatPhase.Output, 'p');
            Set(StatId.MegaStormPower, "Mega Storm Power", 1, StatPhase.Meta, 'x');
            Set(StatId.BoostDuration, "Boost Duration", 1, StatPhase.Meta, 'x');
            Set(StatId.BoostCooldown, "Boost Cooldown", 1, StatPhase.Meta, 'x');
            Set(StatId.BoostPower, "Boost Power", 1, StatPhase.Meta, 'x');
            Set(StatId.OfflineEfficiency, "Offline Efficiency", 0, StatPhase.Meta, 'p');
            Set(StatId.OfflineCapHours, "Offline Cap", 0, StatPhase.Meta, 'n');
            Set(StatId.CoreGain, "Storm Core Gain", 1, StatPhase.Meta, 'x');
            Set(StatId.ResearchGain, "Research Gain", 1, StatPhase.Meta, 'x');
            Set(StatId.ModuleSlots, "Module Slots", 2, StatPhase.Meta, 'n');
            Set(StatId.InstabilityRate, "Instability Rate", 0, StatPhase.Output, 'p');
            Set(StatId.Resonance, "Synergy Resonance", 0, StatPhase.Meta, 'p');
            Set(StatId.EnergyGain, "Energy Income", 1, StatPhase.Meta, 'x');
            return s;
        }

        // ---- Effect helpers -------------------------------------------------------------------

        static EffectDef FlatScaled(StatId s, double per) =>
            new EffectDef { Stat = s, Op = ModOp.Flat, Mode = ValueMode.Scaled, PerLevel = per };

        static EffectDef AddScaled(StatId s, double per) =>
            new EffectDef { Stat = s, Op = ModOp.Add, Mode = ValueMode.Scaled, PerLevel = per };

        static EffectDef AddEffPow(StatId s, double b) =>
            new EffectDef { Stat = s, Op = ModOp.AddEff, Mode = ValueMode.Power, Base = b };

        static EffectDef MultPow(StatId s, double b) =>
            new EffectDef { Stat = s, Op = ModOp.Mult, Mode = ValueMode.Power, Base = b };

        static EffectDef ConvertScaled(StatId from, StatId to, double per) =>
            new EffectDef { Stat = to, From = from, Op = ModOp.Convert, Mode = ValueMode.Scaled, PerLevel = per };

        static EffectDef MultLin(StatId s, double first, double per) =>
            new EffectDef { Stat = s, Op = ModOp.Mult, Mode = ValueMode.Linear, Base = first, PerLevel = per };

        static EffectDef FlatLin(StatId s, double first, double per) =>
            new EffectDef { Stat = s, Op = ModOp.Flat, Mode = ValueMode.Linear, Base = first, PerLevel = per };

        static EffectDef AddLin(StatId s, double first, double per) =>
            new EffectDef { Stat = s, Op = ModOp.Add, Mode = ValueMode.Linear, Base = first, PerLevel = per };

        static EffectDef ConvertLin(StatId from, StatId to, double first, double per) =>
            new EffectDef { Stat = to, From = from, Op = ModOp.Convert, Mode = ValueMode.Linear, Base = first, PerLevel = per };

        static EffectDef WeeklyMult(StatId s, double mult) =>
            new EffectDef { Stat = s, Op = ModOp.Mult, Mode = ValueMode.Linear, Base = mult };

        static EffectDef WeeklyFlat(StatId s, double value) =>
            new EffectDef { Stat = s, Op = ModOp.Flat, Mode = ValueMode.Linear, Base = value };

        static GoalDef Goal(string id, string text, GoalKind kind, string target, double amount, long cores, double seconds, double xp, long gems = 0) =>
            new GoalDef { Id = id, Text = text, Kind = kind, Target = target, Amount = amount, RewardCores = cores, RewardSeconds = seconds, RewardXp = xp, RewardGems = gems };

        static MissionDef Mission(string id, string text, string icon, MissionKind kind, double amount, DailyReward reward, int minLevel = 1, int maxLevel = 0) =>
            new MissionDef { Id = id, Text = text, Icon = icon, Kind = kind, Amount = amount, Reward = reward, MinLevel = minLevel, MaxLevel = maxLevel };

        static FeatureDef Feature(string id, string name, string desc, string icon, int level) =>
            new FeatureDef { Id = id, Name = name, Desc = desc, Icon = icon, Level = level };

        /// <summary>A style bonus: a direct multiplier on one stat.</summary>
        static EffectDef[] M(StatId s, double mult) =>
            new[] { new EffectDef { Stat = s, Op = ModOp.Mult, Mode = ValueMode.Linear, Base = mult } };

        static CosmeticDef Item(CosmeticSlot slot, string id, string name, string style, int primary, int secondary, int accent, int level, long gems,
            EffectDef[] effects, string desc, string discovery, int prestiges, string bundle, bool exclusive, bool isDefault) =>
            new CosmeticDef
            {
                Id = id, Name = name, Slot = slot, Style = style, Primary = primary, Secondary = secondary, Accent = accent,
                UnlockLevel = level, CostGems = gems, Effects = effects ?? Array.Empty<EffectDef>(), Desc = desc,
                RequiresDiscovery = discovery, RequiresPrestiges = prestiges, Bundle = bundle, Exclusive = exclusive, Default = isDefault
            };

        static CosmeticDef Skin(string id, string name, string shape, int primary, int secondary, int accent, int level, long gems, EffectDef[] effects,
            string desc = null, string discovery = null, int prestiges = 0, string bundle = null, bool exclusive = false, bool isDefault = false) =>
            Item(CosmeticSlot.Skin, id, name, shape, primary, secondary, accent, level, gems, effects, desc, discovery, prestiges, bundle, exclusive, isDefault);

        static CosmeticDef Cloud(string id, string name, string style, int primary, int secondary, int accent, int level, long gems, EffectDef[] effects,
            string desc = null, string discovery = null, string bundle = null, bool exclusive = false, bool isDefault = false) =>
            Item(CosmeticSlot.Cloud, id, name, style, primary, secondary, accent, level, gems, effects, desc, discovery, 0, bundle, exclusive, isDefault);

        static CosmeticDef Bolt(string id, string name, string style, int primary, int secondary, int accent, int level, long gems, EffectDef[] effects,
            string desc = null, string discovery = null, string bundle = null, bool exclusive = false, bool isDefault = false) =>
            Item(CosmeticSlot.Bolt, id, name, style, primary, secondary, accent, level, gems, effects, desc, discovery, 0, bundle, exclusive, isDefault);

        static CosmeticDef World(string id, string name, string style, int ground, int skyTop, int skyBottom, int level, long gems, EffectDef[] effects,
            string desc = null, string discovery = null, int prestiges = 0, string bundle = null, bool exclusive = false, bool isDefault = false) =>
            Item(CosmeticSlot.World, id, name, style, ground, skyTop, skyBottom, level, gems, effects, desc, discovery, prestiges, bundle, exclusive, isDefault);

        static CosmeticDef Title(string id, string name, int level, string discovery, EffectDef[] effects, long gems = 0,
            string bundle = null, bool exclusive = false, bool isDefault = false)
        {
            string desc = discovery != null ? "Earned by discovering this storm class." : bundle != null ? "Part of a bundle." :
                gems > 0 ? "Exclusive title." : level > 1 ? "Earned at Engine Level " + level + "." : "Shown next to your name.";
            if (effects != null) desc += " Storm Core gain x" + effects[0].Base.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".";
            return Item(CosmeticSlot.Title, id, name, "title", 0, 0, 0, level, gems, effects, desc, discovery, 0, bundle, exclusive, isDefault);
        }

        static DecorationDef Decor(string id, string name, DecorKind kind, string model, int level, long cores, long gems, double bonus,
            int primary, int accent, string desc, string bundle = null, bool limited = false, bool exclusive = false, int maxCopies = 5) =>
            new DecorationDef
            {
                Id = id, Name = name, Kind = kind, Model = model, UnlockLevel = level, CostCores = cores, CostGems = gems, Bonus = bonus,
                Primary = primary, Accent = accent, Desc = desc, Bundle = bundle, Limited = limited, Exclusive = exclusive,
                MaxCopies = bundle != null ? 1 : maxCopies
            };
    }
}
