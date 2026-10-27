# STORM ENGINE: setup, online services and tools

This guide gets the game running offline in a minute, then switches on the global leaderboards,
server-verified scores and cloud saves through Unity Gaming Services (UGS).

---

## 1. Play (offline, no setup)

1. Open `Assets/StormEngine/Scenes/StormEngine.unity` (it is also scene 0 in Build Profiles).
2. Press **Play**.

The 2.5D diorama needs URP's 3D **Universal Renderer** (lights, shadows, post-processing). The
project was created from the 2D template, so **Storm Engine > Configure Render Pipeline** switches
the active URP asset (`Assets/Settings/UniversalRP.asset`) to
`Assets/StormEngine/Settings/StormRenderer.asset` and turns on MSAA 4x, HDR and soft shadows. This
has already been done; run it again if the scene ever renders pink or flat.

Everything works offline: saving, storms, modules, research, weekly conditions, the tournament
(with local records), the shop and the island. Offline *earnings* need the Offline Generator
(Engine Level 10). Tournament storms queue up while offline and are submitted automatically once
the game is online. Visiting other players needs a connection; the three showcase islands always work.

Editor menu **Storm Engine**:

| Item | What it does |
| --- | --- |
| Setup Game Scene | Recreates the scene and `StormPanelSettings` if you ever need to. |
| Configure Render Pipeline | Switches URP to the Universal Renderer and tunes shadows/MSAA/HDR. |
| Export Content To Cloud Code | Copies the balance data into `SubmitStorm.js` (run after changing balance). |
| Open Save Folder / Delete Local Save | Save file management. |
| Debug > Grant / Fill Storm Charge / Simulate 10 Minutes | Play-mode shortcuts for testing. `StormEngineDebug.SetLevel`, `Wear` and `SetGameViewSize` are also handy from scripts. |

---

## 2. Turn on global leaderboards

The online code is already in the project (`Assets/StormEngine/Online`). It compiles automatically
because the UGS packages are installed, and it stays dormant until the project is linked.

### 2.1 Link the project to Unity Cloud

1. **Edit > Project Settings > Services**.
2. Choose your organization and **Create project ID** (or link an existing Unity Cloud project).
3. In the [Unity Cloud dashboard](https://cloud.unity.com) open the project and make sure these
   are enabled: **Authentication** (Anonymous sign-in), **Leaderboards**, **Cloud Code**, **Cloud Save**.

### 2.2 Deploy the backend from the Editor

Open **Window > Unity Gaming Services > Deployment** (or **Services > Deployment**), pick the
`production` environment and deploy:

| File | Creates |
| --- | --- |
| `Assets/StormEngine/Server/Leaderboards/*.lb` | 9 leaderboards: `storm_alltime`, `storm_weekly` (resets Mondays 00:00 UTC and archives each week, which is how end-of-week tournament rewards find your rank) and one `storm_class_*` per storm class. The weekly board's first reset (`Start`) must be in the future when you deploy: if it has passed, set it to the next Monday `T00:00:00Z`. |
| `Assets/StormEngine/Server/CloudCode/SubmitStorm.js` | The server-side storm validator (endpoint `SubmitStorm`). |
| `Assets/StormEngine/Server/AccessControl/storm_engine.ac` | Policy that stops players from writing leaderboard scores directly. |

If your Deployment window does not list the `.ac` file, apply it with the
[UGS CLI](https://services.docs.unity.com/guides/ugs-cli/latest/general/overview):

```
ugs deploy Assets/StormEngine/Server/AccessControl/storm_engine.ac
```

Deploy the access policy **last**, after `SubmitStorm.js` works. Until then the Editor can fall
back to direct writes (see 2.4).

### 2.3 Check it

Press Play. Open **Tourney** (Engine Level 8): the status card shows a green signal when you are
online. Enter the tournament, then tap any entry to see that player's **Build View** (storm class,
category multipliers, modules and phenomena) and **Visit island**. Islands are stored as public
Cloud Save data (key `island`), so no extra configuration is needed.

### 2.4 Development fallback

If `SubmitStorm` is not deployed yet, Editor and Development builds validate the storm locally
and write it to the boards directly, so you can see the boards working early. Release builds never
do this; set `UgsBackend.AllowDirectWritesInDevelopment = false` to disable it everywhere. Once the
access policy is deployed, direct writes are rejected by the server, which is what you want.

---

## 3. How competitive scores stay honest

- A storm is a deterministic 10 ticks-per-second simulation (`Core/StormSim.cs`).
- Only **tournament** storms are submitted. When one beats a record, the client builds a
  `StormSubmission`: the frozen build (machine, module and research levels, equipped modules and
  style items), and every boost activation with its tick. Tournament storms always start with
  fresh boosts.
- `SubmitStorm.js` replays that storm with the same balance data and writes **only the score it
  computed itself**, plus a build summary (and the player's equipped title, if it is a well-formed
  title id) as leaderboard metadata. It rejects wrong content
  versions, impossible levels, locked or double-fired boosts, unknown or doubled style items,
  carried-in boost timers, mismatched weekly conditions, future timestamps and any score that
  differs from the replay. The server applies the tournament style cap (x1.10) itself.
- Players cannot write scores directly once `storm_engine.ac` is deployed.
- Rewarded ads, Energy Surges and decorations never touch tournament scores; style bonuses are capped.

`Tools/cloudcode-tests` proves the JavaScript replay matches the C# client (see section 5).

What is not covered yet: the server does not know how long it took to *earn* a build, so a player
who edits their local save can still post a valid storm from an inflated build. The next step is
server-side progression checks (e.g. storing lifetime energy in Cloud Save from Cloud Code and
comparing build cost against it).

---

## 4. Changing the balance

All numbers live in `Assets/StormEngine/Core/Content.cs` (machines, boosts, phenomena, modules,
research, weekly conditions, goals) and the `Balance` class at its top.

1. Edit `Content.cs`.
2. If storm scores can change, bump `Content.Version` so old clients are rejected by the server
   (it is currently **3**: style items now carry stat bonuses). Engine Levels, XP, decorations,
   ads, prices and the Offline Generator never affect storm scores, so tuning them needs no version
   bump. Style item *bonuses* do (they are replayed), so changing them needs a bump and an export.
3. **Storm Engine > Export Content To Cloud Code**, then redeploy `SubmitStorm.js`.
4. Re-run the balance simulator and the Cloud Code parity test (below).

---

## 5. Tools (outside Unity)

Both need only the .NET SDK and Node.js.

**Balance simulator:** plays the real game core with a bot and prints a timeline (unlocks,
discoveries, prestiges, Storm Power checkpoints).

```
dotnet run -c Release --project Tools/balance-sim -- active 8
```

Modes: `active` (uses boosts), `casual` (no boosts), `idle` (rare check-ins). The second argument is
hours of game time. `validate` generates 400 random storms, replays them in C#, and writes
`vectors.json` and `content.json` for the next check.

**Cloud Code parity test:**

```
dotnet run -c Release --project Tools/balance-sim -- validate
node Tools/cloudcode-tests/run.js Tools/balance-sim/vectors.json Tools/balance-sim/content.json
```

It replays every C# storm in the JavaScript validator (tournament rules, random style items), tries
the tampering cases (inflated scores, fake levels, unknown or doubled styles, carried-in boost
timers, double-fired boosts...), and calls the Cloud Code entry point with a fake Leaderboards API.
Current result: 400/400 storms identical, all tampering rejected.

**Unit tests:** open **Window > General > Test Runner > EditMode** and run
`StormEngine.Tests.EditMode` (44 tests: formula, style bonuses, storm replay/tamper, tournament
rules, attempts and rewards, levels, offline generator, decorations, ponds, expansions, bundles, ads,
store products, island layouts, saves and migration).

---

## 6. Store (Unity IAP) and rewarded ads

### In-app purchases

`com.unity.purchasing` 5.4.3 is installed. `Assets/StormEngine/Store` (assembly
`StormEngine.Store`) registers a Unity IAP backend for `StoreService` automatically; it compiles
only while the package is installed. Products (see `Content.Products`):

| Product id | Type | Contents |
| --- | --- | --- |
| `com.stormengine.starter` | Non-consumable | 400 Gems, 500 Storm Cores, Founder title (once) |
| `com.stormengine.gems.100` / `.550` / `.1200` / `.2600` / `.7000` | Consumable | Gem packs |

Create the same ids in Google Play Console / App Store Connect before shipping. In the Editor the
IAP fake store completes purchases without charging; prices shown are the catalogue fallbacks.
Rewards are granted before an order is confirmed, so an interrupted purchase is delivered on the
next launch; the Starter Pack's title is restored from the store on reinstall. Server-side receipt
validation is the recommended next step before launch.

### Rewarded ads

`Runtime/App/AdService.cs` drives every rewarded placement (Free Gems, Energy Surge, Instant Charge,
Double Cores, Extra Attempt, Bundle Progress, Double Offline, Music Track, Keep Your Streak; limits
in `Content.AdPlacements`). The network is **Unity LevelPlay 9.5** (`com.unity.services.levelplay`,
with the Unity Ads adapter, resolved by the Mobile Dependency Resolver into
`Assets/Plugins/Android/mainTemplate.gradle`). `Assets/StormEngine/Ads/LevelPlayAds.cs` registers itself
when `Resources/StormEngine/ReleaseConfig.asset` (menu **Storm Engine > Release Settings**) holds the
app key and the rewarded ad unit id. It starts only after the player answers the first-launch privacy
question (`AdConsentPopup`), passes GDPR/CCPA/COPPA flags, keeps one ad loaded and retries with
backoff. The Editor keeps a simulated 3-second ad; a release build without ids offers no ads (and
ad-only music becomes free). Setup steps: `Docs/PlayStore/SETUP_BACKEND_AND_ADS.md`.

---

## 7. Mobile builds

The UI is responsive: landscape uses a side navigation and side panel; portrait uses a bottom tab
bar and a bottom sheet (tap the active tab to collapse it). The camera frames the island inside
the free part of the screen in both layouts. On low-end phones set **More > Settings > Graphics
quality** to Simple (no shadows or post-processing) or Balanced.

Notches and punch-hole cameras: Android is set to render outside the safe area (Player Settings,
"Render outside safe area"), so the world fills the screen and the UI pads itself using
`Screen.safeArea` (`Runtime/UI/SafeArea.cs`); it updates on rotation and window changes. To test
a cutout in the Editor, call `StormEngineDebug.SetSafeAreaInsets(left, top, right, bottom)` (pixels)
in Play mode and `ClearSafeArea()` afterwards, or use the Device Simulator. Switch the platform to Android or iOS
in **Build Profiles** and build. Offline progress is computed when the app is paused or resumed.

**Google Play release**: the package name is `com.stormengine.game`. Menu **Storm Engine > Release**
applies the icons and release settings, bumps the version code and builds either a debug-signed test
APK or the signed App Bundle for Play (it needs your upload keystore). The whole launch, from Play
Console forms to the 14-day closed test, is in `Docs/PlayStore/CHECKLIST.md`; store texts, screenshots
and the feature graphic are generated by `Tools/marketing/` (`make_icon.py`, `make_store_art.py`,
`listing.py`).

**Performance on phones**: the 3D scene renders slightly below native resolution (about 1024 px on
the short side on High; the interface stays sharp), with 4x MSAA on High and 2x on Balanced, a single
shadow cascade fitted to the island and no depth-of-field. If frames keep dropping, the resolution
steps down before graphics quality does. Scroll lists move as one GPU group. Development builds have
an FPS counter (Settings > Developer).

**Vibration, frame rate and battery**: `Runtime/App/Haptics.cs` plays every effect's vibration
(Settings > Vibration). The VIBRATE permission is added automatically. `Runtime/App/Performance.cs`
applies Settings > Frame rate (Battery saver 30 / Standard 60 / Smooth up to 120 Hz; Unity's
Optimized Frame Pacing is on), eases idle screens to 30 FPS, caps at 30 on a low battery and, on
phones, steps graphics quality down once if frames keep dropping (Settings > Auto-adjust graphics).

**Reminders (local notifications)** use `com.unity.mobile.notifications` 2.5 and the
`Assets/StormEngine/Notify` assembly, which only compiles while that package is installed.
`Runtime/App/Reminders.cs` plans them (next daily gift, tournament closing, new tournament week, full
Offline Generator; nothing between 21:00 and 9:00 local time), schedules them when the app goes to
the background and clears them when it returns. Android 13+ needs the notification permission: the
game asks in its own words after the first daily gift and only then shows the system prompt. The
status-bar and large icons are drawn by **Storm Engine > Set Up Notification Icons** (already run;
they are registered in Project Settings > Mobile Notifications as `storm_small` and `storm_large`).
Reminders need a device build to test; the Editor has no notification backend.

**Privacy**: Settings > Privacy has an **Online features** switch (off = nothing leaves the phone; no
leaderboards, tournament, cloud save or visits) and **Delete my online data**, which deletes the
Unity Authentication account and its server data and then switches online features off. Cloud
uploads wait for the start-up cloud check and never overwrite a cloud save with more progress
(`OnlineManager.CloudFloor`).

**Languages**: English plus 11 translations (Spanish, Portuguese, French, German, Italian, Russian,
Turkish, Polish, Indonesian, Vietnamese, Thai). Code uses `L.T("English text")` / `L.F(...)`;
`python Tools/loc/extract.py` collects every text into `Tools/loc/keys.json`, translations live in
`Tools/loc/src/<code>.N.txt` (`id|text`), and `python Tools/loc/build.py` validates placeholders and
writes `Resources/StormEngine/Lang/<code>.json`. Thai needs the advanced text generator (enabled in
Project Settings > UI Toolkit). Languages without a translation file are commented out in
`Runtime/App/Languages.cs`.

---

## 8. Troubleshooting

| Symptom | Fix |
| --- | --- |
| HUD icon stays grey (offline) | The project is not linked (2.1), there is no internet, or sign-in failed. Check the Console for `[StormEngine]` messages. |
| "Leaderboard ... has not been created yet" | Deploy the `.lb` files (2.2). |
| Storms stay "waiting to be verified" | `SubmitStorm.js` is not deployed, or the server rejected it. Check the Cloud Code logs in the dashboard. |
| "content version mismatch" | Export the content to Cloud Code and redeploy (section 4). The script in the repo already contains Version 3, so an older deployed script must be redeployed. |
| No end-of-week tournament reward | The weekly board needs `Archive: true` (already set in `storm_weekly.lb`); rewards are paid the first time the game is online after the week closes. |
| Scene is pink, black or unlit | Run **Storm Engine > Configure Render Pipeline** (section 1). |
| Console shows `Script could not be found` once | `SubmitStorm.js` is not deployed yet; the game stops calling it until the next launch and retries submissions with backoff. |
