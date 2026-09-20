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
    public class TryToGetClubDataModule(ILogger<TryToGetClubDataModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToGetClubDataModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToGetClubData))]
        public async Task<string> TryToGetClubData(IExecutionContext executionContext, string parametersEncryptedJson)
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

            // Create the path used in Firestore to get the club data
            var clubDataJson = await FirebaseApiHelper.GetDocumentAsync
                (firebaseIDToken, 
                firebaseRefreshToken, 
                collection: FirebaseApiHelper.clubsCollectionIdKey, 
                documentId: clubName,
                _logger);

            _logger.LogInformation("Club data JSON retrieved for clubName: {ClubName}\nJson: {ClubJson}", clubName, clubDataJson);

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubDataResponse();

            // Check if the club data exists
            var clubData = !string.IsNullOrEmpty(clubDataJson)
                ? FirestoreClubData.ParseClubData(clubDataJson)
                : null;
            if (clubData is not null)
            {
                // Get club rank and update club data with it
                var clubRank = await FirebaseApiHelper.GetClubRankAsync(
                    firebaseIDToken,
                    firebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    targetClubId: clubName,
                    _logger);

                if (clubRank.HasValue)
                { 
                    _logger.LogInformation($"Club '{clubName}' rank: {clubRank.Value}");
                    clubData.clubRank = clubRank.Value;

                    // Fill the response
                    clubResponse.playerClubData = clubData;
                    clubResponse.clubResponseCodeType = Enums.ClubResponseCodeType.Success;
                }
                else
                { 
                    _logger.LogWarning($"Club '{clubName}' not found in ranking.");

                    // Fill the response
                    clubResponse.playerClubData = null;
                    clubResponse.clubResponseCodeType = Enums.ClubResponseCodeType.ClubNotFound;
                    clubResponse.message = "Club not found in ranking.";
                }
            }
            else
            { 
                _logger.LogWarning("Club data not found for clubName: {ClubName}", clubName);

                // Fill the response
                clubResponse.playerClubData = null;
                clubResponse.clubResponseCodeType = Enums.ClubResponseCodeType.ClubNotFound;
                clubResponse.message = "Club not found.";
            }

            // Derived key/iv for encrypting response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            // Build response
            var clubDataResponseJson = JsonConvert.SerializeObject(clubResponse);
            _logger.LogInformation("Club data found for clubName: {ClubName}\nJson: {ClubJson}", clubName, clubDataResponseJson);

            var encryptedResponse = SecurityHelper.EncryptData(clubDataResponseJson, derivedKey, derivedIv);
            return JsonConvert.SerializeObject(encryptedResponse);
        }
    }
}
