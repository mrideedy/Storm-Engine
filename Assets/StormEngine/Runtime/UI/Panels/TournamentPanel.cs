using System.Collections.Generic;
using StormEngine.Core;
using StormEngine.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Weekly Tournament: enter with the engine you have grinded, under fixed rules, and climb the
    /// weekly, all-time and storm-class leaderboards. Only tournament storms are submitted.
    /// </summary>
    public sealed class TournamentPanel : PanelView
    {
        enum Board { Weekly, AllTime, Class }

        readonly Label _condName, _timeLeft, _best, _attempts, _forecast, _status, _name, _pending, _updated, _rival;
        readonly VisualElement _condLines, _rivalCard, _list, _classPicker;
        readonly Button _enter, _adAttempt;
        readonly Icon _statusIcon;
        readonly Button[] _boardButtons = new Button[3];
        readonly Dictionary<string, Button> _classButtons = new Dictionary<string, Button>();
        Board _board = Board.Weekly;
        string _class = "thunderstorm";
        BoardPage _shown;
        bool _loading;
        int _condShown = -2;
        float _forecastTimer;

        public TournamentPanel(StormApp app) : base(app, "tournament", L.T("Weekly Tournament"), "trophy", L.T("Tourney"))
        {
            // Hero: this week's condition, countdown, attempts and the big ENTER button.
            var hero = Ui.Div("se-card", "se-tourney");
            var top = Ui.Div("se-row");
            top.Add(new Icon("trophy", 44));
            var col = Ui.Div("se-grow");
            col.style.marginLeft = 8;
            col.Add(Ui.Text(L.T("THIS WEEK"), "se-tourney-kicker"));
            _condName = Ui.Text("", "se-h2");
            col.Add(_condName);
            _timeLeft = Ui.Text("", "se-small");
            col.Add(_timeLeft);
            top.Add(col);
            hero.Add(top);
            _condLines = Ui.Div("se-row", "se-wrap");
            _condLines.style.marginTop = 6;
            hero.Add(_condLines);

            var stats = Ui.Div("se-row", "se-tourney-stats");
            stats.Add(Stat(L.T("BEST THIS WEEK"), out _best));
            stats.Add(Stat(L.T("ATTEMPTS TODAY"), out _attempts));
            hero.Add(stats);

            _enter = Ui.Btn(L.T("ENTER TOURNAMENT"), () => App.EnterTournament(), "big", "green");
            _enter.AddToClassList("se-enterbtn");
            hero.Add(_enter);
            _forecast = Ui.Text("", "se-small", "se-center");
            hero.Add(_forecast);
            _adAttempt = Ui.IconBtn("play", L.T("+1 attempt"), () => App.WatchAd("ad_tourney"), "white", "small");
            _adAttempt.style.marginTop = 6;
            hero.Add(_adAttempt);
            Body.Add(hero);

            // Rules and rewards.
            Body.Add(Section(L.T("Rules")));
            var rules = Ui.Div("se-card");
            Rule(rules, "machines", L.T("Your machines, modules and research count exactly as you built them. Grind to get stronger!"));
            Rule(rules, "calendar", L.T("This week's condition always applies."));
            Rule(rules, "brush", L.F("Style bonuses count, but each is capped at x{0}.", Num.Fixed(1 + C.B.TournamentStyleCap, 2)));
            Rule(rules, "island", L.T("Decorations, Energy Surges and running boosts do not count."));
            Rule(rules, "boost", L.T("The storm starts instantly with fresh boosts: fire them during the storm to stack chains."));
            Rule(rules, "clock", L.F("{0} attempts per day (UTC), plus up to {1} more from ads. Your best storm counts.", C.B.TournamentAttemptsPerDay, C.B.TournamentAdAttemptsPerDay));
            Body.Add(rules);

            Body.Add(Section(L.T("Weekly rewards")));
            var rewards = Ui.Div("se-card", "se-row", "se-wrap", "se-rewards");
            var limits = C.B.TournamentRankLimits;
            var gems = C.B.TournamentRankGems;
            for (int i = 0; i < gems.Length; i++)
            {
                string label = i == 0 ? "#1" : i < limits.Length && limits[i] != int.MaxValue ? L.F("Top {0}", limits[i]) : L.T("Everyone");
                var cell = Ui.Div("se-reward");
                cell.Add(Ui.Text(label, "se-reward-rank"));
                var amount = Ui.Div("se-row");
                amount.Add(new Icon("gem", 18));
                amount.Add(Ui.Text(Num.Int(gems[i]), "se-reward-gems"));
                cell.Add(amount);
                rewards.Add(cell);
            }
            Body.Add(rewards);

            // Player status.
            Body.Add(Section(L.T("Leaderboards")));
            var status = Ui.Div("se-card", "se-row");
            _statusIcon = new Icon("offline", 30);
            status.Add(_statusIcon);
            var info = Ui.Div("se-grow");
            info.style.marginLeft = 8;
            _name = Ui.Text("", "se-h3");
            _status = Ui.Text("", "se-small");
            _pending = Ui.Text("", "se-small");
            info.Children(_name, _status, _pending);
            status.Add(info);
            status.Add(Ui.IconBtn("user", L.T("Name"), () => App.Ui.ShowNamePopup(), "white", "small"));
            Body.Add(status);

            var seg = Ui.Div("se-segment", "wide");
            string[] names = { L.T("This week"), L.T("All-time"), L.T("By class") };
            for (int i = 0; i < 3; i++)
            {
                var b = (Board)i;
                _boardButtons[i] = Ui.Btn(names[i], () => SelectBoard(b));
                seg.Add(_boardButtons[i]);
            }
            Body.Add(seg);

            _classPicker = Ui.Div("se-row", "se-wrap");
            _classPicker.style.marginBottom = 6;
            foreach (var p in C.Phenomena)
            {
                string id = p.Id;
                var b = Ui.Btn(p.Name, () => SelectClass(id), "white", "small");
                b.style.marginRight = 4;
                b.style.marginBottom = 4;
                _classButtons[id] = b;
                _classPicker.Add(b);
            }
            Body.Add(_classPicker);

            _rivalCard = Ui.Div("se-card", "glow", "se-row");
            _rivalCard.Add(new Icon("up", 28));
            _rival = Ui.Text("", "se-body", "se-grow");
            _rival.style.marginLeft = 8;
            _rivalCard.Add(_rival);
            Body.Add(_rivalCard);

            var listHead = Ui.Div("se-row");
            _updated = Ui.Text("", "se-small", "se-grow");
            listHead.Add(_updated);
            listHead.Add(Ui.IconBtn("refresh", L.T("Refresh"), () => Load(true), "white", "small"));
            listHead.style.marginBottom = 6;
            Body.Add(listHead);
            _list = Ui.Div();
            Body.Add(_list);
            SelectBoard(Board.Weekly);
        }

        static VisualElement Stat(string title, out Label value)
        {
            var box = Ui.Div("se-tourney-stat");
            box.Add(Ui.Text(title, "se-tourney-kicker"));
            value = Ui.Text("-", "se-h2");
            box.Add(value);
            return box;
        }

        static void Rule(VisualElement into, string icon, string text)
        {
            var row = Ui.Div("se-row", "se-rule");
            row.Add(new Icon(icon, 22));
            var t = Ui.Text(text, "se-small", "se-grow");
            t.style.marginLeft = 8;
            row.Add(t);
            into.Add(row);
        }

        string BoardId => _board == Board.AllTime ? Boards.AllTime : _board == Board.Weekly ? Boards.Weekly : Boards.Class(_class);

        void SelectBoard(Board b)
        {
            _board = b;
            for (int i = 0; i < 3; i++) _boardButtons[i].EnableInClassList("selected", (int)b == i);
            _classPicker.Show(b == Board.Class);
            Load(false);
        }

        void SelectClass(string id)
        {
            _class = id;
            Load(false);
        }

        public override void OnShow()
        {
            _forecastTimer = 0;
            Load(false);
        }

        async void Load(bool force)
        {
            string id = BoardId;
            Render(App.Online.Cached(id));
            if (_loading) return;
            _loading = true;
            try
            {
                var page = await App.Online.FetchBoard(id, force);
                if (id == BoardId) Render(page);
            }
            finally
            {
                _loading = false;
            }
        }

        void Render(BoardPage page)
        {
            _shown = page;
            _list.Clear();
            if (page != null && page.Error != null && page.Entries.Count == 0)
            {
                _list.Add(Ui.Text(page.Error, "se-body"));
                _rivalCard.Show(false);
                return;
            }
            if (page == null || page.Entries.Count == 0)
            {
                string msg = App.Online.State == OnlineState.Disabled
                    ? L.T("Global leaderboards are not connected yet. Your tournament records are still tracked.")
                    : App.Online.IsOnline ? L.T("No scores yet. Enter the tournament and claim the top spot!") : L.T("Offline. Standings appear once you connect.");
                _list.Add(Ui.Text(msg, "se-body"));
                _rivalCard.Show(false);
                return;
            }
            foreach (var e in page.Entries) _list.Add(EntryRow(e));
            if (page.Me != null && !page.Entries.Exists(x => x.IsMe))
            {
                _list.Add(Ui.Text("...", "se-small", "se-center"));
                _list.Add(EntryRow(page.Me));
            }
            if (page.Me != null && page.Rival != null)
            {
                _rivalCard.Show(true);
                _rival.Set(L.F("Next rival: {0} (#{1}) is {2} ahead. Grind your engine and beat them!", page.Rival.Name, page.Rival.Rank, Num.Format(page.Rival.Score - page.Me.Score)));
            }
            else if (page.Me != null && page.Me.Rank == 1)
            {
                _rivalCard.Show(true);
                _rival.Set(L.T("You hold the #1 spot. Defend it!"));
            }
            else _rivalCard.Show(false);
        }

        VisualElement EntryRow(BoardEntry e)
        {
            var row = Ui.Div("se-lbrow");
            row.EnableInClassList("me", e.IsMe);
            var rank = Ui.Text(e.Rank.ToString(), "se-rank");
            if (e.Rank <= 3) rank.AddToClassList("r" + e.Rank);
            row.Add(rank);
            var info = Ui.Div("se-grow");
            info.Add(Ui.Text(e.IsMe ? e.Name + "  " + L.T("(you)") : e.Name, "se-h3"));
            string title = SummaryTitle(e.Summary);
            if (title != null) info.Add(Ui.Text(title, "se-lbtitle"));
            string cls = SummaryClass(e.Summary);
            if (cls != null) info.Add(Ui.Text(cls, "se-small"));
            row.Add(info);
            row.Add(Ui.Text(Num.Format(e.Score), "se-h2"));
            row.RegisterCallback<ClickEvent>(_ => App.Ui.ShowBuildView(e));
            return row;
        }

        string SummaryTitle(string summary)
        {
            if (string.IsNullOrEmpty(summary) || !JsonReader.TryParse(summary, out var o)) return null;
            string id = JsonReader.Str(o, "ttl");
            int i = id != null ? C.CosmeticIndex(id) : -1;
            return i >= 0 && C.Cosmetics[i].Slot == CosmeticSlot.Title ? C.Cosmetics[i].Name : null;
        }

        string SummaryClass(string summary)
        {
            if (string.IsNullOrEmpty(summary) || !JsonReader.TryParse(summary, out var o)) return null;
            string id = JsonReader.Str(o, "cls");
            if (id == null) return null;
            int p = C.PhenoIndex(id);
            return p >= 0 ? C.Phenomena[p].Name : L.T("Squall");
        }

        public override void Refresh()
        {
            int w = G.WeeklyCondition;
            if (w != _condShown)
            {
                _condShown = w;
                _condName.Set(w >= 0 ? C.Weekly[w].Name : L.T("Open conditions"));
                _condLines.Clear();
                if (w >= 0)
                    foreach (var line in C.Weekly[w].Lines)
                    {
                        var tag = Ui.Text(line, "se-tag");
                        tag.AddToClassList(line.StartsWith("-") ? "bad" : "good");
                        tag.style.marginBottom = 4;
                        _condLines.Add(tag);
                    }
            }
            _timeLeft.Set(L.F("Ends in {0}", Num.Time(WeekClock.WeekEnd(G.Week) - G.NowUnix)) + "  ·  " + L.T("rewards are paid when the week closes"));
            var t = G.Tournament;
            _best.Set(t.Week == G.Week && t.Best > 0 ? Num.Format(t.Best) : "-");
            int left = G.TournamentAttemptsLeft;
            _attempts.Set(L.F("{0} left", left));

            bool storming = G.IsStorming;
            _enter.SetEnabledState(G.CanEnterTournament);
            if (G.IsTournamentRun) _enter.SetButton(L.T("TOURNAMENT STORM RUNNING"));
            else if (storming) _enter.SetButton(L.T("STORM RUNNING..."));
            else if (left <= 0) _enter.SetButton(L.T("NO ATTEMPTS LEFT"));
            else _enter.SetButton(L.T("ENTER TOURNAMENT"));
            _adAttempt.Show(left <= 0 && App.CanOfferAd("ad_tourney"));

            _forecastTimer -= 0.2f;
            if (_forecastTimer <= 0 && !storming)
            {
                _forecastTimer = 3f;
                var f = G.TournamentForecast();
                _forecast.Set(L.F("Forecast with every boost fired: {0}  ({1})", Num.Format(f.Score), StormSubmission.ClassName(C, f.ClassIndex)));
            }
            else if (storming) _forecast.Set("");

            var o = App.Online;
            _statusIcon.Kind = o.State == OnlineState.Online ? "signal" : "offline";
            _statusIcon.Tint = o.State == OnlineState.Online ? Palette.Green : (Color?)null;
            _name.Set(string.IsNullOrEmpty(o.PlayerName) ? L.T("Storm Chaser") : o.PlayerName);
            _status.Set(o.Status);
            _pending.Show(o.PendingCount > 0);
            _pending.Set(L.F("Tournament storms waiting to be verified and submitted: {0}", o.PendingCount));

            foreach (var kv in _classButtons) kv.Value.EnableInClassList("selected", kv.Key == _class);
            if (_shown != null && _shown.FetchedUnix > 0)
                _updated.Set(L.F("Updated {0} ago", Num.Time(G.NowUnix - _shown.FetchedUnix)));
            else
                _updated.Set(o.IsOnline ? L.T("Loading...") : L.T("Showing cached standings"));
        }

        public override bool HasAttention() => G.TournamentUnlocked && G.CanEnterTournament && G.Tournament.PlayedWeek != G.Week;
    }
}
