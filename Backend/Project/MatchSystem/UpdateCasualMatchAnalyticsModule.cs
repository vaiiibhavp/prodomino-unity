using Backend.ClubSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.ProfileSystem
{
    public class UpdateCasualMatchAnalyticsModule(ILogger<UpdateCasualMatchAnalyticsModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<UpdateCasualMatchAnalyticsModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(UpdateCasualMatchAnalytics))]
        public async Task UpdateCasualMatchAnalytics(IExecutionContext executionContext, string parametersEncryptedJson)
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
            var isPlayerWinner = false;
            if (!data.TryGetValue("isPlayerWinner", out var isPlayerWinnerObj)
                || isPlayerWinnerObj is not bool _isPlayerWinner)
                _logger.LogWarning("Match winner is not specified or invalid. Defaulting to false.");
            else
                isPlayerWinner = _isPlayerWinner;

            var analyticsIDKey = CloudSaveProperties.Analytics.ToString();

            // Load player data from Cloud Save
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData
                (_gameApiClient, executionContext,
                customID: null,
                alternativePlayerID: null,
                isThrowingException: false,
                analyticsIDKey);

            // Try to get the player data analytics
            var playerDataAnalytics = playerDataResponse
                ?.FirstOrDefault(x => x?.key == analyticsIDKey.ToString())
                ?.value?.ToObject<AnalyticsData>()
                ?? new();

            // Update casual match statistics
            if (isPlayerWinner)
                playerDataAnalytics.casualVictoriesCount++;
            else
                playerDataAnalytics.casualDefeatsCount++;

            // Save updated streaks to Cloud Save
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
                new()
                {
                    [analyticsIDKey] = playerDataAnalytics
                });
        }
    }
}
