using Backend.AdSystem;
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

namespace Backend.LeaderboardSystem;

public class GetLeaderboardEntriesAchievementsModule(ILogger<GetLeaderboardEntriesAchievementsModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<GetLeaderboardEntriesAchievementsModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(GetLeaderboardEntriesAchievements))]
    public async Task<string> GetLeaderboardEntriesAchievements(IExecutionContext executionContext, string parametersEncryptedJson)
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
        var data = BackendHelper.ValidateEncriptedParameters(
            parametersEncryptedJson,
            executionContext.PlayerId,
            executionContext.AccessToken);

        // Validate entry data
        if (data is null || data.Count == 0)
            throw new ArgumentException("Invalid input data. Provide provider id");

        // Check if the data dictionary contains the required keys and values
        if (!data.TryGetValue("leaderboardIds", out var leaderboardIdsObject)
            || leaderboardIdsObject is not JToken leaderboardIdsToken
            || leaderboardIdsToken.ToObject<string[]>() is not string[] leaderboardIds)
            throw new ArgumentException("Missing or invalid leaderboardIds.");

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
        if (configData is null or { leaderboardConfig: null or { maxPlayers: 0 } })
            throw new UGSException("Game Data configuration is invalid.");

        // Get the leaderboard configuration
        var maxPlayers = configData.leaderboardConfig.maxPlayers;
        var achievementsToShow = configData.leaderboardConfig.achievementsToShow;

        // Dictionary to hold player achievement data
        var playersAchievementData = new Dictionary<string, (string? profileIconId, LeaderboardAchievementData? leaderboardAchievementData, string? error)>();
        
        // Iterate for each leaderboard ID and update player achievement for each player
        var globalResults = new List<PlayerAchievementResult>();

        // Register tasks to modify player scores based on match results and return results once all tasks are completed
        var tasks = new List<Task>();

        // Iterate for each leaderboard and register the global result for the player avoiding duplicates
        var playerLeaderboardCollection = new Dictionary<string, List<string>>();
        foreach (var leaderboardId in leaderboardIds)
            tasks.Add(ConfigureLeaderboardAchievementData(leaderboardId));

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);

        // Once all leaderboards are processed, get the achievement data for each player
        if (playerLeaderboardCollection is not null and { Count: > 0 })
        {
            // Clear tasks list to reuse it
            tasks.Clear();


            // Iterate for each player to get his achievement data
            foreach (var kvp in playerLeaderboardCollection)
                tasks.Add(GetPlayerAchievementData(kvp.Key));

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }

        // Once all player achievement data is retrieved, generate the global results
        if (playersAchievementData is not null and { Count: > 0 })
        {
            // Iterate for each player and leaderboard to generate the global results
            foreach (var kvp in playerLeaderboardCollection!)
            {
                var currentPlayerId = kvp.Key;
                var leaderboardIdsForPlayer = kvp.Value;

                if (playersAchievementData.TryGetValue(currentPlayerId, out var achievementDataTuple))
                {
                    var (playerProfileData, leaderboardAchievementData, errorMessage) = achievementDataTuple;
                    foreach (var leaderboardId in leaderboardIdsForPlayer)
                        GeneratePlayerAchievementData(
                            currentPlayerId,
                            leaderboardId,
                            playerProfileData,
                            leaderboardAchievementData,
                            errorMessage);
                } 
                
                // If no achievement data found for the player, log a warning
                else
                    _logger.LogWarning("No achievement data found for player {PlayerId}.", currentPlayerId);
            }
        }

        // Create the response object with the global results and serialize it to JSON
        var leaderboardUpdateResponse = new GetLeaderboardEntriesAchievementsResponse(globalResults);
        var leaderboardUpdateResponseJson = JsonConvert.SerializeObject(leaderboardUpdateResponse);

        // Get the data use to encrypt the response
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Encrypt the response data and return it as a JSON string
        var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(leaderboardUpdateResponseJson, derivedKey, derivedIv));
        return encryptedDataJson;

        async Task ConfigureLeaderboardAchievementData(string leaderboardId)
        {
            _logger.LogInformation("Processing leaderboard {LeaderboardId} with max players {MaxPlayers}.", leaderboardId, maxPlayers);

            var leaderboardEntries = new List<LeaderboardData>();

            // Get the leaderboard scores for the given leaderboard ID
            try
            {
                leaderboardEntries = await UGSApiHelper.GetLeaderboardScores(executionContext, leaderboardId, maxPlayers);
                if (leaderboardEntries is null or { Count: 0 })
                {
                    _logger.LogWarning("No entries found for leaderboard {LeaderboardId}.", leaderboardId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process leaderboard {LeaderboardId}.", leaderboardId);
                return;
            }

            // Iterarate foreach entry and update leaderboard entry
            if (leaderboardEntries is not null and { Count: > 0 })
                foreach (var entry in leaderboardEntries)
                {
                    var currentPlayerId = entry.playerId;
                    if (string.IsNullOrEmpty(currentPlayerId))
                    {
                        _logger.LogWarning("Player ID is null or empty for leaderboard {LeaderboardId}.", leaderboardId);
                        continue;
                    }

                    lock(playerLeaderboardCollection) 
                    {
                        // Initialize player leaderboard collection if not exists
                        if (!playerLeaderboardCollection.ContainsKey(currentPlayerId))
                            playerLeaderboardCollection.Add(currentPlayerId, []);

                        if (!playerLeaderboardCollection[currentPlayerId].Contains(leaderboardId))
                            playerLeaderboardCollection[currentPlayerId].Add(leaderboardId);
                        else
                            _logger.LogWarning("Player {PlayerId} already processed for leaderboard {LeaderboardId}, skipping duplicate.", currentPlayerId, leaderboardId);
                    }
                }
        }

        // Local function to get player achievement data and store it in the dictionary
        async Task GetPlayerAchievementData(string playerId)
        {
            var profileData = default(PlayerProfileData?);
            var analyticsData = default(AnalyticsData?);
            var leaderboardAchievementData = default(LeaderboardAchievementData?);

            var errorMessage = string.Empty;

            var profileKey = CloudSaveProperties.Profile.ToString();
            var analyticsKey = CloudSaveProperties.Analytics.ToString();

            try
            {
                var playerData = await UGSApiHelper.ProtectedLoadData(
                    _gameApiClient, executionContext,
                    customID: null,
                    alternativePlayerID: playerId,
                    isThrowingException: false,
                    profileKey,
                    analyticsKey);

                // Get the player profile data
                profileData = playerData
                    ?.FirstOrDefault(x => x?.key == profileKey)
                    ?.value
                    ?.ToObject<PlayerProfileData>();

                // Get the player achievement data
                analyticsData = playerData
                    ?.FirstOrDefault(x => x?.key == analyticsKey)
                    ?.value
                    ?.ToObject<AnalyticsData>();

                if (profileData is null)
                    _logger.LogWarning("Profile data is null for player {PlayerId}.", playerId);
                else if (string.IsNullOrEmpty(profileData.profileIconID))
                    _logger.LogWarning("Profile icon ID is null or empty for player {PlayerId}.", playerId);

                if (analyticsData is null)
                    _logger.LogWarning("Analytics data is null for player {PlayerId}.", playerId);

                leaderboardAchievementData = new
                    (DateTime.UtcNow,
                    profileData?.badgesIDs ?? [],
                    (uint)Math.Clamp(analyticsData?.achievementsClaimedCount ?? 0, 0, uint.MaxValue)); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate leardeboard achievement data for player {PlayerId}.\n\n{Exception}", playerId, ex.Message);
                errorMessage = ex.Message;
            }

            lock (playersAchievementData)
            { 
                // Try to add the player achievement data to the dictionary
                if (!playersAchievementData.TryAdd(playerId, (profileData?.profileIconID, leaderboardAchievementData, errorMessage)))
                    _logger.LogWarning("Player achievement data for player {PlayerId} already exists, skipping duplicate.", playerId);
            }
        }

        // Local function to generate player achievement result and add it to the global results
        void GeneratePlayerAchievementData(string playerId, string leaderboardId,
            string? profileIconId, LeaderboardAchievementData? leaderboardAchievementData, string? errorMessage)
        {
            // Check if playerId is null or empty
            if (string.IsNullOrEmpty(playerId))
            {
                _logger.LogWarning("Player ID is null or empty for leaderboard {LeaderboardId}.", leaderboardId);
                return;
            }

            // Add the player achievement result to the global results
            globalResults.Add(new PlayerAchievementResult
                (playerId: playerId,
                leaderboardId: leaderboardId,
                profileIconId: profileIconId ?? string.Empty,
                achievementData: leaderboardAchievementData,
                updated: leaderboardAchievementData is not null,
                error: errorMessage ?? string.Empty));
        }
    }
}
