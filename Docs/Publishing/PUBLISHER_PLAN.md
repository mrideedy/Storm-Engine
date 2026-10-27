# Storm Engine: finding and signing a publisher

Pitch deck: https://claude.ai/artifact/6hDTbxEKbB79wr2t4uQ35k (private until you share it; use Share › Export to get a PDF for emails and forms).
Key art and screenshots for forms: `Tools/marketing/pitch/images/`. Trailer and Reels: `Tools/marketing/trailer/out/`.

## 1. Before you send anything

- [ ] Fill the deck placeholders: `[Your name]`, `[start date] to [end date]`, `[YouTube link]`, `[__]` days on the metrics slide.
- [ ] Upload the trailer to YouTube as **Unlisted** and use that link everywhere.
- [ ] Get a raw gameplay video (portrait, 30–60 s, real play, no captions). Publishers judge this more than the trailer.
- [ ] Deploy the Cloud Code script `SubmitStorm`, or delete "server replay of tournament storms" from the Status slide. Don't claim it until it runs.
- [ ] Don't launch to production yet. Publishers want to run their own test and soft launch. Keep the closed test running, because you still need it for production access.
- [ ] Decide the name on the contract: you as an individual, or a registered business. Ask a tax adviser how you'll receive foreign payments.

## 2. Who to pitch (all checked October 2026)

Send to the first four in the same week. Applying to several publishers at once is normal.

| # | Publisher | Where to apply | Why it fits |
|---|---|---|---|
| 1 | Supersonic (Unity) | https://supersonic.com/ › Publish with us | Hybrid-casual, runs on LevelPlay (your ad stack already), has idle games, you keep the IP |
| 2 | SayGames | https://say.games/publishing/ | Hybrid-casual with ads + IAP, short form, wants a gameplay video |
| 3 | TapNation | https://www.tap-nation.io/publishing | Idle hybrid-casual case studies, answers within about 2 weeks, also buys games outright |
| 4 | CrazyLabs | https://www.crazylabs.com/publishing/ › Apply here | "Publishing For All" can keep the game on your own Play account |
| 5 | Voodoo | https://voodoo.io/publishing › Start now | Very test-driven, fast yes or no |
| 6 | Homa | https://www.homagames.com/ (Homa Lab) | You test the game yourself with their SDK and data tools |

Skip these: **Kwalee** (its mobile publishing page now redirects to PC and console), **Raw Fury** (premium PC/console games, not free-to-play mobile), **Kongregate** (publishes only its own games now).

## 3. How these deals really work

- **What you sign is a revenue share, not company shares.** The publisher gets a share of what *this game* earns. Never give away part of your company or the IP.
- **The usual mobile model:** the publisher pays for user acquisition (UA: ads that bring installs) and creatives. It earns that money back from the game's revenue first ("recoup"), then the rest is split. In Unity's growth-program example, Unity first recoups its UA spend and the remainder is shared 50/50.
- **What's typical:** a 2025 review of 100+ indie publishing contracts found a median developer share of **70% without an advance** and **50% with an advance**. Advances were almost always recouped from the developer's share.
- **Your 51/49 idea is a fair ask,** as long as it means 51% of *net revenue after UA is recouped*, not 51% of the company.

**An example month** (made-up numbers, only to show the math):
- Revenue is $100,000, and $85,000 is left after store fees.
- The publisher spent $70,000 on UA, so $15,000 is left to split.
- At 51%, you get $7,650. Once the UA has paid for itself, the split covers almost everything.

So the definitions below matter more than the headline percentage.

## 4. Term sheet: what to ask for

1. **Net revenue, written out:** gross revenue minus store fees, refunds and taxes, minus UA *only* as recoupable spend. Nothing else is deducted ("overhead", "platform fees", "admin").
2. **Recoup at 1.0×:** no multiplier and no interest. It comes only from this game (no cross-collateralization with their other titles). If UA never pays back, you owe nothing.
3. **A step-up:** your share goes up once UA has been recouped (for example 51% → 60%).
4. **A marketing commitment:** a minimum UA budget or launch date in writing. If they stop marketing for 3 months, the rights come back to you.
5. **IP stays yours:** they get a *license* to publish this one mobile game for the term, not an assignment. Sequels, PC/console ports and merchandise stay with you (or they get a first-look right at most).
6. **Short term:** 2–3 years, renewable only if you both agree. The 2025 data shows terms often run 4–7 years, so push back.
7. **Free exit after a failed test:** if the marketability test or soft launch misses the agreed numbers, the deal ends and everything returns to you at no cost.
8. **Store account:** say who owns the Google Play listing. If it moves to their account, require a transfer back, with the players, when the deal ends.
9. **Money and reports:** monthly revenue and UA reports, payment within 30–45 days of month end, and the right to audit once a year.
10. **Your other games are untouched:** no non-compete, and no first-refusal right on your future games (One Block stays yours).
11. **Their SDKs:** they provide the data needed to update your Data safety form and privacy policy.
12. **A lawyer reads the contract before you sign.** A one-hour review of a games contract is cheap insurance.

### Red flags: stop and get advice

- They ask **you** to pay anything: an integration fee, marketing deposit or "testing package". Real publishers never do this.
- A perpetual term, or "for the life of the copyright".
- A recoup multiplier above 1.0×, or interest on UA.
- "Net revenue as determined by Publisher", or no definition at all.
- An IP assignment (they would own the game or the code).
- Rights to all your future games.
- No reports or audit rights.

## 5. Timeline

| When | What happens |
|---|---|
| This week | Fill the placeholders, export the PDF, get the gameplay video, upload the trailer as Unlisted |
| Week 1 | Apply to Supersonic, SayGames, TapNation and CrazyLabs. Add Voodoo and Homa the following week |
| Weeks 1–3 | Replies come in. Interested publishers send their SDK for a marketability (CPI) test |
| Weeks 2–5 | Integrate the SDK, send a test build, they run small ad campaigns and measure cost per install and Day-1 retention |
| Weeks 4–8 | Good numbers lead to a term sheet. Compare offers with section 4, negotiate, lawyer review, sign |

## 6. Ready to paste

### Short pitch (for "Tell us about your game" fields)

```
Storm Engine is a finished hybrid-casual idle tycoon for Android (Unity 6). Players build a weather machine, charge a storm chamber and unleash lightning combos, then grow with modules, research and prestige. Retention: weekly tournaments, an island to decorate and visit, daily gifts, streaks and missions, offline progress. Monetization: opt-in rewarded ads (LevelPlay with Unity Ads bidding) plus gem IAP. Content: 14 skins, 10 clouds, 10 lightning bolts, 9 worlds, 31 decorations, 12 languages. Now in Google Play closed testing, ready for a marketability test.
```

### SayGames form

| Field | Answer |
|---|---|
| Name | [Your name] |
| Email | studiosmoonbit@gmail.com |
| Studio name | Moonbit Studios |
| Location | [City, Country] |
| Team size | [1] |
| Team info | Independent studio. [Your name] designed and built Storm Engine end to end in Unity 6: gameplay, 3D art direction, UI, backend (Unity Gaming Services), monetization and localization into 12 languages. |
| Gameplay video | [Unlisted YouTube link to the raw gameplay video] |
| How did you hear about us | [Your honest answer, e.g. "Website"] |

### First email (where there is a contact address)

```
Subject: Storm Engine – finished idle tycoon (Android, Unity 6) looking for a publisher

Hi [Name],

I'm [Your name] from Moonbit Studios. Storm Engine is a finished idle tycoon about engineering the weather: players build a storm machine, then unleash lightning combos and Mega Storms.

- Hybrid monetization: opt-in rewarded ads (LevelPlay + Unity Ads bidding) and gem IAP
- Retention: weekly tournaments, island decorating and visiting, daily gifts, streaks and missions
- Ready now: 12 languages, trailer, Reels and store art; in Google Play closed testing

Gameplay video: [link]
Trailer: [link]
Pitch deck: attached (PDF)

I'd love to run a marketability test with you. I can integrate your SDK quickly.

Thanks,
[Your name]
Moonbit Studios · studiosmoonbit@gmail.com · https://mrideedy.github.io
```

### Follow-up (7–10 days later, once)

```
Subject: Re: Storm Engine – finished idle tycoon (Android, Unity 6) looking for a publisher

Hi [Name], a quick follow-up on Storm Engine. Since my last email: [one new thing, e.g. closed-test Day-1 retention of __%, or a new update]. Happy to send a build or integrate your SDK for a test whenever it suits you.

Thanks,
[Your name]
```

### When an offer arrives

```
Thank you, that's great news. Before we go further, could you send the proposed terms in writing, including:
1. The revenue share and how net revenue is calculated (exactly what is deducted)
2. How UA spend is recouped (multiplier, interest, time limit) and whether the share changes after recoup
3. The marketing commitment for soft launch and global launch
4. Term, territory, and what happens to the game and the store listing if the deal ends or a test misses its targets
5. IP: whether this is a license or an assignment, and how sequels and ports are treated
6. Reporting, payment timing and audit rights
```

## Sources

- Voyer Law, publishing agreements 2025 (median developer shares, recoup, term lengths): https://gamedevreports.substack.com/p/voyer-law-publishing-agreements-in
- Unity's growth publishing program (UA recouped first, then a 50/50 split): https://gamefromscratch.com/unity-launch-game-growth-publishing-program/
- The economics of mobile game publishing (deals around 50% of net revenue): https://www.gamezebo.com/news/the-economics-of-mobile-game-publishing/
- Negotiating with a publisher (cap marketing deductions, step-up splits, get a second offer): https://discussions.unity.com/t/negotiation-as-a-game-developer-with-a-game-publisher/675404
