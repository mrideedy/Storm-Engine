using System;
using System.Collections.Generic;
using System.Linq;
using StormEngine.Core;

static class Program
{
    static void Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "active";
        double hours = args.Length > 1 ? double.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 8;
        var c = Content.CreateDefault();
        Tune.Apply(c);
        if (mode == "validate")
        {
            Validation.Run(c);
            return;
        }
        if (mode == "export")
        {
            Console.WriteLine(ContentExport.ToJson(c));
            return;
        }
        if (mode == "lockeys")
        {
            // Every translatable content text (untuned content, exactly as the game builds it).
            var w = new JsonWriter().BeginArray();
            foreach (var s in ContentText.Collect(Content.CreateDefault())) w.Value(s);
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine(w.EndArray().ToString());
            return;
        }
        var bot = new Bot(c, mode);
        bot.Run(hours * 3600);
    }
}

sealed class Bot
{
    readonly Content C;
    readonly Game G;
    readonly string Mode;
    bool Active => Mode == "active";
    double t;
    readonly long unix0 = 1790451690;
    readonly List<string> log = new List<string>();
    double runStart;

    public Bot(Content c, string mode)
    {
        C = c;
        Mode = mode;
        G = new Game(c);
        G.SetTime(unix0);
        G.MachineUnlockedEvent += m => Log($"unlock machine {C.Machines[m].Name}");
        G.PhenomenonDiscovered += p => Log($"DISCOVER {C.Phenomena[p].Name}  (SP {Num.Format(G.Snap.StormPower)})");
        G.GoalCompleted += g => Log($"goal: {g.Text}");
        G.LeveledUp += lvl => Log($"LEVEL {lvl}  (SP {Num.Format(G.Snap.StormPower)}/s, E {Num.Format(G.Energy)})  unlocks: {string.Join(", ", G.UnlocksAtLevel(lvl))}");
        G.StormEnded += (r, f, s) =>
        {
            if (f.AllTime && (G.Stats.Storms < 5 || G.Stats.Storms % 25 == 0))
                Log($"storm #{G.Stats.Storms} {StormSubmission.ClassName(C, r.ClassIndex)} score {Num.Format(r.Score)} chain x{r.PeakChain:0.00} strikes {r.Strikes} cores {r.Cores} ticks {r.Ticks} ev {r.Events.Count}");
        };
    }

    void Log(string s) => log.Add($"[{Num.Time(t),8}] {s}");

    public void Run(double seconds)
    {
        double dt = 0.5;
        double[] checkpoints = { 60, 180, 300, 600, 900, 1200, 1800, 2700, 3600, 5400, 7200, 10800, 14400, 21600, 28800, 43200, 64800, 86400, 172800 };
        int cp = 0;
        double nextDecision = 0, nextModules = 0;
        while (t < seconds)
        {
            G.SetTime(unix0 + (long)t);
            if (t >= nextDecision)
            {
                Decide();
                nextDecision = t + (Mode == "idle" ? 30 : 2);
            }
            if (t >= nextModules)
            {
                ManageModules();
                nextModules = t + 20;
            }
            G.Tick(dt);
            t += dt;
            if (cp < checkpoints.Length && t >= checkpoints[cp])
            {
                Report();
                cp++;
            }
        }
        foreach (var l in log) Console.WriteLine(l);
    }

    void Report()
    {
        var s = G.Snap;
        string cats = string.Join(" ", Enumerable.Range(0, 5).Select(i => Num.Mult(s.CatValue(i))));
        string lv = string.Join(",", G.Build.Machines);
        string mods = string.Join(",", G.Build.Equipped.Select(m => C.Modules[m].Id + ":" + G.Build.ModuleLevels[m]));
        Log($"== L{G.Level} SP {Num.Format(s.StormPower)}/s E {Num.Format(G.Energy)} run {Num.Format(G.RunEnergy)} | base {Num.Format(s.Stat[0])} cats {cats} syn {Num.Mult(s.Synergy)} glob {Num.Mult(s.Stat[(int)StatId.GlobalPower])} | lv [{lv}] | cores {G.Cores} mods [{mods}] | R {G.Research}/{G.ResearchLifetime} P{G.Prestiges} onReset {G.ResearchOnReset()} | best {Num.Format(G.Records.BestScore)} storms {G.Stats.Storms}");
    }

    void Decide()
    {
        if (Mode != "idle")
        {
            if (G.IsStorming)
            {
                if (Active)
                    for (int b = 0; b < C.Boosts.Length; b++)
                        if (G.IsBoostReady(b)) G.ActivateBoost(b);
            }
            else if (G.CanUnleash)
            {
                bool wait = false;
                if (Active)
                {
                    double maxWait = 0;
                    for (int b = 0; b < C.Boosts.Length; b++)
                        if (G.IsBoostUnlocked(b) && G.BoostActive[b] <= 0) maxWait = Math.Max(maxWait, G.BoostCooldown[b]);
                    wait = maxWait > 0 && maxWait < 15;
                    if (!wait)
                        for (int b = 0; b < C.Boosts.Length; b++)
                            if (G.IsBoostReady(b)) G.ActivateBoost(b);
                }
                if (!wait) G.Unleash();
            }
        }

        BuyMachines();
        BuyResearch();
        MaybePrestige();
        int gen = C.UpgradeIndex("offline_generator");
        if (gen >= 0 && G.IsUpgradeAvailable(gen) && !G.IsUpgradeMaxed(gen))
        {
            double cost = G.UpgradeCost(gen);
            if (!offlineNoted.Contains(G.Build.Upgrades[gen]))
            {
                offlineNoted.Add(G.Build.Upgrades[gen]);
                Log($"offline generator Lv{G.Build.Upgrades[gen] + 1} costs {Num.Format(cost)} + {G.UpgradeCoreCost(gen)} cores = {Num.Time(cost / Math.Max(1e-9, G.Snap.StormPower))} of production (cores now {G.Cores})");
            }
            if (Mode != "noupgrade" && G.Energy >= cost * 3 && G.Cores >= G.UpgradeCoreCost(gen) + 20) { G.BuyUpgrade(gen); Log($"bought offline generator Lv{G.Build.Upgrades[gen]}"); }
        }
    }

    readonly HashSet<int> offlineNoted = new HashSet<int>();

    void BuyMachines()
    {
        if (G.IsStorming) return;
        int chamber = C.MachineIndex("chamber");
        for (int guard = 0; guard < 200; guard++)
        {
            int best = -1;
            double bestValue = 0;
            for (int m = 0; m < C.Machines.Length; m++)
            {
                if (!G.MachineUnlocked[m] || G.IsMaxed(m) || m == chamber) continue;
                if (G.CostOf(m, 1) > G.Energy) continue;
                double v = G.MachineValue(m);
                if (v > bestValue)
                {
                    bestValue = v;
                    best = m;
                }
            }
            if (best < 0) break;
            G.BuyMachine(best, 1);
        }
        while (G.MachineUnlocked[chamber] && !G.IsMaxed(chamber) && G.CostOf(chamber, 1) < G.Energy * 0.05)
            G.BuyMachine(chamber, 1);
    }

    void BuyResearch()
    {
        if (G.Research <= 0) return;
        string[] priority = { "automation", "module_bay", "engine_efficiency" };
        foreach (var id in priority)
        {
            int i = C.ResearchIndex(id);
            while (G.CanBuyResearch(i) && (id != "engine_efficiency" || G.ResearchCost(i) <= 2 + G.ResearchLifetime / 10)) G.BuyResearch(i);
        }
        for (int guard = 0; guard < 50; guard++)
        {
            int best = -1;
            long cost = long.MaxValue;
            for (int r = 0; r < C.Research.Length; r++)
                if (G.CanBuyResearch(r) && G.ResearchCost(r) < cost)
                {
                    cost = G.ResearchCost(r);
                    best = r;
                }
            if (best < 0) break;
            G.BuyResearch(best);
        }
    }

    void MaybePrestige()
    {
        long gain = G.ResearchOnReset();
        if (gain >= Math.Max(8, G.ResearchLifetime * 0.75) && t - runStart > 900)
        {
            string msg = $"PRESTIGE #{G.Prestiges + 1}: +{gain} research (run {Num.Time(t - runStart)}, run energy {Num.Format(G.RunEnergy)})";
            if (G.Prestige())
            {
                Log(msg);
                runStart = t;
            }
        }
    }

    void ManageModules()
    {
        // Unlock anything affordable (cheapest first), then upgrade equipped modules.
        for (int guard = 0; guard < 20; guard++)
        {
            int best = -1;
            long cost = long.MaxValue;
            for (int m = 0; m < C.Modules.Length; m++)
                if (!G.IsModuleUnlocked(m) && G.ModuleRequirementsMet(m) && C.Modules[m].UnlockCost <= G.Cores && C.Modules[m].UnlockCost < cost)
                {
                    cost = C.Modules[m].UnlockCost;
                    best = m;
                }
            if (best < 0) break;
            G.UnlockModule(best);
            Log($"module unlocked {C.Modules[best].Name}");
        }
        if (G.IsStorming) return;

        // Pick the best loadout by storm score of a boost-free storm.
        var unlocked = Enumerable.Range(0, C.Modules.Length).Where(G.IsModuleUnlocked).ToList();
        int slots = G.ModuleSlots;
        if (unlocked.Count > 0)
        {
            List<int> bestSet = null;
            double bestScore = -1;
            foreach (var combo in Combos(unlocked, Math.Min(slots, unlocked.Count)))
            {
                var b = G.Build.Clone();
                b.Equipped = combo.ToList();
                var setup = new StormSetup { Build = b, BoostActiveTicks = new int[C.Boosts.Length], BoostCooldownTicks = new int[C.Boosts.Length] };
                var r = StormSim.Replay(C, setup, Array.Empty<StormEvent>(), out _);
                var ms = ModifierSet.Create(C, b);
                var snap = new Snapshot(C);
                Formula.Evaluate(C, ms, new EvalContext(C.Boosts.Length), snap);
                double score = snap.StormPower * 60 + r.Score;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestSet = combo.ToList();
                }
            }
            if (bestSet != null && !bestSet.OrderBy(x => x).SequenceEqual(G.Build.Equipped.OrderBy(x => x)))
            {
                foreach (var m in G.Build.Equipped.ToList()) G.Unequip(m);
                foreach (var m in bestSet) G.Equip(m);
            }
        }

        for (int guard = 0; guard < 50; guard++)
        {
            int best = -1;
            long cost = long.MaxValue;
            foreach (int m in G.Build.Equipped)
                if (G.CanUpgradeModule(m) && G.ModuleUpgradeCost(m) < cost)
                {
                    cost = G.ModuleUpgradeCost(m);
                    best = m;
                }
            if (best < 0) break;
            G.UpgradeModule(best);
        }
    }

    static IEnumerable<int[]> Combos(List<int> items, int k)
    {
        if (k == 0) { yield return Array.Empty<int>(); yield break; }
        var idx = Enumerable.Range(0, k).ToArray();
        while (true)
        {
            yield return idx.Select(i => items[i]).ToArray();
            int j = k - 1;
            while (j >= 0 && idx[j] == items.Count - k + j) j--;
            if (j < 0) yield break;
            idx[j]++;
            for (int x = j + 1; x < k; x++) idx[x] = idx[x - 1] + 1;
        }
    }
}

static class Validation
{
    /// <summary>Generates random storms, validates them in C#, and writes test vectors for the JS validator.</summary>
    public static void Run(Content c)
    {
        var rng = new Random(12345);
        var vectors = new System.Text.StringBuilder();
        vectors.Append("[");
        int count = 0, ok = 0;
        long baseUnix = 1790451690;
        for (int n = 0; n < 400; n++)
        {
            var g = new Game(c);
            long unix = baseUnix + rng.Next(0, 60 * 86400);
            g.SetTime(unix);
            // Random build.
            for (int m = 0; m < c.Machines.Length; m++)
            {
                int max = c.Machines[m].MaxLevel > 0 ? c.Machines[m].MaxLevel : 400;
                g.Build.Machines[m] = rng.NextDouble() < 0.1 ? 0 : rng.Next(0, Math.Min(max, n < 200 ? 120 : 400) + 1);
            }
            for (int r = 0; r < c.Research.Length; r++)
                g.Build.Research[r] = rng.NextDouble() < 0.5 ? 0 : rng.Next(0, Math.Min(c.Research[r].MaxLevel, 20) + 1);
            var mods = Enumerable.Range(0, c.Modules.Length).OrderBy(_ => rng.Next()).ToList();
            g.MarkDirty();
            g.Refresh();
            int slots = g.ModuleSlots;
            foreach (var m in mods.Take(rng.Next(0, slots + 1)))
            {
                g.Build.ModuleLevels[m] = rng.Next(1, c.Modules[m].MaxLevel + 1);
                g.Build.Equipped.Add(m);
            }
            g.MarkDirty();
            g.Refresh();
            // Random style items (their bonuses count, capped, in tournaments).
            foreach (CosmeticSlot slot in Enum.GetValues(typeof(CosmeticSlot)))
            {
                if (rng.NextDouble() < 0.3) continue;
                var options = Enumerable.Range(0, c.Cosmetics.Length).Where(i => c.Cosmetics[i].Slot == slot).ToList();
                int pick = options[rng.Next(options.Count)];
                g.OwnedCosmetics.Add(c.Cosmetics[pick].Id);
                g.EquipCosmetic(pick);
            }
            // Tournament runs: fresh boosts, this week's condition, capped style.
            g.SetProgress(Math.Max(8, g.FeatureLevel("tournament")), 0);
            StormSubmission captured = null;
            StormResult result = null;
            g.StormEnded += (res, flags, sub) => { result = res; captured = sub; };
            if (!g.StartTournament()) continue;
            var storm = g.Storm;
            double t = 0;
            while (g.IsStorming && t < 120)
            {
                // Random boost presses during the storm.
                for (int b = 0; b < c.Boosts.Length; b++)
                    if (rng.NextDouble() < 0.02 && g.IsBoostReady(b)) g.ActivateBoost(b);
                double dt = 0.05 + rng.NextDouble() * 0.3;
                g.Tick(dt);
                t += dt;
            }
            if (captured == null) continue;
            count++;
            bool valid = StormValidator.Validate(c, captured, out var replay, out var error);
            if (valid) ok++;
            else Console.WriteLine($"C# self-validation failed for storm {n}: {error} (live {captured.Score} vs replay {replay?.Score})");
            if (count > 1) vectors.Append(",");
            vectors.Append("{\"submission\":").Append(captured.ToJson())
                .Append(",\"now\":").Append(unix + 5)
                .Append(",\"expectedScore\":").Append(result.Score.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(",\"expectedClass\":\"").Append(StormSubmission.ClassId(c, result.ClassIndex)).Append("\"}");
        }
        vectors.Append("]");
        System.IO.File.WriteAllText("vectors.json", vectors.ToString());
        System.IO.File.WriteAllText("content.json", ContentExport.ToJson(c));
        Console.WriteLine($"C# validated {ok}/{count} storms; vectors.json and content.json written.");
    }
}

static class Tune
{
    static string Env(string k) => Environment.GetEnvironmentVariable(k);
    static double D(string k, double fallback)
    {
        var v = Env(k);
        return string.IsNullOrEmpty(v) ? fallback : double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static void Apply(Content c)
    {
        double gCat = D("SE_GCAT", 0), gPump = D("SE_GPUMP", 0), mm = D("SE_MM", 0), add = D("SE_ADD", 0), costMul = D("SE_COSTMUL", 1);
        var ms = Env("SE_MS");
        if (!string.IsNullOrEmpty(ms)) c.MilestoneLevels = ms.Split(',').Select(int.Parse).ToArray();
        foreach (var m in c.Machines)
        {
            if (m.Id == "chamber") continue;
            if (m.Id == "pump") { if (gPump > 0) m.Growth = gPump; }
            else
            {
                if (gCat > 0) m.Growth = gCat;
                if (add > 0) foreach (var e in m.Effects) e.PerLevel = add;
                m.BaseCost *= costMul;
            }
            if (mm > 0 && m.MilestoneMult > 0) m.MilestoneMult = mm;
        }
        double sup = D("SE_SUPER", 0), supPump = D("SE_SUPERPUMP", 0);
        foreach (var m in c.Machines)
        {
            if (m.Id == "chamber") continue;
            if (m.Id == "pump") { if (supPump > 0) m.Super = supPump; }
            else if (sup > 0) m.Super = sup;
        }
        double intensity = D("SE_INT", 0);
        if (intensity > 0) c.Stats[(int)StatId.StormIntensity].Base = intensity;
        c.B.XpBase = D("SE_XPB", c.B.XpBase);
        c.B.XpExponent = D("SE_XPE", c.B.XpExponent);
        double phScale = D("SE_PH", 0);
        if (phScale > 0) foreach (var ph in c.Phenomena) if (!ph.Special) ph.BaseMult = 1 + (ph.BaseMult - 1) * phScale;
        double phThr = D("SE_PHT", 0);
        if (phThr > 0) foreach (var ph in c.Phenomena) for (int i = 0; i < ph.Ingredients.Length; i++) ph.Ingredients[i].Threshold *= phThr;
        double xpMachine = D("SE_XPM", -1);
        if (xpMachine >= 0) c.B.XpPerMachineLevel = xpMachine;
        double costScale = D("SE_COSTSCALE", 0);
        if (costScale > 0) foreach (var m in c.Machines) if (m.Id != "chamber") m.BaseCost *= costScale;
        var pm = D("SE_PUMPMM", 0);
        if (pm > 0) c.Machines[0].MilestoneMult = pm;
        c.B.ResearchScale = D("SE_RSCALE", c.B.ResearchScale);
        c.B.ResearchOffset = D("SE_ROFF", c.B.ResearchOffset);
        c.B.ResearchPower = D("SE_RPOW", c.B.ResearchPower);
    }
}
