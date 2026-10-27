using System;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Stand-in for a real rewarded ad in the Editor and development builds.</summary>
    public sealed class AdSimPopup : Popup
    {
        const float Seconds = 3f;
        readonly Action<bool> _done;
        readonly Bar _bar;
        readonly Label _count;
        readonly Button _claim;
        float _t;
        bool _finished;

        public AdSimPopup(Action<bool> done)
            : base(L.T("REWARDED AD"), L.T("Simulated in the Editor and development builds"), "play")
        {
            _done = done;
            DismissOnScrim = false;
            HeadEl.style.backgroundColor = Palette.Hex(0x3A2A7A);
            BodyEl.Add(Ui.Text(L.T("In a release build a real ad network (for example Unity LevelPlay) plays a short video here."), "se-body"));
            _bar = new Bar(Palette.Green, 14);
            _bar.style.marginTop = 10;
            BodyEl.Add(_bar);
            _count = Ui.Text("", "se-small", "se-center");
            BodyEl.Add(_count);
            Action(L.T("Skip"), () => Finish(false), "white");
            _claim = Action(L.T("Claim reward"), () => Finish(true), "green");
            _claim.SetEnabledState(false);
        }

        void Finish(bool ok)
        {
            if (_finished) return;
            _finished = true;
            _done?.Invoke(ok && _t >= Seconds);
        }

        public override void Tick(float dt)
        {
            if (_t >= Seconds) return;
            _t += dt;
            _bar.Value = Mathf.Clamp01(_t / Seconds);
            _count.Set(_t >= Seconds ? L.T("Ad finished.") : L.F("Ad ends in {0}s", Mathf.CeilToInt(Seconds - _t)));
            _claim.SetEnabledState(_t >= Seconds);
        }
    }

    /// <summary>Celebrates a completed ads-only bundle.</summary>
    public sealed class BundleCompletePopup : Popup
    {
        public BundleCompletePopup(StormApp app, int bundle)
            : base(L.F("{0} UNLOCKED!", app.Content.Bundles[bundle].Name.ToUpperInvariant()), L.T("A complete environment, earned with ads"), "star")
        {
            var c = app.Content;
            var def = c.Bundles[bundle];
            HeadEl.style.backgroundColor = Palette.Hex(def.Primary).Darken(0.35f);
            var grid = Ui.Div("se-row", "se-wrap");
            grid.style.justifyContent = Justify.Center;
            foreach (var id in def.Cosmetics)
            {
                int i = c.CosmeticIndex(id);
                if (i < 0) continue;
                var cell = Ui.Div("se-bundlecell");
                cell.Add(new Swatch(c.Cosmetics[i]));
                cell.Add(Ui.Text(c.Cosmetics[i].Name, "se-small", "se-center"));
                grid.Add(cell);
            }
            BodyEl.Add(grid);
            if (def.Decorations.Length > 0)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var id in def.Decorations)
                {
                    int d = c.DecorationIndex(id);
                    if (d >= 0) names.Add(c.Decorations[d].Name);
                }
                var extra = Ui.Text(L.F("Decorations added to your island: {0}.", string.Join(", ", names)), "se-small");
                extra.style.marginTop = 8;
                BodyEl.Add(extra);
            }
            Action(L.T("Later"), null, "white");
            Action(L.T("Equip it all"), () =>
            {
                foreach (var id in def.Cosmetics)
                {
                    int i = c.CosmeticIndex(id);
                    if (i >= 0) app.Game.EquipCosmetic(i);
                }
                app.Sfx?.Equip();
                app.SaveNow(false);
            }, "green");
        }
    }

    /// <summary>End-of-week tournament reward.</summary>
    public sealed class TournamentRewardPopup : Popup
    {
        public TournamentRewardPopup(StormApp app, int rank, long gems)
            : base(L.T("TOURNAMENT RESULTS"), rank > 0 ? L.F("Last week you finished #{0}", Num.Int(rank)) : L.T("Thanks for competing last week!"), "trophy")
        {
            HeadEl.style.backgroundColor = Palette.Hex(0x7A4A0A);
            BodyEl.Add(Ui.Text(L.T("YOUR REWARD"), "se-small", "se-center"));
            var row = Ui.Div("se-row");
            row.style.justifyContent = Justify.Center;
            row.Add(new Icon("gem", 44));
            row.Add(Ui.Text("+" + Num.Int(gems), "se-bignum"));
            BodyEl.Add(row);
            BodyEl.Add(Ui.Text(L.T("A new weekly tournament has started with a fresh condition. Grind your engine, then enter again!"), "se-small", "se-center"));
            Action(L.T("Collect"), null, "green");
        }
    }

    /// <summary>Details of a style item: bigger preview, bonus, tournament cap and price.</summary>
    public sealed class StyleItemPopup : Popup
    {
        /// <param name="seeBundle">For items only sold as part of a bundle: shows that bundle in the Shop.</param>
        public StyleItemPopup(StormApp app, int item, Action buy, Action seeBundle = null, string seeBundleText = null)
            : base(app.Content.Cosmetics[item].Name, Unlocks.SlotName(app.Content.Cosmetics[item].Slot), "brush")
        {
            var c = app.Content;
            var def = c.Cosmetics[item];
            var preview = new Swatch(def);
            preview.AddToClassList("big");
            BodyEl.Add(preview);
            if (!string.IsNullOrEmpty(def.Desc)) BodyEl.Add(Ui.Text(def.Desc, "se-body"));
            string bonus = StyleText.Bonus(c, def);
            if (bonus != null)
            {
                Kv(L.T("Bonus"), bonus);
                Kv(L.T("In tournaments"), StyleText.Bonus(c, def, c.B.TournamentStyleCap) + " " + L.T("(capped)"));
            }
            if (seeBundle != null)
                BodyEl.Add(Ui.Text(c.BundleIndex(def.Bundle) >= 0 ? L.T("Part of an ads-only bundle in the Shop.") : L.T("Part of a special offer."), "se-small"));
            Action(L.T("Close"), null, "white");
            if (buy != null) Action(L.T("Get it"), buy, "green");
            else if (seeBundle != null) Action(seeBundleText ?? L.T("See bundle"), seeBundle, "green");
        }
    }

    /// <summary>Formats style bonuses ("Humidity x1.10").</summary>
    public static class StyleText
    {
        public static string Bonus(Content c, CosmeticDef def, double cap = 0)
        {
            if (def.Effects.Length == 0) return null;
            var parts = new System.Collections.Generic.List<string>();
            foreach (var e in def.Effects)
            {
                double v = e.ValueAt(1);
                if (cap > 0 && e.Op == ModOp.Mult) v = Math.Min(v, 1 + cap);
                string name = c.Stats[(int)e.Stat].Name;
                parts.Add(e.Op == ModOp.Mult ? name + " x" + v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : name + " +" + Num.Percent(v));
            }
            return string.Join(", ", parts);
        }
    }
}
