using System;
using System.Collections.Generic;
using StormEngine.Core;
using StormEngine.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Modal card with a navy header, a body and a row of actions.</summary>
    public class Popup : VisualElement
    {
        protected readonly VisualElement HeadEl;
        protected readonly VisualElement BodyEl;
        protected readonly VisualElement ActionsEl;
        protected readonly Label TitleEl;
        protected readonly Label SubEl;
        internal Action Closed;
        public bool DismissOnScrim = true;

        public Popup(string title, string sub = null, string icon = null)
        {
            AddToClassList("se-popup");
            HeadEl = Ui.Div("se-popup-head");
            if (icon != null) HeadEl.Add(new Icon(icon, 44));
            TitleEl = Ui.Text(title, "se-popup-title");
            HeadEl.Add(TitleEl);
            SubEl = Ui.Text(sub ?? "", "se-popup-sub");
            SubEl.Show(!string.IsNullOrEmpty(sub));
            HeadEl.Add(SubEl);
            Add(HeadEl);

            var scroll = new ScrollView(ScrollViewMode.Vertical).Smooth();
            scroll.style.flexShrink = 1;
            BodyEl = Ui.Div("se-popup-body");
            scroll.Add(BodyEl);
            Add(scroll);

            ActionsEl = Ui.Div("se-popup-actions");
            Add(ActionsEl);
        }

        protected Button Action(string text, Action onClick, params string[] classes)
        {
            var b = Ui.Btn(text, () =>
            {
                onClick?.Invoke();
                Close();
            }, classes);
            ActionsEl.Add(b);
            return b;
        }

        protected void Kv(string key, string value, VisualElement into = null)
        {
            var row = Ui.Div("se-kv");
            row.Add(new Label(key));
            row.Add(Ui.Text(value, "v"));
            (into ?? BodyEl).Add(row);
        }

        public void Close() => Closed?.Invoke();

        public virtual void Tick(float dt) { }
    }

    public sealed class ConfirmPopup : Popup
    {
        public ConfirmPopup(string title, string text, string confirm, Action onConfirm, string confirmClass = "green", string cancel = "", Action onCancel = null)
            : base(title)
        {
            BodyEl.Add(Ui.Text(text, "se-body"));
            // With a cancel callback the player must choose, so the callback always runs.
            if (onCancel != null) DismissOnScrim = false;
            if (cancel != null) Action(cancel == "" ? L.T("Cancel") : cancel, onCancel, "white");
            Action(confirm, onConfirm, confirmClass);
        }
    }

    public sealed class StormResultPopup : Popup
    {
        readonly StormApp _app;
        readonly Label _submit;
        readonly bool _submitted;

        public StormResultPopup(StormApp app, StormResult r, RecordFlags flags)
            : base(r.Tournament ? L.T("TOURNAMENT STORM") : StormSubmission.ClassName(app.Content, r.ClassIndex).ToUpperInvariant(),
                r.Tournament ? StormSubmission.ClassName(app.Content, r.ClassIndex) + " · " + L.T("weekly tournament") : L.T("STORM COMPLETE"), r.Tournament ? "trophy" : "storm")
        {
            _app = app;
            _submitted = r.Tournament && flags.Any;
            var c = app.Content;
            var g = app.Game;
            var color = r.ClassIndex >= 0 ? Palette.ForPheno(c.Phenomena[r.ClassIndex].Id) : Palette.Metal;
            HeadEl.style.backgroundColor = r.Tournament ? Palette.Hex(0x7A4A0A) : color.Darken(0.55f);

            BodyEl.Add(Ui.Text(r.Tournament ? L.T("TOURNAMENT SCORE") : L.T("STORM SCORE"), "se-small", "se-center"));
            BodyEl.Add(Ui.Text(Num.Format(r.Score), "se-bignum"));

            var badges = Ui.Div("se-row", "se-wrap");
            badges.style.justifyContent = Justify.Center;
            badges.style.marginBottom = 8;
            if (r.Tournament)
            {
                if (flags.Weekly) badges.Add(Badge(L.T("NEW WEEKLY BEST"), Palette.Yellow));
                if (flags.AllTime) badges.Add(Badge(L.T("TOURNAMENT RECORD"), Palette.Hex(0xFF7A6E)));
            }
            else if (flags.AllTime) badges.Add(Badge(L.T("NEW PERSONAL BEST"), Palette.Yellow));
            if (flags.Class && !flags.AllTime) badges.Add(Badge(L.T("CLASS BEST"), color));
            if (r.Collapsed) badges.Add(Badge(L.T("COLLAPSED"), Palette.Chaos));
            BodyEl.Add(badges);
            if (flags.Any)
            {
                // A record is the moment players want to show off: one tap to the share sheet.
                string cls = StormSubmission.ClassName(c, r.ClassIndex);
                var share = Ui.IconBtn("share", L.T("Share"), () =>
                {
                    app.Sfx?.Click();
                    string text = L.F("I just unleashed a {0} storm in Storm Engine: {1} points! Can you beat it?", cls, Num.Format(r.Score));
                    if (!Community.Share("Storm Engine", text + "\n" + ReleaseConfig.Instance.StoreUrl))
                        app.Ui?.Toast("share", L.T("Copied! Paste it anywhere to share."));
                }, "blue", "small");
                share.style.alignSelf = Align.Center;
                share.style.marginBottom = 8;
                BodyEl.Add(share);
            }
            if (r.Tournament)
            {
                var info = Ui.Text(L.F("Best this week: {0}", Num.Format(g.Tournament.Best)) + "  ·  " + L.F("Attempts left today: {0}", g.TournamentAttemptsLeft), "se-small", "se-center");
                info.style.marginBottom = 6;
                BodyEl.Add(info);
            }

            Kv(L.T("Duration"), Num.Fixed(r.Seconds, 1) + "s");
            Kv(L.T("Average Storm Power"), Num.Format(r.AveragePower) + "/s");
            Kv(L.T("Peak chain reaction"), Num.Mult(r.PeakChain) + "  (" + L.F("{0} strikes", r.Strikes) + ")");
            Kv(L.T("Mega Storm time"), Num.Fixed(r.MegaTicks / 10.0, 1) + "s  (" + L.F("{0} phenomena", r.MaxActivePhenos) + ")");
            Kv(L.T("Perfect Window time"), Num.Fixed(r.PerfectTicks / 10.0, 1) + "s");
            Kv(L.T("Boosts fired during storm"), r.Events.Count.ToString());
            Kv(L.T("Energy earned"), "+" + Num.Format(r.Score * g.IncomeMultiplier));
            Kv(L.T("Storm Cores"), "+" + r.Cores);

            if (r.ClassScore != null)
            {
                BodyEl.Add(Ui.Text(L.T("SCORE BY CLASS"), "se-section"));
                for (int i = 0; i < r.ClassScore.Length; i++)
                {
                    if (r.ClassScore[i] <= 0) continue;
                    var row = Ui.Div("se-row");
                    row.style.marginBottom = 3;
                    var name = Ui.Text(StormSubmission.ClassName(c, i - 1), "se-small");
                    name.style.width = 110;
                    row.Add(name);
                    var bar = new Bar(i == 0 ? Palette.Metal : Palette.ForPheno(c.Phenomena[i - 1].Id), 10);
                    bar.Value = (float)(r.ClassScore[i] / Math.Max(1e-9, r.Score));
                    row.Add(bar);
                    BodyEl.Add(row);
                }
            }

            _submit = Ui.Text("", "se-small", "se-center");
            _submit.style.marginTop = 8;
            BodyEl.Add(_submit);
            if (!r.Tournament && flags.Any && g.TournamentUnlocked)
                _submit.Set(L.T("Personal record! Take this engine into the Weekly Tournament to post a score on the global leaderboards."));

            if (r.Cores > 0 && app.CanOfferAd("ad_cores"))
            {
                Action(L.T("Continue"), null, "white");
                ActionsEl.Add(Ui.IconBtn("play", L.F("+{0} Cores", r.Cores), () =>
                {
                    app.WatchAd("ad_cores");
                    Close();
                }, "green"));
            }
            else Action(L.T("Continue"), null, "green");
        }

        static Label Badge(string text, Color color)
        {
            var l = Ui.Text(text, "se-tag");
            l.style.backgroundColor = color.Darken(0.1f);
            l.style.fontSize = 12;
            return l;
        }

        public override void Tick(float dt)
        {
            if (!_submitted) return;
            var o = _app.Online;
            if (o.State == OnlineState.Disabled) _submit.Set(L.T("Record saved locally. Connect Unity Gaming Services to compete globally."));
            else if (!o.IsOnline) _submit.Set(L.T("Offline: this record will be verified and submitted when you reconnect."));
            else if (o.PendingCount > 0) _submit.Set(L.T("Verifying storm replay with the server..."));
            else _submit.Set(L.T("Verified and submitted to the leaderboards.") + (string.IsNullOrEmpty(o.LastSubmitMessage) ? "" : " " + o.LastSubmitMessage));
        }
    }

    public sealed class OfflinePopup : Popup
    {
        public OfflinePopup(StormApp app, OfflineReport r)
            : base(L.T("WELCOME BACK!"), r.Locked ? L.F("You were away for {0}", Num.Time(r.Seconds)) : L.F("Your storm engine kept running for {0}", Num.Time(r.Seconds)), "clock")
        {
            if (r.Locked)
            {
                var g = app.Game;
                int u = app.Content.UpgradeIndex("offline_generator");
                int level = g.FeatureLevel("offline");
                BodyEl.Add(Ui.Text(L.T("Your engine sat idle while you were away. The storm chamber is charged and ready!"), "se-body"));
                string how = g.IsFeatureUnlocked("offline")
                    ? L.F("Build the Offline Generator in the Machines tab ({0} Energy + {1} Storm Cores) to keep producing while you are away.", Num.Format(g.UpgradeCost(u)), Num.Int(g.UpgradeCoreCost(u)))
                    : L.F("The Offline Generator unlocks at Engine Level {0}. It is expensive, but it keeps your engine producing while you are away.", level);
                var hint = Ui.Text(how, "se-small");
                hint.style.marginTop = 8;
                BodyEl.Add(hint);
                Action(L.T("Let's go!"), null, "green");
                return;
            }
            BodyEl.Add(Ui.Text(L.T("ENERGY COLLECTED"), "se-small", "se-center"));
            BodyEl.Add(Ui.Text("+" + Num.Format(r.Energy), "se-bignum"));
            Kv(L.T("Time counted"), Num.Time(r.CountedSeconds) + (r.Capped ? "  " + L.T("(cap reached)") : ""));
            Kv(L.T("Offline efficiency"), Num.Percent(r.Efficiency));
            Kv(L.T("Storms unleashed"), r.Storms > 0 ? Num.Int(r.Storms) : "0  " + L.T("(research Storm Automation)"));
            Kv(L.T("Lightning strikes"), Num.Int(r.Strikes));
            Kv(L.T("Storm Cores"), "+" + r.Cores);
            if (r.Capped) BodyEl.Add(Ui.Text(L.T("Upgrade the Offline Generator or research the Offline Capacitor to stay productive for longer."), "se-small"));

            if (app.CanOfferAd("ad_offline"))
            {
                Action(L.T("Collect"), null, "white");
                var double2 = Ui.IconBtn("play", L.T("Collect x2"), () =>
                {
                    app.WatchAd("ad_offline", null, () => app.Game.GrantOfflineBonus(r));
                    Close();
                }, "green");
                ActionsEl.Add(double2);
            }
            else Action(L.T("Collect"), null, "green");
        }
    }

    public sealed class DiscoveryPopup : Popup
    {
        public DiscoveryPopup(StormApp app, int p)
            : base(app.Content.Phenomena[p].Name.ToUpperInvariant(), L.T("NEW PHENOMENON DISCOVERED!"), "star")
        {
            var def = app.Content.Phenomena[p];
            HeadEl.style.backgroundColor = Palette.ForPheno(def.Id).Darken(0.5f);
            BodyEl.Add(Ui.Text(def.Desc, "se-body"));
            var perk = Ui.Text(L.F("Storm perk: {0}", def.PerkText), "se-h3");
            perk.style.marginTop = 8;
            perk.style.whiteSpace = WhiteSpace.Normal;
            BodyEl.Add(perk);
            if (!def.Special)
            {
                BodyEl.Add(Ui.Text(L.T("RECIPE"), "se-section"));
                foreach (var ing in def.Ingredients)
                    Kv(app.Content.Stats[(int)Content.CatStat((int)ing.Cat)].Name, "x" + ing.Threshold);
            }
            BodyEl.Add(Ui.Text(L.F("+{0} Storm Cores. Discoveries can unlock new modules in the workshop.", app.Content.B.DiscoveryCores), "se-small"));
            Action(L.T("Awesome!"), null, "green");
        }
    }

    public sealed class WelcomePopup : Popup
    {
        public WelcomePopup(StormApp app)
            : base(L.T("STORM ENGINE"), L.T("Build. Optimize. Create the biggest storms."), "storm")
        {
            BodyEl.Add(Step("machines", L.T("Build machines"), L.T("Every machine feeds a weather category. Categories multiply each other into Storm Power, and Storm Power makes Energy.")));
            BodyEl.Add(Step("charge", L.T("Unleash storms"), L.T("When the dome is charged, unleash a storm for a huge burst. Fire boosts during the storm so their effects overlap.")));
            BodyEl.Add(Step("star", L.T("Discover phenomena"), L.T("Push the right categories past their thresholds to discover Thunderstorms, Cyclones, Supercells and more.")));
            BodyEl.Add(Step("trophy", L.T("Compete every week"), L.T("Grind to make your engine stronger, then take it into the Weekly Tournament (Engine Level 8). Tournament storms are verified by the server and ranked worldwide.")));
            Action(L.T("Name my Storm Chaser"), () => app.Ui.ShowNamePopup(), "white");
            Action(L.T("Start the engine!"), null, "green");
        }

        static VisualElement Step(string icon, string title, string text)
        {
            var row = Ui.Div("se-row");
            row.style.marginBottom = 10;
            row.style.alignItems = Align.FlexStart;
            var box = Ui.Div("se-iconbox");
            box.Add(new Icon(icon, 32));
            row.Add(box);
            var col = Ui.Div("se-grow");
            col.Add(Ui.Text(title, "se-h3"));
            col.Add(Ui.Text(text, "se-small"));
            row.Add(col);
            return row;
        }
    }

    public sealed class NamePopup : Popup
    {
        public NamePopup(StormApp app)
            : base(L.T("STORM CHASER NAME"), L.T("Shown on the global leaderboards"), "user")
        {
            // Without Unity's "#1234" tag: saving the name unchanged would otherwise turn it into "Name1234#5678".
            string current = app.Online.PlayerName ?? "";
            int tag = current.LastIndexOf('#');
            var field = new TextField { maxLength = 20, value = tag > 0 ? current.Substring(0, tag) : current };
            field.AddToClassList("se-textfield");
            BodyEl.Add(field);
            BodyEl.Add(Ui.Text(L.T("Letters, numbers, - and _ only. Unity adds a short #tag to keep names unique."), "se-small"));
            BodyEl.Add(Ui.Text(L.T("Names are public, so keep it friendly. Offensive names are hidden from other players."), "se-small"));
            var error = Ui.Text(L.T("That name can't be used. Please pick another one."), "se-small", "se-bad");
            error.Show(false);
            BodyEl.Add(error);
            field.RegisterValueChangedCallback(_ => error.Show(false));
            Action(L.T("Cancel"), null, "white");
            // Not Action(): a rejected name keeps the popup open.
            ActionsEl.Add(Ui.Btn(L.T("Save"), async () =>
            {
                string name = OnlineManager.SanitizeName(field.value);
                if (string.IsNullOrEmpty(name) || !NameFilter.IsAllowed(name))
                {
                    app.Sfx.Deny();
                    error.Show(true);
                    return;
                }
                Close();
                bool ok = await app.Online.Rename(name);
                app.Ui.Toast(ok ? "user" : "offline", ok ? L.F("Name saved: {0}", app.Online.PlayerName) : L.T("Could not change the name right now."));
            }, "green"));
        }
    }

    public sealed class BuildViewPopup : Popup
    {
        public BuildViewPopup(StormApp app, BoardEntry e)
            : base(e.Name, "#" + e.Rank + "  ·  " + Num.Format(e.Score), "trophy")
        {
            var c = app.Content;
            if (string.IsNullOrEmpty(e.Summary) || !JsonReader.TryParse(e.Summary, out var o))
            {
                BodyEl.Add(Ui.Text(L.T("No build details were recorded for this storm."), "se-body"));
                Action(L.T("Close"), null, "white");
                AddVisit(app, e);
                return;
            }
            string cls = JsonReader.Str(o, "cls");
            int p = c.PhenoIndex(cls);
            Kv(L.T("Storm class"), p >= 0 ? c.Phenomena[p].Name : L.T("Squall"));
            var cats = JsonReader.Arr(o, "cat");
            if (cats != null)
                for (int i = 0; i < cats.Count && i < Content.CatCount; i++)
                    Kv(c.Stats[(int)Content.CatStat(i)].Name, Num.Mult(cats[i] is double d ? d : 0));
            Kv(L.T("Peak chain"), Num.Mult(JsonReader.Num(o, "chain", 1)));
            Kv(L.T("Lightning strikes"), ((long)JsonReader.Num(o, "strikes")).ToString());
            Kv(L.T("Duration"), Num.Fixed(JsonReader.Num(o, "sec"), 1) + "s");

            AddList(BodyEl, L.T("MODULES"), JsonReader.Arr(o, "mods"), id => { int m = c.ModuleIndex(id); return m >= 0 ? c.Modules[m].Name : id; });
            AddList(BodyEl, L.T("PHENOMENA"), JsonReader.Arr(o, "phen"), id => { int q = c.PhenoIndex(id); return q >= 0 ? c.Phenomena[q].Name : id; });
            string wk = JsonReader.Str(o, "wk");
            int w = c.WeeklyIndex(wk);
            if (w >= 0) Kv(L.T("Weekly condition"), c.Weekly[w].Name);
            Action(L.T("Close"), null, "white");
            AddVisit(app, e);
        }

        void AddVisit(StormApp app, BoardEntry e)
        {
            if (e.IsMe || string.IsNullOrEmpty(e.PlayerId) || !app.Game.IsFeatureUnlocked("island")) return;
            ActionsEl.Add(Ui.IconBtn("island", L.T("Visit island"), () =>
            {
                Close();
                app.VisitPlayer(e.PlayerId, e.Name);
            }, "green"));
        }

        static void AddList(VisualElement into, string title, List<object> ids, Func<string, string> name)
        {
            into.Add(Ui.Text(title, "se-section"));
            var row = Ui.Div("se-row", "se-wrap");
            if (ids == null || ids.Count == 0) row.Add(Ui.Text(L.T("none"), "se-small"));
            else
                foreach (var id in ids)
                {
                    var tag = Ui.Text(name(id as string), "se-tag");
                    tag.style.marginBottom = 4;
                    row.Add(tag);
                }
            into.Add(row);
        }
    }

    /// <summary>Stack of short notifications at the top of the screen.</summary>
    public sealed class Toasts : VisualElement
    {
        sealed class Item
        {
            public VisualElement El;
            public float Life;
        }

        readonly List<Item> _items = new List<Item>();

        public Toasts()
        {
            AddToClassList("se-toasts");
            pickingMode = PickingMode.Ignore;
        }

        public void Show(string icon, string text, float seconds = 3f)
        {
            if (_items.Count >= 4)
            {
                _items[0].El.RemoveFromHierarchy();
                _items.RemoveAt(0);
            }
            var el = Ui.Div("se-toast");
            el.pickingMode = PickingMode.Ignore;
            el.Add(new Icon(icon, 26));
            el.Add(new Label(text));
            Add(el);
            el.schedule.Execute(() => el.AddToClassList("shown")).StartingIn(16);
            _items.Add(new Item { El = el, Life = seconds });
        }

        public void Tick(float dt)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var it = _items[i];
                it.Life -= dt;
                if (it.Life < 0.3f) it.El.RemoveFromClassList("shown");
                if (it.Life <= 0)
                {
                    it.El.RemoveFromHierarchy();
                    _items.RemoveAt(i);
                }
            }
        }
    }
}
