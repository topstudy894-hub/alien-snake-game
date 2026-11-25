# Release checklist - Google Play (web game as an Android app)

I cannot build or sign an Android app here (no Android SDK or Play account). This path wraps your tested web game as a Trusted Web Activity (TWA): the Play app opens your hosted game full-screen.

## 0. Before anything
- [ ] Open `public/privacy.html`, replace `YOUR-EMAIL@example.com` (two places) and delete the yellow note box.
- [ ] Check the name "Alien Snake" is free (see listing.md). Rename if needed.
- [ ] The `?dev=1` free-coins button only works on localhost/file, not on your hosted site (already done).

## 1. Host the game server (needs HTTPS + WebSocket)
- [ ] Deploy `web/` to Render / Railway / Fly.io / a VPS (`npm install`, `npm start`). `render.yaml` in the repo root is a ready Render blueprint.
- [ ] A **free tier that sleeps** makes the first player wait 30+ s - use an always-on plan for a public launch.
- [ ] Optional hardening env vars: `ALLOWED_ORIGINS=https://your-domain` (browser origins allowed to connect), `MAX_PER_IP=8`, `MAX_ROOMS=300`.
- [ ] Open `https://your-domain/health` (should say `ok`), `/privacy.html`, `/manifest.webmanifest`, and play a match from two phones.

## 2. Make the Android package (.aab)
Pick one:
- **A. PWABuilder (easiest, no coding):** pwabuilder.com > enter your URL > Package for stores > Android. Choose a package ID (e.g. `com.yourname.aliensnake`), let it create a signing key and **save that key file + passwords somewhere safe** (losing it blocks updates).
- **B. Bubblewrap CLI** (needs Node + JDK + Android SDK).
- **C. Unity build** (the `unity/` project) - needs the compile check first and is the only path where ads can be added.
- [ ] **Check the generated app's target API level is 36 or higher before uploading.** I could not confirm what PWABuilder's default is today; if it is lower, Play will reject the upload - rebuild with a newer tool/setting.

## 3. Digital Asset Links (hides the browser address bar)
- [ ] After your first upload, Play Console > your app > Setup > App signing > copy the **SHA-256 fingerprint** of the app signing key.
- [ ] Copy `store/assetlinks.json.example` to `web/public/.well-known/assetlinks.json`, fill in your package ID and fingerprint, redeploy, and open `https://your-domain/.well-known/assetlinks.json` to confirm it loads.

## 4. Play Console
- [ ] Create app: Game, free. Upload icon, feature graphic, screenshots, texts from `store/`.
- [ ] Fill App content: privacy policy URL, ads = no, app access, content rating, target audience 13+, data safety (see listing.md).
- [ ] Upload the .aab to **Closed testing** first.

## 5. Testing rule for new personal accounts
- [ ] If your personal developer account was created after 13 Nov 2023: run a closed test with **at least 12 testers opted in continuously for 14 days**, then apply for production access from the Dashboard. Organization accounts are exempt. Plan about 3 weeks for the first launch.

## 6. After launch
- [ ] Start with a staged rollout, watch Android vitals (crashes/ANRs) and reviews.
- [ ] Every update must also target API 36+ and be signed with the same key.
