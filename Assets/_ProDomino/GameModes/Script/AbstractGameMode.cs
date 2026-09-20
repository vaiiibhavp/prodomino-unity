using Cysharp.Threading.Tasks;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using HelperSharedLibrary;
using ProDomino.Authentication;
using ProDomino.GameSystem;
using ProDomino.Leaderboard;
using ProDomino.ReplaySystem;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Timba.Patterns;
using Timba.ProcGen;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;
using static ProDomino.GameModes.ExtendedGameController;
using Random = UnityEngine.Random;

namespace ProDomino.GameModes
{
    public abstract class AbstractGameMode : MonoBehaviour
    {
        // Game mode
        [SerializeField] protected GameMode gameModeID = GameMode.none;
        [SerializeField] protected CustomButtonUI hintToggle; 

        public GameMode GameModeID => gameModeID;
        protected GameType gameTypeSelectedID = GameType.none;
        public GameType GameTypeSelectedID => gameTypeSelectedID;
        protected NumberPlayers vsPlayerSelectedID = NumberPlayers.none;
        public NumberPlayers VSPlayerSelectedID => vsPlayerSelectedID;

        protected int difficulty = 0;
        protected bool isSinglePlayerVsIA = false;
        protected GameObject lobbyChatContainerObj;
        protected AuthManager authManager;
        protected GameManager gameManager;
        protected DictionaryService dictionaryService;
        protected LeaderboardManager leaderboardManager;
        protected Action openMenuSettingsAction;
        protected Func<bool> isTimeOut_MatchManager;

        /// <summary>
        /// This property tries to handle when the player is time-out in Network multiplayer matches<br></br>
        /// It is use to avoid put a tile when the player is timeout (yep, actually this is not controlled internally)
        /// </summary>
        public bool IsTimeout => isTimeOut_MatchManager?.Invoke() ?? false;

        protected ConcentrateNumberOfTiles concentrateNumberOfTiles = ConcentrateNumberOfTiles.none;
        public ConcentrateNumberOfTiles ConcentrateNumberOfTiles => concentrateNumberOfTiles;

        public GameObject LobbyChatContainerObj
        {
            get => lobbyChatContainerObj;
        }

        [SerializeField] protected ExtendedGameController extendedGameController;
        public ExtendedGameController ExtendedGameController => extendedGameController;

        // Number of tiles per duel type
        [SerializeField] protected int numberOfTiles = 0;
        public int NumberOfTiles => numberOfTiles;

        protected int limitPointsGameMode = 0;

        [SerializeField] protected List<int> tilesID_InShowBoard = new List<int>();

        [SerializeField] protected List<int> handOfPlayer_0 = new List<int>();
        [SerializeField] protected List<int> handOfPlayer_1 = new List<int>();
        [SerializeField] protected List<int> handOfPlayer_2 = new List<int>();
        [SerializeField] protected List<int> handOfPlayer_3 = new List<int>();

        [SerializeField] protected List<int> handOfLocalPlayer = new List<int>();
        public List<int> HandOfLocalPlayer
        {
            get => handOfLocalPlayer;
            set => handOfLocalPlayer = value;
        }

        [SerializeField] protected List<int> dominoTiles = new List<int>();
        [SerializeField] protected List<int> boneyardDominoTiles = new List<int>();

        private PostMatchResultController _postMatchResultController;
        public PostMatchResultController PostMatchResultController => _postMatchResultController = _postMatchResultController != null 
            ? _postMatchResultController 
            : FindFirstObjectByType<PostMatchResultController>();

        [SerializeField] protected CustomButtonUI chatButton;
        [SerializeField] protected CustomButtonUI backButton;
        [SerializeField] protected Image alertIcon;

        [SerializeField] protected PlayerDataInfo dataInfoPlayer_0;
        public PlayerDataInfo DataInfoPlayer_0 => dataInfoPlayer_0;
        [SerializeField] protected PlayerDataInfo dataInfoPlayer_1;
        public PlayerDataInfo DataInfoPlayer_1 => dataInfoPlayer_1;
        [SerializeField] protected PlayerDataInfo dataInfoPlayer_2;
        public PlayerDataInfo DataInfoPlayer_2 => dataInfoPlayer_2;
        [SerializeField] protected PlayerDataInfo dataInfoPlayer_3;
        public PlayerDataInfo DataInfoPlayer_3 => dataInfoPlayer_3;

        [SerializeField] protected TMP_Text boneyardCountText = null;
        
        public bool IsForcingShowTiles { get; private set; }

        protected GameModeConfig gameModeConfig;
        public GameModeConfig GameModeConfig => gameModeConfig;

        protected DominoAI dominoAI;
        public DominoAI DominoAI
        {
            get => dominoAI;
            set => dominoAI = value;
        }

        [SerializeField] protected bool isSelectedTileFromBoneyard = false;
        public bool IsSelectedTileFromBoneyard => isSelectedTileFromBoneyard;

        private RectTransformPanZoomController rectTransformPanZoomController;
        private RectTransformPanZoomController RectTransformPanZoomController => rectTransformPanZoomController = rectTransformPanZoomController != null 
            ? rectTransformPanZoomController 
            : FindFirstObjectByType<RectTransformPanZoomController>();

        public bool IsBlocked => RectTransformPanZoomController ? RectTransformPanZoomController.IsBlocked : false;

        private Action<int> onPlayerTakesFromBoneyard = null;
        public Action<int> OnPlayerTakesFromBoneyard
        {
            get => onPlayerTakesFromBoneyard;
            set => onPlayerTakesFromBoneyard = value;
        }

        protected Action<bool> onPassTurnHostAction = null;
        public Action<bool> OnPassTurnHostAction
        {
            get => onPassTurnHostAction;
            set => onPassTurnHostAction = value;
        }

        protected Action onRestartGameModeRoundHostAction = null;
        public Action OnRestartGameModeRoundHostAction
        {
            get => onRestartGameModeRoundHostAction;
            set => onRestartGameModeRoundHostAction = value;
        }

        protected Action onRematchGameModeHostAction = null;
        public Action OnRematchGameModeHostAction
        {
            get => onRematchGameModeHostAction;
            set => onRematchGameModeHostAction = value;
        }

        protected Func<float> onGetRemainingTime = null;
        protected float maxTimePerTurn = 0f;
        protected Func<StatusTimerInHost> onGetStatusTimerInHost = null;
        protected Coroutine _waitForPlayerPassCoroutine;
        [SerializeField] protected int numberOfTilesPerPlayer = 7;
        [SerializeField] private float chatButtonFeedbackTime = .25f;

        private static UnityEvent onResetLobby;

        protected bool replayTurnWhereHandsDelivered;
        protected bool replayTurnWhereTakeFromBoneyard;
        protected bool replayIsInitialize = false;

        public float MaxTimePerTurn => maxTimePerTurn;
        public bool ReplayIsInitialize => replayIsInitialize;
        public bool ReplayTurnWhereHandsDelivered => replayTurnWhereHandsDelivered;

        /// <summary>
        /// Checks if the user is authenticated via Unity Gaming Services or any provider.
        /// </summary>
        internal bool IsAuthenticated => gameManager is { IsAuthenticated: true };

        #region Abstract Methods
        public abstract void SetupGameMode();

        public abstract void SetupRandomHands();

        public abstract void ValidateNumbersAvailableInBranches(ref int rightBranch, ref int leftBranch, ref int topNum, ref int downNum, ref List<int> doubleTilesEnabled, int firstPlacedTileId, Domino tileInfo = null);

        public abstract int SelectPlayerWhoWillTakeFirstTurn();

        // Starts the first turn
        public abstract void StartTurn(int playerTurnID, int localPlayerID);

        public virtual async void StartTurn_Bot
            (int currentClientID,
            int localClientID,
            bool useRandomTile,
            bool useRandomTimeToWait,
            Action<int, int> onBoneyardTileTaken,
            Action<bool> onBoneyardEmpty)
        {
            if (currentClientID == localClientID)
                extendedGameController.TurnScript.SetTurns(true, false, false, false); // Player, Left, Top, Right
            else
            {
                if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    extendedGameController.TurnScript.SetTurns(false, false, true, false); // Player, Left, Top, Right
                    extendedGameController.UpdatePlayerTurnExternally(3); // is Top Player
                }
                else
                {
                    // In a 4-player match, calculate relative position from the local player
                    // 0 => Bottom (already handled above)
                    // 1 => Left
                    // 2 => Top
                    // 3 => Right
                    int relativePosition = (currentClientID - localClientID + 4) % 4;

                    bool isLeft = relativePosition == 1;
                    bool isTop = relativePosition == 2;
                    bool isRight = relativePosition == 3;

                    extendedGameController.TurnScript.SetTurns(false, isLeft, isTop, isRight); // Bottom, Left, Top, Right

                    var turnIndex = default(int?);
                    if (isLeft) turnIndex = 2; // is Left Player
                    else if (isTop) turnIndex = 3; // is Top Player
                    else if (isRight) turnIndex = 4; // is Right Player

                    if (turnIndex.HasValue)
                        extendedGameController.UpdatePlayerTurnExternally(turnIndex.Value);
                    else
                        Debug.LogWarning("Couldn't get the turn index properly");
                }
            }

            var auxHand = default(List<int>);

            // If the localclient is the host, get the hand of the current bot player that the host manages
            if (localClientID is 0)
                auxHand = GetHandOfPlayerWithID(currentClientID);

            // Else, get the hand of the local player (the bot will play with the same hand as the local player)
            else
                auxHand = HandOfLocalPlayer;

            var botHand = ConvertIdTilesListToDominoList(auxHand);
            ExtendedGameController.HandController.LockAllTiles();
            ExtendedGameController.HandController.SetLockPlayerButtons(false);
            ExtendedGameController.DeckController.ControlAllHands();

            // Wait until the new pan and zoom alteration is done
            ExtendedGameController.SetPanAndZoomControllerInteractivity(false);
            await UniTask.WaitForSeconds(1, true);

            var isFirstTurn = ExtendedGameController.TurnScript.TurnCount is 0;
            if (isFirstTurn)
            {
                var getFirstMoveFunc = (Func<List<Domino>, Domino[]>)(gameModeID switch
                {
                    GameMode.french => DominoAI.GetFirstTurnAvailableTilesFrench_CustomHand,
                    GameMode.block => DominoAI.GetFirstTurnAvailableTilesBlock_CustomHand,
                    GameMode.draw => DominoAI.GetFirstTurnAvailableTilesDraw_CustomHand,
                    GameMode.five => DominoAI.GetFirstTurnAvailableTilesFive_CustomHand,
                    _ => throw new NotImplementedException($"There is not implementation thought about this game mode {gameModeID}")
                });

                var startingAvailablesMoves = getFirstMoveFunc?.Invoke(botHand);
                botHand = startingAvailablesMoves?.ToList();
            }
            Debug.Log($"Bot random hand: {string.Join(", ", botHand.Select(x => $"{x.TopIndex}|{x.BottomIndex}"))}");

            // Check if in this turn the bot will play the best move or a random tile from its playable hand
            // if the current turn is the first one, it will always play the best move
            var currentPlay = default(Domino);
            if (!useRandomTile || isFirstTurn)
            { 
                Debug.Log("Bot is playing the best move.");
                var bestMoveFunc = (Func<List<Domino>, Domino>)(gameModeID switch
                {
                    GameMode.french => DominoAI.GetBestMoveFrench_CustomHand,
                    GameMode.block => DominoAI.GetBestMoveBlock_CustomHand,
                    GameMode.draw => DominoAI.GetBestMoveDraw_CustomHand,
                    GameMode.five => DominoAI.GetBestMoveFive_CustomHand,
                    GameMode.concentrate => DominoAI.GetBestMoveConcentrate_CustomHand,
                    _ => throw new NotImplementedException($"There is not implementation thought about this game mode {gameModeID}")
                });

                // Check if the move selected from server has value
                currentPlay = bestMoveFunc?.Invoke(botHand);
            }
            else
            {
                Debug.Log("Bot is playing a random tile from its playable hand.");

                // Get the list of playable dominos
                var playableHand = DominoAI.GetPlayableDominos_SpecificRules(botHand);
                Debug.Log($"Bot random playable hand: {string.Join(", ", playableHand.Select(x => $"{x.TopIndex}|{x.BottomIndex}"))}");

                // If there is playable dominos, select one randomly
                if (playableHand is not null and { Count: > 0 })
                {
                    var randomIndex = Random.Range(0, playableHand.Count);
                    currentPlay = playableHand.ElementAtOrDefault(randomIndex);
                    
                    Debug.Log($"Bot selected random playable tile: {currentPlay.id} [{currentPlay.TopIndex}|{currentPlay.BottomIndex}]");
                }
            }

            if (currentPlay is not null)
            {
                Debug.Log($"Bot playing tile: {currentPlay.id} [{currentPlay.TopIndex}|{currentPlay.BottomIndex}]");
                ExtendedGameController.DeckController.Deck_HandleHasValidMoves?.Invoke();

                var currentTurnControl = ExtendedGameController.GameTurnController.GetCurrentTurnControl();
                var botHandDragHandlers = ExtendedGameController.DeckController.GetList(currentTurnControl);

                // Get the draghandler according the index of the handKvp (to be more realistic)
                // Note: the drag handler to get is empty (configure a 0|0). This is to avoid register bot data starting the game
                var currentPlayHandIndex = botHand.IndexOf(currentPlay);
                var currentPlayDragHandler =
                    botHandDragHandlers.ElementAtOrDefault(currentPlayHandIndex) // Try to get by index first
                    ?? botHandDragHandlers.FirstOrDefault(x => x.GetDominoView()?.GetDomino()?.id == -1); // Else, get the first empty drag handler (if any)

                // If found, play the tile using the drag handler
                if (currentPlayDragHandler != null)
                {
                    Debug.Log($"Bot DragHandler Tile ID: {currentPlayDragHandler.GetDominoView()?.GetDomino()?.id}");

                    // Wait to simulate human behaviour
                    if (useRandomTimeToWait)
                    {
                        var randomTimeToWait = botHand.Count > 1 ? Random.Range(1f, 3f) : 1.5f;
                        await UniTask.WaitForSeconds(randomTimeToWait, true);
                    }

                    var tempDominoView = currentPlayDragHandler.GetDominoView();
                    tempDominoView.SetDomino
                        (ExtendedGameController.DeckController.SetupDominoInfo(currentPlay.id),
                        ExtendedGameController.DeckController.SpriteArray[currentPlay.id],
                        ExtendedGameController.DeckController.BackTile);

                    ExtendedGameController.currentDrag = currentPlayDragHandler.GetDominoView();
                    await ExtendedGameController.CreateSlot();
                    ExtendedGameController.currentDrag = null;

                    (RectTransform rightSlot, RectTransform leftSlot, RectTransform topSlot, RectTransform downSlot) = (default, default, default, default);
                    ExtendedGameController.GetSlots(ref rightSlot, ref leftSlot, ref topSlot, ref downSlot);

                    // Get the slot where the drag handler could displace
                    var validSlots = new List<RectTransform>
                    {
                        rightSlot,
                        leftSlot,
                        topSlot,
                        downSlot
                    }
                    ?.Where(x => x != null)
                    ?.ToList();

                    // If there is slot to displace, move the tile to anyone of them
                    if (validSlots is not null and { Count: > 0 })
                    {
                        Debug.Log($"Bot valid slots count: {validSlots.Count}");

                        var randomSlot = Random.Range(0, validSlots.Count);
                        var targetSlot = validSlots.ElementAtOrDefault(randomSlot);

                        Debug.LogWarning("Bot SlotPos: " + targetSlot.position);

                        StartCoroutine(currentPlayDragHandler.LerpMove(targetSlot, isAI: true, notifyToHost: true, onFinishDrag: OnFinishDrag));

                        // The movement is already set, and the next steps are called automatically due the notifyToHost arg.
                        return;
                    } 
                    else
                        Debug.LogWarning("Bot couldn't find a valid slot to play the tile.");
                }
                else
                    Debug.LogError("Bot couldn't find the DragHandler for the selected tile to play.");
            }

            // Try to get a tile from the boneyard checking if there is enough tiles in boneyard
            // If the gamemode is Block, omit the use of the boneyard
            else if (ExtendedGameController.GameMode is not GameMode.block && GetBoneyardManager().GetBoneyardTilesCount() > 0)
            { 
                Debug.Log("Bot is taking a tile from the boneyard.");

                // If the local client is the host, call the method to take a tile from the boneyard for the current bot player and then inform the clients
                if (localClientID is 0)
                { 
                    Debug.Log("Bot is taking a tile from the boneyard (host).");
                    BotSendBoneyardTileToHand_FromClient(currentClientID, localClientID, OnBoneyardTileTaken);
                } 
                else
                {
                    Debug.Log($"Bot is taking a tile from the boneyard (client)");

                    // Get a random tile index from the boneyard without removing it (the host will remove it when informing the clients)
                    // The host will be the one who plays the tile from the boneyard to the bot hand because is the only one who manages the boneyard tiles
                    var randomBoneyardIndex = GetBoneyardManager().GetRandomBoneyardTileIndex();
                    if (!randomBoneyardIndex.HasValue)
                    {
                        Debug.LogError("Bot couldn't get a random tile index from the boneyard.");
                        OnBoneyardEmpty();
                        return;
                    }

                    // Inform the host that the bot is taking a tile from the boneyard
                    OnBoneyardTileTaken(randomBoneyardIndex.Value);
                }
            }

            // Else, pass turn
            else
                OnBoneyardEmpty();

            void OnBoneyardTileTaken(int tileTakenIndex)
            {
                Debug.Log($"OnBoneyardTileTaken: selected a tile with boneyard index: {tileTakenIndex}");

                // Calls the registered callback to inform who tile was taken from the boneyard
                onBoneyardTileTaken?.Invoke(currentClientID, tileTakenIndex);

                OnFinish();
            }

            void OnBoneyardEmpty()
            {
                Debug.Log("OnBoneyardEmpty: boneyard is empty.");

                // Turn off the boneyard UI
                BoneyardIsEmpy(false);

                // Calls the registered callback to inform to the host that the boneyard is empty
                onBoneyardEmpty?.Invoke(true);
                OnFinish();
            }

            UniTask OnFinishDrag()
            {
                Debug.Log("Bot finished dragging the tile.");
                
                // Once the tile is dropped, we need to update the game state
                ExtendedGameController.DeckController.ControlAllHands();

                OnFinish();
                return UniTask.CompletedTask;
            }

            void OnFinish()
            {
                Debug.Log("Bot turn finished.");
                ExtendedGameController.SetPanAndZoomControllerInteractivity(true);
            }
        }

        public abstract void StartNextTurn();

        public abstract void EndTurn();

        // What to do if a player has no valid moves
        public abstract bool HandleNoValidMoves(bool isPlayerTurn);

        // What to do if a player has valid moves
        public abstract void HandleHasValidMoves();

        // Calculates the current score
        public abstract int CalculateScore(Player player);

        // Determines the winner of the round
        public abstract Player DetermineRoundWinner();

        // Determines the winner of the full game
        public abstract Player DetermineGameWinner();

        // Resets the game mode
        public abstract void ResetGameMode();

        public abstract int SelectTileFromBoneyard();

        public abstract BoneyardManager GetBoneyardManager();

        public abstract bool CheckRound_GameOver(ref string gameOverCase, bool gameIsBlocked = false);
      
        public abstract int PlayerAvalibleTilesInGameMode(int playerID);

        public abstract void SendBoneyardTileToHand_FromHost(int currentTurnPlayerID, int localPlayerID, int indexSelected, Action callback, int tileID = -1);

        public abstract void BotSendBoneyardTileToHand_FromClient(int currentTurnPlayerID, int localPlayerID, Action<int> callback);

        public abstract void BoneyardIsEmpy(bool enablePassButton, Action<bool> callback = null);

        public abstract void PassTurnBtn();

        public void ShowHandTilesOnEndRound(int localClientID, Dictionary<int, List<int>> clientsHands)
        {
            if (clientsHands is null or { Count: 0 })
            { 
                Debug.LogWarning("No hands data to show on end round.");
                return;
            }

            extendedGameController.ExpandHandViewport(true);

            foreach (var handKvp in clientsHands)
            {
                if (handKvp.Key == localClientID)
                    continue;

                if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    extendedGameController.TurnScript.SetTurns(false, false, true, false); // Player, Left, Top, Right
                    extendedGameController.UpdatePlayerTurnExternally(3); // is Top Player
                    ShowTiles(handKvp.Value, 3);
                } 
                else
                {
                    // In a 4-player match, calculate relative position from the local player
                    // 0 => Bottom (already handled above)
                    // 1 => Left
                    // 2 => Top
                    // 3 => Right
                    int relativePosition = ((int)handKvp.Key - localClientID + 4) % 4;

                    bool isLeft = relativePosition == 1;
                    bool isTop = relativePosition == 2;
                    bool isRight = relativePosition == 3;

                    var listNum = default(int?);
                    if (isLeft) listNum = 2; // is Left Player
                    else if (isTop) listNum = 3; // is Top Player
                    else if (isRight) listNum = 4; // is Right Player

                    if (listNum.HasValue)
                        ShowTiles(handKvp.Value, listNum.Value);
                    else
                        Debug.LogWarning($"Couldn't show hand of client with id {handKvp.Key}");
                }
            }

            void ShowTiles(List<int> handIDsToShow, int listNum)
            {
                var handDragHandlers = ExtendedGameController.DeckController.GetList(listNum);


                for (var i = 0; i < handDragHandlers.Count; i++)
                {
                    var dragHandler = handDragHandlers.ElementAtOrDefault(i);
                    var tileToAssign = handIDsToShow.ElementAtOrDefault(i);
                    var isExceeding = i >= handIDsToShow.Count;
                    
                    if (dragHandler != null && !isExceeding)
                    {
                        var tempDominoView = dragHandler.GetDominoView();
                        tempDominoView.SetDomino
                            (ExtendedGameController.DeckController.SetupDominoInfo(tileToAssign),
                            ExtendedGameController.DeckController.SpriteArray[tileToAssign],
                            ExtendedGameController.DeckController.BackTile);

                        tempDominoView.ChangeBackState(false);
                    }
                }
            }
        }

        public abstract void UpdateScoreAndShowRoundResult(string gameOverCase, int scorePlayer_0, int scorePlayer_1, int scorePlayer_2, int scorePlayer_3, int playerID, bool auxGameIsCompleteAndFinished, string auxPlayerWinner);

        #endregion

        #region Virtual Methods

        protected virtual void Awake()
        {
            onResetLobby ??= new();
            dominoAI = new
                (gameMode: this,
                gameTurnController: ExtendedGameController.GameTurnController,
                slotHelper: ExtendedGameController.SlotPosScript,
                deckController: ExtendedGameController.DeckController);

            authManager = ServiceLocator.Instance.GetService<AuthManager>();
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
            dictionaryService = ServiceLocator.Instance.GetService<DictionaryService>();
            leaderboardManager = ServiceLocator.Instance.GetService<LeaderboardManager>();
        }

        protected virtual void Start()
        {
            extendedGameController.DeckScript.Deck_HandleNoValidMovesInGameMode = HandleNoValidMoves;
            extendedGameController.DeckScript.Deck_HandleHasValidMoves = HandleHasValidMoves;

            extendedGameController.DeckScript.DeckCheckRoundGameOver = CheckRound_GameOver;
            extendedGameController.GameController_SetTileIDUsedInShowBoard = SetTileIdUsedInShowBoard;
            extendedGameController.SlotPosScript.SlotHelper_validateNumbersAvailableInBranches = ValidateNumbersAvailableInBranches;
            extendedGameController.OnPassBtn = PassTurnBtn;

            extendedGameController.HandController.Hand_playerChangesTurn = PlayerChangesTurn;

            // Add listeners to the post-match result controller buttons
            PostMatchResultController?.AddListeners(
                onNextRound: RestartGameModeRound,
                onToLobby: ToLobbyWithOutGiveUp,
                onRematch: RematchGameMode,
                onSaveReview: SaveReplayGame
            );

            // The hint toggle button is optional, so we check if it is assigned before adding the listener
            if (hintToggle)
                hintToggle.AddEventToListener(OnHintButtonPressed);
        }

        protected virtual void Update()
        {
            if (!RectTransformPanZoomController)
                return;

            if (chatButton)
                chatButton.SetButtonInteractable(!IsBlocked);

            if (hintToggle && ExtendedGameController is not null and { TurnScript: not null })
                hintToggle.SetButtonInteractable(ExtendedGameController.TurnScript.GetPlayerTurn());
        }

        private void OnDestroy()
        {
            PostMatchResultController?.RemoveListeners(
                onNextRound: RestartGameModeRound, 
                onToLobby: ToLobbyWithOutGiveUp, 
                onRematch: RematchGameMode,
                onSaveReview: SaveReplayGame
            );

            if (LobbyChatContainerObj)
            {
                LobbyChatContainerObj.SetActive(false);
                onResetLobby?.Invoke();
            }
        }

        protected virtual void RematchGameMode()
        {
            if (isSinglePlayerVsIA)
            {
                RestartGame();
                SetupRandomHands();
            }
            else
            {
                PostMatchResultController?.SetVisibility(false);
                onRematchGameModeHostAction?.Invoke();
            }
        }

        protected virtual void ToLobbyWithOutGiveUp()
        {
            PostMatchResultController.SetVisibility(false);

            if (GameModeConfig != null && GameModeConfig.MenuControllerGameMode != null)
                GameModeConfig.MenuControllerGameMode.ToLobbyWithoutGiveUp();
            else
                Debug.LogWarning("GameModeConfig or MenuControllerGameMode is not assigned.");
        }

        protected virtual void RestartGameModeRound()
        {
            RestartGame(restartNewRound: true);

            if (isSinglePlayerVsIA)
            {
                SetupRandomHands();
            }
            else
            {
                onRestartGameModeRoundHostAction?.Invoke();
            }
        }

        /// <summary>
        /// Initializes the game mode settings, including configuration, difficulty, game type, player mode, AI
        /// settings, and related actions.
        /// </summary>
        /// <param name="aux_gameModeConfig">Game mode configuration to apply.</param>
        /// <param name="aux_Difficulty">Difficulty level for the game.</param>
        /// <param name="aux_GameTypeID">Selected game type identifier.</param>
        /// <param name="aux_VsPlayerID">Selected versus player identifier.</param>
        /// <param name="isSinglePlayerIA">Indicates if the mode is single player versus AI.</param>
        /// <param name="auxConcentrateNumberOfTiles">Number of tiles for concentrate mode.</param>
        /// <param name="openMenuSettingsAction">Action to open the menu settings.</param>
        /// <param name="isTimeOut_MatchManager">Function to determine if the match manager has timed out.</param>
        public virtual void InitializeGameMode
            (GameModeConfig aux_gameModeConfig,
            int aux_Difficulty,
            GameType aux_GameTypeID,
            NumberPlayers aux_VsPlayerID,
            bool isSinglePlayerIA,
            ConcentrateNumberOfTiles auxConcentrateNumberOfTiles,
            Action openMenuSettingsAction,
            Func<bool> isTimeOut_MatchManager)
        {
            gameModeConfig = aux_gameModeConfig;
            difficulty = aux_Difficulty;
            concentrateNumberOfTiles = auxConcentrateNumberOfTiles;
            gameTypeSelectedID = aux_GameTypeID;
            vsPlayerSelectedID = aux_VsPlayerID;
            isSinglePlayerVsIA = isSinglePlayerIA;
            this.openMenuSettingsAction = openMenuSettingsAction;
            this.isTimeOut_MatchManager = isTimeOut_MatchManager;

            // The hint toggle button is only interactable in single player vs IA mode, so we check if it is assigned before setting its state
            if (hintToggle)
                hintToggle.SetButtonActive(isSinglePlayerVsIA && GameModeID is not GameMode.concentrate);

            // Check for hard difficulty to change the AI knowledge profile. In this case, the difficulty is on medium level.
            if (difficulty is 1)
                dominoAI.SetKnowledgeProfile(DominoAI.IAKnowledgeProfile.Realistic);

            // And, in this case, the difficult is on hard level.
            else if (difficulty is 2)
                dominoAI.SetKnowledgeProfile(DominoAI.IAKnowledgeProfile.Undertaker);

            tilesID_InShowBoard.Clear();
        }

        /// <summary>
        /// Indicates when the player accomplishes the condition to win the game mode. It can be used to trigger specific animations, effects, sounds, etc. when the player wins.
        /// </summary>
        public abstract bool RoundShouldBeStopped();

        /// <summary>
        /// Assigns a callback to be invoked when a player movement starts.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player movement event occurs.</param>
        public virtual void SetOnStartPlayerMovementEvent(Action<int, string> callback)
        {
            if (callback != null)
                extendedGameController.OnStartPlayerMovementEvent = callback;
            else
                Debug.Log("Player movement event callback is set.");
        }
        
        
        /// <summary>
        /// Assigns a callback to handle player movement events.
        /// </summary>
        /// <param name="callback">The callback to invoke when a player movement event occurs.</param>
        public virtual void SetPlayerMovementEvent(Action<int, string> callback)
        {
            if (callback != null)
                extendedGameController.PlayerMovementEvent = callback;
            else
                Debug.Log("Player movement event callback is set.");
        }

        /// <summary>
        /// Assigns a callback to be invoked when the local player finishes the game.
        /// </summary>
        /// <param name="callback">The action to execute with the player's ID when the game is finished.</param>
        public virtual void SetPlayerFinishGameEvent(Action<int> callback)
        {
            if (callback != null)
                extendedGameController.LocalPlayerFinishGameEvent = callback;
            else
                Debug.Log("Player finish game event callback is set.");
        }

        /// <summary>
        /// Sets the local player's hand and updates elimination states for all players, then assigns the hand to the
        /// game controller with optional callbacks.
        /// </summary>
        /// <param name="playerHand">The list of tile IDs representing the player's hand.</param>
        /// <param name="clientId">The client ID of the local player.</param>
        /// <param name="playerEliminatedState">Indicates whether the local player is eliminated.</param>
        /// <param name="leftEliminatedState">Indicates whether the left player is eliminated.</param>
        /// <param name="topEliminatedState">Indicates whether the top player is eliminated.</param>
        /// <param name="rightEliminatedState">Indicates whether the right player is eliminated.</param>
        /// <param name="callback">Callback invoked after the hand is set.</param>
        /// <param name="updateBoneyardText">Optional callback to update the boneyard text.</param>
        public virtual void SetLocalPlayerHand
            (List<int> playerHand,
            int clientId,
            bool playerEliminatedState,
            bool leftEliminatedState,
            bool topEliminatedState,
            bool rightEliminatedState,
            Action<int> callback,
            Action updateBoneyardText = null)
        {
            if (vsPlayerSelectedID == NumberPlayers.oneVsThree || vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                switch (clientId)
                {
                    case 0:
                        extendedGameController.TurnScript._playerIsEliminated = playerEliminatedState; //id 0
                        extendedGameController.TurnScript._leftAIIsEliminated = leftEliminatedState; //id 1
                        extendedGameController.TurnScript._topAIIsEliminated = topEliminatedState; //id 2
                        extendedGameController.TurnScript._rightAIIsEliminated = rightEliminatedState;  //id 3
                        break;
                    case 1:
                        extendedGameController.TurnScript._playerIsEliminated = leftEliminatedState; //id 1
                        extendedGameController.TurnScript._leftAIIsEliminated = topEliminatedState; //id 2
                        extendedGameController.TurnScript._topAIIsEliminated = rightEliminatedState;  //id 3
                        extendedGameController.TurnScript._rightAIIsEliminated = playerEliminatedState; //id 0
                        break;
                    case 2:
                        extendedGameController.TurnScript._playerIsEliminated = topEliminatedState; //id 2
                        extendedGameController.TurnScript._leftAIIsEliminated = rightEliminatedState;  //id 3
                        extendedGameController.TurnScript._topAIIsEliminated = playerEliminatedState; //id 0
                        extendedGameController.TurnScript._rightAIIsEliminated = leftEliminatedState; //id 1
                        break;
                    case 3:
                        extendedGameController.TurnScript._playerIsEliminated = rightEliminatedState;  //id 3
                        extendedGameController.TurnScript._leftAIIsEliminated = playerEliminatedState; //id 0
                        extendedGameController.TurnScript._topAIIsEliminated = leftEliminatedState; //id 1
                        extendedGameController.TurnScript._rightAIIsEliminated = topEliminatedState; //id 2
                        break;
                    default:
                        Debug.Log("Invalid player id");
                        break;
                }
            }

            handOfLocalPlayer = playerHand;
            extendedGameController.SetPlayerHand(new List<int>(playerHand), clientId, OnAssignTileToHand, callback, updateBoneyardText);

            void OnAssignTileToHand(DragHandler dragHandler, int side)
            {
                // TODO: fix to implement
                //if (dragHandler == null)
                //    return;

                //var playerDataInfo = GetPlayersDataInfo_fromHost().FirstOrDefault(x => x.currentClientID == side);
                //var view = dragHandler.GetDominoView();

                //if (view != null && extendedGameController?.DeckController is ExtendedDeckController extendedDeckController)
                //    extendedDeckController.OverrideTilesSkin(playerDataInfo.tileSkinID, view);
            }
        }

        public virtual void AddingTileToSpecificPlayerHand(int playerID, int tileID)
        {
            switch (playerID)
            {
                case 0:
                    handOfPlayer_0.Add(tileID);
                    Debug.Log($"Player with ID {playerID} hand: {string.Join(", ", handOfPlayer_0)}");
                    break;
                case 1:
                    handOfPlayer_1.Add(tileID);
                    Debug.Log($"Player with ID {playerID} hand: {string.Join(", ", handOfPlayer_1)}");
                    break;
                case 2:
                    handOfPlayer_2.Add(tileID);
                    Debug.Log($"Player with ID {playerID} hand: {string.Join(", ", handOfPlayer_2)}");
                    break;
                case 3:
                    handOfPlayer_3.Add(tileID);
                    Debug.Log($"Player with ID {playerID} hand: {string.Join(", ", handOfPlayer_3)}");
                    break;
                default:
                    Debug.LogWarning("Class AbstractGameMode Invalid player ID");
                    break;
            }
        }

        public virtual void RestartGame(bool restartNewRound = false)
        {
            if (gameTypeSelectedID != GameType.none && vsPlayerSelectedID != NumberPlayers.none)
            {
                Debug.Log("//** restartNewRound: " + restartNewRound);

                replayTurnWhereTakeFromBoneyard = false;
                
                if (!restartNewRound)
                {
                    replayIsInitialize = false;
                    
                    ExtendedGameController.TurnScript.PlayerScore = 0;
                    ExtendedGameController.TurnScript.LeftAIScore = 0;
                    ExtendedGameController.TurnScript.TopAIScore = 0;
                    ExtendedGameController.TurnScript.RightAIScore = 0;

                    ExtendedGameController.UpdateScoreUI();
                }

                backButton?.SetButtonInteractable(false);
                extendedGameController.TileSortingBtn?.SetButtonInteractable(false);

                // Restart the game with the selected parameters
                tilesID_InShowBoard.Clear();
                IsForcingShowTiles = false;
                PostMatchResultController?.SetVisibility(false);
                extendedGameController.RestartGame(difficulty, gameModeID, gameTypeSelectedID, vsPlayerSelectedID, restartNewRound);

                /*if(!restartNewRound)
                {
                    InitReplayData();   
                }*/
            }
            else
            {
                Debug.LogWarning("Game type or player count not set. Cannot restart game.");
            }
        }

        /// <summary>
        /// Updates the player turn based on external input, mapping client IDs to turn indices.
        /// </summary>
        public virtual void UpdatePlayerTurnExternally(int currentClientID, int localClientID)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> UpdatePlayerTurnExternally: currentClientID={currentClientID}, localClientID={localClientID}</color>");

            var turnIndex = default(int?);
            if (currentClientID == localClientID)
                turnIndex = 1;
            else
            {
                if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                    turnIndex = 3;
                
                else
                {
                    // In a 4-player match, calculate relative position from the local player
                    // 0 => Bottom (already handled above)
                    // 1 => Left
                    // 2 => Top
                    // 3 => Right
                    var relativePosition = (currentClientID - localClientID + 4) % 4;

                    bool isLeft = relativePosition == 1;
                    bool isTop = relativePosition == 2;
                    bool isRight = relativePosition == 3;

                    turnIndex = default(int?);
                    if (isLeft) turnIndex = 2; // is Left Player
                    else if (isTop) turnIndex = 3; // is Top Player
                    else if (isRight) turnIndex = 4; // is Right Player

                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> UpdatePlayerTurnExternally: turnIndex={turnIndex}, relativePosition={relativePosition}, isLeft={isLeft}, isTop={isTop}, isRight={isRight}</color>");
                }
            }

            // If the turn index was determined, update the player turn
            if (turnIndex.HasValue)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> UpdatePlayerTurnExternally: Updating turn to index {turnIndex.Value}</color>");
                extendedGameController.UpdatePlayerTurnExternally(turnIndex.Value);
            }
            
            else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(AbstractGameMode)}]</b> UpdatePlayerTurnExternally: Couldn't get the turn index properly for client ID {currentClientID} and local client ID {localClientID}</color>");
        }

        /// <summary>
        /// Responsible for starting the timebar for the current player.
        /// </summary>
        public void StartTimeBarEventExternally(int targetClientId, int localClientID)
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> StartTimeBarEventExternally: targetClientId={targetClientId}, localClientID={localClientID}</color>");

            var turnIndex = default(int?);
            if (targetClientId == localClientID)
                turnIndex = 1;
            else
            {
                if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                    turnIndex = 3;

                else
                {
                    // In a 4-player match, calculate relative position from the local player
                    // 0 => Bottom (already handled above)
                    // 1 => Left
                    // 2 => Top
                    // 3 => Right
                    var relativePosition = (targetClientId - localClientID + 4) % 4;

                    bool isLeft = relativePosition == 1;
                    bool isTop = relativePosition == 2;
                    bool isRight = relativePosition == 3;

                    turnIndex = default(int?);
                    if (isLeft) turnIndex = 2; // is Left Player
                    else if (isTop) turnIndex = 3; // is Top Player
                    else if (isRight) turnIndex = 4; // is Right Player

                    Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> StartTimeBarEventExternally: turnIndex={turnIndex}, relativePosition={relativePosition}, isLeft={isLeft}, isTop={isTop}, isRight={isRight}</color>");
                }
            }

            // If the turn index was determined, update the player turn
            if (turnIndex.HasValue)
            {
                Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> StartTimeBarEventExternally: Starting timebar for turn index {turnIndex.Value}</color>");

                /// Call a function that stops every score timebar
                ExtendedGameController.OnFinishTimer_Externally();

                // Call a function that starts the timebar for the current player
                ExtendedGameController.OnStartTimer_Externally(turnIndex.Value);
            } 
            else
                Debug.LogWarning($"<color={Consts.Colors.Error}><b>[{nameof(AbstractGameMode)}]</b> StartTimeBarEventExternally: Couldn't get the turn index properly for client ID {targetClientId} and local client ID {localClientID}</color>");
        }

        public virtual List<int> GetHandOfPlayerWithID(int playerIndex)
        {
            List<int> auxHand = new List<int>();

            switch (playerIndex)
            {
                case 0:
                    auxHand = new List<int>(handOfPlayer_0);
                    break;
                case 1:
                    auxHand = new List<int>(handOfPlayer_1);
                    break;
                case 2:
                    auxHand = new List<int>(handOfPlayer_2);
                    break;
                case 3:
                    auxHand = new List<int>(handOfPlayer_3);
                    break;
                default:
                    Debug.LogWarning("Class AbstractGameMode Invalid player ID");
                    break;
            }

            return auxHand;
        }

        public virtual List<int> GetBoneyardTiles()
        {
            return new List<int>(boneyardDominoTiles);
        }

        public virtual List<int> GetBoardTiles()
        {
            return new List<int>(tilesID_InShowBoard);
        }

        public virtual void UpdateTilePlacedByPlayer(int tileID, string sideInfo, int playerID, int localPlayerID, Action callback)
        {
            var numberOfPlayers = vsPlayerSelectedID == NumberPlayers.oneVsOne ? 2 : 4;

            // In a 4-player match, calculate relative position from the local player
            // 0 => Bottom (already handled above)
            // 1 => Left
            // 2 => Top
            // 3 => Right
            var relativePosition = default(int?);

            // Check the number of players to calculate the relative position
            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                relativePosition = playerID == localPlayerID ? 0 : 2; // Bottom or Top
            else
                relativePosition = (playerID - localPlayerID + 4) % 4;

            bool isBottom = (relativePosition == 0);
            bool isLeft = (relativePosition == 1);
            bool isTop = (relativePosition == 2);
            bool isRight = (relativePosition == 3);

            var auxPlayerPlacingTile = 
                isBottom ? "bottomPlayer" :
                isLeft ? "leftPlayer" :
                isTop ? "topPlayer" :
                isRight ? "rightPlayer" : "";

            StartCoroutine(ExtendedGameController.DropTilePlayerOnline(tileID, sideInfo, auxPlayerPlacingTile, callback));
        }

        /// <summary>
        /// Handles the hint button press by toggling the display of probabilities on the player's hand and updating
        /// tile colors or showing AI-calculated probabilities based on the current game type and mode.
        /// </summary>
        private void OnHintButtonPressed()
        {
            // If the game type is not single player vs IA or the game mode is concentrate, reset the color of the tiles in the player's hand to white and abort,
            // since the hint system is only implemented for single player vs IA game type and is not available for concentrate game mode
            if (GameTypeSelectedID is not GameType.singlePlayerIA 
                || GameModeID is GameMode.concentrate 
                || !ExtendedGameController.TurnScript.GetPlayerTurn())
            {
                // Iterate foreach tile in the player's hand to reset the color of the tiles to white
                var tiles = ExtendedGameController.DeckController.PlayerTiles
                    ?.Concat(ExtendedGameController.DeckController.GetList(-1))
                    ?.ToArray();

                if (tiles is not null and { Length: > 0 })
                    foreach (var tile in tiles)
                        tile.SetAnimatorEnabled(true);

                // Abort if the hint button is pressed when the game type is not single player vs IA, since the hint system is only implemented for that game type
                return;
            }

            // Get the corresponding debug action for the current game mode to show the probabilities of the tiles in the player's hand, if it exists. Otherwise, log a warning message.
            var gameModeDebugAction = GameModeID switch
            {
                GameMode.french => dominoAI.DebugFrenchAIScoring_Player,
                GameMode.block => dominoAI.DebugBlockAIScoring_Player,
                GameMode.draw => dominoAI.DebugDrawAIScoring_Player,
                GameMode.five => dominoAI.DebugFiveAIScoring_Player,
                _ => default(Action<Action<Dictionary<Domino, float>>>)
            };

            // Call the corresponding debug action for the current game mode to show the probabilities of the tiles in the player's hand, if it exists. Otherwise, log a warning message.
            if (gameModeDebugAction is not null)
                gameModeDebugAction(ShowPlayerBestMove);
             else
                Debug.LogWarning($"No debug action found for game mode {GameModeID}");
        }

        /// <summary>
        /// Displays probability information for each domino in the player's hand based on the provided probabilities.
        /// </summary>
        /// <param name="dominosProbabilities">A dictionary mapping each domino to its associated probability value.</param>
        protected virtual void ShowPlayerBestMove(Dictionary<Domino, float> dominosProbabilities)
        {
            var playerHand = ExtendedGameController.DeckController.PlayerTiles;

            // If there aren't tiles in hand, return
            if (playerHand is null or { Count: 0 })
            {
                Debug.LogWarning("There aren't any tile to check probability");
                return;
            }

            // Expand the hand viewport to show the probabilities better
            var bestMove = dominosProbabilities?.OrderByDescending(kvp => kvp.Value).FirstOrDefault().Key;

            // Find the tile in the player's hand that corresponds to the best move, if it exists
            var tileDragHandlerToHighlight = playerHand.FirstOrDefault(tile => tile.GetDominoView().GetDomino().id == bestMove?.id);

            // If the tile is found, show the selected tile feedback to highlight it. Otherwise, log a warning message.
            if (tileDragHandlerToHighlight)
            { 
                tileDragHandlerToHighlight.ShowSelectedTileFeedback(true, true);

                var dominoToHighlight = tileDragHandlerToHighlight.GetDominoView().GetDomino();
                var gameController = ExtendedGameController;
                var slotPrefab = gameController.SlotPrefab;

                gameController.SlotPosScript.CreateBestSlot(gameController, dominoToHighlight, slotPrefab, gameController.GameBoard);

                // If the RectTransformPanZoomController is assigned, update the tile bounds to ensure the best move slot is properly framed within the view.
                if (RectTransformPanZoomController)
                    RectTransformPanZoomController.UpdateTilesBounds(true, true);
            }
            else
                Debug.LogWarning("No tile found for the best move");
        }

        /// <summary>
        /// Displays AI tile probabilities in the Unity Editor or development builds by updating the visual state of AI
        /// tiles based on the provided probability data.
        /// </summary>
        /// <param name="dominosProbabilities">A dictionary mapping each AI domino tile to its associated probability value.</param>
        protected virtual void ShowAIProbabilities(Dictionary<Domino, float> dominosProbabilities)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var aiHand = ExtendedGameController.DeckController.TopAITiles
                .Concat(ExtendedGameController.DeckController.LeftAITiles)
                .Concat(ExtendedGameController.DeckController.RightAITiles)
                .ToList();

            // If there aren't tiles in hand, return
            if (aiHand is null or { Count: 0 })
            {
                Debug.LogWarning("There aren't any tile to check probability");
                return;
            }

            ExtendedGameController.ExpandHandViewport(true);

            // Show the value of each tile
            foreach (var tile in aiHand)
                tile.GetDominoView().ChangeBackState(false);

            // If there aren't tiles to check probabilities, reset the tiles in the hand
            if (dominosProbabilities is null or { Count: 0 })
            {
                foreach (var tile in aiHand)
                    ResetTileColor(tile);
                return;
            }

            // Show the probabilities of the tiles in the hand
            PaintHand(aiHand, dominosProbabilities);
#endif
        }

        /// <summary>
        /// Updates the visual feedback of each tile in the hand based on the provided domino probabilities.
        /// </summary>
        /// <param name="handToPaint">The list of DragHandler tiles representing the hand to update.</param>
        /// <param name="dominosProbabilities">A dictionary mapping Domino objects to their associated probability values.</param>
        protected void PaintHand(List<DragHandler> handToPaint, Dictionary<Domino, float> dominosProbabilities)
        {
            // Iterate foreach tile to show the feedback of the tile
            if (handToPaint is not null and { Count: > 0 })
            {
                // Reset colors of the tiles not included in the probabilities
                foreach (var tile in handToPaint)
                {

                    var (domino, probability) = dominosProbabilities.FirstOrDefault(d => d.Key.id == tile.GetDominoView().GetDomino().id);
                    if (domino != null)
                        foreach (var graphic in tile.GetComponentsInChildren<Graphic>(true))
                        {
                            graphic.color = Color.Lerp
                                (ColorUtility.TryParseHtmlString("#FFA500", out var color) ? color : Color.white,
                                Color.green,
                                probability);

                        }
                    else
                        ResetTileColor(tile);
                }
            }
        }

        /// <summary>
        /// Resets the color of all tiles in the player's hand to their default state.
        /// </summary>
        internal void ClearPlayerHandGlow()
        {
            // Iterate foreach tile in the player's hand to reset the color of the tiles to white
            var tiles = ExtendedGameController.DeckController.PlayerTiles
                ?.Concat(ExtendedGameController.DeckController.GetList(-1))
                ?.ToArray();

            // Iterate foreach tile to remove the glow effect 
            if (tiles is not null and { Length: > 0 })
                foreach (var tile in tiles)
                    tile.SetAnimatorEnabled(true);

            // Hide the best move slot if it's being shown
            if (extendedGameController is not null and {  SlotPosScript: not null })
                extendedGameController.SlotPosScript.DestroyBestMoveSlot();
        }

        /// <summary>
        /// Resets the color of all Graphic components in the specified tile and its children to white.
        /// </summary>
        /// <param name="tile">The DragHandler tile whose child Graphic components will have their color reset.</param>
        private void ResetTileColor(DragHandler tile)
        {
            if (tile == null)
                return;

            // Get all Graphic components in the tile and its children, and set their color to white
            foreach (var graphic in tile.GetComponentsInChildren<Graphic>(true))
                graphic.color = Color.white;
        }

        /// <summary>
        /// Reveals all AI tiles by showing their values and resetting their colors to white.
        /// </summary>
        public virtual void ForceShowTiles()
        {
            IsForcingShowTiles = true;

            if (ExtendedGameController is null or { DeckController: null})
            {
                Debug.LogWarning("ExtendedGameController or DeckController is null");
                return;
            }

            var aiHand = new List<DragHandler>();
            if (ExtendedGameController.DeckController.TopAITiles is not null)
                aiHand.AddRange(ExtendedGameController.DeckController.TopAITiles);

            if (ExtendedGameController.DeckController.LeftAITiles is not null)
                aiHand.AddRange(ExtendedGameController.DeckController.LeftAITiles);

            if (ExtendedGameController.DeckController.RightAITiles is not null)
                aiHand.AddRange(ExtendedGameController.DeckController.RightAITiles);

            // If there aren't tiles in hand, return
            if (aiHand is null or { Count: 0 })
            {
                Debug.LogWarning("There aren't any tile to check probability");
                return;
            }

            // Show the value of each tile
            foreach (var tile in aiHand)
            { 
                tile.GetDominoView().ChangeBackState(false);
                ResetTileColor(tile);
            }

            void ResetTileColor(DragHandler tile)
            {
                if (tile == null)
                    return;

                //tile.GetDominoView().ChangeBackState(true);
                foreach (var graphic in tile.GetComponentsInChildren<Graphic>(true))
                    graphic.color = Color.white;
            }
        }

        protected virtual void HideAIProbabilities(bool isResetingBoardTilesColor = false)
        {
            if (ExtendedGameController is null or { DeckController: null })
            {
                Debug.LogWarning("ExtendedGameController or DeckController is null");
                return;
            }

            ExtendedGameController.ExpandHandViewport(false);

            var allTiles = new List<DragHandler>();
            if (ExtendedGameController.DeckController.PlayerTiles is not null)
                allTiles.AddRange(ExtendedGameController.DeckController.PlayerTiles);
            
            if (ExtendedGameController.DeckController.TopAITiles is not null)
                allTiles.AddRange(ExtendedGameController.DeckController.TopAITiles);

            if (ExtendedGameController.DeckController.LeftAITiles is not null)
                allTiles.AddRange(ExtendedGameController.DeckController.LeftAITiles);

            if (ExtendedGameController.DeckController.RightAITiles is not null)
                allTiles.AddRange(ExtendedGameController.DeckController.RightAITiles);

            if (isResetingBoardTilesColor)
                allTiles.AddRange(ExtendedGameController.DeckController.GetList(-1)); // Add board tiles

            foreach (var tile in allTiles)
            {
                if (!ExtendedGameController.DeckController.PlayerTiles.Contains(tile)
                    && (!isResetingBoardTilesColor || !ExtendedGameController.DeckController.GetList(-1).Contains(tile)))
                    tile.GetDominoView().ChangeBackState(true);

                foreach (var graphic in tile.GetComponentsInChildren<Graphic>(true))
                    graphic.color = Color.white; // Reset color to white
            }
        }

        public virtual void SetTileIdUsedInShowBoard(int tileID, string sideInfo, Vector3 posInfo, Quaternion rotInfo, Vector2 sizeInfo)
        {
            isSelectedTileFromBoneyard = false; //Reset variable

            if (!tilesID_InShowBoard.Contains(tileID))
            {
                tilesID_InShowBoard.Add(tileID);

                if(gameModeID != GameMode.replay)
                {
                    int auxCurrentTurnPlayerIndexId = extendedGameController.TurnScript.GetCurrentTurnControl() - 1;
                    ReplayManager.Instance.SetTurnAction_Play(auxCurrentTurnPlayerIndexId, tileID, sideInfo, posInfo, rotInfo, sizeInfo); //SAVE PLAYER ACTION REPLAY
                }
            }
        }

        public virtual void RemoveTileFromUserHand_InHost(int playerID, int tileID)
        {
            switch (playerID)
            {
                case 0:
                    handOfPlayer_0.Remove(tileID);
                    break;
                case 1:
                    handOfPlayer_1.Remove(tileID);
                    break;
                case 2:
                    handOfPlayer_2.Remove(tileID);
                    break;
                case 3:
                    handOfPlayer_3.Remove(tileID);
                    break;
                default:
                    Debug.LogWarning($"<color{Consts.Colors.Error}>Class AbstractGameMode Invalid player ID</color>");
                    break;
            }
        }

        public virtual int GetTileFromBoneyard_InHost(int boneyardTileIndex)
        {
            Debug.Log($"Boneyard Tile Index: {boneyardTileIndex}\n\nBoneyard Tiles: {string.Join(", ", boneyardDominoTiles)}\n\nDomino Tiles: {string.Join(", ", dominoTiles)}");

            int auxTileID = boneyardDominoTiles[boneyardTileIndex];
            dominoTiles.Remove(auxTileID);

            return auxTileID;
        }

        public virtual int GetTotalTilesInBoneyard()
        {
            return dominoTiles.Count;
        }

        public void SetChatLobbyObj(GameObject chatLobbyObj)
        {
            lobbyChatContainerObj = chatLobbyObj;
            if (!lobbyChatContainerObj)
                throw new Exception("LobbyChatManager component not found in chatLobbyObj");

            if (backButton != null)
            {
                backButton.gameObject.SetActive(false);
            }

            chatButton.gameObject.SetActive(true);

            if (lobbyChatContainerObj != null)
            {
                chatButton.AddEventToListener(() =>
                {
                    if (RectTransformPanZoomController is null)
                    {
                        Debug.LogWarning("Zoom controller not found, couldn't check blocked state.");
                        return;
                    }

                    if (IsBlocked)
                        return;

                    alertIcon.enabled = false;

                    if (lobbyChatContainerObj)
                        lobbyChatContainerObj.SetActive(true);
                    else
                        Debug.LogError("LobbyChatContainerObj is null");
                });

                chatButton.SetButtonInteractableWithAlphaFull(true);

                Debug.Log("---c Chat lobby object set successfully.");
            }
        }

        /// <summary>
        /// Configure a new <see cref="PlayerDataInfo"/> depending if the client will be a user or a bot
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="isBot"></param>
        /// <param name="callback"></param>
        public virtual void GenerateNewPlayerDataInfo(int clientId, bool isBot, Action<PlayerDataInfo> callback)
        {
            // Check if AuthManager are initialized
            if (!isBot && authManager is null or { IsAlreadyInitialized: false })
            {
                Debug.LogError($"<color={Consts.Colors.Error}>AuthManager is not initialized</color>");
                return;
            }

            var leaderboardEntry = leaderboardManager?.GetCurrentClientLeaderboardEntry(LeaderboardManager.GetLeaderboardID(GameModeID, vsPlayerSelectedID));

            var playerDataInfo = default(PlayerDataInfo);
            if (isBot)
            {
                var random = new Func<int, int, int>((min, max) => UnityEngine.Random.Range(min, max));

                var tilesIDCollection = new List<string>()
                {
                    "tiles_orange",
                    "tiles_black",
                    "tiles_pink",
                    "tiles_rainbow",
                };

                var randomTileKey = tilesIDCollection.ElementAtOrDefault(random(0, tilesIDCollection.Count));

                var iconKey = CosmeticType.Icons.ToString();
                var boardKey = CosmeticType.Boards.ToString();
                var fundKey = CosmeticType.Fund.ToString();
                var tileCollectionKey = $"{CosmeticType.Tiles}_{randomTileKey}";
                var badgeKey = Consts.CollectionKeys.Achievements;

                var iconsCollection = dictionaryService.GetSpriteCollection(iconKey);
                var boardCollection = dictionaryService.GetSpriteCollection(boardKey);
                var fundCollection = dictionaryService.GetSpriteCollection(fundKey);
                var tileCollection = dictionaryService.GetSpriteCollection(tileCollectionKey);
                var badgeCollection = dictionaryService.GetSpriteCollection(badgeKey);

                var randomIcon = iconsCollection.ElementAtOrDefault(random(0, iconsCollection.Length));
                var randomBoard = iconsCollection.ElementAtOrDefault(random(0, boardCollection.Length));
                var randomFund = iconsCollection.ElementAtOrDefault(random(0, fundCollection.Length));
                var randomTile = tileCollection.ElementAtOrDefault(random(0, tileCollection.Length));

                var randomIconID = dictionaryService.GetSpriteID(iconKey, randomIcon);
                var randomBoardID = dictionaryService.GetSpriteID(iconKey, randomBoard);
                var randomFundID = dictionaryService.GetSpriteID(iconKey, randomFund);
                var randomTileID = dictionaryService.GetSpriteID(tileCollectionKey, randomTile);
                var randomBadges = new List<string>();

                int i = 0;
                int tries = 10;
                var badgesQuantity = random(1, 4);

                // Ensure we don't request more unique badges than exist
                if (badgesQuantity > badgeCollection.Length)
                    badgesQuantity = badgeCollection.Length;

                while (i < badgesQuantity && tries > 0)
                {
                    tries--;

                    // Pick a random badge safely
                    var randomBadge = badgeCollection[random(0, badgeCollection.Length)];
                    var randomBadgeID = dictionaryService.GetSpriteID(badgeKey, randomBadge);

                    // Add only if not already picked
                    if (!randomBadges.Contains(randomBadgeID)) // works if randomBadges is HashSet
                    {
                        randomBadges.Add(randomBadgeID);
                        i++;
                    }
                }

                // Create PlayerDataInfo with the local player's data
                playerDataInfo = new PlayerDataInfo
                (
                    clientId: clientId,
                    userId: RandomUtils.GenerateRandomNumber(random(8, 11)).ToString() ?? string.Empty,
                    username: DummyNameGenerator.GetRandomName() ?? string.Empty,
                    profileIconID: randomIconID ?? string.Empty,
                    tileSkinID: randomTileID ?? string.Empty,
                    boardSkinID: randomBoardID ?? string.Empty,
                    boardFundSkinID: randomFundID ?? string.Empty,
                    badgesIDs: randomBadges is not null and { Count: > 0 }
                        ? string.Join(',', randomBadges)
                        : string.Empty,
                    leaderboardTier: leaderboardEntry?.Tier ?? string.Empty,
                    leaderboardScore: (leaderboardEntry?.Score + random(-50, 50)) ?? 0,
                    isBot
                );
            }

            else
            {
                // Create PlayerDataInfo with the local player's data
                playerDataInfo = new PlayerDataInfo
                (
                    clientId: clientId,
                    userId: authManager?.UUID ?? string.Empty,
                    username: authManager?.Username ?? string.Empty,
                    profileIconID: gameManager.GetProfilePicture().id ?? string.Empty,
                    tileSkinID: gameManager?.PlayerProfileData?.tileSkinID ?? string.Empty,
                    boardSkinID: gameManager?.PlayerProfileData?.boardSkinID ?? string.Empty,
                    boardFundSkinID: gameManager?.PlayerProfileData?.boardFundSkinID ?? string.Empty,
                    badgesIDs: gameManager?.PlayerProfileData?.badgesIDs is not null and { Length: > 0 }
                        ? string.Join(',', gameManager.PlayerProfileData.badgesIDs)
                        : string.Empty,
                    leaderboardTier: leaderboardEntry?.Tier ?? string.Empty,
                    leaderboardScore: leaderboardEntry?.Score ?? 0,
                    isBot
                );
            }

            // Invoke the corresponding callback with the local player ID and PlayerDataInfo
            callback?.Invoke(playerDataInfo);
        }

        public virtual void SetTurnTimerData(Func<float> callbackGetRemainingTime, Func<StatusTimerInHost> callbackGetStatusTimerInHost, float auxMaxTimePerTurn)
        {
            onGetRemainingTime = callbackGetRemainingTime;
            onGetStatusTimerInHost = callbackGetStatusTimerInHost;
            maxTimePerTurn = auxMaxTimePerTurn;
        }

        void OnUpdate()
        {
            Debug.Log("+-+-+ MEcaca: ");
            /*bool auxIsTurnActive = onGetIsTurnActive != null && onGetIsTurnActive.Invoke();
            if (auxIsTurnActive)
            {
                Debug.Log("+-+-+ onGetRemainingTime.Invoke(): " + onGetRemainingTime.Invoke());
                //extendedGameController.UpdateTimerUI(onGetRemainingTime.Invoke(), maxTimePerTurn);
            }*/

            /*if (MatchManager.Instance == null || !MatchManager.Instance.Get_IsTurnActive() || !NetworkManager.Singleton.IsConnectedClient)
           return;

       //Debug.Log("+++---Updating turn timer UI...");

       float time = MatchManager.Instance.GetRemainingTime();
       int seconds = Mathf.CeilToInt(time);

       if (!timerText.gameObject.activeSelf)
       {
           timerText.gameObject.SetActive(true);
       }

       timerText.text = $"Remaining time for turn: {seconds}s";*/
        }

        /// <summary>
        /// From Host (server) get and register the correspondly entry of a new <see cref="PlayerDataInfo"/> 
        /// </summary>
        /// <param name="playerDataInfo"></param>
        public virtual void RegisterRemotePlayersDataInfo_fromHost(PlayerDataInfo playerDataInfo/*, int clientIndexId*/)
        {
            switch (playerDataInfo.clientId)
            //switch (clientIndexId)
            {
                case 0:
                    dataInfoPlayer_0 = playerDataInfo;
                    break;
                case 1:
                    dataInfoPlayer_1 = playerDataInfo;
                    break;
                case 2:
                    dataInfoPlayer_2 = playerDataInfo;
                    break;
                case 3:
                    dataInfoPlayer_3 = playerDataInfo;
                    break;
                default:
                    Debug.Log("Invalid player ID");
                    break;
            }

            // Register the new data as a recently player client
            if (IsAuthenticated && playerDataInfo.userId != authManager.UUID)
                gameManager.RefreshRecentlyPlayedDatas
                    (new PlayerRecentlyPlayedData
                        (playerDataInfo.userId, 
                        playerDataInfo.username, 
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
        }

        public virtual PlayerDataInfo GetPlayersDataInfo_fromHost(int playerHostIndex)
        {
            return playerHostIndex switch
            {
                0 => dataInfoPlayer_0,
                1 => dataInfoPlayer_1,
                2 => dataInfoPlayer_2,
                3 => dataInfoPlayer_3,
                _ => throw new NotImplementedException(),
            };
        }
        
        public virtual PlayerDataInfo[] GetPlayersDataInfo_fromHost()
        {
            return new PlayerDataInfo[]
            {
                dataInfoPlayer_0,
                dataInfoPlayer_1,
                dataInfoPlayer_2,
                dataInfoPlayer_3,
            };
        }
        
        public virtual PlayerDataInfo[] GetPlayersDatasInfo_fromHost()
        {
            return new PlayerDataInfo[4]
            {
                dataInfoPlayer_0,
                dataInfoPlayer_1,
                dataInfoPlayer_2,
                dataInfoPlayer_3,
            };
        }

        /// <summary>
        /// Helper method to print all player data info in the console for debugging purposes.
        /// </summary>
        public void PrintDataPlayers()
        {
            var playersDataInfolist = new List<PlayerDataInfo>
            {
                dataInfoPlayer_0,
                dataInfoPlayer_1,
                dataInfoPlayer_2,
                dataInfoPlayer_3
            };

            // Check if the list is null or empty
            if (playersDataInfolist is null || playersDataInfolist.Count == 0)
            {
                Debug.LogError($"<color={Consts.Colors.Error}><b>[{nameof(AbstractGameMode)}]</b> PrintDataPlayers: No player data info available to print.</color>");
                return;
            }

            // Build each player block as a string
            var playersInfo = string.Join(
                "\n",
                playersDataInfolist.Select((data, index) =>
                    $" Player {index}:\n" +
                    $" Client ID: {data.clientId}\n" +
                    $" User ID: {data.userId}\n" +
                    $" Username: {data.username}\n" +
                    $" Profile Icon ID: {data.profileIconID}"
                )
            );

            // MAke a log that prints all players info
            Debug.Log( $"<color={Consts.Colors.Process}><b>[{nameof(AbstractGameMode)}]</b> PrintDataPlayers: Player Data Info List:</color>\n\n{playersInfo}");
        }

        public virtual bool ValidateIfPlayerIsEliminated_InHost(int playerID)
        {
            if (vsPlayerSelectedID == NumberPlayers.oneVsThree || vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                if (playerID == 0 && extendedGameController.TurnScript._playerIsEliminated)
                {
                    return true;
                }
                else if (playerID == 1 && extendedGameController.TurnScript._leftAIIsEliminated)
                {
                    return true;
                }
                else if (playerID == 2 && extendedGameController.TurnScript._topAIIsEliminated)
                {
                    return true;
                }
                else if (playerID == 3 && extendedGameController.TurnScript._rightAIIsEliminated)
                {
                    return true;
                }
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                if (playerID == 0 && extendedGameController.TurnScript._playerIsEliminated)
                {
                    return true;
                }
                else if (playerID == 1 && extendedGameController.TurnScript._topAIIsEliminated)
                {
                    return true;
                }
            }

            return false;
        }

        public virtual List<Domino> ConvertIdTilesListToDominoList(List<int> idTilesList)
        {
            Dictionary<int, int[]> tiles = new Dictionary<int, int[]>
                {
                    { 0, new int[] { 0, 0 } }, // 0 = 0/0
                    { 1, new int[] { 0, 1 } }, // 7 = 1/1
                    { 2, new int[] { 0, 2 } },
                    { 3, new int[] { 0, 3 } },
                    { 4, new int[] { 0, 4 } },
                    { 5, new int[] { 0, 5 } },
                    { 6, new int[] { 0, 6 } },
                    { 7, new int[] { 1, 1 } },
                    { 8, new int[] { 1, 2 } },
                    { 9, new int[] { 1, 3 } },
                    { 10, new int[] { 1, 4 } },
                    { 11, new int[] { 1, 5 } },
                    { 12, new int[] { 1, 6 } },
                    { 13, new int[] { 2, 2 } },
                    { 14, new int[] { 2, 3 } },
                    { 15, new int[] { 2, 4 } },
                    { 16, new int[] { 2, 5 } },
                    { 17, new int[] { 2, 6 } },
                    { 18, new int[] { 3, 3 } },
                    { 19, new int[] { 3, 4 } },
                    { 20, new int[] { 3, 5 } },
                    { 21, new int[] { 3, 6 } },
                    { 22, new int[] { 4, 4 } },
                    { 23, new int[] { 4, 5 } },
                    { 24, new int[] { 4, 6 } },
                    { 25, new int[] { 5, 5 } },
                    { 26, new int[] { 5, 6 } },
                    { 27, new int[] { 6, 6 } }
                };

            return idTilesList
            .Where(id => tiles.ContainsKey(id))
            .Select(id =>
            {
                int[] value = tiles[id];
                return new Domino
                {
                    id = id,
                    TopIndex = value[0],
                    BottomIndex = value[1]
                };
            })
            .ToList();
        }

        public virtual void UpdateBoneyardText()
        {
            // Update the boneyard count text after taking a tile
            if (boneyardCountText is not null && extendedGameController.DeckController is ExtendedDeckController extendedDeckController)
                boneyardCountText.text = extendedDeckController.GetTotalTilesInBoneyard().ToString();
        }

        // By the momento, the currentClientID is used only because the evevent triggered in MatchManager required it
        public virtual void UpdateBoneyardText(int clientId) => UpdateBoneyardText();

        public void ShowMessageAlert()
        {
            // Only shows the feedback if the chat popUp is hidden
            if (!lobbyChatContainerObj.activeSelf)
            {
                alertIcon.enabled = true;
                SetFlashingChatButton();
            }

            void SetFlashingChatButton()
            {
                if (_waitForPlayerPassCoroutine is null)
                    _waitForPlayerPassCoroutine = StartCoroutine(FlashingChatButton());

                IEnumerator FlashingChatButton()
                {
                    var isSelecting = false;
                    var playerPutCursorOverPassButton = false;
                    while (!playerPutCursorOverPassButton)
                    {
                        playerPutCursorOverPassButton = chatButton.IsPointerOver;

                        chatButton.PreviewVisualState(isSelecting);

                        if (chatButton.Alpha is 1 or 0)
                            isSelecting = !isSelecting;

                        yield return new WaitForSeconds(chatButtonFeedbackTime);
                    }

                    chatButton.PreviewVisualState(false);
                    _waitForPlayerPassCoroutine = null;
                }
            }
        }

        /// <summary>
        /// Configure the UI elements for each player, including profile icons and badges.
        /// </summary>
        public async void ConfigurePlayersUI()
        {
            if (authManager is null or { IsAlreadyInitialized: false })
            {
                Debug.LogWarning("AuthManager is not initialized");
                return;
            }

            // Get profile icon sprite from dictionary service to every player got
            // First, try to get it from the dictionary service; if not found, download it from the URL. And if not found, get the first icon available
            var getProfileIconFunc = new Func<string, Sprite>(_id => dictionaryService.GetSprite(Consts.CollectionKeys.Icons, _id));
            var downloadProfileIcon = new AsyncFuncHandler<Sprite, string>(_spriteURL => authManager.DownloadAvatar(_spriteURL));
            var getFirstIconFunc = new Func<string, Sprite>(_id => dictionaryService.GetSpriteCollection(Consts.CollectionKeys.Icons)?.FirstOrDefault());

            // Get badges sprites from dictionary service to every player got
            var getBadgeSpriteFunc = new Func<string, Sprite>(_id => dictionaryService.GetSprite(Consts.CollectionKeys.Achievements, _id));
            var getPlayerBadgesIDs = new Func<string, List<string>>(_id => string.IsNullOrEmpty(_id) ? new List<string>() : _id?.Split(',')?.ToList());
            var getPlayerBadgesSprites = new Func<List<string>, Sprite[]>(_ids => _ids?.Select(id => getBadgeSpriteFunc(id))?.Where(sprite => sprite != null)?.ToArray());

            // Find the starting player based on the local player's userId
            var startIndex = new[] { dataInfoPlayer_0, dataInfoPlayer_1, dataInfoPlayer_2, dataInfoPlayer_3 }
                .Where(p => p.IsConfigured)
                ?.OrderBy(p => p.clientId)
                ?.ToList()
                ?.FirstOrDefault(p => p.userId == authManager.UUID).clientId;

            if (!startIndex.HasValue)
            {
                Debug.LogWarning("Couldn't configure players UI");
                return;
            }

            // According to the starting player, order the players and create a collection of their data following the order of joining the match
            var playersOrder = new[] { dataInfoPlayer_0, dataInfoPlayer_1, dataInfoPlayer_2, dataInfoPlayer_3 }
                .Where(p => p.IsConfigured)
                ?.OrderBy(p => p.clientId)
                ?.SkipWhile(p => p.clientId != startIndex)
                ?.Concat(
                    new[] { dataInfoPlayer_0, dataInfoPlayer_1, dataInfoPlayer_2, dataInfoPlayer_3 }
                    .Where(p => p.IsConfigured)
                    .OrderBy(p => p.clientId)
                    .TakeWhile(p => p.clientId != startIndex)
                )
                ?.ToList();

            if (playersOrder is null or { Count: < 2 })
            {
                Debug.LogWarning("Couldn't configure players UI");
                return;
            }

            var is2Vs2 = VSPlayerSelectedID is NumberPlayers.twoVsTwo;

            var getTurnIndex = new Func<int, int?>((playerIndex) =>
            {
                // In a 4-player match, calculate relative position from the local player
                // 0 => Bottom (already handled above)
                // 1 => Left
                // 2 => Top
                // 3 => Right
                int relativePosition = (startIndex.Value - playerIndex + 4) % 4;

                bool isLeft = relativePosition == 1;
                bool isTop = relativePosition == 2;
                bool isRight = relativePosition == 3;

                var turnIndex = default(int?);
                if (isLeft) turnIndex = 2; // is Left Player
                else if (isTop) turnIndex = 3; // is Top Player
                else if (isRight) turnIndex = 4; // is Right Player

                return turnIndex;
            });

            // The index of the '<byte, UserProfile>' collection  means the index of the handKvp position (botton => 1, left => 2, top => 3, right => 4)
            // The index used in each internal func means the currentClientID use to multiplayer match (0 => host, 1 => first to join, 2 => second one, 3 => third one)
            var scoreUIDataCollection = new Dictionary<byte, UserProfile>()
            {
                [1] = new
                    (playersOrder[0].userId, 
                    !string.IsNullOrEmpty(playersOrder[0].username) ? playersOrder[0].username : "You",
                    gameManager.ProfilePicture, 
                    getPlayerBadgesSprites(getPlayerBadgesIDs(playersOrder[0].badgesIDs)),
                    is2Vs2 ? playersOrder[0].clientId == 0 || playersOrder[0].clientId == 2 ? true : false : null), //is2Vs2 ? true : null),
            };

            // If there is only one opponent, register him as the 'front' one (3), with data corresponding to the 'first to join' (1) after the host
            if (vsPlayerSelectedID is NumberPlayers.oneVsOne)
            {
                var player = playersOrder[1];
                var iconSprite = getProfileIconFunc(player.profileIconID);
                
                // Try to download the profile icon from the URL
                if (!iconSprite)
                    iconSprite = await downloadProfileIcon(player.profileIconID);

                // If not found, get the first icon available
                if (!iconSprite)
                    iconSprite = getFirstIconFunc(player.profileIconID);

                scoreUIDataCollection.Add
                    (3, 
                    new
                        (playersOrder[1].userId, 
                        playersOrder[1].username,
                        iconSprite, 
                        getPlayerBadgesSprites(getPlayerBadgesIDs(playersOrder[1].badgesIDs))));
            }

            // Configure score UI for every player
            else
            {
                // Generate a collection to record the profile icons of each player after trying to get them from the dictionary service or downloading them
                // This implementation avoid downloading the icon repeatedly if not found in the dictionary service
                var getPlayersIconsTasks = playersOrder
                    .Select(async player =>
                    {
                        var iconSprite = getProfileIconFunc(player.profileIconID);
                        if (iconSprite != null)
                            return iconSprite;

                        // Try to download the profile icon from the URL
                        iconSprite = await downloadProfileIcon(player.profileIconID);
                        if (iconSprite != null)
                            return iconSprite;

                        // If not found, get the first icon available
                        return getFirstIconFunc(player.profileIconID);
                    })
                    .ToList();

                // Get at once all the profile icons of the remote players
                var playersIcons = await UniTask.WhenAll(getPlayersIconsTasks);

                // Starts since: 'handIndex' = 2, because the '1' is already integrated; and 'currentClientID' = 1, because the '0' is already integrated
                for (int handIndex = 2, clientID = 1; handIndex <= 4; handIndex++, clientID++)
                    scoreUIDataCollection.Add
                        ((byte)handIndex,
                        new(
                            playersOrder[clientID].userId,
                            playersOrder[clientID].username,
                            playersIcons?.ElementAtOrDefault(clientID), // Use client id as index because the order is the same
                            getPlayerBadgesSprites(getPlayerBadgesIDs(playersOrder[clientID].badgesIDs)),
                            is2Vs2 ? playersOrder[clientID].clientId == 0 || playersOrder[clientID].clientId == 2 ? true : false : null)); //is2Vs2 ? getTurnIndex(handIndex) is 3 : null));
            }


            extendedGameController.ConfigureScoreUis(scoreUIDataCollection);

        }
        #endregion

        #region Replay functions
        /// <summary>
        /// Initializes or updates replay data for the current game round, setting up player information and hands based
        /// on the selected game mode.
        /// </summary>
        public virtual void InitializeReplayManager(Dictionary<int, List<int>> clientsHands = null)
        {
            if (!ReplayManager.Instance)
            {
                Debug.LogError($"<color={Consts.Colors.Error}>ReplayManager instance not found</color>");
                return;
            }

            var playerHand = clientsHands != null && clientsHands.TryGetValue(0, out var hand) ? hand : handOfPlayer_0;
            var leftHand = clientsHands != null && clientsHands.TryGetValue(1, out var leftAIHand) ? leftAIHand : handOfPlayer_1;
            var topHand = clientsHands != null && clientsHands.TryGetValue(2, out var topAIHand) ? topAIHand : vsPlayerSelectedID is NumberPlayers.oneVsOne ? handOfPlayer_1 : handOfPlayer_2;
            var rightHand = clientsHands != null && clientsHands.TryGetValue(3, out var rightAIHand) ? rightAIHand : handOfPlayer_3;

            var auxPlayersInfo = new List<PlayerInfoReplay>();
            var auxPlayersHands = new List<PlayerHandData>();

            var ownUsername = !string.IsNullOrEmpty(authManager.Username) ? authManager.Username : "You";
            auxPlayersInfo.Add(new PlayerInfoReplay { playerIndexId = 0, playerName = ownUsername });
            auxPlayersHands.Add(new PlayerHandData { playerIndexId = 0, handPieceIds = new List<int> (playerHand) });

            // If the game mode is 1vs1, we only need to register the data of the opponent player, but if not, we need to register the data of all the opponents (3)
            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                auxPlayersInfo.Add(new PlayerInfoReplay { playerIndexId = 1, playerName = extendedGameController.ScoreUITop.UsernameLabel.text });
                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 1, handPieceIds = new List<int> (topHand) });
            }
            else
            {
                auxPlayersInfo.Add(new PlayerInfoReplay { playerIndexId = 1, playerName = extendedGameController.ScoreUILeft.UsernameLabel.text });
                auxPlayersInfo.Add(new PlayerInfoReplay { playerIndexId = 2, playerName = extendedGameController.ScoreUITop.UsernameLabel.text });
                auxPlayersInfo.Add(new PlayerInfoReplay { playerIndexId = 3, playerName = extendedGameController.ScoreUIRight.UsernameLabel.text });

                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 1, handPieceIds = new List<int> (leftHand) });
                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 2, handPieceIds = new List<int> (topHand) });
                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 3, handPieceIds = new List<int> (rightHand) });
            }

            // Initialize the replay data in the first turn of the game, but if the replay data is already initialize, we only need to update the data of the next rounds
            if (!replayIsInitialize)
            {
                replayIsInitialize = true;
                ReplayManager.Instance.InitializeReplayData(gameModeID, gameTypeSelectedID, vsPlayerSelectedID, 0, auxPlayersInfo);
                ReplayManager.Instance.ConfigureFirstTurnOfGame(extendedGameController.TurnScript.RoundsCount, new List<PlayerHandData>(auxPlayersHands), new List<int>(dominoTiles));
            }
            else
                ReplayManager.Instance.SetDataNextRound(extendedGameController.TurnScript.RoundsCount, new List<PlayerHandData>(auxPlayersHands), new List<int>(dominoTiles));
        }

        /// <summary>
        /// This called to force initialization but without create a new turn in the replay manager, because in some cases, like when the player is watching a replay, the replay data is already initialize but we need to force the initialization of the replay manager to be able to use the replay functions without create a new turn.
        /// </summary>
        public void ForceReplayInitialization()
        { 
            replayIsInitialize = true;
        }

        /// This function is called to force the replay manager to create a new turn in the replay data, because in some cases, like when the player is watching a replay, the replay data is already initialize but we need to force the creation of a new turn in the replay manager to be able to use the replay functions without create a new turn.
        public void ForceReplayTurnWhereHandsDelivered()
        {
            replayTurnWhereHandsDelivered = false;
        }

        public void CreateReplayTurn(Dictionary<int, List<int>> clientsHands = null)
        {
            var playerHand = clientsHands != null && clientsHands.TryGetValue(0, out var hand) ? hand : handOfPlayer_0;
            var leftHand = clientsHands != null && clientsHands.TryGetValue(1, out var leftAIHand) ? leftAIHand : handOfPlayer_1;
            var topHand = clientsHands != null && clientsHands.TryGetValue(2, out var topAIHand) ? topAIHand : vsPlayerSelectedID is NumberPlayers.oneVsOne ? handOfPlayer_1 : handOfPlayer_2;
            var rightHand = clientsHands != null && clientsHands.TryGetValue(3, out var rightAIHand) ? rightAIHand : handOfPlayer_3;

            var auxPlayersHands = new List<PlayerHandData>();
            var playerTileIds = GameTypeSelectedID is GameType.singlePlayerIA
                ? GetDominoDragHandlerListToTileIdList(extendedGameController.DeckScript.PlayerTiles)
                : playerHand;

            var topAITileIds = GameTypeSelectedID is GameType.singlePlayerIA 
                ? GetDominoDragHandlerListToTileIdList(extendedGameController.DeckScript.TopAITiles)
                : topHand;

            auxPlayersHands.Add(new PlayerHandData { playerIndexId = 0, handPieceIds = playerTileIds });
            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 1, handPieceIds = topAITileIds });
            
            else
            {
                var leftAITileIds = GameTypeSelectedID is GameType.singlePlayerIA
                    ? GetDominoDragHandlerListToTileIdList(extendedGameController.DeckScript.LeftAITiles)
                    : leftHand;

                var rightAITileIds = GameTypeSelectedID is GameType.singlePlayerIA
                    ? GetDominoDragHandlerListToTileIdList(extendedGameController.DeckScript.RightAITiles)
                    : rightHand;

                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 1, handPieceIds = leftAITileIds });
                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 2, handPieceIds = topAITileIds });
                auxPlayersHands.Add(new PlayerHandData { playerIndexId = 3, handPieceIds = rightAITileIds });
            }

            var pieces = new List<DominoPieceData>();

            // Recorremos todos los hijos directos del RectTransform
            for (int i = 0; i < extendedGameController.GameBoard.childCount; i++)
            {
                var child = extendedGameController.GameBoard.GetChild(i) as RectTransform;
                if (child == null) continue;

                // Obtenemos la referencia al script que contenga el tileId o sideInfo (si aplica)
                var dominoView = child.GetComponentInChildren<DominoView>(true); // ejemplo, cámbialo por tu script real
                if (!dominoView)
                {
                    Debug.LogError($"[AbstractGameMode_CreateReplayTurn] DominoView reference of child with index {i} is missing");
                    continue;
                }

                var data = new DominoPieceData
                {
                    tileId = dominoView.GetDomino().id, // valor por defecto si no hay script
                    position = child.position,
                    rotation = child.rotation,
                    sizeDelta = child.sizeDelta,
                    sideInfo = ""
                };

                pieces.Add(data);
            }

            var auxTurnSlotHelper = new TurnSlotHelper
            {
                sideLimitLeftRight = extendedGameController.SlotPosScript.SideLimitLeftRight,
                sideLimitTopDown = extendedGameController.SlotPosScript.sideLimitTopDown,
                tileOffset = extendedGameController.SlotPosScript.tileOffset,
                lyingOffset = extendedGameController.SlotPosScript.lyingOffset,
                doubleOffset = extendedGameController.SlotPosScript.doubleOffset,
                tileDist_hor = extendedGameController.SlotPosScript.tileDist_hor,

                _rightTiles_Hor = extendedGameController.SlotPosScript.RightTiles_Hor,
                _rightTiles_Ver = extendedGameController.SlotPosScript.RightTiles_Ver,
                _rightPhase = extendedGameController.SlotPosScript.RightPhase,
                _rightNum = extendedGameController.SlotPosScript.RightNum,

                _leftTiles_Hor = extendedGameController.SlotPosScript.LeftTiles_Hor,
                _leftTiles_Ver = extendedGameController.SlotPosScript.LeftTiles_Ver,
                _leftPhase = extendedGameController.SlotPosScript.LeftPhase,
                _leftNum = extendedGameController.SlotPosScript.LeftNum,

                _topTiles_Hor = extendedGameController.SlotPosScript._topTiles_Hor,
                _topTiles_Ver = extendedGameController.SlotPosScript._topTiles_Ver,
                _topPhase = extendedGameController.SlotPosScript._topPhase,
                _topNum = extendedGameController.SlotPosScript._topNum,

                _downTiles_Hor = extendedGameController.SlotPosScript._downTiles_Hor,
                _downTiles_Ver = extendedGameController.SlotPosScript._downTiles_Ver,
                _downPhase = extendedGameController.SlotPosScript._downPhase,
                _downNum = extendedGameController.SlotPosScript._downNum,
                started = extendedGameController.SlotPosScript.started,
                auxStarted = extendedGameController.SlotPosScript.auxStarted,
                firstPlacedTileId = extendedGameController.SlotPosScript.firstPlacedTileId
            };

            if (ReplayManager.Instance)
                ReplayManager.Instance.CreateNewTurn(
                    extendedGameController.TurnScript.RoundsCount,
                    new List<PlayerHandData>(auxPlayersHands), 
                    new List<int>(GetDominoDragHandlerListToTileIdList(extendedGameController.DeckScript.DominoTiles)),
                    auxTurnSlotHelper, 
                    pieces);
            else
                Debug.LogError("ReplayManager instance is not available.");
        }

        /// <summary>
        /// Sets the result of a turn in the replay manager for playback.
        /// </summary>
        /// <param name="turnResultReplay">The result data of the turn to be recorded in the replay manager.</param>
        public void SetResultToTurn(TurnResultReplay turnResultReplay)
        {
            // Check if the ReplayManager instance is available before trying to set the turn result
            if (!ReplayManager.Instance)
            {
                Debug.LogError("ReplayManager instance is not available.");
                return;
            }

            ReplayManager.Instance.SetTurnResult(turnResultReplay);
        }

        /// <summary>
        /// Converts a list of DragHandler objects to a list of domino tile IDs.
        /// </summary>
        /// <param name="auxList">The list of DragHandler objects to convert.</param>
        /// <returns>A list of integer IDs corresponding to the domino tiles, or -1 for any null references.</returns>
        private List<int> GetDominoDragHandlerListToTileIdList(List<DragHandler> auxList)
        {
            if (auxList is null)
            {
                Debug.LogError("The provided list of DragHandlers is null.");
                return new List<int>();
            }

            return auxList.Select(tile => tile.GetDominoView()?.GetDomino()?.id ?? -1).ToList();
        }
        
        /// <summary>
        /// Records a replay action for taking a tile from the boneyard during the current turn.
        /// </summary>
        /// <param name="tileId">The identifier of the tile taken from the boneyard.</param>
        public void SetTurnActionTakeBoneyardReplay(int tileId)
        {
            replayTurnWhereTakeFromBoneyard = true;
            int auxCurrentTurnPlayerIndexId = extendedGameController.TurnScript.GetCurrentTurnControl() - 1;

            if (ReplayManager.Instance)
                ReplayManager.Instance.SetTurnAction_TakeBoneyard(auxCurrentTurnPlayerIndexId, tileId);
            else
                Debug.LogError("ReplayManager instance is not available.");
        }

        /// <summary>
        /// Saves the current replay game data using the ReplayManager instance.
        /// </summary>
        private void SaveReplayGame()
        {
            if (!ReplayManager.Instance)
            {
                Debug.LogError($"<color={Consts.Colors.Error}>ReplayManager instance not found</color>");
                return;
            }

            ReplayManager.Instance.SaveReplayData();
        }
        #endregion

        public void EnableBackBtn() //Back btn
        {
            //backButton.SetButtonInteractable(true);
        }

        private void PlayerChangesTurn(bool isPlayer)
        {
            int auxTotalReplayTurns = ReplayManager.Instance.CurrentReplay.turns.Count;
            bool auxIsLimitGoingBack = false;
            int auxValidationRange = 0;

            if(vsPlayerSelectedID == NumberPlayers.oneVsOne && auxTotalReplayTurns > 4)
            {
                auxValidationRange = 4;
            }
            else if(vsPlayerSelectedID != NumberPlayers.oneVsOne && auxTotalReplayTurns > 8)
            {
                auxValidationRange = 8;
            }
            else
            {
                auxIsLimitGoingBack = true;
            }

            for(int i=1; i <= auxValidationRange; i++)
            {
                if(ReplayManager.Instance.CurrentReplay.turns[auxTotalReplayTurns-i].dealHands)
                {
                    auxIsLimitGoingBack = true;
                    break;
                }
            }

            bool auxEnableBackTurnBtn =
                isSinglePlayerVsIA
                && isPlayer
                && !auxIsLimitGoingBack;

            backButton.SetButtonInteractable(auxEnableBackTurnBtn);
        } 

        public void EnableTileSortingBtn()
        {
            extendedGameController.TileSortingBtn.SetButtonInteractable(true);
        }

        public void SetPlayer_Bot(int clientId)
        {
            switch (clientId)
            {
                case 0:
                    dataInfoPlayer_0.isBot = true;
                    break;
                case 1:
                    dataInfoPlayer_1.isBot = true;
                    break;
                case 2:
                    dataInfoPlayer_2.isBot = true;
                    break;
                case 3:
                    dataInfoPlayer_3.isBot = true;
                    break;
                default:
                    Debug.LogWarning($"<color={Consts.Colors.Error}> Invalid client ID: {clientId}</color>");
                    break;
            }
        }

        public static void HandleOnResetLobby(UnityAction onResetLobby)
        {
            if (AbstractGameMode.onResetLobby is null)
                AbstractGameMode.onResetLobby = new();

            AbstractGameMode.onResetLobby.AddListener(onResetLobby);
        }

        public static void ClearOnResetLobby()
        {
            onResetLobby = null;
        }

        #region Back turn

        [SerializeField] private string backTurnStatus = "none";
        public void PressBackTurn()
        {
            Debug.Log("!!! ReplayManager Total Turns: " + ReplayManager.Instance.CurrentReplay.turns.Count);

            int auxTotalReplayTurns = ReplayManager.Instance.CurrentReplay.turns.Count;
            bool auxIsLimitGoingBack = false;
            int auxValidationRange = 0;

            if(vsPlayerSelectedID == NumberPlayers.oneVsOne && auxTotalReplayTurns > 4)
            {
                auxValidationRange = 4;
            }
            else if(vsPlayerSelectedID != NumberPlayers.oneVsOne && auxTotalReplayTurns > 8)
            {
                auxValidationRange = 8;
            }
            else
            {
                auxIsLimitGoingBack = true;
            }

            for(int i=1; i <= auxValidationRange; i++)
            {
                if(ReplayManager.Instance.CurrentReplay.turns[auxTotalReplayTurns-i].dealHands)
                {
                    auxIsLimitGoingBack = true;
                    break;
                }
            }

            if (extendedGameController.GameTurnController.GetCurrentTurnControl() == 1 && backTurnStatus == "none"
                && !auxIsLimitGoingBack)
            {
                backTurnStatus = "goBack";
                extendedGameController.ScoreUIPlayer.TimerBar.StopTimer();

                // Discard the current selected tile if the player has selected one, because when going back a turn, the player must go back to the state of the previous turn, where the tile is not selected
                extendedGameController.DiscardSelectedTile();
                StartCoroutine(GoToPreviousTurn());

                Debug.Log("!!! Back");
            }
        }

        private IEnumerator GoToPreviousTurn()
        {
            float auxTimeWait = 1f;

            //yield return new WaitForSeconds(auxTimeWait);

            yield return StartCoroutine (ReverseCurrentTurn(ReplayManager.Instance.CurrentReplay.turns[ReplayManager.Instance.CurrentReplay.turns.Count - 1]));

            ReplayManager.Instance.RemoveTurn();

            TurnData _auxTurnData = ReplayManager.Instance.CurrentReplay.turns[ReplayManager.Instance.CurrentReplay.turns.Count - 1];

            while(_auxTurnData.playerIndexId != 0)
            {
                yield return StartCoroutine (ReverseCurrentTurn(ReplayManager.Instance.CurrentReplay.turns[ReplayManager.Instance.CurrentReplay.turns.Count - 1]));

                yield return new WaitForSeconds(0.1f);

                ReplayManager.Instance.RemoveTurn();
                
                _auxTurnData = ReplayManager.Instance.CurrentReplay.turns[ReplayManager.Instance.CurrentReplay.turns.Count - 1];
            }

            //yield return new WaitForSeconds(auxTimeWait);

            yield return new WaitForSeconds(0.1f);

            //ReplayManager.Instance.RemoveTurn();

            TurnData auxTurnData = ReplayManager.Instance.CurrentReplay.turns[ReplayManager.Instance.CurrentReplay.turns.Count - 1];
            
            yield return StartCoroutine (ReverseCurrentTurn(auxTurnData));

            BackButtonHighlightCurrentPlayerTurn(auxTurnData.playerIndexId);

            ReplayManager.Instance.RemoveTurn();

            //extendedGameController.GameTurnController.OnTurnStartEvent?.Invoke();

            extendedGameController.GameTurnController.ValideEndTurnLogic("",0.1f);

            // Try to get if there is any double tile in the board
            var isDoublePresentInBoard = tilesID_InShowBoard?.Any(x => Domino.IsDouble_ViaID(x)) ?? false;

            // If the board doesn't have any double tile, we need to set the status of the first double tile is placed to false, because when going back a turn,
            // the player must go back to the state of the previous turn, where the first double tile is not placed
            if (!isDoublePresentInBoard && TryGetComponent<Five_GameMode>(out var five_GameMode))
                five_GameMode.SetFirstDoubleTileIsPlaceStatus(false);

            backTurnStatus = "none";
        }

        public void BackButtonHighlightCurrentPlayerTurn(int playerIndexId)
        {
            extendedGameController.Replay_OnTurnStart(playerIndexId + 1);

            if(vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                switch (playerIndexId)
                {
                    case 0:
                        extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
                        break;
                    case 2:
                        extendedGameController.TurnScript.SetTurns(false, false, true, false); //player, left, top, right
                        break;
                    default:
                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                        break;
                }
            }
            else
            {
                switch (playerIndexId)
                {
                    case 0:
                        extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
                        break;
                    case 1:
                        extendedGameController.TurnScript.SetTurns(false, true, false, false); //player, left, top, right
                        break;
                    case 2:
                        extendedGameController.TurnScript.SetTurns(false, false, true, false); //player, left, top, right
                        break;
                    case 3:
                        extendedGameController.TurnScript.SetTurns(false, false, false, true); //player, left, top, right
                        break;
                    default:
                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                        break;
                }
            }
        }

        [SerializeField] private int backTurnLocalPlayerIndexId = 0;
        public IEnumerator ReverseCurrentTurn(TurnData auxCurrentTurnData)
        {
            if(auxCurrentTurnData.turnAction != TurnActionReplay.pass)
            {
                if(auxCurrentTurnData.playedPiece != null && tilesID_InShowBoard.Contains(auxCurrentTurnData.playedPiece.tileId))
                    tilesID_InShowBoard.Remove(auxCurrentTurnData.playedPiece.tileId);

                RectTransform nextHand = extendedGameController.DeckScript.Player;
                bool stand = true;
                bool onAI = false;

                List<DragHandler> tilesList = new List<DragHandler>();

                bool auxIsPlayer = false;

                if (auxCurrentTurnData.playerIndexId == backTurnLocalPlayerIndexId)
                {
                    auxIsPlayer = true;
                    tilesList = extendedGameController.DeckScript.PlayerTiles;
                }
                else
                {
                    switch (auxCurrentTurnData.playerIndexId)
                    {
                        case 1:
                            nextHand = extendedGameController.DeckScript.LeftAI;
                            stand = false;
                            onAI = true;
                            tilesList = extendedGameController.DeckScript.LeftAITiles;
                            break;
                        case 2:
                            nextHand = extendedGameController.DeckScript.TopAI;
                            stand = true;
                            onAI = true;
                            tilesList = extendedGameController.DeckScript.TopAITiles;
                            break;
                        case 3:
                            nextHand = extendedGameController.DeckScript.RightAI;
                            stand = false;
                            onAI = true;
                            tilesList = extendedGameController.DeckScript.RightAITiles;
                            break;
                        default:
                            Debug.LogError("Invalid playerIndexId in replay turn data.");
                            break;
                    }
                }

                List<DragHandler> pieces = new List<DragHandler>();

                if(auxCurrentTurnData.turnAction == TurnActionReplay.takeBoneyard)
                {
                    foreach(int aux in auxCurrentTurnData.tilesTakenFromBoneyard)
                    //foreach(int aux in auxCurrentTurnData.boneyardPieceIds)
                    {
                        DragHandler selectedTileBoneyard = GetDragHandlerFromTileId(tilesList, aux);

                        if(selectedTileBoneyard != null)
                        {
                            pieces.Add(selectedTileBoneyard);

                            //tilesList.Remove(selectedTileBoneyard);   
                        }
                    }

                    if (auxCurrentTurnData.playedPiece != null && auxCurrentTurnData.playedPiece.sideInfo != "")
                    {
                        for (int i = extendedGameController.GameBoard.childCount - 1; i >= 0; i--)
                        {
                            RectTransform child = extendedGameController.GameBoard.GetChild(i) as RectTransform;
                            if (child == null) continue;

                            DragHandler auxDragHandler = child.GetComponent<DragHandler>();

                            if(auxDragHandler.GetDominoView().GetDomino().id == auxCurrentTurnData.playedPiece.tileId)
                            {
                                tilesID_InShowBoard.Remove(auxCurrentTurnData.playedPiece.tileId);
                                //auxDragHandler.GetDominoView().OnTable();
                                auxDragHandler.GetDominoView().RemoveTable();
                                pieces.Add(auxDragHandler);

                                // Remove the slot record associated with the played tile
                                extendedGameController.SlotPosScript.RemoveSlotRecordByTileId(auxCurrentTurnData.playedPiece.tileId);

                                break;
                            }
                        }

                        yield return new WaitForSeconds(0.1f);
                    }

                    if(pieces.Count > 0)
                    {
                        extendedGameController.DeckScript.DominoTiles.AddRange(pieces);

                        extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().SendTilesToBoneyard();

                        foreach(DragHandler aux in pieces)
                        {
                            tilesList.Remove(aux);
                        }   
                    }

                    yield return new WaitForSeconds(0.1f);
                }
                else if(auxCurrentTurnData.turnAction == TurnActionReplay.play)
                {
                    if (auxCurrentTurnData.playedPiece.sideInfo != "")
                    {
                        for (int i = extendedGameController.GameBoard.childCount - 1; i >= 0; i--)
                        {
                            RectTransform child = extendedGameController.GameBoard.GetChild(i) as RectTransform;
                            if (child == null) continue;

                            DragHandler auxDragHandler = child.GetComponent<DragHandler>();

                            if (auxDragHandler.GetDominoView().GetDomino().id == auxCurrentTurnData.playedPiece.tileId)
                            {
                                //auxDragHandler.GetDominoView().OnTable();
                                auxDragHandler.GetDominoView().RemoveTable();
                                auxDragHandler.GetDominoView().ChangeBackState(!auxIsPlayer);
                                auxDragHandler.SendToNextHand(nextHand, stand, onAI, instantMove: true);
                                SetTileFromPlayerContainer(auxCurrentTurnData.playerIndexId, auxDragHandler);
                                
                                // Remove the slot record associated with the played tile
                                extendedGameController.SlotPosScript.RemoveSlotRecordByTileId(auxCurrentTurnData.playedPiece.tileId);

                                break;
                            }
                        }
                    }
                }
            }

            SetSlotHelperData(auxCurrentTurnData.turnSlotHelper);

            UpdataReplayScores(auxCurrentTurnData);

            yield return null;
        }

        public void SetTileFromPlayerContainer(int playerIndexId, DragHandler dragHandler)
        {
            if (playerIndexId == backTurnLocalPlayerIndexId)
            {
                extendedGameController.DeckScript.PlayerTiles.Add(dragHandler);
            }
            else
            {
                switch (playerIndexId)
                {
                    case 1:
                        extendedGameController.DeckScript.LeftAITiles.Add(dragHandler);
                        break;
                    case 2:
                        extendedGameController.DeckScript.TopAITiles.Add(dragHandler);
                        break;
                    case 3:
                        extendedGameController.DeckScript.RightAITiles.Add(dragHandler);
                        break;
                    default:
                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                        break;
                }
            }
        }

        private void SetSlotHelperData(TurnSlotHelper turnSlotHelper)
        {
            extendedGameController.SlotPosScript.sideLimitLeftRight = turnSlotHelper.sideLimitLeftRight;
            extendedGameController.SlotPosScript.sideLimitTopDown = turnSlotHelper.sideLimitTopDown;
            extendedGameController.SlotPosScript.tileOffset = turnSlotHelper.tileOffset;
            extendedGameController.SlotPosScript.lyingOffset = turnSlotHelper.lyingOffset;
            extendedGameController.SlotPosScript.doubleOffset = turnSlotHelper.doubleOffset;
            extendedGameController.SlotPosScript.tileDist_hor = turnSlotHelper.tileDist_hor;

            extendedGameController.SlotPosScript._rightTiles_Hor = turnSlotHelper._rightTiles_Hor;
            extendedGameController.SlotPosScript._rightTiles_Ver = turnSlotHelper._rightTiles_Ver;
            extendedGameController.SlotPosScript._rightPhase = turnSlotHelper._rightPhase;
            extendedGameController.SlotPosScript._rightNum = turnSlotHelper._rightNum;

            extendedGameController.SlotPosScript._leftTiles_Hor = turnSlotHelper._leftTiles_Hor;
            extendedGameController.SlotPosScript._leftTiles_Ver = turnSlotHelper._leftTiles_Ver;
            extendedGameController.SlotPosScript._leftPhase = turnSlotHelper._leftPhase;
            extendedGameController.SlotPosScript._leftNum = turnSlotHelper._leftNum;

            extendedGameController.SlotPosScript._topTiles_Hor = turnSlotHelper._topTiles_Hor;
            extendedGameController.SlotPosScript._topTiles_Ver = turnSlotHelper._topTiles_Ver;
            extendedGameController.SlotPosScript._topPhase = turnSlotHelper._topPhase;
            extendedGameController.SlotPosScript._topNum = turnSlotHelper._topNum;

            extendedGameController.SlotPosScript._downTiles_Hor = turnSlotHelper._downTiles_Hor;
            extendedGameController.SlotPosScript._downTiles_Ver = turnSlotHelper._downTiles_Ver;
            extendedGameController.SlotPosScript._downPhase = turnSlotHelper._downPhase;
            extendedGameController.SlotPosScript._downNum = turnSlotHelper._downNum;
            extendedGameController.SlotPosScript.started = turnSlotHelper.started;
            extendedGameController.SlotPosScript.auxStarted = turnSlotHelper.auxStarted;
            extendedGameController.SlotPosScript.firstPlacedTileId = turnSlotHelper.firstPlacedTileId;
        }

        private void UpdataReplayScores(TurnData auxCurrentTurnData)
        {
            if(vsPlayerSelectedID !=  NumberPlayers.oneVsOne)
            {
                extendedGameController.playerRoundScore = auxCurrentTurnData.roundPlayerScores[0];
                extendedGameController.leftRoundScore = auxCurrentTurnData.roundPlayerScores[1];
                extendedGameController.topRoundScore = auxCurrentTurnData.roundPlayerScores[2];
                extendedGameController.rightRoundScore = auxCurrentTurnData.roundPlayerScores[3];

                extendedGameController.TurnScript.PlayerScore = auxCurrentTurnData.cumulatePlayerScores[0];
                extendedGameController.TurnScript.LeftAIScore = auxCurrentTurnData.cumulatePlayerScores[1];
                extendedGameController.TurnScript.TopAIScore = auxCurrentTurnData.cumulatePlayerScores[2];
                extendedGameController.TurnScript.RightAIScore = auxCurrentTurnData.cumulatePlayerScores[3];
            }
            else
            {
                extendedGameController.playerRoundScore = auxCurrentTurnData.roundPlayerScores[0];
                extendedGameController.topRoundScore = auxCurrentTurnData.roundPlayerScores[1];

                extendedGameController.TurnScript.PlayerScore = auxCurrentTurnData.cumulatePlayerScores[0];
                extendedGameController.TurnScript.TopAIScore = auxCurrentTurnData.cumulatePlayerScores[1];
            }

            extendedGameController.UpdateScoreUI();
        }

        public void HighlightCurrentPlayerTurn(int playerIndexId)
        {
            extendedGameController.Replay_OnTurnStart(playerIndexId + 1);

            if (playerIndexId == backTurnLocalPlayerIndexId)
            {
                extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
            }
            else
            {
                switch (playerIndexId)
                {
                    case 1:
                        extendedGameController.TurnScript.SetTurns(false, true, false, false); //player, left, top, right
                        break;
                    case 2:
                        extendedGameController.TurnScript.SetTurns(false, false, true, false); //player, left, top, right
                        break;
                    case 3:
                        extendedGameController.TurnScript.SetTurns(false, false, false, true); //player, left, top, right
                        break;
                    default:
                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                        break;
                }
            }
        }

        private DragHandler GetDragHandlerFromTileId(List<DragHandler> tilesList, int tileId)
        {
            foreach(DragHandler aux in tilesList)
            {
                if(aux.GetDominoView().GetDomino().id == tileId)
                {
                    return aux;
                }
            }

            return null;
        }
        #endregion
    }

    public class Player
    {
        // Define properties and methods for the Player class here
        public string Name { get; set; }
        public int Score { get; set; }

        // Other relevant player attributes and methods
    }
}