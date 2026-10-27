using System;
using System.Collections.Generic;
using StormEngine.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Guided tips. New players get a short walkthrough of the core loop; every feature that unlocks
    /// later gets a one-card introduction pointing at where it lives. Tips wait until no popup, home
    /// screen or storm is in the way, and each is shown once (Settings can replay them).
    /// </summary>
    public sealed class Tutorials
    {
        sealed class Step
        {
            public string Title;
            public string Text;
            /// <summary>Element name to highlight (null = a centred card).</summary>
            public Func<string> Target;
            /// <summary>Action steps finish by themselves when this becomes true.</summary>
            public Func<bool> Done;
            public Action Enter;
        }

        sealed class Tip
        {
            public string Id;
            public Func<bool> Ready;
            /// <summary>Shown even to players who unlocked it before tips existed.</summary>
            public bool NewFeature;
            public Step[] Steps;
        }

        readonly StormApp _app;
        readonly VisualElement _root;
        readonly Coach _coach;
        readonly List<Tip> _tips = new List<Tip>();
        Tip _active;
        int _step;
        float _wait = 1.5f;
        bool _shown;

        Game G => _app.Game;
        List<string> Seen => _app.Save.tutorials;

        public Tutorials(StormApp app, VisualElement root, Coach coach, Action<string> openTab)
        {
            _app = app;
            _root = root;
            _coach = coach;
            _coach.NextClicked += Next;
            _coach.SkipClicked += Finish;
            Define(openTab);
            Migrate();
        }

        public bool Active => _active != null;

        void Define(Action<string> openTab)
        {
            var c = _app.Content;
            int pump = c.MachineIndex("pump"), hydro = c.MachineIndex("hydro");

            Add("first_steps", () => G.Stats.Storms == 0 && G.Build.Machines[pump] < 3, steps: new[]
            {
                new Step
                {
                    Title = L.T("Build your engine"),
                    Text = L.T("Tap BUY on the Pressure Pump a few times. Machines make Storm Power, and Storm Power makes Energy."),
                    Target = () => "buy-pump", Enter = () => openTab("machines"), Done = () => G.Build.Machines[pump] >= 3
                },
                new Step
                {
                    Title = L.T("Storm Power"),
                    Text = L.T("This is your Storm Power: the Energy you earn every second. Spend Energy on machines to raise it."),
                    Target = () => "power-card"
                },
                new Step
                {
                    Title = L.T("Mix the weather"),
                    Text = L.T("Each machine powers a different part of the weather, and the parts multiply each other. Build a Hydro Pump!"),
                    Target = () => "buy-hydro", Enter = () => openTab("machines"), Done = () => G.Build.Machines[hydro] >= 1
                },
                new Step
                {
                    Title = L.T("Unleash a storm"),
                    Text = L.T("The storm chamber is charging. When it is full, tap UNLEASH (or the glass dome) for a big burst of Energy."),
                    Target = () => "storm-button", Done = () => G.Stats.Storms >= 1
                },
                new Step
                {
                    Title = L.T("Goals"),
                    Text = L.T("Follow the goal card for rewards. Everything you do gives XP to raise your Engine Level."),
                    Target = () => "goal-card"
                },
                new Step
                {
                    Title = L.T("Engine Level"),
                    Text = L.T("New levels unlock new machines, features and styles. Tap the level ring any time to see what comes next."),
                    Target = () => "level-pill"
                },
            });

            Add("daily", () => G.DailyUnlocked, newFeature: true, steps: new[]
            {
                new Step
                {
                    Title = L.T("Daily Rewards"),
                    Text = L.T("Open the gift every day for a reward. Keep your streak going and finish three daily missions for Gems."),
                    Target = () => "daily-button"
                }
            });

            Add("boosts", () => FirstBoost() >= 0, steps: new[]
            {
                new Step
                {
                    Title = L.T("Boosts"),
                    Text = L.T("Boosts supercharge one part of your engine for a short time. Fire them while a storm rages to build big chain reactions!"),
                    Target = () => "boost-" + Math.Max(0, FirstBoost())
                }
            });

            Add("atmosphere", () => G.IsFeatureUnlocked("atmosphere"), steps: new[]
            {
                new Step
                {
                    Title = L.T("Atmosphere Lab"),
                    Text = L.T("Tap your Storm Power any time to see exactly how it is calculated and to forecast your next storm."),
                    Target = () => "power-card"
                }
            });

            Add("shop", () => G.IsFeatureUnlocked("customize"), steps: new[]
            {
                new Step
                {
                    Title = L.T("Style Shop"),
                    Text = L.T("Styles change how your engine looks, and every style also gives a small bonus. Gems buy the fanciest ones."),
                    Target = () => "tab-shop"
                }
            });

            Add("modules", () => G.IsFeatureUnlocked("modules"), steps: new[]
            {
                new Step
                {
                    Title = L.T("Modules"),
                    Text = L.T("Modules are special gear paid for with Storm Cores. Equip them in your slots to bend the rules of the formula."),
                    Target = () => "tab-modules"
                }
            });

            Add("island", () => G.IsFeatureUnlocked("island"), steps: new[]
            {
                new Step
                {
                    Title = L.T("Island Decor"),
                    Text = L.T("Decorate your island! Every different decoration adds a little Energy, and other players can fly over to visit."),
                    Target = () => "tab-island"
                }
            });

            Add("tournament", () => G.TournamentUnlocked, steps: new[]
            {
                new Step
                {
                    Title = L.T("Weekly Tournament"),
                    Text = L.T("Your engine can now compete on the global leaderboard. You get 3 attempts a day, and everyone earns Gems when the week ends."),
                    Target = () => "tab-tournament"
                },
                new Step
                {
                    Title = L.T("Weekly conditions"),
                    Text = L.T("Every Monday a new weather condition changes which build is best. Check it before you enter.")
                }
            });

            Add("offline", () => G.IsFeatureUnlocked("offline"), steps: new[]
            {
                new Step
                {
                    Title = L.T("Offline Generator"),
                    Text = L.T("Build the Offline Generator in the Machines tab and your engine keeps earning while you are away."),
                    Target = () => "tab-machines"
                }
            });

            Add("research", () => G.IsFeatureUnlocked("research"), steps: new[]
            {
                new Step
                {
                    Title = L.T("Research Lab"),
                    Text = L.T("Reset the atmosphere to earn Research: permanent upgrades that make every run faster. Modules, Storm Cores and records are kept."),
                    Target = () => "tab-research"
                }
            });

            Add("pedia", () => Array.IndexOf(G.Discovered, true) >= 0, steps: new[]
            {
                new Step
                {
                    Title = L.T("Stormpedia"),
                    Text = L.T("You discovered a phenomenon! Everything you unlock is explained in the Stormpedia. Find it in the menu at the top right."),
                    Target = () => "menu-button"
                }
            });
        }

        void Add(string id, Func<bool> ready, Step[] steps, bool newFeature = false) =>
            _tips.Add(new Tip { Id = id, Ready = ready, Steps = steps, NewFeature = newFeature });

        int FirstBoost()
        {
            for (int b = 0; b < _app.Content.Boosts.Length; b++)
                if (G.IsBoostUnlocked(b)) return b;
            return -1;
        }

        /// <summary>Players from before tips existed only get tips for things that are new to them.</summary>
        void Migrate()
        {
            if (_app.Save.tutorialsMigrated) return;
            _app.Save.tutorialsMigrated = true;
            foreach (var tip in _tips)
                if (!tip.NewFeature && !Seen.Contains(tip.Id) && tip.Ready()) Seen.Add(tip.Id);
        }

        /// <summary>Shows every tip again as its feature comes up.</summary>
        public void Replay()
        {
            Seen.Clear();
            _app.Save.settings.tips = true;
            _wait = 1f;
        }

        /// <summary>Call every frame. <paramref name="blocked"/>: a popup, the home screen, visiting or editing is in the way.</summary>
        public void Tick(float dt, bool blocked)
        {
            if (_active == null)
            {
                if (!_app.Save.settings.tips || blocked || G.IsStorming) return;
                _wait -= dt;
                if (_wait > 0) return;
                _wait = 1f;
                foreach (var tip in _tips)
                {
                    if (Seen.Contains(tip.Id) || !tip.Ready()) continue;
                    Start(tip);
                    break;
                }
                return;
            }

            var step = _active.Steps[_step];
            // Storms play out uncovered; an action step waiting for one finishes when it ends.
            if (blocked || G.IsStorming)
            {
                ShowCoach(false);
                return;
            }
            if (step.Done != null && step.Done())
            {
                Next();
                return;
            }
            ShowCoach(true);
            _coach.Place(TargetRect(step), dt);
        }

        void Start(Tip tip)
        {
            _active = tip;
            _step = 0;
            EnterStep();
        }

        void EnterStep()
        {
            var step = _active.Steps[_step];
            step.Enter?.Invoke();
            _coach.SetStep(step.Title, step.Text, step.Done != null, _step == _active.Steps.Length - 1, _step, _active.Steps.Length);
            _app.Sfx?.Popup();
        }

        void Next()
        {
            if (_active == null) return;
            _app.Sfx?.Click();
            _step++;
            // Action steps that are already done (the player got ahead) are skipped.
            while (_step < _active.Steps.Length && _active.Steps[_step].Done != null && _active.Steps[_step].Done()) _step++;
            if (_step >= _active.Steps.Length)
            {
                Finish();
                return;
            }
            EnterStep();
        }

        void Finish()
        {
            if (_active == null) return;
            if (!Seen.Contains(_active.Id)) Seen.Add(_active.Id);
            _active = null;
            _wait = 2f;
            ShowCoach(false);
            _app.SaveNow(false);
        }

        void ShowCoach(bool show)
        {
            if (_shown == show) return;
            _shown = show;
            _coach.Show(show);
            _coach.pickingMode = PickingMode.Ignore;
        }

        Rect? TargetRect(Step step)
        {
            string name = step.Target?.Invoke();
            if (name == null) return null;
            var e = _root.Q(name);
            if (e == null || !IsVisible(e)) return null;
            var wb = e.worldBound;
            if (float.IsNaN(wb.width) || wb.width < 2 || wb.height < 2) return null;
            return _coach.WorldToLocal(wb);
        }

        static bool IsVisible(VisualElement e)
        {
            for (var p = e; p != null; p = p.parent)
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return false;
            return e.panel != null;
        }
    }
}
