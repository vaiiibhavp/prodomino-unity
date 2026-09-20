using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
//using Cysharp.Threading.Tasks.Bools;
using DominoTemplate.Controllers;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using ProDomino.Shared;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.GameModes
{
    public class ExtendedDeckController : DeckController
    {
        [SerializeField] private BoneyardManager boneyardManager;

        private AbstractGameMode _abstractGameMode;
        private AbstractGameMode AbstractGameMode => _abstractGameMode 
            ? _abstractGameMode 
            : _abstractGameMode = _gameScript?.GetComponent<AbstractGameMode>() ?? GetComponentInParent<AbstractGameMode>();

        public void SetCustomPlayerHand(List<int> playerHand, int clientId, Action<DragHandler, int> onAssignTileToHand, int numberTilesPerPlayer, Action<int> callback, Action updateBoneyardText)
        {
            StartCoroutine(CustomPlayerHand(playerHand, clientId, onAssignTileToHand, numberTilesPerPlayer, callback, updateBoneyardText));

            //Debug.Log("===> CustomPlayerHand called with playerHand: " + string.Join(", ", playerHand));
        }
        private IEnumerator CustomPlayerHand(List<int> playerHand, int clientId, Action<DragHandler, int> onAssignTileToHand, int numberTilesPerPlayer, Action<int> callback, Action updateBoneyardText)
        {
            _playerTiles = new List<DragHandler>();
            _rightAITiles = new List<DragHandler>();
            _leftAITiles = new List<DragHandler>();
            _topAITiles = new List<DragHandler>();

            yield return new WaitForSeconds(1f);

            int i = 0;

            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                while (i < numberTilesPerPlayer * 4 && _dominoTiles.Count > 0)
                {
                    int rand = 0;//Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < numberTilesPerPlayer) //if (i < 7)
                    {
                        if (!_turnScript._playerIsEliminated)
                        {
                            TileSwapHelper(ref _player, ref _playerTiles, playerHand[i], true, false, remove: false);

                            if (i == numberTilesPerPlayer - 1) //if (i == 6)
                                RemoveLocalPlayerTiles(playerHand); // Remove the last tile from the player's hand
                        }
                        else
                        {
                            i = numberTilesPerPlayer - 1; //i = 6; // Skip the player's hand if they are eliminated
                        }
                    }
                    else if (i < numberTilesPerPlayer * 2) //else if (i < 14)
                    {
                        if (!_turnScript._leftAIIsEliminated)
                        {
                            var localIndex = i - numberTilesPerPlayer; //i - 7;
                            TileSwapHelper(ref _leftAI, ref _leftAITiles, rand, false, true, remove: true, setDefatulTile: true);
                            onAssignTileToHand?.Invoke(_leftAITiles.ElementAtOrDefault(localIndex), 1);
                        } 
                        else
                        {
                            i = (numberTilesPerPlayer * 2) - 1; //i = 13; // Skip the left AI's hand if they are eliminated
                        }
                    }
                    else if (i < numberTilesPerPlayer * 3) //else if (i < 21)
                    {
                        if (!_turnScript._topAIIsEliminated)
                        {
                            var localIndex = i - (numberTilesPerPlayer * 2); //var localIndex = i - (7 * 2);
                            TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true, remove: true, setDefatulTile: true);
                            onAssignTileToHand?.Invoke(_topAITiles.ElementAtOrDefault(localIndex), 2);
                        } 
                        else
                        {
                            i = (numberTilesPerPlayer * 3) - 1; //i = 20; // Skip the top AI's hand if they are eliminated
                        }
                    }
                    else
                    {
                        if (!_turnScript._rightAIIsEliminated)
                        {
                            var localIndex = i - (numberTilesPerPlayer * 3); //var localIndex = i - (7 * 3);
                            TileSwapHelper(ref _rightAI, ref _rightAITiles, rand, false, true, remove: true, setDefatulTile: true);
                            onAssignTileToHand?.Invoke(_topAITiles.ElementAtOrDefault(localIndex), 3);
                        } 
                        else
                        {
                            i = numberTilesPerPlayer * 4; //i = 28; // Skip the right AI's hand if they are eliminated
                        }
                    }

                    i++;
                    yield return new WaitForSeconds(0.2f);
                }
            }
            else
            {
                while (i < numberTilesPerPlayer * 2 && _dominoTiles.Count > 0) //while (i < 14 && _dominoTiles.Count > 0)
                {
                    int rand = 0;//Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < numberTilesPerPlayer) //if (i < 7)
                    {
                        Debug.Log("===> Player hand: " + playerHand[i]);
                        TileSwapHelper(ref _player, ref _playerTiles, playerHand[i], true, false, remove: false);

                        if (i == 6)
                            RemoveLocalPlayerTiles(playerHand); // Remove the last tile from the player's hand
                    }
                    else if (i < numberTilesPerPlayer * 2) //else if (i < 14)
                    {
                        var localIndex = i - numberTilesPerPlayer; //var localIndex = i - 7;
                        TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true, remove: true, setDefatulTile: true);
                        onAssignTileToHand?.Invoke(_topAITiles.ElementAtOrDefault(localIndex), 1);
                    }


                    i++;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            //_deckHolder.transform.localPosition = new Vector3(760, 0, 0); // Move deck to the left
            _deckHolder.transform.localPosition = new Vector3(2000, 0, 0);

            yield return new WaitForSeconds(1f);


            // Update the boneyard text if provided
            if (updateBoneyardText is not null)
                updateBoneyardText?.Invoke();

            if (_dominoTiles != null && _dominoTiles.Count > 0)
            {
                SendTilesToBoneyard();
            }

            Debug.Log("===> All list count -> " + _dominoTiles.Count + " Player count -> " + _playerTiles.Count);
            Debug.Log("===> _rightAITiles count -> " + _rightAITiles.Count + " _leftAITiles count -> " +
                      _leftAITiles.Count + " _topAITiles count -> " + _topAITiles.Count);

            AbstractGameMode?.EnableTileSortingBtn();

            callback?.Invoke(clientId);

            yield return null;
        }

        public void DeckExtendedSetupRandomHands(Func<int> selectPlayerWhoWillTakeFirstTurn, Action updateBoneyardText, List<int> handPlayer_0, List<int> handPlayer_1, List<int> handPlayer_2, List<int> handPlayer_3, int numberTilesPerPlayer = 7, ReplayGameMode replayGameMode = null)
        {
            StartCoroutine(SetupExtendedRandomHands(selectPlayerWhoWillTakeFirstTurn, updateBoneyardText, handPlayer_0, handPlayer_1, handPlayer_2, handPlayer_3, numberTilesPerPlayer, replayGameMode: replayGameMode));
        }

        private IEnumerator SetupExtendedRandomHands(Func<int> selectPlayerWhoWillTakeFirstTurn, Action updateBoneyardText, List<int> handPlayer_0, List<int> handPlayer_1, List<int> handPlayer_2, List<int> handPlayer_3, int numberTilesPerPlayer = 7, ReplayGameMode replayGameMode = null)
        {
            _playerTiles = new List<DragHandler>();
            _rightAITiles = new List<DragHandler>();
            _leftAITiles = new List<DragHandler>();
            _topAITiles = new List<DragHandler>();

            if (!replayGameMode || !replayGameMode.instantExecution)
                yield return new WaitForSeconds(1f);

            int i = 0;

            if (_deckHolder)
                _deckHolder.gameObject.SetActive(true); // Show the deck

            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                while (i < numberTilesPerPlayer * 4 && _dominoTiles.Count > 0) //while (i < 28 && _dominoTiles.Count > 0)
                {
                    bool instantMove = !replayGameMode ? false : !replayGameMode.instantExecution ? false : true;

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

                    //yield return new WaitForSeconds(0.2f);
                    if (!instantMove)
                    {
                        if(!replayGameMode)
                        {
                            yield return new WaitForSeconds(0.2f);
                        }
                        else
                        {
                            float startTimer = Time.time;
                            float duration = 0.2f;

                            yield return new WaitUntil(() => !replayGameMode.pauseExecution && (replayGameMode.instantExecution || Time.time >= startTimer + duration));   
                        }
                    }
                }
            }
            else
            {
                while (i < numberTilesPerPlayer * 2 && _dominoTiles.Count > 0) //while (i < 14 && _dominoTiles.Count > 0)
                {
                    bool instantMove = !replayGameMode ? false : !replayGameMode.instantExecution ? false : true;
                    
                    //int rand = UnityEngine.Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < numberTilesPerPlayer && handPlayer_0.Count > 0) //if (i < 7)
                    {
                        //Debug.Log("===> Player hand: " + i + "/" + rand);
                        TileSwapHelper(ref _player, ref _playerTiles, handPlayer_0[i], true, false, remove: false, instantMove: instantMove);
                    }
                    else if (i < numberTilesPerPlayer * 2 && handPlayer_1.Count > 0) //else if (i < 14)
                    {
                        TileSwapHelper(ref _topAI, ref _topAITiles, handPlayer_1[i - numberTilesPerPlayer], true, true, remove: false, instantMove: instantMove);
                        //TileSwapHelper(ref _topAI, ref _topAITiles, handPlayer_1[i-7], true, true, remove: false);
                    }


                    // Update the boneyard text if provided
                    if (updateBoneyardText is not null)
                        updateBoneyardText?.Invoke();

                    i++;

                    //yield return new WaitForSeconds(0.2f);
                    if (!instantMove)
                    {
                        if(!replayGameMode)
                        {
                            yield return new WaitForSeconds(0.2f);
                        }
                        else
                        {
                            float startTimer = Time.time;
                            float duration = 0.2f;

                            yield return new WaitUntil(() => !replayGameMode.pauseExecution && (replayGameMode.instantExecution || Time.time >= startTimer + duration));   
                        }
                    }
                }
            }

            _deckHolder.transform.localPosition = new Vector3(2000, 0, 0);

            // Wait until the hands are fully set up
            if (!replayGameMode || !replayGameMode.instantExecution)
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
                    AbstractGameMode?.RestartGame(restartNewRound: true);
                    AbstractGameMode?.SetupRandomHands();
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

                    _turnScript.ValideEndTurnLogic("Game Started", 1);

                    AbstractGameMode?.CreateReplayTurn();
                    AbstractGameMode?.EnableBackBtn();
                    AbstractGameMode?.EnableTileSortingBtn();

                }
            }
            else
            {
                if (_gameScript.GameModeSelectedID != GameMode.replay)
                    _turnScript.EndTurn("Game Started", 1);
            }

            Debug.Log("===> All list count -> " + _dominoTiles.Count + " Player count -> " + _playerTiles.Count);
            Debug.Log("===> _rightAITiles count -> " + _rightAITiles.Count + " _leftAITiles count -> " +
                      _leftAITiles.Count + " _topAITiles count -> " + _topAITiles.Count);

            if(replayGameMode != null)
                replayGameMode.isExecution = false;

            yield return null;
        }
        
        #region replay mode
        public IEnumerator SetupHandsInspecificTurnReplay(Action updateBoneyardText, List<int> handPlayer_0, List<int> handPlayer_1, List<int> handPlayer_2, List<int> handPlayer_3)
        {
            _playerTiles = new List<DragHandler>();
            _rightAITiles = new List<DragHandler>();
            _leftAITiles = new List<DragHandler>();
            _topAITiles = new List<DragHandler>();

            int i = 0;

            if (_deckHolder)
                _deckHolder.gameObject.SetActive(true); // Show the deck

            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                foreach (int aux in handPlayer_0)
                {
                    TileSwapHelper(ref _player, ref _playerTiles, aux, true, false, remove: false, instantMove: true);
                }

                foreach (int aux in handPlayer_1)
                {
                    TileSwapHelper(ref _leftAI, ref _leftAITiles, aux, false, true, remove: false, instantMove: true);
                }

                foreach (int aux in handPlayer_2)
                {
                    TileSwapHelper(ref _topAI, ref _topAITiles, aux, true, true, remove: false, instantMove: true);
                }

                foreach (int aux in handPlayer_3)
                {
                    TileSwapHelper(ref _rightAI, ref _rightAITiles, aux, false, true, remove: false, instantMove: true);
                }

                if (updateBoneyardText is not null)
                        updateBoneyardText?.Invoke();
            }
            else
            {
                foreach (int aux in handPlayer_0)
                {
                    TileSwapHelper(ref _player, ref _playerTiles, aux, true, false, remove: false, instantMove: true);
                }

                foreach (int aux in handPlayer_1)
                {
                    TileSwapHelper(ref _topAI, ref _topAITiles, aux, true, true, remove: false, instantMove: true);
                }
                
                // Update the boneyard text if provided
                if (updateBoneyardText is not null)
                        updateBoneyardText?.Invoke();
            }

            _deckHolder.transform.localPosition = new Vector3(2000, 0, 0);

            // Wait until the hands are fully set up
            //yield return new WaitForSeconds(1f);

            List<int> allHands = new List<int>();
            allHands.AddRange(handPlayer_0);
            allHands.AddRange(handPlayer_1);
            allHands.AddRange(handPlayer_2);
            allHands.AddRange(handPlayer_3);

            RemoveLocalPlayerTiles(allHands);

            //yield return new WaitForEndOfFrame();

            // Update the boneyard text if provided
            if (updateBoneyardText is not null)
                updateBoneyardText?.Invoke();

            /*if (_dominoTiles != null && _dominoTiles.Count > 0)
            {
                SendTilesToBoneyard();
                //_deckHolder.transform.localPosition = new Vector3(760, 0, 0); // Move deck to the left
            }*/

            Debug.Log("===> All list count -> " + _dominoTiles.Count + " Player count -> " + _playerTiles.Count);
            Debug.Log("===> _rightAITiles count -> " + _rightAITiles.Count + " _leftAITiles count -> " +
                      _leftAITiles.Count + " _topAITiles count -> " + _topAITiles.Count);
            yield return null;
        }
        #endregion

        private void RemoveLocalPlayerTiles(List<int> playerHand)
        {
            if (playerHand != null && playerHand.Count > 0)
            {
                playerHand.Sort(); // Sort the list in ascending order
                playerHand.Reverse(); // Reverse the list to remove from the end

                foreach (int auxIndex in playerHand)
                {
                    Debug.Log("===> Removing tile with index: " + auxIndex);
                    _dominoTiles?.RemoveAt(auxIndex);
                }
            }
        }

        public void ConcentrateSetupNewGame(int numberOfTiles)
        {
            int i;

            _boardTiles.Clear();
            _boardTiles.Clear();

            _boardTiles = new List<DragHandler>();
            _dominoTiles = new List<DragHandler>();
            i = 0;
            int j = 0;
            //while (i < dominoCount) // Create all dominoes
            while (i < numberOfTiles) // Create all dominoes
            {
                DragHandler tempDomino = Instantiate(_dominoPrefab, _deckHolder.GetChild(0).transform); // Child 0 is Deck container
                DominoView tempDominoView;

                _dominoTiles.Add(tempDomino);
                tempDominoView = _dominoTiles[i].GetDominoView();
                _dominoTiles[i].gameScript = _gameScript;
                if (j < SpriteArray.Length)
                {
                    tempDominoView.LockTile();
                    if (SpriteArray[j] != null)
                        tempDominoView.SetDomino(SetupDominoInfo(j), SpriteArray[j], BackTile);
                    else
                        Debug.Log("Needed Sprite is not exits!!!!");
                }
                else
                    Debug.Log("Something wrong with sprites!!!");

                i++;
                j++;

                if (j > 27)
                    j = 0;
                
            }

            _deckHolder.transform.localPosition = new Vector3(0, -1100, 0);

            //_handScript.SetLockPlayerButtons(false);
        }

        #region Boneyard Methods

        public void SendTilesToBoneyard()
        {
            boneyardManager.InitializeSlotsFromBoneyard();

            // Reorganize the tiles in the graveyard randomly using the Fisher-Yates Shuffle algorithm
            int i = 0;
            for (i = _dominoTiles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1); // Includes the value i
                DragHandler temp = _dominoTiles[i];
                _dominoTiles[i] = _dominoTiles[j];
                _dominoTiles[j] = temp;
            }

            //Assigning tiles to the boneyard interface
            i = 0;
            for (i = 0; i < _dominoTiles.Count; i++)
            {
                Debug.Log("CCC index: " + i);
                _dominoTiles[i].SetToParent(boneyardManager.BoneyardTileContainer.GetChild(i).GetComponent<RectTransform>());
                _dominoTiles[i].transform.localPosition = Vector3.zero;

                if (_gameScript.GameModeSelectedID != GameMode.replay)
                {
                    _dominoTiles[i].gameObject.SetActive(false);
                }
                else
                {
                    _dominoTiles[i].GetDominoView().ChangeBackState(false);
                }
            }

            if (_gameScript.GameModeSelectedID == GameMode.replay)
                i = 0;

            //boneyardManager.TotalTilesInBoneyard = _dominoTiles.Count;

            float auxAlphaButton = i > 0 ? 0.5f : 1f;

            for (int j = i; j < boneyardManager.BoneyardTileContainer.childCount; j++)
            {
                boneyardManager.BoneyardTileContainer.GetChild(j).GetComponent<CustomButtonUI>().SetButtonInteractable(false, auxAlpha: auxAlphaButton);
                boneyardManager.BoneyardTileContainer.GetChild(j).GetComponent<Image>().enabled = false;
                boneyardManager.BoneyardTileContainer.GetChild(j).GetChild(0).gameObject.SetActive(false);

                boneyardManager.RemoveSlotFromBoneyard(boneyardManager.BoneyardTileContainer.GetChild(j));
            }
        }

        public bool TakeSpecificIndexTileFromBoneyard(DragHandler tile_dragHandler, bool instantMove = false)
        {
            if (_dominoTiles.Count > 0)
            {
                //int rand = 0;
                int rand = _dominoTiles.IndexOf(tile_dragHandler);

                DragHandler tileIndex = _dominoTiles[rand];

                switch (_turnScript.GetCurrentPlayerTurn())
                {
                    case "playerTurn":
                        TileSwapHelper(ref _player, ref _playerTiles, rand, true, false, instantMove: instantMove);
                        break;
                    case "leftAITurn":
                        TileSwapHelper(ref _leftAI, ref _leftAITiles, rand, false, true, instantMove: instantMove);
                        break;
                    case "topAITurn":
                        TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true, instantMove: instantMove);
                        break;
                    case "rightAITurn":
                        TileSwapHelper(ref _rightAI, ref _rightAITiles, rand, false, true, instantMove: instantMove);
                        break;
                    default:
                        Debug.LogWarning("Unknown player turn: No player is currently active.");
                        return false;
                }

                Debug.Log("===> Player took tile with index: " + tileIndex.GetDominoView().GetDomino().id);

                if (AbstractGameMode != null)//Temporal code. It should be generic for all game modes, at the moment only French GameMode has boneyard
                {
                    AbstractGameMode.UpdateBoneyardText(); //Temporal code. It should be generic for all game modes, at the moment only French GameMode has boneyard

                    if(_gameScript.GameModeSelectedID != GameMode.replay)
                        AbstractGameMode?.SetTurnActionTakeBoneyardReplay(tileIndex.GetDominoView().GetDomino().id);   
                }

                if(_gameScript.GameModeSelectedID != GameMode.replay)
                    StartCoroutine(ValidateNewTilesInHand());
                            
                //_turnScript.CheckTiles = true;

                return true;
            }
            else
            {
                Debug.LogWarning("No tiles left in the boneyard to take.");

                return false;
            }
        }

        public bool TakeSpecificIndexTileFromBoneyard_FromHost(string playerID, int tileID, DragHandler dragHandler, Action callback)
        {
            if (_dominoTiles.Count > 0)
            {
                //DragHandler dragHandler = _dominoTiles[indexSelected];
                
                if (tileID != -1)
                {
                    //List<DragHandler> AITiles = _deckScript.GetList(3);
                    //DragHandler selectedTile = AITiles[0];

                    DominoView tempDominoView = dragHandler.GetDominoView();
                    tempDominoView.SetDomino(SetupDominoInfo(tileID), SpriteArray[tileID], BackTile);

                    Debug.Log(">++ Tile taken from boneyard with ID: " + tileID);
                }
                else
                {
                    dragHandler.GetDominoView().SetDefaultTile(SpriteArray[0]);
                    //_dominoTiles[indexSelected].GetDominoView().SetDefaultTile(_spriteArray[0]);

                    Debug.Log(">++ Tile taken from boneyard with ID: " + tileID);
                }

                //int rand = 0;
                //int rand = indexSelected;//_dominoTiles.IndexOf(tile_dragHandler);
                int rand = _dominoTiles.IndexOf(dragHandler);

                //DragHandler tileIndex = _dominoTiles[rand];

                switch (playerID)
                //switch (_turnScript.GetCurrentPlayerTurn())
                {
                    case "playerTurn":
                        TileSwapHelper(ref _player, ref _playerTiles, rand, true, false);
                        break;
                    case "leftAITurn":
                        TileSwapHelper(ref _leftAI, ref _leftAITiles, rand, false, true);
                        break;
                    case "topAITurn":
                        TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true);
                        break;
                    case "rightAITurn":
                        TileSwapHelper(ref _rightAI, ref _rightAITiles, rand, false, true);
                        break;
                    default:
                        Debug.LogWarning("Unknown player turn: No player is currently active.");
                        return false;
                }

                //Debug.Log("===> Player took tile with index: " + tileIndex.GetDominoView().GetDomino().id);

                //StartCoroutine(ValidateNewTilesInHand());

                if (this.transform.parent.GetComponent<French_GameMode>() != null)//Temporal code. It should be generic for all game modes, at the moment only French GameMode has boneyard
                {
                    this.transform.parent.GetComponent<French_GameMode>().UpdateBoneyardText(); //Temporal code. It should be generic for all game modes, at the moment only French GameMode has boneyard
                }

                callback?.Invoke();

                //_turnScript.CheckTiles = true;

                return true;
            }
            else
            {
                Debug.LogWarning("No tiles left in the boneyard to take.");

                callback?.Invoke();

                return false;
            }
        }

        private IEnumerator ValidateNewTilesInHand()
        {
            yield return new WaitForSeconds(.5f);

            _turnScript.CheckTiles = true;
        }

        public bool TakeTileFromBoneyard()
        {
            Debug.Log("===> _dominoTiles.Count: " + _dominoTiles.Count);

            if (_dominoTiles.Count > 0)
            {
                //int rand = 0;
                int rand = UnityEngine.Random.Range(0, _dominoTiles.Count);

                Debug.Log("===> Random index selected: " + rand);

                DragHandler tileIndex = _dominoTiles[rand];

                switch (_turnScript.GetCurrentPlayerTurn())
                {
                    case "playerTurn":
                        TileSwapHelper(ref _player, ref _playerTiles, rand, true, false);
                        break;
                    case "leftAITurn":
                        TileSwapHelper(ref _leftAI, ref _leftAITiles, rand, false, true);
                        break;
                    case "topAITurn":
                        TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true);
                        break;
                    case "rightAITurn":
                        TileSwapHelper(ref _rightAI, ref _rightAITiles, rand, false, true);
                        break;
                    default:
                        Debug.LogWarning("Unknown player turn: No player is currently active.");
                        return false;
                }

                Debug.Log("===> Player took tile with index: " + tileIndex.GetDominoView().GetDomino().id);

                _turnScript.CheckTiles = true;

                return true;
            }
            else
            {
                Debug.LogWarning("No tiles left in the boneyard to take.");

                return false;
            }
        }

        public int GetTotalTilesInBoneyard()
        {
            if (_dominoTiles is null)
            { 
                Debug.LogWarning("GetTotalTilesInBoneyard: _dominoTiles is null.");
                return 0;
            }
            return _dominoTiles.Count;
        }

        protected override void SetupNewGame()
        {
            base.SetupNewGame();

            // MainReferenceInitialize the boneyard manager if it exists
            if (_dominoTiles is not null and { Count: > 0 })
                foreach (var tile in _dominoTiles)
                    if (_gameScript is ExtendedGameController extendedGameController)
                        tile.Initialize(extendedGameController.RectTransformPanZoomController, _analyticsManager);
        }

        /// <summary>
        /// Overrides the sprite array and back tile sprite used for domino tiles.<br></br>
        /// </summary>
        /// <param name="spriteArray"></param>
        /// <param name="backTile"></param>
        public void OverrideSpriteArray(Sprite[] spriteArray, Sprite backTile)
        { 
            if (spriteArray is not null and { Length: >= 28 })
            {
                _spriteArray = spriteArray;
            }
            else
                Debug.LogError("OverrideSpriteArray: Invalid sprite array provided. It must contain exactly 28 sprites.");

            if (backTile is not null)
            {
                _backTile = backTile;
            }
            else
                Debug.LogError("OverrideSpriteArray: Invalid back tile sprite provided. It cannot be null.");
        }

        /// <summary>
        /// Overrides the skin of the specified domino views with the provided skin ID.<br></br>
        /// This method is used mainly for changing the visual appearance of domino tiles in multiplayer game
        /// </summary>
        /// <param name="skinID"></param>
        /// <param name="dominoViews"></param>
        public void OverrideTilesSkin(string skinID, params DominoView[] dominoViews)
        {
            if (dominoViews is null or { Length: 0 })
            {
                Debug.LogWarning("No domino views provided for skin override.");
                return;
            }

            var skinCollection = _dictionaryService.GetSpriteCollection($"{CosmeticType.Tiles.ToString()}_{skinID}");
            var backTile = _dictionaryService.GetSprite(CosmeticType.Tiles.ToString(), $"{skinID}_{Consts.CollectionKeys.Back}");

            if (skinCollection is null or { Length: not 28 })
            {
                Debug.LogWarning($"Skin with ID '{skinID}' not found or does not contain 28 sprites.");
                return;
            }

            if (backTile is null)
            {
                Debug.LogWarning($"Back tile sprite for skin '{skinID}' not found. Using default back tile.");
                return;
            }

            foreach (var dominoView in dominoViews)
            {
                var skinSearched = skinCollection.ElementAtOrDefault(dominoView.GetDomino().id);
                if (skinSearched != null)
                    dominoView.OverrideSkin(skinSearched, backTile);

                else
                    Debug.LogWarning($"No sprite found for domino ID {dominoView.GetDomino().id} in skin '{skinID}'.");
            }
        }

        /// <summary>
        /// Overrides the skin of the boneyard tiles with the provided skin ID.<br></br>
        /// This method is used mainly for changing the visual appearance of boneyard tiles in multiplayer game
        /// </summary>
        /// <param name="skinID"></param>
        public void OverrideBoneyardSkin(string skinID)
        { 
            if (!boneyardManager)
            {
                Debug.LogWarning("BoneyardManager is not assigned. Cannot override boneyard skin.");
                return;
            }

            boneyardManager.OverrideTilesSkin(skinID);
        }
        #endregion
    }
}