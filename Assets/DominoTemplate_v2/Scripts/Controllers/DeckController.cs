using DominoTemplate.Core;
using DominoTemplate.DragAndDrop;
using DominoTemplate.View;
using ProDomino.AnalyticsSystem;
using ProDomino.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;

namespace DominoTemplate.Controllers
{
    public class DeckController : MonoBehaviour
    {
        [SerializeField] protected RectTransform _deckHolder = null;
        public RectTransform DeckHolder => _deckHolder;
        [SerializeField] protected RectTransform _player = null;
        public RectTransform Player => _player;
        [SerializeField] protected RectTransform _rightAI = null;
        public RectTransform RightAI => _rightAI;
        [SerializeField] protected RectTransform _leftAI = null;
        public RectTransform LeftAI => _leftAI;
        [SerializeField] protected RectTransform _topAI = null;
        public RectTransform TopAI => _topAI;

        [SerializeField] protected DragHandler _dominoPrefab = null;

        [SerializeField] protected Sprite[] _spriteArray = null;
        public Sprite[] SpriteArray => _spriteArray;

        [SerializeField] protected Sprite _backTile = null;
        public Sprite BackTile => _backTile;

        public int dominoCount = 28;
        protected DictionaryService _dictionaryService;
        protected GameControler _gameScript;
        protected HandController _handScript;
        protected GameTurnController _turnScript;
        protected AnalyticsManager _analyticsManager;

        [SerializeField] protected List<DragHandler> _dominoTiles;

        protected List<DragHandler> _boardTiles;
        [SerializeField] protected List<DragHandler> _playerTiles;
        [SerializeField] protected List<DragHandler> _rightAITiles;
        [SerializeField] protected List<DragHandler> _leftAITiles;
        [SerializeField] protected List<DragHandler> _topAITiles;
        public List<DragHandler> DominoTiles => _dominoTiles;
        public List<DragHandler> PlayerTiles => _playerTiles;
        public List<DragHandler> TopAITiles => _topAITiles;
        public List<DragHandler> LeftAITiles => _leftAITiles;
        public List<DragHandler> RightAITiles => _rightAITiles;
        
        [SerializeField] protected Func<bool, bool> deck_HandleNoValidMovesInGameMode = null; //Asigned tue event from game mode script
        public Func<bool, bool> Deck_HandleNoValidMovesInGameMode
        {
            get => deck_HandleNoValidMovesInGameMode;
            set => deck_HandleNoValidMovesInGameMode = value;
        }

        [SerializeField] protected Action deck_HandleHasValidMoves = null; //Asigned tue event from game mode script
        public Action Deck_HandleHasValidMoves
        {
            get => deck_HandleHasValidMoves;
            set => deck_HandleHasValidMoves = value;
        }

        public delegate bool Deck_CheckRoundGameOver(ref string gameOverCase, bool gameIsBlocked = false);
        [SerializeField] protected Deck_CheckRoundGameOver deckCheckRoundGameOver = null;
        public Deck_CheckRoundGameOver DeckCheckRoundGameOver
        {
            get => deckCheckRoundGameOver;
            set => deckCheckRoundGameOver = value;
        }

        public delegate string Deck_ValideWinnerByTileCount();
        [SerializeField] protected Deck_ValideWinnerByTileCount deckValideWinnerByTileCount = null;
        public Deck_ValideWinnerByTileCount DeckValideWinnerByTileCount
        {
            get => deckValideWinnerByTileCount;
            set => deckValideWinnerByTileCount = value;
        }

        public void SetAllRefs(GameControler newGame, HandController newHand, GameTurnController newTurn, AnalyticsManager analyticsManager, DictionaryService dictionaryService)
        {
            _gameScript = newGame;
            _handScript = newHand;
            _turnScript = newTurn;
            _analyticsManager = analyticsManager;
            _dictionaryService = dictionaryService;
        }

        public void RestartDominos()
        {
            KillAllDominos();
            SetupNewGame();
            //StartCoroutine(SetupRandomHands());
        }


        public List<DragHandler> GetList(int listNum)
        {
            List<DragHandler> result = new List<DragHandler>();

            if (listNum == -1)
                result = _boardTiles;
            else if (listNum == 0)
                result = _dominoTiles;
            else if (listNum == 1)
                result = _playerTiles;
            else if (listNum == 2)
                result = _leftAITiles;
            else if (listNum == 3)
                result = _topAITiles;
            else if (listNum == 4)
                result = _rightAITiles;
            else
                Debug.Log("Picked wrong list");

            return result;
        }


        public void ControlAllHands()
        {
            int i = 1;


            while (i < 5)
            {
                List<DragHandler> tempList = GetList(i);
                ControlPlaced(tempList);

                i++;
            }
        }

        private void ControlPlaced(List<DragHandler> tempTileHolder)
        {
            int i = 0;

            while (i < tempTileHolder.Count)
            {
                if (tempTileHolder[i].GetDominoView().IsOnTable())
                {
                    _boardTiles.Add(tempTileHolder[i]);
                    tempTileHolder.RemoveAt(i);
                }

                i++;
            }
        }

        public void DeckSetupRandomHands()
        {
            StartCoroutine(SetupRandomHands());
        }

        private IEnumerator SetupRandomHands()
        {
            _playerTiles = new List<DragHandler>();
            _rightAITiles = new List<DragHandler>();
            _leftAITiles = new List<DragHandler>();
            _topAITiles = new List<DragHandler>();

            yield return new WaitForSeconds(1f);

            int i = 0;

            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                while (i < 28 && _dominoTiles.Count > 0)
                {
                    int rand = UnityEngine.Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < 7)
                        TileSwapHelper(ref _player, ref _playerTiles, rand, true, false);
                    else if (i < 14)
                        TileSwapHelper(ref _leftAI, ref _leftAITiles, rand, false, true);
                    else if (i < 21)
                        TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true);
                    else
                        TileSwapHelper(ref _rightAI, ref _rightAITiles, rand, false, true);


                    i++;
                    yield return new WaitForSeconds(0.2f);
                }
            }
            else
            {
                while (i < 14 && _dominoTiles.Count > 0)
                {
                    int rand = UnityEngine.Random.Range(0, _dominoTiles.Count);
                    //			DominoView tempTileView = _dominoTiles[rand].GetDominoView();

                    if (i < 7)
                    {
                        Debug.Log("===> Player hand: " + i + "/" + rand);
                        TileSwapHelper(ref _player, ref _playerTiles, rand, true, false);
                    }
                    else if (i < 14)
                    {
                        TileSwapHelper(ref _topAI, ref _topAITiles, rand, true, true);
                    }

                    i++;
                    yield return new WaitForSeconds(0.2f);
                }
            }

            if (_dominoTiles != null && _dominoTiles.Count > 0)
            {
                _deckHolder.transform.localPosition = new Vector3(760, 0, 0); // Move deck to the left
            }
            else
            {
                _deckHolder.transform.localPosition = new Vector3(2000, 0, 0); // Reset deck position if no tiles left
                _deckHolder.gameObject.SetActive(false); // Hide the deck if no tiles left
            }

            _turnScript.EndTurn("Game Started", 1);

                Debug.Log("===> All list count -> " + _dominoTiles.Count + " Player count -> " + _playerTiles.Count);
            Debug.Log("===> _rightAITiles count -> " + _rightAITiles.Count + " _leftAITiles count -> " +
                      _leftAITiles.Count + " _topAITiles count -> " + _topAITiles.Count);
            yield return null;
        }


        protected void TileSwapHelper(ref RectTransform nextParent, ref List<DragHandler> newList, int tileIndex,
            bool standing, bool handedAI, bool remove = true, bool setDefatulTile = false, bool instantMove = false)
        {
            //_dominoTiles[tileIndex].GetDominoView().ChangeBackState(handedAI);
            _dominoTiles[tileIndex].GetDominoView().ChangeBackState(handedAI && _gameScript.GameModeSelectedID != GameMode.replay);
            _dominoTiles[tileIndex].SendToNextHand(nextParent, standing, handedAI, instantMove);
            newList.Add(_dominoTiles[tileIndex]);

            if (setDefatulTile) //set to default tile the online player no local
                _dominoTiles[tileIndex].GetDominoView().SetDefaultTile(SpriteArray[0]);

            if (remove)
                _dominoTiles.RemoveAt(tileIndex);
        }

        protected virtual void SetupNewGame()
        {
            int i;

            _boardTiles = new List<DragHandler>();
            _dominoTiles = new List<DragHandler>();
            i = 0;
            while (i < dominoCount) // Create all dominoes
            {
                DragHandler tempDomino = Instantiate(_dominoPrefab, _deckHolder.GetChild(0).transform); // Child 0 is Deck container
                DominoView tempDominoView;

                _dominoTiles.Add(tempDomino);
                tempDominoView = _dominoTiles[i].GetDominoView();
                _dominoTiles[i].gameScript = _gameScript;
                if (i < SpriteArray.Length)
                {
                    tempDominoView.LockTile();
                    if (SpriteArray[i] != null)
                        tempDominoView.SetDomino(SetupDominoInfo(i), SpriteArray[i], BackTile);
                    else
                        Debug.Log("Needed Sprite is not exits!!!!");
                }
                else
                    Debug.Log("Something wrong with sprites!!!");

                i++;
            }

            _deckHolder.transform.localPosition = new Vector3(0, 0, 0);

            _handScript.SetLockPlayerButtons(false);
        }

        public Domino SetupDominoInfo(int dominoNum)
        {
            // Create and initialize the domino
            var resultInfo = new Domino
            {
                id = dominoNum,
                Available = true
            };

            int counter = 0;

            // Iterate through the triangular domino set (0-0 to 6-6)
            for (int top = 0; top <= 6; top++)
            {
                for (int bottom = top; bottom <= 6; bottom++)
                {
                    // When the linear index matches, we found the domino
                    if (counter == dominoNum)
                    {
                        resultInfo.TopIndex = top;
                        resultInfo.BottomIndex = bottom;
                        return resultInfo;
                    }

                    counter++;
                }
            }

            // Fallback (should never happen if dominoNum is valid)
            resultInfo.TopIndex = 6;
            resultInfo.BottomIndex = 6;
            return resultInfo;
        }

        public bool CheckForGameOver(ref string gameOverCase)
        {
            if (_gameScript.VsPlayerSelectedID == NumberPlayers.oneVsThree)
            {
                if (_playerTiles.Count == 0)
                    gameOverCase = "Player win!";
                if (_leftAITiles.Count == 0)
                    gameOverCase = "Left AI win!";
                if (_topAITiles.Count == 0)
                    gameOverCase = "Top AI win!";
                if (_rightAITiles.Count == 0)
                    gameOverCase = "Right AI win!";
            }
            else if (_gameScript.VsPlayerSelectedID == NumberPlayers.oneVsOne)
            {
                if (_playerTiles.Count == 0)
                    gameOverCase = "Player win!";
                if (_topAITiles.Count == 0)
                    gameOverCase = "Top AI win!";
            }
            else if (_gameScript.VsPlayerSelectedID == NumberPlayers.twoVsTwo)
            {
                if (_playerTiles.Count + _topAITiles.Count == 0)
                    gameOverCase = "Player and Top AI win!";
                if (_leftAITiles.Count + _rightAITiles.Count == 0)
                    gameOverCase = "Left AI and Right AI win!";
            }

            if (gameOverCase != null)
                return true;

            return false;
        }


        private void ClearList(List<DragHandler> tempList)
        {
            int i;

            i = 0;
            while (i < tempList.Count)
            {
                DragHandler tempTile = tempList[i];
                if (tempTile.gameObject != null)
                    Destroy(tempTile.gameObject);
                i++;
                if (i >= tempList.Count)
                    tempList.RemoveRange(0, tempList.Count);
            }
        }

        private void KillAllDominos()
        {
            int i;

            i = 0;
            while (i < 5)
            {
                List<DragHandler> tempList = GetList(i);

                if (tempList != null)
                    ClearList(tempList);
                i++;
            }
        }
    }
}