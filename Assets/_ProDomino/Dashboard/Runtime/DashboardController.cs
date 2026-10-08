using ProDomino.AchievementSystem;
using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.GameSystem;
using ProDomino.NavigationSystem;
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
    public class DashboardController : MonoBehaviour, INavigationPanel
    {
        public NavigationPanelType NavigationPanelType => NavigationPanelType.Dashboard;
        public CanvasGroup RootCanvasGroup => dashboardCanvasGroup;
        public bool RequiresAuthentication => false;

        public void SetActiveNavigationPanel(bool isActive)
        {
            isVisible = isActive;
            suppressLobbyOverlay = !isActive;
            gameObject.SetActive(isActive);
            SetVisible(isActive);
            if (isActive && gameModeConfig && !gameModeConfig.IsInMatch && !gameModeConfig.IsMatchMaking)
            {
                gameModeConfig.gameObject.SetActive(false);
            }
        }

        [Header("Visibility")]
        [SerializeField] private CanvasGroup dashboardCanvasGroup;
        [SerializeField] private GameModeConfig gameModeConfig;
        [Tooltip("Old lobby background of the Play panel, replaced by the dashboard.")]
        [SerializeField] private Image legacyLobbyBackground;

        [Header("Cards")]
        [SerializeField] private Button playAndWinButton;
        [Tooltip("Label of the Play & Win button. Found in the button's children if unassigned.")]
        [SerializeField] private TMP_Text playAndWinLabel;
        [SerializeField] private string playAndWinLoggedInText = "Play & Win";
        [SerializeField] private string playAndWinLoggedOutText = "Register Now";
        [SerializeField] private Button aiMatchButton;
        [SerializeField] private Button randomPlayersButton;
        [SerializeField] private Button competitiveButton;
        [SerializeField] private Button blockButton;
        [SerializeField] private Button concentrateButton;
        [SerializeField] private Button frenchButton;
        [SerializeField] private Button drawButton;
        [SerializeField] private Button fiveButton;

        [Header("Matchmaking overlay")]
        [SerializeField] private CanvasGroup matchmakingOverlay;
        [SerializeField] private TMP_Text matchmakingTitle;
        [SerializeField] private TMP_Text matchmakingDetails;
        [SerializeField] private TMP_Text matchmakingTimer;
        [SerializeField] private Button cancelMatchmakingButton;
        [Tooltip("How long to wait for a search to begin after a card is pressed.")]
        [SerializeField] private float startTimeoutSeconds = 30f;
        [Tooltip("How long to wait for the match to appear after the search ends (MatchManager waits ~3s to load everyone).")]
        [SerializeField] private float matchStartTimeoutSeconds = 20f;

        [Tooltip("Used to switch to the Games tab for the Block/Concentrate cards. Found at runtime if unassigned.")]
        [SerializeField] private NavigationPanelController navigationPanelController;
        [SerializeField] private AchievementsTabController achievementsTabController;

        private static readonly DifficultyLevel[] AiDifficulties = { DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Pro };

        private GameManager gameManager;
        private AuthManager authManager;
        private PromptFadeController promptFadeController;

        private bool isVisible = true;
        private bool suppressLobbyOverlay;
        private float? launchRequestedAt;
        private float? searchStartedAt;
        private bool isCancelling;
        private bool wasSearching;
        private float? matchFoundAt;
        private string pendingDetails;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

            playAndWinButton?.onClick.AddListener(OnPressPlayAndWin);
            aiMatchButton?.onClick.AddListener(StartAiMatch);
            randomPlayersButton?.onClick.AddListener(() => StartOnline(GameMode.french, GameType.casual, "Random players · 1 vs 1"));
            competitiveButton?.onClick.AddListener(StartCompetitiveMatch);
            blockButton?.onClick.AddListener(() => OpenGamesModal(GameMode.block));
            concentrateButton?.onClick.AddListener(() => OpenGamesModal(GameMode.concentrate));
            frenchButton?.onClick.AddListener(() => OpenGamesModal(GameMode.french));
            drawButton?.onClick.AddListener(() => OpenGamesModal(GameMode.draw));
            fiveButton?.onClick.AddListener(() => OpenGamesModal(GameMode.five));
            cancelMatchmakingButton?.onClick.AddListener(CancelMatchmaking);

            SetOverlayVisible(false);

            if (!playAndWinLabel && playAndWinButton)
                playAndWinLabel = playAndWinButton.GetComponentInChildren<TMP_Text>(true);

            // "Register Now" is longer than "Play & Win": keep it on one line and shrink to fit the button.
            if (playAndWinLabel)
            {
                playAndWinLabel.textWrappingMode = TextWrappingModes.NoWrap;
                playAndWinLabel.fontSizeMax = playAndWinLabel.fontSize;
                playAndWinLabel.fontSizeMin = playAndWinLabel.fontSize * 0.6f;
                playAndWinLabel.enableAutoSizing = true;
            }

            gameManager?.HandleOnSignIn(RefreshPlayAndWinLabel);
            gameManager?.HandleOnSignOut(RefreshPlayAndWinLabel);
        }

        // Sign-in/out can happen while the dashboard is inactive, so resync whenever it is shown.
        private void OnEnable() => RefreshPlayAndWinLabel();

        private void OnDestroy()
        {
            gameManager?.UnHandleOnSignIn(RefreshPlayAndWinLabel);
            gameManager?.UnHandleOnSignOut(RefreshPlayAndWinLabel);
        }

        private bool? lastLoggedIn;

        private void RefreshPlayAndWinLabel()
        {
            if (!playAndWinLabel)
                return;
            bool isLoggedIn = gameManager && gameManager.IsAuthenticated;
            if (lastLoggedIn == isLoggedIn)
                return;
            lastLoggedIn = isLoggedIn;
            playAndWinLabel.text = isLoggedIn ? playAndWinLoggedInText : playAndWinLoggedOutText;
        }

        private void LateUpdate()
        {
            // onSignedIn can fire before IsAuthenticated turns true (sessionActive/init flags set later),
            // so the event alone leaves a stale label; this cached check catches the real transition.
            RefreshPlayAndWinLabel();

            if (!gameModeConfig)
                return;

            bool gameOnScreen = gameModeConfig.IsInMatch || gameModeConfig.IsSelectionUIHiddenForMatch;

            if (isVisible)
            {
                if (gameOnScreen || suppressLobbyOverlay)
                {
                    if (dashboardCanvasGroup && dashboardCanvasGroup.alpha > 0f)
                    {
                        dashboardCanvasGroup.alpha = 0f;
                        dashboardCanvasGroup.interactable = false;
                        dashboardCanvasGroup.blocksRaycasts = false;
                    }
                }
                else
                {
                    if (dashboardCanvasGroup && dashboardCanvasGroup.alpha < 1f)
                    {
                        dashboardCanvasGroup.alpha = 1f;
                        dashboardCanvasGroup.interactable = true;
                        dashboardCanvasGroup.blocksRaycasts = true;
                    }

                    // Keep GameModeSelectUI_NavPanel hidden in hierarchy while on the Dashboard tab
                    // Not between "match found" and the match appearing either: deactivating the panel
                    // in that gap kills the pending match start, so the Gameplay screen never opens.
                    if (!gameModeConfig.IsMatchMaking && !launchRequestedAt.HasValue && !matchFoundAt.HasValue && gameModeConfig.gameObject.activeSelf)
                    {
                        gameModeConfig.gameObject.SetActive(false);
                    }
                }
            }

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

        // Logged out the button reads "Register Now": open the same auth pop-up as the Options login button.
        private void OnPressPlayAndWin()
        {
            if (gameManager is not { IsAuthenticated: true })
            {
                if (authManager)
                    authManager.SetActiveAuthUI(true);
                return;
            }

            OpenChallenges();
        }

        // Logged in: switch to the Achievements tab and show its Challenges section.
        private void OpenChallenges()
        {
            if (!navigationPanelController)
                navigationPanelController = FindAnyObjectByType<NavigationPanelController>(FindObjectsInactive.Include);

            if (navigationPanelController)
                navigationPanelController.ExternalActivateNavigationPanel(NavigationPanelType.Achievements);
            else
                Debug.LogWarning($"[{nameof(DashboardController)}] NavigationPanelController not found; cannot open Achievements.");

            if (!achievementsTabController)
                achievementsTabController = FindAnyObjectByType<AchievementsTabController>(FindObjectsInactive.Include);

            if (achievementsTabController)
                achievementsTabController.SwitchTab(AchievementsTabController.Tab.Challenges);
            else
                Debug.LogWarning($"[{nameof(DashboardController)}] AchievementsTabController not found; Challenges tab not selected.");
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

        /// <summary>
        /// Block/Concentrate cards open the same mode-select popup as the Games tab: switch to Games
        /// (sidebar NavegationPanel_Games_Button, toggle ID "Play") and open its modal there.
        /// </summary>
        private void OpenGamesModal(GameMode mode)
        {
            if (!CanStart()) return;

            if (!navigationPanelController)
                navigationPanelController = FindAnyObjectByType<NavigationPanelController>(FindObjectsInactive.Include);

            if (navigationPanelController)
                navigationPanelController.ExternalActivateNavigationPanel(NavigationPanelType.Play);
            else
                Debug.LogWarning($"[{nameof(DashboardController)}] NavigationPanelController not found; opening popup without switching tab.");

            gameModeConfig.OpenGameModal(mode.ToString());
        }

        private bool CanStart()
        {
            if (!gameModeConfig)
            {
                Debug.LogError($"[{nameof(DashboardController)}] GameModeConfig is not assigned.");
                return false;
            }

            // RunGameMode cancels an ongoing search, so never call it while one is running.
            if (gameModeConfig.IsInMatch || gameModeConfig.IsInOnlineMatch || gameModeConfig.IsMatchMaking || launchRequestedAt.HasValue || matchFoundAt.HasValue)
                return false;

            return true;
        }

        private void Launch(GameMode mode, GameType type, NumberPlayers players, DifficultyLevel? difficulty, ConcentrateNumberOfTiles? tiles, string details)
        {
            if (gameModeConfig)
            {
                gameModeConfig.gameObject.SetActive(true);
                // Restore interactable/blocksRaycasts too, not just alpha: the panel was hidden with all three
                // off, and a non-interactable root disables every Selectable in the match UI (Settings, Hint, Pass).
                if (gameModeConfig.RootCanvasGroup)
                    gameModeConfig.RootCanvasGroup.SetActive(true);
            }

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

            if (isVisible && gameModeConfig.gameObject.activeSelf)
            {
                gameModeConfig.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ overlay

        private void UpdateMatchmakingOverlay(bool gameOnScreen)
        {
            bool searching = gameModeConfig.IsMatchMaking;

            if (searching && !searchStartedAt.HasValue)
                searchStartedAt = Time.unscaledTime;

            // Search ended without a cancel: a match was found and MatchManager is loading it.
            // Search ended with no session joined (timed out / failed): the timer is back at 00, so close
            // the overlay now instead of lingering on "Match found…" until matchStartTimeoutSeconds.
            if (wasSearching && !searching && !isCancelling && !gameModeConfig.IsInOnlineMatch && !gameModeConfig.IsInMatch)
            {
                ResetMatchmakingState();
                SetOverlayVisible(false);
                return;
            }

            if (wasSearching && !searching && !isCancelling)
                matchFoundAt = Time.unscaledTime;
            wasSearching = searching;

            if (searching || gameOnScreen || isCancelling ||
                (matchFoundAt.HasValue && Time.unscaledTime - matchFoundAt.Value > matchStartTimeoutSeconds))
                matchFoundAt = null;

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

            bool show = !gameOnScreen && (searching || launchRequestedAt.HasValue || matchFoundAt.HasValue);
            SetOverlayVisible(show);
            if (!show)
                return;

            if (matchmakingTitle)
                matchmakingTitle.text = isCancelling ? "Cancelling…" : searching ? "Finding a match…" : matchFoundAt.HasValue ? "Match found…" : "Setting up the session…";
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

        // Clears every matchmaking flag so the next CanStart()/Launch() opens the overlay from a clean state.
        private void ResetMatchmakingState()
        {
            launchRequestedAt = null;
            searchStartedAt = null;
            matchFoundAt = null;
            isCancelling = false;
            wasSearching = false;
            pendingDetails = null;
            if (matchmakingTimer)
                matchmakingTimer.text = "00:00";
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
