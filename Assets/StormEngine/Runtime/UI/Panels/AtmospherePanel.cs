using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Formula breakdown: players can always inspect why Storm Power is what it is.</summary>
    public sealed class AtmospherePanel : PanelView
    {
        static readonly StatId[] Terms =
        {
            StatId.BasePressure, StatId.Temperature, StatId.Humidity, StatId.Wind, StatId.Cloud, StatId.Electricity
        };
        static readonly string[] TermIcons = { "pump", "heat", "water", "wind", "cloud", "bolt" };

        readonly Label[] _termValues = new Label[8];
        readonly Label _total;
        readonly List<StatDetail> _details = new List<StatDetail>();
        readonly PhenoCard[] _phenos;
        readonly Label[] _stormValues = new Label[6];
        readonly List<BreakdownLine> _lines = new List<BreakdownLine>();
        readonly Label _forecastPlain, _forecastBoosted, _forecastHint;
        float _forecastTimer;

        public AtmospherePanel(StormApp app) : base(app, "atmosphere", L.T("Atmosphere"), "atmosphere", L.T("Atmosphere"))
        {
            var formula = Ui.Div("se-formula");
            formula.Add(Ui.Text(L.T("STORM POWER ="), "se-formula-title"));
            var terms = Ui.Div("se-row", "se-wrap");
            for (int i = 0; i < 8; i++)
            {
                var term = Ui.Div("se-term");
                if (i > 0) term.Add(Ui.Text("x", "se-term-op"));
                string icon = i < 6 ? TermIcons[i] : i == 6 ? "synergy" : "gear";
                term.Add(new Icon(icon, 18));
                _termValues[i] = new Label("1");
                term.Add(_termValues[i]);
                terms.Add(term);
            }
            formula.Add(terms);
            _total = Ui.Text("", "se-formula-total");
            formula.Add(_total);
            formula.Add(Ui.Text(L.T("Additive bonuses stack inside a category; categories multiply each other. The same +10% is worth more in your weakest category."), "se-formula-line"));
            Body.Add(formula);

            var forecast = Ui.Div("se-card", "glow");
            var fh = Ui.Div("se-row");
            fh.Add(new Icon("storm", 30));
            var ft = Ui.Text(L.T("STORM FORECAST"), "se-h3", "se-grow");
            ft.style.marginLeft = 8;
            fh.Add(ft);
            forecast.Add(fh);
            _forecastPlain = Ui.Text("", "se-body");
            _forecastBoosted = Ui.Text("", "se-body");
            _forecastHint = Ui.Text(L.T("A dry run of your next storm with the current build. Change modules or levels and watch it move."), "se-small");
            forecast.Children(_forecastPlain, _forecastBoosted, _forecastHint);
            Body.Add(forecast);

            Body.Add(Section(L.T("Why this number? (tap to expand)")));
            for (int i = 0; i < Terms.Length; i++) AddDetail(Terms[i], TermIcons[i]);
            AddDetail(StatId.GlobalPower, "gear");

            Body.Add(Section(L.T("Phenomena (synergies)")));
            Body.Add(Ui.Text(L.T("Phenomena switch on when every ingredient reaches its threshold. Each active one multiplies Storm Power, and the strongest decides your storm class."), "se-small"));
            _phenos = new PhenoCard[C.Phenomena.Length];
            for (int p = 0; p < C.Phenomena.Length; p++)
            {
                _phenos[p] = new PhenoCard(App, p);
                Body.Add(_phenos[p]);
            }

            Body.Add(Section(L.T("Storm engine")));
            var storm = Ui.Div("se-card");
            string[] names = { L.T("Storm intensity"), L.T("Storm duration"), L.T("Lightning"), L.T("Chain per strike"), L.T("Charge time"), L.T("Mega Storm") };
            for (int i = 0; i < names.Length; i++)
            {
                var kv = Ui.Div("se-kv");
                kv.Add(Ui.Text(names[i]));
                _stormValues[i] = Ui.Text("", "v");
                kv.Add(_stormValues[i]);
                storm.Add(kv);
            }
            storm.Add(Ui.Text(L.T("Storm Score = Storm Power x Intensity x Chain x Mega x Perfect Window, summed over every tick of the storm. Boosts that overlap the storm multiply everything inside it."), "se-small"));
            Body.Add(storm);
        }

        void AddDetail(StatId stat, string icon)
        {
            var d = new StatDetail(this, stat, icon);
            _details.Add(d);
            Body.Add(d);
        }

        public override void Refresh()
        {
            var v = G.View;
            for (int i = 0; i < 6; i++)
                _termValues[i].Set(i == 0 ? Num.Format(v.Stat[0]) : Num.Mult(v.Stat[(int)Terms[i]]));
            _termValues[6].Set(Num.Mult(v.Synergy));
            _termValues[7].Set(Num.Mult(v.Stat[(int)StatId.GlobalPower]));

            _forecastTimer -= 0.2f;
            if (_forecastTimer <= 0 && !G.IsStorming)
            {
                _forecastTimer = 1f;
                var plain = G.Forecast(false);
                var boosted = G.Forecast(true);
                _forecastPlain.Set(L.F("Unleash now: {0} storm, {1} score over {2}s.", StormSubmission.ClassName(C, plain.ClassIndex), Num.Format(plain.Score), Num.Fixed(plain.Seconds, 1)));
                bool anyBoost = boosted.Events.Count > 0;
                _forecastBoosted.Show(anyBoost);
                if (anyBoost)
                    _forecastBoosted.Set(L.F("Firing your ready boosts first ({0}): {1}, {2} ({3}).", boosted.Events.Count, StormSubmission.ClassName(C, boosted.ClassIndex), Num.Format(boosted.Score), Num.Mult(boosted.Score / System.Math.Max(1e-9, plain.Score))));
            }
            _total.Set("= " + L.F("{0} /sec", Num.Format(v.StormPower)));

            foreach (var d in _details) d.Refresh();
            foreach (var p in _phenos) p.Refresh();

            _stormValues[0].Set(Num.Mult(v.Stat[(int)StatId.StormIntensity]));
            _stormValues[1].Set(Num.Fixed(v.Stat[(int)StatId.StormDuration], 1) + "s");
            _stormValues[2].Set(L.F("{0} strikes/s", Num.Fixed(v.Stat[(int)StatId.LightningRate], 2)));
            _stormValues[3].Set(L.F("+{0} chain", Num.Percent(v.Stat[(int)StatId.ChainPerStrike])));
            _stormValues[4].Set(Num.Seconds(G.ChargeSeconds));
            _stormValues[5].Set(L.T("3 / 5 / 7 phenomena: x1.5 / x2 / x3"));
        }

        internal string SourceName(in BreakdownLine l)
        {
            switch (l.Source)
            {
                case SourceKind.Machine: return L.F("{0} Lv {1}", C.Machines[l.SourceIndex].Name, l.Level);
                case SourceKind.Milestone: return L.F("{0} milestones x{1}", C.Machines[l.SourceIndex].Name, l.Level);
                case SourceKind.Module: return L.F("{0} Lv {1}", C.Modules[l.SourceIndex].Name, l.Level);
                case SourceKind.Research: return L.F("{0} Lv {1}", C.Research[l.SourceIndex].Name, l.Level);
                case SourceKind.Weekly: return L.F("Weekly: {0}", C.Weekly[l.SourceIndex].Name);
                case SourceKind.Boost: return L.F("{0} (active)", C.Boosts[l.SourceIndex].Name);
                case SourceKind.Electricity: return L.T("Electricity level");
                case SourceKind.Upgrade: return L.F("{0} Lv {1}", C.Upgrades[l.SourceIndex].Name, l.Level);
                case SourceKind.Style: return L.F("Style: {0}", C.Cosmetics[l.SourceIndex].Name);
                case SourceKind.Decoration: return L.F("Island decorations ({0} kinds)", G.UniqueDecorations);
                default: return l.Source.ToString();
            }
        }

        internal List<BreakdownLine> Explain(StatId stat)
        {
            Formula.Explain(C, G.ViewMods, G.ViewContext, G.View, stat, _lines);
            return _lines;
        }

        /// <summary>Expandable row showing every modifier feeding one stat.</summary>
        sealed class StatDetail : VisualElement
        {
            readonly AtmospherePanel _panel;
            readonly StatId _stat;
            readonly Label _value, _math;
            readonly VisualElement _lines;
            bool _open;
            int _lineCount = -1;

            public StatDetail(AtmospherePanel panel, StatId stat, string icon)
            {
                _panel = panel;
                _stat = stat;
                AddToClassList("se-card");
                var head = Ui.Div("se-row");
                head.Add(new Icon(icon, 26));
                var name = Ui.Text(panel.C.Stats[(int)stat].Name, "se-h3", "se-grow");
                name.style.marginLeft = 8;
                head.Add(name);
                _value = Ui.Text("", "se-h2");
                head.Add(_value);
                Add(head);
                _math = Ui.Text("", "se-small");
                Add(_math);
                _lines = Ui.Div();
                _lines.style.marginTop = 4;
                Add(_lines);
                _lines.Show(false);
                RegisterCallback<ClickEvent>(_ =>
                {
                    _open = !_open;
                    _lines.Show(_open);
                    _lineCount = -1;
                    Refresh();
                });
            }

            public void Refresh()
            {
                var g = _panel.G;
                var calc = g.View.Calc[(int)_stat];
                _value.Set(_stat == StatId.BasePressure ? Num.Format(calc.Value) : Num.Mult(calc.Value));
                string eff = calc.AddEff != 1 ? " x " + L.F("{0} eff", Num.Fixed(calc.AddEff, 2)) : "";
                _math.Set(L.F("base {0}  x  (1 + {1} add{2})  x  {3} mult", Num.Format(calc.Base), Num.Percent(calc.Add), eff, Num.Format(calc.Mult)));
                if (!_open) return;

                var lines = _panel.Explain(_stat);
                if (lines.Count != _lineCount)
                {
                    _lineCount = lines.Count;
                    _lines.Clear();
                    if (lines.Count == 0) _lines.Add(Ui.Text(L.T("No modifiers yet. Build machines to grow this stat."), "se-small"));
                    foreach (var l in lines)
                    {
                        var row = Ui.Div("se-bdline");
                        var tag = Ui.Text(TagText(l), "se-tag", TagClass(l));
                        tag.style.minWidth = 46;
                        row.Add(tag);
                        row.Add(Ui.Text(_panel.SourceName(l), "se-body", "se-grow"));
                        row.Add(Ui.Text(ValueText(_panel, l), "se-h3"));
                        _lines.Add(row);
                    }
                }
                else
                {
                    int i = 0;
                    foreach (var child in _lines.Children())
                    {
                        if (i >= lines.Count) break;
                        var labels = child.Query<Label>().ToList();
                        if (labels.Count >= 3)
                        {
                            labels[1].Set(_panel.SourceName(lines[i]));
                            labels[2].Set(ValueText(_panel, lines[i]));
                        }
                        i++;
                    }
                }
            }

            static string TagText(in BreakdownLine l)
            {
                if (l.Conditional) return L.T("BOOST");
                switch (l.Op)
                {
                    case ModOp.Flat: return L.T("BASE");
                    case ModOp.Add: return L.T("ADD");
                    case ModOp.AddEff: return L.T("EFF");
                    case ModOp.Convert: return L.T("CONV");
                    default: return L.T("MULT");
                }
            }

            static string TagClass(in BreakdownLine l)
            {
                if (l.Conditional) return "cond";
                switch (l.Op)
                {
                    case ModOp.Flat: return "flat";
                    case ModOp.Add: return "add";
                    case ModOp.Convert: return "conv";
                    default: return "mult";
                }
            }

            static string ValueText(AtmospherePanel panel, in BreakdownLine l)
            {
                switch (l.Op)
                {
                    case ModOp.Flat: return "+" + Num.Format(l.Value);
                    case ModOp.Add: return "+" + Num.Percent(l.Value);
                    case ModOp.AddEff: return "x" + L.F("{0} eff", Num.Fixed(l.Value, 2));
                    case ModOp.Convert: return L.F("+{0} from {1}", Num.Percent(l.Value), panel.C.Stats[(int)l.From].Name);
                    default: return Num.Mult(l.Value);
                }
            }
        }

        /// <summary>One phenomenon: ingredients with progress, strength and perk.</summary>
        sealed class PhenoCard : VisualElement
        {
            readonly StormApp _app;
            readonly int _p;
            readonly Label _name, _status, _perk;
            readonly Bar[] _bars;
            readonly Label[] _ingLabels;
            readonly Icon _icon;

            public PhenoCard(StormApp app, int p)
            {
                _app = app;
                _p = p;
                var def = app.Content.Phenomena[p];
                AddToClassList("se-card");
                var head = Ui.Div("se-row");
                _icon = new Icon("storm", 30);
                head.Add(_icon);
                _name = Ui.Text(def.Name, "se-h3", "se-grow");
                _name.style.marginLeft = 8;
                head.Add(_name);
                _status = Ui.Text("", "se-tag");
                head.Add(_status);
                Add(head);

                int n = def.Special ? 1 : def.Ingredients.Length;
                _bars = new Bar[n];
                _ingLabels = new Label[n];
                for (int i = 0; i < n; i++)
                {
                    var row = Ui.Div("se-row");
                    row.style.marginTop = 4;
                    _ingLabels[i] = Ui.Text("", "se-small");
                    _ingLabels[i].style.width = 190;
                    row.Add(_ingLabels[i]);
                    var color = def.Special ? Palette.Chaos : Palette.Category[(int)def.Ingredients[i].Cat];
                    _bars[i] = new Bar(color, 10);
                    row.Add(_bars[i]);
                    Add(row);
                }
                _perk = Ui.Text("", "se-small");
                _perk.style.marginTop = 4;
                Add(_perk);
            }

            public void Refresh()
            {
                var g = _app.Game;
                var def = g.C.Phenomena[_p];
                var v = g.View;
                bool found = g.Discovered[_p];
                bool active = v.PhenoActive[_p];
                _name.Set(found ? def.Name : L.T("??? Unknown phenomenon"));
                _icon.Tint = found ? Palette.ForPheno(def.Id) : Palette.Metal;
                EnableInClassList("active", active);
                EnableInClassList("dim", !found && !active);
                _status.Set(active ? L.F("ACTIVE {0}", Num.Mult(v.PhenoStrength[_p])) : found ? L.T("DISCOVERED") : L.T("UNDISCOVERED"));
                _status.style.backgroundColor = active ? Palette.GreenDark : found ? Palette.NavySoft : Palette.Metal;

                if (def.Special)
                {
                    _ingLabels[0].Set(g.Build.IsEquipped(g.C.ModuleIndex("chaos_reactor")) ? L.T("Instability >= 40% (in storms)") : L.T("Equip the Chaos Reactor"));
                    _bars[0].Value = g.Storm != null ? (float)(g.Storm.Instability / g.C.B.AnomalyThreshold) : 0;
                }
                else
                {
                    for (int i = 0; i < def.Ingredients.Length; i++)
                    {
                        var ing = def.Ingredients[i];
                        double cur = v.CatValue((int)ing.Cat);
                        _ingLabels[i].Set(g.C.Stats[(int)Content.CatStat((int)ing.Cat)].Name + " " + Num.Mult(cur) + " / x" + ing.Threshold.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
                        _bars[i].Value = (float)(cur / ing.Threshold);
                    }
                }
                _perk.Set(found ? def.Desc + " " + L.F("Storm perk: {0}", def.PerkText) : L.F("Reach every threshold to discover it (+{0} Storm Cores).", g.C.B.DiscoveryCores));
            }
        }
    }
}
