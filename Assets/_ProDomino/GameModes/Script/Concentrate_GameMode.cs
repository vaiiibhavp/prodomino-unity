using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using ProDomino.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ProDomino.GameModes
{
    public class Concentrate_GameMode : AbstractGameMode
    {
        [SerializeField] CanvasGroup boneyardCanvasGroup = null;
        //[SerializeField] TMP_Text boneyardCountText = null;

        private ExtendedDeckController extendedDeckController;

        [SerializeField] BoneyardManager boneyardManager;

        [SerializeField] ConcentrateBoard concentrateBoard_28;
        [SerializeField] ConcentrateBoard concentrateBoard_56;
        [SerializeField] ConcentrateGameTimer concentrateGameTimer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool debugToolsEnabled; // Enable debug tools only in editor or development builds
        protected bool IsDebugingTilesRightNow { get; set; }
#endif
        public string CurrentDisplayName => ScoreUI.SelectedScoreUI?.UserProfile?.Username;

        protected override void Awake()
        {
            base.Awake();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugToolsEnabled = false;
#endif
        }

        /// <summary>
        /// Initializes the game mode with the specified configuration, difficulty, game type, player settings, and AI
        /// event handlers.
        /// </summary>
        /// <param name="aux_gameModeConfig">Game mode configuration settings.</param>
        /// <param name="aux_Difficulty">Selected difficulty level.</param>
        /// <param name="aux_GameTypeID">Type of the game to initialize.</param>
        /// <param name="aux_VsPlayerID">Player mode or number of players.</param>
        /// <param name="isSinglePlayerIA">Indicates if the game is single-player against AI.</param>
        /// <param name="auxConcentrateNumberOfTiles">Tile concentration settings for the game.</param>
        /// <param name="openMenuSettingsAction">Action to open the menu settings.</param>
        /// <param name="isTimeOut_MatchManager">Function to determine if the match manager has timed out.</param>
        public override void InitializeGameMode(GameModeConfig aux_gameModeConfig, int aux_Difficulty, GameType aux_GameTypeID, NumberPlayers aux_VsPlayerID, bool isSinglePlayerIA, ConcentrateNumberOfTiles auxConcentrateNumberOfTiles, Action openMenuSettingsAction, Func<bool> isTimeOut_MatchManager)
        {
            base.InitializeGameMode(aux_gameModeConfig, aux_Difficulty, aux_GameTypeID, aux_VsPlayerID, isSinglePlayerIA, auxConcentrateNumberOfTiles, openMenuSettingsAction, isTimeOut_MatchManager);

            // Register some AI events to be used
            extendedGameController.Initialize
                (PostMatchResultController,
                gameModeConfig.SetGameplayVisibility,
                openMenuSettingsAction,
                dominoAI.GetFirstTurnAvailableTilesFrench,
                dominoAI.GetBestMoveFrench,
                () => CurrentDisplayName);
        }

        public override void RestartGame(bool restartNewRound = false)
        {
            base.RestartGame(restartNewRound);

            //fullSides = false;

            //UpdateBoneyardText();

            if (!restartNewRound)
            {
                playerPoints = 0;
                leftAIPoints = 0;
                topAIPoints = 0;
                rightAIPoints = 0;
                pointsTeam_1 = 0;
                pointsTeam_2 = 0;
            }
        }


        protected override void ShowAIProbabilities(Dictionary<Domino, float> dominosProbabilities)
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

            // Iterate foreach tile to show the feedback of the tile
            if (aiHand is not null and { Count: > 0 })
            {
                // Reset colors of the tiles not included in the probabilities
                foreach (var tile in aiHand)
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

            void ResetTileColor(DragHandler tile)
            {
                if (tile == null)
                    return;

                //tile.GetDominoView().ChangeBackState(true);
                foreach (var graphic in tile.GetComponentsInChildren<Graphic>(true))
                    graphic.color = Color.white;
            }
#endif
        }


        public override void SetupGameMode()
        {
            throw new System.NotImplementedException();
        }

        public override void SetupRandomHands()
        {
            extendedDeckController = extendedGameController.DeckScript.GetComponent<ExtendedDeckController>();

            numberOfTiles = concentrateNumberOfTiles == ConcentrateNumberOfTiles.tiles_56 ? 56 : 28;
            //numberOfTiles = 56;

            extendedDeckController.ConcentrateSetupNewGame(numberOfTiles);

            dominoTiles.Clear();
            dominoTiles = new List<int>();

            for (int i = 0; i < numberOfTiles; i++)
            {
                dominoTiles.Add(i);
            }

            handOfPlayer_0.Clear();
            handOfPlayer_1.Clear();
            handOfPlayer_2.Clear();
            handOfPlayer_3.Clear();

            handOfPlayer_0 = new List<int>();
            handOfPlayer_1 = new List<int>();
            handOfPlayer_2 = new List<int>();
            handOfPlayer_3 = new List<int>();

            System.Random random = new System.Random();

            // Reorganize the tiles in the graveyard randomly using the Fisher-Yates Shuffle algorithm
            for (int i = dominoTiles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1); // Includes the value i
                int temp = dominoTiles[i];
                dominoTiles[i] = dominoTiles[j];
                dominoTiles[j] = temp;
            }

            if (isSinglePlayerVsIA)
            {
                StartCoroutine(SetupTilesInBoard(SelectPlayerWhoWillTakeFirstTurn,
                    UpdateBoneyardText,
                    dominoTiles,
                    numberOfTiles));

                /*extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().DeckExtendedSetupRandomHands
                    (SelectPlayerWhoWillTakeFirstTurn,
                    UpdateBoneyardText,
                    handOfPlayer_0,
                    handOfPlayer_1,
                    handOfPlayer_2,
                    handOfPlayer_3);*/
            }
            else
            {
                //The remaining chips in the cemetery are arranged randomly:
                for (int i = dominoTiles.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1); // Includes the value i
                    int temp = dominoTiles[i];
                    dominoTiles[i] = dominoTiles[j];
                    dominoTiles[j] = temp;
                }

                boneyardDominoTiles = new List<int>(dominoTiles);
                Debug.Log($"Concentrate: Boneyard domino tiles setted. Values: {string.Join(", ", boneyardDominoTiles)}");
            }
        }

        /*public void SendTileToBoneyard(DragHandler dragHandler)
        {
            boneyardManager.InitializeSlotsFromBoneyard();

            // Reorganize the tiles in the graveyard randomly using the Fisher-Yates Shuffle algorithm
            int i = 0;
            for (i = extendedDeckController.DominoTiles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1); // Includes the value i
                DragHandler temp = extendedDeckController.DominoTiles[i];
                extendedDeckController.DominoTiles[i] = extendedDeckController.DominoTiles[j];
                extendedDeckController.DominoTiles[j] = temp;
            }

            //Assigning tiles to the boneyard interface
            i = 0;
            for (i = 0; i < extendedDeckController.DominoTiles.Count; i++)
            {
                extendedDeckController.DominoTiles[i].SetToParent(boneyardManager.BoneyardTileContainer.GetChild(i).GetComponent<RectTransform>());
                extendedDeckController.DominoTiles[i].gameObject.SetActive(false);
            }

            //boneyardManager.TotalTilesInBoneyard = _dominoTiles.Count;

            for (int j = i; j < boneyardManager.BoneyardTileContainer.childCount; j++)
            {
                boneyardManager.BoneyardTileContainer.GetChild(j).GetComponent<CustomButtonUI>().SetButtonInteractable(false);
                boneyardManager.BoneyardTileContainer.GetChild(j).GetComponent<Image>().enabled = false;
                boneyardManager.BoneyardTileContainer.GetChild(j).GetChild(0).gameObject.SetActive(false);

                boneyardManager.RemoveSlotFromBoneyard(boneyardManager.BoneyardTileContainer.GetChild(j));
            }
        }*/

        ConcentrateBoard auxConcentrateBoard = null;
        private IEnumerator SetupTilesInBoard(Func<int> selectPlayerWhoWillTakeFirstTurn, Action updateBoneyardText, List<int> dominos_ids, int auxNumberOfTiles)
        {
            if (vsPlayerSelectedID == NumberPlayers.solo)
            {
                concentrateGameTimer.ResetTimer(); //Concentrate timer
                ExtendedGameController.HidePlayerTimer();
            }
            else
                concentrateGameTimer.DisableTimer();

            if (auxNumberOfTiles == 28)
            {
                concentrateBoard_56.gameObject.SetActive(false);
                auxConcentrateBoard = concentrateBoard_28;
            }
            else
            {
                concentrateBoard_28.gameObject.SetActive(false);
                auxConcentrateBoard = concentrateBoard_56;
            }

            auxConcentrateBoard.gameObject.SetActive(true);
            auxConcentrateBoard.InitializeSlotsFromConcentrate();

            yield return new WaitForSeconds(1f);

            if (extendedDeckController.DeckHolder)
            {
                extendedDeckController.DeckHolder.transform.localPosition = new Vector3(0, -1100, 0);
                extendedDeckController.DeckHolder.gameObject.SetActive(true); // Show the deck
            }

            float auxWaitingTime = auxNumberOfTiles == 28 ? 0.2f : 0.1f;

            int i = 0;
            foreach (int auxID in dominos_ids)
            {
                StartCoroutine(FlyTileToConcentrateBoard(extendedDeckController.DominoTiles[auxID], auxConcentrateBoard.BoneyardTileContainer.GetChild(i).GetComponent<RectTransform>(), auxNumberOfTiles));
                i++;

                SoundManager.Instance.PlaySFX(IDAudioClip.shuffle);

                yield return new WaitForSeconds(auxWaitingTime);
            }

            yield return new WaitForSeconds(auxWaitingTime);

            selectPlayerWhoWillTakeFirstTurn?.Invoke();

            tileContainerIndex.Clear();
            //tileContainerIndex = Enumerable.Range(0, auxConcentrateBoard.TileContainers.Count - 1).ToList();
            tileContainerIndex = Enumerable.Range(0, auxConcentrateBoard.TileContainers.Count).ToList();


            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();

            if (extendedGameController.TurnScript.GetCurrentPlayerTurn() == "playerTurn")
            {
                auxConcentrateBoard.EnableAllConcentrateSlots();
            }
            else
            {

                StartCoroutine(SelectedTileIA());
            }

            if (vsPlayerSelectedID == NumberPlayers.solo)
                concentrateGameTimer.StartTimer();

            /*while (i < numberTilesPerPlayer * 4 && extendedDeckController.DominoTiles.Count > 0) //while (i < 28 && _dominoTiles.Count > 0)
                {
                    //TileSwapHelper(ref _player, ref _playerTiles, handPlayer_0[i], true, false, remove: false);

                    // Update the boneyard text if provided
                    if (updateBoneyardText is not null)
                        updateBoneyardText?.Invoke();

                    i++;
                    yield return new WaitForSeconds(0.2f);
                }*/

            //extendedDeckController.DeckHolder.transform.localPosition = new Vector3(2000, 0, 0);

            // Wait until the hands are fully set up
            //yield return new WaitForSeconds(1f);

            yield return null;
        }

        private IEnumerator FlyTileToConcentrateBoard(DragHandler dragHandler, RectTransform _nextParent, int auxNumberOfTiles)
        {
            float duration = auxNumberOfTiles == 28 ? 0.4f : 0.2f;
            float currentTime = 0;
            float normalizedValue = 0;
            //bool changeParent = false;

            float moveSpeed = 1f;
            float rotationSpeed = 1f;

            RectTransform _dragObject = dragHandler.DragObject;
            _dragObject.gameObject.AddComponent<CanvasGroup>();
            CanvasGroup cgDragObject = _dragObject.gameObject.GetComponent<CanvasGroup>();

            cgDragObject.alpha = 1f;
            cgDragObject.blocksRaycasts = true;
            cgDragObject.interactable = true;
            cgDragObject.ignoreParentGroups = true;

            // Save initial transform state
            Vector3 startPosition = _dragObject.position;
            Vector2 startSize = _dragObject.sizeDelta;
            Vector3 startScale = _dragObject.localScale;
            Quaternion startRotation = _dragObject.rotation;

            Vector2 targetSize = _nextParent.rect.size;
            Vector3 targetScale = Vector3.one;
            Quaternion targetRotation = _nextParent.rotation;

            Vector3 targetWorldPosition = _nextParent.position;

            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                normalizedValue = Mathf.Clamp01(currentTime / duration);

                _dragObject.position = Vector3.Lerp(startPosition, targetWorldPosition, normalizedValue * moveSpeed);
                _dragObject.sizeDelta = Vector2.Lerp(startSize, targetSize, normalizedValue * moveSpeed);
                _dragObject.localScale = Vector3.Lerp(startScale, targetScale, normalizedValue * moveSpeed);
                _dragObject.rotation = Quaternion.Lerp(startRotation, targetRotation, normalizedValue * rotationSpeed);

                if (Vector3.Distance(_dragObject.position, targetWorldPosition) <= 1f)
                {
                    _nextParent.GetComponent<CustomButtonUI>().SetButtonInteractableWithAlphaFull(false);

                    //_nextParent.GetComponent<Image>().enabled = showSlots;
                    //_nextParent.GetChild(0).gameObject.SetActive(showSlots);

                    //_nextParent.gameObject.SetActive(true);
                    //_dragObject.position = targetWorldPosition;
                    _dragObject.sizeDelta = targetSize;
                    _dragObject.localScale = targetScale;

                    _dragObject.SetParent(_nextParent, false); // Change parent without preserving world transform
                    _dragObject.localRotation = Quaternion.identity; // Force local rotation to zero
                    _dragObject.localPosition = new Vector3(0, 0, 0);
                    _dragObject.gameObject.SetActive(false);
                    //_dragObject.SetSiblingIndex(0);


                    extendedGameController.transform.RefreshContentSizeFitterImmediateAndRecursive(this);
                    extendedGameController.transform.RefreshLayoutGroupsImmediateAndRecursive();
                    yield break;
                }

                yield return null;
            }
        }

        #region Validate Numbers Available In Branches

        public override void ValidateNumbersAvailableInBranches(ref int rightBranch, ref int leftBranch, ref int topNum, ref int downNum, ref List<int> doubleTilesEnabled, int firstPlacedTileId, Domino tileInfo = null)
        {
            throw new System.NotImplementedException();
        }

        #endregion

        public override int SelectPlayerWhoWillTakeFirstTurn()
        {
            int auxRandomPlayerId = -1;

            if (vsPlayerSelectedID == NumberPlayers.solo)
            {
                extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                auxRandomPlayerId = UnityEngine.Random.Range(0, 2);

                switch (auxRandomPlayerId)
                {
                    case 0:
                        extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                        break;
                    case 1:
                        extendedGameController.TurnScript.SetTurns(false, false, true, false);// Player, Left, Top, Right
                        break;
                    default:
                        extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                        Debug.Log("Invalid player ID for first turn selection.");
                        break;
                }
            }
            else
            {
                auxRandomPlayerId = UnityEngine.Random.Range(0, 4);

                switch (auxRandomPlayerId)
                {
                    case 0:
                        extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                        break;
                    case 1:
                        extendedGameController.TurnScript.SetTurns(false, true, false, false);// Player, Left, Top, Right
                        break;
                    case 2:
                        extendedGameController.TurnScript.SetTurns(false, false, true, false);// Player, Left, Top, Right
                        break;
                    case 3:
                        extendedGameController.TurnScript.SetTurns(false, false, false, true);// Player, Left, Top, Right
                        break;
                    default:
                        extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                        Debug.Log("Invalid player ID for first turn selection.");
                        break;
                }
            }

            Debug.Log("--**// auxRandomPlayerId: " + auxRandomPlayerId);

            return 0;
        }

        public override void StartTurn(int playerTurnID, int localPlayerID)
        {
            boneyardCanvasGroup.interactable = false;
            extendedGameController.EnableTurnForPlayer(playerTurnID, localPlayerID);
        }

        public override void StartNextTurn()
        {
            throw new System.NotImplementedException();
        }

        public override void EndTurn()
        {
            throw new System.NotImplementedException();
        }

        #region Boneyard:

        public override bool HandleNoValidMoves(bool isPlayerTurn)
        {
            Debug.Log("No Valid Moves");

            if (isSinglePlayerVsIA)
            {
                bool auxTotalTilesInBoneyard = extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().GetTotalTilesInBoneyard() > 0;

                if (auxTotalTilesInBoneyard)
                {
                    if (!isPlayerTurn)
                    {
                        Debug.Log("No valid moves for AI, taking tile from boneyard.");
                        //return extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().TakeTileFromBoneyard();

                        boneyardManager.ShowBoneyard();

                        bool tileTaken = boneyardManager.SelectedRandomBoneyardTile();

                        Debug.Log($"Tile taken from boneyard: {tileTaken}");

                        return tileTaken;

                        //return boneyardManager.SelectedRandomBoneyardTile();
                    }
                    else
                    {
                        Debug.Log("++-- EnableBoneyard");

                        StartCoroutine(OpenBoneyard());

                        return true;
                    }
                }
                else
                {
                    boneyardManager.HideBoneyard();
                    Debug.Log("No tiles left in boneyard, player must pass.");
                    return false;
                }
            }
            else
            {
                Debug.Log("Boneyard in multiplayer-Online");

                bool auxTotalTilesInBoneyard = extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().GetTotalTilesInBoneyard() > 0;

                if (auxTotalTilesInBoneyard)
                {
                    if (!isPlayerTurn)
                    {
                        boneyardManager.ShowBoneyard(enableTilesButtons: false);
                    }
                    else
                    {
                        Debug.Log("++-- EnableBoneyard");

                        StartCoroutine(OpenBoneyard());
                    }

                    return true;
                }
                else
                {
                    boneyardManager.HideBoneyard();
                    return false;
                }
            }

            //extendedGameController.NotifyPlayerEndsMovement_ToHost(null, "No valid moves available for player: " + player.PlayerID);
        }

        private IEnumerator OpenBoneyard()
        {
            yield return new WaitForSeconds(1.0f);

            boneyardManager.ShowBoneyard();
        }

        public override void HandleHasValidMoves()
        {
            Debug.Log("Has Valid Moves");

            boneyardManager.HideBoneyard();
        }

        public override void SendBoneyardTileToHand_FromHost(int currentTurnPlayerID, int localPlayerID, int indexSelected, Action callback, int tileID = -1)
        {
            string auxCurrentPlayerTurn = "";

            if (currentTurnPlayerID == localPlayerID)
            {
                handOfLocalPlayer.Add(tileID);
                auxCurrentPlayerTurn = "playerTurn";
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                auxCurrentPlayerTurn = "topAITurn";
            }
            else
            {
                // In a 4-player match, calculate relative position from the local player
                // 0 => Bottom (already handled above)
                // 1 => Left
                // 2 => Top
                // 3 => Right
                int relativePosition = (currentTurnPlayerID - localPlayerID + 4) % 4;

                bool isLeft = (relativePosition == 1);
                bool isTop = (relativePosition == 2);
                bool isRight = (relativePosition == 3);

                auxCurrentPlayerTurn = isLeft ? "leftAITurn" :
                                       isTop ? "topAITurn" :
                                       isRight ? "rightAITurn" : "playerTurn";
            }

            boneyardManager.Select_BoneyardTileForPlayer_FromHost(auxCurrentPlayerTurn, indexSelected, tileID, callback);
        }

        public override async void BotSendBoneyardTileToHand_FromClient(int currentTurnPlayerID, int localPlayerID, Action<int> callback)
        {
            Debug.Log("No valid moves for Bot, taking tile from boneyard.");

            boneyardManager.ShowBoneyard(false, false);

            // Wait to simulate human behaviour
            var randomTimeToWait = Random.Range(1, 4);
            await UniTask.WaitForSeconds(randomTimeToWait, true);

            var tileTaken = boneyardManager.GetRandomBoneyardTile();
            int auxIndex = boneyardManager.Select_BoneyardTileForPlayerAndReturnIndex(tileTaken);

            Debug.Log("++-- Boneyard Bot selection Index: " + auxIndex);

            string auxCurrentPlayerTurn = "";

            if (currentTurnPlayerID == localPlayerID)
                auxCurrentPlayerTurn = "playerTurn";

            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                auxCurrentPlayerTurn = "topAITurn";

            else
            {
                // In a 4-player match, calculate relative position from the local player
                // 0 => Bottom (already handled above)
                // 1 => Left
                // 2 => Top
                // 3 => Right
                int relativePosition = (currentTurnPlayerID - localPlayerID + 4) % 4;

                bool isLeft = (relativePosition == 1);
                bool isTop = (relativePosition == 2);
                bool isRight = (relativePosition == 3);

                auxCurrentPlayerTurn = isLeft ? "leftAITurn" :
                                        isTop ? "topAITurn" :
                                        isRight ? "rightAITurn" : "playerTurn";
            }

            // According the record of boneyard tiles, get the one that corresponds to the index obtained
            // Don't use the id of the tile taken, because it could be different from the one in the boneyardDominoTiles list
            var tileID = boneyardDominoTiles?.ElementAtOrDefault(auxIndex);
            boneyardManager.Select_BoneyardTileForPlayer_FromHost(auxCurrentPlayerTurn, auxIndex, tileID ?? -1, () => callback?.Invoke(auxIndex));
        }

        public override void BoneyardIsEmpy(bool enablePassButton, Action<bool> callback = null)
        {
            boneyardManager.HideBoneyard();
            onPassTurnHostAction = callback;

            if (enablePassButton)
            {
                ExtendedGameController.HandController.PlayerPassButton.SetButtonInteractable(true);
                //ExtendedGameController.HandController.OnPlayerHasToPass();
            }
        }

        private Transform firstTileSelected = null;
        private Transform secondTileSelected = null;
        //private DominoView firstTileSelected = null;
        //private DominoView secondTileSelected = null;
        private string statusTileSelection = "waiting";

        public void SelectConcentrateTileForPlayer(Transform container)
        {
            if (isSinglePlayerVsIA)
            {
                switch (statusTileSelection)
                {
                    case "incorrectCombination":
                        statusTileSelection = "waiting";
                        firstTileSelected.GetChild(1).gameObject.SetActive(false);
                        secondTileSelected.GetChild(1).gameObject.SetActive(false);

                        if (extendedGameController.TurnScript.GetCurrentPlayerTurn() is "playerTurn")
                        { 
                            firstTileSelected.GetComponent<CustomButtonUI>().SetButtonInteractable(true);
                            secondTileSelected.GetComponent<CustomButtonUI>().SetButtonInteractable(true);
                        }

                        firstTileSelected = null;
                        secondTileSelected = null;

                        tileContainerIndex.Clear();
                        tileContainerIndex = Enumerable.Range(0, auxConcentrateBoard.TileContainers.Count).ToList();
                        break;
                    case "correctCombination":
                        statusTileSelection = "waiting";
                        firstTileSelected = null;
                        secondTileSelected = null;

                        tileContainerIndex.Clear();
                        tileContainerIndex = Enumerable.Range(0, auxConcentrateBoard.TileContainers.Count).ToList();
                        break;
                    /*case "waiting":
                        tileContainerIndex.Clear();
                        tileContainerIndex = Enumerable.Range(0, auxConcentrateBoard.TileContainers.Count - 1).ToList();
                        break;*/
                    default:
                        Debug.Log("Incorrect status");
                        break;
                }

                int auxIndexContainer = auxConcentrateBoard.TileContainers.IndexOf(container);
                tileContainerIndex.Remove(auxIndexContainer);

                if (firstTileSelected == null)
                {
                    firstTileSelected = container; //container.GetChild(1).GetComponent<DominoView>();

                    firstTileSelected.GetChild(1).gameObject.SetActive(true);
                    firstTileSelected.GetChild(1).gameObject.GetComponent<DominoView>().OnTable();
                    firstTileSelected.GetChild(1).gameObject.GetComponent<DominoView>().ChangeBackState(false);

                    firstTileSelected.GetComponent<CustomButtonUI>().SetButtonInteractable(false, auxAlpha: 0);
                }
                else if (secondTileSelected == null)
                {
                    secondTileSelected = container; //container.GetChild(1).GetComponent<DominoView>();

                    secondTileSelected.GetChild(1).gameObject.SetActive(true);
                    secondTileSelected.GetChild(1).gameObject.GetComponent<DominoView>().OnTable();
                    secondTileSelected.GetChild(1).gameObject.GetComponent<DominoView>().ChangeBackState(false);

                    secondTileSelected.GetComponent<CustomButtonUI>().SetButtonInteractable(false, auxAlpha: 0);

                    bool auxIsCorrect = ValidateSelectedTiles(firstTileSelected, secondTileSelected);

                    if (!auxIsCorrect)
                    {
                        NextTurn();
                    }
                    else
                    {
                        tileContainerIndex.Clear();
                        tileContainerIndex = Enumerable.Range(0, auxConcentrateBoard.TileContainers.Count).ToList();

                        if (tileContainerIndex.Count > 0)
                        {
                            if (extendedGameController.TurnScript.GetCurrentPlayerTurn() != "playerTurn")
                            {
                                StartCoroutine(SelectedTileIA());
                            } 
                        }
                        else
                        {
                            EndConcentrateGame();
                        }
                    }
                }
                //auxConcentrateBoard.Select_BoneyardTileForPlayer(container);

                SoundManager.Instance.PlaySFX(IDAudioClip.placePiece);
            }
            else
            {
                /*Debug.Log("++-- Selected Tile from boneytard and return Index");

                int auxIndex = auxConcentrateBoard.Select_BoneyardTileForPlayerAndReturnIndex(container);

                Debug.Log("++-- Boneyard selection Index: " + auxIndex);

                OnPlayerTakesFromBoneyard?.Invoke(auxIndex);*/
            }
        }

        private bool ValidateSelectedTiles(Transform auxFirstTileSelected, Transform auxSecondTileSelected)
        {
            DominoView auxFirstTileDominoView = auxFirstTileSelected.GetChild(1).gameObject.GetComponent<DominoView>();
            DominoView auxSecondTileDominoView = auxSecondTileSelected.GetChild(1).gameObject.GetComponent<DominoView>();

            int totalSum = auxFirstTileDominoView.GetDomino().TopIndex + auxFirstTileDominoView.GetDomino().BottomIndex +
                    auxSecondTileDominoView.GetDomino().TopIndex + auxSecondTileDominoView.GetDomino().BottomIndex;

            if (totalSum == 12)
            {
                statusTileSelection = "correctCombination";

                auxConcentrateBoard.RemoveSlotFromBoneyard(auxFirstTileSelected);
                auxConcentrateBoard.RemoveSlotFromBoneyard(auxSecondTileSelected);

                auxFirstTileDominoView.DisableInConcentrataGameMode(.5f);
                auxSecondTileDominoView.DisableInConcentrataGameMode(.5f);

                switch (extendedGameController.TurnScript.GetCurrentPlayerTurn())
                {
                    case "playerTurn":
                        extendedGameController.TurnScript.PlayerScore += 1;
                        break;
                    case "leftAITurn":
                        extendedGameController.TurnScript.LeftAIScore += 1;
                        break;
                    case "topAITurn":
                        extendedGameController.TurnScript.TopAIScore += 1;
                        break;
                    case "rightAITurn":
                        extendedGameController.TurnScript.RightAIScore += 1;
                        break;
                    default:
                        Debug.Log("Invalid player ID for first turn selection.");
                        break;
                }

                extendedGameController.UpdateScoreUI(); //Update Score

                return true;
            }
            else
            {
                statusTileSelection = "incorrectCombination";

                return false;
            }
        }

        [SerializeField] private List<int> tileContainerIndex = new List<int>();
        private void NextTurn()
        {
            auxConcentrateBoard.DisableAllConcentrateSlots();

            if (auxConcentrateBoard.TileContainers.Count > 0)
            {
                // Make sure the timer for both players are deactivated
                extendedGameController.ScoreUIPlayer?.TimerBar?.StopTimer();
                extendedGameController.ScoreUILeft?.TimerBar?.StopTimer();
                extendedGameController.ScoreUITop?.TimerBar?.StopTimer();
                extendedGameController.ScoreUIRight?.TimerBar?.StopTimer();

                if (vsPlayerSelectedID == NumberPlayers.solo)
                {
                    extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                    auxConcentrateBoard.EnableAllConcentrateSlots();
                }
                else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    switch (extendedGameController.TurnScript.GetCurrentPlayerTurn())
                    {
                        case "playerTurn":
                            extendedGameController.TurnScript.SetTurns(false, false, true, false);// Player, Left, Top, Right
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            StartCoroutine(SelectedTileIA());
                            break;
                        case "topAITurn":
                            extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                            auxConcentrateBoard.EnableAllConcentrateSlots();

                            Debug.LogError("TESTING");
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            break;
                        default:
                            extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                            auxConcentrateBoard.EnableAllConcentrateSlots();
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            Debug.Log("Invalid player ID for first turn selection.");
                            break;
                    }
                }
                else
                {
                    switch (extendedGameController.TurnScript.GetCurrentPlayerTurn())
                    {
                        case "playerTurn":
                            extendedGameController.TurnScript.SetTurns(false, true, false, false);// Player, Left, Top, Right
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            StartCoroutine(SelectedTileIA());
                            break;
                        case "leftAITurn":
                            extendedGameController.TurnScript.SetTurns(false, false, true, false);// Player, Left, Top, Right
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            StartCoroutine(SelectedTileIA());
                            break;
                        case "topAITurn":
                            extendedGameController.TurnScript.SetTurns(false, false, false, true);// Player, Left, Top, Right
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            StartCoroutine(SelectedTileIA());
                            break;
                        case "rightAITurn":
                            extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                            auxConcentrateBoard.EnableAllConcentrateSlots();
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            break;
                        default:
                            extendedGameController.TurnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                            auxConcentrateBoard.EnableAllConcentrateSlots();
                            extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
                            Debug.Log("Invalid player ID for first turn selection.");
                            break;
                    }
                }

                //extendedGameController.TurnScript.OnTurnStartEvent?.Invoke();
            }
            else
            {
                EndConcentrateGame();
            }
        }

        /// <summary>
        /// Method used to play automatically a tile<br></br>
        /// Mainly use when the t
        /// </summary>
        public void ForcePlayATile()
        {
            auxConcentrateBoard.DisableAllConcentrateSlots();
            StartCoroutine(CompleteSelection());

            IEnumerator CompleteSelection()
            {
                var randomTileIndex = default(int);
                var auxWaitingTime = default(float);

                if (tileContainerIndex.Count > 0)
                {
                    Debug.Log("[ConcentrateGameMode_ForcePlayATile] Proceding to select lefting tiles...");

                    // If the first tile selection is lefting. Proceed to select it
                    if (!firstTileSelected)
                        yield return PlayARandomTile();

                    // If the second tile selection is lefting. Proceed to select it
                    if (!secondTileSelected)
                        yield return PlayARandomTile();
                } 
                else
                    EndConcentrateGame();

                IEnumerator PlayARandomTile()
                {
                    auxWaitingTime = Random.Range(1, 3);
                    randomTileIndex = Random.Range(0, tileContainerIndex.Count);

                    yield return new WaitForSeconds(auxWaitingTime);
                    SelectConcentrateTileForPlayer(auxConcentrateBoard.TileContainers[tileContainerIndex[randomTileIndex]]);
                }
            }
        }

        private IEnumerator SelectedTileIA()
        {
            float auxWaitingTime = Random.Range(2, 4);

            yield return new WaitForSeconds(auxWaitingTime);

            if (tileContainerIndex.Count > 0)
            {
                int auxIndex = Random.Range(0, tileContainerIndex.Count);
                SelectConcentrateTileForPlayer(auxConcentrateBoard.TileContainers[tileContainerIndex[auxIndex]]);

                auxWaitingTime = Random.Range(1, 3);
                yield return new WaitForSeconds(auxWaitingTime);

                auxIndex = Random.Range(0, tileContainerIndex.Count);
                SelectConcentrateTileForPlayer(auxConcentrateBoard.TileContainers[tileContainerIndex[auxIndex]]);
                SoundManager.Instance.PlaySFX(IDAudioClip.placePiece);
            } 
            else
            {
                EndConcentrateGame();
            }

            yield return null;
        }

        private void EndConcentrateGame()
        {
            //Stop timer
            if (vsPlayerSelectedID == NumberPlayers.solo)
                concentrateGameTimer.StopTimer();

            //Calcule winner:
            Dictionary<string, int> roundPoints = new Dictionary<string, int>()
            {
                { "Player", extendedGameController.TurnScript.PlayerScore }, // Player
                { "Left", extendedGameController.TurnScript.LeftAIScore }, // Left AI
                { "Top", extendedGameController.TurnScript.TopAIScore }, // Top AI
                { "Right", extendedGameController.TurnScript.RightAIScore } // Right AI
            };

            Debug.Log("roundWinners PlayerScore: " + extendedGameController.TurnScript.PlayerScore);
            Debug.Log("roundWinners LeftAIScore: " + extendedGameController.TurnScript.LeftAIScore);
            Debug.Log("roundWinners TopAIScore: " + extendedGameController.TurnScript.TopAIScore);
            Debug.Log("roundWinners RightAIScore: " + extendedGameController.TurnScript.RightAIScore);

            int minValue = roundPoints.Values.Max();

            List<string> roundWinners = roundPoints
                .Where(p => p.Value == minValue)
                .Select(p => p.Key)
                .ToList();

            if (roundWinners.Count == 1)
            {
                extendedGameController.PlayerWinner = roundWinners[0];

                Debug.Log("roundWinners 1: " + roundWinners[0]);
            }
            else
            {
                extendedGameController.PlayerWinner = "draw";

                Debug.Log("roundWinners 1: " + roundWinners[0]);
                Debug.Log("roundWinners 2: " + roundWinners[1]);
            }

            extendedGameController.GameIsCompleteAndFinished = true;
            extendedGameController.TurnScript.EndRound("Game Over **");
        }

        /*private void OnTurnStart()
        {
            var newTurnIndex = extendedGameController.TurnScript.GetCurrentTurnControl();

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
            currentScoreUI?.TimerBar?.StartTimer(40);
        }*/

        #endregion

        public override void PassTurnBtn()
        {
            if (isSinglePlayerVsIA)
            {
                //ExtendedGameController.AIController.PassTurn();
                NextTurn();
            }
            else
            {
                onPassTurnHostAction?.Invoke(true);
            }

            SoundManager.Instance.PlaySFX(IDAudioClip.passTurn);
        }

        public override void UpdateScoreAndShowRoundResult
            (string gameOverCase, int scorePlayer_0, int scorePlayer_1, int scorePlayer_2, int scorePlayer_3,
            int playerID,
            bool auxGameIsCompleteAndFinished,
            string auxPlayerWinner)
        {
            extendedGameController.GameIsCompleteAndFinished = auxGameIsCompleteAndFinished;
            extendedGameController.PlayerWinner = auxPlayerWinner;

            Dictionary<string, byte> playersOrderId = new()
            {
                { "Player", 0 }, // Player
                { "Left", 1 }, // Left AI
                { "Top", 2 }, // Top AI
                { "Right", 3} // Right AI
            };

            if (!isSinglePlayerVsIA && vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                playersOrderId = new Dictionary<string, byte>()
                {
                    { "Player", 0 }, // Player
                    { "Top", 1 } // Top AI
                };
            }

            if (playersOrderId[auxPlayerWinner] == playerID)
                extendedGameController.PlayerWinner = "Player";
            
            Dictionary<int, int> scorePlayersOrder = new Dictionary<int, int>
            {
                { 0, scorePlayer_0 }, //score player 0
                { 1, scorePlayer_1 }, //score player 1
                { 2, scorePlayer_2 }, //score player 2
                { 3, scorePlayer_3 } //score player 3
            };

            if (vsPlayerSelectedID == NumberPlayers.oneVsThree || vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                List<int> auxScoreValuesOrder = new List<int>();

                int auxPlayersCounter = 0;
                int auxPlayerID_counter = playerID;

                while (auxPlayersCounter < 4)
                {
                    auxScoreValuesOrder.Add(scorePlayersOrder[auxPlayerID_counter]);

                    auxPlayerID_counter = auxPlayerID_counter + 1 > 3 ? 0 : auxPlayerID_counter + 1;

                    auxPlayersCounter++;
                }

                extendedGameController.TurnScript.PlayerScore = auxScoreValuesOrder[0];
                extendedGameController.TurnScript.LeftAIScore = auxScoreValuesOrder[1];
                extendedGameController.TurnScript.TopAIScore = auxScoreValuesOrder[2];
                extendedGameController.TurnScript.RightAIScore = auxScoreValuesOrder[3];

            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                if (playerID == 0)
                {
                    extendedGameController.TurnScript.PlayerScore = scorePlayer_0;
                    extendedGameController.TurnScript.TopAIScore = scorePlayer_2; //In this case “scorePlayer_2” is the Top player.
                }
                else
                {
                    extendedGameController.TurnScript.PlayerScore = scorePlayer_2;
                    extendedGameController.TurnScript.TopAIScore = scorePlayer_0; //In this case “scorePlayer_0” is the Top player.
                }
            }

            ExtendedGameController.TurnScript.EndRound(gameOverCase);
        }

        public override int CalculateScore(Player player)
        {
            throw new System.NotImplementedException();
        }

        public override Player DetermineRoundWinner()
        {
            throw new System.NotImplementedException();
        }

        public override Player DetermineGameWinner()
        {
            throw new System.NotImplementedException();
        }

        public override void ResetGameMode()
        {
            throw new System.NotImplementedException();
        }

        public override int SelectTileFromBoneyard()
        {
            if (dominoTiles.Count > 0)
            {
                int rand = UnityEngine.Random.Range(0, dominoTiles.Count);

                int tileSelectedID = dominoTiles[rand];

                dominoTiles.RemoveAt(rand);

                // Update the boneyard count text after taking a tile
                UpdateBoneyardText();

                return tileSelectedID;
            }
            else
            {
                Debug.LogWarning("No tiles left in the boneyard to take.");

                return -1; // Indicating no tile can be taken
            }
        }

        public override BoneyardManager GetBoneyardManager()
        {
            return boneyardManager;
        }

        public override bool CheckRound_GameOver(ref string gameOverCase, bool gameIsBlocked = false)
        {
            Debug.Log("Checking if round is over...");

            /*if (gameIsBlocked)
            {
                gameOverCase = DeliverPointsAndValidateEliminated();
                //gameOverCase = ValideWinnerByTileCount();
                return true;
            }

            gameOverCase = CheckGameOver();

            Debug.Log("++--> Valide Winner: " + gameOverCase);

            if (gameOverCase != "")
            {
                gameOverCase = DeliverPointsAndValidateEliminated();
                return true;
            }*/

            // If the game is not over, return false
            return false;
        }


        /// <summary>
        /// Determines whether the match should be stopped based on whether any player has used all their tiles
        /// </summary>
        /// <returns>True if the match should be stopped; otherwise, false.</returns>
        public override bool RoundShouldBeStopped()
        {
            return false; // In Concentrate game mode, the match is not stopped based on players using all their tiles. The game continues until all possible combinations are made or the boneyard is empty.
        }

        public override void SetLocalPlayerHand(List<int> playerHand, int localPlayerID, bool playerEliminatedState, bool leftEliminatedState, bool topEliminatedState, bool rightEliminatedState, Action<int> callback, Action updateBoneyardText = null)
        {
            base.SetLocalPlayerHand(playerHand, localPlayerID, playerEliminatedState, leftEliminatedState, topEliminatedState, rightEliminatedState, callback, UpdateBoneyardText);
        }

        public override int PlayerAvalibleTilesInGameMode(int playerID)
        {
            switch (playerID)
            {
                case 0:
                    return extendedGameController.HandController.PlayerAvalibleTilesWithCustomID(playerID, handOfPlayer_0);
                case 1:
                    return extendedGameController.HandController.PlayerAvalibleTilesWithCustomID(playerID, handOfPlayer_1);
                case 2:
                    return extendedGameController.HandController.PlayerAvalibleTilesWithCustomID(playerID, handOfPlayer_2);
                case 3:
                    return extendedGameController.HandController.PlayerAvalibleTilesWithCustomID(playerID, handOfPlayer_3);
                default:
                    Debug.LogWarning($"Player ID {playerID} is not valid. Returning 0.");
                    return 0;
            }
        }

        [SerializeField] int playerPoints = 0;
        [SerializeField] int leftAIPoints = 0;
        [SerializeField] int topAIPoints = 0;
        [SerializeField] int rightAIPoints = 0;
        [SerializeField] int pointsTeam_1 = 0;
        [SerializeField] int pointsTeam_2 = 0;
        [SerializeField] string lastRoundWinner = "";

        private string DeliverPointsAndValidateEliminated()
        {
            Debug.Log("Delivering points...");

            playerPoints = 0;
            leftAIPoints = 0;
            topAIPoints = 0;
            rightAIPoints = 0;
            pointsTeam_1 = 0;
            pointsTeam_2 = 0;

            lastRoundWinner = "";

            int id_lastTilePlaced = tilesID_InShowBoard[tilesID_InShowBoard.Count - 1];

            Dictionary<int, int> Id_DoubleTiles = new Dictionary<int, int>
            {
                { 0, 0 }, //0 = 0/0
                { 1, 7 }, //7 = 1/1
                { 2, 13 }, //13 = 2/2
                { 3, 18 }, //18 = 3/3
                { 4, 22 }, //22 = 4/4
                { 5, 25 }, //25 = 5/5
                { 6, 27 } //27 = 6/6
            };

            int auxMultiplier = Id_DoubleTiles.ContainsValue(id_lastTilePlaced) ? 2 : 1;

            int eliminationLimitPoints = 100;

            List<Domino> playerDominoes = new List<Domino>();
            List<Domino> leftDominoes = new List<Domino>();
            List<Domino> topDominoes = new List<Domino>();
            List<Domino> rightDominoes = new List<Domino>();

            if (isSinglePlayerVsIA)
            {
                playerDominoes = extendedGameController.DeckScript.PlayerTiles.Select(tile => tile.GetDominoView().GetDomino()).ToList();
                leftDominoes = extendedGameController.DeckScript.LeftAITiles.Select(tile => tile.GetDominoView().GetDomino()).ToList();
                topDominoes = extendedGameController.DeckScript.TopAITiles.Select(tile => tile.GetDominoView().GetDomino()).ToList();
                rightDominoes = extendedGameController.DeckScript.RightAITiles.Select(tile => tile.GetDominoView().GetDomino()).ToList();
            }
            else
            {
                if (vsPlayerSelectedID == NumberPlayers.oneVsThree || vsPlayerSelectedID == NumberPlayers.twoVsTwo)
                {
                    playerDominoes = ConvertIdTilesListToDominoList(handOfPlayer_0); //Client host
                    leftDominoes = ConvertIdTilesListToDominoList(handOfPlayer_1);
                    topDominoes = ConvertIdTilesListToDominoList(handOfPlayer_2);
                    rightDominoes = ConvertIdTilesListToDominoList(handOfPlayer_3);
                }
                else if(vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    playerDominoes = ConvertIdTilesListToDominoList(handOfPlayer_0); //Client host
                    topDominoes = ConvertIdTilesListToDominoList(handOfPlayer_1);
                }
            }

            Dictionary<string, int> roundPoints = new Dictionary<string, int>();
            Dictionary<string, int> accumulatedPoints = new Dictionary<string, int>();

            if (vsPlayerSelectedID == NumberPlayers.oneVsThree)
            {
                if (!extendedGameController.TurnScript._playerIsEliminated)
                {
                    playerPoints = playerDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //playerPoints = extendedGameController.DeckScript.PlayerTiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.PlayerScore += playerPoints;

                    extendedGameController.TurnScript._playerIsEliminated = extendedGameController.TurnScript.PlayerScore >= eliminationLimitPoints;

                    roundPoints.Add("Player", playerPoints);
                    accumulatedPoints.Add("Player", extendedGameController.TurnScript.PlayerScore);
                }

                if (!extendedGameController.TurnScript._leftAIIsEliminated)
                {
                    leftAIPoints = leftDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //leftAIPoints = extendedGameController.DeckScript.LeftAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.LeftAIScore += leftAIPoints;

                    extendedGameController.TurnScript._leftAIIsEliminated = extendedGameController.TurnScript.LeftAIScore >= eliminationLimitPoints;

                    roundPoints.Add("Left", leftAIPoints);
                    accumulatedPoints.Add("Left", extendedGameController.TurnScript.LeftAIScore);
                }

                if (!extendedGameController.TurnScript._topAIIsEliminated)
                {
                    topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //topAIPoints = extendedGameController.DeckScript.TopAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.TopAIScore += topAIPoints;

                    extendedGameController.TurnScript._topAIIsEliminated = extendedGameController.TurnScript.TopAIScore >= eliminationLimitPoints;

                    roundPoints.Add("Top", topAIPoints);
                    accumulatedPoints.Add("Top", extendedGameController.TurnScript.TopAIScore);
                }

                if (!extendedGameController.TurnScript._rightAIIsEliminated)
                {
                    rightAIPoints = rightDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //rightAIPoints = extendedGameController.DeckScript.RightAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.RightAIScore += rightAIPoints;

                    extendedGameController.TurnScript._rightAIIsEliminated = extendedGameController.TurnScript.RightAIScore >= eliminationLimitPoints;

                    roundPoints.Add("Right", rightAIPoints);
                    accumulatedPoints.Add("Right", extendedGameController.TurnScript.RightAIScore);
                }
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                playerPoints = playerDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                /*playerPoints = extendedGameController.DeckScript.PlayerTiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;
                topAIPoints = extendedGameController.DeckScript.TopAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;*/

                extendedGameController.TurnScript.PlayerScore += playerPoints;
                extendedGameController.TurnScript.TopAIScore += topAIPoints;

                extendedGameController.TurnScript._playerIsEliminated = extendedGameController.TurnScript.PlayerScore >= eliminationLimitPoints;
                extendedGameController.TurnScript._topAIIsEliminated = extendedGameController.TurnScript.TopAIScore >= eliminationLimitPoints;

                roundPoints.Add("Player", playerPoints);
                roundPoints.Add("Top", topAIPoints);

                accumulatedPoints.Add("Player", extendedGameController.TurnScript.PlayerScore);
                accumulatedPoints.Add("Top", extendedGameController.TurnScript.TopAIScore);
            }
            else if (vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                playerPoints = playerDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                leftAIPoints = leftDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                rightAIPoints = rightDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;

                /*playerPoints = extendedGameController.DeckScript.PlayerTiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;
                leftAIPoints = extendedGameController.DeckScript.LeftAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;
                topAIPoints = extendedGameController.DeckScript.TopAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;
                rightAIPoints = extendedGameController.DeckScript.RightAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;*/

                extendedGameController.TurnScript.PlayerScore += playerPoints;
                extendedGameController.TurnScript.LeftAIScore += leftAIPoints;
                extendedGameController.TurnScript.TopAIScore += topAIPoints;
                extendedGameController.TurnScript.RightAIScore += rightAIPoints;

                pointsTeam_1 = extendedGameController.TurnScript.PlayerScore + extendedGameController.TurnScript.TopAIScore;
                pointsTeam_2 = extendedGameController.TurnScript.LeftAIScore + extendedGameController.TurnScript.RightAIScore;

                extendedGameController.TurnScript._playerIsEliminated = pointsTeam_1 >= eliminationLimitPoints;
                extendedGameController.TurnScript._leftAIIsEliminated = pointsTeam_1 >= eliminationLimitPoints;

                extendedGameController.TurnScript._topAIIsEliminated = pointsTeam_2 >= eliminationLimitPoints;
                extendedGameController.TurnScript._rightAIIsEliminated = pointsTeam_2 >= eliminationLimitPoints;

                roundPoints.Add("Player", playerPoints);
                roundPoints.Add("Left", leftAIPoints);
                roundPoints.Add("Top", topAIPoints);
                roundPoints.Add("Right", rightAIPoints);

                accumulatedPoints.Add("Points Team 1", pointsTeam_1);
                accumulatedPoints.Add("Points Team 2", pointsTeam_2);
            }

            Debug.Log($"+++Player Points: {playerPoints}, Left AI Points: {leftAIPoints}, Top AI Points: {topAIPoints}, Right AI Points: {rightAIPoints}");

            // Get the minimum value
            int minCountAccumulatedPoints = accumulatedPoints.Values.Min();
            int minCountRoundPoints = roundPoints.Values.Min();

            // Get all players that have the minimum value
            //var winners = allPoints.Where(p => p.Value == minCount).Select(p => p.Key).ToList();
            List<string> winners = accumulatedPoints.Where(p => p.Value < eliminationLimitPoints).Select(p => p.Key).ToList();

            extendedGameController.GameIsCompleteAndFinished = false;

            if (winners.Count > 0)
            {
                if (winners.Count > 1)
                {
                    winners = roundPoints.Where(p => p.Value == minCountRoundPoints).Select(p => p.Key).ToList();

                    // Display results
                    if (winners.Count == 1)
                    {
                        lastRoundWinner = winners[0];
                        return $"Round winner: {winners[0]}";
                    }
                    else
                    {
                        return $"Game Block. Tie between {string.Join(", ", winners)} with <b>{minCountRoundPoints}</b> points!";
                    }
                }
                else //If only one player is below the minimum score, that player is the winner of the entire game.
                {
                    lastRoundWinner = winners[0];

                    extendedGameController.GameIsCompleteAndFinished = true;

                    return $"Game winner: {winners[0]}";
                }
            }
            else //If all players have a score equal to or higher than the minimum score, then validate who has the lowest score to give the victory or declare a tie.
            {
                winners = accumulatedPoints.Where(p => p.Value == minCountAccumulatedPoints).Select(p => p.Key).ToList();

                // Display results
                if (winners.Count == 1)
                {
                    lastRoundWinner = winners[0];

                    extendedGameController.GameIsCompleteAndFinished = true;

                    return $"Game winner: {winners[0]}";
                }
                else
                {
                    extendedGameController.GameIsCompleteAndFinished = true;

                    return $"Game Block. Tie between {string.Join(", ", winners)} with <b>{minCountAccumulatedPoints}</b> points!";
                }
            }
        }

        private string CheckGameOver()
        {
            string gameOverCase = "";

            int playerTilesCounter = 0;
            int leftTilesCounter = 0;
            int topTilesCounter = 0;
            int rightTilesCounter = 0;

            if (isSinglePlayerVsIA)
            {
                playerTilesCounter = extendedGameController.DeckScript.PlayerTiles.Count;
                leftTilesCounter = extendedGameController.DeckScript.LeftAITiles.Count;
                topTilesCounter = extendedGameController.DeckScript.TopAITiles.Count;
                rightTilesCounter = extendedGameController.DeckScript.RightAITiles.Count;
            }
            else
            {
                if (vsPlayerSelectedID == NumberPlayers.oneVsThree || vsPlayerSelectedID == NumberPlayers.twoVsTwo)
                {
                    playerTilesCounter = handOfPlayer_0.Count;
                    leftTilesCounter = handOfPlayer_1.Count;
                    topTilesCounter = handOfPlayer_2.Count;
                    rightTilesCounter = handOfPlayer_3.Count;
                }
                else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    playerTilesCounter = handOfPlayer_0.Count;
                    topTilesCounter = handOfPlayer_1.Count;
                }
            }

            Debug.Log("Checking if game is over...");
            if (vsPlayerSelectedID == NumberPlayers.oneVsThree)
            {
                if (playerTilesCounter == 0 && !extendedGameController.TurnScript._playerIsEliminated)
                    gameOverCase = "Player win!";
                if (leftTilesCounter == 0 && !extendedGameController.TurnScript._leftAIIsEliminated)
                    gameOverCase = "Left AI win!";
                if (topTilesCounter == 0 && !extendedGameController.TurnScript._topAIIsEliminated)
                    gameOverCase = "Top AI win!";
                if (rightTilesCounter == 0 && !extendedGameController.TurnScript._rightAIIsEliminated)
                    gameOverCase = "Right AI win!";
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                if (playerTilesCounter == 0)
                    gameOverCase = "Player win!";
                if (topTilesCounter == 0)
                    gameOverCase = "Top AI win!";
            }
            else if (vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                if (playerTilesCounter + topTilesCounter == 0)
                    gameOverCase = "Player and Top AI win!";
                if (leftTilesCounter + rightTilesCounter == 0)
                    gameOverCase = "Left AI and Right AI win!";
            }

            return gameOverCase;
        }

        private string ValideWinnerByTileCount()
        {
            Debug.Log("Validate locked game winner...");

            string gameOverCase = "No Winner";

            if (vsPlayerSelectedID == NumberPlayers.oneVsThree)
            {
                int player_counter = extendedGameController.DeckScript.PlayerTiles.Count;
                int left_counter = extendedGameController.DeckScript.LeftAITiles.Count;
                int top_counter = extendedGameController.DeckScript.TopAITiles.Count;
                int right_counter = extendedGameController.DeckScript.RightAITiles.Count;

                // Store all the counters in a dictionary to simplify lookup
                Dictionary<string, int> playerCounters = new Dictionary<string, int>();
                /*{
                    { "Player", player_counter },
                    { "Left", left_counter },
                    { "Top", top_counter },
                    { "Right", right_counter }
                };*/

                if (!extendedGameController.TurnScript._playerIsEliminated)
                    playerCounters.Add("Player", player_counter);

                if (!extendedGameController.TurnScript._leftAIIsEliminated)
                    playerCounters.Add("Left", left_counter);

                if (!extendedGameController.TurnScript._topAIIsEliminated)
                    playerCounters.Add("Top", top_counter);

                if (!extendedGameController.TurnScript._rightAIIsEliminated)
                    playerCounters.Add("Right", right_counter);

                // Get the minimum value
                int minCount = playerCounters.Values.Min();

                // Get all players that have the minimum value
                var winners = playerCounters.Where(p => p.Value == minCount).Select(p => p.Key).ToList();

                // Display results
                if (winners.Count == 1)
                {
                    //Debug.Log($"El ganador es {winners[0]} con {minCount} fichas.");
                    gameOverCase = $"Game Block. {winners[0]} win with <b>{minCount}</b> tiles!";
                }
                else
                {
                    gameOverCase = $"Game Block. Tie between {string.Join(", ", winners)} with <b>{minCount}</b> tiles!";
                }
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                int player_counter = extendedGameController.DeckScript.PlayerTiles.Count;
                int top_counter = extendedGameController.DeckScript.TopAITiles.Count;

                // Store all the counters in a dictionary to simplify lookup
                Dictionary<string, int> playerCounters = new Dictionary<string, int>()
                {
                    { "Player", player_counter },
                    { "Top", top_counter }
                };

                // Get the minimum value
                int minCount = playerCounters.Values.Min();

                // Get all players that have the minimum value
                var winners = playerCounters.Where(p => p.Value == minCount).Select(p => p.Key).ToList();

                // Display results
                if (winners.Count == 1)
                {
                    //Debug.Log($"El ganador es {winners[0]} con {minCount} fichas.");
                    gameOverCase = $"Game Block. {winners[0]} win with <b>{minCount}</b> tiles!";
                }
                else
                {
                    gameOverCase = $"Game Block. Tie between {string.Join(", ", winners)} with <b>{minCount}</b> tiles!";
                }
            }
            else if (vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                if (extendedGameController.DeckScript.PlayerTiles.Count + extendedGameController.DeckScript.TopAITiles.Count == 0)
                    gameOverCase = "Player and Top AI win!";
                if (extendedGameController.DeckScript.LeftAITiles.Count + extendedGameController.DeckScript.RightAITiles.Count == 0)
                    gameOverCase = "Left AI and Right AI win!";

                int Team_1_counter = extendedGameController.DeckScript.PlayerTiles.Count + extendedGameController.DeckScript.TopAITiles.Count;
                int Team_2_counter = extendedGameController.DeckScript.LeftAITiles.Count + extendedGameController.DeckScript.RightAITiles.Count;

                // Store all the counters in a dictionary to simplify lookup
                Dictionary<string, int> teamCounters = new Dictionary<string, int>
                {
                    { "Team 1", Team_1_counter },
                    { "Team 2", Team_2_counter }
                };

                // Get the minimum value
                int minCount = teamCounters.Values.Min();

                // Get all players that have the minimum value
                var winners = teamCounters.Where(p => p.Value == minCount).Select(p => p.Key).ToList();

                // Display results
                if (winners.Count == 1)
                {
                    //Debug.Log($"El ganador es {winners[0]} con {minCount} fichas.");
                    gameOverCase = winners[0] + " winner with " + minCount + " tiles!";
                }
                else
                {
                    gameOverCase = "No winner tie between " + string.Join(", ", winners) + " with " + minCount + " tiles!";
                }
            }

            Debug.Log("+Result blocked game: " + gameOverCase);

            return gameOverCase;
        }

        public void OverrideTilesSkin(string skinID)
        {
            concentrateBoard_28?.OverrideTilesSkin(skinID);
            concentrateBoard_56?.OverrideTilesSkin(skinID);
        }


        #region Own functions
        /*public void TakeFromBoneyard()
        {
            if (isSinglePlayerVsIA)
            {
                extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().TakeTileFromBoneyard();
            }
            else
            {
                //OnPlayerTakesFromBoneyard?.Invoke();
                Debug.Log("Taking tile from boneyard for player turn, Onlyne.");
                //extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().TakeTileFromBoneyard();
            }

            // Update the boneyard count text after taking a tile
            UpdateBoneyardText();
        }*/

        /*public void UpdateBoneyardText()
        {
            // Update the boneyard count text after taking a tile
            if (boneyardCountText is not null && extendedGameController.DeckController is ExtendedDeckController extendedDeckController)
                boneyardCountText.text = extendedDeckController.GetTotalTilesInBoneyard().ToString();
        }*/

        #endregion
    }
}