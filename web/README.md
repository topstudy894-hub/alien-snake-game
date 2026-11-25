# Hungry Alien .io – Online Multiplayer

```
hungry-alien-online/
├─ server.js          ← authoritative game server (Node.js + ws)
├─ package.json
└─ public/index.html  ← your game + lobby + multiplayer client (served by server.js)
```

The game page **must be opened from this server** (it connects back to the same host over WebSocket).
It will not connect if you open `index.html` as a file, or from a static host (GitHub Pages, Claude artifact, etc.).

## 1. Run locally
1. Install Node.js 18+ from nodejs.org.
2. In this folder: `npm install` then `npm start`.
3. Open http://localhost:3000.

### Test with 2 devices (same Wi-Fi)
1. Find your PC's local IP (`ipconfig` on Windows, e.g. `192.168.1.20`).
2. Allow Node.js through the firewall when Windows asks (private networks).
3. Phone: open `http://192.168.1.20:3000`. PC: open `http://localhost:3000`.
4. Both tap **Quick Play** → you should see each other's aliens moving live.
5. Friends test: on one device **Create Room**, read the 5-letter code (top of screen / Friends panel), on the other device **Friends → enter code → Join Room**.

## 2. Host it online (so anyone can play)
Any host that runs a Node web service with WebSocket support works:
- **Render.com / Railway / Fly.io:** create a *Web Service* from this folder (upload to a GitHub repo first). Build command `npm install`, start command `npm start`. They set `PORT` automatically and give you HTTPS; the client switches to `wss://` by itself.
- **VPS (DigitalOcean, etc.):** `npm install`, run with `pm2 start server.js`, and put nginx/Caddy in front for HTTPS (forward WebSocket upgrade headers).
- Free tiers may sleep when idle, so the first connection can take ~30 s.

No database, API keys or environment variables are required (only optional `PORT`).

## 3. How it works
- **Server-authoritative:** the server simulates every alien (movement, boost, eating, growth, collisions, deaths, scores). Clients only send `{angle, boost}` ~20×/s, so they cannot teleport, change score or skip collisions.
- **Tick rate:** 30 ticks/s simulation, ~15 snapshots/s to each player (clients smooth between snapshots).
- **Rooms:** up to 12 players per room. *Quick Play* joins the fullest public room with space, or creates one. *Create Room* makes a private 5-letter-code room (never used by matchmaking). *Join Room* / `?room=CODE` links join by code.
- **Deaths:** every player in the room gets a `kill` event ("X ate Y"); the dead player gets their result and can respawn.
- **Leaderboard:** top 10 players in your room, live, computed from server state. Online player count is shown in the lobby and in-game.
- **Reconnect:** if the connection drops the client retries (1–5 s back-off) and rejoins your room code (or Quick Play). Your alien respawns with score 0.

## 4. Known limits
- Alien types (Classic, Cyclops, Triclops, Stalker, Robo, Spike) and colours are purely cosmetic; the server stores your choice and sends it to every player in the room.
- Skins and avatars are chosen on the client (cosmetic only, not verified). Profile pictures are emoji avatars (tap the avatar in the lobby); photo upload is not implemented.
- One Node process keeps all rooms in memory (fine for dozens–a few hundred players; no multi-server scaling, no accounts, no persistent global leaderboard).
- Coins/gems/shop/daily rewards still live in each player's browser (`localStorage`), as before.
- Practice vs Bots (offline) is kept as a separate mode and is not part of online matches.

## 5. Characters, skin shop & coins (where everything lives)
There are **no external image/model files** – all 16 characters are drawn in code, so nothing extra needs uploading. Everything is inside `public/index.html`:

| What | Where (search for this name in `public/index.html`) |
|---|---|
| 16 character definitions (name, price, colours, effects) | `const CH=[` |
| Head drawing (dragon head, crown, Santa hat, horn, robot…) | `function head(` |
| Body drawing, segment colours, glowing/animated effects | `function renderSnake(` |
| In-game drawing (uses the player's skin id `s.sp`) + trail | `function drawAlien(` |
| Legendary entrance (rings, sparks, flash, shake) | `function entr(` |
| Skin shop UI (HTML) | `<div id='sk2'>` |
| Skin shop styles | CSS block starting `#sk2{` |
| Buy / select / confirm / coin logic | `function buy(`, `function act(`, `function ask(` |
| Big rotating preview | `function pvDraw(` |
| Skin id sent to other players | `function pf(` (client) and `case 'hello'` in `server.js` |

- Prices: Basic = free starter, 250, 500, 1000, 1500, 2000, 2500, 3000, 3500, 4000, 5000, 6000, 7500, 10000, Shadow 12500, **Gangrage 15000**.
- Saved in the browser (`localStorage`): unlocked skins `alienIoP.own2`, coins `alienIoP.coins`, selected skin `alienIoCfg.ch`.
- Multiplayer: the selected skin id (0–15) is sent in the `hello` message, stored by the server and included in every snapshot, so every player sees everyone's real skin. The server clamps the id to 0–15.
- **Testing:** open the game with `?dev=1` (e.g. `http://localhost:3000/?dev=1`) to show a `+10000 coins` button in the skin shop. Remove that button before a public release if you don't want it.
- **Limit:** coins and unlocks are stored on the player's device, so a technical player could edit them. Skins are cosmetic only (no gameplay advantage). For real-money or competitive value, move coins/unlocks to a server database with accounts.

## 6. Power-ups, spawn protection, joystick, kill feed
- **Power-ups (online matches, server-authoritative):** 6 orbs per room, each lasts 10 s once picked up: 🧲 Magnet (pulls food from far away), 🛡️ Shield (enemy bodies can't kill you; walls still do), ✨ x2 Score. Logic: `mkpu`, `step` in `server.js`. Drawing: `PUE`/`PUC` and the `for(let i=0;i<pu.length` loop in `public/index.html`.
- **Spawn protection:** every spawn/respawn gets a 3 s shield (`sh: 90` in `spawn()` in `server.js`).
- **Practice vs Bots (offline)** does not have power-ups or spawn protection (online only).
- **Mobile joystick:** touch anywhere on the screen, a floating joystick appears where your thumb lands (search `id='joy'`).
- **Kill feed + game-over stats:** `feed`, `kills`, `fmt()` in `public/index.html`. Phones also vibrate when you eat, pick up a power-up or die.

## 7. Latest polish
- **Practice vs Bots now has power-ups and the 3 s spawn shield**, same rules as online.
- **First-time tip** (steer / boost / power-ups) shown once per device.
- **Kill streaks** (Double Kill, Triple Kill, Rampage) and a **Level Up** message.
- **Ping** (ms) shown in the online HUD (server answers `ping` with `pong`).
- **Low graphics** switch in Settings (also turns on automatically on slow devices).

## 8. Google Play release
This version is prepared for a Play Store launch: installable web app (PWA: manifest, service worker, offline Practice), privacy policy page, hardened server (no path traversal, per-IP and room limits, optional origin check), no third-party requests (no Google Fonts), and a `store/` folder with icon, feature graphic, 5 phone screenshots, listing text (English + Hindi) and `RELEASE_CHECKLIST.md`. Server env vars: `PORT`, `ALLOWED_ORIGINS`, `MAX_PER_IP` (8), `MAX_ROOMS` (300).
