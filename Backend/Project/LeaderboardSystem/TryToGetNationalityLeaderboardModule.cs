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
using static HelperSharedLibrary.Enums;

namespace Backend.LeaderboardSystem;

public class TryToGetNationalityLeaderboardModule(ILogger<TryToGetNationalityLeaderboardModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<TryToGetNationalityLeaderboardModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(TryToGetNationalityLeaderboard))]
    public async Task<string> TryToGetNationalityLeaderboard(IExecutionContext executionContext, string parametersEncryptedJson)
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
        if (!data.TryGetValue("leaderboardIds", out var leaderboardIdsToken) 
            || leaderboardIdsToken is not JToken leaderboardsToken
            || leaderboardsToken.ToObject<string[]>() is not string[] leaderboardIds)
            throw new ArgumentException("Missing or invalid 'leaderboardIds'");

        // Check if the data dictionary contains the required keys and values
        var nationalityType = NationalityType.International;
        if (!data.TryGetValue("nationalityType", out var nationalityTypeObj)
            || nationalityTypeObj is null
            || nationalityTypeObj.ToString() is not string _nationalityTypeStr
            || string.IsNullOrEmpty(_nationalityTypeStr)
            || !Enum.TryParse(nationalityTypeObj.ToString(), out NationalityType _nationalityType)
            || _nationalityType is NationalityType.International)
            _logger.LogWarning("Cosmetic type is not specified or invalid");
        else
            nationalityType = _nationalityType;

        var configKey = CloudSaveProperties.Config.ToString();

        // Load current player Firebase data
        var configData = (await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
            customID, null, false,
            CloudSaveProperties.Config.ToString()))
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Ads.ToString())?.value?.ToObject<ConfigData>();

        // Get the max players to return for the leaderboard from config, default to 20 if not set
        var limit = configData?.leaderboardConfig?.maxPlayers ?? 20;

        // No specific parameters needed for leaderboard (optional filters could be added later)
        var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
        var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
        var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

        // Load player data (Firebase credentials)
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            firebaseIDKey,
            firebaseIDTokenKey,
            firebaseRefreshTokenKey);

        // Extract Firebase credentials
        var firebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
        var firebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
        var firebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

        // Create the response object
        var leaderboardResponse = new RTDBPlayerLeaderboardDataResponse();

        if (string.IsNullOrEmpty(firebaseID) ||
            string.IsNullOrEmpty(firebaseIDToken) ||
            string.IsNullOrEmpty(firebaseRefreshToken))
        {
            _logger.LogWarning("Missing or invalid Firebase user credentials.");
            leaderboardResponse.message = "Missing or invalid user credentials. Please log in again.";
        } 
        else
        {
            _logger.LogInformation($"User {firebaseID} is requesting the top clubs leaderboard.");

            // Call Firestore API to get top 10 clubs ordered by score
            var topNationalityLeaderboard = await FirebaseApiHelper.GetMultipleLeaderboardsAsync(
                executionContext: executionContext,
                gameApiClient: _gameApiClient,
                leaderboardIds: leaderboardIds,
                nationalityType: nationalityType,
                limit: limit,
                logger: _logger);

            if (topNationalityLeaderboard is null or { Count: 0 })
            {
                _logger.LogWarning("No nationality leaderboard found.");
                leaderboardResponse.message = $"No leaderboard of nationality {nationalityType} found.";
            } 
            else
            {
                _logger.LogInformation($"Successfully retrieved top {topNationalityLeaderboard.Count} nationality ranking.");
                leaderboardResponse.rankingByLeaderboard = topNationalityLeaderboard;
            }
        }

        // Encrypt and return response
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        var responseJson = JsonConvert.SerializeObject(leaderboardResponse);
        var encryptedResponse = SecurityHelper.EncryptData(responseJson, derivedKey, derivedIv);

        return JsonConvert.SerializeObject(encryptedResponse);
    }
}
