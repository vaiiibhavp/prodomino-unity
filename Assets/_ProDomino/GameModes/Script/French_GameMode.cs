using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using ProDomino.ReplaySystem;
using ProDomino.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ProDomino.GameModes
{
    public class French_GameMode : AbstractGameMode
    {
        [SerializeField] CanvasGroup boneyardCanvasGroup = null;
        //[SerializeField] TMP_Text boneyardCountText = null;

        [SerializeField] BoneyardManager boneyardManager;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool debugToolsEnabled; // Enable debug tools only in editor or development builds
        protected bool IsDebugingTilesRightNow { get; set; }
#endif
        public string CurrentDisplayName => ScoreUI.SelectedScoreUI?.UserProfile?.Username;

        public bool AreAllFirstTileBranchesFilled { get; private set; }


        protected override void Awake()
        {
            base.Awake();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            debugToolsEnabled = false;
#endif
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsForcingShowTiles && debugToolsEnabled && !IsDebugingTilesRightNow || IsDebugingTilesRightNow)
            {
                IsDebugingTilesRightNow = true;
                dominoAI.DebugFrenchAIScoring_AI(ShowAIProbabilities);
            }

            if (!debugToolsEnabled && IsDebugingTilesRightNow)
            {
                IsDebugingTilesRightNow = false;
                HideAIProbabilities(true);
            }

            // If the AI is in turn, do not allow debugging to avoid interference
            if (ExtendedGameController.AIController.IsDropingTile)
                return;

            if (extendedGameController is not null and { isActiveAndEnabled: true }
                && GameModeID is GameMode.french
                && gameTypeSelectedID is GameType.singlePlayerIA
                && Input.GetKeyDown(KeyCode.T))
            {
                debugToolsEnabled = !debugToolsEnabled;
            }
#endif

            if (onGetStatusTimerInHost != null)
            {
                StatusTimerInHost auxStatusTimerInHost = onGetStatusTimerInHost.Invoke();

                switch (auxStatusTimerInHost)
                {
                    case StatusTimerInHost.waitingTurn:
                        extendedGameController.OnUpdateTimer_Externally(onGetRemainingTime.Invoke(), maxTimePerTurn);
                        break;
                    case StatusTimerInHost.waitingNextRound:
                        PostMatchResultController?.SetNexRoundText("Next Round (" + (int)onGetRemainingTime.Invoke() + "s)");
                        break;
                    case StatusTimerInHost.waitingRematch:
                        PostMatchResultController?.SetRematchText("Rematch (" + (int)onGetRemainingTime.Invoke() + "s)");
                        break;
                }

                /*if (auxIsTurnActive)
                {
                    //Debug.Log("+-+-+ onGetRemainingTime.Invoke(): " + onGetRemainingTime.Invoke());
                    extendedGameController.UpdateTimerUI_FromHost(onGetRemainingTime.Invoke(), maxTimePerTurn);
                    
                }*/
            }
        }

        public override void InitializeGameMode(GameModeConfig aux_gameModeConfig, int aux_Difficulty, GameType aux_GameTypeID, NumberPlayers aux_VsPlayerID, bool isSinglePlayerIA, ConcentrateNumberOfTiles auxConcentrateNumberOfTiles, Action openMenuSettingsAction, Func<bool> isTimeOut_MatchManager)
        {
            base.InitializeGameMode(aux_gameModeConfig, aux_Difficulty, aux_GameTypeID, aux_VsPlayerID, isSinglePlayerIA, auxConcentrateNumberOfTiles, openMenuSettingsAction, isTimeOut_MatchManager);

            limitPointsGameMode = aux_VsPlayerID == NumberPlayers.oneVsOne ? 50 : 100;
            ExtendedGameController.LimitPointsGameController = limitPointsGameMode;

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

            AreAllFirstTileBranchesFilled = false;

            UpdateBoneyardText();

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


        public override void SetupGameMode()
        {
            throw new System.NotImplementedException();
        }

        public override void SetupRandomHands()
        {
            replayTurnWhereHandsDelivered = true;

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

            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                int index = -1;

                for (int i = 0; i < 7; i++)
                {
                    //index = index != -1 ? random.Next(dominoTiles.Count) : dominoTiles.Count - 1;
                    index = random.Next(dominoTiles.Count);
                    handOfPlayer_0.Add(dominoTiles[index]);
                    dominoTiles.RemoveAt(index);

                    index = random.Next(dominoTiles.Count);
                    handOfPlayer_1.Add(dominoTiles[index]);
                    dominoTiles.RemoveAt(index);
                }
            }
            else
            {
                int index = -1;

                for (int i = 0; i < 7; i++)
                {
                    //index = index != -1 ? random.Next(dominoTiles.Count) : dominoTiles.Count - 1;
                    if (!extendedGameController.TurnScript._playerIsEliminated)
                    {
                        index = random.Next(dominoTiles.Count);
                        handOfPlayer_0.Add(dominoTiles[index]);
                        dominoTiles.RemoveAt(index);
                    }

                    if (!extendedGameController.TurnScript._leftAIIsEliminated)
                    {
                        index = random.Next(dominoTiles.Count);
                        handOfPlayer_1.Add(dominoTiles[index]);
                        dominoTiles.RemoveAt(index);
                    }

                    if (!extendedGameController.TurnScript._topAIIsEliminated)
                    {
                        index = random.Next(dominoTiles.Count);
                        handOfPlayer_2.Add(dominoTiles[index]);
                        dominoTiles.RemoveAt(index);
                    }

                    if (!extendedGameController.TurnScript._rightAIIsEliminated)
                    {
                        index = random.Next(dominoTiles.Count);
                        handOfPlayer_3.Add(dominoTiles[index]);
                        dominoTiles.RemoveAt(index);
                    }
                }
            }

            if (isSinglePlayerVsIA)
            {
                extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().DeckExtendedSetupRandomHands
                    (SelectPlayerWhoWillTakeFirstTurn,
                    UpdateBoneyardText,
                    handOfPlayer_0,
                    handOfPlayer_1,
                    handOfPlayer_2,
                    handOfPlayer_3);
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
                Debug.Log($"French: Boneyard domino tiles setted. Values: {string.Join(", ", boneyardDominoTiles)}");
            }

            // Initalize replay data if the game mode is not replay or concentrate
            if (gameModeID is not GameMode.replay and not GameMode.concentrate)
                InitializeReplayManager();
        }

        #region Validate Numbers Available In Branches

        //0 = 0/0
        //7 = 1/1
        //13 = 2/2
        //18 = 3/3
        //22 = 4/4
        //25 = 5/5
        //27 = 6/6
        //if (tileInfo.id == 0 || tileInfo.id == 7 || tileInfo.id == 13 || tileInfo.id == 18 || tileInfo.id == 22 || tileInfo.id == 25 || tileInfo.id == 27)
        public override void ValidateNumbersAvailableInBranches(ref int rightBranch, ref int leftBranch, ref int topNum, ref int downNum, ref List<int> doubleTilesEnabled, int firstPlacedTileId, Domino tileInfo = null)
        {
            //french_FirstPlacedTileId = firstPlacedTileId;

            int auxDefaultTile = 99;

            // Dictionary that maps each number to the required tile ID it needs to be considered valid
            Dictionary<int, int> doubleTilesCheck = new Dictionary<int, int>
            {
                { 0, 0 }, //0 = 0/0
                { 1, 7 }, //7 = 1/1
                { 2, 13 }, //13 = 2/2
                { 3, 18 }, //18 = 3/3
                { 4, 22 }, //22 = 4/4
                { 5, 25 }, //25 = 5/5
                { 6, 27 } //27 = 6/6
            };

            // Local function to validate a number based on the dictionary and the current tiles in the board
            int ValidateNum(int sideValue)
            {
                return doubleTilesCheck.ContainsKey(sideValue) && tilesID_InShowBoard.Contains(doubleTilesCheck[sideValue])
                    ? sideValue
                    : auxDefaultTile;
            }

            var slot = extendedGameController.SlotPosScript;

            rightBranch = slot.RightNum != -1 ? ValidateNum(slot.RightNum) : -1;
            leftBranch = slot.LeftNum != -1 ? ValidateNum(slot.LeftNum) : -1;
            topNum = slot.TopNum != -1 ? ValidateNum(slot.TopNum) : -1;
            downNum = slot.DownNum != -1 ? ValidateNum(slot.DownNum) : -1;

            // This is to make sure the first tile branches are filled
            if (!AreAllFirstTileBranchesFilled 
                && slot.RightTileId != firstPlacedTileId
                && slot.LeftTileId != firstPlacedTileId
                && slot.TopTileId != firstPlacedTileId
                && slot.DownTileId != firstPlacedTileId)
            {
                AreAllFirstTileBranchesFilled = true;
            }
            else if (!AreAllFirstTileBranchesFilled)
            {
                rightBranch = slot.RightTileId != firstPlacedTileId ? 99 : rightBranch;
                leftBranch = slot.LeftTileId != firstPlacedTileId ? 99 : leftBranch;
                topNum = slot.TopTileId != firstPlacedTileId ? 99 : topNum;
                downNum = slot.DownTileId != firstPlacedTileId ? 99 : downNum;
            }

            if (tileInfo != null) //Validates the case if it is a double tile when creating a slot to put the tile in
            {
                rightBranch = rightBranch == 99 && tileInfo.TopIndex == slot.RightNum && tileInfo.BottomIndex == slot.RightNum ? slot.RightNum : rightBranch;
                leftBranch = leftBranch == 99 && tileInfo.TopIndex == slot.LeftNum && tileInfo.BottomIndex == slot.LeftNum ? slot.LeftNum : leftBranch;
                topNum = topNum == 99 && tileInfo.TopIndex == slot.TopNum && tileInfo.BottomIndex == slot.TopNum ? slot.TopNum : topNum;
                downNum = downNum == 99 && tileInfo.TopIndex == slot.DownNum && tileInfo.BottomIndex == slot.DownNum ? slot.DownNum : downNum;
            }
            else
            {
                if (AreAllFirstTileBranchesFilled)
                {
                    doubleTilesEnabled.Add(slot.RightNum);
                    doubleTilesEnabled.Add(slot.LeftNum);
                    doubleTilesEnabled.Add(slot.TopNum);
                    doubleTilesEnabled.Add(slot.DownNum);
                }
            }
        }

        #endregion

        public override int SelectPlayerWhoWillTakeFirstTurn()
        {
            /*if (isSinglePlayerVsIA)
            {*/

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
                playerDominoes = ConvertIdTilesListToDominoList(handOfPlayer_0);
                leftDominoes = ConvertIdTilesListToDominoList(handOfPlayer_1);
                topDominoes = ConvertIdTilesListToDominoList(handOfPlayer_2);
                rightDominoes = ConvertIdTilesListToDominoList(handOfPlayer_3);
            }

            var tilesCollection = new Dictionary<byte, List<Domino>>()
            {
                { 0, playerDominoes }, // Player's tiles
                { 1, leftDominoes }, // Left AI's tiles
                { 2, topDominoes }, // Top AI's tiles
                { 3, rightDominoes } // Right AI's tiles
            };

            // Valdiate if there are any tiles in the collection
            if (tilesCollection is null or { Count: 0 })
            {
                Debug.LogWarning("No tiles found for any player.");
                return -1; // Indicating no player can take the first turn
            }

            // Check if any player has more than 5 double tiles
            if (tilesCollection.Any(x => x.Value.Where(x => x.IsDouble())?.Count() is >= 5))
            {
                Debug.Log("There is a player with more than 5 double tiles, reshuffling...");
                return -1;
            }

            // Get all drag handlers from the collection to make searching easier
            var allDragHandlers = tilesCollection.Values.SelectMany(x => x).ToList();

            // TODO: comented due rules preference, uncomment if needed
            // Search if any has the 0|0 tile
            //if (allDragHandlers.Any(x => x.GetDominoView().GetDomino().id is 0))
            //{ 
            //    var (index, collection) = tilesCollection.FirstOrDefault(x => x.Value.FirstOrDefault(x => x.GetDominoView().GetDomino().id is 0));
            //    return index;
            //}

            Debug.Log("---+++ RoundsCount: " + extendedGameController.TurnScript.RoundsCount
                + " / LastRoundWinner: " + lastRoundWinner);

            if (extendedGameController.TurnScript.RoundsCount > 0 && lastRoundWinner != "")
            {
                Debug.Log($"---+++ Last round winner: {lastRoundWinner}");
                Dictionary<string, byte> playersOrderId = new Dictionary<string, byte>()
                    {
                        { "Player", 0 }, // Player
                        { "Left", 1 }, // Left AI
                        { "Top", 2 }, // Top AI
                        { "Right", 3} // Right AI
                    };

                byte auxPlayerOrderId = playersOrderId[lastRoundWinner];

                Debug.Log($"---+++ AuxPlayerOrderId player: {auxPlayerOrderId}");

                int counter = 0;
                while (counter < 4)
                {
                    bool hasDouble = tilesCollection[auxPlayerOrderId]
                    .Any(d =>
                    {
                        Domino domino = d;
                        return domino.TopIndex == domino.BottomIndex;
                    });

                    if (hasDouble)
                    {
                        Debug.Log($"---+++ AuxPlayerOrderId Selected: {auxPlayerOrderId}");
                        return auxPlayerOrderId;
                    }

                    auxPlayerOrderId = (byte)((auxPlayerOrderId + 1) % playersOrderId.Count);

                    counter++;
                }
            }
            else
            {
                // If no player has the 0|0 tile, search for the greates tuple
                var greatestTuple = tilesCollection
                ?.Select(d => (index: d.Key, collection: d.Value))
                ?.SelectMany(d => d.collection.Select(y => (d.index, dragHander: y, domino: y)))
                ?.Where(d => d.domino.TopIndex == d.domino.BottomIndex)
                ?.OrderByDescending(d => d.domino.TopIndex + d.domino.BottomIndex)
                ?.FirstOrDefault();

                // If a greatest tuple is found, return its index
                if (greatestTuple.HasValue)
                    return greatestTuple.Value.index;
            }

            Debug.Log("There is no player with the 0|0 tile or any double tile to start the game. Reshuffling...");
            return -1;

            //extendedGameController.TurnScript.SetTurns(false, false, true, false);// Player, Left, Top, Right
            //extendedGameController.TurnScript.ValideEndTurnLogic("Game Started", 1);

            /*}
            else
            {
                Debug.Log("Selecting player who will take the first turn.");
                //return 0;
                return -1;
            }*/
        }

        public override void StartTurn(int playerTurnID, int localPlayerID)
        {
            boneyardCanvasGroup.interactable = false;
            extendedGameController.EnableTurnForPlayer(playerTurnID, localPlayerID);

            //isSelectedTileFromBoneyard = false;
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

                        isSelectedTileFromBoneyard = true;

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

            //isSelectedTileFromBoneyard = false;
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
                ExtendedGameController.HandController.StartPassButtonHighlight();
            }
        }

        public void SelectBoneyardTileForPlayer(Transform container)
        {
            isSelectedTileFromBoneyard = true;

            if (isSinglePlayerVsIA)
            {
                boneyardManager.Select_BoneyardTileForPlayer(container);
            }
            else
            {
                Debug.Log("++-- Selected Tile from boneytard and return Index");

                int auxIndex = boneyardManager.Select_BoneyardTileForPlayerAndReturnIndex(container);

                Debug.Log("++-- Boneyard selection Index: " + auxIndex);

                OnPlayerTakesFromBoneyard?.Invoke(auxIndex);
            }
        }

        #endregion

        public override void PassTurnBtn()
        {
            isSelectedTileFromBoneyard = false; //Reset variable

            if (isSinglePlayerVsIA)
                ExtendedGameController.AIController.PassTurn();
            else
            {
                ExtendedGameController.HandController.PlayerPassButton.SetButtonInteractable(false);
                onPassTurnHostAction?.Invoke(true);
            }

            if (gameModeID != GameMode.replay)
            {
                int auxCurrentTurnPlayerIndexId = extendedGameController.TurnScript.GetCurrentTurnControl() - 1;
                ReplayManager.Instance.SetTurnAction_Pass(auxCurrentTurnPlayerIndexId);
            }

            SoundManager.Instance.PlaySFX(IDAudioClip.passTurn);

            // Stop highlight of the pass button
            ExtendedGameController.HandController.StopPassButtonHighlight();
        }

        public override void UpdateScoreAndShowRoundResult
            (string gameOverCase, int scorePlayer_0, int scorePlayer_1, int scorePlayer_2, int scorePlayer_3,
            int playerID,
            bool auxGameIsCompleteAndFinished,
            string auxPlayerWinner)
        {
            extendedGameController.GameIsCompleteAndFinished = auxGameIsCompleteAndFinished;
            extendedGameController.PlayerWinner = auxPlayerWinner;

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

                if (playerID != 0) //Points earned in the round are calculated only for customers other than the host, as the host calculates them beforehand.
                {
                    extendedGameController.playerRoundScore = auxScoreValuesOrder[0] - extendedGameController.TurnScript.PlayerScore;
                    extendedGameController.leftRoundScore = auxScoreValuesOrder[1] - extendedGameController.TurnScript.LeftAIScore;
                    extendedGameController.topRoundScore = auxScoreValuesOrder[2] - extendedGameController.TurnScript.TopAIScore;
                    extendedGameController.rightRoundScore = auxScoreValuesOrder[3] - extendedGameController.TurnScript.RightAIScore;
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
                    //Points earned in the round are calculated only for customers other than the host, as the host calculates them beforehand.
                    extendedGameController.playerRoundScore = scorePlayer_2 - extendedGameController.TurnScript.PlayerScore;
                    extendedGameController.topRoundScore = scorePlayer_0 - extendedGameController.TurnScript.TopAIScore;

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

            // Clear the colors of all tiles in the player's hand
            ClearPlayerHandGlow();

            if (gameIsBlocked)
            {
                gameOverCase = DeliverPointsAndValidateEliminated();
                //gameOverCase = ValideWinnerByTileCount();

                if(gameModeID != GameMode.replay)
                {
                    SetResultToTurn(TurnResultReplay.gameblocked);

                    if (!extendedGameController.GameIsCompleteAndFinished)
                    {
                        CreateReplayTurn();
                    }
                    else
                    {
                        //ReplayManager.Instance.SaveReplayData();
                    }   
                }

                return true;
            }

            gameOverCase = CheckGameOver();
            if (gameOverCase != "")
            {
                gameOverCase = DeliverPointsAndValidateEliminated();

                if(gameModeID != GameMode.replay)
                {
                    SetResultToTurn(TurnResultReplay.gameOver);

                    if (!extendedGameController.GameIsCompleteAndFinished)
                    {
                        CreateReplayTurn();
                    }
                    else
                    {
                        //ReplayManager.Instance.SaveReplayData();
                    }   
                }

                return true;
            }

            if (!replayTurnWhereHandsDelivered && !replayTurnWhereTakeFromBoneyard && gameModeID != GameMode.replay)
            {
                SetResultToTurn(TurnResultReplay.none);

                CreateReplayTurn();
            }

            if (replayTurnWhereHandsDelivered)
                replayTurnWhereHandsDelivered = false;
                
            if(replayTurnWhereTakeFromBoneyard)
                replayTurnWhereTakeFromBoneyard = false;

            // If the game is not over, return false
            return false;
        }

        /// <summary>
        /// Determines whether the match should be stopped based on whether any player has used all their tiles
        /// </summary>
        /// <returns>True if the match should be stopped; otherwise, false.</returns>
        public override bool RoundShouldBeStopped()
        {
            // Check if any player has used all their tiles, which indicates the end of the match
            var gameOver = CheckGameOver();
            return gameOver != "";
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

            extendedGameController.playerRoundScore = extendedGameController.TurnScript.PlayerScore;
            extendedGameController.leftRoundScore = extendedGameController.TurnScript.LeftAIScore;
            extendedGameController.topRoundScore = extendedGameController.TurnScript.TopAIScore;
            extendedGameController.rightRoundScore = extendedGameController.TurnScript.RightAIScore;

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

            limitPointsGameMode = VSPlayerSelectedID == NumberPlayers.oneVsOne ? 50 : 100;

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

                    extendedGameController.TurnScript._playerIsEliminated = extendedGameController.TurnScript.PlayerScore >= limitPointsGameMode;

                    roundPoints.Add("Player", playerPoints);
                    accumulatedPoints.Add("Player", extendedGameController.TurnScript.PlayerScore);
                }

                if (!extendedGameController.TurnScript._leftAIIsEliminated)
                {
                    leftAIPoints = leftDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //leftAIPoints = extendedGameController.DeckScript.LeftAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.LeftAIScore += leftAIPoints;

                    extendedGameController.TurnScript._leftAIIsEliminated = extendedGameController.TurnScript.LeftAIScore >= limitPointsGameMode;

                    roundPoints.Add("Left", leftAIPoints);
                    accumulatedPoints.Add("Left", extendedGameController.TurnScript.LeftAIScore);
                }

                if (!extendedGameController.TurnScript._topAIIsEliminated)
                {
                    topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //topAIPoints = extendedGameController.DeckScript.TopAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.TopAIScore += topAIPoints;

                    extendedGameController.TurnScript._topAIIsEliminated = extendedGameController.TurnScript.TopAIScore >= limitPointsGameMode;

                    roundPoints.Add("Top", topAIPoints);
                    accumulatedPoints.Add("Top", extendedGameController.TurnScript.TopAIScore);
                }

                if (!extendedGameController.TurnScript._rightAIIsEliminated)
                {
                    rightAIPoints = rightDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    //rightAIPoints = extendedGameController.DeckScript.RightAITiles.Sum(tile => tile.GetDominoView().GetDomino().TopIndex + tile.GetDominoView().GetDomino().BottomIndex) * auxMultiplier;

                    extendedGameController.TurnScript.RightAIScore += rightAIPoints;

                    extendedGameController.TurnScript._rightAIIsEliminated = extendedGameController.TurnScript.RightAIScore >= limitPointsGameMode;

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

                extendedGameController.TurnScript._playerIsEliminated = extendedGameController.TurnScript.PlayerScore >= limitPointsGameMode;
                extendedGameController.TurnScript._topAIIsEliminated = extendedGameController.TurnScript.TopAIScore >= limitPointsGameMode;

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

                extendedGameController.TurnScript._playerIsEliminated = pointsTeam_1 >= limitPointsGameMode;
                extendedGameController.TurnScript._leftAIIsEliminated = pointsTeam_1 >= limitPointsGameMode;

                extendedGameController.TurnScript._topAIIsEliminated = pointsTeam_2 >= limitPointsGameMode;
                extendedGameController.TurnScript._rightAIIsEliminated = pointsTeam_2 >= limitPointsGameMode;

                roundPoints.Add("Player", playerPoints);
                roundPoints.Add("Left", leftAIPoints);
                roundPoints.Add("Top", topAIPoints);
                roundPoints.Add("Right", rightAIPoints);

                accumulatedPoints.Add("Team A", pointsTeam_1);
                accumulatedPoints.Add("Team B", pointsTeam_2);
            }

            Debug.Log($"+++Player Points: {playerPoints}, Left AI Points: {leftAIPoints}, Top AI Points: {topAIPoints}, Right AI Points: {rightAIPoints}");

            // Get the minimum value
            int minCountAccumulatedPoints = accumulatedPoints.Values.Min();
            int minCountRoundPoints = roundPoints.Values.Min();

            // Get all players that have the minimum value
            var winners = accumulatedPoints.Where(p => p.Value < limitPointsGameMode).Select(p => p.Key).ToList();

            extendedGameController.GameIsCompleteAndFinished = false;

            extendedGameController.playerRoundScore = extendedGameController.TurnScript.PlayerScore - extendedGameController.playerRoundScore;
            extendedGameController.leftRoundScore = extendedGameController.TurnScript.LeftAIScore - extendedGameController.leftRoundScore;
            extendedGameController.topRoundScore = extendedGameController.TurnScript.TopAIScore - extendedGameController.topRoundScore;
            extendedGameController.rightRoundScore = extendedGameController.TurnScript.RightAIScore - extendedGameController.rightRoundScore;

            //**REPLAY:
            List<int> auxRoundPlayerScores = new List<int>();
            List<int> auxCumulatePlayerScores = new List<int>();

            Dictionary<string, int> playersIndexId = new Dictionary<string, int>();

            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                auxRoundPlayerScores.Add(extendedGameController.playerRoundScore);
                auxRoundPlayerScores.Add(extendedGameController.topRoundScore);

                auxCumulatePlayerScores.Add(extendedGameController.TurnScript.PlayerScore);
                auxCumulatePlayerScores.Add(extendedGameController.TurnScript.TopAIScore);

                playersIndexId.Add("Player", 0);
                playersIndexId.Add("Top", 1);
            }
            else
            {
                auxRoundPlayerScores.Add(extendedGameController.playerRoundScore);
                auxRoundPlayerScores.Add(extendedGameController.leftRoundScore);
                auxRoundPlayerScores.Add(extendedGameController.topRoundScore);
                auxRoundPlayerScores.Add(extendedGameController.rightRoundScore);

                auxCumulatePlayerScores.Add(extendedGameController.TurnScript.PlayerScore);
                auxCumulatePlayerScores.Add(extendedGameController.TurnScript.LeftAIScore);
                auxCumulatePlayerScores.Add(extendedGameController.TurnScript.TopAIScore);
                auxCumulatePlayerScores.Add(extendedGameController.TurnScript.RightAIScore);

                playersIndexId.Add("Player", 0);
                playersIndexId.Add("Left", 1);
                playersIndexId.Add("Top", 2);
                playersIndexId.Add("Right", 3);
            }
            //*******

            // Check if in this round there are players (or team) with a score below the minimum score to validate if there is an immediate winner or a tie, without needing to check the accumulated points.
            if (winners.Count > 0)
            {
                // If there are more than one player (or team) with a score below the minimum score, then validate who has the lowest score in the round to give the victory of the round or declare a tie, without needing to check the accumulated points.
                if (winners.Count > 1)
                {
                    // Get the minimum value of round points among the players (or teams) that are below the minimum score
                    winners = roundPoints.Where(p => p.Value == minCountRoundPoints).Select(p => p.Key).ToList();

                    // If there are a only one player (or team) with the lowest score in the round among those who are below the minimum score, that player is the winner of the round.
                    if (winners.Count == 1)
                    {
                        var winner = winners[0];

                        // If the victory is for a team in a 2 vs 2 match, get the player with the lowest score in the round among the members of the winning team to declare them as the winner of the game, instead of declaring the entire team as the winner of the game.
                        // This is done to give more relevance to the points obtained in each round and not only to the accumulated points, so even if a team wins the game, it can be relevant for players to know which member of the team performed better in the last round.
                        if (VSPlayerSelectedID is NumberPlayers.twoVsTwo && winner.StartsWith("Team"))
                        {
                            var teamMembers = winner == "Team 1" ? new List<string> { "Player", "Top" } : new List<string> { "Left", "Right" };

                            // Get the team member with the lowest score in the round to declare them as the winner of the round, even if the victory of the game is for the team.
                            winner = roundPoints.Where(p => teamMembers.Contains(p.Key)).OrderBy(p => p.Value).FirstOrDefault().Key;
                        }

                        lastRoundWinner = winner;
                        extendedGameController.PlayerWinner = winner;

                        if (gameModeID != GameMode.replay)
                            ReplayManager.Instance.SetTurnScores(auxRoundPlayerScores, auxCumulatePlayerScores, isEndGame: false, auxRoundWinnerPlayerIndexId: playersIndexId[lastRoundWinner]);   

                        return $"Round winner: {winners[0]}";
                    }

                    // Otherwise, if there are more than one player (or team) with the lowest score in the round among those who are below the minimum score, then it's a tie of the round.
                    else
                    {
                        if (gameModeID != GameMode.replay)
                            ReplayManager.Instance.SetTurnIsTie(true);   

                        extendedGameController.PlayerWinner = "draw";

                        return $"Game Block. Tie between {string.Join(", ", winners)} with <b>{minCountRoundPoints}</b> points!";
                    }
                }
                
                // If only one player (or team) is below the minimum score, that player is the winner of the entire game.
                else 
                {
                    lastRoundWinner = winners[0];
                    extendedGameController.PlayerWinner = winners[0];

                    extendedGameController.GameIsCompleteAndFinished = true;

                    if (gameModeID != GameMode.replay)
                        ReplayManager.Instance.SetTurnScores(auxRoundPlayerScores, auxCumulatePlayerScores, isEndGame: true, auxRoundWinnerPlayerIndexId: playersIndexId[lastRoundWinner]);   

                    return $"Game winner: {winners[0]}";
                }
            }
            
            // If all players (or teams) have a score equal to or higher than the limit score, then validate who has the lowest score to give the victory or declare a tie.
            else 
            {
                winners = accumulatedPoints.Where(p => p.Value == minCountAccumulatedPoints).Select(p => p.Key).ToList();

                // Display results
                if (winners.Count == 1)
                {
                    var winner = winners[0];

                    // If the victory is for a team in a 2 vs 2 match, get the player with the lowest score in the round among the members of the winning team to declare them as the winner of the game, instead of declaring the entire team as the winner of the game.
                    // This is done to give more relevance to the points obtained in each round and not only to the accumulated points, so even if a team wins the game, it can be relevant for players to know which member of the team performed better in the last round.
                    if (VSPlayerSelectedID is NumberPlayers.twoVsTwo && winner.StartsWith("Team"))
                    {
                        var teamMembers = winner is "Team A" ? new List<string> { "Player", "Top" } : new List<string> { "Left", "Right" };

                        // Get the team member with the lowest score in the round to declare them as the winner of the round, even if the victory of the game is for the team.
                        winner = roundPoints.Where(p => teamMembers.Contains(p.Key)).OrderBy(p => p.Value).FirstOrDefault().Key;
                    }

                    lastRoundWinner = winner;
                    extendedGameController.PlayerWinner = winner;
                    extendedGameController.GameIsCompleteAndFinished = true;

                    if (gameModeID != GameMode.replay)
                        ReplayManager.Instance.SetTurnScores(auxRoundPlayerScores, auxCumulatePlayerScores, isEndGame: true, auxRoundWinnerPlayerIndexId: playersIndexId[lastRoundWinner]);   

                    return $"Game winner: {winners[0]}";
                }
                else
                {
                    extendedGameController.PlayerWinner = "draw";
                    extendedGameController.GameIsCompleteAndFinished = true;

                    if (gameModeID != GameMode.replay)
                        ReplayManager.Instance.SetTurnIsTie(true);   

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
                if (playerTilesCounter is 0 || topTilesCounter is 0)
                    gameOverCase = "Player and Top AI win!";
                if (leftTilesCounter is 0 || rightTilesCounter is 0)
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
