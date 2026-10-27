using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    public sealed class ModulesPanel : PanelView
    {
        const int MaxSlots = 5;
        readonly VisualElement[] _slots = new VisualElement[MaxSlots];
        readonly Icon[] _slotIcons = new Icon[MaxSlots];
        readonly Label[] _slotNames = new Label[MaxSlots];
        readonly Label _slotInfo;
        readonly Row[] _rows;

        sealed class Row
        {
            public VisualElement Card;
            public Label Name, Level, Desc, Req;
            public Button Unlock, Upgrade, Equip;
            public Icon Icon;
        }

        public ModulesPanel(StormApp app) : base(app, "modules", L.T("Modules"), "modules", L.T("Modules"))
        {
            Body.Add(Section(L.T("Equipped")));
            var slotRow = Ui.Div("se-row");
            for (int i = 0; i < MaxSlots; i++)
            {
                int slot = i;
                var s = Ui.Div("se-slot");
                _slotIcons[i] = new Icon("lock", 34);
                _slotNames[i] = Ui.Text("", "se-slot-name");
                s.Add(_slotIcons[i]);
                s.Add(_slotNames[i]);
                s.RegisterCallback<ClickEvent>(_ => OnSlot(slot));
                _slots[i] = s;
                slotRow.Add(s);
            }
            Body.Add(slotRow);
            _slotInfo = Ui.Text("", "se-small");
            _slotInfo.style.marginTop = 6;
            _slotInfo.style.marginBottom = 4;
            Body.Add(_slotInfo);

            Body.Add(Section(L.T("Workshop")));
            _rows = new Row[C.Modules.Length];
            for (int i = 0; i < C.Modules.Length; i++)
            {
                _rows[i] = BuildRow(i);
                Body.Add(_rows[i].Card);
            }
        }

        Row BuildRow(int i)
        {
            var def = C.Modules[i];
            var row = new Row { Card = Ui.Div("se-card") };
            var top = Ui.Div("se-row");
            var box = Ui.Div("se-iconbox");
            row.Icon = new Icon(def.Icon, 36);
            box.Add(row.Icon);
            top.Add(box);
            var info = Ui.Div("se-grow");
            var titleRow = Ui.Div("se-row");
            row.Name = Ui.Text(def.Name, "se-h3");
            titleRow.Add(row.Name);
            row.Level = Ui.Text("", "se-machine-level");
            titleRow.Add(row.Level);
            info.Add(titleRow);
            row.Desc = Ui.Text(def.Desc, "se-small");
            info.Add(row.Desc);
            row.Req = Ui.Text("", "se-small", "se-bad");
            info.Add(row.Req);
            top.Add(info);
            row.Card.Add(top);

            var actions = Ui.Div("se-row");
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginTop = 6;
            row.Unlock = Ui.IconBtn("core", L.T("Unlock"), () => App.UnlockModule(i), "blue", "small");
            row.Upgrade = Ui.IconBtn("core", L.T("Upgrade"), () => App.UpgradeModule(i), "green", "small");
            row.Equip = Ui.Btn(L.T("Equip"), () => ToggleEquip(i), "white", "small");
            row.Equip.style.marginLeft = 6;
            row.Upgrade.style.marginLeft = 6;
            actions.Children(row.Unlock, row.Equip, row.Upgrade);
            row.Card.Add(actions);
            return row;
        }

        void ToggleEquip(int i)
        {
            if (G.Build.IsEquipped(i)) App.Unequip(i);
            else App.Equip(i);
        }

        void OnSlot(int slot)
        {
            if (slot < G.Build.Equipped.Count) App.Unequip(G.Build.Equipped[slot]);
        }

        public override void Refresh()
        {
            int slots = G.ModuleSlots;
            for (int i = 0; i < MaxSlots; i++)
            {
                var s = _slots[i];
                bool open = i < slots;
                bool filled = i < G.Build.Equipped.Count;
                s.EnableInClassList("locked", !open);
                s.EnableInClassList("filled", filled);
                if (filled)
                {
                    var def = C.Modules[G.Build.Equipped[i]];
                    _slotIcons[i].Kind = def.Icon;
                    _slotIcons[i].Tint = null;
                    _slotNames[i].Set((def.Short ?? def.Name) + "\n" + L.F("Lv {0}", G.Build.ModuleLevels[G.Build.Equipped[i]]));
                }
                else
                {
                    _slotIcons[i].Kind = open ? "chip" : "lock";
                    _slotIcons[i].Tint = open ? Palette.Hex(0xC9D3E2) : (Color?)null;
                    _slotNames[i].Set(open ? L.T("Empty") : L.T("Locked"));
                }
            }
            _slotInfo.Set(G.IsStorming
                ? L.T("Modules are locked while a storm is running.")
                : L.F("{0}/{1} slots used. Tap an equipped module to remove it. Storm Cores come from storms and discoveries.", G.Build.Equipped.Count, slots));

            for (int i = 0; i < _rows.Length; i++)
            {
                var def = C.Modules[i];
                var row = _rows[i];
                bool unlocked = G.IsModuleUnlocked(i);
                bool reqs = G.ModuleRequirementsMet(i);
                bool equipped = G.Build.IsEquipped(i);
                int level = G.Build.ModuleLevels[i];

                row.Card.EnableInClassList("dim", !unlocked && !reqs);
                row.Card.EnableInClassList("active", equipped);
                row.Level.Set(unlocked ? L.F("Lv {0}/{1}", level, def.MaxLevel) : L.T("LOCKED"));
                row.Icon.Kind = reqs || unlocked ? def.Icon : "lock";

                string req = null;
                if (!unlocked && !reqs)
                {
                    if (def.RequiresPrestiges > G.Prestiges) req = L.F("Requires atmosphere resets: {0}.", def.RequiresPrestiges);
                    else if (def.RequiresDiscovery != null)
                    {
                        int p = C.PhenoIndex(def.RequiresDiscovery);
                        req = G.Discovered[p] ? L.F("Discover {0} first.", C.Phenomena[p].Name) : L.F("Discover a new phenomenon ({0}) first.", Hint(p));
                    }
                }
                row.Req.Show(req != null);
                if (req != null) row.Req.Set(req);

                row.Unlock.Show(!unlocked);
                if (!unlocked)
                {
                    SetIconBtnText(row.Unlock, L.F("Unlock {0}", Num.Int(def.UnlockCost)));
                    row.Unlock.SetEnabledState(reqs && G.Cores >= def.UnlockCost);
                }

                row.Equip.Show(unlocked);
                if (unlocked)
                {
                    row.Equip.SetButton(equipped ? L.T("Unequip") : L.T("Equip"));
                    row.Equip.SetEnabledState(!G.IsStorming && (equipped || G.Build.Equipped.Count < G.ModuleSlots));
                }

                bool maxed = unlocked && level >= def.MaxLevel;
                row.Upgrade.Show(unlocked && !maxed);
                if (unlocked && !maxed)
                {
                    long cost = G.ModuleUpgradeCost(i);
                    SetIconBtnText(row.Upgrade, L.F("Upgrade {0}", Num.Int(cost)));
                    row.Upgrade.SetEnabledState(!G.IsStorming && G.Cores >= cost);
                }
            }
        }

        string Hint(int p)
        {
            var def = C.Phenomena[p];
            if (def.Special) return L.T("needs the Chaos Reactor");
            var parts = new System.Collections.Generic.List<string>();
            foreach (var ing in def.Ingredients) parts.Add(C.Stats[(int)Content.CatStat((int)ing.Cat)].Name + " x" + ing.Threshold);
            return string.Join(" + ", parts);
        }

        static void SetIconBtnText(Button b, string text)
        {
            var label = b.Q<Label>(className: "se-iconbtn-label");
            label?.Set(text);
        }

        public override bool HasAttention()
        {
            for (int i = 0; i < C.Modules.Length; i++)
                if (!G.IsModuleUnlocked(i) && G.ModuleRequirementsMet(i) && G.Cores >= C.Modules[i].UnlockCost) return true;
            return false;
        }
    }
}
