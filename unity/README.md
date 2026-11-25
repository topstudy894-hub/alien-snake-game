# Alien Snake .io – Unity project

A complete Unity (C#) version of the game. It plays **offline** (Practice vs 8 bots, no server needed) and **online** on the same Node.js server as the web version (`hungry-alien-online/server.js`, unchanged). Web and Unity players can share a room.

> **Status: written but NOT yet compiled or run in Unity.** The tools used to write it have no Unity Editor or C# compiler (only bracket-balance was checked). Expect a few compile errors on the first open – paste them back and they get fixed. Not tested on a real phone. No APK/build is included.

## 1. Open it (5 minutes)
1. Install **Unity Hub** + **Unity 2022.3 LTS** (`2022.3.47f1` is written in `ProjectSettings/ProjectVersion.txt`; any 2022.3.x works – Hub will offer to use the one you have).
2. Unity Hub > **Add > Add project from disk** > pick the `AlienSnakeUnity` folder. Wait for packages to import (internet needed once).
3. **Window > TextMeshPro > Import TMP Essential Resources** (one click, needed for text).
4. Press **Play** (any scene, even empty – `Bootstrap` builds everything at runtime).

Try it without any server: lobby > **Practice (offline)**. For online, set `DefaultServerUrl` in `Scripts/Bootstrap.cs` (`ws://localhost:3000` for a local server started with `npm start`; `wss://...` for a hosted one).

Controls: mouse steers, **Space / right mouse = boost**. Phone: drag anywhere (floating joystick) + BOOST button.

## 2. Android build
Build Settings > Android, IL2CPP + ARM64. Player Settings > **Internet Access: Require**. Use `wss://` for real releases; `ws://` only on the same Wi-Fi with the PC's IP (not `localhost`). WebGL is not supported by this networking code.

## 3. What is in the game
Lobby (Play = Quick Play, Create Room, Join Room by 5-letter code, Practice offline, Daily Reward, Skins & Shop, sound on/off), 16 characters incl. Gangrage Legendary (dragon head, spikes, glowing nodes, aura, permanent trail, **entrance: rings + screen flash + shake**), skin shop (BUY/SELECT/LOCK, preview, purchase confirmation), coins (earned = score/2 on death; start 300), **7-day daily reward** (50,100,150,200,250,300,500), power-ups (magnet / shield / x2) + 3 s spawn shield, boost, zoom-out as you grow, live top 10, kill feed, game-over stats, sound effects (generated in code), auto-reconnect and rejoin.

## 4. Files (`Assets/AlienSnake/Scripts`)
`Bootstrap` (starts everything, server URL) · `NetClient` (WebSocket + reconnect) · `Protocol` (snapshot parsing) · `SimRoom` (offline rules + bots, same numbers as server.js) · `MatchController` (arena, camera, rooms, practice) · `SnakeView` (draws an alien) · `SkinDatabase` (16 skins) · `PlayerProfile` (+ `DailyReward`; coins/unlocks rules) · `SpriteFactory` · `InputController` · `GameUI` · `Sfx`.

## 5. Not included yet
Team and Time modes, missions / gems / rank screens, minimap, music, electric-bolt body effect, emoji food/avatars (food = coloured circles), real art (everything is drawn by code), **ads** (waiting for the LevelPlay "Ads Mediation" package check), a signed Android build.

## 6. Limits
Coins and unlocks are stored on the device (editable by a technical player; skins are cosmetic only). For real money or competitive value move them to a server with accounts. Daily reward uses the device clock.
