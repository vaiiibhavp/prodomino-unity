# ProDomino

Online multiplayer dominoes game built with **Unity 6000.4.1f1** (URP, WebGL/Web target).
Players sign in, play ranked or casual matches against other players or AI, and progress
through leaderboards, achievements, missions, clubs and a cosmetics shop.

Version: `0.7.15` · Main scene: `Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity`

---

## Getting started

1. Open the project with **Unity 6000.4.1f1** (Unity Hub → Add → this folder).
2. Open the scene `Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity`.
3. Press **Play**. The game starts on the Dashboard; sign-in and online play require the
   Unity Services / Firebase credentials configured for the project.

`Library/`, `Temp/`, `Logs/`, `UserSettings/` and backend build output are not in the repo —
Unity regenerates them on first open (the first import takes a while).

---

## Repository layout

| Path | What it is |
|---|---|
| `Assets/_ProDomino/` | All game code and content, split per system (see below) |
| `Assets/_tests/` | Test scenes, including the main playable scene |
| `Assets/Localization/` | Localization tables (English, Spanish) |
| `Backend/` | C# Cloud Code modules + shared libraries (`Project`, `FirebaseSharedLibrary`, `HelperSharedLibrary`) |
| `FirebaseFunctions/` | Firebase project config for cloud functions |
| `Packages/`, `ProjectSettings/` | Unity package manifest and project settings |

### Gameplay systems (`Assets/_ProDomino/`)

Each system is its own assembly definition:

`Authentication` · `_GameManager` · `GameModes` · `RelayMultiplayer` · `NetworkSystem` ·
`QuickMatchSystem` · `LeaderboardSystem` · `Achievements` · `MissionSystem` · `ClubSystem` ·
`FriendSystem` · `Shop` · `Customization` · `InAppPurchaseSystem` · `AdSystem` ·
`AnalyticsSystem` · `Notification` · `Nationality` · `NavigationSystem` · `Dashboard` ·
`Options` · `AccountSystem` · `ReplaySystem` · `LearningTool` · `Insights` ·
`HandleProcessesSystem` · `Shared` · `_Backend`

---

## Tech stack

- **Engine:** Unity 6000.4.1f1, Universal Render Pipeline, uGUI + TextMeshPro
- **Multiplayer:** Netcode for GameObjects 2.11, Unity Relay/Multiplayer, Cinemachine
- **Unity Services:** Cloud Code, Cloud Save, Leaderboards, Friends, Remote Config, Deployment
- **Backend:** C# Cloud Code modules (account deletion, e-mail verification, password recovery,
  protected player/game data) plus Firebase (auth, messaging, functions) through a WebGL bridge
- **Other:** Facebook SDK, Unity Localization (en/es), Input System, Visual Scripting

---

## Game modes and match types

Modes (`ProDomino.Shared.GameMode`): `french`, `block`, `draw`, `five`, `concentrate`, plus `replay`
for reviewing past games.

Match types (`GameType`): `singlePlayerIA` (vs AI, local), `casual` (online, bots fill empty
seats), `competitive` (online ranked). Leaderboard ids are `{GameMode}{NumberOfPlayers}`,
e.g. `Block2`.

---

## Main menu UI

The whole menu lives in one shared prefab, `Assets/_ProDomino/Shared/Prefabs/ProDomino_MainCanvas.prefab`:

- **Sidebar** (`Background/NavegationPanelController`) — Dashboard, Leaderboard, Achievements,
  Club, Party, Tournament, Shop, Rules, Review, plus Settings/Help and the invite card.
- **Header** (`UserControlCenter.prefab`) — rank chip (class per game mode), token balance,
  notifications, nationality and the profile/account menu.
- **Screens** (`MiddleScreen_Scalable/InnerScreen`) — one panel per navigation entry
  (`INavigationPanel`), switched by `NavigationPanelController`, plus the shared pop-ups
  (account, customization, friend list, settings, post-match results, login, …).

### Dashboard

The Dashboard (`Dashboard_Content` + `DashboardController`) is the home screen and the lobby view
of the Play panel. Its cards start matches through the existing `GameModeConfig`:

| Card | Starts |
|---|---|
| AI | Instant match vs AI (French, 1v1, random difficulty) |
| Random Players | Online casual search, 1v1 |
| Competitive | Ranked search (requires a verified account) |
| Block | Block game, online casual, 1v1 |
| Concentrate | Solo Concentrate, 28 tiles |
| Play & Win | Block game, online casual |

While searching, an overlay shows the mode, a timer and a Cancel button. The Dashboard hides
itself while a match is on screen and returns when the match ends.

### Editor tools (`ProDomino` menu)

Used to (re)apply the UI design to the prefabs and to verify the result headlessly:

- `ProDomino/Dashboard/Restyle Sidebar + Render`
- `ProDomino/Dashboard/Restyle Header + Dashboard + Render`
- `ProDomino/Dashboard/Clean MainSceneDomDemo Layout Overrides`
- `ProDomino/Dashboard/Render Main Canvas To PNG` and `Render MainSceneDomDemo Canvas To PNG`

The render tools write PNGs of the canvas (edit-mode and simulated runtime) so UI changes can be
checked without entering Play mode.

---

## WebGL build

The project targets **Web (WebGL)**. The relevant settings are already saved in
`ProjectSettings` — don't change them per build unless you mean to:

| Setting | Value |
|---|---|
| Template | `WebGL_ProDomino_Template` (in `Assets/WebGLTemplates/`) |
| Compression | Brotli, with decompression fallback **enabled** |
| Memory | 32 MB initial, 2048 MB max, geometric growth |
| Exceptions | Full, without stacktrace |
| Data caching | Enabled (build files cached in the browser) |
| Linker target | WebAssembly, threads off |
| Scripting defines (WebGL) | `DOTWEEN;PAYPAL_IAP` |

### Build from the Editor

1. **File → Build Settings** → platform **Web** → *Switch Platform* (first time only; it takes a while).
2. Check the scene list — `Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity`
   must be enabled and is the scene the game boots into.
3. **Build** (or *Build And Run*) and pick an output folder, e.g. `Builds/WebGL/`.
4. `Assets/Editor/WebGLPostBuild.cs` runs automatically after the build and copies
   `firebase-messaging-sw.js` next to `index.html`. Push notifications don't work without it,
   so check the console for the "copied" log line.

The output folder contains `index.html`, `Build/` (the `.br` compressed engine and data files)
`TemplateData/` and `firebase-messaging-sw.js`. Builds are **not** committed (`.gitignore`
excludes `Build/` and `Builds/`).

> There is no headless/CI build script in the repo yet, so builds are made from the Editor.

### Testing the build locally

Open the build through a local web server, not `file://`, or the browser blocks the engine
files and the service worker:

```bash
cd Builds/WebGL && python -m http.server 8080   # then open http://localhost:8080
```

---

## Deploy

### Web build hosting

Not configured in this repository — the build folder is uploaded to whichever web host you use.
Whatever the host, it must serve the Brotli files correctly:

- Serve `Build/*.br` with `Content-Encoding: br` and the matching `Content-Type`
  (`application/wasm` for `.wasm.br`, `application/javascript` for `.js.br`).
- Without those headers the player still loads, because decompression fallback is enabled, but
  it is noticeably slower to start.
- Serve `firebase-messaging-sw.js` from the site root, over HTTPS, so push notifications work.

### Firebase (project `playprodomino`)

`FirebaseFunctions/` holds the Firebase config (`.firebaserc`, `firebase.json`) for the cloud
functions codebase. The functions source itself is not in this repository.

```bash
cd FirebaseFunctions && firebase deploy --only functions
```

### Unity Cloud Code

`Backend/` contains the C# Cloud Code modules (account deletion, e-mail verification, password
recovery, protected data) and their shared libraries. They are published to Unity Cloud Code
with the **Deployment** package (`com.unity.services.deployment`) from the Unity Editor.

---

## Known gaps

- Dashboard statistics (games played / players online) and the monthly challenge progress are
  still static placeholder values.
- The old game-mode selection screen is kept but hidden; its extra options (Draw, Five, 2v2,
  manual difficulty) are not reachable from the new Dashboard.
- The reference design's "Games" and "Friends List" sidebar rows are not implemented.
