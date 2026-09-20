using Cysharp.Threading.Tasks;
using ProDomino.AdSystem;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.Events;

namespace ProDomino.GameModes
{
    /// <summary>
    /// Manages the main menu and game mode selection, initialization, and transitions for different domino game modes,
    /// including UI handling, analytics, and match lifecycle operations.
    /// </summary>
    public class MenuControllerGameMode : MonoBehaviour
    {
        [SerializeField] private RectTransform _canvas = null;
        [SerializeField] private GameModeConfig _gameModeConfig = null;
        public GameModeConfig _GameModeConfig => _gameModeConfig;
        [SerializeField] private French_GameMode french_GameMode = null;
        [SerializeField] private Block_GameMode block_GameMode = null;
        [SerializeField] private Draw_GameMode draw_GameMode = null;
        [SerializeField] private Five_GameMode five_GameMode = null;
        [SerializeField] private Concentrate_GameMode concentrate_GameMode = null;
        [SerializeField] private RectTransform _chooseDifficulty = null;
        [SerializeField] private RectTransform _chooseConcentrateNumberOfTiles = null;
        [SerializeField] private RectTransform _inGameMenuStuff = null;
        [SerializeField] private CanvasGroup _inGameMenu = null;
        [SerializeField] private GameObject _lobbyChatContainer = null;

        [SerializeField] private GameMode gameModeSelectedID = GameMode.none;
        [SerializeField] private GameType gameTypeSelectedID = GameType.none;
        [SerializeField] private NumberPlayers vsPlayerSelectedID = NumberPlayers.none;

        [SerializeField] private GameModeDataEvent runSingleVsIAEvent;
        [SerializeField] private GameModeDataEvent runCreateOrJoinMatchSessionEvent;
        [SerializeField] private UnityEvent runGiveUpGame;
        [SerializeField] private UnityEvent resetNetworkManager;
        [SerializeField] private UniqueBoolEvent isTimeOut_MatchManager;

        private GameManager gameManager;
        private AdManager adManager;

        private PostMatchResultController _postMatchResultController;
        private RectTransform _currentGameHolder;
        private FullScreenController _fullScreenController;
        private AbstractGameMode currentGameModeScript;

        private int _difficulty;
        private bool _isOpenMenu => _inGameMenu.alpha is not 0;
        private ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none;
        public ConcentrateNumberOfTiles ConcentrateNumberOfTiles => concentrateNumberOfTiles;
        public bool IsInMatch => currentGameModeScript != null;
        private FullScreenController FullScreenController => _fullScreenController = _fullScreenController != null 
            ? _fullScreenController 
            : FindFirstObjectByType<FullScreenController>();
        public PostMatchResultController PostMatchResultController => _postMatchResultController = _postMatchResultController != null
            ? _postMatchResultController
            : FindFirstObjectByType<PostMatchResultController>();

        /// <summary>
        /// This property tries to handle when the player is time-out in Network multiplayer matches<br></br>
        /// It is use to avoid put a tile when the player is timeout (yep, actually this is not controlled internally)
        /// </summary>
        public bool IsTimeOut_MatchManager
        {
            get 
            {
                if (isTimeOut_MatchManager == null)
                    throw new ArgumentException("isTimeOut_MatchManager is missing and is indispensible to avoid bugs");
                
                return isTimeOut_MatchManager.Invoke();
            }
        }

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            adManager = ServiceLocator.Instance.GetService<AdManager>();
        }

        private void OnDestroy()
        {
            // Try to Indicates that the player is no longer playing the game mode when the game is shoutting down
            if (_gameModeConfig)
            {
                if (gameManager)
                { 
                    gameManager?.UpdateAnalyticsValue((false, GlobalAnalyticType.usersPlayingNow, _gameModeConfig.GameModeSelectedID));

                    // Register the local starttime at finish
                    gameManager.UpdateTotalTimeMatch().Forget();

                    // Reset the match start time when the match ends
                    gameManager.RegisterStartMatchDateTime(true);
                }
                else
                    Debug.LogWarning("Couldn't update firebase global analytics due gamemanager is already destroyed");
            }
        }

        /// <summary>
        /// Closes all main menu UI elements by hiding the selection UI and the difficulty selection menu.
        /// </summary>
        private void CloseAllMainMenus()
        {
            _gameModeConfig.SetSelectionUIVisibility(false);
            _chooseDifficulty.gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens the main menu, updates UI elements, resets network state if not in a party relay, and clears selected
        /// game mode and player settings.
        /// </summary>
        public void OpenMainMenu()
        {
            Debug.Log("///-- ***---*** OpenMainMenu");
            _gameModeConfig.PlayGameModeButton.SetButtonInteractable(false);
            CloseAllMainMenus();
            _gameModeConfig.SetSelectionUIVisibility(true);

            //Debug.Log("///-- ***---*** PartyController.IsRelay: " + PartyController.IsRelay);
            Debug.Log("///-- ***---*** PartyController.IsRelay: " + _gameModeConfig.IsPartyRelay);
            Debug.Log("///-- ***---*** gameTypeSelectedID: " + gameTypeSelectedID);

            // If the player is not into a party relay, reset the network manager to avoid issues when creating/joining matches
            //if (!PartyController.IsRelay && gameTypeSelectedID != GameType.singlePlayerIA)
            if (!_gameModeConfig.IsPartyRelay && gameTypeSelectedID != GameType.singlePlayerIA)
            {
                Debug.Log("///-- Open Main Menu");

                resetNetworkManager?.Invoke();
            }
            else
            {
                _gameModeConfig.PlayGameModeButton.SetButtonInteractable(true);
            }

            gameModeSelectedID = GameMode.none;
            gameTypeSelectedID = GameType.none;
            vsPlayerSelectedID = NumberPlayers.none;
        }

        /// <summary>
        /// Enables or disables the Play Game Mode button based on the specified state.
        /// </summary>
        /// <param name="enableState">True to enable the button; false to disable it.</param>
        public void EnablePlayGameModeButton(bool enableState)
        {
            _gameModeConfig.PlayGameModeButton.SetButtonInteractable(enableState);
        }

        /// <summary>
        /// Displays the difficulty selection menu after closing all main menus.
        /// </summary>
        public void OpenDifficultyMenu()
        {
            CloseAllMainMenus();
            _chooseDifficulty.gameObject.SetActive(true);
        }

        /// <summary>
        /// Displays the menu for selecting the number of tiles to concentrate by closing all main menus and activating
        /// the relevant UI element.
        /// </summary>
        public void OpenConcentrateNumberOfTilesMenu()
        {
            CloseAllMainMenus();
            _chooseConcentrateNumberOfTiles.gameObject.SetActive(true);
        }

        /// <summary>
        /// Toggles the active state of the in-game menu based on the current menu open state.
        /// </summary>
        public void InGameMenuActivate()
        {
            _inGameMenu.SetActive(!_isOpenMenu);
        }

        /// <summary>
        /// Sets the game difficulty and starts a new domino game.
        /// </summary>
        /// <param name="selectedDifficulty">The difficulty level to set.</param>
        public void SetDifficulty(int selectedDifficulty)
        {
            _difficulty = selectedDifficulty;
            StartDomino(); //OpenTypeSingleVsIA();
        }

        /// <summary>
        /// Sets the number of tiles for the concentrate game mode based on the provided value and updates the game
        /// state accordingly.
        /// </summary>
        /// <param name="numberOfTiles">The string representation of the number of tiles to use.</param>
        public void SetConcentrateNumberOfTIles(string numberOfTiles)
        {
            if (!Enum.TryParse(numberOfTiles, out concentrateNumberOfTiles))
                concentrateNumberOfTiles = ConcentrateNumberOfTiles.tiles_28_default;

            _chooseConcentrateNumberOfTiles.gameObject.SetActive(false);

            if (gameTypeSelectedID == GameType.singlePlayerIA)
            {
                StartDomino(); //OpenTypeSingleVsIA();
            }
            else
            {
                runCreateOrJoinMatchSessionEvent?.Invoke(new(gameModeSelectedID, gameTypeSelectedID, vsPlayerSelectedID, concentrateNumberOfTiles));
            }
        }

        /// <summary>
        /// Closes all main menus and destroys the current game holder object if it exists.
        /// </summary>
        private void EndGame()
        {
            CloseAllMainMenus();

            if (_currentGameHolder != null)
                Destroy(_currentGameHolder.gameObject);
        }
        
        /// <summary>
        /// Restarts the current domino game by ending the current game and starting a new one.
        /// </summary>
        public void RestartDomino()
        {
            /* TODO: check if this segment of code is really needed
            
            if (gameTypeSelectedID is GameType.competitive or GameType.casual)
            {
                runGiveUpGame?.Invoke();

                if (currentGameModeScript is not null and { ExtendedGameController: not null })
                    currentGameModeScript.ExtendedGameController.DeterminePlayerResult(false);
                else
                    Debug.LogWarning("Couldn't determine player result on surrender because references are null");
            }
            */

            EndGame();
            // This will restart inGameMenu;
            _inGameMenu.SetActive(false);

            StartDomino();
        }

        /// <summary>
        /// Returns the player to the lobby, optionally calling the give up event to determine the player result.
        /// </summary>
        /// <param name="isCallingGiveUpEvent">Whether to call the give up event.</param>
        public void ToLobby(bool isCallingGiveUpEvent = true)
        {
            // If the game type is competitive or casual, we need to call the give up event to determine the player result
            // and avoid bugs like the player put a tile after surrendering or leaving the match without surrendering and
            // the match is still active until the player put a tile or the opponent put a tile
            if (isCallingGiveUpEvent && gameTypeSelectedID is GameType.competitive or GameType.casual)
            {
                Debug.Log($"[MenuControllerGameMode] Player is giving up the match in game mode {gameModeSelectedID} with game type {gameTypeSelectedID} and vs player {vsPlayerSelectedID}");

                // Invoke the give up game event (this calls MatchManager.LeaveMatchSessionProxy and is the responsible to disconnect the player from the match and handle the surrender logic)
                runGiveUpGame?.Invoke();

                // In case of one vs one, the player will be the last one, but in case of 4 players, the player will be the fourth one, so we need to set the last position according to the number of players
                var lastPositionAccoirdingNumberOfPlayers = vsPlayerSelectedID is NumberPlayers.oneVsOne ? 2 : 4;
                currentGameModeScript.ExtendedGameController.DeterminePlayerResult(lastPositionAccoirdingNumberOfPlayers);
            }

            // Turn off post match result screen if it's active
            PostMatchResultController?.SetVisibility(false);

            // Indicates that the player is no longer playing the game mode
            gameManager.UpdateAnalyticsValue((false, GlobalAnalyticType.usersPlayingNow, _gameModeConfig.GameModeSelectedID));

            // Register the local starttime at finish
            gameManager.UpdateTotalTimeMatch().Forget();

            // Reset the match start time when the match ends
            gameManager.RegisterStartMatchDateTime(true);

            EndGame();

            // Once the match is ended, show an interstitial ad before going to lobby
            adManager?.ShowInterstitialAd(async () =>
            {
                // This will close inGameMenu;
                _inGameMenu.SetActive(false);
                _inGameMenuStuff.gameObject.SetActive(false);
                FullScreenController?.SetFullscreen(false);

                OpenMainMenu();

                // Wait until the match is fully closed
                await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => UniTask.WaitUntil(() => !IsInMatch).TimeoutWithoutException(TimeSpan.FromSeconds(3)),
                    taskId: "Wait until match is completely end",
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: true,
                    shouldRetrySomeTimes: false);

                // Validate the game mode full data in case something was changed during the match
                _gameModeConfig.ValidateGameModeFullData();
            });
        }

        /// <summary>
        /// Transitions the player to the lobby without triggering the give up process, handling analytics, match
        /// cleanup, UI updates, and displaying an interstitial ad.
        /// </summary>
        public void ToLobbyWithoutGiveUp()
        {
            // Turn off post match result screen if it's active
            PostMatchResultController?.SetVisibility(false);

            // Indicates that the player is no longer playing the game mode
            gameManager.UpdateAnalyticsValue((false, GlobalAnalyticType.usersPlayingNow, _gameModeConfig.GameModeSelectedID));

            // Register the local starttime at finish
            gameManager.UpdateTotalTimeMatch().Forget();

            // Reset the match start time when the match ends
            gameManager.RegisterStartMatchDateTime(true);

            EndGame();

            // Once the match is ended, show an interstitial ad before going to lobby
            adManager?.ShowInterstitialAd(async () =>
            {
                // This will close inGameMenu;
                _inGameMenu.SetActive(false);
                _inGameMenuStuff.gameObject.SetActive(false);
                FullScreenController?.SetFullscreen(false);

                OpenMainMenu();

                // Wait until the match is fully closed
                await gameManager.HandleProcess_GameManagerProxy
                    (uniTask: () => UniTask.WaitUntil(() => !IsInMatch).TimeoutWithoutException(TimeSpan.FromSeconds(3)),
                    taskId: "Wait until match is completely end",
                    showLoading: true,
                    shouldIgnoreTryAgainProcess: true,
                    shouldRetrySomeTimes: false);

                // Validate the game mode full data in case something was changed during the match
                _gameModeConfig.ValidateGameModeFullData();
            });
        }

        /// <summary>
        /// Initializes and starts a new domino game session by configuring UI elements, setting up the selected game
        /// mode, and updating analytics.
        /// </summary>
        public void StartDomino()
        {
            CloseAllMainMenus();

            // this turns off main menu stuff
            _chooseDifficulty.gameObject.SetActive(false);
            _chooseConcentrateNumberOfTiles.gameObject.SetActive(false);
            _inGameMenuStuff.gameObject.SetActive(true);

            currentGameModeScript = GetGameModeSelected(gameModeSelectedID);

            if (currentGameModeScript == null)
            {
                Debug.LogError("Game mode not found for ID: " + gameModeSelectedID);
            }
            else
            {
                _currentGameHolder = currentGameModeScript.ExtendedGameController.GetRect();

                _currentGameHolder.SetParent(_canvas, false);
                _currentGameHolder.localScale = Vector3.one;
                _currentGameHolder.offsetMin = new Vector2(0, 0);
                _currentGameHolder.offsetMin = new Vector2(0, 0);

                currentGameModeScript.InitializeGameMode(_gameModeConfig, _difficulty, gameTypeSelectedID, vsPlayerSelectedID, isSinglePlayerIA: true, concentrateNumberOfTiles, 
                    InGameMenuActivate, () => IsTimeOut_MatchManager);
                currentGameModeScript.RestartGame();
                currentGameModeScript.SetupRandomHands();

                // Register the new analytic in firebase
                gameManager.UpdateAnalyticsValue((true, GlobalAnalyticType.usersPlayingNow, _gameModeConfig.GameModeSelectedID));

                gameManager.RegisterStartMatchDateTime();
            }
        }

        /// <summary>
        /// Initializes and returns the selected game mode, configuring UI elements and game settings based on the
        /// provided data.
        /// </summary>
        /// <param name="gameModeData">Optional data specifying the game mode and related settings to use.</param>
        /// <returns>The initialized game mode instance, or null if the game mode is not found.</returns>
        public AbstractGameMode GenerateGameMode(GameModeData gameModeData = null)
        {
            _difficulty = 0;

            // this turns off main menu stuff
            _chooseDifficulty.gameObject.SetActive(false);
            _chooseConcentrateNumberOfTiles.gameObject.SetActive(false);
            _inGameMenuStuff.gameObject.SetActive(true);

            // If there is an override object, use it instead the field registered before
            if (gameModeData is not null)
            {
                gameModeSelectedID = gameModeData.gameMode;
                gameTypeSelectedID = gameModeData.gameType;
                vsPlayerSelectedID = gameModeData.NumberPlayers;
                concentrateNumberOfTiles = gameModeData.concentrateNumberOfTiles;
            }

            currentGameModeScript = GetGameModeSelected(gameModeSelectedID);

            if (currentGameModeScript == null)
            {
                Debug.LogError("Game mode not found for ID: " + gameModeSelectedID);
                return null;
            }
            else
            {
                currentGameModeScript.SetChatLobbyObj(_lobbyChatContainer);
                _currentGameHolder = currentGameModeScript.ExtendedGameController.GetRect();

                _currentGameHolder.SetParent(_canvas, false);
                _currentGameHolder.localScale = Vector3.one;
                _currentGameHolder.offsetMin = new Vector2(0, 0);
                _currentGameHolder.offsetMin = new Vector2(0, 0);

                currentGameModeScript.InitializeGameMode(_gameModeConfig, _difficulty, gameTypeSelectedID, vsPlayerSelectedID, isSinglePlayerIA: false, concentrateNumberOfTiles, 
                    InGameMenuActivate, () => IsTimeOut_MatchManager);

                return currentGameModeScript;
            }
        }

        /// <summary>
        /// Returns an instance of the game mode corresponding to the specified GameMode identifier.
        /// </summary>
        /// <param name="gameModeSelected_ID">The identifier of the selected game mode.</param>
        /// <returns>An instantiated AbstractGameMode matching the selected game mode, or null if no match is found.</returns>
        private AbstractGameMode GetGameModeSelected(GameMode gameModeSelected_ID)
        {
            switch (gameModeSelected_ID)
            {
                case GameMode.french:
                    return Instantiate(french_GameMode);
                case GameMode.block:
                    return Instantiate(block_GameMode);
                case GameMode.draw:
                    return Instantiate(draw_GameMode);
                case GameMode.five:
                    return Instantiate(five_GameMode);
                case GameMode.concentrate:
                    return Instantiate(concentrate_GameMode);
                /*case GameMode.concentrate:*/
                default:
                    return null;
            }
        }

        /// <summary>
        /// Sets the selected game mode, game type, player configuration, difficulty level, and number of tiles for the
        /// game.
        /// </summary>
        /// <param name="gameModeID">The game mode to set.</param>
        /// <param name="gameTypeID">The game type to set.</param>
        /// <param name="vsPlayerID">The player configuration to set.</param>
        /// <param name="difficultyLevelSelected">The difficulty level to set.</param>
        /// <param name="concentrateNumberOfTilesSelected">The number of tiles to set for the game.</param>
        public void SetGameModeData(GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID, DifficultyLevel difficultyLevelSelected, ConcentrateNumberOfTiles concentrateNumberOfTilesSelected)
        {
            gameModeSelectedID = gameModeID;
            gameTypeSelectedID = gameTypeID;
            vsPlayerSelectedID = vsPlayerID;
            concentrateNumberOfTiles = concentrateNumberOfTilesSelected;

            switch(difficultyLevelSelected)
            {
                case DifficultyLevel.Easy:
                    _difficulty = 0;
                break;
                case DifficultyLevel.Medium:
                    _difficulty = 1;
                break;
                case DifficultyLevel.Pro:
                    _difficulty = 2;
                break;
                default:
                    _difficulty = 0;
                break;
            }
        }

        /// <summary>
        /// Initializes single-player versus AI game mode data and triggers the corresponding event.
        /// </summary>
        private void OpenTypeSingleVsIA()
        {
            GameModeData gameModeData = new GameModeData
            {
                gameMode = gameModeSelectedID,
                gameType = GameType.singlePlayerIA, //gameTypeSelectedID,
                NumberPlayers = vsPlayerSelectedID,
                concentrateNumberOfTiles = concentrateNumberOfTiles
            };
            
            runSingleVsIAEvent.Invoke(gameModeData);
        }
    }
}
