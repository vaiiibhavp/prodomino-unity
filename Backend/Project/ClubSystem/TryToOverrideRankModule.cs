using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.ClubSystem
{
    public class TryToOverrideRankModule(ILogger<TryToOverrideRankModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToOverrideRankModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToOverrideRank))]
        public async Task<string> TryToOverrideRank(IExecutionContext executionContext, string parametersEncryptedJson)
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

            // Extract the target player and new rank to apply
            if (!data.TryGetValue("toChangeRankID", out var toChangeRankIDToken) || toChangeRankIDToken is not string toChangeRankID)
                throw new ArgumentException("Missing or invalid player ID");

            if (!data.TryGetValue("newRank", out var newRankObj)
                || newRankObj is null
                || newRankObj.ToString() is not string newRankStr
                || string.IsNullOrEmpty(newRankStr)
                || !Enum.TryParse(newRankStr, out ClubRanksTypes newRank))
                throw new ArgumentException("Missing or invalid rank value");

            // Define Cloud Save keys to load Firebase and club data
            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubNameKey = CloudSaveProperties.Club.ToString();
            var configKey = CloudSaveProperties.Config.ToString();

            // Load global club configuration (Game Data)
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext, 
                customID, null, false, configKey);

            var configData = loadGameDataResponse
                ?.FirstOrDefault(x => x?.key == configKey)
                ?.value?.ToObject<ConfigData>();

            if (configData?.clubsConfig?.clubPermissionDatas == null || configData.clubsConfig.clubPermissionDatas.Length == 0)
                throw new UGSException("Invalid or missing Game Data configuration.");

            // Load current player Firebase data
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext, 
                null, null, false,
                firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey, clubNameKey);

            // Load target player Firebase data (whose rank will be changed)
            var toChangeRankDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext, 
                null, toChangeRankID, false,
                firebaseIDKey, firebaseIDTokenKey, clubNameKey);

            // Extract data for current and target players
            var memberFirebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var memberFirebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var memberFirebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();
            var memberClubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubNameKey)?.value?.ToObject<string>();

            var targetFirebaseID = toChangeRankDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var targetClubName = toChangeRankDataResponse?.FirstOrDefault(x => x?.key == clubNameKey)?.value?.ToObject<string>();

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubDataResponse();

            // Validate essential data
            if (string.IsNullOrEmpty(memberFirebaseID) ||
                string.IsNullOrEmpty(memberFirebaseIDToken) ||
                string.IsNullOrEmpty(memberFirebaseRefreshToken) ||
                string.IsNullOrEmpty(memberClubName))
            {
                _logger.LogWarning("Missing or invalid current user data. Ensure you are properly registered and part of a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubResponse.message = "Missing or invalid user data. Ensure you are logged in and you are in a club.";
            }

            else if (string.IsNullOrEmpty(targetFirebaseID) || string.IsNullOrEmpty(targetClubName))
            {
                _logger.LogWarning("Target player data is invalid or not part of a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                clubResponse.message = "The user to add is not in a club";
            }

            else if (memberClubName != targetClubName)
            {
                _logger.LogWarning("Both users must belong to the same club to perform a rank override.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                clubResponse.message = "Both users must belong to the same club to perform a rank override.";
            }

            else
            { 
                _logger.LogInformation($"User {memberFirebaseID} is attempting to override the rank of {targetFirebaseID} to {newRank}.");

                // Fetch Firestore club document
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync(
                    memberFirebaseIDToken,
                    memberFirebaseRefreshToken,
                    FirebaseApiHelper.clubsCollectionIdKey,
                    memberClubName,
                    _logger);

                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? FirestoreClubData.ParseClubData(clubDataJson)
                    : null;

                if (clubData is null)
                {
                    _logger.LogWarning($"Club '{memberClubName}' not found in Firestore.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    clubResponse.message = "Club not found.";
                }

                else
                {
                    // Log the club data load success
                    _logger.LogInformation($"Club data for '{memberClubName}' loaded successfully.");

                    // Check permission before proceeding
                    if (HasPermissionsToOverrideRank(clubData, configData.clubsConfig, memberFirebaseID, targetFirebaseID, newRank,
                        ref clubResponse.clubResponseCodeType, ref clubResponse.message))
                    {
                        // Update the rank of the target member (indirect modification of the array)
                        var memberToOverrideRank = clubData.members.FirstOrDefault(x => x.firebaseMemberId == targetFirebaseID);
                        if (memberToOverrideRank != null)
                        { 
                            memberToOverrideRank.rank = newRank;
                        
                            // Build Firestore update payload (replaces entire array)
                            var updatedFields = new
                            {
                                members = FirebaseApiHelper.BuildFirestoreMembersArray(clubData.members),
                                updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                            };

                            // Send update request to Firestore
                            var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                                executionContext,
                                _gameApiClient,
                                FirebaseApiHelper.clubsCollectionIdKey,
                                memberClubName,
                                updatedFields,
                                updateFieldPaths: ["members", "updatedAt"]
                            );

                            if (!string.IsNullOrEmpty(updateResponse))
                            { 
                                _logger.LogInformation($"Successfully updated rank of {targetFirebaseID} in club '{memberClubName}'.");

                                // Fill the response
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                                clubResponse.playerClubData = clubData;
                            }
                            else
                            { 
                                _logger.LogError($"Failed to update rank of {targetFirebaseID} in club '{memberClubName}'.");
                                
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                                clubResponse.message = "Failed to update the member's rank due to an unknown issue.";
                            }
                        }
                        else
                        { 
                            _logger.LogWarning($"Target member {targetFirebaseID} not found in club data.");
                            
                            clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                            clubResponse.message = "The target member is not found in the club.";
                        }
                    } 
                    else
                        _logger.LogWarning($"User {memberFirebaseID} lacks permission to override ranks in club '{memberClubName}'.");
                }
            }


            // Derived key/iv for encrypting response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            // Build response
            var clubDataResponseJson = JsonConvert.SerializeObject(clubResponse);
            var encryptedResponse = SecurityHelper.EncryptData(clubDataResponseJson, derivedKey, derivedIv);
            return JsonConvert.SerializeObject(encryptedResponse);
        }

        /// <summary>
        /// Determines whether the current user has the right to override another member's rank.
        /// </summary>
        private bool HasPermissionsToOverrideRank(FirestoreClubData clubData, ClubsConfig clubsConfig, string currentMemberFirebaseID, string targetFirebaseID, ClubRanksTypes newRank,
            ref ClubResponseCodeType clubResponseCodeType, ref string message)
        {
            // Validate input data
            if (clubData is null or { members: null or { Count: 0 } })
            {
                clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                message = "Invalid or missing club data.";

                _logger.LogWarning("Invalid or missing club data.");
                return false;
            }

            // Check if the club configuration is valid
            if (clubsConfig is null or { clubPermissionDatas: null or { Length: 0 } })
            {
                clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                message = "Invalid or missing club configuration.";

                _logger.LogWarning("Invalid or missing club configuration.");
                return false;
            }

            // Check if the club doesn't have the player as member
            if (!clubData.members.Any(x => x.firebaseMemberId == targetFirebaseID))
            {
                _logger.LogWarning("Target user is not part of the club.");

                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "The user to change rank is not a member of the club.";

                _logger.LogWarning("The user to change rank is not a member of the club.");
                return false;
            }

            // Find the current user in the club members
            var currentMember = clubData.members.FirstOrDefault(x => x.firebaseMemberId == currentMemberFirebaseID);
            if (currentMember == null)
            {
                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "You are not a member of the club.";

                _logger.LogWarning("The current user is not a member of the club.");
                return false;
            }

            var targetMember = clubData.members.FirstOrDefault(x => x.firebaseMemberId == targetFirebaseID);
            if (targetMember == null)
            {
                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "The target member is not found in the club.";

                _logger.LogWarning("Target member not found in the club.");
                return false;
            }

            // Prevent unnecessary changes
            if (newRank == targetMember.rank)
            {
                clubResponseCodeType = ClubResponseCodeType.InvalidRank;
                message = "The new rank is the same as the target member's current rank. No change needed.";

                _logger.LogWarning("The new rank is the same as the target member's current rank. No change needed.");
                return false;
            }

            // Additional check: Prevent users from changing their own rank
            if (currentMemberFirebaseID == targetFirebaseID)
            {
                clubResponseCodeType = ClubResponseCodeType.NoPermission;
                message = "Users cannot change their own rank.";

                _logger.LogWarning("Users cannot change their own rank.");
                return false;
            }

            // Additional check: Prevent changing the rank of someone with a higher rank than the current user
            if (currentMember.rank < targetMember.rank)
            {
                clubResponseCodeType = ClubResponseCodeType.NoPermission;
                message = "Cannot change the rank of a member with a higher rank than the current user.";

                _logger.LogWarning("Cannot change the rank of a member with a higher rank than the current user.");
                return false;
            }

            // Additional check: Prevent downgrading someone to a rank equal to or higher than the current user's rank
            if (newRank >= currentMember.rank)
            {
                clubResponseCodeType = ClubResponseCodeType.InvalidRank;
                message = "Cannot set the target member's rank to be equal to or higher than the current user's rank.";

                _logger.LogWarning("Cannot set the target member's rank to be equal to or higher than the current user's rank.");
                return false;
            }

            var permissions = clubsConfig.clubPermissionDatas
                ?.FirstOrDefault(x => x.clubRanksType == currentMember.rank)?.clubPermissionsType
                ?? ClubPermissionsTypes.None;

            // Check if the current user has the permission to override members rank
            var hasOverrideRankPermission = permissions.HasFlag(ClubPermissionsTypes.OverrideRank);
            if (!hasOverrideRankPermission)
            { 
                _logger.LogWarning("Current user lacks OverrideRank permission.");
                
                clubResponseCodeType = ClubResponseCodeType.NoPermission;
                message = "The current user does not have permission to change members rank.";
            }

            // Check if the current rank allows overriding others
            return hasOverrideRankPermission;
        }
    }
}
