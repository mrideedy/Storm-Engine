# Play Console answers (App content)

Answers for Play Console > **Policy and programs > App content**, based on what the game and its SDKs
actually do (Unity Gaming Services, Unity IAP 5.4, Unity LevelPlay 9.5 with Unity Ads). If you add an SDK
(for example Google AdMob or analytics), update the Data safety answers.

## App access
**All functionality is available without special access.** No login, no invite codes.

## Ads
**Yes, my app contains ads.** (Rewarded ads through Unity LevelPlay.)

## Advertising ID
**Yes**, the app uses the advertising ID. Purposes: **Advertising or marketing**, **Analytics**, **Fraud prevention,
security and compliance** (LevelPlay and Unity Ads). The AD_ID permission is declared in the project
(*Ads Mediation > Developer Settings > LevelPlay Mediation Settings*).

## Content rating (IARC questionnaire)
- Category: **Game**
- Violence, blood, fear, sexuality, nudity, profanity, drugs, alcohol, tobacco, crude humor: **No**
- Gambling or simulated gambling: **No** (daily gifts are a fixed 7-day cycle; nothing random is sold)
- Users can interact or exchange content (chat, messaging): **No** (only a chosen player name on leaderboards and
  visits to other players' decorated islands, built from the game's own items; no free text or chat; names pass a
  word filter)
- Shares the user's current location with other users: **No**
- Digital purchases: **Yes** (optional gem packs and a starter pack)

Expected result: **Everyone / PEGI 3** with an **In-Game Purchases** notice.

## Target audience and content
- Target age groups: **13-15, 16-17, 18+** (not under 13).
- "Could your store listing unintentionally appeal to children?" Answer honestly. The art is cartoony, so Google may
  review it. The game is an idle optimization game with leaderboards, rewarded ads and purchases, and it is not
  designed for children. If Google decides it appeals to children, you will be asked to follow the Families policy:
  LevelPlay/Unity Ads are part of Google's Families Ads program, but you would then need an age screen before ads.
- Store presence: no under-13 content in screenshots or text (already the case).

## Data safety

**Does your app collect or share any of the required user data types?** Yes
**Is all of the user data collected by your app encrypted in transit?** Yes
**Do you provide a way for users to request that their data is deleted?** Yes: *Settings > Privacy > Delete my
online data*, and by email (see the privacy policy).

| Data type | Collected | Shared | Processed ephemerally | Required? | Purposes | Source |
|---|---|---|---|---|---|---|
| Location > Approximate location | Yes | Yes | No | Required | App functionality, Analytics, Advertising or marketing, Fraud prevention/security/compliance | LevelPlay, Unity IAP |
| Personal info > Name (player nickname) | Yes | No | No | Optional | App functionality | Leaderboards (name the player picks) |
| Personal info > Email address | Yes | No | Yes | Required | App functionality | Unity IAP 5.4+ (Google Play purchase flow) |
| Personal info > User IDs | Yes | Yes | No | Required | App functionality, Account management, Analytics | Unity Authentication, Cloud Save, IAP |
| Financial info > Purchase history | Yes | No | No | Required | App functionality, Analytics | Unity IAP |
| App activity > Other user-generated content | Yes | No | No | Required | App functionality | Cloud Save (island layout, progress) |
| App activity > Other actions | Yes | Yes | No | Required | App functionality, Analytics, Advertising or marketing, Fraud prevention | Cloud Save, Leaderboards; LevelPlay (ad interactions) |
| App info and performance > Crash logs | Yes | No | No | Required | Analytics | Unity IAP |
| App info and performance > Diagnostics | Yes | No | No | Required | App functionality, Analytics, Fraud prevention | LevelPlay, Unity IAP |
| App info and performance > Other app performance data | Yes | No | No | Required | App functionality, Analytics | Unity IAP |
| Device or other IDs | Yes | Yes | No | Required | App functionality, Analytics, Advertising or marketing, Fraud prevention | LevelPlay, Cloud Save, IAP |

Not collected: health and fitness, messages, photos and videos, audio, files and docs, calendar, contacts, web
browsing, precise location, phone number, address, race, religion, sexual orientation, credit/debit card or bank data.

Sources (Unity's own Data safety tables):
[LevelPlay](https://docs.unity.com/en-us/grow/levelplay/platform/legal-resources/google-data-safety-questionnaire),
[Authentication](https://docs.unity.com/ugs/manual/authentication/manual/google-data-safety),
[In-App Purchasing](https://docs.unity.com/en-us/iap/privacy-and-consent/google-play-data-safety),
[Cloud Save](https://docs.unity.com/ugs/en-us/manual/cloud-save/manual/privacy/google-play-data-safety).
Unity marks some UGS data as "shared" with its vendors; declaring it as shared (as above) is the cautious choice.

## Other declarations
- News app: **No** · COVID-19 contact tracing/status: **No** · Government app: **No**
- Financial features: **None** · Health: **No** · Health Connect: **No**
- Photo and video permissions / foreground service / exact alarms: **not used** (reminders use inexact scheduling).
- Privacy policy: the public URL of `Docs/Website/privacy.html` (see CHECKLIST.md, step 2).

## In-app products (Monetize > Products > One-time products)
Create these after the first bundle is uploaded (Play only unlocks products for apps whose bundle includes the
billing permission, which Unity IAP adds). The IDs must match exactly.

| Product ID | Name | Description | Price (USD) | Type |
|---|---|---|---|---|
| `com.stormengine.starter` | Starter Pack | 400 Gems, 500 Storm Cores and the Founder title. Once per player. | 2.99 | One-time (non-consumable) |
| `com.stormengine.gems.100` | Pouch of Gems | 100 Gems | 0.99 | Consumable |
| `com.stormengine.gems.550` | Bag of Gems | 550 Gems | 4.99 | Consumable |
| `com.stormengine.gems.1200` | Chest of Gems | 1,200 Gems | 9.99 | Consumable |
| `com.stormengine.gems.2600` | Vault of Gems | 2,600 Gems | 19.99 | Consumable |
| `com.stormengine.gems.7000` | Storm Hoard | 7,000 Gems | 49.99 | Consumable |

Play converts prices for every country (you can adjust them). In Play all of these are "one-time products"; the game
itself consumes the gem packs. Add your own Gmail under **Settings > License testing** so test purchases are free.
