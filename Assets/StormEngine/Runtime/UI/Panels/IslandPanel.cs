using System.Collections.Generic;
using StormEngine.Core;
using StormEngine.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Island Decor: buy and place decorations (each new kind adds a small Energy bonus), grow the
    /// island with terrain expansions and fly over to other players' islands.
    /// </summary>
    public sealed class IslandPanel : PanelView
    {
        enum Tab { Land, Air, Water, Expand, Visit }

        readonly Label _count, _bonus, _limitedTime, _waterHint;
        readonly VisualElement _limitedCard, _limitedHost;
        readonly Button[] _tabButtons = new Button[5];
        readonly VisualElement _grid, _expansions, _visits;
        readonly List<DecorCard> _cards = new List<DecorCard>();
        readonly List<ExpansionRow> _expansionRows = new List<ExpansionRow>();
        readonly VisualElement _playerList;
        DecorCard _limited;
        int _limitedIndex = -2;
        Tab _tab;
        string _boardShown;

        sealed class DecorCard
        {
            public int Index;
            public VisualElement Root;
            public Label Owned;
            public Label Bonus;
            public Button Buy;
            public Button Place;
        }

        sealed class ExpansionRow
        {
            public int Index;
            public VisualElement Root;
            public Label Status;
            public Button Buy;
        }

        public IslandPanel(StormApp app) : base(app, "island", L.T("Island Decor"), "island", L.T("Island"))
        {
            var head = Ui.Div("se-card", "se-islandhead");
            var row = Ui.Div("se-row");
            row.Add(new Icon("island", 40));
            var col = Ui.Div("se-grow");
            col.style.marginLeft = 8;
            _count = Ui.Text("", "se-h3");
            _bonus = Ui.Text("", "se-small", "se-good");
            col.Children(_count, _bonus);
            row.Add(col);
            head.Add(row);
            var actions = Ui.Div("se-row");
            actions.style.marginTop = 8;
            var edit = Ui.IconBtn("edit", L.T("Edit layout"), () => App.Ui.EnterEditMode(), "white", "small");
            edit.style.flexGrow = 1;
            edit.style.marginRight = 6;
            actions.Add(edit);
            var visit = Ui.IconBtn("eye", L.T("Visit islands"), () => ShowTab(Tab.Visit), "blue", "small");
            visit.style.flexGrow = 1;
            actions.Add(visit);
            head.Add(actions);
            head.Add(Ui.Text(L.T("Every different decoration you own adds to your Energy income once. Other players can visit your island."), "se-small", "se-note"));
            Body.Add(head);

            _limitedCard = Ui.Div("se-card", "se-limited");
            var lhead = Ui.Div("se-row");
            lhead.Add(Ui.Text(L.T("THIS WEEK ONLY"), "se-offer-tag"));
            lhead.Add(Ui.Spacer());
            _limitedTime = Ui.Text("", "se-small");
            lhead.Add(_limitedTime);
            _limitedCard.Add(lhead);
            _limitedHost = Ui.Div();
            _limitedCard.Add(_limitedHost);
            Body.Add(_limitedCard);

            var seg = Ui.Div("se-segment", "wide");
            string[] names = { L.T("Land"), L.T("Air"), L.T("Water"), L.T("Expand"), L.T("Visit") };
            for (int i = 0; i < names.Length; i++)
            {
                var t = (Tab)i;
                _tabButtons[i] = Ui.Btn(names[i], () => ShowTab(t));
                seg.Add(_tabButtons[i]);
            }
            Body.Add(seg);

            _waterHint = Ui.Text("", "se-small", "se-note");
            Body.Add(_waterHint);
            _grid = Ui.Div("se-stylegrid");
            Body.Add(_grid);

            _expansions = Ui.Div();
            for (int e = 0; e < C.Expansions.Length; e++)
            {
                var r = BuildExpansion(e);
                _expansionRows.Add(r);
                _expansions.Add(r.Root);
            }
            Body.Add(_expansions);

            _visits = Ui.Div();
            _visits.Add(Section(L.T("Showcase islands")));
            foreach (var layout in Showcases.All(C)) _visits.Add(VisitRow(layout.Name, layout.Title, layout.Level, layout.BestTournament, () => App.Visit(layout)));
            _visits.Add(Section(L.T("Tournament players")));
            _playerList = Ui.Div();
            _visits.Add(_playerList);
            Body.Add(_visits);

            ShowTab(Tab.Land);
        }

        // ------------------------------------------------------------ builders

        DecorCard BuildCard(int i, bool wide = false)
        {
            var def = C.Decorations[i];
            var card = new DecorCard { Index = i, Root = Ui.Div("se-card", wide ? "se-decorwide" : "se-stylecard") };
            if (def.Exclusive) card.Root.Add(Ui.Text(L.T("EXCLUSIVE"), "se-ribbon", "gold"));
            else if (def.Limited) card.Root.Add(Ui.Text(L.T("LIMITED"), "se-ribbon", "red"));
            var sw = new DecorSwatch(def);
            card.Root.Add(sw);
            var info = wide ? Ui.Div("se-grow") : card.Root;
            if (wide)
            {
                info.style.marginLeft = 10;
                card.Root.Add(info);
            }
            info.Add(Ui.Text(def.Name, "se-stylecard-name"));
            if (wide) info.Add(Ui.Text(def.Desc, "se-small"));
            card.Bonus = Ui.Text("", "se-stylecard-bonus");
            info.Add(card.Bonus);
            card.Owned = Ui.Text("", "se-stylecard-status");
            info.Add(card.Owned);
            var buttons = Ui.Div("se-row", "se-decorbtns");
            card.Buy = Ui.Btn("", () => App.BuyDecoration(i), "green", "se-stylecard-btn");
            card.Place = Ui.Btn(L.T("Place"), () => App.PlaceDecoration(def.Id), "white", "se-stylecard-btn");
            buttons.Children(card.Buy, card.Place);
            info.Add(buttons);
            return card;
        }

        ExpansionRow BuildExpansion(int e)
        {
            var def = C.Expansions[e];
            var row = new ExpansionRow { Index = e, Root = Ui.Div("se-card", "se-row") };
            var box = Ui.Div("se-iconbox");
            box.Add(new Icon("island", 34));
            row.Root.Add(box);
            var info = Ui.Div("se-grow");
            info.Add(Ui.Text(def.Name, "se-h3"));
            info.Add(Ui.Text(def.Desc, "se-small"));
            row.Status = Ui.Text("", "se-small");
            info.Add(row.Status);
            row.Root.Add(info);
            row.Buy = Ui.Btn(L.F("{0} Gems", Num.Int(def.CostGems)), () => App.BuyExpansion(e), "green", "small");
            row.Root.Add(row.Buy);
            return row;
        }

        VisualElement VisitRow(string name, string title, int level, double best, System.Action visit)
        {
            var row = Ui.Div("se-lbrow");
            var badge = Ui.Text(level > 0 ? level.ToString() : "?", "se-rank");
            row.Add(badge);
            var info = Ui.Div("se-grow");
            info.Add(Ui.Text(name, "se-h3"));
            int t = title != null ? C.CosmeticIndex(title) : -1;
            if (t >= 0) info.Add(Ui.Text(C.Cosmetics[t].Name, "se-lbtitle"));
            if (best > 0) info.Add(Ui.Text(L.F("Best tournament storm {0}", Num.Format(best)), "se-small"));
            row.Add(info);
            row.Add(Ui.IconBtn("eye", L.T("Visit"), () =>
            {
                App.Sfx?.Click();
                visit();
            }, "blue", "small"));
            return row;
        }

        // ------------------------------------------------------------ tabs

        void ShowTab(Tab t)
        {
            _tab = t;
            for (int i = 0; i < _tabButtons.Length; i++) _tabButtons[i].EnableInClassList("selected", (int)t == i);
            bool shop = t == Tab.Land || t == Tab.Air || t == Tab.Water;
            _grid.Show(shop);
            _waterHint.Show(t == Tab.Water);
            _expansions.Show(t == Tab.Expand);
            _visits.Show(t == Tab.Visit);
            if (shop)
            {
                var kind = t == Tab.Land ? DecorKind.Land : t == Tab.Air ? DecorKind.Air : DecorKind.Water;
                _grid.Clear();
                _cards.Clear();
                for (int i = 0; i < C.Decorations.Length; i++)
                {
                    var def = C.Decorations[i];
                    if (def.Kind != kind) continue;
                    // Limited items only appear while featured (or once owned); bundle items once owned.
                    if ((def.Limited || def.Bundle != null) && G.DecorCopies(def.Id) <= 0) continue;
                    var card = BuildCard(i);
                    _cards.Add(card);
                    _grid.Add(card.Root);
                }
            }
            if (t == Tab.Visit) LoadPlayers();
            Refresh();
        }

        async void LoadPlayers()
        {
            RenderPlayers(App.Online.Cached(Boards.Weekly));
            if (!App.Online.IsOnline) return;
            var page = await App.Online.FetchBoard(Boards.Weekly, false);
            if (_tab == Tab.Visit) RenderPlayers(page);
        }

        void RenderPlayers(BoardPage page)
        {
            string sig = page == null ? "none" : page.Id + page.FetchedUnix;
            if (sig == _boardShown) return;
            _boardShown = sig;
            _playerList.Clear();
            if (page == null || page.Entries.Count == 0)
            {
                _playerList.Add(Ui.Text(App.Online.IsOnline
                    ? L.T("No tournament scores yet this week. Enter the tournament and others will visit you!")
                    : L.T("Connect to visit other players' islands. Showcase islands work offline."), "se-small", "se-note"));
                return;
            }
            int shown = 0;
            foreach (var e in page.Entries)
            {
                if (e.IsMe || string.IsNullOrEmpty(e.PlayerId)) continue;
                var entry = e;
                string title = null;
                if (!string.IsNullOrEmpty(e.Summary) && JsonReader.TryParse(e.Summary, out var o)) title = JsonReader.Str(o, "ttl");
                _playerList.Add(VisitRow("#" + e.Rank + "  " + e.Name, title, 0, e.Score, () => App.VisitPlayer(entry.PlayerId, entry.Name)));
                if (++shown >= 15) break;
            }
        }

        // ------------------------------------------------------------ refresh

        public override void OnShow()
        {
            _limitedIndex = -2;
            ShowTab(_tab);
        }

        public override void Refresh()
        {
            int kinds = G.UniqueDecorations;
            _count.Set(L.F("{0}/{1} placed", G.Placed.Count, G.DecorCapacity) + "  ·  " + L.F("Kinds owned: {0}", kinds));
            _bonus.Set(G.Build.DecorBonus > 0 ? L.F("Collection bonus: +{0} Energy income", Num.Percent(G.Build.DecorBonus)) : L.T("Buy your first decoration for an Energy bonus"));

            int featured = C.FeaturedDecoration(G.Week);
            _limitedCard.Show(featured >= 0 && G.Level >= C.Decorations[featured].UnlockLevel && G.IsFeatureUnlocked("island"));
            if (featured != _limitedIndex)
            {
                _limitedIndex = featured;
                _limitedHost.Clear();
                _limited = featured >= 0 ? BuildCard(featured, true) : null;
                if (_limited != null) _limitedHost.Add(_limited.Root);
            }
            if (_limited != null)
            {
                _limitedTime.Set(L.F("Leaves in {0}", Num.Time(WeekClock.WeekEnd(G.Week) - G.NowUnix)));
                RefreshCard(_limited);
            }

            if (_tab == Tab.Water)
            {
                int ponds = G.PondCount;
                _waterHint.Set(ponds == 0
                    ? L.T("Water decorations float on a Pond (see Land). Each pond holds three.")
                    : L.F("Ponds placed: {0}", ponds) + "  ·  " + L.F("Free water spots: {0}", Mathf.Max(0, G.WaterSlotsFree)));
            }
            foreach (var card in _cards) RefreshCard(card);
            if (_tab == Tab.Expand) RefreshExpansions();
            if (_tab == Tab.Visit) RenderPlayers(App.Online.Cached(Boards.Weekly));
        }

        void RefreshCard(DecorCard card)
        {
            var def = C.Decorations[card.Index];
            int owned = G.DecorCopies(def.Id);
            int stored = G.StoredCopies(def.Id);
            card.Bonus.Set(owned > 0 ? L.F("Bonus collected  +{0}", Num.Percent(def.Bonus)) : L.F("+{0} Energy", Num.Percent(def.Bonus)));
            card.Bonus.EnableInClassList("se-good", owned > 0);
            string status = owned > 0 ? L.F("{0}/{1} owned", owned, def.MaxCopies) + (stored > 0 ? "  ·  " + L.F("{0} stored", stored) : "")
                : def.Kind == DecorKind.Air ? L.T("Flies above the island") : def.Kind == DecorKind.Water ? L.T("Floats on a pond") : "";
            bool levelOk = G.Level >= def.UnlockLevel;
            if (!levelOk) status = L.F("Engine Lv {0}", def.UnlockLevel);
            card.Owned.Set(status);
            card.Root.EnableInClassList("locked", !levelOk);
            card.Place.Show(stored > 0 && !App.World.IsVisiting);
            bool sold = G.IsDecorationOnSale(card.Index);
            bool maxed = owned >= def.MaxCopies;
            card.Buy.Show(sold && !maxed && levelOk);
            if (sold && !maxed)
            {
                card.Buy.SetButton(def.CostGems > 0 ? L.F("{0} Gems", Num.Int(def.CostGems)) : L.F("{0} Cores", Num.Int(def.CostCores)));
                card.Buy.SetEnabledState(G.CanBuyDecoration(card.Index));
            }
        }

        void RefreshExpansions()
        {
            foreach (var row in _expansionRows)
            {
                var def = C.Expansions[row.Index];
                bool owned = G.IsExpansionOwned(row.Index);
                bool prevOk = row.Index == 0 || G.IsExpansionOwned(row.Index - 1);
                row.Buy.Show(!owned);
                row.Buy.SetEnabledState(G.CanBuyExpansion(row.Index));
                if (owned) row.Status.Set(L.T("Owned"));
                else if (G.Level < def.UnlockLevel) row.Status.Set(L.F("Unlocks at Engine Level {0}", def.UnlockLevel));
                else if (!prevOk) row.Status.Set(L.F("Build {0} first", C.Expansions[row.Index - 1].Name));
                else row.Status.Set(L.F("Island radius {0}", Num.Fixed(def.Radius, 1)) + "  ·  " + L.F("+{0} decorations", def.Capacity));
                row.Root.EnableInClassList("dim", !owned && (G.Level < def.UnlockLevel || !prevOk));
            }
        }

        public override bool HasAttention()
        {
            foreach (var d in C.Decorations)
                if (G.StoredCopies(d.Id) > 0 && G.Placed.Count < G.DecorCapacity) return true;
            return false;
        }
    }
}
