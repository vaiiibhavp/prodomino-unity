using System;
using System.Collections.Generic;
using System.Collections;
using DominoTemplate.AI;
using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using UnityEngine;
using ProDomino.Shared;
using ProDomino.AnalyticsSystem;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Timba.Utils;
using UnityEngine.Events;

namespace DominoTemplate.Controllers
{
    public class GameControler : MonoBehaviour
    {
        public static Action<List<RectTransform>> SetSlotPoosition;

        [HideInInspector] public DominoView currentDrag;

        [SerializeField] public RectTransform _gameBoard = null;
        public RectTransform GameBoard => _gameBoard;
        [SerializeField] protected EmptySlot _slotPrefab = null;
        [SerializeField] protected HandController _handScript = null;
        [SerializeField] protected RectTransform _ownRect = null;
            
        [SerializeField] protected SlotHelper _slotPosScript = null;
        public SlotHelper SlotPosScript => _slotPosScript;

        [SerializeField] protected AIController _AIScript = null;
        public AIController AIController => _AIScript;

        [SerializeField] protected DeckController _deckScript = null;
        public DeckController DeckScript => _deckScript;


        [SerializeField] protected GameTurnController _turnScript = null;
        public GameTurnController TurnScript => _turnScript;

        [SerializeField] protected GameMode gameModeSelectedID = GameMode.none;
        public GameMode GameModeSelectedID => gameModeSelectedID;
        [SerializeField] protected GameType gameTypeSelectedID = GameType.none;
        public GameType GameTypeSelectedID => gameTypeSelectedID;
        [SerializeField] protected NumberPlayers vsPlayerSelectedID = NumberPlayers.none;
        public NumberPlayers VsPlayerSelectedID => vsPlayerSelectedID;

        [SerializeField] protected GameObject leftAI_container;
        [SerializeField] protected GameObject topAI_container;
        [SerializeField] protected GameObject rightAI_container;
        [SerializeField] protected GameObject playerAI_container;

        [SerializeField] protected List<RectTransform> _slotList = new List<RectTransform>();

        protected RectTransform slot;
        protected RectTransform slot2;
        protected RectTransform slot3;
        protected RectTransform slot4;
        
        protected Action<bool> _setVisibleGameplay;
        protected Func<Domino[]> _getFirstTurnAvailableTiles;
        protected Func<Domino> _getBestTile;
        protected Func<string> _getCurrentDisplayName;
        protected UnityAction _onShowingLastTile;

        private DictionaryService _dictionaryService;
        private AnalyticsManager _analyticsManager;
        protected bool _slotsCreated;

        public GameMode GameMode => gameModeSelectedID;
        public HandController HandController => _handScript;
        public DeckController DeckController => _deckScript;
        public GameTurnController GameTurnController => _turnScript;
        public EmptySlot SlotPrefab => _slotPrefab;

        [SerializeField] protected Action<int, string, Vector3, Quaternion, Vector2> gameController_SetTileIDUsedInShowBoard;
        public Action<int, string, Vector3, Quaternion, Vector2> GameController_SetTileIDUsedInShowBoard
        {
            get => gameController_SetTileIDUsedInShowBoard;
            set => gameController_SetTileIDUsedInShowBoard = value;
        }

        private void OnEnable()
        {
            DragHandler.CreateSlot += CreateSlot;
        }

        private void OnDisable()
        {
            DragHandler.CreateSlot -= CreateSlot;
        }

        private void Update()
        {
            if (!_turnScript)
            { 
                Debug.LogError($"<color={Consts.Colors.Error}>[{nameof(GameControler)}] Update: GameTurnController reference is missing!</color>");
                return;
            }

            // Process Game Turn
            _turnScript.GameProcess();
        }

        public RectTransform GetRect()
        {
            return _ownRect;
        }

        public void Initialize
            (Action<bool> setVisibleGameplay, 
            Func<Domino[]> getFirstTurnAvailableTiles, 
            Func<Domino> getBestTile, 
            Func<string> getCurrentDisplayName,
            UnityAction onShowingLastTile,
            DictionaryService dictionaryService,
            AnalyticsManager analyticsManager)
        {
            _setVisibleGameplay = setVisibleGameplay;
            _getFirstTurnAvailableTiles = getFirstTurnAvailableTiles;
            _getBestTile = getBestTile;
            _getCurrentDisplayName = getCurrentDisplayName;
            _dictionaryService = dictionaryService;
            _analyticsManager = analyticsManager;
            _onShowingLastTile = onShowingLastTile;
        }

        public virtual void RestartGame(int difficulty, GameMode gameModeID, GameType gameTypeID, NumberPlayers vsPlayerID, bool restartNewRound = false)
        {
            gameModeSelectedID = gameModeID;
            gameTypeSelectedID = gameTypeID;
            vsPlayerSelectedID = vsPlayerID;

            ClearGameBoard();

            SetupComponentReferences();
            _turnScript.InitGameVariables();

            if(!restartNewRound)
                _turnScript.InitEliminationsAndScoring();

            _AIScript.SetDifficulty(difficulty);
            _deckScript.RestartDominos();
            _slotPosScript.RestartSlotHelper();

            var areFourPeople = vsPlayerSelectedID == NumberPlayers.oneVsThree || vsPlayerSelectedID == NumberPlayers.twoVsTwo;
            topAI_container.SetActive(vsPlayerSelectedID != NumberPlayers.solo); //topAI_container.SetActive(true);
            leftAI_container.SetActive(areFourPeople);
            rightAI_container.SetActive(areFourPeople);

            Debug.Log("GameControler: RestartGame ejecutado");
        }

        protected virtual void ClearGameBoard()
        {
            if (_gameBoard == null)
            {
                Debug.LogError("GameBoard is not assigned!");
                return;
            }

            for (int i = _gameBoard.childCount - 1; i >= 0; i--)
            {
                Transform child = _gameBoard.GetChild(i);
                Destroy(child.gameObject);
            }
        }

        private void SetupComponentReferences()
        {
            _AIScript.SetAllRefs(this, _deckScript, _slotPosScript, _turnScript, _getFirstTurnAvailableTiles, _getBestTile, _getCurrentDisplayName, _onShowingLastTile);
            _deckScript.SetAllRefs(this, _handScript, _turnScript, _analyticsManager, _dictionaryService);
            _handScript.SetAllRefs(this, _deckScript, _slotPosScript, _turnScript, _getFirstTurnAvailableTiles);
            _turnScript.SetAllRefs(this, _handScript, _deckScript, _AIScript);
        }

        // Creates Slot for Domino Tile
        public virtual UniTask CreateSlot()
        {
            if (currentDrag == null)
            {
                Debug.Log("Something not right");
                return UniTask.CompletedTask;
            }

            /*slot = null;
            slot2 = null;*/

            _slotsCreated = _slotPosScript.SetGamePositions(ref slot, ref slot2, ref slot3, ref slot4,
                currentDrag, _slotPrefab, _turnScript.GetPlayerTurn(), this);

            if (slot != null)
            {
                slot.SetParent(_gameBoard, false);

                if (!_slotList.Contains(slot))
                {
                    _slotList.Add(slot);
                }
            }

            if (slot2 != null)
            {
                slot2.SetParent(_gameBoard, false);

                if (!_slotList.Contains(slot2))
                {
                    _slotList.Add(slot2);
                }
            }

            if (slot3 != null)
            {
                slot3.SetParent(_gameBoard, false);

                if (!_slotList.Contains(slot3))
                {
                    _slotList.Add(slot3);
                }
            }

            if (slot4 != null)
            {
                slot4.SetParent(_gameBoard, false);

                if (!_slotList.Contains(slot4))
                {
                    _slotList.Add(slot4);
                }
            }

            SetSlotPoosition?.Invoke(_slotList);
            return UniTask.CompletedTask;
        }


        public void ConfirmSlotOccupation(ref RectTransform slotOccupied, DominoView dominoView)
        {
            // Ensure the domino's z-position is set to 0 to keep it on the correct layer
            var localPosition = dominoView.transform.localPosition;
            localPosition.z = 0;
            dominoView.transform.localPosition = localPosition;

            Vector3 auxPosition = slotOccupied.position;
            Quaternion auxRotation = slotOccupied.rotation;
            Vector2 auxSizeDelta = slotOccupied.sizeDelta;
            
            
            if (slotOccupied == null)
                return;

            if (slot != null)
            {
                if (slotOccupied.position == slot.position)
                {
                    // Right
                    _slotPosScript.ConfirmOccupation(slotOccupied, 0, dominoView);
                }
            }

            if (slot2 != null)
            {
                if (slotOccupied.position == slot2.position)
                {
                    // Left
                    _slotPosScript.ConfirmOccupation(slotOccupied, 1, dominoView);
                }
            }

            if (slot3 != null)
            {
                if (slotOccupied.position == slot3.position)
                {
                    // Right
                    _slotPosScript.ConfirmOccupation(slotOccupied, 2, dominoView);
                }
            }

            if (slot4 != null)
            {
                if (slotOccupied.position == slot4.position)
                {
                    // Left
                    _slotPosScript.ConfirmOccupation(slotOccupied, 3, dominoView);
                }
            }
        }

        /// <summary>
        /// Updates domino positions based on recorded data using the SlotHelper component.
        /// </summary>
        public void UpdateDominoPositionsAccordingRecords_Proxy()
        {
            // Check if the SlotHelper reference is null before proceeding
            if (!_slotPosScript)
            {
                Debug.LogError($"[GameControler] UpdateDominoPositionsAccordingRecords: SlotHelper reference is missing!</color>");
                return;
            }

            // Call the SlotHelper function to update the domino positions according to the records
            _slotPosScript.UpdateDominoPositionsAccordingRecords();
        }

        /// <summary>
        /// Function that calls turn and hand controller to update theirs values according the domino played
        /// </summary>
        public void FinishTurn(ref RectTransform slotOccupied, Domino dominoInfo, bool isNotificationFromHost = false, bool wasUsed = false)
        {
            if (wasUsed)
            {
                Vector3 auxPosition = slotOccupied.position;
                Quaternion auxRotation = slotOccupied.rotation;
                Vector2 auxSizeDelta = slotOccupied.sizeDelta;

                string auxSideInfo = slotOccupied.GetComponent<EmptySlot>().sideInfo;
                gameController_SetTileIDUsedInShowBoard?.Invoke(dominoInfo.id, auxSideInfo, auxPosition, auxRotation, auxSizeDelta);
            }

            if (slotOccupied != null && !isNotificationFromHost)
            {
                _turnScript.SetLastActivePlayer();
                _handScript.LockAllTiles();

                if(gameModeSelectedID != GameMode.replay)
                {
                    _turnScript.EndTurn(null, 0);   
                }

                /*if (!isNotificationFromHost)
                {
                    Debug.Log("++-- validar 01");

                    _turnScript.EndTurn(null, 0);
                }
                else
                {
                    Debug.Log("++-- validar 02");
                    _turnScript.ValideEndTurnLogic(null, 0);
                }*/

                Debug.Log("++-- ConfirmSlotOccupation: ");
            }


            // TODO: check with vlad why the turn count is not be modified
            else if (isNotificationFromHost)
            {
                _turnScript.AddTurnCountExternally();
            }
        }

        public void RemoveAllSlots()
        {
            if (_slotList.Count > 0)
            {
                _slotList.RemoveRange(0, _slotList.Count);
                StartCoroutine(ClearObjects());
            }
        }

        protected virtual IEnumerator ClearObjects()
        {
            Transform result;

            while ((result = _gameBoard.Find("EmptySlot(Clone)")) != null)
            {
                Destroy(result.gameObject);
                yield return null;
            }
        }

        public void RemoveSpecificSlot(ref RectTransform slotToRemove)
        {
            if (slotToRemove == null)
                return;

            if (_slotList.Contains(slotToRemove))
            {
                _slotList.Remove(slotToRemove);
                Destroy(slotToRemove.gameObject);

                slotToRemove = null;
            }
        }

        public bool GetInfoSlots()
        {
            return _slotsCreated;
        }

        public void GetSlots(ref RectTransform rightSlot, ref RectTransform leftSlot, ref RectTransform topSlot, ref RectTransform downSlot)
        {
            rightSlot = slot;
            leftSlot = slot2;

            topSlot = slot3;
            downSlot = slot4;
        }
        
        public virtual void NotifyPlayerStartsMovement_ToHost(DominoView tileDominoView, string sideInfo)
        {
            Debug.Log("NotifyPlayerStartsMovement_ToHost: " + tileDominoView.GetDomino().TopIndex + " " +
                      tileDominoView.GetDomino().BottomIndex);
        }
        public virtual void NotifyPlayerEndsMovement_ToHost(DominoView tileDominoView, string sideInfo)
        {
            Debug.Log("NotifyPlayerEndsMovement_ToHost: " + tileDominoView.GetDomino().TopIndex + " " +
                      tileDominoView.GetDomino().BottomIndex);
        }

        public virtual void OnPassingTurn(bool isPlayer)
        { }
    }
}