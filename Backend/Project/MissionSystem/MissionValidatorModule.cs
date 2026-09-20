using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.MissionSystem;

public class MissionValidatorModule(ILogger<MissionValidatorModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<MissionValidatorModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(ValidateMissions))]
    public async Task<string> ValidateMissions(IExecutionContext executionContext)
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

        // Load Game Data configuration and missions (from LiveOps namespace)
        var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: true,
            CloudSaveProperties.Config.ToString(),
            CloudSaveProperties.DailyMissions.ToString(),
            CloudSaveProperties.WeeklyMissions.ToString());

        // Extract config file from Game Data
        var configData = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Config.ToString())
            ?.value?.ToObject<ConfigData>();

        // Check that config file exists
        if (configData is null or { missionConfig: null })
            throw new UGSException("No config data found.\n\n" + JsonConvert.SerializeObject(loadGameDataResponse, Formatting.Indented));

        // Get daily missions defined in Game Data
        var dailyGameDataMissions = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.DailyMissions.ToString())
            ?.value?.ToObject<GameMissionData[]>();

        // Get weekly missions defined in Game Data
        var weeklyGameDataMissions = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.WeeklyMissions.ToString())
            ?.value?.ToObject<GameMissionData[]>();

        // Validate that Game Data contains both types of missions
        if (dailyGameDataMissions is null or { Length: 0 } || weeklyGameDataMissions is null or { Length: 0 })
            throw new UGSException("No game missions found.");

        // Load player's current mission state (from Cloud Save)
        var loadPlayerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Missions.ToString());

        // Try to get the missions previously assigned to the player
        var playerDataMissions = loadPlayerDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Missions.ToString())
            ?.value?.ToObject<PlayerMissionData[]>();

        var now = DateTimeOffset.UtcNow;
        var dailyBonusMissionId = "mission_daily_bonus";

        // Prepare mission collections
        var validDailyMissions = new List<PlayerMissionData>();
        var validWeeklyMissions = new List<PlayerMissionData>();

        var requireNewDailyMissions = false;
        var requireNewWeeklyMissions = false;

        // Iterate through player missions and determine expiration
        if (playerDataMissions is not null and { Length: > 0 })
            foreach (var missionPlayerData in playerDataMissions)
            {
                // Skip if data is null
                if (missionPlayerData == null)
                {
                    _logger.LogInformation($"Skipping null mission data for player {playerId}");
                    continue;
                }

                // Try to match the player's mission with one in Game Data
                var missionGameData = missionPlayerData.isDaily
                    ? dailyGameDataMissions.FirstOrDefault(x => x?.id == missionPlayerData.id)
                    : weeklyGameDataMissions.FirstOrDefault(x => x?.id == missionPlayerData.id);

                // If mission doesn't exist in Game Data, skip it
                if (missionGameData is null)
                {
                    _logger.LogInformation($"Mission {missionPlayerData.id} not found in Game Data for player {playerId}");
                    continue;
                }

                // Check if the mission has expired based on UTC midnight logic
                var isExpired = IsMissionExpired(missionPlayerData, now);
                if (isExpired)
                {
                    if (missionPlayerData.isDaily)
                        requireNewDailyMissions = true;
                    else
                        requireNewWeeklyMissions = true;

                    _logger.LogInformation($"Mission {missionPlayerData.id} has expired for player {playerId}. Requiring new mission.");
                    continue;
                }

                // If mission is still valid, register it
                if (missionPlayerData.isDaily)
                    validDailyMissions.Add(missionPlayerData);
                else
                    validWeeklyMissions.Add(missionPlayerData);
            }

        // If no missions were found or all have expired, we need to generate new ones
        else
        {
            requireNewDailyMissions = true;
            requireNewWeeklyMissions = true;
            _logger.LogInformation($"No player missions found for player {playerId}. Generating new missions.");
        }

        // Replace all daily missions if any have expired
        if (requireNewDailyMissions)
        {
            var dailyPlayerMissions = playerDataMissions
                ?.Where(x => x is not null and { isDaily: true })
                ?.ToArray();

            validDailyMissions.Clear();
            validDailyMissions = GetNewMissions(
                isDaily: true,
                lastAssignedMissions: dailyPlayerMissions,
                gameDataMissions: dailyGameDataMissions,
                missionsCount: configData.missionConfig.dailyMissionsCount,
                now: now.ToUnixTimeSeconds(),
                dailyBonusMissionId); // Exclude the daily bonus mission from being reassigned because it is always added later

            _logger.LogInformation($"New daily missions assigned for player {playerId}. Count: {validDailyMissions.Count}");
        }

        // Replace all weekly missions if any have expired
        if (requireNewWeeklyMissions)
        {
            var weeklyPlayerMissions = playerDataMissions
                ?.Where(x => x is not null and { isDaily: false })
                ?.ToArray();

            validWeeklyMissions.Clear();
            validWeeklyMissions = GetNewMissions(
                isDaily: false,
                lastAssignedMissions: weeklyPlayerMissions,
                gameDataMissions: weeklyGameDataMissions,
                missionsCount: configData.missionConfig.weeklyMissionsCount,
                now: now.ToUnixTimeSeconds());

            _logger.LogInformation($"New weekly missions assigned for player {playerId}. Count: {validWeeklyMissions.Count}");
        }

        // Check if there is a bonus mission for daily missions. Else, create one.
        var bonusGameMission = dailyGameDataMissions.FirstOrDefault(x => x.id == dailyBonusMissionId);
        if (bonusGameMission is not null && !validDailyMissions.Any(x => x.isBonus))
        {
            var bonusMission = new PlayerMissionData(
                id: dailyBonusMissionId,
                isDaily: true,
                isBonus: true,
                startTime: now.ToUnixTimeSeconds());

            // Override locally (don't touch the GameData) the goal amount to the number of valid daily missions
            bonusGameMission.goalAmount = validDailyMissions.Count; 
            validDailyMissions.Add(bonusMission);
        }

        // Extract GameMissionData corresponding to valid player missions
        var filteredValidDailyGameMissions = dailyGameDataMissions
            ?.Where(x => x is not null && validDailyMissions.Any(y => y.id == x.id))
            ?.ToList();

        var filteredValidWeeklyGameMissions = weeklyGameDataMissions
            ?.Where(x => x is not null && validWeeklyMissions.Any(y => y.id == x.id))
            ?.ToList();

        // Ensure we have complete mission data before saving
        if (filteredValidDailyGameMissions is null or { Count: 0 } || filteredValidWeeklyGameMissions is null or { Count: 0 })
            throw new UGSException("Couldn't get the expected missions");

        // Save the updated mission state to Cloud Save
        var allPlayerMissions = validDailyMissions.Concat(validWeeklyMissions).ToArray();
        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
            new()
            {
                [CloudSaveProperties.Missions.ToString()] = JToken.FromObject(allPlayerMissions)
            });

        var missionResponse = new MissionResponse(
            filteredValidDailyGameMissions!,
            filteredValidWeeklyGameMissions!,
            validDailyMissions,
            validWeeklyMissions);

        // Initializes the missions server times
        missionResponse.InitializeServerTimes();

        // Return mission data to the client
        var missionResponseJson = JsonConvert.SerializeObject(missionResponse);

        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(missionResponseJson, derivedKey, derivedIv));
        return encryptedDataJson;
    }

    /// <summary>
    /// Select new missions from Game Data and assign them to the player.
    /// </summary>
    private List<PlayerMissionData> GetNewMissions(
        bool isDaily,
        IEnumerable<PlayerMissionData>? lastAssignedMissions,
        IEnumerable<GameMissionData?>? gameDataMissions,
        byte missionsCount,
        long now,
        params string[] missionesToExclude)
    {
        var newMissions = new List<PlayerMissionData>();
        for (var i = 0; i < missionsCount; i++)
        {
            var temp = lastAssignedMissions?.ToArray();

            // Filter available missions that have not already been selected.
            // If `temp` is null, it means no missions have been assigned yet; In this case, all missions are available.
            var possibleNewMissions = gameDataMissions
                ?.Where(x => 
                    x is not null && !missionesToExclude.Contains(x.id)
                    && (!temp?.Any(y => x.id == y.id) ?? true)
                    && (!newMissions?.Any(y => x.id == y.id) ?? true))
                ?.ToArray();

            // If there is not any mission avaiblable with the filter that avoid repeat mission, remove them
            if (possibleNewMissions is null or { Length: 0 })
            {
                possibleNewMissions = gameDataMissions
                   ?.Where(x =>
                       x is not null && !missionesToExclude.Contains(x.id)
                       && (!newMissions?.Any(y => x.id == y.id) ?? true))
                   ?.ToArray();

                if (possibleNewMissions is null or { Length: 0 })
                {
                    _logger.LogWarning($"There aren't availables missions to assign");
                    break;
                }
            }

            // Select a random mission from the remaining pool
            var newMission = possibleNewMissions[new Random().Next(0, possibleNewMissions.Length)];
            if (newMission is null)
                continue;

            // Create and assign the new mission to the player
            newMissions.Add(new PlayerMissionData(
                id: newMission.id,
                isDaily: isDaily,
                isBonus: false,
                startTime: now));
        }

        return newMissions;
    }

    /// <summary>
    /// Determine if the mission has expired based on its start time and UTC rules:
    /// - Daily: expires next day at 00:00 UTC
    /// - Weekly: expires next Monday at 00:00 UTC
    /// </summary>
    private static bool IsMissionExpired(PlayerMissionData playerMissionData, DateTimeOffset now)
    {
        var startTime = DateTimeOffset.FromUnixTimeSeconds(playerMissionData.startTime).UtcDateTime;

        if (playerMissionData.isDaily)
        {
            // Daily mission expires the next day at midnight UTC
            var nextMidnight = startTime.Date.AddDays(1);
            return now.UtcDateTime >= nextMidnight;
        } 
        
        else
        {
            // Weekly mission expires next Monday at 00:00 UTC
            var daysUntilNextMonday = ((int)DayOfWeek.Monday - (int)startTime.DayOfWeek + 7) % 7;
            var nextWeekStart = startTime.Date.AddDays(daysUntilNextMonday == 0 ? 7 : daysUntilNextMonday);
            return now.UtcDateTime >= nextWeekStart;
        }
    }
}
