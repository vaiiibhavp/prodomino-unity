using HelperSharedLibrary;
using TMPro;
using UnityEngine;
using Timba;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using System;
using Timba.Utils;

namespace ProDomino.MissionSystem
{
    internal class MissionElement : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text rewardLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text refreshLabel;
        [SerializeField] private Button claimButton;

        [Space, Header("Main Containers")]
        [SerializeField] private GameObject RewardContainer;
        [SerializeField] private GameObject ProgressContainer;
        [SerializeField] private GameObject ResetTimeContainer;

        private AsyncActionHandler<string> claimReward;
        private PlayerMissionData playerMissionData;
        private GameMissionData gameMissionData;

        private Func<bool> checkIfIsAuthenticated;
        private Func<DateTime> getEstimateServerTime;
        private Func<(DateTime daily, DateTime weekly)> getMissionsResetTime;

        internal PlayerMissionData PlayerMissionData { get; private set; }
        internal GameMissionData GameMissionData { get; private set; }

        internal bool IsAuthenticated => checkIfIsAuthenticated?.Invoke() ?? false;
        internal DateTime EstimatedServerTime => getEstimateServerTime?.Invoke() ?? DateTime.UtcNow;
        internal DateTime NextDailyReset => getMissionsResetTime?.Invoke().daily ?? DateTime.MinValue;
        internal DateTime NextWeeklyReset => getMissionsResetTime?.Invoke().weekly ?? DateTime.MinValue;

        private void Update()
        {
            if (PlayerMissionData is null)
                return;

            if (PlayerMissionData != playerMissionData)
                playerMissionData = (PlayerMissionData)PlayerMissionData.Clone(); // Update the local copy with the current mission data
            else
            { 
                ConfigureTimeRemaining();
                return;
            }

            ConfigurePopUp();
        }

        internal void Initialize
            (Func<bool> checkIfIsAuthenticated,
            AsyncActionHandler<string> claimReward,
            Func<DateTime> getEstimateServerTime, 
            Func<(DateTime daily, DateTime weekly)> getMissionsResetTime)
        {
            this.checkIfIsAuthenticated = checkIfIsAuthenticated;
            this.claimReward = claimReward;
            this.getEstimateServerTime = getEstimateServerTime;
            this.getMissionsResetTime = getMissionsResetTime;

            // Subscribe to the claim button click event
            if (claimButton)
                claimButton.onClick.AddListener(OnClaimReward);
        }

        internal void Configure(PlayerMissionData playerMissionData, GameMissionData gameMissionData)
        {
            // Save the references to the mission data
            PlayerMissionData = playerMissionData;
            GameMissionData = gameMissionData;

            // Clone the data to avoid direct references (use when is necessary check if the data has changed)
            this.playerMissionData = (PlayerMissionData)PlayerMissionData.Clone();
            this.gameMissionData = (GameMissionData)GameMissionData.Clone();

            ConfigurePopUp();
        }

        private void ConfigurePopUp()
        {
            // Update the labels with the current mission data
            if (nameLabel)
                nameLabel.text = gameMissionData.name.BoldNumbers();
            
            if (rewardLabel)
                rewardLabel.text = $"<b>{gameMissionData.rewardAmount}</b>";
            
            if (progressLabel)
                progressLabel.text = !playerMissionData.completed
                    ? $"<b>{Mathf.Clamp(playerMissionData.progress, 0, gameMissionData.goalAmount)}</b>/<b>{gameMissionData.goalAmount}</b>"
                    : !playerMissionData.claimed ? "Claim" : "Completed";

            // Hide the element if the mission is completed and claimed
            if (playerMissionData is { completed: true } and { claimed: true })
                gameObject.SetActive(false);

            var isCompleted = PlayerMissionData.completed;
            var isClaimed = PlayerMissionData.claimed;

            // 1. RewardContainer: active if NOT claimed
            if (RewardContainer != null && RewardContainer.activeSelf != !isClaimed)
                RewardContainer.SetActive(!isClaimed);

            // 2. ProgressContainer: active if NOT completed AND NOT claimed
            bool showProgress = !isCompleted && !isClaimed;
            if (ProgressContainer != null && ProgressContainer.activeSelf != showProgress)
                ProgressContainer.SetActive(showProgress);

            // 3. claimButton: active if completed BUT NOT claimed
            bool showClaimButton = isCompleted && !isClaimed;
            if (claimButton != null && claimButton.gameObject.activeSelf != showClaimButton)
                claimButton.gameObject.SetActive(showClaimButton);

            // 4. ResetTimeContainer: active if completed AND claimed
            bool showResetTime = isCompleted && isClaimed;
            if (ResetTimeContainer != null && ResetTimeContainer.activeSelf != showResetTime)
                ResetTimeContainer.SetActive(showResetTime);

            ConfigureTimeRemaining();
        }

        private void ConfigureTimeRemaining()
        {
            if (refreshLabel == null || getEstimateServerTime == null || PlayerMissionData is {completed: false } or { claimed: false }  )
            {
                refreshLabel.text = string.Empty;
                return;
            }

            var NextResetTime = playerMissionData.isDaily ? NextDailyReset : NextWeeklyReset;
            refreshLabel.text = (NextResetTime - EstimatedServerTime).FormatTimeRemaining();
        }

        private async void OnClaimReward()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Player is not authenticated. Cannot claim reward.");
                return;
            }

            if (playerMissionData is not null && claimReward is not null)
            {
                // Hide the mission interface button if it exists
                if (claimButton)
                    claimButton.interactable = false;

                // Use an try-catch block to handle any exceptions during the claim process to avoid crashes related the button state
                try
                {
                    await claimReward.Invoke(playerMissionData.id);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to claim reward for mission {playerMissionData.id}: {ex.Message}");
                }

                // Turn on the button again after the claim process
                if (claimButton)
                    claimButton.interactable = true;
            }
        }
    }
}
