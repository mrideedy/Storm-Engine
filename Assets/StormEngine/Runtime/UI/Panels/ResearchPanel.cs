using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    public sealed class ResearchPanel : PanelView
    {
        readonly Label _gain, _runEnergy, _next, _owned, _balance;
        readonly Button _reset;
        readonly Row[] _rows;

        sealed class Row
        {
            public VisualElement Card;
            public Label Level;
            public Button Buy;
        }

        public ResearchPanel(StormApp app) : base(app, "research", L.T("Research"), "research", TabName())
        {
            _balance = HeaderBalance("research");
            var card = Ui.Div("se-card", "glow");
            var head = Ui.Div("se-row");
            head.Add(new Icon("refresh", 34, Palette.Research));
            var title = Ui.Div("se-grow");
            title.style.marginLeft = 8;
            title.Add(Ui.Text(L.T("Reset the Atmosphere"), "se-h2"));
            title.Add(Ui.Text(L.T("Machines and Energy reset. You keep Research, modules, Storm Cores, discoveries and records."), "se-small"));
            head.Add(title);
            card.Add(head);

            var stats = Ui.Div("se-row");
            stats.style.marginTop = 8;
            var gainBox = Ui.Div("se-grow");
            gainBox.Add(Ui.Text(L.T("RESEARCH ON RESET"), "se-small"));
            _gain = Ui.Text("+0", "se-h1");
            _gain.style.color = Palette.Research;
            gainBox.Add(_gain);
            stats.Add(gainBox);
            var runBox = Ui.Div("se-grow");
            runBox.Add(Ui.Text(L.T("ENERGY THIS RUN"), "se-small"));
            _runEnergy = Ui.Text("0", "se-h2");
            runBox.Add(_runEnergy);
            stats.Add(runBox);
            card.Add(stats);
            _next = Ui.Text("", "se-small");
            card.Add(_next);
            _reset = Ui.IconBtn("refresh", L.T("Reset Atmosphere"), () => App.RequestPrestige(), "purple");
            _reset.style.marginTop = 8;
            card.Add(_reset);
            Body.Add(card);

            _owned = Ui.Text("", "se-small");
            Body.Add(Section(L.T("Permanent research")));
            Body.Add(_owned);

            _rows = new Row[C.Research.Length];
            for (int i = 0; i < C.Research.Length; i++)
            {
                _rows[i] = BuildRow(i);
                Body.Add(_rows[i].Card);
            }
        }

        /// <summary>
        /// Some languages need a shorter word on the narrow tab ("Investigación" becomes "Ciencia"):
        /// the "tab:Research" translation, falling back to the normal one.
        /// </summary>
        static string TabName()
        {
            L.N("tab:Research");
            return L.T("Research", "tab");
        }

        Row BuildRow(int i)
        {
            var def = C.Research[i];
            var row = new Row { Card = Ui.Div("se-card", "se-row") };
            var box = Ui.Div("se-iconbox");
            box.style.backgroundColor = Palette.Research.Lighten(0.8f);
            box.Add(new Icon(def.Icon, 34));
            row.Card.Add(box);
            var info = Ui.Div("se-grow");
            var titleRow = Ui.Div("se-row");
            titleRow.Add(Ui.Text(def.Name, "se-h3"));
            row.Level = Ui.Text("", "se-machine-level");
            titleRow.Add(row.Level);
            info.Add(titleRow);
            info.Add(Ui.Text(def.Desc, "se-small"));
            row.Card.Add(info);
            row.Buy = Ui.IconBtn("research", "0", () => App.BuyResearch(i), "purple", "small");
            row.Buy.style.minWidth = 84;
            row.Card.Add(row.Buy);
            return row;
        }

        public override void Refresh()
        {
            long gain = G.ResearchOnReset();
            _balance.Set(Num.Int(G.Research));
            _gain.Set("+" + Num.Int(gain));
            _runEnergy.Set(Num.Format(G.RunEnergy));
            double next = G.EnergyForResearch(gain + 1);
            _next.Set(gain < 1
                ? L.F("First Research point at {0} Energy this run.", Num.Format(next))
                : L.F("Next point at {0} Energy. Research grows with every order of magnitude you reach.", Num.Format(next)));
            _reset.SetEnabledState(gain >= 1 && !G.IsStorming);
            _owned.Set(L.F("You have {0} Research ({1} earned, {2} resets).", Num.Int(G.Research), Num.Int(G.ResearchLifetime), G.Prestiges));

            for (int i = 0; i < _rows.Length; i++)
            {
                var def = C.Research[i];
                var row = _rows[i];
                int level = G.Build.Research[i];
                bool visible = G.IsResearchVisible(i);
                row.Card.Show(visible);
                if (!visible) continue;
                bool maxed = level >= def.MaxLevel;
                row.Level.Set(def.MaxLevel == 1 ? (level > 0 ? L.T("OWNED") : "") : L.F("Lv {0}/{1}", level, def.MaxLevel));
                row.Level.Show(def.MaxLevel > 1 || level > 0);
                row.Card.EnableInClassList("active", maxed);
                var label = row.Buy.Q<Label>(className: "se-iconbtn-label");
                if (maxed)
                {
                    label?.Set(L.T("MAX"));
                    row.Buy.SetEnabledState(false);
                }
                else
                {
                    label?.Set(Num.Int(G.ResearchCost(i)));
                    row.Buy.SetEnabledState(G.CanBuyResearch(i));
                }
            }
        }

        public override bool HasAttention()
        {
            for (int i = 0; i < C.Research.Length; i++)
                if (G.CanBuyResearch(i)) return true;
            return G.Prestiges == 0 && G.ResearchOnReset() >= 5;
        }
    }
}
