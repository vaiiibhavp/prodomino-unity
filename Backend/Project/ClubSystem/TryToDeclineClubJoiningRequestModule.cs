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
    public class TryToDeclineClubJoiningRequestModule(ILogger<TryToDeclineClubJoiningRequestModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToDeclineClubJoiningRequestModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToDeclineClubJoiningRequest))]
        public async Task<string> TryToDeclineClubJoiningRequest(IExecutionContext executionContext, string parametersEncryptedJson)
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

            if (!data.TryGetValue("toDeclineUserUnityID", out var toDeclineUserUnityIDToken) || toDeclineUserUnityIDToken is not string toDeclineUserUnityID)
                throw new ArgumentException("Missing or invalid user ID");

            var optionalIconID = data.TryGetValue("optionalIconID", out var optionalIconIDObj) && optionalIconIDObj is string optionalIconIDStr ? optionalIconIDStr : null;

            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubIDKey = CloudSaveProperties.Club.ToString();
            var configKey = CloudSaveProperties.Config.ToString();

            // Load global club configuration (Game Data)
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(
                 _gameApiClient, executionContext,
                customID, null, false, configKey);

            var configData = loadGameDataResponse?.FirstOrDefault(x => x?.key == configKey)?.value?.ToObject<ConfigData>();
            if (configData?.clubsConfig?.clubPermissionDatas == null || configData.clubsConfig.clubPermissionDatas.Length == 0)
                throw new UGSException("Invalid or missing Game Data configuration.");

            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null, 
                alternativePlayerID: null, 
                isThrowingException: false,
                firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey, clubIDKey);

            var toDeclinePlayerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null,
                alternativePlayerID: toDeclineUserUnityID, // Load data of the user to add
                isThrowingException: false,
                firebaseIDKey, clubIDKey);

            // Validate current member data
            var memberFirebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var memberClubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();
            var memberFirebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var memberFirebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

            // Validate user to add data
            var toDeclineFirebaseID = toDeclinePlayerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var toDeclineClubID = toDeclinePlayerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubDataResponse();

            if (configData?.clubsConfig?.clubPermissionDatas == null || configData.clubsConfig.clubPermissionDatas.Length == 0)
                throw new UGSException("Invalid or missing Game Data configuration.");

            // Check if the firebase tokens are valid
            if (string.IsNullOrEmpty(memberFirebaseID)
                || string.IsNullOrEmpty(memberFirebaseIDToken)
                || string.IsNullOrEmpty(memberFirebaseRefreshToken))
            {
                _logger.LogWarning("Missing or invalid user data. Ensure you are registered in firebase.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubResponse.message = "Missing or invalid user data. Ensure you are logged in.";
            }

            // Check if the user to add is already in a club or its firebase data is valid
            else if (string.IsNullOrEmpty(toDeclineFirebaseID) || !string.IsNullOrEmpty(toDeclineClubID))
            {
                _logger.LogWarning("The user to decline joining request is already in a club or its firebase data is invalid.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.AlreadyInClub;
                clubResponse.message = "The user is already in a club or no longer has a pending request.";
            }

            // Check if the user is in a club
            else if (string.IsNullOrEmpty(memberClubName))
            {
                _logger.LogWarning("You are not in a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                clubResponse.message = "You are not in a club.";
            }
            
            else
            { 
                // Once every check is done, proceed to add the member to the club
                _logger.LogInformation($"User {memberFirebaseID} is trying to decline joining request of user {toDeclineFirebaseID} to club {memberClubName}");

                // Create the path used in Firestore to get the club data
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                    (memberFirebaseIDToken,
                    memberFirebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: memberClubName,
                    logger: _logger);

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
                    // Log the club data load success
                    _logger.LogInformation($"Club {memberClubName} data loaded successfully.");

                    // Check if the user has rank to add members to the club
                    if (HasPermissiontoDecline(clubData, configData.clubsConfig, memberFirebaseID, toDeclineFirebaseID,
                        ref clubResponse.clubResponseCodeType, ref clubResponse.message))
                    {
                        // Add the new member to the club
                        var searchedApplicant = clubData.applicants.FirstOrDefault(x => x.unityID == toDeclineUserUnityID);
                        if (searchedApplicant != null)
                        {
                            _logger.LogInformation($"Found applicant with Unity ID {toDeclineUserUnityID} in club {memberClubName} applicants list.");
                            clubData.applicants.Remove(searchedApplicant);
                        }
                        else
                            _logger.LogWarning($"Applicant with Unity ID {toDeclineUserUnityID} not found in club {memberClubName} applicants list.");

                        // Build updateFields with the updated members list
                        var updatedFields = new
                        {
                            applicants = FirebaseApiHelper.BuildFirestoreApplicantsArray(clubData.applicants),
                            updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                        };

                        // Update club data in Firestore
                        var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                            executionContext,
                            _gameApiClient,
                            collection: FirebaseApiHelper.clubsCollectionIdKey,
                            documentId: memberClubName,
                            documentFields: updatedFields,
                            updateFieldPaths: ["applicants", "updatedAt"]
                        );

                        // Log the result of the update operation
                        if (!string.IsNullOrEmpty(updateResponse))
                        { 
                            _logger.LogInformation($"Club {memberClubName} declined applicant with id <b>{toDeclineFirebaseID}</b> successfully.");

                            // Fill the response
                            clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                            clubResponse.playerClubData = clubData;
                        }
                        else
                        { 
                            _logger.LogError($"Failed to decline applicant with id {toDeclineFirebaseID} to club {memberClubName}.");

                            // Fill the response
                            clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                            clubResponse.message = "Failed to decline applicant from the club due to an unknown issue.";
                        }
                    } 
                    else
                        _logger.LogWarning($"User {memberFirebaseID} lacks permission to decline new members in the club '{memberClubName}'.");
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
        /// Check if the current user has the rank to add a member to the club
        /// </summary>
        private bool HasPermissiontoDecline(FirestoreClubData clubData, ClubsConfig clubsConfig, string currentMemberFirebaseID, string toDeclinePlayerId, 
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
            if (clubData.members.Any(x => x.firebaseMemberId == toDeclinePlayerId))
            {
                clubResponseCodeType = ClubResponseCodeType.AlreadyInClub;
                message = "The user to add is already a member of the club.";

                _logger.LogWarning("The user to add is already a member of the club.");
                return false;
            }

            // Find the current user in the club members
            var memberData = clubData.members.FirstOrDefault(x => x.firebaseMemberId == currentMemberFirebaseID);
            if (memberData is null)
            {
                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "The current user is not a member of the club.";

                _logger.LogWarning("The current user is not a member of the club.");
                return false;
            }

            var permissions = clubsConfig.clubPermissionDatas
                ?.FirstOrDefault(x => x.clubRanksType == memberData.rank)?.clubPermissionsType
                ?? ClubPermissionsTypes.None;

            // Check if the current user has the permission to decline applicants
            var hasDeclineMemberPermission = permissions.HasFlag(ClubPermissionsTypes.DeclineApplicant);
            if (!hasDeclineMemberPermission)
            { 
                _logger.LogWarning("The current user does not have permission to decline applicants of the club.");

                clubResponseCodeType = ClubResponseCodeType.NoPermission;
                message = "The current user does not have permission to decline applicants of the club.";
                return false;
            }

            // User has permission
            return hasDeclineMemberPermission;
        }
    }
}
