using StormEngine.Core;
using StormEngine.Online;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Pick an island to visit: the built-in showcases and this week's top tournament players.</summary>
    public sealed class VisitListPopup : Popup
    {
        readonly StormApp _app;
        readonly VisualElement _players;

        public VisitListPopup(StormApp app)
            : base(L.T("VISIT ISLANDS"), L.T("See how other storm engineers decorate"), "eye")
        {
            _app = app;
            var c = app.Content;
            BodyEl.Add(Ui.Text(L.T("SHOWCASE ISLANDS"), "se-section"));
            foreach (var layout in Showcases.All(c))
            {
                var l = layout;
                BodyEl.Add(Row(l.Name, l.Title, l.Level, l.BestTournament, () =>
                {
                    Close();
                    _app.Visit(l);
                }));
            }
            BodyEl.Add(Ui.Text(L.T("TOURNAMENT PLAYERS"), "se-section"));
            _players = Ui.Div();
            BodyEl.Add(_players);
            Action(L.T("Close"), null, "white");
            Load();
        }

        async void Load()
        {
            Render(_app.Online.Cached(Boards.Weekly));
            if (!_app.Online.IsOnline) return;
            var page = await _app.Online.FetchBoard(Boards.Weekly, false);
            if (panel != null) Render(page);
        }

        void Render(BoardPage page)
        {
            _players.Clear();
            int shown = 0;
            if (page != null)
                foreach (var e in page.Entries)
                {
                    if (e.IsMe || string.IsNullOrEmpty(e.PlayerId)) continue;
                    var entry = e;
                    string title = null;
                    if (!string.IsNullOrEmpty(e.Summary) && JsonReader.TryParse(e.Summary, out var o)) title = JsonReader.Str(o, "ttl");
                    _players.Add(Row("#" + e.Rank + "  " + e.Name, title, 0, e.Score, () =>
                    {
                        Close();
                        _app.VisitPlayer(entry.PlayerId, entry.Name);
                    }));
                    if (++shown >= 10) break;
                }
            if (shown == 0)
                _players.Add(Ui.Text(_app.Online.IsOnline
                    ? L.T("Nobody has entered this week's tournament yet.")
                    : L.T("Connect to visit other players. Showcase islands work offline."), "se-small"));
        }

        VisualElement Row(string name, string title, int level, double best, System.Action visit)
        {
            var c = _app.Content;
            var row = Ui.Div("se-lbrow");
            row.Add(Ui.Text(level > 0 ? level.ToString() : "?", "se-rank"));
            var info = Ui.Div("se-grow");
            info.Add(Ui.Text(name, "se-h3"));
            int t = title != null ? c.CosmeticIndex(title) : -1;
            if (t >= 0) info.Add(Ui.Text(c.Cosmetics[t].Name, "se-lbtitle"));
            if (best > 0) info.Add(Ui.Text(L.F("Best tournament storm {0}", Num.Format(best)), "se-small"));
            row.Add(info);
            row.Add(Ui.IconBtn("eye", L.T("Visit"), visit, "blue", "small"));
            return row;
        }
    }
}
