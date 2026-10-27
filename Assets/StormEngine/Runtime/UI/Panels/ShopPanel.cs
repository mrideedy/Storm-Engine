using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// The shop: free rewards (rewarded ads), ads-only bundles, style items that boost a stat
    /// (bought with Gems or earned) and Gem packs (in-app purchases).
    /// </summary>
    public sealed class ShopPanel : PanelView
    {
        enum Tab { Featured, Styles, Gems }

        static readonly CosmeticSlot[] Slots = { CosmeticSlot.Skin, CosmeticSlot.Cloud, CosmeticSlot.Bolt, CosmeticSlot.World, CosmeticSlot.Title };
        static readonly string[] SlotLabels = { L.N("Skins"), L.N("Clouds"), L.N("Bolts"), L.N("Worlds"), L.N("Titles") };
        static readonly string[] FreeAds = { "ad_gems", "ad_surge" };

        readonly Button[] _sectionButtons = new Button[3];
        readonly VisualElement[] _sections = new VisualElement[3];
        readonly Label _gemBalance;
        readonly Button[] _slotButtons = new Button[Slots.Length];
        readonly VisualElement _grid, _exclusiveGrid;
        readonly Label _slotHint, _storeNote;
        readonly List<StyleCard> _cards = new List<StyleCard>();
        readonly List<StyleCard> _exclusiveCards = new List<StyleCard>();
        readonly List<AdRow> _adRows = new List<AdRow>();
        readonly List<BundleCard> _bundles = new List<BundleCard>();
        readonly List<ProductRow> _products = new List<ProductRow>();
        readonly VisualElement _starter;
        Tab _section;
        int _slot;

        sealed class StyleCard
        {
            public int Index;
            public VisualElement Root;
            public Label Status;
            public Button Action;
        }

        sealed class AdRow
        {
            public string Id;
            public VisualElement Root;
            public Label Left;
            public Button Watch;
        }

        sealed class BundleCard
        {
            public int Index;
            public VisualElement Root;
            public Bar Bar;
            public Label Progress;
            public Button Watch;
            public Button Equip;
        }

        sealed class ProductRow
        {
            public int Index;
            public VisualElement Root;
            public Button Buy;
        }

        public ShopPanel(StormApp app) : base(app, "shop", L.T("Shop"), "shop", L.T("Shop"))
        {
            var balance = Ui.Div("se-card", "se-row", "se-gembalance");
            balance.Add(new Icon("gem", 34));
            var col = Ui.Div("se-grow");
            col.style.marginLeft = 8;
            col.Add(Ui.Text(L.T("YOUR GEMS"), "se-small"));
            _gemBalance = Ui.Text("0", "se-h2");
            col.Add(_gemBalance);
            balance.Add(col);
            balance.Add(Ui.Btn(L.T("Get Gems"), () => Show(Tab.Gems), "small"));
            Body.Add(balance);

            var seg = Ui.Div("se-segment", "wide");
            string[] names = { L.T("Featured"), L.T("Styles"), L.T("Gems") };
            for (int i = 0; i < 3; i++)
            {
                var section = (Tab)i;
                _sectionButtons[i] = Ui.Btn(names[i], () => Show(section));
                seg.Add(_sectionButtons[i]);
            }
            Body.Add(seg);

            // ---- Featured
            var featured = Ui.Div();
            featured.Add(Section(L.T("Free rewards")));
            var free = Ui.Div("se-card");
            foreach (var id in FreeAds) free.Add(BuildAdRow(id));
            featured.Add(free);

            _starter = BuildStarter();
            if (_starter != null) featured.Add(_starter);

            featured.Add(Section(L.T("Ad bundles: complete environments")));
            featured.Add(Ui.Text(L.T("Only available by watching rewarded ads. Each bundle unlocks a full matching set at once."), "se-small", "se-note"));
            for (int b = 0; b < C.Bundles.Length; b++)
            {
                var card = BuildBundle(b);
                _bundles.Add(card);
                featured.Add(card.Root);
            }

            featured.Add(Section(L.T("Exclusives")));
            featured.Add(Ui.Text(L.T("Gem-only showpieces with the strongest bonuses."), "se-small", "se-note"));
            _exclusiveGrid = Ui.Div("se-stylegrid");
            for (int i = 0; i < C.Cosmetics.Length; i++)
            {
                if (!C.Cosmetics[i].Exclusive) continue;
                var card = BuildCard(i);
                _exclusiveCards.Add(card);
                _exclusiveGrid.Add(card.Root);
            }
            featured.Add(_exclusiveGrid);
            _sections[0] = featured;

            // ---- Styles
            var styles = Ui.Div();
            var slots = Ui.Div("se-segment", "wide");
            for (int i = 0; i < Slots.Length; i++)
            {
                int slot = i;
                _slotButtons[i] = Ui.Btn(L.T(SlotLabels[i]), () => SetSlot(slot));
                slots.Add(_slotButtons[i]);
            }
            styles.Add(slots);
            _slotHint = Ui.Text("", "se-small", "se-note");
            styles.Add(_slotHint);
            _grid = Ui.Div("se-stylegrid");
            styles.Add(_grid);
            _sections[1] = styles;

            // ---- Gems
            var gems = Ui.Div();
            var freeGems = Ui.Div("se-card");
            freeGems.Add(BuildAdRow("ad_gems"));
            gems.Add(freeGems);
            gems.Add(Section(L.T("Gem packs")));
            for (int p = 0; p < C.Products.Length; p++)
            {
                if (C.Products[p].Gems <= 0 || C.Products[p].OneTime) continue;
                var row = BuildProduct(p);
                _products.Add(row);
                gems.Add(row.Root);
            }
            var earn = Ui.Text(L.F("Gems are also earned by playing: +{0} every Engine Level, +{1} for each new phenomenon, daily rewards, goal rewards and weekly tournament prizes.", C.B.GemsPerLevel, C.B.GemsPerDiscovery), "se-small", "se-note");
            gems.Add(earn);
            _storeNote = Ui.Text("", "se-small", "se-note");
            gems.Add(_storeNote);
            _sections[2] = gems;

            foreach (var s in _sections) Body.Add(s);
            SetSlot(0);
            Show(Tab.Featured);
        }

        // ------------------------------------------------------------ builders

        VisualElement BuildAdRow(string id)
        {
            int a = C.AdIndex(id);
            var def = C.AdPlacements[a];
            var row = new AdRow { Id = id, Root = Ui.Div("se-row", "se-adrow") };
            var box = Ui.Div("se-iconbox", "small");
            box.Add(new Icon(def.Icon, 28));
            row.Root.Add(box);
            var info = Ui.Div("se-grow");
            info.Add(Ui.Text(def.Name, "se-h3"));
            info.Add(Ui.Text(def.Desc, "se-small"));
            row.Left = Ui.Text("", "se-small");
            info.Add(row.Left);
            row.Root.Add(info);
            row.Watch = Ui.IconBtn("play", L.T("Watch"), () => App.WatchAd(id), "green", "small");
            row.Root.Add(row.Watch);
            _adRows.Add(row);
            return row.Root;
        }

        VisualElement BuildStarter()
        {
            int p = -1;
            for (int i = 0; i < C.Products.Length; i++)
                if (C.Products[i].OneTime) p = i;
            if (p < 0) return null;
            var def = C.Products[p];
            var card = Ui.Div("se-card", "se-offer");
            var head = Ui.Div("se-row");
            head.Add(new Icon("gem", 40));
            var col = Ui.Div("se-grow");
            col.style.marginLeft = 8;
            col.Add(Ui.Text(def.Tag ?? L.T("OFFER"), "se-offer-tag"));
            col.Add(Ui.Text(def.Name, "se-h2"));
            col.Add(Ui.Text(def.Desc, "se-small"));
            head.Add(col);
            card.Add(head);
            var buy = Ui.Btn(App.Store.Price(def), () => App.BuyProduct(p), "green");
            buy.style.marginTop = 8;
            card.Add(buy);
            var row = new ProductRow { Index = p, Root = card, Buy = buy };
            _products.Add(row);
            return card;
        }

        BundleCard BuildBundle(int b)
        {
            var def = C.Bundles[b];
            var card = new BundleCard { Index = b, Root = Ui.Div("se-card", "se-bundle") };
            var head = Ui.Div("se-bundle-head");
            head.style.backgroundColor = Palette.Hex(def.Primary);
            head.Add(Ui.Text(def.Name.ToUpperInvariant(), "se-bundle-title"));
            head.Add(Ui.Text(L.T("ADS ONLY"), "se-bundle-tag"));
            card.Root.Add(head);
            var items = Ui.Div("se-row", "se-bundle-items");
            foreach (var id in def.Cosmetics)
            {
                int i = C.CosmeticIndex(id);
                if (i < 0 || C.Cosmetics[i].Slot == CosmeticSlot.Title) continue;
                var sw = new Swatch(C.Cosmetics[i]);
                sw.AddToClassList("small");
                items.Add(sw);
            }
            foreach (var id in def.Decorations)
            {
                int d = C.DecorationIndex(id);
                if (d < 0) continue;
                var sw = new DecorSwatch(C.Decorations[d]);
                sw.AddToClassList("small");
                items.Add(sw);
            }
            card.Root.Add(items);
            card.Root.Add(Ui.Text(def.Desc, "se-small"));
            var bonus = BundleBonus(def);
            if (bonus != null)
            {
                var b2 = Ui.Text(bonus, "se-small", "se-good");
                b2.style.marginTop = 2;
                card.Root.Add(b2);
            }
            var progress = Ui.Div("se-row");
            progress.style.marginTop = 8;
            card.Bar = new Bar(Palette.Hex(def.Primary).Lighten(0.2f), 14);
            progress.Add(card.Bar);
            card.Progress = Ui.Text("", "se-bundle-count");
            progress.Add(card.Progress);
            card.Root.Add(progress);
            var actions = Ui.Div("se-row");
            actions.style.marginTop = 8;
            card.Watch = Ui.IconBtn("play", L.T("Watch ad  +1"), () => App.WatchAd("ad_bundle", def.Id), "green");
            card.Watch.style.flexGrow = 1;
            actions.Add(card.Watch);
            card.Equip = Ui.Btn(L.T("Equip the set"), () =>
            {
                foreach (var id in def.Cosmetics)
                {
                    int i = C.CosmeticIndex(id);
                    if (i >= 0) G.EquipCosmetic(i);
                }
                App.Sfx?.Equip();
                App.SaveNow(false);
            }, "white");
            card.Equip.style.flexGrow = 1;
            actions.Add(card.Equip);
            card.Root.Add(actions);
            return card;
        }

        string BundleBonus(BundleDef def)
        {
            var parts = new List<string>();
            foreach (var id in def.Cosmetics)
            {
                int i = C.CosmeticIndex(id);
                if (i < 0) continue;
                string b = StyleText.Bonus(C, C.Cosmetics[i]);
                if (b != null && C.Cosmetics[i].Slot != CosmeticSlot.Title) parts.Add(b);
            }
            return parts.Count > 0 ? L.F("Bonuses: {0}", string.Join(", ", parts)) : null;
        }

        ProductRow BuildProduct(int p)
        {
            var def = C.Products[p];
            var row = new ProductRow { Index = p, Root = Ui.Div("se-card", "se-row", "se-product") };
            var box = Ui.Div("se-iconbox");
            box.Add(new Icon("gem", 24 + Mathf.Min(20, (int)(Mathf.Log10(def.Gems) * 7))));
            row.Root.Add(box);
            var info = Ui.Div("se-grow");
            if (!string.IsNullOrEmpty(def.Tag)) info.Add(Ui.Text(def.Tag, "se-offer-tag"));
            info.Add(Ui.Text(def.Name, "se-h3"));
            info.Add(Ui.Text(L.F("{0} Gems", Num.Int(def.Gems)), "se-small"));
            row.Root.Add(info);
            row.Buy = Ui.Btn(App.Store.Price(def), () => App.BuyProduct(p), "green");
            row.Buy.AddToClassList("se-pricebtn");
            row.Root.Add(row.Buy);
            return row;
        }

        StyleCard BuildCard(int i)
        {
            var def = C.Cosmetics[i];
            var card = new StyleCard { Index = i, Root = Ui.Div("se-card", "se-stylecard") };
            if (def.Exclusive) card.Root.Add(Ui.Text(L.T("EXCLUSIVE"), "se-ribbon", "gold"));
            else if (def.Bundle != null) card.Root.Add(Ui.Text(C.BundleIndex(def.Bundle) >= 0 ? L.T("AD BUNDLE") : L.T("OFFER"), "se-ribbon", "purple"));
            card.Root.Add(new Swatch(def));
            card.Root.Add(Ui.Text(def.Name, "se-stylecard-name"));
            string bonus = StyleText.Bonus(C, def);
            card.Root.Add(Ui.Text(bonus ?? L.T("No bonus"), "se-stylecard-bonus"));
            card.Status = Ui.Text("", "se-stylecard-status");
            card.Root.Add(card.Status);
            card.Action = Ui.Btn("", () =>
            {
                if (InBundle(i)) FocusBundle(def.Bundle);
                else App.BuyOrEquipCosmetic(i);
            }, "green", "se-stylecard-btn");
            card.Root.Add(card.Action);
            card.Root.RegisterCallback<ClickEvent>(e =>
            {
                if (e.target is Button) return;
                App.Sfx?.Click();
                bool buyable = !G.IsCosmeticOwned(i) && G.IsCosmeticForSale(i) && G.CosmeticRequirementsMet(i);
                App.Ui.ShowPopup(new StyleItemPopup(App, i, buyable ? () => App.BuyOrEquipCosmetic(i) : (System.Action)null,
                    InBundle(i) ? () => FocusBundle(def.Bundle) : (System.Action)null, BundleLinkText(def)));
            });
            return card;
        }

        /// <summary>Not owned yet and only available as part of a bundle (an ads-only bundle or the starter offer).</summary>
        bool InBundle(int i) => C.Cosmetics[i].Bundle != null && !C.Cosmetics[i].Default && !G.IsCosmeticOwned(i);

        string BundleLinkText(CosmeticDef def) => C.BundleIndex(def.Bundle) >= 0 ? L.T("See bundle") : L.T("See offer");

        // ------------------------------------------------------------ navigation

        void Show(Tab s)
        {
            _section = s;
            for (int i = 0; i < 3; i++)
            {
                _sectionButtons[i].EnableInClassList("selected", (int)s == i);
                _sections[i].Show((int)s == i);
            }
            Scroll.scrollOffset = Vector2.zero;
            Refresh();
        }

        /// <summary>Opens a section by name: "featured", "styles" or "gems".</summary>
        public void OpenSection(string name)
        {
            if (name == "gems") Show(Tab.Gems);
            else if (name == "styles") Show(Tab.Styles);
            else Show(Tab.Featured);
        }

        /// <summary>
        /// Shows the Featured section scrolled to a bundle's card and makes it glow for a moment. Items of
        /// the starter offer (not an ads-only bundle) lead to the starter card instead.
        /// </summary>
        public void FocusBundle(string bundleId)
        {
            Show(Tab.Featured);
            int b = C.BundleIndex(bundleId);
            VisualElement target = null;
            foreach (var card in _bundles)
                if (card.Index == b) target = card.Root;
            if (target == null && _starter != null && !G.IsProductOwned(StarterId())) target = _starter;
            if (target == null) return;
            // The section was hidden until now: wait for its layout, then bring the card near the top.
            target.schedule.Execute(() =>
            {
                float y = target.ChangeCoordinatesTo(Scroll.contentContainer, Vector2.zero).y;
                Scroll.scrollOffset = new Vector2(0, Mathf.Max(0, y - 10));
                target.AddToClassList("glow");
                target.schedule.Execute(() => target.RemoveFromClassList("glow")).ExecuteLater(2500);
            }).ExecuteLater(50);
        }

        string StarterId()
        {
            foreach (var p in C.Products)
                if (p.OneTime) return p.Id;
            return null;
        }

        void SetSlot(int slot)
        {
            _slot = slot;
            for (int i = 0; i < _slotButtons.Length; i++) _slotButtons[i].EnableInClassList("selected", i == slot);
            _grid.Clear();
            _cards.Clear();
            for (int i = 0; i < C.Cosmetics.Length; i++)
            {
                if (C.Cosmetics[i].Slot != Slots[slot]) continue;
                var card = BuildCard(i);
                _cards.Add(card);
                _grid.Add(card.Root);
            }
            _slotHint.Set(Slots[slot] == CosmeticSlot.Title
                ? L.T("Titles show next to your name on the leaderboards and add Storm Core gain.")
                : L.T("Every style boosts one stat while equipped (capped at x1.10 in tournaments). Tap a card for details."));
            Refresh();
        }

        // ------------------------------------------------------------ refresh

        public override void OnShow() => Refresh();

        public override void Refresh()
        {
            _gemBalance.Set(Num.Int(G.Gems));
            switch (_section)
            {
                case Tab.Featured:
                    RefreshAds();
                    RefreshBundles();
                    RefreshProducts();
                    foreach (var card in _exclusiveCards) RefreshCard(card);
                    break;
                case Tab.Styles:
                    foreach (var card in _cards) RefreshCard(card);
                    break;
                default:
                    RefreshAds();
                    RefreshProducts();
                    _storeNote.Set(App.Store.Simulated ? L.T("Test store: purchases are simulated in the Editor and development builds (nothing is charged).") : "");
                    _storeNote.Show(App.Store.Simulated);
                    break;
            }
        }

        void RefreshAds()
        {
            foreach (var row in _adRows)
            {
                int a = C.AdIndex(row.Id);
                var def = C.AdPlacements[a];
                bool levelOk = G.Level >= def.UnlockLevel;
                int left = G.AdUsesLeft(a);
                bool can = App.CanOfferAd(row.Id);
                row.Watch.SetEnabledState(can);
                if (!levelOk) row.Left.Set(L.F("Unlocks at Engine Level {0}", def.UnlockLevel));
                else if (def.Reward == AdReward.EnergySurge && G.SurgeSeconds > 0)
                    row.Left.Set(L.F("Active: {0} left", Num.Time(G.SurgeSeconds)) + (def.DailyLimit > 0 ? "  ·  " + L.F("{0}/{1} today", left, def.DailyLimit) : ""));
                else row.Left.Set(def.DailyLimit > 0 ? L.F("{0}/{1} left today", left, def.DailyLimit) : L.T("Unlimited"));
                row.Root.EnableInClassList("dim", !levelOk);
            }
        }

        void RefreshBundles()
        {
            foreach (var card in _bundles)
            {
                var def = C.Bundles[card.Index];
                bool complete = G.IsBundleComplete(card.Index);
                bool levelOk = G.Level >= def.UnlockLevel;
                int n = G.BundleProgress(def.Id);
                card.Bar.Value = complete ? 1 : n / (float)def.AdsRequired;
                card.Progress.Set(complete ? L.T("OWNED") : L.F("{0}/{1} ads", Mathf.Min(n, def.AdsRequired), def.AdsRequired));
                card.Watch.Show(!complete);
                card.Equip.Show(complete);
                if (!complete)
                {
                    bool can = levelOk && App.CanOfferAd("ad_bundle", def.Id);
                    card.Watch.SetEnabledState(can);
                    var label = card.Watch.Q<Label>(className: "se-iconbtn-label");
                    label?.Set(!levelOk ? L.F("Unlocks at Lv {0}", def.UnlockLevel) : G.AdUsesLeft(C.AdIndex("ad_bundle")) <= 0 ? L.T("Come back tomorrow") : L.T("Watch ad  +1"));
                }
                card.Root.EnableInClassList("dim", !levelOk);
            }
        }

        void RefreshProducts()
        {
            foreach (var row in _products)
            {
                var def = C.Products[row.Index];
                bool owned = def.OneTime && G.IsProductOwned(def.Id);
                if (row.Root == _starter)
                {
                    _starter.Show(!owned);
                    continue;
                }
                row.Buy.SetButton(App.Store.Price(def));
                row.Buy.SetEnabledState(App.Store.IsReady);
            }
            if (_starter != null)
            {
                foreach (var row in _products)
                    if (row.Root == _starter) row.Buy.SetButton(App.Store.Price(C.Products[row.Index]));
            }
        }

        void RefreshCard(StyleCard card)
        {
            var def = C.Cosmetics[card.Index];
            var equipped = G.Equipped(def.Slot);
            bool owned = G.IsCosmeticOwned(card.Index) || def.Default;
            bool isEquipped = equipped != null && equipped.Id == def.Id;
            bool reqs = G.CosmeticRequirementsMet(card.Index);
            card.Root.EnableInClassList("equipped", isEquipped);
            card.Root.EnableInClassList("locked", !owned && (!reqs || !G.IsCosmeticForSale(card.Index)));
            if (isEquipped)
            {
                card.Status.Set(L.T("EQUIPPED"));
                card.Action.Show(false);
            }
            else if (owned)
            {
                card.Status.Set(L.T("Owned"));
                card.Action.Show(true);
                card.Action.SetButton(L.T("Equip"));
                card.Action.SetEnabledState(true);
            }
            else if (def.Bundle != null)
            {
                int b = C.BundleIndex(def.Bundle);
                card.Status.Set(b >= 0 ? L.F("{0} bundle", C.Bundles[b].Name) : L.T("Starter Pack"));
                // Bundle items are not sold alone: the button takes the player to the bundle.
                card.Action.Show(true);
                card.Action.SetButton(BundleLinkText(def));
                card.Action.SetEnabledState(true);
            }
            else if (!reqs)
            {
                card.Action.Show(false);
                card.Status.Set(RequirementText(def));
            }
            else if (def.CostGems > 0)
            {
                card.Status.Set("");
                card.Action.Show(true);
                card.Action.SetButton(L.F("{0} Gems", Num.Int(def.CostGems)));
                card.Action.SetEnabledState(G.Gems >= def.CostGems);
            }
            else
            {
                card.Status.Set(def.CostCores > 0 ? L.F("{0} Storm Cores", def.CostCores) : L.T("Free"));
                card.Action.Show(true);
                card.Action.SetButton(def.CostCores > 0 ? L.T("Unlock") : L.T("Claim"));
                card.Action.SetEnabledState(G.Cores >= def.CostCores);
            }
        }

        string RequirementText(CosmeticDef def)
        {
            if (G.Level < def.UnlockLevel) return L.F("Engine Lv {0}", def.UnlockLevel);
            if (def.RequiresDiscovery != null)
            {
                int p = C.PhenoIndex(def.RequiresDiscovery);
                return L.F("Discover {0}", p >= 0 && G.Discovered[p] ? C.Phenomena[p].Name : "???");
            }
            if (G.Prestiges < def.RequiresPrestiges) return L.F("Atmosphere resets: {0}", def.RequiresPrestiges);
            return L.T("Locked");
        }

        public override bool HasAttention()
        {
            for (int i = 0; i < C.Cosmetics.Length; i++)
            {
                var def = C.Cosmetics[i];
                if (G.IsCosmeticForSale(i) && !G.IsCosmeticOwned(i) && def.CostGems == 0 && def.CostCores == 0 && G.CosmeticRequirementsMet(i)) return true;
            }
            return false;
        }
    }
}
