using System.Collections;
using System.Collections.Generic;
using ProDomino.GameSystem;
using ProDomino.Shared;
using Timba.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace ProDomino.ReplaySystem
{
    public class ReviewUI : MonoBehaviour, INavigationPanel
    {
        private GameManager gameManager;
        [field: SerializeField] public CanvasGroup RootCanvasGroup { get; private set; }
        [SerializeField] private CustomButtonUI 
            renameButton, 
            deleteButton, 
            reviewButton;

        public NavigationPanelType NavigationPanelType => NavigationPanelType.Review;
        public bool RequiresAuthentication => false;

        [SerializeField] CanvasGroup LatestSavedGamesCanvasGroup;
        [SerializeField] ReplayTurnByTurn replayTurnByTurn;
        [SerializeField] ReplayChangeNameDataPanel replayChangeNameDataPanel;

        [SerializeField] CanvasGroup deleteConfirmPanel_cg;

        [SerializeField] List<ReplaySavedGameContainer> replaySavedGameContainers;

        [SerializeField] int indexMatchReplaySelected = -1;

        private bool panelInitialized = false;

        private void Awake()
        {
            gameManager = ServiceLocator.Instance.GetService<GameManager>();
        }

        private void Start()
        {
            for (int i = 0; i < replaySavedGameContainers.Count; i++)
            {
                int index = i + 1;
                replaySavedGameContainers[i].button.onClick.AddListener(() => SelectedReplayData(index));
            }

            gameManager?.HandleOnSignIn(SetButtonsInteractivity);
        }

        private void OnDestroy()
        {
            gameManager?.HandleOnSignOut(SetButtonsInteractivity);
        }

        private void Update()
        {
            var interactableState = indexMatchReplaySelected > 0;
            if (renameButton)
                renameButton.SetButtonInteractable(interactableState);

            if (deleteButton)
                deleteButton.SetButtonInteractable(interactableState);

            if (reviewButton)
                reviewButton.SetButtonInteractable(interactableState);
        }

        public void OnClickOpenReviewTurnByTurn()
        {
            if (indexMatchReplaySelected > 0)
            {
                LatestSavedGamesCanvasGroup.interactable = false;
                LatestSavedGamesCanvasGroup.blocksRaycasts = false;
                LatestSavedGamesCanvasGroup.alpha = 0;

                replayTurnByTurn.InitializeTurnByTurnReplay(ReplayManager.Instance.CurrentReplayList[indexMatchReplaySelected - 1]);
                /*MatchReplay replayData = ReplayManager.Instance.CurrentReplayList[indexMatchReplaySelected];
                ReplayManager.Instance.StartReplay(replayData);
                UIManager.Instance.ShowPanel(NavigationPanelType.Replay);*/
            }
            else
            {
                Debug.LogWarning("⚠️ No replay selected or index out of range.");
            }
        }

        public void DeleteSelectedReplay()
        {
            if (indexMatchReplaySelected > 0)
            {
                OpenConfirmDeletePanel(true);
            }
            else
            {
                Debug.LogWarning("⚠️ No replay selected or index out of range.");
            }
        }

        public void ConfirmDeletion()
        {
            if (indexMatchReplaySelected > 0)
            {
                ReplayManager.Instance.DeleteReplayByIndex(ReplayManager.Instance.CurrentReplayList[indexMatchReplaySelected - 1].saveSlotIndex);
                indexMatchReplaySelected = -1;
                LoadReplaySlots();
                TurnByTurnBackToSavedGames();

                OpenConfirmDeletePanel(false);
            }
            else
            {
                Debug.LogWarning("⚠️ No replay selected or index out of range.");
            }
        }

        public void OpenConfirmDeletePanel(bool isActive)
        {
            deleteConfirmPanel_cg.alpha = isActive ? 1 : 0;
            deleteConfirmPanel_cg.blocksRaycasts = isActive;
            deleteConfirmPanel_cg.interactable = isActive;
        }

        #region Change Name Panel
        public void OpenChangeNamePanel()
        {
            if (indexMatchReplaySelected > 0)
            {
                replayChangeNameDataPanel.OpenChangeNameDataPanel(
                    ReplayManager.Instance.CurrentReplayList[indexMatchReplaySelected - 1].matchId,
                    (newName) =>
                    {
                        ReplayManager.Instance.ChangeReplayNameByIndex(ReplayManager.Instance.CurrentReplayList[indexMatchReplaySelected - 1].saveSlotIndex, newName);
                        replayTurnByTurn.titleNameLabel.text = newName;
                        LoadReplaySlots();
                    },
                    () =>
                    {
                        // Cancelar
                    });
            }
            else
            {
                Debug.LogWarning("⚠️ No replay selected or index out of range.");
            }
        }
        #endregion   

        public void TurnByTurnBackToSavedGames()
        {
            replayTurnByTurn.CloseTurnByTurnReplay();

            LatestSavedGamesCanvasGroup.interactable = true;
            LatestSavedGamesCanvasGroup.blocksRaycasts = true;
            LatestSavedGamesCanvasGroup.alpha = 1;
        }

        private void SelectedReplayData(int index)
        {
            indexMatchReplaySelected = index;
            replayTurnByTurn.titleNameLabel.text = ReplayManager.Instance.CurrentReplayList[indexMatchReplaySelected - 1].matchId;
        }

        void INavigationPanel.SetActiveNavigationPanel(bool isActive)
        {
            RootCanvasGroup?.SetActive(isActive);
            RootCanvasGroup.transform.RefreshLayoutGroupsImmediateAndRecursive();

            indexMatchReplaySelected = -1;

            if (isActive)
            {
                panelInitialized = true;
                LoadReplaySlots();
            }
            else
            {
                if(panelInitialized)
                {
                    TurnByTurnBackToSavedGames();
                }
            }
        }

        private void SetButtonsInteractivity()
        {
            SetButtonInteractivity(renameButton);
            SetButtonInteractivity(deleteButton);
            SetButtonInteractivity(reviewButton);
        }

        private void SetButtonInteractivity(CustomButtonUI button)
        {
            if (!button)
            {
                Debug.LogError("Button component is missing on OpenFriendListPopUpDirectly script.");
                return;
            }

            // Enable or disable the button based on authentication and initialization status
            if (gameManager is not null)
            {
                var shouldButtonBeInteractable = gameManager.IsAuthenticatedAndVerified;
                button.SetButtonInteractable(shouldButtonBeInteractable);

                // Also manage the raycast target of the tooltip image if it exists
                if (button.TooltipContainer && button.TooltipContainer.TryGetComponent<Image>(out var image))
                    image.raycastTarget = !shouldButtonBeInteractable;
                else
                    Debug.LogWarning("TooltipContainer or Image component is missing on the button.");
            }
        }

        private void LoadReplaySlots()
        {
            // Ensure ReplayManager instance is available before trying to access it
            if (!ReplayManager.Instance)
            {
                Debug.LogError("ReplayManager instance is not available.");
                return;
            }

            // Get the latest replay data from the ReplayManager and save it globally
            ReplayManager.Instance.RunLoadSlots();

            // Check if the list is assigned before trying to access it
            if (replaySavedGameContainers is null)
            { 
                Debug.LogWarning("ReplaySavedGameContainers list is not assigned in the inspector.");
                return;
            }

            for (int i = 0; i < replaySavedGameContainers.Count; i++)
            {
                var container = replaySavedGameContainers[i];

                if (i < ReplayManager.Instance.CurrentReplayList.Count)
                {
                    var replayData = ReplayManager.Instance.CurrentReplayList[i];

                    // Check if replayData is null before trying to access its properties
                    if (replayData is null)
                    {
                        Debug.LogError($"Replay data at index {i} is null. Skipping this entry.");
                        continue;
                    }

                    // Populate the UI elements with the replay data
                    container.nameLabel.text = replayData.matchId;
                    container.gameTypeLabel.text = replayData.gameType.ToString().CapitalizeFirstLetter().SplitByUpperCase();
                    container.dateLabel.text = replayData.date;
                    container.gameModeLabel.text = replayData.gameMode.ToString().CapitalizeFirstLetter();
                    container.gameModeIcon.sprite = gameManager.GetSprite(replayData.gameMode.ToString(), Consts.CollectionKeys.GameMode);

                    // Show the button with data and hide the "no saved data" message
                    container.noSavedDataCanvasGroup.SetActive(false);
                    container.buttonWithDataCanvasGroup.SetActive(true);
                }

                // If there's no replay data for this slot, show the "no saved data" message and hide the button with data
                else
                {
                    container.noSavedDataCanvasGroup.SetActive(true);
                    container.buttonWithDataCanvasGroup.SetActive(false);
                }
            }
        }
    }
}
