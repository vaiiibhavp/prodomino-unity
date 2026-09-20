using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;
using static HelperSharedLibrary.Enums;
using static HelperSharedLibrary.ExceptionHelper;

namespace Backend.ClubSystem
{
    public class TryToSendClubChatMessageModule(ILogger<TryToSendClubChatMessageModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToSendClubChatMessageModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToSendClubChatMessage))]
        public async Task<string> TryToSendClubChatMessage(IExecutionContext executionContext, string parametersEncryptedJson)
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

            // Extract the new message
            if (!data.TryGetValue("newMessage", out var newMessageToken) || newMessageToken is not string newMessage)
                throw new ArgumentException("Missing or invalid new chat message");

            // Load global club configuration (Game Data)
            var configKey = CloudSaveProperties.Config.ToString();
            var loadGameDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
                customID, null, false, configKey);

            var configData = loadGameDataResponse?.FirstOrDefault(x => x?.key == configKey)?.value?.ToObject<ConfigData>();
            if (configData?.clubsConfig?.clubPermissionDatas is null or { Length: 0 })
                throw new UGSException("Invalid or missing Game Data configuration.");

            // Define Cloud Save keys to load Firebase and club data
            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();
            var clubNameKey = CloudSaveProperties.Club.ToString();

            // Load current player Firebase data
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(_gameApiClient, executionContext,
                null, null, false,
                firebaseIDKey, firebaseIDTokenKey, firebaseRefreshTokenKey, clubNameKey);

            // Extract data for current and target players
            var firebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var firebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var firebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();
            var clubName = playerDataResponse?.FirstOrDefault(x => x?.key == clubNameKey)?.value?.ToObject<string>();

            // Create an empty response to be filled later
            var clubChatResponse = new FirestoreClubChatResponse();

            // Validate essential data
            if (string.IsNullOrEmpty(firebaseID) ||
                string.IsNullOrEmpty(firebaseIDToken) ||
                string.IsNullOrEmpty(firebaseRefreshToken) ||
                string.IsNullOrEmpty(clubName))
            {
                _logger.LogWarning("Missing or invalid current user data. Ensure you are properly registered and part of a club.");

                // Fill the response
                clubChatResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubChatResponse.message = "Missing or invalid user data. Ensure you are logged in and you are in a club.";
            } 
            
            else
            {
                _logger.LogInformation($"User {firebaseID} is attempting to send a new club chat message.");

                // Fetch Firestore club document
                var clubDataJson = await FirebaseApiHelper.GetDocumentAsync(
                    firebaseIDToken,
                    firebaseRefreshToken,
                    FirebaseApiHelper.clubsCollectionIdKey,
                    clubName,
                    _logger);

                var clubData = !string.IsNullOrEmpty(clubDataJson)
                    ? FirestoreClubData.ParseClubData(clubDataJson)
                    : null;

                if (clubData is null)
                {
                    _logger.LogWarning($"Club '{clubName}' not found in Firestore.");

                    // Fill the response
                    clubChatResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    clubChatResponse.message = "Club not found.";
                } 
                
                else
                {
                    // Log the club data load success
                    _logger.LogInformation($"Club data for '{clubName}' loaded successfully.");

                    // Validate the user is part of the club
                    var memberData = clubData.members.FirstOrDefault(x => x.unityMemberId == executionContext.PlayerId);
                    if (memberData is not null)
                    {
                        // Fetch Firestore club document
                        var clubChatDataJson = await FirebaseApiHelper.GetDocumentAsync(
                            firebaseIDToken,
                            firebaseRefreshToken,
                            FirebaseApiHelper.clubsChatsCollectionIdKey,
                            clubName,
                            _logger);

                        var clubChatData = !string.IsNullOrEmpty(clubChatDataJson)
                            ? FirestoreClubChatData.ParseChatData(clubChatDataJson)
                            : null;

                        // Check if the club already has data of its chats; else, create one
                        var isCreatingClubChatData = clubChatData is null;
                        if (isCreatingClubChatData)
                        {
                            clubChatData = new FirestoreClubChatData(clubName);
                            _logger.LogInformation($"The FirestoreClubChatData of the club {clubName} is null. Initializating it...");
                        }
                    
                        // Else, just log the club chat data load success
                        else
                            _logger.LogInformation($"Club chat data for '{clubName}' loaded successfully.");

                        // Increase the count of messages sent
                        clubChatData!.historyCount++;

                        // Register the new message in the records
                        clubChatData.messages.Add(new
                            (FirestoreClubChatData.GenerateMessageId(clubChatData.historyCount),
                            memberData.unityMemberId, 
                            memberData.memberName, 
                            memberData.profileIconId,
                            newMessage));

                        clubChatData.updatedAt = DateTime.UtcNow;

                        // Make sure the messages don't exceeed its limits
                        var limit = configData.clubsConfig?.chatMessageLimit ?? 100;
                        if (clubChatData.messages.Count > limit)
                            clubChatData.messages.RemoveRange(0, clubChatData.messages.Count - limit);

                        // Build Firestore update payload (replaces entire array)
                        var updatedFields = new
                        {
                            // Last message preview text
                            lastMessage = new { stringValue = newMessage ?? clubChatData.lastMessage ?? string.Empty },

                            // Timestamp (Unix milliseconds)
                            updatedAt = new { timestampValue = DateTime.UtcNow.ToString("o") },

                            // The last record of the messages sent and received
                            messages = FirebaseApiHelper.BuildFirestoreClubChatArray(clubChatData.messages.ToArray()),
                        };

                        var updateResponse = string.Empty;

                        // Send create request to Firestore if the club chat data doesn't exist
                        if (isCreatingClubChatData)
                        {
                            _logger.LogInformation("Creating new club chat document...");
                            updateResponse = await FirebaseApiHelper.CreateDocumentAsync(
                                executionContext,
                                _gameApiClient,
                                collection: FirebaseApiHelper.clubsChatsCollectionIdKey,
                                documentFields: updatedFields,
                                documentId: clubName,
                                logger: _logger
                            );
                        }

                        // But, if the data exists, just patch it
                        else
                        {
                            _logger.LogInformation("Updating the club chat document...");
                            updateResponse = await FirebaseApiHelper.PatchDocumentAsync(
                                executionContext,
                                _gameApiClient,
                                FirebaseApiHelper.clubsChatsCollectionIdKey,
                                clubName,
                                updatedFields,
                                updateFieldPaths: ["lastMessage", "messages", "updatedAt"]
                            );
                        }

                        if (!string.IsNullOrEmpty(updateResponse))
                        {
                            _logger.LogInformation($"Successfully send message in '{clubName}'.");

                            // Fill the response
                            clubChatResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                            clubChatResponse.clubChatData = clubChatData;
                        } 
                        else
                        {
                            _logger.LogError($"Failed to send message in '{clubName}'.");

                            clubChatResponse.clubResponseCodeType = ClubResponseCodeType.UnknownIssue;
                            clubChatResponse.message = "Failed to send club chat message due an unknown issue.";
                        }
                    } 
                    
                    else
                    {
                        _logger.LogWarning($"Target member {firebaseID} not found in club data.");

                        clubChatResponse.clubResponseCodeType = ClubResponseCodeType.NotInClub;
                        clubChatResponse.message = "The target member is not found in the club.";
                    }
                }
            }

            // Derived key/iv for encrypting response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            // Build response
            var clubDataResponseJson = JsonConvert.SerializeObject(clubChatResponse);
            var encryptedResponse = SecurityHelper.EncryptData(clubDataResponseJson, derivedKey, derivedIv);
            return JsonConvert.SerializeObject(encryptedResponse);
        }
    }
}
