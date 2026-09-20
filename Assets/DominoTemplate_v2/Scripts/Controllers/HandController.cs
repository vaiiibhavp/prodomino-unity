using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DominoTemplate.Controllers
{
    public class HandController : MonoBehaviour
    {
        protected DeckController _deckScript;
        protected SlotHelper _slotPosScript;
        public SlotHelper SlotPosScript => _slotPosScript;

        private GameControler _gameScript;
        private GameTurnController _gameTurnController;

        private Func<Domino[]> _getFirstTurnAvailableTiles;
        private Coroutine _waitForPlayerPassCoroutine;

        [SerializeField] private CustomButtonUI playerPassButton = null;
        public CustomButtonUI PlayerPassButton => playerPassButton;
        [SerializeField] private float passButtonFeedbackTime = .25f;

        public bool IsPlayerTurn => _gameTurnController?.GetPlayerTurn() ?? false;
        public bool IsPlayerPassing { get; private set; }

        [SerializeField] protected Action<bool> hand_playerChangesTurn = null; //Asigned tue event from game mode script
        public Action<bool> Hand_playerChangesTurn
        {
            get => hand_playerChangesTurn;
            set => hand_playerChangesTurn = value;
        }

        public void SetAllRefs(GameControler newGame, DeckController newDeck, SlotHelper newSlot, GameTurnController gameTurnController, Func<Domino[]> getFirstTurnAvailableTiles)
        {
            _gameScript = newGame;
            _deckScript = newDeck;
            _slotPosScript = newSlot;
            _gameTurnController = gameTurnController;
            _getFirstTurnAvailableTiles = getFirstTurnAvailableTiles;
        }

        private void UnlockTile(List<DragHandler> tempList, bool isPlayer)
        {
            int i;

            i = 0;
            if (tempList is not null and { Count: > 0 })
                while (i < tempList.Count)
                {
                    tempList[i].GetDominoView().UnLockTile(isPlayer);
                    i++;
                }
        }

        public void LockTilesHere(List<DragHandler> tempList)
        {
            int i;

            i = 0;
            if (tempList is not null and { Count: > 0 })
                while (i < tempList.Count)
                {
                    tempList[i].GetDominoView().LockTile();
                    i++;
                }
        }

        public void LockAllTiles()
        {
            int i;

            i = 1;
            while (i < 5)
            {
                LockTilesHere(_deckScript.GetList(i));
                i++;
            }
        }

        public void SetLockPlayerButtons(bool theLock)
        {
            playerPassButton.SetButtonInteractable(theLock);
        }


        public void SetTurnFor(bool player, bool leftAI, bool topAI, bool rightAI, bool isFromHost = false)
        {
            //isFromHost = false;

            LockAllTiles();
            SetLockPlayerButtons(false);

            hand_playerChangesTurn?.Invoke(player);

            if (player)
            {
                var isPlayerPassing = false;

                // Check if the Player does not have any available tiles to play
                if (PlayerAvalibleTiles() == 0)
                {
                    Debug.Log("++-- Player Deck_HandleNoValidMovesInGameMode");
                    // Check if there is custom logic for handling no valid moves in the game mode
                    if (_deckScript.Deck_HandleNoValidMovesInGameMode != null)
                    {
                        bool auxValidMovesInGameMode = _deckScript.Deck_HandleNoValidMovesInGameMode.Invoke(true); //Here: At this moment enable the boneyard

                        // If there are no valid moves in the game mode, we can pass the turn
                        isPlayerPassing = !auxValidMovesInGameMode;
                    }

                    // If there is no custom logic for handling no valid moves, we assume the player can pass
                    else
                        isPlayerPassing = true;
                }

                // If the player has available tiles and the boneyard is not required; Invoke and event 
                else
                    _deckScript.Deck_HandleHasValidMoves?.Invoke();

                // If the player has available tiles, we unlock them
                playerPassButton.SetButtonInteractable(isPlayerPassing);

                // Register the player as passing if they have no valid moves
                IsPlayerPassing = isPlayerPassing;

                if (IsPlayerPassing)
                    StartPassButtonHighlight();
            }
            else if (leftAI && !isFromHost)
                UnlockTile(_deckScript.GetList(2), false);
            else if (topAI && !isFromHost)
                UnlockTile(_deckScript.GetList(3), false);
            else if (rightAI && !isFromHost)
                UnlockTile(_deckScript.GetList(4), false);
        }


        private int PlayerAvalibleTiles()
        {
            int rightNum = -1;
            int leftNum = -1;
            int topNum = -1;
            int downNum = -1;
            List<int> doubleTilesEnabled = new List<int>();

            int i = 0;
            List<DragHandler> playerTileList = _deckScript.GetList(1);

            // If this is the first turn
            if (_gameTurnController is { TurnCount: 0 })
            {
                var initialTiles = _getFirstTurnAvailableTiles?.Invoke()?.ToList() ?? default;

                // Filter the AI tiles based on the initial available tiles
                if (initialTiles is not null and { Count: > 0 })
                    playerTileList = playerTileList.Where(tile => initialTiles.Any(x => tile.GetDominoView().GetDomino().id == x.id)).ToList();
            }

            int unlocked = 0;

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            while (i < playerTileList.Count)
            {
                DominoView dominoView = playerTileList[i].GetDominoView();
                Domino dominoInfo = dominoView.GetDomino();

                if (dominoInfo.TopIndex == rightNum || dominoInfo.BottomIndex == rightNum
                                                    || dominoInfo.TopIndex == leftNum ||
                                                    dominoInfo.BottomIndex == leftNum || dominoInfo.TopIndex == topNum ||
                                                    dominoInfo.BottomIndex == topNum || dominoInfo.TopIndex == downNum ||
                                                    dominoInfo.BottomIndex == downNum )
                {
                    dominoView.UnLockTile(true);
                    unlocked++;
                    Debug.Log("Enter 01_01: " + unlocked);
                }
                else if (rightNum == -1 || leftNum == -1 || topNum == -1 || downNum == -1)
                {
                    dominoView.UnLockTile(true);
                    unlocked++;
                    Debug.Log("Enter 01_02: " + unlocked);
                }

                foreach (int aux in doubleTilesEnabled) //To validate the double tiles that can be placed
                {
                    if (dominoInfo.TopIndex == aux && dominoInfo.BottomIndex == aux)
                    {
                        dominoView.UnLockTile(true);
                        unlocked++;
                        break;
                    }
                }

                i++;
            }

            Debug.Log("Enter 01_0: " + unlocked);

            return unlocked;
        }
        
        public int PlayerAvalibleTilesWithCustomID(int playerID, List<int> handOfPlayer)
        {
            int rightNum = -1;
            int leftNum = -1;
            int topNum = -1;
            int downNum = -1;
            List<int> doubleTilesEnabled = new List<int>();

            int i = 0;
            List<Domino> playerTileList = new List<Domino>();

            foreach (int tileID in handOfPlayer)
            {
                Domino domino = _deckScript.SetupDominoInfo(tileID);
                playerTileList.Add(domino);
            }

            int unlocked = 0;

            _slotPosScript.TellBranchNums(ref rightNum, ref leftNum, ref topNum, ref downNum, ref doubleTilesEnabled);
            while (i < playerTileList.Count)
            {
                Domino dominoInfo = playerTileList[i];

                if (dominoInfo.TopIndex == rightNum || dominoInfo.BottomIndex == rightNum
                                                    || dominoInfo.TopIndex == leftNum ||
                                                    dominoInfo.BottomIndex == leftNum || dominoInfo.TopIndex == topNum ||
                                                    dominoInfo.BottomIndex == topNum || dominoInfo.TopIndex == downNum ||
                                                    dominoInfo.BottomIndex == downNum)
                {
                    unlocked++;
                }
                else if (rightNum == -1 || leftNum == -1 || topNum == -1 || downNum == -1)
                {
                    unlocked++;
                }

                foreach (int aux in doubleTilesEnabled) //To validate the double tiles that can be placed
                {
                    if (dominoInfo.TopIndex == aux && dominoInfo.BottomIndex == aux)
                    {
                        unlocked++;
                    }
                }

                i++;
            }

            return unlocked;
        }

        /// <summary>
        /// Starts a coroutine to highlight the pass button when the player has to pass their turn.
        /// </summary>
        public void StartPassButtonHighlight()
        {
            // If the coroutine is already running, stop it first
            if (_waitForPlayerPassCoroutine != null)
            {
                playerPassButton.PreviewVisualState(false);

                StopCoroutine(_waitForPlayerPassCoroutine);
                _waitForPlayerPassCoroutine = null;
            }

            // Start the coroutine to highlight the pass button
            _waitForPlayerPassCoroutine = StartCoroutine(WaitForPlayerPass());

            // Coroutine to wait for the player to pass
            IEnumerator WaitForPlayerPass()
            {
                var isSelecting = false;
                var playerPutCursorOverPassButton = false;

                // Wait until the player puts the cursor over the pass button
                while (!playerPutCursorOverPassButton)
                {
                    // Check if the player is hovering over the pass button
                    playerPutCursorOverPassButton = playerPassButton.IsPointerOver;

                    // Toggle the button visual state for feedback
                    var targetAlpha = isSelecting ? 1f : 0f;
                    playerPassButton.PreviewVisualState(isSelecting);

                    // Only toggle the state if the alpha reached the target
                    if (playerPassButton.Alpha is 1 or 0)
                        isSelecting = !isSelecting;

                    // Wait for the next frame
                    yield return new WaitForSeconds(passButtonFeedbackTime);
                }

                playerPassButton.PreviewVisualState(false);
                _waitForPlayerPassCoroutine = null;
            }
        }

        /// <summary>
        /// Stops the pass button highlight coroutine and resets the button visual state.
        /// </summary>
        public void StopPassButtonHighlight()
        {
            // Check if the coroutine is running
            if (_waitForPlayerPassCoroutine != null)
            {
                // Reset the button visual state
                playerPassButton.PreviewVisualState(false);

                // Stop the coroutine and clear the reference
                StopCoroutine(_waitForPlayerPassCoroutine);
                _waitForPlayerPassCoroutine = null;
            }
        }
    }
}