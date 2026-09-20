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

namespace Backend.ClubSystem
{
    public class TryToSearchClubsByNameModule(ILogger<TryToSearchClubsByNameModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToSearchClubsByNameModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToSearchClubsByName))]
        public async Task<string> TryToSearchClubsByName(IExecutionContext executionContext, string parametersEncryptedJson)
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

            if (!data.TryGetValue("partialName", out var partialNameToken) || partialNameToken is not string partialName)
                throw new ArgumentException("Missing or invalid club");

            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

            // Load current player data
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null, alternativePlayerID: null, isThrowingException: false,
                firebaseIDKey,
                firebaseIDTokenKey,
                firebaseRefreshTokenKey);

            // Validate entry data
            var firebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var firebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var firebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

            // Create an empty response to be filled later
            var clubResponse = new FirestoreClubSearchResponse();

            // Validate essential data
            if (string.IsNullOrEmpty(firebaseID) ||
                string.IsNullOrEmpty(firebaseIDToken) ||
                string.IsNullOrEmpty(firebaseRefreshToken))
            {
                _logger.LogWarning("Missing or invalid current user data. Ensure you are properly registered and part of a club.");

                // Fill the response
                clubResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                clubResponse.message = "Missing or invalid user data. Ensure you are logged in and you are in a club.";
            } 
            
            else
            {
                _logger.LogInformation($"User {firebaseID} is attempting to search clubs with name containing '{partialName}'.");

                // Fetch Firestore club document
                var clubsFound = await FirebaseApiHelper.SearchClubsByNameAsync(
                    idToken: firebaseIDToken,
                    refreshToken: firebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    searchTerm: partialName,
                    logger: _logger);

                if (clubsFound is null or { Count: 0 })
                {
                    _logger.LogWarning($"No clubs found matching the search term '{partialName}'.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    clubResponse.message = $"No clubs found matching the search term '{partialName}'.";
                } 
                else
                {
                    _logger.LogInformation($"Found {clubsFound.Count} clubs matching the search term '{partialName}'.");

                    // Fill the response
                    clubResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                    clubResponse.clubsFound = clubsFound;
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
    }
}
