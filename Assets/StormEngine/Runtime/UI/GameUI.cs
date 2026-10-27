using System;
using System.Collections.Generic;
using StormEngine.Core;
using StormEngine.Online;
using StormEngine.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace StormEngine.UI
{
    /// <summary>
    /// Root of the interface, layered over the 3D world. Builds the responsive layout (landscape:
    /// side nav + side panel, portrait: bottom sheet + tab bar), the home screen, visiting and
    /// decoration editing, locks features behind Engine Levels, routes game events to effects
    /// and runs the refresh loop.
    /// </summary>
    public sealed class GameUI
    {
        readonly StormApp _app;
        readonly VisualElement _docRoot;
        readonly VisualElement _root;
        readonly VisualElement _stage;
        readonly VisualElement _nav;
        readonly VisualElement _panelHost;
        readonly VisualElement _popupLayer;
        readonly VisualElement _hud;
        readonly VisualElement _safeLayer;
        readonly FxLayer _fx;
        readonly TopBar _topBar;
        readonly PowerCard _power;
        readonly StatChips _chips;
        readonly GoalCard _goal;
        readonly DailyButton _daily;
        readonly StormHud _stormHud;
        readonly StormButton _stormButton;
        readonly AdOffers _offers;
        readonly List<BoostButton> _boosts = new List<BoostButton>();
        readonly Toasts _toasts;
        readonly HomeScreen _home;
        readonly VisitBanner _visitBanner;
        readonly EditBar _editBar;
        readonly Coach _coach;
        readonly Tutorials _tutorials;
        /// <summary>Undoes every handler added to the game and the world, so the interface can be rebuilt.</summary>
        readonly List<Action> _unhook = new List<Action>();
        readonly List<PanelView> _panels = new List<PanelView>();
        readonly Dictionary<string, Button> _tabs = new Dictionary<string, Button>();
        readonly Dictionary<string, VisualElement> _badges = new Dictionary<string, VisualElement>();
        readonly Dictionary<string, Label> _tabLabels = new Dictionary<string, Label>();
        readonly Dictionary<string, string> _tabFeature = new Dictionary<string, string>();
        /// <summary>Panels that have a button in the tab bar (the Atmosphere Lab does not).</summary>
        readonly List<string> _navTabs = new List<string>();
        readonly VisualElement _menuCatcher, _menuLayer;
        readonly Label _fpsLabel;
        int _fpsFrames;
        float _fpsTime;
        readonly HashSet<string> _opened = new HashSet<string>();
        readonly Queue<Popup> _popupQueue = new Queue<Popup>();
        Popup _popup;
        VisualElement _scrim;
        PanelView _active;
        bool _portrait;
        bool _collapsed;
        bool _homeShown;
        bool _editing;
        float _textTimer, _panelTimer, _badgeTimer, _pediaTimer;
        double _lastEnergyPop;
        double _lastChainShown;
        bool _wasReady = true;
        Vector4 _insets = new Vector4(-1, -1, -1, -1);
        float _safeTimer;

        WorldView World => _app.World;

        /// <summary>Raised when the player leaves the home screen with PLAY.</summary>
        public event Action PlayPressed;

        public GameUI(StormApp app, VisualElement documentRoot, StyleSheet sheet)
        {
            _app = app;
            _docRoot = documentRoot;
            _docRoot.pickingMode = PickingMode.Ignore;
            _root = Ui.Div("se-root", "landscape");
            _root.pickingMode = PickingMode.Ignore;
            if (sheet != null) _root.styleSheets.Add(sheet);
            // Per-language styling; scripts like Arabic, Hindi and Thai need the advanced text generator.
            _root.AddToClassList("lang-" + Languages.Current.Code);
            _root.EnableInClassList("lang-complex", Languages.Current.Complex);
            documentRoot.Add(_root);
            _root.StretchToParentSize();

            // Navigation. The logo returns to the home screen.
            _nav = Ui.Div("se-nav");
            var logo = Ui.Div("se-nav-logo");
            logo.Add(new Icon("storm", 56));
            logo.RegisterCallback<ClickEvent>(_ =>
            {
                _app.Sfx?.Click();
                ShowHome(true);
            });
            _nav.Add(logo);

            // Stage: transparent window onto the 3D world, with effects and HUD on top.
            _stage = Ui.Div("se-stage");
            _stage.pickingMode = PickingMode.Ignore;
            _fx = new FxLayer();
            _stage.Add(_fx);

            _hud = Ui.Div("se-layer", "se-hud");
            _hud.pickingMode = PickingMode.Ignore;
            _topBar = new TopBar(app);
            _topBar.WeeklyClicked += () =>
            {
                _app.Sfx?.Click();
                var g = _app.Game;
                if (g.TournamentUnlocked) Open("tournament");
                else if (g.Build.Weekly >= 0) Toast("calendar", _app.Content.Weekly[g.Build.Weekly].Name + ": " + string.Join(", ", _app.Content.Weekly[g.Build.Weekly].Lines));
            };
            _topBar.LevelClicked += () =>
            {
                _app.Sfx?.Click();
                ShowPopup(new LevelRoadPopup(_app));
            };
            _topBar.GemsClicked += () =>
            {
                _app.Sfx?.Click();
                OpenShop("gems");
            };
            _topBar.PediaClicked += () => ShowPedia(false);
            _topBar.HomeClicked += () => ShowHome(true);
            _topBar.VisitClicked += () => ShowPopup(new VisitListPopup(_app));
            _topBar.SettingsClicked += () => Open("more");
            _topBar.StatsClicked += () => _chips.Toggle();

            _power = new PowerCard(app) { name = "power-card" };
            _power.Clicked += OpenAtmosphere;
            _chips = new StatChips(app);
            _chips.Clicked += OpenAtmosphere;
            _daily = new DailyButton(app, () =>
            {
                _app.Sfx?.Click();
                _app.ShowDaily();
            });
            _daily.name = "daily-button";
            _chips.Head.Insert(0, _daily);
            _goal = new GoalCard(app) { name = "goal-card" };
            _offers = new AdOffers(app);
            var left = Ui.Div("se-leftcol");
            left.pickingMode = PickingMode.Ignore;
            left.Children(_power, _goal);
            // The Energy Surge offer sits on the right under the gift, balancing the two columns.
            _chips.Insert(1, _offers.Surge);
            _stormHud = new StormHud(app);
            var controls = Ui.Div("se-controls");
            controls.pickingMode = PickingMode.Ignore;
            var boostBar = Ui.Div("se-boostbar");
            boostBar.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < app.Content.Boosts.Length; i++)
            {
                var b = new BoostButton(app, i);
                b.name = "boost-" + i;
                _boosts.Add(b);
                boostBar.Add(b);
            }
            controls.Add(boostBar);
            var stormCol = Ui.Div("se-stormcol");
            stormCol.pickingMode = PickingMode.Ignore;
            _stormButton = new StormButton(app) { name = "storm-button" };
            stormCol.Children(_offers.Storm, _stormButton);
            controls.Add(stormCol);
            // The top bar's dropdown lives in a screen-wide layer above the panel sheet (the stage is
            // clipped where an open panel begins). While it is open, a tap anywhere else closes it.
            _menuCatcher = Ui.Div("se-layer", "se-menucatcher");
            _menuCatcher.RegisterCallback<PointerDownEvent>(_ => _topBar.SetMenu(false, false));
            _menuCatcher.Show(false);
            _topBar.MenuToggled += open => _menuCatcher.Show(open);
            _menuLayer = Ui.Div("se-layer", "se-menulayer");
            _menuLayer.pickingMode = PickingMode.Ignore;
            _menuLayer.Children(_menuCatcher, _topBar.Menu);
            _hud.Children(left, _chips, _stormHud, controls, _topBar);
            // Everything interactive on the stage lives in a layer inset by the device's safe area
            // (notches, punch-hole cameras, rounded corners); the world and effects stay edge to edge.
            _safeLayer = Ui.Div("se-layer", "se-safe");
            _safeLayer.pickingMode = PickingMode.Ignore;
            _safeLayer.Add(_hud);
            _stage.Add(_safeLayer);

            _visitBanner = new VisitBanner();
            _visitBanner.BackClicked += () => _app.EndVisit();
            _visitBanner.Show(false);
            _safeLayer.Add(_visitBanner);
            _editBar = new EditBar(app);
            _editBar.DoneClicked += ExitEditMode;
            _editBar.Show(false);
            _safeLayer.Add(_editBar);

            // Panels. The Atmosphere Lab has no tab: it opens from the Storm Power readout.
            _panelHost = Ui.Div("se-panelhost");
            AddPanel(new MachinesPanel(app), null);
            AddPanel(new AtmospherePanel(app), "atmosphere", false);
            AddPanel(new ShopPanel(app), "customize");
            AddPanel(new ModulesPanel(app), "modules");
            AddPanel(new IslandPanel(app), "island");
            AddPanel(new TournamentPanel(app), "tournament");
            AddPanel(new ResearchPanel(app), "research");
            AddPanel(new MorePanel(app), null);

            _home = new HomeScreen(app);
            _home.PlayClicked += () =>
            {
                _app.Sfx?.Click();
                ShowHome(false);
                PlayPressed?.Invoke();
            };
            _home.TournamentClicked += () =>
            {
                if (!_app.Game.TournamentUnlocked)
                {
                    _app.Sfx?.Deny();
                    Toast("lock", L.F("The Weekly Tournament unlocks at Engine Level {0}. Press PLAY and grind your engine!", _app.Game.FeatureLevel("tournament")));
                    return;
                }
                _app.Sfx?.Click();
                ShowHome(false);
                Open("tournament");
            };
            _home.PediaClicked += ShowPedia;
            _home.DailyClicked += () =>
            {
                _app.Sfx?.Click();
                _app.ShowDaily();
            };
            _home.VisitClicked += () =>
            {
                _app.Sfx?.Click();
                ShowPopup(new VisitListPopup(_app));
            };
            _home.SettingsClicked += () =>
            {
                _app.Sfx?.Click();
                ShowHome(false);
                Open("more");
            };
            _home.Show(false);

            _toasts = new Toasts();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Development builds: optional frame-rate readout (Settings > Developer).
            _fpsLabel = Ui.Text("", "se-fps");
            _fpsLabel.pickingMode = PickingMode.Ignore;
            _fpsLabel.Show(false);
#else
            _fpsLabel = null;
#endif
            _popupLayer = Ui.Div("se-layer", "se-popups");
            _popupLayer.pickingMode = PickingMode.Ignore;
            _coach = new Coach();

            _app.Ads.SimulatedPresenter = done => ShowPopup(new AdSimPopup(done));

            _root.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                UpdateOrientation();
                ApplySafeArea(false);
                if (_homeShown) PlaceMachine();
            });
            _stage.RegisterCallback<GeometryChangedEvent>(_ => PlaceMachine());
            UpdateOrientation(true);
            Open("machines");
            RefreshTabs();
            ApplySettings();
            HookGame();
            _tutorials = new Tutorials(app, _root, _coach, Open);

            if (World != null)
            {
                World.Panel = _root.panel;
                World.IsOverUi = IsOverUi;
                var world = World;
                Action<int> decorTapped = i =>
                {
                    if (!_editing) return;
                    _app.Sfx?.Click();
                    _editBar.Select(i);
                };
                world.Tapped += OnWorldTapped;
                world.DecorTapped += decorTapped;
                world.GroundTapped += OnGroundTapped;
                _unhook.Add(() =>
                {
                    world.Tapped -= OnWorldTapped;
                    world.DecorTapped -= decorTapped;
                    world.GroundTapped -= OnGroundTapped;
                });
                _root.RegisterCallback<AttachToPanelEvent>(_ => World.Panel = _root.panel);
            }
        }

        public StormApp App => _app;

        IntroView _intro;

        /// <summary>
        /// The launch intro (once per app start; not after a language switch). It plays in the world
        /// and ends on the main menu: the menu comes in with it, and popups wait until it is over.
        /// </summary>
        public void PlayIntro()
        {
            if (World == null) return;
            World.PlayIntro();
            _intro = new IntroView(World);
            _root.Add(_intro);
            _home.IntroReveal(0f);
        }

        /// <summary>Detaches this interface from the game and the screen (used when switching language).</summary>
        public void Dispose()
        {
            foreach (var undo in _unhook) undo();
            _unhook.Clear();
            _root.RemoveFromHierarchy();
        }

        public Tutorials Tutorials => _tutorials;

        void AddPanel(PanelView panel, string feature, bool tab = true)
        {
            _panels.Add(panel);
            if (feature != null) _tabFeature[panel.Id] = feature;
            var button = new Button(() => OnTab(panel.Id)) { focusable = false };
            button.AddToClassList("se-tab");
            button.name = "tab-" + panel.Id;
            button.Add(new Icon(panel.IconKind, 34));
            var label = Ui.Text(panel.TabLabel, "se-tab-label").FitWidth(8f);
            button.Add(label);
            _tabLabels[panel.Id] = label;
            var lockIcon = new Icon("lock", 20);
            lockIcon.AddToClassList("se-tab-lock");
            button.Add(lockIcon);
            var badge = Ui.Div("se-badge");
            badge.pickingMode = PickingMode.Ignore;
            badge.Show(false);
            button.Add(badge);
            _badges[panel.Id] = badge;
            _tabs[panel.Id] = button;
            if (!tab) return;
            _nav.Add(button);
            _navTabs.Add(panel.Id);
        }

        bool IsTabUnlocked(string id) => !_tabFeature.TryGetValue(id, out var f) || _app.Game.IsFeatureUnlocked(f);

        void RefreshTabs()
        {
            // Locked tabs stay out of the way: only the next one to unlock is shown, as a teaser.
            string next = null;
            int nextLevel = int.MaxValue;
            foreach (var id in _navTabs)
            {
                if (IsTabUnlocked(id)) continue;
                int level = _app.Game.FeatureLevel(_tabFeature[id]);
                if (level < nextLevel)
                {
                    nextLevel = level;
                    next = id;
                }
            }
            foreach (var p in _panels)
            {
                bool unlocked = IsTabUnlocked(p.Id);
                var tab = _tabs[p.Id];
                tab.EnableInClassList("locked", !unlocked);
                tab.Show(unlocked || p.Id == next);
                _tabLabels[p.Id].Set(unlocked ? p.TabLabel : L.F("Lv {0}", _app.Game.FeatureLevel(_tabFeature[p.Id])));
            }
        }

        void OnTab(string id)
        {
            if (!IsTabUnlocked(id))
            {
                _app.Sfx?.Deny();
                var f = _app.Content.Feature(_tabFeature[id]);
                Toast("lock", L.F("{0} unlocks at Engine Level {1}.", f.Name, f.Level) + " " + f.Desc);
                return;
            }
            _app.Sfx?.Click();
            if (_portrait && _active != null && _active.Id == id)
            {
                SetCollapsed(!_collapsed);
                return;
            }
            Open(id);
        }

        public void Open(string id)
        {
            if (!IsTabUnlocked(id)) return;
            if (_homeShown) ShowHome(false);
            if (_editing) ExitEditMode();
            foreach (var p in _panels)
            {
                if (p.Id != id) continue;
                if (_active != null) _active.RemoveFromHierarchy();
                _active = p;
                _panelHost.Add(p);
                p.OnShow();
                p.Refresh();
            }
            _opened.Add(id);
            foreach (var kv in _tabs) kv.Value.EnableInClassList("selected", kv.Key == id);
            SetCollapsed(false);
        }

        void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            _panelHost.EnableInClassList("collapsed", _collapsed);
            UpdateSheetState();
        }

        /// <summary>Opens the shop on a section: "featured", "styles" or "gems".</summary>
        public void OpenShop(string section)
        {
            if (!IsTabUnlocked("shop"))
            {
                _app.Sfx?.Deny();
                Toast("lock", L.F("The Style Shop unlocks at Engine Level {0}.", _app.Game.FeatureLevel("customize")));
                return;
            }
            Open("shop");
            Panel<ShopPanel>()?.OpenSection(section);
        }

        void OpenAtmosphere()
        {
            _app.Sfx?.Click();
            if (!IsTabUnlocked("atmosphere"))
            {
                Toast("atmosphere", L.F("The Atmosphere Lab unlocks at Engine Level {0}: it breaks down every multiplier.", _app.Game.FeatureLevel("atmosphere")));
                return;
            }
            if (_active != null && _active.Id == "atmosphere" && !_collapsed && _portrait)
            {
                SetCollapsed(true);
                return;
            }
            Open("atmosphere");
        }

        void ShowPedia() => ShowPedia(true);

        void ShowPedia(bool sound)
        {
            if (sound) _app.Sfx?.Click();
            ShowPopup(new StormpediaPopup(_app));
            _pediaTimer = 0.5f;
        }

        void UpdateSheetState()
        {
            _root.EnableInClassList("sheet-open", _portrait && !_collapsed);
            PlaceMachine();
        }

        T Panel<T>() where T : PanelView
        {
            foreach (var p in _panels)
                if (p is T t) return t;
            return null;
        }

        void UpdateOrientation(bool force = false)
        {
            var r = _root.layout;
            bool portrait = !float.IsNaN(r.width) && r.width > 0 && r.height > r.width * 1.05f;
            if (!force && portrait == _portrait) return;
            _portrait = portrait;
            _root.EnableInClassList("portrait", portrait);
            _root.EnableInClassList("landscape", !portrait);
            _root.Clear();
            if (portrait)
                _root.Children(_stage, _panelHost, _nav);
            else
                _root.Children(_nav, _stage, _panelHost);
            _root.Add(_home);
            _root.Add(_menuLayer);
            _root.Add(_toasts);
            _root.Add(_coach);
            _root.Add(_popupLayer);
            if (_fpsLabel != null) _root.Add(_fpsLabel);
            if (_intro != null) _root.Add(_intro);
            if (portrait && !force) _collapsed = true;
            _panelHost.EnableInClassList("collapsed", _collapsed);
            ApplySafeArea(true);
            UpdateSheetState();
        }

        /// <summary>
        /// Keeps the interface inside the safe area. Bars keep their backgrounds out to the screen
        /// edge and pad their content instead, so nothing looks cut off around a notch.
        /// </summary>
        void ApplySafeArea(bool force)
        {
            var panel = _root.panel;
            if (panel == null) return;
            var inset = SafeArea.Panel(panel);
            if (!force && Mathf.Abs(inset.x - _insets.x) + Mathf.Abs(inset.y - _insets.y) + Mathf.Abs(inset.z - _insets.z) + Mathf.Abs(inset.w - _insets.w) < 0.5f) return;
            _insets = inset;
            float l = inset.x, t = inset.y, r = inset.z, b = inset.w;

            foreach (var e in new[] { _nav, _panelHost })
            {
                e.style.width = StyleKeyword.Null;
                e.style.height = StyleKeyword.Null;
                e.style.paddingLeft = StyleKeyword.Null;
                e.style.paddingRight = StyleKeyword.Null;
                e.style.paddingTop = StyleKeyword.Null;
                e.style.paddingBottom = StyleKeyword.Null;
            }
            if (_portrait)
            {
                // Status bar / camera cutout at the top, gesture bar at the bottom.
                SetInsets(_safeLayer, l, t, r, 0);
                _nav.style.height = 84 + b;
                _nav.style.paddingBottom = 8 + b;
                _nav.style.paddingLeft = 4 + l;
                _nav.style.paddingRight = 4 + r;
                _panelHost.style.paddingLeft = l;
                _panelHost.style.paddingRight = r;
            }
            else
            {
                // The cutout sits on the left or right when the phone is turned sideways.
                SetInsets(_safeLayer, 0, t, 0, b);
                _nav.style.width = 98 + l;
                _nav.style.paddingLeft = 6 + l;
                _nav.style.paddingTop = 10 + t;
                _nav.style.paddingBottom = 10 + b;
                _panelHost.style.width = 480 + r;
                _panelHost.style.paddingRight = r;
                _panelHost.style.paddingTop = t;
                _panelHost.style.paddingBottom = b;
            }
            _toasts.style.top = 58 + t;
            _popupLayer.style.paddingLeft = l;
            _popupLayer.style.paddingTop = t;
            _popupLayer.style.paddingRight = r;
            _popupLayer.style.paddingBottom = b;
            _home.style.paddingLeft = 16 + l;
            _home.style.paddingTop = 18 + t;
            _home.style.paddingRight = 16 + r;
            _home.style.paddingBottom = 22 + b;
            PlaceMachine();
        }

        static void SetInsets(VisualElement e, float l, float t, float r, float b)
        {
            e.style.left = l;
            e.style.top = t;
            e.style.right = r;
            e.style.bottom = b;
        }

        /// <summary>Safe-area insets in panel units (left, top, right, bottom).</summary>
        public Vector4 Insets => new Vector4(Mathf.Max(0, _insets.x), Mathf.Max(0, _insets.y), Mathf.Max(0, _insets.z), Mathf.Max(0, _insets.w));

        /// <summary>Frames the 3D machine in the free part of the screen, clear of the HUD.</summary>
        void PlaceMachine()
        {
            if (World == null) return;
            var ins = Insets;
            if (_homeShown)
            {
                var rr = _root.contentRect;
                if (!(rr.width >= 10) || !(rr.height >= 10)) return;
                float top = (_portrait ? 205 : 200) + ins.y;
                float bottom = (_portrait ? 330 : 250) + ins.w;
                var home = new Rect(rr.width * 0.1f, top, rr.width * 0.8f, Mathf.Max(80, rr.height - top - bottom));
                home.position += _root.worldBound.position;
                World.SetViewArea(home, _root.worldBound.size);
                return;
            }
            var r = _stage.contentRect;
            if (!(r.width >= 10) || !(r.height >= 10)) return;
            bool sheetOpen = _portrait && !_collapsed;
            float t = (_portrait ? (sheetOpen ? 110 : 140) : 90) + ins.y;
            float b = _portrait ? 112 : 118 + ins.w;
            Rect area;
            // With a panel open the island shares a short strip with the left HUD column (Storm Power,
            // Goal): frame it a little right of center so the engine stays clear of the goal card.
            if (_portrait && sheetOpen) area = new Rect(r.width * 0.14f, t, r.width * 0.84f, r.height - t - b);
            else if (_portrait) area = new Rect(r.width * 0.02f, t, r.width * 0.96f, r.height - t - b);
            else if (r.width > 900) area = new Rect(r.width * 0.12f, t, r.width * 0.76f, r.height - t - b);
            else area = new Rect(r.width * 0.08f, t, r.width * 0.84f, r.height - t - b);
            if (area.height < 60) area = new Rect(area.x, 20, area.width, r.height - 40);
            area.position += _stage.worldBound.position;
            World.SetViewArea(area, _root.worldBound.size);
        }

        bool IsOverUi(Vector2 panelPos)
        {
            var panel = _root.panel;
            if (panel == null) return false;
            var picked = panel.Pick(panelPos);
            return picked != null && picked != _root && picked != _stage && picked != _docRoot && picked != panel.visualTree && picked != _home;
        }

        void OnWorldTapped(int machine)
        {
            var g = _app.Game;
            if (_popup != null || _homeShown || _editing) return;
            if (machine < 0)
            {
                if (g.CanUnleash) _app.Unleash();
                else if (!g.IsStorming)
                {
                    _app.Sfx?.Click();
                    PopAtDome(L.F("Charging {0}", Num.Percent(g.StormCharge)), "small", 0.1f);
                }
                return;
            }
            _app.Sfx?.Click();
            Open("machines");
            Panel<MachinesPanel>()?.Focus(machine);
        }

        public void ApplySettings()
        {
            var s = _app.Save.settings;
            Haptics.Enabled = s.vibrate;
            _fx.ReduceMotion = s.reduceMotion;
            Num.Notation = s.notation;
            if (World != null)
            {
                World.ReduceMotion = s.reduceMotion;
                World.ApplyQuality(s.quality);
            }
        }

        // ------------------------------------------------------------ home, visiting, editing

        public void ShowHome(bool show)
        {
            _topBar.SetMenu(false, false);
            if (show && _editing) ExitEditMode();
            if (show && World != null && World.IsVisiting) _app.EndVisit();
            _homeShown = show;
            _home.Show(show);
            _root.EnableInClassList("home", show);
            if (show) _home.Refresh();
            PlaceMachine();
        }

        public bool HomeShown => _homeShown;

        /// <summary>Switches the HUD into (or out of) visiting mode.</summary>
        public void SetVisiting(IslandLayout layout)
        {
            bool visiting = layout != null;
            if (visiting && _homeShown)
            {
                _homeShown = false;
                _home.Show(false);
                _root.EnableInClassList("home", false);
            }
            _root.EnableInClassList("visiting", visiting);
            _visitBanner.Show(visiting);
            if (visiting)
            {
                _visitBanner.Set(layout, _app.Content);
                if (_portrait) SetCollapsed(true);
            }
            PlaceMachine();
        }

        public void OnTournamentStarted()
        {
            if (_homeShown) ShowHome(false);
            if (_portrait) SetCollapsed(true);
            Toast("trophy", L.T("Tournament storm! Fire your boosts during the storm to stack chains."), 3.5f);
        }

        public void EnterEditMode()
        {
            if (World == null) return;
            if (World.IsVisiting) _app.EndVisit();
            if (_app.Game.IsStorming)
            {
                _app.Sfx?.Deny();
                Toast("storm", L.T("Wait for the storm to finish before rearranging."));
                return;
            }
            _editing = true;
            World.EditMode = true;
            _root.EnableInClassList("editing", true);
            _editBar.Show(true);
            _editBar.Select(-1);
            if (_portrait) SetCollapsed(true);
            _app.Sfx?.Click();
        }

        public void ExitEditMode()
        {
            if (!_editing) return;
            _editing = false;
            _editBar.Select(-1);
            if (World != null) World.EditMode = false;
            _root.EnableInClassList("editing", false);
            _editBar.Show(false);
            _app.SaveNow(false);
        }

        void OnGroundTapped(Vector2 spot)
        {
            if (!_editing) return;
            int index = _editBar.Selected;
            if (index < 0) return;
            if (World.MovePlacement(index, spot))
            {
                _app.Sfx?.Equip();
                _editBar.Select(index);
            }
            else
            {
                _app.Sfx?.Deny();
                PopAt(spot, L.T("No room there"));
            }
        }

        void PopAt(Vector2 spot, string text)
        {
            if (World == null) return;
            var world = new Vector3(spot.x, 1.2f, spot.y);
            _fx.PopText(text, _fx.WorldToLocal(World.ToPanel(world)), "small");
        }

        // ------------------------------------------------------------ game events

        void HookGame()
        {
            var g = _app.Game;
            Action<StormSim> stormStarted = _ =>
            {
                _lastChainShown = 1;
                _app.Sfx?.StormStart();
                _fx.Flash(0.25f);
                if (_editing) ExitEditMode();
            };
            Action<int> lightning = n =>
            {
                _fx.Flash(0.12f + 0.05f * n);
                _app.Sfx?.Thunder();
                var storm = _app.Game.Storm;
                if (storm == null) return;
                double step = Math.Floor(storm.Chain * 2) / 2;
                if (step > _lastChainShown)
                {
                    _lastChainShown = step;
                    PopAtDome(L.F("CHAIN {0}!", Num.Mult(step)), "mult", 0.5f);
                }
            };
            Action<StormResult, RecordFlags, StormSubmission> stormEnded = (r, flags, submission) =>
            {
                _app.Sfx?.StormEnd();
                PopAtDome("+" + Num.Format(r.Score), "big", 0.2f);
                string cls = StormSubmission.ClassName(_app.Content, r.ClassIndex);
                bool showCard = r.Tournament || _app.Game.Stats.Storms == 1 || (flags.FirstOfClass && r.ClassIndex >= 0);
                if (showCard) ShowPopup(new StormResultPopup(_app, r, flags));
                else if (flags.AllTime) Toast("trophy", L.F("New personal best! {0} scored {1}  (+{2} cores)", cls, Num.Format(r.Score), r.Cores));
                else Toast("storm", L.F("{0} storm: {1}  (+{2} cores)", cls, Num.Format(r.Score), r.Cores), 2.2f);
            };
            Action<int> discovered = p =>
            {
                _app.Sfx?.Discovery();
                ShowPopup(new DiscoveryPopup(_app, p));
            };
            Action<GoalDef> goalCompleted = goal =>
            {
                _app.Sfx?.Goal();
                var parts = new List<string>();
                if (goal.RewardGems > 0) parts.Add(L.F("+{0} gems", goal.RewardGems));
                if (goal.RewardCores > 0) parts.Add(L.F("+{0} cores", goal.RewardCores));
                if (goal.RewardXp > 0) parts.Add(L.F("+{0} XP", goal.RewardXp));
                Toast("check", L.F("Goal complete: {0}", goal.Text) + (parts.Count > 0 ? "  (" + string.Join(", ", parts) + ")" : ""));
            };
            Action<int> machineUnlocked = m => Toast("machines", L.F("New machine available: {0}", _app.Content.Machines[m].Name));
            Action<int> missionCompleted = slot =>
            {
                var m = _app.Game.MissionAt(slot);
                if (m == null) return;
                _app.Sfx?.Goal();
                Toast("gift", L.F("Daily mission done: {0}. Tap the gift to collect!", m.Text));
            };
            Action dayChanged = () =>
            {
                if (_app.Game.DailyUnlocked) Toast("gift", L.T("A new day! Your daily gift and fresh missions are waiting."), 4f);
            };
            Action<int> boostActivated = b =>
            {
                _app.Sfx?.Boost();
                var btn = _boosts[b];
                var center = btn.LocalToWorld(btn.contentRect.center);
                Burst(center, Palette.Category[Mathf.Clamp(Content.StatCat(_app.Content.Boosts[b].Target), 0, 4)], 14);
            };
            Action<int> leveledUp = level =>
            {
                _app.Sfx?.LevelUp();
                World?.Burst(World.DomeWorld, Palette.Yellow, 60, 8f);
                RefreshTabs();
                ShowPopup(new LevelUpPopup(_app, level));
                _app.SaveNow(false);
            };
            Action<int> bundleCompleted = b =>
            {
                _app.Sfx?.Discovery();
                if (World != null && !World.IsVisiting)
                    foreach (var id in _app.Content.Bundles[b].Decorations) World.AutoPlace(id);
                ShowPopup(new BundleCompletePopup(_app, b));
            };

            g.StormStarted += stormStarted;
            g.LightningStruck += lightning;
            g.StormEnded += stormEnded;
            g.PhenomenonDiscovered += discovered;
            g.GoalCompleted += goalCompleted;
            g.MachineUnlockedEvent += machineUnlocked;
            g.MissionCompleted += missionCompleted;
            g.DayChanged += dayChanged;
            g.BoostActivated += boostActivated;
            g.LeveledUp += leveledUp;
            g.BundleCompleted += bundleCompleted;
            _unhook.Add(() =>
            {
                g.StormStarted -= stormStarted;
                g.LightningStruck -= lightning;
                g.StormEnded -= stormEnded;
                g.PhenomenonDiscovered -= discovered;
                g.GoalCompleted -= goalCompleted;
                g.MachineUnlockedEvent -= machineUnlocked;
                g.MissionCompleted -= missionCompleted;
                g.DayChanged -= dayChanged;
                g.BoostActivated -= boostActivated;
                g.LeveledUp -= leveledUp;
                g.BundleCompleted -= bundleCompleted;
            });
        }

        void PopAtDome(string text, string cls, float spread)
        {
            if (World == null || World.IsVisiting) return;
            var local = _fx.WorldToLocal(World.ToPanel(World.DomeWorld));
            float r = World.DomePanelRadius();
            local += new Vector2(UnityEngine.Random.Range(-r, r) * spread, -r * 0.3f + UnityEngine.Random.Range(-r, r) * spread * 0.5f);
            _fx.PopText(text, local, cls);
        }

        void Burst(Vector2 panelPos, Color color, int count)
        {
            _fx.Burst(_fx.WorldToLocal(panelPos), color, count);
        }

        public void PurchaseFx(VisualElement source)
        {
            if (source == null) return;
            Burst(source.LocalToWorld(source.contentRect.center), Palette.Yellow, 10);
        }

        // ------------------------------------------------------------ popups & toasts

        public void ShowPopup(Popup p)
        {
            _topBar.SetMenu(false, false);
            if (_popup != null || _intro != null)
            {
                _popupQueue.Enqueue(p);
                return;
            }
            _popup = p;
            _scrim = Ui.Div("se-scrim");
            _scrim.RegisterCallback<ClickEvent>(_ =>
            {
                if (_popup != null && _popup.DismissOnScrim) _popup.Close();
            });
            _popupLayer.pickingMode = PickingMode.Position;
            _popupLayer.Add(_scrim);
            _popupLayer.Add(p);
            p.Closed = ClosePopup;
            _app.Sfx?.Popup();
            p.schedule.Execute(() => p.AddToClassList("shown")).StartingIn(16);
        }

        void ClosePopup()
        {
            if (_popup == null) return;
            _app.Sfx?.Click();
            _popup.RemoveFromHierarchy();
            _scrim?.RemoveFromHierarchy();
            _popup = null;
            _popupLayer.pickingMode = PickingMode.Ignore;
            if (_popupQueue.Count > 0) ShowPopup(_popupQueue.Dequeue());
        }

        public void Toast(string icon, string text, float seconds = 3f) => _toasts.Show(icon, text, seconds);

        public void ShowNamePopup() => ShowPopup(new NamePopup(_app));

        public void ShowBuildView(BoardEntry e) => ShowPopup(new BuildViewPopup(_app, e));

        public void ShowOffline(OfflineReport r) => ShowPopup(new OfflinePopup(_app, r));

        public void ShowWelcome() => ShowPopup(new WelcomePopup(_app));

        public void Confirm(string title, string text, string confirm, Action onConfirm, string cls = "green", Action onCancel = null) =>
            ShowPopup(new ConfirmPopup(title, text, confirm, onConfirm, cls, L.T("Cancel"), onCancel));

        // ------------------------------------------------------------ loop

        public void Tick(float dt)
        {
            var g = _app.Game;

            if (_intro != null)
            {
                _intro.Tick();
                _home.IntroReveal(World != null ? World.IntroTime : -1f);
                if (_intro.Done)
                {
                    _intro.RemoveFromHierarchy();
                    _intro = null;
                    _home.IntroReveal(-1f);
                    if (_popup == null && _popupQueue.Count > 0) ShowPopup(_popupQueue.Dequeue());
                }
            }

            if (_fpsLabel != null)
            {
                bool showFps = _app.Save.settings.showFps;
                _fpsLabel.Show(showFps);
                if (showFps)
                {
                    _fpsFrames++;
                    _fpsTime += Time.unscaledDeltaTime;
                    if (_fpsTime >= 0.5f)
                    {
                        _fpsLabel.Set(Mathf.RoundToInt(_fpsFrames / _fpsTime) + " FPS  " + (1000f * _fpsTime / _fpsFrames).ToString("0.0") + " ms");
                        _fpsFrames = 0;
                        _fpsTime = 0;
                    }
                }
            }

            _fx.Rain = g.IsStorming && (World == null || !World.IsVisiting) ? 0.35f : 0f;
            _fx.Tick(dt);
            if (_app.Sfx != null) _app.Sfx.StormLevel = World != null ? World.StormLevel : (g.IsStorming ? 1 : 0);
            bool ready = g.CanUnleash;
            if (ready && !_wasReady && !g.AutoStormActive) _app.Sfx?.Charged();
            _wasReady = ready;
            _toasts.Tick(dt);
            _popup?.Tick(dt);
            _coach.Insets = Insets;
            _tutorials.Tick(dt, _popup != null || _homeShown || _editing || (World != null && World.IsVisiting));
            _safeTimer -= dt;
            if (_safeTimer <= 0)
            {
                // Rotation, split screen or a foldable changing shape can move the safe area.
                _safeTimer = 0.5f;
                ApplySafeArea(false);
            }

            _stormButton.Tick(dt);
            foreach (var b in _boosts) b.Refresh();
            _offers.Tick(dt);

            _textTimer -= dt;
            if (_textTimer <= 0)
            {
                _textTimer = 0.1f;
                _topBar.Refresh(0.1f);
                _daily.Refresh();
                _power.Refresh();
                _chips.Refresh();
                _goal.Refresh();
                _stormHud.Refresh();
                _goal.Show(!g.IsStorming && g.CurrentGoal != null);
                _root.EnableInClassList("storming", g.IsStorming);
                if (_homeShown) _home.Refresh();
            }

            _panelTimer -= dt;
            if (_panelTimer <= 0 && _active != null && !_collapsed)
            {
                _panelTimer = 0.2f;
                _active.Refresh();
            }

            _badgeTimer -= dt;
            if (_badgeTimer <= 0)
            {
                _badgeTimer = 1f;
                RefreshTabs();
                foreach (var p in _panels)
                {
                    bool unlocked = IsTabUnlocked(p.Id);
                    bool fresh = unlocked && _tabFeature.ContainsKey(p.Id) && !_opened.Contains(p.Id) && !_app.Save.seenFeatures.Contains(p.Id);
                    _badges[p.Id].Show(unlocked && p != _active && (fresh || p.HasAttention()));
                }
                if (_active != null && !_app.Save.seenFeatures.Contains(_active.Id)) _app.Save.seenFeatures.Add(_active.Id);
            }

            _pediaTimer -= dt;
            if (_pediaTimer <= 0)
            {
                _pediaTimer = 4f;
                _root.EnableInClassList("pedia-new", !(_popup is StormpediaPopup) && Stormpedia.CountNew(_app) > 0);
            }

            // Passive income sparkle from the dome.
            if (!g.IsStorming && !_homeShown && Time.unscaledTime - _lastEnergyPop > 2.5f && g.EnergyPerSecond > 0)
            {
                _lastEnergyPop = Time.unscaledTime;
                PopAtDome("+" + Num.Format(g.EnergyPerSecond * 2.5), null, 0.5f);
            }
        }
    }
}
