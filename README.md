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

## UI redesign

The new design lives in Figma: **ProDomino UI (Client)**
(`https://www.figma.com/design/JuelfIraT35vtjM4WxIGW7/ProDomino-UI--Client-`). The redesign is
applied screen by screen to the real prefabs — there are no parallel "new UI" prefabs, so the game
keeps working while the look changes.

### How a screen is redesigned

Every screen follows the same loop, so a redesign never costs functionality:

1. **Inventory the screen.** Dump its hierarchy and the scripts that own it
   (`DashboardDiagnostics.LogScreenInventory`, `LogBrokenReferences`). Note every object a script
   holds a reference to — those objects are moved and re-skinned, never replaced or deleted.
2. **Capture the design.** Pull the frame from Figma (rect, colours, fonts, gaps) into a spec file,
   and export any artwork the frame uses.
3. **Write/extend an editor script** under `Assets/_ProDomino/Dashboard/Editor/` that rebuilds the
   screen in its own prefab: reparent the existing widgets into layout groups that mirror the
   Figma frame tree, apply the generated sprites and the type styles, and add nothing the runtime
   cannot keep alive.
4. **Re-apply scene overrides.** The game scene stores its own copy of layout values for the canvas
   instance, which wins over the prefab — run `Clean MainSceneDomDemo Layout Overrides` (or the
   screen's own revert step) after rebuilding.
5. **Verify, three ways:**
   - *numbers* — a per-element check against the Figma rects (`Verify Login + Register Against
     Figma` is the template; each screen gets its own table),
   - *pictures* — off-screen renders at 1920x1080 plus small windows (1280x720, 940x600, 600x900),
     including the screen's error/empty states,
   - *behaviour* — `PlayModeLoginProbe` style probe: run the game headless, raycast the entry
     point, click it and assert the screen opens.
6. **Commit** the editor script, the rebuilt prefab and the scene, and push.

**Rules that keep functionality intact**

- Keep every referenced object alive. Emptied legacy containers are taken out of the layout
  (`ignoreLayout`) or deactivated — their children are moved out first.
- Don't rename an object before `grep`ing for its name: some code looks widgets up by name.
- Don't touch serialized script fields from the restyler. If a link is already broken, repair it
  explicitly (`DashboardDiagnostics.RelinkClearedReferences`).
- Variable-height content (validation text, lists) belongs in layout groups, so it pushes the rest
  of the screen instead of overlapping it (`HideWhenEmpty` collapses an empty message).
- Fixed-size cards get `FitInArea`, which scales them down on small windows instead of clipping.

### Screen inventory

Everything the client can show, where it lives, and its redesign status.

| # | Screen | Prefab / location | Owner script | Opened from | Status |
|---|---|---|---|---|---|
| 1 | Sidebar | `Shared/Prefabs/ProDomino_MainCanvas.prefab` (`Background`) | `NavigationPanelController` | always | **Done** |
| 2 | Header | `Shared/Prefabs/UserControlCenter.prefab` | `OptionsUI`, `PlayerBestRankController` | always | **Done** |
| 3 | Dashboard (Play lobby) | `ProDomino_MainCanvas.prefab` (`Dashboard_Content`) | `DashboardController` | sidebar → Dashboard | **Done** |
| 4 | Login | `Authentication/Prefabs/AuthUI.prefab` (`SignIn_Container`) | `AuthUI`, `Credentials_AuthUI` | header → Log In | **Done** |
| 5 | Create Account | same prefab (`SignUp_Container`) | `Credentials_AuthUI` | login → Create an Account | **Done** |
| 6 | Forgot Password | same prefab (`Recovery_Container`) | `Credentials_AuthUI` | login → Forgot Password | **Done** |
| 7 | Leaderboard | `LeaderboardSystem/Prefabs/LeaderboardUI_NavPanel_New.prefab` (and `_Old`) | `LeaderboardUI_New` / `_Old` | sidebar → Leaderboard, rank chip | **Next** |
| 8 | Shop | `Prefabs/UI/Shop_Screen.prefab` | `ShopUI`, `ShopManager` | sidebar → Shop, token chip | Planned |
| 9 | Achievements | `Prefabs/UI/Achievements_Screen.prefab` | `AchievementUI` | sidebar → Achievements | Planned |
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
| 23 | Game mode select | `Prefabs/UI/GameModeSelectUI.prefab` | `GameModeConfig` | Play panel (full options) | Planned |
| 24 | Quick match | `QuickMatchSystem/Prefabs/QuickMatchUI_NavPanel.prefab` | `QuickMatchController` | dashboard cards | Planned |
| 25 | Post-match results | `Prefabs/UI/Post_match_Results_Popup.prefab` | `PostMatchResultController` | end of a match | Planned |
| 26 | In-match HUD / board | `DominoTemplate_v2/Prefabs/*` (`Gameplay_FrameContainer`, `ScoreContainer_*`) | `GameController`, `DominoView` | during a match | Planned |
| 27 | Loading / retry overlay | `HandleProcessesSystem/Prefabs/HandleProcessesController.prefab` | `HandleProcessesController` | any pending request | Planned |
| 28 | Session / delete account / provider error | `_GameManager/Prefabs/*`, `Authentication/Prefabs/AuthProviderError_PopUp.prefab` | `SessionPopUp`, `DeleteAccountController` | error states | Planned |
| 29 | Tournament | not implemented | — | sidebar → Tournament | Needs design **and** code |

### Order of work

**Phase 0 — shared UI kit (do once, before #7).** The sidebar, header and auth screens each
generate their own sprites and type styles. Pull them into one editor module (colour tokens, radii,
fonts, `MakePanelSprite`, button/field/chip builders) so later screens stay consistent and a token
change is a one-line edit. No visual change expected; re-render all three finished screens to prove
it.

**Phase 1 — Leaderboard (#7).** Second sidebar entry, linked from the header rank chip and the
dashboard. It introduces the list components (tabs, filter dropdown, table rows, your-rank row)
that Achievements, Club and Party reuse.

**Phase 2 — Shop (#8)**, because the header token chip already opens it and it carries revenue.

**Phase 3 — Achievements (#9)**, reusing Phase 1's grid/row components.

**Phase 4 — Party, friend list, friendship request (#10, #11, #12)** — one flow, done together;
the dashboard invite card and the sidebar party rows already point at it.

**Phase 5 — Club (#13).** **Phase 6 — Rules and Review (#14, #15).** **Phase 7 — Help (#16).**

**Phase 8 — account pop-ups (#17–#22)**, all small and sharing the auth card style.

**Phase 9 — match flow (#23, #24, #25).** **Phase 10 — in-match HUD (#26)**, the largest piece and
the one that touches gameplay code; keep it on its own branch.

**Phase 11 — system overlays (#27, #28)** and finally **Tournament (#29)**, which needs both a
design and new gameplay code.

### Next task in detail — Leaderboard (#7)

1. **Decide which panel survives.** `LeaderboardUI_NavPanel_New` and `..._Old` both live under
   `InnerScreen` and both implement `INavigationPanel`; `LeaderboardManager` picks whichever is
   *active* (`FindFirstObjectByType<AbstractLeaderboardUI>`), which is fragile. Keep `_New`, take
   `_Old` out of the canvas (deactivate, don't delete), and confirm `LeaderboardManager`,
   `PlayerBestRankController` and the rank chip still resolve.
2. **Inventory** `LeaderboardUI_NavPanel_New.prefab`: entry template (`LeaderboardEntry_Prefab`),
   filter (`LeaderboardFilter_Prefab`), `Rankings_PlayerContainer`, the mode/period selectors and
   the empty state. Record every serialized reference in `LeaderboardUI_New`.
3. **Capture the Figma leaderboard frame** (needs a working token) — table columns, row height,
   rank badges, the highlighted "you" row, tabs and the period dropdown.
4. **Rebuild** in a new `LeaderboardRestyler` editor script: header + tabs row, a scroll view whose
   content is a vertical layout group, restyled row template (rank, avatar, name, score, trend),
   sticky "your rank" row, empty/loading state. Rows must stay the prefab the manager instantiates.
5. **Verify**: rect table vs Figma; renders at four window sizes with 0, 3 and 50 entries; a
   play-mode probe that opens the panel from the sidebar and asserts rows are built and the
   player's own row is highlighted.
6. **Commit** script + prefab + scene, and re-render the dashboard to confirm the rank chip still
   matches.

### Blocked / needed from you

- **A fresh Figma token** (the current one returns `403 Token expired`). Without it I cannot read
  any new frame, so screens from #7 on can only be built from screenshots. Two assets are still
  missing for the finished screens as well: the decorative artwork behind the auth cards and the
  design's padlock icon (currently drawn in code).
- **Which leaderboard panel to keep** (see step 1 above) — my recommendation is `_New`.

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
- Two design assets are missing from the auth screens: the decorative artwork behind the card and
  the padlock icon (drawn in code for now). Both need a Figma export.
- Both leaderboard panels (`_New` and `_Old`) are still in the canvas and `LeaderboardManager`
  picks whichever happens to be active.
- Everything from **Leaderboard** onwards in the redesign is still the old look — see
  [UI redesign](#ui-redesign).
