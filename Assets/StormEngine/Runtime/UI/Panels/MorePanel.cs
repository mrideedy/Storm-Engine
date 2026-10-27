using System.Collections.Generic;
using StormEngine.Audio;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>Weekly condition, lifetime stats and settings.</summary>
    public sealed class MorePanel : PanelView
    {
        readonly Label _weeklyName, _weeklyTime;
        readonly VisualElement _weeklyLines, _weeklyCard, _weeklyLocked;
        readonly Label[] _stats;
        readonly Button _sfx, _music, _ambience, _quality, _notation, _motion, _auto, _vibrate, _fps, _autoQuality, _reminders, _language, _tips;
        readonly Button _ads, _showFps, _online, _deleteOnline;
        readonly Label _sfxVolume, _musicVolume;
        readonly Label _saveInfo;
        readonly List<TrackRow> _tracks = new List<TrackRow>();
        int _weeklyShown = -2;

        sealed class TrackRow
        {
            public MusicTrack Track;
            public VisualElement Root;
            public Button Play;
            public Button Unlock;
            public Label State;
        }

        static readonly string[] StatNames =
        {
            L.N("Storms unleashed"), L.N("Lightning strikes"), L.N("Mega storms"), L.N("Best chain"), L.N("Boosts used"),
            L.N("Best Storm Power"), L.N("Lifetime Energy"), L.N("Atmosphere resets"), L.N("Phenomena discovered"), L.N("Time played")
        };

        public MorePanel(StormApp app) : base(app, "more", L.T("Storm Lab"), "more", L.T("More"))
        {
            var weekly = Ui.Div("se-card", "glow");
            var head = Ui.Div("se-row");
            head.Add(new Icon("calendar", 34));
            var t = Ui.Div("se-grow");
            t.style.marginLeft = 8;
            t.Add(Ui.Text(L.T("WEEKLY CONDITION"), "se-small"));
            _weeklyName = Ui.Text("", "se-h2");
            t.Add(_weeklyName);
            head.Add(t);
            weekly.Add(head);
            _weeklyLines = Ui.Div();
            _weeklyLines.style.marginTop = 6;
            weekly.Add(_weeklyLines);
            _weeklyTime = Ui.Text("", "se-small");
            weekly.Add(_weeklyTime);
            weekly.Add(Ui.Text(L.T("Conditions rotate every Monday (UTC) and change which build is best. The weekly tournament and its leaderboard reset at the same time."), "se-small"));
            Body.Add(weekly);
            _weeklyCard = weekly;
            _weeklyLocked = Ui.Div("se-card", "dim", "se-row");
            _weeklyLocked.Add(new Icon("lock", 30));
            var lockText = Ui.Text(L.F("Weekly Conditions unlock at Engine Level {0}: a new atmospheric twist every Monday.", G.FeatureLevel("weekly")), "se-small", "se-grow");
            lockText.style.marginLeft = 8;
            _weeklyLocked.Add(lockText);
            Body.Add(_weeklyLocked);

            Body.Add(Section(L.T("Statistics")));
            var stats = Ui.Div("se-card");
            _stats = new Label[StatNames.Length];
            for (int i = 0; i < StatNames.Length; i++)
            {
                var kv = Ui.Div("se-kv");
                kv.Add(Ui.Text(L.T(StatNames[i])));
                _stats[i] = Ui.Text("", "v");
                kv.Add(_stats[i]);
                stats.Add(kv);
            }
            Body.Add(stats);

            Body.Add(Section(L.T("Music")));
            var music = Ui.Div("se-card");
            foreach (var track in MusicLibrary.Tracks) _tracks.Add(TrackRowFor(music, track));
            var musicNote = Ui.Text(L.T("Ambient and cozy tracks. Locked ones are yours to keep after a short ad."), "se-small");
            musicNote.style.marginTop = 6;
            music.Add(musicNote);
            Body.Add(music);

            Body.Add(Section(L.T("Settings")));
            var settings = Ui.Div("se-card");
            _language = Toggle(settings, L.T("Language"), () => App.Ui.ShowPopup(new LanguagePopup(App)));
            _language.AddToClassList("se-langvalue");
            _sfx = Toggle(settings, L.T("Sound effects"), () => { App.Save.settings.sfx = !App.Save.settings.sfx; });
            _sfxVolume = Stepper(settings, L.T("Effects volume"), d => App.Save.settings.sfxVolume = Mathf.Clamp(Mathf.Round((App.Save.settings.sfxVolume + d) * 10) / 10f, 0f, 1f));
            _music = Toggle(settings, L.T("Music"), () => { App.Save.settings.music = !App.Save.settings.music; });
            _musicVolume = Stepper(settings, L.T("Music volume"), d => App.Save.settings.musicVolume = Mathf.Clamp(Mathf.Round((App.Save.settings.musicVolume + d) * 10) / 10f, 0f, 1f));
            _ambience = Toggle(settings, L.T("Weather ambience"), () => { App.Save.settings.ambience = !App.Save.settings.ambience; });
            _vibrate = Toggle(settings, L.T("Vibration"), () =>
            {
                App.Save.settings.vibrate = !App.Save.settings.vibrate;
                App.Ui.ApplySettings();
            });
            _quality = Toggle(settings, L.T("Graphics quality"), () =>
            {
                App.Save.settings.quality = (App.Save.settings.quality + 1) % 3;
                App.Ui.ApplySettings();
            });
            _autoQuality = Toggle(settings, L.T("Auto-adjust graphics on slow phones"), () => { App.Save.settings.autoQuality = !App.Save.settings.autoQuality; });
            _fps = Toggle(settings, L.T("Frame rate"), () =>
            {
                // Smooth only differs from Standard on screens faster than 60 Hz.
                int modes = App.Perf.MaxRefresh > 60 ? 3 : 2;
                App.Save.settings.frameRate = (Mathf.Clamp(App.Save.settings.frameRate, 0, 2) + 1) % modes;
            });
            _reminders = Toggle(settings, L.T("Reminders (daily gift, tournament)"), () =>
            {
                var s = App.Save.settings;
                s.notifications = !s.notifications;
                s.notifyAsked = true;
                if (s.notifications && App.Reminders.Supported && !App.Reminders.Allowed) App.Reminders.RequestPermission(null);
            });
            _notation = Toggle(settings, L.T("Number format"), () =>
            {
                App.Save.settings.notation = 1 - App.Save.settings.notation;
                Num.Notation = App.Save.settings.notation;
            });
            _motion = Toggle(settings, L.T("Reduce motion & flashes"), () =>
            {
                App.Save.settings.reduceMotion = !App.Save.settings.reduceMotion;
                App.Ui.ApplySettings();
            });
            _auto = Toggle(settings, L.T("Auto-storm (Storm Automation)"), () => { G.AutoStorm = !G.AutoStorm; });
            _tips = Toggle(settings, L.T("Tips & tutorials"), () => { App.Save.settings.tips = !App.Save.settings.tips; });
            var replay = Ui.Btn(L.T("Show all tips again"), () =>
            {
                App.Sfx?.Click();
                App.Ui.Tutorials.Replay();
                App.Ui.Toast("nimbus", L.T("Tips will show again as you play."));
                Refresh();
            }, "white", "small");
            replay.style.marginTop = 6;
            settings.Add(replay);
            Body.Add(settings);

            // Privacy: online features, the ad choice (when this build has an ad network), the policy
            // page and deleting the online account.
            var config = ReleaseConfig.Instance;
            Body.Add(Section(L.T("Privacy")));
            var privacy = Ui.Div("se-card");
            _online = Toggle(privacy, L.T("Online features"), () => App.Online.SetEnabled(!App.Save.settings.online));
            var onlineNote = Ui.Text(L.T("Leaderboards, tournaments, cloud save and island visits use the game servers. Turned off, nothing leaves your phone."), "se-small");
            onlineNote.style.marginTop = 4;
            privacy.Add(onlineNote);
            if (App.Ads.HasNetwork)
            {
                _ads = Toggle(privacy, L.T("Personalized ads"), () => App.SetAdConsent(App.Save.settings.adConsent != 1));
                var note = Ui.Text(L.T("Ads only play when you choose to watch one. Personalized ads use your device's advertising ID."), "se-small");
                note.style.marginTop = 4;
                privacy.Add(note);
            }
            var privacyButtons = Ui.Div("se-row", "se-wrap");
            privacyButtons.style.marginTop = 8;
            if (!string.IsNullOrWhiteSpace(config.privacyPolicyUrl))
            {
                var policy = Ui.Btn(L.T("Privacy policy"), App.OpenPrivacyPolicy, "white", "small");
                policy.style.marginRight = 6;
                policy.style.marginBottom = 4;
                privacyButtons.Add(policy);
            }
            _deleteOnline = Ui.Btn(L.T("Delete my online data"), () =>
            {
                if (!App.Online.IsOnline) return;
                App.RequestOnlineDataDeletion();
            }, "red", "small");
            _deleteOnline.style.marginBottom = 4;
            privacyButtons.Add(_deleteOnline);
            privacy.Add(privacyButtons);
            Body.Add(privacy);

            // Word of mouth: rating and sharing are the biggest free marketing a small game gets.
            Body.Add(Section(L.T("Enjoying Storm Engine?")));
            var community = Ui.Div("se-card");
            community.Add(Ui.Text(L.T("A rating or a share helps other storm chasers find the game."), "se-small"));
            var communityRow = Ui.Div("se-row");
            communityRow.style.marginTop = 8;
            var rate = Ui.IconBtn("star", L.T("Rate the game"), () =>
            {
                App.Sfx?.Click();
                Community.OpenStorePage();
            }, "green", "small");
            rate.style.marginRight = 6;
            communityRow.Add(rate);
            communityRow.Add(Ui.IconBtn("share", L.T("Share"), () =>
            {
                App.Sfx?.Click();
                string text = L.T("Build a weather machine, unleash giant storms and climb the weekly leaderboards in Storm Engine!");
                if (!Community.Share("Storm Engine", text + "\n" + ReleaseConfig.Instance.StoreUrl))
                    App.Ui.Toast("share", L.T("Copied! Paste it anywhere to share."));
            }, "blue", "small"));
            community.Add(communityRow);
            Body.Add(community);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Developer tools: compiled only in the Editor and Development builds.
            Body.Add(Section(L.T("Developer")));
            var dev = Ui.Div("se-card");
            dev.Add(Ui.Text(L.T("Testing only. Not included in release builds."), "se-small"));
            var unlock = Ui.Btn(L.T("Unlock everything"), () => App.Ui.Confirm(L.T("UNLOCK EVERYTHING?"),
                L.T("Max Engine Level and every machine, module, style, bundle, decoration, expansion and music track, plus 100K Gems, 100K Storm Cores and 10K Research."),
                L.T("Unlock"), App.DevUnlockEverything, "purple"), "purple");
            unlock.style.marginTop = 6;
            dev.Add(unlock);
            _showFps = Toggle(dev, "FPS counter", () => { App.Save.settings.showFps = !App.Save.settings.showFps; });
            if (App.Ads.HasNetwork && config.showAdTestSuite)
            {
                var suite = Ui.Btn("Ad test suite (LevelPlay)", () => App.Ads.OpenTestSuite(), "white", "small");
                suite.style.marginTop = 6;
                dev.Add(suite);
            }
            Body.Add(dev);
#else
            _showFps = null;
#endif

            Body.Add(Section(L.T("Save")));
            var save = Ui.Div("se-card");
            _saveInfo = Ui.Text("", "se-small");
            save.Add(_saveInfo);
            var buttons = Ui.Div("se-row");
            buttons.style.marginTop = 8;
            var saveBtn = Ui.Btn(L.T("Save now"), () => App.SaveNow(true), "blue", "small");
            saveBtn.style.marginRight = 6;
            buttons.Add(saveBtn);
            buttons.Add(Ui.Btn(L.T("Reset progress"), () => App.RequestHardReset(), "red", "small"));
            save.Add(buttons);
            Body.Add(save);

            Body.Add(Ui.Text(L.T("STORM ENGINE · Build smart. Storm big."), "se-small", "se-center"));
        }

        TrackRow TrackRowFor(VisualElement parent, MusicTrack track)
        {
            var row = new TrackRow { Track = track, Root = Ui.Div("se-row", "se-trackrow") };
            var box = Ui.Div("se-iconbox", "small");
            box.Add(new Icon(track.Mood == "Cozy" ? "home" : "cloud", 26));
            row.Root.Add(box);
            var info = Ui.Div("se-grow");
            var head = Ui.Div("se-row");
            head.Add(Ui.Text(L.T(track.Name), "se-h3"));
            var mood = Ui.Text(L.T(track.Mood).ToUpperInvariant(), "se-tag", track.Mood == "Cozy" ? "cozy" : "ambient");
            mood.style.marginLeft = 6;
            head.Add(mood);
            info.Add(head);
            info.Add(Ui.Text(L.T(track.Desc), "se-small"));
            row.State = Ui.Text("", "se-small", "se-good");
            info.Add(row.State);
            row.Root.Add(info);
            row.Play = Ui.Btn(L.T("Play"), () => App.SelectTrack(track.Id), "white", "small");
            row.Play.style.minWidth = 86;
            row.Root.Add(row.Play);
            row.Unlock = Ui.IconBtn("play", L.T("Unlock"), () => App.UnlockTrack(track.Id), "green", "small");
            row.Root.Add(row.Unlock);
            parent.Add(row.Root);
            return row;
        }

        Button Toggle(VisualElement parent, string label, System.Action onClick)
        {
            var row = Ui.Div("se-toggle");
            row.Add(Ui.Text(label, "se-body", "se-grow"));
            var b = Ui.Btn("", () =>
            {
                onClick();
                App.Sfx?.Click();
                Refresh();
            }, "white", "small");
            b.style.minWidth = 110;
            row.Add(b);
            parent.Add(row);
            return b;
        }

        Label Stepper(VisualElement parent, string label, System.Action<float> change)
        {
            var row = Ui.Div("se-toggle");
            row.Add(Ui.Text(label, "se-body", "se-grow"));
            var minus = Ui.Btn("-", () =>
            {
                change(-0.1f);
                App.Sfx?.Click();
                Refresh();
            }, "white", "small", "se-stepbtn");
            var value = Ui.Text("", "se-stepvalue");
            var plus = Ui.Btn("+", () =>
            {
                change(0.1f);
                App.Sfx?.Click();
                Refresh();
            }, "white", "small", "se-stepbtn");
            row.Children(minus, value, plus);
            parent.Add(row);
            return value;
        }

        public override void Refresh()
        {
            bool weeklyOpen = G.IsFeatureUnlocked("weekly");
            _weeklyCard.Show(weeklyOpen);
            _weeklyLocked.Show(!weeklyOpen);
            int w = G.Build.Weekly;
            if (w != _weeklyShown)
            {
                _weeklyShown = w;
                _weeklyLines.Clear();
                if (w >= 0)
                {
                    var def = C.Weekly[w];
                    _weeklyName.Set(def.Name);
                    foreach (var line in def.Lines)
                    {
                        bool good = line.StartsWith("+");
                        var l = Ui.Text((good ? "+ " : "- ") + line.Substring(1), "se-body", good ? "se-good" : "se-bad");
                        _weeklyLines.Add(l);
                    }
                }
            }
            _weeklyTime.Set(L.F("Ends in {0}", Num.Time(WeekClock.WeekEnd(G.Week) - G.NowUnix)));

            var s = G.Stats;
            int found = 0;
            foreach (var d in G.Discovered) if (d) found++;
            string[] values =
            {
                Num.Int(s.Storms), Num.Int(s.Strikes), Num.Int(s.MegaStorms), Num.Mult(s.BestChain), Num.Int(s.BoostsUsed),
                Num.Format(s.BestPower) + "/s", Num.Format(G.LifetimeEnergy), G.Prestiges.ToString(),
                found + " / " + C.Phenomena.Length, Num.Time(s.PlaySeconds)
            };
            for (int i = 0; i < values.Length; i++) _stats[i].Set(values[i]);

            var set = App.Save.settings;
            _language.SetButton(Languages.Current.Native);
            _sfx.SetButton(OnOff(set.sfx));
            _music.SetButton(OnOff(set.music));
            _ambience.SetButton(OnOff(set.ambience));
            _quality.SetButton(set.quality == 0 ? L.T("SIMPLE") : set.quality == 1 ? L.T("BALANCED") : L.T("HIGH"));
            _vibrate.SetButton(OnOff(set.vibrate));
            _autoQuality.SetButton(OnOff(set.autoQuality));
            _fps.SetButton(set.frameRate <= 0 ? L.T("30 SAVER") : L.F("{0} FPS", set.frameRate == 1 || App.Perf.MaxRefresh <= 60 ? 60 : App.Perf.MaxRefresh));
            _reminders.SetButton(!set.notifications ? L.T("OFF") : App.Reminders.Supported && !App.Reminders.Allowed ? L.T("NOT ALLOWED") : L.T("ON"));
            _tips.SetButton(OnOff(set.tips));
            _ads?.SetButton(OnOff(set.adConsent == 1));
            _showFps?.SetButton(OnOff(set.showFps));
            _online.SetButton(OnOff(set.online));
            _deleteOnline.SetEnabledState(App.Online.IsOnline);
            _sfxVolume.Set(Mathf.RoundToInt(set.sfxVolume * 100) + "%");
            _musicVolume.Set(Mathf.RoundToInt(set.musicVolume * 100) + "%");
            _notation.SetButton(set.notation == 0 ? "1.23M" : "1.23e6");
            _motion.SetButton(OnOff(set.reduceMotion));
            bool automation = G.Mods != null && G.Mods.Automation;
            _auto.SetButton(!automation ? L.T("LOCKED") : OnOff(G.AutoStorm));
            _auto.SetEnabledState(automation);
            _saveInfo.Set(L.T("Saved locally every few seconds.") + " " + (App.Online.IsOnline ? L.T("Cloud save is on.") : L.T("Cloud save syncs when you are online.")));

            string current = App.Sfx != null ? App.Sfx.CurrentTrack : set.musicTrack;
            bool loading = App.Sfx != null && App.Sfx.TrackLoading;
            foreach (var row in _tracks)
            {
                bool owned = App.IsTrackOwned(row.Track.Id);
                bool playing = set.music && current == row.Track.Id;
                row.Root.EnableInClassList("playing", playing);
                row.Play.Show(owned && !playing);
                row.Unlock.Show(!owned);
                row.Unlock.SetEnabledState(App.CanOfferAd("ad_music", row.Track.Id));
                row.State.Show(playing || !owned);
                row.State.EnableInClassList("se-good", playing);
                row.State.Set(playing ? (loading ? L.T("Tuning in...") : L.T("Now playing")) : L.T("Locked: watch an ad to unlock"));
            }
        }

        static string OnOff(bool on) => on ? L.T("ON") : L.T("OFF");
    }
}
