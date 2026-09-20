using Backend.IAPSystem;
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
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;
using static HelperSharedLibrary.PlayerLeaderboardData;

namespace Backend.LeaderboardSystem;

public class GetLeaderboardEntriesPlayerDataModule(ILogger<GetLeaderboardEntriesPlayerDataModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<GetLeaderboardEntriesPlayerDataModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(GetLeaderboardEntriesPlayerData))]
    public async Task<string> GetLeaderboardEntriesPlayerData(IExecutionContext executionContext, string parametersEncryptedJson)
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

        // Initialize optional values as null
        var optionalNationality = default(NationalityType?);
        var optionalLeaderboards = default(Dictionary<string, List<RTDBPlayerLeaderboardData>>?);

        // Try to get optionalLeaderboardData from payload
        if (data.TryGetValue("optionalLeaderboardData", out var optionalObj)
            && optionalObj is JToken optionalToken
            && optionalToken.Type == JTokenType.Object)
        {
            // Validate nationality exists and is valid enum value
            if (optionalToken["nationality"] is JToken nationalityToken
                && !Enum.TryParse<NationalityType>(nationalityToken.ToString(), out var newNationalityType))
                optionalNationality = newNationalityType;

            // Validate leaderboard exists and is array
            if (optionalToken["topByLeaderboards"] is JToken leaderboardToken
                && leaderboardToken.ToObject<Dictionary<string, List<RTDBPlayerLeaderboardData>>>() is Dictionary<string, List<RTDBPlayerLeaderboardData>> topByLeaderboardData)
                optionalLeaderboards = topByLeaderboardData;
        }

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

        // Tiers data should be ordered by scoreStartsAt in ascending order to get the correct tier with LastOrDefault
        configData.leaderboardConfig.tiersData = [.. configData.leaderboardConfig.tiersData.OrderBy(t => t.scoreStartsAt)];

        // Get the leaderboard configuration
        var maxPlayers = configData.leaderboardConfig.maxPlayers;

        // Dictionary to hold player data
        var playersLeaderboardTupleData = new Dictionary<string, (PlayerLeaderboardData? leaderboardPlayerData, string? errorMessage)>();
        
        // Iterate for each leaderboard ID and update player  for each player
        var globalResults = new List<PlayerLeaderboardDataResult>();

        // Register tasks to modify player scores based on match results and return results once all tasks are completed
        var tasks = new List<Task>();

        // Iterate for each leaderboard and register the global result for the player avoiding duplicates
        var playerLeaderboardCollection = new Dictionary<string, List<(string leaderboardId, LeaderboardData? leaderboardData)>>();

        // First, check if the optional leaderboard data is provided, if not, get the leaderboard entries for each leaderboard ID provided in the input data
        if (optionalLeaderboards is null or { Count: 0 })
            foreach (var leaderboardId in leaderboardIds)
                tasks.Add(GetLeaderboardPlayers(leaderboardId));

        // If the optional leaderboard data is provided, use it to register the player leaderboard data for the specific leaderboard ID provided
        else 
            foreach (var (leaderboardId, leaderboardData) in optionalLeaderboards)
                tasks.Add(GetSpecificlLeaderboardPlayers(leaderboardId, leaderboardData));

        // This will register each leaderboard entry for each player in the player leaderboard collection dictionary,
        // avoiding duplicates and logging warnings in case of duplicated entries or missing player IDs
        await Task.WhenAll(tasks);

        _logger?.LogInformation("[GLEPDM] Obtained the following leaderboard entries for each player. {LeadeboardsData}", JsonConvert.SerializeObject(playerLeaderboardCollection, Formatting.Indented));

        // Once all leaderboards are processed, get the  data for each player
        if (playerLeaderboardCollection is not null and { Count: > 0 })
        {
            // Clear tasks list to reuse it
            tasks.Clear();

            // Iterate for each player to get his data collection
            foreach (var kvp in playerLeaderboardCollection)
                tasks.Add(GetPlayerData(kvp.Key, kvp.Value));

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
        }

        // Once all player data is retrieved, generate the global results
        if (playersLeaderboardTupleData is not null and { Count: > 0 })
            foreach (var (playerID, (playerLeaderboardData, errorMessage)) in playersLeaderboardTupleData!)
            {
                if (!globalResults.Any(x => x.playerLeaderboardData != null && x.playerLeaderboardData.playerId == playerID))
                    GeneratePlayerDataResult(
                        playerLeaderboardData,
                        errorMessage);
                else
                    _logger?.LogWarning("[GLEPDM] Player {PlayerId} already exists in global results, skipping duplicate.", playerID);
            }

        // Create the response object with the global results and serialize it to JSON
        var leaderboardUpdateResponse = new GetLeaderboardPlayerDataResponse(globalResults);
        var leaderboardUpdateResponseJson = JsonConvert.SerializeObject(leaderboardUpdateResponse);

        // Get the data use to encrypt the response
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Encrypt the response data and return it as a JSON string
        var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(leaderboardUpdateResponseJson, derivedKey, derivedIv));
        return encryptedDataJson;

        // Get leaderboard players for a given leaderboard ID and store them in the player leaderboard collection
        async Task GetLeaderboardPlayers(string leaderboardId)
        {
            _logger.LogInformation("[GLEPDM] Processing leaderboard {LeaderboardId} with max players {MaxPlayers}.", leaderboardId, maxPlayers);

            var leaderboardEntries = new List<LeaderboardData>();

            // Get the leaderboard scores for the given leaderboard ID
            try
            {
                leaderboardEntries = await UGSApiHelper.GetLeaderboardScores(executionContext, leaderboardId, maxPlayers);
                if (leaderboardEntries is null or { Count: 0 })
                {
                    _logger.LogWarning("[GLEPDM] No entries found for leaderboard {LeaderboardId}.", leaderboardId);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GLEPDM] Failed to process leaderboard {LeaderboardId}.", leaderboardId);
                return;
            }

            // Iterarate foreach entry and update leaderboard entry
            if (leaderboardEntries is not null and { Count: > 0 })
                foreach (var entry in leaderboardEntries)
                {
                    var currentPlayerId = entry.playerId;
                    if (string.IsNullOrEmpty(currentPlayerId))
                    {
                        _logger.LogWarning("[GLEPDM] Player ID is null or empty for leaderboard {LeaderboardId}.", leaderboardId);
                        continue;
                    }

                    lock(playerLeaderboardCollection) 
                    {
                        // Initialize player leaderboard collection if not exists
                        if (!playerLeaderboardCollection.ContainsKey(currentPlayerId))
                            playerLeaderboardCollection.Add(currentPlayerId, []);

                        if (!playerLeaderboardCollection[currentPlayerId].Any(x => x.leaderboardId == leaderboardId))
                            playerLeaderboardCollection[currentPlayerId].Add(new (leaderboardId, entry));
                        else
                            _logger.LogWarning("[GLEPDM] Player {PlayerId} already processed for leaderboard {LeaderboardId}, skipping duplicate.", currentPlayerId, leaderboardId);
                    }
                }
        }

        // Get specific leaderboard players for a given leaderboard ID and store them in the player leaderboard collection, using the optional player scores provided as input data and avoiding duplicates
        Task GetSpecificlLeaderboardPlayers(string leaderboardId, List<RTDBPlayerLeaderboardData> optionalPlayerScores)
        {
            _logger.LogInformation("[GLEPDM] Processing leaderboard {LeaderboardId} with max players {MaxPlayers}.", leaderboardId, maxPlayers);

            // Local function to get the current tier based on the player's score and the leaderboard configuration tiers data
            var getCurrentTier = new Func<int, string>(score =>
            {
                var tier = configData.leaderboardConfig.tiersData
                    .LastOrDefault(t => score >= t.scoreStartsAt);

                return tier?.leaderboardTier.ToString() ?? "Unranked";
            });

            // Local function to generate a leaderboard entry from a RTDBPlayerLeaderboardData object and an index, using the getCurrentTier function to determine the tier based on the player's score
            var generateLeaderboardEntry = new Func<RTDBPlayerLeaderboardData, int, LeaderboardData>((x, index) => new LeaderboardData
            (
                playerId: x.userId!,
                playerName: x.username ?? string.Empty,
                rank: index,
                score: x.score,
                tier: getCurrentTier(x.score),
                metadata: string.Empty
            ));

            optionalPlayerScores = [.. optionalPlayerScores
                .Where(x => !string.IsNullOrEmpty(x.userId))
                .Take(maxPlayers)
                .OrderBy(x => x.score)];

            var leaderboardEntries = optionalPlayerScores.Select((x, index) => generateLeaderboardEntry(x, index))?.ToArray();

            // Iterarate foreach entry and update leaderboard entry
            if (leaderboardEntries is not null and { Length: > 0 })
                foreach (var entry in leaderboardEntries)
                {
                    var currentPlayerId = entry.playerId;
                    if (string.IsNullOrEmpty(currentPlayerId))
                    {
                        _logger.LogWarning("[GLEPDM] Player ID is null or empty for leaderboard {LeaderboardId}.", leaderboardId);
                        continue;
                    }

                    lock (playerLeaderboardCollection)
                    {
                        // Initialize player leaderboard collection if not exists
                        if (!playerLeaderboardCollection.ContainsKey(currentPlayerId))
                            playerLeaderboardCollection.Add(currentPlayerId, []);

                        if (!playerLeaderboardCollection[currentPlayerId].Any(x => x.leaderboardId == leaderboardId))
                            playerLeaderboardCollection[currentPlayerId].Add(new(leaderboardId, entry));
                        else
                            _logger.LogWarning("[GLEPDM] Player {PlayerId} already processed for leaderboard {LeaderboardId}, skipping duplicate.", currentPlayerId, leaderboardId);
                    }
                }

            // This is just to satisfy the return type of the method, the actual results are stored in the player leaderboard collection and processed later in the main method flow
            return Task.CompletedTask;
        }

        // Local function to get player  data and store it in the dictionary
        async Task GetPlayerData(string playerId, List<(string leaderboardId, LeaderboardData? leaderboadData)> ugsLeaderboardsData)
        {
            // Initialize the main leaderboard player data variable to null
            var leaderboardPlayerData = default(PlayerLeaderboardData?);

            // Initialize the player profile data, match data and analytics data variables to null
            var profileData = default(PlayerProfileData?);
            var matchData = default(PlayerMatchData?);
            var analyticsData = default(AnalyticsData?);
            var nationalityData = default(NationalityData?);

            var errorMessage = string.Empty;

            var profileKey = CloudSaveProperties.Profile.ToString();
            var matchKey = CloudSaveProperties.Match.ToString();
            var analyticsKey = CloudSaveProperties.Analytics.ToString();
            var nationalityKey = CloudSaveProperties.Nationality.ToString();

            try
            {
                var playerData = await UGSApiHelper.ProtectedLoadData(
                    _gameApiClient, executionContext,
                    customID: null,
                    alternativePlayerID: playerId,
                    isThrowingException: false,
                    profileKey, matchKey, analyticsKey, nationalityKey);

                _logger?.LogInformation("[GLEPDM] Loaded player data for player {PlayerId}.\n\n {SerializedData}", 
                    playerId, JsonConvert.SerializeObject(playerData, formatting: Formatting.Indented));

                // Get the player profile data
                profileData = playerData
                    ?.FirstOrDefault(x => x?.key == profileKey)
                    ?.value
                    ?.ToObject<PlayerProfileData>();
                
                // Get the player math data
                matchData = playerData
                    ?.FirstOrDefault(x => x?.key == matchKey)
                    ?.value
                    ?.ToObject<PlayerMatchData>();

                // Get the player data
                analyticsData = playerData
                    ?.FirstOrDefault(x => x?.key == analyticsKey)
                    ?.value
                    ?.ToObject<AnalyticsData>();

                // Get the nationality data
                nationalityData = playerData
                    ?.FirstOrDefault(x => x?.key == nationalityKey)
                    ?.value
                    ?.ToObject<NationalityData>();

                if (profileData is null)
                    _logger?.LogWarning("[GLEPDM] Profile data is null for player {PlayerId}.", playerId);

                if (matchData is null)
                    _logger?.LogWarning("[GLEPDM]Match data is null for player {PlayerId}.", playerId);

                if (analyticsData is null)
                    _logger?.LogWarning("[GLEPDM]Analytics data is null for player {PlayerId}.", playerId);

                if (nationalityData is null)
                    _logger?.LogWarning("[GLEPDM]Nationality data is null for player {PlayerId}.", playerId);

                var leaderboardInfo = ugsLeaderboardsData
                    ?.Where(x => !string.IsNullOrEmpty(x.leaderboardId))
                    ?.Select(x => new LeaderboardInfo(x.leaderboardId, x.leaderboadData))
                    ?.ToList() 
                    ?? [];

                // Create the main leaderboard player data object with the retrieved data
                leaderboardPlayerData = new PlayerLeaderboardData(
                    playerId: playerId,
                    playerNationality: nationalityData?.nationalityType ?? default,
                    leaderboardInfos: leaderboardInfo,
                    playerProfileData: profileData,
                    playerMatchData: matchData,
                    analyticsData: analyticsData);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[GLEPDM] Failed to generate leardeboard data for player {PlayerId}.\n\n{Exception}", playerId, ex.Message);
                errorMessage = ex.Message;
            }

            lock (playersLeaderboardTupleData)
            { 
                // Try to add the player  data to the dictionary
                if (!playersLeaderboardTupleData.TryAdd(playerId, (leaderboardPlayerData, errorMessage)))
                    _logger?.LogWarning("[GLEPDM] Player data of player {PlayerId} already exists, skipping duplicate.", playerId);
            }
        }

        // Local function to generate player  result and add it to the global results
        void GeneratePlayerDataResult(PlayerLeaderboardData? playerLeaderboardData, string? errorMessage)
        {
            // Check if playerId is null or empty
            if (playerLeaderboardData is null or { playerId: null or "" })
            {
                _logger?.LogWarning("[GLEPDM] Player data or its id is null.");
                return;
            }

            // Add the player  result to the global results
            globalResults.Add(new PlayerLeaderboardDataResult
                (playerLeaderboardData: playerLeaderboardData,
                error: errorMessage ?? string.Empty));
        }
    }
}
