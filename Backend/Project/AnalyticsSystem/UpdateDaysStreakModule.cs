using Backend.AuthenticationSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.AnalyticsSystem;

public class UpdateDaysStreakModule(ILogger<UpdateDaysStreakModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<UpdateDaysStreakModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Update the days streak in analytics data
    /// </summary>
    [CloudCodeFunction(nameof(UpdateDaysStreak))]
    public async Task UpdateDaysStreak(IExecutionContext executionContext)
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

        // Load current player Firebase data
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
            null, null, false,
            CloudSaveProperties.Analytics.ToString());

        await TryToUpdateDaysStreak(executionContext, playerDataResponse);
    }

    /// <summary>
    /// Try to update the days streak in analytics data
    /// </summary>
    private async Task TryToUpdateDaysStreak(IExecutionContext executionContext, 
        ResponseData?[]? loadDataResponse)
    {
        var analytics = loadDataResponse?.FirstOrDefault(x => x?.key == CloudSaveProperties.Analytics.ToString())?.value?.ToObject<AnalyticsData>();
        if (analytics is null)
        {
            _logger.LogWarning("Failed to load analytics data.");
            return;
        }

        try
        {
            var analyticsData = UGSApiHelper.UpdateDaysStreak(analytics);
            if (analyticsData is null)
            {
                _logger.LogWarning("Failed to update days streak. Response is null.");
                return;
            }

            // Load current player Firebase data
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, new()
            { 
                [CloudSaveProperties.Analytics.ToString()] = analyticsData
            });

            _logger.LogInformation("Days streak updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update days streak. Exception: {ex.Message}");
        }
    }
}
