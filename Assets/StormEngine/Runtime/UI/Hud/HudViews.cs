using System;
using StormEngine.Core;
using StormEngine.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Top bar: level ring, currencies and one menu button. The less frequent buttons (Stormpedia,
    /// atmosphere stats, the weekly condition, visiting, settings, home) drop down from the menu so
    /// the view stays clean.
    /// </summary>
    public sealed class TopBar : VisualElement
    {
        readonly Label _energy, _cores, _research, _gems, _weeklyLabel;
        readonly VisualElement _researchPill, _coresPill, _weeklyChip, _menu;
        readonly Button _menuButton, _statsItem, _weeklyItem;
        readonly LevelPill _levelPill;
        readonly StormApp _app;

        public event Action WeeklyClicked;
        public event Action LevelClicked;
        public event Action GemsClicked;
        public event Action PediaClicked;
        public event Action HomeClicked;
        public event Action VisitClicked;
        public event Action SettingsClicked;
        public event Action StatsClicked;
        /// <summary>Raised when the dropdown opens (true) or closes (false).</summary>
        public event Action<bool> MenuToggled;

        public bool MenuOpen { get; private set; }

        /// <summary>
        /// The dropdown. GameUI hosts it in a layer above the panel sheet, because the stage the bar
        /// lives on is clipped where an open panel begins.
        /// </summary>
        public VisualElement Menu => _menu;

        public TopBar(StormApp app)
        {
            _app = app;
            AddToClassList("se-topbar");
            pickingMode = PickingMode.Ignore;

            _levelPill = new LevelPill(app) { name = "level-pill" };
            _levelPill.Clicked += () => LevelClicked?.Invoke();
            Add(_levelPill);

            // An active Energy Surge shows on its own chip (x2 and the time left), not on this pill.
            var energy = Pill("energy", 28, out _energy);
            energy.AddToClassList("energy");
            Add(energy);
            _coresPill = Pill("core", 24, out _cores);
            _coresPill.AddToClassList("small");
            Add(_coresPill);
            _researchPill = Pill("research", 24, out _research);
            _researchPill.AddToClassList("small");
            // Slots let the stylesheet drop these on narrow (portrait) screens even while shown.
            Add(Slot("research", _researchPill));

            Add(Ui.Spacer());

            var gems = Pill("gem", 26, out _gems);
            gems.AddToClassList("gems");
            gems.pickingMode = PickingMode.Position;
            gems.Add(Ui.Text("+", "se-pill-plus"));
            gems.RegisterCallback<ClickEvent>(_ => GemsClicked?.Invoke());
            Add(gems);

            _weeklyChip = Ui.Div("se-roundbtn", "weekly");
            _weeklyChip.Add(new Icon("calendar", 24));
            _weeklyChip.RegisterCallback<ClickEvent>(_ => WeeklyClicked?.Invoke());
            Add(Slot("weekly", _weeklyChip));

            _menuButton = RoundButton("menu", () => SetMenu(!MenuOpen));
            _menuButton.name = "menu-button";
            _menuButton.AddToClassList("se-menubtn");
            _menuButton.Add(Dot());
            Add(_menuButton);

            // The dropdown hangs under the bar, right-aligned with the menu button.
            _menu = Ui.Div("se-hudmenu");
            _menu.pickingMode = PickingMode.Ignore;
            var pedia = MenuItem("book", L.T("Stormpedia"), () => PediaClicked?.Invoke());
            pedia.name = "pedia-button";
            pedia.Add(Dot());
            _statsItem = MenuItem("atmosphere", L.T("Atmosphere"), () => StatsClicked?.Invoke());
            _weeklyItem = MenuItem("calendar", "", () => WeeklyClicked?.Invoke());
            _weeklyLabel = _weeklyItem.Q<Label>();
            MenuItem("globe", L.T("Visit islands"), () => VisitClicked?.Invoke());
            MenuItem("settings", L.T("Settings"), () => SettingsClicked?.Invoke());
            MenuItem("home", L.T("Home"), () => HomeClicked?.Invoke());
            _menu.Show(false);
            Add(_menu);
        }

        static VisualElement Dot()
        {
            var dot = Ui.Div("se-badge", "se-pediadot");
            dot.pickingMode = PickingMode.Ignore;
            return dot;
        }

        Button MenuItem(string icon, string label, Action onClick)
        {
            var b = new Button(() =>
            {
                _app.Sfx?.Click();
                SetMenu(false, false);
                onClick();
            }) { focusable = false };
            b.AddToClassList("se-hudmenu-item");
            var box = Ui.Div("se-hudmenu-icon");
            box.pickingMode = PickingMode.Ignore;
            box.Add(new Icon(icon, 26));
            b.Add(box);
            b.Add(Ui.Text(label, "se-hudmenu-label"));
            _menu.Add(b);
            return b;
        }

        /// <summary>Opens or closes the dropdown.</summary>
        public void SetMenu(bool open, bool sound = true)
        {
            if (open == MenuOpen) return;
            MenuOpen = open;
            _menu.Show(open);
            _menuButton.EnableInClassList("selected", open);
            if (open)
            {
                RefreshMenu();
                PlaceMenu();
            }
            if (sound) _app.Sfx?.Click();
            MenuToggled?.Invoke(open);
        }

        /// <summary>Hangs the dropdown under the menu button, right-aligned with it, wherever it is hosted.</summary>
        void PlaceMenu()
        {
            var host = _menu.parent;
            if (host == null || host == this) return;
            Rect button = _menuButton.worldBound, box = host.worldBound;
            if (float.IsNaN(button.yMax) || float.IsNaN(box.xMax)) return;
            _menu.style.top = button.yMax - box.y;
            _menu.style.right = box.xMax - button.xMax;
        }

        void RefreshMenu()
        {
            var g = _app.Game;
            _statsItem.EnableInClassList("selected", _app.Save.settings.showStats);
            bool weekly = g.Build.Weekly >= 0;
            _weeklyItem.Show(weekly);
            if (weekly) _weeklyLabel.Set(L.F("This week: {0}", g.C.Weekly[g.Build.Weekly].Name));
        }

        static VisualElement Slot(string name, VisualElement child)
        {
            var slot = Ui.Div("se-topslot", name);
            slot.pickingMode = PickingMode.Ignore;
            slot.Add(child);
            return slot;
        }

        static Button RoundButton(string icon, Action onClick)
        {
            var b = new Button(onClick) { focusable = false };
            b.AddToClassList("se-roundbtn");
            b.Add(new Icon(icon, 24));
            return b;
        }

        static VisualElement Pill(string icon, float iconSize, out Label value)
        {
            var pill = Ui.Div("se-pill");
            pill.pickingMode = PickingMode.Ignore;
            pill.Add(new Icon(icon, iconSize));
            value = Ui.Text("0", "se-pill-value");
            pill.Add(value);
            return pill;
        }

        /// <summary>Whole numbers up to 9,999, then the compact form (12.3K) so the bar never overflows.</summary>
        static string Count(long v) => v < 10000 ? Num.Int(v) : Num.Format(v);

        public void Refresh(float dt)
        {
            var g = _app.Game;
            _levelPill.Refresh(dt);
            string energy = Num.Format(g.Energy), cores = Count(g.Cores), gems = Count(g.Gems);
            _energy.Set(energy);
            _cores.Set(cores);
            _research.Set(Num.Int(g.Research));
            _gems.Set(gems);
            bool coresShown = g.Cores > 0 || g.Stats.Storms > 0;
            _coresPill.Show(coresShown);
            _researchPill.Show(g.IsFeatureUnlocked("research") || g.Research > 0 || g.Prestiges > 0);
            _weeklyChip.Show(g.Build.Weekly >= 0);
            // Long numbers switch the bar to slightly smaller text instead of overflowing.
            int chars = energy.Length + (coresShown ? cores.Length : 0) + gems.Length;
            EnableInClassList("tight", chars > 12);
            if (MenuOpen) RefreshMenu();
        }
    }

    /// <summary>Compact "1.82M /s" readout. Tapping it opens the Atmosphere Lab.</summary>
    public sealed class PowerCard : VisualElement
    {
        readonly Label _value, _class;
        readonly StormApp _app;

        public event Action Clicked;

        public PowerCard(StormApp app)
        {
            _app = app;
            AddToClassList("se-power");
            Add(Ui.Text(L.T("STORM POWER"), "se-power-title"));
            var row = Ui.Div("se-row");
            row.pickingMode = PickingMode.Ignore;
            _value = Ui.Text("0 /s", "se-power-value");
            row.Add(_value);
            Add(row);
            _class = Ui.Text("", "se-power-class");
            _class.Show(false);
            Add(_class);
            RegisterCallback<ClickEvent>(_ => Clicked?.Invoke());
        }

        public void Refresh()
        {
            var g = _app.Game;
            _value.Set(Num.Format(g.EnergyPerSecond) + " /s");
            var v = g.View;
            bool pheno = v.Dominant >= 0 && !g.IsStorming;
            _class.Show(pheno);
            if (pheno)
            {
                _class.Set(g.C.Phenomena[v.Dominant].Name + (v.ActivePhenos > 1 ? " +" + (v.ActivePhenos - 1) : ""));
                _class.style.backgroundColor = Palette.ForPheno(g.C.Phenomena[v.Dominant].Id).Darken(0.2f);
            }
        }
    }

    /// <summary>Category multipliers. Shown from the top bar's menu ("Atmosphere") to keep the view clean.</summary>
    public sealed class StatChips : VisualElement
    {
        readonly Label[] _values = new Label[7];
        readonly VisualElement[] _chips = new VisualElement[7];
        readonly VisualElement _list;
        readonly StormApp _app;
        static readonly string[] Icons = { "heat", "water", "wind", "cloud", "bolt", "synergy", "pump" };

        /// <summary>Row above the chips for round buttons (Daily Rewards).</summary>
        public VisualElement Head { get; }
        static readonly string[] Names = { L.N("Temperature"), L.N("Humidity"), L.N("Wind"), L.N("Cloud"), L.N("Electricity"), L.N("Synergy"), L.N("Pressure") };

        public event Action Clicked;

        /// <summary>Shows or hides the chips (remembered in the settings).</summary>
        public void Toggle()
        {
            _app.Save.settings.showStats = !_app.Save.settings.showStats;
            Refresh();
        }

        public StatChips(StormApp app)
        {
            _app = app;
            AddToClassList("se-stats");
            pickingMode = PickingMode.Ignore;
            Head = Ui.Div("se-stats-head");
            Head.pickingMode = PickingMode.Ignore;
            Add(Head);
            _list = Ui.Div("se-stats-list");
            _list.pickingMode = PickingMode.Ignore;
            Add(_list);
            for (int i = 0; i < 7; i++)
            {
                var chip = Ui.Div("se-chip");
                chip.Add(new Icon(Icons[i], 22));
                chip.Add(Ui.Text(L.T(Names[i]), "se-chip-label"));
                _values[i] = Ui.Text("x1", "se-chip-value");
                chip.Add(_values[i]);
                chip.RegisterCallback<ClickEvent>(_ => Clicked?.Invoke());
                _chips[i] = chip;
                _list.Add(chip);
            }
        }

        int[] _catMachine;

        public void Refresh()
        {
            var g = _app.Game;
            bool show = _app.Save.settings.showStats;
            _list.Show(show);
            if (!show) return;
            var v = g.View;
            if (_catMachine == null)
            {
                _catMachine = new int[5];
                for (int c = 0; c < 5; c++)
                {
                    _catMachine[c] = -1;
                    for (int m = 0; m < g.C.Machines.Length; m++)
                        if (g.C.Machines[m].Cat == c) _catMachine[c] = m;
                }
            }
            bool anyPheno = false;
            foreach (bool d in g.Discovered) anyPheno |= d;
            _chips[5].Show(anyPheno || g.IsFeatureUnlocked("atmosphere"));
            for (int c = 0; c < 5; c++)
            {
                int m = _catMachine[c];
                _chips[c].Show(m < 0 || g.MachineUnlocked[m] || v.CatValue(c) > 1.0001);
                _values[c].Set(Num.Mult(v.CatValue(c)));
                bool boosted = false;
                for (int b = 0; b < g.C.Boosts.Length; b++)
                    if (g.BoostActive[b] > 0 && g.C.Boosts[b].Target == Content.CatStat(c)) boosted = true;
                _chips[c].EnableInClassList("boosted", boosted);
            }
            _values[5].Set(Num.Mult(v.Synergy));
            _chips[5].EnableInClassList("inactive", v.ActivePhenos == 0);
            _values[6].Set(Num.Format(v.Stat[(int)StatId.BasePressure]));
        }
    }

    /// <summary>Current tutorial / milestone goal. Tapping folds it into a small chip (remembered) to free the view.</summary>
    public sealed class GoalCard : VisualElement
    {
        readonly Label _text, _reward;
        readonly Bar _bar;
        readonly StormApp _app;
        string _goalId;

        public GoalCard(StormApp app)
        {
            _app = app;
            AddToClassList("se-goal");
            var head = Ui.Div("se-row");
            head.Add(Ui.Text(L.T("GOAL"), "se-goal-title"));
            _reward = Ui.Text("", "se-goal-reward");
            head.Add(_reward);
            Add(head);
            _text = Ui.Text("", "se-goal-text");
            Add(_text);
            _bar = new Bar(Palette.Green, 8);
            Add(_bar);
            RegisterCallback<ClickEvent>(_ =>
            {
                _app.Sfx?.Click();
                _app.Save.settings.goalFolded = !_app.Save.settings.goalFolded;
                EnableInClassList("folded", _app.Save.settings.goalFolded);
            });
            EnableInClassList("folded", _app.Save.settings.goalFolded);
        }

        public void Refresh()
        {
            var g = _app.Game;
            var goal = g.CurrentGoal;
            this.Show(goal != null);
            if (goal == null) return;
            if (_goalId != goal.Id)
            {
                _goalId = goal.Id;
                _text.Set(KeepEndTogether(goal.Text));
                var parts = new System.Collections.Generic.List<string>();
                if (goal.RewardGems > 0) parts.Add(L.F("+{0} gems", goal.RewardGems));
                if (goal.RewardCores > 0) parts.Add(L.F("+{0} cores", goal.RewardCores));
                if (goal.RewardSeconds > 0 && parts.Count < 2) parts.Add(L.T("+Energy"));
                _reward.Set(string.Join(" · ", parts));
            }
            double progress = g.GoalProgress(goal);
            if (goal.Kind == GoalKind.RunEnergy || goal.Kind == GoalKind.StormScore)
                _bar.Value = (float)(Math.Log10(1 + progress) / Math.Log10(1 + goal.Amount));
            else
                _bar.Value = (float)(progress / goal.Amount);
        }

        /// <summary>Joins a short last word ("5", "x2") to the one before it so a line never holds just a number.</summary>
        static string KeepEndTogether(string text)
        {
            int space = text.LastIndexOf(' ');
            return space > 0 && text.Length - space - 1 <= 3 ? text.Substring(0, space) + " " + text.Substring(space + 1) : text;
        }
    }

    /// <summary>Small rewarded-ad offers on the HUD: Energy Surge, Instant Charge and Double Cores.</summary>
    public sealed class AdOffers
    {
        readonly StormApp _app;
        readonly Button _surge, _charge, _cores;
        readonly Label _surgeText, _chargeText, _coresText;
        float _coresWindow;
        long _coresAmount;
        float _timer;

        public VisualElement Surge => _surge;
        public VisualElement Storm { get; }

        public AdOffers(StormApp app)
        {
            _app = app;
            _surge = Offer("energy", out _surgeText, () => _app.WatchAd("ad_surge"));
            _surge.AddToClassList("surge");
            Storm = Ui.Div("se-offers");
            Storm.pickingMode = PickingMode.Ignore;
            _charge = Offer("charge", out _chargeText, () => _app.WatchAd("ad_charge"));
            _cores = Offer("core", out _coresText, () =>
            {
                _coresWindow = 0;
                _app.WatchAd("ad_cores");
            });
            Storm.Add(_cores);
            Storm.Add(_charge);
            _app.Game.StormEnded += (r, _, __) =>
            {
                _coresAmount = r.Cores;
                _coresWindow = r.Cores > 0 ? 25f : 0f;
            };
        }

        static Button Offer(string icon, out Label text, Action onClick)
        {
            var b = new Button(onClick) { focusable = false };
            b.AddToClassList("se-adoffer");
            var play = Ui.Div("se-adoffer-play");
            play.Add(new Icon("play", 16));
            b.Add(play);
            b.Add(new Icon(icon, 22));
            text = Ui.Text("", "se-adoffer-text");
            b.Add(text);
            b.Show(false);
            return b;
        }

        public void Tick(float dt)
        {
            if (_coresWindow > 0) _coresWindow -= dt;
            _timer -= dt;
            if (_timer > 0) return;
            _timer = 0.25f;
            var g = _app.Game;
            bool surgeActive = g.SurgeSeconds > 0;
            bool canSurge = _app.CanOfferAd("ad_surge");
            // During a storm the live storm card takes the top of the stage.
            _surge.Show((canSurge || surgeActive) && !g.IsStorming);
            _surge.EnableInClassList("active", surgeActive);
            _surge.SetEnabledState(canSurge);
            // Short text: the bolt icon already says "Energy", and the chip must fit beside the goal card.
            _surgeText.Set(surgeActive ? "x2  " + Clock(g.SurgeSeconds) + (canSurge ? "  " + L.T("+15m") : "") : "x2  " + L.F("{0} min", 15));

            bool cores = _coresWindow > 0 && !g.IsStorming && _app.CanOfferAd("ad_cores");
            _cores.Show(cores);
            if (cores) _coresText.Set(L.F("+{0} Cores", Num.Int(_coresAmount)));
            bool charge = !cores && !g.IsStorming && g.StormCharge < 0.85 && g.Stats.Storms > 0 && _app.CanOfferAd("ad_charge");
            _charge.Show(charge);
            if (charge) _chargeText.Set(L.T("Charge now"));
        }

        static string Clock(double seconds)
        {
            int s = (int)Math.Ceiling(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    /// <summary>One timed boost with a radial cooldown overlay.</summary>
    public sealed class BoostButton : Button
    {
        readonly int _index;
        readonly StormApp _app;
        readonly Label _time;
        readonly VisualElement _dial;
        readonly Icon _icon;
        float _fraction = -1;
        bool _active;

        public BoostButton(StormApp app, int index) : base()
        {
            _app = app;
            _index = index;
            focusable = false;
            AddToClassList("se-boost");
            var def = app.Content.Boosts[index];

            // The dial never shrinks, so the painted ring cannot spill over the labels.
            _dial = new VisualElement { pickingMode = PickingMode.Ignore };
            _dial.AddToClassList("se-boost-dial");
            _dial.generateVisualContent += PaintDial;
            _icon = new Icon(def.Icon, 30);
            _dial.Add(_icon);
            Add(_dial);

            Add(Ui.Text(def.Name, "se-boost-name"));
            // Locked boosts show a padlock and the level needed ("Lv 10"); the tap explains which machine.
            var timeRow = Ui.Div("se-boost-timerow");
            timeRow.pickingMode = PickingMode.Ignore;
            _lock = new Icon("lock", 13);
            _lock.AddToClassList("se-boost-lock");
            timeRow.Add(_lock);
            _time = Ui.Text("", "se-boost-time");
            timeRow.Add(_time);
            Add(timeRow);
            clicked += () => _app.ActivateBoost(_index);
        }

        readonly Icon _lock;

        public void Refresh()
        {
            var g = _app.Game;
            bool unlocked = g.IsBoostUnlocked(_index);
            int machine = g.C.MachineIndex(g.C.Boosts[_index].UnlockMachine);
            this.Show(unlocked || machine < 0 || g.MachineUnlocked[machine]);
            EnableInClassList("locked", !unlocked);
            float fraction;
            bool active = g.BoostActive[_index] > 0;
            _lock.Show(!unlocked);
            if (!unlocked)
            {
                _time.Set(L.F("Lv {0}", g.C.Boosts[_index].UnlockLevel));
                fraction = 0;
            }
            else if (active)
            {
                _time.Set(Num.Seconds(g.BoostActive[_index]));
                fraction = (float)(g.BoostActive[_index] / Math.Max(0.01, g.Mods.BoostDuration[_index]));
            }
            else if (g.BoostCooldown[_index] > 0)
            {
                _time.Set(Num.Seconds(g.BoostCooldown[_index]));
                fraction = -(float)(g.BoostCooldown[_index] / Math.Max(0.01, g.Mods.BoostCooldown[_index]));
            }
            else
            {
                _time.Set(L.T("READY"));
                fraction = 0;
            }
            EnableInClassList("active", active);
            if (Mathf.Abs(fraction - _fraction) > 0.004f || active != _active)
            {
                _fraction = fraction;
                _active = active;
                _dial.MarkDirtyRepaint();
            }
        }

        void PaintDial(MeshGenerationContext ctx)
        {
            var r = _dial.contentRect;
            var p = ctx.painter2D;
            var c = r.center;
            float rad = r.width * 0.5f - 2;
            var col = Palette.Category[Mathf.Clamp(Content.StatCat(_app.Content.Boosts[_index].Target), 0, 4)];
            rad = Mathf.Min(r.width, r.height) * 0.5f - 2;
            Draw.FillCircle(p, c, rad, col.Lighten(0.72f));
            if (_fraction > 0)
                Draw.ArcStroke(p, c, rad - 2, -90, -90 + 360 * _fraction, col, 5);
            else if (_fraction < 0)
                Draw.Pie(p, c, rad, -_fraction, new Color(0.08f, 0.12f, 0.23f, 0.45f));
            Draw.ArcStroke(p, c, rad, 0, 360, Palette.Ink, 2.5f);
        }
    }

    /// <summary>The big UNLEASH STORM button with a charge fill.</summary>
    public sealed class StormButton : Button
    {
        readonly StormApp _app;
        readonly VisualElement _fill;
        readonly Label _title, _sub;
        float _pulse;
        float _forecastTimer;
        string _forecast;

        public StormButton(StormApp app)
        {
            _app = app;
            focusable = false;
            AddToClassList("se-stormbtn");
            _fill = Ui.Div("se-stormbtn-fill");
            _fill.pickingMode = PickingMode.Ignore;
            Add(_fill);
            // Long words ("CARREGANDO", "ENTFESSELN!") shrink to the button instead of spilling out.
            _title = Ui.Text(L.T("CHARGING"), "se-stormbtn-title").FitWidth(11f, 10f);
            _title.pickingMode = PickingMode.Ignore;
            Add(_title);
            _sub = Ui.Text("", "se-stormbtn-sub");
            _sub.pickingMode = PickingMode.Ignore;
            Add(_sub);
            clicked += () => _app.Unleash();
        }

        public void Tick(float dt)
        {
            var g = _app.Game;
            bool storming = g.IsStorming;
            bool ready = g.CanUnleash;
            EnableInClassList("storming", storming);
            EnableInClassList("ready", ready);
            if (storming)
            {
                _title.Set(L.T("STORMING"));
                _sub.Set(L.F("{0} left", Num.Seconds(g.Storm.SecondsLeft)));
                _fill.style.width = Length.Percent(0);
                style.scale = new Scale(Vector2.one);
            }
            else if (ready)
            {
                _title.Set(L.T("UNLEASH!"));
                _forecastTimer -= dt;
                if (_forecastTimer <= 0 || _forecast == null)
                {
                    _forecastTimer = 2f;
                    _forecast = L.F("Forecast {0}", Num.Format(g.Forecast(false).Score));
                }
                _sub.Set(g.Mods.Automation && g.AutoStorm ? L.T("Auto-storm on") : _forecast);
                _fill.style.width = Length.Percent(0);
                _pulse += dt * 5f;
                float s = 1f + Mathf.Sin(_pulse) * 0.035f;
                style.scale = new Scale(new Vector2(s, s));
            }
            else
            {
                _title.Set(L.T("CHARGING"));
                double left = (1 - g.StormCharge) * g.ChargeSeconds;
                _sub.Set(Num.Percent(g.StormCharge) + " · " + Num.Seconds(left));
                _fill.style.width = Length.Percent((float)(g.StormCharge * 100));
                style.scale = new Scale(Vector2.one);
            }
            if (_title.text != _fitted)
            {
                _fitted = _title.text;
                _title.Refit();
            }
        }

        string _fitted;
    }

    /// <summary>Live storm readout: class, score, chain and multipliers.</summary>
    public sealed class StormHud : VisualElement
    {
        readonly StormApp _app;
        readonly Label _tag, _class, _score, _line, _mega;
        readonly Bar _time;

        public StormHud(StormApp app)
        {
            _app = app;
            AddToClassList("se-stormhud");
            pickingMode = PickingMode.Ignore;
            var card = Ui.Div("se-stormhud-card");
            card.pickingMode = PickingMode.Ignore;
            _tag = Ui.Text(L.T("WEEKLY TOURNAMENT"), "se-stormhud-tag");
            _class = Ui.Text(L.T("SQUALL"), "se-stormhud-class");
            _score = Ui.Text("0", "se-stormhud-score");
            _line = Ui.Text("", "se-stormhud-line");
            _time = new Bar(Palette.Hex(0xB99CFF), 8);
            _time.style.width = 220;
            _time.style.flexGrow = 0;
            _time.style.marginTop = 4;
            _mega = Ui.Text(L.T("MEGA STORM"), "se-mega");
            card.Children(_tag, _class, _score, _line, _time, _mega);
            Add(card);
            this.Show(false);
        }

        public void Refresh()
        {
            var g = _app.Game;
            var storm = g.Storm;
            this.Show(storm != null);
            if (storm == null) return;
            _tag.Show(g.IsTournamentRun);
            EnableInClassList("tournament", g.IsTournamentRun);
            int dom = storm.Snap.Dominant;
            _class.Set((dom >= 0 ? g.C.Phenomena[dom].Name : L.T("Squall")).ToUpperInvariant());
            _class.style.color = dom >= 0 ? Palette.ForPheno(g.C.Phenomena[dom].Id).Lighten(0.35f) : Color.white;
            _score.Set(Num.Format(storm.Score));
            _line.Set(L.F("Chain {0}  ·  Intensity {1}  ·  {2} strikes", Num.Mult(storm.Chain), Num.Mult(storm.LastIntensity), storm.Strikes));
            _time.Value = 1 - (float)storm.Progress;
            string mega = storm.LastMega >= 3 ? L.T("PERFECT STORM") : storm.LastMega >= 2 ? L.T("HYPER STORM") : storm.LastMega > 1 ? L.T("MEGA STORM") : null;
            if (storm.LastPerfect > 1) mega = (mega != null ? mega + " + " : "") + L.T("PERFECT WINDOW");
            _mega.Show(mega != null);
            if (mega != null) _mega.Set(mega);
        }
    }
}
