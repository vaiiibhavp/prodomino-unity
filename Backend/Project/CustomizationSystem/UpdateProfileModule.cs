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

namespace Backend.CustomizationSystem;

public class UpdateProfileModule(ILogger<UpdateProfileModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<UpdateProfileModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(UpdateProfile))]
    public async Task<string> UpdateProfile(IExecutionContext executionContext, string parametersEncryptedJson)
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
        var cosmeticsIDs = default(string[]?);
        if (!data.TryGetValue("cosmeticsIDs", out var cosmeticsIDsObj)
            || cosmeticsIDsObj is not JToken cosmeticsIDsToken
            || cosmeticsIDsToken.ToObject<string[]>() is not string[] cosmeticsIDsArray
            || cosmeticsIDsArray is null or { Length: 0 })
            _logger.LogWarning("Cosmetic ID is not specified or invalid");
        else
            cosmeticsIDs = cosmeticsIDsArray;

        // Check if the data dictionary contains the required keys and values
        var cosmeticType = CosmeticType.None;
        if (!data.TryGetValue("cosmeticType", out var cosmeticTypeObj)
            || cosmeticTypeObj is null
            || cosmeticTypeObj.ToString() is not string _cosmeticTypeStr
            || string.IsNullOrEmpty(_cosmeticTypeStr)
            || !Enum.TryParse(cosmeticTypeObj.ToString(), out CosmeticType _cosmeticType)
            || _cosmeticType is CosmeticType.None)
            _logger.LogWarning("Cosmetic type is not specified or invalid");
        else
            cosmeticType = _cosmeticType;

        var configIDKey = CloudSaveProperties.Config.ToString();
        var cosmeticsIDKey = CloudSaveProperties.Cosmetics.ToString();

        // Load Game Data
        var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: customID,
            alternativePlayerID: null,
            isThrowingException: false,
            configIDKey, cosmeticsIDKey);

        if (loadGameDataResponse is null)
            throw new UGSException("Failed to load game data. Please check your configuration or try again later.");

        // Get the Game Data configuration from the response
        var configData = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == CloudSaveProperties.Config.ToString())
            ?.value?.ToObject<ConfigData>();

        // Check if the Game Data configuration is valid
        if (configData is null or { cosmeticConfig: null })
            throw new UGSException("Game Data configuration is invalid. Please check your configuration or try again later.");

        // Get game data cosmetics defined in Game Data
        var gameDataCosmetics = loadGameDataResponse
            ?.FirstOrDefault(x => x?.key == cosmeticsIDKey.ToString())
            ?.value?.ToObject<GameCosmeticData[]>();

        // Check if the Game Data dictionary contains the required keys and values
        if (gameDataCosmetics is null or { Length: 0 })
            throw new UGSException("No game cosmetic definitions found.");

        var profileIDKey = CloudSaveProperties.Profile.ToString();
        var achievementIDKey = CloudSaveProperties.Achievements.ToString();
        var clubKey = CloudSaveProperties.Club.ToString();
        var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
        var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
        var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

        // Load player profile and cosmetics data from Cloud Save
        var playerDataResponse = await UGSApiHelper.ProtectedLoadData
            (_gameApiClient, executionContext,
            customID: null,
            alternativePlayerID: null,
            isThrowingException: false,
            profileIDKey, cosmeticsIDKey, achievementIDKey, clubKey, firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey);

        // Try to get the current player profile
        var playerDataProfile = playerDataResponse
            ?.FirstOrDefault(x => x?.key == profileIDKey)
            ?.value?.ToObject<PlayerProfileData>()
            ?? new();

        // Try to get the player data cosmetics
        var playerDataCosmetics = playerDataResponse
            ?.FirstOrDefault(x => x?.key == cosmeticsIDKey.ToString())
            ?.value?.ToObject<PlayerCosmeticData[]>()
            ?.ToList()
            ?? new();
        
        // Try to get the player data achievements
        var playerDataAchievements = playerDataResponse
            ?.FirstOrDefault(x => x?.key == achievementIDKey.ToString())
            ?.value?.ToObject<PlayerAchievementData[]>()
            ?.ToList();

        // Try to get the player club's name
        var playerDataClubName = playerDataResponse
            ?.FirstOrDefault(x => x?.key == clubKey.ToString())
            ?.value?.ToObject<string>();

        var firebaseID = playerDataResponse
            ?.FirstOrDefault(x => x?.key == firebaseIDKey.ToString())
            ?.value?.ToObject<string>();
        
        var firebaseIDToken = playerDataResponse
            ?.FirstOrDefault(x => x?.key == firebaseIDTokenKey.ToString())
            ?.value?.ToObject<string>();
        
        var firebaseRefreshToken = playerDataResponse
            ?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey.ToString())
            ?.value?.ToObject<string>();

        var wasUpdate = false;
        var dataToSave = new Dictionary<string, object>();

        // Check if the player data cosmetics is null or empty to initialize it with default cosmetics
        var ownedCosmeticIDs = playerDataCosmetics
            .Where(x => x is not null && !string.IsNullOrEmpty(x.id))
            ?.Select(x => x.id)
            ?.ToArray();

        var ownedIdsHashSet = ownedCosmeticIDs is not null and { Length: > 0 }
            ? new HashSet<string>(ownedCosmeticIDs!)
            : [];

        // Check if the default cosmetics are defined in the game data configuration
        var defaultCosmetics = configData.cosmeticConfig?.defaultCosmeticIDs?.ToArray();
        if (defaultCosmetics is not null and { Length: > 0 })
            // Check if there are any default cosmetics that the player does not own
            if (defaultCosmetics.Any(x => !ownedIdsHashSet.Contains(x)))
            {
                wasUpdate = true;

                // Iterate through the default cosmetics and add them to the player data cosmetics if not already owned
                foreach (var cosmetic in defaultCosmetics)
                {
                    if (ownedIdsHashSet.Contains(cosmetic))
                        continue;

                    playerDataCosmetics.Add(new PlayerCosmeticData
                    {
                        id = cosmetic,
                        cosmeticPurchaseMethod = CosmeticPurchaseMethod.Default
                    });
                }

                // Add the updated player cosmetics data to the data to save
                dataToSave.Add(cosmeticsIDKey, playerDataCosmetics);
                _logger.LogInformation("Default cosmetics added to player data cosmetics for player {PlayerId}.\n\nDefault Cosmetics added:\n{Cosmetics}", playerId, JsonConvert.SerializeObject(defaultCosmetics, Formatting.Indented));
            } else
                _logger.LogInformation("Player {PlayerId} already owns all default cosmetics defined in the game data configuration.", playerId);
else
            _logger.LogWarning("No default cosmetics found in the game data configuration.");


        // Check if the player data cosmetics is not null and has items
        if (playerDataCosmetics is not null and { Count: > 0 } && cosmeticsIDs is not null and { Length: > 0 })
        {
            var newBadges = new List<string>();
            foreach (var cosmeticId in cosmeticsIDs)
            {
                var isCosmeticInGame = gameDataCosmetics.Any(x => x?.id == cosmeticId && x?.type == cosmeticType);
                var hasPlayerCosmetic = playerDataCosmetics.Any(x => x?.id == cosmeticId
                    && (x.cosmeticPurchaseMethod is CosmeticPurchaseMethod.Default // Check if it's a default cosmetic
                    || DateTimeOffset.FromUnixTimeSeconds(x.acquiredTime) < DateTime.UtcNow)); // Check if the cosmetic was acquired before the current time

                // If the cosmetic is defined in the game data and the player has it, update the profile
                if (isCosmeticInGame && hasPlayerCosmetic)
                {
                    // According the type, override the icon id
                    switch (cosmeticType)
                    {
                        case CosmeticType.Icons: playerDataProfile.profileIconID = cosmeticId; break;
                        case CosmeticType.Tiles: playerDataProfile.tileSkinID = cosmeticId; break;
                        case CosmeticType.Boards: playerDataProfile.boardSkinID = cosmeticId; break;
                        case CosmeticType.Fund: playerDataProfile.boardFundSkinID = cosmeticId; break;
                        default: break;
                    }

                    wasUpdate = true;

                    // No need to continue if the cosmetic type is not badge
                    break;
                }

                // But, if the cosmetic type is badge and is not registered yet. Register it
                else if (cosmeticType is CosmeticType.Badges 
                    && !newBadges!.Contains(cosmeticId)

                    // Check if the achievement is already unlocked
                    && playerDataAchievements is not null and { Count: > 0 } 
                    && Enum.TryParse<Achievement>(cosmeticId, out var achievement)
                    && playerDataAchievements.Any(x => x.achievement == achievement))
                {
                    newBadges.Add(cosmeticId);
                    wasUpdate = true;
                }

                else
                    _logger.LogWarning("No cosmetics purchased found for player {PlayerId}.", playerId);
            }

            // If the player is in a club, try to update the profile icon of the member in the club data
            if (cosmeticType is CosmeticType.Icons && wasUpdate)
                await TryToUpdateClubMemberIcon(executionContext, _gameApiClient,
                    playerDataClubName, firebaseID, firebaseIDToken, firebaseRefreshToken, playerDataProfile.profileIconID!);

            // If the cosmetics were badges, replace the ones registered in the profile data
            if (cosmeticType is CosmeticType.Badges && newBadges.Count > 0)
            { 
                playerDataProfile.badgesIDs = [.. newBadges];

                // If the player is in a club, try to update the badges of the member in the club data
                await TryToUpdateClubMemberBadges(executionContext, _gameApiClient,
                    playerDataClubName, firebaseID, firebaseIDToken, firebaseRefreshToken, playerDataProfile.badgesIDs!);
            }

            // Add the updated player profile data to the data to save
            if (wasUpdate)
                dataToSave.Add(profileIDKey, playerDataProfile);
        }

        // Try to save the updated profile to the player's data
        if (wasUpdate)
            await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, dataToSave);

        // Once all tasks are completed, create the response object
        var profileResponse = new ProfileResponse(playerDataProfile);

        // Return the response with the updated profile and game data
        var responseDataJson = JsonConvert.SerializeObject(profileResponse);

        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        var encryptedDataJson = JsonConvert.SerializeObject(SecurityHelper.EncryptData(responseDataJson, derivedKey, derivedIv));
        return encryptedDataJson;
    }

    /// <summary>
    /// Try to update the profile icon of a member in the club data stored in Firestore
    /// </summary>
    private async Task TryToUpdateClubMemberIcon(IExecutionContext executionContext, IGameApiClient gameApiClient,
        string? playerDataClubName, string? firebaseID, string? firebaseIDToken, string? firebaseRefreshToken, string cosmeticId)
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
                // Update the icon of the member in the club data
                var member = clubData.members.FirstOrDefault(x => x.firebaseMemberId == firebaseID);
                if (member != null)
                { 
                    member.profileIconId = cosmeticId;

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
                        _logger.LogInformation($"Club {playerDataClubName} icon updated successfully.");
                    else
                        _logger.LogWarning($"Failed to update club {playerDataClubName} icon.");
                }
                else
                    _logger.LogWarning($"Target member {firebaseID} not found in club data.");
            }
        }
    }

    /// <summary>
    /// Try to update the badges of a member in the club data stored in Firestore
    /// </summary>
    private async Task TryToUpdateClubMemberBadges(IExecutionContext executionContext, IGameApiClient gameApiClient,
        string? playerDataClubName, string? firebaseID, string? firebaseIDToken, string? firebaseRefreshToken, string[]? newBadges)
    {
        _logger.LogInformation("Badges of the user to add: {Badges}", string.Join("\n", newBadges?.Select(x => x ?? "null").ToArray() ?? Array.Empty<string>()));

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
                // Update the badges of the member in the club data
                var member = clubData.members.FirstOrDefault(x => x.firebaseMemberId == firebaseID);
                if (member != null)
                    member.badges = newBadges?.ToArray() ?? [];
                else
                    _logger.LogWarning($"Target member {firebaseID} not found in club data.");

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
                    _logger.LogInformation($"Club {playerDataClubName} badges updated successfully.");
                else
                    _logger.LogWarning($"Failed to update club {playerDataClubName} badges.");
            }
        }
    }
}
