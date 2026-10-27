using System;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    public sealed class MachinesPanel : PanelView
    {
        static readonly int[] Modes = { 1, 10, 25, -1 };
        static readonly string[] ModeNames = { "x1", "x10", "x25", L.N("MAX") };

        readonly Row[] _rows;
        readonly Button[] _modeButtons = new Button[Modes.Length];
        readonly Label _lockNote;
        readonly UpgradeCard[] _upgrades;
        float _valueTimer;
        int _bestValue = -1;
        int _focus = -1;
        float _focusTime;

        sealed class UpgradeCard
        {
            public VisualElement Card;
            public Label Level, Effect, BuyTop, BuyCost, BuyCores;
            public Button Buy;
        }

        sealed class Row
        {
            public VisualElement Card;
            public Label Level, Effect, Milestone, BuyTop, BuyCost, Tag;
            public Button Buy;
            public Bar MilestoneBar;
        }

        public MachinesPanel(StormApp app) : base(app, "machines", L.T("Machines"), "machines", L.T("Machines"))
        {
            var seg = Ui.Div("se-segment");
            for (int i = 0; i < Modes.Length; i++)
            {
                int mode = i;
                _modeButtons[i] = Ui.Btn(L.T(ModeNames[i]), () => SetMode(mode));
                seg.Add(_modeButtons[i]);
            }
            Header.Add(seg);

            _lockNote = Ui.Text(L.T("Build is locked while a storm is running."), "se-small");
            _lockNote.style.marginBottom = 6;
            Body.Add(_lockNote);

            _rows = new Row[C.Machines.Length];
            for (int i = 0; i < C.Machines.Length; i++)
            {
                _rows[i] = BuildRow(i);
                Body.Add(_rows[i].Card);
            }

            Body.Add(Section(L.T("Permanent upgrades")));
            _upgrades = new UpgradeCard[C.Upgrades.Length];
            for (int i = 0; i < C.Upgrades.Length; i++)
            {
                _upgrades[i] = BuildUpgrade(i);
                Body.Add(_upgrades[i].Card);
            }
            SetMode(Mathf.Clamp(App.Save.settings.buyMode, 0, Modes.Length - 1));
        }

        /// <summary>Scrolls to a machine card and makes it pulse (tapping a generator in the world).</summary>
        public void Focus(int machine)
        {
            if (machine < 0 || machine >= _rows.Length) return;
            _focus = machine;
            _focusTime = 1.6f;
            var card = _rows[machine].Card;
            schedule.Execute(() => Scroll.ScrollTo(card)).StartingIn(30);
        }

        UpgradeCard BuildUpgrade(int i)
        {
            var def = C.Upgrades[i];
            var u = new UpgradeCard { Card = Ui.Div("se-card", "se-row", "se-upgradecard") };
            var box = Ui.Div("se-iconbox");
            box.style.backgroundColor = Palette.Core.Lighten(0.7f);
            box.Add(new Icon(def.Icon, 38));
            u.Card.Add(box);
            var info = Ui.Div("se-grow");
            var titleRow = Ui.Div("se-row");
            titleRow.Add(Ui.Text(def.Name, "se-h3"));
            u.Level = Ui.Text("", "se-machine-level");
            titleRow.Add(u.Level);
            info.Add(titleRow);
            u.Effect = Ui.Text("", "se-small");
            info.Add(u.Effect);
            u.Card.Add(info);
            u.Buy = Ui.Btn("", () =>
            {
                if (App.BuyUpgrade(i)) Refresh();
            }, "blue", "se-buybtn");
            u.Buy.name = "upgrade-" + def.Id;
            u.BuyTop = Ui.Text(L.T("BUILD"), "se-buybtn-top");
            var cost = Ui.Div("se-buybtn-cost");
            cost.Add(new Icon("energy", 16));
            u.BuyCost = new Label("0");
            cost.Add(u.BuyCost);
            var cores = Ui.Div("se-buybtn-cost");
            cores.Add(new Icon("core", 16));
            u.BuyCores = new Label("0");
            cores.Add(u.BuyCores);
            u.Buy.Add(u.BuyTop);
            u.Buy.Add(cost);
            u.Buy.Add(cores);
            u.Card.Add(u.Buy);
            return u;
        }

        void RefreshUpgrades()
        {
            for (int i = 0; i < _upgrades.Length; i++)
            {
                var def = C.Upgrades[i];
                var u = _upgrades[i];
                int level = G.Build.Upgrades[i];
                bool available = G.IsUpgradeAvailable(i);
                u.Card.EnableInClassList("dim", !available);
                if (!available)
                {
                    u.Level.Set(L.T("LOCKED"));
                    u.Effect.Set(def.Desc + " " + L.F("Unlocks at Engine Level {0}.", G.FeatureLevel(def.Feature)));
                    u.Buy.Show(false);
                    continue;
                }
                u.Level.Set(level == 0 ? L.T("Not built") : L.F("Lv {0}/{1}", level, def.MaxLevel));
                u.Effect.Set(UpgradeEffect(def, level));
                u.Buy.Show(true);
                if (G.IsUpgradeMaxed(i))
                {
                    u.BuyTop.Set(L.T("MAXED"));
                    u.BuyCost.Set("-");
                    u.BuyCores.Set("-");
                    u.Buy.SetEnabledState(false);
                    continue;
                }
                u.BuyTop.Set(level == 0 ? L.T("BUILD") : L.T("UPGRADE"));
                u.BuyCost.Set(Num.Format(G.UpgradeCost(i)));
                u.BuyCores.Set(Num.Int(G.UpgradeCoreCost(i)));
                u.Buy.SetEnabledState(G.CanBuyUpgrade(i));
            }
        }

        string UpgradeEffect(UpgradeDef def, int level)
        {
            if (def.Id != "offline_generator") return def.Desc;
            var s = G.Snap;
            double hours = s.Stat[(int)StatId.OfflineCapHours];
            double eff = s.Stat[(int)StatId.OfflineEfficiency];
            if (level == 0) return L.T("Keeps your engine producing while you are away. Built once, kept forever (survives resets).");
            return L.F("Offline: up to {0}h at {1} efficiency. Next level: more hours and efficiency.", Num.Fixed(hours, 0), Num.Percent(eff));
        }

        int BuyCount(int machine)
        {
            int mode = Modes[App.Save.settings.buyMode];
            return mode > 0 ? mode : Math.Max(1, G.MaxAffordable(machine));
        }

        void SetMode(int mode)
        {
            App.Save.settings.buyMode = mode;
            for (int i = 0; i < _modeButtons.Length; i++) _modeButtons[i].EnableInClassList("selected", i == mode);
            Refresh();
        }

        Row BuildRow(int i)
        {
            var def = C.Machines[i];
            var row = new Row();
            row.Card = Ui.Div("se-card", "se-row");

            var box = Ui.Div("se-iconbox");
            var tint = def.Cat >= 0 ? Palette.Category[def.Cat] : (def.Id == "chamber" ? Palette.Hex(0xB99CFF) : Palette.Orange);
            box.style.backgroundColor = tint.Lighten(0.7f);
            box.Add(new Icon(def.Icon, 38));
            row.Card.Add(box);

            var info = Ui.Div("se-grow");
            var titleRow = Ui.Div("se-row");
            titleRow.Add(Ui.Text(def.Name, "se-h3"));
            row.Level = Ui.Text(L.T("Lv 0"), "se-machine-level");
            titleRow.Add(row.Level);
            info.Add(titleRow);
            row.Effect = Ui.Text("", "se-small");
            info.Add(row.Effect);
            var ms = Ui.Div("se-row");
            ms.style.marginTop = 3;
            row.MilestoneBar = new Bar(Palette.Orange, 8);
            row.MilestoneBar.style.maxWidth = 70;
            row.MilestoneBar.style.marginRight = 6;
            ms.Add(row.MilestoneBar);
            row.Milestone = Ui.Text("", "se-milestone");
            ms.Add(row.Milestone);
            info.Add(ms);
            row.Card.Add(info);

            row.Buy = Ui.Btn("", () => OnBuy(i), "green", "se-buybtn");
            row.Buy.name = "buy-" + C.Machines[i].Id;
            row.BuyTop = Ui.Text(L.T("BUY"), "se-buybtn-top");
            var cost = Ui.Div("se-buybtn-cost");
            cost.Add(new Icon("energy", 16));
            row.BuyCost = new Label("0");
            cost.Add(row.BuyCost);
            row.Buy.Add(row.BuyTop);
            row.Buy.Add(cost);
            row.Card.Add(row.Buy);

            row.Tag = Ui.Text(L.T("BEST VALUE"), "se-tag", "se-card-ribbon");
            row.Card.Add(row.Tag);
            return row;
        }

        void OnBuy(int i)
        {
            int count = BuyCount(i);
            if (App.BuyMachine(i, count)) Refresh();
        }

        public override void Refresh()
        {
            _valueTimer -= 0.2f;
            if (_valueTimer <= 0)
            {
                _valueTimer = 1f;
                _bestValue = -1;
                double best = 0;
                for (int i = 0; i < C.Machines.Length; i++)
                {
                    double v = G.MachineValue(i);
                    if (v > best)
                    {
                        best = v;
                        _bestValue = i;
                    }
                }
            }

            _lockNote.Show(G.IsStorming);
            if (_focusTime > 0)
            {
                _focusTime -= 0.2f;
                for (int i = 0; i < _rows.Length; i++) _rows[i].Card.EnableInClassList("focus", i == _focus && _focusTime > 0);
            }
            RefreshUpgrades();
            bool shownLocked = false;
            var snap = G.Snap;
            for (int i = 0; i < _rows.Length; i++)
            {
                var def = C.Machines[i];
                var row = _rows[i];
                bool unlocked = G.MachineUnlocked[i];
                if (!unlocked)
                {
                    row.Card.Show(!shownLocked);
                    if (shownLocked) continue;
                    shownLocked = true;
                    row.Card.AddToClassList("dim");
                    row.Level.Set(L.T("LOCKED"));
                    row.Effect.Set(L.F("Unlocks at Engine Level {0}. Earn XP by building and unleashing storms.", def.UnlockLevel));
                    row.Milestone.Set("");
                    row.MilestoneBar.Show(false);
                    row.Tag.Show(false);
                    row.Buy.Show(false);
                    continue;
                }

                row.Card.Show(true);
                row.Card.RemoveFromClassList("dim");
                row.Buy.Show(true);
                int level = G.Build.Machines[i];
                row.Level.Set(L.F("Lv {0}", level));
                row.Tag.Show(i == _bestValue && !G.IsMaxed(i));
                row.Card.EnableInClassList("glow", i == _bestValue);
                row.Effect.Set(EffectText(i, snap));

                int next = def.MilestoneMult > 0 ? C.NextMilestone(level) : -1;
                if (next > 0)
                {
                    int prev = 0;
                    foreach (int m in C.MilestoneLevels)
                        if (m <= level) prev = m;
                    row.MilestoneBar.Show(true);
                    row.MilestoneBar.Value = (float)(level - prev) / (next - prev);
                    row.Milestone.Set(L.F("Lv {0}", next) + ": " + C.Stats[(int)def.MilestoneStat].Name + " x" + def.MilestoneMult.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    row.MilestoneBar.Show(false);
                    row.Milestone.Set(def.MaxLevel > 0 ? L.F("Max level {0}", def.MaxLevel) : "");
                }

                if (G.IsMaxed(i))
                {
                    row.BuyTop.Set(L.T("MAXED"));
                    row.BuyCost.Set("-");
                    row.Buy.SetEnabledState(false);
                    continue;
                }
                int count = BuyCount(i);
                if (def.MaxLevel > 0) count = Math.Min(count, def.MaxLevel - level);
                double cost = G.CostOf(i, count);
                row.BuyTop.Set(count > 1 ? L.F("BUY x{0}", count) : L.T("BUY"));
                row.BuyCost.Set(Num.Format(cost));
                row.Buy.SetEnabledState(!G.IsStorming && cost <= G.Energy);
            }
        }

        string EffectText(int i, Snapshot snap)
        {
            var def = C.Machines[i];
            switch (def.Id)
            {
                case "pump":
                    return L.T("+1 Base Pressure per level") + "  ·  " + L.F("Base now {0}", Num.Format(snap.Stat[(int)StatId.BasePressure]));
                case "chamber":
                    return L.T("+0.5s storms, +4% charge speed per level") + "  ·  " + L.F("Storm {0}s, charge {1}", Num.Fixed(snap.Stat[(int)StatId.StormDuration], 1), Num.Seconds(G.ChargeSeconds));
                default:
                    var e = def.Effects[0];
                    return L.F("+{0}% {1} per level", (e.PerLevel * 100).ToString("0", System.Globalization.CultureInfo.InvariantCulture), C.Stats[(int)e.Stat].Name) + "  ·  " + L.F("now {0}", Num.Mult(snap.Stat[(int)e.Stat]));
            }
        }

        public override bool HasAttention()
        {
            if (G.IsStorming) return false;
            for (int i = 0; i < C.Machines.Length; i++)
                if (G.MachineUnlocked[i] && !G.IsMaxed(i) && G.CostOf(i, 1) <= G.Energy) return true;
            return false;
        }
    }
}
