# Backend and ads setup

Two dashboards to finish before real players arrive: Unity Cloud (tournament verification) and LevelPlay (ads).

## 1. Cloud Code: SubmitStorm (tournament scores)

The leaderboards are deployed, but the script that verifies tournament storms is not (the Editor's Deployment
window kept failing with a 404). Without it, release builds keep tournament storms queued and never post them.
Creating the script by hand in the dashboard avoids that bug.

1. Open [cloud.unity.com](https://cloud.unity.com), pick the **Storm Engine** project and the **production** environment
   (top bar).
2. Go to **Products > Cloud Code > Scripts** and click **Create script**.
3. Name: `SubmitStorm` (exactly). Type: **API**. Language: **JavaScript**.
4. Replace the template with the whole file `Assets/StormEngine/Server/CloudCode/SubmitStorm.js`.
5. Check the **Parameters** panel shows `submission` (String, required), `summary` (String, optional) and
   `title` (String, optional). If not, add them by hand.
6. Click **Save**, then **Publish**.
7. Test in the game (Editor or phone): play a tournament storm. The result card should end with
   "Verified and submitted to the leaderboards" and the score should appear on the Tournament tab.

When you change balance in `Content.cs`, run **Storm Engine > Export Content To Cloud Code**, then paste and publish
the script again (the server replays storms with the same numbers as the game).

### Access policy (players cannot write scores directly)
Without this, a modified client could write any score. In the dashboard: **Administration > Access control >
Project-based policies > Add statement** (or deploy `Assets/StormEngine/Server/AccessControl/storm_engine.ac`
from the Deployment window):

```json
{
  "Sid": "deny_player_leaderboard_writes",
  "Action": ["Write"],
  "Effect": "Deny",
  "Principal": "Player",
  "Resource": "urn:ugs:leaderboards:/v1/projects/*/leaderboards/*/scores/players/*",
  "Version": "1.0.0"
}
```

Add it **after** step 7 works: from then on only Cloud Code can post scores (the development-only shortcut that
writes directly to the boards stops working too, which is intended).

## 2. Unity LevelPlay (rewarded ads)

The game already contains LevelPlay 9.5 with the Unity Ads network and asks for the player's ad choice on first
launch. It only needs your account's ids.

1. In the Unity Cloud dashboard open **Grow > LevelPlay** (or [platform.ironsrc.com](https://platform.ironsrc.com))
   and create an account if you do not have one.
2. **Add app**: platform **Android**, choose "my app is not live yet", package name `com.stormengine.game`,
   name "Storm Engine". Copy the **App Key**.
3. **Ad units**: create a **Rewarded** ad unit (for example "Rewarded main") and copy its **Ad Unit ID**.
4. **Networks / Setup**: enable **Unity Ads** (bidding). LevelPlay can create the Unity Ads game ids for you.
   More networks can come later (each needs its adapter from **Ads Mediation > Network Manager** in Unity).
5. In Unity, select `Assets/StormEngine/Resources/StormEngine/ReleaseConfig.asset` (menu **Storm Engine > Release
   Settings** also works) and fill in:
   - `Level Play Android App Key` and `Level Play Android Rewarded Ad Unit Id`
   - `Privacy Policy Url` (from CHECKLIST.md step 2) and, once the game is live, `Store Url` (optional).
6. Already set for you (check only): **Ads Mediation > Developer Settings > LevelPlay Mediation Settings** has
   **Declare AD_ID Permission** ticked and **Enable LevelPlay SDK Init API** off (the game starts LevelPlay
   itself, after the privacy question).
7. Test on your phone with a **development** build: *Settings > Developer > Ad test suite (LevelPlay)* shows
   whether Unity Ads is integrated and can load. In the LevelPlay dashboard add your phone as a **test device**
   (by its advertising ID) so you get test ads. Never tap live ads on your own device repeatedly: networks treat it
   as invalid traffic.
8. Put the LevelPlay **app-ads.txt** lines (LevelPlay > Setup > app-ads.txt) into `Docs/Website/app-ads.txt` and
   publish it at the root of your developer website (see CHECKLIST.md step 2).

How it behaves:
- Editor: the game keeps its own simulated ad popup, so you can test rewards without LevelPlay.
- A build without ids: no ad buttons can be used (they stay disabled) and ad-only music is free.
- Personalized ads follow the player's answer (Settings > Privacy > Personalized ads). "No" also sets the US
  "do not sell or share" flag. Nothing in the game is directed at children, so the COPPA flag is off.
- If you later add Google AdMob to the mediation, Google requires a certified consent tool (Google UMP) for players
  in the EEA/UK before showing its ads.

## 3. In-app purchases
Already wired (Unity IAP 5.4). Create the products in Play Console as listed in PLAY_CONSOLE_FORMS.md; prices
then come from Google Play automatically. In the Editor and development builds, purchases are simulated (free).
