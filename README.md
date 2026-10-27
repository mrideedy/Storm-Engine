# STORM ENGINE

**Build. Optimize. Create the biggest storms.**
A cartoon idle / optimization game for mobile and PC (Unity 6.3), playable online or offline. Grind
your engine in **PLAY**, then take it into the server-verified **Weekly TOURNAMENT**.

> Setup, online services and tools: see [STORM_ENGINE_SETUP.md](STORM_ENGINE_SETUP.md).

---

## The game

You run a ridiculous machine that manufactures storms on a floating island. Everything multiplies:

```
Storm Power = Base Pressure x Temperature x Humidity x Wind x Cloud x Electricity x Synergy x Engine Efficiency
```

- **Machines** (Energy): the Pressure Pump adds flat Base Pressure; the Hydro Pump, Cloud Condenser,
  Tesla Coil, Thermal Furnace and Wind Turbine each add +5% per level to their category. Milestones
  (Lv 25, 50, 100, ...) multiply a category by x1.5. The Storm Chamber lengthens and speeds up storms.
  Categories multiply each other, so the same +5% is worth most in your weakest category.
- **Phenomena** (the synergies) switch on when every ingredient category reaches a threshold:
  Thunderstorm, Heat Storm, Monsoon, Cyclone, Supercell, Overcharge and the Chaos-only Anomaly.
  Each one multiplies Storm Power and grows with its weakest ingredient. The strongest one sets
  your **storm class**. Phenomena are *discovered* by reaching them, never bought.
- **Storms**: the chamber charges (60s base). Tap the dome (or UNLEASH) for a 12s+ storm on a fixed
  10 Hz tick. Storm Score = Storm Power x Intensity x Chain Reaction x Mega Storm x Perfect Window,
  summed over every tick. Lightning strikes grow the chain. The dominant phenomenon adds a perk.
- **Boosts**: Cloud Burst, Arc Overload, Heat Surge and Jetstream are timed boosts with cooldowns
  (each unlocks at level 10 of its machine). Overlapping 3+ is a *Perfect Window*; boosts can push a
  category past a threshold *during* a storm. That timing is the skill game.
- **Modules** (Storm Cores): 12 pieces of equipment with limited slots, from the Atmospheric
  Condenser to the Chaos Reactor (huge power, but storms build instability and collapse at 100%).
- **Research** (prestige): "Reset the Atmosphere" turns this run's Energy into Research. Research
  buys Engine Efficiency, masteries, Storm Automation, module slots, longer storms and more.
- **Weekly conditions** rotate every Monday (UTC) from Engine Level 8 and change the best build.
- **Goals** guide new players through every system (some pay Gems).

### Home screen: PLAY and TOURNAMENT

The game opens on a title screen over your live island. **PLAY** is the idle game where you grind.
**TOURNAMENT** (Engine Level 8) opens the weekly tournament and its leaderboards. The home screen
also links the **Stormpedia**, **Visit islands** and **Settings**; the logo (or the house button)
brings you back.

The in-game HUD is deliberately small: a level ring, Energy, Storm Cores, Research, Gems (with a
"+" to the gem shop), and two round buttons (Stormpedia, home). The Storm Power readout opens the
Atmosphere Lab; the category chips hide behind a toggle.

### Engine Level: things unlock as you play

Building, storming, discovering and completing goals earn **XP**. Each Engine Level opens something
new, so a new player starts with just the Pressure Pump, the Hydro Pump and storms:

| Level | Unlocks |
| --- | --- |
| 1 | Pressure Pump, Hydro Pump, storms |
| 2 | Cloud Condenser |
| 3 | Tesla Coil, Atmosphere Lab (formula breakdown) |
| 2 | Free rewards (rewarded ads) |
| 4 | Style Shop |
| 5 | Thermal Furnace, Module Workshop |
| 6 | Storm Chamber, Island Decor, Volcano Forge ad bundle |
| 7 | Wind Turbine |
| 8 | Weekly Tournament, Weekly Conditions, first island expansion |
| 10 | Offline Generator |
| 11 | Research Lab (prestige) |
| 9, 12-20 | Further modules, research nodes, styles, decorations, bundles and expansions |

Locked tabs show the level they open at; tap the level ring for the full road map. Each level also
pays Gems.

### Offline progress is earned

Nothing is produced offline until you build the **Offline Generator** (Engine Level 10). It is a
permanent upgrade that survives resets and costs 500M Energy plus 40 Storm Cores. Level 1 runs for
1 hour at 20% efficiency; each level adds an hour and 5% (up to level 8, with steeply rising costs).
Research (Offline Capacitor) and the Rain Collector module extend it further.

### Style Shop: every style is useful

Five slots: **machine skins** (each with its own silhouette: ship hull, gear crown, onion domes,
neon spires, pagoda roofs, rocket capsule...), **storm clouds** (puffy, wisps, flames, crystals,
petals, cubes), **lightning** (classic, heavy, zig-zag, twin bolts), **worlds** (sky, terrain and
props) and **titles**. Every item except the defaults multiplies one stat while equipped, e.g. Ocean
Breeze Humidity x1.10, Crystal Spire Electricity x1.30, Tempest storm duration x1.20, titles Storm
Core gain.

- Most items cost **Gems**; some are earned by discoveries or resets.
- **Exclusives** (Brass Steamworks, Crystal Spire, Sky Rocket, Tempest, Thunder Hammer, Alien Planet,
  Tournament Champion) are Gem-only showpieces with the strongest bonuses.
- **Ads-only bundles** (Volcano Forge, Sakura Garden, Neon Nights) unlock a complete environment
  (skin, clouds, lightning, world, title and decorations) after 12/16/20 rewarded ads.

### Gems, store and free rewards

Gems are the premium currency: earned from levels, discoveries, goals, free rewards and tournament
prizes, or bought as packs (plus a one-time Starter Pack) through **Unity IAP**. Rewarded ads give
Gems, an Energy Surge (x2 income for 15 minutes, stacking to an hour), an instant storm charge,
double Storm Cores after a storm, extra tournament attempts, bundle progress and double offline
earnings. Each has a daily limit. In the Editor and development builds the store and ads are
simulated so every flow can be tested.

### Daily Rewards

From Engine Level 2 a gift button (top right, and on the home screen) opens **Daily Rewards**:

- A **login gift** every UTC day on a seven-day cycle (Gems, Storm Cores, a 30-minute Energy
  Surge, and a big Day 7 chest). Collecting on consecutive days builds a **streak**; missing a
  day resets it, but a streak lost after one or two missed days can be won back with one ad.
- **Three daily missions**, the same for everyone on a given day but only ones you can already do
  (unleash storms, land lightning strikes, buy machine levels, fire boosts mid-storm, phenomenon
  storms, chain reactions, Mega Storms, modules, decorations, the tournament). Each pays Gems,
  Storm Cores or a surge, and finishing all three pays a bonus. Storm Core rewards grow with
  Engine Level.
- The window opens by itself once a day when a gift is waiting; missions refresh at midnight UTC.

### Island Decor and visiting

Decorate the island on land, in the air and on ponds (31 decorations, including limited weekly ones
and bundle exclusives). Each *different* decoration you own adds a small Energy income bonus (never
Storm Power or scores). Arrange them in **Edit layout** (tap to select, tap the ground to move,
rotate or store). **Terrain expansions** (Gems) make the island bigger and hold more decorations.
Your island is published to the cloud; anyone can fly over to see it from the leaderboards, and
three showcase islands are always available.

### Weekly Tournament

Grinding in PLAY makes your engine stronger; the tournament is where it counts:

- Your machines, modules and research count exactly as built; this week's condition always applies.
- Style bonuses count but are capped at x1.10 each; decorations, surges and running boosts do not.
- The storm starts instantly with fresh boosts; you fire them during the storm.
- 3 attempts per day (UTC) plus up to 2 from ads; your best storm is your score.
- Only tournament storms are submitted. They are replayed and verified by the server, then ranked
  on the **weekly**, **all-time** and **storm class** boards (with rival, Build View and "Visit
  island"). When the week closes you get Gems by rank: #1 1000, top 3 600, top 10 350, top 50 150,
  top 100 80, everyone 30.

### Stormpedia

An in-game encyclopedia of every mechanic, machine, boost, phenomenon, module, research node,
weekly condition, style and decoration. Only what you have unlocked is readable; everything else is
"???" with a hint on how to find it. New entries get a red dot.

## Presentation

- **2.5D diorama**: the machine sits on a floating island rendered in 3D with URP: custom toon
  shaders with ink outlines, soft shadows, bloom, a gradient sky, drifting clouds, a sea of clouds,
  distant islets, rain, sky-to-antenna lightning and screen shake. Every machine is modelled
  procedurally and animates with the stats (turbine speed follows Wind, the furnace glows, the
  Tesla coil arcs, and the dome's cloud darkens as it charges). Drag to orbit, scroll or pinch to
  zoom, tap the dome to unleash, tap a machine to open its card.
- **Graphics quality** (More > Settings): **High** adds dense swaying grass and wildflowers, world
  particles (pollen, fireflies, snowflakes, embers, sakura petals, sea sparkles), rain splashes,
  crisp soft shadows fitted to the island, a warm/cool colour grade and (on PCs and tablets in the
  Editor) a tilt-shift blur on the distant scenery. **Balanced** keeps about half the detail with hard
  shadows. **Simple** (no post-processing or real shadows) still gets soft contact shadows, a
  sky/ground colour tint and extra vibrance. Phones with under 4 GB of memory start on Balanced, and
  phones render the 3D scene a little below native resolution for a steady frame rate.
- **Interface**: big, thumb-friendly HUD (the phone layout is 440 units wide), one menu button for
  Stormpedia, atmosphere stats, the weekly condition, island visits, settings and home; the tab bar
  shows unlocked features plus the next one to unlock.
- **12 languages** (picked from the phone, changeable in Settings) and guided tips for every new feature.
- **Sound**: every effect (FM bells, heavy thunder with a tearing crack, boom and rolling rumble,
  distant thunder during storms, whooshes, fanfares) and weather ambience (breeze and birds,
  crossfading to rain) is synthesized. **Music**: five soft, low-passed stereo tracks in More >
  Music: Drifting Clouds and Starlit Sky (ambient), Cozy Hearth, Rainy Window and Morning Meadow
  (cozy). Two are free; the others unlock for good with a short rewarded ad.
- **Every screen shape**: the interface stays inside the device's safe area (notches, punch-hole
  cameras, rounded corners, gesture bars) in both orientations, while the 3D world still fills the
  screen. Android tablets get a roomier portrait layout.
- **Phone comforts** (More > Settings): **Vibration** (light ticks on taps and purchases, a double
  buzz on "no", a heavy hit for thunder and boosts, a pattern for level-ups; Android 8+ uses
  amplitude effects, Android 10+ the crisp system clicks). **Frame rate**: Battery saver (30),
  Standard (60, easing to 30 after 90 s without a touch, never during storms) or Smooth (up to the
  screen's 90/120 Hz). The game drops to 30 FPS on a low battery, and **Auto-adjust graphics** steps
  quality down once if a phone keeps missing its frame rate. **Reminders** (Android): the next daily
  gift, the tournament closing, a new tournament week and a full Offline Generator, never between
  21:00 and 9:00. The game asks in its own words after your first daily gift before Android shows
  its permission prompt.
- There are no external art or audio assets to license; swap in authored ones any time.

## Project layout

```
Assets/StormEngine/
  Core/        Pure C# game model (no UnityEngine): content, formula engine, storm simulation,
               game logic, levels/unlocks, save model, submission + validator. Shared by the
               client, tests and tools.
  Runtime/     Unity layer: StormApp (loop, save, offline), World/ (3D diorama, camera, effects),
               Audio/ (synthesizer, sound bank), UI/ (UI Toolkit HUD, panels, popups).
  Online/      Unity Gaming Services backend (auth, Cloud Code, Leaderboards, Cloud Save,
               public island layouts, archived weekly ranks).
  Store/       Unity IAP 5 backend for the store (compiles only when com.unity.purchasing is installed).
  Ads/         Unity LevelPlay rewarded ads (compiles only when com.unity.services.levelplay is installed).
  Branding/    App icon layers (generated by Tools/marketing/make_icon.py).
  Notify/      Android reminders via com.unity.mobile.notifications (compiles only when installed),
               plus Editor/ tooling that draws and registers the notification icons.
  Server/      Cloud Code validator (SubmitStorm.js), leaderboard configs, access policy.
  Editor/      Scene + render pipeline setup, Cloud Code content export, debug helpers.
  Tests/       EditMode tests for the core, daily rewards and reminders (51).
  Resources/   Shaders, UI stylesheet, panel settings, theme.
  Settings/    Universal Renderer asset used by the diorama.
  Scenes/      StormEngine.unity
Tools/
  balance-sim/     Bot-driven balance simulator + storm test-vector generator (.NET).
  cloudcode-tests/ Node test that proves the JS validator matches the C# client.
  loc/             Translation pipeline: extract.py, src/<lang>.N.txt, build.py.
  marketing/       Icon, store screenshots, feature graphic and store listing generators.
Docs/
  PlayStore/       Launch checklist, Play Console answers, backend and ads setup, store listing.
  Marketing/       Marketing kit (pitch, tester recruitment, launch posts, video plan, press kit).
  Website/         Landing page, privacy policy and app-ads.txt, ready for GitHub Pages.
```

**Shipping to Google Play:** start with `Docs/PlayStore/CHECKLIST.md`.

## Balance snapshot (from the simulator)

Numbers start small (Storm Power in the tens and hundreds) and reach billions only after hours.

| | Active player (uses boosts) | Casual (no boosts) |
| --- | --- | --- |
| Storm Power after 1 min / 18 min / ~45 min | 14 / 58 / ~800 per s | 14 / 58 / ~500 per s |
| Weekly Tournament (Lv 8) | ~1h 10m | ~1h 40m |
| Offline Generator unlocked (Lv 10) | ~1h 50m (costs ~20 min of production) | ~2h 50m (costs ~5.5 h of production) |
| First prestige (Lv 11) | ~2h 15m | ~3h 15m |
| Storm Power in the billions | ~4.5 h | ~10 h |

Re-run `dotnet run -c Release --project Tools/balance-sim -- active 8` (or `casual`, `idle`) after
changing `Content.cs`.
