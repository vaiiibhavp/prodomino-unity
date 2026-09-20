using HelperSharedLibrary;
using System;
using System.Linq;
using Timba.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static HelperSharedLibrary.Enums;

namespace ProDomino.AchievementSystem
{
    /// <summary>
    /// Represents a UI component for displaying and managing achievement information, including progress, rewards, and
    /// claim actions.
    /// </summary>
    internal class AchievementElement : MonoBehaviour
    {
        [SerializeField] private bool isAddingRewardsToDescription;
        [SerializeField] private Button claimButton;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private Image achievementIcon;
        [SerializeField] private GameObject rewardObject;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text requirementLabel;
        [SerializeField] private TMP_Text completedDateLabel;

        [Space, Header("Main Containers")]
        [SerializeField] private GameObject incompletedContainer;
        [SerializeField] private GameObject completedContainer;

        private AsyncActionHandler<Achievement> claimReward;
        private PlayerAchievementData temporalPlayerAchievementData;

        private Func<bool> checkIfIsAuthenticated;
        private Func<(Achievement achievement, AchievementRank rank), Sprite> getAchievementIcon;
        private Func<string, Sprite> getRewardIcon;
        private Func<string> getHightlightHex;

        internal PlayerAchievementData PlayerAchievementData { get; private set; }
        internal GameAchievementData GameAchievementData { get; private set; }

        internal bool IsAuthenticated => checkIfIsAuthenticated?.Invoke() ?? false;
        internal string HighlightHex => getHightlightHex?.Invoke() ?? "#FFD700"; // Default to golden yellow if not set

        private void Update()
        {
            if (PlayerAchievementData is null)
                return;

            if (PlayerAchievementData != temporalPlayerAchievementData)
                temporalPlayerAchievementData = (PlayerAchievementData)PlayerAchievementData.Clone(); // Update the local copy with the current achievement data
            else
                return;

            ConfigurePopUp();
        }

        /// <summary>
        /// Initializes the achievement UI component with authentication, icon retrieval, highlight color, and reward
        /// claiming handlers.
        /// </summary>
        /// <param name="checkIfIsAuthenticated">Function to determine if the user is authenticated.</param>
        /// <param name="getAchievementIcon">Function to retrieve the icon for a given achievement and rank.</param>
        /// <param name="getRewardIcon">Function to retrieve the icon for a specified reward.</param>
        /// <param name="getHightlightHex">Function to get the highlight color in hexadecimal format.</param>
        /// <param name="claimReward">Asynchronous handler for claiming an achievement reward.</param>
        internal void Initialize
            (Func<bool> checkIfIsAuthenticated,
            Func<(Achievement achievement, AchievementRank rank), Sprite> getAchievementIcon,
            Func<string, Sprite> getRewardIcon,
            Func<string> getHightlightHex,
            AsyncActionHandler<Achievement> claimReward)
        {
            this.checkIfIsAuthenticated = checkIfIsAuthenticated;
            this.getAchievementIcon = getAchievementIcon;
            this.getRewardIcon = getRewardIcon;
            this.getHightlightHex = getHightlightHex;
            this.claimReward = claimReward;

            // Subscribe to the claim button click event
            if (claimButton)
                claimButton.onClick.AddListener(OnClaimReward);
        }

        /// <summary>
        /// Initializes the achievement data and prepares the popup with the provided game and player achievement
        /// information.
        /// </summary>
        /// <param name="gameAchievementData">The game achievement data to configure.</param>
        /// <param name="playerAchievementData">The player achievement data to configure.</param>
        internal void Configure(GameAchievementData gameAchievementData, PlayerAchievementData playerAchievementData)
        {
            // Save the references to the achievement data
            GameAchievementData = gameAchievementData;
            PlayerAchievementData = playerAchievementData;

            // Clone the data to avoid direct references (use when is necessary check if the data has changed)
            if (PlayerAchievementData is not null)
                temporalPlayerAchievementData = (PlayerAchievementData)PlayerAchievementData.Clone();

            ConfigurePopUp();
        }

        /// <summary>
        /// Configures the achievement pop-up UI elements based on the current achievement and player progress data.
        /// </summary>
        private void ConfigurePopUp()
        {
            var isCompleted = PlayerAchievementData?.completed ?? false;
            var isClaimed = PlayerAchievementData?.claimed ?? false;

            var showClaimButton = isCompleted && !isClaimed;
            var notReadyYet = !isCompleted && !isClaimed;
            var readyToReclaim = isCompleted && !isClaimed;

            var hasReward = (GameAchievementData.rewardCosmeticsIDs?.Any(x => !string.IsNullOrEmpty(x)) ?? false);

            // Update the labels with the current achievement data
            if (nameLabel)
                nameLabel.text = GameAchievementData.name.BoldNumbers();

            if (descriptionLabel)
            {
                var description = GameAchievementData.description;
                var rewards = string.Join(", ", GameAchievementData.rewardCosmeticsIDs.Select(x => $"<b>{x}</b>"));

                // Replace last comma with "and" for better readability
                if (rewards.LastIndexOf(",") is var lastCommaIndex and > 0)
                    rewards = rewards.Remove(lastCommaIndex, 1).Insert(lastCommaIndex, " and");

                // Set the description label text
                descriptionLabel.text = $"{description}";

                // If isAddingRewardsToDescription, append the rewards to the description
                if (isAddingRewardsToDescription)
                    descriptionLabel.text += $"\n\n<b>Reward{(GameAchievementData.rewardCosmeticsIDs.Length > 1 ? "s" : "")}</b>:\n{rewards}";
            }

            // Format the requirement label with the current progress and total
            if (requirementLabel)
                requirementLabel.text = $"{PlayerAchievementData?.progress ?? 0}/{GameAchievementData.goalAmount}";

            // Update the completed date label if it exists and the completed time is set
            if (completedDateLabel && (PlayerAchievementData?.completedTime.HasValue ?? false))
                completedDateLabel.text = DateTimeOffset.FromUnixTimeSeconds(PlayerAchievementData.completedTime.Value).ToString("dd/MM/yyyy");

            // Update the reward icon if it exists and the getRewardIcon function is provided
            if (rewardIcon && hasReward && getRewardIcon is not null)
                rewardIcon.sprite = getRewardIcon.Invoke(GameAchievementData.rewardCosmeticsIDs.FirstOrDefault());

            // If there are no rewards, hide the icon
            if (rewardObject)
                rewardObject.gameObject.SetActive(hasReward);

            // Update the achievement icon if it exists and the getAchievementIcon function is provided
            if (achievementIcon)
                achievementIcon.sprite = getAchievementIcon?.Invoke((GameAchievementData.achievement, GameAchievementData.achievementRank));

            // 1. completedContainer: active if claimed
            if (completedContainer != null && completedContainer.activeSelf != isClaimed)
                completedContainer.SetActive(isClaimed);

            // 2. ProgressContainer: active if NOT completed AND NOT claimed
            if (incompletedContainer != null && incompletedContainer.activeSelf != notReadyYet)
                incompletedContainer.SetActive(notReadyYet);

            // 3. claimButton: active if completed BUT NOT claimed
            if (claimButton != null && claimButton.gameObject.activeSelf != readyToReclaim)
                claimButton.gameObject.SetActive(readyToReclaim);
        }

        /// <summary>
        /// Highlights portions of the achievement name in the label that match the provided search input.
        /// </summary>
        /// <param name="searchInput">The search string to match and highlight within the achievement name.</param>
        internal void HighlightNameMatches(string searchInput)
        {
            if (string.IsNullOrWhiteSpace(searchInput) || nameLabel == null || string.IsNullOrWhiteSpace(GameAchievementData?.name))
            {
                // Restore original name if no search input
                nameLabel.text = GameAchievementData?.name.BoldNumbers();
                return;
            }

            // Normalize input and name
            var tokens = AchievementManager.Tokenize(searchInput);
            var originalName = GameAchievementData.name;
            var loweredName = AchievementManager.RemoveDiacritics(originalName.ToLowerInvariant());

            // Track original character positions for highlighting
            var highlights = new bool[originalName.Length];

            foreach (var token in tokens)
            {
                int start = 0;
                while (start < loweredName.Length)
                {
                    int index = loweredName.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
                    if (index == -1) break;

                    // Mark positions to highlight
                    for (int i = index; i < index + token.Length && i < highlights.Length; i++)
                        highlights[i] = true;

                    start = index + token.Length;
                }
            }

            // Build the highlighted string using <mark> or <color> (TMPro safe)
            var highlighted = new System.Text.StringBuilder();
            bool inHighlight = false;

            for (int i = 0; i < originalName.Length; i++)
            {
                if (highlights[i] && !inHighlight)
                {
                    highlighted.Append($"<color=#{HighlightHex}>"); // golden yellow highlight
                    inHighlight = true;
                } else if (!highlights[i] && inHighlight)
                {
                    highlighted.Append("</color>");
                    inHighlight = false;
                }

                highlighted.Append(originalName[i]);
            }

            if (inHighlight)
                highlighted.Append("</color>");

            nameLabel.text = highlighted.ToString();
        }

        /// <summary>
        /// Attempts to claim the achievement reward for the authenticated player, handling UI state and exceptions
        /// during the process.
        /// </summary>
        private async void OnClaimReward()
        {
            if (!IsAuthenticated)
            {
                Debug.LogWarning("Player is not authenticated. Cannot claim reward.");
                return;
            }

            if (PlayerAchievementData is not null && claimReward is not null)
            {
                // Hide the achievement interface button if it exists
                if (claimButton)
                    claimButton.interactable = false;

                // Use an try-catch block to handle any exceptions during the claim process to avoid crashes related the button state
                try
                {
                    await claimReward.Invoke(PlayerAchievementData.achievement);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to claim reward for achievement {PlayerAchievementData.achievement}: {ex.Message}");
                }

                // Turn on the button again after the claim process
                if (claimButton)
                    claimButton.interactable = true;
            } 
            else
                Debug.LogWarning("PlayerAchievementData is null or claimReward is not set. Cannot claim reward.");
        }
    }
}
