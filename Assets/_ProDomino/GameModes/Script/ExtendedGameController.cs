using Cysharp.Threading.Tasks;
using DominoTemplate.Controllers;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using ProDomino.AnalyticsSystem;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Timba.Patterns;
using Timba.ProcGen;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;
using static ProDomino.GameModes.PostMatchResultController;

namespace ProDomino.GameModes
{
    public class ExtendedGameController : GameControler
    {
        [SerializeField] private Image boardImage;
        [SerializeField] private Image boardFundImage;
        [SerializeField] private Button settingsButton;
        [SerializeField] private ScoreUI scoreUIPlayer;

        public ScoreUI ScoreUIPlayer => scoreUIPlayer;
        [SerializeField] private ScoreUI scoreUILeft;
        public ScoreUI ScoreUILeft => scoreUILeft;
        [SerializeField] private ScoreUI scoreUITop;
        public ScoreUI ScoreUITop => scoreUITop;
        [SerializeField] private ScoreUI scoreUIRight;
        public ScoreUI ScoreUIRight => scoreUIRight;
        [SerializeField] private RectTransformPanZoomController rectTransformPanZoomController;
        [SerializeField] private FloatRangeDictionaryColor scoreColorDictionary;

        [SerializeField] public int playerRoundScore = 0;
        [SerializeField] public int leftRoundScore = 0;
        [SerializeField] public int topRoundScore = 0;
        [SerializeField] public int rightRoundScore = 0;

        [SerializeField] private int limitPointsGameController = 0;
        public int LimitPointsGameController
        {
            get => limitPointsGameController;
            set => limitPointsGameController = value;
        }

        [Space(10)]
        [SerializeField] Action<int, string> playerMovementEvent;
        [SerializeField] Action<int> localPlayerFinishGameEvent;

        private Sprite defaultBoardImage;
        private Sprite defaultBoardFundImage;

        private int turnCurrentPlayerID = -1;

        /// <summary>
        /// Occurs when player movement starts, providing the player ID and movement type.
        /// </summary>
        public Action<int, string> OnStartPlayerMovementEvent { get; set; }
        
        public Action<int, string> PlayerMovementEvent
        {
            get => playerMovementEvent;
            set => playerMovementEvent = value;
        }

        public Action<int> LocalPlayerFinishGameEvent
        {
            get => localPlayerFinishGameEvent;
            set => localPlayerFinishGameEvent = value;
        }

        bool gameIsCompleteAndFinished;
        public bool GameIsCompleteAndFinished
        {
            get => gameIsCompleteAndFinished;
            set => gameIsCompleteAndFinished = value;
        }

        [SerializeField] string playerWinner;
        public string PlayerWinner
        {
            get => playerWinner;
            set => playerWinner = value;
        }

        private AuthManager authManager;
        private GameManager gameManager;
        private AnalyticsManager  analyticsManager;
        private DictionaryService dictionaryService;
        private PostMatchResultController postMatchResultController;
        private TimerBar lastTimebarSelected;

        public PostMatchResultController PostMatchResultController => postMatchResultController;
        public RectTransformPanZoomController RectTransformPanZoomController => rectTransformPanZoomController;

        [SerializeField] protected RectTransform tilesContainerPlayerHand;
        [SerializeField] protected CustomButtonUI tileSortingBtn;
        public CustomButtonUI TileSortingBtn => tileSortingBtn;

        public bool IsAuthenticated => gameManager is { IsAuthenticated: true };

        private Action openSettings;
        protected Action onPassBtn = null;
        public Action OnPassBtn
        {
            get => onPassBtn;
            set => onPassBtn = value;
        }

        private void Awake()
        {
            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            analyticsManager = ServiceLocator.Instance.GetService<AnalyticsManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();

            if (settingsButton)
                settingsButton.onClick.AddListener(OpenMenuSettings);
            else
                Debug.LogWarning("SettingsButton is not assigned in the inspector.");

            // Check if the required services are available
            if (!authManager || !gameManager || !analyticsManager || !dictionaryService)
            {
                Debug.LogError("One or more required services are not available. Please ensure AuthManager, GameManager, AnalyticsManager, and DictionaryService are properly initialized.");
                return;
            }

            // MainReferenceInitialize the default board fund image
            if (boardFundImage)
                defaultBoardFundImage = boardFundImage.sprite;
            else
                Debug.LogWarning("BoardFundImage is not assigned in the inspector.");

            // MainReferenceInitialize the default board image
            if (defaultBoardImage)
                defaultBoardImage = boardImage.sprite;
            else
                Debug.LogWarning("BoardImage is not assigned in the inspector.");

            // MainReferenceInitialize the RectTransformPanZoomController if it exists
            if (RectTransformPanZoomController)
                RectTransformPanZoomController.Initialize();
            else
                Debug.LogWarning("RectTransformPanZoomController is not assigned or found in the scene.");

            // Subscribe to game events if GameTurnController is available
            GameTurnController?.HandleTurnStartEvent(OnTurnStart);
            GameTurnController?.HandleTurnOverEvent(OnTurnOver);
            GameTurnController?.HandleRoundOverEvent(OnRoundOver);

            var scoreUIs = new ScoreUI[]
            {
                scoreUIPlayer,
                scoreUILeft,
                scoreUITop,
                scoreUIRight
            };

            // Configure timer bar callbacks for each ScoreUI
            foreach (var scoreUI in scoreUIs)
            {
                // Validate the ScoreUI reference
                if (scoreUI is null)
                {
                    Debug.LogWarning("One of the ScoreUI instances is not assigned in the inspector.");
                    continue;
                }

                // Configure the TimerBar with external references
                scoreUI.TimerBar.ConfigureExternalReferences(
                    onStartTimer: OnStartTimer,
                    onUpdateTimer: OnUpdateTimer,
                    onFinishTimer: OnFinishTimer);
            }
        }

        void Start()
        {
            tileSortingBtn?.AddEventToListener(SortDominos);
        }

        // Variant of the MainReferenceInitialize method to set up the post-match result controller
        public void Initialize
            (PostMatchResultController postMatchResultController,
            Action<bool> setVisibleGameplay,
            Action openSettings,
            Func<Domino[]> getFirstTurnAvailableTiles,
            Func<Domino> getBestTile,
            Func<string> getCurrentDisplayName)
        {
            this.postMatchResultController = postMatchResultController ?? throw new ArgumentNullException(nameof(postMatchResultController));
            this.openSettings = openSettings;

            base.Initialize
                (setVisibleGameplay, 
                getFirstTurnAvailableTiles, 
                getBestTile, 
                getCurrentDisplayName, 
                OnShowingLastTile,
                dictionaryService,
                analyticsManager);
        }

        public void ConfigureScoreUis(Dictionary<byte, UserProfile> scoreUIDataCollection)
        {
            if (scoreUIDataCollection is null or { Count: 0 })
            {
                Debug.LogWarning("ConfigureScoreUis: scoreUIDataCollection is null or empty.");
                return;
            }

            // Iterate through the scoreUIDataCollection and configure the corresponding ScoreUI instances
            foreach (var (index, scoreUIData) in scoreUIDataCollection)
                (index switch
                {
                    1 => scoreUIPlayer,
                    2 => scoreUILeft,
                    3 => scoreUITop,
                    4 => scoreUIRight,
                    _ => null
                })?.Configure(scoreUIData, limitPointsGameController, () => turnCurrentPlayerID);
                
            /*scoreUIPlayer?.DisableSelectScoreUI();
            scoreUILeft?.DisableSelectScoreUI();
            scoreUITop?.DisableSelectScoreUI();
            scoreUIRight?.DisableSelectScoreUI();*/
        }

        public override void NotifyPlayerStartsMovement_ToHost(DominoView tileDominoView, string sideInfo)
        {
            _handScript.LockAllTiles();
            OnStartPlayerMovementEvent?.Invoke(tileDominoView.GetDomino().id, sideInfo);
            //_handScript.SetLockPlayerButtons(false);
        }

        public override void NotifyPlayerEndsMovement_ToHost(DominoView tileDominoView, string sideInfo)
        {
            playerMovementEvent?.Invoke(tileDominoView.GetDomino().id, sideInfo);
        }

        public override void RestartGame(int difficulty, GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID, bool restartNewRound = false)
        {
            OnStartPlayerMovementEvent = null;
            PlayerMovementEvent = null;
            SetPlayerUI();
            ExpandHandViewport(false);

            base.RestartGame(difficulty, gameModeID, gameTypeID, vsPlayerID, restartNewRound);
            SetGameplayVisibility(true);

            // Create dummy ScoreUI instances for AI players
            if (gameTypeID is GameType.singlePlayerIA)
            {
                var iconCollection = dictionaryService.GetSpriteCollection(CosmeticType.Icons.ToString());

                var searchRandomSprite = new Func<Sprite>(() =>
                {
                    if (iconCollection is not null && iconCollection.Length > 0)
                        return iconCollection[UnityEngine.Random.Range(0, iconCollection.Length)];
                    return null;
                });

                var randomUniqueNames = new List<string>();

                // Iterate to ensure we have 3 unique names
                for (int i = 0, j = 0; i < 3 || j < 10;)
                {
                    var randomName = DummyNameGenerator.GetRandomName();
                    if (!randomUniqueNames.Contains(randomName))
                    {
                        randomUniqueNames.Add(randomName);
                        i++;

                        j = 0; // Reset j to avoid infinite loop if we have enough unique names
                    } 
                    else
                        j++;
                }

                if (!restartNewRound)
                {
                    var is2v2Mode = VsPlayerSelectedID is NumberPlayers.twoVsTwo;

                    // Configure the ScoreUI instances with the player and AI data
                    ConfigureScoreUis(
                    new()
                    {
                        [1] = new(authManager.UUID, !string.IsNullOrEmpty(authManager.Username) ? authManager.Username : "You", profileIcon: gameManager.ProfilePicture, gameManager.Badges, is2v2Mode ? true : null),
                        [2] = new(null, randomUniqueNames.ElementAtOrDefault(0) ?? "Left AI", searchRandomSprite(), isTeamA: is2v2Mode ? false : null),
                        [3] = new(null, randomUniqueNames.ElementAtOrDefault(1) ?? "Top AI", searchRandomSprite(), isTeamA: is2v2Mode ? true : null),
                        [4] = new(null, randomUniqueNames.ElementAtOrDefault(2) ?? "Right AI", searchRandomSprite(), isTeamA: is2v2Mode ? false : null),
                    });   
                }

                DragHandler.Static_Initialize(() =>
                {
                    if (TurnScript.GetCurrentTurnControl() is 1)
                    {
                        if (ScoreUIPlayer is null or { TimerBar: null })
                        {
                            Debug.LogError("[DragHandler.Static_Initialize] Couldn't check if the player still at time to play the tile. Missing references 'ScoreUIPlayer' or its 'TimerBar'");
                            
                            // It's returned as "time-out" because there is no waut to check. The dev should check those references asap
                            return true;
                        }

                        // Check if the player is time-out
                        return ScoreUIPlayer.TimerBar.LeftingTime <= 0;
                    }

                    // If the bots are playing, in Single vs AI is alwas "still at time" (no-timeout)
                    return false;
                });
            }
            else
            {
                if (TryGetComponent<AbstractGameMode>(out var abstractGameMode))
                    DragHandler.Static_Initialize(() => abstractGameMode.IsTimeout);
                else
                    Debug.LogError("[RestartGame_ExtendedGameController] How is possible don't have a reference of itself?");
            }

            Debug.Log("CustomGameController: RestartGame override ejecutado");
        }

        public void SetPlayerHand(List<int> playerHands, int clientId, Action<DragHandler, int> onAssignTileToHand, Action<int> callback, Action updateBoneyardText)
        {

            int auxNumberTilesPerPlayer = 7;

            if(gameModeSelectedID == GameMode.draw && vsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                auxNumberTilesPerPlayer = 5;
            }

            _deckScript.GetComponent<ExtendedDeckController>().SetCustomPlayerHand(playerHands, clientId, onAssignTileToHand, auxNumberTilesPerPlayer, callback, updateBoneyardText);
        }

        //public IEnumerator DropTilePlayerOnline(int tileID, string sideInfo, int playerID, int clientId, Action callback)
        public IEnumerator DropTilePlayerOnline(int tileID, string sideInfo, string playerPlacingTile, Action callback)
        {
            var AITiles = new List<DragHandler>();

            //1 = _playerTiles, 2 = _leftAITiles, 3 = _topAITiles, 4 = _rightAITiles
            switch (playerPlacingTile)
            {
                case "bottomPlayer":
                    AITiles = _deckScript.GetList(1);
                    break;
                case "leftPlayer":
                    AITiles = _deckScript.GetList(2); 
                    break;
                case "topPlayer":
                    AITiles = _deckScript.GetList(3);
                    break;
                case "rightPlayer":
                    AITiles = _deckScript.GetList(4);
                    break;
                default:
                    Debug.Log("Not a valid player");
                    break;
            }

            // If the list is null or empty, log an error and exit
            if (AITiles is null or { Count: 0 })
            { 
                Debug.LogError($"DropTilePlayerOnline: AITiles list is null or empty. Couldn't continue with the process\n\nPlayer Placing Tile: {playerPlacingTile}");
                yield break;
            }

            // Try to get the selected tile based on the provided tileID; if not found, select the first tile in the list (white tile)
            var selectedTile = AITiles
                ?.Where(x => x) // Filter out null entries
                ?.Select(x => (dragHandler: x, domino: x.GetDominoView()?.GetDomino())) // Project to include domino info
                ?.FirstOrDefault(x => x.domino.id == tileID || x.domino.id is -1) // Find matching tileID or white tile
                .dragHandler;

            // If no matching tile found, log an error and select the first tile in the list as fallback
            if (!selectedTile)
            {
                Debug.LogError($"DropTilePlayerOnline: No matching tile found for Tile ID: {tileID}. Selecting the first tile in the list as fallback. AiTiles: {string.Join(", ", AITiles.Select(t => t.GetDominoView()?.GetDomino() is null ? "null" : t.GetDominoView().GetDomino().id.ToString()))}\n\nPlayer Placing Tile: {playerPlacingTile}");
                selectedTile = AITiles?.FirstOrDefault();
            }

            // If the selected tile is still null, log an error and exit
            if (!selectedTile)
            {
                Debug.LogError($"DropTilePlayerOnline: Selected tile is null. Couldn't continue with the process\n\nTile ID: {tileID}\nPlayer Placing Tile: {playerPlacingTile}");
                yield break;
            }

            var tempDominoView = selectedTile.GetDominoView();
            tempDominoView.SetDomino(_deckScript.SetupDominoInfo(tileID), _deckScript.SpriteArray[tileID], _deckScript.BackTile);

            DragHandler toDrop = selectedTile;

            currentDrag = tempDominoView;
            yield return StartCoroutine(CreateSlot().ToCoroutine());
            currentDrag = null;

            RectTransform rightSlot = null;
            RectTransform leftSlot = null;

            RectTransform topSlot = null;
            RectTransform downSlot = null;


            GetSlots(ref rightSlot, ref leftSlot, ref topSlot, ref downSlot);

            // Map each side to its corresponding slot
            var slotBySide = new Dictionary<string, RectTransform>
            {
                { "right", rightSlot },
                { "left",  leftSlot  },
                { "top",   topSlot   },
                { "down",  downSlot  }
            };

            // Try to get the target slot based on sideInfo
            slotBySide.TryGetValue(sideInfo, out var targetSlot);

            // Move to the resolved slot (null is allowed and intentional)
            yield return StartCoroutine(
                toDrop.LerpMove(
                    targetSlot,
                    isAI: true,
                    notifyToHost: false,
                    isNotificationFromHost: true,
                    OnFinishDrag
                )
            );

            //AITiles.RemoveAt(0);

            callback?.Invoke();

            yield return null;
        }

        #region Drop Tile In Replay Mode
        public IEnumerator DropTilePlayerReplay(DragHandler selectedTile, string sideInfo, string playerPlacingTile, bool placingInstant = false, Action callback = null)
        {

            List<DragHandler> AITiles = new List<DragHandler>();

            switch (playerPlacingTile)
            {
                case "leftPlayer":
                    AITiles = _deckScript.GetList(2); //1 = _playerTiles, 2 = _leftAITiles, 3 = _topAITiles, 4 = _rightAITiles
                    break;
                case "topPlayer":
                    AITiles = _deckScript.GetList(3);
                    break;
                case "rightPlayer":
                    AITiles = _deckScript.GetList(4);
                    break;
                case "player":
                    AITiles = _deckScript.GetList(1);
                    break;
                case "dominoTiles":
                    AITiles = _deckScript.GetList(0);
                    break;
                default:
                    Debug.Log("Not a valid player");
                    break;
            }

            //DragHandler selectedTile = AITiles[0];
            DominoView tempDominoView = selectedTile.GetDominoView();
            //tempDominoView.SetDomino(_deckScript.SetupDominoInfo(tileID), _deckScript.SpriteArray[tileID], _deckScript.BackTile);

            DragHandler toDrop = selectedTile;

            currentDrag = tempDominoView;
            yield return StartCoroutine(CreateSlot().ToCoroutine());
            currentDrag = null;

            RectTransform rightSlot = null;
            RectTransform leftSlot = null;

            RectTransform topSlot = null;
            RectTransform downSlot = null;


            GetSlots(ref rightSlot, ref leftSlot, ref topSlot, ref downSlot);

            // Map each side to its corresponding slot
            var slotBySide = new Dictionary<string, RectTransform>
            {
                { "right", rightSlot },
                { "left",  leftSlot  },
                { "top",   topSlot   },
                { "down",  downSlot  }
            };

            // Resolve target slot (null if side is invalid or slot missing)
            slotBySide.TryGetValue(sideInfo, out var targetSlot);

            // Choose movement coroutine based on placingInstant flag
            IEnumerator moveRoutine = placingInstant
                ? toDrop.PlacingInstant(targetSlot, notifyToHost: false, isNotificationFromHost: true)
                : toDrop.LerpMove(targetSlot, isAI: true, notifyToHost: false, isNotificationFromHost: true, OnFinishDrag);

            // Execute movement
            yield return StartCoroutine(moveRoutine);

            //AITiles.RemoveAt(0);
            AITiles.Remove(selectedTile);

            callback?.Invoke();

            yield return null;
        }
        #endregion

        /// <summary>
        /// Discards the currently selected tile if one is being dragged, removing all associated slots and clearing the
        /// drag reference.
        /// </summary>
        public void DiscardSelectedTile()
        {
            // Check if there is a current drag reference to discard
            if (currentDrag)
            {
                // Check if the current drag has a DragHandler component and call DiscardTile on it
                if (currentDrag.TryGetComponent<DragHandler>(out var dragHandler))
                    dragHandler.DiscardTile();

                // Remove all slots and clear the current drag reference
                RemoveAllSlots();
                currentDrag = null;
            }
            else
                Debug.LogWarning("DiscardSelectedTile: currentDrag reference is null. Cannot discard tile.");
        }

        /// <summary>
        /// Enables the turn indicators for the player whose turn it is
        /// </summary>
        public void EnableTurnForPlayer(int playerTurnID, int localPlayerID) //HERE TURN UI
        {
            // Check if the player whose turn it is matches the local player
            if (playerTurnID == localPlayerID)
            {
                _turnScript.SetTurns(true, false, false, false); // Player, Left, Top, Right
                _handScript.SetTurnFor(true, false, false, false, isFromHost: true); //Player, Left, Top, Right
            }

            // Else, it's another player's turn
            else
            {
                // If it's a 1v1 match, the other player is always at the top
                if (vsPlayerSelectedID is NumberPlayers.oneVsOne)
                {
                    _turnScript.SetTurns(false, false, true, false); // Player, Left, Top, Right
                    _handScript.SetTurnFor(false, false, true, false, isFromHost: true); //Player, Left, Top, Right
                }
                else
                {
                    // In a 4-player match, calculate relative position from the local player
                    // 0 => Bottom (already handled above)
                    // 1 => Left
                    // 2 => Top
                    // 3 => Right
                    var relativePosition = (playerTurnID - localPlayerID + 4) % 4;

                    var isLeft = (relativePosition == 1);
                    var isTop = (relativePosition == 2);
                    var isRight = (relativePosition == 3);

                    // Set the turn indicators based on the relative position
                    _turnScript.SetTurns(false, isLeft, isTop, isRight); // Bottom, Left, Top, Right
                    _handScript.SetTurnFor(false, isLeft, isTop, isRight, isFromHost: true); // Bottom, Left, Top, Right
                }
            }
        }

        public void DeterminePlayerResult(int resultPosition)
        { 
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(ExtendedGameController)}]</b> DeterminePlayerResult: Player result position: {resultPosition}.</color>");
            LocalPlayerFinishGameEvent?.Invoke(resultPosition);

            // Send analytics for the game played
            analyticsManager?.SendAnalytic(AnalyticType.GamesPlayed, 1);

            // If the player is the winner, send the total wins analytic
            if (resultPosition is 1)
            {
                analyticsManager?.SendAnalytic(AnalyticType.TotalWins, 1);

                // If the game type is competitive, send the total competitive wins analytic
                if (GameTypeSelectedID is GameType.competitive)
                    analyticsManager?.SendAnalytic(AnalyticType.TotalCompetitiveWins, 1);
            }
        }

        private void OpenMenuSettings()
        {
            if (openSettings is null)
            { 
                Debug.LogWarning("OpenMenuSettings: openSettings action is not assigned.");
                return;
            }

            openSettings();
        }

        #region Updte Ui From Host
        /// <summary>
        /// This function should be called only when the turn ends
        /// </summary>
        /// <param name="newTurnIndex"></param>
        public void UpdatePlayerTurnExternally(int newTurnIndex)
        {
            Debug.Log($"<color={Consts.Colors.Success}><b>[{nameof(ExtendedGameController)}]</b> UpdatePlayerTurnExternally: Updating current player turn from {turnCurrentPlayerID} to {newTurnIndex}.</color>");
            turnCurrentPlayerID = newTurnIndex;
        }

        /// <summary>
        /// Starts the timer bar externally for the current player
        /// </summary>
        public void OnStartTimer_Externally(int turnIndex)
        {
            // Select the appropriate timebar based on the current player's turn
            var scoreUITostart = turnIndex switch
            {
                1 => scoreUIPlayer,
                2 => scoreUILeft,
                3 => scoreUITop,
                4 => scoreUIRight,
                _ => default
            };

            // Start the timer for the current player's ScoreUI
            if (scoreUITostart && scoreUITostart.TimerBar)
                scoreUITostart.TimerBar.CallExternallyOnStartTimer();
            else
                Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(ExtendedGameController)}]</b> OnStartTimer_Externally: ScoreUI instance to start or its timebar is null.</color>");
        }

        /// <summary>
        /// Updates the timer bar externally for the current player
        /// Avoid logging too much info with this method
        /// </summary>
        public void OnUpdateTimer_Externally(float remainingTime, float maxTimePerTurn)
        {
            // Select the appropriate timebar based on the current player's turn
            var timebarToUpdate = turnCurrentPlayerID switch
            {
                1 => scoreUIPlayer?.TimerBar,
                2 => scoreUILeft?.TimerBar,
                3 => scoreUITop?.TimerBar,
                4 => scoreUIRight?.TimerBar,
                _ => default
            };

            // Update the selected timebar
            if (timebarToUpdate)
                timebarToUpdate.CallExternallyOnUpdateTimeBar(remainingTime, maxTimePerTurn);
        }

        /// <summary>
        /// Stops every timer bar externally
        /// </summary>
        public void OnFinishTimer_Externally()
        {
            var scoresUIToStop = new List<ScoreUI>
            {
                scoreUIPlayer,
                scoreUILeft,
                scoreUITop,
                scoreUIRight
            }
            ?.ToArray();

            // Iterate through the timebars and stop them
            if (scoresUIToStop is not null and { Length: > 0 })
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(ExtendedGameController)}]</b> OnFinishTimer_Externally: Stopping {scoresUIToStop.Length} ScoreUI timer bars.</color>");
                foreach (var scoreUI in scoresUIToStop)
                {
                    if (scoreUI && scoreUI.TimerBar)
                        scoreUI.TimerBar.CallExternallyOnFinishTimer(false);
                    else
                        Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(ExtendedGameController)}]</b> OnFinishTimer_Externally: One of the ScoreUI instances to stop or its TimerBar is null.</color>");
                }
            }
            else
                Debug.LogWarning($"<color={Consts.Colors.Process}><b>[{nameof(ExtendedGameController)}]</b> OnFinishTimer_Externally: No ScoreUI instances to stop were found.</color>");
        }
        #endregion

        #region tiles sorting
        [SerializeField] private bool tileSortAscending = false; // true = ascendente, false = descendente

        public void SortDominos()
        {
            tileSortAscending = !tileSortAscending;

            List<DominoView> dominoViews = tilesContainerPlayerHand
                .GetComponentsInChildren<DominoView>()
                .ToList();

            IOrderedEnumerable<DominoView> ordered = tileSortAscending
                ? dominoViews.OrderBy(d => d.GetDomino().TopIndex + d.GetDomino().BottomIndex)
                : dominoViews.OrderByDescending(d => d.GetDomino().TopIndex + d.GetDomino().BottomIndex);

            foreach (DominoView item in ordered)
            {
                item.transform.SetSiblingIndex(tilesContainerPlayerHand.childCount - 1);
                item.transform.SetAsLastSibling();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(tilesContainerPlayerHand);

            SoundManager.Instance.PlaySFX(IDAudioClip.sorting);
        }

        /*public void SortDominos()
        {
            tileSortAscending = !tileSortAscending;

            // Get all children that a DominoView has
            List<DominoView> dominoViews = tilesContainerPlayerHand
                .GetComponentsInChildren<DominoView>()
                .ToList();

            // Sort by ID
            IOrderedEnumerable<DominoView> ordered = tileSortAscending
                ? dominoViews.OrderBy(d => d.GetDomino().id)
                : dominoViews.OrderByDescending(d => d.GetDomino().id);

            // Reassign the order in the hierarchy (from left to right)
            foreach (DominoView item in ordered)
            {
                item.transform.SetSiblingIndex(tilesContainerPlayerHand.childCount - 1);
                item.transform.SetAsLastSibling(); // move the object to the end
            }

            // Force refresh of the layout to apply the new visual order
            LayoutRebuilder.ForceRebuildLayoutImmediate(tilesContainerPlayerHand);
        }*/
        #endregion
        
        public void ActivatePrompt(string promptText, float? forcedMinWaitSeconds = null, Action onShown = null)
        {
            // Select the appropriate ScoreUI based on the new turn index
            var currentScoreUI = (turnCurrentPlayerID switch
            {
                1 => scoreUIPlayer,
                2 => scoreUILeft,
                3 => scoreUITop,
                4 => scoreUIRight,
                _ => default
            });

            if (!currentScoreUI)
                currentScoreUI = ScoreUI.SelectedScoreUI;

            if (currentScoreUI)
                currentScoreUI.ActivatePrompt(turnCurrentPlayerID, promptText, forcedMinWaitSeconds, onShown);
        }

        public void SetGameplayVisibility(bool isVisible)
        {
            _setVisibleGameplay?.Invoke(isVisible);
        }

        /// <summary>
        /// Configure the client UI based on the current game state and player authentication.
        /// </summary>
        private void SetPlayerUI()
        {
            // Wait until the AuthManager is initialized
            if (gameManager is null or { IsAlreadyInitialized: false }
                || !IsAuthenticated)
            { 
                Debug.LogWarning("AuthManager or GameManager is not initialized or authenticated. Cannot set UI.");
                return;
            }

            // Override the board fund image if the board fund skin searched from the GameManager is available
            if (boardFundImage)
                boardFundImage.sprite = gameManager?.BoardFund ?? defaultBoardFundImage;
            else
                Debug.LogWarning("BoardFundImage is not assigned in the inspector.");

            // Override the board image if the board skin searched from the GameManager is available
            if (boardImage)
                boardImage.sprite = gameManager?.Board ?? defaultBoardImage;
            else
                Debug.LogWarning("BoardImage is not assigned in the inspector.");

            // Override the tile sprites if the tiles skin searched from the GameManager is available
            var tiles = gameManager?.Tiles;
            if (tiles is not null and { Length: 28 })
                (DeckController as ExtendedDeckController)?.OverrideSpriteArray(gameManager.Tiles, gameManager.BackTile);
            else
                Debug.LogWarning("GameManager.Tiles is null or does not contain 28 tiles.");

            // Override the tile skin for Concentrate game mode if available
            if (TryGetComponent(out Concentrate_GameMode concentrate_GameMode))
                concentrate_GameMode.OverrideTilesSkin(gameManager.GetTiles().id);

            // Override the boneyard skin if ExtendedDeckController is available
            (DeckController as ExtendedDeckController)?.OverrideBoneyardSkin(gameManager.PlayerProfileData.tileSkinID);
        }

        protected override void ClearGameBoard()
        {
            base.ClearGameBoard();

            if (!RectTransformPanZoomController)
            { 
                Debug.LogWarning("RectTransformPanZoomController is not assigned.");
                return;
            }

            RectTransformPanZoomController.ResetController();
        }

        public override async UniTask CreateSlot()
        {
            if (currentDrag == null)
            {
                Debug.LogError("[ExtendedGameController] currentDrag reference is missing. Couldn't' configure slots");
                return;
            }

            // HOTFIX: Register reference to "fix" a bug that clean currentDrag reference in the next frame
            var oldReference = currentDrag;

            if (RectTransformPanZoomController)
            { 
                RectTransformPanZoomController.StopCoroutines();
                RectTransformPanZoomController.RestartFields();
                RectTransformPanZoomController.SetPanZoomBlockStatus(true);

                // Wait to make sure the pan and zoom controller is stopped
                await UniTask.NextFrame();
            }

            // Check if the currentDrag lost its reference
            if (!currentDrag)
            {
                Debug.LogWarning("[ExtendedGameController] currentDrag reference is missing. Trying to use old reference to re-link");

                // Try to link again the old reference
                currentDrag = oldReference;

                // If the reference still missing, maybe is because the GO is destroyed instead of unlinked
                if (!currentDrag)
                {
                    Debug.LogError("[ExtendedGameController] Someone delete the current drag in the fram when the pan zoom controller was blocking itself");
                    return;
                }
            }

            // Create four slots (up, down, left, right)
            _slotsCreated = _slotPosScript.SetGamePositions(ref slot, ref slot2, ref slot3, ref slot4,
                currentDrag, _slotPrefab, _turnScript.GetPlayerTurn(), this);

            // Register the created slots
            RegisterSlot(slot);
            RegisterSlot(slot2);
            RegisterSlot(slot3);
            RegisterSlot(slot4);

            // Invoke the SetSlotPosition action if it exists
            SetSlotPoosition?.Invoke(_slotList);

            // Wait for the slots to be created (or destroyed)
            await UniTask.NextFrame();

            // Update the RectTransformPanZoomController bounds if it exists and has a target rect
            if (RectTransformPanZoomController is not null and { TargetRect: not null } && (slot || slot2 || slot3 || slot4))
            {
                // Update the bounds of the RectTransformPanZoomController only if the player is currently taking their turn
                if (GameTurnController.GetPlayerTurn())
                { 
                    RectTransformPanZoomController.UpdateTilesBounds(true, true);
                    
                    // Wait a moment to ensure the bounds are updated
                    await UniTask.WaitForSeconds(.25f);
                }
            }

            void RegisterSlot(RectTransform slot)
            {
                if (slot != null)
                {
                    // Add the created slots to the list of slots
                    slot.DeepParenting(RectTransformPanZoomController.TargetRect);

                    if (!_slotList.Contains(slot))
                        _slotList.Add(slot);
                }
            }
        }

        public void SetPanAndZoomControllerInteractivity(bool isInteractable)
        {
            RectTransformPanZoomController?.SetPanZoomBlockStatus(!isInteractable);
        }

        protected override IEnumerator ClearObjects()
        {
            var everySlots = RectTransformPanZoomController.TargetRect.GetComponentsInChildren<EmptySlot>(true);
            if (everySlots is not null and { Length: > 0 })
            { 
                for (var i = 0; i < everySlots.Length; i++)
                    Destroy(everySlots[i].gameObject);
                
                yield return null;
            }
        }

        public void UpdateScoreUI()
        {
            var scoreUICollection = new Dictionary<byte, ScoreUI>
            {
                { 1, scoreUIPlayer },
                { 2, scoreUILeft },
                { 3, scoreUITop },
                { 4, scoreUIRight }
            };

            // Try to update the score UI
            foreach (var (index, scoreUI) in scoreUICollection)
            {
                if (scoreUI != null)
                {
                    var score = index switch
                    {
                        1 => GameTurnController.PlayerScore,
                        2 => GameTurnController.LeftAIScore,
                        3 => GameTurnController.TopAIScore,
                        4 => GameTurnController.RightAIScore,
                        _ => default(int?)
                    };

                    if (score.HasValue)
                    {
                        var targetScore = default(int?); // TODO: get target score from game settings or rules
                        scoreUI.ConfigureScore(score.Value, limitPointsGameController, targetScore);
                    }
                } else
                    Debug.LogWarning($"ScoreUI for player {index} is null.");
            }
        }

        public void Replay_OnTurnStart(int currentTurnIndex)
        {
            var newTurnIndex = currentTurnIndex;

            // Select the appropriate ScoreUI based on the new turn index
            var currentScoreUI = (newTurnIndex switch
            {
                1 => scoreUIPlayer,
                2 => scoreUILeft,
                3 => scoreUITop,
                4 => scoreUIRight,
                _ => default
            });

            // Set the current ScoreUI as selected and start its timer
            currentScoreUI?.SelectScoreUI();
        }

        public void HidePlayerTimer()
        {
            scoreUIPlayer?.TimerBar?.gameObject.SetActive(false);
        }

        public void ExpandHandViewport(bool isExpanded)
        {
            var scores = new List<ScoreUI> { scoreUILeft, scoreUITop, scoreUIRight }.Where(x => x != null).ToList();
            if (scores is null or { Count: 0 })
                return;

            scores.ForEach(x => x.ExpandHandViewport(isExpanded));
        }

        /// <summary>
        /// Called when the timer starts for the current player.
        /// </summary>
        private void OnStartTimer(ScoreUI scoreUI)
        {
            // Check if the ScoreUI is null
            if (!scoreUI)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(ExtendedGameController)}]</b> OnStartTimer: ScoreUI is null for player with clientId {turnCurrentPlayerID}.</color>");
                return;
            }

            // Set the current ScoreUI as selected and start its timer
            scoreUI?.SelectScoreUI();
        }

        /// <summary>
        /// Called when the timer updates for the current player.
        /// </summary>
        private void OnUpdateTimer(float currentTimer, float duration, ScoreUI scoreUI) 
        {
            // In Single Player Vs AI mode, we don't update the timer here. It is handled via ExtendedGameController.OnTurnStart
            if (GameTypeSelectedID is GameType.singlePlayerIA)
                return;

            // Check if the ScoreUI is null
            if (!scoreUI || !scoreUI.TimerBar)
            {
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(ExtendedGameController)}]</b> OnUpdateTimer: ScoreUI or its Timebar is null for player with clientId {turnCurrentPlayerID}.</color>");
                return;
            }

            // Set the current ScoreUI as selected and start its timer
            scoreUI.TimerBar.SetSpecificTimerValue(currentTimer, duration);

        }

        /// <summary>
        /// Called when the timer finishes for the current player.
        /// </summary>
        public async void OnFinishTimer(bool isTimeOut, ScoreUI scoreUI)
        {
            // If the ScoreUI is null, log a warning and exit
            if (scoreUI)
            {
                // Deselect the current ScoreUI
                scoreUI.DeselectScoreUI();

                // If the timer finished without timeout, stop the timer bar
                if (scoreUI.TimerBar)
                    scoreUI.TimerBar.StopTimer();
                else
                    Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(ExtendedGameController)}]</b> OnFinishTimer: TimerBar is null in ScoreUI for player with clientId {turnCurrentPlayerID}.</color>");
            } 
            else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(ExtendedGameController)}]</b> OnFinishTimer: ScoreUI is null for player with clientId {turnCurrentPlayerID}.</color>");


            if (GameMode is not GameMode.concentrate)
            {
                // If the timeout occurred for an AI player, then let the AI play a random move
                if (GameTypeSelectedID is GameType.singlePlayerIA && isTimeOut)
                {
                    // Block the hand and deck controllers
                    HandController.LockAllTiles();
                    HandController.SetLockPlayerButtons(false);
                    DeckController.ControlAllHands();

                    // Wait until the new pan and zoom alteration is donde
                    SetPanAndZoomControllerInteractivity(false);

                    // Wait for a second before letting the PanAndZoomController be interactable again
                    await UniTask.WaitForSeconds(1, true);

                tryRandomMove:

                    // Let the AI play a random move
                    AIController.PlayRandomMove(out var hasPassed, out var hasPlayedATile);

                    // Wait for a second if the AI has neither passed nor played a tile
                    if (!hasPassed && !hasPlayedATile)
                    {
                        Debug.Log("AI did not play a tile nor passed. Waiting for 1 second before trying again.");
                        await UniTask.WaitForSeconds(1, true);

                        HandController.LockAllTiles();
                        HandController.SetLockPlayerButtons(false);
                        DeckController.ControlAllHands();

                        Debug.Log("Trying to let the AI play a random move again.");
                        goto tryRandomMove;
                    }
                }
            } else if (TryGetComponent<Concentrate_GameMode>(out var concentrate_GameMode))
            {
                concentrate_GameMode.ForcePlayATile();
            } else
            {
                Debug.LogError("[ExtendedGameController_OnFinishTimer] Is concentrate game mode, but its abstract script reference is missing");
            }
        }

        /// <summary>
        /// Called when a new turn starts.<br></br>
        /// Called in SinglePlayer Vs Ai mode only.
        /// </summary>
        private void OnTurnStart()
        {
            var newTurnIndex = GameTurnController.GetCurrentTurnControl();

            // Select the appropriate ScoreUI based on the new turn index
            var currentScoreUI = (newTurnIndex switch
            {
                1 => scoreUIPlayer,
                2 => scoreUILeft,
                3 => scoreUITop,
                4 => scoreUIRight,
                _ => default
            });

            // Set the current ScoreUI as selected and start its timer
            currentScoreUI?.SelectScoreUI();

            AbstractGameMode auxAbstractGameMode = gameObject.GetComponent<AbstractGameMode>();

            if (vsPlayerSelectedID != NumberPlayers.solo && gameModeSelectedID != GameMode.replay && !auxAbstractGameMode.IsSelectedTileFromBoneyard)
                currentScoreUI?.TimerBar?.StartTimer(60);
        }

        /// <summary>
        /// Called when the turn is over.<br></br>
        /// Called in SinglePlayer Vs Ai mode only.
        /// </summary>
        private void OnTurnOver()
        {
            var newTurnIndex = GameTurnController.GetCurrentTurnControl();

            // Select the appropriate ScoreUI based on the new turn index
            var currentScoreUI = (newTurnIndex switch
            {
                1 => scoreUIPlayer,
                2 => scoreUILeft,
                3 => scoreUITop,
                4 => scoreUIRight,
                _ => default
            });

            // Deselect the current ScoreUI and stop its timer
            currentScoreUI?.DeselectScoreUI();
            currentScoreUI?.TimerBar?.StopTimer();

            UpdateScoreUI();
        }

        public override void OnPassingTurn(bool isPlayer)
        {
            if (!isPlayer)
                ActivatePrompt("Pass!", 2);
        }

        private void OnShowingLastTile()
        {
            ActivatePrompt("Last Tile!", 2);
        }

        private void OnRoundOver()
        {
            UpdateScoreUI();

            if (TryGetComponent(out AbstractGameMode abstractGameMode))
                abstractGameMode.ForceShowTiles();

            if (!postMatchResultController)
            {
                Debug.LogError("postMatchResultController is missing");
                return;
            }

            /*
                Note: Concentrate was designed to have only one round. So, instead of using the round score, 
                we will use the total score of the player and AIs to show them in the post-match result controller
             */

            var scoreCollection = new Dictionary<byte, (ScoreUI scoreUI, int roundScore)>()
            {
                { 1, (scoreUIPlayer, GameMode is not GameMode.concentrate ? playerRoundScore : GameTurnController.PlayerScore) }
            };

            // According to the players amount, add the corresponding ScoreUI instances to the collection
            if (VsPlayerSelectedID is NumberPlayers.oneVsThree or NumberPlayers.twoVsTwo)
            {
                scoreCollection.Add(2, (scoreUILeft, GameMode is not GameMode.concentrate ? leftRoundScore : GameTurnController.LeftAIScore));
                scoreCollection.Add(3, (scoreUITop, GameMode is not GameMode.concentrate ? topRoundScore : GameTurnController.TopAIScore));
                scoreCollection.Add(4, (scoreUIRight, GameMode is not GameMode.concentrate ? rightRoundScore : GameTurnController.RightAIScore));
            }
            else if (VsPlayerSelectedID is NumberPlayers.oneVsOne)
            {
                scoreCollection.Add(2, (scoreUITop, GameMode is not GameMode.concentrate ? topRoundScore : GameTurnController.TopAIScore));
            }

            var orderAssignationLowerized = new List<string> { "player", "left", "top", "right" };
            var playerWinnerArray = PlayerWinner.Split(' ');
            
            // In some cases the match winner will contain not only the person that put the last tile, but the complete team
            var winnerIndex = playerWinnerArray
                ?.Select(x => orderAssignationLowerized.IndexOf(x.ToLower()))
                ?.FirstOrDefault(x => x is not -1) 
                ?? -1; // If the winner is not in the array, return missing (-1)

            // Try to "standarize" the winner index to starts from the recognized value of '1' instead of the common one '0'
            // This is to make sure compatibility between everything
            if (winnerIndex is not -1)
                winnerIndex += 1;

            Debug.Log($"[ExtendedGameController] Round is over. Recognized winner index: {winnerIndex} based on PlayerWinner string: '{PlayerWinner}'");

            // French mode inverts score logic: higher score means worse result
            var isFrenchMode = GameMode is GameMode.french;

            // Order the score collection based on the score, taking into account that in French mode the order is inverted (lower score is better)
            var orderedScoreList = scoreCollection
                ?.OrderBy(x =>
                    !isFrenchMode
                        ? -x.Value.scoreUI.Score // higher score first
                        : x.Value.scoreUI.Score  // lower score first
                )?.ToList();

            // If the winner is recognized and it's not already in the first position of the ordered list,
            // reorder the list to put the winner first
            if (orderedScoreList is not null && orderedScoreList.First().Key != winnerIndex)
            {
                // Locate the winner entry in the ordered list
                var winnerEntry = scoreCollection.FirstOrDefault(x => x.Key == winnerIndex);

                // Insert the winner at the beginning of the list if it's not there already (can happen in team games when the teammate of the winner is the one that put the last tile)
                if (!winnerEntry.Equals(default(KeyValuePair<byte, (ScoreUI scoreUI, int roundScore)>)))
                {
                    orderedScoreList.Remove(winnerEntry);
                    orderedScoreList.Insert(0, winnerEntry);
                }
            }

            // Once the list is ordered, create the MatchResult array to configure the post-match result controller,
            // including player information and scores
            var matchResults = orderedScoreList?.Select((x, index) =>
                new MatchResult(
                    playerUsername: x.Value.scoreUI.UserProfile?.Username,
                    playerUID: x.Value.scoreUI.UserProfile?.UserId,
                    playerIcon: x.Value.scoreUI.UserProfile?.ProfileIcon,
                    position: (byte)(index + 1),
                    lastRoundScore: x.Value.roundScore,
                    rankedPoints: x.Value.scoreUI.Score,
                    playerIndex: x.Key,
                    isTeamA: x.Value.scoreUI.IsTeamA
                ))
                ?.ToArray();

            // Configure the post-match result controller with the match results
            if (matchResults is not null and { Length: > 0 })
            {
                var teamARoundPoints = 0;
                var teamBRoundPoints = 0;

                // Calculate the ranked points to be the sum of the player's score and their partner's score, to show them in the post-match result controller
                var teamAAccumulatedPoints = 0;
                var teamBAccumulatedPoints = 0;

                // If the game mode is 2v2, reorder players so teammates share the same final position
                if (VsPlayerSelectedID is NumberPlayers.twoVsTwo)
                {
                    var playerWhoWon = 
                        matchResults.FirstOrDefault(x => x.PlayerIndex == winnerIndex) ?? matchResults.FirstOrDefault();

                    // Calculate team points if it's a 2v2 game mode, to show them in the post-match result controller
                    teamARoundPoints = scoreCollection?.Where(x => x.Key is 1 or 3)?.Sum(x => x.Value.roundScore) ?? 0;
                    teamBRoundPoints = scoreCollection?.Where(x => x.Key is 2 or 4)?.Sum(x => x.Value.roundScore) ?? 0;

                    // Calculate the ranked points to be the sum of the player's score and their partner's score, to show them in the post-match result controller
                    teamAAccumulatedPoints = matchResults.Where(x => x.PlayerIndex is 1 or 3).Sum(x => x.RankedPoints);
                    teamBAccumulatedPoints = matchResults.Where(x => x.PlayerIndex is 2 or 4).Sum(x => x.RankedPoints);

                    if (playerWhoWon is not null)
                    {
                        // Team A: PlayerIndex 1 and 3
                        // Team B: PlayerIndex 2 and 4
                        bool isTeamAWinner = playerWhoWon.PlayerIndex is 1 or 3;

                        matchResults = matchResults
                            // 1) Winner first
                            .OrderByDescending(x => x.PlayerIndex == winnerIndex)

                            // 2) Winner's teammate second
                            .ThenByDescending(x =>
                                isTeamAWinner
                                    ? x.PlayerIndex is 1 or 3
                                    : x.PlayerIndex is 2 or 4
                            )

                            // 3) Order inside each team by score
                            .ThenByDescending(x => x.LastRoundScore)
                            .ToArray();

                        // Update the match result according the new order.
                        // In 2v2, first two players share position 1, last two share position 2
                        for (int i = 0; i < matchResults.Length; i++)
                            matchResults[i].Position = (uint)(i < 2 ? 1 : 2);
                    }
                }

                // Determine if the game is complete and finished based on the current round, scores, and game rules
                postMatchResultController.Configure(
                    gameMode: GameMode, gameType: GameTypeSelectedID, numberPlayers: VsPlayerSelectedID, 
                    matchResults: matchResults, 
                    limitPoints: limitPointsGameController, playerWinner: playerWinner, currentRound: _turnScript.RoundsCount, 
                    isGameOver: gameIsCompleteAndFinished, 
                    teamARoundPoints: teamARoundPoints,
                    teamBRoundPoints: teamBRoundPoints,
                    teamAAccumulatedPoints: teamAAccumulatedPoints,
                    teamBAccumulatedPoints: teamBAccumulatedPoints);

                postMatchResultController.SetVisibility(true);
                ExpandHandViewport(true);

                // Once the game is complete and finished, invoke the PlayerFinishGameEvent to register the player's match result to backend
                if (GameIsCompleteAndFinished)
                {
                    var playerMatchResult = matchResults.FirstOrDefault(x => x.PlayerUID == authManager.UUID);

                    // If we can't find the player's match result by UID (can happen in some edge cases or if the UID is not properly registered), try to find it by username as a fallback (assuming the username is unique, which should be the case in our game) 
                    if (playerMatchResult is null)
                        playerMatchResult = matchResults.FirstOrDefault(x => x.PlayerUsername.ToLowerInvariant() == "you");

                    // If after all the attempts we can't find the player's match result, log an error.
                    // This should never happen, but it's good to be prepared for it to avoid silent errors and make them easier to track.
                    if (playerMatchResult is null)
                    { 
                        Debug.LogError($"[ExtendedGameController] Could not find the player's match result in the match results array. Player UID: {authManager.UUID}");
                        return;
                    }

                    var isPlayerWinner = playerMatchResult.Position is 1;

                    // If the player did not win, check if in a 2v2 match their teammate won
                    if (!isPlayerWinner && VsPlayerSelectedID is NumberPlayers.twoVsTwo)
                        isPlayerWinner = matchResults.FirstOrDefault(x => x.PlayerIndex is 3).Position is 1;

                    Debug.Log($"[ExtendedGameController] Player with id '{authManager.UUID}' has the final match result: " + playerMatchResult.Position);
                    DeterminePlayerResult((int)playerMatchResult.Position);
                }
            }
        }

        private UniTask OnFinishDrag()
        {
            // Once the tile is dropped, we need to update the game state
            _deckScript.ControlAllHands();

            return UniTask.CompletedTask;
        }

        public class UserProfile
        {
            public string UserId { get; private set; }
            public string Username { get; private set; }
            public Sprite ProfileIcon { get; private set; }
            public Sprite[] Achievements { get; private set; }
            public bool? IsTeamA { get; private set; }

            public UserProfile(string userId, string username, Sprite profileIcon = null, Sprite[] achievements = null, bool? isTeamA = null)
            {
                UserId = userId;
                Username = username;
                ProfileIcon = profileIcon;
                Achievements = achievements;
                IsTeamA = isTeamA;
            }
        }
    }
}
