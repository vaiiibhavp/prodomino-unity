using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProDomino.GameModes;
using ProDomino.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.ReplaySystem
{
    public class ReplayTimelineController : MonoBehaviour
    {
        [Header("Configuración")]
        public Slider timelineSlider;

        [SerializeField]
        private ReplayGameMode prefabReplayGameMode;

        [SerializeField]
        private ReplayGameMode currentReplayGameMode;
        public ReplayGameMode CurrentReplayGameMode => currentReplayGameMode;

        [SerializeField]
        private RectTransform replayGameModeContainer;

        [SerializeField]
        private GameModeConfig _gameModeConfig = null;

        [SerializeField]
        private RectTransform timelineContainer;

        [SerializeField]
        private GameObject linePrefab;
        [SerializeField]
        private GameObject roundLinePrefab;

        [SerializeField]
        private int lineLength = 1014;

        [SerializeField]
        private TMP_Text turnLineText;
        [SerializeField]
        private TMP_Text iaHintText;
        [SerializeField]
        private TMP_Text ai_InsightText; //Set_AI_Insight_Info 

        [SerializeField]
        private MatchReplay currentReplay;

        [SerializeField]
        private Button playTurnBtn;
        [SerializeField]
        private Button previousTurnBtn;
        [SerializeField]
        private Button nextTurnBtn;

        [SerializeField]
        private int currentIndex = 0;
        [SerializeField]
        private bool isPlaying = false;
        [SerializeField]
        private bool isPaused = false;
        private Coroutine replayCoroutine;
        private float totalDuration;
        private float elapsedTime;

        //Wait times in timeline actions
        [SerializeField]
        private float waitTimeDealHands = 8f;

        [SerializeField]
        private float waitTimePlay = 3f;

        [SerializeField]
        private float waitTimeTakeBoneyard = 3f;

        [SerializeField]
        private float waitTimePass = 3f;

        [SerializeField]
        private float waitTimeResultPopup = 5f;

        private List<int> initRoundIndex = new List<int>();

        public Func<(double, double, double)> Calcule_IA_Insight;

        void Update()
        {
            if (isPlaying && !isPaused)
            {
                elapsedTime += Time.deltaTime;
                timelineSlider.value = elapsedTime;
            }
        }

        private ReplayGameMode CreateReplayGameMode()
        {
            ReplayGameMode auxReplayGameMode = Instantiate(prefabReplayGameMode);
            auxReplayGameMode.gameObject.SetActive(true);
            
            RectTransform auxCurrentGameHolder = auxReplayGameMode.ExtendedGameController.GetRect();

            auxCurrentGameHolder.SetParent(replayGameModeContainer, false);
            auxCurrentGameHolder.localScale = Vector3.one;
            auxCurrentGameHolder.offsetMin = new Vector2(0, 0);
            auxCurrentGameHolder.offsetMin = new Vector2(0, 0);

            return auxReplayGameMode;
        }

        public IEnumerator SetupTimeLineData(MatchReplay auxCurrentReplay)
        {
            foreach (Transform child in timelineContainer)
            {
                Destroy(child.gameObject);
            }

            elapsedTime = 0;
            timelineSlider.value = elapsedTime;

            timelineSlider.minValue = 0;
            timelineSlider.maxValue = 0;

            totalDuration = 0;

            currentReplayGameMode = CreateReplayGameMode();

            List<PlayerInfoReplay> auxPlayersInfo = new List<PlayerInfoReplay>();

            if (auxCurrentReplay.numberPlayers == NumberPlayers.oneVsOne)
            {
                auxPlayersInfo.Add(auxCurrentReplay.player_0);
                auxPlayersInfo.Add(auxCurrentReplay.player_1);
            }
            else
            {
                auxPlayersInfo.Add(auxCurrentReplay.player_0);
                auxPlayersInfo.Add(auxCurrentReplay.player_1);
                auxPlayersInfo.Add(auxCurrentReplay.player_2);
                auxPlayersInfo.Add(auxCurrentReplay.player_3);
            }

            currentReplayGameMode.inicializeReplayGameMode(_gameModeConfig, auxCurrentReplay.gameMode, auxCurrentReplay.gameType, auxCurrentReplay.numberPlayers, auxPlayersInfo, auxCurrentReplay.turns.ElementAtOrDefault(0)?.cumulatePlayerScores ?? new List<int> { 0, 0, 0, 0 }, auxCurrentReplay.localPlayerIndexId);

            currentReplay = auxCurrentReplay;
            currentIndex = 0;
            elapsedTime = 0;
            isPlaying = false;
            isPaused = false;
            iaHintText.text = "";

            foreach (TurnData turn in currentReplay.turns)
            {
                turn.turnTimer = 0;
                
                if (turn.dealHands)
                {
                    turn.turnTimer += waitTimeDealHands;
                    totalDuration += waitTimeDealHands;
                }
                else if (turn.turnAction == TurnActionReplay.play)
                {
                    turn.turnTimer += waitTimePlay;
                    totalDuration += waitTimePlay;
                }
                else if (turn.turnAction == TurnActionReplay.takeBoneyard)
                {
                    turn.turnTimer += waitTimeTakeBoneyard + turn.tilesTakenFromBoneyard.Count;
                    totalDuration += waitTimeTakeBoneyard + turn.tilesTakenFromBoneyard.Count;
                }
                else if (turn.turnAction == TurnActionReplay.pass)
                {
                    turn.turnTimer += waitTimePass;
                    totalDuration += waitTimePass;
                }

                if (turn.turnResult != TurnResultReplay.none)
                {
                    turn.turnTimer += waitTimeResultPopup;
                    totalDuration += waitTimeResultPopup;
                }
            }

            timelineSlider.minValue = 0;
            timelineSlider.maxValue = totalDuration;

            initRoundIndex = new List<int>();

            float auxCurrentTimerCounter = 0;
            int auxLineIndex = 0;
            
            foreach (TurnData turn in currentReplay.turns)
            {

                GameObject lineObj;

                if (!turn.dealHands)
                {
                    lineObj = Instantiate(linePrefab, timelineContainer);
                }
                else
                {
                    lineObj = Instantiate(roundLinePrefab, timelineContainer);

                    ReplayLinePointBtn replayLinePointBtn = lineObj.GetComponent<ReplayLinePointBtn>();

                    replayLinePointBtn.SetLineIndex(auxLineIndex);
                    replayLinePointBtn.SetLineBtnAction(GoSpecificRound);

                    initRoundIndex.Add(turn.turnNumber);
                }

                turn.timeOnTimeline = auxCurrentTimerCounter;

                RectTransform rect = lineObj.GetComponent<RectTransform>();

                Vector2 pos = new Vector2((auxCurrentTimerCounter * lineLength) / totalDuration, 9);

                rect.anchoredPosition = pos;

                auxCurrentTimerCounter += turn.turnTimer;

                auxLineIndex++;
            }

            UpdateTimeLineText(0);

            ActiveControllerBtns(isActive: true);
            
            Set_AI_Insight_Info();

            yield return null;
        }

        private void GoToTurn(int auxCurrentIndexTurn, bool isPrevious)
        {
            ActiveControllerBtns(isActive: false);

            isPlaying = false;
            isPaused = false;

            if (isPrevious)
            {
                if(auxCurrentIndexTurn > 0)
                {
                    currentIndex = auxCurrentIndexTurn-1;
                }
                else
                {
                    currentIndex = 0;
                }
            }
            else
            {
                if(auxCurrentIndexTurn < currentReplay.turns.Count - 2)
                {
                    currentIndex = auxCurrentIndexTurn + 1;
                }
                else
                {
                    Resume();

                    return;
                }
            }

            if (replayCoroutine != null)
                StopCoroutine(replayCoroutine);

            TurnData auxTurn = currentReplay.turns[currentIndex];

            elapsedTime = auxTurn.timeOnTimeline;
            timelineSlider.value = elapsedTime;

            UpdateTimeLineText(currentIndex);

            if(auxTurn.dealHands)
            {
                GoSpecificRound(currentIndex);
                StartCoroutine(SetActiveControllerBtns(0.1f, true));
            }
            else
            {
                if(isPrevious)
                {
                    //StartCoroutine(currentReplayGameMode.ExecuteSpecificTurn(currentIndex, 0, currentReplay));
                    if(!currentReplay.turns[currentIndex+1].dealHands)
                    {
                        Debug.Log("lll No es dealHands");
                        StartCoroutine(GoToPreviousTurn());
                        
                        currentReplayGameMode.SetSpecificTurn_iaHint(currentReplay.turns[currentIndex]);
                    }
                    else
                    {
                        Debug.Log("lll Si es dealHands");

                        int auxIndexPrevious = ValideNumer(currentIndex, true);

                        Debug.Log("lll 02: " + auxIndexPrevious);

                        if(auxIndexPrevious >= 0)
                        {
                            GoSpecificRound(auxIndexPrevious);   
                        }

                        //GoSpecificRound(currentIndex);
                        StartCoroutine(SetActiveControllerBtns(0.1f, true));
                    }
                }
                else
                {
                    if(currentReplayGameMode.isExecution)
                    {
                        Debug.Log("uuu Pause: False");
                        currentReplayGameMode.instantExecution = true;
                        currentReplayGameMode.HighlightCurrentPlayerTurn(auxTurn.playerIndexId);   
                    }
                    else
                    {
                        Debug.Log("uuu Pause: True");
                        currentReplayGameMode.SetCurrentTurnData(currentReplay.turns[currentIndex-1], iaHintText, instantTurn: true);
                        currentReplayGameMode.HighlightCurrentPlayerTurn(auxTurn.playerIndexId);
                    }

                    currentReplayGameMode.SetSpecificTurn_iaHint(currentReplay.turns[currentIndex]);

                    StartCoroutine(SetActiveControllerBtns(0.8f, true));
                }
            }
        }

        private IEnumerator GoToPreviousTurn()
        {
            float auxTimeWait = 0.2f;

            if(currentReplayGameMode.isExecution)
            {
                currentReplayGameMode.instantExecution = true;

                auxTimeWait = 1f;
            }

            yield return new WaitForSeconds(auxTimeWait);

            currentReplayGameMode.ReverseCurrentTurn(currentReplay.turns[currentIndex+1]);

            yield return new WaitForSeconds(0.2f);
            
            currentReplayGameMode.ReverseCurrentTurn(currentReplay.turns[currentIndex]);

            currentReplayGameMode.HighlightCurrentPlayerTurn(currentReplay.turns[currentIndex].playerIndexId);

            StartCoroutine(SetActiveControllerBtns(0.1f, true));
        }

        private IEnumerator SetActiveControllerBtns(float timeWait, bool isActive)
        {
            yield return new WaitForSeconds(timeWait);

            Set_AI_Insight_Info();
            ActiveControllerBtns(isActive);
        }

        public void ActiveControllerBtns(bool isActive)
        {
            playTurnBtn.interactable = isActive;
            previousTurnBtn.interactable = isActive;
            nextTurnBtn.interactable = isActive;
        }

        private void GoSpecificRound(int lineRoundIndex)
        {
            if (replayCoroutine != null)
                StopCoroutine(replayCoroutine);

            currentIndex = lineRoundIndex;

            elapsedTime = currentReplay.turns[lineRoundIndex].timeOnTimeline;
            timelineSlider.value = elapsedTime;

            UpdateTimeLineText(lineRoundIndex);
            
            StartCoroutine(LoadSpecificTurn(lineRoundIndex));
        }

        private IEnumerator LoadSpecificTurn(int lineRoundIndex)
        {
            Destroy(currentReplayGameMode.gameObject);

            //yield return new WaitForSeconds(1f);
            yield return new WaitForEndOfFrame();

            currentReplayGameMode = CreateReplayGameMode();

            List<PlayerInfoReplay> auxPlayersInfo = new List<PlayerInfoReplay>();

            if (currentReplay.numberPlayers == NumberPlayers.oneVsOne)
            {
                auxPlayersInfo.Add(currentReplay.player_0);
                auxPlayersInfo.Add(currentReplay.player_1);
            }
            else
            {
                auxPlayersInfo.Add(currentReplay.player_0);
                auxPlayersInfo.Add(currentReplay.player_1);
                auxPlayersInfo.Add(currentReplay.player_2);
                auxPlayersInfo.Add(currentReplay.player_3);
            }

            iaHintText.text = "Handing over the hands of the round";

            currentReplayGameMode.inicializeReplayGameMode(_gameModeConfig, currentReplay.gameMode, currentReplay.gameType, currentReplay.numberPlayers, auxPlayersInfo, currentReplay.turns.ElementAtOrDefault(lineRoundIndex)?.cumulatePlayerScores ?? new List<int> { 0, 0, 0, 0 }, currentReplay.localPlayerIndexId);

            if (isPlaying && !isPaused)
            {
                replayCoroutine = StartCoroutine(PlayTimeline());
            }
            else
            {
                isPlaying = false;
            }
        }

        public void ClearReplay()
        {
            Stop();

            elapsedTime = 0;
            timelineSlider.value = elapsedTime;

            timelineSlider.minValue = 0;
            timelineSlider.maxValue = 0;

            totalDuration = 0;

            foreach (Transform child in timelineContainer)
            {
                Destroy(child.gameObject);
            }

            if(currentReplayGameMode != null)
                Destroy(currentReplayGameMode.gameObject);
        }
        
        private void UpdateTimeLineText(int currenTurnIndex)
        {
            turnLineText.text = "Turn timeline " + currenTurnIndex + "/" + currentReplay.turns.Count;
        }

        #region Time Control
        public void Play()
        {
            if (isPlaying) 
                return;

            currentReplayGameMode.pauseExecution = false;
            isPlaying = true;
            isPaused = false;
            replayCoroutine = StartCoroutine(PlayTimeline());
        }

        public void ReplayButton()
        {
            if (isPlaying)
            {
                if (isPaused)
                    Resume();
                else
                    Pause();
            }
            else
            {
                Play();
            }
        }

        private void Pause()
        {
            currentReplayGameMode.pauseExecution = true;
            isPaused = true;
        }

        private void Resume()
        {
            currentReplayGameMode.pauseExecution = false;
            isPaused = false;
        }

        public void Stop()
        {
            if (replayCoroutine != null)
                StopCoroutine(replayCoroutine);

            isPlaying = false;
            isPaused = false;
            currentIndex = 0;
            elapsedTime = 0;
            timelineSlider.value = 0;
        }

        public void PreviousTurn()
        {
            GoToTurn(currentIndex, true);
            /*int auxIndexPrevious = ValideNumer(currentIndex, true);

            Debug.Log("+/*- 02: " + auxIndexPrevious);

            if(auxIndexPrevious >= 0)
            {
                GoSpecificRound(auxIndexPrevious);   
            }*/
        }

        public void NextTurn()
        {
            GoToTurn(currentIndex, false);

            /*int auxIndexNext = ValideNumer(currentIndex, false);
            
            if(auxIndexNext >= 0)
            {
                GoSpecificRound(auxIndexNext);   
            }*/
        }
        #endregion

        private int ValideNumer(int index, bool isPrevius)
        {
            int left = 0;
            int right = 0;

            for (int i = 0; i < initRoundIndex.Count - 1; i++)
            {
                if (index >= initRoundIndex[i] && index <= initRoundIndex[i + 1])
                {
                    left = initRoundIndex[i];
                    right = initRoundIndex[i + 1];

                    if (isPrevius)
                    {
                        Debug.Log("+/*- 01: " + left);
                        if (index != left)
                            return left;
                        else if (index != initRoundIndex[0])
                            return initRoundIndex[i - 1];
                    }
                    else
                    {
                        if (index != right)
                            return right;
                        else if (index != initRoundIndex[initRoundIndex.Count - 1])
                            return initRoundIndex[i + 2];
                    }

                    break;
                }
                else if(isPrevius && index > initRoundIndex[initRoundIndex.Count - 1])
                {
                    return initRoundIndex[initRoundIndex.Count - 1];
                }
            }

            return -1;
        }

        private IEnumerator PlayTimeline()
        {
            while (currentIndex < currentReplay.turns.Count)
            {
                UpdateTimeLineText(currentIndex);

                TurnData action = currentReplay.turns[currentIndex];

                // Espera mientras está pausado
                while (isPaused)
                    yield return null;

                Debug.Log("**--// timeline index: " + currentIndex);

                // Ejecuta la acción
                ExecuteAction(action);

                float auxWaitingTime = action.turnTimer;    
                
                // Espera el tiempo asignado
                float timer = 0f;
                while (timer < auxWaitingTime)//2)//action.delayBeforeNext)
                {
                    if (!isPaused)
                        timer += Time.deltaTime;
                    yield return null;
                }

                currentIndex++;
            }

            // Finaliza reproducción
            isPlaying = false;
        }

        private void ExecuteAction(TurnData turnData, bool instantTurn = false)
        {
            Set_AI_Insight_Info();
            currentReplayGameMode.SetCurrentTurnData(turnData, iaHintText, instantTurn);

            //SetNextTurn(turnData);

            // Aquí haces lo que corresponda según el tipo de acción
            /*switch (turnData.turnAction)
            {
                case TurnActionReplay.play:
                    Debug.Log("*+* Colocando ficha: " + turnData.turnAction);
                    break;
                case TurnActionReplay.pass:
                    Debug.Log("*+* Pasar turno: " + turnData.turnAction);
                    break;
                case TurnActionReplay.takeBoneyard:
                    Debug.Log("*+* Tomar del boneyard: " + turnData.turnAction);
                    break;
                default:00000
                    Debug.Log("*+* Ninguna accion asignada: " + turnData.turnAction);
                    break;
            }*/
        }

        public void Set_AI_Insight_Info()
        {
            var (winProbability, blockingProbability, tieProbability) = Calcule_IA_Insight();
            
            ai_InsightText.text = "% of Winning " + (int)Math.Round(winProbability*100) + "%";
            ai_InsightText.text += "\n\n% for the game to be Blocked " + (int)Math.Round(blockingProbability*100) + "%";
            ai_InsightText.text += "\n\n% for the game to end in a Draw " + (int)Math.Round(tieProbability*100) + "%";

            /*
            % of Winning
            100%

            % for the game to be Blocked
            100%

            % for the game to end in a Draw
            100%
            */
        }
    }
}
