using Backend.LeaderboardSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.MissionSystem;

public class ClaimMissionRewardModule(ILogger<ClaimMissionRewardModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<ClaimMissionRewardModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(ClaimMissionReward))]
    public async Task<string?> ClaimMissionReward(IExecutionContext executionContext, string parametersEncryptedJson)
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
            throw new ArgumentException("Invalid input data. Provide player mission");

        // Check if the data dictionary contains the required keys and values
        if (!data.TryGetValue("completedPlayerMissionID", out var completedPlayerMissionIDObj) || completedPlayerMissionIDObj is not string completedPlayerMissionID || string.IsNullOrEmpty(completedPlayerMissionID))
            throw new ArgumentException("Completed mission is not specified");

        var analiticsKeyID = CloudSaveProperties.Analytics.ToString();

        // Load current player missions data from Cloud Save
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Missions.ToString(),
            analiticsKeyID);

        // Get the current player analytics data from the response
        var analyticsData = playerDataResponse
            ?.FirstOrDefault(x => x?.key == analiticsKeyID)
            ?.value?.ToObject<AnalyticsData>()
            ?? new();

        // Get the current player missions data from the response
        var currentMissions = playerDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Missions.ToString())
            ?.value?.ToObject<PlayerMissionData[]>();

        // Check if the player missions data is valid
        if (currentMissions is null || currentMissions.Length == 0)
            throw new UGSException("No player missions found.");

        // Search for the mission by ID
        var missionToClaim = currentMissions.FirstOrDefault(x => x.id == completedPlayerMissionID);
        if (missionToClaim is null)
            throw new UGSException("Mission not found in player data.");

        // Verify that the mission is completed and not already claimed
        if (!missionToClaim.completed || missionToClaim.claimed)
            throw new UGSException("Mission is either not completed or already claimed.");

        // Load Game Data configuration and missions (from LiveOps namespace)
        var missionKeyID = missionToClaim.isDaily
            ? CloudSaveProperties.DailyMissions.ToString()
            : CloudSaveProperties.WeeklyMissions.ToString();

        var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: false,
            missionKeyID,
            CloudSaveProperties.Config.ToString());

        // Get the Game Data configuration from the response
        var configData = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Config.ToString())
            ?.value?.ToObject<ConfigData>();

        // Check if the Game Data configuration is valid
        if (configData is null or { missionConfig: null } || configData.missionConfig.dailyMissionBonusCurrency == Currency.None || configData.missionConfig.dailyMissionBonusAmout <= 0)
            throw new UGSException("Game Data configuration is invalid.");

        // Get daily missions defined in Game Data
        var gameDataMission = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == missionKeyID)
            ?.value?.ToObject<GameMissionData[]>()
            ?.FirstOrDefault(x => x != null && x.id == completedPlayerMissionID);

        // Check if the Game Data mission definition is valid
        if (gameDataMission is null || gameDataMission.rewardCurrency == Currency.None || gameDataMission.rewardAmount <= 0)
            throw new UGSException("Mission definition or reward is invalid.");

        // Initialize some variables for the response
        var newCurrencyAmount = default(uint);
        var isRetrievingDailyBonus = false;
        var dataToSave = new Dictionary<string, object>();

        // Get the mission configuration data and check if it's a bonus mission
        var missionConfig = configData?.missionConfig;
        var isBonusMission = missionToClaim.isBonus;

        // If the player has completed all missions, update the analytics data to reclaim the daily bonus
        if (isBonusMission && currentMissions.Where(x => x.isDaily).All(x => x.claimed))
        {
            var bonusDatesReclaimed = analyticsData?.dailyBonusReclaimedDates
                ?.Select(x => DateTimeOffset.FromUnixTimeSeconds(x).UtcDateTime.Date)
                ?.ToList() ?? [];

            // Check if the bonus dates have been reclaimed or if the player has no bonus dates
            var today = DateTime.UtcNow.Date;
            if (!bonusDatesReclaimed.Contains(today))
            {
                _logger.LogInformation("All missions completed for player {PlayerId}.", playerId);
                isRetrievingDailyBonus = true;

                // Grant the daily bonus currency to the player
                newCurrencyAmount = await UGSApiHelper.GrantCurrencyAsync(_gameApiClient, executionContext,
                    missionConfig!.dailyMissionBonusCurrency, missionConfig!.dailyMissionBonusAmout);
                bonusDatesReclaimed.Add(today);

                // Convert the reclaimed bonus dates to Unix timestamps
                var newBonusReclaimedDate = bonusDatesReclaimed?.Select(x => ((DateTimeOffset)x).ToUnixTimeSeconds())?.ToArray();
                if (newBonusReclaimedDate is not null and { Length: > 0 })
                {
                    // Get the existing daily bonus reclaimed dates or initialize an empty array if not present and add the new dates
                    var updatedDailyBonusReclaimedDates = (analyticsData is not null and { dailyBonusReclaimedDates: not null } 
                        ? analyticsData.dailyBonusReclaimedDates 
                        : [])
                        .Concat(newBonusReclaimedDate)
                        .ToArray();

                    // Update the analytics data with the new reclaimed dates
                    analyticsData!.dailyBonusReclaimedDates = updatedDailyBonusReclaimedDates;

                    // Register the bonus date as reclaimed
                    dataToSave.Add(analiticsKeyID, analyticsData);
                }
            }
        }

        // Else, confirm that the mission progress is sufficient to claim the reward
        else if (missionToClaim.progress < gameDataMission.goalAmount)
            throw new UGSException("Mission progress is insufficient to claim reward.");

        // Update the mission to mark it as claimed
        missionToClaim.claimed = true;
        missionToClaim.reclaimedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Grant the currency to the player
        newCurrencyAmount = await UGSApiHelper.GrantCurrencyAsync(_gameApiClient, executionContext,
            gameDataMission.rewardCurrency, gameDataMission.rewardAmount);

        // Save the updated player missions data back to Cloud Save
        dataToSave.Add(CloudSaveProperties.Missions.ToString(), currentMissions.Select(x => x.Clone()).ToArray());

        // Update the analytics data to increment the missions claimed count
        if (dataToSave.TryGetValue(analiticsKeyID, out var entry) && entry is AnalyticsData _analyticsData)
            _analyticsData.missionsClaimedCount++;

        // Else, if the analytics data is not present yet in the collection, register a new one
        else
        { 
            analyticsData!.missionsClaimedCount++;
            dataToSave.Add(CloudSaveProperties.Analytics.ToString(), analyticsData);
        }

        // Save the updated data to Cloud Save
        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, dataToSave);

        // Return whether the player is receiving a daily bonus
        if (missionConfig is null)
            return null;

        // Serialize the mission configuration data to JSON
        else
        {
            var claimRewardResponse = new MissionClaimRewardResponse(
                missionConfigData: missionConfig,
                isRetrievingDailyBonus: isRetrievingDailyBonus,
                newCurrencyAmount: newCurrencyAmount);

            var claimRewardDataJson = JsonConvert.SerializeObject(claimRewardResponse);
            var derivedKey = SecurityHelper.DeriveKey
                (executionContext?.PlayerId ?? string.Empty,
                executionContext?.AccessToken ?? string.Empty);

            var securityDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(claimRewardDataJson, derivedKey));
            return securityDataJson;
        }
    }
}
