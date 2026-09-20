using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using DominoTemplate.AI;
using ProDomino.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace DominoTemplate.Controllers
{
    public class GameTurnController : MonoBehaviour
    {
        [SerializeField] private TMP_Text endTurnMessage = null;

        private HandController _handScript = null;
        private DeckController _deckScript = null;
        private AIController _AIScript = null;

        /*[SerializeField] private PlayerStatus playerTurnStatus;
        [SerializeField] private PlayerStatus leftAITurnStatus;
        [SerializeField] private PlayerStatus topAITurnStatus;
        [SerializeField] private PlayerStatus rightAITurnStatus;*/

        [SerializeField] private bool _playerTurn;
        [SerializeField] private bool _leftAITurn;
        [SerializeField] private bool _topAITurn;
        [SerializeField] private bool _rightAITurn;

        public bool _playerIsEliminated;
        public bool _leftAIIsEliminated;
        public bool _topAIIsEliminated;
        public bool _rightAIIsEliminated;

        private int _lastActivePlayer;

        private bool _checkTiles;
        public bool CheckTiles
        {
            get { return _checkTiles; }
            set { _checkTiles = value; }
        }
        private bool _gameOver;

        private int _playerScore;
        private int _leftAIScore;
        private int _topAIScore;
        private int _rightAIScore;

        private CancellationTokenSource alertCancellationTokenSource;
        private GameControler _gameScript;
        private UnityEvent _onTurnStartEvent = new UnityEvent();
        public UnityEvent OnTurnStartEvent => _onTurnStartEvent;
        private UnityEvent _onTurnOverEvent = new UnityEvent();
        private UnityEvent _onRoundOverEvent = new UnityEvent();
        public UnityEvent OnRoundOverEvent => _onRoundOverEvent;

        public byte TurnCount { get; private set; }
        public byte RoundsCount { get; set; } // TODO: implement rounds count logic

        public int PlayerScore
        {
            get => _playerScore;
            set => _playerScore = value;
        }

        public int LeftAIScore
        {
            get => _leftAIScore;
            set => _leftAIScore = value;
        }

        public int TopAIScore
        {
            get => _topAIScore;
            set => _topAIScore = value;
        }

        public int RightAIScore
        {
            get => _rightAIScore;
            set => _rightAIScore = value;
        }

        public bool GetPlayerTurn()
        {
            return _playerTurn;
        }

        public void SetAllRefs(GameControler newGame, HandController newHand, DeckController newDeck, AIController newAI)
        {
            _gameScript = newGame;
            _handScript = newHand;
            _deckScript = newDeck;
            _AIScript = newAI;
        }

        public void InitGameVariables()
        {
            _playerTurn = false;
            _leftAITurn = false;
            _topAITurn = false;
            _rightAITurn = false;

            // Reset scores and turn count
            TurnCount = 0;
            //RoundsCount = 0; // TODO: its normal reset when a new game starts, but it shouldn't be reset when a new round round starts

            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                int rand = UnityEngine.Random.Range(0, 4);

                if (rand == 0)
                    _playerTurn = true;
                else if (rand == 1)
                    _leftAITurn = true;
                else if (rand == 2)
                    _topAITurn = true;
                else
                    _rightAITurn = true;
            }
            else
            {
                int rand = UnityEngine.Random.Range(0, 2);

                if (rand == 0)
                    _playerTurn = true;
                else
                    _topAITurn = true;
            }

            _lastActivePlayer = -1;

            _checkTiles = false;
            _gameOver = false;

            SetGameTextMessage();
        }

        public void InitEliminationsAndScoring()
        {
            RoundsCount = 0;

            _playerIsEliminated = false;
            _leftAIIsEliminated = false;
            _topAIIsEliminated = false;
            _rightAIIsEliminated = false;

            _playerScore = 0;
            _leftAIScore = 0;
            _topAIScore = 0;
            _rightAIScore = 0;
        }

        public void HandleTurnStartEvent(UnityAction action)
        {
            if (_onTurnStartEvent == null)
                _onTurnStartEvent = new UnityEvent();
            _onTurnStartEvent.AddListener(action);
        }
        
        public void HandleTurnOverEvent(UnityAction action)
        {
            if (_onTurnOverEvent == null)
                _onTurnOverEvent = new UnityEvent();
            _onTurnOverEvent.AddListener(action);
        }
        
        public void HandleRoundOverEvent(UnityAction action)
        {
            if (_onRoundOverEvent == null)
                _onRoundOverEvent = new UnityEvent();
            _onRoundOverEvent.AddListener(action);
        }

        /// <summary>
        /// Main domino game process that manages turns and checks for game over conditions
        /// </summary>
        public void GameProcess()
        {
            // The MatchManager is the responsible for casual and competitive games
            if (!_gameScript || _gameScript.GameTypeSelectedID is GameType.casual or GameType.competitive)
                return;

            if (_checkTiles)
            {
                string endGameCase = null;
                _checkTiles = false;

                _deckScript.ControlAllHands();
                if (_gameOver == false) //Protection, cuz could be scenarios when game is already over
                {
                    //_gameOver = _deckScript.CheckForGameOver(ref endGameCase);
                    //_gameOver = _deckScript.DeckCheckForGameOver.Invoke(ref endGameCase); //check game over condition from game mode
                    _gameOver = _deckScript.DeckCheckRoundGameOver.Invoke(ref endGameCase); //check game over condition from game mode
                    if (_gameOver)
                        EndRound(endGameCase);

                    else
                    {
                        Debug.Log("____playerTurn: " + _playerTurn);
                        _handScript.SetTurnFor(_playerTurn, _leftAITurn, _topAITurn, _rightAITurn); // Set the turn for the player and AI
                        _onTurnStartEvent?.Invoke(); // Notify that the turn has started

                        // AI Logic
                        if (_leftAITurn)
                        {
                            _AIScript.MakeTurn(2, false);
                        } else if (_topAITurn)
                        {
                            _AIScript.MakeTurn(3, false);
                        } else if (_rightAITurn)
                        {
                            _AIScript.MakeTurn(4, true);
                        }

                        if (!_playerTurn && !_leftAITurn && !_topAITurn && !_rightAITurn)
                        {
                            Debug.LogWarning("---> Nobody could play this turn. Game Blocked");
                        }
                    }
                } else
                {
                    _deckScript.DeckCheckRoundGameOver.Invoke(ref endGameCase, gameIsBlocked: true);
                    EndRound(endGameCase);
                    //EndRound(_deckScript.DeckValideWinnerByTileCount());
                    //EndRound("\"Fish\" Game Over");
                }
            }

            if (_gameOver)
            {
                // Game Over Logic //valide score rules
            }
        }


        public void SetLastActivePlayer()
        {
            if (_playerTurn)
                _lastActivePlayer = 1;
            else if (_leftAITurn)
                _lastActivePlayer = 2;
            else if (_topAITurn)
                _lastActivePlayer = 3;
            else if (_rightAITurn)
                _lastActivePlayer = 4;
            else
                Debug.Log("Something went wrong with Pass");
        }

        public void PassBehaviour()
        {
            int scoreForPass = 1;

            if (_playerTurn)
            {
                _handScript.SetLockPlayerButtons(false);
                _handScript.LockTilesHere(_deckScript.GetList(1));
            }

            switch (_lastActivePlayer)
            {
                case 1:
                    if (!_playerTurn) { }
                    //_playerScore += scoreForPass;
                    else
                        _gameOver = true;
                    break;

                case 2:
                    if (!_leftAITurn) { }
                    //_leftAIScore += scoreForPass;
                    else
                        _gameOver = true;
                    break;

                case 3:
                    if (!_topAITurn) { }
                    //_topAIScore += scoreForPass;
                    else
                        _gameOver = true;
                    break;

                case 4:
                    if (!_rightAITurn) { }
                    //_rightAIScore += scoreForPass;
                    else
                        _gameOver = true;
                    break;

                default:
                    Debug.Log("Something went wrong with Pass Scoring");
                    break;
            }
        }


        private IEnumerator EndTurnLogic(string message, float time)
        {
            if (message != null)
            { 
                SetGameTextMessage(message);
                yield return new WaitForSeconds(time);

                SetGameTextMessage();
            }

            Debug.Log("_checkTiles: _checkTiles");
            _checkTiles = true;
        }

        public void EndTurn(string message, float time) //En Estafuncion ver como se integra la rotacion de los turno || AQUI!
        {
            Debug.Log("Turns Player-> " + _playerTurn + " LeftAI-> " + _leftAITurn
                      + " TopAI-> " + _topAITurn + " RightAI-> " + _rightAITurn);

            _onTurnOverEvent?.Invoke();
            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                if (_playerTurn)
                {
                    _playerTurn = false;
                    //_leftAITurn = true;

                    if (!_leftAIIsEliminated)
                        _leftAITurn = true;
                    else if (!_topAIIsEliminated)
                        _topAITurn = true;
                    else if (!_rightAIIsEliminated)
                        _rightAITurn = true;
                    else
                    {
                        Debug.LogWarning("All AI players are eliminated, game is blocked.");
                        _gameOver = true;
                    }
                }
                else if (_leftAITurn)
                {
                    _leftAITurn = false;
                    //_topAITurn = true;
                    if (!_topAIIsEliminated)
                        _topAITurn = true;
                    else if (!_rightAIIsEliminated)
                        _rightAITurn = true;
                    else if (!_playerIsEliminated)
                        _playerTurn = true;
                    else
                    {
                        Debug.LogWarning("All AI players are eliminated, game is blocked.");
                        _gameOver = true;
                    }
                }
                else if (_topAITurn)
                {
                    _topAITurn = false;
                    //_rightAITurn = true;
                    if (!_rightAIIsEliminated)
                        _rightAITurn = true;
                    else if (!_playerIsEliminated)
                        _playerTurn = true;
                    else if (!_leftAIIsEliminated)
                        _leftAITurn = true;
                    else
                    {
                        Debug.LogWarning("All AI players are eliminated, game is blocked.");
                        _gameOver = true;
                    }
                }
                else if (_rightAITurn)
                {
                    _rightAITurn = false;
                    //_playerTurn = true;
                    if (!_playerIsEliminated)
                        _playerTurn = true;
                    else if (!_leftAIIsEliminated)
                        _leftAITurn = true;
                    else if (!_topAIIsEliminated)
                        _topAITurn = true;
                    else
                    {
                        Debug.LogWarning("All AI players are eliminated, game is blocked.");
                        _gameOver = true;
                    }
                }
                else
                {
                    Debug.Log("Wrong END OF Turn");
                }
            }
            else
            {
                if (_playerTurn)
                {
                    _playerTurn = false;
                    _topAITurn = true;
                }
                else if (_topAITurn)
                {
                    _topAITurn = false;
                    _playerTurn = true;
                }
                else
                {
                    Debug.Log("Wrong END OF Turn");
                }
            }

            ValideEndTurnLogic(message, time);
            //StartCoroutine(EndTurnLogic(message, time));

            // Increment the turn count
            TurnCount++;
        }

        /// <summary>
        /// Use to directly increase the turn count without ding any background login 
        /// </summary>
        public void AddTurnCountExternally()
        {
            Debug.Log($"<color={Consts.Colors.Process}><b>[{nameof(GameTurnController)}]</b> TurnCount increased externally to {TurnCount}.</color>");
            TurnCount++;
        }

        public void ValideEndTurnLogic(string message, float time)
        {
            Debug.Log("HERE");
            StartCoroutine(EndTurnLogic(message, time));
        }

        public void AlertText(string message, float time)
        {
            AlertTextIE(message, time).Forget();
        }
        
        public async UniTask AlertTextIE(string message, float time)
        {
            if (alertCancellationTokenSource == null)
                alertCancellationTokenSource = new CancellationTokenSource();
            else
            {
                alertCancellationTokenSource.Cancel();
                alertCancellationTokenSource.Dispose();
                alertCancellationTokenSource = new CancellationTokenSource();
            }

            await SimpleAlertText(message, time).WithCancellation(alertCancellationTokenSource.Token).SuppressCancellationThrow();
        }

        public IEnumerator SimpleAlertText(string message, float time)
        {
            SetGameTextMessage(message);

            yield return new WaitForSeconds(time);
            SetGameTextMessage();

        }


        
        #region New methods added

        public void SetTurns(bool playerTurn, bool leftTurn, bool topTurn, bool rightTurn)
        {
            _playerTurn = playerTurn;
            _leftAITurn = leftTurn;
            _topAITurn = topTurn;
            _rightAITurn = rightTurn;
        }
        public string GetCurrentPlayerTurn()
        {
            if (_playerTurn)
            {
                return "playerTurn";
            }
            else if (_leftAITurn)
            {
                return "leftAITurn";
            }
            else if (_topAITurn)
            {
                return "topAITurn";
            }
            else if (_rightAITurn)
            {
                return "rightAITurn";
            }
            else
            {
                return "No player is currently active.";
            }
        }
        public int GetCurrentTurnControl()
        {
            if (_playerTurn)
                return 1;
            else if (_leftAITurn)
                return 2;
            else if (_topAITurn)
                return 3;
            else if (_rightAITurn)
                return 4;
            else
                throw new Exception("[GameTurnController_GetCurrentTurnControl] Each player turn indcator is disabled. No player are playing this turn");
        }
        public int GetNextTurn()
        { 
            var currentTurn = GetCurrentTurnControl();
            if (_gameScript.VsPlayerSelectedID != NumberPlayers.oneVsOne)
            {
                currentTurn = currentTurn + 1;
                if (currentTurn > 4)
                    currentTurn = 1; // Reset to Player
            } 
            else
                currentTurn = currentTurn is 1 ? 3 : 1; // Toggle between Player and Top AI
            return currentTurn;
        }

        public void EndRound(string message)
        {
            //SetGameTextMessage(message);
            _handScript.SetLockPlayerButtons(false);

            RoundsCount++;
            _onRoundOverEvent?.Invoke();

            Debug.Log("+-+EndRound: " + RoundsCount + " -> " + message);
        }

        private void SetGameTextMessage(string message = "")
        {
            if (endTurnMessage)
            {
                endTurnMessage.gameObject.SetActive(!string.IsNullOrEmpty(message));
                endTurnMessage.text = message ?? default;
            }
            else
                Debug.LogWarning("EndTurnMessage is not assigned in GameTurnController.");
        }
        #endregion
    }
}