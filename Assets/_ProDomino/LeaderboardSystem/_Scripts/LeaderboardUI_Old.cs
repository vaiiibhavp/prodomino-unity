using ProDomino.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Leaderboards.Models;
using UnityEngine;
using UnityEngine.Events;
using static HelperSharedLibrary.Enums;

namespace ProDomino.Leaderboard
{
    internal class LeaderboardUI_Old : AbstractLeaderboardUI, INavigationPanel
    {
        [Header("Leaderboard UI - Old")]
        [SerializeField] private Transform elementsParent1, elementsParent2;
        [SerializeField] private TooltipTarget UpdateAdviceTooltipTarget;

        [Header("Filter Custom Buttoms Toggle Group")]
        [SerializeField] private CustomButtonToggleGroupUI gameModesFiltersToggleGroup;
        [SerializeField] private CustomButtonToggleGroupUI tierFiltersToggleGroup;
        [SerializeField] private CustomButtonToggleGroupUI playerAmountFiltersToggleGroup;

        internal override void Awake_LeaderboardUI()
        {
            leaderboardElements = elementsParent1.GetComponentsInChildren<LeaderboardElement>(true)?.ToList() ?? new();
            leaderboardElements.AddRange(elementsParent2.GetComponentsInChildren<LeaderboardElement>(true) ?? Array.Empty<LeaderboardElement>());

            // Initialize each leaderboard element with the method to get the sprite for a given game mode
            if (leaderboardElements is not null and { Count: > 0 })
                leaderboardElements.ForEach(element => element.Initialize(GetSprite));

            if (gameModesFiltersToggleGroup)
                gameModesFiltersToggleGroup.SetOnCustomButtonSelectedCallback(OnGameModeSelected);

            if (tierFiltersToggleGroup)
                tierFiltersToggleGroup.SetOnCustomButtonSelectedCallback(OnTierSelected);

            if (playerAmountFiltersToggleGroup)
                playerAmountFiltersToggleGroup.SetOnCustomButtonSelectedCallback(OnPlayerAmountSelected);
        }

        internal override void Start_LeaderboardUI()
        {
            // By default, select the first button in each toggle group (each entry filters the leaderboard)
            gameModesFiltersToggleGroup?.GetFirstButtonUI()?.Select();
            tierFiltersToggleGroup?.GetFirstButtonUI()?.Select();
            playerAmountFiltersToggleGroup?.GetFirstButtonUI()?.Select();
        }

        internal override void Update_LeaderboardUI()
        {
            // If the leaderboard is saving in cache, update the advice label with the countdown
            // Its check the cache due to the fact that the leaderboard data is not updated in real-time,
            // but rather at specific intervals (only if this is enabled)
            if (IsSavingInCache && UpdateAdviceTooltipTarget)
            {
                if (!UpdateAdviceTooltipTarget.isActiveAndEnabled)
                    UpdateAdviceTooltipTarget.gameObject.SetActive(true);
                UpdateCountdownText();
            }

            // If the leaderboard is not saving in cache, hide the update advice label
            else if ((UpdateAdviceTooltipTarget?.isActiveAndEnabled) ?? false)
                UpdateAdviceTooltipTarget.gameObject.SetActive(false);
        }

        internal override void AddListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener)
        {
            if (listener == null)
            {
                Debug.LogWarning("Listener is null. Cannot add to onPlayerLeaderboardDataUpdated.");
                return;
            }
            onPlayerLeaderboardDataUpdated.AddListener(listener);
        }
        internal override void RemoveListener_OnPlayerLeaderboardDataUpdated(UnityAction<Dictionary<string, LeaderboardEntry>> listener)
        {
            if (listener == null)
            {
                Debug.LogWarning("Listener is null. Cannot remove from onPlayerLeaderboardDataUpdated.");
                return;
            }
            onPlayerLeaderboardDataUpdated.RemoveListener(listener);
        }

        /// <summary>
        /// Filters the collection of elements based on predefined criteria.
        /// </summary>
        internal override void ConfigureFilters()
        {
            // Apply filtering and ordering to the leaderboard elements based on the selected filters
            FilterAndOrderElements(LeaderboardPlayers);
        }

        // Updates the countdown text based on current time
        private void UpdateCountdownText()
        {
            // Get remaining time
            TimeSpan remaining = NextUpdateTime - DateTime.UtcNow;

            // Clamp to zero to avoid negative times
            if (remaining < TimeSpan.Zero)
                remaining = TimeSpan.Zero;

            // Extract components
            var days = remaining.Days;
            var hours = remaining.Hours;
            var minutes = remaining.Minutes;
            var seconds = remaining.Seconds;

            // Build formatted string dynamically
            var formatted = string.Empty;

            if (days > 0)
                formatted += $"{days}d ";

            if (hours > 0 || days > 0)
                formatted += $"{hours:D2}h ";

            if (minutes > 0 || hours > 0 || days > 0)
                formatted += $"{minutes:D2}m ";

            formatted += $"{seconds:D2}s";

            // Update the UI element
            UpdateAdviceTooltipTarget?.SetMessage($"Time to update leaderboard: <b>{formatted}</b>");
        }

        private void OnGameModeSelected(string obj)
        {
            if (!Enum.TryParse(obj, out GameMode gameMode))
            {
                Debug.LogWarning($"Invalid game mode selected: {obj}");
                return;
            }

            SelectedGameMode = gameMode;
            ConfigureFilters();
        }

        private void OnTierSelected(string obj)
        {
            if (!Enum.TryParse(obj, out LeaderboardTier leaderboardTier))
            {
                Debug.LogWarning($"Invalid tier selected: {obj}");
                return;
            }

            SelectedTier = leaderboardTier;
            ConfigureFilters();
        }

        private void OnPlayerAmountSelected(string obj)
        {
            if (!Enum.TryParse(obj, out NumberPlayers numberPlayers))
            {
                // Check fi the entry arg is empty. If so, that means the toggle is probably deselecting
                if (obj is not "")
                    Debug.LogWarning($"Invalid player amount selected: {obj}");
                return;
            }

            SelectedNumberPlayers = numberPlayers;
            ConfigureFilters();
        }
    }
}
