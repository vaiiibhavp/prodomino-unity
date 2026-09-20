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
using static HelperSharedLibrary.ConfigData;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.ClubSystem
{
    public class TryToRemoveMemberToClubModule(ILogger<TryToRemoveMemberToClubModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToRemoveMemberToClubModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToRemoveMemberToClub))]
        public async Task<string> TryToRemoveMemberToClub(IExecutionContext executionContext, string parametersEncryptedJson)
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

            if (!data.TryGetValue("toRemoveUserUnityID", out var toRemoveUserUnityIDToken) || toRemoveUserUnityIDToken is not string toRemoveUserUnityID)
                throw new ArgumentException("Missing or invalid player id");

            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubIDKey = CloudSaveProperties.Club.ToString();
            var configKey = CloudSaveProperties.Config.ToString();
            var analyticsKey = CloudSaveProperties.Analytics.ToString();

            // Load global club configuration (Game Data)
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID, null, false, configKey);

            var configData = loadGameDataResponse
                ?.FirstOrDefault(x => x?.key == configKey)
                ?.value?.ToObject<ConfigData>();

            if (configData?.clubsConfig?.clubPermissionDatas == null || configData.clubsConfig.clubPermissionDatas.Length == 0)
                throw new UGSException("Invalid or missing Game Data configuration.");

            // Load current player data
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                    _gameApiClient, executionContext,
                    customID: null,
                    alternativePlayerID: null,
                    isThrowingException: false,
                    firebaseIDKey,
                    firebaseIDTokenKey,
                    firebaseRefreshTokenKey,
                    clubIDKey);

            var toRemovePlayerDataResponse = await UGSApiHelper.ProtectedLoadData(
                    _gameApiClient, executionContext,
                    customID: null,
                    alternativePlayerID: toRemoveUserUnityID, // Load data of the user to add
                    isThrowingException: false,
                    analyticsKey,
                    firebaseIDKey,
                    firebaseIDTokenKey,
                    clubIDKey);

            // Validate entry data
            var memberFirebaseIDObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var memberFirebaseIDTokenObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var memberFirebaseRefreshTokenObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();
            var memberClubIdObj = playerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();

            var toRemoveFirebaseIDObj = toRemovePlayerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var toRemoveClubIdObj = toRemovePlayerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();
            var toRemoveAnalyticsData = toRemovePlayerDataResponse?.FirstOrDefault(x => x?.key == analyticsKey)?.value?.ToObject<AnalyticsData>();

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubDataResponse();

            // Check if the firebase tokens are valid
            if (memberFirebaseIDObj is not string memberFirebaseID || string.IsNullOrEmpty(memberFirebaseID)
                || memberFirebaseIDTokenObj is not string memberFirebaseIDToken || string.IsNullOrEmpty(memberFirebaseIDToken)
                || memberFirebaseRefreshTokenObj is not string memberFirebaseRefreshToken || string.IsNullOrEmpty(memberFirebaseRefreshToken)
                || memberClubIdObj is not string memberClubName || string.IsNullOrEmpty(memberClubName))
            { 
                _logger.LogWarning("Missing or invalid user data. Ensure you are registered in firebase and you are in a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubResponse.message = "Missing or invalid user data. Ensure you are logged in and you are in a club.";
            }

            // Check if the user to remove exists and is currently assigned to a club
            else if (toRemoveFirebaseIDObj is not string toRemoveFirebaseID || string.IsNullOrEmpty(toRemoveFirebaseID)
                || toRemoveClubIdObj is not string toRemoveClubId || string.IsNullOrEmpty(toRemoveClubId))
            { 
                _logger.LogWarning("The user to remove is not registered in firebase or is not in a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                clubResponse.message = "The user to remove is not in a club";
            }

            // Check if the user is in a club
            else if (string.IsNullOrEmpty(memberClubName))
            {
                _logger.LogWarning("You are not in the club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                clubResponse.message = "You are not in the club.";
            }

            else
            {
                // Once every check is done, proceed to add the member to the club
                _logger.LogInformation($"User {memberFirebaseID} is trying to <b>Remove</b> user {toRemoveFirebaseID} to club {memberClubName}");

                // Create the path used in Firestore to get the club data
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                    (memberFirebaseIDToken,
                    memberFirebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: memberClubName);

                // Check if the club data exists
                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? FirestoreClubData.ParseClubData(clubDataJson)
                    : null;
                if (clubData is null)
                {
                    _logger.LogWarning($"Club {memberClubName} does not exist.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    clubResponse.message = "Club not found.";
                }

                else
                {
                    // Fill the response
                    clubResponse.playerClubData = clubData;

                    // Log the club data load success
                    _logger.LogInformation($"Club {memberClubName} data loaded successfully.");

                    // Check if the user has permissions to remove members to the club
                    if (HasPermissionToRemove(toRemoveAnalyticsData, clubData, configData.clubsConfig, memberFirebaseID, toRemoveFirebaseID,
                        ref clubResponse.clubResponseCodeType, ref clubResponse.message))
                    {
                        var dataToUpdate = new Dictionary<string, object>() {
                            [clubIDKey] = string.Empty
                        };

                        // Update the analytics data of the user to remove
                        if (toRemoveAnalyticsData is not null)
                        { 
                            // Update the last club move date in analytics data to avoid spamming clubs
                            toRemoveAnalyticsData.lastClubMoveDate = DateTime.UtcNow;
                            dataToUpdate[analyticsKey] = toRemoveAnalyticsData;
                        }

                        // Update the club reference in the new member data
                        await UGSApiHelper.ProtectedSaveData(_gameApiClient, executionContext, dataToUpdate, customID: null,
                            alternativePlayerID: toRemoveUserUnityID); // Load data of the user to remove

                        var shouldDestroyClub = false;
                        if (clubData.members.Any(x => x.firebaseMemberId == toRemoveFirebaseID && x.rank is ClubRanksTypes.GuildMaster))
                        {
                            var oldestMember = clubData.members
                                .Where(x => x.firebaseMemberId != toRemoveFirebaseID)
                                .OrderBy(x => x.joinedDate)
                                .FirstOrDefault();

                            if (oldestMember != null)
                            {
                                oldestMember.rank = ClubRanksTypes.GuildMaster;
                                _logger.LogInformation($"The member {oldestMember.firebaseMemberId} is now the new Guild Master of the club {memberClubName}.");
                            } 
                            else
                            { 
                                _logger.LogWarning($"The club {memberClubName} has no more members after removing the Guild Master {toRemoveFirebaseID}. The club will have no Guild Master.");
                                shouldDestroyClub = true;
                            }
                        }

                        // Remove the new member to the club
                        clubData.members.RemoveAll(x => x.firebaseMemberId == toRemoveFirebaseID);

                        // Check if the club should be destroyed (no more members)
                        if (!shouldDestroyClub)
                        { 
                            // Build updateFields with the updated members list
                            var updatedFields = new
                            {
                                members = FirebaseApiHelper.BuildFirestoreMembersArray(clubData.members),
                                updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                            };

                            // Update club data in Firestore
                            var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                                executionContext,
                                _gameApiClient,
                                collection: FirebaseApiHelper.clubsCollectionIdKey,
                                documentId: memberClubName,
                                documentFields: updatedFields,
                                updateFieldPaths: ["members", "updatedAt"]
                            );

                            // Log the result of the update operation
                            if (!string.IsNullOrEmpty(updateResponse))
                            { 
                                _logger.LogInformation($"Club {memberClubName} removed member with id {toRemoveFirebaseID} successfully.");
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;

                                if (memberFirebaseID == toRemoveFirebaseID)
                                {
                                    // If the user removed himself, update the response to reflect that he is no longer in the club
                                    clubResponse.playerClubData = null;
                                    _logger.LogInformation($"User {memberFirebaseID} removed himself from the club {memberClubName}.");
                                }
                            }
                            else
                                _logger.LogError($"Failed to remove member with id {toRemoveFirebaseID} to club {memberClubName}.");
                        }

                        // If the club has no more members, delete it
                        else
                        {
                            // Delete the club document from Firestore
                            var deleteResponse = await FirebaseApiHelper.DeleteDocumentAsync(
                                executionContext,
                                _gameApiClient,
                                collection: FirebaseApiHelper.clubsCollectionIdKey,
                                documentId: memberClubName
                            );

                            // Trying to delete chats record
                            var deleteChatResponse = await FirebaseApiHelper.DeleteDocumentAsync(
                                executionContext,
                                _gameApiClient,
                                collection: FirebaseApiHelper.clubsChatsCollectionIdKey,
                                documentId: memberClubName
                            );

                            // Log the result of the delete operation
                            if (deleteResponse && deleteChatResponse)
                            {
                                _logger.LogInformation($"Club {memberClubName} has been deleted as it has no more members after removing user {toRemoveFirebaseID}.");

                                // Update the response to reflect that the user is no longer in a club
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                                clubResponse.playerClubData = null;
                                clubResponse.message = "The club has been deleted as it has no more members.";

                                // Once the club was removed, fetch the entire collection according the new ranks
                                await TryToFetchClubsRank(memberFirebaseIDToken, memberFirebaseRefreshToken, _logger);
                            } 
                            else if (deleteResponse)
                            {
                                _logger.LogWarning($"Club {memberClubName} has been deleted, but the chat record still active.");

                                // Update the response to reflect that the user is no longer in a club
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                                clubResponse.playerClubData = null;
                                clubResponse.message = "The club has been deleted successfully.";

                                // Once the club was removed, fetch the entire collection according the new ranks
                                await TryToFetchClubsRank(memberFirebaseIDToken, memberFirebaseRefreshToken, _logger);
                            }
                            else
                            {
                                _logger.LogError($"Failed to delete club {memberClubName} after removing user {toRemoveFirebaseID}.");

                                clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                                clubResponse.message = $"Failed to delete club {memberClubName} after removing each member.";
                            }
                        }
                    } 
                    else
                        _logger.LogWarning($"User {memberFirebaseID} lacks permission to remove members from the club '{memberClubName}'.");
                }
            }

            // Derived key/iv for encrypting response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            // Build response and encrypt it even if there was an error. The client needs to decrypt it to read the error.
            var clubDataResponseJson = JsonConvert.SerializeObject(clubResponse);
            var encryptedResponse = SecurityHelper.EncryptData(clubDataResponseJson, derivedKey, derivedIv);
            return JsonConvert.SerializeObject(encryptedResponse);

            async Task<bool> TryToFetchClubsRank(string idToken, string refreshToken, ILogger logger)
            {
                try
                {
                    // Step 1: Get all clubs from Firestore
                    var clubsJson = await FirebaseApiHelper.GetCollectionAsync(
                        collection: FirebaseApiHelper.clubsCollectionIdKey,
                        idToken: idToken,
                        refreshToken: refreshToken,
                        logger: logger);

                    if (string.IsNullOrEmpty(clubsJson))
                    {
                        logger.LogWarning("No clubs found.");
                        return false;
                    }

                    var clubsArray = JArray.Parse(clubsJson);
                    if (clubsArray.Count == 0)
                    {
                        logger.LogWarning("Empty club list, skipping rank recalculation.");
                        return false;
                    }

                    // Step 2: Build list of club data
                    var clubList = new List<(string name, int score)>();
                    foreach (var club in clubsArray)
                    {
                        var name = club["name"]?.ToString()?.Split('/').LastOrDefault() ?? "Unknown";
                        var scoreStr = club["fields"]?["score"]?["integerValue"]?.ToString() ?? "0";
                        int.TryParse(scoreStr, out var score);
                        clubList.Add((name, score));
                    }

                    // Step 3: Sort by score descending
                    var ordered = clubList.OrderByDescending(c => c.score).ToList();

                    // Step 4: Reassign clubRank and update Firestore
                    int updated = 0;
                    for (int i = 0; i < ordered.Count; i++)
                    {
                        var clubName = ordered[i].name;
                        var newRank = i + 1;

                        var fields = new
                        {
                            clubRank = new { integerValue = newRank.ToString() },
                            updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                        };

                        var result = await FirebaseApiHelper.PatchDocumentAsync(
                            executionContext,
                            _gameApiClient,
                            FirebaseApiHelper.clubsCollectionIdKey,
                            clubName,
                            fields,
                            updateFieldPaths: ["clubRank", "updatedAt"]
                        );

                        if (!string.IsNullOrEmpty(result))
                            updated++;
                    }

                    logger.LogInformation($"Successfully updated {updated} clubs.");
                    return true;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error recalculating club ranks: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Check if the current user has the permissions to remove a member to the club
        /// </summary>
        private bool HasPermissionToRemove(AnalyticsData? analyticsData, FirestoreClubData clubData, ClubsConfig clubsConfig, string currentMemberFirebaseID, string toRemovePlayerId,
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

            // Check if the user data is valid
            if (string.IsNullOrEmpty(currentMemberFirebaseID) || string.IsNullOrEmpty(toRemovePlayerId))
            {
                clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                message = "Invalid or missing user data.";
                _logger.LogWarning("Invalid or missing user data.");
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

            // Check if the current user is trying to remove himself from the club. This operation is always allowed.
            // Other checks are skipped in this case.
            if (currentMemberFirebaseID == toRemovePlayerId)
            {
                var minDaysRequiredToLeaveAClub = clubsConfig.minDaysRequiredToLeaveAClub;

                // Check if the analytics data is valid
                if (analyticsData is null)
                    _logger.LogWarning("Analytics data is null. Cannot validate last club move date. Permiting to leave the club.");

                // Validate last club creation date to avoid spamming clubs
                else if ((DateTime.UtcNow.Date - analyticsData.lastClubMoveDate.Date).TotalDays < minDaysRequiredToLeaveAClub)
                {
                    var leftingDays = minDaysRequiredToLeaveAClub - (int)(DateTime.UtcNow.Date - analyticsData.lastClubMoveDate.Date).TotalDays;
                    clubResponseCodeType = ClubResponseCodeType.NoPermission;
                    message = $"You cannot leave the club yet. You need to wait {leftingDays} more day(s) to leave the club.";
                    _logger.LogWarning("The user cannot leave the club yet. Minimum days required to leave a club not met. \n" +
                        $"Last club move date: {analyticsData.lastClubMoveDate}, " +
                        $"Days since last club move: {(DateTime.UtcNow.Date - analyticsData.lastClubMoveDate.Date).TotalDays}, " +
                        $"Minimum days required to leave a club: {minDaysRequiredToLeaveAClub}");
                    return false;
                }
                else
                    _logger.LogInformation("Analytics data is valid. Permiting to leave the club.\n" +
                        $"Last club move date: {analyticsData.lastClubMoveDate}, " +
                        $"Days since last club move: {(DateTime.UtcNow.Date - analyticsData.lastClubMoveDate.Date).TotalDays}, " +
                        $"Minimum days required to leave a club: {minDaysRequiredToLeaveAClub}");

                _logger.LogInformation("The current user is trying to remove himself from the club. Operation allowed.");
                return true;
            }

            // Check if the club doesn't have the player as member
            if (!clubData.members.Any(x => x.firebaseMemberId == toRemovePlayerId))
            {
                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "The user to remove is not in the club.";

                _logger.LogWarning("The user to remove is not a member of the club.");
                return false;
            }

            // Find the current user in the club members
            var memberData = clubData.members.FirstOrDefault(x => x.firebaseMemberId == currentMemberFirebaseID);
            if (memberData is null)
            {
                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "You are not a member of the club.";

                _logger.LogWarning("The current user is not a member of the club.");
                return false;
            }

            var permissions = clubsConfig.clubPermissionDatas
                ?.FirstOrDefault(x => x.clubRanksType == memberData.rank)?.clubPermissionsType
                ?? ClubPermissionsTypes.None;

            var hasRemoveMemberPermission = permissions.HasFlag(ClubPermissionsTypes.RemoveMember);
            if (!hasRemoveMemberPermission)
            { 
                _logger.LogWarning($"The current user with rank {memberData.rank} does not have permission to remove members from the club.");
                
                clubResponseCodeType = ClubResponseCodeType.NoPermission;
                message = "The current user does not have permission to remove members from the club.";
                return false;
            }

            // Check if the current user has the permission to accept members
            return hasRemoveMemberPermission;
        }
    }
}
