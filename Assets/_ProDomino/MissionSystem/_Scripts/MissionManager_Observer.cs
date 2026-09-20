using HelperSharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HelperSharedLibrary.Enums;

namespace ProDomino.MissionSystem
{
    public partial class MissionManager
    {
        public async void UpdatePlayerMissionData(AnalyticType type, object data)
        {
            var tasksToUpdate = DailyMissionsDataCollection?.Where(x => x.Value.analyticType == type)?.ToList() ?? new();
            tasksToUpdate.AddRange(WeeklyMissionsDataCollection?.Where(x => x.Value.analyticType == type));
            tasksToUpdate = tasksToUpdate?.Where(x => !x.Key.completed)?.ToList();

            uint? uintData = data != null && uint.TryParse(data.ToString(), out uint uintValue) ? uintValue : null;
            var anyMissionWasCompleted = false;

            switch (type)
            {
                case AnalyticType.None:
                    break;

                case AnalyticType.MissionsCompleted:                    
                    await UpdateProgress();
                    break;

                case AnalyticType.Bonus:
                    if (tasksToUpdate == null || tasksToUpdate.Count == 0)
                    {
                        Debug.LogWarning($"UpdatePlayerMissionData: No missions to update for analytic type {type} with data: {data}");
                        return; // No missions to update for this analytic type
                    }

                    // Iterate foreach mission and update progress if it matches the analytic type. We only need update the progress of the frontend missions (backend missions are updated automatically)
                    foreach (var mission in tasksToUpdate)
                        if (mission.Value.analyticType == type && !mission.Key.completed)
                        {
                            // Set the flag to indicate that an update was made
                            WasUpdate = true;

                            mission.Key.progress = (uint)(DailyMissionsDataCollection?.Count(x => x.Key is { isBonus: false, claimed: true }) ?? 0);
                            mission.Key.lastProgressUpdateTime = ((DateTimeOffset)EstimatedServerTime).ToUnixTimeSeconds();

                            CheckMissionCompletion(mission, ref anyMissionWasCompleted);
                        }
                    break;

                case AnalyticType.GamesPlayed:
                case AnalyticType.TotalWins:
                case AnalyticType.TotalCompetitiveWins:
                case AnalyticType.ConcentrateMatchTiles:
                    if (tasksToUpdate == null || tasksToUpdate.Count == 0)
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
                    foreach (var mission in tasksToUpdate)
                        if (mission.Value.analyticType == type && !mission.Key.completed)
                        {
                            // Set the flag to indicate that an update was made
                            WasUpdate = true;

                            mission.Key.progress += uintData.Value;
                            mission.Key.lastProgressUpdateTime = ((DateTimeOffset)EstimatedServerTime).ToUnixTimeSeconds();

                            CheckMissionCompletion(mission, ref anyMissionWasCompleted);
                        }
                    break;
            }

            // Once the iteration is complete, check if any mission was completed
            if (anyMissionWasCompleted)    
                analyticsManager.SendAnalytic(AnalyticType.MissionsCompleted);

            void CheckMissionCompletion(KeyValuePair<PlayerMissionData, GameMissionData> mission, ref bool _anyMissionWasCompleted)
            {
                // If the mission is not completed and the progress meets or exceeds the goal amount, mark it as completed
                if (mission.Key.completed || mission.Key.progress < mission.Value.goalAmount) 
                    return;

                mission.Key.completed = true;
                _anyMissionWasCompleted = true; // Track if any mission was completed
            }
        }
    }
}
