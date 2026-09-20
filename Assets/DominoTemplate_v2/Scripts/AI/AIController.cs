using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DominoTemplate.Controllers;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using Timba.Utils;
using ProDomino.Shared;
using UnityEngine;
using UnityEngine.Events;

namespace DominoTemplate.AI
{
    public class AIController : MonoBehaviour
    {
        [SerializeField] private AICheater _AICheaterScript = null;
        [Tooltip("Determine if the AI will use the default best tile defined by the template or a custom one.")]
        [SerializeField] private bool isUsingDefaultBestTile;

        private GameControler _gameScript;
        private DeckController _deckScript;
        private SlotHelper _slotPosScript;
        private GameTurnController _turnScript;

        private bool _isPassing;
        private int _difficulty;
        private Func<Domino[]> _getFirstTurnAvailableTiles;
        private Func<Domino> _getBestTile;
        private Func<string> _getCurrentDisplayName;
        private UnityAction _onShowingLastTile;

        public bool IsDropingTile { get; private set; }

        public void SetAllRefs(GameControler newGame, DeckController newDeck, SlotHelper newSlot,
            GameTurnController newTurn, Func<Domino[]> getFirstTurnAvailableTiles, Func<Domino> getBestTile, Func<string> getCurrentDisplayName,
            UnityAction onShowingLastTile)
        {
            _gameScript = newGame;
            _deckScript = newDeck;
            _slotPosScript = newSlot;
            _turnScript = newTurn;

            _getFirstTurnAvailableTiles = getFirstTurnAvailableTiles;
            _getBestTile = getBestTile;
            _getCurrentDisplayName = getCurrentDisplayName;

            _onShowingLastTile = onShowingLastTile;
        }

        public void SetDifficulty(int newDifficult)
        {
            _difficulty = newDifficult;
            _AICheaterScript.SetData(_deckScript, _slotPosScript,
                this, _difficulty);
        }

        public List<DragHandler> GetMatchingTiles(List<DragHandler> tempTiles)
        {
            int i;
            List<DragHandler> matchingTiles = new List<DragHandler>();
            int rightNum = -1;
            int leftNum = -1;

            int topNum = -1;
            int downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            i = 0;
            while (i < tempTiles.Count)
            {
                DominoView dominoView = tempTiles[i].GetDominoView();
                Domino dominoInfo = dominoView.GetDomino();

                if (dominoInfo.TopIndex == rightNum || dominoInfo.BottomIndex == rightNum
                                                    || dominoInfo.TopIndex == leftNum ||
                                                    dominoInfo.BottomIndex == leftNum
                                                    || rightNum == -1 || leftNum == -1
                                                    || dominoInfo.TopIndex == topNum
                                                    || dominoInfo.BottomIndex == topNum
                                                    || dominoInfo.TopIndex == downNum ||
                                                    dominoInfo.BottomIndex == downNum
                                                    || topNum == -1 || downNum == -1)
                {
                    matchingTiles.Add(tempTiles[i]);
                }

                foreach (int aux in doubleTilesEnabled) //To validate the double tiles that can be placed
                {
                    if (dominoInfo.TopIndex == aux && dominoInfo.BottomIndex == aux)
                    {
                        matchingTiles.Add(tempTiles[i]);
                        break;
                    }
                }

                i++;
            }

            return matchingTiles;
        }

        private DragHandler PickRandomTile(List<DragHandler> tempTiles)
        {
            List<DragHandler> matchingTiles;
            int rand;

            matchingTiles = GetMatchingTiles(tempTiles);
            // Pass if no matching tiles
            if (matchingTiles.Count == 0)
                return null;
            rand = UnityEngine.Random.Range(0, matchingTiles.Count);

            return matchingTiles[rand];
        }


        private bool CheckStart()
        {
            int rightNum = -1;
            int leftNum = -1;

            int topNum = -1;
            int downNum = -1;

            List<int> doubleTilesEnabled = new List<int>();

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            if (rightNum == -1 || leftNum == -1 || topNum == -1 || downNum == -1)
                return true;
            return false;
        }

        public IEnumerator DropTile(string aiName, int aiNum, DragHandler toDrop) //AQUI!
        {
            DominoView tempDominoView = toDrop.GetDominoView();

            yield return new WaitForSeconds(UnityEngine.Random.Range(0.5f, 3f));

            // Determine if the AI is dropping a tile
            IsDropingTile = true;

            _gameScript.currentDrag = tempDominoView;
            yield return StartCoroutine(_gameScript.CreateSlot().ToCoroutine());
            _gameScript.currentDrag = null;

            RectTransform rightSlot = null;
            RectTransform leftSlot = null;

            RectTransform topSlot = null;
            RectTransform downSlot = null;

            _gameScript.GetSlots(ref rightSlot, ref leftSlot, ref topSlot, ref downSlot);

            // Wait for the end of the frame to ensure slots are ready (created or destroyed)
            yield return new WaitForEndOfFrame();

            if (rightSlot != null && leftSlot != null && topSlot != null && downSlot != null)
            {
                var rand = UnityEngine.Random.Range(0, 4);

                if (CheckStart())
                    rand = 0;

                //It's not necessary to include the cases for topSlot and downSlot because this is the first tile played in the entire game
                if (rand == 0) 
                    StartCoroutine(toDrop.LerpMove(rightSlot, isAI: true, onFinishDrag: () => OnFinishDrag(aiName, aiNum)));
                else
                {
                    var targetSlot = rand switch
                    {
                        1 => leftSlot,
                        2 => topSlot,
                        3 => downSlot,
                        _ => throw new NotImplementedException("[AIController] Slot not selected"),
                    };

                    StartCoroutine(toDrop.LerpMove(targetSlot, isAI: true, onFinishDrag: () => OnFinishDrag(aiName, aiNum)));
                }
            }
            else
            {
                var possibleSlots = new List<RectTransform>()
                {
                    rightSlot, leftSlot, topSlot, downSlot,
                };

                var targetSlot = possibleSlots?.FirstOrDefault(x => x != null);
                if (!targetSlot)
                    Debug.LogError("[AIController] Each slot is null. Couldn't displace tile. This is a bug");
                StartCoroutine(toDrop.LerpMove(targetSlot, isAI: true, onFinishDrag: () => OnFinishDrag(aiName, aiNum)));
            }

            yield return null;
        }

        private async UniTask OnFinishDrag(string aiName, int aiNum)
        {
            // Once the tile is dropped, we need to update the game state
            _deckScript.ControlAllHands();

            var leftingTiles = _gameScript.DeckController.GetList(aiNum).Count;
            if (leftingTiles is 1)
            {
                _onShowingLastTile?.Invoke();
                await _turnScript.AlertTextIE($"Watch out! {aiName} is about to finish.", 3f);
            }
            
            IsDropingTile = false;
        }

        public async void PassTurn(bool isWaiting = false)
        {
            if (_isPassing)
                return;

            var owner = _getCurrentDisplayName?.Invoke() ?? (_turnScript.GetPlayerTurn() ? "Player" : "AI");
            _isPassing = true;
            _gameScript.TurnScript.AlertText($"{owner} has passed", 3);

            // Wait a while before passing the turn
            if (isWaiting)
                await UniTask.WaitForSeconds(3);

            Debug.Log("===========PASS==========");

            if(_gameScript.GameModeSelectedID != ProDomino.Shared.GameMode.replay)
            {
                string auxMsg = await LocalizationHelper.Get("haspassed");

                _turnScript.PassBehaviour();
                _turnScript.EndTurn($"{owner} " + auxMsg, 1); //_turnScript.EndTurn($"{owner} has passed", 1); // TODO: validate if is better to set arg in "null"
                _gameScript.RemoveAllSlots();   
            }

            _isPassing = false;
        }


        public void MakeTurn(int aiNum, bool nextPlayer)
        {
            List<DragHandler> AITiles;
            DragHandler selectedTile = null;

            if (aiNum < 2 || aiNum > 4)
            {
                Debug.Log("Wrong AI Turn");
                return;
            }

            // Register a non filtered list of tiles for the current AI
            AITiles = _deckScript.GetList(aiNum);

            // Only if it's the first turn, we filter the AI tiles based on the initial available tiles
            if (_turnScript is { TurnCount: 0 })
            {
                var initialTiles = _getFirstTurnAvailableTiles?.Invoke()?.ToList() ?? default;

                // Filter the AI tiles based on the initial available tiles
                if (initialTiles is not null and { Count: > 0 })
                    AITiles = AITiles.Where(tile => initialTiles.Any(x => tile.GetDominoView().GetDomino().id == x.id)).ToList();
            } 
            
            if (isUsingDefaultBestTile)
                GetBestTileUsingTemplate();
            else
                GetBestTileUsingCustomHelper();

            if (selectedTile == null)
            {
                if (_deckScript.Deck_HandleNoValidMovesInGameMode != null)
                {
                    Debug.Log("++-- IA Deck_HandleNoValidMovesInGameMode");
                    bool auxBoneyardResult = _deckScript.Deck_HandleNoValidMovesInGameMode.Invoke(false);

                    if (!auxBoneyardResult)
                    { 
                        _gameScript.OnPassingTurn(false);
                        PassTurn(true);
                    }
                }
                else
                {
                    _gameScript.OnPassingTurn(false);
                    PassTurn(true);
                }

                // If the AI has no valid moves, end the turn
                IsDropingTile = false;

                //PassTurn();
            }
            else
            {
                var owner = _getCurrentDisplayName?.Invoke() ?? (_turnScript.GetPlayerTurn() ? "Player" : "AI");

                StartCoroutine(DropTile(owner, aiNum, selectedTile));
                _deckScript.Deck_HandleHasValidMoves?.Invoke();
            }

            void GetBestTileUsingTemplate()
            {
                // Check for Medium difficulty
                if (_difficulty == 1)
                    selectedTile = PickRandomTile(AITiles);
                else
                {
                    List<DragHandler> nextTiles;
                    if (aiNum == 4)
                        nextTiles = _deckScript.GetList(1);
                    else
                        nextTiles = _deckScript.GetList(aiNum + 1);

                    // Check for easy or hard difficulty
                    selectedTile = _AICheaterScript.PickHardTile(AITiles, nextTiles, nextPlayer);
                }
            }

            void GetBestTileUsingCustomHelper()
            { 
                // Check for easy difficulty
                if (_difficulty == 0)
                { 
                    selectedTile = PickRandomTile(AITiles);
                    return;
                }

                // Check for medium difficulty
                else if (_difficulty == 1)
                { 
                    var isChoosingEasy = UnityEngine.Random.Range(0f, 1f) < 0.5f;
                    if (isChoosingEasy)
                    { 
                        selectedTile = PickRandomTile(AITiles);
                        return;
                    }
                }

                // Check for hard difficulty
                var dominoTile = _getBestTile?.Invoke();
                if (dominoTile != null)
                    selectedTile = AITiles.Find(tile => tile.GetDominoView().GetDomino().IsValue(dominoTile.TopIndex, dominoTile.BottomIndex));
            }
        }
        
        public void PlayRandomMove(out bool hasPassed, out bool hasPlayedATile)
        {
            // Register a non filtered list of tiles for the current AI
            var turnControl = _turnScript.GetCurrentTurnControl();
            var tiles = _deckScript.GetList(turnControl);

            // By default, the AI has not passed
            hasPassed = false;

            // By default, the AI has not played a tile
            hasPlayedATile = false;

            // Only if it's the first turn, we filter the AI tiles based on the initial available tiles
            if (_turnScript is { TurnCount: 0 })
            {
                var initialTiles = _getFirstTurnAvailableTiles?.Invoke()?.ToList() ?? default;

                // Filter the AI tiles based on the initial available tiles
                if (initialTiles is not null and { Count: > 0 })
                    tiles = tiles.Where(tile => initialTiles.Any(x => tile.GetDominoView().GetDomino().id == x.id)).ToList();
            }

            var selectedTile = PickRandomTile(tiles);
            if (!selectedTile)
            {
                if (_deckScript.Deck_HandleNoValidMovesInGameMode != null)
                {
                    Debug.Log("++-- Deck_HandleNoValidMovesInGameMode");
                    bool auxBoneyardResult = _deckScript.Deck_HandleNoValidMovesInGameMode.Invoke(false);

                    if (!auxBoneyardResult)
                    { 
                        _gameScript.OnPassingTurn(false);
                        PassTurn(true);

                        hasPassed = true;
                    }
                }
                else
                {
                    _gameScript.OnPassingTurn(false);
                    PassTurn(true);

                    hasPassed = true;
                }

                // If the AI has no valid moves, end the turn
                IsDropingTile = false;
            }
            else
            {
                var owner = _getCurrentDisplayName?.Invoke() ?? (_turnScript.GetPlayerTurn() ? "Player" : "AI");

                StartCoroutine(DropTile(owner, turnControl, selectedTile));
                _deckScript.Deck_HandleHasValidMoves?.Invoke();

                hasPlayedATile = true;
            }
        }
    }
}