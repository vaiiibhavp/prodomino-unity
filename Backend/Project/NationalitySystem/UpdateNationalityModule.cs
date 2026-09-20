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

namespace Backend.NationalitySystem;

/// <summary>
/// Handles updating a player's nationality, enforcing cooldown periods, updating player data, and synchronizing
/// leaderboard entries.
/// </summary>
/// <param name="logger">Logger used for recording diagnostic and error information.</param>
/// <param name="gameApiClient">Client for interacting with the game API.</param>
public class UpdateNationalityModule(ILogger<DeleteAccountModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<DeleteAccountModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    /// <summary>
    /// Updates the player's nationality, enforces cooldown restrictions, and synchronizes changes with leaderboard
    /// data.
    /// </summary>
    /// <param name="executionContext">The execution context containing player and session information.</param>
    /// <param name="parametersEncryptedJson">Encrypted JSON string containing the new nationality and other parameters.</param>
    /// <returns>An encrypted JSON response indicating the result of the nationality update.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the execution context is null.</exception>
    /// <exception cref="Exception">Thrown if the player ID is invalid or null.</exception>
    /// <exception cref="ArgumentException">Thrown if the 'newNationality' parameter is missing or invalid.</exception>
    /// <exception cref="UGSException">Thrown if the game data configuration is invalid.</exception>
    [CloudCodeFunction(nameof(UpdateNationalityAsync))]
    public async Task<string> UpdateNationalityAsync(IExecutionContext executionContext, string parametersEncryptedJson)
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

        // Decrypt and parse parameters
        var data = BackendHelper.ValidateEncriptedParameters(
            parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        // Validate required parameters
        if (!data.TryGetValue("newNationality", out var newNationalityToken)
            || !Enum.TryParse<NationalityType>(newNationalityToken.ToString(), out var newNationalityType))
            throw new ArgumentException("Missing or invalid 'newNationality'");

        // Load Game Data
        var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(
            _gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: false,
            CloudSaveProperties.Config.ToString());

        // Get the Game Data configuration from the response
        var configData = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Config.ToString())
            ?.value?.ToObject<ConfigData>();

        // Check if the Game Data configuration is valid
        if (configData is null or { nationalityConfig: null })
            throw new UGSException("Game Data configuration is invalid.");

        // Get the player's username to be used in the leaderboard entry
        var playerUsername = await UGSApiHelper.GetUsername(executionContext, _logger: _logger).ConfigureAwait(false);

        // Key used to get/set the player nationality in Cloud Save
        var nationalityKey = CloudSaveProperties.Nationality.ToString();

        // Load current player data
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            nationalityKey);

        // Parse player data with safe defaults
        var playerNationalityData = playerDataResponse?.FirstOrDefault(x => x?.key == nationalityKey)?.value?.ToObject<NationalityData>() ?? new NationalityData();

        // Prepare response object
        var updateNationalityResponse = new UpdateNationalityResponse();

        // Get the date when the player will be eligible
        var availableDateForDeletion = playerNationalityData.updateAtUtc != null
            ? DateTime.Parse(playerNationalityData.updateAtUtc)
            : DateTime.UtcNow; // If updateAtUtc is missing, treat it as if the account was just created

        // Add the configured cooldown period to the account creation date
        availableDateForDeletion = availableDateForDeletion.AddSeconds(configData.nationalityConfig.secondsToAbilityToUpdateNationality);

        // Prevent deletion of very recently created accounts.
        // This protects against abuse, bot churn, and accidental deletions.
        if ((DateTime.UtcNow - availableDateForDeletion).TotalHours <= 0)
        {
            var leftingHours = (availableDateForDeletion - DateTime.UtcNow).TotalHours;
            updateNationalityResponse.message = $"You must wait {leftingHours:F2} hours before you can update your nationality again.";

            return EncryptResponse(executionContext, updateNationalityResponse);
        }

        // Before updating the nationality in the player data, save the old nationality
        var oldNationalityType = playerNationalityData.nationalityType;

        // First, update nationality in the player data
        playerNationalityData.nationalityType = newNationalityType;

        // Then, update the timestamp for when the player can next update their
        playerNationalityData.updateAtUtc = DateTime.UtcNow.ToString("o"); // Use ISO 8601 format for consistency

        // Save the updated analytics data
        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, new Dictionary<string, object>
        {
            [nationalityKey] = playerNationalityData
        }).ConfigureAwait(false);

        // Once the player data is successfully updated, we can proceed to update the leaderboard entries. This ensures that we don't have a mismatch between the player's
        updateNationalityResponse.newNationality = playerNationalityData;

        return await UpdateLeaderboardsInRTDB().ConfigureAwait(false);

        /// Update the player's nationality in each of the relevant leaderboard entries in Firebase Realtime Database. 
        /// This ensures that the player's new nationality is reflected in the leaderboards, which may be used for filtering or display purposes.
        async Task<string> UpdateLeaderboardsInRTDB()
        { 
            // Call firebase real-time database to get the leaderboard IDs for the game. This is needed to update the player's
            var leaderboardsIds = await FirebaseApiHelper.GetLeaderboardIdsAsync(executionContext, _gameApiClient, _logger);

            _logger.LogInformation("Retrieved the following leaderboard IDs: {LeaderboardIds}", string.Join(", ", leaderboardsIds));

            // Get the current leaderboard data for the player. This is needed to update the player's
            var currentLeaderboardsDataCollection = await FirebaseApiHelper.GetPlayerLeaderboardDataAsync
                (executionContext, _gameApiClient,
                nationality: oldNationalityType, 
                unityUserId: playerId, 
                leaderboardsIds: leaderboardsIds, 
                logger: _logger);

            // If there is no existing leaderboard data, we just return the response with the updated
            if (currentLeaderboardsDataCollection.Count is 0)
            {
                _logger.LogWarning("No existing leaderboard data found for player {PlayerId}.", playerId);
                return EncryptResponse(executionContext, updateNationalityResponse);
            }

            // Prepare multi-location update data for Firebase. We need to remove the player's
            var multiLocationUpdateData = new Dictionary<string, object?>();

            // For each leaderboard entry, we need to remove the old entry under
            foreach (var kvp in currentLeaderboardsDataCollection)
            {
                var leaderboardId = kvp.Key;
                var entry = kvp.Value;

                // Remove from old nationality
                multiLocationUpdateData[$"{FirebaseApiHelper.leaderboardsCollectionIdKey}/{leaderboardId}/{oldNationalityType}/{playerId}"] = null;

                // Recreate under new nationality
                multiLocationUpdateData[$"{FirebaseApiHelper.leaderboardsCollectionIdKey}/{leaderboardId}/{newNationalityType}/{playerId}"] = new
                {
                    userId = entry.userId,
                    score = entry.score,
                    username = entry.username,
                    updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
            }

            _logger.LogInformation("Prepared multi-location update data for Firebase: {MultiLocationUpdateData}", JsonConvert.SerializeObject(multiLocationUpdateData, Formatting.Indented));

            // Perform a multi-location update in Firebase to atomically update all leaderboard entries with the new
            await FirebaseApiHelper.PatchMultiLocationAsync(
                executionContext: executionContext,
                gameApiClient: _gameApiClient,
                updates: multiLocationUpdateData,
                logger: _logger);
            
            _logger.LogInformation("Successfully updated leaderboard entries for player {PlayerId} with new nationality {NewNationalityType}.", playerId, newNationalityType);

            // If we successfully update the leaderboard entries, we can return the response with the updated nationality
            return EncryptResponse(executionContext, updateNationalityResponse);
        }
    }

    /// <summary>
    /// Encrypts the response payload using a key derived from the player's identity.
    /// This prevents response tampering and replay attacks on the client.
    /// </summary>
    private static string EncryptResponse(
        IExecutionContext executionContext,
        UpdateNationalityResponse response)
    {
        // Derived key/iv for encrypting response
        var playerId = executionContext.PlayerId ?? string.Empty;
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Build response
        var responseJson = JsonConvert.SerializeObject(response);
        var encryptedResponse = SecurityHelper.EncryptData(responseJson, derivedKey, derivedIv);
        return JsonConvert.SerializeObject(encryptedResponse);
    }
}
