using System;
using StormEngine.Core;
using StormEngine.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Title screen over the live island: PLAY to grind, TOURNAMENT to compete.</summary>
    public sealed class HomeScreen : VisualElement
    {
        readonly StormApp _app;
        readonly Label _level, _tourneySub, _tourneyInfo, _playSub, _dailySub;
        readonly VisualElement _logo, _card;
        readonly Button _tourney, _daily;
        readonly Icon _tourneyLock;
        readonly VisualElement _dailyDot;

        public event Action DailyClicked;
        public event Action PlayClicked;
        public event Action TournamentClicked;
        public event Action PediaClicked;
        public event Action VisitClicked;
        public event Action SettingsClicked;

        public HomeScreen(StormApp app)
        {
            _app = app;
            AddToClassList("se-home");
            pickingMode = PickingMode.Ignore;

            var top = Ui.Div("se-home-top");
            top.pickingMode = PickingMode.Ignore;
            // The logo (Tools/marketing/make_logo.py); the plain title only if the art is missing.
            var logoArt = Resources.Load<Texture2D>("StormEngine/logo");
            _logo = logoArt != null ? Ui.Div("se-home-logo") : Ui.Text(L.T("STORM ENGINE"), "se-home-title");
            if (logoArt != null) _logo.style.backgroundImage = new StyleBackground(logoArt);
            _logo.pickingMode = PickingMode.Ignore;
            top.Add(_logo);
            _level = Ui.Text("", "se-home-sub");
            top.Add(_level);
            Add(top);

            var spacer = Ui.Spacer();
            Add(spacer);

            var card = _card = Ui.Div("se-home-card");
            _daily = new Button(() => DailyClicked?.Invoke()) { focusable = false };
            _daily.AddToClassList("se-homedaily");
            _daily.Add(new Icon("gift", 36));
            var dText = Ui.Div("se-homebtn-text");
            dText.Add(Ui.Text(L.T("DAILY REWARDS"), "se-homedaily-title"));
            _dailySub = Ui.Text("", "se-homedaily-sub");
            dText.Add(_dailySub);
            _daily.Add(dText);
            _dailyDot = Ui.Div("se-badge");
            _dailyDot.pickingMode = PickingMode.Ignore;
            _daily.Add(_dailyDot);
            card.Add(_daily);

            var play = new Button(() => PlayClicked?.Invoke()) { focusable = false };
            play.AddToClassList("se-btn");
            play.AddToClassList("green");
            play.AddToClassList("se-homebtn");
            play.Add(new Icon("play", 40));
            var playText = Ui.Div("se-homebtn-text");
            playText.Add(Ui.Text(L.T("PLAY"), "se-homebtn-title"));
            _playSub = Ui.Text(L.T("Grind your storm engine"), "se-homebtn-sub");
            playText.Add(_playSub);
            play.Add(playText);
            card.Add(play);

            _tourney = new Button(() => TournamentClicked?.Invoke()) { focusable = false };
            _tourney.AddToClassList("se-btn");
            _tourney.AddToClassList("se-homebtn");
            _tourney.Add(new Icon("trophy", 40));
            var tText = Ui.Div("se-homebtn-text");
            tText.Add(Ui.Text(L.T("TOURNAMENT"), "se-homebtn-title"));
            _tourneySub = Ui.Text("", "se-homebtn-sub");
            tText.Add(_tourneySub);
            _tourneyInfo = Ui.Text("", "se-homebtn-sub");
            tText.Add(_tourneyInfo);
            _tourney.Add(tText);
            _tourneyLock = new Icon("lock", 26);
            _tourneyLock.AddToClassList("se-homebtn-lock");
            _tourney.Add(_tourneyLock);
            card.Add(_tourney);

            var small = Ui.Div("se-row", "se-home-small");
            small.Add(Small("book", L.T("Stormpedia"), () => PediaClicked?.Invoke()));
            small.Add(Small("eye", L.T("Visit islands"), () => VisitClicked?.Invoke()));
            small.Add(Small("settings", L.T("Settings"), () => SettingsClicked?.Invoke()));
            card.Add(small);
            Add(card);
        }

        /// <summary>
        /// The menu's part in the launch intro (seconds in; negative when it is over): the logo lands
        /// with the last strike, the player line follows, and the buttons rise in as the camera arrives.
        /// </summary>
        public void IntroReveal(float t)
        {
            if (t < 0f)
            {
                Reveal(_logo, 1f, 1f, 0f);
                Reveal(_level, 1f, 1f, 0f);
                Reveal(_card, 1f, 1f, 0f);
                return;
            }
            // A beat after the strike, once its flash has passed, so the logo lands clearly.
            float logo = Mathf.Clamp01((t - WorldView.IntroTitleAt - 0.08f) / 0.45f);
            Reveal(_logo, Mathf.Clamp01(logo * 4f), Mathf.LerpUnclamped(1.4f, 1f, BackOut(logo)), 0f);
            Reveal(_level, Mathf.Clamp01((t - WorldView.IntroTitleAt - 0.55f) / 0.4f), 1f, 0f);
            float card = Mathf.Clamp01((t - (WorldView.IntroLength - 1.15f)) / 0.85f);
            float rise = 1f - (1f - card) * (1f - card) * (1f - card);
            Reveal(_card, card, 1f, (1f - rise) * 70f);
        }

        /// <summary>Ease-out with a small overshoot: the logo lands a touch small, then settles.</summary>
        static float BackOut(float t)
        {
            const float c = 1.7f;
            float u = t - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        static void Reveal(VisualElement e, float opacity, float scale, float dy)
        {
            e.style.opacity = opacity;
            e.style.scale = new Scale(new Vector3(scale, scale, 1f));
            e.style.translate = new Translate(0, dy);
        }

        static Button Small(string icon, string text, Action onClick)
        {
            var b = Ui.IconBtn(icon, text, onClick, "white", "small");
            b.style.flexGrow = 1;
            b.style.marginLeft = 3;
            b.style.marginRight = 3;
            return b;
        }

        public void Refresh()
        {
            var g = _app.Game;
            string name = string.IsNullOrEmpty(_app.Online.PlayerName) ? L.T("Storm Chaser") : _app.Online.PlayerName;
            _level.Set(name + "  ·  " + L.F("Engine Level {0}", g.Level));
            _playSub.Set(g.Stats.Storms == 0 ? L.T("Build your first storm engine") : L.F("{0} Energy /s", Num.Format(g.EnergyPerSecond)));
            _daily.Show(g.DailyUnlocked);
            if (g.DailyUnlocked)
            {
                int claimable = g.DailyClaimable;
                _dailyDot.Show(claimable > 0);
                if (g.LoginRewardReady) _dailySub.Set(L.F("Your Day {0} gift is ready!", g.TodayStreakDay));
                else if (claimable > 0) _dailySub.Set(L.T("Mission rewards to collect!"));
                else _dailySub.Set(L.F("{0}-day streak", g.Daily.Streak) + "  ·  " + L.F("missions {0}/{1}", g.MissionsClaimed, g.MissionsOffered) + "  ·  " + L.F("new day in {0}", Num.Time(g.SecondsToNewDay)));
            }
            bool unlocked = g.TournamentUnlocked;
            _tourney.EnableInClassList("locked", !unlocked);
            _tourneyLock.Show(!unlocked);
            _tourneyInfo.Show(unlocked);
            if (!unlocked) _tourneySub.Set(L.F("Unlocks at Engine Level {0}", g.FeatureLevel("tournament")));
            else
            {
                _tourneySub.Set(L.F("This week: {0}", g.WeeklyCondition >= 0 ? g.C.Weekly[g.WeeklyCondition].Name : L.T("open conditions")));
                int left = g.TournamentAttemptsLeft;
                _tourneyInfo.Set(L.F("Attempts left today: {0}", left) + "  ·  " + L.F("ends in {0}", Num.Time(WeekClock.WeekEnd(g.Week) - g.NowUnix)));
            }
        }
    }

    /// <summary>Shown while looking at someone else's island.</summary>
    public sealed class VisitBanner : VisualElement
    {
        readonly Label _name, _info;

        public event Action BackClicked;

        public VisitBanner()
        {
            AddToClassList("se-visitbanner");
            var card = Ui.Div("se-visitcard");
            card.Add(new Icon("eye", 30));
            var col = Ui.Div("se-grow");
            col.style.marginLeft = 8;
            col.Add(Ui.Text(L.T("VISITING"), "se-tourney-kicker"));
            _name = Ui.Text("", "se-visit-name");
            _info = Ui.Text("", "se-visit-info");
            col.Children(_name, _info);
            card.Add(col);
            card.Add(Ui.IconBtn("home", L.T("Back home"), () => BackClicked?.Invoke(), "green", "small"));
            Add(card);
        }

        public void Set(IslandLayout l, Content c)
        {
            if (l == null) return;
            _name.Set(L.F("{0}'s island", l.Name));
            int t = l.Title != null ? c.CosmeticIndex(l.Title) : -1;
            string info = L.F("Engine Level {0}", l.Level) + (t >= 0 ? "  ·  " + c.Cosmetics[t].Name : "") + "  ·  " + L.F("{0} decorations", l.Decor.Count);
            if (l.BestTournament > 0) info += "  ·  " + L.F("best tournament storm {0}", Num.Format(l.BestTournament));
            _info.Set(info);
        }
    }

    /// <summary>Bottom bar while rearranging decorations.</summary>
    public sealed class EditBar : VisualElement
    {
        readonly StormApp _app;
        readonly Label _hint;
        readonly Button _rotate, _store;

        public int Selected { get; private set; } = -1;
        public event Action DoneClicked;

        public EditBar(StormApp app)
        {
            _app = app;
            AddToClassList("se-editbar");
            var card = Ui.Div("se-editcard");
            _hint = Ui.Text("", "se-edit-hint");
            card.Add(_hint);
            var row = Ui.Div("se-row");
            _rotate = Ui.IconBtn("rotate", L.T("Rotate"), () =>
            {
                if (Selected < 0) return;
                _app.World.RotatePlacement(Selected, 45f);
                _app.Sfx?.Click();
            }, "white", "small");
            _store = Ui.IconBtn("close", L.T("Store"), () =>
            {
                if (Selected < 0) return;
                int index = Selected;
                Select(-1);
                _app.StoreDecoration(index);
            }, "white", "small");
            var done = Ui.IconBtn("check", L.T("Done"), () => DoneClicked?.Invoke(), "green", "small");
            row.Children(_rotate, _store, Ui.Spacer(), done);
            card.Add(row);
            Add(card);
            Select(-1);
        }

        public void Select(int index)
        {
            var g = _app.Game;
            Selected = index >= 0 && index < g.Placed.Count ? index : -1;
            _app.World?.SelectDecoration(Selected);
            _rotate.SetEnabledState(Selected >= 0);
            _store.SetEnabledState(Selected >= 0);
            if (Selected < 0)
            {
                _hint.Set(g.Placed.Count == 0 ? L.T("Your island has no decorations yet. Buy some in Island Decor.") : L.T("Tap a decoration to select it."));
                return;
            }
            int d = _app.Content.DecorationIndex(g.Placed[Selected].Id);
            var def = d >= 0 ? _app.Content.Decorations[d] : null;
            string name = def != null ? def.Name : L.T("Decoration");
            _hint.Set(def != null && def.Kind == DecorKind.Water
                ? L.F("{0}: water decorations move with their pond.", name)
                : L.F("{0}: tap the ground to move it.", name));
        }
    }
}
