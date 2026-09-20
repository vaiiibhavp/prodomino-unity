using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
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

namespace Backend.AchievementsSystem
{
    public class UpdateAchievementProgressModule
        (ILogger<UpdateAchievementProgressModule> logger, 
        IGameApiClient gameApiClient,
        Dictionary<GameModeFilter, Dictionary<LeaderboardTier, Achievement[]>>? leaderboardDatas)
    {
        private readonly ILogger<UpdateAchievementProgressModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;
        private Dictionary<GameModeFilter, Dictionary<LeaderboardTier, Achievement[]>>? leaderboardDatas = leaderboardDatas ?? [];

        private GameTutorialData[]? gameTutorialDatas;
        private List<PlayerTutorialData>? playerTutorialDatas;

        [CloudCodeFunction(nameof(UpdateAchievementProgress))]
        public async Task UpdateAchievementProgress(IExecutionContext executionContext, string parametersEncryptedJson)
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

            var updatesFromFrontend = default(PlayerAchievementData[]); 
                
            // Check if the data dictionary contains the required keys and values
            if (data is null || data.Count == 0
                || !data.TryGetValue("updatesFromFrontend", out var updatesFromFrontendObj)
                || updatesFromFrontendObj is not JToken updatesFromFrontendToken
                || (updatesFromFrontend = updatesFromFrontendToken.ToObject<PlayerAchievementData[]>()) is not PlayerAchievementData[]
                || updatesFromFrontend is null or { Length: 0 })
                _logger.LogWarning("Player Achievements are not specified");

            var achievementKeyID = CloudSaveProperties.Achievements.ToString();
            var tutorialsKeyID = CloudSaveProperties.Tutorials.ToString();

            // Load Game Data
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData
                (_gameApiClient, executionContext,
                customID: customID,
                alternativePlayerID: null,
                isThrowingException: false,
                achievementKeyID, tutorialsKeyID);

            if (loadGameDataResponse is null)
                throw new UGSException("Failed to load game data. Please check your configuration or try again later.");

            // Get game data achievements defined in Game Data
            var gameDataAchievements = loadGameDataResponse
                ?.FirstOrDefault(x => x?.key == achievementKeyID.ToString())
                ?.value?.ToObject<GameAchievementData[]>();

            // Get the current game tutorials data from the response
            gameTutorialDatas = loadGameDataResponse
                ?.FirstOrDefault(x => x?.key == tutorialsKeyID)
                ?.value?.ToObject<GameTutorialData[]>() ?? [];

            // Check if the Game Data dictionary contains the required keys and values
            if (gameDataAchievements is null or { Length: 0 })
                throw new UGSException("No game achievement definitions found.");

            var analyticsKeyID = CloudSaveProperties.Analytics.ToString();

            // Load player achievementes
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData
                (_gameApiClient, executionContext,
                customID: null,
                alternativePlayerID: null,
                isThrowingException: false,
                achievementKeyID, analyticsKeyID);

            // Try to get the current player analytics data from the response
            var analyticsData = playerDataResponse
                ?.FirstOrDefault(x => x?.key == analyticsKeyID)
                ?.value?.ToObject<AnalyticsData>()
                ?? new();

            // Try to get the player data achievements
            var playerDataAchievements = playerDataResponse
                ?.FirstOrDefault(x => x?.key == achievementKeyID.ToString())
                ?.value?.ToObject<PlayerAchievementData[]>()
                ?.ToList()
                ?? [];

            // Try to get the current player tutorials data from the response
            playerTutorialDatas = playerDataResponse
                ?.FirstOrDefault(x => x?.key == tutorialsKeyID)
                ?.value?.ToObject<List<PlayerTutorialData>>() 
                ?? [];

            // Load leaderboard data for the player only if there are achievements of type ReachLeaderboardTier in the player data achievements
            if (gameDataAchievements.Any(x => x.achievementType is AchievementType.ReachLeaderboardTier))
                leaderboardDatas = await UGSApiHelper.TryToGetPlayerTierAchievements(executionContext, _logger);

            // Update the progress of the achievements based on the updates from the frontend
            await TryToCompleteAchievements(executionContext, analyticsData, playerDataAchievements, gameDataAchievements, updatesFromFrontend);
            if (playerDataAchievements is null or { Count: 0 })
            {
                _logger.LogWarning("No achievements found to update for player {PlayerId}.", executionContext.PlayerId);
                return;
            }

            // Save the updated achievements to the player's data
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, 
                new()
                {
                    [achievementKeyID] = playerDataAchievements
                });
        }

        private async Task TryToCompleteAchievements
            (IExecutionContext executionContext,
            AnalyticsData analyticsData,
            List<PlayerAchievementData> playerAchievementsData,
            GameAchievementData[] gameAchievementsData,
            PlayerAchievementData[]? updatesFromFrontend)
        {
            var achievementCollection = gameAchievementsData
                ?.ToDictionary
                    (x => x, 
                    x => (playerAchievementsData: playerAchievementsData?.FirstOrDefault(y => y.achievement == x.achievement), 
                        updateFromFrontend: updatesFromFrontend?.FirstOrDefault(y => y.achievement == x.achievement)));

            // Iterate for each entry and, according the update from frontend, update the achievement iterated
            var achievementToComplete = new List<Task<(bool wasAchievementClaimed, GameAchievementData gameAchievement, PlayerAchievementData? playerAchievementData, PlayerAchievementData? updateFromFrontend)>>();
            if (achievementCollection is not null and { Count: > 0 })
            { 
                foreach (var (_gameAchievement, (_playerAchievement, _updateFromFrontend)) in achievementCollection)
                {
                    // Check if the game achievement data is null
                    if (_gameAchievement is null)
                    {
                        _logger.LogWarning("Game achievement data is null for player {PlayerId}.", executionContext.PlayerId);
                        continue;
                    }

                    // Skip if the achievement is already completed
                    if (_playerAchievement is not null and { completed: true })
                    {
                        _logger.LogInformation("Achievement {Achievement} is already completed for player {PlayerId}.", _gameAchievement.achievement, executionContext.PlayerId);
                        continue;
                    }

                    achievementToComplete.Add(TryToCompleteSpecificAchievement(_gameAchievement, _playerAchievement, _updateFromFrontend));
                }

                if (achievementToComplete.Count > 0)
                { 
                    // Wait for all tasks to complete and collect the results
                    var unlockedAchievements = await Task.WhenAll(achievementToComplete);
                    _logger.LogInformation("Unlocked {Count} achievements for player {PlayerId}", unlockedAchievements.Length, executionContext.PlayerId);

                    for (var i = 0; i < unlockedAchievements.Length; i++)
                    {
                        var (shouldClaimReward, gameAchievement, playerAchievement, updateFromFrontend) = unlockedAchievements[i];

                        // If the achievement was claimed, update the player achievement data
                        if (shouldClaimReward)
                        {
                            // Check if the player achievement data is null or not; if it is null, create a new instance and add it to the list
                            if (playerAchievement is null)
                                playerAchievement = new(gameAchievement.achievement);

                            // Complete the achievement and set the completed time
                            playerAchievement.completed = true;
                            playerAchievement.completedTime = ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeSeconds();

                            // If there aren't any cosmetics to claim, mark the achievement as claimed and set the reclaimed time to the completed time
                            if (gameAchievement.rewardCosmeticsIDs?.All(string.IsNullOrEmpty) ?? true)
                            {
                                playerAchievement.claimed = true;
                                playerAchievement.reclaimedTime = playerAchievement.completedTime;
                            }
                        }

                        // Try to replace player achievement in the list to get always the most recent data
                        if (playerAchievement is not null)
                        { 
                            var existing = playerAchievementsData.FirstOrDefault(x => x.achievement == playerAchievement.achievement);

                            // If exists, remove the existing achievement and add the updated one
                            if (existing != null)
                                playerAchievementsData.Remove(existing);
                            playerAchievementsData.Add(playerAchievement);
                        }
                    }
                }
                else
                    _logger.LogWarning("No achievements were updated for player {PlayerId}.", executionContext.PlayerId);
            } 
            else
                _logger.LogWarning("No achievements found to update for player {PlayerId}.", playerAchievementsData.FirstOrDefault()?.achievement);

            async Task<(bool shouldClaimReward, GameAchievementData gameAchievement, PlayerAchievementData? playerAchievementData, PlayerAchievementData? updateFromFrontend)> 
                TryToCompleteSpecificAchievement(GameAchievementData gameAchievement, PlayerAchievementData? playerAchievement, PlayerAchievementData? updateFromFrontend)
            {
                var (shouldClaimReward, newplayerAchievement) = await (gameAchievement.achievementType switch
                    {
                        AchievementType.ReachLeaderboardTier => CheckTierProgress(),
                        AchievementType.ObtainCosmetic => CheckCosmeticsObtainedProgress(),
                        AchievementType.PlaceTiles => CheckPlaceTilesProgress(),
                        AchievementType.AddFriends => CheckAddFriendsProgress(),
                        AchievementType.CompleteTutorial => CheckTutorialProgress(),
                        _ => throw new NotImplementedException(),
                    });

                return 
                    (shouldClaimReward, gameAchievement, newplayerAchievement, updateFromFrontend);

                Task<(bool shouldClaimReward, PlayerAchievementData? newplayerAchievement)> CheckTierProgress()
                {
                    // Check if the game achievement data is null
                    if (gameAchievement is null)
                    {
                        _logger.LogWarning("Game achievement data is null for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Check if the leaderboard tier is null
                    if (gameAchievement.leaderboardTier is null)
                    {
                        _logger.LogWarning("Leaderboard tier is null for game achievement {GameAchievement}. Player ID: {PlayerId}", gameAchievement.achievement, executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Invoke all tasks to get player scores for each leaderboard
                    var playerAchievementsByTierCollection = leaderboardDatas;

                    // Check if the collection has entries
                    if (playerAchievementsByTierCollection is null or { Count: 0 })
                    { 
                        _logger.LogWarning("No leaderboard data found for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Check if the player achievements collection contains the game mode filter; if not, it means the player has no achievements for that game mode
                    if (!playerAchievementsByTierCollection.TryGetValue(gameAchievement.gameModeFilter!.Value, out var playerTierCollection))
                    {
                        _logger.LogWarning("No leaderboard tier data found for game mode {GameMode}. Player ID: {PlayerId}", gameAchievement.gameModeFilter, executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Try to get the achievements for the specified leaderboard tier; if not found, it means the player has no achievements for that tier
                    var unlockedAchievementsByTier = playerTierCollection.FirstOrDefault(x => x.Key == gameAchievement.leaderboardTier.Value).Value;
                    if (unlockedAchievementsByTier is null or { Length: 0 })
                    {
                        _logger.LogWarning("No achievements found for leaderboard tier {LeaderboardTier} in game mode {GameMode}. Player ID: {PlayerId}",
                            gameAchievement.leaderboardTier, gameAchievement.gameModeFilter, executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    var isTierReached = unlockedAchievementsByTier.Contains(gameAchievement.achievement);

                    // Check if the player achievement matches the game achievement
                    return Task.FromResult((isTierReached, playerAchievement));
                }

                Task<(bool shouldClaimReward, PlayerAchievementData? newplayerAchievement)> CheckPlaceTilesProgress()
                {
                    // Check if the analytics data is null
                    if (analyticsData is null)
                    {
                        _logger.LogWarning("Analytics data is null for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Check if the frontend game achievement data is null (neccesary to update the player achievement data using local analytics data)
                    if (updateFromFrontend is null)
                    {
                        _logger.LogWarning("Frontend achievement data is null for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Check if the player achievement data is null or empty
                    if (playerAchievement is not null)
                    {
                        // Only update if the last progress is the most recent
                        if (updateFromFrontend!.lastProgressUpdateTime < playerAchievement.lastProgressUpdateTime)
                            return Task.FromResult(default((bool, PlayerAchievementData?)));

                        // Get the difference in progress
                        var progressUpdated = updateFromFrontend.progress - playerAchievement.progress;

                        // If there aren't progress, continue the iteration
                        if (progressUpdated <= 0)
                            return Task.FromResult(default((bool, PlayerAchievementData?)));

                        // Sum progress and update the lastProgressUpdateTime
                        playerAchievement.progress += progressUpdated;
                    }

                    // If the player achievement data is null, create a new instance
                    else
                    {
                        _logger.LogInformation("Creating new player achievement data for achievement {Achievement} for player {PlayerId}.", gameAchievement.achievement, executionContext.PlayerId);
                        playerAchievement = new PlayerAchievementData(gameAchievement.achievement)
                        {
                            progress = updateFromFrontend.progress,
                        };
                    }

                    playerAchievement.lastProgressUpdateTime = updateFromFrontend.lastProgressUpdateTime;

                    // Update the analytics data with the new progress
                    analyticsData.tilesPlaced = playerAchievement.progress;
                    var shouldClaimReward = playerAchievement.progress >= gameAchievement.goalAmount;

                    return Task.FromResult((shouldClaimReward, newplayerAchievement: (PlayerAchievementData?)playerAchievement));
                }

                Task<(bool shouldClaimReward, PlayerAchievementData? newplayerAchievement)> CheckCosmeticsObtainedProgress()
                {
                    if (analyticsData is null)
                    {
                        _logger.LogWarning("Analytics data is null for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    return Task.FromResult((analyticsData.cosmeticsObtainedCount >= gameAchievement.goalAmount, playerAchievement));
                }

                Task<(bool shouldClaimReward, PlayerAchievementData? newplayerAchievement)> CheckAddFriendsProgress()
                {
                    // Check if the analytics data is null
                    if (analyticsData is null)
                    {
                        _logger.LogWarning("Analytics data is null for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Check if the frontend game achievement data is null (neccesary to update the player achievement data using local analytics data)
                    if (updateFromFrontend is null)
                    {
                        _logger.LogWarning("Frontend achievement data is null for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    // Check if the player achievement data is null or empty
                    if (playerAchievement is not null)
                    {
                        // Only update if the last progress is the most recent
                        if (updateFromFrontend!.lastProgressUpdateTime < playerAchievement.lastProgressUpdateTime)
                            return Task.FromResult(default((bool, PlayerAchievementData?)));

                        // Get the difference in progress
                        var progressUpdated = updateFromFrontend.progress - playerAchievement.progress;

                        // If there aren't progress, continue the iteration
                        if (progressUpdated <= 0)
                            return Task.FromResult(default((bool, PlayerAchievementData?)));

                        // Sum progress and update the lastProgressUpdateTime
                        playerAchievement.progress += progressUpdated;
                    }

                    // If the player achievement data is null, create a new instance
                    else
                    {
                        _logger.LogInformation("Creating new player achievement data for achievement {Achievement} for player {PlayerId}.", gameAchievement.achievement, executionContext.PlayerId);
                        playerAchievement = new PlayerAchievementData(gameAchievement.achievement)
                        {
                            progress = updateFromFrontend.progress,
                        };
                    }

                    playerAchievement.lastProgressUpdateTime = updateFromFrontend.lastProgressUpdateTime;

                    // Update the analytics data with the new progress
                    analyticsData.friendsAdded = playerAchievement.progress;
                    var shouldClaimReward = playerAchievement.progress >= gameAchievement.goalAmount;

                    return Task.FromResult((shouldClaimReward, newplayerAchievement: (PlayerAchievementData?)playerAchievement));
                }

                Task<(bool shouldClaimReward, PlayerAchievementData? newplayerAchievement)> CheckTutorialProgress()
                {
                    // Check if the player achievement is null or empty
                    if (gameTutorialDatas is null or { Length: 0 })
                    {
                        _logger.LogWarning("No game tutorial data found for player {PlayerId}.", executionContext.PlayerId);
                        return Task.FromResult(default((bool, PlayerAchievementData?)));
                    }

                    var isTutorialCompleted = false;

                    // Check if the player tutorial data is null or empty. This check if every tutorial is completed
                    if (gameAchievement.achievement is Achievement.CompleteEveryTutorial)
                    {
                        var allDictionaryCollection = gameTutorialDatas
                            .Where(x => x.relatedGameMode.HasValue)
                            .ToDictionary(x => x, x => playerTutorialDatas?.FirstOrDefault(y => y?.relatedGameMode!.Value == x.relatedGameMode!.Value));

                        // Check if all tutorials are completed
                        isTutorialCompleted = allDictionaryCollection.All(x => x.Value is not null
                            && x.Value.steps != null
                            && x.Value.steps.Select(s => s.id).SequenceEqual(x.Key.steps!)
                            && x.Value.steps.All(x => DateTimeOffset.FromUnixTimeSeconds(x.completedTime) <= DateTime.UtcNow));
                    }

                    // But, if the game achievement is not CompleteEveryTutorial, we need to check the specific game tutorial
                    else
                    { 
                        // Find the game tutorial that matches the game mode filter of the achievement
                        var gameTutorial = gameTutorialDatas.FirstOrDefault(x => x.relatedGameMode == gameAchievement.gameModeFilter);
                        if (gameTutorial is null or { steps: null or { Length: 0 } })
                        {
                            _logger.LogWarning("No game tutorial found for game mode {GameMode}. Player ID: {PlayerId}", gameAchievement.gameModeFilter, executionContext.PlayerId);
                            return Task.FromResult(default((bool, PlayerAchievementData?)));
                        }

                        // Check if the player tutorial matches the game tutorial and if all steps are completed
                        var playerTutorial = playerTutorialDatas?.FirstOrDefault(x => x.relatedGameMode == gameTutorial.relatedGameMode);
                        isTutorialCompleted = playerTutorial is not null
                            && playerTutorial.steps != null
                            && playerTutorial.steps.Select(s => s.id).SequenceEqual(gameTutorial.steps)
                            && playerTutorial.steps.All(x => DateTimeOffset.FromUnixTimeSeconds(x.completedTime) <= DateTime.UtcNow);
                    }


                    // Check if the player achievement matches the game achievement
                    return Task.FromResult((isTutorialCompleted, playerAchievement));
                }
            }
        }
    }
}
