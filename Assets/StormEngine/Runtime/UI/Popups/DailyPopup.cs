using System;
using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Formats daily rewards ("+15 Gems", "30m x2 Energy Surge").</summary>
    public static class DailyText
    {
        public struct Part
        {
            public string Icon;
            public string Amount;
            public string Name;
        }

        public static List<Part> Parts(Game g, DailyReward r)
        {
            var list = new List<Part>();
            if (r == null) return list;
            if (r.Gems > 0) list.Add(new Part { Icon = "gem", Amount = "+" + Num.Int(r.Gems), Name = L.T("Gems") });
            long cores = g.DailyCores(r.Cores);
            if (cores > 0) list.Add(new Part { Icon = "core", Amount = "+" + Num.Int(cores), Name = L.T("Storm Cores") });
            if (r.SurgeSeconds > 0) list.Add(new Part { Icon = "energy", Amount = Duration(r.SurgeSeconds), Name = L.T("x2 Energy Surge") });
            return list;
        }

        /// <summary>"30 min", "1 h", "1.5 h".</summary>
        public static string Duration(double seconds) =>
            seconds < 3600
                ? L.F("{0} min", Math.Max(1, Math.Round(seconds / 60)))
                : L.F("{0} h", (seconds / 3600).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));

        public static string Describe(Game g, DailyReward r)
        {
            var parts = new List<string>();
            foreach (var p in Parts(g, r)) parts.Add(p.Amount + " " + p.Name);
            return string.Join(", ", parts);
        }

        /// <summary>A small icon + amount chip for rows and cells.</summary>
        public static VisualElement Chip(Part p)
        {
            var chip = Ui.Div("se-rewardchip");
            chip.pickingMode = PickingMode.Ignore;
            chip.Add(new Icon(p.Icon, 18));
            chip.Add(Ui.Text(p.Amount, "se-rewardchip-text"));
            return chip;
        }
    }

    /// <summary>Round gift button on the HUD. The dot means something is waiting to be collected.</summary>
    public sealed class DailyButton : Button
    {
        readonly StormApp _app;
        readonly VisualElement _dot;

        public DailyButton(StormApp app, Action onClick) : base(onClick)
        {
            _app = app;
            focusable = false;
            AddToClassList("se-roundbtn");
            AddToClassList("se-dailybtn");
            Add(new Icon("gift", 26));
            _dot = Ui.Div("se-badge", "se-dailydot");
            _dot.pickingMode = PickingMode.Ignore;
            Add(_dot);
        }

        public void Refresh()
        {
            var g = _app.Game;
            this.Show(g.DailyUnlocked);
            bool waiting = g.DailyClaimable > 0;
            _dot.Show(waiting);
            EnableInClassList("waiting", waiting);
        }
    }

    /// <summary>
    /// Daily Rewards: a gift for every day in a row (seven-day cycle, a lost streak can be won back with
    /// an ad), three missions that refresh at midnight UTC, and a bonus for finishing all of them.
    /// </summary>
    public sealed class DailyPopup : Popup
    {
        sealed class DayCell
        {
            public VisualElement Root;
            public Label Day;
            public Icon Icon;
            public Label Amount;
            public Label Extra;
            public Icon Check;
        }

        sealed class MissionRow
        {
            public int Slot;
            public VisualElement Root;
            public Label Count;
            public Bar Bar;
            public Button Claim;
            public Icon Done;
        }

        readonly StormApp _app;
        readonly Label _streak, _restoreText, _loginNote, _bonusText, _timer;
        readonly VisualElement _restore, _missions;
        readonly Button _claimLogin, _restoreButton, _claimBonus;
        readonly Icon _bonusDone;
        readonly List<DayCell> _cells = new List<DayCell>();
        readonly List<MissionRow> _rows = new List<MissionRow>();
        string _shape;
        float _refresh;

        Game G => _app.Game;

        public DailyPopup(StormApp app)
            : base(L.T("DAILY REWARDS"), L.T("A gift every day you visit, plus three fresh missions"), "gift")
        {
            _app = app;
            AddToClassList("se-dailypopup");
            HeadEl.style.backgroundColor = Palette.Hex(0x6A2C7A);

            var streak = Ui.Div("se-row", "se-daily-streak");
            streak.Add(new Icon("flame", 30));
            _streak = Ui.Text("", "se-h3", "se-grow");
            _streak.style.marginLeft = 6;
            streak.Add(_streak);
            BodyEl.Add(streak);

            _restore = Ui.Div("se-row", "se-daily-restore");
            _restoreText = Ui.Text("", "se-small", "se-grow");
            _restore.Add(_restoreText);
            _restoreButton = Ui.IconBtn("play", L.T("Keep it"), () => _app.RestoreStreakWithAd(), "green", "small");
            _restore.Add(_restoreButton);
            BodyEl.Add(_restore);

            var cells = Ui.Div("se-row", "se-daycells");
            for (int i = 0; i < 7; i++)
            {
                var cell = new DayCell { Root = Ui.Div("se-daycell") };
                cell.Day = Ui.Text("", "se-daycell-day");
                cell.Icon = new Icon("gem", 30);
                cell.Amount = Ui.Text("", "se-daycell-amount");
                cell.Extra = Ui.Text("", "se-daycell-extra");
                cell.Check = new Icon("check", 22);
                cell.Check.AddToClassList("se-daycell-check");
                cell.Root.Children(cell.Day, cell.Icon, cell.Amount, cell.Extra, cell.Check);
                if (i == 6) cell.Root.AddToClassList("big");
                cells.Add(cell.Root);
                _cells.Add(cell);
            }
            BodyEl.Add(cells);

            _claimLogin = Ui.Btn("", null, "green", "se-daily-claim");
            _claimLogin.clicked += () =>
            {
                _app.ClaimLoginReward(_claimLogin);
                Refresh(false);
            };
            BodyEl.Add(_claimLogin);
            _loginNote = Ui.Text("", "se-small", "se-center");
            BodyEl.Add(_loginNote);

            BodyEl.Add(Ui.Text(L.T("TODAY'S MISSIONS"), "se-section"));
            _missions = Ui.Div();
            BodyEl.Add(_missions);

            var bonus = Ui.Div("se-row", "se-daily-bonus");
            bonus.Add(new Icon("gift", 34));
            _bonusText = Ui.Text("", "se-small", "se-grow");
            _bonusText.style.marginLeft = 8;
            bonus.Add(_bonusText);
            _claimBonus = Ui.Btn(L.T("Claim"), null, "green", "small");
            _claimBonus.clicked += () =>
            {
                _app.ClaimMissionBonus(_claimBonus);
                Refresh(false);
            };
            bonus.Add(_claimBonus);
            _bonusDone = new Icon("check", 26);
            bonus.Add(_bonusDone);
            BodyEl.Add(bonus);

            _timer = Ui.Text("", "se-small", "se-center");
            _timer.style.marginTop = 6;
            BodyEl.Add(_timer);

            Action(L.T("Close"), null, "white");
            Refresh(true);
        }

        void BuildMissions()
        {
            _missions.Clear();
            _rows.Clear();
            for (int slot = 0; slot < DailyState.MissionCount; slot++)
            {
                var def = G.MissionAt(slot);
                if (def == null) continue;
                var row = new MissionRow { Slot = slot, Root = Ui.Div("se-row", "se-missionrow") };
                var box = Ui.Div("se-iconbox", "small");
                box.Add(new Icon(def.Icon, 26));
                row.Root.Add(box);
                var info = Ui.Div("se-grow");
                info.Add(Ui.Text(def.Text, "se-mission-text"));
                var progress = Ui.Div("se-row");
                row.Bar = new Bar(Palette.Hex(0xB65BD0), 10);
                row.Bar.AddToClassList("se-grow");
                progress.Add(row.Bar);
                row.Count = Ui.Text("", "se-mission-count");
                progress.Add(row.Count);
                info.Add(progress);
                row.Root.Add(info);
                var rewards = Ui.Div("se-mission-rewards");
                foreach (var p in DailyText.Parts(G, def.Reward)) rewards.Add(DailyText.Chip(p));
                row.Root.Add(rewards);
                int s = slot;
                row.Claim = Ui.Btn(L.T("Claim"), null, "green", "small");
                row.Claim.clicked += () =>
                {
                    _app.ClaimMission(s, row.Claim);
                    Refresh(false);
                };
                row.Root.Add(row.Claim);
                row.Done = new Icon("check", 26);
                row.Done.AddToClassList("se-mission-done");
                row.Root.Add(row.Done);
                _missions.Add(row.Root);
                _rows.Add(row);
            }
            if (_rows.Count == 0)
                _missions.Add(Ui.Text(L.T("New missions arrive at midnight (UTC)."), "se-small", "se-center"));
        }

        void Refresh(bool force)
        {
            var g = G;
            var d = g.Daily;
            string shape = g.Today + "|" + string.Join(",", d.Missions);
            if (force || shape != _shape)
            {
                _shape = shape;
                BuildMissions();
            }

            bool ready = g.LoginRewardReady;
            int today = g.TodayStreakDay;
            _streak.Set(d.Streak > 0
                ? L.F("{0}-day streak", d.Streak) + (d.BestStreak > d.Streak ? "  ·  " + L.F("best {0}", d.BestStreak) : "")
                : ready ? L.T("Collect today's gift to start a streak!") : L.T("Streak starts today!"));

            bool restore = g.CanRestoreStreak;
            _restore.Show(restore);
            if (restore)
            {
                _restoreText.Set(L.F("You missed a day and lost your {0}-day streak. Watch a short ad to keep it going!", d.LostStreak));
                _restoreButton.SetEnabledState(_app.CanOfferAd("ad_streak"));
            }

            int start = (today - 1) / 7 * 7 + 1;
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                int day = start + i;
                var parts = DailyText.Parts(g, g.LoginRewardFor(day));
                cell.Day.Set(L.F("DAY {0}", day));
                if (parts.Count > 0)
                {
                    cell.Icon.Kind = parts[0].Icon;
                    cell.Amount.Set(parts[0].Amount);
                }
                cell.Extra.Set(parts.Count > 1 ? (parts[1].Icon == "core" ? L.F("{0} cores", parts[1].Amount) : parts[1].Amount) : "");
                cell.Extra.Show(parts.Count > 1);
                bool collected = day < today || day == today && !ready;
                bool current = day == today && ready;
                cell.Root.EnableInClassList("collected", collected);
                cell.Root.EnableInClassList("current", current);
                cell.Check.Show(collected);
            }

            _claimLogin.Show(ready);
            if (ready) _claimLogin.SetButton(L.F("Collect Day {0} gift", today));
            _loginNote.Show(!ready);
            if (!ready) _loginNote.Set(L.F("Come back tomorrow for Day {0}. New day in {1}.", today + 1, Num.Time(g.SecondsToNewDay)));

            foreach (var row in _rows)
            {
                var def = g.MissionAt(row.Slot);
                if (def == null) continue;
                double have = Math.Min(d.Progress[row.Slot], def.Amount);
                row.Bar.Value = (float)(have / Math.Max(1e-9, def.Amount));
                row.Count.Set(def.Kind == MissionKind.Chain
                    ? Num.Mult(Math.Max(1, have)) + " / " + Num.Mult(def.Amount)
                    : Num.Int((long)Math.Floor(have)) + " / " + Num.Int((long)def.Amount));
                bool claimed = d.Claimed[row.Slot];
                row.Claim.Show(!claimed && g.IsMissionDone(row.Slot));
                row.Done.Show(claimed);
                row.Root.EnableInClassList("claimed", claimed);
            }

            int offered = g.MissionsOffered;
            _claimBonus.Show(g.CanClaimMissionBonus);
            _bonusDone.Show(d.BonusClaimed);
            _bonusText.Set(d.BonusClaimed
                ? L.T("All missions done. See you tomorrow!")
                : L.F("Finish all missions ({0}/{1} collected): {2}", g.MissionsClaimed, offered, DailyText.Describe(g, g.C.MissionBonus)));
            _timer.Set(L.F("New missions in {0} (midnight UTC)", Num.Time(g.SecondsToNewDay)));
        }

        public override void Tick(float dt)
        {
            _refresh -= dt;
            if (_refresh > 0) return;
            _refresh = 0.25f;
            Refresh(false);
        }
    }
}
