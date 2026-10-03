# ProDomino

Online multiplayer dominoes game built with **Unity 6000.4.1f1** (URP, WebGL/Web target).
Players sign in, play ranked or casual matches against other players or AI, and progress
through leaderboards, achievements, missions, clubs and a cosmetics shop.

Version: `0.7.15` · Main scene: `Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity`

> The production scene lives under `Assets/_tests/` for historical reasons. It is the scene the
> game boots into, despite the folder name.

---

## Contents

- [Getting started](#getting-started)
- [Repository and branches](#repository-and-branches)
- [Tech stack](#tech-stack)
- [Game modes and match types](#game-modes-and-match-types)
- [Main menu UI](#main-menu-ui)
- [UI redesign](#ui-redesign)
- [WebGL build](#webgl-build)
- [Deploy](#deploy)
- [Developer tooling (Unity MCP)](#developer-tooling-unity-mcp)
- [Known gaps](#known-gaps)

---

## Getting started

1. Clone the repository (see [Repository and branches](#repository-and-branches)).
2. Open the project with **Unity 6000.4.1f1** (Unity Hub → Add → this folder). The first import
   takes a while; `Library/`, `Temp/`, `Logs/` and `UserSettings/` are regenerated locally and are
   not in the repo.
3. Open `Assets/_tests/TemporalTestDemoMultiplayer/Scene/MainSceneDomDemo.unity`.
4. Press **Play**. The game starts on the Dashboard.

Sign-in and online play need the project linked to its Unity Cloud project
(**Edit → Project Settings → Services**) and the Firebase project `playprodomino`. Ask the project
owner for access to both.

Local-only secrets (for example the Figma API token used for design work) go in `.env.local` at
the repo root. That file is gitignored; never commit tokens.

---

## Repository and branches

| Remote | URL |
|---|---|
| `neworigin` (active) | `git@github.com:vaiiibhavp/prodomino-unity.git` |
| `origin` (legacy) | `git@github.com:pipaliyavivek/ProDomino.git` |

- Day-to-day work happens on `dev_jaimin` and is pushed to `neworigin`.
- `main` is the release branch; open pull requests against it.
- Commit freely, but push only after review.

### Layout

| Path | What it is |
|---|---|
| `Assets/_ProDomino/` | All game code and content, split per system (see below) |
| `Assets/DominoTemplate_v2/` | Board, tiles and in-match game controller prefabs |
| `Assets/_tests/` | Test scenes, including the main playable scene |
| `Assets/Localization/` | Localization tables (English, Spanish) |
| `Assets/WebGLTemplates/` | `WebGL_ProDomino_Template` |
| `Backend/` | C# Cloud Code modules + shared libraries (`Project`, `FirebaseSharedLibrary`, `HelperSharedLibrary`) |
| `FirebaseFunctions/` | Firebase project config (`.firebaserc`, `firebase.json`) only |
| `.agents/`, `Tools/` | Unity MCP bridge and its setup scripts |
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
- **Other:** Facebook SDK, Unity Localization (en/es), Input System, Visual Scripting, DOTween

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

---

## UI redesign

The design lives in Figma: **ProDomino UI (Client)**
(`https://www.figma.com/design/JuelfIraT35vtjM4WxIGW7/ProDomino-UI--Client-`). It is applied
screen by screen to the real prefabs. There are no parallel "new UI" prefabs, so the game keeps
working while the look changes.

### How a screen is redesigned

1. **Inventory the screen.** List its hierarchy and the scripts that own it. Every object a script
   references is moved and re-skinned, never replaced or deleted.
2. **Capture the design.** Read the frame from Figma (rects, colours, fonts, gaps) and export any
   artwork it uses.
3. **Edit the prefab directly.** Reparent existing widgets into layout groups that mirror the Figma
   frame tree and apply sprites and type styles. Add nothing the runtime cannot keep alive.
4. **Clear scene overrides.** `MainSceneDomDemo.unity` stores its own layout values for the canvas
   instance, and those win over the prefab. Revert them after editing the prefab.
5. **Verify** in the Editor at 1920x1080 and at small windows (1280x720, 940x600, 600x900),
   including the error and empty states, then open the screen from its entry point in Play mode.
6. **Commit** the prefab and the scene.

The first screens (sidebar, header, dashboard, auth) were built with editor restylers under
`Assets/_ProDomino/Dashboard/Editor/` on top of the shared kit `PdUiKit.cs` (colour tokens, radii,
fonts, sprite factory). Those scripts and their `ProDomino/Dashboard/*` menu items still exist,
but new screens are edited in the prefab directly.

**Rules that keep functionality intact**

- Keep every referenced object alive. Take emptied legacy containers out of the layout
  (`ignoreLayout`) or deactivate them, after moving their children out.
- Search the code for an object's name before renaming it: some code looks widgets up by name.
- Don't change serialized script fields while restyling. Repair already-broken links explicitly.
- Put variable-height content (validation text, lists) in layout groups so it pushes the rest of
  the screen instead of overlapping it (`HideWhenEmpty` collapses an empty message).
- Give fixed-size cards `FitInArea`, which scales them down on small windows instead of clipping.

### Screen inventory

| # | Screen | Prefab / location | Owner script | Opened from | Status |
|---|---|---|---|---|---|
| 1 | Sidebar | `Shared/Prefabs/ProDomino_MainCanvas.prefab` (`Background`) | `NavigationPanelController` | always | **Done** |
| 2 | Header | `Shared/Prefabs/UserControlCenter.prefab` | `OptionsUI`, `PlayerBestRankController` | always | **Done** |
| 3 | Dashboard (Play lobby) | `ProDomino_MainCanvas.prefab` (`Dashboard_Content`) | `DashboardController` | sidebar → Dashboard | **Done** |
| 4 | Login | `Authentication/Prefabs/AuthUI.prefab` (`SignIn_Container`) | `AuthUI`, `Credentials_AuthUI` | header → Log In | **Done** |
| 5 | Create Account | same prefab (`SignUp_Container`) | `Credentials_AuthUI` | login → Create an Account | **Done** |
| 6 | Forgot Password | same prefab (`Recovery_Container`) | `Credentials_AuthUI` | login → Forgot Password | **Done** |
| 7 | Leaderboard | `LeaderboardSystem/Prefabs/LeaderboardUI_NavPanel_New.prefab` | `LeaderboardUI_New` | sidebar → Leaderboard, rank chip | **Done** |
| 8 | Shop | `Prefabs/UI/Shop_Screen.prefab`, `Shop/Prefabs/Shop_Element.prefab` | `ShopUI`, `ShopManager` | sidebar → Shop, token chip | **Done** |
| 9 | Achievements | `Prefabs/UI/Achievements_Screen.prefab`, `Achiev_List_Container.prefab` | `AchievementUI` | sidebar → Achievements | **Done** |
| 10 | Party / Form party | `FriendSystem/Prefabs/Form_party_Screen.prefab` | `PartyController` | sidebar → Party | Planned |
| 11 | Friend list | `FriendSystem/Prefabs/FriendList_PopUp.prefab` | `PartyController` | account menu → Friend list | Planned |
| 12 | Friendship request | `FriendSystem/Prefabs/ConfirmFriendship_PopUp.prefab` | `ConfirmFriendshipPopUp` | invite link | Planned |
| 13 | Club | `Prefabs/UI/ClubUI_NavPanel.prefab` | `ClubUI` | sidebar → Club | Planned |
| 14 | Rules (learning tool) | `LearningTool/Prefabs/LearnTool_UI.prefab` | `LearningToolUI` | sidebar → Rules | Planned |
| 15 | Review (replays) | `LearningTool/Prefabs/ReviewUI_NavPanel.prefab` | `ReviewUI`, `ReplayManager` | sidebar → Review | Planned |
| 16 | Help | `Prefabs/UI/Help_Screen.prefab` | `HelpScreenUI` | sidebar → Help | Planned |
| 17 | Settings | `Shared/Prefabs/Settings/SettingsController_PopUp.prefab` | `SettingsController` | sidebar → Settings | Planned |
| 18 | Account data | `AccountSystem/Prefabs/AccountData_PopUp.prefab` | `AccountDataController` | account menu → Account | Planned |
| 19 | Customization | `Customization/Prefabs/Customization_PopUp.prefab` | `CustomizationController` | account menu → Customization | Planned |
| 20 | Email verification | `Prefabs/UI/EmailVerificationPopup.prefab` | `OptionsUI` | after sign-up | Planned |
| 21 | Daily bonus | `MissionSystem/Prefabs/DailyBonus_PopUp.prefab` | `MissionManager` | daily login | Planned |
| 22 | Monthly subscription | `InAppPurchaseSystem/Prefabs/MonthlySubscription_PopUp.prefab` | `MonthlySubscriptionPopUp` | header / shop | Planned |
| 23 | Game mode select | `Prefabs/UI/GameModeSelectUI.prefab` | `GameModeConfig` | Play panel (full options) | **Done** |
| 24 | Quick match | `QuickMatchSystem/Prefabs/QuickMatchUI_NavPanel.prefab` | `QuickMatchController` | dashboard cards | Planned |
| 25 | Post-match results | `Prefabs/UI/Post_match_Results_Popup.prefab` | `PostMatchResultController` | end of a match | Planned |
| 26 | In-match HUD / board | `DominoTemplate_v2/Prefabs/*` (`Gameplay_FrameContainer`, `ScoreContainer_*`) | `GameController`, `DominoView` | during a match | In progress |
| 27 | Loading / retry overlay | `HandleProcessesSystem/Prefabs/HandleProcessesController.prefab` | `HandleProcessesController` | any pending request | Planned |
| 28 | Session / delete account / provider error | `_GameManager/Prefabs/*`, `Authentication/Prefabs/AuthProviderError_PopUp.prefab` | `SessionPopUp`, `DeleteAccountController` | error states | Planned |
| 29 | Tournament | not implemented | — | sidebar → Tournament | Needs design **and** code |

#26 status: the Concentrate board uses the new dark/neon look; Block, Draw, French and Five
prefabs are synced to the same layout but still need the board sizing fix listed in
[Known gaps](#known-gaps).

### Order of remaining work

1. **In-match HUD (#26)** — finish the board across all modes. It touches gameplay code, so keep
   it on its own branch.
2. **Party, friend list, friendship request (#10–#12)** — one flow, done together.
3. **Club (#13)**, then **Rules and Review (#14, #15)**, then **Help (#16)**.
4. **Account pop-ups (#17–#22)** — small, and they share the auth card style.
5. **Quick match and post-match results (#24, #25)**.
6. **System overlays (#27, #28)**, then **Tournament (#29)**, which needs design and new code.

### Design coverage (Figma)

The file has four pages: **High-Fidelity-Web-UI** (desktop source of truth), **Mobile
Responsive UI** (same flows at phone width), **Design System** (palette, typography, buttons,
tabs, popups, header, sidebar, states, domino tiles) and **Draft**.

| Figma flow (node) | Screens | Client screens |
|---|---|---|
| Onboarding Flow (`9:6`) | Login, Registration, Account created OK / failed, Forgot password, Create new password ×2 | #4–#6 and `AuthResult_PopUp` |
| Dashboard (`113:3362`, `174:9469`, `780:25875`) | Dashboard, before login, Monthly / Daily challenge, header before & after login | #2, #3 |
| Leaderboard (`263:24215`) | Leaderboard, empty state | #7 |
| Shop Flow (`44:4`) | Tiles, Icons, Frames, Boards, Boards pop-up, Badges | #8 |
| Achievements & Rewards (`188:47834`) | 2 screens | #9 |
| Party Flow (`175:10323`) | Party, send invitation, waiting, start game, select mode, mode selected, 2 pop-ups | #10 |
| Friends List (`115:3033`) | 11 states: no matches, no friends, has friends, add / remove / not-found pop-ups | #11, #12 |
| Club (`167:6176`) | 20 screens: empty states, detail, members, chat, applications, roles, create / leave pop-ups | #13 |
| Rules (`94:1640`) | Rules, Rules/Block, Rules/Concentrate | #14 |
| Review Flow (`201:15821`) | Review, on hover, Review detail | #15 |
| Help (`68:218`) | Help | #16 |
| Settings (`63:199`) | Game type selection pop-up | #17, #23 |
| Profile (`249:24823`) | Profile, Account settings, Edit profile, Delete profile ×3 | #18, #19 |
| Notification (`248:42429`) | Notification | header bell (not yet a client screen) |
| Payment Portal (`441:34252`) | 2 screens | #22 and shop checkout |
| Block Game — Single vs AI (`178:10920`) | Games, game type pop-up (+ before login), 1v1 / 1v3 / 2v2 boards, result pop-ups | #23, #25, #26 |
| Block Game — Casual & Competitive (`185:24058`) | 13 screens incl. in-match chat | #23, #25, #26 |
| Concentrate — Single vs AI (`235:23741`) | 13 screens, solo / 1v1 / 1v3 / 2v2, 28 and 56 tiles | #23, #25, #26 |
| Tournament (`188:44375`) | Tournament | #29 |

Notes:

- The design has **"before login" variants** (dashboard, header, games, game-type pop-up) that
  the client does not implement; the logged-out state shows the same screens with empty data.
- The **Mobile Responsive UI** page is a real phone layout. Built screens only scale to fit
  (`FitInArea`); a proper mobile pass comes after the desktop screens.
- The design does not cover the loading/retry overlay (#27) or the session/error pop-ups (#28);
  those follow the Design System components.

Design artwork used by built screens is exported from Figma into
`Assets/_ProDomino/_UI/Icons/`.

---

## WebGL build

The project targets **Web (WebGL)**. The settings below are saved in `ProjectSettings`; don't
change them per build unless you mean to.

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

1. **File → Build Settings** → platform **Web** → *Switch Platform* (first time only; slow).
2. Make sure `MainSceneDomDemo.unity` is enabled in the scene list. It is the boot scene.
3. **Build** (or *Build And Run*) into an output folder, e.g. `Builds/WebGL/`.
4. `Assets/Editor/WebGLPostBuild.cs` runs after the build and copies `firebase-messaging-sw.js`
   next to `index.html`. Push notifications don't work without it, so check the console for the
   "copied" log line.

The output contains `index.html`, `Build/` (Brotli-compressed engine and data), `TemplateData/`
and `firebase-messaging-sw.js`. Builds are not committed (`.gitignore` excludes `Build/` and
`Builds/`). There is no headless/CI build script yet.

### Testing the build locally

Serve the build over HTTP, not `file://`, or the browser blocks the engine files and the service
worker:

```bash
cd Builds/WebGL
python -m http.server 8080
```

Then open `http://localhost:8080`.

---

## Deploy

### Web build hosting

Not configured in this repository; upload the build folder to your web host. The host must:

- Serve `Build/*.br` with `Content-Encoding: br` and the matching `Content-Type`
  (`application/wasm` for `.wasm.br`, `application/javascript` for `.js.br`). Without these
  headers the player still loads (decompression fallback is on) but starts noticeably slower.
- Serve `firebase-messaging-sw.js` from the site root over HTTPS, so push notifications work.

### Firebase (project `playprodomino`)

`FirebaseFunctions/` holds only the Firebase config. The functions source is not in this
repository, so deploying functions has to be done from the repository that holds the source.

### Unity Cloud Code

`Backend/` contains the C# Cloud Code modules (account deletion, e-mail verification, password
recovery, protected data) and their shared libraries. Publish them to Unity Cloud Code with the
**Deployment** package (`com.unity.services.deployment`) from the Unity Editor
(**Window → Deployment**).

---

## Developer tooling (Unity MCP)

The repo includes a **Model Context Protocol (MCP)** bridge that lets an AI coding assistant
(set up for [Antigravity](https://deepmind.google/antigravity)) control the live Unity Editor:
read logs, inspect GameObjects, run menu items, toggle Play mode and capture screenshots.

```
AI assistant  ◄── stdio (JSON-RPC) ──►  server.js  ◄── HTTP 127.0.0.1:8080 ──►  UnityMcpBridge.cs
                                        .agents/mcp/unity-bridge/              Assets/_ProDomino/Dashboard/Editor/
```

- **`UnityMcpBridge.cs`** — `[InitializeOnLoad]` Editor script. Starts an `HttpListener` on
  `127.0.0.1` (ports 8080–8084) and runs Unity API calls on the main thread via
  `EditorApplication.delayCall`.
- **`server.js`** — Node.js process that speaks MCP over stdio and forwards each tool call to the
  bridge over HTTP.

### Setup

Requirements: Node.js ≥ 18 and the Unity Editor open on this project.

The project-level config [`.agents/mcp.json`](.agents/mcp.json) points to the bundled
[`server.js`](.agents/mcp/unity-bridge/server.js) by relative path, so opening the repo in
Antigravity is enough. To register the tools globally (all sessions), run:

```powershell
powershell -ExecutionPolicy Bypass -File Tools\setup-antigravity.ps1
```

On macOS/Linux use `Tools/setup-antigravity.sh`. The script merges into
`~/.gemini/config/mcp_config.json` and keeps any other servers you have configured.

### Tools

| Tool | Description |
|---|---|
| `unity_status` | Unity version, active scene, play mode state |
| `unity_execute_menu_item` | Run any `MenuItem` on the main thread |
| `unity_get_logs` | Recent console logs (filter by Error / Warning / Log) |
| `unity_clear_logs` | Clear the captured log buffer |
| `unity_get_hierarchy` | Root GameObjects in the active scene |
| `unity_inspect_object` | Components and RectTransform of a named GameObject |
| `unity_play_mode` | Start, pause or stop Play mode |
| `unity_capture_screenshot` | Screenshot of the Game/Scene view |

### Troubleshooting

| Problem | Fix |
|---|---|
| Is the bridge running? | **ProDomino → MCP → Check Status** logs `[UnityMcpBridge] Running: True on port 8080`. **ProDomino → MCP → Restart Bridge Server** restarts it. |
| `ECONNREFUSED` on a tool call | Unity is not open or has not finished compiling. Wait for `[UnityMcpBridge] Connected` in the console. |
| Port 8080 in use | The bridge tries 8080–8084. If all are taken, free one or change `DefaultPort` in `UnityMcpBridge.cs`. |
| Tools missing in the assistant | Check that `.agents/mcp.json` exists, or run the global setup script. |

---

## Known gaps

- **Leaderboard panels:** both `LeaderboardUI_NavPanel_New` and `_Old` are under `InnerScreen`,
  and `LeaderboardManager` uses whichever is active
  (`FindFirstObjectByType<AbstractLeaderboardUI>`). Recommended: keep `_New` and deactivate
  `_Old` (don't delete it), then confirm `LeaderboardManager`, `PlayerBestRankController` and the
  rank chip still resolve.
- **Board sizing outside Concentrate:** Block, Draw, French and Five boards lack Concentrate's
  `AspectRatioFitter` sizing chain, so the board can collapse to 0x0 at runtime.
- **Password reset:** the design's "Create new password" and "Password updated" screens need an
  in-app reset (a backend step that accepts a reset code, and an email link that opens the game
  with it). Recovery currently only emails a link.
- **Onboarding backdrop:** the design's 3D-domino background behind the auth card is not built;
  the pop-up opens over the dimmed dashboard.
- **Dashboard data:** games played, players online and monthly challenge progress are static
  placeholders.
- **Hidden game options:** the old game-mode screen is hidden; Draw, Five, 2v2 and manual
  difficulty are not reachable from the Dashboard.
- **Sidebar:** the design's "Games" and "Friends List" rows are not implemented.
- **Logged-out variants:** the "before login" dashboard, header and game-type screens are not
  built.
