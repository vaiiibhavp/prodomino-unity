using Backend.CustomizationSystem;
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
    public class TryToSendClubJoiningRequestModule(ILogger<TryToSendClubJoiningRequestModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToSendClubJoiningRequestModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToSendClubJoiningRequest))]
        public async Task<string> TryToSendClubJoiningRequest(IExecutionContext executionContext, string parametersEncryptedJson)
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

            if (!data.TryGetValue("toRequestClubName", out var toRequestClubNameToken) || toRequestClubNameToken is not string toRequestClubName)
                throw new ArgumentException("Missing or invalid club ID");
            
            if (!data.TryGetValue("bestLeaderboardId", out var bestLeaderboardIdToken) || bestLeaderboardIdToken is not string bestLeaderboardId)
                throw new ArgumentException("Missing or invalid leaderboard id");

            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubIDKey = CloudSaveProperties.Club.ToString();
            var configKey = CloudSaveProperties.Config.ToString();
            var profileKey = CloudSaveProperties.Profile.ToString();
            var matchKey = CloudSaveProperties.Match.ToString();

            // Load global club configuration (Game Data)
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext, 
                customID, null, false, configKey);

            var configData = loadGameDataResponse?.FirstOrDefault(x => x?.key == configKey)?.value?.ToObject<ConfigData>();
            if (configData?.clubsConfig?.clubPermissionDatas is null or { Length: 0 })
                throw new UGSException("Invalid or missing Game Data configuration.");

            var optionalIconID = data.TryGetValue("optionalIconID", out var optionalIconIDObj) && optionalIconIDObj is string optionalIconIDStr ? optionalIconIDStr : null;

            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null, 
                alternativePlayerID: null, 
                isThrowingException: false,
                firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey, clubIDKey, profileKey, matchKey);

            // Validate user to add data
            var playerProfileData = playerDataResponse?.FirstOrDefault(x => x?.key == profileKey)?.value?.ToObject<PlayerProfileData>();
            var playerMatchData = playerDataResponse?.FirstOrDefault(x => x?.key == matchKey)?.value?.ToObject<PlayerMatchData>();
            var playerFirebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var playerFirebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var playerFirebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();
            var playerClubID = playerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubDataResponse();

            if (configData?.clubsConfig?.clubPermissionDatas == null || configData.clubsConfig.clubPermissionDatas.Length == 0)
                throw new UGSException("Invalid or missing Game Data configuration.");

            // Check if the firebase tokens are valid
            if (string.IsNullOrEmpty(playerFirebaseID)
                || string.IsNullOrEmpty(playerFirebaseIDToken)
                || string.IsNullOrEmpty(playerFirebaseRefreshToken))
            {
                _logger.LogWarning("Missing or invalid user data. Ensure you are registered in firebase and you are logged in");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubResponse.message = "Missing or invalid user data. Ensure you are logged in";
            }

            // Check if the user to add is already in a club or its firebase data is valid
            else if (!string.IsNullOrEmpty(playerClubID))
            {
                _logger.LogWarning("You are already in a club. Couldn't send a joining request.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.AlreadyInClub;
                clubResponse.message = "You are already in a club. Couldn't send a joining request";
            }
            
            else
            { 
                // Once every check is done, proceed to add the member to the club
                _logger.LogInformation($"User {playerFirebaseID} is trying to send a joining request to the club {toRequestClubName}");

                // Create the path used in Firestore to get the club data
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                    (playerFirebaseIDToken,
                    playerFirebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: toRequestClubName,
                    logger: _logger);

                // Check if the club data exists
                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? FirestoreClubData.ParseClubData(clubDataJson)
                    : null;
                if (clubData is null)
                {
                    _logger.LogWarning($"Club {toRequestClubName} does not exist.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    clubResponse.message = "Club not found.";
                }
                else
                {
                    // If the user has already sent a joining request to the expected club, avoid sending another one
                    if (clubData.applicants.Any(x => x.unityID == playerId))
                    {
                        _logger.LogWarning($"User {playerFirebaseID} has already sent a joining request to the club {toRequestClubName}.");

                        // Fill the response
                        clubResponse.clubResponseCodeType = ClubResponseCodeType.AlreadyRequested;
                        clubResponse.message = "You have already sent a joining request to this club.";
                    }

                    // But, if the club data is valid, proceed to add the member as a new applicant
                    else
                    {
                        // Log the club data load success
                        _logger.LogInformation($"Club {toRequestClubName} data loaded successfully.");

                        // Check if the user could send a joining request to the expected club
                        if (HasFreeSpace(clubData, configData.clubsConfig, playerFirebaseID,
                            ref clubResponse.clubResponseCodeType, ref clubResponse.message))
                        {
                            var username = "Unknown";
                            try
                            {
                                // Get the username of the current user for logging purposes
                                var usernameResponse = await UGSApiHelper.GetUsername(executionContext, _logger: _logger);
                                if (!string.IsNullOrEmpty(usernameResponse))
                                    username = FirebaseApiHelper.NormalizeName(usernameResponse);
                                else
                                    _logger.LogWarning("Failed to get the username of the current user.");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to get the username of the current user.");
                            }

                            var bestLeaderboardTier = LeaderboardTier.None;
                            var bestLeaderboardScore = 0;

                            // Search for the best leaderboard score data
                            var bestLeaderboardData = !string.IsNullOrEmpty(bestLeaderboardId) ? await UGSApiHelper.GetPlayerScore(executionContext, bestLeaderboardId) : default;
                            if (bestLeaderboardData is not null and { tier: not null and not "" })
                            {
                                // Try to get the best leaderboard tier
                                bestLeaderboardTier = Enum.TryParse(bestLeaderboardData.tier, out bestLeaderboardTier)
                                    ? bestLeaderboardTier
                                    : LeaderboardTier.None;

                                // Get the best leaderboard score
                                bestLeaderboardScore = (int)bestLeaderboardData.score;
                                _logger.LogInformation($"Best leaderboard score for leaderboard id '{bestLeaderboardId}' is {bestLeaderboardScore} with tier {bestLeaderboardTier}.");
                            } else
                                _logger.LogWarning($"Failed to get the best leaderboard score for leaderboard id '{bestLeaderboardId}'. Setting score to 0.");

                            // Add the new member to the club
                            clubData.applicants.Add(new FirestoreClubData.ApplicantData
                            {
                                firebaseID = playerFirebaseID,
                                unityID = playerId,
                                applicantName = username,
                                profileIconId = optionalIconID ?? playerProfileData?.profileIconID ?? string.Empty,
                                eloRating = playerMatchData?.elo ?? 0,
                                bestLeaderboardTier = bestLeaderboardTier,
                                bestLeaderboardScore = bestLeaderboardScore,
                            });

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
                                documentId: toRequestClubName,
                                documentFields: updatedFields,
                                updateFieldPaths: ["applicants", "updatedAt"]
                            );

                            // Log the result of the update operation
                            if (!string.IsNullOrEmpty(updateResponse))
                            {
                                _logger.LogInformation($"Send joining request from applicant with id <b>{playerFirebaseID}</b> Club {toRequestClubName} successfully.");

                                // Fill the response
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                                clubResponse.playerClubData = clubData;
                            } else
                            {
                                _logger.LogError($"Failed to add member with id {playerFirebaseID} to club {toRequestClubName}.");

                                // Fill the response
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                                clubResponse.message = "Failed to add member to the club due to an unknown issue.";
                            }
                        } else
                            _logger.LogWarning($"User {playerFirebaseID} cannot send a joining request to the club {toRequestClubName}. Reason: {clubResponse.message}");
                    }
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
        private bool HasFreeSpace(FirestoreClubData clubData, ClubsConfig clubsConfig, string playerPlayerId, 
            ref ClubResponseCodeType clubResponseCodeType, ref string message)
        {
            // Validate input data (avoid sending requests to club without member that can accept the request)
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

            // Avoid adding members if the club has reached the maximum number of applicants
            if (clubData.applicants.Count + 1 > clubsConfig.applicantsLimit)
            {
                clubResponseCodeType = ClubResponseCodeType.ClubFull;
                message = "The club has reached the maximum number of applicants.";

                _logger.LogWarning("The club has reached the maximum number of applicants.");
                return false;
            }

            // Check if the club doesn't have the player as member
            if (clubData.members.Any(x => x.firebaseMemberId == playerPlayerId))
            {
                clubResponseCodeType = ClubResponseCodeType.AlreadyInClub;
                message = "The user to send joining request is already a member of the club.";

                _logger.LogWarning("The user to send joining request is already a member of the club.");
                return false;
            }

            // User can send joining request
            return true;
        }
    }
}
