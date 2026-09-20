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

namespace Backend.AchievementsSystem
{
    public class ClaimAchievementRewardModule(ILogger<ClaimAchievementRewardModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<ClaimAchievementRewardModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        private GameTutorialData[]? gameTutorialDatas;
        private List<PlayerTutorialData>? playerTutorialDatas;

        [CloudCodeFunction(nameof(ClaimAchievementReward))]
        public async Task<string?> ClaimAchievementReward(IExecutionContext executionContext, string parametersEncryptedJson)
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
                throw new ArgumentException("Invalid input data. Provide player achievement");

            // Check if the data dictionary contains the required keys and values
            if (!data.TryGetValue("completedPlayerAchievement", out var completedPlayerAchievementObj) 
                || !Enum.TryParse(completedPlayerAchievementObj.ToString(), out Achievement completedPlayerAchievement) 
                || completedPlayerAchievement is Achievement.None)
                throw new ArgumentException("Completed achievement is not specified");

            var achievementKeyID = CloudSaveProperties.Achievements.ToString();
            var analyticsKeyID = CloudSaveProperties.Analytics.ToString();
            var cosmeticKeyID = CloudSaveProperties.Cosmetics.ToString();
            var tutorialsKeyID = CloudSaveProperties.Tutorials.ToString();

            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubIDKey = CloudSaveProperties.Club.ToString();

            // Load current player achievements data from Cloud Save
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null,
                alternativePlayerID: null,
                isThrowingException: false,
                achievementKeyID, analyticsKeyID, cosmeticKeyID, tutorialsKeyID, 
                firebaseIDKey,
                firebaseIDTokenKey,
                firebaseRefreshTokenKey,
                clubIDKey);

            // Get the current player achievements data from the response
            var playerAchievements = playerDataResponse
                ?.FirstOrDefault(x => x?.key == achievementKeyID)
                ?.value?.ToObject<PlayerAchievementData[]>();

            // Get the current player analytics data from the response
            var analyticsData = playerDataResponse
                ?.FirstOrDefault(x => x?.key == analyticsKeyID)
                ?.value?.ToObject<AnalyticsData>()
                ?? new();

            // Get the current player owned cosmetics data from the response
            var ownedCosmetics = playerDataResponse?
                .FirstOrDefault(x => x?.key == cosmeticKeyID)
                ?.value?.ToObject<PlayerCosmeticData[]>() ?? [];

            // Get the current player tutorials data from the response
            playerTutorialDatas = playerDataResponse?
                .FirstOrDefault(x => x?.key == tutorialsKeyID)
                ?.value?.ToObject<List<PlayerTutorialData>>() ?? [];

            // Check if the player achievements data is valid
            if (playerAchievements is null || playerAchievements.Length == 0)
                throw new UGSException("No player achievements found.");

            // Search for the achievement by ID
            var achievementToClaim = playerAchievements.FirstOrDefault(x => x.achievement == completedPlayerAchievement);
            if (achievementToClaim is null)
                throw new UGSException("Achievement not found in player data.");

            // Verify that the achievement is completed and not already claimed
            if (!achievementToClaim.completed || achievementToClaim.claimed)
                throw new UGSException("Achievement is either not completed or already claimed.");

            // Validate entry data
            var memberFirebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var memberFirebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var memberFirebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();
            var memberClubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();

            // Load Game Data configuration and achievements (from LiveOps namespace)
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: customID,
                alternativePlayerID: null,
                isThrowingException: false,
                achievementKeyID, tutorialsKeyID);

            // Get daily achievements defined in Game Data
            var gameDataAchievement = loadGameDataResponse
                ?.FirstOrDefault(x => x?.key == achievementKeyID)
                ?.value?.ToObject<GameAchievementData[]>()
                ?.FirstOrDefault(x => x != null && x.achievement == completedPlayerAchievement);

            // Get the current game tutorials data from the response
            gameTutorialDatas = loadGameDataResponse?
                .FirstOrDefault(x => x?.key == tutorialsKeyID)
                ?.value?.ToObject<GameTutorialData[]>() ?? [];

            // Check if the Game Data achievement definition is valid
            if (gameDataAchievement is null || gameDataAchievement.rewardCosmeticsIDs is null or { Length: 0 })
                throw new UGSException("Game Data achievement definition is invalid or missing.");

            // Initialize some variables for the response
            var dataToSave = new Dictionary<string, object>();

            // Else, confirm that the achievement progress is sufficient to claim the reward
            var isAchievmentCompletedInThisSesion = await CheckAchievementProgress(executionContext, analyticsData, achievementToClaim, gameDataAchievement);
            if (!isAchievmentCompletedInThisSesion)
                throw new UGSException("Achievement progress is insufficient to claim reward.");

            // Update the achievement to mark it as claimed
            achievementToClaim.claimed = true;
            achievementToClaim.reclaimedTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var rewardCosmetics = gameDataAchievement.rewardCosmeticsIDs;
            var newCosmeticsToAdd = rewardCosmetics
                .Where(rewardId => !ownedCosmetics.Any(c => c.id == rewardId))
                .Select(id => new PlayerCosmeticData { 
                    id = id, 
                    acquiredTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 
                    cosmeticPurchaseMethod = CosmeticPurchaseMethod.Achievement 
                })
                .ToArray();

            if (newCosmeticsToAdd.Length > 0)
            {
                var updatedCosmetics = ownedCosmetics.Concat(newCosmeticsToAdd).ToArray();
                dataToSave.Add(cosmeticKeyID, updatedCosmetics);
            }

            // Save the updated player achievements data back to Cloud Save
            dataToSave.Add(achievementKeyID, playerAchievements.Select(x => x.Clone()).ToArray());

            // Update the analytics data to increment the achievements claimed count and save it
            analyticsData!.achievementsClaimedCount++;
            dataToSave.Add(analyticsKeyID, analyticsData);

            // Save the updated data to Cloud Save
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, dataToSave);

            // If the player is in a club, update their total achievements in the club data
            await TryToUpdateClubMemberTotalAchievements(executionContext, _gameApiClient, 
                memberClubName, memberFirebaseID, memberFirebaseIDToken, memberFirebaseRefreshToken, analyticsData!.achievementsClaimedCount);

            // Serialize the achievement configuration data to JSON
            var claimRewardResponse = new AchievementClaimRewardResponse(
                cosmeticsObtained: gameDataAchievement.rewardCosmeticsIDs);

            var claimRewardDataJson = JsonConvert.SerializeObject(claimRewardResponse);
            var derivedKey = SecurityHelper.DeriveKey
                (executionContext?.PlayerId ?? string.Empty,
                executionContext?.AccessToken ?? string.Empty);

            var securityDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(claimRewardDataJson, derivedKey));
            return securityDataJson;   
        }

        /// <summary>
        /// Checks the achievement progress based on the player and game achievement data.<br></br>
        /// Specific checks will be performed based on the achievement type
        /// </summary>
        /// <returns> Return true if the achievement is completed in this session, otherwise false.</returns>
        private Task<bool> CheckAchievementProgress
            (IExecutionContext executionContext,
            AnalyticsData analyticsData,
            PlayerAchievementData playerAchievementData, 
            GameAchievementData gameAchievementData)
        { 
            if (gameAchievementData is null)
                throw new ArgumentNullException(nameof(gameAchievementData), "Game achievement data cannot be null.");

            if (gameAchievementData.achievementType is AchievementType.None)
                throw new ArgumentException("Game achievement type cannot be None.", nameof(gameAchievementData.achievementType));

            // Determine if the player achievement is completed and if the completion time is valid
            var isAchievementCompleted = playerAchievementData is not null
                and { completed: true, completedTime: not null or 0 }
                && DateTimeOffset.FromUnixTimeSeconds(playerAchievementData.completedTime.Value) < DateTime.UtcNow;

            var isAchievementReclaimed = playerAchievementData is not null
                and { claimed: true, reclaimedTime: not null or 0 }
                && DateTimeOffset.FromUnixTimeSeconds(playerAchievementData.reclaimedTime.Value) < DateTime.UtcNow;

            // If the achievement is not completed or has been reclaimed, return false immediately
            if (!isAchievementCompleted || isAchievementCompleted && isAchievementReclaimed)
                return Task.FromResult(false);

            return gameAchievementData.achievementType switch
            {
                AchievementType.PlaceTiles => CheckPlaceTilesProgress(),
                AchievementType.ObtainCosmetic => CheckCosmeticsObtainedProgress(),
                AchievementType.CompleteTutorial => CheckTutorialProgress(),
                AchievementType.ReachLeaderboardTier => CheckTierProgress(),
                _ => throw new ArgumentException($"Unsupported achievement type: {gameAchievementData.achievementType}")
            };
            
            Task<bool> CheckPlaceTilesProgress()
            { 
                if (analyticsData is null)
                    throw new ArgumentNullException(nameof(analyticsData), "Analytics data cannot be null.");

                return Task.FromResult(analyticsData.tilesPlaced >= gameAchievementData.goalAmount);
            }
            
            Task<bool> CheckCosmeticsObtainedProgress()
            { 
                if (analyticsData is null)
                    throw new ArgumentNullException(nameof(analyticsData), "Analytics data cannot be null.");

                return Task.FromResult(analyticsData.cosmeticsObtainedCount >= gameAchievementData.goalAmount);
            }

            Task<bool> CheckTutorialProgress()
            {
                // Check if the player achievement is null or empty
                if (gameTutorialDatas is null or { Length: 0 })
                    throw new ArgumentException("There aren'tn any game tutorial data to load");

                // Find the game tutorial that matches the game mode filter of the achievement
                var gameTutorial = gameTutorialDatas.FirstOrDefault(x => x.relatedGameMode == gameAchievementData.gameModeFilter);
                if (gameTutorial is null or { steps: null or { Length: 0 }})
                    throw new ArgumentException($"Game tutorial not found for the specified game mode filter. Game Mode: {gameAchievementData.gameModeFilter}");

                // Check if the player tutorial matches the game tutorial and if all steps are completed
                var playerTutorial = playerTutorialDatas?.FirstOrDefault(x => x.relatedGameMode == gameTutorial.relatedGameMode);
                var isTutorialCompleted = playerTutorial is not null
                    && playerTutorial.steps != null
                    && gameTutorial.steps.All(x => playerTutorial.steps.Any(y => y.id == x))
                    && playerTutorial.steps.All(x => DateTimeOffset.FromUnixTimeSeconds(x.completedTime) <= DateTime.UtcNow);

                // Check if the player achievement matches the game achievement
                return Task.FromResult(isTutorialCompleted);
            }

            async Task<bool> CheckTierProgress()
            {
                // Invoke all tasks to get player scores for each leaderboard
                var playerAchievementsByTierCollection = await UGSApiHelper.TryToGetPlayerTierAchievements(executionContext, _logger);

                // Check if the collection has entries
                if (playerAchievementsByTierCollection is null or { Count: 0 })
                    throw new ArgumentException("There aren'tn any game tutorial data to load");

                // Check if the player achievements collection contains the game mode filter; if not, it means the player has no achievements for that game mode
                if (!playerAchievementsByTierCollection.TryGetValue(gameAchievementData.gameModeFilter!.Value, out var playerTierCollection))
                    return false;
                
                // Try to get the achievements for the specified leaderboard tier; if not found, it means the player has no achievements for that tier
                var unlockedAchievementsByTier = playerTierCollection.FirstOrDefault(x => x.Key == gameAchievementData.leaderboardTier).Value;
                if (unlockedAchievementsByTier is null or { Length: 0 })
                    return false;

                // Check if the player achievement matches the game achievement
                var isTierReached = playerAchievementData is not null ? unlockedAchievementsByTier.Contains(playerAchievementData.achievement) : false;
                return isTierReached;
            }  
        }

        /// <summary>
        /// Tries to update the total achievements of a club member in Firestore.<br></br>
        /// </summary>
        private async Task TryToUpdateClubMemberTotalAchievements(IExecutionContext executionContext, IGameApiClient gameApiClient,
            string? playerDataClubName, string? firebaseID, string? firebaseIDToken, string? firebaseRefreshToken, int totalAchievements)
        {
            if (!string.IsNullOrEmpty(playerDataClubName) && !string.IsNullOrEmpty(firebaseIDToken) && !string.IsNullOrEmpty(firebaseRefreshToken))
            {
                // Create the path used in Firestore to get the club data
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                    (firebaseIDToken,
                    firebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: playerDataClubName,
                    logger: _logger);

                // Check if the club data exists
                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? FirestoreClubData.ParseClubData(clubDataJson)
                    : null;
                if (clubData is not null)
                {
                    // Find the member in the club data and update their total achievements
                    var member = clubData.members.FirstOrDefault(x => x.firebaseMemberId == firebaseID);
                    if (member != null)
                    {
                        member.totalAchievements = totalAchievements;

                        var updatedFields = new
                        {
                            members = FirebaseApiHelper.BuildFirestoreMembersArray(clubData.members),
                            updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                        };

                        // Update club data in Firestore
                        var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                            executionContext,
                            gameApiClient,
                            collection: FirebaseApiHelper.clubsCollectionIdKey,
                            documentId: playerDataClubName,
                            documentFields: updatedFields,
                            updateFieldPaths: ["members", "updatedAt"]
                        );

                        // Log the result of the update operation
                        if (!string.IsNullOrEmpty(updateResponse))
                            _logger.LogInformation($"Club {playerDataClubName} total achievements updated for member {firebaseID}.");
                        else
                            _logger.LogWarning($"Failed to update club {playerDataClubName} for member {firebaseID}.");
                    
                    } else
                        _logger.LogWarning($"Target member {firebaseID} not found in club data.");
                }
            }
        }
    }
}
