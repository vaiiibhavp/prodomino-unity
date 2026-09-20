using Backend.AnalyticsSystem;
using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.ClubSystem
{
    public class TryToGetClubChatDataModule(ILogger<TryToGetClubChatDataModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToGetClubChatDataModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToGetClubChatData))]
        public async Task<string> TryToGetClubChatData(IExecutionContext executionContext, string parametersEncryptedJson)
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

            if (!data.TryGetValue("clubName", out var clubNameToken) || clubNameToken is not string clubName)
                throw new ArgumentException("Missing or invalid club");

            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

            // Load current player data
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null, 
                alternativePlayerID: null, 
                isThrowingException: false,
                firebaseIDTokenKey,
                firebaseRefreshTokenKey);

            // Validate entry data
            var firebaseIDTokenObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var firebaseRefreshTokenObj = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

            if (firebaseIDTokenObj is not string firebaseIDToken || string.IsNullOrEmpty(firebaseIDToken)
                || firebaseRefreshTokenObj is not string firebaseRefreshToken || string.IsNullOrEmpty(firebaseRefreshToken))
                throw new ArgumentException("Firebase ID token or refresh token is missing.");

            // Create the path used in Firestore to get the club chat data
            var clubChatDataJson = await FirebaseApiHelper.GetDocumentAsync
                (firebaseIDToken, 
                firebaseRefreshToken, 
                collection: FirebaseApiHelper.clubsChatsCollectionIdKey, 
                documentId: clubName,
                _logger);

            _logger.LogInformation("Club chat data JSON retrieved for clubName: {ClubName}\nJson: {ClubJson}", clubName, clubChatDataJson);

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubChatResponse();

            // Check if the club chat data exists
            var clubChatData = !string.IsNullOrEmpty(clubChatDataJson)
                ? FirestoreClubChatData.ParseChatData(clubChatDataJson)
                : null;
            if (clubChatData is not null)
            {
                // Fill the response
                clubResponse.clubChatData = clubChatData;
                clubResponse.clubResponseCodeType = Enums.ClubResponseCodeType.Success;
            }
            else
            { 
                _logger.LogWarning("Club chat data not found for clubName: {ClubName}", clubName);

                // Fill the response
                clubResponse.clubResponseCodeType = Enums.ClubResponseCodeType.ClubNotFound;
                clubResponse.message = "Club not found.";
            }

            // Derived key/iv for encrypting response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            // Build response
            var clubChatDataResponseJson = JsonConvert.SerializeObject(clubResponse);
            _logger.LogInformation("Club chat data found for clubName: {ClubName}\nJson: {ClubJson}", clubName, clubChatDataResponseJson);

            var encryptedResponse = SecurityHelper.EncryptData(clubChatDataResponseJson, derivedKey, derivedIv);
            return JsonConvert.SerializeObject(encryptedResponse);
        }
    }
}
