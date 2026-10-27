using System;
using System.Collections.Generic;
using NUnit.Framework;
using StormEngine.Core;
using UnityEngine;

namespace StormEngine.Tests
{
    public class DailyTests
    {
        const long T0 = 1790451690;
        const long Day = 86400;
        Content _c;

        [SetUp]
        public void SetUp() => _c = Content.CreateDefault();

        Game NewGame(int level)
        {
            var g = new Game(_c);
            g.SetProgress(level, 0);
            g.SetTime(T0);
            g.Refresh();
            return g;
        }

        [Test]
        public void EachDayOffersThreeDifferentMissionsThePlayerCanDo()
        {
            for (int level = 2; level <= 30; level += 7)
            {
                var g = NewGame(level);
                var kinds = new HashSet<MissionKind>();
                for (int i = 0; i < DailyState.MissionCount; i++)
                {
                    var m = g.MissionAt(i);
                    Assert.IsNotNull(m, $"level {level}, slot {i}");
                    Assert.IsTrue(kinds.Add(m.Kind), "never two missions of the same kind");
                    Assert.LessOrEqual(m.MinLevel, level);
                    Assert.IsTrue(m.MaxLevel == 0 || m.MaxLevel >= level);
                }
            }
            CollectionAssert.AreEqual(NewGame(10).Daily.Missions, NewGame(10).Daily.Missions, "same missions for everyone on the same day");
            var early = NewGame(2);
            foreach (var id in early.Daily.Missions)
            {
                var kind = _c.Missions[_c.MissionIndex(id)].Kind;
                Assert.AreNotEqual(MissionKind.Tournament, kind, "no tournament mission before the tournament unlocks");
                Assert.AreNotEqual(MissionKind.MegaStorms, kind);
            }
        }

        [Test]
        public void LoginStreakGrowsDailyAndBreaksAfterAMissedDay()
        {
            var g = NewGame(5);
            Assert.IsTrue(g.LoginRewardReady);
            long gems = g.Gems;
            Assert.AreSame(_c.LoginRewards[0], g.ClaimLoginReward());
            Assert.AreEqual(gems + _c.LoginRewards[0].Gems, g.Gems);
            Assert.IsFalse(g.LoginRewardReady, "once per day");
            Assert.IsNull(g.ClaimLoginReward());

            g.SetTime(T0 + Day);
            Assert.AreEqual(2, g.TodayStreakDay);
            g.ClaimLoginReward();
            Assert.AreEqual(2, g.Daily.Streak);

            // One missed day: the streak breaks, but an ad can bring it back.
            g.SetTime(T0 + 3 * Day);
            Assert.AreEqual(0, g.Daily.Streak);
            Assert.AreEqual(1, g.TodayStreakDay);
            Assert.IsTrue(g.CanRestoreStreak);
            int ad = _c.AdIndex("ad_streak");
            Assert.IsTrue(g.GrantAd(ad));
            Assert.AreEqual(2, g.Daily.Streak);
            Assert.AreEqual(3, g.TodayStreakDay);
            Assert.IsFalse(g.CanRestoreStreak);
            Assert.IsFalse(g.CanWatchAd(ad));
            g.ClaimLoginReward();
            Assert.AreEqual(3, g.Daily.Streak);

            // A long absence cannot be restored.
            g.SetTime(T0 + 10 * Day);
            Assert.AreEqual(0, g.Daily.Streak);
            Assert.IsFalse(g.CanRestoreStreak);
            Assert.AreEqual(3, g.Daily.BestStreak);
        }

        [Test]
        public void TurningTheDateBackGrantsNothingAgain()
        {
            var g = NewGame(20);
            int gemsAd = _c.AdIndex("ad_gems");
            g.SetTime(T0 + 2 * Day);
            Assert.IsNotNull(g.ClaimLoginReward());
            Assert.IsTrue(g.GrantAd(gemsAd));
            Assert.Greater(g.TournamentAttemptsLeft, 0);
            g.Tournament.AttemptsUsed = 2;
            var missions = (string[])g.Daily.Missions.Clone();
            int streak = g.Daily.Streak;
            var gems = g.Gems;

            // Flip the phone's date a day back, then forward to the day already claimed.
            foreach (long t in new[] { T0 + Day, T0 + 2 * Day, T0 + Day + 3600 })
            {
                g.SetTime(t);
                Assert.IsFalse(g.LoginRewardReady, "login gift");
                Assert.IsNull(g.ClaimLoginReward());
                Assert.AreEqual(1, g.AdUses(gemsAd), "daily ad limit is not reset");
                Assert.AreEqual(_c.B.TournamentAttemptsPerDay - 2, g.TournamentAttemptsLeft, "tournament attempts are not reset");
                CollectionAssert.AreEqual(missions, g.Daily.Missions, "missions are not rolled again");
            }
            Assert.AreEqual(streak, g.Daily.Streak);
            Assert.AreEqual(gems, g.Gems);

            // The next real day works as usual.
            g.SetTime(T0 + 3 * Day);
            Assert.IsTrue(g.LoginRewardReady);
            Assert.AreEqual(0, g.AdUses(gemsAd));
        }

        [Test]
        public void ASaveEditedOutsideTheGameIsRefused()
        {
            var save = SaveMapper.Capture(NewGame(8), null);
            save.gems = 50;
            string text = SaveSystem.Seal(save);
            Assert.AreEqual(50, SaveSystem.Open(text).gems, "the game's own copy loads");
            string edited = text.Replace("\"gems\":50", "\"gems\":999999");
            Assert.AreNotEqual(text, edited);
            Assert.IsNull(SaveSystem.Open(edited), "edited Gems are refused");
        }

        [Test]
        public void LoginRewardsCycleEverySevenDays()
        {
            var g = NewGame(5);
            long cores = g.Cores;
            for (int day = 0; day < 9; day++)
            {
                g.SetTime(T0 + day * Day);
                Assert.AreSame(_c.LoginRewards[day % 7], g.ClaimLoginReward());
            }
            Assert.AreEqual(9, g.Daily.Streak);
            long expected = 0;
            for (int day = 0; day < 9; day++) expected += g.DailyCores(_c.LoginRewards[day % 7].Cores);
            Assert.AreEqual(cores + expected, g.Cores);
            Assert.Greater(g.DailyCores(100), 100, "Storm Core rewards grow with Engine Level");
        }

        [Test]
        public void MissionsCountProgressAndPayOnce()
        {
            var g = NewGame(8);
            g.Daily.Missions[0] = "m_levels20";
            g.Daily.Missions[1] = "m_tourney";
            g.Daily.Missions[2] = "m_decor";
            int completed = 0;
            g.MissionCompleted += _ => completed++;
            g.Energy = 1e12;
            g.Cores = 100000;

            Assert.IsTrue(g.BuyMachine(_c.MachineIndex("pump"), 15));
            Assert.IsFalse(g.CanClaimMission(0));
            Assert.IsTrue(g.BuyMachine(_c.MachineIndex("pump"), 5));
            Assert.IsTrue(g.CanClaimMission(0));
            long cores = g.Cores;
            Assert.IsNotNull(g.ClaimMission(0));
            Assert.AreEqual(cores + g.DailyCores(25), g.Cores);
            Assert.IsNull(g.ClaimMission(0), "only once");

            Assert.IsTrue(g.StartTournament());
            Assert.IsTrue(g.CanClaimMission(1));
            Assert.IsTrue(g.BuyDecoration(_c.DecorationIndex("dec_bench")));
            Assert.AreEqual(3, completed);
            Assert.AreEqual(2, g.DailyClaimable - (g.LoginRewardReady ? 1 : 0));

            Assert.IsFalse(g.CanClaimMissionBonus, "every mission first");
            g.ClaimMission(1);
            g.ClaimMission(2);
            Assert.IsTrue(g.CanClaimMissionBonus);
            long gems = g.Gems;
            Assert.IsNotNull(g.ClaimMissionBonus());
            Assert.AreEqual(gems + _c.MissionBonus.Gems, g.Gems);
            Assert.IsFalse(g.CanClaimMissionBonus);

            g.SetTime(T0 + Day);
            Assert.AreEqual(0, g.MissionsClaimed, "fresh missions every day");
            Assert.IsFalse(g.Daily.BonusClaimed);
        }

        [Test]
        public void MissionsDoNotCountBeforeDailyRewardsUnlock()
        {
            var g = NewGame(1);
            g.Daily.Missions[0] = "m_levels20";
            g.Energy = 1e12;
            Assert.IsTrue(g.BuyMachine(_c.MachineIndex("pump"), 25));
            Assert.AreEqual(0, g.Daily.Progress[0]);
            Assert.IsFalse(g.LoginRewardReady);
            Assert.AreEqual(0, g.DailyClaimable);
        }

        [Test]
        public void DailyStateSurvivesASave()
        {
            var g = NewGame(8);
            g.ClaimLoginReward();
            g.Daily.Missions[0] = "m_levels20";
            g.Energy = 1e12;
            Assert.IsTrue(g.BuyMachine(_c.MachineIndex("pump"), 7));

            string json = JsonUtility.ToJson(SaveMapper.Capture(g, null));
            var restored = new Game(_c);
            restored.SetTime(T0 - 5 * Day);
            SaveMapper.Apply(JsonUtility.FromJson<SaveData>(json), restored);
            restored.SetTime(T0);
            CollectionAssert.AreEqual(g.Daily.Missions, restored.Daily.Missions);
            Assert.AreEqual(7, restored.Daily.Progress[0], 1e-9);
            Assert.AreEqual(1, restored.Daily.Streak);
            Assert.IsFalse(restored.LoginRewardReady);

            // Saves from before daily rewards get missions picked for the player's level.
            var old = JsonUtility.FromJson<SaveData>(json);
            old.daily = null;
            var migrated = new Game(_c);
            migrated.SetTime(T0);
            SaveMapper.Apply(old, migrated);
            migrated.SetTime(T0);
            Assert.AreEqual(3, migrated.MissionsOffered);
            Assert.IsTrue(migrated.LoginRewardReady);
        }

        [Test]
        public void RemindersAvoidTheNightAndNeverDoubleUp()
        {
            var g = NewGame(12);
            g.Build.Upgrades[_c.UpgradeIndex("offline_generator")] = 2;
            g.MarkDirty();
            g.Refresh();
            var now = DateTimeOffset.FromUnixTimeSeconds(T0);
            var plan = Reminders.Plan(g, now);
            Assert.IsNotEmpty(plan);
            var ids = new HashSet<int>();
            foreach (var r in plan)
            {
                Assert.IsTrue(ids.Add(r.Id), "one reminder of each kind");
                Assert.Greater(r.Time, now, "only future reminders");
                int hour = r.Time.ToLocalTime().Hour;
                Assert.IsTrue(hour >= 9 && hour < 21, $"{r.Title} at {r.Time.ToLocalTime()} falls in quiet hours");
                Assert.IsFalse(string.IsNullOrEmpty(r.Text));
            }
            Assert.IsTrue(plan.Exists(r => r.Id == 2), "full Offline Generator");
            Assert.IsEmpty(Reminders.Plan(NewGame(1), now), "nothing to remind a brand-new player about");
        }
    }
}
