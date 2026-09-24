using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Linq;
using Timba.Patterns;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.Dashboard
{
    /// <summary>
    /// Home dashboard. It is the lobby view of the Play panel: visible while the Play panel is
    /// open and no match is on screen, and its cards start matches through <see cref="GameModeConfig"/>
    /// exactly like the old game-mode screen and QuickMatch panel did.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        [Header("Visibility")]
        [SerializeField] private CanvasGroup dashboardCanvasGroup;
        [SerializeField] private GameModeConfig gameModeConfig;
        [Tooltip("Old lobby background of the Play panel, replaced by the dashboard.")]
        [SerializeField] private Image legacyLobbyBackground;

        [Header("Cards")]
        [SerializeField] private Button playAndWinButton;
        [SerializeField] private Button aiMatchButton;
        [SerializeField] private Button randomPlayersButton;
        [SerializeField] private Button competitiveButton;
        [SerializeField] private Button blockButton;
        [SerializeField] private Button concentrateButton;

        [Header("Matchmaking overlay")]
        [SerializeField] private CanvasGroup matchmakingOverlay;
        [SerializeField] private TMP_Text matchmakingTitle;
        [SerializeField] private TMP_Text matchmakingDetails;
        [SerializeField] private TMP_Text matchmakingTimer;
        [SerializeField] private Button cancelMatchmakingButton;
        [Tooltip("How long to wait for a search to begin after a card is pressed.")]
        [SerializeField] private float startTimeoutSeconds = 30f;

        private static readonly DifficultyLevel[] AiDifficulties = { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Pro };

        private GameManager gameManager;
        private AuthManager authManager;
        private PromptFadeController promptFadeController;

        private bool isVisible = true;
        private bool suppressLobbyOverlay;
        private float? launchRequestedAt;
        private float? searchStartedAt;
        private bool isCancelling;
        private string pendingDetails;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            playAndWinButton?.onClick.AddListener(() => StartOnline(GameMode.block, GameType.casual, "Block · Online · 1 vs 1"));
            aiMatchButton?.onClick.AddListener(StartAiMatch);
            randomPlayersButton?.onClick.AddListener(() => StartOnline(GameMode.french, GameType.casual, "Random players · 1 vs 1"));
            competitiveButton?.onClick.AddListener(StartCompetitiveMatch);
            blockButton?.onClick.AddListener(() => StartOnline(GameMode.block, GameType.casual, "Block · Online · 1 vs 1"));
            concentrateButton?.onClick.AddListener(StartConcentrate);
            cancelMatchmakingButton?.onClick.AddListener(CancelMatchmaking);

            SetOverlayVisible(false);
        }

        private void LateUpdate()
        {
            if (!gameModeConfig)
                return;

            // Lobby = Play panel open and its selection UI not hidden by a running match. (The game
            // view itself stays "visible" in the lobby, it is just empty, so it can't be used here.)
            // While searching the selection UI is only dimmed to 0.5 by GameModeConfig, which still
            // counts as lobby -- IsSelectionUIHiddenForMatch only flips once the selector is fully
            // hidden for an actual match transition (deliberately NOT the selector's raw alpha,
            // which SetSelectionUIForceHidden below also drives -- reading that here would loop).
            bool playPanelOpen = gameModeConfig.RootCanvasGroup && gameModeConfig.RootCanvasGroup.alpha > 0.5f;
            bool gameOnScreen = gameModeConfig.IsInMatch || gameModeConfig.IsSelectionUIHiddenForMatch;
            bool dashboardVisible = playPanelOpen && !gameOnScreen && !suppressLobbyOverlay;
            SetVisible(dashboardVisible);

            // The dashboard's cards/banner are drawn over GameModeConfig's raw selector but don't
            // fully cover the screen region (gaps in the card grid), so the selector -- opaque by
            // default, or only dimmed to 0.5 mid-search -- bleeds through underneath. Force it
            // fully hidden every frame while covered; that's cheap and can't fight anything, since
            // nothing else wants the selector visible while the dashboard covers it. The reveal
            // path runs once, on the transition, in SetVisible -- doing it here every frame would
            // fight GameModeConfig's own dim while a search started from the Games tab is running.
            if (dashboardVisible)
                gameModeConfig.SetSelectionUIForceHidden();

            // GameModeConfig re-enables its lobby background whenever it returns to the menu.
            if (legacyLobbyBackground && legacyLobbyBackground.enabled)
                legacyLobbyBackground.enabled = false;

            UpdateMatchmakingOverlay(gameOnScreen);
        }

        // ------------------------------------------------------------------ card actions

        private void StartAiMatch()
        {
            if (!CanStart()) return;
            if (IsInParty)
            {
                Prompt("You can't play against the AI while you are in a party.");
                return;
            }

            var difficulty = AiDifficulties[UnityEngine.Random.Range(0, AiDifficulties.Length)];
            Launch(GameMode.french, GameType.singlePlayerIA, NumberPlayers.oneVsOne, difficulty, null, $"AI · {difficulty}");
        }

        private void StartCompetitiveMatch()
        {
            if (!CanStart()) return;
            if (gameManager is not { IsAuthenticatedAndVerified: true })
            {
                Prompt("Sign in with a verified account to play Competitive matches.");
                return;
            }
            if (IsInParty)
            {
                Prompt("Competitive matches can't be played in a party.");
                return;
            }

            Launch(GameMode.french, GameType.competitive, NumberPlayers.oneVsOne, null, null, "Competitive · 1 vs 1");
        }

        private void StartOnline(GameMode mode, GameType type, string details)
        {
            if (!CanStart()) return;
            if (IsInParty && !IsPartyLeader)
            {
                Prompt("Only the party leader can start a match.");
                return;
            }

            Launch(mode, type, NumberPlayers.oneVsOne, null, null, details);
        }

        private void StartConcentrate()
        {
            if (!CanStart()) return;
            if (IsInParty)
            {
                Prompt("Concentrate is a solo game and can't be played in a party.");
                return;
            }

            Launch(GameMode.concentrate, GameType.singlePlayerIA, NumberPlayers.solo, null, ConcentrateNumberOfTiles.tiles_28, "Concentrate · Solo");
        }

        private bool CanStart()
        {
            if (!gameModeConfig)
            {
                Debug.LogError($"[{nameof(DashboardController)}] GameModeConfig is not assigned.");
                return false;
            }

            // RunGameMode cancels an ongoing search, so never call it while one is running.
            if (gameModeConfig.IsInMatch || gameModeConfig.IsInOnlineMatch || gameModeConfig.IsMatchMaking || launchRequestedAt.HasValue)
                return false;

            return true;
        }

        private void Launch(GameMode mode, GameType type, NumberPlayers players, DifficultyLevel? difficulty, ConcentrateNumberOfTiles? tiles, string details)
        {
            gameModeConfig.SetExternalGameData(mode, type, players, difficulty, tiles);
            gameModeConfig.RunGameMode();

            // Local matches (AI, Concentrate) start straight away; online ones start a search.
            if (type is not GameType.singlePlayerIA)
            {
                launchRequestedAt = Time.unscaledTime;
                pendingDetails = details;
            }
        }

        private void CancelMatchmaking()
        {
            if (isCancelling || !gameModeConfig)
                return;

            if (gameModeConfig.IsMatchMaking)
            {
                isCancelling = true;
                gameModeConfig.RunGameMode(); // toggles: a second call cancels the running search
            }
            else
            {
                launchRequestedAt = null;
            }
        }

        // ------------------------------------------------------------------ overlay

        private void UpdateMatchmakingOverlay(bool gameOnScreen)
        {
            bool searching = gameModeConfig.IsMatchMaking;

            if (searching && !searchStartedAt.HasValue)
                searchStartedAt = Time.unscaledTime;

            if (!searching)
            {
                searchStartedAt = null;
                isCancelling = false;
            }

            // A request is done once the search (or the match) has begun; give up if neither happens
            // in time (GameModeConfig shows its own error prompt in that case).
            if (launchRequestedAt.HasValue &&
                (searching || gameOnScreen || Time.unscaledTime - launchRequestedAt.Value > startTimeoutSeconds))
                launchRequestedAt = null;

            bool show = !gameOnScreen && (searching || launchRequestedAt.HasValue);
            SetOverlayVisible(show);
            if (!show)
                return;

            if (matchmakingTitle)
                matchmakingTitle.text = isCancelling ? "Cancelling…" : searching ? "Finding a match…" : "Setting up the session…";
            if (matchmakingDetails)
                matchmakingDetails.text = pendingDetails ?? string.Empty;
            if (matchmakingTimer)
            {
                var elapsed = searchStartedAt.HasValue ? TimeSpan.FromSeconds(Time.unscaledTime - searchStartedAt.Value) : TimeSpan.Zero;
                matchmakingTimer.text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
            }
            if (cancelMatchmakingButton)
                cancelMatchmakingButton.interactable = searching && !isCancelling;
        }

        private void SetOverlayVisible(bool visible)
        {
            if (!matchmakingOverlay)
                return;
            matchmakingOverlay.alpha = visible ? 1f : 0f;
            matchmakingOverlay.interactable = visible;
            matchmakingOverlay.blocksRaycasts = visible;
        }

        private void SetVisible(bool visible)
        {
            if (visible == isVisible || !dashboardCanvasGroup)
                return;
            isVisible = visible;
            dashboardCanvasGroup.alpha = visible ? 1f : 0f;
            dashboardCanvasGroup.interactable = visible;
            dashboardCanvasGroup.blocksRaycasts = visible;

            // Dashboard stopped covering the selector (Games tab opened, or a match starting) --
            // restore it once here; see SetSelectionUIForceHidden/RestoreAfterCover in LateUpdate.
            if (!visible && gameModeConfig)
                gameModeConfig.SetSelectionUIRestoreAfterCover();
        }

        /// <summary>
        /// Wired to the sidebar's Games/Dashboard buttons (both route to NavigationPanelType.Play,
        /// reusing GameModeConfig's existing match-start wiring rather than duplicating it). When
        /// suppressed, the lobby banner stays out of the way so GameModeConfig's raw mode/type/
        /// players/difficulty selector shows through underneath -- the "Games" entry point.
        /// Cleared again when the dashboard's own tab is opened.
        /// </summary>
        public void SetLobbyOverlaySuppressed(bool suppress) => suppressLobbyOverlay = suppress;

        // ------------------------------------------------------------------ helpers

        private bool IsInParty => gameModeConfig && gameModeConfig.IsPartyRelay;

        private bool IsPartyLeader =>
            authManager && (PartyController.MainPartyMembers?.PartyEntries.Any(x => x.IsLeader && x.PartyEntryData?.PlayerID == authManager.UUID) ?? false);

        private void Prompt(string message)
        {
            if (promptFadeController)
                promptFadeController.Fade(message, 3f);
            else
                Debug.LogWarning($"[{nameof(DashboardController)}] {message}");
        }
    }
}
