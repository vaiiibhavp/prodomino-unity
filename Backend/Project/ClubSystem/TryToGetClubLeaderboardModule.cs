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
    public class TryToGetClubLeaderboardModule(ILogger<TryToGetClubLeaderboardModule> logger, IGameApiClient gameApiClient)
    {
        private readonly ILogger<TryToGetClubLeaderboardModule> _logger = logger;
        private readonly IGameApiClient _gameApiClient = gameApiClient;

        [CloudCodeFunction(nameof(TryToGetClubLeaderboard))]
        public async Task<string> TryToGetClubLeaderboard(IExecutionContext executionContext)
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

            // No specific parameters needed for leaderboard (optional filters could be added later)
            var firebaseIDKey = CloudSaveProperties.FirebaseID.ToString();
            var firebaseIDTokenKey = CloudSaveProperties.FirebaseIDToken.ToString();
            var firebaseRefreshTokenKey = CloudSaveProperties.FirebaseRefreshToken.ToString();

            // Load player data (Firebase credentials)
            var playerDataResponse = await UGSApiHelper.ProtectedLoadData(
                _gameApiClient, executionContext,
                customID: null,
                alternativePlayerID: null,
                isThrowingException: false,
                firebaseIDKey,
                firebaseIDTokenKey,
                firebaseRefreshTokenKey);

            // Extract Firebase credentials
            var firebaseID = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDKey)?.value?.ToObject<string>();
            var firebaseIDToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseIDTokenKey)?.value?.ToObject<string>();
            var firebaseRefreshToken = playerDataResponse?.FirstOrDefault(x => x?.key == firebaseRefreshTokenKey)?.value?.ToObject<string>();

            // Create the response object
            var leaderboardResponse = new FirestoreTopClubsResponse();

            if (string.IsNullOrEmpty(firebaseID) ||
                string.IsNullOrEmpty(firebaseIDToken) ||
                string.IsNullOrEmpty(firebaseRefreshToken))
            {
                _logger.LogWarning("Missing or invalid Firebase user credentials.");

                leaderboardResponse.clubResponseCodeType = ClubResponseCodeType.InvalidConfiguration;
                leaderboardResponse.message = "Missing or invalid user credentials. Please log in again.";
            } 
            else
            {
                _logger.LogInformation($"User {firebaseID} is requesting the top clubs leaderboard.");

                // Call Firestore API to get top 10 clubs ordered by score
                var topClubs = await FirebaseApiHelper.GetTopClubsByScoreAsync(
                    idToken: firebaseIDToken,
                    refreshToken: firebaseRefreshToken,
                    collection: FirebaseApiHelper.clubsCollectionIdKey,
                    limit: 10,
                    logger: _logger);

                if (topClubs is null or { Count: 0 })
                {
                    _logger.LogWarning("No clubs found for leaderboard ranking.");

                    leaderboardResponse.clubResponseCodeType = ClubResponseCodeType.ClubNotFound;
                    leaderboardResponse.message = "No clubs found for leaderboard ranking.";
                } 
                else
                {
                    _logger.LogInformation($"Successfully retrieved top {topClubs.Count} clubs by score.");

                    leaderboardResponse.clubResponseCodeType = ClubResponseCodeType.Success;
                    leaderboardResponse.leaderboard = topClubs;
                }
            }

            // Encrypt and return response
            var accessToken = executionContext.AccessToken ?? string.Empty;
            var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
            var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

            var responseJson = JsonConvert.SerializeObject(leaderboardResponse);
            var encryptedResponse = SecurityHelper.EncryptData(responseJson, derivedKey, derivedIv);

            return JsonConvert.SerializeObject(encryptedResponse);
        }
    }
}
