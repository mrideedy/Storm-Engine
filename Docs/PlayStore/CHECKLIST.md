# Storm Engine: Google Play launch checklist

✅ = done in the project · 🔲 = your action (accounts, passwords and payments stay with you)

Other docs in this folder: [PLAY_CONSOLE_FORMS.md](PLAY_CONSOLE_FORMS.md) (every form answer, products),
[SETUP_BACKEND_AND_ADS.md](SETUP_BACKEND_AND_ADS.md) (Cloud Code, LevelPlay), [STORE_LISTING.md](STORE_LISTING.md)
(texts in 12 languages), and [../Marketing/MARKETING_KIT.md](../Marketing/MARKETING_KIT.md).

## Already done in the project
- ✅ Package name `com.stormengine.game`, version `1.0.0` (code 1), IL2CPP, ARM64, target API 36 (newest), min API 25
- ✅ App icon (adaptive, round, legacy), "Storm Core": the game's engine under a storm cloud, struck by a lightning bolt,
  on a purple-to-blue sky (`Tools/marketing/make_icon.py`, from in-game renders); no Unity splash. The game opens with an
  in-world intro: the engine draws three lightning strikes in a storm, the STORM ENGINE logo lands
  (`Tools/marketing/make_logo.py`, Bungee font, SIL Open Font License) and the camera glides into the main menu
  (about 3.7 seconds; a tap speeds it up)
- ✅ Rewarded ads: Unity LevelPlay 9.5 + Unity Ads, first-launch privacy question, personalized-ads switch
- ✅ In-app purchases: Unity IAP 5.4, six products (ids in PLAY_CONSOLE_FORMS.md)
- ✅ Privacy: online-features switch, "Delete my online data", privacy policy link (after you set the URL)
- ✅ Player-name filter (12 languages): offensive names are rejected and hidden on leaderboards and island visits;
  the privacy policy explains the rules and how to report a name
- ✅ Download size: unused Unity AI (Sentis) shaders are stripped from builds (test APK 61 → 53 MB); native crash
  symbols go into the bundle for Play Console
- ✅ Share (records, settings) and Google Play in-app review (asked at most twice, after level-ups)
- ✅ 12 languages in game, store texts in 12 languages, screenshots with captions in 12 languages
- ✅ Store graphics: `Tools/marketing/store/` (512 icon, 1024x500 feature graphic, 1080x1920 screenshots)
- ✅ Website with privacy policy: `Docs/Website/` · build tools: menu **Storm Engine > Release**
- ✅ EditMode tests 128/128

## Launch steps

### 1. 🔲 Google Play developer account
- [play.google.com/console](https://play.google.com/console): personal account, one-time US$25 fee, identity
  verification (ID document) and phone check. Verification can take a few days, so start first.
- New personal accounts must run a **closed test with at least 12 testers for 14 days in a row** before they can
  publish to production (step 10). Start recruiting testers now (MARKETING_KIT.md has messages to copy).

### 2. 🔲 Website and privacy policy (free with GitHub Pages)
1. ✅ Developer name **Moonbit Studios** and support email **studiosmoonbit@gmail.com** are filled in (website,
   privacy policy, marketing kit). Use exactly this developer name in Play Console.
2. Repository: [github.com/mrideedy/mrideedy.github.io](https://github.com/mrideedy/mrideedy.github.io) with the
   contents of `Docs/Website/` (index.html, privacy.html, app-ads.txt and the `assets/` folder) at its root.
   Settings > Pages must deploy from branch `main`, folder `/ (root)`.
3. ✅ Privacy policy URL **https://mrideedy.github.io/privacy.html** is set in `ReleaseConfig.asset`; paste the same
   link into Play Console (App content > Privacy policy).
4. Developer website for Play Console: **https://mrideedy.github.io/**; `app-ads.txt` sits at its root (fill it in
   step 4).

### 3. 🔲 Tournament backend (Unity Cloud dashboard)
Create the `SubmitStorm` Cloud Code script by hand and add the access policy:
[SETUP_BACKEND_AND_ADS.md, part 1](SETUP_BACKEND_AND_ADS.md#1-cloud-code-submitstorm-tournament-scores).

### 4. 🔲 Ads (LevelPlay dashboard)
Create the app and a rewarded ad unit, paste the ids into `ReleaseConfig.asset`, test with the ad test
suite, publish app-ads.txt (the AD_ID permission is already declared):
[SETUP_BACKEND_AND_ADS.md, part 2](SETUP_BACKEND_AND_ADS.md#2-unity-levelplay-rewarded-ads).

### 5. 🔲 Upload key (keystore)
Google signs the final app (Play App Signing); you sign uploads with your own **upload key**. Create it once:

- Unity: **Project Settings > Player > Android > Publishing Settings > Keystore Manager > Create New >
  Anywhere**. Save it **outside the project** (for example `D:\Keys\stormengine-upload.keystore`), alias `upload`,
  validity 50 years, strong passwords.
- Or in a terminal (it asks for the passwords itself):

```bash
"/c/Program Files/Unity/Hub/Editor/6000.3.0f1/Editor/Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool.exe" -genkeypair -v -keystore "D:/Keys/stormengine-upload.jks" -alias upload -keyalg RSA -keysize 2048 -validity 18250
```

Back up the keystore file and both passwords (password manager plus an offline copy). If the upload key is ever
lost, Play support can reset it, but that takes days. Never commit it to a repository.

In Unity: Publishing Settings > **Custom Keystore** > select the file, enter the passwords, pick alias `upload`.
Unity forgets the passwords when it restarts; you can instead set the environment variables `STORM_KEYSTORE_PASS`
and `STORM_KEY_PASS` before opening Unity.

### 6. 🔲 Build the bundle
Menu **Storm Engine > Release > Build Google Play Bundle (AAB)** → `Build/StormEngine-<version>-<version code>.aab` (for example `StormEngine-1.0.0-4.aab`).
It applies the release settings first (IL2CPP, ARM64, no splash, lighter logs) and refuses to build without the
upload key. For every later upload, first use **Storm Engine > Release > Bump Version Code**.

To try a release build on your phone before Play: **Storm Engine > Release > Build Test APK** and install it over USB
(`adb install -r Build/StormEngine-1.0.0-test.apk`). It is signed with the debug key, so uninstall it before installing
the version from Google Play.

### 7. 🔲 Create the app in Play Console
1. **Create app**: name "Storm Engine", default language English (United States), **Game**, **Free**, accept the
   declarations.
2. **App content**: answer every section with [PLAY_CONSOLE_FORMS.md](PLAY_CONSOLE_FORMS.md) (privacy policy, ads,
   app access, content rating, target audience, data safety, advertising ID, government/financial/health: none).
3. **Store settings**: category **Games > Simulation**, tags (Idle, Simulation, Casual), contact email, website.
4. **Main store listing**: title, short and full description from [STORE_LISTING.md](STORE_LISTING.md); graphics
   from `Tools/marketing/store/`: `play_icon_512.png`, `feature_graphic.png`, and the phone screenshots from
   `screenshots/en/` (upload all 6). Add the other 11 languages under **Manage translations**, each with its own
   screenshot folder.

### 8. 🔲 Internal testing (minutes, not days)
Testing > Internal testing > create a release, upload the AAB (accept Play App Signing), add yourself as a tester and
open the opt-in link on your phone. Check: first launch and privacy question, ads (test device), purchases (after
step 9, with License testing), reminders, language, cloud save, tournament.

### 9. 🔲 In-app products
Monetize > Products > One-time products: create the six products from PLAY_CONSOLE_FORMS.md and activate them.
Add your Gmail under Settings > **License testing** so your own purchases are not charged.

### 10. 🔲 Closed testing: 12+ testers for 14 days (required for new personal accounts)
Testing > Closed testing > create a track, add testers (an email list or a Google Group), release the same AAB, and
share the opt-in link. At least **12 testers must stay opted in for 14 consecutive days**. Ask them to really play
and send feedback; Play asks about this afterwards. Fix what they report and upload updates to the same track.

### 11. 🔲 Apply for production access
Dashboard > **Apply for production** once the 14 days are done. Describe the test (how many testers, what feedback
changed) and why the game is ready. Google's review usually takes up to about a week.

### 12. 🔲 Production release
Create the production release with a **staged rollout** (for example 20%, then 50%, then 100%). Watch
**Android vitals** (user-perceived crash rate below 1.09% and ANR rate below 0.47% are Google's bad-behavior
thresholds), the pre-launch report and reviews. Reply to reviews: it helps the rating.

## Before every update
- **Storm Engine > Release > Bump Version Code**, build the AAB, upload, write release notes.
- Balance changes: re-export the content to Cloud Code and republish `SubmitStorm` (SETUP_BACKEND_AND_ADS.md).
- Each August Google raises the target API level; Unity's "automatic" target picks up the newest installed SDK.

## Worth doing soon after launch
- Answer name reports quickly (support email): in the Unity Cloud dashboard, delete the player's leaderboard scores
  (Leaderboards) or the player (Player Management), and add any word that slipped through to `Core/NameFilter.cs`.
- Localized screenshots with the game's own UI in each language (current sets translate the captions only).
- More ad networks in LevelPlay for better fill once there is traffic.
