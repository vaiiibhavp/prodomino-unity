using Backend.ShopSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.AdSystem;

public class TryToDisableAdsTemporarilyModule(ILogger<TryToDisableAdsTemporarilyModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<TryToDisableAdsTemporarilyModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Try to update the monthly subscription
    /// </summary>
    [CloudCodeFunction(nameof(TryToDisableAdsTemporarily))]
    public async Task TryToDisableAdsTemporarily(IExecutionContext executionContext)
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
        var configData = (await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
            customID, null, false,
            CloudSaveProperties.Config.ToString()))
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Ads.ToString())?.value?.ToObject<ConfigData>();

        // Load current player Firebase data
        var playerAdData = (await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
            null, null, false,
            CloudSaveProperties.Ads.ToString()))
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Ads.ToString())?.value?.ToObject<PlayerAdData>();

        await TryToDisableAdsTemporarily(executionContext, playerAdData, configData);
    }

    /// <summary>
    /// Try to buy the monthly subscription
    /// </summary>
    private async Task TryToDisableAdsTemporarily(IExecutionContext executionContext,
        PlayerAdData? playerAdData, ConfigData? configData)
    {
        if (playerAdData is null || configData is null)
        {
            _logger.LogWarning("Failed to load ads data.");
            return;
        }

        try
        {
            var secondsSinceLastAdDisabled = (DateTime.UtcNow - playerAdData.lastTimeAdDisabled)?.TotalSeconds ?? double.MaxValue;
            var targetSecondsDisabled = configData.adsConfig?.secondsDisabled ?? 1200;

            // Check if ads are temporarily disabled for the player
            if (secondsSinceLastAdDisabled <= targetSecondsDisabled)
            {
                _logger.LogInformation($"Ads are already temporarily disabled for the player. Wait for {targetSecondsDisabled - secondsSinceLastAdDisabled} seconds before disabling them again.");
                return;
            }

            playerAdData.lastTimeAdDisabled = DateTime.UtcNow;

            // Save the updated analytics data
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, new Dictionary<string, object>
            {
                [CloudSaveProperties.Ads.ToString()] = playerAdData
            }).ConfigureAwait(false);

            _logger.LogInformation("Ads temporarily disabled successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to disable ads temporarily. Exception: {ex.Message}");
        }
    }
}
