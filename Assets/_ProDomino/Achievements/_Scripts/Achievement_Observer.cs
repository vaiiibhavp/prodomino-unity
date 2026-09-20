using HelperSharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.AchievementSystem
{
    /// <summary>
    /// Manages player achievement data, including progress tracking, completion status, and analytics reporting.
    /// </summary>
    public partial class AchievementManager
    {
        /// <summary>
        /// Updates the player's achievement data based on the specified analytic type and associated data, handling
        /// progress, completion, and analytics reporting.
        /// </summary>
        /// <param name="type">The analytic type indicating which achievement or mission to update.</param>
        /// <param name="data">The data associated with the analytic event, typically representing progress or completion information.</param>
        public async void UpdatePlayerAchievementData(AnalyticType type, object data)
        {
            var achievementsToUpdate = PlayerAchievementsDataCollection?.Where(x => x.Key.analyticType == type)?.ToList() ?? new();
            achievementsToUpdate = achievementsToUpdate?.Where(x => !x.Value?.completed ?? true)?.ToList();

            var anyAchievementWasCompleted = false;
            uint? uintData = data != null && uint.TryParse(data.ToString(), out uint uintValue) ? uintValue : null;

            switch (type)
            {
                case AnalyticType.None:
                    break;

                case AnalyticType.AchievementsCompleted:
                    await UpdateProgress();
                    break;

                case AnalyticType.TotalCompetitiveWins:
                case AnalyticType.CompleteTutorial:
                case AnalyticType.ObtainCosmetic:
                    // Set the flag to indicate that an update was made
                    WasUpdate = true;
                    anyAchievementWasCompleted = true;
                    break;

                case AnalyticType.AddFriends:
                case AnalyticType.PlaceTilesOnBoard:
                    if (achievementsToUpdate == null || achievementsToUpdate.Count == 0)
                    { 
                        Debug.LogWarning($"UpdatePlayerMissionData: No missions to update for analytic type {type} with data: {data}");
                        return; // No missions to update for this analytic type
                    }

                    if (uintData == null)
                    { 
                        Debug.LogWarning($"UpdatePlayerMissionData: Data for {type} is not in the expected format (uint). Received: {data}");
                        return; // Data is not in the expected format
                    }

                    // Iterate foreach mission and update progress if it matches the analytic type
                    for (var i = 0; i < achievementsToUpdate.Count; i++)
                    {
                        var (gameAchievementData, playerAchievementData) = achievementsToUpdate[i];

                        // If the achievement data is null, initialize it
                        if (playerAchievementData is null)
                        { 
                            playerAchievementData = new(gameAchievementData.achievement);
                            PlayerAchievementsDataCollection[gameAchievementData] = playerAchievementData;
                        }

                        if (gameAchievementData.analyticType == type && !playerAchievementData.completed)
                        {
                            // Set the flag to indicate that an update was made
                            WasUpdate = true;
                            Debug.Log($"Updating achievement '{gameAchievementData.achievement}' progress by {uintData.Value}." +
                                $"\n\nPrevious progress: {playerAchievementData.progress}" +
                                $"\nNew progress: {playerAchievementData.progress + uintData.Value}");

                            playerAchievementData.progress += uintData.Value;
                            playerAchievementData.lastProgressUpdateTime = ((DateTimeOffset)(EstimatedServerTime ?? DateTime.UtcNow)).ToUnixTimeSeconds();

                            // Log the progress update for debugging purposes
                            if (!EstimatedServerTime.HasValue)
                                Debug.LogWarning("<b>[CRITIC]</b> EstimatedServerTime is null. This may affect the last progress update time. Using local utc");

                            CheckCountableAchievementCompletion((gameAchievementData, playerAchievementData), ref anyAchievementWasCompleted);
                        }
                    }

                    AchievementUI.RefreshElements();
                    break;
            }

            // Once the iteration is complete, check if any achievement was completed
            if (anyAchievementWasCompleted)    
                analyticsManager.SendAnalytic(AnalyticType.AchievementsCompleted);

            /// This method checks if a countable achievement has been completed based on the player's progress and the achievement's goal amount.
            void CheckCountableAchievementCompletion((GameAchievementData gameAchievementData, PlayerAchievementData playerAchievementData) achievementData, ref bool _anyAchievementWasCompleted)
            {
                var (gameAchievementData, playerAchievementData) = achievementData;

                // If the mission is not completed and the progress meets or exceeds the goal amount, mark it as completed
                if (playerAchievementData.completed || playerAchievementData.progress < gameAchievementData.goalAmount) 
                    return;

                playerAchievementData.completed = true;
                _anyAchievementWasCompleted = true; // Track if any mission was completed
            }
        }
    }
}
