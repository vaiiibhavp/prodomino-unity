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
    public class TryToUpdateClubDataModule(ILogger<TryToUpdateClubDataModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToUpdateClubDataModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToUpdateClubData))]
        public async Task<string> TryToUpdateClubData(IExecutionContext executionContext, string parametersEncryptedJson)
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


            var isCreatingClub = data.TryGetValue("isCreatingClub", out var isCreatingClubToken) && isCreatingClubToken is bool isCreatingClubBool
                ? isCreatingClubBool
                : false;
            
            var newClubName = data.TryGetValue("clubName", out var newClubNameToken) && newClubNameToken is string newClubNameString
                ? newClubNameString
                : string.Empty;
            
            var selectedSlogan = data.TryGetValue("selectedSlogan", out var selectedSloganToken) && selectedSloganToken is string selectedSloganString 
                ? selectedSloganString 
                : string.Empty;

            // Try to Parse selectedClubIconJson
            var selectedClubIcon = default(FirestoreClubData.IconData);
            if (data.TryGetValue("selectedClubIcon", out var iconToken) && iconToken is JToken token)
            {
                try
                {
                    selectedClubIcon = token.ToObject<FirestoreClubData.IconData>();
                }
                catch (Exception ex)
                {
                    throw new Exception("Failed to parse selectedClubIconJson.", ex);
                }
            }

            var optionalIconID = data.TryGetValue("optionalIconID", out var optionalIconIDObj) && optionalIconIDObj is string optionalIconIDStr ? optionalIconIDStr : null;

            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubIDKey = CloudSaveProperties.Club.ToString();
            var configKey = CloudSaveProperties.Config.ToString();
            var profileKey = CloudSaveProperties.Profile.ToString();
            var analyticsKey = CloudSaveProperties.Analytics.ToString();

            // Load global club configuration (Game Data)
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
                customID, null, false, configKey);

            var configData = loadGameDataResponse
                ?.FirstOrDefault(x => x?.key == configKey)
                ?.value?.ToObject<ConfigData>();

            if (configData?.clubsConfig?.clubPermissionDatas == null || configData.clubsConfig.clubPermissionDatas.Length == 0)
                throw new UGSException("Invalid or missing Game Data configuration.");

            // Load current player data
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext, 
                customID: null, alternativePlayerID: null, isThrowingException: false, 
                firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey,
                clubIDKey, profileKey, analyticsKey);

            // Validate entry data
            var memberFirebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var memberFirebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var memberFirebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();
            var memberClubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubIDKey)?.value?.ToObject<string>();

            var profileData = playerDataResponse
                ?.FirstOrDefault(x => x?.key == profileKey)
                ?.value?.ToObject<PlayerProfileData>();

            var analyticsData = playerDataResponse
                ?.FirstOrDefault(x => x?.key == analyticsKey)
                ?.value?.ToObject<AnalyticsData>();

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubDataResponse();

            // Check if the firebase tokens are valid
            if (string.IsNullOrEmpty(memberFirebaseID)
                || string.IsNullOrEmpty(memberFirebaseIDToken)
                || string.IsNullOrEmpty(memberFirebaseRefreshToken))
            { 
                _logger.LogWarning("Missing or invalid user data. Ensure you are registered in firebase and you are in a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubResponse.message = "Missing or invalid user data. Ensure you are logged in and you are in a club.";
            }

            else
            { 
                // Try to get the club name
                var currentClubName = !string.IsNullOrEmpty(memberClubName) ? memberClubName : null;
            
                // If the user is not creating a club, check if they are in one before updating data
                if (!isCreatingClub && string.IsNullOrEmpty(currentClubName))
                {
                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                    clubResponse.message = "You are not in a club and you are not creating one. Join or create a club to update its data.";

                    _logger.LogWarning("You are not in a club and you are not creating one. Join or create a club to update its data.");
                }
                else
                {
                    // Initialie the banned words validator
                    if (await TryToInitializaProfanityValidator())
                    {
                        var bannedNameReference = string.Empty;
                        var bannedSloganReference = string.Empty;

                        var isNameBanned = !string.IsNullOrEmpty(newClubName) ? CloudCodeProfanityValidator.ContainsProfanity(newClubName, out bannedNameReference) : false;
                        var isSloganBanned = !string.IsNullOrEmpty(selectedSlogan) ? CloudCodeProfanityValidator.ContainsProfanity(selectedSlogan, out bannedSloganReference) : false;
                        if (!isNameBanned && !isSloganBanned)
                        { 
                            if (!string.IsNullOrEmpty(currentClubName))
                            {
                                // Once every check is done, proceed to update the club data
                                _logger.LogInformation($"User {memberFirebaseID} is trying to UPDATE club {currentClubName} data.");

                                await UpdateClubData(currentClubName);
                            }
                            else if (isCreatingClub)
                            {
                                // Once every check is done, proceed to update the club data
                                _logger.LogInformation($"User {memberFirebaseID} is trying to CREATE club but its name is null or empty");

                                await CreateClubData();
                            }
                            else
                            { 
                                _logger.LogWarning("You are not in a club and you are not creating one. Join or create a club to update its data.");

                                // Fill the response
                                clubResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                                clubResponse.message = "You are not in a club and you are not creating one. Join or create a club to update its data.";
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Couldn't create or update the club because the new name or slogan are banned words");

                            // Fill the response
                            clubResponse.clubResponseCodeType = isNameBanned ? ClubResponseCodeType.InvalidName : ClubResponseCodeType.InvalidSlogan;
                            clubResponse.message = $"Invalid {(isNameBanned ? $"Name ({bannedNameReference})" : $"Slogan ({bannedSloganReference})")}";
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Couldn't initialize the profanity words validator before updating or creating a club.");

                        // Fill the response
                        clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                        clubResponse.message = $"Unknow issue {(isCreatingClub ? "creating a" : "updating the")} club";
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

            // Get the word validator from remote config
            async Task<bool> TryToInitializaProfanityValidator()
            {
                try
                {
                    var profanityValidator = new CloudCodeProfanityValidator(_gameApiClient, executionContext, _logger);
                    await profanityValidator.Initialize();
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to initialize profanity words validator\n\nMessage: {ex.Message}");
                    return false;
                }
            }

            // Update the club data that is registered in the user's cloud save and firestore
            async Task CreateClubData()
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

                // Check if the plauer username is valid
                if (string.IsNullOrEmpty(username))
                {
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                    clubResponse.message = "Couldn't obtain required user information to create the club";
                    return;
                }

                // Validate days streaked to create a club
                var minDaysStreakToCreateClub = configData.clubsConfig.minDaysStreakToCreateClub;

                // Check if the analytics data is valid
                if (analyticsData is null)
                    _logger.LogWarning("Analytics data is null. Cannot validate last club move date. Permiting to leave the club.");

                // Validate last club creation date to avoid spamming clubs
                else if (analyticsData.daysStreaked < minDaysStreakToCreateClub)
                {
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.NoPermission;
                    clubResponse.message = $"You need to have at least {minDaysStreakToCreateClub} days streaked to create a club. You currently have {analyticsData?.daysStreaked ?? 0} days streaked.";
                    return;
                }

                // Validate last club creation date to avoid spamming clubs
                var minDaysBetweenClubCreation = configData?.clubsConfig?.minDaysBetweenClubCreation ?? 0;
                if (analyticsData?.lastClubMoveDate != null
                    && (DateTime.UtcNow - analyticsData.lastClubMoveDate).TotalDays < minDaysBetweenClubCreation)
                {
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.NoPermission;

                    var leftDays = minDaysBetweenClubCreation - (DateTime.UtcNow - analyticsData.lastClubMoveDate).TotalDays;
                    clubResponse.message = $"You need to wait {Math.Ceiling(leftDays)} more day(s) before creating a new club";
                    return;
                }

                var normalizedName = UGSApiHelper.NormalizeName(newClubName) ?? string.Empty;
                var searchTokens = UGSApiHelper.GenerateSearchTokens(newClubName) ?? [];

                // Get current total of clubs
                var totalClubs = await FirebaseApiHelper.GetCollectionCountAsync(
                    memberFirebaseIDToken,
                    memberFirebaseRefreshToken,
                    FirebaseApiHelper.clubsCollectionIdKey,
                    _logger);

                // Assign rank = total + 1
                var newRank = totalClubs + 1;
                _logger.LogInformation($"Assigning initial club rank: {newRank}");

                // Check if the club data exists
                var newClubData = new FirestoreClubData
                    (clubName: newClubName,
                    normalizedName: normalizedName,
                    score: 0, 
                    iconData: selectedClubIcon, 
                    slogan: selectedSlogan,
                    members: [new
                        (firebaseMemberId: memberFirebaseID, 
                        unityMemberId: playerId!, 
                        memberName: username,
                        profileIconId: optionalIconID ?? profileData?.profileIconID ?? string.Empty,
                        badges: profileData?.badgesIDs?.Where(x => !string.IsNullOrEmpty(x))?.ToArray() ?? [], 
                        totalAchievements: analyticsData?.achievementsClaimedCount ?? 0, 
                        rank: ClubRanksTypes.GuildMaster )],
                    clubRank: newRank,
                    searchTokens: searchTokens.ToArray());

                // Create a new document usign firestore payload format
                var newDocument = new
                {
                    clubName = new { stringValue = newClubName },
                    normalizedName = new { stringValue = normalizedName },
                    iconData = FirebaseApiHelper.BuildFirestoreIconData(newClubData),
                    slogan = new { stringValue = newClubData.slogan ?? string.Empty },
                    score = new { integerValue = newClubData.score.ToString() },
                    clubRank = new { integerValue = newClubData.clubRank.ToString() },
                    updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") },
                    creationDate = new { timestampValue = DateTime.UtcNow.ToString("o") },
                    members = FirebaseApiHelper.BuildFirestoreMembersArray(newClubData.members),
                    applicants = FirebaseApiHelper.BuildFirestoreApplicantsArray(newClubData.applicants),
                    searchTokens = new
                    {
                        arrayValue = new
                        {
                            values = searchTokens.Select(t => new { stringValue = t }).ToArray()
                        }
                    },
                };

                _logger.LogInformation($"Creating club {newClubName}...");

                // Create club data in Firestore
                var createClubResponse = await FirebaseApiHelper.CreateDocumentAsync(
                    executionContext,
                    _gameApiClient,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentFields: newDocument,
                    documentId: newClubName
                );

                // Log the result of the create operation
                if (!string.IsNullOrEmpty(createClubResponse))
                {
                    // Register himself as the club owner in his cloud save
                    await UGSApiHelper.ProtectedSaveData(
                        _gameApiClient, executionContext,
                        new()
                        {
                            [clubIDKey] = newClubName
                        });

                    _logger.LogInformation($"Club {newClubName} data CREATED successfully and saved in user cloud save.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                    clubResponse.playerClubData = newClubData;
                } 
                else
                {
                    _logger.LogError($"Failed to CREATE club {newClubName} data .");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                    clubResponse.message = "Unknown error creating the club, try again later";
                }
            }

            // Update the club data that is registered in the user's cloud save and firestore
            async Task UpdateClubData(string currentClubName)
            {
                // Create the path used in Firestore to get the club data
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                    (memberFirebaseIDToken,
                    memberFirebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: currentClubName,
                    logger: _logger);

                // Check if the club data exists
                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? FirestoreClubData.ParseClubData(clubDataJson)
                    : null;
                if (clubData is null)
                {
                    _logger.LogWarning($"Club {currentClubName} does not exist.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    clubResponse.message = $"Club {currentClubName} does not exist.";
                    return;

                } 

                // Log the club data load success
                _logger.LogInformation($"Club {currentClubName} data loaded successfully.");

                var iconDataToFirestore = default(object);
                var sloganToFirestore = default(object);

                // Update icon if provided
                var wasIconUpdated = false;
                if (selectedClubIcon is not null)
                {
                    // Check if the user has permissions to update the icon
                    if (HasPermissionsToUpdateData(clubData, configData.clubsConfig, memberFirebaseID, ClubUpdateType.Icon,
                        ref clubResponse.clubResponseCodeType, ref clubResponse.message))
                    {
                        wasIconUpdated = true;

                        // Update the club icon
                        clubData.iconData = selectedClubIcon;
                        _logger.LogInformation($"Club {currentClubName} icon updated successfully.");

                        iconDataToFirestore = new
                        {
                            iconData = FirebaseApiHelper.BuildFirestoreIconData(clubData)
                        };
                    } 
                    else
                    { 
                        _logger.LogWarning("The user does not have permissions to update the club icon.");
                        return;
                    }
                }

                // Update slogan if provided
                var wasSloganUpdated = false;
                if (!string.IsNullOrEmpty(selectedSlogan))
                {
                    // Check if the user has permissions to update the slogan
                    if (HasPermissionsToUpdateData(clubData, configData.clubsConfig, memberFirebaseID, ClubUpdateType.Slogan,
                        ref clubResponse.clubResponseCodeType, ref clubResponse.message))
                    {
                        wasSloganUpdated = true;

                        // Update the club slogan
                        clubData.slogan = selectedSlogan;
                        _logger.LogInformation($"Club {currentClubName} slogan updated successfully.");

                        sloganToFirestore = new
                        {
                            slogan = new { stringValue = clubData.slogan }
                        };
                    } 
                    else
                    { 
                        _logger.LogWarning("The user does not have permissions to update the club slogan.");
                        return;
                    }
                }

                // Merge updates correctly into Firestore fields object
                var updatedFields = new Dictionary<string, object>();

                // Add or update the iconData field
                if (wasIconUpdated && iconDataToFirestore is not null)
                    foreach (var prop in iconDataToFirestore.GetType().GetProperties())
                    {
                        var value = prop.GetValue(iconDataToFirestore);
                        if (value != null)
                            updatedFields[prop.Name] = value;
                    }

                // Add or update the slogan field
                if (wasSloganUpdated && sloganToFirestore is not null)
                    foreach (var prop in sloganToFirestore.GetType().GetProperties())
                    {
                        var value = prop.GetValue(sloganToFirestore);
                        if (value != null)
                            updatedFields[prop.Name] = value;
                    }

                // Modify the update time
                updatedFields["updatedAt"] = new { timestampValue = DateTime.UtcNow.ToString("o") };

                // Build update mask dynamically
                var updateFieldPaths = updatedFields.Keys.ToList();
                if (!updateFieldPaths.Contains("updatedAt"))
                    updateFieldPaths.Add("updatedAt");

                // Update club data in Firestore
                var updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                    executionContext,
                    _gameApiClient,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    documentId: currentClubName,
                    documentFields: updatedFields, // correct Firestore JSON,
                    updateFieldPaths: [.. updateFieldPaths]
                );

                // Log the result of the update operation
                if (!string.IsNullOrEmpty(updateResponse))
                { 
                    _logger.LogInformation($"Club {currentClubName} data UPDATED successfully.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                    clubResponse.playerClubData = clubData;
                }

                else
                {
                    _logger.LogError($"Failed to UPDATE club {currentClubName} data.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                    clubResponse.message = "Unknown error updating the club, try again later";
                }
            }
        }

        /// <summary>
        /// Check if the current user has the permissions to update club data
        /// </summary>
        private bool HasPermissionsToUpdateData(FirestoreClubData clubData, ClubsConfig clubsConfig, string currentMemberFirebaseID, ClubUpdateType clubUpdateType,
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

            // Find the current user in the club members
            var memberData = clubData.members.FirstOrDefault(x => x.firebaseMemberId == currentMemberFirebaseID);
            if (memberData is null)
            {
                clubResponseCodeType = ClubResponseCodeType.NotInClub;
                message = "The The current user is not a member of the club.";

                _logger.LogWarning("The current user is not a member of the club.");
                return false;
            }

            var permissions = clubsConfig.clubPermissionDatas
                ?.FirstOrDefault(x => x.clubRanksType == memberData.rank)?.clubPermissionsType
                ?? ClubPermissionsTypes.None;

            var hasUpdatePermissions = clubUpdateType switch
            {
                ClubUpdateType.Icon => permissions.HasFlag(ClubPermissionsTypes.ChangeLogo),
                ClubUpdateType.Slogan => permissions.HasFlag(ClubPermissionsTypes.ChangeSlogan),
                _ => false
            };

            if (!hasUpdatePermissions)
            { 
                _logger.LogWarning($"The current user does not have permission to update {clubUpdateType}.");

                clubResponseCodeType =ClubResponseCodeType.NoPermission;
                message = $"The current user does not have permission to update {clubUpdateType}.";
                return false;
            }

            // Check if the current user has the permission to accept members
            return hasUpdatePermissions;
        }
    }
}
