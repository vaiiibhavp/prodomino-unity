using HelperSharedLibrary;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using static Backend.UGSBackend;

namespace Backend.IAPSystem;

public class TryToGetPurchasesDataModule(ILogger<TryToGetPurchasesDataModule> logger, IGameApiClient gameApiClient)
{
    private readonly ILogger<TryToGetPurchasesDataModule> _logger = logger;
    private readonly IGameApiClient _gameApiClient = gameApiClient;

    [CloudCodeFunction(nameof(TryToGetPurchasesData))]
    public async Task<string> TryToGetPurchasesData(IExecutionContext executionContext)
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

        // Create an empty response to be filled later
        var purchasesResponse = new PurchaseDataResponse();

        // Load current player data
        var purchasesData = await FirebaseApiHelper.GetPlayerPurchaseDataAsync
            (executionContext, _gameApiClient,
            unityUserId: executionContext.PlayerId!,
            _logger);

        if (purchasesData is not null)
        {
            _logger.LogInformation("Purchases data retrieved\nData: {PurchasesData}", JsonConvert.SerializeObject(purchasesData));

            // Fill the response
            purchasesResponse.purchaseData = purchasesData;
        }
        else
        { 
            _logger.LogWarning("Purchases data not found for player with Id: {PlayerId}", playerId);

            // Fill the response
            purchasesResponse.message = "Player purchases were not found.";
        }

        // Derived key/iv for encrypting response
        var accessToken = executionContext.AccessToken ?? string.Empty;
        var derivedKey = SecurityHelper.DeriveKey(playerId, accessToken);
        var derivedIv = SecurityHelper.DeriveIV(playerId, accessToken);

        // Build response
        var purchasesDataResponseJson = JsonConvert.SerializeObject(purchasesResponse);
        var encryptedResponse = SecurityHelper.EncryptData(purchasesDataResponseJson, derivedKey, derivedIv);
        return JsonConvert.SerializeObject(encryptedResponse);
    }
}
