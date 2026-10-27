using System;
using StormEngine.Audio;
using StormEngine.Core;
using StormEngine.Online;
using StormEngine.UI;

namespace StormEngine
{
    /// <summary>Player actions around the core loop: shop, rewarded ads, tournaments, island decor and visiting.</summary>
    public sealed partial class StormApp
    {
        float _rewardTimer = 5f;
        bool _claimingReward;
        bool _welcomed;

        void OnFirstPlay()
        {
            if (!_isNewPlayer || _welcomed) return;
            _welcomed = true;
            Ui.ShowWelcome();
        }

        void TickMeta(float dt)
        {
            if (_checkDaily) MaybeShowDaily();
            _rewardTimer -= dt;
            if (_rewardTimer > 0) return;
            _rewardTimer = 30f;
            if (Game.UnclaimedTournamentWeek >= 0) ClaimTournamentReward();
        }

        void RememberWeekly()
        {
            int w = Game.Build.Weekly;
            if (w < 0) return;
            string id = Content.Weekly[w].Id;
            if (!Save.weeklySeen.Contains(id)) Save.weeklySeen.Add(id);
        }

        // ------------------------------------------------------------ rewarded ads

        /// <summary>Plays a rewarded ad and grants the placement's reward. <paramref name="target"/> names the bundle for bundle ads.</summary>
        public void WatchAd(string placementId, string target = null, Action onRewarded = null)
        {
            int a = Content.AdIndex(placementId);
            if (a < 0 || !Game.CanWatchAd(a, target))
            {
                Sfx.Deny();
                if (a >= 0 && Game.AdUsesLeft(a) <= 0) Ui.Toast("clock", L.T("That reward is used up for today. Come back tomorrow!"));
                return;
            }
            if (!Ads.Available)
            {
                Sfx.Deny();
                Ui.Toast("play", L.T("No ad is available right now. Try again in a moment."));
                return;
            }
            Ads.ShowRewarded(ok =>
            {
                if (!ok)
                {
                    Ui.Toast("close", L.T("The ad was skipped, so there is no reward this time."));
                    return;
                }
                var def = Content.AdPlacements[a];
                long coresBefore = Game.LastStormCores;
                if (!Game.GrantAd(a, target)) return;
                Sfx.Goal();
                string text = null;
                switch (def.Reward)
                {
                    case AdReward.Gems: text = L.F("+{0} Gems!", Num.Int((long)def.Amount)); break;
                    case AdReward.EnergySurge: text = L.F("Energy Surge! x{0} income for {1}.", Num.Fixed(Content.B.SurgeMultiplier, 0), Num.Time(Game.SurgeSeconds)); break;
                    case AdReward.InstantCharge: text = L.T("Storm chamber fully charged!"); break;
                    case AdReward.TournamentAttempt: text = L.T("+1 tournament attempt today."); break;
                    case AdReward.DoubleCores: text = L.F("+{0} Storm Cores!", Num.Int(coresBefore)); break;
                    case AdReward.DoubleOffline: text = L.T("Offline Energy doubled!"); break;
                    case AdReward.MusicTrack: text = L.F("New music unlocked: {0}!", MusicLibrary.Find(target) is MusicTrack track ? L.T(track.Name) : L.T("a new track")); break;
                    case AdReward.StreakRestore: text = L.F("Streak saved! {0} days and counting.", Game.Daily.Streak); break;
                    case AdReward.BundleProgress:
                        int b = Content.BundleIndex(target);
                        if (b >= 0 && !Game.IsBundleComplete(b))
                            text = L.F("{0}: {1}/{2} ads watched.", Content.Bundles[b].Name, Game.BundleProgress(target), Content.Bundles[b].AdsRequired);
                        break;
                }
                if (text != null) Ui.Toast(def.Icon, text);
                onRewarded?.Invoke();
                SaveNow(false);
            });
        }

        /// <summary>True when the placement can be offered right now (reward valid, limit left, an ad to show).</summary>
        public bool CanOfferAd(string placementId, string target = null)
        {
            int a = Content.AdIndex(placementId);
            return a >= 0 && Ads.Available && Game.CanWatchAd(a, target);
        }

        // ------------------------------------------------------------ music

        public bool IsTrackOwned(string id)
        {
            var t = MusicLibrary.Find(id);
            // A build that can never show ads must not lock anything behind one.
            bool adsImpossible = !Ads.HasNetwork && !Ads.Simulated;
            return t != null && (t.Free || adsImpossible || Save.musicOwned.Contains(id));
        }

        /// <summary>Plays an owned track and remembers the choice.</summary>
        public void SelectTrack(string id)
        {
            if (!IsTrackOwned(id))
            {
                Sfx.Deny();
                return;
            }
            Save.settings.musicTrack = id;
            Save.settings.music = true;
            Sfx.PlayTrack(id);
            SaveNow(false);
        }

        /// <summary>Unlocks a track for good with a rewarded ad, then plays it.</summary>
        public void UnlockTrack(string id)
        {
            if (IsTrackOwned(id))
            {
                SelectTrack(id);
                return;
            }
            WatchAd("ad_music", id, () =>
            {
                if (!Save.musicOwned.Contains(id)) Save.musicOwned.Add(id);
                SelectTrack(id);
            });
        }

        // ------------------------------------------------------------ shop

        public void BuyProduct(int p) => Store.Buy(p);

        /// <summary>Equips an owned style item, or buys it (with a confirmation when it costs Gems).</summary>
        public void BuyOrEquipCosmetic(int i)
        {
            var def = Content.Cosmetics[i];
            if (Game.IsCosmeticOwned(i) || def.Default)
            {
                if (def.Default) Game.OwnedCosmetics.Add(def.Id);
                if (Game.EquipCosmetic(i))
                {
                    Sfx.Equip();
                    SaveNow(false);
                }
                return;
            }
            if (!Game.CanUnlockCosmetic(i))
            {
                Sfx.Deny();
                if (Game.IsCosmeticForSale(i) && Game.CosmeticRequirementsMet(i) && def.CostGems > Game.Gems)
                {
                    Ui.Toast("gem", L.F("You need {0} more Gems.", Num.Int(def.CostGems - Game.Gems)));
                    Ui.OpenShop("gems");
                }
                return;
            }
            void Unlock()
            {
                if (!Game.UnlockCosmetic(i))
                {
                    Sfx.Deny();
                    return;
                }
                Sfx.Discovery();
                string bonus = StyleText.Bonus(Content, def);
                Ui.Toast("brush", L.F("{0} unlocked and equipped!", def.Name) + (bonus != null ? " " + bonus : ""));
                SaveNow(false);
            }
            if (def.CostGems > 0)
                Ui.Confirm(L.F("BUY {0}?", def.Name.ToUpperInvariant()),
                    L.F("Spend {0} Gems on {1}?", Num.Int(def.CostGems), def.Name) + (StyleText.Bonus(Content, def) is string b ? " " + L.F("Bonus: {0}.", b) : ""),
                    L.F("Buy for {0}", Num.Int(def.CostGems)), Unlock);
            else Unlock();
        }

        // ------------------------------------------------------------ tournament

        public void EnterTournament()
        {
            if (World != null && World.IsVisiting) EndVisit();
            if (Game.StartTournament())
            {
                Ui.OnTournamentStarted();
                SaveNow(false);
                return;
            }
            Sfx.Deny();
            if (!Game.TournamentUnlocked)
                Ui.Toast("lock", L.F("The Weekly Tournament unlocks at Engine Level {0}.", Game.FeatureLevel("tournament")));
            else if (Game.IsStorming)
                Ui.Toast("storm", L.T("Wait for the current storm to finish first."));
            else if (Game.TournamentAttemptsLeft <= 0)
                Ui.Toast("trophy", CanOfferAd("ad_tourney")
                    ? L.T("No attempts left today. They refill at midnight (UTC), or watch an ad for one more.")
                    : L.T("No attempts left today. They refill at midnight (UTC)."));
        }

        async void ClaimTournamentReward()
        {
            int week = Game.UnclaimedTournamentWeek;
            if (week < 0 || _claimingReward) return;
            _claimingReward = true;
            try
            {
                int rank = 0;
                if (Online.State != OnlineState.Disabled)
                {
                    var archived = await Online.LastWeekRank();
                    if (archived == null) return;
                    // No archived week yet (or an older week): the player still gets the participation reward.
                    bool sameWeek = archived.VersionId != null && Math.Abs(archived.EndUnix - WeekClock.WeekEnd(week)) < 2 * 86400;
                    rank = sameWeek ? Math.Max(0, archived.Rank) : 0;
                }
                if (Game == null) return;
                long gems = Game.ClaimTournamentReward(week, rank);
                if (gems <= 0) return;
                Sfx.Discovery();
                Ui.ShowPopup(new TournamentRewardPopup(this, rank, gems));
                SaveNow(false);
            }
            finally
            {
                _claimingReward = false;
            }
        }

        // ------------------------------------------------------------ island decor

        public void BuyDecoration(int d)
        {
            var def = Content.Decorations[d];
            if (World != null && World.IsVisiting) EndVisit();
            if (!Game.CanBuyDecoration(d))
            {
                Sfx.Deny();
                if (Game.Placed.Count >= Game.DecorCapacity) Ui.Toast("island", L.T("Your island is full. Expand it or store a decoration first."));
                else if (def.Kind == DecorKind.Water && Game.WaterSlotsFree <= 0) Ui.Toast("water", L.T("Water decorations float on a pond. Place a Pond first (each holds three)."));
                else if (Game.DecorCopies(def.Id) >= def.MaxCopies) Ui.Toast("lock", L.F("You can own up to {0} of {1}.", def.MaxCopies, def.Name));
                else if (def.CostGems > Game.Gems) Ui.Toast("gem", L.F("You need {0} more Gems.", Num.Int(def.CostGems - Game.Gems)));
                else if (def.CostCores > Game.Cores) Ui.Toast("core", L.F("You need {0} more Storm Cores.", Num.Int(def.CostCores - Game.Cores)));
                return;
            }
            void Buy()
            {
                bool first = Game.DecorCopies(def.Id) == 0;
                if (!Game.BuyDecoration(d))
                {
                    Sfx.Deny();
                    return;
                }
                Sfx.Buy();
                bool placed = World != null && World.AutoPlace(def.Id);
                string bonus = first ? " " + L.F("Collection bonus +{0} Energy.", Num.Percent(def.Bonus)) : "";
                Ui.Toast("island", (placed ? L.F("{0} placed on your island!", def.Name) : L.F("{0} bought and stored (no free spot).", def.Name)) + bonus);
                SaveNow(false);
            }
            if (def.CostGems > 0)
                Ui.Confirm(L.F("BUY {0}?", def.Name.ToUpperInvariant()), L.F("Spend {0} Gems on {1}?", Num.Int(def.CostGems), def.Name), L.F("Buy for {0}", Num.Int(def.CostGems)), Buy);
            else Buy();
        }

        /// <summary>Puts a stored copy back on the island.</summary>
        public void PlaceDecoration(string id)
        {
            if (World == null || World.IsVisiting) return;
            if (Game.Placed.Count >= Game.DecorCapacity)
            {
                Sfx.Deny();
                Ui.Toast("island", L.T("Your island is full. Expand it or store a decoration first."));
                return;
            }
            if (World.AutoPlace(id))
            {
                Sfx.Equip();
                SaveNow(false);
            }
            else
            {
                Sfx.Deny();
                int d = Content.DecorationIndex(id);
                bool water = d >= 0 && Content.Decorations[d].Kind == DecorKind.Water;
                Ui.Toast("island", water ? L.T("No free pond spot. Each pond holds three water decorations.") : L.T("No free spot left. Move things around or expand the island."));
            }
        }

        /// <summary>Takes a placed decoration off the island (it stays owned).</summary>
        public void StoreDecoration(int index)
        {
            if (index < 0 || index >= Game.Placed.Count) return;
            var place = Game.Placed[index];
            if (place.Id == "dec_pond" && PondHasWater(place))
            {
                Sfx.Deny();
                Ui.Toast("water", L.T("Store the water decorations on this pond first."));
                return;
            }
            if (!Game.RemovePlacement(index)) return;
            Sfx.Click();
            World?.Puff(new UnityEngine.Vector3(place.X, 0.8f, place.Z), 10);
            SaveNow(false);
        }

        bool PondHasWater(PlacedDecor pond)
        {
            foreach (var p in Game.Placed)
            {
                int d = Content.DecorationIndex(p.Id);
                if (d < 0 || Content.Decorations[d].Kind != DecorKind.Water) continue;
                float dx = p.X - pond.X, dz = p.Z - pond.Z;
                if (dx * dx + dz * dz <= 1.1f) return true;
            }
            return false;
        }

        public void BuyExpansion(int e)
        {
            var def = Content.Expansions[e];
            if (!Game.CanBuyExpansion(e))
            {
                Sfx.Deny();
                if (def.CostGems > Game.Gems && !Game.IsExpansionOwned(e)) Ui.Toast("gem", L.F("You need {0} more Gems.", Num.Int(def.CostGems - Game.Gems)));
                return;
            }
            Ui.Confirm(def.Name.ToUpperInvariant() + "?", def.Desc + " " + L.F("Spend {0} Gems?", Num.Int(def.CostGems)), L.F("Expand for {0}", Num.Int(def.CostGems)), () =>
            {
                if (World != null && World.IsVisiting) EndVisit();
                if (!Game.BuyExpansion(e))
                {
                    Sfx.Deny();
                    return;
                }
                Sfx.Upgrade();
                Ui.Toast("island", L.F("{0}! Your island grew.", def.Name));
                SaveNow(false);
            });
        }

        // ------------------------------------------------------------ visiting

        /// <summary>Loads another player's published island and flies there.</summary>
        public async void VisitPlayer(string playerId, string name)
        {
            if (string.IsNullOrEmpty(playerId)) return;
            if (playerId == Online.PlayerId)
            {
                Ui.Toast("island", L.T("That is your own island!"));
                return;
            }
            if (!Online.IsOnline)
            {
                Sfx.Deny();
                Ui.Toast("offline", L.T("Visiting other players needs a connection."));
                return;
            }
            Ui.Toast("island", L.F("Flying to {0}'s island...", name), 1.5f);
            var layout = await Online.LoadIsland(playerId, Content);
            if (Game == null) return;
            if (layout == null)
            {
                Ui.Toast("island", L.F("{0} has not opened their island to visitors yet.", name));
                return;
            }
            Visit(layout);
        }

        public void Visit(IslandLayout layout)
        {
            if (World == null || layout == null) return;
            if (Game.IsStorming)
            {
                Sfx.Deny();
                Ui.Toast("storm", L.T("Finish the current storm before visiting."));
                return;
            }
            World.EditMode = false;
            World.EnterVisit(layout);
            Ui.SetVisiting(layout);
            Sfx.Popup();
        }

        public void EndVisit()
        {
            if (World == null || !World.IsVisiting) return;
            World.ExitVisit();
            Ui.SetVisiting(null);
            Sfx.Click();
        }
    }
}
