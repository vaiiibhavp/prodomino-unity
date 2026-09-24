using Cysharp.Threading.Tasks;
using ProDomino.AdSystem;
using ProDomino.Authentication;
using ProDomino.FriendSystem;
using ProDomino.GameModes;
using ProDomino.Shared;
using System;
using System.Collections;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Manages the configuration, selection, and UI logic for game modes, types, player counts, difficulty, and matchmaking
/// in the game's navigation panel.
/// </summary>
public class GameModeConfig : MonoBehaviour, INavigationPanel
{
    [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup selectionUICanvasGroup;
    [SerializeField] private CanvasGroup gameViewCanvasGroup;
    [SerializeField] private CanvasGroup uiBackgroundCanvasGroup;
    [SerializeField] private CustomButtonUI playNavigationButton;

    [Space(10)]
    [SerializeField] private CustomButtonToggleGroupUI gameModeSelector;
    [SerializeField] private CustomButtonToggleGroupUI gameTypeSelector;
    [SerializeField] private CustomButtonToggleGroupUI vsPlayerSelector;
    [SerializeField] private CustomButtonToggleGroupUI difficultySelector;
    [SerializeField] private CustomButtonToggleGroupUI numberOfTilesSelector;

    [SerializeField] private GameSearchStatus gameSearchStatus = GameSearchStatus.none;
    public GameSearchStatus _GameSearchStatus
    {
        get => gameSearchStatus;
        set => gameSearchStatus = value;
    }    

    [Space(10)]
    [SerializeField] private CustomButtonUI playGameModeButton;
    public CustomButtonUI PlayGameModeButton => playGameModeButton;
    [SerializeField] private MenuControllerGameMode menuControllerGameMode;
    public MenuControllerGameMode MenuControllerGameMode => menuControllerGameMode;

    [SerializeField] private Transform difficultyPanelUI;

    [SerializeField] private Transform numberOfTilesPanelUI;

    [Header("Games Grid Modal")]
    [Tooltip("Dim background + centered panel wrapping the selector, shown when a Games grid card is opened.")]
    [SerializeField] private GameObject gamesModalRoot;
    [SerializeField] private TMPro.TMP_Text gamesModalTitle;
    [Tooltip("The Games tab's card grid. Covered/restored alongside the raw selector -- see SetSelectionUIForceHidden.")]
    [SerializeField] private CanvasGroup gamesGridCanvasGroup;

    [SerializeField] private GameModeDataEvent runCreateOrJoinMatchSessionEvent;
    [SerializeField] private GameModeDataEvent runSingleVsIAEvent;

    [SerializeField] private UniqueBoolEvent checkIfIsMatchMaking;
    [SerializeField] private UniqueBoolEvent checkIfIsInMatchEvent;
    [SerializeField] private UniqueBoolEvent checkIfLocalPlayerIsHost;
    [SerializeField] private UniqueBoolEvent checkIfIsPartyRelay;
    [SerializeField] private UnityEvent runCancelMatchMaking;

    private AuthManager authManager;
    private AdManager adManager;
    private PromptFadeController promptFadeController;
    private bool isForcingBlockPlayButtonInteraction;
    private string playNavigationDefaultText;
    private DateTime? matchmakinStartTime;

    public bool RequiresAuthentication => false;
    public NavigationPanelType NavigationPanelType => NavigationPanelType.Play;

    // Determine the game mode that will be played
    private GameMode _gameModeSelectedID;
    public GameMode GameModeSelectedID => _gameModeSelectedID;

    // Determines if the game is casual, competitive or single player vs IA
    private GameType _gameTypeSelectedID;
    public GameType GameTypeSelectedID => _gameTypeSelectedID;

    // Determines the quantity of player of the match
    private NumberPlayers _vsPlayerSelectedID;
    public NumberPlayers VSPlayerSelectedID => _vsPlayerSelectedID;

    private DifficultyLevel _difficultySelector;
    public DifficultyLevel DifficultySelector => _difficultySelector;

    private ConcentrateNumberOfTiles _numberOfTilesSelector;
    public ConcentrateNumberOfTiles NumberOfTilesSelector => _numberOfTilesSelector;

    public bool IsInMatch => menuControllerGameMode?.IsInMatch ?? false;
    public bool IsInOnlineMatch => checkIfIsInMatchEvent?.Invoke() ?? false;
    public bool IsMatchMaking => checkIfIsMatchMaking?.Invoke() ?? false;
    public bool IsLocaPlayerHost => checkIfLocalPlayerIsHost?.Invoke() ?? false;
    public bool IsPartyRelay => checkIfIsPartyRelay?.Invoke() ?? false;

    private bool selectionUIHiddenForMatch;
    /// <summary>
    /// True once <see cref="SetSelectionUIVisibility"/>(false) has hidden the selector for an
    /// actual match transition (set by MenuControllerGameMode/MatchManager), as opposed to
    /// <see cref="SetSelectionUIForceHidden"/>, which only hides it because the dashboard lobby is
    /// drawn over it. Dashboard's own visibility check needs this distinction -- reading the
    /// selector's raw alpha there instead would create a feedback loop with ForceHidden.
    /// </summary>
    public bool IsSelectionUIHiddenForMatch => selectionUIHiddenForMatch;

    private void Awake()
    {
        authManager = ServiceLocator.Instance.GetService<AuthManager>();
        adManager = ServiceLocator.Instance.GetService<AdManager>();
        promptFadeController = ServiceLocator.Instance.GetService<PromptFadeController>();

        // Ensure any stray duplicate GamesGrid_Root or GamesModal_Root are removed, and references assigned
        int gridFound = 0;
        int modalFound = 0;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name == "GamesGrid_Root")
            {
                gridFound++;
                if (gridFound > 1)
                {
                    Destroy(child.gameObject);
                    continue;
                }
                if (!gamesGridCanvasGroup)
                    gamesGridCanvasGroup = child.GetComponent<CanvasGroup>();
            }
            else if (child.name == "GamesModal_Root")
            {
                modalFound++;
                if (modalFound > 1)
                {
                    Destroy(child.gameObject);
                    continue;
                }
                if (!gamesModalRoot)
                    gamesModalRoot = child.gameObject;
            }
        }

        if (playGameModeButton)
            playGameModeButton.SetIsInteractableByDefault(true);
    }

    private async void Start()
    {
        if (!playGameModeButton)
        {
            Debug.LogError("PlayGameButton reference is null. Is critic to assign it");
            return;
        }

        // Register the default value for the play button text if it exists
        if (playNavigationButton)
            playNavigationDefaultText = playNavigationButton.GetMainText();

        playGameModeButton.SetIsInteractableByDefault(true);
        // By default, disable play button
        playGameModeButton.SetButtonInteractable(false);

        // Subscribe run main method
        playGameModeButton.onClick.AddListener(RunGameMode);
        playGameModeButton.onClick.AddListener(CloseGameModal);

        // Register all UI selector callbacks immediately so buttons respond to user clicks from frame 1
        gameModeSelector.SetOnCustomButtonSelectedCallback((id) =>
        {
            if (!Enum.TryParse(id, true, out _gameModeSelectedID))
                _gameModeSelectedID = GameMode.none;
            
            ValidateGameModeFullData();
        });

        gameTypeSelector.SetOnCustomButtonSelectedCallback((id) =>
        {
            if (!Enum.TryParse(id, true, out _gameTypeSelectedID))
                _gameTypeSelectedID = GameType.none;

            ValidateGameModeFullData();
        });

        vsPlayerSelector.SetOnCustomButtonSelectedCallback((id) =>
        {
            if (string.Equals(id, "oneVsTree", StringComparison.OrdinalIgnoreCase))
                _vsPlayerSelectedID = NumberPlayers.oneVsThree;
            else if (!Enum.TryParse(id, true, out _vsPlayerSelectedID))
                _vsPlayerSelectedID = NumberPlayers.none;

            ValidateGameModeFullData();
        });

        difficultySelector.SetOnCustomButtonSelectedCallback((id) =>
        {
            if (string.Equals(id, "Normal", StringComparison.OrdinalIgnoreCase))
                _difficultySelector = DifficultyLevel.Medium;
            else if (!Enum.TryParse(id, true, out _difficultySelector))
                _difficultySelector = DifficultyLevel.None;

            ValidateGameModeFullData();
        });

        numberOfTilesSelector.SetOnCustomButtonSelectedCallback((id) =>
        {
            if (!Enum.TryParse(id, true, out _numberOfTilesSelector))
                _numberOfTilesSelector = ConcentrateNumberOfTiles.none;

            ValidateGameModeFullData();
        });

        // Wait until the AuthManager is initialized
        await UniTask.WaitUntil(() => authManager is not null and { IsAlreadyInitialized: true });
    }

    private void Update()
    {
        if (IsInMatch || IsInOnlineMatch || selectionUIHiddenForMatch)
        {
            if (gamesGridCanvasGroup && gamesGridCanvasGroup.alpha > 0f)
            {
                gamesGridCanvasGroup.alpha = 0f;
                gamesGridCanvasGroup.interactable = false;
                gamesGridCanvasGroup.blocksRaycasts = false;
            }
            if (gamesModalRoot && gamesModalRoot.activeSelf)
            {
                gamesModalRoot.SetActive(false);
            }
        }

        if (playNavigationButton)
        {
            // Check if we are in matchmaking or in a match
            if (IsInOnlineMatch)
            {
                // Stops the matchmaking start time if it was set
                if (matchmakinStartTime.HasValue)
                    matchmakinStartTime = null;

                // Update the play button text to indicate that we are in a match
                if (playNavigationButton.GetMainText() != "In Match!")
                    playNavigationButton.SetMainText($"In Match!");
            }

            // If we are in matchmaking, update the play button text with the elapsed time
            else if (matchmakinStartTime.HasValue)
            {
                var timeElapsed = (DateTime.Now - matchmakinStartTime.Value).ToString(@"mm\:ss");
                playNavigationButton.SetMainText($"<size=+15>Finding Game\n({timeElapsed})");
            }

            // But, if we are not in a match and there is no matchmaking in progress, reset the play button text to the default value
            else if (playNavigationButton.GetMainText() != playNavigationDefaultText && (!IsPartyRelay || IsLocaPlayerHost))
                playNavigationButton.SetMainText(playNavigationDefaultText);
        }
    }

    /// <summary>
    /// Activates or deactivates the navigation panel and updates its layout and related UI elements.
    /// </summary>
    /// <param name="isActive">True to activate the navigation panel; false to deactivate it.</param>
    void INavigationPanel.SetActiveNavigationPanel(bool isActive)
    {
        gameObject.SetActive(isActive);
        RootCanvasGroup?.SetActive(isActive);
        if (isActive)
        {
            SetSelectionUIRestoreAfterCover();
            RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
            ValidateGameModeFullData();

            // Show an interstitial ad when opening the menu each X minutes
            adManager?.ShowInterstitialAd(isConsideringIntervals: true);
        }
    }

    /// <summary>
    /// Force the interactivity of the selectors based on the given type
    /// </summary>
    /// <param name="isInteractable">True to make the selectors interactable; false to disable them.</param>
    /// <param name="selectorType">The type of selector to apply the interactivity change to.</param>
    public void ForceSelectorsInteractivity(bool isInteractable, SelectorType selectorType)
    { 
        if (gameModeSelector && (selectorType.HasFlag(SelectorType.GameMode) || selectorType.HasFlag(SelectorType.All)))
            gameModeSelector?.SetAllButtonsInteractable(isInteractable);

        if (gameTypeSelector && (selectorType.HasFlag(SelectorType.GameType) || selectorType.HasFlag(SelectorType.All)))
            gameTypeSelector.SetAllButtonsInteractable(isInteractable);

        if (vsPlayerSelector && (selectorType.HasFlag(SelectorType.VsPlayer) || selectorType.HasFlag(SelectorType.All)))
            vsPlayerSelector.SetAllButtonsInteractable(isInteractable);
    }

    /// <summary>
    /// Validates and updates the UI state for game mode selection, enabling or disabling buttons and panels based on
    /// current selections, authentication status, and party conditions.
    /// </summary>
    public void ValidateGameModeFullData()
    {
        if (playGameModeButton == null)
        {
            Debug.LogWarning("Play button ref is null");
            return;
        }

        // --- 1. Validate Play Button ---
        bool isPlayButtonValid = !isForcingBlockPlayButtonInteraction
                                 && GameModeSelectedID != GameMode.none
                                 && GameTypeSelectedID != GameType.none
                                 && VSPlayerSelectedID != NumberPlayers.none
                                 && (
                                        (GameModeSelectedID != GameMode.concentrate && DifficultySelector != DifficultyLevel.None)
                                        || (GameModeSelectedID == GameMode.concentrate && NumberOfTilesSelector != ConcentrateNumberOfTiles.none)
                                        || GameTypeSelectedID != GameType.singlePlayerIA
                                    );

        
        playGameModeButton.SetButtonInteractable(isPlayButtonValid, ignoreDefault: true);

        // The tooltip container is optional (the Play button has none assigned); without this check
        // the exception aborts NavigationPanelController.SetActiveNavigationPanel and no panel opens.
        if (playGameModeButton.TooltipContainer && playGameModeButton.TooltipContainer.TryGetComponent<Image>(out var tooltipImage))
            tooltipImage.raycastTarget = !isPlayButtonValid;

        // --- 2. Early exits for incompatible states ---
        if (IsMatchMaking || menuControllerGameMode.IsInMatch || (PartyController.IsRelay && !IsPartyRelay))
            return;

        // --- 3. Calculate base conditions ---
        bool isAuthenticated = authManager != null && ((authManager.IsUserAuthenticatedWithCredentials && authManager.IsEmailVerified) || authManager.IsUserAuthenticatedWithProvider);
        bool isConcentrateMode = GameModeSelectedID == GameMode.concentrate;
        bool canShowMultiplayer = isAuthenticated && !isConcentrateMode;

        // --- 4. Update Game Type buttons ---
        SetGameTypeInteractable(GameType.casual, !isConcentrateMode);
        SetGameTypeInteractable(GameType.competitive, false); // hidden in the raw selector; started from Dashboard's own Competitive card instead
        SetGameTypeInteractable(GameType.singlePlayerIA, true); // always reset; may change later for party conditions

        // --- 5. Update VS Player buttons ---
        SetVsPlayerInteractable(NumberPlayers.solo, isConcentrateMode);
        SetVsPlayerInteractable(NumberPlayers.oneVsOne, !isConcentrateMode);
        SetVsPlayerInteractable(NumberPlayers.oneVsThree, !isConcentrateMode);
        SetVsPlayerInteractable(NumberPlayers.twoVsTwo, !isConcentrateMode);

        // --- 6. Update Difficulty panel and buttons ---
        SetDifficultyUI(!isConcentrateMode && GameTypeSelectedID == GameType.singlePlayerIA);

        // --- 7. Update Number Of Tiles panel and buttons ---
        SetNumberOfTilesUI(isConcentrateMode);

        // --- 8. Deselect invalid selections ---
        if (vsPlayerSelector.CheckIfSelected(NumberPlayers.solo.ToString()) && !isConcentrateMode)
            vsPlayerSelector.DeselectAll(true);
        else if (isConcentrateMode && (vsPlayerSelector.CheckIfSelected(NumberPlayers.oneVsOne.ToString()) ||
                                       vsPlayerSelector.CheckIfSelected("oneVsTree") ||
                                       vsPlayerSelector.CheckIfSelected(NumberPlayers.twoVsTwo.ToString())))
            vsPlayerSelector.DeselectAll(true);

        if ((gameTypeSelector.CheckIfSelected(GameType.casual.ToString()) && isConcentrateMode) ||
            (gameTypeSelector.CheckIfSelected(GameType.competitive.ToString()) && !canShowMultiplayer))
            gameTypeSelector.DeselectAll(true);

        // --- 9. Update Play Button interactivity ---
        SetPlayButtonInteractivityAsync(!IsMatchMaking && !IsInMatch, default, auxText: "Play");

        // --- 10. Handle Party Mode ---
        if (PartyController.IsRelay)
        {
            HandlePartyMode();
            return;
        }

        // --- 11. Default (non-party) behavior ---
        SetSelectionUIInteractivity(true);

        #region --- Helper Methods ---

        // Helper to safely set GameType button interactability
        void SetGameTypeInteractable(GameType type, bool interactable)
        {
            var button = gameTypeSelector.GetButtonUI(type.ToString());
            if (button != null)
                button.SetButtonActive(interactable);
        }

        // Helper to safely set VS Player button interactability
        void SetVsPlayerInteractable(NumberPlayers playerType, bool interactable)
        {
            var button = vsPlayerSelector.GetButtonUI(playerType.ToString());
            if (button != null)
                button.SetButtonActive(interactable);
        }

        void SetDifficultyUI(bool isActive)
        {
            difficultyPanelUI.gameObject.SetActive(isActive);
        }

        void SetNumberOfTilesUI(bool isActive)
        {
            numberOfTilesPanelUI.gameObject.SetActive(isActive);
        }

        // Handles the logic for when the user is in a party
        void HandlePartyMode()
        {
            if (IsPartyRelay && IsLocaPlayerHost)
            {
                // Host restrictions
                SetGameTypeInteractable(GameType.singlePlayerIA, false);
                SetGameTypeInteractable(GameType.competitive, false);

                if (PartyController.PartyCount > 2)
                {
                    SetVsPlayerInteractable(NumberPlayers.oneVsOne, false);
                    vsPlayerSelector.DeselectAll(true);
                }

                if (GameTypeSelectedID is GameType.singlePlayerIA or GameType.competitive)
                    gameTypeSelector.DeselectAll(true);
            } 
            else if (IsPartyRelay)
            {
                // Non-host relay members can't interact
                SetSelectionUIInteractivity(false);

                if (!IsMatchMaking) 
                {
                    var text = "Party!";
                    if (playNavigationButton.GetMainText() != text)
                        playNavigationButton.SetMainText(text);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Starts the selected game mode or matchmaking process if valid options are selected and no match is currently in
    /// progress.
    /// </summary>
    public void RunGameMode()
    {
        if (IsInMatch)
        { 
            Debug.LogWarning("Player is already in a match, could not start a match");
            return;
        }

        bool isValidOption = GameModeSelectedID != GameMode.none 
                             && GameTypeSelectedID != GameType.none 
                             && VSPlayerSelectedID != NumberPlayers.none 
                             && (
                                    (GameModeSelectedID != GameMode.concentrate && DifficultySelector != DifficultyLevel.None) 
                                    || (GameModeSelectedID == GameMode.concentrate && NumberOfTilesSelector != ConcentrateNumberOfTiles.none)
                                    || GameTypeSelectedID != GameType.singlePlayerIA
                                );

        if (!isValidOption)
        {
            Debug.LogWarning("Please select valid options before running the game mode.");
            return;
        }

        // If there is not a match in progress, we can start the game mode
        if (IsInOnlineMatch)
        { 
            Debug.LogWarning("There is already a match in progress. Please finish the current match before starting a new one.");
            return;
        }

        Debug.Log($"Game Mode: {GameModeSelectedID}, Game Type: {GameTypeSelectedID}, vs Player: {VSPlayerSelectedID}");

        if (GameTypeSelectedID is GameType.singlePlayerIA)
        {
            // Disabled the forced block after starting the match when playing vs IA
            isForcingBlockPlayButtonInteraction = false;

            CloseGameModal();
            if (gamesGridCanvasGroup)
            {
                gamesGridCanvasGroup.alpha = 0f;
                gamesGridCanvasGroup.interactable = false;
                gamesGridCanvasGroup.blocksRaycasts = false;
            }

            // Directly start the match vs IA
            TryToStartMatch();
        }
        else
        {
            // Check if there is a matchmaking in progress
            if (!IsMatchMaking)
            {
                gameSearchStatus = GameSearchStatus.SearchingGame;

                // Disable the button for some time to avoid spamming
                isForcingBlockPlayButtonInteraction = true;

                Debug.Log($"<b>[{nameof(GameModeConfig)}]</b> Starting matchmaking...");

                // First, start the matchmaking
                TryToStartMatch();

                // When the matchmaking is already started, set the button interactivity
                SetPlayButtonInteractivityAsync
                    (false,
                    UniTask.Create(async () =>
                    {
                        // If there is already a matchamking process active, wait until the session is created and the matchmaking is deactivated
                        if (IsMatchMaking)
                        {
                            try
                            {
                                await UniTask.WaitUntil(() => PartyController.IsRelay && !IsMatchMaking).Timeout(TimeSpan.FromSeconds(25));
                            }
                            catch (TimeoutException) 
                            {
                                Debug.LogWarning($"<b>[{nameof(GameModeConfig)}]</b> Matchmaking process could not be completed in a timely manner. Enabling Play button interaction again.");
                                TryToCancelMatchMaking().Forget();

                                promptFadeController.Fade("Could not start matchmaking. Please try again later.");
                            }
                        } 

                        // Else, try to wait until the matchmaking process is defined
                        else
                        {
                            try
                            {
                                await UniTask.WaitUntil(() => IsMatchMaking).Timeout(TimeSpan.FromSeconds(10));
                            }
                            catch (TimeoutException) 
                            {
                                Debug.LogWarning($"<b>[{nameof(GameModeConfig)}]</b> Matchmaking process could not be started in a timely manner. Enabling Play button interaction again.");
                            }

                            // Make a delay time to avoid staart a process until another one is running
                            await UniTask.WaitForSeconds(2);
                        }
                    }),
                    () => isForcingBlockPlayButtonInteraction = false);

            }

            // If there is a matchmaking in progress, we can cancel it
            else
            {
                Debug.Log($"<b>[{nameof(GameModeConfig)}]</b> Cancelling matchmaking...");

                gameSearchStatus = GameSearchStatus.none;
                playGameModeButton.SetMainText("Cancelling...");

                TryToCancelMatchMaking().Forget();
            }
        }


        async UniTask TryToCancelMatchMaking()
        {
            matchmakinStartTime = null;

            try
            {
                runCancelMatchMaking?.Invoke();

                // Wait until the matchmaking is cancelled, 
                await UniTask.WaitUntil(() => !isForcingBlockPlayButtonInteraction).Timeout(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Debug.LogError($"<b>[{nameof(GameModeConfig)}]</b> An error occurred while trying to cancel matchmaking: {ex.Message}");
            }
            finally
            { 
                isForcingBlockPlayButtonInteraction = false;
                ValidateGameModeFullData();

                // Try to re-enable UI if the matchmaking was cancelled before the Networs was stablished
                if (!IsMatchMaking && !IsInOnlineMatch && !IsInMatch)
                    EnablePlayButtonAfterCancelingSearch();
            }
        }
    }

    /// <summary>
    /// Enables the play button and updates its text to "Play" after canceling a search.
    /// </summary>
    public void EnablePlayButtonAfterCancelingSearch()
    {
        Debug.Log("OnNetworkDespawn: Enable Button 02");

        playGameModeButton.SetMainText("Play");
        SetPlayButtonInteractivity(true);
    }

    /// <summary>
    /// Initializes and starts a match based on the selected game mode, type, player, difficulty, and tile settings,
    /// triggering the appropriate UI updates and events.
    /// </summary>
    void TryToStartMatch()
    {
        // Implement the logic to run the game mode based on the selected IDs
        menuControllerGameMode.SetGameModeData(GameModeSelectedID, GameTypeSelectedID, VSPlayerSelectedID, DifficultySelector, NumberOfTilesSelector);

        // If the game mode is concentrate, open its specific tile selection menu
        if (_gameModeSelectedID is GameMode.concentrate)
            menuControllerGameMode.StartDomino(); //menuControllerGameMode.OpenConcentrateNumberOfTilesMenu();

        else

            switch (GameTypeSelectedID)
            {
                case GameType.singlePlayerIA:
                    // Directly turn of the selection UI and open the game difficulty menu if the gamemode is not concentrate
                    //menuControllerGameMode.OpenDifficultyMenu();

                    // Call the event to run the single player vs IA with the selected game mode data
                    runSingleVsIAEvent?.Invoke(new(GameModeSelectedID, GameTypeSelectedID, VSPlayerSelectedID, menuControllerGameMode.ConcentrateNumberOfTiles));

                    menuControllerGameMode.StartDomino();

                    gameSearchStatus = GameSearchStatus.none;

                    break;

                case GameType.casual:
                case GameType.competitive:
                    Debug.Log($"{(GameTypeSelectedID is GameType.casual ? "Casual " : "Competitive")} game type selected.");

                    // Inform that we are going to the lobby with the selected game mode data
                    ForceSelectorsInteractivity(false, selectorType: SelectorType.All);

                    // Run the event that start the matchmaking
                    runCreateOrJoinMatchSessionEvent?.Invoke(new(GameModeSelectedID, GameTypeSelectedID, VSPlayerSelectedID, menuControllerGameMode.ConcentrateNumberOfTiles));

                    // Register the matchmaking start time
                    matchmakinStartTime = DateTime.Now;

                    break;

                default:
                    Debug.LogWarning("Invalid game type selected.");
                    break;
            }
    }

    /// <summary>
    /// Waits for one second before attempting to start a new matchmaking process.
    /// </summary>
    /// <returns>An enumerator for coroutine execution.</returns>
    public IEnumerator RestartMatchmaking()
    {
        yield return new WaitForSeconds(1f);
        
        TryToStartMatch();
        //RunGameMode();
    }
    
    /// <summary>
    /// Displays the selection UI menu by activating its canvas group.
    /// </summary>
    public void OpenThisMenu()
    {
        selectionUICanvasGroup.SetActive(true);
    }

    /// <summary>
    /// Sets the external game data by updating the selected game mode, type, number of players, difficulty level, and
    /// number of tiles if the player is not currently in a match.
    /// </summary>
    /// <remarks>Settings cannot be changed while the player is in a match.</remarks>
    /// <param name="gameMode">Optional game mode to select.</param>
    /// <param name="gameType">Optional game type to select.</param>
    /// <param name="numberPlayers">Optional number of players to select.</param>
    /// <param name="difficultyLevel">Optional difficulty level to select.</param>
    /// <param name="numberOfTiles">Optional number of tiles to select.</param>
    public void SetExternalGameData
        (GameMode? gameMode = null, 
        GameType? gameType = null, 
        NumberPlayers? numberPlayers = null, 
        DifficultyLevel? difficultyLevel = null, 
        ConcentrateNumberOfTiles? numberOfTiles = null)
    {
        if (IsInMatch)
        {
            Debug.LogWarning("Player is already in a match, could not change its settings");
            return;
        }

        if (numberOfTiles.HasValue)
            numberOfTilesSelector.GetButtonUI(numberOfTiles.Value.ToString())?.Select();

        if (gameMode.HasValue)
            gameModeSelector.GetButtonUI(gameMode.Value.ToString())?.Select();

        if (gameType.HasValue)
            gameTypeSelector.GetButtonUI(gameType.Value.ToString())?.Select();

        if (numberPlayers.HasValue)
            vsPlayerSelector.GetButtonUI(numberPlayers.Value.ToString())?.Select();

        if (difficultyLevel.HasValue)
            difficultySelector.GetButtonUI(difficultyLevel.Value.ToString())?.Select();

        ValidateGameModeFullData();
    }

    /// <summary>
    /// Shows or hides the selection UI and refreshes its layout if made visible.
    /// </summary>
    /// <param name="isVisible">True to show the selection UI; false to hide it.</param>
    public void SetSelectionUIVisibility(bool isVisible)
    {
        selectionUIHiddenForMatch = !isVisible;
        selectionUICanvasGroup.SetActive(isVisible);
        if (isVisible)
            selectionUICanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

        if (gamesGridCanvasGroup)
        {
            gamesGridCanvasGroup.alpha = isVisible ? 1f : 0f;
            gamesGridCanvasGroup.interactable = isVisible;
            gamesGridCanvasGroup.blocksRaycasts = isVisible;
        }

        if (!isVisible)
            CloseGameModal();
    }
    
    /// <summary>
    /// Enables or disables interactivity for the selection UI and adjusts its alpha transparency accordingly.
    /// </summary>
    /// <param name="isInteractable">If true, makes the selection UI interactive and fully opaque; if false, disables interactivity and sets partial
    /// transparency.</param>
    public void SetSelectionUIInteractivity(bool isInteractable)
    {
        selectionUICanvasGroup.SetActive(isInteractable, isSettingAlpha: false, optionalForcedAlpha: isInteractable ? 1 : .5f);
    }

    /// <summary>
    /// Forces the selector AND the Games grid fully hidden because the dashboard lobby is drawn
    /// over this whole panel right now. Meant to be called every frame while covered (cheap
    /// CanvasGroup writes) -- nothing else legitimately wants either visible in that state, so
    /// there's nothing for this to fight.
    /// </summary>
    public void SetSelectionUIForceHidden()
    {
        selectionUICanvasGroup.alpha = 0f;
        selectionUICanvasGroup.interactable = false;
        selectionUICanvasGroup.blocksRaycasts = false;

        if (gamesGridCanvasGroup)
        {
            gamesGridCanvasGroup.alpha = 0f;
            gamesGridCanvasGroup.interactable = false;
            gamesGridCanvasGroup.blocksRaycasts = false;
        }
    }

    /// <summary>
    /// Restores the selector and the Games grid to their own normal visibility once the dashboard
    /// stops covering this panel (Games tab opened, or a match starting), and rebuilds the
    /// selector's layout -- switching it back on without a refresh leaves stale positions from
    /// being force-hidden (missing/overlapping rows). Meant to be called once, on that transition;
    /// GameModeConfig's own interactivity/visibility calls own the selector's state afterward
    /// (e.g. the mid-search dim). The grid has no such follow-up state, so a plain SetActive covers it.
    /// </summary>
    public void SetSelectionUIRestoreAfterCover()
    {
        selectionUICanvasGroup.SetActive(true, isSettingAlpha: false, optionalForcedAlpha: IsMatchMaking ? 0.5f : 1f);
        selectionUICanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

        if (gamesGridCanvasGroup && !IsInMatch && !IsInOnlineMatch && !selectionUIHiddenForMatch)
        {
            gamesGridCanvasGroup.alpha = 1f;
            gamesGridCanvasGroup.interactable = true;
            gamesGridCanvasGroup.blocksRaycasts = true;
        }
    }

    /// <summary>
    /// Opens the Games grid's mode-select popup for one specific game (a card's "Play" button),
    /// pre-selecting that mode on <see cref="gameModeSelector"/> so the popup's type/players/
    /// difficulty fields resolve against it exactly like picking it from the old inline row did.
    /// </summary>
    /// <param name="gameModeName">A <see cref="GameMode"/> member name (matches CustomButtonUI's
    /// string-based toggle IDs, so this can be wired directly from a persistent UnityEvent call).</param>
    public void OpenGameModal(string gameModeName)
    {
        isForcingBlockPlayButtonInteraction = false;

        if (Enum.TryParse<GameMode>(gameModeName, true, out var mode) && mode != GameMode.none)
        {
            _gameModeSelectedID = mode;
            gameModeSelector?.GetButtonUI(mode.ToString())?.Select();
        }
        else
        {
            Debug.LogWarning($"[{nameof(GameModeConfig)}] OpenGameModal: '{gameModeName}' is not a valid {nameof(GameMode)}.");
        }

        if (gamesModalTitle && !string.IsNullOrEmpty(gameModeName))
            gamesModalTitle.text = $"{char.ToUpperInvariant(gameModeName[0])}{gameModeName[1..]} Game";

        bool isConcentrate = _gameModeSelectedID == GameMode.concentrate;

        // Auto-select defaults so user can immediately click Play
        if (isConcentrate || GameTypeSelectedID == GameType.none)
            _gameTypeSelectedID = GameType.singlePlayerIA;

        var typeBtn = gameTypeSelector?.GetButtonUI(_gameTypeSelectedID.ToString()) ?? gameTypeSelector?.GetFirstButtonUI();
        typeBtn?.Select();

        if (isConcentrate)
        {
            _vsPlayerSelectedID = NumberPlayers.solo;
            var vsSoloBtn = vsPlayerSelector?.GetButtonUI(NumberPlayers.solo.ToString()) ?? vsPlayerSelector?.GetFirstButtonUI();
            vsSoloBtn?.Select();

            if (NumberOfTilesSelector == ConcentrateNumberOfTiles.none)
                _numberOfTilesSelector = ConcentrateNumberOfTiles.tiles_28;

            var tilesBtn = numberOfTilesSelector?.GetButtonUI(_numberOfTilesSelector.ToString()) ?? numberOfTilesSelector?.GetFirstButtonUI();
            tilesBtn?.Select();
        }
        else
        {
            if (VSPlayerSelectedID == NumberPlayers.none || VSPlayerSelectedID == NumberPlayers.solo)
                _vsPlayerSelectedID = NumberPlayers.oneVsOne;

            var vsBtn = vsPlayerSelector?.GetButtonUI(_vsPlayerSelectedID.ToString()) ?? vsPlayerSelector?.GetFirstButtonUI();
            vsBtn?.Select();

            if (DifficultySelector == DifficultyLevel.None)
                _difficultySelector = DifficultyLevel.Easy;

            var diffId = _difficultySelector == DifficultyLevel.Medium ? "Normal" : _difficultySelector.ToString();
            var diffBtn = difficultySelector?.GetButtonUI(diffId) ?? difficultySelector?.GetFirstButtonUI();
            diffBtn?.Select();
        }

        ValidateGameModeFullData();

        // Ensure play button is fully interactable and ready to click
        if (playGameModeButton)
        {
            playGameModeButton.SetButtonInteractable(true);
            if (playGameModeButton.Button != null)
                playGameModeButton.Button.interactable = true;
            if (playGameModeButton.GetComponent<CanvasGroup>() is CanvasGroup cg)
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
                cg.alpha = 1f;
            }
        }

        if (gamesModalRoot)
            gamesModalRoot.SetActive(true);
    }

    /// <summary>
    /// Closes the Games grid popup opened by <see cref="OpenGameModal"/>. Wired to the popup's
    /// close button, the dim background, and the Play Now button (so starting a match dismisses it).
    /// </summary>
    public void CloseGameModal()
    {
        if (gamesModalRoot)
            gamesModalRoot.SetActive(false);
    }

    /// <summary>
    /// Controls the visibility of the gameplay UI and updates related UI elements accordingly.
    /// </summary>
    /// <param name="isVisible">True to show the gameplay UI; false to hide it.</param>
    public void SetGameplayVisibility(bool isVisible)
    {
        gameViewCanvasGroup.SetActive(isVisible);
        if (isVisible)
            gameViewCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

        SetUIBackgroundVisibility(!isVisible);

        if (gamesGridCanvasGroup)
        {
            gamesGridCanvasGroup.alpha = isVisible ? 0f : 1f;
            gamesGridCanvasGroup.interactable = !isVisible;
            gamesGridCanvasGroup.blocksRaycasts = !isVisible;
        }

        if (isVisible)
            CloseGameModal();
    }

    /// <summary>
    /// Sets the visibility of the UI background and refreshes its layout if made visible.
    /// </summary>
    /// <param name="isVisible">True to show the UI background; false to hide it.</param>
    public void SetUIBackgroundVisibility(bool isVisible)
    {
        uiBackgroundCanvasGroup.SetActive(isVisible);
        if (isVisible)
            uiBackgroundCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();
    }

    /// <summary>
    /// Asynchronously sets the interactivity of the play button, optionally waits for a task to complete, updates the
    /// button text, and invokes a callback upon completion.
    /// </summary>
    /// <param name="interactivity">Whether the play button should be interactable.</param>
    /// <param name="waitingTask">An optional task to await before restoring the button's original interactivity.</param>
    /// <param name="onFinish">An optional action to invoke after the operation completes.</param>
    /// <param name="auxText">Optional text to set as the button's main label after completion.</param>
    private async void SetPlayButtonInteractivityAsync(bool interactivity, UniTask? waitingTask, Action onFinish = null, string auxText = "")
    {
        var startingInteractibity = playGameModeButton.IsInteractable;
        if (startingInteractibity == interactivity)
            return;

        SetPlayButtonInteractivity(interactivity);

        // Wait until the given task is finished
        if (waitingTask.HasValue)
            await waitingTask.Value;

        SetPlayButtonInteractivity(startingInteractibity);

        if(auxText != "")
            playGameModeButton.SetMainText(auxText);

        onFinish?.Invoke();
    }

    /// <summary>
    /// Sets the interactivity state of the play game mode button.
    /// </summary>
    /// <param name="interactivity">True to make the button interactable; false to disable interaction.</param>
    private void SetPlayButtonInteractivity(bool interactivity)
    {
        if (!playGameModeButton)
        { 
            Debug.LogWarning("Play button reference is null");
            return;
        }

        playGameModeButton.SetButtonInteractable(interactivity, ignoreDefault: true);
    }

    /// <summary>
    /// Event called when NavigationController updates its login status
    /// </summary>
    /// <param name="isLogged">Indicates whether the user is logged in.</param>
    void INavigationPanel.OnUpdateLoginStatus(bool isLogged)
    {
        // Update the UI when the login event is invoked
        ValidateGameModeFullData();
    }

    /// <summary>
    /// Handles logic when a player joins the party by validating the game mode's full data.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player who joined.</param>
    public void PartyController_OnPlayerJoined(string playerId) => ValidateGameModeFullData();

    /// <summary>
    /// Handles logic when a player leaves the party by validating the game mode's full data.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player who is leaving.</param>
    public void PartyController_OnPlayerLeaving(string playerId) => ValidateGameModeFullData();

    #region Matchmaking Events
    /// <summary>
    /// Invokes the event before the matchmaking starts<br></br>
    /// Register as an event from NetworkManagerUIDemo
    /// </summary>
    public void MatchManager_OnSetMatchmakingPreviousSettingsEvent()
    {
        if (playGameModeButton)
            playGameModeButton.SetMainText("Setting session...");

        ForceSelectorsInteractivity(false, SelectorType.All);
    }
    
    /// <summary>
    /// Invokes the event when matchmaking starts<br></br>
    /// Register as an event from NetworkManagerUIDemo
    /// </summary>
    public void MatchManager_OnStartMatchmaking()
    {
        if (playGameModeButton)
            playGameModeButton.SetMainText("Cancel Search");

        ForceSelectorsInteractivity(false, SelectorType.All);
    }

    /// <summary>
    /// Invokes the event when matchmaking is cancelled<br></br>
    /// Register as an event from NetworkManagerUIDemo
    /// </summary>
    public void MatchManager_OnCancelMatchmaking() //HERE
    {
        ForceSelectorsInteractivity(true, SelectorType.All);
        ValidateGameModeFullData();
    }
    #endregion

    /// <summary>
    /// Specifies selector options using bitwise flags for game mode, game type, and versus player criteria.
    /// </summary>
    [Flags]
    public enum SelectorType
    {
        None = 0,
        GameMode = 1 << 0,
        GameType = 1 << 1,
        VsPlayer = 1 << 2,
        All = GameMode | GameType | VsPlayer
    }
}
