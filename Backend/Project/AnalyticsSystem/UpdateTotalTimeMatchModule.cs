using Backend.ProfileSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.AnalyticsSystem;

public class UpdateTotalTimeMatchModule(ILogger<UpdateTotalTimeMatchModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<UpdateTotalTimeMatchModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Update the days streak in analytics data
    /// </summary>
    [CloudCodeFunction(nameof(UpdateTotalTimeMatch))]
    public async Task<string> UpdateTotalTimeMatch(IExecutionContext executionContext, string parametersEncryptedJson)
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
        var matchTimeTicks = default(long);
        if (!data.TryGetValue("matchTimeTicks", out var matchTimeObj)
            || matchTimeObj is not long _matchTimeTicks)
            _logger.LogWarning("Match time is not specified or invalid.");
        else
            matchTimeTicks = _matchTimeTicks;

        // Load current player Firebase data
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            null, null, false,
            CloudSaveProperties.Analytics.ToString());

        var analyticsData = playerDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Analytics.ToString())?.value
            ?.ToObject<AnalyticsData>() 
            ?? new();

        analyticsData.totalMatchTimeTicks += matchTimeTicks;

        // Save updated streaks to Cloud Save
        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
            new()
            {
                [CloudSaveProperties.Analytics.ToString()] = analyticsData
            });

        // Derived key/iv for encrypting response
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Build response
        var analyticsDataResponseJson = JsonConvert.SerializeObject(new AnalyticsDataResponse(analyticsData));
        var encryptedResponse = SecurityHelper.EncryptData(analyticsDataResponseJson, derivedKey, derivedIv);
        return JsonConvert.SerializeObject(encryptedResponse);
    }
}
