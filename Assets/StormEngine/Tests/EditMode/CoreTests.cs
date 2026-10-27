using System;
using System.Linq;
using NUnit.Framework;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine.Tests
{
    public class FormulaTests
    {
        Content _c;

        [SetUp]
        public void SetUp() => _c = Content.CreateDefault();

        Game NewGame()
        {
            var g = new Game(_c);
            g.Build.Weekly = -1;
            g.MarkDirty();
            g.Refresh();
            return g;
        }

        int M(string id) => _c.MachineIndex(id);

        [Test]
        public void EmptyEngineProducesBasePower()
        {
            var g = NewGame();
            Assert.AreEqual(1, g.Snap.StormPower, 1e-12);
        }

        [Test]
        public void MachineLevelsAreAdditiveInsideACategory()
        {
            var g = NewGame();
            g.Build.Machines[M("furnace")] = 10;
            g.MarkDirty();
            g.Refresh();
            Assert.AreEqual(1.5, g.Snap.Stat[(int)StatId.Temperature], 1e-12);
        }

        [Test]
        public void MilestonesMultiply()
        {
            var g = NewGame();
            g.Build.Machines[M("furnace")] = 25;
            g.Build.Machines[M("pump")] = 25;
            g.MarkDirty();
            g.Refresh();
            Assert.AreEqual((1 + 0.05 * 25) * 1.5, g.Snap.Stat[(int)StatId.Temperature], 1e-12);
            Assert.AreEqual(26 * 1.5, g.Snap.Stat[(int)StatId.BasePressure], 1e-12);
        }

        [Test]
        public void CategoriesMultiplyEachOther()
        {
            var g = NewGame();
            g.Build.Machines[M("pump")] = 4;
            g.Build.Machines[M("furnace")] = 10;
            g.Build.Machines[M("hydro")] = 5;
            g.MarkDirty();
            g.Refresh();
            Assert.AreEqual(5 * 1.5 * 1.25, g.Snap.StormPower, 1e-9);
        }

        [Test]
        public void ConversionFeedsBasePressureFromTemperatureBonus()
        {
            var g = NewGame();
            g.Build.Machines[M("furnace")] = 10;
            int core = _c.ModuleIndex("thermal_core");
            g.Build.ModuleLevels[core] = 1;
            g.Build.Equipped.Add(core);
            g.MarkDirty();
            g.Refresh();
            Assert.AreEqual(1.5 * 1.5, g.Snap.Stat[(int)StatId.Temperature], 1e-12);
            Assert.AreEqual(1 + 0.10 * 0.5, g.Snap.Stat[(int)StatId.BasePressure], 1e-12);
        }

        [Test]
        public void MasteryMakesAdditiveBonusesMoreEffective()
        {
            var g = NewGame();
            g.Build.Machines[M("furnace")] = 10;
            g.Build.Research[_c.ResearchIndex("thermal_mastery")] = 1;
            g.MarkDirty();
            g.Refresh();
            Assert.AreEqual(1 + 0.5 * 1.1, g.Snap.Stat[(int)StatId.Temperature], 1e-12);
        }

        [Test]
        public void ThunderstormActivatesWhenAllIngredientsReachThreshold()
        {
            // Level 34 with one x1.5 milestone: (1 + 0.05 x 34) x 1.5 = 4.05; level 33 gives 3.975.
            var g = NewGame();
            g.Build.Machines[M("hydro")] = 34;
            g.Build.Machines[M("condenser")] = 34;
            g.Build.Machines[M("tesla")] = 33;
            g.MarkDirty();
            g.Refresh();
            int thunder = _c.PhenoIndex("thunderstorm");
            Assert.IsFalse(g.Snap.PhenoActive[thunder], "Electricity below x4 should keep it inactive");

            g.Build.Machines[M("tesla")] = 34;
            g.MarkDirty();
            g.Refresh();
            Assert.IsTrue(g.Snap.PhenoActive[thunder]);
            double expected = 1.18 * (1 + 0.2 * Math.Log(4.05 / 4.0));
            Assert.AreEqual(expected, g.Snap.PhenoStrength[thunder], 1e-12);
            Assert.AreEqual(thunder, g.Snap.Dominant);
            Assert.AreEqual(expected, g.Snap.Synergy, 1e-12);
        }

        [Test]
        public void BoostsCanTemporarilyActivateAPhenomenon()
        {
            var g = NewGame();
            g.Build.Machines[M("furnace")] = 50;
            g.Build.Machines[M("turbine")] = 30;
            g.MarkDirty();
            g.Refresh();
            int heat = _c.PhenoIndex("heat_storm");
            Assert.Less(g.Snap.CatValue((int)Cat.Temperature), 10);
            Assert.IsFalse(g.Snap.PhenoActive[heat]);

            Assert.IsTrue(g.ActivateBoost(_c.BoostIndex("heat_surge")));
            g.Refresh();
            Assert.IsTrue(g.Snap.PhenoActive[heat], "Heat Surge doubles Temperature past the Heat Storm threshold");
        }

        [Test]
        public void StyleItemsMultiplyTheirStat()
        {
            var g = NewGame();
            g.Build.Machines[M("pump")] = 20;
            g.Build.Machines[M("hydro")] = 20;
            g.MarkDirty();
            g.Refresh();
            double humidity = g.Snap.Stat[(int)StatId.Humidity];
            double power = g.Snap.StormPower;
            int ocean = _c.CosmeticIndex("skin_ocean");
            g.OwnedCosmetics.Add("skin_ocean");
            Assert.IsTrue(g.EquipCosmetic(ocean));
            Assert.AreEqual(humidity * 1.10, g.Snap.Stat[(int)StatId.Humidity], humidity * 1e-12, "Ocean Breeze: Humidity x1.10");
            Assert.Greater(g.Snap.StormPower, power);

            // Unowned items never apply, even if the equipped id is forced.
            var cheat = NewGame();
            cheat.EquippedCosmetics[(int)CosmeticSlot.Skin] = "skin_steam";
            cheat.SyncStyle();
            Assert.AreEqual(-1, cheat.Build.Style[(int)CosmeticSlot.Skin]);
        }

        [Test]
        public void EveryStyleItemExceptDefaultsHasABonus()
        {
            foreach (var def in _c.Cosmetics)
            {
                if (def.Default) Assert.AreEqual(0, def.Effects.Length, def.Id + " is a default and stays neutral");
                else Assert.Greater(def.Effects.Length, 0, def.Id + " must boost a stat");
            }
        }
    }

    public class StormTests
    {
        Content _c;

        [SetUp]
        public void SetUp() => _c = Content.CreateDefault();

        [Test]
        public void LiveStormsReplayToTheSameScore()
        {
            var rng = new System.Random(99);
            for (int n = 0; n < 40; n++)
            {
                var g = RandomGame(rng);
                StormSubmission submission = null;
                StormResult live = null;
                g.StormEnded += (r, f, s) =>
                {
                    live = r;
                    submission = s;
                };
                Assert.IsTrue(g.StartTournament());
                double t = 0;
                while (g.IsStorming && t < 120)
                {
                    for (int b = 0; b < _c.Boosts.Length; b++)
                        if (rng.NextDouble() < 0.03 && g.IsBoostReady(b)) g.ActivateBoost(b);
                    double dt = 0.02 + rng.NextDouble() * 0.4;
                    g.Tick(dt);
                    t += dt;
                }
                Assert.IsNotNull(submission, "a record tournament storm must produce a submission");
                var parsed = StormSubmission.FromJson(submission.ToJson());
                Assert.IsTrue(StormValidator.Validate(_c, parsed, out var replay, out var error), error);
                Assert.AreEqual(live.Score, replay.Score, 0, "replay must be bit-identical");
            }
        }

        [Test]
        public void TamperedSubmissionsAreRejected()
        {
            var rng = new System.Random(5);
            var g = RandomGame(rng);
            StormSubmission submission = null;
            g.StormEnded += (r, f, s) => submission = s;
            Assert.IsTrue(g.StartTournament());
            while (g.IsStorming) g.Tick(0.1);
            Assert.IsNotNull(submission);
            Assert.IsTrue(StormValidator.Validate(_c, StormSubmission.FromJson(submission.ToJson()), out _, out var ok), ok);

            var inflated = StormSubmission.FromJson(submission.ToJson());
            inflated.Score *= 1.001;
            Assert.IsFalse(StormValidator.Validate(_c, inflated, out _, out _));

            var hacked = StormSubmission.FromJson(submission.ToJson().Replace("\"pump\":", "\"pump\":9"));
            Assert.IsFalse(StormValidator.Validate(_c, hacked, out _, out _));

            var wrongVersion = StormSubmission.FromJson(submission.ToJson());
            wrongVersion.Version++;
            Assert.IsFalse(StormValidator.Validate(_c, wrongVersion, out _, out var err));
            Assert.AreEqual("content version mismatch", err);

            var fakeStyle = StormSubmission.FromJson(submission.ToJson());
            fakeStyle.Style.Add("skin_does_not_exist");
            Assert.IsFalse(StormValidator.Validate(_c, fakeStyle, out _, out err));
        }

        [Test]
        public void OnlyTournamentStormsAreSubmitted()
        {
            var g = RandomGame(new System.Random(3));
            StormSubmission submission = null;
            RecordFlags flags = default;
            g.StormEnded += (r, f, s) =>
            {
                submission = s;
                flags = f;
            };
            g.StormCharge = 1;
            Assert.IsTrue(g.Unleash());
            while (g.IsStorming) g.Tick(0.1);
            Assert.IsTrue(flags.AllTime, "the first storm is a personal best");
            Assert.IsNull(submission, "grinding storms never reach the leaderboards");

            Assert.IsTrue(g.StartTournament());
            Assert.IsTrue(g.IsTournamentRun);
            while (g.IsStorming) g.Tick(0.1);
            Assert.IsNotNull(submission);
            Assert.Greater(g.Tournament.Best, 0);
        }

        [Test]
        public void TournamentsCapStylesAndIgnoreDecorAndSurges()
        {
            var g = new Game(_c);
            g.SetTime(1790451690);
            g.SetProgress(20, 0);
            foreach (var id in new[] { "pump", "hydro", "condenser", "tesla", "furnace" }) g.Build.Machines[_c.MachineIndex(id)] = 25;
            g.MarkDirty();
            g.Refresh();
            var plain = ModifierSet.Create(_c, g.CreateTournamentSetup().Build);

            g.OwnedCosmetics.Add("skin_steam");
            g.EquipCosmetic(_c.CosmeticIndex("skin_steam"));
            g.Build.DecorBonus = 0.25;
            g.SurgeSeconds = 600;
            g.MarkDirty();
            g.Refresh();
            Assert.Greater(g.IncomeMultiplier, 2.4, "decorations and the surge boost normal income");

            var setup = g.CreateTournamentSetup();
            Assert.AreEqual(0, setup.Build.DecorBonus);
            Assert.AreEqual(g.WeeklyCondition, setup.Build.Weekly, "the weekly condition always applies");
            var capped = ModifierSet.Create(_c, setup.Build);
            var ctxA = new EvalContext(_c.Boosts.Length);
            var a = new Snapshot(_c);
            var b = new Snapshot(_c);
            Formula.Evaluate(_c, plain, ctxA, a);
            Formula.Evaluate(_c, capped, new EvalContext(_c.Boosts.Length), b);
            double ratio = b.Stat[(int)StatId.BasePressure] / a.Stat[(int)StatId.BasePressure];
            Assert.AreEqual(1 + _c.B.TournamentStyleCap, ratio, 1e-9, "Brass Steamworks x1.30 is capped at x1.10");
        }

        [Test]
        public void TournamentAttemptsAreLimitedPerDay()
        {
            var g = RandomGame(new System.Random(11));
            int attempts = _c.B.TournamentAttemptsPerDay;
            for (int i = 0; i < attempts; i++)
            {
                Assert.IsTrue(g.StartTournament(), "attempt " + (i + 1));
                while (g.IsStorming) g.Tick(0.1);
            }
            Assert.AreEqual(0, g.TournamentAttemptsLeft);
            Assert.IsFalse(g.StartTournament());

            int ad = _c.AdIndex("ad_tourney");
            Assert.IsTrue(g.GrantAd(ad));
            Assert.AreEqual(1, g.TournamentAttemptsLeft, "an ad adds one attempt");
            Assert.IsTrue(g.StartTournament());
            while (g.IsStorming) g.Tick(0.1);

            g.SetTime(g.NowUnix + 86400);
            Assert.AreEqual(attempts, g.TournamentAttemptsLeft, "attempts refill every day");
        }

        [Test]
        public void TournamentRewardsArePaidOncePerWeek()
        {
            var g = RandomGame(new System.Random(12));
            Assert.IsTrue(g.StartTournament());
            while (g.IsStorming) g.Tick(0.1);
            int week = g.Week;
            Assert.AreEqual(-1, g.UnclaimedTournamentWeek, "the current week is still running");
            g.SetTime(WeekClock.WeekEnd(week) + 60);
            Assert.AreEqual(week, g.UnclaimedTournamentWeek);
            long gems = g.Gems;
            Assert.AreEqual(_c.B.TournamentRankGems[0], g.ClaimTournamentReward(week, 1));
            Assert.AreEqual(gems + _c.B.TournamentRankGems[0], g.Gems);
            Assert.AreEqual(0, g.ClaimTournamentReward(week, 1), "each week pays once");
            Assert.AreEqual(-1, g.UnclaimedTournamentWeek);
            Assert.AreEqual(_c.B.TournamentRankGems[_c.B.TournamentRankGems.Length - 1], g.TournamentRewardFor(0), "taking part always pays something");
        }

        [Test]
        public void PermanentUpgradesNeverAffectStormScores()
        {
            var rng = new System.Random(8);
            var g = RandomGame(rng);
            var withUpgrade = g.Build.Clone();
            withUpgrade.Upgrades[_c.UpgradeIndex("offline_generator")] = 5;
            var plain = new StormSetup { Build = g.Build.Clone(), BoostActiveTicks = new int[_c.Boosts.Length], BoostCooldownTicks = new int[_c.Boosts.Length] };
            var upgraded = new StormSetup { Build = withUpgrade, BoostActiveTicks = new int[_c.Boosts.Length], BoostCooldownTicks = new int[_c.Boosts.Length] };
            var a = StormSim.Replay(_c, plain, Array.Empty<StormEvent>(), out _);
            var b = StormSim.Replay(_c, upgraded, Array.Empty<StormEvent>(), out _);
            Assert.AreEqual(a.Score, b.Score, 0);
        }

        [Test]
        public void ChaosReactorStormsCollapseAtFullInstability()
        {
            var g = new Game(_c);
            g.Build.Weekly = -1;
            foreach (var id in new[] { "pump", "furnace", "hydro", "turbine", "condenser", "tesla" }) g.Build.Machines[_c.MachineIndex(id)] = 30;
            g.Build.Machines[_c.MachineIndex("chamber")] = 30;
            int chaos = _c.ModuleIndex("chaos_reactor");
            g.Build.ModuleLevels[chaos] = 1;
            g.Build.Equipped.Add(chaos);
            g.Build.Research[_c.ResearchIndex("long_storms")] = 5;
            g.MarkDirty();
            g.Refresh();
            var setup = new StormSetup { Build = g.Build.Clone(), BoostActiveTicks = new int[_c.Boosts.Length], BoostCooldownTicks = new int[_c.Boosts.Length] };
            var r = StormSim.Replay(_c, setup, Array.Empty<StormEvent>(), out bool legal);
            Assert.IsTrue(legal);
            Assert.IsTrue(r.Collapsed, "37s storm must collapse before the timer runs out");
            Assert.IsTrue(r.PhenoSeen[_c.PhenoIndex("anomaly")], "Anomaly appears past 40% instability");
        }

        Game RandomGame(System.Random rng)
        {
            var g = new Game(_c);
            g.SetTime(1790451690 + rng.Next(0, 30 * 86400));
            g.SetProgress(Math.Max(8, g.FeatureLevel("tournament")) + rng.Next(0, 20), 0);
            // Random style items: their bonuses are part of the replayed build (capped in tournaments).
            for (int slot = 0; slot < 5; slot++)
            {
                var options = Enumerable.Range(0, _c.Cosmetics.Length).Where(i => (int)_c.Cosmetics[i].Slot == slot).ToList();
                int pick = options[rng.Next(options.Count)];
                g.OwnedCosmetics.Add(_c.Cosmetics[pick].Id);
                g.EquipCosmetic(pick);
            }
            for (int m = 0; m < _c.Machines.Length; m++)
            {
                int max = _c.Machines[m].MaxLevel > 0 ? _c.Machines[m].MaxLevel : 200;
                g.Build.Machines[m] = rng.Next(0, max + 1);
            }
            g.Build.Machines[_c.MachineIndex("pump")] = Math.Max(1, g.Build.Machines[_c.MachineIndex("pump")]);
            for (int r = 0; r < _c.Research.Length; r++)
                g.Build.Research[r] = rng.Next(0, Math.Min(_c.Research[r].MaxLevel, 8) + 1);
            g.MarkDirty();
            g.Refresh();
            foreach (var m in Enumerable.Range(0, _c.Modules.Length).OrderBy(_ => rng.Next()).Take(rng.Next(0, g.ModuleSlots + 1)))
            {
                g.Build.ModuleLevels[m] = rng.Next(1, _c.Modules[m].MaxLevel + 1);
                g.Build.Equipped.Add(m);
            }
            g.MarkDirty();
            g.Refresh();
            for (int b = 0; b < _c.Boosts.Length; b++)
            {
                if (!g.IsBoostUnlocked(b)) continue;
                if (rng.NextDouble() < 0.3) g.BoostActive[b] = rng.NextDouble() * g.Mods.BoostDuration[b];
                else if (rng.NextDouble() < 0.3) g.BoostCooldown[b] = rng.NextDouble() * g.Mods.BoostCooldown[b];
            }
            return g;
        }
    }

    public class ProgressionTests
    {
        Content _c;

        [SetUp]
        public void SetUp() => _c = Content.CreateDefault();

        Game WithGenerator(int level)
        {
            var g = new Game(_c);
            g.Build.Weekly = -1;
            g.Build.Machines[_c.MachineIndex("pump")] = 10;
            g.Build.Upgrades[_c.UpgradeIndex("offline_generator")] = level;
            g.MarkDirty();
            g.Refresh();
            return g;
        }

        [Test]
        public void OfflineProgressIsLockedUntilTheGeneratorIsBuilt()
        {
            var g = WithGenerator(0);
            Assert.IsFalse(g.OfflineUnlocked);
            double energy = g.Energy;
            var r = g.ApplyOffline(2 * 3600);
            Assert.IsTrue(r.Locked);
            Assert.AreEqual(0, r.Energy);
            Assert.AreEqual(energy, g.Energy, "no Energy is produced offline without the generator");
            Assert.AreEqual(1, g.StormCharge, 1e-9, "the storm chamber still charges");
        }

        [Test]
        public void OfflineEarningsUseGeneratorEfficiencyAndCap()
        {
            var g = WithGenerator(1);
            Assert.IsTrue(g.OfflineUnlocked);
            double sp = g.Snap.StormPower;
            var hour = g.ApplyOffline(3600);
            Assert.IsFalse(hour.Locked);
            Assert.AreEqual(0.20, hour.Efficiency, 1e-12);
            Assert.AreEqual(sp * 3600 * 0.20, hour.Energy, 1e-6);
            Assert.AreEqual(0, hour.Storms, "no storms offline without Storm Automation");

            var day = g.ApplyOffline(10 * 3600);
            Assert.IsTrue(day.Capped);
            Assert.AreEqual(3600, day.CountedSeconds, 1e-9, "level 1 counts one hour");

            var better = WithGenerator(3);
            Assert.AreEqual(3, better.Snap.Stat[(int)StatId.OfflineCapHours], 1e-12);
            Assert.AreEqual(0.30, better.Snap.Stat[(int)StatId.OfflineEfficiency], 1e-12);
        }

        [Test]
        public void OfflineGeneratorIsExpensiveLevelGatedAndPermanent()
        {
            int u = _c.UpgradeIndex("offline_generator");
            var g = new Game(_c);
            g.Energy = 1e12;
            g.Cores = 1000;
            Assert.AreEqual(5e8, g.UpgradeCost(u));
            Assert.AreEqual(40, g.UpgradeCoreCost(u));
            Assert.IsFalse(g.CanBuyUpgrade(u), "locked below Engine Level 10");

            g.SetProgress(g.FeatureLevel("offline"), 0);
            Assert.IsTrue(g.BuyUpgrade(u));
            Assert.AreEqual(1e12 - 5e8, g.Energy, 1e-3);
            Assert.AreEqual(960, g.Cores);
            Assert.IsTrue(g.OfflineUnlocked);
            Assert.Greater(g.UpgradeCost(u), 5e8, "each level costs more");

            g.SetProgress(g.FeatureLevel("research"), 0);
            g.RunEnergy = 1e10;
            Assert.IsTrue(g.Prestige());
            Assert.AreEqual(1, g.Build.Upgrades[u], "resets keep permanent upgrades");
            Assert.IsTrue(g.OfflineUnlocked);
        }

        [Test]
        public void AutomationRunsStormsWhileOffline()
        {
            var g = WithGenerator(1);
            g.Build.Research[_c.ResearchIndex("automation")] = 1;
            g.MarkDirty();
            g.Refresh();
            var r = g.ApplyOffline(3600);
            Assert.Greater(r.Storms, 30);
            Assert.Greater(r.Cores, 0);
        }

        [Test]
        public void EngineLevelsUnlockMachinesAndFeaturesGradually()
        {
            var g = new Game(_c);
            Assert.AreEqual(1, g.Level);
            Assert.IsTrue(g.MachineUnlocked[_c.MachineIndex("pump")]);
            Assert.IsTrue(g.MachineUnlocked[_c.MachineIndex("hydro")]);
            Assert.IsFalse(g.MachineUnlocked[_c.MachineIndex("condenser")]);
            Assert.IsFalse(g.MachineUnlocked[_c.MachineIndex("turbine")]);
            foreach (var f in new[] { "atmosphere", "customize", "modules", "island", "tournament", "weekly", "offline", "research" })
            {
                Assert.IsNotNull(_c.Feature(f), f + " exists");
                Assert.IsFalse(g.IsFeatureUnlocked(f), f + " must start locked");
            }

            int levels = 0;
            g.LeveledUp += _ => levels++;
            g.AddXp(g.XpToNext + _c.XpToNext(2) + 1);
            Assert.AreEqual(3, g.Level);
            Assert.AreEqual(2, levels);
            Assert.IsTrue(g.MachineUnlocked[_c.MachineIndex("condenser")]);
            Assert.IsTrue(g.MachineUnlocked[_c.MachineIndex("tesla")]);
            Assert.IsTrue(g.IsFeatureUnlocked("atmosphere"));
            Assert.IsFalse(g.IsFeatureUnlocked("tournament"));
            CollectionAssert.Contains(g.UnlocksAtLevel(8), "Weekly Tournament");
            Assert.AreEqual(2 * _c.B.GemsPerLevel, g.Gems, "each level pays Gems");
        }

        [Test]
        public void WeeklyConditionsStartWithTheTournament()
        {
            var g = new Game(_c);
            g.SetTime(1790451690);
            g.SetProgress(2, 0);
            Assert.AreEqual(-1, g.Build.Weekly, "no hidden weekly modifiers for new players");
            g.SetProgress(g.FeatureLevel("weekly"), 0);
            Assert.AreEqual(g.WeeklyCondition, g.Build.Weekly);
            Assert.GreaterOrEqual(g.Build.Weekly, 0);
        }

        [Test]
        public void XpCurveGrowsWithEveryLevel()
        {
            Assert.AreEqual(42, _c.XpToNext(1));
            for (int l = 1; l < _c.B.MaxEngineLevel; l++) Assert.Greater(_c.XpToNext(l + 1), _c.XpToNext(l));
        }

        [Test]
        public void PrestigeConvertsRunEnergyIntoResearchAndKeepsModules()
        {
            var g = new Game(_c);
            g.Build.Weekly = -1;
            g.SetProgress(g.FeatureLevel("research"), 0);
            g.RunEnergy = 1e8;
            g.Build.Machines[_c.MachineIndex("pump")] = 50;
            int core = _c.ModuleIndex("thermal_core");
            g.Build.ModuleLevels[core] = 3;
            g.Build.Equipped.Add(core);
            g.MarkDirty();
            g.Refresh();
            Assert.AreEqual(9, g.ResearchOnReset(), "(log10(1e8) - 5)^2 = 9");
            Assert.IsTrue(g.Prestige());
            Assert.AreEqual(9, g.Research);
            Assert.AreEqual(0, g.Build.Machines[_c.MachineIndex("pump")]);
            Assert.AreEqual(3, g.Build.ModuleLevels[core]);
            Assert.IsTrue(g.Build.IsEquipped(core));
            Assert.AreEqual(1, g.Prestiges);
        }

        [Test]
        public void PrestigeIsLockedBeforeTheResearchLab()
        {
            var g = new Game(_c);
            g.RunEnergy = 1e12;
            Assert.IsFalse(g.Prestige());
            Assert.AreEqual(0, g.Prestiges);
        }

        [Test]
        public void GoalsCompleteInOrderAndPayRewards()
        {
            var g = new Game(_c);
            g.Energy = 1e6;
            Assert.AreEqual("g_pump5", g.CurrentGoal.Id);
            Assert.IsTrue(g.BuyMachine(_c.MachineIndex("pump"), 5));
            Assert.AreEqual("g_hydro", g.CurrentGoal.Id);
            double xp = g.Xp;
            Assert.IsTrue(g.BuyMachine(_c.MachineIndex("hydro"), 1));
            Assert.AreEqual("g_storm1", g.CurrentGoal.Id);
            Assert.Greater(g.Xp, xp, "buying and finishing goals earns XP");
        }

        [Test]
        public void SaveRoundTripRestoresTheGame()
        {
            var g = new Game(_c);
            g.SetTime(1790451690);
            g.Energy = 12345.5;
            g.Cores = 77;
            g.Research = 5;
            g.Prestiges = 2;
            g.SetProgress(12, 17);
            g.Build.Machines[_c.MachineIndex("furnace")] = 42;
            int core = _c.ModuleIndex("thermal_core");
            g.Build.ModuleLevels[core] = 4;
            g.Build.Equipped.Add(core);
            g.Build.Research[_c.ResearchIndex("engine_efficiency")] = 3;
            g.Build.Upgrades[_c.UpgradeIndex("offline_generator")] = 2;
            g.Discovered[_c.PhenoIndex("monsoon")] = true;
            g.Records.BestScore = 9.5e9;
            g.Records.BestClass = _c.PhenoIndex("monsoon");
            g.Records.ClassBest[_c.PhenoIndex("monsoon") + 1] = 9.5e9;
            int world = _c.CosmeticIndex("world_snow");
            g.OwnedCosmetics.Add("world_snow");
            g.EquipCosmetic(world);
            g.Gems = 321;
            g.DecorOwned["dec_pond"] = 1;
            g.DecorOwned["dec_ducks"] = 2;
            g.Placed.Add(new PlacedDecor { Id = "dec_pond", X = 5.5f, Z = -3.25f, Rot = 90 });
            g.Placed.Add(new PlacedDecor { Id = "dec_ducks", X = 5.5f, Z = -3.25f, Rot = 10 });
            g.Expansions.Add("exp_1");
            g.BundleAds["bundle_volcano"] = 7;
            g.OwnedProducts.Add("com.stormengine.starter");
            g.Tournament.Week = g.Week;
            g.Tournament.Best = 4.5e6;
            g.Tournament.AllTimeBest = 8e6;
            g.Tournament.PlayedWeek = g.Week;
            g.Tournament.RewardedWeek = g.Week - 1;
            g.Tournament.Runs = 9;
            g.SurgeSeconds = 120;
            g.SyncDecor();

            string json = JsonUtility.ToJson(SaveMapper.Capture(g, null));
            var restored = new Game(_c);
            SaveMapper.Apply(JsonUtility.FromJson<SaveData>(json), restored);

            Assert.AreEqual(g.Energy, restored.Energy);
            Assert.AreEqual(77, restored.Cores);
            Assert.AreEqual(5, restored.Research);
            Assert.AreEqual(2, restored.Prestiges);
            Assert.AreEqual(12, restored.Level);
            Assert.AreEqual(17, restored.Xp, 1e-9);
            Assert.AreEqual(42, restored.Build.Machines[_c.MachineIndex("furnace")]);
            Assert.AreEqual(4, restored.Build.ModuleLevels[core]);
            Assert.IsTrue(restored.Build.IsEquipped(core));
            Assert.AreEqual(3, restored.Build.Research[_c.ResearchIndex("engine_efficiency")]);
            Assert.AreEqual(2, restored.Build.Upgrades[_c.UpgradeIndex("offline_generator")]);
            Assert.IsTrue(restored.Discovered[_c.PhenoIndex("monsoon")]);
            Assert.AreEqual(9.5e9, restored.Records.BestScore);
            Assert.AreEqual(_c.PhenoIndex("monsoon"), restored.Records.BestClass);
            Assert.AreEqual("world_snow", restored.Equipped(CosmeticSlot.World).Id);
            Assert.AreEqual(_c.CosmeticIndex("world_snow"), restored.Build.Style[(int)CosmeticSlot.World], "style bonuses come back");
            Assert.AreEqual(321, restored.Gems);
            Assert.AreEqual(2, restored.DecorCopies("dec_ducks"));
            Assert.AreEqual(2, restored.Placed.Count);
            Assert.AreEqual(5.5f, restored.Placed[0].X, 1e-4);
            Assert.AreEqual(0.02, restored.Build.DecorBonus, 1e-12, "two kinds owned");
            Assert.IsTrue(restored.IsExpansionOwned(_c.ExpansionIndex("exp_1")));
            Assert.AreEqual(7, restored.BundleProgress("bundle_volcano"));
            Assert.IsTrue(restored.IsProductOwned("com.stormengine.starter"));
            Assert.AreEqual(4.5e6, restored.Tournament.Best);
            Assert.AreEqual(8e6, restored.Tournament.AllTimeBest);
            Assert.AreEqual(g.Week, restored.Tournament.PlayedWeek);
            Assert.AreEqual(9, restored.Tournament.Runs);
            Assert.AreEqual(120, restored.SurgeSeconds, 1e-9);
        }

        [Test]
        public void VersionOneSavesAreMigratedToEngineLevels()
        {
            var old = new SaveData { version = 1, playerId = "p", energy = 100, engineLevel = 1 };
            old.machines.Add(new IdLevel { id = "pump", level = 40 });
            old.machines.Add(new IdLevel { id = "hydro", level = 30 });
            old.stats.storms = 20;
            old.discovered.Add("thunderstorm");
            var g = new Game(_c);
            SaveMapper.Apply(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(old)), g);
            Assert.Greater(g.Level, 3, "earlier progress becomes XP");
            Assert.AreEqual(40, g.Build.Machines[_c.MachineIndex("pump")]);
            Assert.IsTrue(g.MachineUnlocked[_c.MachineIndex("condenser")]);
        }
    }

    public class MetaTests
    {
        Content _c;

        [SetUp]
        public void SetUp() => _c = Content.CreateDefault();

        Game NewGame(int level)
        {
            var g = new Game(_c);
            g.SetTime(1790451690);
            g.SetProgress(level, 0);
            g.Build.Machines[_c.MachineIndex("pump")] = 10;
            g.MarkDirty();
            g.Refresh();
            return g;
        }

        [Test]
        public void DecorationsGiveACollectionBonusOncePerKind()
        {
            var g = NewGame(8);
            g.Cores = 10000;
            double power = g.Snap.StormPower;
            int flowers = _c.DecorationIndex("dec_flowerbed");
            int bench = _c.DecorationIndex("dec_bench");
            Assert.IsTrue(g.BuyDecoration(flowers));
            Assert.IsTrue(g.BuyDecoration(flowers));
            Assert.AreEqual(_c.Decorations[flowers].Bonus, g.Build.DecorBonus, 1e-12, "copies of the same kind count once");
            Assert.IsTrue(g.BuyDecoration(bench));
            double bonus = _c.Decorations[flowers].Bonus + _c.Decorations[bench].Bonus;
            Assert.AreEqual(bonus, g.Build.DecorBonus, 1e-12);
            Assert.AreEqual(1 + bonus, g.IncomeMultiplier, 1e-12, "the bonus is Energy income");
            Assert.AreEqual(power, g.Snap.StormPower, 0, "decorations never change Storm Power or scores");
            Assert.AreEqual(3, g.StoredCopies("dec_flowerbed") + g.StoredCopies("dec_bench"), "bought copies wait to be placed");
        }

        [Test]
        public void DecorationsAreGatedByLevelCapacityAndCopies()
        {
            var g = NewGame(3);
            g.Cores = 100000;
            g.Gems = 100000;
            int flowers = _c.DecorationIndex("dec_flowerbed");
            Assert.IsFalse(g.CanBuyDecoration(flowers), "Island Decor is locked before its level");
            g.SetProgress(g.FeatureLevel("island"), 0);
            var def = _c.Decorations[flowers];
            for (int i = 0; i < def.MaxCopies; i++) Assert.IsTrue(g.BuyDecoration(flowers));
            Assert.IsFalse(g.BuyDecoration(flowers), "copy limit");

            int whale = _c.DecorationIndex("dec_whale");
            Assert.IsFalse(g.CanBuyDecoration(whale), "higher-level decorations wait for their level");
            int limited = _c.FeaturedDecoration(g.Week);
            for (int i = 0; i < _c.Decorations.Length; i++)
                if (_c.Decorations[i].Limited && i != limited) Assert.IsFalse(g.IsDecorationOnSale(i), "limited items are only sold in their week");
        }

        [Test]
        public void WaterDecorationsNeedAPond()
        {
            var g = NewGame(10);
            g.Cores = 100000;
            int ducks = _c.DecorationIndex("dec_ducks");
            Assert.IsFalse(g.CanBuyDecoration(ducks), "no pond yet");
            Assert.IsTrue(g.BuyDecoration(_c.DecorationIndex("dec_pond")));
            Assert.IsTrue(g.AddPlacement(new PlacedDecor { Id = "dec_pond", X = 5, Z = 5 }));
            Assert.AreEqual(3, g.WaterSlotsFree);
            Assert.IsTrue(g.BuyDecoration(ducks));
            Assert.IsTrue(g.AddPlacement(new PlacedDecor { Id = "dec_ducks", X = 5, Z = 5 }));
            Assert.AreEqual(2, g.WaterSlotsFree);
            Assert.IsFalse(g.AddPlacement(new PlacedDecor { Id = "dec_ducks", X = 5, Z = 5 }), "no stored copy left");
        }

        [Test]
        public void ExpansionsGrowTheIslandInOrder()
        {
            var g = NewGame(20);
            g.Gems = 5000;
            int e1 = _c.ExpansionIndex("exp_1"), e2 = _c.ExpansionIndex("exp_2");
            int capacity = g.DecorCapacity;
            Assert.IsFalse(g.CanBuyExpansion(e2), "expansions are bought in order");
            Assert.IsTrue(g.BuyExpansion(e1));
            Assert.AreEqual(_c.Expansions[e1].Radius, g.IslandRadius, 1e-6);
            Assert.AreEqual(capacity + _c.Expansions[e1].Capacity, g.DecorCapacity);
            Assert.AreEqual(5000 - _c.Expansions[e1].CostGems, g.Gems);
            Assert.IsFalse(g.BuyExpansion(e1), "owned once");
            Assert.IsTrue(g.BuyExpansion(e2));
            Assert.AreEqual(_c.Expansions[e2].Radius, g.IslandRadius, 1e-6);
        }

        [Test]
        public void GemItemsCostGemsAndBundleItemsAreNotForSale()
        {
            var g = NewGame(12);
            int steam = _c.CosmeticIndex("skin_steam");
            Assert.IsTrue(_c.Cosmetics[steam].Exclusive);
            Assert.IsFalse(g.CanUnlockCosmetic(steam), "not enough Gems");
            g.Gems = _c.Cosmetics[steam].CostGems;
            Assert.IsTrue(g.UnlockCosmetic(steam));
            Assert.AreEqual(0, g.Gems);
            Assert.AreEqual("skin_steam", g.Equipped(CosmeticSlot.Skin).Id);

            int magma = _c.CosmeticIndex("skin_magma");
            g.Gems = 100000;
            Assert.IsFalse(g.IsCosmeticForSale(magma));
            Assert.IsFalse(g.UnlockCosmetic(magma), "bundle items only come from their bundle");
        }

        [Test]
        public void AdsOnlyBundlesUnlockAFullEnvironment()
        {
            var g = NewGame(10);
            int b = _c.BundleIndex("bundle_volcano");
            int ad = _c.AdIndex("ad_bundle");
            int completed = -1;
            g.BundleCompleted += x => completed = x;
            var def = _c.Bundles[b];
            for (int i = 0; i < def.AdsRequired; i++)
            {
                if (g.AdUsesLeft(ad) == 0) g.SetTime(g.NowUnix + 86400);
                Assert.IsTrue(g.GrantAd(ad, def.Id), "ad " + (i + 1));
            }
            Assert.AreEqual(b, completed);
            Assert.IsTrue(g.IsBundleComplete(b));
            foreach (var id in def.Cosmetics) Assert.IsTrue(g.OwnedCosmetics.Contains(id), id);
            foreach (var id in def.Decorations) Assert.AreEqual(1, g.DecorCopies(id), id);
            Assert.IsFalse(g.CanWatchAd(ad, def.Id), "a finished bundle takes no more ads");
            Assert.IsFalse(g.CanWatchAd(ad, "bundle_neon"), "Neon Nights needs Engine Level 12");
        }

        [Test]
        public void RewardedAdsRespectDailyLimits()
        {
            var g = NewGame(5);
            int ad = _c.AdIndex("ad_gems");
            var def = _c.AdPlacements[ad];
            long gems = g.Gems;
            for (int i = 0; i < def.DailyLimit; i++) Assert.IsTrue(g.GrantAd(ad));
            Assert.AreEqual(gems + (long)(def.Amount * def.DailyLimit), g.Gems);
            Assert.IsFalse(g.GrantAd(ad), "daily limit");
            g.SetTime(g.NowUnix + 86400);
            Assert.IsTrue(g.GrantAd(ad), "limits reset daily");

            var low = NewGame(1);
            Assert.IsFalse(low.CanWatchAd(ad), "ads unlock with Engine Level");
        }

        [Test]
        public void EnergySurgeDoublesIncomeForAWhile()
        {
            var g = NewGame(5);
            int ad = _c.AdIndex("ad_surge");
            double income = g.EnergyPerSecond;
            Assert.IsTrue(g.GrantAd(ad));
            Assert.AreEqual(income * _c.B.SurgeMultiplier, g.EnergyPerSecond, income * 1e-12);
            for (int i = 0; i < 10; i++) g.GrantAd(ad);
            Assert.LessOrEqual(g.SurgeSeconds, _c.B.SurgeMaxSeconds, "surges stack up to the cap");
            g.Tick(g.SurgeSeconds + 1);
            Assert.AreEqual(0, g.SurgeSeconds, 1e-9);
            Assert.AreEqual(income, g.EnergyPerSecond, income * 1e-12);
        }

        [Test]
        public void StarterPackIsSoldOnce()
        {
            var g = NewGame(3);
            int p = _c.ProductIndex("com.stormengine.starter");
            long gems = g.Gems;
            Assert.IsTrue(g.GrantProduct(p));
            Assert.AreEqual(gems + _c.Products[p].Gems, g.Gems);
            Assert.IsTrue(g.OwnedCosmetics.Contains("title_founder"));
            Assert.IsFalse(g.GrantProduct(p), "one per player");
            int pack = _c.ProductIndex("com.stormengine.gems.550");
            Assert.IsTrue(g.GrantProduct(pack));
            Assert.IsTrue(g.GrantProduct(pack), "gem packs can be bought again");
        }

        [Test]
        public void IslandLayoutsRoundTripAndAreSanitized()
        {
            var g = NewGame(12);
            g.OwnedCosmetics.Add("skin_royal");
            g.EquipCosmetic(_c.CosmeticIndex("skin_royal"));
            g.DecorOwned["dec_bench"] = 1;
            g.Placed.Add(new PlacedDecor { Id = "dec_bench", X = 6.25f, Z = -2.5f, Rot = 45 });
            var layout = IslandLayout.FromGame(g, "Tester");
            var back = IslandLayout.FromJson(layout.ToJson(), _c);
            Assert.AreEqual("Tester", back.Name);
            Assert.AreEqual("skin_royal", back.Skin);
            Assert.AreEqual(12, back.Level);
            Assert.AreEqual(1, back.Decor.Count);
            Assert.AreEqual(6.25f, back.Decor[0].X, 1e-4);
            Assert.AreEqual(10, back.MachineLevel("pump"));

            string evil = "{\"name\":\"" + new string('x', 200) + "\",\"skin\":\"cloud_candy\",\"level\":9999,\"radius\":500," +
                          "\"machines\":{\"pump\":1e30,\"nope\":5},\"decor\":[[\"dec_bench\",1e9,0,0],[\"dec_fake\",1,1,0],[\"dec_bench\",7,1,0]]}";
            var clean = IslandLayout.FromJson(evil, _c);
            Assert.IsNotNull(clean);
            Assert.LessOrEqual(clean.Name.Length, 24);
            Assert.IsNull(clean.Skin, "a cloud id is not a skin");
            Assert.AreEqual(_c.B.MaxEngineLevel, clean.Level);
            Assert.LessOrEqual(clean.Radius, _c.Expansions[_c.Expansions.Length - 1].Radius);
            Assert.LessOrEqual(clean.MachineLevel("pump"), 100000);
            Assert.AreEqual(0, clean.MachineLevel("nope"));
            Assert.AreEqual(1, clean.Decor.Count, "out-of-range and unknown decorations are dropped");
            Assert.IsNull(IslandLayout.FromJson("not json", _c));
        }

        [Test]
        public void ShowcaseLikeLayoutsKeepDecorClearOfTheMachine()
        {
            var g = NewGame(20);
            g.Cores = 1000000;
            int bench = _c.DecorationIndex("dec_bench");
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(g.BuyDecoration(bench));
                Assert.IsTrue(StormEngine.World.DecorRig.FindSpot(_c, _c.Decorations[bench], g.Placed, g.IslandRadius, null, 100 + i, out var spot));
                Assert.Greater(spot.magnitude, 3.2f, "never on the platform");
                Assert.IsTrue(g.AddPlacement(new PlacedDecor { Id = "dec_bench", X = spot.x, Z = spot.y }));
            }
        }
    }

    public class UtilTests
    {
        // The formats under test are the English ones; a Play session may have left another language loaded.
        [SetUp]
        public void UseEnglish() => L.Use("en", null);

        [Test]
        public void WeeksStartOnMondayUtc()
        {
            int week = WeekClock.WeekOf(1790451690);
            var start = DateTimeOffset.FromUnixTimeSeconds(WeekClock.WeekStart(week));
            Assert.AreEqual(DayOfWeek.Monday, start.DayOfWeek);
            Assert.AreEqual(0, start.Hour);
            Assert.AreEqual(week + 1, WeekClock.WeekOf(WeekClock.WeekEnd(week)));
            Assert.AreEqual(week, WeekClock.WeekOf(WeekClock.WeekEnd(week) - 1));
        }

        [Test]
        public void NumbersUseReadableSuffixes()
        {
            Num.Notation = 0;
            Assert.AreEqual("999", Num.Format(999));
            Assert.AreEqual("1.23K", Num.Format(1234));
            Assert.AreEqual("12.4M", Num.Format(12.4e6));
            Assert.AreEqual("1.00B", Num.Format(999.9e6));
            Assert.AreEqual("x4.2", Num.Mult(4.2));
            Assert.AreEqual("1h 5m", Num.Time(3900));
        }

        [Test]
        public void ContentExportIsValidJson()
        {
            string json = ContentExport.ToJson(Content.CreateDefault());
            Assert.IsTrue(JsonReader.TryParse(json, out var root));
            Assert.AreEqual(Content.CreateDefault().Version, (int)JsonReader.Num(root, "version"));
        }
    }
}
