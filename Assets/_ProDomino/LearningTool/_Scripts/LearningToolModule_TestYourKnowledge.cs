using DominoTemplate.DragAndDrop;
using ProDomino.Insights;
using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.LearningTool
{
    internal class LearningToolModule_TestYourKnowledge : LearningToolModule
    {
        [Space(15), Header(nameof(LearningToolModule_TestYourKnowledge) + "_Attributes")]
        [SerializeField] private ExposedGameControllerDictionaryDatabase exposedGameControllerPrefabCollection;
        [SerializeField] private Transform gameInstanceParent;
        [SerializeField] private Button resetGameButton;

        [Space(15), Header("Insights PopUp")]
        [SerializeField] private CanvasGroup insightPopUpCanvasGroup;
        [SerializeField] private ExposedTile exposedDominoPrefab;
        [SerializeField] private Transform exposedDominoParent;
        [SerializeField] private CustomButtonToggleGroupUI exposedDominoCustomToggleGroupUI;
        [SerializeField] private Button openInsightPopUpButton;
        [SerializeField] private Button closeInsightPopUpButton;
        [SerializeField] private Button playerDrawTileButton, aiDrawTileButton;
        [SerializeField] private ExposedTile portraitExposedTile;

        [Space(15), Header("Visible Info")]
        [SerializeField] private VisibleInfo
            winningPercentageVisibleInfo,
            gameBlockedPercentageVisibleInfo,
            tiePercentageVisibleInfo;

        [SerializeField] private TMP_Text
            percentageOfWinning,
            percentageOfGameBlocked,
            percentageOfTie,
            percentageOfTileToAppear;

        private Dictionary<GameMode, IExposedGameController> exposedGameControllerDictionary;
        private List<ExposedTile> dominoInstances;

        internal override ModuleType ModuleType => ModuleType.TestYourKnowledge;
        internal IExposedGameController CurrentGameController { get; private set; }
        internal TargetType CurrentTargetType { get; private set; }
        internal ExposedTile CurrentTileToSearch { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            dominoInstances = new();
            exposedGameControllerDictionary = new();

            dominoInstances = exposedDominoParent.GetComponentsInChildren<ExposedTile>(true)?.ToList();
        }

        private void Update()
        {
            if (CurrentGameController != null && canvasGroup.alpha is 1)
                ConfigureProbabilities();
        }

        internal override void SetActive(bool isActive)
        {
            base.SetActive(isActive);

            if (isActive)
                StartPractice();
        }

        internal override void Initialize(DictionaryService dictionaryService, Func<GameMode> getCurrentGameMode, Action<bool> setBackButtonVisibility)
        {
            base.Initialize(dictionaryService, getCurrentGameMode, setBackButtonVisibility);

            if (exposedDominoPrefab == null)
            {
                Debug.LogWarning($"Exposed domino prefab is not assigned in {gameObject.name}");
                return;
            }

            var spriteCollection = dictionaryService.GetSpriteCollection(Consts.CollectionKeys.DominoSkins);
            if (spriteCollection is not null and { Length: > 0 })
                for (var i = 0; i < spriteCollection.Length; i++)
                {
                    var sprite = spriteCollection[i];

                    var dominoInstance = dominoInstances?.ElementAtOrDefault(i) ?? Instantiate(exposedDominoPrefab, exposedDominoParent);
                    var (dominoTopIndex, dominoBottomIndex) = GetupDominoInfo(i);

                    dominoInstance.ConfigureDomino(i, dominoTopIndex, dominoBottomIndex, true, true, sprite);

                    if (!dominoInstances.Contains(dominoInstance))
                        dominoInstances.Add(dominoInstance);
                }
            else 
            { 
                Debug.LogWarning($"Sprite collection is not assigned in {gameObject.name}");
                return;
            }


            // Intialize the exposed domino group UI
            exposedDominoCustomToggleGroupUI.SetOnCustomButtonSelectedCallback(OnPressTilePercentageToShow);
            exposedDominoCustomToggleGroupUI.Configure();

            if (openInsightPopUpButton)
                openInsightPopUpButton.onClick.AddListener(() => insightPopUpCanvasGroup.SetActive(true));

            if (closeInsightPopUpButton)
                closeInsightPopUpButton.onClick.AddListener(() => insightPopUpCanvasGroup.SetActive(false));

            if (playerDrawTileButton)
                playerDrawTileButton.onClick.AddListener(() => ChangeTileTargetType(TargetType.Player));

            if (aiDrawTileButton)
                aiDrawTileButton.onClick.AddListener(() => ChangeTileTargetType(TargetType.AI));

            if (resetGameButton)
                resetGameButton.onClick.AddListener(() => StartPractice(true));

            (byte topIndex, byte bottomIndex) GetupDominoInfo(int dominoNum)
            {
                return dominoNum switch
                {
                    >= 0 and < 7 => (0, (byte)dominoNum),
                    >= 7 and < 13 => (1, (byte)(dominoNum - 6)),
                    >= 13 and < 18 => (2, (byte)(dominoNum - 11)),
                    >= 18 and < 22 => (3, (byte)(dominoNum - 15)),
                    >= 22 and < 25 => (4, (byte)(dominoNum - 18)),
                    >= 25 and < 27 => (5, (byte)(dominoNum - 20)),
                    _ => (6, 6)

                };
            }
        }

        internal void StartPractice(bool isRestarting = false)
        {
            var currentGameMode = GameMode;

            if (CurrentGameController != null)
            {
                SetGameActive(false);

                // If the game is already running, and the game controller is the same, we just need to restart it displacing to the line when it's started
                if (CurrentGameController.GameMode == currentGameMode)
                {
                    if (isRestarting)
                        goto RestartGame;

                    SetGameActive(true);
                    return;
                }

                // Else if the game controller is different but it's already in the dictionary, we just need to set it active
                else if (exposedGameControllerDictionary.TryGetValue(currentGameMode, out var exposedGameController))
                {
                    // Turn off the old game controller
                    SetGameActive(false);
                    CurrentGameController = exposedGameController;

                    // If restart is requested, we need to restart the game jumping to the line when it's started
                    if (isRestarting)
                        goto RestartGame;

                    // Turn on the new game controller
                    SetGameActive(true);
                    return;
                }
            }

            var exposedControllerPrefab = exposedGameControllerPrefabCollection.GetExposedGameController("ExposedControllerCollection", currentGameMode);
            if (exposedControllerPrefab == null)
            {
                Debug.LogWarning($"Exposed game controller is not assigned in {gameObject.name}");
                return;
            }

            CurrentGameController = Instantiate(exposedControllerPrefab.gameObject).GetComponent<IExposedGameController>();
            exposedGameControllerDictionary.Add(currentGameMode, CurrentGameController);

            if (CurrentGameController is not null and { GameTurnController: not null })
                CurrentGameController.GameTurnController.HandleTurnOverEvent(OnTurnOver);
            else
                Debug.LogWarning($"GameTurnController is not assigned in {gameObject.name}");

            var _currentGameHolder = CurrentGameController.GetRect();

            _currentGameHolder.SetParent(gameInstanceParent, false);
            _currentGameHolder.localScale = Vector3.one;
            _currentGameHolder.offsetMin = new Vector2(0, 0);
            _currentGameHolder.offsetMin = new Vector2(0, 0);

        RestartGame:
            CurrentGameController.RestartGame(0, currentGameMode, GameType.singlePlayerIA, NumberPlayers.oneVsOne);

            ConfigureProbabilities();
            ChangeTileTargetType(TargetType.Player);
        }

        private void SetGameActive(bool isActive)
        {
            if (CurrentGameController == null)
            {
                Debug.LogWarning($"GameController is not assigned in {gameObject.name}");
                return;
            }

            CurrentGameController.gameObject.SetActive(isActive);
            DetermineBoardTilesVisibility();
        }

        private void ConfigureProbabilities()
        {
            if (!percentageOfWinning || !percentageOfGameBlocked || !percentageOfTie || !percentageOfTileToAppear)
            {
                Debug.LogWarning($"Probabilities text labels are not assigned in {gameObject.name}");
                return;
            }

            var winningProbabilities = CurrentGameController.CalculateWinningProbability(true, winningPercentageVisibleInfo);
            var blockProbabilities = CurrentGameController.CalculateBlockingProbability(gameBlockedPercentageVisibleInfo);
            var tieProbabilities = CurrentGameController.CalculateTieProbability(tiePercentageVisibleInfo);

            percentageOfWinning.text = $"{Mathf.RoundToInt((float)winningProbabilities * 100)}%";
            percentageOfGameBlocked.text = $"{Mathf.RoundToInt((float)blockProbabilities * 100)}%";
            percentageOfTie.text = $"{Mathf.RoundToInt((float)tieProbabilities * 100)}%";
        }

        private void ChangeTileTargetType(TargetType targetType)
        {
            if (CurrentGameController == null)
            {
                Debug.LogWarning($"GameController is not assigned in {gameObject.name}");
                return;
            }
            CurrentTargetType = targetType;
            DetermineBoardTilesVisibility();
        }

        private void DetermineBoardTilesVisibility()
        {
            var boneyardTiles = CurrentGameController.BoneyardTiles;
            var knownTiles = new List<DragHandler>(boneyardTiles);

            if (CurrentTargetType is TargetType.Player)
                knownTiles.AddRange(CurrentGameController.AITiles);

            else if (CurrentTargetType is TargetType.AI)
                knownTiles.AddRange(CurrentGameController.PlayerTiles);

            if (knownTiles is not null and { Count: > 0 }) 
            {
                var knownTileIDs = knownTiles?.Select(tile => tile.GetDominoView().GetDomino().id)?.ToList();
                foreach (var dominoInstance in dominoInstances)
                {
                    var dominoID = dominoInstance.Domino.id;
                    dominoInstance.SetInteractibity(knownTileIDs.Contains(dominoID));
                }
            }

            if (CurrentTileToSearch)
            { 
                var tileProbabilities = CurrentGameController.CalculateTileDrawProbability
                    (targetType: CurrentTargetType,
                    tileToSearch: ((byte)CurrentTileToSearch.Domino.TopIndex, (byte)CurrentTileToSearch.Domino.BottomIndex));

                percentageOfTileToAppear.text = $"{tileProbabilities}%";
            }
        }

        private void OnPressTilePercentageToShow(string tileUID)
        {
            var exposedDominoInstance = exposedDominoCustomToggleGroupUI.GetButtonUI(tileUID) as ExposedTile;
            if (CurrentGameController == null || !exposedDominoInstance)
            {
                Debug.LogWarning($"GameController is not assigned in {gameObject.name}");
                return;
            }

            CurrentTileToSearch = exposedDominoInstance;
            portraitExposedTile?.ConfigureDomino(exposedDominoInstance.Domino.id,
                exposedDominoInstance.Domino.TopIndex,
                exposedDominoInstance.Domino.BottomIndex,
                true, true, exposedDominoInstance.Sprite);

            var tileProbabilities = CurrentGameController.CalculateTileDrawProbability
                (targetType: CurrentTargetType,
                tileToSearch: ((byte)CurrentTileToSearch.Domino.TopIndex, (byte)CurrentTileToSearch.Domino.BottomIndex));

            percentageOfTileToAppear.text = $"{tileProbabilities}%";
        }

        private void OnTurnOver()
        {
            ConfigureProbabilities();
        }

    }
}
