using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>One Stormpedia entry. Unknown entries show "???" and a hint instead.</summary>
    public struct PediaEntry
    {
        public string Key;
        public string Icon;
        public string Name;
        public string Desc;
        public bool Known;
        public string Hint;
    }

    /// <summary>The in-game encyclopedia: what everything does, for the things you have unlocked.</summary>
    public static class Stormpedia
    {
        public static readonly string[] Categories =
        {
            L.N("Basics"), L.N("Machines"), L.N("Boosts"), L.N("Phenomena"), L.N("Modules"), L.N("Research"), L.N("Weekly"), L.N("Styles"), L.N("Decor")
        };

        public static List<PediaEntry> Entries(StormApp app, int category)
        {
            var g = app.Game;
            var c = app.Content;
            var list = new List<PediaEntry>();
            switch (category)
            {
                case 0:
                    Basic(list, g, "storm", L.T("Storm Power"), 1,
                        L.T("Storm Power = Base Pressure x Temperature x Humidity x Wind x Cloud x Electricity x Synergy. The categories multiply each other, so a balanced engine beats a lopsided one. Your Energy per second is your Storm Power times your income bonuses."));
                    Basic(list, g, "charge", L.T("Storms and chain reactions"), 1,
                        L.T("The storm chamber charges over time. Unleash a storm for a burst of Energy. Every lightning strike grows the chain reaction, which multiplies the whole storm score."));
                    Basic(list, g, "star", L.T("Engine Level"), 1,
                        L.T("Building machines, unleashing storms, discovering phenomena and finishing goals earn XP. New levels unlock machines, features and styles, and each level gives Gems."));
                    Basic(list, g, "core", L.T("Storm Cores"), 1,
                        L.T("Every storm leaves Storm Cores behind (bigger storms leave more). Spend them on modules, decorations and permanent upgrades."));
                    Basic(list, g, "gem", L.T("Gems"), 2,
                        L.F("The premium currency. Earned from Engine Levels (+{0}), new phenomena (+{1}), goals, daily rewards, free rewards and tournament prizes, or bought in the Shop. Spend them on styles, decorations and island expansions.", c.B.GemsPerLevel, c.B.GemsPerDiscovery));
                    Basic(list, g, "gift", L.T("Daily Rewards"), g.FeatureLevel("daily"),
                        L.T("Open the gift every day for a reward on a seven-day cycle. Collecting on consecutive days builds a streak, and three daily missions pay Gems, Storm Cores or Energy Surges. Everything resets at midnight UTC."));
                    Basic(list, g, "play", L.T("Free rewards"), 2,
                        L.T("Optional short ads give Gems, Energy Surges (x2 income), instant charges, double Storm Cores, extra tournament attempts and progress toward the ads-only bundles. Each has a daily limit."));
                    Basic(list, g, "atmosphere", L.T("Atmosphere Lab"), g.FeatureLevel("atmosphere"),
                        L.T("Tap the Storm Power readout to open the lab. It shows every modifier behind each category and forecasts your next storm."));
                    Basic(list, g, "chaos", L.T("Mega Storms and Perfect Windows"), g.FeatureLevel("atmosphere"),
                        L.F("With {0}+ phenomena active at once a storm becomes a Mega Storm (x{1}, more with {2} and {3}). Keeping {4}+ boosts running together opens a Perfect Window (x{5}).",
                            c.B.MegaCounts[0], Num.Fixed(c.B.MegaMults[0], 1), c.B.MegaCounts[1], c.B.MegaCounts[2], c.B.PerfectCounts[0], Num.Fixed(c.B.PerfectMults[0], 2)));
                    Basic(list, g, "brush", L.T("Style bonuses"), g.FeatureLevel("customize"),
                        L.F("Every skin, cloud, lightning, world and title boosts one stat while equipped. In tournaments each style bonus is capped at x{0}, so grinding your engine always matters most.", Num.Fixed(1 + c.B.TournamentStyleCap, 2)));
                    Basic(list, g, "modules", L.T("Modules"), g.FeatureLevel("modules"),
                        L.T("Modules bend the formula: conversions, conditions and big multipliers. Unlock them with Storm Cores and equip them in your limited slots."));
                    Basic(list, g, "island", L.T("Island and visitors"), g.FeatureLevel("island"),
                        L.T("Decorate your island on land, in the air and on ponds. Each different decoration you own adds a little Energy income, once. Players can visit each other's islands from the leaderboards."));
                    Basic(list, g, "trophy", L.T("Weekly Tournament"), g.FeatureLevel("tournament"),
                        L.F("Take your engine into the tournament under fixed rules: this week's condition applies, style bonuses are capped and decorations or surges do not count. {0} attempts a day; your best storm is ranked and verified by the server. Gems are paid out when the week ends.", c.B.TournamentAttemptsPerDay));
                    Basic(list, g, "calendar", L.T("Weekly conditions"), g.FeatureLevel("weekly"),
                        L.T("Every Monday (UTC) a new atmospheric condition boosts some stats and weakens others. It changes which build is best, in your engine and in the tournament."));
                    Basic(list, g, "battery", L.T("Offline Generator"), g.FeatureLevel("offline"),
                        L.T("An expensive permanent upgrade that keeps the engine producing while you are away, at reduced efficiency and up to a time cap."));
                    Basic(list, g, "research", L.T("Atmosphere reset"), g.FeatureLevel("research"),
                        L.T("Reset Energy and machine levels to earn Research, then spend it on permanent upgrades in the Research Lab. Modules, Storm Cores, styles and decorations are kept."));
                    break;

                case 1:
                    for (int m = 0; m < c.Machines.Length; m++)
                    {
                        var def = c.Machines[m];
                        list.Add(new PediaEntry
                        {
                            Key = "machine:" + def.Id, Icon = def.Icon, Name = def.Name, Desc = def.Desc, Known = g.MachineUnlocked[m],
                            Hint = g.Level < def.UnlockLevel ? L.F("Appears at Engine Level {0}.", def.UnlockLevel) : L.T("Keep producing Energy to reveal it.")
                        });
                    }
                    break;

                case 2:
                    for (int b = 0; b < c.Boosts.Length; b++)
                    {
                        var def = c.Boosts[b];
                        int m = c.MachineIndex(def.UnlockMachine);
                        string machine = m >= 0 && g.MachineUnlocked[m] ? c.Machines[m].Name : L.T("a machine you have not found yet");
                        list.Add(new PediaEntry
                        {
                            Key = "boost:" + def.Id, Icon = def.Icon, Name = def.Name,
                            Desc = def.Desc + " " + L.F("Lasts {0}s, recharges in {1}s.", Num.Fixed(def.Duration, 0), Num.Fixed(def.Cooldown, 0)),
                            Known = g.IsBoostUnlocked(b), Hint = L.F("Upgrade {0} to Lv {1}.", machine, def.UnlockLevel)
                        });
                    }
                    break;

                case 3:
                    for (int p = 0; p < c.Phenomena.Length; p++)
                    {
                        var def = c.Phenomena[p];
                        string recipe = "";
                        foreach (var ing in def.Ingredients)
                            recipe += (recipe.Length > 0 ? " + " : "") + c.Stats[(int)Content.CatStat((int)ing.Cat)].Name;
                        list.Add(new PediaEntry
                        {
                            Key = "pheno:" + def.Id, Icon = "storm", Name = def.Name, Desc = def.Desc + " " + L.F("Storm perk: {0}", def.PerkText),
                            Known = g.Discovered[p],
                            Hint = def.Special ? L.T("Only appears inside storms driven by a very unusual module.") : L.F("Recipe hint: push {0} high enough at the same time.", recipe)
                        });
                    }
                    break;

                case 4:
                    for (int m = 0; m < c.Modules.Length; m++)
                    {
                        var def = c.Modules[m];
                        string hint;
                        if (g.Level < def.UnlockLevel) hint = L.F("Appears at Engine Level {0}.", def.UnlockLevel);
                        else if (def.RequiresDiscovery != null && c.PhenoIndex(def.RequiresDiscovery) is int need && need >= 0 && !g.Discovered[need]) hint = L.T("Discover a new phenomenon to reveal it.");
                        else if (g.Prestiges < def.RequiresPrestiges) hint = L.T("Reset the atmosphere to reveal it.");
                        else hint = L.T("Unlock it in the Module Workshop.");
                        list.Add(new PediaEntry { Key = "module:" + def.Id, Icon = def.Icon, Name = def.Name, Desc = def.Desc, Known = g.IsModuleUnlocked(m), Hint = hint });
                    }
                    break;

                case 5:
                    for (int r = 0; r < c.Research.Length; r++)
                    {
                        var def = c.Research[r];
                        bool known = g.Build.Research[r] > 0 || (g.IsFeatureUnlocked("research") && g.IsResearchVisible(r));
                        list.Add(new PediaEntry
                        {
                            Key = "research:" + def.Id, Icon = def.Icon, Name = def.Name, Desc = def.Desc, Known = known,
                            Hint = g.IsFeatureUnlocked("research") ? L.T("Keep resetting the atmosphere to reveal it.") : L.F("Research unlocks at Engine Level {0}.", g.FeatureLevel("research"))
                        });
                    }
                    break;

                case 6:
                    for (int w = 0; w < c.Weekly.Length; w++)
                    {
                        var def = c.Weekly[w];
                        bool known = g.IsFeatureUnlocked("weekly") && (g.Build.Weekly == w || app.Save.weeklySeen.Contains(def.Id));
                        list.Add(new PediaEntry
                        {
                            Key = "weekly:" + def.Id, Icon = "calendar", Name = def.Name, Desc = string.Join("   ", def.Lines) + (g.Build.Weekly == w ? "   " + L.T("(this week)") : ""),
                            Known = known, Hint = g.IsFeatureUnlocked("weekly") ? L.T("A condition from a future week.") : L.F("Weekly conditions start at Engine Level {0}.", g.FeatureLevel("weekly"))
                        });
                    }
                    break;

                case 7:
                    for (int i = 0; i < c.Cosmetics.Length; i++)
                    {
                        var def = c.Cosmetics[i];
                        string bonus = StyleText.Bonus(c, def);
                        string where = def.Bundle != null ? (c.BundleIndex(def.Bundle) >= 0 ? L.T("Part of an ads-only bundle in the Shop.") : L.T("Part of a special offer."))
                            : def.RequiresDiscovery != null ? L.T("Discover a phenomenon to earn it.")
                            : def.Exclusive ? L.T("An exclusive in the Shop.") : L.T("Find it in the Shop.");
                        list.Add(new PediaEntry
                        {
                            Key = "style:" + def.Id, Icon = "brush", Name = def.Name + "  ·  " + Unlocks.SlotName(def.Slot),
                            Desc = (def.Desc ?? "") + (bonus != null && (def.Desc == null || !def.Desc.Contains("x1.")) ? " " + L.F("Bonus: {0}.", bonus) : ""),
                            Known = def.Default || g.IsCosmeticOwned(i), Hint = where
                        });
                    }
                    break;

                default:
                    foreach (var def in c.Decorations)
                    {
                        string kind = def.Kind == DecorKind.Air ? L.T("Air") : def.Kind == DecorKind.Water ? L.T("Water") : L.T("Land");
                        list.Add(new PediaEntry
                        {
                            Key = "decor:" + def.Id, Icon = "island", Name = def.Name + "  ·  " + kind,
                            Desc = def.Desc + " " + L.F("Collection bonus +{0} Energy.", Num.Percent(def.Bonus)),
                            Known = g.DecorCopies(def.Id) > 0,
                            Hint = def.Limited ? L.T("A limited decoration. It returns to the island shop some weeks.") : def.Bundle != null ? L.T("Part of an ads-only bundle.") : L.T("Sold in Island Decor.")
                        });
                    }
                    break;
            }
            return list;
        }

        static void Basic(List<PediaEntry> list, Game g, string icon, string name, int level, string desc)
        {
            list.Add(new PediaEntry
            {
                Key = "basic:" + icon, Icon = icon, Name = name, Desc = desc, Known = g.Level >= level,
                Hint = L.F("Unlocks at Engine Level {0}.", level)
            });
        }

        /// <summary>Unlocked entries the player has not read yet.</summary>
        public static int CountNew(StormApp app)
        {
            int n = 0;
            for (int cat = 0; cat < Categories.Length; cat++)
                foreach (var e in Entries(app, cat))
                    if (e.Known && !app.Save.pediaSeen.Contains(e.Key)) n++;
            return n;
        }
    }

    public sealed class StormpediaPopup : Popup
    {
        readonly StormApp _app;
        readonly Button[] _catButtons = new Button[Stormpedia.Categories.Length];
        readonly VisualElement _list;
        readonly Label _progress;
        int _cat;

        public StormpediaPopup(StormApp app, int category = 0)
            : base(L.T("STORMPEDIA"), L.T("Everything you have discovered so far"), "book")
        {
            _app = app;
            AddToClassList("se-pedia");
            var cats = Ui.Div("se-row", "se-wrap", "se-pediacats");
            for (int i = 0; i < _catButtons.Length; i++)
            {
                int cat = i;
                _catButtons[i] = Ui.Btn(L.T(Stormpedia.Categories[i]), () => Select(cat), "white", "small");
                cats.Add(_catButtons[i]);
            }
            BodyEl.Add(cats);
            _progress = Ui.Text("", "se-small", "se-note");
            BodyEl.Add(_progress);
            _list = Ui.Div();
            BodyEl.Add(_list);
            Action(L.T("Close"), null, "green");
            Select(category);
        }

        void Select(int cat)
        {
            _cat = cat;
            _app.Sfx?.Click();
            for (int i = 0; i < _catButtons.Length; i++) _catButtons[i].EnableInClassList("selected", i == cat);
            _list.Clear();
            var entries = Stormpedia.Entries(_app, cat);
            int known = 0;
            foreach (var e in entries)
            {
                if (e.Known) known++;
                _list.Add(Row(e));
                if (e.Known && !_app.Save.pediaSeen.Contains(e.Key)) _app.Save.pediaSeen.Add(e.Key);
            }
            _progress.Set(L.F("{0} of {1} discovered", known, entries.Count));
            RefreshDots();
        }

        VisualElement Row(PediaEntry e)
        {
            bool fresh = e.Known && !_app.Save.pediaSeen.Contains(e.Key);
            var row = Ui.Div("se-unlock", "se-pediarow");
            row.EnableInClassList("locked", !e.Known);
            var box = Ui.Div("se-iconbox", "small");
            if (e.Known) box.Add(new Icon(e.Icon, 28));
            else box.Add(Ui.Text("?", "se-pedia-q"));
            row.Add(box);
            var col = Ui.Div("se-grow");
            var head = Ui.Div("se-row");
            head.Add(Ui.Text(e.Known ? e.Name : "???", "se-h3"));
            if (fresh) head.Add(Ui.Text(L.T("NEW"), "se-newtag"));
            col.Add(head);
            col.Add(Ui.Text(e.Known ? e.Desc : e.Hint, "se-small"));
            row.Add(col);
            return row;
        }

        void RefreshDots()
        {
            for (int i = 0; i < _catButtons.Length; i++)
            {
                if (i == _cat) continue;
                bool any = false;
                foreach (var e in Stormpedia.Entries(_app, i))
                    if (e.Known && !_app.Save.pediaSeen.Contains(e.Key)) { any = true; break; }
                _catButtons[i].EnableInClassList("has-new", any);
            }
            _catButtons[_cat].EnableInClassList("has-new", false);
        }
    }
}
