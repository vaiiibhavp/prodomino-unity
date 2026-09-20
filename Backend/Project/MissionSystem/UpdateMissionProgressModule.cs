using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.MissionSystem;

public class UpdateMissionProgressModule(ILogger<UpdateMissionProgressModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<UpdateMissionProgressModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(UpdateMissionProgress))]
    public async Task<string> UpdateMissionProgress(IExecutionContext executionContext, string parametersEncryptedJson)
    {
        // Validate the execution context
        if (executionContext is null)
            throw new ArgumentNullException(nameof(executionContext), "Execution context cannot be null.");

        // Check if the execution context is null
        BackendHelper.ContextValidation(executionContext);

        // Get the player ID from the execution context and validate it
        var playerId = executionContext.PlayerId;
        if (string.IsNullOrEmpty(playerId))
            throw new Exception("Player ID is invalid or null.");

        // Get the data from the encrypted parameters JSON
        var data = BackendHelper.ValidateEncriptedParameters
            (parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        // Validate entry data
        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data. Provide provider id");

        // Check if the data dictionary contains the required keys and values
        if (!data.TryGetValue("updatesFromFrontend", out var updatesFromFrontendObj) 
            || updatesFromFrontendObj is not JToken updatesFromFrontendToken 
            || updatesFromFrontendToken.ToObject<PlayerMissionData[]>() is not PlayerMissionData[] updatesFromFrontend 
            || updatesFromFrontend is null or { Length: 0 })
            throw new ArgumentException("Missions are not specified");

        // Load Game Data
        var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.DailyMissions.ToString(),
            CloudSaveProperties.WeeklyMissions.ToString());

        // Get daily missions defined in Game Data
        var dailyGameDataMissions = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.DailyMissions.ToString())
            ?.value?.ToObject<GameMissionData[]>();

        // Get weekly missions defined in Game Data
        var weeklyGameDataMissions = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.WeeklyMissions.ToString())
            ?.value?.ToObject<GameMissionData[]>();

        // Check if the Game Data dictionary contains the required keys and values
        if (dailyGameDataMissions is null or { Length: 0 } || weeklyGameDataMissions is null or { Length: 0 })
            throw new UGSException("No game mission definitions found.");

        // Load player missiones
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Missions.ToString());

        // Try to get the player data missions
        var currentMissions = playerDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Missions.ToString())
            ?.value?.ToObject<PlayerMissionData[]>();

        if (currentMissions is null || currentMissions.Length == 0)
            throw new UGSException("No player missions found.");

        // Concatenate daily and weekly game missions into a single array
        var allGameMissions = dailyGameDataMissions.Concat(weeklyGameDataMissions).ToArray();

        // Update the progress of the missions based on the updates from the frontend
        var updatedMissions = UpdateMissions(currentMissions, updatesFromFrontend, allGameMissions!);
        if (updatedMissions is null || updatedMissions.Length == 0)
            throw new UGSException("No updated missions found.");

        // Save the updated missions to the player's data
        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
            new()
            {
                [CloudSaveProperties.Missions.ToString()] = updatedMissions
            });

        // Split the updated missions into daily and weekly lists
        var dailyPlayerMissions = updatedMissions!.Where(x => x.isDaily).ToList();
        var weeklyPlayerMissions = updatedMissions!.Where(x => !x.isDaily).ToList();

        var filteredDailyGameData = dailyGameDataMissions
            ?.Where(x => x is not null && dailyPlayerMissions.Any(p => p.id == x.id)).ToList();

        var filteredWeeklyGameData = weeklyGameDataMissions
            ?.Where(x => x is not null && weeklyPlayerMissions.Any(p => p.id == x.id)).ToList();

        if (filteredDailyGameData is null or { Count: 0 } || filteredWeeklyGameData is null or { Count: 0 })
            throw new UGSException("No game missions found for the updated player missions.");

        var missionResponse = new MissionResponse(
            filteredDailyGameData!,
            filteredWeeklyGameData!,
            dailyPlayerMissions,
            weeklyPlayerMissions);

        // Initializes the missions server times
        missionResponse.InitializeServerTimes();

        // Return the response with the updated missions and game data
        var responseDataJson = JsonConvert.SerializeObject(missionResponse);

        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(responseDataJson, derivedKey, derivedIv));
        return encryptedDataJson;
    }

    private PlayerMissionData[]? UpdateMissions
        (PlayerMissionData[] currentMissions,
        PlayerMissionData[] progressUpdates,
        GameMissionData[] gameMissions)
    {
        if (currentMissions is null or { Length: 0 } || progressUpdates is null or { Length: 0 }|| gameMissions is null or { Length: 0 })
        { 
            _logger.LogWarning("Invalid input data for mission update. Current missions, progress updates, or game missions are null or empty.");
            return null;
        }

        // Create a dictionary for quick access to GameMissionData by ID
        var missionCollection = currentMissions
            ?.Select(
                x =>
                    (currentMission: (PlayerMissionData?)x?.Clone(),
                    progressUpdate: (PlayerMissionData?)progressUpdates.FirstOrDefault(y => y.id == x!.id)?.Clone(),
                    gameMission: (GameMissionData?)gameMissions.FirstOrDefault(y => y.id == x!.id)?.Clone()))
            ?.Where(x => x is { currentMission: not null, gameMission: not null, progressUpdate: not null })
            ?.ToDictionary(x => x.currentMission?.id!);

        // Iterate for each entry and, according the update from frontend, update the mission iterated
        if (missionCollection is not null and { Count: > 0 })
        { 
            foreach (var (id, (_currentMission, _progressUpdate, _gameMission)) in missionCollection)
            {
                // If the current mission is a bonus one, omit it. The bonus process are done at the end when the other missions are already updated
                // Only update if the last progress is the most recent
                if (_currentMission!.isBonus || _progressUpdate!.lastProgressUpdateTime < _currentMission.lastProgressUpdateTime)
                    continue;

                // Get the difference in progress
                var progressUpdated = _progressUpdate.progress - _currentMission.progress;

                // If there aren't progress, continue the iteration
                if (progressUpdated <= 0)
                    continue;

                // Sum progress and update the lastProgressUpdateTime
                _currentMission.progress += progressUpdated;
                _currentMission.lastProgressUpdateTime = _progressUpdate.lastProgressUpdateTime;

                // Check if the mission is completed
                if (_currentMission.progress >= _gameMission!.goalAmount)
                {
                    _currentMission.completed = true;
                    _currentMission.completedTime = _progressUpdate.lastProgressUpdateTime;
                    _logger.LogInformation("Mission {MissionId} completed for player {PlayerId}.", _currentMission.id, _currentMission.id);
                }
            }

            // Iterate again to update the bonus missions
            foreach (var (id, (_currentMission, _progressUpdate, _gameMission)) in missionCollection)
            { 
                if (!_currentMission!.isBonus)
                    continue;

                // If the current mission is a bonus one, check if all daily missions are completed
                var allDailyMissionsCompleted = missionCollection.Values
                    .Where(x => x.currentMission is { isDaily: true, isBonus: false })
                    .All(x => x.currentMission is { completed: true, claimed: true });

                if (allDailyMissionsCompleted)
                {
                    _currentMission.completed = true;
                    _currentMission.completedTime = _progressUpdate!.lastProgressUpdateTime;
                    _logger.LogInformation("Bonus mission {MissionId} completed for player {PlayerId}.", _currentMission.id, _currentMission.id);
                }
            }
        }
        else
        { 
            _logger.LogWarning("No missions found to update for player {PlayerId}.", currentMissions?.FirstOrDefault()?.id);
            return null;
        }

        return missionCollection.Values.Select(x => x.currentMission).ToArray()!;
    }
}
