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

namespace Backend.AchievementsSystem;

public class AchievementValidatorModule(ILogger<AchievementValidatorModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<AchievementValidatorModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    private bool wasUpdated = false; // Flag to track if achievements were updated

    [CloudCodeFunction(nameof(ValidateAchievements))]
    public async Task<string> ValidateAchievements(IExecutionContext executionContext)
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

        // Load player's current achievement state (from Cloud Save)
        var loadPlayerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Achievements.ToString(),
            CloudSaveProperties.Analytics.ToString());

        // Try to get the achievements that player has completed
        var playerDataAchievements = loadPlayerDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Achievements.ToString())
            ?.value?.ToObject<PlayerAchievementData[]>()
            ?.ToList()
            ?? [];

        // Try to get the current player analytics data from the response
        var analyticsData = loadPlayerDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Achievements.ToString())
            ?.value?.ToObject<AnalyticsData>()
            ?? new();

        // Create a list to hold tasks for validating achievements
        var achievementValidation = new List<Task>()
        {
            UGSApiHelper.TryToCompleteTierAchievement(executionContext, _logger, playerDataAchievements)
        };

        // Wait until all achievement validation tasks are completed
        await Task.WhenAll(achievementValidation);

        // If any of the tasks updated achievements, save the updated achievements back to Cloud Save
        if (wasUpdated)
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
                new()
                {
                    [CloudSaveProperties.Achievements.ToString()] = JToken.FromObject(playerDataAchievements)
                });

        // Once all tasks are completed, create the response object
        var achievementResponse = new AchievementResponse(playerDataAchievements, analyticsData);

        // Return the response with the updated achievements and game data
        var responseDataJson = JsonConvert.SerializeObject(achievementResponse);

        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(responseDataJson, derivedKey, derivedIv));
        return encryptedDataJson;
    }
}
