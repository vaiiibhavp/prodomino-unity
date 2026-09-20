using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DominoTemplate.AI;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using ProDomino.ReplaySystem;
using ProDomino.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ProDomino.GameModes
{
    public class ReplayGameMode : AbstractGameMode
    {
        [SerializeField] private TurnData currentTurnData;
        [SerializeField] private int localPlayerIndexId;
        [SerializeField] CanvasGroup boneyardCanvasGroup = null;
        //[SerializeField] TMP_Text boneyardCountText = null;

        [SerializeField] BoneyardManager boneyardManager;

        [SerializeField] private GameModeConfig _gameModeConfig = null;

        [SerializeField] private GameObject lockPanel;

        private MatchReplay currentReplay;

        #region replay mode

        private GameMode auxGameMode_id;

        public void inicializeReplayGameMode(GameModeConfig auxGameModeConfig, GameMode auxGameModeID, GameType gameTypeID, NumberPlayers vsPlayerID, List<PlayerInfoReplay> playersInfo, List<int> cumulatePlayerScores, int auxLocalPlayerIndexId)
        {
            _gameModeConfig = auxGameModeConfig;

            localPlayerIndexId = auxLocalPlayerIndexId;

            //currentTurnData = auxCurrentTurnData;

            //extendedGameController.GameModeSelectedID = currentTurnData.numberPlayers == 2 ? NumberPlayers.oneVsOne : NumberPlayers.twoVsTwo;

            //base.InitializeGameMode(aux_gameModeConfig, aux_Difficulty, aux_GameTypeID, aux_VsPlayerID, isSinglePlayerIA, auxConcentrateNumberOfTiles);

            /*isSinglePlayerVsIA = true;

            gameModeID = auxGameModeID;
            gameTypeSelectedID = gameTypeID;
            vsPlayerSelectedID = vsPlayerID;*/

            //InitializeGameMode(_gameModeConfig, 1, gameTypeSelectedID, vsPlayerSelectedID, isSinglePlayerIA: true, ConcentrateNumberOfTiles.none);
            gameModeID = GameMode.replay; //auxGameModeID;

            auxGameMode_id = auxGameModeID;

            InitializeGameMode(_gameModeConfig, 1, gameTypeID, vsPlayerID, isSinglePlayerIA: true, ConcentrateNumberOfTiles.none, openMenuSettingsAction: () => auxGameModeConfig?.MenuControllerGameMode?.InGameMenuActivate(), () => false);
            RestartGame(restartNewRound: true);
            //RestartGame();
            //SetupRandomHands();

            //extendedGameController.StopTimebars();

            foreach (int aux in cumulatePlayerScores)
            {
                Debug.Log("cumulatePlayerScores: " + aux);
            }

            if (vsPlayerID == NumberPlayers.oneVsOne)//if (vsPlayerID == NumberPlayers.oneVsOne)
            {
                dataInfoPlayer_0 = new PlayerDataInfo(0, "userId_0", playersInfo[0].playerName, "default", "classic", "classic", "default", "", "bronze", cumulatePlayerScores[0], false);
                dataInfoPlayer_1 = new PlayerDataInfo(1, "userId_1", playersInfo[1].playerName, "default", "classic", "classic", "default", "", "bronze", cumulatePlayerScores[1], false);
                dataInfoPlayer_2 = new PlayerDataInfo(2, "userId_2", "none", "default", "classic", "classic", "default", "", "bronze", 0, false);
                dataInfoPlayer_3 = new PlayerDataInfo(3, "userId_3", "none", "default", "classic", "classic", "default", "", "bronze", 0, false);

                extendedGameController.TurnScript.PlayerScore = cumulatePlayerScores[0];
                extendedGameController.TurnScript.LeftAIScore = 0;
                extendedGameController.TurnScript.TopAIScore = cumulatePlayerScores[1];
                extendedGameController.TurnScript.RightAIScore = 0;
            } else
            {
                dataInfoPlayer_0 = new PlayerDataInfo(0, "userId_0", playersInfo[0].playerName, "default", "classic", "classic", "default", "", "bronze", cumulatePlayerScores[0], false);
                dataInfoPlayer_1 = new PlayerDataInfo(1, "userId_1", playersInfo[1].playerName, "default", "classic", "classic", "default", "", "bronze", cumulatePlayerScores[1], false);
                dataInfoPlayer_2 = new PlayerDataInfo(2, "userId_2", playersInfo[2].playerName, "default", "classic", "classic", "default", "", "bronze", cumulatePlayerScores[2], false);
                dataInfoPlayer_3 = new PlayerDataInfo(3, "userId_3", playersInfo[3].playerName, "default", "classic", "classic", "default", "", "bronze", cumulatePlayerScores[3], false);

                extendedGameController.TurnScript.PlayerScore = cumulatePlayerScores[0];
                extendedGameController.TurnScript.LeftAIScore = cumulatePlayerScores[1];
                extendedGameController.TurnScript.TopAIScore = cumulatePlayerScores[2];
                extendedGameController.TurnScript.RightAIScore = cumulatePlayerScores[3];
            }

            ConfigurePlayersUI();
            extendedGameController.UpdateScoreUI();

            if (!lockPanel.activeSelf)
                lockPanel.SetActive(true);
        }

        /*public void StopAllCoroutinesOnThisObjectAndChildren()
        {
            // Obtiene todos los scripts MonoBehaviour en este objeto y sus hijos (activos e inactivos)
            MonoBehaviour[] scripts = GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour script in scripts)
            {
                if (script != null)
                {
                    script.StopAllCoroutines();
                }
            }
        }*/

        public bool instantExecution = false;
        public bool pauseExecution = false;
        public bool isExecution = false;
        public TMP_Text aiHintText;

        public void SetCurrentTurnData(TurnData auxCurrentTurnData, TMP_Text auxAiHintText, bool instantTurn = false)
        {
            aiHintText = auxAiHintText;

            currentTurnData = auxCurrentTurnData;

            if (!instantTurn)
            {
                instantExecution = false;
                StartCoroutine(ExecuteTurnAction(auxCurrentTurnData));
            } else
            {
                instantExecution = true;
                //StartCoroutine(ExecuteInstantTurnAction(auxCurrentTurnData));
                StartCoroutine(ExecuteTurnAction(auxCurrentTurnData));
            }
        }

        private IEnumerator ExecuteTurnAction(TurnData auxCurrentTurnData)
        {
            isExecution = true;

            if (auxCurrentTurnData.dealHands)
            {
                Debug.Log("++--** Repartir Fichas");
                RestartGame(restartNewRound: true);
                SetupHands(auxCurrentTurnData);

                aiHintText.text = "Handing over the hands of the round";
            } 
            else
            {
                extendedGameController.Replay_OnTurnStart(auxCurrentTurnData.playerIndexId + 1);

                List<DragHandler> tilesList = new List<DragHandler>();
                string auxPlayerPlacingTile = "";
                string auxPlayerName = "";

                if (auxCurrentTurnData.playerIndexId == localPlayerIndexId)
                {
                    extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
                    auxPlayerPlacingTile = "player";
                    auxPlayerName = DataInfoPlayer_0.username;
                    tilesList = extendedGameController.DeckScript.PlayerTiles;
                } else
                {
                    switch (auxCurrentTurnData.playerIndexId)
                    {
                        case 1:
                            extendedGameController.TurnScript.SetTurns(false, true, false, false); //player, left, top, right
                            auxPlayerPlacingTile = "leftPlayer";
                            auxPlayerName = DataInfoPlayer_1.username;
                            tilesList = extendedGameController.DeckScript.LeftAITiles;
                            break;
                        case 2:
                            extendedGameController.TurnScript.SetTurns(false, false, true, false); //player, left, top, right
                            auxPlayerPlacingTile = "topPlayer";
                            auxPlayerName = DataInfoPlayer_2.username;
                            tilesList = extendedGameController.DeckScript.TopAITiles;
                            break;
                        case 3:
                            extendedGameController.TurnScript.SetTurns(false, false, false, true); //player, left, top, right
                            auxPlayerPlacingTile = "rightPlayer";
                            auxPlayerName = DataInfoPlayer_3.username;
                            tilesList = extendedGameController.DeckScript.RightAITiles;
                            break;
                        default:
                            Debug.LogError("Invalid playerIndexId in replay turn data.");
                            break;
                    }
                }

                switch (auxCurrentTurnData.turnAction) //ACTION CASES
                {
                    case TurnActionReplay.play:
                        DragHandler selectedTile = GetDragHandlerFromTileId(tilesList, auxCurrentTurnData.playedPiece.tileId);
                        var domino = selectedTile?.GetDominoView()?.GetDomino();

                        if (domino is null)
                        {
                            Debug.LogError("Selected tile is null for tileId: " + auxCurrentTurnData.playedPiece.tileId);
                            break;
                        }

                        aiHintText.text = "Player: " + auxPlayerName;
                        aiHintText.text += "\nDrew the " + domino?.TopIndex + "/" + domino?.BottomIndex + " tile";

                        //yield return new WaitForSeconds(1f);
                        float start = Time.time;
                        float duration = 1f;

                        yield return new WaitUntil(() => !pauseExecution && (instantExecution || Time.time >= start + duration));
                        yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTile, auxCurrentTurnData.playedPiece.sideInfo, auxPlayerPlacingTile, placingInstant: instantExecution/*, owner*/)); //right, left, top, down / leftPlayer, topPlayer, rightPlayer, player

                        UpdataReplayScores(auxCurrentTurnData);
                        break;

                    case TurnActionReplay.takeBoneyard:
                        Debug.Log("Replay Action: takeBoneyard");

                        if (!instantExecution)
                            boneyardManager.ShowBoneyard();

                        //yield return new WaitForSeconds(1f);
                        float takeBoneyardStartTimer = Time.time;
                        float takeBoneyardDuration = 1f;

                        yield return new WaitUntil(() => !pauseExecution && (instantExecution || Time.time >= takeBoneyardStartTimer + takeBoneyardDuration));

                        foreach (int aux in auxCurrentTurnData.tilesTakenFromBoneyard)
                        {
                            DragHandler selectedBoneyardTile = GetDragHandlerFromTileId(extendedGameController.DeckScript.DominoTiles, aux);
                            var dominoBoneyard = selectedBoneyardTile?.GetDominoView()?.GetDomino();

                            boneyardManager.Select_BoneyardTileForPlayer(selectedBoneyardTile.DragObject.parent.transform, instantMove: instantExecution);

                            aiHintText.text = "Player: " + auxPlayerName;
                            aiHintText.text += "\nTake " + dominoBoneyard?.TopIndex + "/" + dominoBoneyard?.BottomIndex + " from the boneyard";

                            //yield return new WaitForSeconds(1f);
                            takeBoneyardStartTimer = Time.time;
                            takeBoneyardDuration = 1f;
                            yield return new WaitUntil(() => !pauseExecution && (instantExecution || Time.time >= takeBoneyardStartTimer + takeBoneyardDuration));
                        }

                        boneyardManager.HideBoneyard();

                        //yield return new WaitForSeconds(4f);

                        //if (auxCurrentTurnData.playedPiece != null)
                        if (auxCurrentTurnData.playedPiece.sideInfo != "")
                        {
                            DragHandler selectedTileBoneyard = GetDragHandlerFromTileId(tilesList, auxCurrentTurnData.playedPiece.tileId);
                            var dominoBoneyard = selectedTileBoneyard?.GetDominoView()?.GetDomino();

                            if (!selectedTileBoneyard)
                            {
                                Debug.Log("Es Null id: " + auxCurrentTurnData.playedPiece.tileId);
                            } else
                            {
                                Debug.Log("No Es Null id" + auxCurrentTurnData.playedPiece.tileId);
                            }

                            aiHintText.text = "Player: " + auxPlayerName;
                            aiHintText.text += "\nDrew the " + dominoBoneyard?.TopIndex + "/" + dominoBoneyard?.BottomIndex + " tile";

                            yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTileBoneyard, auxCurrentTurnData.playedPiece.sideInfo, auxPlayerPlacingTile, placingInstant: instantExecution));
                        } else
                        {
                            float auxWaitingTime = !instantExecution ? 2f : 0;
                            StartCoroutine(ExtendedGameController.GameTurnController.SimpleAlertText("Player Pass Turn", auxWaitingTime));

                            ExtendedGameController.ActivatePrompt("Pass!", 2);
                            aiHintText.text += "\nPass turn";

                            //yield return new WaitForSeconds(1f);
                            takeBoneyardStartTimer = Time.time;
                            takeBoneyardDuration = 1f;
                            yield return new WaitUntil(() => !pauseExecution && (instantExecution || Time.time >= takeBoneyardStartTimer + takeBoneyardDuration));

                            ExtendedGameController.AIController.PassTurn();
                        }

                        //StartCoroutine(OpenBoneyard());
                        UpdataReplayScores(auxCurrentTurnData);
                        //ValideTurnResult(auxCurrentTurnData.turnResult);
                        break;
                    case TurnActionReplay.pass:
                        Debug.Log("Replay Action: pass");

                        float auxWaitingTimePass = !instantExecution ? 2f : 0;
                        StartCoroutine(ExtendedGameController.GameTurnController.SimpleAlertText("Player Pass Turn", auxWaitingTimePass));

                        ExtendedGameController.ActivatePrompt("Pass!", 2);

                        aiHintText.text = "Player: " + auxPlayerName;
                        aiHintText.text += "\nPass turn";

                        //yield return new WaitForSeconds(1f);
                        float passStartTimer = Time.time;
                        float passDuration = 1f;

                        yield return new WaitUntil(() => !pauseExecution && (instantExecution || Time.time >= passStartTimer + passDuration));

                        ExtendedGameController.AIController.PassTurn();

                        UpdataReplayScores(auxCurrentTurnData);
                        //ValideTurnResult(auxCurrentTurnData.turnResult);
                        break;
                    default:
                        aiHintText.text = "";
                        Debug.Log("Replay Action: none");
                        break;
                }

                switch (auxCurrentTurnData.turnResult) //RESULT TURN CASES
                {
                    case TurnResultReplay.gameOver:

                        auxPlayerName = "";

                        if (!auxCurrentTurnData.isTie)
                        {
                            if (localPlayerIndexId == auxCurrentTurnData.roundWinnerPlayerIndexId)
                            {
                                extendedGameController.PlayerWinner = "Player";
                                auxPlayerName = DataInfoPlayer_0.username;
                            } else
                            {
                                switch (auxCurrentTurnData.roundWinnerPlayerIndexId)
                                {
                                    case 1:
                                        extendedGameController.PlayerWinner = "Left";
                                        auxPlayerName = DataInfoPlayer_1.username;
                                        break;
                                    case 2:
                                        extendedGameController.PlayerWinner = "Top";
                                        auxPlayerName = DataInfoPlayer_2.username;
                                        break;
                                    case 3:
                                        extendedGameController.PlayerWinner = "Right";
                                        auxPlayerName = DataInfoPlayer_3.username;
                                        break;
                                    default:
                                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                                        break;
                                }
                            }

                            aiHintText.text = "Player: " + auxPlayerName;
                        } else
                        {
                            extendedGameController.PlayerWinner = "draw"; //"noWin"
                        }

                        if (auxCurrentTurnData.isEndGame && !auxCurrentTurnData.isTie)
                        {
                            aiHintText.text += "\nWinner Game";
                        } else if (!auxCurrentTurnData.isTie)
                        {
                            aiHintText.text += "\nWinner Round";
                        } else if (auxCurrentTurnData.isTie)
                        {
                            aiHintText.text = "Game is draw";
                        }

                        extendedGameController.GameIsCompleteAndFinished = auxCurrentTurnData.isEndGame;
                        extendedGameController.GameTurnController.OnRoundOverEvent?.Invoke();
                        //yield return new WaitForSeconds(2f);
                        //RestartGame(restartNewRound: true);
                        break;
                    case TurnResultReplay.gameblocked:

                        auxPlayerName = "";

                        if (!auxCurrentTurnData.isTie)
                        {
                            if (localPlayerIndexId == auxCurrentTurnData.roundWinnerPlayerIndexId)
                            {
                                extendedGameController.PlayerWinner = "Player";
                                auxPlayerName = DataInfoPlayer_0.username;
                            } else
                            {
                                switch (auxCurrentTurnData.roundWinnerPlayerIndexId)
                                {
                                    case 1:
                                        extendedGameController.PlayerWinner = "Left";
                                        auxPlayerName = DataInfoPlayer_1.username;
                                        break;
                                    case 2:
                                        extendedGameController.PlayerWinner = "Top";
                                        auxPlayerName = DataInfoPlayer_2.username;
                                        break;
                                    case 3:
                                        extendedGameController.PlayerWinner = "Right";
                                        auxPlayerName = DataInfoPlayer_3.username;
                                        break;
                                    default:
                                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                                        break;
                                }
                            }

                            aiHintText.text = "Player: " + auxPlayerName;
                        } else
                        {
                            extendedGameController.PlayerWinner = "draw"; //"noWin"
                        }

                        if (auxCurrentTurnData.isEndGame && !auxCurrentTurnData.isTie)
                        {
                            aiHintText.text += "\nWinner Game";
                        } else if (!auxCurrentTurnData.isTie)
                        {
                            aiHintText.text += "\nWinner Round";
                        } else if (auxCurrentTurnData.isTie)
                        {
                            aiHintText.text = "Game is draw";
                        }

                        extendedGameController.GameIsCompleteAndFinished = auxCurrentTurnData.isEndGame;
                        extendedGameController.GameTurnController.OnRoundOverEvent?.Invoke();
                        //yield return new WaitForSeconds(2f);
                        //RestartGame(restartNewRound: true);
                        break;
                    case TurnResultReplay.none:
                        break;
                    default:
                        Debug.Log("Invalid result");
                        break;
                }

                isExecution = false;
            }

            yield return null;
        }

        public void HighlightCurrentPlayerTurn(int playerIndexId)
        {
            extendedGameController.Replay_OnTurnStart(playerIndexId + 1);

            /*if (auxCurrentTurnData.playerIndexId == localPlayerIndexId)
            {
                extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
            }
            else
            {
                switch (auxCurrentTurnData.playerIndexId)
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
            }*/
        }

        public void SetSpecificTurn_iaHint(TurnData auxCurrentTurnData)
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

            string auxPlayerName = "";

            if (auxCurrentTurnData.playerIndexId == localPlayerIndexId)
            {
                auxPlayerName = DataInfoPlayer_0.username;
            } else
            {
                switch (auxCurrentTurnData.playerIndexId)
                {
                    case 1:
                        auxPlayerName = DataInfoPlayer_1.username;
                        break;
                    case 2:
                        auxPlayerName = DataInfoPlayer_2.username;
                        break;
                    case 3:
                        auxPlayerName = DataInfoPlayer_3.username;
                        break;
                    default:
                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                        break;
                }
            }

            switch (auxCurrentTurnData.turnAction) //ACTION CASES
            {
                case TurnActionReplay.play:

                    aiHintText.text = "Player: " + auxPlayerName;
                    aiHintText.text += "\nDrew the " + tiles[auxCurrentTurnData.playedPiece.tileId][0] + "/" + tiles[auxCurrentTurnData.playedPiece.tileId][1] + " tile";

                    break;
                case TurnActionReplay.takeBoneyard:
                    Debug.Log("Replay Action: takeBoneyard");

                    aiHintText.text = "Player: " + auxPlayerName;
                    aiHintText.text += "\nTake from the boneyard";

                    break;
                case TurnActionReplay.pass:

                    aiHintText.text = "Player: " + auxPlayerName;
                    aiHintText.text += "\nPass turn";

                    break;
                default:
                    aiHintText.text = "";
                    Debug.Log("Replay Action: none");
                    break;
            }
        }

        public void ReverseCurrentTurn(TurnData auxCurrentTurnData)
        {
            if (auxCurrentTurnData.turnAction != TurnActionReplay.pass)
            {
                RectTransform nextHand = extendedGameController.DeckScript.Player;
                bool stand = true;
                bool onAI = false;

                List<DragHandler> tilesList = new List<DragHandler>();

                if (auxCurrentTurnData.playerIndexId == localPlayerIndexId)
                {
                    tilesList = extendedGameController.DeckScript.PlayerTiles;
                } else
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

                if (auxCurrentTurnData.turnAction == TurnActionReplay.takeBoneyard)
                {
                    foreach (int aux in auxCurrentTurnData.tilesTakenFromBoneyard)
                    //foreach(int aux in auxCurrentTurnData.boneyardPieceIds)
                    {
                        DragHandler selectedTileBoneyard = GetDragHandlerFromTileId(tilesList, aux);

                        if (selectedTileBoneyard != null)
                        {
                            pieces.Add(selectedTileBoneyard);
                        }
                    }

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
                                pieces.Add(auxDragHandler);
                                break;
                            }
                        }
                    }

                    if (pieces.Count > 0)
                    {
                        extendedGameController.DeckScript.DominoTiles.AddRange(pieces);

                        extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().SendTilesToBoneyard();
                    }
                } else if (auxCurrentTurnData.turnAction == TurnActionReplay.play)
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
                                auxDragHandler.SendToNextHand(nextHand, stand, onAI, instantMove: true);
                                SetTileFromPlayerContainer(auxCurrentTurnData.playerIndexId, auxDragHandler);
                                break;
                            }
                        }
                    }
                }
            }

            SetSlotHelperData(auxCurrentTurnData.turnSlotHelper);

            UpdataReplayScores(auxCurrentTurnData);
        }

        public void SetTileFromPlayerContainer(int playerIndexId, DragHandler dragHandler)
        {
            if (playerIndexId == localPlayerIndexId)
            {
                extendedGameController.DeckScript.PlayerTiles.Add(dragHandler);
            } else
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


        public IEnumerator ExecuteSpecificTurn(int indexTurnData, int IndexStartTurnRound, MatchReplay auxCurrentReplay)
        {
            currentReplay = auxCurrentReplay;

            TurnData auxCurrentTurnData = auxCurrentReplay.turns[indexTurnData];

            RestartGame(restartNewRound: true);

            instantExecution = true;
            SetupHands(auxCurrentTurnData);
            //SetupHands(auxCurrentTurnData, instantMove: true);

            //yield return new WaitForSeconds(1f);

            extendedGameController.Replay_OnTurnStart(auxCurrentTurnData.playerIndexId + 1);

            /*List<DragHandler> tilesList = extendedGameController.DeckScript.DominoTiles;
            string auxPlayerPlacingTile = "";

            if (auxCurrentTurnData.playerIndexId == localPlayerIndexId)
            {
                extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
                auxPlayerPlacingTile = "player";
            }
            else
            {
                switch (auxCurrentTurnData.playerIndexId)
                {
                    case 1:
                        extendedGameController.TurnScript.SetTurns(false, true, false, false); //player, left, top, right
                        auxPlayerPlacingTile = "leftPlayer";
                        break;
                    case 2:
                        extendedGameController.TurnScript.SetTurns(false, false, true, false); //player, left, top, right
                        auxPlayerPlacingTile = "topPlayer";
                        break;
                    case 3:
                        extendedGameController.TurnScript.SetTurns(false, false, false, true); //player, left, top, right
                        auxPlayerPlacingTile = "rightPlayer";
                        break;
                    default:
                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                        break;
                }
            }*/

            SetSlotHelperData(auxCurrentTurnData.turnSlotHelper);

            /*extendedGameController.SlotPosScript.sideLimitLeftRight = auxCurrentTurnData.turnSlotHelper.sideLimitLeftRight;
            extendedGameController.SlotPosScript.sideLimitTopDown = auxCurrentTurnData.turnSlotHelper.sideLimitTopDown;
            extendedGameController.SlotPosScript.tileOffset = auxCurrentTurnData.turnSlotHelper.tileOffset;
            extendedGameController.SlotPosScript.lyingOffset = auxCurrentTurnData.turnSlotHelper.lyingOffset;
            extendedGameController.SlotPosScript.doubleOffset = auxCurrentTurnData.turnSlotHelper.doubleOffset;
            extendedGameController.SlotPosScript.tileDist_hor = auxCurrentTurnData.turnSlotHelper.tileDist_hor;

            extendedGameController.SlotPosScript._rightTiles_Hor = auxCurrentTurnData.turnSlotHelper._rightTiles_Hor;
            extendedGameController.SlotPosScript._rightTiles_Ver = auxCurrentTurnData.turnSlotHelper._rightTiles_Ver;
            extendedGameController.SlotPosScript._rightPhase = auxCurrentTurnData.turnSlotHelper._rightPhase;
            extendedGameController.SlotPosScript._rightNum = auxCurrentTurnData.turnSlotHelper._rightNum;

            extendedGameController.SlotPosScript._leftTiles_Hor = auxCurrentTurnData.turnSlotHelper._leftTiles_Hor;
            extendedGameController.SlotPosScript._leftTiles_Ver = auxCurrentTurnData.turnSlotHelper._leftTiles_Ver;
            extendedGameController.SlotPosScript._leftPhase = auxCurrentTurnData.turnSlotHelper._leftPhase;
            extendedGameController.SlotPosScript._leftNum = auxCurrentTurnData.turnSlotHelper._leftNum;

            extendedGameController.SlotPosScript._topTiles_Hor = auxCurrentTurnData.turnSlotHelper._topTiles_Hor;
            extendedGameController.SlotPosScript._topTiles_Ver = auxCurrentTurnData.turnSlotHelper._topTiles_Ver;
            extendedGameController.SlotPosScript._topPhase = auxCurrentTurnData.turnSlotHelper._topPhase;
            extendedGameController.SlotPosScript._topNum = auxCurrentTurnData.turnSlotHelper._topNum;

            extendedGameController.SlotPosScript._downTiles_Hor = auxCurrentTurnData.turnSlotHelper._downTiles_Hor;
            extendedGameController.SlotPosScript._downTiles_Ver = auxCurrentTurnData.turnSlotHelper._downTiles_Ver;
            extendedGameController.SlotPosScript._downPhase = auxCurrentTurnData.turnSlotHelper._downPhase;
            extendedGameController.SlotPosScript._downNum = auxCurrentTurnData.turnSlotHelper._downNum;
            extendedGameController.SlotPosScript.started = auxCurrentTurnData.turnSlotHelper.started;
            extendedGameController.SlotPosScript.auxStarted = auxCurrentTurnData.turnSlotHelper.auxStarted;
            extendedGameController.SlotPosScript.firstPlacedTileId = auxCurrentTurnData.turnSlotHelper.firstPlacedTileId;*/

            /*foreach(DominoPieceData aux in auxCurrentTurnData.boardPieces)
            {
                DragHandler selectedTile = GetDragHandlerFromTileId(tilesList, aux.tileId);

                selectedTile.DragObject.SetParent(extendedGameController._gameBoard);

                selectedTile.PPlaceInBoard(aux.position, aux.rotation, aux.sizeDelta);

                //yield return StartCoroutine(selectedTile.PlaceInBoard(aux.position, aux.rotation, aux.sizeDelta));
                
                tilesList.Remove(selectedTile);
            }*/

            /*for (int i = 0; i < auxCurrentTurnData.boardPieces.Count; i++)
            {
                DragHandler selectedTile = GetDragHandlerFromTileId(tilesList, auxCurrentReplay.turns[i].playedPiece.tileId);
                
                yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTile, auxCurrentTurnData.playedPiece.sideInfo, "dominoTiles", placingInstant: true)); //right, left, top, down / leftPlayer, topPlayer, rightPlayer, player
            }*/

            /*for (int i = IndexStartTurnRound; i < indexTurnData; i++)
            {
                if (auxCurrentReplay.turns[i].playedPiece.sideInfo != "")
                {
                    DragHandler selectedTile = GetDragHandlerFromTileId(tilesList, auxCurrentReplay.turns[i].playedPiece.tileId);
                    //yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTile, auxCurrentTurnData.playedPiece.sideInfo, auxPlayerPlacingTile, placingInstant: true)); //right, left, top, down / leftPlayer, topPlayer, rightPlayer, player
                    yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTile, auxCurrentTurnData.playedPiece.sideInfo, "dominoTiles", placingInstant: true)); //right, left, top, down / leftPlayer, topPlayer, rightPlayer, player   
                }
            }*/

            UpdataReplayScores(auxCurrentTurnData);

            extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().SendTilesToBoneyard();

            yield return null;
        }

        private IEnumerator ExecuteInstantTurnAction(TurnData auxCurrentTurnData)
        {
            if (auxCurrentTurnData.dealHands)
            {
                Debug.Log("++--** Repartir Fichas");
                RestartGame(restartNewRound: true);
                SetupHands(auxCurrentTurnData, instantMove: true);
            } else
            {
                Debug.Log("++--** JUGAR");
                /*extendedGameController.playerRoundScore = extendedGameController.TurnScript.PlayerScore - extendedGameController.playerRoundScore;
                extendedGameController.leftRoundScore = extendedGameController.TurnScript.LeftAIScore - extendedGameController.leftRoundScore;
                extendedGameController.topRoundScore = extendedGameController.TurnScript.TopAIScore - extendedGameController.topRoundScore;
                extendedGameController.rightRoundScore = extendedGameController.TurnScript.RightAIScore - extendedGameController.rightRoundScore;*/

                extendedGameController.Replay_OnTurnStart(auxCurrentTurnData.playerIndexId + 1);

                List<DragHandler> tilesList = new List<DragHandler>();
                string auxPlayerPlacingTile = "";

                if (auxCurrentTurnData.playerIndexId == localPlayerIndexId)
                {
                    extendedGameController.TurnScript.SetTurns(true, false, false, false); //player, left, top, right
                    auxPlayerPlacingTile = "player";
                    tilesList = extendedGameController.DeckScript.PlayerTiles;
                } else
                {
                    switch (auxCurrentTurnData.playerIndexId)
                    {
                        case 1:
                            extendedGameController.TurnScript.SetTurns(false, true, false, false); //player, left, top, right
                            auxPlayerPlacingTile = "leftPlayer";
                            tilesList = extendedGameController.DeckScript.LeftAITiles;
                            break;
                        case 2:
                            extendedGameController.TurnScript.SetTurns(false, false, true, false); //player, left, top, right
                            auxPlayerPlacingTile = "topPlayer";
                            tilesList = extendedGameController.DeckScript.TopAITiles;
                            break;
                        case 3:
                            extendedGameController.TurnScript.SetTurns(false, false, false, true); //player, left, top, right
                            auxPlayerPlacingTile = "rightPlayer";
                            tilesList = extendedGameController.DeckScript.RightAITiles;
                            break;
                        default:
                            Debug.LogError("Invalid playerIndexId in replay turn data.");
                            break;
                    }
                }

                switch (auxCurrentTurnData.turnAction) //ACTION CASES
                {
                    case TurnActionReplay.play:
                        Debug.Log("Replay Action: play");

                        DragHandler selectedTile = GetDragHandlerFromTileId(tilesList, auxCurrentTurnData.playedPiece.tileId);

                        yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTile, auxCurrentTurnData.playedPiece.sideInfo, auxPlayerPlacingTile, placingInstant: true/*, owner*/)); //right, left, top, down / leftPlayer, topPlayer, rightPlayer, player

                        UpdataReplayScores(auxCurrentTurnData);
                        break;
                    case TurnActionReplay.takeBoneyard:
                        Debug.Log("Replay Action: takeBoneyard");

                        foreach (int aux in auxCurrentTurnData.tilesTakenFromBoneyard)
                        {
                            DragHandler selectedBoneyardTile = GetDragHandlerFromTileId(extendedGameController.DeckScript.DominoTiles, aux);

                            boneyardManager.Select_BoneyardTileForPlayer(selectedBoneyardTile.DragObject.parent.transform, true);

                            //yield return new WaitForSeconds(1f);
                        }

                        if (auxCurrentTurnData.playedPiece.sideInfo != "")
                        {
                            DragHandler selectedTileBoneyard = GetDragHandlerFromTileId(tilesList, auxCurrentTurnData.playedPiece.tileId);
                            yield return StartCoroutine(extendedGameController.DropTilePlayerReplay(selectedTileBoneyard, auxCurrentTurnData.playedPiece.sideInfo, auxPlayerPlacingTile, placingInstant: true));
                        } else
                        {
                            //StartCoroutine(ExtendedGameController.GameTurnController.SimpleAlertText("Player Pass Turn", 2f));

                            //ExtendedGameController.ActivatePassPrompt();

                            //yield return new WaitForSeconds(1f);

                            ExtendedGameController.AIController.PassTurn();
                        }

                        //StartCoroutine(OpenBoneyard());
                        UpdataReplayScores(auxCurrentTurnData);
                        //ValideTurnResult(auxCurrentTurnData.turnResult);
                        break;
                    case TurnActionReplay.pass:
                        Debug.Log("Replay Action: pass");

                        //StartCoroutine(ExtendedGameController.GameTurnController.SimpleAlertText("Player Pass Turn", 2f));

                        //ExtendedGameController.ActivatePassPrompt();

                        //yield return new WaitForSeconds(1f);

                        ExtendedGameController.AIController.PassTurn();

                        UpdataReplayScores(auxCurrentTurnData);
                        //ValideTurnResult(auxCurrentTurnData.turnResult);
                        break;
                    default:
                        Debug.Log("Replay Action: none");
                        break;
                }



                switch (auxCurrentTurnData.turnResult) //RESULT TURN CASES
                {
                    case TurnResultReplay.gameOver:

                        if (!auxCurrentTurnData.isTie)
                        {
                            if (localPlayerIndexId == auxCurrentTurnData.roundWinnerPlayerIndexId)
                            {
                                extendedGameController.PlayerWinner = "Player";
                            } else
                            {
                                switch (auxCurrentTurnData.roundWinnerPlayerIndexId)
                                {
                                    case 1:
                                        extendedGameController.PlayerWinner = "Left";
                                        break;
                                    case 2:
                                        extendedGameController.PlayerWinner = "Top";
                                        break;
                                    case 3:
                                        extendedGameController.PlayerWinner = "Right";
                                        break;
                                    default:
                                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                                        break;
                                }
                            }
                        } else
                        {
                            extendedGameController.PlayerWinner = "draw"; //"noWin"
                        }

                        extendedGameController.GameIsCompleteAndFinished = auxCurrentTurnData.isEndGame;
                        ExtendedGameController.GameTurnController.OnRoundOverEvent?.Invoke();
                        //yield return new WaitForSeconds(2f);
                        //RestartGame(restartNewRound: true);
                        break;
                    case TurnResultReplay.gameblocked:

                        if (!auxCurrentTurnData.isTie)
                        {
                            if (localPlayerIndexId == auxCurrentTurnData.roundWinnerPlayerIndexId)
                            {
                                extendedGameController.PlayerWinner = "Player";
                            } else
                            {
                                switch (auxCurrentTurnData.roundWinnerPlayerIndexId)
                                {
                                    case 1:
                                        extendedGameController.PlayerWinner = "Left";
                                        break;
                                    case 2:
                                        extendedGameController.PlayerWinner = "Top";
                                        break;
                                    case 3:
                                        extendedGameController.PlayerWinner = "Right";
                                        break;
                                    default:
                                        Debug.LogError("Invalid playerIndexId in replay turn data.");
                                        break;
                                }
                            }
                        } else
                        {
                            extendedGameController.PlayerWinner = "draw"; //"noWin"
                        }

                        extendedGameController.GameIsCompleteAndFinished = auxCurrentTurnData.isEndGame;
                        ExtendedGameController.GameTurnController.OnRoundOverEvent?.Invoke();
                        //yield return new WaitForSeconds(2f);
                        //RestartGame(restartNewRound: true);
                        break;
                    case TurnResultReplay.none:
                        break;
                    default:
                        Debug.Log("Invalid result");
                        break;
                }
            }

            yield return null;
        }

        /*private void ValideTurnResult(TurnResultReplay turnResultReplay)
        {
            switch (turnResultReplay) //RESULT TURN CASES
            {
                case TurnResultReplay.gameOver:
                    ExtendedGameController.GameTurnController.OnRoundOverEvent?.Invoke();
                    break;
                case TurnResultReplay.gameblocked:
                    ExtendedGameController.GameTurnController.OnRoundOverEvent?.Invoke();
                    break;
                case TurnResultReplay.none:
                    break;
                default:
                    Debug.Log("Invalid result");
                    break;
            }
        }*/

        private DragHandler GetDragHandlerFromTileId(List<DragHandler> tilesList, int tileId)
        {
            foreach (DragHandler aux in tilesList)
            {
                if (aux.GetDominoView().GetDomino().id == tileId)
                {
                    return aux;
                }
            }

            return null;//tilesList.Find(tile => tile.GetDominoView().GetDomino().id == tileId);
        }

        /*private DragHandler GetBoneyardDragHandlerWithTileId(List<DragHandler> tilesList, int tileId)
        {
            return extendedGameController.DeckScript.DominoTiles.Find(tile => tile.GetDominoView().GetDomino().id == tileId);
        }*/

        private void UpdataReplayScores(TurnData auxCurrentTurnData)
        {
            extendedGameController.playerRoundScore = auxCurrentTurnData?.roundPlayerScores?.ElementAtOrDefault(0) ?? 0;
            extendedGameController.leftRoundScore = auxCurrentTurnData?.roundPlayerScores.ElementAtOrDefault(1) ?? 0;
            extendedGameController.topRoundScore = auxCurrentTurnData?.roundPlayerScores.ElementAtOrDefault(2) ?? 0;
            extendedGameController.rightRoundScore = auxCurrentTurnData?.roundPlayerScores.ElementAtOrDefault(3) ?? 0;

            extendedGameController.TurnScript.PlayerScore = auxCurrentTurnData?.cumulatePlayerScores?.ElementAtOrDefault(0) ?? 0;
            extendedGameController.TurnScript.LeftAIScore = auxCurrentTurnData?.cumulatePlayerScores?.ElementAtOrDefault(1) ?? 0;
            extendedGameController.TurnScript.TopAIScore = auxCurrentTurnData?.cumulatePlayerScores?.ElementAtOrDefault(2) ?? 0;
            extendedGameController.TurnScript.RightAIScore = auxCurrentTurnData?.cumulatePlayerScores?.ElementAtOrDefault(3) ?? 0;

            extendedGameController.UpdateScoreUI();
        }

        public void SetupHands(TurnData auxCurrentTurnData, bool instantMove = false)
        {
            dominoTiles.Clear();
            dominoTiles = new List<int>();

            dominoTiles = auxCurrentTurnData.boneyardPieceIds;

            handOfPlayer_0.Clear();
            handOfPlayer_1.Clear();
            handOfPlayer_2.Clear();
            handOfPlayer_3.Clear();

            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                //numberOfTilesPerPlayer = 7;
                numberOfTilesPerPlayer = auxCurrentTurnData.playersHands[0].handPieceIds.Count;

                handOfPlayer_0 = auxCurrentTurnData.playersHands.Find(p => p.playerIndexId == 0)?.handPieceIds ?? new List<int>();
                handOfPlayer_1 = auxCurrentTurnData.playersHands.Find(p => p.playerIndexId == 1)?.handPieceIds ?? new List<int>();
                handOfPlayer_2 = new List<int>();
                handOfPlayer_3 = new List<int>();
            } else
            {
                //numberOfTilesPerPlayer = 5;
                numberOfTilesPerPlayer = auxCurrentTurnData.playersHands[0].handPieceIds.Count;

                handOfPlayer_0 = auxCurrentTurnData.playersHands.Find(p => p.playerIndexId == 0)?.handPieceIds ?? new List<int>();
                handOfPlayer_1 = auxCurrentTurnData.playersHands.Find(p => p.playerIndexId == 1)?.handPieceIds ?? new List<int>();
                handOfPlayer_2 = auxCurrentTurnData.playersHands.Find(p => p.playerIndexId == 2)?.handPieceIds ?? new List<int>();
                handOfPlayer_3 = auxCurrentTurnData.playersHands.Find(p => p.playerIndexId == 3)?.handPieceIds ?? new List<int>();
            }

            if (!instantMove)
            {
                extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().DeckExtendedSetupRandomHands
                      (null,
                      UpdateBoneyardText,
                      handOfPlayer_0,
                      handOfPlayer_1,
                      handOfPlayer_2,
                      handOfPlayer_3, numberOfTilesPerPlayer, replayGameMode: this);
            } else
            {
                StartCoroutine(extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().SetupHandsInspecificTurnReplay
                      (UpdateBoneyardText,
                      handOfPlayer_0,
                      handOfPlayer_1,
                      handOfPlayer_2,
                      handOfPlayer_3));
            }
        }

        private IEnumerator SetupExtendedRandomHands(Action updateBoneyardText, List<int> handPlayer_0, List<int> handPlayer_1, List<int> handPlayer_2, List<int> handPlayer_3, int numberTilesPerPlayer = 7, bool instantMove = false)
        {
            //extendedGameController.DeckScript.GetComponent<ExtendedDeckController>()
            //extendedDeckController

            yield return null;

            //extendedGameController.DeckScript.PlayerTiles = 

            //extendedGameController.DeckScript.PlayerTiles = new List<DragHandler>();
            //extendedGameController.DeckScript.RightAITiles = new List<DragHandler>();

            /*_leftAITiles = new List<DragHandler>();
            _topAITiles = new List<DragHandler>();

            if(!instantMove)
                yield return new WaitForSeconds(1f);

            int i = 0;

            if (_deckHolder)
                _deckHolder.gameObject.SetActive(true); // Show the deck

            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                while (i < numberTilesPerPlayer * 4 && _dominoTiles.Count > 0) //while (i < 28 && _dominoTiles.Count > 0)
                {
                    //int rand = UnityEngine.Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < numberTilesPerPlayer) //if (i < 7)
                    {
                        if (!_turnScript._playerIsEliminated && handPlayer_0.Count > 0)
                        {
                            TileSwapHelper(ref _player, ref _playerTiles, handPlayer_0[i], true, false, remove: false, instantMove: instantMove);
                        }
                        else
                        {
                            i = numberTilesPerPlayer - 1;//i = 6; // Skip the player's hand if they are eliminated
                        }
                    }
                    else if (i < numberTilesPerPlayer * 2) //else if (i < 14)
                    {
                        if (!_turnScript._leftAIIsEliminated && handPlayer_1.Count > 0)
                        {
                            TileSwapHelper(ref _leftAI, ref _leftAITiles, handPlayer_1[i - numberTilesPerPlayer], false, true, remove: false, instantMove: instantMove);
                            //TileSwapHelper(ref _leftAI, ref _leftAITiles, handPlayer_1[i - 7], false, true, remove: false);
                        }
                        else
                        {
                            i = (numberTilesPerPlayer * 2) - 1; //i = 13; // Skip the left AI's hand if they are eliminated
                        }
                    }
                    else if (i < numberTilesPerPlayer * 3) //else if (i < 21)
                    {
                        if (!_turnScript._topAIIsEliminated && handPlayer_2.Count > 0)
                        {
                            TileSwapHelper(ref _topAI, ref _topAITiles, handPlayer_2[i - (numberTilesPerPlayer * 2)], true, true, remove: false, instantMove: instantMove);
                            //TileSwapHelper(ref _topAI, ref _topAITiles, handPlayer_2[i - 14], true, true, remove: false);
                        }
                        else
                        {
                            i = (numberTilesPerPlayer * 3) - 1;//i = 20; // Skip the top AI's hand if they are eliminated
                        }
                    }
                    else
                    {
                        if (!_turnScript._rightAIIsEliminated && handPlayer_3.Count > 0)
                        {
                            //TileSwapHelper(ref _rightAI, ref _rightAITiles, handPlayer_3[i - 21], false, true, remove: false);
                            TileSwapHelper(ref _rightAI, ref _rightAITiles, handPlayer_3[i - numberTilesPerPlayer * 3], false, true, remove: false, instantMove: instantMove);
                        }
                        else
                        {
                            i = numberTilesPerPlayer * 4; //i = 28; // Skip the right AI's hand if they are eliminated
                        }
                    }

                    // Update the boneyard text if provided
                    if (updateBoneyardText is not null)
                        updateBoneyardText?.Invoke();

                    i++;
                    
                    if(!instantMove)
                        yield return new WaitForSeconds(0.2f);
                }
            }
            else
            {
                while (i < numberTilesPerPlayer * 2 && _dominoTiles.Count > 0) //while (i < 14 && _dominoTiles.Count > 0)
                {
                    //int rand = UnityEngine.Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < numberTilesPerPlayer && handPlayer_0.Count > 0) //if (i < 7)
                    {
                        //Debug.Log("===> Player hand: " + i + "/" + rand);
                        TileSwapHelper(ref _player, ref _playerTiles, handPlayer_0[i], true, false, remove: false, instantMove: instantMove);
                    }
                    else if (i < numberTilesPerPlayer * 2 && handPlayer_1.Count > 0) //else if (i < 14)
                    {
                        TileSwapHelper(ref _topAI, ref _topAITiles, handPlayer_1[i-numberTilesPerPlayer], true, true, remove: false, instantMove: instantMove);
                        //TileSwapHelper(ref _topAI, ref _topAITiles, handPlayer_1[i-7], true, true, remove: false);
                    }


                    // Update the boneyard text if provided
                    if (updateBoneyardText is not null)
                        updateBoneyardText?.Invoke();

                    i++;

                    if(!instantMove)
                        yield return new WaitForSeconds(0.2f);
                }
            }

            _deckHolder.transform.localPosition = new Vector3(2000, 0, 0);

            // Wait until the hands are fully set up
            yield return new WaitForSeconds(1f);

            List<int> allHands = new List<int>();
            allHands.AddRange(handPlayer_0);
            allHands.AddRange(handPlayer_1);
            allHands.AddRange(handPlayer_2);
            allHands.AddRange(handPlayer_3);

            RemoveLocalPlayerTiles(allHands);

            // Update the boneyard text if provided
            if (updateBoneyardText is not null)
                updateBoneyardText?.Invoke();

            if (_dominoTiles != null && _dominoTiles.Count > 0)
            {
                SendTilesToBoneyard();
                //_deckHolder.transform.localPosition = new Vector3(760, 0, 0); // Move deck to the left
            }
            else
            {
                //_deckHolder.transform.localPosition = new Vector3(2000, 0, 0); // Reset deck position if no tiles left
                //_deckHolder.gameObject.SetActive(false); // Hide the deck if no tiles left
            }

            if (selectPlayerWhoWillTakeFirstTurn != null)
            {
                int auxPlayerFirstTurnID = selectPlayerWhoWillTakeFirstTurn.Invoke(); // Call the function to select the player who will take the first turn

                if (auxPlayerFirstTurnID == -1)
                {
                    yield return _turnScript.AlertTextIE("Shuffling Tiles again!", 2.5f);

                    Debug.Log("===> *** No player selected for the first turn. Defaulting to Player 1.");
                    //_gameScript.GetComponent<ExtendedGameController>().SetPlayerHand(new List<int>(handPlayer_0), null);
                    _gameScript.GetComponent<AbstractGameMode>().RestartGame(restartNewRound: true);
                    _gameScript.GetComponent<AbstractGameMode>().SetupRandomHands();
                }
                else
                {
                    Debug.Log("===> *** Player With 6/6: " + auxPlayerFirstTurnID);

                    switch (auxPlayerFirstTurnID)
                    {
                        case 0:
                            _turnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                            break;
                        case 1:
                            _turnScript.SetTurns(false, true, false, false);// Player, Left, Top, Right
                            break;
                        case 2:
                            _turnScript.SetTurns(false, false, true, false);// Player, Left, Top, Right
                            break;
                        case 3:
                            _turnScript.SetTurns(false, false, false, true);// Player, Left, Top, Right
                            break;
                        default:
                            _turnScript.SetTurns(true, false, false, false);// Player, Left, Top, Right
                            Debug.Log("Invalid player ID for first turn selection.");
                            break;
                    }

                    _gameScript.GetComponent<AbstractGameMode>().CreateReplayTurn();
                    _turnScript.ValideEndTurnLogic("Game Started", 1);
                }    
            }
            else
            {
                if(_gameScript.GameModeSelectedID != GameMode.replay)
                    _turnScript.EndTurn("Game Started", 1);
            }

                Debug.Log("===> All list count -> " + _dominoTiles.Count + " Player count -> " + _playerTiles.Count);
            Debug.Log("===> _rightAITiles count -> " + _rightAITiles.Count + " _leftAITiles count -> " +
                      _leftAITiles.Count + " _topAITiles count -> " + _topAITiles.Count);
            yield return null;*/
        }

        /*public void LoadSpecificRound()
        {
            RestartGame(restartNewRound: true);
        }*/

        #endregion

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

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!IsForcingShowTiles && debugToolsEnabled && !IsDebugingTilesRightNow || IsDebugingTilesRightNow)
            {
                IsDebugingTilesRightNow = true;
                dominoAI.DebugDrawAIScoring_AI(ShowAIProbabilities);
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
                && GameModeID is GameMode.draw
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
            }

            /*bool auxIsTurnActive = onGetIsTurnActive != null && onGetIsTurnActive.Invoke();
            if (auxIsTurnActive)
            {
                //Debug.Log("+-+-+ onGetRemainingTime.Invoke(): " + onGetRemainingTime.Invoke());
                extendedGameController.UpdateTimerUI_FromHost(onGetRemainingTime.Invoke(), maxTimePerTurn);
            }*/
        }

        public override void InitializeGameMode
            (GameModeConfig aux_gameModeConfig,
            int aux_Difficulty,
            GameType aux_GameTypeID,
            NumberPlayers aux_VsPlayerID,
            bool isSinglePlayerIA,
            ConcentrateNumberOfTiles auxConcentrateNumberOfTiles,
            Action openMenuSettingsAction,
            Func<bool> isTimeOut_MatchManager)
        {
            base.InitializeGameMode(aux_gameModeConfig, aux_Difficulty, aux_GameTypeID, aux_VsPlayerID, isSinglePlayerIA, auxConcentrateNumberOfTiles, openMenuSettingsAction, isTimeOut_MatchManager);

            limitPointsGameMode = aux_VsPlayerID == NumberPlayers.oneVsOne ? 50 : 100;
            ExtendedGameController.LimitPointsGameController = limitPointsGameMode;

            /*Debug.Log("Data: " + PostMatchResultController);
            Debug.Log("Data: " + gameModeConfig.SetGameplayVisibility);
            Debug.Log("Data: " + dominoAI.GetFirstTurnAvailableTilesDraw);
            Debug.Log("Data: " + dominoAI.GetBestMoveDraw);
            Debug.Log("Data: " + CurrentDisplayName);*/

            // Register some AI events to be used
            extendedGameController.Initialize
                (PostMatchResultController,
                gameModeConfig.SetGameplayVisibility,
                openMenuSettingsAction,
                dominoAI.GetFirstTurnAvailableTilesDraw,
                dominoAI.GetBestMoveDraw,
                () => CurrentDisplayName);
        }

        public override void RestartGame(bool restartNewRound = false)
        {
            base.RestartGame(restartNewRound);

            firstDoubleTileIsPlace = false;
            fullSides = false;

            extendedGameController.SlotPosScript.SideLimitLeftRight = 999;

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
                numberOfTilesPerPlayer = 7;

                int index = -1;

                for (int i = 0; i < numberOfTilesPerPlayer; i++)
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
                numberOfTilesPerPlayer = 5;
                int index = -1;

                for (int i = 0; i < numberOfTilesPerPlayer; i++)
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
                    handOfPlayer_3, numberOfTilesPerPlayer);
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
                Debug.Log($"Replay: Boneyard domino tiles setted. Values: {string.Join(", ", boneyardDominoTiles)}");
            }
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
        private bool fullSides = false;
        public override void ValidateNumbersAvailableInBranches(ref int rightBranch, ref int leftBranch, ref int topNum, ref int downNum, ref List<int> doubleTilesEnabled, int firstPlacedTileId, Domino tileInfo = null)
        {
            //french_FirstPlacedTileId = firstPlacedTileId;

            int auxDefaultTile = 99;

            // Dictionary that maps each number to the required tile ID it needs to be considered valid
            /*Dictionary<int, int> numberCheckMap = new Dictionary<int, int>
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
                return numberCheckMap.ContainsKey(sideValue) && tilesID_InShowBoard.Contains(numberCheckMap[sideValue])
                    ? sideValue
                    : auxDefaultTile;
            }*/

            var slot = extendedGameController.SlotPosScript;

            int auxRightBranch = auxDefaultTile;
            int auxLeftBranch = auxDefaultTile;
            int auxTopNum = auxDefaultTile;
            int auxDownNum = auxDefaultTile;

            List<int> auxDoubleTilesEnabled = doubleTilesEnabled;

            switch(auxGameMode_id)
            {
                case GameMode.draw:
                    Draw_Branches();
                break;
                case GameMode.french:
                    French_Branches();
                    doubleTilesEnabled = auxDoubleTilesEnabled;
                break;
                case GameMode.block:
                    Block_Branches();
                break;
                case GameMode.five:
                    Five_Branches();
                break;
                default:
                    Debug.Log("Invalid Game Mode");
                break;
            }

            int baseValue = 10;

            void PrintDouble()  // ← función local dentro de Start()
            {
                int doubled = baseValue * 2;  // ← puede usar 'baseValue'
                Debug.Log("El doble es: " + doubled);
            }

            PrintDouble(); // Llamamos la función local

            void Draw_Branches()
            {
                auxRightBranch = slot.RightNum; //!= -1 ? ValidateNum(slot.RightNum) : -1;
                auxLeftBranch = slot.LeftNum; //!= -1 ? ValidateNum(slot.LeftNum) : -1;
                auxTopNum = auxDefaultTile;  //slot.TopNum != -1 ? ValidateNum(slot.TopNum) : -1;
                auxDownNum = auxDefaultTile;  //slot.DownNum != -1 ? ValidateNum(slot.DownNum) : -1;   
            }

            void French_Branches()
            {
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

                auxRightBranch = slot.RightNum != -1 ? ValidateNum(slot.RightNum) : -1;
                auxLeftBranch = slot.LeftNum != -1 ? ValidateNum(slot.LeftNum) : -1;
                auxTopNum = slot.TopNum != -1 ? ValidateNum(slot.TopNum) : -1;
                auxDownNum = slot.DownNum != -1 ? ValidateNum(slot.DownNum) : -1;

                if (!fullSides && auxRightBranch != firstPlacedTileId && auxLeftBranch != firstPlacedTileId && auxTopNum != firstPlacedTileId && auxDownNum != firstPlacedTileId)
                {
                    fullSides = true;
                }
                else if (!fullSides)
                {
                    auxRightBranch = auxRightBranch != firstPlacedTileId ? 99 : auxRightBranch;
                    auxLeftBranch = auxLeftBranch != firstPlacedTileId ? 99 : auxLeftBranch;
                    auxTopNum = auxTopNum != firstPlacedTileId ? 99 : auxTopNum;
                    auxDownNum = auxDownNum != firstPlacedTileId ? 99 : auxDownNum;
                }

                if (tileInfo != null) //Validates the case if it is a double tile when creating a slot to put the tile in
                {
                    auxRightBranch = auxRightBranch == 99 && tileInfo.TopIndex == slot.RightNum && tileInfo.BottomIndex == slot.RightNum ? slot.RightNum : auxRightBranch;
                    auxLeftBranch = auxLeftBranch == 99 && tileInfo.TopIndex == slot.LeftNum && tileInfo.BottomIndex == slot.LeftNum ? slot.LeftNum : auxLeftBranch;
                    auxTopNum = auxTopNum == 99 && tileInfo.TopIndex == slot.TopNum && tileInfo.BottomIndex == slot.TopNum ? slot.TopNum : auxTopNum;
                    auxDownNum = auxDownNum == 99 && tileInfo.TopIndex == slot.DownNum && tileInfo.BottomIndex == slot.DownNum ? slot.DownNum : auxDownNum;
                }
                else
                {
                    if (fullSides)
                    {
                        auxDoubleTilesEnabled.Add(slot.RightNum);
                        auxDoubleTilesEnabled.Add(slot.LeftNum);
                        auxDoubleTilesEnabled.Add(slot.TopNum);
                        auxDoubleTilesEnabled.Add(slot.DownNum);
                    }
                }
            }

            void Block_Branches()
            {
                auxRightBranch = slot.RightNum; //!= -1 ? ValidateNum(slot.RightNum) : -1;
                auxLeftBranch = slot.LeftNum; //!= -1 ? ValidateNum(slot.LeftNum) : -1;
                auxTopNum = auxDefaultTile;  //slot.TopNum != -1 ? ValidateNum(slot.TopNum) : -1;
                auxDownNum = auxDefaultTile;  //slot.DownNum != -1 ? ValidateNum(slot.DownNum) : -1;
            }

            void Five_Branches()
            {
                auxRightBranch = slot.RightNum; //!= -1 ? ValidateNum(slot.RightNum) : -1;
                auxLeftBranch = slot.LeftNum; //!= -1 ? ValidateNum(slot.LeftNum) : -1;
                auxTopNum = firstDoubleTileIsPlace ? slot.TopNum : auxDefaultTile;  //slot.TopNum != -1 ? ValidateNum(slot.TopNum) : -1;
                auxDownNum = firstDoubleTileIsPlace ? slot.DownNum : auxDefaultTile;  //slot.DownNum != -1 ? ValidateNum(slot.DownNum) : -1;
            }

            rightBranch = auxRightBranch;
            leftBranch = auxLeftBranch;
            topNum = auxTopNum;
            downNum = auxDownNum;

            /*rightBranch = slot.RightNum; //!= -1 ? ValidateNum(slot.RightNum) : -1;
            leftBranch = slot.LeftNum; //!= -1 ? ValidateNum(slot.LeftNum) : -1;
            topNum = auxDefaultTile;  //slot.TopNum != -1 ? ValidateNum(slot.TopNum) : -1;
            downNum = auxDefaultTile;  //slot.DownNum != -1 ? ValidateNum(slot.DownNum) : -1;*/

            /*Debug.Log("++// sideInfo: " + currentTurnData.playedPiece.sideInfo);
            Debug.Log("++// tile: " + currentTurnData.playedPiece.tileId);

            Debug.Log("++// rightBranch: " + slot.RightNum);
            Debug.Log("++// leftBranch: " + slot.LeftNum);
            Debug.Log("++// topNum: " + slot.TopNum);
            Debug.Log("++// downNum: " + slot.DownNum);*/

            /*switch(currentTurnData.playedPiece.sideInfo)
            {
                case "right":
                    rightBranch = slot.RightNum;
                break;
                case "left":
                    leftBranch = slot.LeftNum;
                break;
                case "top":
                    topNum = slot.TopNum;
                break;
                case "down":
                    downNum = slot.DownNum;
                break;
                default:
                    Debug.Log("Invalid side");
                break;
            }*/

            /*if (!fullSides && rightBranch != firstPlacedTileId && leftBranch != firstPlacedTileId && topNum != firstPlacedTileId && downNum != firstPlacedTileId)
            {
                fullSides = true;
            }
            else if (!fullSides)
            {
                rightBranch = rightBranch != firstPlacedTileId ? 99 : rightBranch;
                leftBranch = leftBranch != firstPlacedTileId ? 99 : leftBranch;
                topNum = topNum != firstPlacedTileId ? 99 : topNum;
                downNum = downNum != firstPlacedTileId ? 99 : downNum;
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
                if (fullSides)
                {
                    doubleTilesEnabled.Add(slot.RightNum);
                    doubleTilesEnabled.Add(slot.LeftNum);
                    doubleTilesEnabled.Add(slot.TopNum);
                    doubleTilesEnabled.Add(slot.DownNum);
                }
            }*/

            Debug.Log($"--->rightBranch: {rightBranch} / leftBranch: {leftBranch} / topNum: {topNum} / downNum: {downNum}");
        }

        #endregion

        public override int SelectPlayerWhoWillTakeFirstTurn()
        {
            return 1;
            //return currentTurnData.playerIndexId;
;
            /*if (isSinglePlayerVsIA)
            {*/

            /*List<Domino> playerDominoes = new List<Domino>();
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

            Dictionary<int, List<Domino>> tilesCollection = new Dictionary<int, List<Domino>>()
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

                if (!isSinglePlayerVsIA && vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    playersOrderId = new Dictionary<string, byte>()
                    {
                        { "Player", 0 }, // Player
                        { "Top", 1 } // Top AI
                    };
                }

                int auxPlayerOrderId = playersOrderId[lastRoundWinner];

                Debug.Log($"---+++ AuxPlayerOrderId player: {auxPlayerOrderId}");

                return auxPlayerOrderId;
            }
            else
            {
                List<int> playersWithAvailableHands = tilesCollection
                .Where(pair => pair.Value != null && pair.Value.Count > 0)
                .Select(pair => pair.Key)
                .ToList();

                if (playersWithAvailableHands.Count > 0)
                {
                    int randomIndex = UnityEngine.Random.Range(0, playersWithAvailableHands.Count);
                    int selectedPlayerId = playersWithAvailableHands[randomIndex];

                    Debug.Log("Randomly selected key: " + selectedPlayerId);

                    return selectedPlayerId;
                }
                else
                {
                    Debug.Log("No list contains any elements.");
                }
            }

            Debug.Log("There is no player with the 0|0 tile or any double tile to start the game. Reshuffling...");
            return -1;*/
        }

        public override void StartTurn(int playerTurnID, int localPlayerID)
        {
            boneyardCanvasGroup.interactable = false;
            extendedGameController.EnableTurnForPlayer(playerTurnID, localPlayerID);
        }

        private bool firstDoubleTileIsPlace = false; //FIVE FIVE GAME MODE
        public override void SetTileIdUsedInShowBoard(int tileID, string sideInfo, Vector3 posInfo, Quaternion rotInfo, Vector2 sizeInfo)
        {
            base.SetTileIdUsedInShowBoard(tileID, sideInfo, posInfo, rotInfo, sizeInfo);

            if(auxGameMode_id == GameMode.five){

                Debug.Log("++---- >>++ Place Tile: " + tileID + ", sideInfo: " + sideInfo);

                SlotHelper slot = extendedGameController.SlotPosScript;

                if (!firstDoubleTileIsPlace)
                {
                    Dictionary<int, int> doubleTilesCheck = new Dictionary<int, int>
                    {
                        { 0, 0 }, //0 = 0/0
                        { 7, 1 }, //7 = 1/1
                        { 13, 2 }, //13 = 2/2
                        { 18, 3 }, //18 = 3/3
                        { 22, 4 }, //22 = 4/4
                        { 25, 5 }, //25 = 5/5
                        { 27, 6 } //27 = 6/6
                    };

                    if (doubleTilesCheck.ContainsKey(tileID))
                    {
                        firstDoubleTileIsPlace = true;

                        slot.TopNum = doubleTilesCheck[tileID];
                        slot.DownNum = doubleTilesCheck[tileID];

                        switch (sideInfo)
                        {
                            case "right":
                                slot.TopTiles_Hor = slot.RightTiles_Hor - 1;
                                slot.DownTiles_Hor = slot.RightTiles_Hor - 1;
                                break;
                            case "left":
                                slot.TopTiles_Hor = slot.LeftTiles_Hor + 1;
                                slot.DownTiles_Hor = slot.LeftTiles_Hor + 1;
                                break;
                        }

                        slot.TopTiles_Ver = 1.5f;
                        slot.DownTiles_Ver = -1.5f;
                    }
                }

                Debug.Log("++---- +++ firstDoubleTileIsPlace: " + firstDoubleTileIsPlace + ", slot.RightNum: " + slot.RightNum + ", slot.LeftNum: " + slot.LeftNum + ", slot.TopNum: " + slot.TopNum + ", slot.DownNum: " + slot.DownNum);

                int rightBranch = slot.RightNum; //!= -1 ? ValidateNum(slot.RightNum) : -1;
                int leftBranch = slot.LeftNum; //!= -1 ? ValidateNum(slot.LeftNum) : -1;
                int topNum = firstDoubleTileIsPlace ? slot.TopNum : 99; //!= -1 ? ValidateNum(slot.TopNum) : -1;
                int downNum = firstDoubleTileIsPlace ? slot.DownNum : 99; //!= -1 ? ValidateNum(slot.DownNum) : -1;

                int totalAllPoints = rightBranch + leftBranch + (topNum != 99 ? topNum : 0) + (downNum != 99 ? downNum : 0);

                int pointsToDeliver = totalAllPoints % 5 == 0 ? totalAllPoints : 0;

                Debug.Log("++---- rightBranch: " + rightBranch + ", leftBranch: " + leftBranch + ", topNum: " + topNum + ", downNum: " + downNum);
                Debug.Log("++---- totalAllPoints: " + totalAllPoints + ", totalAllPoints % 5: " + totalAllPoints % 5);
                Debug.Log("++---- pointsToDeliver: " + pointsToDeliver);

                Dictionary<int, string> auxPlayerId = new Dictionary<int, string>()
                {
                    { 1, "Player" },
                    { 2, "Left" },
                    { 3, "Top" },
                    { 4, "Right" }
                };

                string auxPlayerID = auxPlayerId[extendedGameController.TurnScript.GetCurrentTurnControl()];

                switch (auxPlayerID)
                {
                    case "Player":
                        extendedGameController.TurnScript.PlayerScore += pointsToDeliver;
                        break;
                    case "Left":
                        extendedGameController.TurnScript.LeftAIScore += pointsToDeliver;
                        break;
                    case "Top":
                        extendedGameController.TurnScript.TopAIScore += pointsToDeliver;
                        break;
                    case "Right":
                        extendedGameController.TurnScript.RightAIScore += pointsToDeliver;
                        break;
                    default:
                        Debug.Log("Unidentified player");
                        break;
                }

                Debug.Log("+-*/ Player: " + auxPlayerID + ", pointsToDeliver: " + pointsToDeliver);

                if(!isSinglePlayerVsIA)
                    extendedGameController.UpdateScoreUI();

            }
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
                bool auxTotalTilesInBoneyard = extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().GetTotalTilesInBoneyard() > 0; //0;

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

                bool auxTotalTilesInBoneyard = extendedGameController.DeckScript.GetComponent<ExtendedDeckController>().GetTotalTilesInBoneyard() > 2; //0;

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
                ExtendedGameController.HandController.StartPassButtonHighlight();
            }
        }

        public void SelectBoneyardTileForPlayer(Transform container)
        {
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
            if (isSinglePlayerVsIA)
            {
                ExtendedGameController.AIController.PassTurn();
            }
            else
            {
                ExtendedGameController.HandController.PlayerPassButton.SetButtonInteractable(false);
                onPassTurnHostAction?.Invoke(true);
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
                return true;
            }

            string auxPlayerID_win = CheckIfAnyPlayerUsedAllTiles();

            Debug.Log("++--> Valide Winner: " + auxPlayerID_win);

            if (auxPlayerID_win != "")
            {
                gameOverCase = DeliverPointsAndValidateEliminated(auxPlayerID_win);
                return true;
            }

            // If the game is not over, return false
            return false;
        }

        /// <summary>
        /// Determines whether the match should be stopped based on whether any player has used all their tiles
        /// </summary>
        /// <returns>True if the match should be stopped; otherwise, false.</returns>
        public override bool RoundShouldBeStopped()
        {
            // First, check if any player has used all their tiles
            var auxPlayerID_win = CheckIfAnyPlayerUsedAllTiles();
            return auxPlayerID_win != "";
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

        private string DeliverPointsAndValidateEliminated(string playerIdWhoUsedAllTiles = "")
        {
            Debug.Log("Delivering points...");

            playerPoints = 0;
            leftAIPoints = 0;
            topAIPoints = 0;
            rightAIPoints = 0;
            /*pointsTeam_1 = 0;
            pointsTeam_2 = 0;*/

            lastRoundWinner = "";

            //int id_lastTilePlaced = tilesID_InShowBoard[tilesID_InShowBoard.Count - 1];

            /*Dictionary<int, int> Id_DoubleTiles = new Dictionary<int, int>
            {
                { 0, 0 }, //0 = 0/0
                { 1, 7 }, //7 = 1/1
                { 2, 13 }, //13 = 2/2
                { 3, 18 }, //18 = 3/3
                { 4, 22 }, //22 = 4/4
                { 5, 25 }, //25 = 5/5
                { 6, 27 } //27 = 6/6
            };*/

            int auxMultiplier = 1; //Id_DoubleTiles.ContainsValue(id_lastTilePlaced) ? 2 : 1;

            limitPointsGameMode = vsPlayerSelectedID == NumberPlayers.oneVsOne ? 50 : 100;

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
                else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    playerDominoes = ConvertIdTilesListToDominoList(handOfPlayer_0); //Client host
                    topDominoes = ConvertIdTilesListToDominoList(handOfPlayer_1);
                }
            }

            Dictionary<string, int> roundPoints = new Dictionary<string, int>();
            //Dictionary<string, int> accumulatedPoints = new Dictionary<string, int>();

            if (vsPlayerSelectedID == NumberPlayers.oneVsThree)
            {
                if (!extendedGameController.TurnScript._playerIsEliminated)
                {
                    playerPoints = playerDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    
                    /*extendedGameController.TurnScript.PlayerScore += playerPoints;

                    extendedGameController.TurnScript._playerIsEliminated = extendedGameController.TurnScript.PlayerScore >= maximumGamepoints;*/

                    roundPoints.Add("Player", playerPoints);
                    //accumulatedPoints.Add("Player", extendedGameController.TurnScript.PlayerScore);
                }

                if (!extendedGameController.TurnScript._leftAIIsEliminated)
                {
                    leftAIPoints = leftDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;

                    /*extendedGameController.TurnScript.LeftAIScore += leftAIPoints;

                    extendedGameController.TurnScript._leftAIIsEliminated = extendedGameController.TurnScript.LeftAIScore >= maximumGamepoints;*/

                    roundPoints.Add("Left", leftAIPoints);
                    //accumulatedPoints.Add("Left", extendedGameController.TurnScript.LeftAIScore);
                }

                if (!extendedGameController.TurnScript._topAIIsEliminated)
                {
                    topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;

                    /*extendedGameController.TurnScript.TopAIScore += topAIPoints;

                    extendedGameController.TurnScript._topAIIsEliminated = extendedGameController.TurnScript.TopAIScore >= maximumGamepoints;*/

                    roundPoints.Add("Top", topAIPoints);
                    //accumulatedPoints.Add("Top", extendedGameController.TurnScript.TopAIScore);
                }

                if (!extendedGameController.TurnScript._rightAIIsEliminated)
                {
                    rightAIPoints = rightDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                    
                    /*extendedGameController.TurnScript.RightAIScore += rightAIPoints;

                    extendedGameController.TurnScript._rightAIIsEliminated = extendedGameController.TurnScript.RightAIScore >= maximumGamepoints;*/

                    roundPoints.Add("Right", rightAIPoints);
                    //accumulatedPoints.Add("Right", extendedGameController.TurnScript.RightAIScore);
                }
            }
            else if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                playerPoints = playerDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;

                /*extendedGameController.TurnScript.PlayerScore += playerPoints;
                extendedGameController.TurnScript.TopAIScore += topAIPoints;

                extendedGameController.TurnScript._playerIsEliminated = extendedGameController.TurnScript.PlayerScore >= maximumGamepoints;
                extendedGameController.TurnScript._topAIIsEliminated = extendedGameController.TurnScript.TopAIScore >= maximumGamepoints;*/

                roundPoints.Add("Player", playerPoints);
                roundPoints.Add("Top", topAIPoints);

                /*accumulatedPoints.Add("Player", extendedGameController.TurnScript.PlayerScore);
                accumulatedPoints.Add("Top", extendedGameController.TurnScript.TopAIScore);*/
            }
            else if (vsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                playerPoints = playerDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                leftAIPoints = leftDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                topAIPoints = topDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;
                rightAIPoints = rightDominoes.Sum(tile => tile.TopIndex + tile.BottomIndex) * auxMultiplier;

                /*extendedGameController.TurnScript.PlayerScore += playerPoints;
                extendedGameController.TurnScript.LeftAIScore += leftAIPoints;
                extendedGameController.TurnScript.TopAIScore += topAIPoints;
                extendedGameController.TurnScript.RightAIScore += rightAIPoints;

                pointsTeam_1 = extendedGameController.TurnScript.PlayerScore + extendedGameController.TurnScript.LeftAIScore;
                pointsTeam_2 = extendedGameController.TurnScript.TopAIScore + extendedGameController.TurnScript.RightAIScore;

                extendedGameController.TurnScript._playerIsEliminated = pointsTeam_1 >= maximumGamepoints;
                extendedGameController.TurnScript._leftAIIsEliminated = pointsTeam_1 >= maximumGamepoints;

                extendedGameController.TurnScript._topAIIsEliminated = pointsTeam_2 >= maximumGamepoints;
                extendedGameController.TurnScript._rightAIIsEliminated = pointsTeam_2 >= maximumGamepoints;*/

                roundPoints.Add("Player", playerPoints);
                roundPoints.Add("Left", leftAIPoints);
                roundPoints.Add("Top", topAIPoints);
                roundPoints.Add("Right", rightAIPoints);

                //accumulatedPoints.Add("Points Team 1", pointsTeam_1);
                //accumulatedPoints.Add("Points Team 2", pointsTeam_2);
            }

            Debug.Log($"+++Player Points: {playerPoints}, Left AI Points: {leftAIPoints}, Top AI Points: {topAIPoints}, Right AI Points: {rightAIPoints}");

            //Get the minimum value to validate which player/players have the minimum value and declare him/her the winner of the round or tie and validate if there is already a winner of the game.
            //int minCountAccumulatedPoints = accumulatedPoints.Values.Min();
            int minCountRoundPoints = roundPoints.Values.Min();

            int totalRoundPoints = roundPoints.Values.Sum();
            
            List<string> roundWinners = roundPoints.Where(p => p.Value == minCountRoundPoints).Select(p => p.Key).ToList();
            
            if(playerIdWhoUsedAllTiles != "") //We validate if a player used all his tiles to assign him directly as the winner of the round.
                roundWinners = new List<string> { playerIdWhoUsedAllTiles };
                
            extendedGameController.playerRoundScore = extendedGameController.TurnScript.PlayerScore;
            extendedGameController.leftRoundScore = extendedGameController.TurnScript.LeftAIScore;
            extendedGameController.topRoundScore = extendedGameController.TurnScript.TopAIScore;
            extendedGameController.rightRoundScore = extendedGameController.TurnScript.RightAIScore;

            if (roundWinners.Count > 0)
            {
                if (roundWinners.Count == 1)
                {
                    int auxAccumulatedPoints = 0;
                    int pointsToDeliver = totalRoundPoints - roundPoints[roundWinners[0]];
                    string auxPlayerWinner = "";

                   switch (roundWinners[0])
                    {
                        case "Player":
                            if (vsPlayerSelectedID != NumberPlayers.twoVsTwo)
                            {
                                extendedGameController.TurnScript.PlayerScore += pointsToDeliver;
                                auxAccumulatedPoints = extendedGameController.TurnScript.PlayerScore;
                                auxPlayerWinner = "Player";
                            }
                            else
                            {
                                // Remove points from both players of the winning team to calculate accumulated points correctly
                                pointsToDeliver -= topAIPoints;
                                extendedGameController.TurnScript.PlayerScore += pointsToDeliver;

                                auxAccumulatedPoints = extendedGameController.TurnScript.PlayerScore + extendedGameController.TurnScript.TopAIScore;
                                auxPlayerWinner = "Player and Top";
                            }
                            break;

                        case "Left":
                            if (vsPlayerSelectedID != NumberPlayers.twoVsTwo)
                            {
                                extendedGameController.TurnScript.LeftAIScore += pointsToDeliver;
                                auxAccumulatedPoints = extendedGameController.TurnScript.LeftAIScore;
                                auxPlayerWinner = "Left";
                            }
                            else
                            {
                                // Remove points from both players of the winning team to calculate accumulated points correctly
                                pointsToDeliver -= rightAIPoints;
                                extendedGameController.TurnScript.LeftAIScore += pointsToDeliver;

                                auxAccumulatedPoints = extendedGameController.TurnScript.LeftAIScore + extendedGameController.TurnScript.RightAIScore;
                                auxPlayerWinner = "Left and Right";
                            }
                            break;

                        case "Top":
                            if (vsPlayerSelectedID != NumberPlayers.twoVsTwo)
                            {
                                extendedGameController.TurnScript.TopAIScore += pointsToDeliver;
                                auxAccumulatedPoints = extendedGameController.TurnScript.TopAIScore;
                                auxPlayerWinner = "Top";
                            }
                            else
                            {
                                // Remove points from both players of the winning team to calculate accumulated points correctly
                                pointsToDeliver -= playerPoints;
                                extendedGameController.TurnScript.TopAIScore += pointsToDeliver;

                                auxAccumulatedPoints = extendedGameController.TurnScript.TopAIScore + extendedGameController.TurnScript.PlayerScore;
                                auxPlayerWinner = "Top and Player";
                            }
                            break;

                        case "Right":
                            if (vsPlayerSelectedID != NumberPlayers.twoVsTwo)
                            {
                                extendedGameController.TurnScript.RightAIScore += pointsToDeliver;
                                auxAccumulatedPoints = extendedGameController.TurnScript.RightAIScore;
                                auxPlayerWinner = "Right";
                            }
                            else
                            {
                                // Remove points from both players of the winning team to calculate accumulated points correctly
                                pointsToDeliver -= leftAIPoints;
                                extendedGameController.TurnScript.RightAIScore += pointsToDeliver;

                                auxAccumulatedPoints = extendedGameController.TurnScript.RightAIScore + extendedGameController.TurnScript.LeftAIScore;
                                auxPlayerWinner = "Right and Left";
                            }
                            break;

                        default:
                            Debug.Log("Unidentified player");
                            break;
                    }

                    extendedGameController.playerRoundScore = extendedGameController.TurnScript.PlayerScore - extendedGameController.playerRoundScore;
                    extendedGameController.leftRoundScore = extendedGameController.TurnScript.LeftAIScore - extendedGameController.leftRoundScore;
                    extendedGameController.topRoundScore = extendedGameController.TurnScript.TopAIScore - extendedGameController.topRoundScore;
                    extendedGameController.rightRoundScore = extendedGameController.TurnScript.RightAIScore - extendedGameController.rightRoundScore;

                    if (auxAccumulatedPoints >= limitPointsGameMode)
                    {
                        lastRoundWinner = roundWinners[0];
                        extendedGameController.PlayerWinner = isSinglePlayerVsIA ? roundWinners[0] : auxPlayerWinner;
                        //extendedGameController.PlayerWinner = roundWinners[0];
                        extendedGameController.GameIsCompleteAndFinished = true;

                        return $"Game winner: {auxPlayerWinner} with {auxAccumulatedPoints} points";
                        //return $"Game winner: {roundWinners[0]}";
                    }
                    else
                    {
                        lastRoundWinner = roundWinners[0];
                        extendedGameController.PlayerWinner = isSinglePlayerVsIA ? roundWinners[0] : auxPlayerWinner;
                        //extendedGameController.PlayerWinner = roundWinners[0];
                        return $"Round winner: {roundWinners[0]} and adds {pointsToDeliver} points";
                    }
                }
                else
                {
                    extendedGameController.playerRoundScore = 0;
                    extendedGameController.leftRoundScore = 0;
                    extendedGameController.topRoundScore = 0;
                    extendedGameController.rightRoundScore = 0;

                    extendedGameController.PlayerWinner = "draw";

                    lastRoundWinner = ""; //So that in the next round the player who plays the first turn is chosen randomly.
                    return $"Game Block. Tie between {string.Join(", ", roundWinners)} with <b>{minCountRoundPoints}</b> points!";
                }
            }

            Debug.Log("Error: No winners validated");

            extendedGameController.playerRoundScore = 0;
            extendedGameController.leftRoundScore = 0;
            extendedGameController.topRoundScore = 0;
            extendedGameController.rightRoundScore = 0;

            extendedGameController.PlayerWinner = "error";

            lastRoundWinner = ""; //So that in the next round the player who plays the first turn is chosen randomly.
            return $"No winners validated";
            

            // Get all players that have the minimum value
            //var winners = allPoints.Where(p => p.Value == minCount).Select(p => p.Key).ToList();
            /*List<string> winners = accumulatedPoints.Where(p => p.Value < maximumGamepoints).Select(p => p.Key).ToList();

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
                else //
                {
                    lastRoundWinner = winners[0];

                    extendedGameController.GameIsCompleteAndFinished = true;

                    return $"Game winner: {winners[0]}";
                }
            }
            else
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
            }*/
        }

        private string CheckIfAnyPlayerUsedAllTiles()
        {
            string auxPlayerID_win = "";

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
                if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
                {
                    playerTilesCounter = handOfPlayer_0.Count;
                    topTilesCounter = handOfPlayer_1.Count;
                }
                else
                {
                    playerTilesCounter = handOfPlayer_0.Count;
                    leftTilesCounter = handOfPlayer_1.Count;
                    topTilesCounter = handOfPlayer_2.Count;
                    rightTilesCounter = handOfPlayer_3.Count;
                }
            }

            Debug.Log("Checking if game is over...");

            if (vsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                if (playerTilesCounter == 0)
                    auxPlayerID_win = "Player";
                if (topTilesCounter == 0)
                    auxPlayerID_win = "Top";
            }
            else
            {
                if (playerTilesCounter == 0 && !extendedGameController.TurnScript._playerIsEliminated)
                    auxPlayerID_win = "Player";
                if (leftTilesCounter == 0 && !extendedGameController.TurnScript._leftAIIsEliminated)
                    auxPlayerID_win = "Left";
                if (topTilesCounter == 0 && !extendedGameController.TurnScript._topAIIsEliminated)
                    auxPlayerID_win = "Top";
                if (rightTilesCounter == 0 && !extendedGameController.TurnScript._rightAIIsEliminated)
                    auxPlayerID_win = "Right";
            }

            return auxPlayerID_win;
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
